// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4DcfPackager/Mp4DcfPackager.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - upstream never closes its files on the early `return 1` paths (the C
//   runtime does it at exit); here every stream that was opened is disposed
//   when Main returns.

using System;
using System.Collections.Generic;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4DcfPackager
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 DCF Packager - Version 1.0.1\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2009 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: fprintf(stderr) replacement)
        +---------------------------------------------------------------------*/
        private static readonly List<ByteStream> s_OpenStreams = new List<ByteStream>();

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
        private static string D(Ap4Result value) { return D((int)value); }

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
                "usage: mp4dcfpackager --method <method> [options] <input> <output>\n" +
                "  --method: <method> is NULL, CBC or CTR\n" +
                "  Options:\n" +
                "  --show-progress: show progress details\n" +
                "  --content-type:  content MIME type\n" +
                "  --content-id:    content ID\n" +
                "  --rights-issuer: rights issuer URL\n" +
                "  --key <k>:<iv>\n" +
                "      Specifies the key to use for encryption.\n" +
                "      <k> a 128-bit key in hex (32 characters)\n" +
                "      and <iv> a 128-bit IV or salting key in hex (32 characters)\n" +
                "  --textual-header <name>:<value>\n" +
                "      Specifies a textual header where <name> is the header name,\n" +
                "      and <value> is the header value\n" +
                "      (several --textual-header options can be used)\n" +
                "\n"
                );
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
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
            }
            return exit_code;
        }

        private static int Run(string[] args)
        {
            if (args.Length == 0) return PrintUsageAndExit();

            // parse options
            byte       encryption_method        = 0;
            bool       encryption_method_is_set = false;
            byte       padding_scheme           = 0;
            string     input_filename           = null;
            string     output_filename          = null;
            bool       show_progress            = false;
            bool       key_is_set               = false;
            byte[]     key = new byte[16];
            byte[]     iv = new byte[16];
            BlockCipher.CipherMode cipher_mode  = BlockCipher.CipherMode.CBC;
            string     content_type             = "";
            string     content_id               = "";
            string     rights_issuer_url        = "";
            ulong      plaintext_length         = 0;
            DataBuffer textual_headers_buffer = new DataBuffer();
            TrackPropertyMap textual_headers = new TrackPropertyMap();

            // parse the command line arguments
            int argi = 0; // PORT NOTE: index of the current argument (`argv` walk upstream)
            string arg;
            while ((arg = argi < args.Length ? args[argi++] : null) != null) {
                if (arg == "--method") {
                    arg = argi < args.Length ? args[argi++] : null;
                    // PORT NOTE: upstream passes a NULL pointer to strcmp() when
                    // --method is the last argument (undefined behaviour, usually
                    // a crash); here it is reported as an invalid value.
                    if (arg == "CBC") {
                        encryption_method = Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CBC;
                        encryption_method_is_set = true;
                        padding_scheme = Ap4.OMA_DCF_PADDING_SCHEME_RFC_2630;
                        cipher_mode = BlockCipher.CipherMode.CBC;
                    } else if (arg == "CTR") {
                        encryption_method = Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CTR;
                        encryption_method_is_set = true;
                        padding_scheme = Ap4.OMA_DCF_PADDING_SCHEME_NONE;
                        cipher_mode = BlockCipher.CipherMode.CTR;
                    } else if (arg == "NULL") {
                        encryption_method = Ap4.OMA_DCF_ENCRYPTION_METHOD_NULL;
                        encryption_method_is_set = true;
                        padding_scheme = Ap4.OMA_DCF_PADDING_SCHEME_NONE;
                    } else {
                        Eprintf("ERROR: invalid value for --method argument\n");
                        return 1;
                    }
                } else if (arg == "--show-progress") {
                    show_progress = true;
                } else if (arg == "--content-type") {
                    content_type = argi < args.Length ? args[argi++] : null;
                    if (content_type == null) {
                        Eprintf("ERROR: missing argument for --content-type option\n");
                        return 1;
                    }
                } else if (arg == "--content-id") {
                    content_id = argi < args.Length ? args[argi++] : null;
                    if (content_id == null) {
                        Eprintf("ERROR: missing argument for --content-id option\n");
                        return 1;
                    }
                } else if (arg == "--rights-issuer") {
                    rights_issuer_url = argi < args.Length ? args[argi++] : null;
                    if (rights_issuer_url == null) {
                        Eprintf("ERROR: missing argument for --rights-issuer option\n");
                        return 1;
                    }
                } else if (arg == "--key") {
                    if (!encryption_method_is_set) {
                        Eprintf("ERROR: --method argument must appear before --key\n");
                        return 1;
                    } else if (encryption_method == Ap4.OMA_DCF_ENCRYPTION_METHOD_NULL) {
                        Eprintf("ERROR: --key cannot be used with --method NULL\n");
                        return 1;
                    }
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --key option\n");
                        return 1;
                    }
                    string key_ascii;
                    string iv_ascii;
                    if (Ap4Utils.SplitArgs(arg, out key_ascii, out iv_ascii).Failed()) {
                        Eprintf("ERROR: invalid argument for --key option\n");
                        return 1;
                    }
                    if (Ap4Utils.ParseHex(key_ascii, key, 16) != Ap4Result.SUCCESS) {
                        Eprintf("ERROR: invalid hex format for key\n");
                    }
                    if (Ap4Utils.ParseHex(iv_ascii, iv, 16) != Ap4Result.SUCCESS) {
                        Eprintf("ERROR: invalid hex format for iv\n");
                        return 1;
                    }
                    // check that the key is not already there
                    if (key_is_set) {
                        Eprintf("ERROR: key already set\n");
                        return 1;
                    }
                    key_is_set = true;
                } else if (arg == "--textual-header") {
                    string name;
                    string value;
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --textual-header option\n");
                        return 1;
                    }
                    if (Ap4Utils.SplitArgs(arg, out name, out value).Failed()) {
                        Eprintf("ERROR: invalid argument for --textual-header option\n");
                        return 1;
                    }

                    // check that the property is not already set
                    if (textual_headers.GetProperty(0, name) != null) {
                        Eprintf("ERROR: textual header " + name + " already set\n");
                        return 1;
                    }
                    // set the property in the map
                    textual_headers.SetProperty(0, name, value);
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
            if (!encryption_method_is_set) {
                Eprintf("ERROR: missing --method argument\n");
                return 1;
            }
            if (!key_is_set) {
                Eprintf("ERROR: encryption key not specified\n");
                return 1;
            }
            if (input_filename == null) {
                Eprintf("ERROR: missing input filename\n");
                return 1;
            }
            if (output_filename == null) {
                Eprintf("ERROR: missing output filename\n");
                return 1;
            }
            bool unused1 = show_progress; // avoid warnings

            // convert to a textual headers buffer
            textual_headers.GetTextualHeaders(0, textual_headers_buffer);

            // create the input stream
            Ap4Result result;
            ByteStream input = null;
            result = CreateFileByteStream(input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file (" + input_filename + ") " + D(result) + "\n");
                return 1;
            }

            // get the size of the input
            result = input.GetSize(out plaintext_length);
            if (result.Failed()) {
                Eprintf("ERROR: cannot get the size of the input\n");
                return 1;
            }

            // create an encrypting stream for the input
            ByteStream encrypted_stream;
            if (encryption_method == Ap4.OMA_DCF_ENCRYPTION_METHOD_NULL) {
                encrypted_stream = input;
            } else {
                result = EncryptingStream.Create(cipher_mode, input, iv, 16, key, 16, true, DefaultBlockCipherFactory.Instance, out encrypted_stream);
                if (result.Failed()) {
                    Eprintf("ERROR: failed to create cipher (" + D(result) + ")\n");
                    return 1;
                }
            }

            // create the output stream
            ByteStream output = null;
            result = CreateFileByteStream(output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file (" + output_filename + ") " + D(result) + "\n");
                return 1;
            }

            // create the file
            File file = new File();

            // set the brand
            uint[] compatible_brands = new uint[1] {Ap4.OMA_DCF_BRAND_ODCF};
            file.SetFileType(Ap4.OMA_DCF_BRAND_ODCF, 2, compatible_brands, 1);

            // create the odrm atom (force a 64-bit size)
            ContainerAtom odrm = new ContainerAtom(AtomType.ODRM, Ap4.FULL_ATOM_HEADER_SIZE_64, true, 0, 0);

            // create the ohdr atom
            OhdrAtom ohdr = new OhdrAtom(encryption_method,
                                         padding_scheme,
                                         plaintext_length,
                                         content_id,
                                         rights_issuer_url,
                                         textual_headers_buffer.GetData(),
                                         textual_headers_buffer.GetDataSize());

            // create the odhe atom (the ownership is transfered)
            OdheAtom odhe = new OdheAtom(content_type, ohdr);
            odrm.AddChild(odhe);

            // create the odda atom
            OddaAtom odda = new OddaAtom(encrypted_stream);
            odrm.AddChild(odda);

            // add the odrm atom to the file (the owndership is transfered)
            file.GetTopLevelAtoms().Add(odrm);

            // write the file to the output
            FileWriter.Write(file, output);

            // cleanup
            Release(input);
            Release(output);

            return 0;
        }
    }
}
