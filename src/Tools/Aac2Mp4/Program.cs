// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Aac2Mp4/Aac2Mp4.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - AP4_Debug() is not compiled out upstream (it prints on stdout), so the
//   per-frame "AAC frame [...]" lines are part of the tool's output. They go
//   through one shared "-stdout" FileByteStream so that the bytes written are
//   exactly those of upstream ('\n' line endings, no encoding translation).

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Aac2Mp4
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "AAC to MP4 Converter - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2008 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: AP4_Debug / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static ByteStream s_Stdout;

        // AP4_Debug (prints on stdout, truncated to the 1024 byte buffer of upstream)
        private static void Debug(string text)
        {
            if (text.Length > Ap4.DEBUG_MAX_BUFFER - 1) {
                text = text.Substring(0, Ap4.DEBUG_MAX_BUFFER - 1);
            }
            if (s_Stdout == null) {
                FileByteStream.Create("-stdout", FileByteStream.Mode.STREAM_MODE_WRITE, out s_Stdout);
            }
            if (s_Stdout != null) s_Stdout.WriteString(text);
        }

        private static void FlushStdout()
        {
            if (s_Stdout is IDisposable) ((IDisposable)s_Stdout).Dispose();
            s_Stdout = null;
        }

        private static void Eprintf(string text)
        {
            Console.Error.Write(text);
        }

        private static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(uint value) { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value
        private static string D06(uint value) // %06d of an unsigned value
        {
            int v = unchecked((int)value);
            return v < 0 ? "-" + (-(long)v).ToString("D5", CultureInfo.InvariantCulture)
                         : v.ToString("D6", CultureInfo.InvariantCulture);
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: aac2mp4 <input> <output>\n");
            return 1;
        }

        /*----------------------------------------------------------------------
        |   MakeDsi
        +---------------------------------------------------------------------*/
        private static void MakeDsi(uint sampling_frequency_index, uint channel_configuration, byte[] dsi)
        {
            uint object_type = 2; // AAC LC by default
            dsi[0] = unchecked((byte)((object_type<<3) | (sampling_frequency_index>>1)));
            dsi[1] = unchecked((byte)(((sampling_frequency_index&1)<<7) | (channel_configuration<<3)));
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            int exit_code = Run(args);
            FlushStdout();
            return exit_code;
        }

        private static int Run(string[] args)
        {
            Ap4Result result;

            if (args.Length < 2) {
                return PrintUsageAndExit();
            }

            // open the input
            ByteStream  input = null;
            result = FileByteStream.Create(args[0], FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Debug("ERROR: cannot open input (" + args[0] + ") " + D((int)result) + "\n");
                return 1;
            }

            // open the output
            ByteStream  output = null;
            result = FileByteStream.Create(args[1], FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Debug("ERROR: cannot open output (" + args[1] + ") " + D((int)result) + "\n");
                return 1;
            }

            // create a sample table
            SyntheticSampleTable sample_table = new SyntheticSampleTable();

            // create an ADTS parser
            AdtsParser parser = new AdtsParser();
            bool       initialized = false;
            uint       sample_description_index = 0;

            // read from the input, feed, and get AAC frames
            uint sample_rate = 0;
            uint sample_count = 0;
            bool eos = false;
            byte[] input_buffer = new byte[4096];
            for(;;) {
                // try to get a frame
                AacFrame frame = new AacFrame();
                result = parser.FindFrame(frame);
                if (result.Succeeded()) {
                    Debug("AAC frame [" + D06(sample_count) + "]: size = " + D(frame._info._frameLength) +
                          ", " + D(frame._info._samplingFrequency) + " kHz, " +
                          D(frame._info._channelConfiguration) + " ch\n");
                    if (!initialized) {
                        initialized = true;

                        // create a sample description for our samples
                        DataBuffer dsi = new DataBuffer();
                        byte[] aac_dsi = new byte[2];
                        MakeDsi(frame._info._samplingFrequencyIndex, frame._info._channelConfiguration, aac_dsi);
                        dsi.SetData(aac_dsi, 2);
                        MpegAudioSampleDescription sample_description =
                            new MpegAudioSampleDescription(
                            Ap4.OTI_MPEG4_AUDIO,   // object type
                            frame._info._samplingFrequency,
                            16,                    // sample size
                            unchecked((ushort)frame._info._channelConfiguration),
                            dsi,                   // decoder info
                            6144,                  // buffer size
                            128000,                // max bitrate
                            128000);               // average bitrate
                        sample_description_index = sample_table.GetSampleDescriptionCount();
                        sample_table.AddSampleDescription(sample_description);
                        sample_rate = frame._info._samplingFrequency;
                    }

                    MemoryByteStream sample_data = new MemoryByteStream(frame._info._frameLength);
                    frame._source.ReadBytes(sample_data.UseData(), frame._info._frameLength);
                    sample_table.AddSample(sample_data, 0, frame._info._frameLength, 1024, sample_description_index, 0, 0, true);
                    sample_count++;
                } else if (!eos) {
                    // read some data and feed the parser
                    uint to_read = parser.GetBytesFree();
                    if (to_read != 0) {
                        uint bytes_read = 0;
                        if (to_read > (uint)input_buffer.Length) to_read = (uint)input_buffer.Length;
                        result = input.ReadPartial(input_buffer, to_read, out bytes_read);
                        if (result.Succeeded()) {
                            uint to_feed = bytes_read;
                            result = parser.Feed(input_buffer, ref to_feed);
                            if (result.Failed()) {
                                Debug("ERROR: parser.Feed() failed (" + D((int)result) + ")\n");
                                return 1;
                            }
                        } else {
                            if (result == Ap4Result.ERROR_EOS) {
                                eos = true;
                                // PORT NOTE: upstream calls parser.Feed(NULL, 0, true):
                                // NULL buffer, NULL size pointer, flags = 1 (AP4_BITSTREAM_FLAG_EOS)
                                uint no_size = 0;
                                parser.Feed(null, ref no_size, 1);
                            }
                        }
                    }
                } else {
                    break;
                }
            }

            // create a movie
            Movie movie = new Movie();

            // create an audio track
            Track track = new Track(Track.Type.TYPE_AUDIO,
                                    sample_table,
                                    0,     // track id
                                    sample_rate,       // movie time scale
                                    sample_count*1024, // track duration
                                    sample_rate,       // media time scale
                                    sample_count*1024, // media duration
                                    "eng", // language
                                    0, 0); // width, height

            // add the track to the movie
            movie.AddTrack(track);

            // create a multimedia file
            File file = new File(movie);

            // set the file type
            uint[] compatible_brands = new uint[2] {
                Ap4.FILE_BRAND_ISOM,
                Ap4.FILE_BRAND_MP42
            };
            file.SetFileType(Ap4.FILE_BRAND_M4A_, 0, compatible_brands, 2);

            // write the file to the output
            FileWriter.Write(file, output);

            if (input is IDisposable) ((IDisposable)input).Dispose();
            if (output is IDisposable) ((IDisposable)output).Dispose();

            return 0;
        }
    }
}
