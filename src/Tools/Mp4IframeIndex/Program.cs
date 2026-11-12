// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4IframeIndex/Mp4IframeIndex.cpp
// Original work Copyright 2002-2017 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4IframeIndex
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Iframe Index - Version 1.0.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2017 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static void Eprintf(string text)
        {
            Console.Error.Write(text);
        }

        private static string D(int value)   { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(uint value)  { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value
        private static string U(uint value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string U(ulong value) { return value.ToString(CultureInfo.InvariantCulture); }

        // (unsigned int)strtoul(str, NULL, 10)
        // PORT NOTE: `unsigned long` is 32 bits on the reference platforms
        // (Windows, wasm32): out of range values saturate to ULONG_MAX.
        private static uint Strtoul(string str)
        {
            int i = 0;
            while (i < str.Length && (str[i] == ' ' || (str[i] >= '\t' && str[i] <= '\r'))) ++i;
            bool negative = false;
            if (i < str.Length && (str[i] == '+' || str[i] == '-')) {
                negative = str[i] == '-';
                ++i;
            }
            ulong value = 0;
            bool overflow = false;
            while (i < str.Length && str[i] >= '0' && str[i] <= '9') {
                value = value*10+(ulong)(str[i]-'0');
                if (value > uint.MaxValue) {
                    overflow = true;
                    value = uint.MaxValue;
                }
                ++i;
            }
            if (overflow) return uint.MaxValue;
            return negative ? unchecked((uint)(-(long)value)) : (uint)value;
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp4iframeindex [options] <input> [<output>]\n" +
                    "  options:\n" +
                    "    --track <id>: ID of the video track\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   IndexTrack
        +---------------------------------------------------------------------*/
        private static Ap4Result IndexTrack(Track track, ref string separator, ByteStream output)
        {
            Sample sample = new Sample();
            for (uint i=0; i<track.GetSampleCount(); i++) {
                // get the sample
                Ap4Result result = track.GetSample(i, sample);
                if (result.Failed()) return result;

                if (sample.IsSync()) {
                    ulong offset = sample.GetOffset();
                    string workspace = separator +
                                       "{ \"size\": " + U(sample.GetSize()) +
                                       ", \"offset\": " + U(offset) +
                                       ", \"fragmentStart\": 0 }";
                    separator = ",\n";
                    output.WriteString(workspace);
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   IndexFragments
        +---------------------------------------------------------------------*/
        private static Ap4Result IndexFragments(Movie movie, uint track_id, ByteStream stream, ref string separator, ByteStream output)
        {
            stream.Seek(0);
            LinearReader reader = new LinearReader(movie, stream);
            reader.EnableTrack(track_id);

            Sample sample = new Sample();
            ulong last_fragment_position = unchecked((ulong)(-1));
            for (uint i=0; ; i++) {
                uint found_track_id = 0;
                Ap4Result result = reader.GetNextSample(sample, out found_track_id);
                if (result.Failed()) break;

                // only output the first sync sample of each fragment
                if (reader.GetCurrentFragmentPosition() == last_fragment_position) {
                    continue;
                }
                if (found_track_id == track_id && sample.IsSync()) {
                    ulong offset = sample.GetOffset();
                    string workspace = separator +
                                       "{ \"size\": " + U(sample.GetSize()) +
                                       ", \"offset\": " + U(offset) +
                                       ", \"fragmentStart\": " + U(reader.GetCurrentFragmentPosition()) + " }";
                    separator = ",\n";
                    output.WriteString(workspace);
                }
                last_fragment_position = reader.GetCurrentFragmentPosition();
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            if (args.Length < 1) {
                return PrintUsageAndExit();
            }
            string input_filename  = null;
            string output_filename = null;
            string  moov_filename   = null;
            uint    track_id = 0;

            int argi = 0;
            while (argi < args.Length) {
                string arg = args[argi++];
                if (arg == "--track") {
                    if (argi >= args.Length) {
                        Eprintf("ERROR: missing argument after --track option\n");
                        return 1;
                    }
                    arg = args[argi++];
                    track_id = Strtoul(arg);
                } else if (arg == "--fragments-info") {
                    if (argi >= args.Length) {
                        Eprintf("ERROR: missing argument after --fragments-info option\n");
                        // PORT NOTE: upstream does not return here and keeps reading
                        // past the end of argv (undefined behaviour); the port stops
                        // parsing the command line.
                        break;
                    }
                    moov_filename = args[argi++];
                } else if (input_filename == null) {
                    input_filename = arg;
                } else if (output_filename == null) {
                    output_filename = arg;
                } else {
                    Eprintf("ERROR: unexpected argument '" + arg + "'\n");
                    return 1;
                }
            }
            if (input_filename == null) {
                Eprintf("ERROR: input filename missing\n");
                return 1;
            }
            if (output_filename == null) {
                output_filename = "-stdout";
            }

            ByteStream output = null;
            Ap4Result result = FileByteStream.Create(output_filename,
                                                     FileByteStream.Mode.STREAM_MODE_WRITE,
                                                     out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file " + input_filename + " (" + D((int)result) + ")\n");
                return 1;
            }

            ByteStream input = null;
            result = FileByteStream.Create(input_filename,
                                           FileByteStream.Mode.STREAM_MODE_READ,
                                           out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file " + input_filename + " (" + D((int)result) + ")\n");
                if (output is IDisposable) ((IDisposable)output).Dispose(); // PORT NOTE: closed by exit() upstream
                return 1;
            }

            ByteStream moov = null;
            if (moov_filename != null) {
                result = FileByteStream.Create(moov_filename,
                                               FileByteStream.Mode.STREAM_MODE_READ,
                                               out moov);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot open fragments info file " + moov_filename + " (" + D((int)result) + ")\n");
                    if (output is IDisposable) ((IDisposable)output).Dispose(); // PORT NOTE: closed by exit() upstream
                    if (input is IDisposable) ((IDisposable)input).Dispose();
                    return 1;
                }
            }

            File input_file = new File(input, true);
            Movie input_movie = input_file.GetMovie();
            File moov_file = moov != null ? new File(moov, true) : null;
            Track  track = null;
            string separator = "";

            if (input_movie == null) {
                // we need a movie to get to the fragments
                if (moov_file == null) {
                    Eprintf("ERROR: input file does not contain a movie (use --fragments-info option)\n");
                    goto end;
                }
                input_movie = moov_file.GetMovie();
            }

            // select the track
            if (track_id == 0) {
                track = input_movie.GetTrack(Track.Type.TYPE_VIDEO);
                if (track != null) {
                    track_id = track.GetId();
                }
            } else {
                track = input_movie.GetTrack(track_id);
            }

            // check that we found the track
            if (track == null) {
                Eprintf("ERROR: video track not found\n");
                goto end;
            }

            // check the track type
            if (track.GetType() != Track.Type.TYPE_VIDEO) {
                Eprintf("ERROR: track ID " + D(track.GetId()) + " is not a video track\n");
                goto end;
            }

            // start
            output.WriteString("[\n");

            // index the track
            result = IndexTrack(track, ref separator, output);
            if (result.Failed()) {
                Eprintf("ERROR: failed to index track (" + D((int)result) + ")\n");
            }

            // index the fragments
            result = IndexFragments(input_movie, track_id, input, ref separator, output);
            if (result.Failed()) {
                Eprintf("ERROR: failed to index fragments (" + D((int)result) + ")\n");
            }

            // end
            output.WriteString("\n]\n");

        end:
            if (moov is IDisposable) ((IDisposable)moov).Dispose();
            if (input is IDisposable) ((IDisposable)input).Dispose();
            if (output is IDisposable) ((IDisposable)output).Dispose();

            return result == Ap4Result.SUCCESS ? 0 : 1;
        }
    }
}
