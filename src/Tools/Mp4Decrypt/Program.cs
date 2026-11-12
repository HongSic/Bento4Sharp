// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Decrypt/Mp4Decrypt.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream so that
//   the bytes written are exactly those of upstream.
// - upstream never closes its files on the early `return 1` paths (the C
//   runtime does it at exit); here every stream that was opened is disposed
//   when Main returns.

using System;
using System.Collections.Generic;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Decrypt
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Decrypter - Version 1.4\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2015 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static ByteStream s_Stdout;
        private static readonly List<ByteStream> s_OpenStreams = new List<ByteStream>();

        private static void Printf(string text)
        {
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

        // PORT NOTE: fprintf(stderr) writes the raw bytes of the text (file names
        // given on the command line included), not the console encoding.
        private static System.IO.Stream s_Stderr;

        private static void Eprintf(string text)
        {
            if (s_Stderr == null) s_Stderr = Console.OpenStandardError();
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stderr.Write(bytes, 0, bytes.Length);
            s_Stderr.Flush();
        }

        private static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(uint value) { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value
        private static string D(Ap4Result value) { return D((int)value); }

        // strtoul(str, NULL, 10)
        // PORT NOTE: `unsigned long` is 32 bits wide here, as in the reference
        // build of the original tools (wasm32) and in native Windows builds
        // (it is 64 bits wide on 64-bit Linux/macOS): values that do not fit
        // in 32 bits give ULONG_MAX = 0xFFFFFFFF.
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

        // AP4_FileByteStream::Create + bookkeeping (see the PORT NOTES above)
        private static Ap4Result CreateFileByteStream(string name, FileByteStream.Mode mode, out ByteStream stream)
        {
            Ap4Result result = FileByteStream.Create(name, mode, out stream);
            if (result.Succeeded() && stream != null) s_OpenStreams.Add(stream);
            return result;
        }

        // stream->Release()
        private static void Release(ByteStream stream)
        {
            if (stream == null) return;
            s_OpenStreams.Remove(stream);
            if (stream is IDisposable) ((IDisposable)stream).Dispose();
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(
                    BANNER +
                    "\n\n" +
                    "usage: mp4decrypt [options] <input> <output>\n" +
                    "Options are:\n" +
                    "  --show-progress : show progress details\n" +
                    "  --key <id>:<k>\n" +
                    "      <id> is either a track ID in decimal or a 128-bit KID in hex,\n" +
                    "      <k> is a 128-bit key in hex\n" +
                    "      (several --key options can be used, one for each track or KID)\n" +
                    "      note: for dcf files, use 1 as the track index\n" +
                    "      note: for Marlin IPMP/ACGK, use 0 as the track ID\n" +
                    "      note: KIDs are only applicable to some encryption methods like MPEG-CENC\n" +
                    "  --fragments-info <filename>\n" +
                    "      Decrypt the fragments read from <input>, with track info read\n" +
                    "      from <filename>.\n"
                    );
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   ProgressListener
        +---------------------------------------------------------------------*/
        private class ProgressListener : Processor.ProgressListener
        {
            public override Ap4Result OnProgress(uint step, uint total)
            {
                Printf("\r" + D(step) + "/" + D(total));
                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            int exit_code;
            try {
                exit_code = Run(args);
            } finally {
                foreach (ByteStream stream in s_OpenStreams.ToArray()) {
                    Release(stream);
                }
                FlushStdout();
            }
            return exit_code;
        }

        private static int Run(string[] args)
        {
            if (args.Length == 0) {
                return PrintUsageAndExit();
            }

            // create a key map object to hold keys
            ProtectionKeyMap key_map = new ProtectionKeyMap();

            // parse options
            string input_filename = null;
            string output_filename = null;
            string  fragments_info_filename = null;
            bool    show_progress = false;

            int argi = 0; // PORT NOTE: index of the current argument (`argv` walk upstream)
            string arg;
            while ((arg = argi < args.Length ? args[argi++] : null) != null) {
                if (arg == "--key") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --key option\n");
                        return 1;
                    }
                    string keyid_text;
                    string key_text;
                    if (Ap4Utils.SplitArgs(arg, out keyid_text, out key_text) != Ap4Result.SUCCESS) {
                        Eprintf("ERROR: invalid argument for --key option\n");
                        return 1;
                    }
                    byte[] key = new byte[16];
                    uint   track_id = 0;
                    byte[] kid = new byte[16];
                    if (Ap4Utils.StringLength(keyid_text) == 32) {
                        if (Ap4Utils.ParseHex(keyid_text, kid, 16) != Ap4Result.SUCCESS) {
                            Eprintf("ERROR: invalid hex format for key id\n");
                            return 1;
                        }
                    } else {
                        track_id = Strtoul(keyid_text);
                        if (track_id == 0) {
                            Eprintf("ERROR: invalid key id\n");
                            return 1;
                        }
                    }
                    if (Ap4Utils.ParseHex(key_text, key, 16) != Ap4Result.SUCCESS) {
                        Eprintf("ERROR: invalid hex format for key\n");
                        return 1;
                    }
                    // set the key in the map
                    if (track_id != 0) {
                        key_map.SetKey(track_id, key, 16);
                    } else {
                        key_map.SetKeyForKid(kid, key, 16);
                    }
                } else if (arg == "--fragments-info") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --fragments-info option\n");
                        return 1;
                    }
                    fragments_info_filename = arg;
                } else if (arg == "--show-progress") {
                    show_progress = true;
                } else if (input_filename == null) {
                    input_filename = arg;
                } else if (output_filename == null) {
                    output_filename = arg;
                } else {
                    Eprintf("ERROR: unexpected argument (" + arg + ")\n");
                    return 1;
                }
            }

            // check the arguments
            if (input_filename == null) {
                Eprintf("ERROR: missing input filename\n");
                return 1;
            }
            if (output_filename == null) {
                Eprintf("ERROR: missing output filename\n");
                return 1;
            }

            // create the input stream
            Ap4Result result;
            ByteStream input = null;
            result = CreateFileByteStream(input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file (" + input_filename + ") " + D(result) + "\n");
                return 1;
            }

            // create the output stream
            ByteStream output = null;
            result = CreateFileByteStream(output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file (" + output_filename + ") " + D(result) + "\n");
                return 1;
            }

            // create the fragments stream if needed
            ByteStream fragments_info = null;
            if (fragments_info_filename != null) {
                result = CreateFileByteStream(fragments_info_filename, FileByteStream.Mode.STREAM_MODE_READ, out fragments_info);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot open fragments info file (" + fragments_info_filename + ")\n");
                    return 1;
                }
            }

            // create the decrypting processor
            Processor processor = null;
            File input_file = new File(fragments_info != null?fragments_info:input);
            FtypAtom ftyp = input_file.GetFileType();
            if (ftyp != null) {
                if (ftyp.GetMajorBrand() == Ap4.OMA_DCF_BRAND_ODCF || ftyp.HasCompatibleBrand(Ap4.OMA_DCF_BRAND_ODCF)) {
                    processor = new OmaDcfDecryptingProcessor(key_map);
                } else if (ftyp.GetMajorBrand() == Ap4.MARLIN_BRAND_MGSV || ftyp.HasCompatibleBrand(Ap4.MARLIN_BRAND_MGSV)) {
                    processor = new MarlinIpmpDecryptingProcessor(key_map);
                } else if (ftyp.GetMajorBrand() == Ap4.PIFF_BRAND || ftyp.HasCompatibleBrand(Ap4.PIFF_BRAND)) {
                    processor = new CencDecryptingProcessor(key_map);
                }
            }
            if (processor == null) {
                // no ftyp, look at the sample description of the tracks first
                Movie movie = input_file.GetMovie();
                if (movie != null) {
                    List<Track> tracks = movie.GetTracks();
                    for (int i=0; i<tracks.Count; i++) {
                        Track track = tracks[i];
                        if (track != null) {
                            SampleDescription sdesc = track.GetSampleDescription(0);
                            if (sdesc != null && sdesc.GetType() == SampleDescription.Type.TYPE_PROTECTED) {
                                ProtectedSampleDescription psdesc = sdesc as ProtectedSampleDescription;
                                if (psdesc != null) {
                                    if (psdesc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_CENC ||
                                        psdesc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_CBC1 ||
                                        psdesc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_CENS ||
                                        psdesc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_CBCS) {
                                        processor = new CencDecryptingProcessor(key_map);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // by default, try a standard decrypting processor
            if (processor == null) {
                processor = new StandardDecryptingProcessor(key_map);
            }

            input_file = null;
            if (fragments_info != null) {
                fragments_info.Seek(0);
            } else {
                input.Seek(0);
            }

            // process/decrypt the file
            ProgressListener listener = new ProgressListener();
            if (fragments_info != null) {
                result = processor.Process(input, output, fragments_info, show_progress?listener:null);
            } else {
                result = processor.Process(input, output, show_progress?listener:null);
            }
            if (result.Failed()) {
                Eprintf("ERROR: failed to process the file (" + D(result) + ")\n");
            }

            // cleanup
            Release(input);
            Release(output);

            return 0;
        }
    }
}
