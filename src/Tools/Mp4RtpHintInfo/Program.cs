// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4RtpHintInfo/Mp4RtpHintInfo.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4RtpHintInfo
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Rtp Hint Info - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2008 Axiomatic Systems, LLC";

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

        private static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(uint value) { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value

        // atoi(str)
        private static int Atoi(string str)
        {
            int i = 0;
            while (i < str.Length && (str[i] == ' ' || (str[i] >= '\t' && str[i] <= '\r'))) ++i;
            bool negative = false;
            if (i < str.Length && (str[i] == '+' || str[i] == '-')) {
                negative = str[i] == '-';
                ++i;
            }
            int value = 0;
            while (i < str.Length && str[i] >= '0' && str[i] <= '9') {
                value = unchecked(value*10+(str[i]-'0'));
                ++i;
            }
            return negative ? unchecked(-value) : value;
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp4rtphintinfo [--trackid <hinttrackid>] <input>\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   DumpRtpPackets
        +---------------------------------------------------------------------*/
        private static Ap4Result DumpRtpPackets(HintTrackReader reader, string file_name)
        {
            // create the output stream
            Ap4Result result;
            ByteStream output = null;
            result = FileByteStream.Create(file_name, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file (" + file_name + ")\n");
                return Ap4Result.FAILURE;
            }

            // read the packets from the reader and write them in the output stream
            DataBuffer data = new DataBuffer(1500);
            uint ts_ms;
            while (reader.GetNextPacket(data, out ts_ms).Succeeded()) {
                output.Write(data.GetData(), data.GetDataSize());
                Ap4.Debug("#########\n\tpacket contains " + D(data.GetDataSize()) + " bytes\n");
                Ap4.Debug("\tsent at time stamp " + D(ts_ms) + "\n\n");
            }

            if (output is IDisposable) ((IDisposable)output).Dispose();

            return Ap4Result.SUCCESS;
        }


        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] argv)
        {
            Ap4Result result = Ap4Result.SUCCESS;
            int argc = argv.Length+1;

            // parse the command line
            if (argc != 4) return PrintUsageAndExit();

            // parse options
            string input_filename = null;
            uint hint_track_id = 0;

            // PORT NOTE: `*++argv` walks the NULL-terminated argv upstream
            int     argi = 0;
            string  arg;
            while ((arg = (argi < argv.Length ? argv[argi++] : null)) != null) {
                if (arg == "--trackid") {
                    arg = (argi < argv.Length ? argv[argi++] : null);
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --trackid option\n");
                        return PrintUsageAndExit();
                    }
                    hint_track_id = unchecked((uint)Atoi(arg));
                } else if (input_filename == null) {
                    input_filename = arg;
                } else {
                    Eprintf("ERROR: unexpected argument (" + arg + ")\n");
                    return 1;
                }
            }
            // create the input stream
            ByteStream input = null;
            // PORT NOTE: with exactly 3 arguments input_filename cannot be NULL here
            result = FileByteStream.Create(input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                // PORT NOTE: upstream prints argv[1] here, but argv has been advanced
                // to its terminating NULL by the loop above, so this reads one entry
                // past the end of the argument vector (undefined behaviour); the
                // reference build prints "(null)".
                Eprintf("ERROR: cannot open input file ((null))\n");
                return PrintUsageAndExit();
            }

            File file = new File(input);
            Movie movie = file.GetMovie();
            if (movie != null) {
                // get a hint track reader
                Track  hint_track = movie.GetTrack(hint_track_id);
                if (hint_track == null) {
                    Eprintf("ERROR: No track for track id (" + D(hint_track_id) + ")\n");
                    return (int)Ap4Result.FAILURE;
                }
                if (hint_track.GetType() != Track.Type.TYPE_HINT) {
                    Eprintf("track of id (" + D(hint_track_id) + ") is not of type 'hint'\n");
                    return (int)Ap4Result.FAILURE;
                }
                HintTrackReader reader = null;
                result = HintTrackReader.Create(hint_track, movie, 0x01020304, out reader);
                if (result.Failed()) return (int)result;

                // PORT NOTE: AP4_FormatString into a 256 byte buffer: the name is
                // truncated to 255 bytes upstream.
                string rtp_file_name = input_filename + "-track-" + D(hint_track_id) + ".rtp";
                byte[] rtp_file_name_bytes = Ap4Utils.StringToBytes(rtp_file_name);
                if (rtp_file_name_bytes.Length > 255) rtp_file_name = Ap4Utils.StringFromBytes(rtp_file_name_bytes, 0, 255);

                // display the sdp
                string sdp;
                reader.GetSdpText(out sdp);
                Ap4.Debug("sdp:\n" + sdp + "\n\n");

                // dump the packet
                result = DumpRtpPackets(reader, rtp_file_name);
            } else {
                Ap4.Debug("No movie found in the file\n");
                return (int)Ap4Result.FAILURE;
            }

            if (input is IDisposable) ((IDisposable)input).Dispose();

            return (int)result;
        }
    }
}
