// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/HevcInfo/HevcInfo.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one buffered binary standard output stream
//   so that the bytes written are exactly those of upstream ('\n' line
//   endings, no encoding translation).

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.HevcInfo
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "HEVC Bitstream Info - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2014 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static System.IO.Stream s_Stdout;

        private static void Printf(string text)
        {
            if (s_Stdout == null) {
                s_Stdout = new System.IO.BufferedStream(Console.OpenStandardOutput(), 65536);
            }
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stdout.Write(bytes, 0, bytes.Length);
        }

        private static void FlushStdout()
        {
            if (s_Stdout != null) s_Stdout.Flush();
        }

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

        // PORT NOTE: upstream reads the first bytes of a NAL unit without checking
        // its size; for a NAL unit that is too short it reads what follows in the
        // parser's buffer. The same bytes are read here from the backing array of
        // the buffer; bytes beyond the end of the array are taken as 0.
        private static byte ByteAt(byte[] data, int index)
        {
            return (data != null && index < data.Length) ? data[index] : (byte)0;
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: hevcinfo [options] <input>\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] argv)
        {
            int result = RealMain(argv);
            FlushStdout();
            return result;
        }

        private static int RealMain(string[] argv)
        {
            int argc = argv.Length+1;
            if (argc < 2) {
                return PrintUsageAndExit();
            }
            string filename = null;

            foreach (string arg in argv) {
                if (filename == null) {
                    filename = arg;
                } else {
                    Eprintf("ERROR: unexpected argument '" + arg + "'\n");
                    return 1;
                }
            }
            if (filename == null) {
                Eprintf("ERROR: filename missing\n");
                return 1;
            }

            ByteStream input = null;
            Ap4Result result = FileByteStream.Create(filename,
                                                     FileByteStream.Mode.STREAM_MODE_READ,
                                                     out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file " + filename + " (" + D((int)result) + ")\n");
                return 1;
            }

            HevcNalParser parser = new HevcNalParser();
            uint nalu_count = 0;
            byte[] input_buffer = new byte[4096];
            for (;;) {
                bool eos;
                uint bytes_in_buffer = 0;
                result = input.ReadPartial(input_buffer, (uint)input_buffer.Length, out bytes_in_buffer);
                if (result.Succeeded()) {
                    eos = false;
                } else if (result == Ap4Result.ERROR_EOS) {
                    eos = true;
                } else {
                    Eprintf("ERROR: failed to read from input file\n");
                    break;
                }
                uint offset = 0;
                do {
                    DataBuffer nalu = null;
                    uint bytes_consumed = 0;
                    result = parser.Feed(input_buffer, (int)offset, bytes_in_buffer, out bytes_consumed, out nalu, eos);
                    if (result.Failed()) {
                        Eprintf("ERROR: Feed() failed (" + D((int)result) + ")\n");
                        break;
                    }
                    if (nalu != null) {
                        byte[]  nalu_payload = nalu.GetData();
                        uint    nalu_type = (uint)((ByteAt(nalu_payload, 0) >> 1) & 0x3F);
                        string  nalu_type_name = HevcNalParser.NaluTypeName(nalu_type);
                        if (nalu_type_name == null) nalu_type_name = "UNKNOWN";
                        Printf("NALU " + D(nalu_count).PadLeft(5) +
                               ": size=" + D((int)nalu.GetDataSize()).PadLeft(5) +
                               ", type=" + ((int)nalu_type).ToString("00", CultureInfo.InvariantCulture) +
                               " (" + nalu_type_name + ")");
                        if (nalu_type == Ap4.HEVC_NALU_TYPE_AUD_NUT && nalu.GetDataSize() > 1) {
                            // Access Unit Delimiter
                            uint    pic_type = (uint)(nalu_payload[1]>>5);
                            string  pic_type_name = HevcNalParser.PicTypeName(pic_type);
                            if (pic_type_name == null) pic_type_name = "UNKNOWN";
                            Printf(" [" + D(pic_type) + ":" + pic_type_name + "]\n");
                        } else {
                            Printf("\n");
                        }
                        nalu_count++;
                    }
                    offset += bytes_consumed;
                    bytes_in_buffer -= bytes_consumed;
                } while (bytes_in_buffer != 0);
                if (eos) break;
            }

            if (input is IDisposable) ((IDisposable)input).Dispose();

            return 0;
        }
    }
}
