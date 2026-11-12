// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4AudioClip/Mp4AudioClip.cpp
// Original work Copyright 2002-2013 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4AudioClip
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Audio Clip Extractor - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2013 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: fprintf(stderr) replacement)
        +---------------------------------------------------------------------*/
        // The bytes are written as they are: Console.Error would re-encode the
        // text (file names given on the command line) with the console code page.
        private static System.IO.Stream  s_Stderr;

        private static void Eprintf(string text)
        {
            if (s_Stderr == null) {
                s_Stderr = Console.OpenStandardError();
            }
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stderr.Write(bytes, 0, bytes.Length);
        }

        private static string D(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\n" +
                    "usage: mp4audioclip [options] <input> <output>\n" +
                    "Options:\n" +
                    "  --start <time>         : start time in milliseconds (default = 0)\n" +
                    "  --duration <duration>  : clip duration in milliseconds (default = until end)\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] argv)
        {
            int argc = argv.Length+1;
            if (argc == 1) return PrintUsageAndExit();

            // parse options
            string    input_filename  = null;
            string    output_filename = null;
            uint      start_time_ms   = 0;
            uint      duration_ms     = 0;
            Ap4Result result = Ap4Result.SUCCESS; // PORT NOTE: declared without a value upstream; assigned before any use

            // parse the command line arguments
            // PORT NOTE: `*++argv` walks the NULL-terminated argv upstream
            int     argi = 0;
            string  arg;
            while ((arg = (argi < argv.Length ? argv[argi++] : null)) != null) {
                if (arg == "--start") {
                    arg = (argi < argv.Length ? argv[argi++] : null);
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --start option\n");
                        return 1;
                    }
                    start_time_ms = Ap4Utils.ParseIntegerU(arg);
                } else if (arg == "--duration") {
                    arg = (argi < argv.Length ? argv[argi++] : null);
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --duration option\n");
                        return 1;
                    }
                    duration_ms = Ap4Utils.ParseIntegerU(arg);
                } else if (input_filename == null) {
                    input_filename = arg;
                } else if (output_filename == null) {
                    output_filename = arg;
                } else {
                    Eprintf("ERROR: unexpected argument (" + arg + ")\n");
                    return 1;
                }
            }

            // check arguments
            if (input_filename == null) {
                Eprintf("ERROR: missing input file name argument\n");
                return 1;
            }
            if (output_filename == null) {
                Eprintf("ERROR: missing output file name argument\n");
                return 1;
            }

            // create the input stream
            ByteStream input = null;
            result = FileByteStream.Create(input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file (" + input_filename + ")\n");
                return 1;
            }

            // parse the input
            File input_file = new File(input);
            Movie input_movie = input_file.GetMovie();
            if (input_movie == null) {
                Eprintf("ERROR: cannot parse input (" + D((int)result) + ")\n");
                return 1;
            }

            // find the first audio track
            Track input_track = input_movie.GetTrack(Track.Type.TYPE_AUDIO);
            if (input_track == null) {
                Eprintf("ERROR: no audio track found\n");
                return 1;
            }

            // create a synthetic sample table to hold the output samples
            SyntheticSampleTable sample_table = new SyntheticSampleTable();

            // use the same sample description(s) in the output as what's in the input
            // (in almost all cases, there will only be one sample description)
            // NOTE: we don't transfer the ownership of the object
            for (uint i=0; i<input_track.GetSampleDescriptionCount(); i++) {
                SampleDescription sample_description = input_track.GetSampleDescription(i);
                if (sample_description == null) {
                    Eprintf("ERROR: invalid/unsupported sample description\n");
                    return 1;
                }
                sample_table.AddSampleDescription(sample_description, false);
            }

            // the first output timestamp is 0
            ulong dts = 0;

            // compute the maximum timestamp based on the duration
            ulong max_dts;
            if (duration_ms == 0) {
                // go until the end
                max_dts = input_track.GetDuration();
            } else {
                max_dts = Ap4Utils.ConvertTime(duration_ms, 1000, input_track.GetMediaTimeScale());
            }

            // compute the first sample index
            uint start_sample = 0;
            if (input_track.GetSampleIndexForTimeStampMs(start_time_ms, out start_sample).Failed()) {
                Eprintf("ERROR: start time out of range\n");
                return 1;
            }

            // add all the input samples to the sample table
            for (uint i=start_sample; i<input_track.GetSampleCount(); i++) {
                Sample sample = new Sample();
                if (input_track.GetSample(i, sample).Failed()) break;

                // obtain the sample data stream in a variable so that we can release the reference
                ByteStream sample_stream = sample.GetDataStream();

                // add a sample to the sample table
                sample_table.AddSample(sample_stream,
                                       sample.GetOffset(),
                                       sample.GetSize(),
                                       sample.GetDuration(),
                                       sample.GetDescriptionIndex(),
                                       dts,
                                       0,
                                       true);

                // update the current timestamp
                dts += sample.GetDuration();

                // stop if we've exceeded the duration
                if (dts > max_dts) break;
            }

            // compute the actual effective duration (could be less than what was asked for)
            duration_ms = unchecked((uint)Ap4Utils.ConvertTime(dts, input_track.GetMediaTimeScale(), 1000));

            // create an output track
            Track output_track =
                new Track(Track.Type.TYPE_AUDIO,
                          sample_table,
                          1,                                                                         // track id
                          input_movie.GetTimeScale(),                                                // movie time scale
                          Ap4Utils.ConvertTime(duration_ms, 1000, input_movie.GetTimeScale()),       // track duration (in the timescale of the movie)
                          input_track.GetMediaTimeScale(),                                           // media time scale
                          Ap4Utils.ConvertTime(duration_ms, 1000, input_track.GetMediaTimeScale()),  // media duration (in the timescale of the media)
                          "eng",                                                                     // language
                          0, 0);                                                                     // width, height


            // create the output movie
            Movie output_movie = new Movie(input_movie.GetTimeScale());

            // create the output file
            File output_file = new File(output_movie);

            // add the output track
            output_movie.AddTrack(output_track);

            // set the output file type
            uint[] compatible_brands = new uint[2] {
                Ap4.FILE_BRAND_ISOM,
                Ap4.FILE_BRAND_MP42
            };
            output_file.SetFileType(Ap4.FILE_BRAND_M4A_, 0, compatible_brands, 2);

            // create the output stream
            ByteStream output = null;
            result = FileByteStream.Create(output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file (" + output_filename + ")\n");
                return 1;
            }

            // write the output file
            FileWriter.Write(output_file, output);

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();
            if (output is IDisposable) ((IDisposable)output).Dispose();

            return 0;
        }
    }
}
