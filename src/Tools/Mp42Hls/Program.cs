// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp42Hls/Mp42Hls.cpp
// Original work Copyright 2002-2018 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream so that
//   the bytes written are exactly those of upstream ('\n' line endings, no
//   encoding translation).
// - sprintf(buffer, <user supplied pattern>, n) is done by Sprintf(), a small
//   implementation of the printf integer conversions.
// - the library has no reference counting, but this tool depends on it (an
//   EncryptingStream keeps its output file alive): AddReference()/Release()
//   below keep the upstream counts for the streams of this file, and close a
//   file when its count drops to 0.
// - upstream relies on exit() to flush and close the files that are still
//   open when main() returns early: the port closes the remaining ones when
//   Main returns.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Bento4;

namespace Bento4Sharp.Tools.Mp42Hls
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 To HLS File Converter - Version 1.2\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2018 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   options
        +---------------------------------------------------------------------*/
        private enum EncryptionMode {
            ENCRYPTION_MODE_NONE,
            ENCRYPTION_MODE_AES_128,
            ENCRYPTION_MODE_SAMPLE_AES
        }

        private enum EncryptionIvMode {
            ENCRYPTION_IV_MODE_NONE,
            ENCRYPTION_IV_MODE_SEQUENCE,
            ENCRYPTION_IV_MODE_RANDOM,
            ENCRYPTION_IV_MODE_FPS
        }

        private enum AudioFormat {
            AUDIO_FORMAT_TS,
            AUDIO_FORMAT_PACKED
        }

        private static class Options {
            public static string           input;
            public static bool             verbose;
            public static uint             hls_version;
            public static uint             pmt_pid;
            public static uint             audio_pid;
            public static uint             video_pid;
            public static int              audio_track_id;
            public static int              video_track_id;
            public static AudioFormat      audio_format;
            public static string           index_filename;
            public static string           iframe_index_filename;
            public static bool             output_single_file;
            public static bool             show_info;
            public static string           segment_filename_template;
            public static string           segment_url_template;
            public static uint             segment_duration;
            public static uint             segment_duration_threshold;
            public static string           allow_cache;
            public static string           encryption_key_hex;
            public static byte[]           encryption_key = new byte[16];
            public static byte[]           encryption_iv = new byte[16];
            public static EncryptionMode   encryption_mode;
            public static EncryptionIvMode encryption_iv_mode;
            public static string           encryption_key_uri;
            public static string           encryption_key_format;
            public static string           encryption_key_format_versions;
            public static List<string>     encryption_key_lines = new List<string>();
            public static ulong            pcr_offset;
        }

        private static class Stats {
            public static ulong  segments_total_size;
            public static double segments_total_duration;
            public static uint   segment_count;
            public static double max_segment_bitrate;
            public static ulong  iframes_total_size;
            public static uint   iframe_count;
            public static double max_iframe_bitrate;
        }

        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const uint DefaultSegmentDurationThreshold = 15; // milliseconds

        private const byte AP4_MPEG2_STREAM_TYPE_SAMPLE_AES_AVC             = 0xDB;
        private const byte AP4_MPEG2_STREAM_TYPE_SAMPLE_AES_ISO_IEC_13818_7 = 0xCF;
        private const byte AP4_MPEG2_STREAM_TYPE_SAMPLE_AES_ATSC_AC3        = 0xC1;
        private const byte AP4_MPEG2_STREAM_TYPE_SAMPLE_AES_ATSC_EAC3       = 0xC2;
        private const byte AP4_MPEG2_PRIVATE_DATA_INDICATOR_DESCRIPTOR_TAG  = 15;
        private const byte AP4_MPEG2_REGISTRATION_DESCRIPTOR_TAG            = 5;

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static ByteStream s_Stdout;

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

        private static void Eprintf(string text)
        {
            Console.Error.Write(text);
        }

        private static string D(int value)   { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(uint value)  { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value
        private static string U(uint value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string LLD(ulong value) { return unchecked((long)value).ToString(CultureInfo.InvariantCulture); } // %lld of an unsigned value

        // %f, %.2f ...
        private static string F(double value, int precision = 6)
        {
#if NET6_0_OR_GREATER
            if (double.IsNaN(value)) return double.IsNegative(value) ? "-nan" : "nan";
#else
            if (double.IsNaN(value)) return Compat.IsNegative(value) ? "-nan" : "nan";
#endif
            if (double.IsInfinity(value)) return value < 0 ? "-inf" : "inf";
#if NET6_0_OR_GREATER
            string text = value.ToString("F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
#else
            string text = Compat.FormatFixed(value, precision);
#endif
#if NET6_0_OR_GREATER
            if (double.IsNegative(value) && text.Length > 0 && text[0] != '-') text = "-" + text; // -0.000000
#else
            if (Compat.IsNegative(value) && text.Length > 0 && text[0] != '-') text = "-" + text; // -0.000000
#endif
            return text;
        }

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

        // sprintf(buffer, format, arg) where `format` is supplied by the user and
        // `arg` is the only argument (an unsigned int)
        private static string Sprintf(string format, uint arg)
        {
            StringBuilder sb = new StringBuilder();
            bool arg_used = false;
            int i = 0;
            while (i < format.Length) {
                char c = format[i++];
                if (c == '\0') break;
                if (c != '%') {
                    sb.Append(c);
                    continue;
                }
                if (i < format.Length && format[i] == '%') {
                    sb.Append('%');
                    ++i;
                    continue;
                }

                // flags
                bool left = false, plus = false, space = false, alt = false, zero = false;
                for (; i < format.Length; i++) {
                    char f = format[i];
                    if      (f == '-') left  = true;
                    else if (f == '+') plus  = true;
                    else if (f == ' ') space = true;
                    else if (f == '#') alt   = true;
                    else if (f == '0') zero  = true;
                    else break;
                }

                // width
                int width = 0;
                if (i < format.Length && format[i] == '*') {
                    width = unchecked((int)(arg_used ? 0 : arg));
                    arg_used = true;
                    if (width < 0) { left = true; width = -width; }
                    ++i;
                } else {
                    while (i < format.Length && format[i] >= '0' && format[i] <= '9') {
                        width = width*10+(format[i++]-'0');
                    }
                }

                // precision
                int precision = -1;
                if (i < format.Length && format[i] == '.') {
                    ++i;
                    precision = 0;
                    if (i < format.Length && format[i] == '*') {
                        precision = unchecked((int)(arg_used ? 0 : arg));
                        arg_used = true;
                        ++i;
                    } else {
                        while (i < format.Length && format[i] >= '0' && format[i] <= '9') {
                            precision = precision*10+(format[i++]-'0');
                        }
                    }
                }

                // length modifiers
                while (i < format.Length && "hlLjzt".IndexOf(format[i]) >= 0) ++i;

                // conversion
                if (i >= format.Length) break; // incomplete conversion specification
                char conv = format[i++];
                uint value = arg_used ? 0 : arg;
                string sign = "";
                string prefix = "";
                string digits;
                switch (conv) {
                    case 'd':
                    case 'i': {
                        arg_used = true;
                        int signed_value = unchecked((int)value);
                        if (signed_value < 0) {
                            sign = "-";
                        } else if (plus) {
                            sign = "+";
                        } else if (space) {
                            sign = " ";
                        }
                        digits = Math.Abs((long)signed_value).ToString(CultureInfo.InvariantCulture);
                        break;
                    }
                    case 'u':
                        arg_used = true;
                        digits = value.ToString(CultureInfo.InvariantCulture);
                        break;
                    case 'x':
                        arg_used = true;
                        digits = value.ToString("x", CultureInfo.InvariantCulture);
                        if (alt && value != 0) prefix = "0x";
                        break;
                    case 'X':
                        arg_used = true;
                        digits = value.ToString("X", CultureInfo.InvariantCulture);
                        if (alt && value != 0) prefix = "0X";
                        break;
                    case 'o':
                        arg_used = true;
                        digits = Convert.ToString((long)value, 8);
                        if (alt && (precision < 0 || precision <= digits.Length) && value != 0) prefix = "0";
                        break;
                    case 'c': {
                        arg_used = true;
                        string ch = ((char)(value & 0xFF)).ToString();
                        sb.Append(left ? ch.PadRight(width) : ch.PadLeft(width));
                        continue;
                    }
                    default:
                        // PORT NOTE: other conversions (%s, %f, %n...) would make
                        // upstream read an argument of the wrong type (undefined
                        // behaviour): nothing is emitted for them.
                        arg_used = true;
                        continue;
                }

                if (precision >= 0) {
                    zero = false;
                    if (precision == 0 && value == 0) digits = "";
                    if (digits.Length < precision) digits = digits.PadLeft(precision, '0');
                }
                int length = sign.Length+prefix.Length+digits.Length;
                if (left) {
                    sb.Append(sign).Append(prefix).Append(digits);
                    if (length < width) sb.Append(' ', width-length);
                } else if (zero) {
                    sb.Append(sign).Append(prefix);
                    if (length < width) sb.Append('0', width-length);
                    sb.Append(digits);
                } else {
                    if (length < width) sb.Append(' ', width-length);
                    sb.Append(sign).Append(prefix).Append(digits);
                }
            }
            // PORT NOTE: the result is a C string upstream: a %c conversion of the
            // value 0 writes a null character, which ends the string
            string text = sb.ToString();
            int terminator = text.IndexOf('\0');
            return terminator >= 0 ? text.Substring(0, terminator) : text;
        }

        /*----------------------------------------------------------------------
        |   reference counting (PORT NOTE: see the notes at the top of the file)
        +---------------------------------------------------------------------*/
        private static readonly Dictionary<ByteStream, int> s_ReferenceCounts = new Dictionary<ByteStream, int>();

        private static void AddReference(ByteStream stream)
        {
            EncryptingStream encrypting_stream;
            int count;
            if ((encrypting_stream = stream as EncryptingStream) != null) {
                encrypting_stream.AddReference();
            } else if (s_ReferenceCounts.TryGetValue(stream, out count)) {
                s_ReferenceCounts[stream] = count+1;
            }
        }

        private static void Release(ByteStream stream)
        {
            if (stream == null) return;
            EncryptingStream encrypting_stream;
            int count;
            if ((encrypting_stream = stream as EncryptingStream) != null) {
                encrypting_stream.Release();
            } else if (s_ReferenceCounts.TryGetValue(stream, out count)) {
                if (--count == 0) {
                    s_ReferenceCounts.Remove(stream);
                    if (stream is IDisposable) ((IDisposable)stream).Dispose();
                } else {
                    s_ReferenceCounts[stream] = count;
                }
            }
        }

        // what exit() does with the files that are still open
        private static void CloseOpenFiles()
        {
            foreach (ByteStream stream in s_ReferenceCounts.Keys) {
                if (stream is IDisposable) ((IDisposable)stream).Dispose();
            }
            s_ReferenceCounts.Clear();
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp42hls [options] <input>\n" +
                    "Options:\n" +
                    "  --verbose\n" +
                    "  --show-info\n" +
                    "  --hls-version <n> (default: 3)\n" +
                    "  --pmt-pid <pid>\n" +
                    "    PID to use for the PMT (default: 0x100)\n" +
                    "  --audio-pid <pid>\n" +
                    "    PID to use for audio packets (default: 0x101)\n" +
                    "  --video-pid <pid>\n" +
                    "    PID to use for video packets (default: 0x102)\n" +
                    "  --audio-track-id <n>\n" +
                    "    Read audio from track ID <n> (0 means no audio)\n" +
                    "  --video-track-id <n>\n" +
                    "    Read video from track ID <n> (0 means no video)\n" +
                    "  --audio-format <format>\n" +
                    "    Format to use for audio-only segments: 'ts' or 'packed' (default: 'ts')\n" +
                    "  --segment-duration <n>\n" +
                    "    Target segment duration in seconds (default: 6)\n" +
                    "  --segment-duration-threshold <t>\n" +
                    "    Segment duration threshold in milliseconds (default: 15)\n" +
                    "  --pcr-offset <offset> in units of 90kHz (default 10000)\n" +
                    "  --index-filename <filename>\n" +
                    "    Filename to use for the playlist/index (default: stream.m3u8)\n" +
                    "  --allow-cache <YES|NO>\n" +
                    "    set #EXT-X-ALLOW-CACHE to YES or NO\n" +
                    "  --segment-filename-template <pattern>\n" +
                    "    Filename pattern to use for the segments. Use a printf-style pattern with\n" +
                    "    one number field for the segment number, unless using single file mode\n" +
                    "    (default: segment-%d.<ext> for separate segment files, or stream.<ext> for single file)\n" +
                    "  --segment-url-template <pattern>\n" +
                    "    URL pattern to use for the segments. Use a printf-style pattern with\n" +
                    "    one number field for the segment number unless unsing single file mode.\n" +
                    "    (may be a relative or absolute URI).\n" +
                    "    (default: segment-%d.<ext> for separate segment files, or stream.<ext> for single file)\n" +
                    "  --iframe-index-filename <filename>\n" +
                    "    Filename to use for the I-Frame playlist (default: iframes.m3u8 when HLS version >= 4)\n" +
                    "  --output-single-file\n" +
                    "    Output all the media in a single file instead of separate segment files.\n" +
                    "    The segment filename template and segment URL template must be simple strings\n" +
                    "    without '%d' or other printf-style patterns\n" +
                    "  --encryption-mode <mode>\n" +
                    "    Encryption mode (only used when --encryption-key is specified). AES-128 or SAMPLE-AES (default: AES-128)\n" +
                    "  --encryption-key <key>\n" +
                    "    Encryption key in hexadecimal (default: no encryption)\n" +
                    "  --encryption-iv-mode <mode>\n" +
                    "    Encryption IV mode: 'sequence', 'random' or 'fps' (FairPlay Streaming) (default: sequence)\n" +
                    "    (when the mode is 'fps', the encryption key must be 32 bytes: 16 bytes for the key\n" +
                    "    followed by 16 bytes for the IV).\n" +
                    "  --encryption-key-uri <uri>\n" +
                    "    Encryption key URI (may be a realtive or absolute URI). (default: key.bin)\n" +
                    "  --encryption-key-format <format>\n" +
                    "    Encryption key format. (default: 'identity')\n" +
                    "  --encryption-key-format-versions <versions>\n" +
                    "    Encryption key format versions.\n" +
                    "  --encryption-key-line <ext-x-key-line>\n" +
                    "    Preformatted encryption key line (only the portion after the #EXT-X-KEY: tag).\n" +
                    "    This option can be used multiple times, once for each preformatted key line to be included in the playlist.\n" +
                    "    (this option is mutually exclusive with the --encryption-key-uri, --encryption-key-format and --encryption-key-format-versions options)\n" +
                    "    (the IV and METHOD parameters will automatically be added, so they must not appear in the <ext-x-key-line> argument)\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   SampleReader
        +---------------------------------------------------------------------*/
        private abstract class SampleReader
        {
            public abstract Ap4Result ReadSample(Sample sample, DataBuffer sample_data);
        }

        /*----------------------------------------------------------------------
        |   TrackSampleReader
        +---------------------------------------------------------------------*/
        private class TrackSampleReader : SampleReader
        {
            public TrackSampleReader(Track track) { _track = track; _sampleIndex = 0; }

            /*----------------------------------------------------------------------
            |   TrackSampleReader
            +---------------------------------------------------------------------*/
            public override Ap4Result ReadSample(Sample sample, DataBuffer sample_data)
            {
                if (_sampleIndex >= _track.GetSampleCount()) return Ap4Result.ERROR_EOS;
                return _track.ReadSample(_sampleIndex++, sample, sample_data);
            }

            private Track _track;
            private uint  _sampleIndex;
        }

        /*----------------------------------------------------------------------
        |   FragmentedSampleReader
        +---------------------------------------------------------------------*/
        private class FragmentedSampleReader : SampleReader
        {
            public FragmentedSampleReader(LinearReader fragment_reader, uint track_id)
            {
                _fragmentReader = fragment_reader;
                _trackId = track_id;
                fragment_reader.EnableTrack(track_id);
            }

            /*----------------------------------------------------------------------
            |   FragmentedSampleReader
            +---------------------------------------------------------------------*/
            public override Ap4Result ReadSample(Sample sample, DataBuffer sample_data)
            {
                return _fragmentReader.ReadNextSample(_trackId, sample, sample_data);
            }

            private LinearReader _fragmentReader;
            private uint         _trackId;
        }

        /*----------------------------------------------------------------------
        |   OpenOutput
        +---------------------------------------------------------------------*/
        private static ByteStream OpenOutput(string filename_pattern, uint segment_number)
        {
            ByteStream output = null;
            string filename = Sprintf(filename_pattern, segment_number);
            Ap4Result result = FileByteStream.Create(filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output (" + D((int)result) + ")\n");
                return null;
            }
            s_ReferenceCounts[output] = 1;

            return output;
        }

        /*----------------------------------------------------------------------
        |   PreventStartCodeEmulation
        +---------------------------------------------------------------------*/
        private static void PreventStartCodeEmulation(byte[] payload, int payload_offset, uint payload_size, DataBuffer output)
        {
            output.Reserve(payload_size*2); // more than enough
            uint   output_size = 0;
            byte[] buffer = output.UseData();

            uint zero_counter = 0;
            for (uint i = 0; i < payload_size; i++) {
                byte b = payload[payload_offset+(int)i];
                if (zero_counter == 2) {
                    if (b == 0 || b == 1 || b == 2 || b == 3) {
                        buffer[output_size++] = 3;
                        zero_counter = 0;
                    }
                }

                buffer[output_size++] = b;

                if (b == 0) {
                    ++zero_counter;
                } else {
                    zero_counter = 0;
                }
            }

            output.SetDataSize(output_size);
        }

        /*----------------------------------------------------------------------
        |   EncryptingStream
        +---------------------------------------------------------------------*/
        private class EncryptingStream : ByteStream {
            /*----------------------------------------------------------------------
            |   EncryptingStream::Create
            +---------------------------------------------------------------------*/
            public static Ap4Result Create(byte[] key, byte[] iv, ByteStream output, out EncryptingStream stream) {
                stream = null;
                BlockCipher block_cipher = null;
                Ap4Result result = DefaultBlockCipherFactory.Instance.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                                   BlockCipher.CipherDirection.ENCRYPT,
                                                                                   BlockCipher.CipherMode.CBC,
                                                                                   null,
                                                                                   key,
                                                                                   16,
                                                                                   out block_cipher);
                if (result.Failed()) return result;
                CbcStreamCipher stream_cipher = new CbcStreamCipher(block_cipher);
                stream_cipher.SetIV(iv);
                stream = new EncryptingStream(stream_cipher, output);

                return Ap4Result.SUCCESS;
            }

            public override Ap4Result ReadPartial(byte[] buffer, int offset, uint bytes_to_read, out uint bytes_read) {
                bytes_read = 0;
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }
            public override Ap4Result WritePartial(byte[]   buffer,
                                                   int      offset,
                                                   uint     bytes_to_write,
                                                   out uint bytes_written) {
                byte[] @out = new byte[bytes_to_write+16];
                uint   out_size = bytes_to_write+16;
                Ap4Result result = _streamCipher.ProcessBuffer(buffer,
                                                               offset,
                                                               bytes_to_write,
                                                               @out,
                                                               0,
                                                               ref out_size);
                if (result.Succeeded()) {
                    result = _output.Write(@out, out_size);
                    bytes_written = bytes_to_write;
                    _size        += bytes_to_write;
                } else {
                    bytes_written = 0;
                }
                return result;
            }
            public override Ap4Result Flush() {
                byte[] trailer = new byte[16];
                uint   trailer_size = 16;
#if !NET20
                Ap4Result result = _streamCipher.ProcessBuffer(Array.Empty<byte>(), 0, 0, trailer, 0, ref trailer_size, true);
#else
                Ap4Result result = _streamCipher.ProcessBuffer(new byte[0], 0, 0, trailer, 0, ref trailer_size, true);
#endif
                if (result.Succeeded() && trailer_size != 0) {
                    _output.Write(trailer, trailer_size);
                    _size += 16-(_size%16);
                }

                return Ap4Result.SUCCESS;
            }
            public override Ap4Result Seek(ulong position) { return Ap4Result.ERROR_NOT_SUPPORTED; }
            public override Ap4Result Tell(out ulong position) { position = _size; return Ap4Result.SUCCESS; }
            public override Ap4Result GetSize(out ulong size) { size = _size;     return Ap4Result.SUCCESS; }

            public void AddReference() {
                ++_referenceCount;
            }
            public void Release() {
                if (--_referenceCount == 0) {
                    // ~EncryptingStream
                    Program.Release(_output);
                }
            }

            private EncryptingStream(CbcStreamCipher stream_cipher, ByteStream output) {
                _referenceCount = 1;
                _streamCipher = stream_cipher;
                _output = output;
                _size = 0;
                Program.AddReference(output);
            }

            private uint            _referenceCount;
            private CbcStreamCipher _streamCipher;
            private ByteStream      _output;
            private ulong           _size;
        }

        /*----------------------------------------------------------------------
        |   SampleEncrypter
        +---------------------------------------------------------------------*/
        private class SampleEncrypter {
            /*----------------------------------------------------------------------
            |   SampleEncrypter::Create
            +---------------------------------------------------------------------*/
            public static Ap4Result Create(byte[] key, byte[] iv, out SampleEncrypter encrypter) {
                encrypter = null;
                BlockCipher block_cipher = null;
                Ap4Result result = DefaultBlockCipherFactory.Instance.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                                   BlockCipher.CipherDirection.ENCRYPT,
                                                                                   BlockCipher.CipherMode.CBC,
                                                                                   null,
                                                                                   key,
                                                                                   16,
                                                                                   out block_cipher);
                if (result.Failed()) return result;
                CbcStreamCipher stream_cipher = new CbcStreamCipher(block_cipher);
                encrypter = new SampleEncrypter(stream_cipher, iv);

                return Ap4Result.SUCCESS;
            }

            /*----------------------------------------------------------------------
            |   SampleEncrypter::EncryptAudioSample
            |
            |   AAC: entire frame
            |   ADTS_header                        // 7 or 9
            |   unencrypted_leader                 // 16 bytes
            |   while (bytes_remaining() >= 16) {
            |       protected_block                // 16 bytes
            |   }
            |   unencrypted_trailer                // 0-15 bytes
            |
            |   AC3: entire frame
            |   unencrypted_leader                 // 16 bytes
            |   while (bytes_remaining() >= 16) {
            |       encrypted_block                // 16 bytes
            |   }
            |   unencrypted_trailer                // 0-15 bytes
            |
            |   E-AC3: each syncframe
            |   unencrypted_leader                 // 16 bytes
            |   while (bytes_remaining() >= 16) {
            |       encrypted_block                // 16 bytes
            |   }
            |   unencrypted_trailer                // 0-15 bytes
            |
            +---------------------------------------------------------------------*/
            public Ap4Result EncryptAudioSample(DataBuffer sample, SampleDescription sample_description)
            {
                if (sample.GetDataSize() <= 16) {
                    return Ap4Result.SUCCESS;
                }

                // deal with E-AC3 syncframes
                if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_EC_3) {
                    // syncword    : 16
                    // strmtyp     : 2
                    // substreamid : 3
                    // frmsiz      : 11
                    byte[] data      = sample.UseData();
                    int    data_pos  = 0; // PORT NOTE: `data` is advanced as a pointer upstream
                    uint   data_size = sample.GetDataSize();
                    while (data_size > 4) {
                        uint syncword = (uint)((data[data_pos]<<8) | data[data_pos+1]);
                        if (syncword != 0x0b77) {
                            return Ap4Result.ERROR_INVALID_FORMAT; // unexpected!
                        }
                        uint frmsiz = (uint)(1+(((data[data_pos+2]<<8)|(data[data_pos+3]))&0x7FF));
                        uint frame_size = 2*frmsiz;
                        if (data_size < frame_size) {
                            return Ap4Result.ERROR_INVALID_FORMAT; // unexpected!
                        }

                        // encrypt the syncframe
                        if (frame_size > 16) {
                            uint encrypted_block_count = (frame_size-16)/16;
                            uint encrypted_size = encrypted_block_count*16;
                            _streamCipher.SetIV(_iv);
                            _streamCipher.ProcessBuffer(data, data_pos+16, encrypted_size, data, data_pos+16, ref encrypted_size);
                        }

                        data_pos  += (int)frame_size;
                        data_size -= frame_size;
                    }
                } else {
                    uint encrypted_block_count = (sample.GetDataSize()-16)/16;
                    uint encrypted_size = encrypted_block_count*16;
                    _streamCipher.SetIV(_iv);
                    _streamCipher.ProcessBuffer(sample.UseData(), 16, encrypted_size, sample.UseData(), 16, ref encrypted_size);
                }

                return Ap4Result.SUCCESS;
            }

            /*----------------------------------------------------------------------
            |   SampleEncrypter::EncryptVideoSample
            |
            |   Sequence of NAL Units:
            |   NAL_unit_type_byte                // 1 byte
            |   unencrypted_leader                // 31 bytes
            |   while (bytes_remaining() > 16) {
            |       protected_block_one_in_ten    // 16 bytes
            |   }
            |   unencrypted_trailer               // 1-16 bytes
            +---------------------------------------------------------------------*/
            public Ap4Result EncryptVideoSample(DataBuffer sample, byte nalu_length_size)
            {
                DataBuffer encrypted = new DataBuffer();

                byte[] data = sample.UseData();
                int    nalu = 0; // PORT NOTE: `nalu` is a pointer into the sample data upstream
                uint   bytes_remaining = sample.GetDataSize();
                while (bytes_remaining > nalu_length_size) {
                    uint nalu_length = 0;
                    switch (nalu_length_size) {
                        case 1:
                            nalu_length = data[nalu];
                            break;

                        case 2:
                            nalu_length = Ap4Utils.BytesToUInt16BE(data, nalu);
                            break;

                        case 4:
                            nalu_length = Ap4Utils.BytesToUInt32BE(data, nalu);
                            break;

                        default:
                            break;
                    }

                    if (bytes_remaining < unchecked(nalu_length_size+nalu_length)) {
                        break;
                    }

                    byte nalu_type = (byte)(data[nalu+nalu_length_size] & 0x1F);
                    if (nalu_length > 48 && (nalu_type == 1 || nalu_type == 5)) {
                        uint encrypted_size = 16*((nalu_length-32)/16);
                        if ((nalu_length%16) == 0) {
                            encrypted_size -= 16;
                        }
                        _streamCipher.SetIV(_iv);
                        for (uint i=0; i<encrypted_size; i += 10*16) {
                            uint one_block_size = 16;
                            _streamCipher.ProcessBuffer(data, nalu+nalu_length_size+32+(int)i, one_block_size,
                                                        data, nalu+nalu_length_size+32+(int)i, ref one_block_size);
                        }

                        // perform startcode emulation prevention
                        DataBuffer escaped_nalu = new DataBuffer();
                        PreventStartCodeEmulation(data, nalu+nalu_length_size, nalu_length, escaped_nalu);

                        // the size may have changed
                        // TODO: this could overflow if nalu_length_size is too small
                        switch (nalu_length_size) {
                            case 1:
                                data[nalu] = (byte)(escaped_nalu.GetDataSize()&0xFF);
                                break;

                            case 2:
                                Ap4Utils.BytesFromUInt16BE(data, nalu, unchecked((ushort)escaped_nalu.GetDataSize()));
                                break;

                            case 4:
                                Ap4Utils.BytesFromUInt32BE(data, nalu, escaped_nalu.GetDataSize());
                                break;

                            default:
                                break;
                        }

                        encrypted.AppendData(data, nalu, nalu_length_size);
                        encrypted.AppendData(escaped_nalu.GetData(), escaped_nalu.GetDataSize());
                    } else {
                        encrypted.AppendData(data, nalu, nalu_length_size);
                        encrypted.AppendData(data, nalu+nalu_length_size, nalu_length);
                    }

                    nalu            += (int)(nalu_length_size+nalu_length);
                    bytes_remaining -= nalu_length_size+nalu_length;
                }

                sample.SetData(encrypted.GetData(), encrypted.GetDataSize());

                return Ap4Result.SUCCESS;
            }

            private SampleEncrypter(CbcStreamCipher stream_cipher, byte[] iv) {
                _streamCipher = stream_cipher;
                Array.Copy(iv, 0, _iv, 0, 16);
            }

            private CbcStreamCipher _streamCipher;
            private byte[]          _iv = new byte[16];
        }

        /*----------------------------------------------------------------------
        |   GetSamplingFrequencyIndex
        +---------------------------------------------------------------------*/
        private static uint GetSamplingFrequencyIndex(uint sampling_frequency)
        {
            switch (sampling_frequency) {
                case 96000: return 0;
                case 88200: return 1;
                case 64000: return 2;
                case 48000: return 3;
                case 44100: return 4;
                case 32000: return 5;
                case 24000: return 6;
                case 22050: return 7;
                case 16000: return 8;
                case 12000: return 9;
                case 11025: return 10;
                case 8000:  return 11;
                case 7350:  return 12;
                default:    return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   WriteAdtsHeader
        +---------------------------------------------------------------------*/
        private static Ap4Result WriteAdtsHeader(ByteStream output,
                                                 uint       frame_size,
                                                 uint       sampling_frequency_index,
                                                 uint       channel_configuration)
        {
            byte[] bits = new byte[7];

            bits[0] = 0xFF;
            bits[1] = 0xF1; // 0xF9 (MPEG2)
            bits[2] = (byte)(0x40 | (sampling_frequency_index << 2) | (channel_configuration >> 2));
            bits[3] = (byte)(((channel_configuration&0x3)<<6) | ((frame_size+7) >> 11));
            bits[4] = (byte)(((frame_size+7) >> 3)&0xFF);
            bits[5] = (byte)((((frame_size+7) << 5)&0xFF) | 0x1F);
            bits[6] = 0xFC;

            return output.Write(bits, 7);

            /*
                0:  syncword 12 always: '111111111111'
                12: ID 1 0: MPEG-4, 1: MPEG-2
                13: layer 2 always: '00'
                15: protection_absent 1
                16: profile 2
                18: sampling_frequency_index 4
                22: private_bit 1
                23: channel_configuration 3
                26: original/copy 1
                27: home 1
                28: emphasis 2 only if ID == 0

                ADTS Variable header: these can change from frame to frame
                28: copyright_identification_bit 1
                29: copyright_identification_start 1
                30: aac_frame_length 13 length of the frame including header (in bytes)
                43: adts_buffer_fullness 11 0x7FF indicates VBR
                54: no_raw_data_blocks_in_frame 2
                ADTS Error check
                crc_check 16 only if protection_absent == 0
           */
        }

        /*----------------------------------------------------------------------
        |   WriteAc4Header
        +---------------------------------------------------------------------*/
        private static Ap4Result WriteAc4Header(ByteStream output,
                                                uint       frame_size)
        {
            byte[] bits = new byte[7];

            bits[0] = 0xac;
            bits[1] = 0x40;
            bits[2] = 0xff;
            bits[3] = 0xff;
            bits[4] = (byte)((frame_size>>16)&0xFF);
            bits[5] = (byte)((frame_size>>8 )&0xFF);
            bits[6] = (byte)((frame_size    )&0xFF);

            return output.Write(bits, 7);
        }

        /*----------------------------------------------------------------------
        |   MakeAudioSetupData
        +---------------------------------------------------------------------*/
        private static Ap4Result MakeAudioSetupData(DataBuffer        audio_setup_data,
                                                    SampleDescription sample_description,
                                                    DataBuffer        sample_data /* needed for encrypted AC3 */)
        {
            byte[]     audio_type = new byte[4];
            DataBuffer setup_data = new DataBuffer();

            if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_MP4A) {
                MpegAudioSampleDescription mpeg_audio_desc = sample_description as MpegAudioSampleDescription;
                if (mpeg_audio_desc == null ||
                    !(mpeg_audio_desc.GetObjectTypeId() == Ap4.OTI_MPEG4_AUDIO          ||
                      mpeg_audio_desc.GetObjectTypeId() == Ap4.OTI_MPEG2_AAC_AUDIO_LC   ||
                      mpeg_audio_desc.GetObjectTypeId() == Ap4.OTI_MPEG2_AAC_AUDIO_MAIN)) {
                    return Ap4Result.ERROR_NOT_SUPPORTED;
                }
                DataBuffer dsi = mpeg_audio_desc.GetDecoderInfo();
                setup_data.SetData(dsi.GetData(), dsi.GetDataSize());

                Mp4AudioDecoderConfig dec_config = new Mp4AudioDecoderConfig();
                Ap4Result result = dec_config.Parse(dsi.GetData(), dsi.GetDataSize());
                if (result.Failed()) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }

                if (dec_config._extension._sbrPresent || dec_config._extension._psPresent) {
                    if (dec_config._extension._psPresent) {
                        audio_type[0] = (byte)'z';
                        audio_type[1] = (byte)'a';
                        audio_type[2] = (byte)'c';
                        audio_type[3] = (byte)'p';
                    } else {
                        audio_type[0] = (byte)'z';
                        audio_type[1] = (byte)'a';
                        audio_type[2] = (byte)'c';
                        audio_type[3] = (byte)'h';
                    }
                } else {
                    audio_type[0] = (byte)'z';
                    audio_type[1] = (byte)'a';
                    audio_type[2] = (byte)'a';
                    audio_type[3] = (byte)'c';
                }
            } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AC_3) {
                audio_type[0] = (byte)'z';
                audio_type[1] = (byte)'a';
                audio_type[2] = (byte)'c';
                audio_type[3] = (byte)'3';

                // the setup data is the first 10 bytes of a syncframe
                if (sample_data.GetDataSize() >= 10) {
                    setup_data.SetData(sample_data.GetData(), 10);
                }
            } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_EC_3) {
                audio_type[0] = (byte)'z';
                audio_type[1] = (byte)'e';
                audio_type[2] = (byte)'c';
                audio_type[3] = (byte)'3';

                Dec3Atom dec3 = sample_description.GetDetails().GetChild(AtomType.DEC3) as Dec3Atom;
                if (dec3 == null) {
                    Eprintf("ERROR: failed to find 'dec3' atom in sample description\n");
                    return (Ap4Result)1;
                }
                setup_data.SetData(dec3.GetRawBytes().GetData(), dec3.GetRawBytes().GetDataSize());
            } else {
                return Ap4Result.SUCCESS;
            }

            audio_setup_data.SetDataSize(8+setup_data.GetDataSize());
            byte[] payload = audio_setup_data.UseData();
            payload[0] = audio_type[0];
            payload[1] = audio_type[1];
            payload[2] = audio_type[2];
            payload[3] = audio_type[3];
            payload[4] = 0; // priming
            payload[5] = 0; // priming
            payload[6] = 1; // version
            payload[7] = unchecked((byte)setup_data.GetDataSize()); // setup_data_length
            if (setup_data.GetDataSize() != 0) {
                Array.Copy(setup_data.GetData(), 0, payload, 8, (int)setup_data.GetDataSize());
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   MakeSampleAesAudioDescriptor
        +---------------------------------------------------------------------*/
        private static Ap4Result MakeSampleAesAudioDescriptor(DataBuffer        descriptor,
                                                              SampleDescription sample_description,
                                                              DataBuffer        sample_data /* needed for encrypted AC3 */)
        {
            // descriptor
            descriptor.SetDataSize(6);
            byte[] payload = descriptor.UseData();
            payload[0] = AP4_MPEG2_PRIVATE_DATA_INDICATOR_DESCRIPTOR_TAG;
            payload[1] = 4;
            if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_MP4A) {
                payload[2] = (byte)'a';
                payload[3] = (byte)'a';
                payload[4] = (byte)'c';
                payload[5] = (byte)'d';
            } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AC_3) {
                payload[2] = (byte)'a';
                payload[3] = (byte)'c';
                payload[4] = (byte)'3';
                payload[5] = (byte)'d';
            } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_EC_3) {
                payload[2] = (byte)'e';
                payload[3] = (byte)'c';
                payload[4] = (byte)'3';
                payload[5] = (byte)'d';
            }

            // compute the audio setup data
            DataBuffer audio_setup_data = new DataBuffer();
            Ap4Result result = MakeAudioSetupData(audio_setup_data, sample_description, sample_data);
            if (result.Failed()) {
                Eprintf("ERROR: failed to make audio setup data (" + D((int)result) + ")\n");
                return result;
            }

            descriptor.SetDataSize(descriptor.GetDataSize()+6+audio_setup_data.GetDataSize());
            payload = descriptor.UseData();
            payload[6+0] = AP4_MPEG2_REGISTRATION_DESCRIPTOR_TAG;
            payload[6+1] = unchecked((byte)(4+audio_setup_data.GetDataSize()));
            payload[6+2] = (byte)'a';
            payload[6+3] = (byte)'p';
            payload[6+4] = (byte)'a';
            payload[6+5] = (byte)'d';
            if (audio_setup_data.GetDataSize() != 0) {
                Array.Copy(audio_setup_data.GetData(), 0, payload, 6+6, (int)audio_setup_data.GetDataSize());
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   MakeSampleAesVideoDescriptor
        +---------------------------------------------------------------------*/
        private static Ap4Result MakeSampleAesVideoDescriptor(DataBuffer descriptor)
        {
            descriptor.SetDataSize(6);
            byte[] payload = descriptor.UseData();
            payload[0] = AP4_MPEG2_PRIVATE_DATA_INDICATOR_DESCRIPTOR_TAG;
            payload[1] = 4;
            payload[2] = (byte)'z';
            payload[3] = (byte)'a';
            payload[4] = (byte)'v';
            payload[5] = (byte)'c';

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   PackedAudioWriter
        +---------------------------------------------------------------------*/
        private class PackedAudioWriter {
            /*----------------------------------------------------------------------
            |   PackedAudioWriter::WriteHeader
            +---------------------------------------------------------------------*/
            public Ap4Result WriteHeader(double     timestamp,
                                         string     private_extension_name,
                                         byte[]     private_extension_data,
                                         uint       private_extension_data_size,
                                         ByteStream output)
            {
                uint header_size = 10+10+45+8;
                // PORT NOTE: the name is copied with its null terminator
                byte[]  private_extension_name_bytes = private_extension_name != null ? Ap4Utils.StringToCStringBytes(private_extension_name) : null;
                uint private_extension_name_size = private_extension_name_bytes != null?(uint)private_extension_name_bytes.Length+1:0;
                if (private_extension_name != null) {
                    header_size += 10+private_extension_name_size+private_extension_data_size;
                }
                byte[] header = new byte[header_size];

                /* ID3 Tag Header
                 ID3v2/file identifier      "ID3"
                 ID3v2 version              $04 00
                 ID3v2 flags                %abcd0000
                 ID3v2 size                 4 * %0xxxxxxx
                */
                header[0] = (byte)'I';
                header[1] = (byte)'D';
                header[2] = (byte)'3';
                header[3] = 4;
                header[4] = 0;
                header[5] = 0;
                header[6] = 0;
                header[7] = 0;
                header[8] = (byte)(((header_size-10)>>7) & 0x7F);
                header[9] = (byte)( (header_size-10)     & 0x7F);

                /* PRIV frame
                 Frame ID   $xx xx xx xx  (four characters)
                 Size       $xx xx xx xx
                 Flags      $xx xx

                 Owner identifier      <text string> $00
                 The private data      <binary data>
                */
                header[10] = (byte)'P';
                header[11] = (byte)'R';
                header[12] = (byte)'I';
                header[13] = (byte)'V';
                header[14] = 0;
                header[15] = 0;
                header[16] = 0;
                header[17] = 45+8;
                header[18] = 0;
                header[19] = 0;
                byte[] owner = Ap4Utils.StringToCStringBytes("com.apple.streaming.transportStreamTimestamp");
                Array.Copy(owner, 0, header, 20, 44); // 45 bytes with the null terminator
                header[20+44] = 0;
                ulong mpeg_ts = (ulong)(timestamp*90000.0);
                Ap4Utils.BytesFromUInt64BE(header, 10+10+45, mpeg_ts);

                // add the extra private extension if needed
                if (private_extension_name != null) {
                    int ext = 10+10+45+8;
                    header[ext+0] = (byte)'P';
                    header[ext+1] = (byte)'R';
                    header[ext+2] = (byte)'I';
                    header[ext+3] = (byte)'V';
                    header[ext+4] = 0;
                    header[ext+5] = 0;
                    header[ext+6] = (byte)(((private_extension_name_size+private_extension_data_size)>>7) & 0x7F);
                    header[ext+7] = (byte)( (private_extension_name_size+private_extension_data_size)     & 0x7F);
                    header[ext+8] = 0;
                    header[ext+9] = 0;
                    Array.Copy(private_extension_name_bytes, 0, header, ext+10, private_extension_name_bytes.Length);
                    header[ext+10+private_extension_name_bytes.Length] = 0;
                    if (private_extension_data_size != 0) {
                        Array.Copy(private_extension_data, 0, header, ext+10+(int)private_extension_name_size, (int)private_extension_data_size);
                    }
                }

                // write the header out
                Ap4Result result = output.Write(header, header_size);

                return result;
            }

            /*----------------------------------------------------------------------
            |   PackedAudioWriter::WriteSample
            +---------------------------------------------------------------------*/
            public Ap4Result WriteSample(Sample             sample,
                                         DataBuffer         sample_data,
                                         SampleDescription  sample_description,
                                         ByteStream         output)
            {
                AudioSampleDescription audio_desc = sample_description as AudioSampleDescription;
                if (audio_desc == null) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_MP4A) {
                    MpegAudioSampleDescription mpeg_audio_desc = sample_description as MpegAudioSampleDescription;

                    if (mpeg_audio_desc == null) return Ap4Result.ERROR_NOT_SUPPORTED;
                    if (mpeg_audio_desc.GetMpeg4AudioObjectType() != Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_LC   &&
                        mpeg_audio_desc.GetMpeg4AudioObjectType() != Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_MAIN &&
                        mpeg_audio_desc.GetMpeg4AudioObjectType() != Ap4.MPEG4_AUDIO_OBJECT_TYPE_SBR      &&
                        mpeg_audio_desc.GetMpeg4AudioObjectType() != Ap4.MPEG4_AUDIO_OBJECT_TYPE_PS) {
                        return Ap4Result.ERROR_NOT_SUPPORTED;
                    }

                    uint sample_rate   = mpeg_audio_desc.GetSampleRate();
                    uint channel_count = mpeg_audio_desc.GetChannelCount();
                    DataBuffer dsi     = mpeg_audio_desc.GetDecoderInfo();
                    if (dsi.GetDataSize() != 0) {
                        Mp4AudioDecoderConfig dec_config = new Mp4AudioDecoderConfig();
                        Ap4Result result = dec_config.Parse(dsi.GetData(), dsi.GetDataSize());
                        if (result.Succeeded()) {
                            sample_rate = dec_config._samplingFrequency;
                            channel_count = dec_config._channelCount;
                        }
                    }
                    uint sampling_frequency_index = GetSamplingFrequencyIndex(sample_rate);
                    uint channel_configuration    = channel_count;

                    WriteAdtsHeader(output, sample.GetSize(), sampling_frequency_index, channel_configuration);
                } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AC_4) {
                    WriteAc4Header(output, sample.GetSize());
                }
                return output.Write(sample_data.GetData(), sample_data.GetDataSize());
            }
        }

        /*----------------------------------------------------------------------
        |   ReadSample
        +---------------------------------------------------------------------*/
        private static Ap4Result ReadSample(SampleReader reader,
                                            Track        track,
                                            Sample       sample,
                                            DataBuffer   sample_data,
                                            ref double   ts,
                                            ref double   duration,
                                            ref bool     eos)
        {
            Ap4Result result = reader.ReadSample(sample, sample_data);
            if (result.Failed()) {
                if (result == Ap4Result.ERROR_EOS) {
                    // advance the timestamp by the last sample's duration
                    ts += duration;
                    eos = true;
                    return Ap4Result.SUCCESS;
                } else {
                    return result;
                }
            }
            ts = (double)sample.GetDts()/(double)track.GetMediaTimeScale();
            duration = sample.GetDuration()/(double)track.GetMediaTimeScale();

            return Ap4Result.SUCCESS;
        }

        // PORT NOTE: the key lines are written twice upstream with the same code;
        // helper for `AP4_FormatHex(Options.encryption_iv, 16, iv_hex)`
        private static string IvHex()
        {
            string iv_hex;
            Ap4Utils.FormatHex(Options.encryption_iv, 16, out iv_hex);
            return iv_hex;
        }

        /*----------------------------------------------------------------------
        |   WriteSamples
        +---------------------------------------------------------------------*/
        private static Ap4Result WriteSamples(Mpeg2TsWriter               ts_writer,
                                              PackedAudioWriter           packed_writer,
                                              Track                       audio_track,
                                              SampleReader                audio_reader,
                                              Mpeg2TsWriter.SampleStream  audio_stream,
                                              Track                       video_track,
                                              SampleReader                video_reader,
                                              Mpeg2TsWriter.SampleStream  video_stream,
                                              uint                        segment_duration_threshold,
                                              byte                        nalu_length_size)
        {
            Sample           audio_sample = new Sample();
            DataBuffer       audio_sample_data = new DataBuffer();
            uint             audio_sample_count = 0;
            double           audio_ts = 0.0;
            double           audio_frame_duration = 0.0;
            bool             audio_eos = false;
            Sample           video_sample = new Sample();
            DataBuffer       video_sample_data = new DataBuffer();
            uint             video_sample_count = 0;
            double           video_ts = 0.0;
            double           video_frame_duration = 0.0;
            bool             video_eos = false;
            double           last_ts = 0.0;
            uint             segment_number = 0;
            ByteStream       segment_output = null;
            double           segment_duration = 0.0;
            List<double>     segment_durations = new List<double>();
            List<uint>       segment_sizes = new List<uint>();
            ulong            segment_position = 0;
            List<ulong>      segment_positions = new List<ulong>();
            List<ulong>      iframe_positions = new List<ulong>();
            List<uint>       iframe_sizes = new List<uint>();
            List<double>     iframe_times = new List<double>();
            List<double>     iframe_durations = new List<double>();
            List<uint>       iframe_segment_indexes = new List<uint>();
            bool             new_segment = true;
            ByteStream       raw_output = null;
            ByteStream       playlist = null;
            string           string_buffer;
            SampleEncrypter  sample_encrypter = null;
            Ap4Result        result = Ap4Result.SUCCESS;

            // prime the samples
            if (audio_reader != null) {
                result = ReadSample(audio_reader, audio_track, audio_sample, audio_sample_data, ref audio_ts, ref audio_frame_duration, ref audio_eos);
                if (result.Failed()) return result;
            }
            if (video_reader != null) {
                result = ReadSample(video_reader, video_track, video_sample, video_sample_data, ref video_ts, ref video_frame_duration, ref video_eos);
                if (result.Failed()) return result;
            }

            for (;;) {
                bool sync_sample = false;
                Track chosen_track = null;
                if (audio_track != null && !audio_eos) {
                    chosen_track = audio_track;
                    if (video_track == null) {
                        if (audio_track.GetSampleDescription(0).GetFormat() == Ap4.SAMPLE_FORMAT_AC_4) {
                            if (audio_sample.IsSync()) {
                                sync_sample = true;
                            }
                        } else {
                            sync_sample = true;
                        }
                    }
                }
                if (video_track != null && !video_eos) {
                    if (audio_track != null) {
                        if (video_ts <= audio_ts) {
                            chosen_track = video_track;
                        }
                    } else {
                        chosen_track = video_track;
                    }
                    if (chosen_track == video_track && video_sample.IsSync()) {
                        sync_sample = true;
                    }
                }

                // check if we need to start a new segment
                if (Options.segment_duration != 0 && (sync_sample || chosen_track == null)) {
                    if (video_track != null) {
                        segment_duration = video_ts - last_ts;
                    } else {
                        segment_duration = audio_ts - last_ts;
                    }
                    if ((segment_duration >= (double)Options.segment_duration - (double)segment_duration_threshold/1000.0) ||
                        chosen_track == null) {
                        if (video_track != null) {
                            last_ts = video_ts;
                        } else {
                            last_ts = audio_ts;
                        }
                        if (segment_output != null) {
                            // flush the output stream
                            segment_output.Flush();

                            // compute the segment size (including padding)
                            ulong segment_end = 0;
                            segment_output.Tell(out segment_end);
                            uint segment_size = 0;
                            if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_AES_128) {
                                segment_size = unchecked((uint)segment_end);
                            } else if (segment_end > segment_position) {
                                segment_size = unchecked((uint)(segment_end-segment_position));
                            }

                            // update counters
                            segment_sizes.Add(segment_size);
                            segment_positions.Add(segment_position);
                            segment_durations.Add(segment_duration);

                            if (segment_duration != 0.0) {
                                double segment_bitrate = 8.0*(double)segment_size/segment_duration;
                                if (segment_bitrate > Stats.max_segment_bitrate) {
                                    Stats.max_segment_bitrate = segment_bitrate;
                                }
                            }
                            if (Options.verbose) {
                                Printf("Segment " + D(segment_number) + ", duration=" + F(segment_duration, 2) + ", " +
                                       D(audio_sample_count) + " audio samples, " +
                                       D(video_sample_count) + " video samples, " +
                                       D(segment_size) + " bytes @" + LLD(segment_position) + "\n");
                            }
                            if (!Options.output_single_file) {
                                Release(segment_output);
                                segment_output = null;
                            }
                            ++segment_number;
                            audio_sample_count = 0;
                            video_sample_count = 0;
                        }
                        new_segment = true;
                    }
                }

                // check if we're done
                if (chosen_track == null) break;

                if (new_segment) {
                    new_segment = false;

                    // compute the new segment position
                    segment_position = 0;
                    if (Options.output_single_file) {
                        if (raw_output != null) {
                            raw_output.Tell(out segment_position);
                        }
                    }

                    // manage the new segment stream
                    if (segment_output == null) {
                        segment_output = OpenOutput(Options.segment_filename_template, segment_number);
                        raw_output = segment_output;
                        if (segment_output == null) return Ap4Result.ERROR_CANNOT_OPEN_FILE;
                    }
                    if (Options.encryption_mode != EncryptionMode.ENCRYPTION_MODE_NONE) {
                        if (Options.encryption_iv_mode == EncryptionIvMode.ENCRYPTION_IV_MODE_SEQUENCE) {
                            Array.Clear(Options.encryption_iv, 0, Options.encryption_iv.Length);
                            Ap4Utils.BytesFromUInt32BE(Options.encryption_iv, 12, segment_number);
                        }
                    }
                    if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_AES_128) {
                        EncryptingStream encrypting_stream = null;
                        result = EncryptingStream.Create(Options.encryption_key, Options.encryption_iv, raw_output, out encrypting_stream);
                        if (result.Failed()) {
                            Eprintf("ERROR: failed to create encrypting stream (" + D((int)result) + ")\n");
                            return result;
                        }
                        Release(segment_output);
                        segment_output = encrypting_stream;
                    } else if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                        sample_encrypter = null;
                        result = SampleEncrypter.Create(Options.encryption_key, Options.encryption_iv, out sample_encrypter);
                        if (result.Failed()) {
                            Eprintf("ERROR: failed to create sample encrypter (" + D((int)result) + ")\n");
                            return result;
                        }
                    }

                    // write the PAT and PMT
                    if (ts_writer != null) {
                        // update the descriptors if needed
                        if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                            DataBuffer descriptor = new DataBuffer();
                            if (audio_track != null) {
                                result = MakeSampleAesAudioDescriptor(descriptor, audio_track.GetSampleDescription(0), audio_sample_data);
                                if (result.Succeeded() && descriptor.GetDataSize() != 0) {
                                    audio_stream.SetDescriptor(descriptor.GetData(), descriptor.GetDataSize());
                                } else {
                                    Eprintf("ERROR: failed to create sample-aes descriptor (" + D((int)result) + ")\n");
                                    return result;
                                }
                            }
                            if (video_track != null) {
                                result = MakeSampleAesVideoDescriptor(descriptor);
                                if (result.Succeeded() && descriptor.GetDataSize() != 0) {
                                    video_stream.SetDescriptor(descriptor.GetData(), descriptor.GetDataSize());
                                } else {
                                    Eprintf("ERROR: failed to create sample-aes descriptor (" + D((int)result) + ")\n");
                                    return result;
                                }
                            }
                        }

                        ts_writer.WritePAT(segment_output);
                        ts_writer.WritePMT(segment_output);
                    } else if (packed_writer != null && chosen_track == audio_track) {
                        DataBuffer private_extension_buffer = new DataBuffer();
                        string     private_extension_name = null;
                        byte[]     private_extension_data = null;
                        uint       private_extension_data_size = 0;
                        if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                            private_extension_name = "com.apple.streaming.audioDescription";
                            result = MakeAudioSetupData(private_extension_buffer, audio_track.GetSampleDescription(0), audio_sample_data);
                            if (result.Failed()) {
                                Eprintf("ERROR: failed to make audio setup data (" + D((int)result) + ")\n");
                                return (Ap4Result)1;
                            }
                            private_extension_data      = private_extension_buffer.GetData();
                            private_extension_data_size = private_extension_buffer.GetDataSize();
                        }
                        packed_writer.WriteHeader(audio_ts,
                                                  private_extension_name,
                                                  private_extension_data,
                                                  private_extension_data_size,
                                                  segment_output);
                    }
                }

                // write the samples out and advance to the next sample
                if (chosen_track == audio_track) {
                    // perform sample-level encryption if needed
                    if (sample_encrypter != null) {
                        result = sample_encrypter.EncryptAudioSample(audio_sample_data, audio_track.GetSampleDescription(audio_sample.GetDescriptionIndex()));
                        if (result.Failed()) {
                            Eprintf("ERROR: failed to encrypt audio sample (" + D((int)result) + ")\n");
                            return result;
                        }
                    }

                    // write the sample data
                    if (audio_stream != null) {
                        result = audio_stream.WriteSample(audio_sample,
                                                          audio_sample_data,
                                                          audio_track.GetSampleDescription(audio_sample.GetDescriptionIndex()),
                                                          video_track==null,
                                                          segment_output);
                    } else if (packed_writer != null) {
                        result = packed_writer.WriteSample(audio_sample,
                                                           audio_sample_data,
                                                           audio_track.GetSampleDescription(audio_sample.GetDescriptionIndex()),
                                                           segment_output);
                    } else {
                        return Ap4Result.ERROR_INTERNAL;
                    }
                    if (result.Failed()) return result;

                    result = ReadSample(audio_reader, audio_track, audio_sample, audio_sample_data, ref audio_ts, ref audio_frame_duration, ref audio_eos);
                    if (result.Failed()) return result;
                    ++audio_sample_count;
                } else if (chosen_track == video_track) {
                    // perform sample-level encryption if needed
                    if (sample_encrypter != null) {
                        result = sample_encrypter.EncryptVideoSample(video_sample_data, nalu_length_size);
                        if (result.Failed()) {
                            Eprintf("ERROR: failed to encrypt video sample (" + D((int)result) + ")\n");
                            return result;
                        }
                    }

                    // write the sample data
                    ulong frame_start = 0;
                    segment_output.Tell(out frame_start);
                    result = video_stream.WriteSample(video_sample,
                                                       video_sample_data,
                                                       video_track.GetSampleDescription(video_sample.GetDescriptionIndex()),
                                                       true,
                                                       segment_output);
                    if (result.Failed()) return result;
                    ulong frame_end = 0;
                    segment_output.Tell(out frame_end);

                    // measure I frames
                    if (video_sample.IsSync()) {
                        ulong frame_size = 0;
                        if (frame_end > frame_start) {
                            frame_size = frame_end-frame_start;
                        }
                        if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_AES_128) {
                            frame_start += segment_position;
                        }
                        iframe_positions.Add(frame_start);
                        iframe_sizes.Add(unchecked((uint)frame_size));
                        iframe_times.Add(video_ts);
                        iframe_segment_indexes.Add(segment_number);
                        iframe_durations.Add(0.0); // will be computed later
                        if (Options.verbose) {
                            Printf("I-Frame: " + D(unchecked((uint)frame_size)) + "@" + LLD(frame_start) + ", t=" + F(video_ts) + "\n");
                        }
                    }

                    // read the next sample
                    result = ReadSample(video_reader, video_track, video_sample, video_sample_data, ref video_ts, ref video_frame_duration, ref video_eos);
                    if (result.Failed()) return result;
                    ++video_sample_count;
                } else {
                    break;
                }
            }

            // create the media playlist/index file
            playlist = OpenOutput(Options.index_filename, 0);
            if (playlist == null) return Ap4Result.ERROR_CANNOT_OPEN_FILE;

            uint   target_duration = 0;
            double total_duration = 0.0;
            for (int i=0; i<segment_durations.Count; i++) {
                if ((uint)(segment_durations[i]+0.5) > target_duration) {
                    target_duration = (uint)segment_durations[i];
                }
                total_duration += segment_durations[i];
            }

            playlist.WriteString("#EXTM3U\r\n");
            if (Options.hls_version > 1) {
                string_buffer = "#EXT-X-VERSION:" + D(Options.hls_version) + "\r\n";
                playlist.WriteString(string_buffer);
            }
            playlist.WriteString("#EXT-X-PLAYLIST-TYPE:VOD\r\n");
            if (video_track != null) {
                playlist.WriteString("#EXT-X-INDEPENDENT-SEGMENTS\r\n");
            }
            if (Options.allow_cache != null) {
                playlist.WriteString("#EXT-X-ALLOW-CACHE:");
                playlist.WriteString(Options.allow_cache);
                playlist.WriteString("\r\n");
            }
            playlist.WriteString("#EXT-X-TARGETDURATION:");
            string_buffer = D(target_duration) + "\r\n";
            playlist.WriteString(string_buffer);
            playlist.WriteString("#EXT-X-MEDIA-SEQUENCE:0\r\n");

            if (Options.encryption_mode != EncryptionMode.ENCRYPTION_MODE_NONE) {
                if (Options.encryption_key_lines.Count != 0) {
                    for (int i=0; i<Options.encryption_key_lines.Count; i++) {
                        string key_line = Options.encryption_key_lines[i];
                        string key_line_cstr = key_line;
                        bool omit_iv = false;

                        // omit the IV if the key line starts with a "!" (and skip the "!")
                        if (key_line.Length > 0 && key_line[0] == '!') {
                            key_line_cstr = key_line.Substring(1);
                            omit_iv = true;
                        }

                        playlist.WriteString("#EXT-X-KEY:METHOD=");
                        if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_AES_128) {
                            playlist.WriteString("AES-128");
                        } else if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                            playlist.WriteString("SAMPLE-AES");
                        }
                        playlist.WriteString(",");
                        playlist.WriteString(key_line_cstr);
                        if ((Options.encryption_iv_mode == EncryptionIvMode.ENCRYPTION_IV_MODE_RANDOM ||
                             Options.encryption_iv_mode == EncryptionIvMode.ENCRYPTION_IV_MODE_FPS) && !omit_iv) {
                            playlist.WriteString(",IV=0x");
                            string iv_hex = IvHex();
                            playlist.WriteString(iv_hex);
                        }
                        playlist.WriteString("\r\n");
                    }
                } else {
                    playlist.WriteString("#EXT-X-KEY:METHOD=");
                    if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_AES_128) {
                        playlist.WriteString("AES-128");
                    } else if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                        playlist.WriteString("SAMPLE-AES");
                    }
                    playlist.WriteString(",URI=\"");
                    playlist.WriteString(Options.encryption_key_uri);
                    playlist.WriteString("\"");
                    if (Options.encryption_iv_mode == EncryptionIvMode.ENCRYPTION_IV_MODE_RANDOM) {
                        playlist.WriteString(",IV=0x");
                        string iv_hex = IvHex();
                        playlist.WriteString(iv_hex);
                    }
                    if (Options.encryption_key_format != null) {
                        playlist.WriteString(",KEYFORMAT=\"");
                        playlist.WriteString(Options.encryption_key_format);
                        playlist.WriteString("\"");
                    }
                    if (Options.encryption_key_format_versions != null) {
                        playlist.WriteString(",KEYFORMATVERSIONS=\"");
                        playlist.WriteString(Options.encryption_key_format_versions);
                        playlist.WriteString("\"");
                    }
                    playlist.WriteString("\r\n");
                }
            }

            for (int i=0; i<segment_durations.Count; i++) {
                if (Options.hls_version >= 3) {
                    string_buffer = "#EXTINF:" + F(segment_durations[i]) + ",\r\n";
                } else {
                    string_buffer = "#EXTINF:" + U((uint)(segment_durations[i]+0.5)) + ",\r\n";
                }
                playlist.WriteString(string_buffer);
                if (Options.output_single_file) {
                    string_buffer = "#EXT-X-BYTERANGE:" + D(segment_sizes[i]) + "@" + LLD(segment_positions[i]) + "\r\n";
                    playlist.WriteString(string_buffer);
                }
                string_buffer = Sprintf(Options.segment_url_template, (uint)i);
                playlist.WriteString(string_buffer);
                playlist.WriteString("\r\n");
            }

            playlist.WriteString("#EXT-X-ENDLIST\r\n");
            Release(playlist);

            // create the iframe playlist/index file
            if (video_track != null && Options.hls_version >= 4) {
                // compute the iframe durations and target duration
                for (int i=0; i<iframe_positions.Count; i++) {
                    double iframe_duration = 0.0;
                    if (i+1 < iframe_positions.Count) {
                        iframe_duration = iframe_times[i+1]-iframe_times[i];
                    } else if (total_duration > iframe_times[i]) {
                        iframe_duration = total_duration-iframe_times[i];
                    }
                    iframe_durations[i] = iframe_duration;
                }
                uint iframes_target_duration = 0;
                for (int i=0; i<iframe_durations.Count; i++) {
                    if ((uint)(iframe_durations[i]+0.5) > iframes_target_duration) {
                        iframes_target_duration = (uint)iframe_durations[i];
                    }
                }

                playlist = OpenOutput(Options.iframe_index_filename, 0);
                if (playlist == null) return Ap4Result.ERROR_CANNOT_OPEN_FILE;

                playlist.WriteString("#EXTM3U\r\n");
                if (Options.hls_version > 1) {
                    string_buffer = "#EXT-X-VERSION:" + D(Options.hls_version) + "\r\n";
                    playlist.WriteString(string_buffer);
                }
                playlist.WriteString("#EXT-X-PLAYLIST-TYPE:VOD\r\n");
                playlist.WriteString("#EXT-X-I-FRAMES-ONLY\r\n");
                playlist.WriteString("#EXT-X-INDEPENDENT-SEGMENTS\r\n");
                playlist.WriteString("#EXT-X-TARGETDURATION:");
                string_buffer = D(iframes_target_duration) + "\r\n";
                playlist.WriteString(string_buffer);
                playlist.WriteString("#EXT-X-MEDIA-SEQUENCE:0\r\n");

                if (Options.encryption_mode != EncryptionMode.ENCRYPTION_MODE_NONE) {
                    playlist.WriteString("#EXT-X-KEY:METHOD=");
                    if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_AES_128) {
                        playlist.WriteString("AES-128");
                    } else if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                        playlist.WriteString("SAMPLE-AES");
                    }
                    playlist.WriteString(",URI=\"");
                    playlist.WriteString(Options.encryption_key_uri);
                    playlist.WriteString("\"");
                    if (Options.encryption_iv_mode == EncryptionIvMode.ENCRYPTION_IV_MODE_RANDOM) {
                        playlist.WriteString(",IV=0x");
                        string iv_hex = IvHex();
                        playlist.WriteString(iv_hex);
                    }
                    if (Options.encryption_key_format != null) {
                        playlist.WriteString(",KEYFORMAT=\"");
                        playlist.WriteString(Options.encryption_key_format);
                        playlist.WriteString("\"");
                    }
                    if (Options.encryption_key_format_versions != null) {
                        playlist.WriteString(",KEYFORMATVERSIONS=\"");
                        playlist.WriteString(Options.encryption_key_format_versions);
                        playlist.WriteString("\"");
                    }
                    playlist.WriteString("\r\n");
                }

                for (int i=0; i<iframe_positions.Count; i++) {
                    string_buffer = "#EXTINF:" + F(iframe_durations[i]) + ",\r\n";
                    playlist.WriteString(string_buffer);
                    string_buffer = "#EXT-X-BYTERANGE:" + D(iframe_sizes[i]) + "@" + LLD(iframe_positions[i]) + "\r\n";
                    playlist.WriteString(string_buffer);
                    string_buffer = Sprintf(Options.segment_url_template, iframe_segment_indexes[i]);
                    playlist.WriteString(string_buffer);
                    playlist.WriteString("\r\n");
                }

                playlist.WriteString("#EXT-X-ENDLIST\r\n");
                Release(playlist);
            }

            // update stats
            Stats.segment_count = (uint)segment_sizes.Count;
            for (int i=0; i<segment_sizes.Count; i++) {
                Stats.segments_total_size     += segment_sizes[i];
                Stats.segments_total_duration += segment_durations[i];
            }
            Stats.iframe_count = (uint)iframe_sizes.Count;
            for (int i=0; i<iframe_sizes.Count; i++) {
                Stats.iframes_total_size += iframe_sizes[i];
            }
            for (int i=0; i<iframe_positions.Count; i++) {
                if (iframe_durations[i] != 0.0) {
                    double iframe_bitrate = 8.0*(double)iframe_sizes[i]/iframe_durations[i];
                    if (iframe_bitrate > Stats.max_iframe_bitrate) {
                        Stats.max_iframe_bitrate = iframe_bitrate;
                    }
                }
            }

            if (Options.verbose) {
                Printf("Conversion complete, total duration=" + F(total_duration, 2) + " secs\n");
            }

            if (segment_output != null) Release(segment_output);

            return result;
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] argv)
        {
            try {
                return DoMain(argv);
            } finally {
                CloseOpenFiles();
                FlushStdout();
            }
        }

        private static int DoMain(string[] argv)
        {
            if (argv.Length < 1) {
                return PrintUsageAndExit();
            }

            // default options
            Options.input                          = null;
            Options.verbose                        = false;
            Options.hls_version                    = 0;
            Options.pmt_pid                        = 0x100;
            Options.audio_pid                      = 0x101;
            Options.video_pid                      = 0x102;
            Options.audio_track_id                 = -1;
            Options.video_track_id                 = -1;
            Options.audio_format                   = AudioFormat.AUDIO_FORMAT_TS;
            Options.output_single_file             = false;
            Options.show_info                      = false;
            Options.index_filename                 = "stream.m3u8";
            Options.iframe_index_filename          = null;
            Options.segment_filename_template      = null;
            Options.segment_url_template           = null;
            Options.segment_duration               = 6;
            Options.segment_duration_threshold     = DefaultSegmentDurationThreshold;
            Options.allow_cache                    = null;
            Options.encryption_key_hex             = null;
            Options.encryption_mode                = EncryptionMode.ENCRYPTION_MODE_NONE;
            Options.encryption_iv_mode             = EncryptionIvMode.ENCRYPTION_IV_MODE_NONE;
            Options.encryption_key_uri             = "key.bin";
            Options.encryption_key_format          = null;
            Options.encryption_key_format_versions = null;
            Options.pcr_offset                     = Ap4.MPEG2_TS_DEFAULT_PCR_OFFSET;
            Array.Clear(Options.encryption_key, 0, Options.encryption_key.Length);
            Array.Clear(Options.encryption_iv,  0, Options.encryption_iv.Length);

            // parse command line
            Ap4Result result;
            int args = 0;
            while (args < argv.Length) {
                string arg = argv[args++];
                string next = args < argv.Length ? argv[args] : null; // *args
                if (arg == "--verbose") {
                    Options.verbose = true;
                } else if (arg == "--hls-version") {
                    if (next == null) {
                        Eprintf("ERROR: --hls-version requires a number\n");
                        return 1;
                    }
                    Options.hls_version = Strtoul(argv[args++]);
                    if (Options.hls_version ==0) {
                        Eprintf("ERROR: --hls-version requires number > 0\n");
                        return 1;
                    }
                } else if (arg == "--segment-duration") {
                    if (next == null) {
                        Eprintf("ERROR: --segment-duration requires a number\n");
                        return 1;
                    }
                    Options.segment_duration = Strtoul(argv[args++]);
                } else if (arg == "--segment-duration-threshold") {
                    if (next == null) {
                        Eprintf("ERROR: --segment-duration-threshold requires a number\n");
                        return 1;
                    }
                    Options.segment_duration_threshold = Strtoul(argv[args++]);
                } else if (arg == "--segment-filename-template") {
                    if (next == null) {
                        Eprintf("ERROR: --segment-filename-template requires an argument\n");
                        return 1;
                    }
                    Options.segment_filename_template = argv[args++];
                } else if (arg == "--segment-url-template") {
                    if (next == null) {
                        Eprintf("ERROR: --segment-url-template requires an argument\n");
                        return 1;
                    }
                    Options.segment_url_template = argv[args++];
                } else if (arg == "--allow-cache") {
                    if (next == null || (next != "NO" && next != "YES")) {
                        Eprintf("ERROR: --allow-cache requires a YES or NO argument\n");
                        return 1;
                    }
                    Options.allow_cache = argv[args++];
                } else if (arg == "--pmt-pid") {
                    if (next == null) {
                        Eprintf("ERROR: --pmt-pid requires a number\n");
                        return 1;
                    }
                    Options.pmt_pid = Strtoul(argv[args++]);
                } else if (arg == "--audio-pid") {
                    if (next == null) {
                        Eprintf("ERROR: --audio-pid requires a number\n");
                        return 1;
                    }
                    Options.audio_pid = Strtoul(argv[args++]);
                } else if (arg == "--video-pid") {
                    if (next == null) {
                        Eprintf("ERROR: --video-pid requires a number\n");
                        return 1;
                    }
                    Options.video_pid = Strtoul(argv[args++]);
                } else if (arg == "--audio-track-id") {
                    if (next == null) {
                        Eprintf("ERROR: --audio-track-id requires a number\n");
                        return 1;
                    }
                    Options.audio_track_id = unchecked((int)Strtoul(argv[args++]));
                } else if (arg == "--audio-format") {
                    if (next == null) {
                        Eprintf("ERROR: --audio-format requires an argument\n");
                        return 1;
                    }
                    string format = argv[args++];
                    if (format == "ts") {
                        Options.audio_format = AudioFormat.AUDIO_FORMAT_TS;
                    } else if (format == "packed") {
                        Options.audio_format = AudioFormat.AUDIO_FORMAT_PACKED;
                    } else {
                        Eprintf("ERROR: unknown audio format\n");
                        return 1;
                    }
                } else if (arg == "--video-track-id") {
                    if (next == null) {
                        Eprintf("ERROR: --video-track-id requires a number\n");
                        return 1;
                    }
                    Options.video_track_id = unchecked((int)Strtoul(argv[args++]));
                } else if (arg == "--pcr-offset") {
                    if (next == null) {
                        Eprintf("ERROR: --pcr-offset requires a number\n");
                        return 1;
                    }
                    Options.pcr_offset = Strtoul(argv[args++]);
                } else if (arg == "--output-single-file") {
                    Options.output_single_file = true;
                } else if (arg == "--index-filename") {
                    if (next == null) {
                        Eprintf("ERROR: --index-filename requires a filename\n");
                        return 1;
                    }
                    Options.index_filename = argv[args++];
                } else if (arg == "--iframe-index-filename") {
                    if (next == null) {
                        Eprintf("ERROR: --iframe-index-filename requires a filename\n");
                        return 1;
                    }
                    Options.iframe_index_filename = argv[args++];
                } else if (arg == "--show-info") {
                    Options.show_info = true;
                } else if (arg == "--encryption-key") {
                    if (next == null) {
                        Eprintf("ERROR: --encryption-key requires an argument\n");
                        return 1;
                    }
                    Options.encryption_key_hex = argv[args++];
                    result = Ap4Utils.ParseHex(Options.encryption_key_hex, Options.encryption_key, 16);
                    if (result.Failed()) {
                        Eprintf("ERROR: invalid hex key\n");
                        return 1;
                    }
                    if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_NONE) {
                        Options.encryption_mode = EncryptionMode.ENCRYPTION_MODE_AES_128;
                    }
                } else if (arg == "--encryption-mode") {
                    if (next == null) {
                        Eprintf("ERROR: --encryption-mode requires an argument\n");
                        return 1;
                    }
                    if (next.StartsWith("AES-128", StringComparison.Ordinal)) {
                        Options.encryption_mode = EncryptionMode.ENCRYPTION_MODE_AES_128;
                    } else if (next.StartsWith("SAMPLE-AES", StringComparison.Ordinal)) {
                        Options.encryption_mode = EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES;
                    } else {
                        Eprintf("ERROR: unknown encryption mode\n");
                        return 1;
                    }
                    ++args;
                } else if (arg == "--encryption-iv-mode") {
                    if (next == null) {
                        Eprintf("ERROR: --encryption-iv-mode requires an argument\n");
                        return 1;
                    }
                    if (next.StartsWith("sequence", StringComparison.Ordinal)) {
                        Options.encryption_iv_mode = EncryptionIvMode.ENCRYPTION_IV_MODE_SEQUENCE;
                    } else if (next.StartsWith("random", StringComparison.Ordinal)) {
                        Options.encryption_iv_mode = EncryptionIvMode.ENCRYPTION_IV_MODE_RANDOM;
                    } else if (next.StartsWith("fps", StringComparison.Ordinal)) {
                        Options.encryption_iv_mode = EncryptionIvMode.ENCRYPTION_IV_MODE_FPS;
                    } else {
                        Eprintf("ERROR: unknown encryption IV mode\n");
                        return 1;
                    }
                    ++args;
                } else if (arg == "--encryption-key-uri") {
                    if (next == null) {
                        Eprintf("ERROR: --encryption-key-uri requires an argument\n");
                        return 1;
                    }
                    Options.encryption_key_uri = argv[args++];
                } else if (arg == "--encryption-key-format") {
                    if (next == null) {
                        Eprintf("ERROR: --encryption-key-format requires an argument\n");
                        return 1;
                    }
                    Options.encryption_key_format = argv[args++];
                } else if (arg == "--encryption-key-format-versions") {
                    if (next == null) {
                        Eprintf("ERROR: --encryption-key-format-versions requires an argument\n");
                        return 1;
                    }
                    Options.encryption_key_format_versions = argv[args++];
                } else if (arg == "--encryption-key-line") {
                    if (next == null) {
                        Eprintf("ERROR: --encryption-key-line requires an argument\n");
                        return 1;
                    }
                    Options.encryption_key_lines.Add(argv[args++]);
                } else if (Options.input == null) {
                    Options.input = arg;
                } else {
                    Eprintf("ERROR: unexpected argument: " + arg + "\n");
                    return 1;
                }
            }

            // check args
            if (Options.input == null) {
                Eprintf("ERROR: missing input file name\n");
                return 1;
            }
            if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_NONE && Options.encryption_key_lines.Count != 0) {
                Eprintf("ERROR: --encryption-key-line requires --encryption-key and --encryption-key-mode\n");
                return 1;
            }
            if (Options.encryption_mode != EncryptionMode.ENCRYPTION_MODE_NONE && Options.encryption_key_hex == null) {
                Eprintf("ERROR: no encryption key specified\n");
                return 1;
            }
            if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES && Options.hls_version > 0 && Options.hls_version < 5) {
                Options.hls_version = 5;
                Eprintf("WARNING: forcing version to 5 in order to support SAMPLE-AES encryption\n");
            }
            if (Options.iframe_index_filename != null && Options.hls_version > 0 && Options.hls_version < 4) {
                Eprintf("WARNING: forcing version to 4 in order to support I-FRAME-ONLY playlists\n");
                Options.hls_version = 4;
            }
            if (Options.encryption_iv_mode == EncryptionIvMode.ENCRYPTION_IV_MODE_NONE && Options.encryption_mode != EncryptionMode.ENCRYPTION_MODE_NONE) {
                if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                    // sequence-mode IVs don't work well with i-frame only playlists, use random instead
                    Options.encryption_iv_mode = EncryptionIvMode.ENCRYPTION_IV_MODE_RANDOM;
                } else {
                    Options.encryption_iv_mode = EncryptionIvMode.ENCRYPTION_IV_MODE_SEQUENCE;
                }
            }
            if ((Options.encryption_key_format != null || Options.encryption_key_format_versions != null) && Options.hls_version > 0 && Options.hls_version < 5) {
                Options.hls_version = 5;
                Eprintf("WARNING: forcing version to 5 in order to support KEYFORMAT and/or KEYFORMATVERSIONS\n");
            }
            if (Options.output_single_file && Options.hls_version > 0 && Options.hls_version < 4) {
                Options.hls_version = 4;
                Eprintf("WARNING: forcing version to 4 in order to support single file output\n");
            }
            if (Options.hls_version == 0) {
                // default version is 3 for cleartext or AES-128 encryption, and 5 for SAMPLE-AES
                if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                    Options.hls_version = 5;
                } else if (Options.output_single_file || Options.iframe_index_filename != null) {
                    Options.hls_version = 4;
                } else {
                    Options.hls_version = 3;
                }
            }

            if (Options.verbose && Options.show_info) {
                Eprintf("WARNING: --verbose will be ignored because --show-info is selected\n");
                Options.verbose = false;
            }

            // compute some derived values
            if (Options.iframe_index_filename == null) {
                if (Options.hls_version >= 4) {
                    Options.iframe_index_filename = "iframes.m3u8";
                }
            }
            if (Options.audio_format == AudioFormat.AUDIO_FORMAT_TS) {
                if (Options.segment_filename_template == null) {
                    if (Options.output_single_file) {
                        Options.segment_filename_template = "stream.ts";
                    } else {
                        Options.segment_filename_template = "segment-%d.ts";
                    }
                }
                if (Options.segment_url_template == null) {
                    if (Options.output_single_file) {
                        Options.segment_url_template = "stream.ts";
                    } else {
                        Options.segment_url_template = "segment-%d.ts";
                    }
                }
            }

            if (Options.encryption_iv_mode == EncryptionIvMode.ENCRYPTION_IV_MODE_FPS) {
                if (Options.encryption_key_hex == null || Ap4Utils.StringLength(Options.encryption_key_hex) != 64) {
                    Eprintf("ERROR: 'fps' IV mode requires a 32 byte key value (64 characters in hex)\n");
                    return 1;
                }
                result = Ap4Utils.ParseHex(Options.encryption_key_hex, 32, Options.encryption_iv, 16);
                if (result.Failed()) {
                    Eprintf("ERROR: invalid hex IV\n");
                    return 1;
                }
            } else if (Options.encryption_iv_mode == EncryptionIvMode.ENCRYPTION_IV_MODE_RANDOM) {
                result = Ap4Utils.System_GenerateRandomBytes(Options.encryption_iv, (uint)Options.encryption_iv.Length);
                if (result.Failed()) {
                    Eprintf("ERROR: failed to get random IV (" + D((int)result) + ")\n");
                    return 1;
                }
            }

            // create the input stream
            ByteStream input = null;
            result = FileByteStream.Create(Options.input, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input (" + D((int)result) + ")\n");
                return 1;
            }

            // open the file
            File input_file = new File(input, true);

            // get the movie
            SampleDescription sample_description;
            Movie movie = input_file.GetMovie();
            if (movie == null) {
                Eprintf("ERROR: no movie in file\n");
                return 1;
            }

            // get the audio and video tracks
            Track  audio_track = null;
            if (Options.audio_track_id == -1) {
                audio_track = movie.GetTrack(Track.Type.TYPE_AUDIO);
            } else if (Options.audio_track_id > 0) {
                audio_track = movie.GetTrack((uint)Options.audio_track_id);
                if (audio_track == null) {
                    Eprintf("ERROR: audio track ID " + D(Options.audio_track_id) + " not found\n");
                    return 1;
                }
                if (audio_track.GetType() != Track.Type.TYPE_AUDIO) {
                    Eprintf("ERROR: track ID " + D(Options.audio_track_id) + " is not an audio track\n");
                    return 1;
                }
            }
            Track video_track = null;
            if (Options.video_track_id == -1) {
                video_track = movie.GetTrack(Track.Type.TYPE_VIDEO);
            } else if (Options.video_track_id > 0) {
                video_track = movie.GetTrack((uint)Options.video_track_id);
                if (video_track == null) {
                    Eprintf("ERROR: video track ID " + D(Options.video_track_id) + " not found\n");
                    return 1;
                }
                if (video_track.GetType() != Track.Type.TYPE_VIDEO) {
                    Eprintf("ERROR: track ID " + D(Options.video_track_id) + " is not a video track\n");
                    return 1;
                }
            }
            if (audio_track == null && video_track == null) {
                Eprintf("ERROR: no suitable tracks found\n");
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return 1;
            }
            if (Options.audio_format == AudioFormat.AUDIO_FORMAT_PACKED && video_track != null) {
                if (audio_track == null) {
                    Eprintf("ERROR: packed audio format requires an audio track\n");
                    return 1;
                }
                Eprintf("WARNING: ignoring video track because of the packed audio format\n");
                video_track = null;
            }
            if (video_track == null) {
                Options.segment_duration_threshold = 0;
            }

            // create the appropriate readers
            LinearReader  linear_reader = null;
            SampleReader audio_reader  = null;
            SampleReader video_reader  = null;
            if (movie.HasFragments()) {
                // create a linear reader to get the samples
                linear_reader = new LinearReader(movie, input);

                if (audio_track != null) {
                    linear_reader.EnableTrack(audio_track.GetId());
                    audio_reader = new FragmentedSampleReader(linear_reader, audio_track.GetId());
                }
                if (video_track != null) {
                    linear_reader.EnableTrack(video_track.GetId());
                    video_reader = new FragmentedSampleReader(linear_reader, video_track.GetId());
                }
            } else {
                if (audio_track != null) {
                    audio_reader = new TrackSampleReader(audio_track);
                }
                if (video_track != null) {
                    video_reader = new TrackSampleReader(video_track);
                }
            }

            Mpeg2TsWriter               ts_writer = null;
            Mpeg2TsWriter.SampleStream  audio_stream = null;
            Mpeg2TsWriter.SampleStream  video_stream = null;
            byte                        nalu_length_size = 0;
            PackedAudioWriter           packed_writer = null;
            if (Options.audio_format == AudioFormat.AUDIO_FORMAT_PACKED) {
                packed_writer = new PackedAudioWriter();

                // figure out the file extensions if needed
                sample_description = audio_track.GetSampleDescription(0);
                if (sample_description == null) {
                    Eprintf("ERROR: unable to parse audio sample description\n");
                    goto end;
                }
                if (Options.segment_filename_template == null || Options.segment_url_template == null) {
                    string default_stream_name    = "stream.es";
                    string default_stream_pattern = "segment-%d.es";
                    if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_MP4A) {
                        MpegAudioSampleDescription mpeg_audio_desc = sample_description as MpegAudioSampleDescription;
                        if (mpeg_audio_desc == null ||
                            !(mpeg_audio_desc.GetObjectTypeId() == Ap4.OTI_MPEG4_AUDIO          ||
                              mpeg_audio_desc.GetObjectTypeId() == Ap4.OTI_MPEG2_AAC_AUDIO_LC   ||
                              mpeg_audio_desc.GetObjectTypeId() == Ap4.OTI_MPEG2_AAC_AUDIO_MAIN)) {
                            Eprintf("ERROR: only AAC audio is supported\n");
                            return 1;
                        }
                        default_stream_name    = "stream.aac";
                        default_stream_pattern = "segment-%d.aac";
                    } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AC_3) {
                        default_stream_name    = "stream.ac3";
                        default_stream_pattern = "segment-%d.ac3";
                    } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_EC_3) {
                        default_stream_name    = "stream.ec3";
                        default_stream_pattern = "segment-%d.ec3";
                    } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AC_4) {
                        default_stream_name    = "stream.ac4";
                        default_stream_pattern = "segment-%d.ac4";
                    }

                    // override the segment names
                    if (Options.segment_filename_template == null) {
                        if (Options.output_single_file) {
                            Options.segment_filename_template = default_stream_name;
                        } else {
                            Options.segment_filename_template = default_stream_pattern;
                        }
                    }
                    if (Options.segment_url_template == null) {
                        if (Options.output_single_file) {
                            Options.segment_url_template = default_stream_name;
                        } else {
                            Options.segment_url_template = default_stream_pattern;
                        }
                    }
                }
            } else {
                // create an MPEG2 TS Writer
                ts_writer = new Mpeg2TsWriter(unchecked((ushort)Options.pmt_pid));

                // add the audio stream
                if (audio_track != null) {
                    sample_description = audio_track.GetSampleDescription(0);
                    if (sample_description == null) {
                        Eprintf("ERROR: unable to parse audio sample description\n");
                        goto end;
                    }

                    uint stream_type = 0;
                    uint stream_id   = 0;
                    if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_MP4A) {
                        if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                            stream_type = AP4_MPEG2_STREAM_TYPE_SAMPLE_AES_ISO_IEC_13818_7;
                        } else {
                            stream_type = Ap4.MPEG2_STREAM_TYPE_ISO_IEC_13818_7;
                        }
                        stream_id   = Ap4.MPEG2_TS_DEFAULT_STREAM_ID_AUDIO;
                    } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AC_3) {
                        if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                            stream_type = AP4_MPEG2_STREAM_TYPE_SAMPLE_AES_ATSC_AC3;
                        } else {
                            stream_type = Ap4.MPEG2_STREAM_TYPE_ATSC_AC3;
                        }
                        stream_id   = Ap4.MPEG2_TS_STREAM_ID_PRIVATE_STREAM_1;
                    } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_EC_3) {
                        if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                            stream_type = AP4_MPEG2_STREAM_TYPE_SAMPLE_AES_ATSC_EAC3;
                        } else {
                            stream_type = Ap4.MPEG2_STREAM_TYPE_ATSC_EAC3;
                        }
                        stream_id   = Ap4.MPEG2_TS_STREAM_ID_PRIVATE_STREAM_1;
                    } else {
                        Eprintf("ERROR: audio codec not supported\n");
                        return 1;
                    }
                    if (stream_type == Ap4.MPEG2_STREAM_TYPE_ATSC_EAC3) {
                        // E-AC-3 descriptor
                        uint number_of_channels = 0;
                        string track_language;
                        Dec3Atom  dec3 = sample_description.GetDetails().GetChild(AtomType.DEC3) as Dec3Atom;
                        BitWriter bits = new BitWriter(8);
                        bits.Write(0xCC, 8);
                        bits.Write(0x06, 8);    // fixed value
                        bits.Write(0xC0, 8);    // reserved, bsid_flag, mainid_flag, asvc_flag, mixinfoexists, substream1_flag, substream2_flag and substream3_flag
                        bits.Write(24, 5);      // reserved, full_service_flag and service_type
                        if (dec3.GetSubStreams()[0].acmod == 0) {
                            number_of_channels = 1;
                        } else if (dec3.GetSubStreams()[0].acmod == 1) {
                            number_of_channels = 0;
                        } else if (dec3.GetSubStreams()[0].acmod == 2) {
                            number_of_channels = 2;
                        } else {
                            number_of_channels = 4;
                        }
                        if (dec3.GetSubStreams()[0].num_dep_sub > 0) {
                            number_of_channels = 5;
                        }
                        bits.Write(number_of_channels, 3);              // number_of_channels
                        bits.Write(4, 3);                               // language_flag, language_flag_2, reserved
                        bits.Write(dec3.GetSubStreams()[0].bsid, 5);    // bsid
                        track_language = audio_track.GetTrackLanguage();
                        byte[] track_language_chars = Ap4Utils.StringToCStringBytes(track_language);
                        if (track_language_chars.Length == 3) {
                            bits.Write(track_language_chars[0], 8);
                            bits.Write(track_language_chars[1], 8);
                            bits.Write(track_language_chars[2], 8);
                        } else {
                            bits.Write(0x75, 8);
                            bits.Write(0x6E, 8);
                            bits.Write(0x64, 8);
                        }
                         // setup the audio stream
                        result = ts_writer.SetAudioStream(audio_track.GetMediaTimeScale(),
                                                          (byte)stream_type,
                                                          (ushort)stream_id,
                                                          out audio_stream,
                                                          unchecked((ushort)Options.audio_pid),
                                                          bits.GetData(), 8,
                                                          Options.pcr_offset);
                    } else {
                    // setup the audio stream
                    result = ts_writer.SetAudioStream(audio_track.GetMediaTimeScale(),
                                                      (byte)stream_type,
                                                      (ushort)stream_id,
                                                      out audio_stream,
                                                      unchecked((ushort)Options.audio_pid),
                                                      null, 0,
                                                      Options.pcr_offset);
                    }
                    if (result.Failed()) {
                        Eprintf("could not create audio stream (" + D((int)result) + ")\n");
                        goto end;
                    }
                }

                // add the video stream
                if (video_track != null) {
                    sample_description = video_track.GetSampleDescription(0);
                    if (sample_description == null) {
                        Eprintf("ERROR: unable to parse video sample description\n");
                        goto end;
                    }

                    // decide on the stream type
                    uint stream_type = 0;
                    uint stream_id   = Ap4.MPEG2_TS_DEFAULT_STREAM_ID_VIDEO;
                    if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AVC1 ||
                        sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AVC2 ||
                        sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AVC3 ||
                        sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AVC4 ||
                        sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_DVAV ||
                        sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_DVA1) {
                        if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                            stream_type = AP4_MPEG2_STREAM_TYPE_SAMPLE_AES_AVC;
                            AvcSampleDescription avc_desc = sample_description as AvcSampleDescription;
                            if (avc_desc == null) {
                                Eprintf("ERROR: not a proper AVC track\n");
                                return 1;
                            }
                            nalu_length_size = avc_desc.GetNaluLengthSize();
                        } else {
                            stream_type = Ap4.MPEG2_STREAM_TYPE_AVC;
                        }
                    } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_HEV1 ||
                               sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_HVC1 ||
                               sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_DVHE ||
                               sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_DVH1) {
                        stream_type = Ap4.MPEG2_STREAM_TYPE_HEVC;
                    } else {
                        Eprintf("ERROR: video codec not supported\n");
                        return 1;
                    }
                    if (Options.encryption_mode == EncryptionMode.ENCRYPTION_MODE_SAMPLE_AES) {
                        if (stream_type != AP4_MPEG2_STREAM_TYPE_SAMPLE_AES_AVC) {
                            Eprintf("ERROR: AES-SAMPLE encryption can only be used with H.264 video\n");
                            return 1;
                        }
                    }

                    // setup the video stream
                    result = ts_writer.SetVideoStream(video_track.GetMediaTimeScale(),
                                                      (byte)stream_type,
                                                      (ushort)stream_id,
                                                      out video_stream,
                                                      unchecked((ushort)Options.video_pid),
                                                      null, 0,
                                                      Options.pcr_offset);
                    if (result.Failed()) {
                        Eprintf("could not create video stream (" + D((int)result) + ")\n");
                        goto end;
                    }
                }
            }

            result = WriteSamples(ts_writer, packed_writer,
                                  audio_track, audio_reader, audio_stream,
                                  video_track, video_reader, video_stream,
                                  Options.segment_duration_threshold,
                                  nalu_length_size);
            if (result.Failed()) {
                Eprintf("ERROR: failed to write samples (" + D((int)result) + ")\n");
            }

            if (Options.show_info) {
                double average_segment_bitrate = 0.0;
                if (Stats.segments_total_duration != 0.0) {
                    average_segment_bitrate = 8.0*(double)Stats.segments_total_size/Stats.segments_total_duration;
                }
                double average_iframe_bitrate = 0.0;
                if (Stats.segments_total_duration != 0.0) {
                    average_iframe_bitrate = 8.0*(double)Stats.iframes_total_size/Stats.segments_total_duration;
                }

                double frame_rate = 0.0;
                if (video_track != null && (Stats.segments_total_duration != 0.0)) {
                    double sample_count = (double)video_track.GetSampleCount();
                    double media_duration = (double)video_track.GetMediaDuration();
                    double timescale = (double)video_track.GetMediaTimeScale();
                    if (media_duration > 0.0) {
                        frame_rate = sample_count/(media_duration/timescale);
                    }
                }

                Printf(
                    "{\n"
                );
                Printf(
                    "  \"stats\": {\n" +
                    "    \"duration\": " + F((double)movie.GetDurationMs()/1000.0) + ",\n" +
                    "    \"avg_segment_bitrate\": " + F(average_segment_bitrate) + ",\n" +
                    "    \"max_segment_bitrate\": " + F(Stats.max_segment_bitrate) + ",\n" +
                    "    \"avg_iframe_bitrate\": " + F(average_iframe_bitrate) + ",\n" +
                    "    \"max_iframe_bitrate\": " + F(Stats.max_iframe_bitrate) + ",\n" +
                    "    \"frame_rate\": " + F(frame_rate) + "\n" +
                    "  }"
                );
                if (audio_track != null) {
                    string codec = "";
                    SampleDescription sdesc = audio_track.GetSampleDescription(0);
                    if (sdesc != null) {
                        sdesc.GetCodecString(out codec);
                    }
                    Printf(
                        ",\n" +
                        "  \"audio\": {\n" +
                        "    \"codec\": \"" + codec + "\"\n" +
                        "  }"
                    );
                }
                if (video_track != null) {
                    string codec = "";
                    ushort width = unchecked((ushort)(video_track.GetWidth()/65536.0));
                    ushort height = unchecked((ushort)(video_track.GetHeight()/65536.0));
                    SampleDescription sdesc = video_track.GetSampleDescription(0);
                    if (sdesc != null) {
                        sdesc.GetCodecString(out codec);
                        VideoSampleDescription video_desc = sdesc as VideoSampleDescription;
                        if (video_desc != null) {
                            width = video_desc.GetWidth();
                            height = video_desc.GetHeight();
                        }
                    }
                    Printf(
                        ",\n" +
                        "  \"video\": {\n" +
                        "    \"codec\": \"" + codec + "\",\n" +
                        "    \"width\": " + D((int)width) + ",\n" +
                        "    \"height\": " + D((int)height) + "\n" +
                        "  }"
                    );
                }
                Printf(
                    "\n" +
                    "}\n"
                );
            }

        end:
            if (input is IDisposable) ((IDisposable)input).Dispose();

            return result == Ap4Result.SUCCESS?0:1;
        }
    }
}
