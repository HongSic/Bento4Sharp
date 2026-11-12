// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/AvcInfo/AvcInfo.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one buffered binary standard output stream
//   so that the bytes written are exactly those of upstream ('\n' line
//   endings, no encoding translation).

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.AvcInfo
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "AVC Bitstream Info - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2009 Axiomatic Systems, LLC";

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
                    "\n\nusage: avcinfo [options] <input>\n" +
                    "Options:\n" +
                    "  --verbose: show more details\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   ReadGolomb
        +---------------------------------------------------------------------*/
        private static uint ReadGolomb(BitStream bits)
        {
            uint leading_zeros = 0;
            while (bits.ReadBit() == 0) {
                leading_zeros++;
            }
            if (leading_zeros != 0) {
                return unchecked((uint)((1<<(int)leading_zeros)-1)+bits.ReadBits(leading_zeros));
            } else {
                return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   PrintSliceInfo
        +---------------------------------------------------------------------*/
        private static void PrintSliceInfo(byte[] data, int offset)
        {
            BitStream bits = new BitStream();
            // PORT NOTE: upstream always copies 8 bytes from the pointer, even if
            // the NAL unit is shorter (it then reads whatever follows in the
            // buffer). The same bytes are read here from the backing array; bytes
            // beyond the end of the array are taken as 0.
            byte[] bytes = new byte[8];
            for (int i=0; i<8 && offset+i<data.Length; i++) {
                bytes[i] = data[offset+i];
            }
            bits.WriteBytes(bytes, 8);
            ReadGolomb(bits);

            uint slice_type = ReadGolomb(bits);
            string slice_type_name = AvcNalParser.SliceTypeName(slice_type);
            if (slice_type_name == null) slice_type_name = "?";
            Printf(" slice=" + D(slice_type) + " (" + slice_type_name + ")");
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
            //bool verbose = false;

            foreach (string arg in argv) {
                if (arg == "--verbose") {
                    //verbose = true;
                } else {
                    if (filename == null) {
                        filename = arg;
                    } else {
                        Eprintf("ERROR: unexpected argument '" + arg + "'\n");
                        return 1;
                    }
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

            AvcNalParser parser = new AvcNalParser();
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
                        uint    nalu_type = (uint)(ByteAt(nalu_payload, 0)&0x1F);
                        string  nalu_type_name = AvcNalParser.NaluTypeName(nalu_type);
                        if (nalu_type_name == null) nalu_type_name = "UNKNOWN";
                        Printf("NALU " + D(nalu_count).PadLeft(5) +
                               ": size=" + D((int)nalu.GetDataSize()).PadLeft(5) +
                               ", type=" + ((int)nalu_type).ToString("00", CultureInfo.InvariantCulture) +
                               " (" + nalu_type_name + ")");
                        if (nalu_type == 9) {
                            uint    primary_pic_type = (uint)(ByteAt(nalu_payload, 1)>>5);
                            string  primary_pic_type_name = AvcNalParser.PrimaryPicTypeName(primary_pic_type);
                            if (primary_pic_type_name == null) primary_pic_type_name = "UNKNOWN";
                            Printf(" [" + D(primary_pic_type) + ":" + primary_pic_type_name + "]\n");
                        } else if ((nalu_type == 1 || nalu_type == 2 || nalu_type == 5 || nalu_type == 19)
                            && nalu.GetDataSize() > 1) {
                            PrintSliceInfo(nalu_payload, 1);
                            Printf("\n");
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
