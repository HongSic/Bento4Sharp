// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Encrypt/Mp4Encrypt.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream so that
//   the bytes written are exactly those of upstream ('\n' line endings, no
//   encoding translation).
// - upstream never closes its files on the early `return 1` paths (the C
//   runtime does it at exit); here every stream that was opened is disposed
//   when Main returns.

using System;
using System.Collections.Generic;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Encrypt
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Encrypter - Version 1.7\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2021 Axiomatic Systems, LLC";

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
                "usage: mp4encrypt --method <method> [options] <input> <output>\n" +
                "     <method> is OMA-PDCF-CBC, OMA-PDCF-CTR, MARLIN-IPMP-ACBC,\n" +
                "     MARLIN-IPMP-ACGK, ISMA-IAEC, PIFF-CBC, PIFF-CTR, MPEG-CENC,\n" +
                "     MPEG-CBC1, MPEG-CENS, MPEG-CBCS\n" +
                "  Options:\n" +
                "  --show-progress\n" +
                "      Show progress details\n" +
                "  --fragments-info <filename>\n" +
                "      Encrypt the fragments read from <input>, with track info read\n" +
                "      from <filename>\n" +
                " --multi\n" +
                "      Encrypt multiple individual fragment files. With this option,\n" +
                "      <input> is a list of fragment input files, and <output> is a\n" +
                "      printf-style filename pattern where the first %s is replaced with\n" +
                "      the name of the corresponding input file, with the suffix omitted\n" +
                "  --key <n>:<k>:<iv>\n" +
                "      Specifies the key to use for a track (or group key).\n" +
                "      <n> is a track ID, <k> a 128-bit key in hex (32 characters)\n" +
                "      and <iv> a 64-bit or 128-bit IV or salting key in hex\n" +
                "      (16 or 32 characters) depending on the cipher mode\n" +
                "      The key and IV values can also be specified with the keyword 'random'\n" +
                "      instead of a hex-encoded value, in which case a randomly generated value\n" +
                "      will be used.\n" +
                "      (several --key options can be used, one for each track)\n" +
                "  --strict\n" +
                "      Fail if there is a warning (ex: one or more tracks would be left unencrypted)\n" +
                "  --property <n>:<name>:<value>\n" +
                "      Specifies a named string property for a track\n" +
                "      <n> is a track ID, <name> a property name, and <value> is the\n" +
                "      property value\n" +
                "      (several --property options can be used, one or more for each track)\n" +
                "  --global-option <name>:<value>\n" +
                "      Sets the global option <name> to be equal to <value>\n" +
                "  --pssh <system-id>:<filename>\n" +
                "      Add a 'pssh' atom for this system ID, with the payload\n" +
                "      loaded from <filename>.\n" +
                "      (several --pssh options can be used, with a different system ID for each)\n" +
                "      (the filename can be left empty after the ':' if no payload is needed)\n" +
                "  --pssh-v1 <system-id>:<filename>\n" +
                "      Same as --pssh but generates a version=1 'pssh' atom\n" +
                "      (this option must appear *after* the --property options on the command line)\n" +
                "  --kms-uri <uri>\n" +
                "      Specifies the KMS URI for the ISMA-IAEC method\n" +
                "\n" +
                "  Method Specifics:\n" +
                "    OMA-PDCF-CBC, MARLIN-IPMP-ACBC, MARLIN-IPMP-ACGK, PIFF-CBC, MPEG-CBC1, MPEG-CBCS: \n" +
                "    the <iv> can be 64-bit or 128-bit\n" +
                "    If the IV is specified as a 64-bit value, it will be padded with zeros.\n" +
                "\n" +
                "    OMA-PDCF-CTR, ISMA-IAEC, PIFF-CTR, MPEG-CENC, MPEG-CENS:\n" +
                "    the <iv> should be a 64-bit hex string.\n" +
                "    If a 128-bit value is supplied, it will be truncated to 64-bit.\n" +
                "\n" +
                "    OMA-PDCF-CBC, OMA-PDCF-CTR: The following properties are defined,\n" +
                "    and all other properties are stored in the textual headers:\n" +
                "      ContentId       -> the content ID for the track\n" +
                "      RightsIssuerUrl -> the Rights Issuer URL\n" +
                "\n" +
                "    MARLIN-IPMP-ACBC, MARLIN-IPMP-ACGK: The following properties are defined:\n" +
                "      ContentId -> the content ID for the track\n" +
                "\n" +
                "    MARLIN-IPMP-ACGK: The group key is specified with --key where <n>\n" +
                "    is 0. The <iv> part of the key must be present, but will be ignored;\n" +
                "    It should therefore be set to 0000000000000000\n" +
                "\n" +
                "    MPEG-CENC, MPEG-CBC1, MPEG-CENS, MPEG-CBCS, PIFF-CTR, PIFF-CBC:\n" +
                "    The following properties are defined:\n" +
                "      KID -> the value of KID, 16 bytes, in hexadecimal (32 characters)\n" +
                "      ContentId -> Content ID mapping for KID (Marlin option)\n" +
                "      PsshPadding -> pad the 'pssh' container to this size\n" +
                "                    (only when using ContentId).\n" +
                "                    This property should be set for track ID 0 only\n"
                );
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private enum Method {
            METHOD_NONE,
            METHOD_OMA_PDCF_CBC,
            METHOD_OMA_PDCF_CTR,
            METHOD_MARLIN_IPMP_ACBC,
            METHOD_MARLIN_IPMP_ACGK,
            METHOD_PIFF_CBC,
            METHOD_PIFF_CTR,
            METHOD_MPEG_CENC,
            METHOD_MPEG_CBC1,
            METHOD_MPEG_CENS,
            METHOD_MPEG_CBCS,
            METHOD_ISMA_AES
        }

        private const uint MP4_ENCRYPT_MAX_FILENAME_LENGTH = 2048;

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
        |   CheckWarning
        +---------------------------------------------------------------------*/
        private static bool CheckWarning(ByteStream stream, ProtectionKeyMap key_map, Method method)
        {
            File file = new File(stream, true);
            Movie movie = file.GetMovie();
            if (movie == null) {
                if (method != Method.METHOD_MPEG_CENC &&
                    method != Method.METHOD_MPEG_CBC1 &&
                    method != Method.METHOD_MPEG_CENS &&
                    method != Method.METHOD_MPEG_CBCS &&
                    method != Method.METHOD_PIFF_CBC  &&
                    method != Method.METHOD_PIFF_CTR) {
                    Eprintf("WARNING: no movie atom found in input file\n");
                    return false;
                }
            }

            bool warning = false;
            switch (method) {
                case Method.METHOD_MPEG_CENC:
                case Method.METHOD_MPEG_CBC1:
                case Method.METHOD_MPEG_CENS:
                case Method.METHOD_MPEG_CBCS:
                case Method.METHOD_PIFF_CBC:
                case Method.METHOD_PIFF_CTR:
                    if (movie != null && !movie.HasFragments()) {
                        Eprintf("WARNING: this encryption method only applies to fragmented files\n");
                        warning = true;
                    }
                    break;
                default:
                    break;
            }

            if (movie != null) {
                for (int i=0; i<movie.GetTracks().Count; i++) {
                    Track track = movie.GetTracks()[i];
                    if (track == null) return false;
                    DataBuffer key = key_map.GetKey(track.GetId());
                    if (key == null) {
                        Eprintf("WARNING: track ID " + D(track.GetId()) + " will not be encrypted\n");
                        warning = true;
                    }
                }
            }

            stream.Seek(0);
            return warning;
        }

        /*----------------------------------------------------------------------
        |   CreateProcessor
        +---------------------------------------------------------------------*/
        private static Processor CreateProcessor(Method           method,
                                                  string           kms_uri,
                                                  ProtectionKeyMap key_map,
                                                  TrackPropertyMap property_map,
                                                  List<PsshAtom>   pssh_atoms)
        {
            if (method == Method.METHOD_ISMA_AES) {
                if (kms_uri == null) {
                    Eprintf("ERROR: method ISMA-IAEC requires --kms-uri\n");
                    return null;
                }
                IsmaEncryptingProcessor isma_processor = new IsmaEncryptingProcessor(kms_uri);
                isma_processor.GetKeyMap().SetKeys(key_map);
                return isma_processor;
            } else if (method == Method.METHOD_MARLIN_IPMP_ACBC ||
                       method == Method.METHOD_MARLIN_IPMP_ACGK) {
                bool use_group_key = (method == Method.METHOD_MARLIN_IPMP_ACGK);
                if (use_group_key) {
                    // check that the group key is set
                    if (key_map.GetKey(0) == null) {
                        Eprintf("ERROR: method MARLIN-IPMP-ACGK requires a group key\n");
                        return null;
                    }
                }
                MarlinIpmpEncryptingProcessor marlin_processor =
                    new MarlinIpmpEncryptingProcessor(use_group_key);
                marlin_processor.GetKeyMap().SetKeys(key_map);
                marlin_processor.GetPropertyMap().SetProperties(property_map);
                return marlin_processor;
            } else if (method == Method.METHOD_OMA_PDCF_CTR ||
                       method == Method.METHOD_OMA_PDCF_CBC) {
                OmaDcfEncryptingProcessor oma_processor =
                    new OmaDcfEncryptingProcessor(method == Method.METHOD_OMA_PDCF_CTR?
                                                  OmaDcfCipherMode.OMA_DCF_CIPHER_MODE_CTR :
                                                  OmaDcfCipherMode.OMA_DCF_CIPHER_MODE_CBC);
                oma_processor.GetKeyMap().SetKeys(key_map);
                oma_processor.GetPropertyMap().SetProperties(property_map);
                return oma_processor;
            } else if (method == Method.METHOD_PIFF_CTR  ||
                       method == Method.METHOD_PIFF_CBC  ||
                       method == Method.METHOD_MPEG_CENC ||
                       method == Method.METHOD_MPEG_CBC1 ||
                       method == Method.METHOD_MPEG_CENS ||
                       method == Method.METHOD_MPEG_CBCS) {
                CencVariant variant = CencVariant.CENC_VARIANT_MPEG_CENC;
                uint        options = 0;

                // init local options based on global options
                if (GlobalOptions.GetBool("mpeg-cenc.eme-pssh")) {
                    options |= CencEncryptingProcessor.OPTION_EME_PSSH;
                }
                if (GlobalOptions.GetBool("mpeg-cenc.piff-compatible")) {
                    options |= CencEncryptingProcessor.OPTION_PIFF_COMPATIBILITY;
                }
                if (GlobalOptions.GetBool("mpeg-cenc.piff-iv-size-16")) {
                    options |= CencEncryptingProcessor.OPTION_PIFF_IV_SIZE_16;
                }
                if (GlobalOptions.GetBool("mpeg-cenc.iv-size-8")) {
                    options |= CencEncryptingProcessor.OPTION_IV_SIZE_8;
                }
                if (GlobalOptions.GetBool("mpeg-cenc.no-senc")) {
                    options |= CencEncryptingProcessor.OPTION_NO_SENC;
                }

                switch (method) {
                    case Method.METHOD_PIFF_CBC:
                        variant = CencVariant.CENC_VARIANT_PIFF_CBC;
                        break;

                    case Method.METHOD_PIFF_CTR:
                        variant = CencVariant.CENC_VARIANT_PIFF_CTR;
                        break;

                    case Method.METHOD_MPEG_CENC:
                        variant = CencVariant.CENC_VARIANT_MPEG_CENC;
                        break;

                    case Method.METHOD_MPEG_CBC1:
                        variant = CencVariant.CENC_VARIANT_MPEG_CBC1;
                        break;

                    case Method.METHOD_MPEG_CENS:
                        variant = CencVariant.CENC_VARIANT_MPEG_CENS;
                        break;

                    case Method.METHOD_MPEG_CBCS:
                        variant = CencVariant.CENC_VARIANT_MPEG_CBCS;
                        break;

                    default:
                        break;
                }
                CencEncryptingProcessor cenc_processor = new CencEncryptingProcessor(variant, options);
                cenc_processor.GetKeyMap().SetKeys(key_map);
                cenc_processor.GetPropertyMap().SetProperties(property_map);
                for (int i=0; i<pssh_atoms.Count; i++) {
                    cenc_processor.GetPsshAtoms().Add(pssh_atoms[i]);
                }
                return cenc_processor;
            }

            return null;
        }

        /*----------------------------------------------------------------------
        |   FormatOutputFilename
        +---------------------------------------------------------------------*/
        // PORT NOTE: snprintf(buffer, MP4_ENCRYPT_MAX_FILENAME_LENGTH+1, pattern, name)
        // with a user supplied pattern. Only what is defined for a single string
        // argument is implemented: "%%" and "%s" (the first one gets the name,
        // further ones, undefined upstream, produce nothing; other conversions,
        // also undefined upstream, are copied verbatim).
        private static string FormatOutputFilename(string pattern, string name)
        {
            System.Text.StringBuilder output = new System.Text.StringBuilder();
            bool name_used = false;
            for (int i=0; i<pattern.Length; i++) {
                char c = pattern[i];
                if (c == '%' && i+1 < pattern.Length) {
                    char n = pattern[i+1];
                    if (n == '%') {
                        output.Append('%');
                        ++i;
                        continue;
                    } else if (n == 's') {
                        if (!name_used) output.Append(name);
                        name_used = true;
                        ++i;
                        continue;
                    }
                }
                output.Append(c);
            }

            // truncate like snprintf does (sizes are in bytes)
            byte[] bytes = Ap4Utils.StringToBytes(output.ToString());
            if (bytes.Length > MP4_ENCRYPT_MAX_FILENAME_LENGTH) {
                return Ap4Utils.StringFromBytes(bytes, 0, MP4_ENCRYPT_MAX_FILENAME_LENGTH);
            }
            return output.ToString();
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
            if (args.Length == 0) return PrintUsageAndExit();

            // parse options
            string           kms_uri = null;
            Method           method  = Method.METHOD_NONE;
            string           input_filename = null;
            string           output_filename = null;
            string           fragments_info_filename = null;
            bool             multi = false;
            List<string>     input_fragments = new List<string>();
            ProtectionKeyMap key_map = new ProtectionKeyMap();
            TrackPropertyMap property_map = new TrackPropertyMap();
            bool             show_progress = false;
            bool             strict = false;
            List<PsshAtom>   pssh_atoms = new List<PsshAtom>();
            DataBuffer       kids = new DataBuffer();
            uint             kid_count = 0;
            Ap4Result        result = Ap4Result.SUCCESS;

            // parse the command line arguments
            int argi = 0; // PORT NOTE: index of the current argument (`argv` walk upstream)
            string arg;
            while ((arg = argi < args.Length ? args[argi++] : null) != null) {
                if (arg == "--method") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --method option\n");
                        return 1;
                    }
                    if (arg == "OMA-PDCF-CBC") {
                        method = Method.METHOD_OMA_PDCF_CBC;
                    } else if (arg == "OMA-PDCF-CTR") {
                        method = Method.METHOD_OMA_PDCF_CTR;
                    } else if (arg == "MARLIN-IPMP-ACBC") {
                        method = Method.METHOD_MARLIN_IPMP_ACBC;
                    } else if (arg == "MARLIN-IPMP-ACGK") {
                        method = Method.METHOD_MARLIN_IPMP_ACGK;
                    } else if (arg == "PIFF-CBC") {
                        method = Method.METHOD_PIFF_CBC;
                    } else if (arg == "PIFF-CTR") {
                        method = Method.METHOD_PIFF_CTR;
                    } else if (arg == "MPEG-CENC") {
                        method = Method.METHOD_MPEG_CENC;
                    } else if (arg == "MPEG-CBC1") {
                        method = Method.METHOD_MPEG_CBC1;
                    } else if (arg == "MPEG-CENS") {
                        method = Method.METHOD_MPEG_CENS;
                    } else if (arg == "MPEG-CBCS") {
                        method = Method.METHOD_MPEG_CBCS;
                    } else if (arg == "ISMA-IAEC") {
                        method = Method.METHOD_ISMA_AES;
                    } else {
                        Eprintf("ERROR: invalid value for --method argument\n");
                        return 1;
                    }
                } else if (arg == "--fragments-info") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --fragments-info option\n");
                        return 1;
                    }
                    fragments_info_filename = arg;
                } else if (arg == "--multi") {
                    multi = true;
                } else if (arg == "--pssh" || arg == "--pssh-v1") {
                    bool v1 = (arg == "--pssh-v1");
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --pssh\n");
                        return 1;
                    }
                    // PORT NOTE: upstream indexes the raw bytes of the argument
                    byte[] arg_bytes = Ap4Utils.StringToCStringBytes(arg);
                    if (arg_bytes.Length < 32+1 || arg_bytes[32] != (byte)':') {
                        Eprintf("ERROR: invalid argument syntax for --pssh\n");
                        return 1;
                    }
                    byte[] system_id = new byte[16];
                    result = Ap4Utils.ParseHex(Ap4Utils.StringFromBytes(arg_bytes, 0, 32), system_id, 16);
                    if (result.Failed()) {
                        Eprintf("ERROR: invalid argument syntax for --pssh\n");
                        return 1;
                    }
                    string pssh_filename = Ap4Utils.StringFromBytes(arg_bytes, 33, (uint)(arg_bytes.Length-33));

                    // load the pssh payload
                    DataBuffer pssh_payload = new DataBuffer();
                    if (pssh_filename.Length != 0) {
                        ByteStream pssh_input = null;
                        result = CreateFileByteStream(pssh_filename, FileByteStream.Mode.STREAM_MODE_READ, out pssh_input);
                        if (result.Failed()) {
                            Eprintf("ERROR: cannot open pssh payload file (" + D(result) + ")\n");
                            return 1;
                        }
                        ulong pssh_payload_size = 0;
                        pssh_input.GetSize(out pssh_payload_size);
                        pssh_payload.SetDataSize((uint)pssh_payload_size);
                        result = pssh_input.Read(pssh_payload.UseData(), (uint)pssh_payload_size);
                        if (result.Failed()) {
                            Eprintf("ERROR: cannot read pssh payload from file (" + D(result) + ")\n");
                            return 1;
                        }
                    }
                    PsshAtom pssh;
                    if (v1) {
                        if (kid_count != 0) {
                            pssh = new PsshAtom(system_id, kids.GetData(), kid_count);
                        } else {
                            pssh = new PsshAtom(system_id);
                        }
                    } else {
                        pssh = new PsshAtom(system_id);
                    }
                    if (pssh_payload.GetDataSize() != 0) {
                        pssh.SetData(pssh_payload.GetData(), pssh_payload.GetDataSize());
                    }
                    pssh_atoms.Add(pssh);
                } else if (arg == "--kms-uri") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --kms-uri option\n");
                        return 1;
                    }
                    if (method != Method.METHOD_ISMA_AES) {
                        Eprintf("ERROR: --kms-uri only applies to method ISMA-IAEC\n");
                        return 1;
                    }
                    kms_uri = arg;
                } else if (arg == "--show-progress") {
                    show_progress = true;
                } else if (arg == "--strict") {
                    strict = true;
                } else if (arg == "--key") {
                    if (method == Method.METHOD_NONE) {
                        Eprintf("ERROR: --method argument must appear before --key\n");
                        return 1;
                    }
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --key option\n");
                        return 1;
                    }
                    string track_ascii;
                    string key_ascii;
                    string iv_ascii;
                    if (Ap4Utils.SplitArgs(arg, out track_ascii, out key_ascii, out iv_ascii).Failed()) {
                        Eprintf("ERROR: invalid argument for --key option\n");
                        return 1;
                    }
                    uint track = Strtoul(track_ascii);


                    // parse the key value
                    byte[] key = new byte[16];
                    if (key_ascii == "random") {
                        result = Ap4Utils.System_GenerateRandomBytes(key, 16);
                        if (result.Failed()) {
                            Eprintf("ERROR: failed to generate random key (" + D(result) + ")\n");
                            return 1;
                        }
                        string key_hex;
                        Ap4Utils.FormatHex(key, 16, out key_hex);
                        Printf("KEY." + D(track) + "=" + key_hex + "\n");
                    } else {
                        if (Ap4Utils.ParseHex(key_ascii, key, 16) != Ap4Result.SUCCESS) {
                            Eprintf("ERROR: invalid hex format for key\n");
                            return 1;
                        }
                    }

                    // parse the iv
                    byte[] iv = new byte[16];
                    if (iv_ascii == "random") {
                        result = Ap4Utils.System_GenerateRandomBytes(iv, 16);
                        if (result.Failed()) {
                            Eprintf("ERROR: failed to generate random key (" + D(result) + ")\n");
                            return 1;
                        }
                        iv[0] &= 0x7F; // always set the MSB to 0 so we don't have wraparounds
                    } else {
                        uint iv_size = Ap4Utils.StringLength(iv_ascii)/2;
                        // PORT NOTE: upstream parses iv_size bytes into a 16-byte
                        // array without checking iv_size (buffer overflow when the
                        // IV has more than 32 hex chars). Here the extra bytes are
                        // parsed into a scratch buffer and dropped.
                        byte[] iv_parsed = new byte[iv_size > 16 ? iv_size : 16];
                        if (Ap4Utils.ParseHex(iv_ascii, iv_parsed, iv_size) != Ap4Result.SUCCESS) {
                            Eprintf("ERROR: invalid hex format for iv\n");
                            return 1;
                        }
                        Array.Copy(iv_parsed, 0, iv, 0, 16);
                    }
                    switch (method) {
                        case Method.METHOD_OMA_PDCF_CTR:
                        case Method.METHOD_ISMA_AES:
                        case Method.METHOD_PIFF_CTR:
                        case Method.METHOD_MPEG_CENC:
                        case Method.METHOD_MPEG_CENS:
                            // truncate the IV
                            Array.Clear(iv, 8, 8);
                            break;

                        default:
                            break;
                    }

                    // check that the key is not already there
                    if (key_map.GetKey(track) != null) {
                        Eprintf("ERROR: key already set for track " + D(track) + "\n");
                        return 1;
                    }

                    // set the key in the map
                    key_map.SetKey(track, key, 16, iv, 16);
                } else if (arg == "--property") {
                    string track_ascii;
                    string name;
                    string value;
                    if (method != Method.METHOD_OMA_PDCF_CBC     &&
                        method != Method.METHOD_OMA_PDCF_CTR     &&
                        method != Method.METHOD_MARLIN_IPMP_ACBC &&
                        method != Method.METHOD_MARLIN_IPMP_ACGK &&
                        method != Method.METHOD_PIFF_CBC         &&
                        method != Method.METHOD_PIFF_CTR         &&
                        method != Method.METHOD_MPEG_CENC        &&
                        method != Method.METHOD_MPEG_CBC1        &&
                        method != Method.METHOD_MPEG_CENS        &&
                        method != Method.METHOD_MPEG_CBCS) {
                        Eprintf("ERROR: this method does not use properties\n");
                        return 1;
                    }
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --property option\n");
                        return 1;
                    }
                    if (Ap4Utils.SplitArgs(arg, out track_ascii, out name, out value).Failed()) {
                        Eprintf("ERROR: invalid argument for --property option\n");
                        return 1;
                    }
                    uint track = Strtoul(track_ascii);

                    // check that the property is not already set
                    if (property_map.GetProperty(track, name) != null) {
                        Eprintf("ERROR: property " + name + " already set for track " + D(track) + "\n");
                        return 1;
                    }
                    // set the property in the map
                    property_map.SetProperty(track, name, value);

                    // special treatment for KID properties
                    if (name == "KID") {
                        if (Ap4Utils.StringLength(value) != 32) {
                            Eprintf("ERROR: invalid size for KID property (must be 32 hex chars)\n");
                            return 1;
                        }
                        byte[] kid = new byte[16];
                        if (Ap4Utils.ParseHex(value, kid, 16).Failed()) {
                            Eprintf("ERROR: invalid syntax for KID property (must be 32 hex chars)\n");
                            return 1;
                        }

                        // check if we already have this KID
                        bool kid_already_present = false;
                        for (uint i=0; i<kid_count; i++) {
#if NET6_0_OR_GREATER
                            if (new ReadOnlySpan<byte>(kids.GetData(), (int)(i*16), 16).SequenceEqual(kid)) {
#else
                            if (Compat.SequenceEqual(kids.GetData(), (int)(i*16), kid, 0, 16)) {
#endif
                                kid_already_present = true;
                                break;
                            }
                        }
                        if (!kid_already_present) {
                            ++kid_count;
                            kids.AppendData(kid, 16);
                        }
                    }
                } else if (arg == "--global-option") {
                    arg = argi < args.Length ? args[argi++] : null;
                    string name;
                    string value;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument for --global-option option\n");
                        return 1;
                    }
                    if (Ap4Utils.SplitArgs(arg, out name, out value).Failed()) {
                        Eprintf("ERROR: invalid argument for --global-option option\n");
                        return 1;
                    }
                    GlobalOptions.SetString(name, value);
                } else if (multi) {
                    if (argi < args.Length) {
                        input_fragments.Add(arg);
                    } else {
                        output_filename = arg;
                    }
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
            if (method == Method.METHOD_NONE) {
                Eprintf("ERROR: missing --method argument\n");
                return 1;
            }
            if (multi) {
                if (fragments_info_filename == null) {
                    Eprintf("ERROR: --multi requires --fragments-info\n");
                    return 1;
                }
                if (input_fragments.Count == 0) {
                    Eprintf("ERROR: at least one input fragment is required\n");
                    return 1;
                }
                if (output_filename == null) {
                    Eprintf("ERROR: missing output filename\n");
                    return 1;
                }
                if (output_filename.IndexOf("%s", StringComparison.Ordinal) < 0) {
                    Eprintf("ERROR: the output patter must contain a %s placeholder\n");
                    return 1;
                }
            } else {
                if (input_filename == null) {
                    Eprintf("ERROR: missing input filename\n");
                    return 1;
                }
                if (output_filename == null) {
                    Eprintf("ERROR: missing output filename\n");
                    return 1;
                }
            }

            // create an encrypting processor
            Processor processor = null;
            if (!multi) {
                processor = CreateProcessor(method, kms_uri, key_map, property_map, pssh_atoms);
                if (processor == null) {
                    return 1;
                }
            }

            // create the input stream
            ByteStream input = null;
            if (!multi) {
                result = CreateFileByteStream(input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot open input file (" + input_filename + ")\n");
                    return 1;
                }
            }

            // create the output stream
            ByteStream output = null;
            if (!multi) {
                result = CreateFileByteStream(output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot open output file (" + output_filename + ")\n");
                    return 1;
                }
            }

            // create the fragments info stream if needed
            ByteStream fragments_info = null;
            if (fragments_info_filename != null) {
                result = CreateFileByteStream(fragments_info_filename, FileByteStream.Mode.STREAM_MODE_READ, out fragments_info);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot open fragments info file (" + fragments_info_filename + ")\n");
                    return 1;
                }
            }

            // process/encrypt the file
            ProgressListener listener = new ProgressListener();
            if (fragments_info != null) {
                if (multi) {
                    for (int i = 0; i < input_fragments.Count; i++) {
                        // create the input stream
                        if (Ap4Utils.StringLength(input_fragments[i]) > MP4_ENCRYPT_MAX_FILENAME_LENGTH) {
                            Eprintf("ERROR: input filename too long (" + input_fragments[i] + ")\n");
                            return 1;
                        }
                        string fragment_input_filename = input_fragments[i];
                        int last_dot = fragment_input_filename.LastIndexOf('.');
                        if (last_dot >= 0) {
                            fragment_input_filename = fragment_input_filename.Substring(0, last_dot);
                        }
                        result = CreateFileByteStream(input_fragments[i],
                                                      FileByteStream.Mode.STREAM_MODE_READ,
                                                      out input);
                        if (result.Failed()) {
                            Eprintf("ERROR: cannot open input file (" + input_fragments[i] + ")\n");
                            return 1;
                        }

                        // create the output stream
                        string fragment_output_filename = FormatOutputFilename(output_filename, fragment_input_filename);
                        result = CreateFileByteStream(fragment_output_filename,
                                                      FileByteStream.Mode.STREAM_MODE_WRITE,
                                                      out output);
                        if (result.Failed()) {
                            Eprintf("ERROR: cannot open output file (" + fragment_output_filename + ")\n");
                            return 1;
                        }

                        // encrypt the fragment
                        bool check = CheckWarning(fragments_info, key_map, method);
                        if (strict && check) return 1;
                        processor = CreateProcessor(method, kms_uri, key_map, property_map, pssh_atoms);
                        if (processor == null) {
                            Eprintf("ERROR: failed to create decryptor\n");
                            return 1;
                        }
                        result = processor.Process(input, output, fragments_info, show_progress?listener:null);
                        if (result.Failed()) {
                            Eprintf("ERROR: failed to process the file (" + D(result) + ")\n");
                        }

                        // close the streams
                        Release(input);
                        Release(output);

                        // rewind the fragments info stream so we can reuse it
                        fragments_info.Seek(0);
                    }
                    input = null;
                    output = null;
                    processor = null;
                } else {
                    bool check = CheckWarning(fragments_info, key_map, method);
                    if (strict && check) return 1;
                    result = processor.Process(input, output, fragments_info, show_progress?listener:null);
                }
            } else {
                bool check = CheckWarning(input, key_map, method);
                if (strict && check) return 1;
                result = processor.Process(input, output, show_progress?listener:null);
            }
            if (result.Failed()) {
                Eprintf("ERROR: failed to process the file (" + D(result) + ")\n");
            }

            // cleanup
            if (input != null) Release(input);
            if (output != null) Release(output);
            if (fragments_info != null) Release(fragments_info);

            return 0;
        }
    }
}
