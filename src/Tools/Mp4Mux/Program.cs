// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Mux/Mp4Mux.cpp
// Original work Copyright 2002-2020 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() / AP4_Debug() output goes through one shared "-stdout"
//   FileByteStream so that the bytes written are exactly those of upstream
//   ('\n' line endings, no encoding translation).
// - upstream edits argv strings in place (it cuts them at ':' and '#' and
//   lower-cases the file extension when the type is inferred from it); the
//   port computes the same strings.
// - `--track` as the very last argument makes upstream append a NULL input
//   name and then read past the end of argv (undefined behaviour); the port
//   treats the missing name as an empty string.
// - streams of imported MP4 files stay open until the output is written
//   (upstream: reference counting keeps them alive through the samples).

using System;
using System.Collections.Generic;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Mux
{

    /*----------------------------------------------------------------------
    |   SampleOrder
    +---------------------------------------------------------------------*/
    internal struct SampleOrder {
        public SampleOrder(uint decode_order, uint display_order) {
            _decodeOrder = decode_order;
            _displayOrder = display_order;
        }
        public uint _decodeOrder;
        public uint _displayOrder;
    }

    /*----------------------------------------------------------------------
    |   Parameter
    +---------------------------------------------------------------------*/
    internal class Parameter {
        public Parameter(string name, string value) {
            _name = name;
            _value = value;
        }
        public string _name;
        public string _value;
    }

    /*----------------------------------------------------------------------
    |   SampleFileStorage
    +---------------------------------------------------------------------*/
    internal class SampleFileStorage : IDisposable
    {
        public static Ap4Result Create(string basename, out SampleFileStorage sample_file_storage)
        {
            sample_file_storage = null;
            SampleFileStorage @object = new SampleFileStorage(basename);
            Ap4Result result = FileByteStream.Create(@object._filename,
                                                     FileByteStream.Mode.STREAM_MODE_WRITE,
                                                     out @object._stream);
            if (result.Failed()) {
                return result;
            }
            sample_file_storage = @object;
            return Ap4Result.SUCCESS;
        }

        // PORT NOTE: destructor upstream
        public void Dispose() {
            if (_stream is IDisposable) ((IDisposable)_stream).Dispose();
            try {
                System.IO.File.Delete(_filename);   // remove(m_Filename.GetChars())
            } catch (Exception) {
            }
        }

        public ByteStream GetStream() { return _stream; }

        private SampleFileStorage(string basename) {
            _stream = null;
            _filename = basename + "_";
        }

        private ByteStream  _stream;
        private string      _filename;
    }

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Elementary Stream Multiplexer - Version 3.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-20020 Axiomatic Systems, LLC";

        private const uint AP4_MUX_DEFAULT_VIDEO_FRAME_RATE = 24;
        private const uint AP4_MUX_READ_BUFFER_SIZE         = 65536;

        /*----------------------------------------------------------------------
        |   globals
        +---------------------------------------------------------------------*/
        private static class Options {
            public static bool verbose;
        }

        // PORT NOTE: streams of the MP4 inputs, kept open until the output is written
        private static readonly List<ByteStream> s_Mp4InputStreams = new List<ByteStream>();

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

        // AP4_Debug (prints on stdout, truncated to the 1024 byte buffer of upstream)
        private static void Debug(string text)
        {
            if (text.Length > Ap4.DEBUG_MAX_BUFFER - 1) {
                text = text.Substring(0, Ap4.DEBUG_MAX_BUFFER - 1);
            }
            Printf(text);
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

        private static bool IsSpace(char c) { return c == ' ' || (c >= '\t' && c <= '\r'); }

        // atoi(str)
        private static int Atoi(string str)
        {
            int i = 0;
            while (i < str.Length && IsSpace(str[i])) ++i;
            bool negative = false;
            if (i < str.Length && (str[i] == '+' || str[i] == '-')) {
                negative = str[i] == '-';
                ++i;
            }
            int value = 0;
            while (i < str.Length && str[i] >= '0' && str[i] <= '9') {
                value = unchecked(value*10 + (str[i]-'0'));
                ++i;
            }
            return negative ? unchecked(-value) : value;
        }

        // strtoul(str, NULL, 10)
        // PORT NOTE: `unsigned long` is 32 bits wide in the reference build of the
        // original tools (and on Windows): values that do not fit give ULONG_MAX.
        private static uint Strtoul(string str)
        {
            int i = 0;
            while (i < str.Length && IsSpace(str[i])) ++i;
            bool negative = false;
            if (i < str.Length && (str[i] == '+' || str[i] == '-')) {
                negative = str[i] == '-';
                ++i;
            }
            uint value = 0;
            bool overflow = false;
            while (i < str.Length && str[i] >= '0' && str[i] <= '9') {
                uint digit = (uint)(str[i]-'0');
                if (value > (uint.MaxValue-digit)/10) {
                    overflow = true;
                } else {
                    value = value*10+digit;
                }
                ++i;
            }
            if (overflow) return uint.MaxValue;
            return negative ? unchecked((uint)(-(int)value)) : value;
        }

        // (unsigned int)value
        // PORT NOTE: the conversion is undefined in C++ when the truncated value
        // does not fit; the reference build of the original tools gives 0 then.
        private static uint DoubleToUInt(double value)
        {
            if (value > -1.0 && value < 4294967296.0) {
                return (uint)value;
            }
            return 0;
        }

        // atof(str): converts the longest valid prefix (decimal or hexadecimal
        // notation, optional exponent, "inf"/"infinity"/"nan"), 0.0 when there is none.
        private static double Atof(string str)
        {
            int i = 0;
            while (i < str.Length && IsSpace(str[i])) ++i;
            int start = i;
            bool negative = false;
            if (i < str.Length && (str[i] == '+' || str[i] == '-')) {
                negative = str[i] == '-';
                ++i;
            }
            string rest = str.Substring(i);
            if (rest.StartsWith("inf", StringComparison.OrdinalIgnoreCase)) {
                return negative ? double.NegativeInfinity : double.PositiveInfinity;
            }
            if (rest.StartsWith("nan", StringComparison.OrdinalIgnoreCase)) {
                return double.NaN;
            }
            if (rest.Length >= 2 && rest[0] == '0' && (rest[1] == 'x' || rest[1] == 'X')) {
                double hex_value;
                if (HexFloat(rest, out hex_value)) {
                    return negative ? -hex_value : hex_value;
                }
            }
            int digits = 0;
            while (i < str.Length && str[i] >= '0' && str[i] <= '9') { ++i; ++digits; }
            if (i < str.Length && str[i] == '.') {
                ++i;
                while (i < str.Length && str[i] >= '0' && str[i] <= '9') { ++i; ++digits; }
            }
            if (digits == 0) return 0.0;
            if (i < str.Length && (str[i] == 'e' || str[i] == 'E')) {
                int j = i+1;
                if (j < str.Length && (str[j] == '+' || str[j] == '-')) ++j;
                if (j < str.Length && str[j] >= '0' && str[j] <= '9') {
                    while (j < str.Length && str[j] >= '0' && str[j] <= '9') ++j;
                    i = j;
                }
            }
            double value;
#if NET6_0_OR_GREATER
            if (!double.TryParse(str.Substring(start, i-start),
                                 NumberStyles.Float,
                                 CultureInfo.InvariantCulture,
                                 out value)) {
#else
            if (!Compat.TryParseDouble(str.Substring(start, i-start), out value)) {
#endif
                return 0.0;
            }
            return value;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c-'0';
            if (c >= 'a' && c <= 'f') return c-'a'+10;
            if (c >= 'A' && c <= 'F') return c-'A'+10;
            return -1;
        }

        // hexadecimal floating point notation: 0x<hex digits>[.<hex digits>][p[+-]<decimal digits>]
        // (str starts with "0x"); returns false when no hex digit follows the prefix
        private static bool HexFloat(string str, out double value)
        {
            value = 0.0;
            int i = 2;
            ulong mantissa = 0;      // the first 15 significant digits
            bool  sticky = false;    // non-zero digits were dropped
            int   significant = 0;
            long  exponent = 0;      // binary exponent
            int   digits = 0;
            bool  after_point = false;
            for (; i < str.Length; i++) {
                if (str[i] == '.' && !after_point) {
                    after_point = true;
                    continue;
                }
                int digit = HexDigit(str[i]);
                if (digit < 0) break;
                ++digits;
                if (significant == 0 && digit == 0) {
                    if (after_point) exponent -= 4;
                    continue;
                }
                if (significant < 15) {
                    mantissa = (mantissa<<4) | (uint)digit;
                    ++significant;
                    if (after_point) exponent -= 4;
                } else {
                    if (digit != 0) sticky = true;
                    if (!after_point) exponent += 4;
                }
            }
            if (digits == 0) return false;
            if (i < str.Length && (str[i] == 'p' || str[i] == 'P')) {
                int j = i+1;
                bool exponent_negative = false;
                if (j < str.Length && (str[j] == '+' || str[j] == '-')) {
                    exponent_negative = str[j] == '-';
                    ++j;
                }
                if (j < str.Length && str[j] >= '0' && str[j] <= '9') {
                    long e = 0;
                    while (j < str.Length && str[j] >= '0' && str[j] <= '9') {
                        if (e < 100000000) e = e*10 + (str[j]-'0');
                        ++j;
                    }
                    exponent += exponent_negative ? -e : e;
                }
            }
            if (mantissa == 0) return true;
            // 60 bits of mantissa + a sticky bit: the conversion to double rounds correctly
            mantissa = (mantissa<<1) | (sticky ? 1UL : 0UL);
            exponent -= 1;
            if (exponent > 100000) exponent = 100000;
            if (exponent < -100000) exponent = -100000;
#if NET6_0_OR_GREATER
            value = Math.ScaleB((double)mantissa, (int)exponent);
#else
            value = Compat.ScaleB((double)mantissa, (int)exponent);
#endif
            return true;
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp4mux [options] --track [<type>:]<input>[#<params>] [--track [<type>:]<input>[#<params>] ...] <output>\n" +
                    "\n" +
                    "  <params>, when specified, are expressd as a comma-separated list of\n" +
                    "  one or more <name>=<value> parameters\n" +
                    "\n" +
                    "Supported types:\n" +
                    "  h264: H264/AVC NAL units\n" +
                    "    optional params:\n" +
                    "      dv_profile: integer number for Dolby vision profile ID (valid value: 9)\n" +
                    "      dv_bc: integer number for Dolby vision BL signal cross-compatibility ID (must be 2 if dv_profile is set to 9)\n" +
                    "      frame_rate: floating point number in frames per second (default=24.0)\n" +
                    "      format: avc1 (default) or avc3  for AVC tracks and Dolby Vision back-compatible tracks\n" +
                    "  h265: H265/HEVC NAL units\n" +
                    "    optional params:\n" +
                    "      dv_profile: integer number for Dolby vision profile ID (valid value: 5,8)\n" +
                    "      dv_bc: integer number for Dolby vision BL signal cross-compatibility ID (mandatory if dv_profile is set to 8)\n" +
                    "      frame_rate: floating point number in frames per second (default=24.0)\n" +
                    "      format: hev1 or hvc1 (default) for HEVC tracks and Dolby Vision backward-compatible tracks\n" +
                    "              dvhe or dvh1 (default) for Dolby vision tracks\n" +
                    "  aac:  AAC in ADTS format\n" +
                    "  ac3:  Dolby Digital\n" +
                    "  ec3:  Dolby Digital Plus\n" +
                    "  ac4:  Dolby AC-4\n" +

                    "  mp4:  MP4 track(s) from an MP4 file\n" +
                    "    optional params:\n" +
                    "      track: audio, video, or integer track ID (default=all tracks)\n" +
                    "\n" +
                    "Common optional parameters for all types:\n" +
                    "  language: language code (3-character ISO 639-2 Alpha-3 code)\n" +
                    "\n" +
                    "If no type is specified for an input, the type will be inferred from the file extension\n" +
                    "\n" +
                    "Options:\n" +
                    "  --verbose: show more details\n");
            return 1;
        }

        /*----------------------------------------------------------------------
        |   ParseParameters
        +---------------------------------------------------------------------*/
        private static Ap4Result ParseParameters(string params_str, List<Parameter> parameters)
        {
            List<string> @params = new List<string>();
            int cursor = 0;
            int start = 0;
            for (;;) {
                // PORT NOTE: *cursor == '\0' is cursor == params_str.Length
                bool at_end = cursor == params_str.Length;
                if (at_end || params_str[cursor] == ',') {
                    string param = params_str.Substring(start, cursor-start);
                    @params.Add(param);
                    if (!at_end) {
                        start = cursor+1;
                    }
                }
                ++cursor;
                if (at_end) break;
            }

            for (int i=0; i<@params.Count; i++) {
                string param = @params[i];
                string name = "";
                string value = "";
                int equal = param.IndexOf('=');
                if (equal >= 0) {
                    name = param.Substring(0, equal);
                    value = param.Substring(equal+1);
                } else {
                    name = param;
                }
                parameters.Add(new Parameter(name, value));
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   ComputeDoviLevel
        +---------------------------------------------------------------------*/
        private static Ap4Result ComputeDoviLevel(uint video_width, uint video_height, double frame_rate, ref uint dv_level)
        {
            double level = (uint)(video_width * video_height) * frame_rate;

            if (level <= 1280*720*24) {
                dv_level = 1;
            } else if (level<= 1280*720*30) {
                dv_level = 2;
            } else if (level <= 1920*1080*24) {
                dv_level = 3;
            } else if (level <= 1920*1080*30) {
                dv_level = 4;
            } else if (level <= 1920*1080*60) {
                dv_level = 5;
            } else if (level <= 3840*2160*24) {
                dv_level = 6;
            } else if (level <= 3840*2160*30) {
                dv_level = 7;
            } else if (level <= 3840*2160*48) {
                dv_level = 8;
            } else if (level <= 3840*2160*60) {
                dv_level = 9;
            } else if (level <= 3840*2160*120) {
                if (video_width == 7680) {
                    dv_level = 11;
                } else {
                    dv_level = 10;
                }
            } else if (level <= 7680*4320*60) {
                dv_level = 12;
            } else if (level <= (double)7680*4320*120) {
                dv_level = 13;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   CheckDoviInputParameters
        +---------------------------------------------------------------------*/
        private static Ap4Result CheckDoviInputParameters(List<Parameter> parameters)
        {
            uint profile = 0;
            uint bc = unchecked((uint)-1);
            uint format = 0;

            //check: profile set correctly
            for (int i=0; i<parameters.Count; i++) {
                if (parameters[i]._name == "dv_profile") {
                    profile = unchecked((uint)Atoi(parameters[i]._value));
                    if ((profile != 5) && (profile != 8) && (profile != 9)) {
                        Eprintf("ERROR: invalid dolby vision profile id\n");
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                }
            }

            //check: bl signal compatibility id value range
            for (int i=0; i<parameters.Count; i++) {
                if (parameters[i]._name == "dv_bc") {
                    bc = unchecked((uint)Atoi(parameters[i]._value));
                    if (bc > 7) {
                        Eprintf("ERROR: invalid dolby vision bl signal compatibility id\n");
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                }
            }

            for (int i=0; i<parameters.Count; i++) {
                if (parameters[i]._name == "format") {
                    if (parameters[i]._value == "hvc1") {
                        format = Ap4.SAMPLE_FORMAT_HVC1;
                    } else if (parameters[i]._value == "hev1") {
                        format = Ap4.SAMPLE_FORMAT_HEV1;
                    } else if (parameters[i]._value == "dvh1") {
                        format = Ap4.SAMPLE_FORMAT_DVH1;
                    } else if (parameters[i]._value == "dvhe") {
                        format = Ap4.SAMPLE_FORMAT_DVHE;
                    } else if (parameters[i]._value == "avc1") {
                        format = Ap4.SAMPLE_FORMAT_AVC1;
                    } else if (parameters[i]._value == "avc3") {
                        format = Ap4.SAMPLE_FORMAT_AVC3;
                    } else if (parameters[i]._value == "dvav") {
                        format = Ap4.SAMPLE_FORMAT_DVAV;
                    } else if (parameters[i]._value == "dva1") {
                        format = Ap4.SAMPLE_FORMAT_DVA1;
                    } else {
                        Eprintf("ERROR: format name is invalid\n");
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                }
            }

            //check: sample entry box name is set correctly

            if (format != 0) {
                if (profile == 5) {
                    // profile 5 does not compatible with other profile, only use dvhe or dvh1
                    if ((format != Ap4.SAMPLE_FORMAT_DVHE) && (format != Ap4.SAMPLE_FORMAT_DVH1)) {
                        Eprintf("ERROR: sample entry name is not correct for profile 5\n");
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                } else if (profile == 8) {
                    // profile 8 has CCID with 1, 2, 4, may be compatible with HDR10, SDR or HLG, should
                    // not use dvhe or dvh1
                    if ((format != Ap4.SAMPLE_FORMAT_HVC1) && (format != Ap4.SAMPLE_FORMAT_HEV1)) {
                        Eprintf("ERROR: sample entry name is not correct for profile 8\n");
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                } else if (profile == 9) {
                    // profile 9 only has CCID with 2, which is SDR compliant, should not use dvav or dva1
                    if ((format != Ap4.SAMPLE_FORMAT_AVC1) && (format != Ap4.SAMPLE_FORMAT_AVC3)) {
                        Eprintf("ERROR: sample entry name is not correct for profile 9\n");
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                }
            }

            //check: for profile 8/9, bl signal compatibility id must be set
            if ((bc == unchecked((uint)-1)) && ((profile == 8) || (profile == 9))) {
                Eprintf("ERROR: dolby vision bl signal compatibility id must be set when profile is 8 or 9\n");
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            //check: for profile 8, bl signal compatibility id must be: 1,2,4
            if ((profile == 8) && (bc != 1) && (bc != 2) && (bc != 4)) {
                Eprintf("ERROR: for dolby vision profile 8, bl signal compatibility id must be: 1, 2 or 4\n");
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            //check: for profile 9, bl signal compatibility id must be: 2
            if ((profile == 9) && (bc != 2) ) {
                Eprintf("ERROR: for dolby vision profile 9, bl signal compatibility id must be 2\n");
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            //check: for profile 5, bl signal compatibility id must be 0
            if ((profile == 5) && (bc != 0) && (bc != unchecked((uint)-1))) {
                Eprintf("ERROR: for dolby vision profile 5, bl signal compatibility id must be 0\n");
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   SortSamples
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream takes a pointer into the array; `start` is the index
        // of the first element of the sub-array here.
        private static void SortSamples(SampleOrder[] array, int start, uint n)
        {
            if (n < 2) {
                return;
            }
            SampleOrder pivot = array[start + (int)(n / 2)];
            int left  = start;
            int right = start + (int)n - 1;
            while (left <= right) {
                if (array[left]._displayOrder < pivot._displayOrder) {
                    ++left;
                    continue;
                }
                if (array[right]._displayOrder > pivot._displayOrder) {
                    --right;
                    continue;
                }
                SampleOrder temp = array[left];
                array[left++] = array[right];
                array[right--] = temp;
            }
            SortSamples(array, start, (uint)(right - start + 1));
            SortSamples(array, left, (uint)(start + (int)n - left));
        }

        /*----------------------------------------------------------------------
        |   GetLanguageFromParameters
        +---------------------------------------------------------------------*/
        private static string GetLanguageFromParameters(List<Parameter> parameters, string defaut_value)
        {
            // check if we have a language parameter
            for (int i=0; i<parameters.Count; i++) {
                if (parameters[i]._name == "language") {
                    string language = parameters[i]._value;

                    // the language must be a 3-character ISO 639-2 Alpha-3 code
                    if (Ap4Utils.StringLength(language) != 3) {
                        Eprintf("ERROR: language codes must be 3-character ISO 639-2 Alpha-3 codes\n");
                        return null;
                    }

                    return language;
                }
            }

            return defaut_value;
        }

        /*----------------------------------------------------------------------
        |   AddAacTrack
        +---------------------------------------------------------------------*/
        private static void AddAacTrack(Movie             movie,
                                        string            input_name,
                                        List<Parameter>   parameters,
                                        SampleFileStorage sample_storage)
        {
            ByteStream input;
            Ap4Result result = FileByteStream.Create(input_name, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file '" + input_name + "' (" + D((int)result) + "))\n");
                return;
            }

            // check if we have a language parameter
            string language = GetLanguageFromParameters(parameters, "und");
            if (language == null) return;

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
            byte[] input_buffer = new byte[AP4_MUX_READ_BUFFER_SIZE];
            for(;;) {
                // try to get a frame
                AacFrame frame = new AacFrame();
                result = parser.FindFrame(frame);
                if (result.Succeeded()) {
                    if (Options.verbose) {
                        Printf("AAC frame [" + D06(sample_count) + "]: size = " + D(frame._info._frameLength) +
                               ", " + D(unchecked((int)frame._info._samplingFrequency)) + " kHz, " +
                               D(frame._info._channelConfiguration) + " ch\n");
                    }
                    if (!initialized) {
                        initialized = true;

                        // create a sample description for our samples
                        DataBuffer dsi = new DataBuffer();
                        byte[] aac_dsi = new byte[2];

                        uint object_type = 2; // AAC LC by default
                        aac_dsi[0] = unchecked((byte)((object_type<<3) | (frame._info._samplingFrequencyIndex>>1)));
                        aac_dsi[1] = unchecked((byte)(((frame._info._samplingFrequencyIndex&1)<<7) | (frame._info._channelConfiguration<<3)));

                        dsi.SetData(aac_dsi, 2);
                        MpegAudioSampleDescription sample_description =
                            new MpegAudioSampleDescription(
                            Ap4.OTI_MPEG4_AUDIO,   // object type
                            (uint)frame._info._samplingFrequency,
                            16,                    // sample size
                            unchecked((ushort)frame._info._channelConfiguration),
                            dsi,                   // decoder info
                            6144,                  // buffer size
                            128000,                // max bitrate
                            128000);               // average bitrate
                        sample_description_index = sample_table.GetSampleDescriptionCount();
                        sample_table.AddSampleDescription(sample_description);
                        sample_rate = (uint)frame._info._samplingFrequency;
                    }

                    // read and store the sample data
                    ulong position = 0;
                    sample_storage.GetStream().Tell(out position);
                    DataBuffer sample_data = new DataBuffer(frame._info._frameLength);
                    sample_data.SetDataSize(frame._info._frameLength);
                    frame._source.ReadBytes(sample_data.UseData(), frame._info._frameLength);
                    sample_storage.GetStream().Write(sample_data.GetData(), frame._info._frameLength);

                    // add the sample to the table
                    sample_table.AddSample(sample_storage.GetStream(), position, frame._info._frameLength, 1024, sample_description_index, 0, 0, true);
                    sample_count++;
                } else {
                    if (result == Ap4.ERROR_CORRUPTED_BITSTREAM) {
                        Eprintf("WARN: The stream in corrupted, muxing stopped. The muxed MP4 only contains the first " + D(sample_count) + " samples.\n");
                        break;
                    } else if ((result == Ap4Result.ERROR_NOT_ENOUGH_DATA) && (parser.GetBytesAvailable() == (Ap4.BITSTREAM_BUFFER_SIZE -1))){
                        Eprintf("WARN: The frame " + D(sample_count + 1) + " size is larger than the max buffer size, muxing stopped. The muxed MP4 only contains the first " + D(sample_count) + " samples.\n");
                        break;
                    }
                    if (eos) break;
                }

                // read some data and feed the parser
                uint to_read = parser.GetBytesFree();
                if (to_read != 0) {
                    uint bytes_read = 0;
                    if (to_read > AP4_MUX_READ_BUFFER_SIZE) to_read = AP4_MUX_READ_BUFFER_SIZE;
                    result = input.ReadPartial(input_buffer, to_read, out bytes_read);
                    if (result.Succeeded()) {
                        uint to_feed = bytes_read;
                        result = parser.Feed(input_buffer, ref to_feed);
                        if (result.Failed()) {
                            Debug("ERROR: parser.Feed() failed (" + D((int)result) + ")\n");
                            return;
                        }
                    } else {
                        if (result == Ap4Result.ERROR_EOS) {
                            eos = true;
                            uint no_size = 0; // PORT NOTE: NULL buffer and NULL size pointer upstream
                            parser.Feed(null, ref no_size, Ap4.BITSTREAM_FLAG_EOS);
                        }
                    }
                }
            }

            // create an audio track
            Track track = new Track(Track.Type.TYPE_AUDIO,
                                    sample_table,
                                    0,                 // track id
                                    sample_rate,       // movie time scale
                                    sample_count*1024, // track duration
                                    sample_rate,       // media time scale
                                    sample_count*1024, // media duration
                                    language,          // language
                                    0, 0);             // width, height

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();

            movie.AddTrack(track);
        }

        /*----------------------------------------------------------------------
        |   AddAc3Track
        +---------------------------------------------------------------------*/
        private static void AddAc3Track(Movie             movie,
                                        string            input_name,
                                        List<Parameter>   parameters,
                                        SampleFileStorage sample_storage)
        {
            ByteStream input;
            Ap4Result result = FileByteStream.Create(input_name, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file '" + input_name + "' (" + D((int)result) + "))\n");
                return;
            }

            // check if we have a language parameter
            string language = GetLanguageFromParameters(parameters, "und");
            if (language == null) return;

            // create a sample table
            SyntheticSampleTable sample_table = new SyntheticSampleTable(); // chunk_size is used to control chunk size in 'stsc' box

            // create an AC-3 Sync Frame parser
            Ac3Parser parser = new Ac3Parser();
            bool      initialized = false;
            uint      sample_description_index = 0;

            // read from the input, feed, and get AC-3 frames
            uint sample_rate = 0;
            uint sample_count = 0;

            bool eos = false;
            byte[] input_buffer = new byte[AP4_MUX_READ_BUFFER_SIZE];
            for(;;) {
                // try to get a frame
                Ac3Frame frame = new Ac3Frame();
                result = parser.FindFrame(frame);
                if (result.Succeeded()) {
                    if (Options.verbose) {
                        Printf("AC-3 frame [" + D06(sample_count) + "]: size = " + D(frame._info._frameSize) +
                               ", " + D(frame._info._sampleRate) + " kHz, " +
                               D(frame._info._channelCount) + " ch\n");
                    }
                    if (!initialized) {
                        initialized = true;

                        // create a sample description for our samples
                        Dac3Atom.StreamInfo ac3_stream_info = frame._info._ac3StreamInfo;

                        Ac3SampleDescription sample_description =
                        new Ac3SampleDescription(
                                                 frame._info._sampleRate,       // sample rate
                                                 16,                            // sample size
                                                 2,                             // channel count
                                                 frame._info._frameSize,        // Access Unit size
                                                 ac3_stream_info);              // AC-3 SubStream

                        sample_description_index = sample_table.GetSampleDescriptionCount();
                        sample_table.AddSampleDescription(sample_description);
                        sample_rate      = frame._info._sampleRate;
                    }

                    // read and store the sample data
                    ulong position = 0;
                    sample_storage.GetStream().Tell(out position);
                    DataBuffer sample_data = new DataBuffer(frame._info._frameSize);
                    sample_data.SetDataSize(frame._info._frameSize);
                    frame._source.ReadBytes(sample_data.UseData(), frame._info._frameSize);
                    if (frame._littleEndian != 0) {
                        Ap4Utils.ByteSwap16(sample_data.UseData(), (uint)frame._info._frameSize);
                    }
                    sample_storage.GetStream().Write(sample_data.GetData(), frame._info._frameSize);

                    // add the sample to the table
                    sample_table.AddSample(sample_storage.GetStream(), position, frame._info._frameSize, 1536, sample_description_index, 0, 0, true);
                    sample_count++;
                } else {
                    if (eos) break;
                }

                // read some data and feed the parser
                uint to_read = parser.GetBytesFree();
                if (to_read != 0) {
                    uint bytes_read = 0;
                    if (to_read > AP4_MUX_READ_BUFFER_SIZE) to_read = AP4_MUX_READ_BUFFER_SIZE;
                    result = input.ReadPartial(input_buffer, to_read, out bytes_read);
                    if (result.Succeeded()) {
                        uint to_feed = bytes_read;
                        result = parser.Feed(input_buffer, ref to_feed);
                        if (result.Failed()) {
                            Debug("ERROR: parser.Feed() failed (" + D((int)result) + ")\n");
                            return;
                        }
                    } else {
                        if (result == Ap4Result.ERROR_EOS) {
                            eos = true;
                            uint no_size = 0; // PORT NOTE: NULL buffer and NULL size pointer upstream
                            parser.Feed(null, ref no_size, Ap4.BITSTREAM_FLAG_EOS);
                        }
                    }
                }
            }

            // create an audio track
            Track track = new Track(Track.Type.TYPE_AUDIO,
                                    sample_table,
                                    0,                   // track id
                                    sample_rate,         // movie time scale
                                    sample_count * 1536, // track duration
                                    sample_rate,         // media time scale
                                    sample_count * 1536, // media duration
                                    language,            // language
                                    0, 0);

            // add an edit list with MediaTime==0 to ac3 track defautly.
            if (true) {
                // create an 'edts' container
                ContainerAtom new_edts = new ContainerAtom(AtomType.EDTS);
                ElstAtom new_elst = new ElstAtom();
                ulong duration = 0;
                if (movie.GetTimeScale() == 0) {
                    duration = sample_count * 1536;
                } else {
                    duration = Ap4Utils.ConvertTime(1536*sample_table.GetSampleCount(), sample_rate, movie.GetTimeScale());
                }
                ElstEntry new_elst_entry = new ElstEntry(duration, 0, 1);
                new_elst.AddEntry(new_elst_entry);
                new_edts.AddChild(new_elst);
                track.UseTrakAtom().AddChild(new_edts, 1);
            }

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();

            movie.AddTrack(track);
        }

        /*----------------------------------------------------------------------
        |   AddEac3Track
        +---------------------------------------------------------------------*/
        private static void AddEac3Track(Movie             movie,
                                         string            input_name,
                                         List<Parameter>   parameters,
                                         SampleFileStorage sample_storage)
        {
            ByteStream input;
            Ap4Result result = FileByteStream.Create(input_name, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file '" + input_name + "' (" + D((int)result) + "))\n");
                return;
            }

            // check if we have a language parameter
            string language = GetLanguageFromParameters(parameters, "und");
            if (language == null) return;

            // create a sample table
            SyntheticSampleTable sample_table = new SyntheticSampleTable(); // The parameter chunk_size is used to control chunk size in 'stsc' box

            // create an E-AC-3 Sync Frame parser
            Eac3Parser parser = new Eac3Parser();
            bool       initialized = false;
            uint       sample_description_index = 0;

            // read from the input, feed, and get E-AC-3 frames
            uint sample_rate = 0;
            uint sample_count = 0;

            bool eos = false;
            byte[] input_buffer = new byte[AP4_MUX_READ_BUFFER_SIZE];
            for(;;) {
                // try to get a frame
                Eac3Frame frame = new Eac3Frame();
                result = parser.FindFrame(frame);
                if (result.Succeeded()) {
                    if (Options.verbose) {
                        Printf("E-AC-3 frame [" + D06(sample_count) + "]: size = " + D(frame._info._frameSize) +
                               ", " + D(frame._info._sampleRate) + " kHz, " +
                               D(frame._info._channelCount) + " ch\n");
                    }
                    if (!initialized) {
                        initialized = true;

                        // create a sample description for our samples
                        Dec3Atom.SubStream eac3_substream = frame._info._eac3SubStream;
                        uint obj_num = frame._info.complexity_index_type_a;

                        Eac3SampleDescription sample_description =
                            new Eac3SampleDescription(
                            frame._info._sampleRate,      // sample rate
                            16,                           // sample size
                            2,                            // channel count
                            frame._info._frameSize,       // Access Unit size
                            eac3_substream,               // E-AC-3 SubStream
                            obj_num);                     // DD+JOC object numbers
                        sample_description_index = sample_table.GetSampleDescriptionCount();
                        sample_table.AddSampleDescription(sample_description);
                        sample_rate      = frame._info._sampleRate;
                    }

                    // read and store the sample data
                    ulong position = 0;
                    sample_storage.GetStream().Tell(out position);
                    DataBuffer sample_data = new DataBuffer(frame._info._frameSize);
                    sample_data.SetDataSize(frame._info._frameSize);
                    frame._source.ReadBytes(sample_data.UseData(), frame._info._frameSize);
                    if (frame._littleEndian != 0) {
                        Ap4Utils.ByteSwap16(sample_data.UseData(), (uint)frame._info._frameSize);
                    }
                    sample_storage.GetStream().Write(sample_data.GetData(), frame._info._frameSize);

                    // add the sample to the table
                    sample_table.AddSample(sample_storage.GetStream(), position, frame._info._frameSize, 1536, sample_description_index, 0, 0, true);
                    sample_count++;
                } else {
                    if (result == Ap4.ERROR_CORRUPTED_BITSTREAM) {
                        Eprintf("WARN: The stream in corrupted, so stop muxing. The muxed MP4 only contains the first " + D(sample_count) + " samples.\n");
                        break;
                    } else if ((result == Ap4Result.ERROR_NOT_ENOUGH_DATA) && (parser.GetBytesAvailable() == (Ap4.BITSTREAM_BUFFER_SIZE -1))){
                        Eprintf("WARN: The frame " + D(sample_count + 1) + " size is larger than pre-defined buffer size (8191 bytes), so stop muxing. The muxed MP4 only contains the first " + D(sample_count) + " samples.\n");
                        break;
                    }
                    if (eos) break;
                }

                // read some data and feed the parser
                uint to_read = parser.GetBytesFree();
                if (to_read != 0) {
                    uint bytes_read = 0;
                    if (to_read > AP4_MUX_READ_BUFFER_SIZE) to_read = AP4_MUX_READ_BUFFER_SIZE;
                    result = input.ReadPartial(input_buffer, to_read, out bytes_read);
                    if (result.Succeeded()) {
                        uint to_feed = bytes_read;
                        result = parser.Feed(input_buffer, ref to_feed);
                        if (result.Failed()) {
                            Debug("ERROR: parser.Feed() failed (" + D((int)result) + ")\n");
                            return;
                        }
                    } else {
                        if (result == Ap4Result.ERROR_EOS) {
                            eos = true;
                            uint no_size = 0; // PORT NOTE: NULL buffer and NULL size pointer upstream
                            parser.Feed(null, ref no_size, Ap4.BITSTREAM_FLAG_EOS);
                        }
                    }
                }
            }

            // create an audio track
            Track track = new Track(Track.Type.TYPE_AUDIO,
                                    sample_table,
                                    0,                   // track id
                                    sample_rate,         // movie time scale
                                    sample_count * 1536, // track duration
                                    sample_rate,         // media time scale
                                    sample_count * 1536, // media duration
                                    language,            // language
                                    0, 0);               // width, height

            // add an edit list with MediaTime==0 to ec3 track defautly.
            if (true) {
                // create an 'edts' container
                ContainerAtom new_edts = new ContainerAtom(AtomType.EDTS);
                ElstAtom new_elst = new ElstAtom();
                ulong duration = 0;
                if (movie.GetTimeScale() == 0) {
                    duration = sample_count * 1536;
                } else {
                    duration = Ap4Utils.ConvertTime(1536*sample_table.GetSampleCount(), sample_rate, movie.GetTimeScale());
                }
                ElstEntry new_elst_entry = new ElstEntry(duration, 0, 1);
                new_elst.AddEntry(new_elst_entry);
                new_edts.AddChild(new_elst);
                track.UseTrakAtom().AddChild(new_edts, 1);
            }

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();

            movie.AddTrack(track);
        }

        /*----------------------------------------------------------------------
        |   AddAc4Track
        +---------------------------------------------------------------------*/
        private static void AddAc4Track(Movie             movie,
                                        string            input_name,
                                        List<Parameter>   parameters,
                                        SampleFileStorage sample_storage)
        {
            ByteStream input;
            Ap4Result result = FileByteStream.Create(input_name, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file '" + input_name + "' (" + D((int)result) + "))\n");
                return;
            }

            // check if we have a language parameter
            string language = GetLanguageFromParameters(parameters, "und");
            if (language == null) return;

            // create a sample table
            SyntheticSampleTable sample_table = new SyntheticSampleTable(); // The parameter chunk_size is used to control chunk size in 'stsc' box

            // create an AC-4 Sync Frame parser
            Ac4Parser parser = new Ac4Parser();
            bool      initialized = false;
            uint      sample_description_index = 0;

            // read from the input, feed, and get AC-4 frames
            // AP4_UI32     sample_rate = 0;
            uint sample_count = 0;
            uint sample_duration = 0;
            uint media_time_scale = 0;
            bool eos = false;
            byte[] input_buffer = new byte[AP4_MUX_READ_BUFFER_SIZE];
            for(;;) {
                // try to get a frame
                Ac4Frame frame = new Ac4Frame();
                result = parser.FindFrame(frame);
                if (result.Succeeded()) {
                    if (Options.verbose) {
                        Printf("AC-4 frame [" + D06(sample_count) + "]: size = " + D(frame._info._frameSize) +
                               ", " + D(unchecked((int)frame._info._ac4Dsi.d.v1.fs)) + " kHz, " +
                               D(frame._info._channelCount) + " ch\n");
                    }
                    if (!initialized) {
                        initialized = true;

                        // create a sample description for our samples
                        Dac4Atom.Ac4Dsi ac4Dsi = frame._info._ac4Dsi;

                        Ac4SampleDescription sample_description =
                            new Ac4SampleDescription(
                            frame._info._ac4Dsi.d.v1.fs,                   // sample rate
                            16,                                            // sample size
                            unchecked((ushort)frame._info._channelCount),  // channel count
                            frame._info._frameSize,                        // DIS size, can't calcuate the DSI size in advance, so assume the maximum value is frame size.
                            ac4Dsi);                                       // AC-4 DSI
                        sample_description_index = sample_table.GetSampleDescriptionCount();
                        sample_table.AddSampleDescription(sample_description);
                        /* sample_rate = frame.m_Info.m_Ac4Dsi.d.v1.fs */;
                        sample_duration  = frame._info._sampleDuration;
                        media_time_scale = frame._info._mediaTimeScale;

                    }

                    // read and store the sample data
                    ulong position = 0;
                    sample_storage.GetStream().Tell(out position);
                    DataBuffer sample_data = new DataBuffer(frame._info._frameSize);
                    sample_data.SetDataSize(frame._info._frameSize);
                    frame._source.ReadBytes(sample_data.UseData(), frame._info._frameSize);
                    sample_storage.GetStream().Write(sample_data.GetData(), frame._info._frameSize);

                    // add the sample to the table
                    sample_table.AddSample(sample_storage.GetStream(), position, frame._info._frameSize, sample_duration, sample_description_index, 0, 0, (frame._info._iframe == 1));
                    sample_count++;
                    // Skip the CRC word for 0xAC41 stream
                    frame._source.SkipBytes(frame._info._CRCSize);
                } else {
                    if (result == Ap4.ERROR_CORRUPTED_BITSTREAM) {
                        Eprintf("WARN: The stream in corrupted, so stop muxing. The muxed MP4 only contains the first " + D(sample_count) + " samples.\n");
                        break;
                    } else if ((result == Ap4Result.ERROR_NOT_ENOUGH_DATA) && (parser.GetBytesAvailable() == (Ap4.BITSTREAM_BUFFER_SIZE -1))){
                        Eprintf("WARN: The frame " + D(sample_count + 1) + " size is larger than pre-defined buffer size (8191 bytes), so stop muxing. The muxed MP4 only contains the first " + D(sample_count) + " samples.\n");
                        break;
                    }
                    if (eos) break;
                }

                // read some data and feed the parser
                uint to_read = parser.GetBytesFree();
                if (to_read != 0) {
                    uint bytes_read = 0;
                    if (to_read > AP4_MUX_READ_BUFFER_SIZE) to_read = AP4_MUX_READ_BUFFER_SIZE;
                    result = input.ReadPartial(input_buffer, to_read, out bytes_read);
                    if (result.Succeeded()) {
                        uint to_feed = bytes_read;
                        result = parser.Feed(input_buffer, ref to_feed);
                        if (result.Failed()) {
                            Debug("ERROR: parser.Feed() failed (" + D((int)result) + ")\n");
                            return;
                        }
                    } else {
                        if (result == Ap4Result.ERROR_EOS) {
                            eos = true;
                            uint no_size = 0; // PORT NOTE: NULL buffer and NULL size pointer upstream
                            parser.Feed(null, ref no_size, Ap4.BITSTREAM_FLAG_EOS);
                        }
                    }
                }
            }

            // create an audio track
            Track track = new Track(Track.Type.TYPE_AUDIO,
                                    sample_table,
                                    0,                                     // track id
                                    media_time_scale,                      // movie time scale
                                    (ulong)(sample_count) * sample_duration, // track duration
                                    media_time_scale,                      // media time scale
                                    (ulong)(sample_count) * sample_duration, // media duration
                                    language,                              // language
                                    0, 0);                                 // width, height

            // add an edit list with MediaTime==0 to ac4 track defautly.
            if (true) {
                // create an 'edts' container
                ContainerAtom new_edts = new ContainerAtom(AtomType.EDTS);
                ElstAtom new_elst = new ElstAtom();
                ulong duration = 0;
                if (movie.GetTimeScale() == 0) {
                    duration = (ulong)(sample_count) * sample_duration;
                } else {
                    duration = Ap4Utils.ConvertTime((ulong)(sample_duration)*sample_table.GetSampleCount(), media_time_scale, movie.GetTimeScale());
                }
                ElstEntry new_elst_entry = new ElstEntry(duration, 0, 1);
                new_elst.AddEntry(new_elst_entry);
                new_edts.AddChild(new_elst);
                track.UseTrakAtom().AddChild(new_edts, 1);
            }

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();

            movie.AddTrack(track);
        }

        /*----------------------------------------------------------------------
        |   AddH264Track
        +---------------------------------------------------------------------*/
        private static void AddH264Track(Movie             movie,
                                         string            input_name,
                                         List<Parameter>   parameters,
                                         List<uint>        brands,
                                         SampleFileStorage sample_storage)
        {
            ByteStream input;
            Ap4Result result = FileByteStream.Create(input_name, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file '" + input_name + "' (" + D((int)result) + "))\n");
                return;
            }

            // check if we have a language parameter
            string language = GetLanguageFromParameters(parameters, "und");
            if (language == null) return;

            // see if the frame rate is specified
            uint video_frame_rate = AP4_MUX_DEFAULT_VIDEO_FRAME_RATE*1000;
            for (int i=0; i<parameters.Count; i++) {
                if (parameters[i]._name == "frame_rate") {
                    double frame_rate = Atof(parameters[i]._value);
                    if (frame_rate == 0.0) {
                        Eprintf("ERROR: invalid video frame rate " + parameters[i]._value + "\n");
                        if (input is IDisposable) ((IDisposable)input).Dispose();
                        return;
                    }
                    video_frame_rate = DoubleToUInt(1000.0*frame_rate);
                }
            }

            // create a sample table
            SyntheticSampleTable sample_table = new SyntheticSampleTable();

            // allocate an array to keep track of sample order
            List<SampleOrder> sample_orders_list = new List<SampleOrder>();

            // parse the input
            AvcFrameParser parser = new AvcFrameParser();
            byte[] input_buffer = new byte[AP4_MUX_READ_BUFFER_SIZE];
            for (;;) {
                bool eos;
                uint bytes_in_buffer = 0;
                result = input.ReadPartial(input_buffer, AP4_MUX_READ_BUFFER_SIZE, out bytes_in_buffer);
                if (result.Succeeded()) {
                    eos = false;
                } else if (result == Ap4Result.ERROR_EOS) {
                    eos = true;
                } else {
                    Eprintf("ERROR: failed to read from input file\n");
                    break;
                }
                uint offset = 0;
                bool found_access_unit = false;
                do {
                    AvcFrameParser.AccessUnitInfo access_unit_info = new AvcFrameParser.AccessUnitInfo();

                    found_access_unit = false;
                    uint bytes_consumed = 0;
                    result = parser.Feed(input_buffer,
                                         (int)offset,
                                         bytes_in_buffer,
                                         out bytes_consumed,
                                         access_unit_info,
                                         eos);
                    if (result.Failed()) {
                        Eprintf("ERROR: Feed() failed (" + D((int)result) + ")\n");
                        break;
                    }
                    if (access_unit_info.nal_units.Count != 0) {
                        // we got one access unit
                        found_access_unit = true;
                        if (Options.verbose) {
                            Printf("H264 Access Unit, " + D(access_unit_info.nal_units.Count) + " NAL units, decode_order=" +
                                   D(access_unit_info.decode_order) + ", display_order=" +
                                   D(access_unit_info.display_order) + "\n");
                        }

                        // compute the total size of the sample data
                        uint sample_data_size = 0;
                        for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                            sample_data_size += 4+access_unit_info.nal_units[i].GetDataSize();
                        }

                        // store the sample data
                        ulong position = 0;
                        sample_storage.GetStream().Tell(out position);
                        for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                            sample_storage.GetStream().WriteUI32(access_unit_info.nal_units[i].GetDataSize());
                            sample_storage.GetStream().Write(access_unit_info.nal_units[i].GetData(), access_unit_info.nal_units[i].GetDataSize());
                        }

                        // add the sample to the track
                        sample_table.AddSample(sample_storage.GetStream(), position, sample_data_size, 1000, 0, 0, 0, access_unit_info.is_idr);

                        // remember the sample order
                        sample_orders_list.Add(new SampleOrder(access_unit_info.decode_order, access_unit_info.display_order));

                        // free the memory buffers
                        access_unit_info.Reset();
                    }

                    offset += bytes_consumed;
                    bytes_in_buffer -= bytes_consumed;
                } while (bytes_in_buffer != 0 || found_access_unit);
                if (eos) break;
            }

            // adjust the sample CTS/DTS offsets based on the sample orders
            SampleOrder[] sample_orders = sample_orders_list.ToArray();
            if (sample_orders.Length > 1) {
                uint start = 0;
                for (uint i=1; i<=sample_orders.Length; i++) {
                    if (i == sample_orders.Length || sample_orders[i]._displayOrder == 0) {
                        // we got to the end of the GOP, sort it by display order
                        SortSamples(sample_orders, (int)start, i-start);
                        start = i;
                    }
                }
            }
            uint max_delta = 0;
            for (uint i=0; i<sample_orders.Length; i++) {
                if (sample_orders[i]._decodeOrder > i) {
                    uint delta =sample_orders[i]._decodeOrder-i;
                    if (delta > max_delta) {
                        max_delta = delta;
                    }
                }
            }
            for (uint i=0; i<sample_orders.Length; i++) {
                sample_table.UseSample(sample_orders[i]._decodeOrder).SetCts(1000UL*(ulong)(i+max_delta));
            }

            // check the video parameters
            AvcSequenceParameterSet sps = null;
            for (uint i=0; i<=Ap4.AVC_SPS_MAX_ID; i++) {
                if (parser.GetSequenceParameterSets()[i] != null) {
                    sps = parser.GetSequenceParameterSets()[i];
                    break;
                }
            }
            if (sps == null) {
                Eprintf("ERROR: no sequence parameter set found in video\n");
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return;
            }
            uint video_width = 0;
            uint video_height = 0;
            sps.GetInfo(out video_width, out video_height);
            if (Options.verbose) {
                Printf("VIDEO: " + D(video_width) + "x" + D(video_height) + "\n");
            }

            // collect the SPS and PPS into arrays
            List<DataBuffer> sps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.AVC_SPS_MAX_ID; i++) {
                if (parser.GetSequenceParameterSets()[i] != null) {
                    sps_array.Add(new DataBuffer(parser.GetSequenceParameterSets()[i].raw_bytes));
                }
            }
            List<DataBuffer> pps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.AVC_PPS_MAX_ID; i++) {
                if (parser.GetPictureParameterSets()[i] != null) {
                    pps_array.Add(new DataBuffer(parser.GetPictureParameterSets()[i].raw_bytes));
                }
            }

            // setup the video the sample descripton
            AvcSampleDescription sample_description =
                new AvcSampleDescription(Ap4.SAMPLE_FORMAT_AVC1,
                                         unchecked((ushort)video_width),
                                         unchecked((ushort)video_height),
                                         24,
                                         "AVC Coding",
                                         unchecked((byte)sps.profile_idc),
                                         unchecked((byte)sps.level_idc),
                                         unchecked((byte)(sps.constraint_set0_flag<<7 |
                                                          sps.constraint_set1_flag<<6 |
                                                          sps.constraint_set2_flag<<5 |
                                                          sps.constraint_set3_flag<<4)),
                                         4,
                                         unchecked((byte)sps.chroma_format_idc),
                                         unchecked((byte)sps.bit_depth_luma_minus8),
                                         unchecked((byte)sps.bit_depth_chroma_minus8),
                                         sps_array,
                                         pps_array);
            sample_table.AddSampleDescription(sample_description);

            uint  movie_timescale      = 1000;
            uint  media_timescale      = video_frame_rate;
            ulong video_track_duration = Ap4Utils.ConvertTime(1000*sample_table.GetSampleCount(), media_timescale, movie_timescale);
            ulong video_media_duration = 1000*sample_table.GetSampleCount();

            // create a video track
            Track track = new Track(Track.Type.TYPE_VIDEO,
                                    sample_table,
                                    0,                    // auto-select track id
                                    movie_timescale,      // movie time scale
                                    video_track_duration, // track duration
                                    media_timescale,      // media time scale
                                    video_media_duration, // media duration
                                    language,             // language
                                    video_width<<16,      // width
                                    video_height<<16      // height
                                    );
            // Use an edit list to compensate for the inital cts offset
            if (max_delta != 0) {
                // create an 'edts' container
                ContainerAtom new_edts = new ContainerAtom(AtomType.EDTS);
                ElstAtom new_elst = new ElstAtom();
                ulong duration = 0;
                if (movie.GetTimeScale() == 0) {
                    duration = video_media_duration;
                } else {
                    duration = Ap4Utils.ConvertTime(1000*sample_table.GetSampleCount(), media_timescale, movie.GetTimeScale());
                }
                ElstEntry new_elst_entry = new ElstEntry(duration, (long)(max_delta*1000UL), 1);
                new_elst.AddEntry(new_elst_entry);
                new_edts.AddChild(new_elst);
                track.UseTrakAtom().AddChild(new_edts, 1);
            }
            // update the brands list
            brands.Add(Ap4.FILE_BRAND_AVC1);

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();

            movie.AddTrack(track);
        }

        /*----------------------------------------------------------------------
        |   AddH264DoviTrack
        +---------------------------------------------------------------------*/
        private static void AddH264DoviTrack(Movie             movie,
                                             string            input_name,
                                             List<Parameter>   parameters,
                                             List<uint>        brands,
                                             SampleFileStorage sample_storage)
        {
            double frame_rate = 0.0;
            //based on the Dovi iso spec, set the following values to const
            const uint dv_major_version = 1;
            const uint dv_minor_version = 0;
            const bool dv_rpu_flag = true;
            const bool dv_el_flag = false;
            const bool dv_bl_flag = true;

            uint dv_profile = 0;
            uint dv_bl_signal_comp_id = 0;
            uint dv_level = 0;

            uint format = 0;

            ByteStream input;
            Ap4Result result = FileByteStream.Create(input_name, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file '" + input_name + "' (" + D((int)result) + "))\n");
                return;
            }

            // check if we have a language parameter
            string language = GetLanguageFromParameters(parameters, "und");
            if (language == null) return;

            // see if the frame rate is specified
            uint video_frame_rate = AP4_MUX_DEFAULT_VIDEO_FRAME_RATE*1000;
            for (int i=0; i<parameters.Count; i++) {
                if (parameters[i]._name == "frame_rate") {
                    frame_rate = Atof(parameters[i]._value);
                    if (frame_rate == 0.0) {
                        Eprintf("ERROR: invalid video frame rate " + parameters[i]._value + "\n");
                        if (input is IDisposable) ((IDisposable)input).Dispose();
                        return;
                    }
                    video_frame_rate = DoubleToUInt(1000.0*frame_rate);
                } else if (parameters[i]._name == "format") {
                    if (parameters[i]._value == "avc1") {
                        format = Ap4.SAMPLE_FORMAT_AVC1;
                    } else if (parameters[i]._value == "avc3") {
                        format = Ap4.SAMPLE_FORMAT_AVC3;
                    } else if (parameters[i]._value == "dva1") {
                        format = Ap4.SAMPLE_FORMAT_DVA1;
                    } else if (parameters[i]._value == "dvav") {
                        format = Ap4.SAMPLE_FORMAT_DVAV;
                    }
                } else if (parameters[i]._name == "dv_profile") {
                    dv_profile = unchecked((uint)Atoi(parameters[i]._value));
                } else if (parameters[i]._name == "dv_bc") {
                    dv_bl_signal_comp_id = unchecked((uint)Atoi(parameters[i]._value));
                }
            }

            //set default frame rate value
            if (frame_rate == 0.0) {
                frame_rate = 24.0;
            }

            // create a sample table
            SyntheticSampleTable sample_table = new SyntheticSampleTable();

            // allocate an array to keep track of sample order
            List<SampleOrder> sample_orders_list = new List<SampleOrder>();

            // parse the input
            AvcFrameParser parser = new AvcFrameParser();

            if (format == Ap4.SAMPLE_FORMAT_AVC3 || format == Ap4.SAMPLE_FORMAT_DVAV) {
                parser.SetParameterControl(true);
            } else if (format == Ap4.SAMPLE_FORMAT_AVC1 || format == Ap4.SAMPLE_FORMAT_DVA1) {
                parser.SetParameterControl(false);
            }

            byte[] input_buffer = new byte[AP4_MUX_READ_BUFFER_SIZE];
            for (;;) {
                bool eos;
                uint bytes_in_buffer = 0;
                result = input.ReadPartial(input_buffer, AP4_MUX_READ_BUFFER_SIZE, out bytes_in_buffer);
                if (result.Succeeded()) {
                    eos = false;
                } else if (result == Ap4Result.ERROR_EOS) {
                    eos = true;
                } else {
                    Eprintf("ERROR: failed to read from input file\n");
                    break;
                }
                uint offset = 0;
                bool found_access_unit = false;
                do {
                    AvcFrameParser.AccessUnitInfo access_unit_info = new AvcFrameParser.AccessUnitInfo();

                    found_access_unit = false;
                    uint bytes_consumed = 0;
                    result = parser.Feed(input_buffer,
                                         (int)offset,
                                         bytes_in_buffer,
                                         out bytes_consumed,
                                         access_unit_info,
                                         eos);
                    if (result.Failed()) {
                        Eprintf("ERROR: Feed() failed (" + D((int)result) + ")\n");
                        break;
                    }
                    if (access_unit_info.nal_units.Count != 0) {
                        // we got one access unit
                        found_access_unit = true;
                        if (Options.verbose) {
                            Printf("H264 Access Unit, " + D(access_unit_info.nal_units.Count) + " NAL units, decode_order=" +
                                   D(access_unit_info.decode_order) + ", display_order=" +
                                   D(access_unit_info.display_order) + "\n");
                        }

                        // compute the total size of the sample data
                        uint sample_data_size = 0;
                        for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                            sample_data_size += 4+access_unit_info.nal_units[i].GetDataSize();
                        }

                        // store the sample data
                        ulong position = 0;
                        sample_storage.GetStream().Tell(out position);
                        for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                            sample_storage.GetStream().WriteUI32(access_unit_info.nal_units[i].GetDataSize());
                            sample_storage.GetStream().Write(access_unit_info.nal_units[i].GetData(), access_unit_info.nal_units[i].GetDataSize());
                        }

                        // add the sample to the track
                        sample_table.AddSample(sample_storage.GetStream(), position, sample_data_size, 1000, 0, 0, 0, access_unit_info.is_idr);

                        // remember the sample order
                        sample_orders_list.Add(new SampleOrder(access_unit_info.decode_order, access_unit_info.display_order));

                        // free the memory buffers
                        access_unit_info.Reset();
                    }

                    offset += bytes_consumed;
                    bytes_in_buffer -= bytes_consumed;
                } while (bytes_in_buffer != 0 || found_access_unit);
                if (eos) break;
            }

            // adjust the sample CTS/DTS offsets based on the sample orders
            SampleOrder[] sample_orders = sample_orders_list.ToArray();
            if (sample_orders.Length > 1) {
                uint start = 0;
                for (uint i=1; i<=sample_orders.Length; i++) {
                    if (i == sample_orders.Length || sample_orders[i]._displayOrder == 0) {
                        // we got to the end of the GOP, sort it by display order
                        SortSamples(sample_orders, (int)start, i-start);
                        start = i;
                    }
                }
            }
            uint max_delta = 0;
            for (uint i=0; i<sample_orders.Length; i++) {
                if (sample_orders[i]._decodeOrder > i) {
                    uint delta =sample_orders[i]._decodeOrder-i;
                    if (delta > max_delta) {
                        max_delta = delta;
                    }
                }
            }
            for (uint i=0; i<sample_orders.Length; i++) {
                sample_table.UseSample(sample_orders[i]._decodeOrder).SetCts(1000UL*(ulong)(i+max_delta));
            }

            // check the video parameters
            AvcSequenceParameterSet sps = null;
            for (uint i=0; i<=Ap4.AVC_SPS_MAX_ID; i++) {
                if (parser.GetSequenceParameterSets()[i] != null) {
                    sps = parser.GetSequenceParameterSets()[i];
                    break;
                }
            }
            if (sps == null) {
                Eprintf("ERROR: no sequence parameter set found in video\n");
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return;
            }
            uint video_width = 0;
            uint video_height = 0;
            sps.GetInfo(out video_width, out video_height);
            if (Options.verbose) {
                Printf("VIDEO: " + D(video_width) + "x" + D(video_height) + "\n");
            }

            // collect the SPS and PPS into arrays
            List<DataBuffer> sps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.AVC_SPS_MAX_ID; i++) {
                if (parser.GetSequenceParameterSets()[i] != null) {
                    sps_array.Add(new DataBuffer(parser.GetSequenceParameterSets()[i].raw_bytes));
                }
            }
            List<DataBuffer> pps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.AVC_PPS_MAX_ID; i++) {
                if (parser.GetPictureParameterSets()[i] != null) {
                    pps_array.Add(new DataBuffer(parser.GetPictureParameterSets()[i].raw_bytes));
                }
            }

            // compute the dolby vision level
            ComputeDoviLevel(video_width, video_height, frame_rate, ref dv_level);

            // setup the video the sample descripton
            AvcDoviSampleDescription sample_description =
                new AvcDoviSampleDescription(format,
                                             unchecked((ushort)video_width),
                                             unchecked((ushort)video_height),
                                             24,
                                             "DOVI Coding",
                                             unchecked((byte)sps.profile_idc),
                                             unchecked((byte)sps.level_idc),
                                             unchecked((byte)(sps.constraint_set0_flag<<7 |
                                                              sps.constraint_set1_flag<<6 |
                                                              sps.constraint_set2_flag<<5 |
                                                              sps.constraint_set3_flag<<4)),
                                             4,
                                             sps_array,
                                             pps_array,
                                             unchecked((byte)sps.chroma_format_idc),
                                             unchecked((byte)sps.bit_depth_luma_minus8),
                                             unchecked((byte)sps.bit_depth_chroma_minus8),
                                             unchecked((byte)dv_major_version),
                                             unchecked((byte)dv_minor_version),
                                             unchecked((byte)dv_profile),
                                             unchecked((byte)dv_level),
                                             dv_rpu_flag,
                                             dv_el_flag,
                                             dv_bl_flag,
                                             unchecked((byte)dv_bl_signal_comp_id));
            sample_table.AddSampleDescription(sample_description);

            uint  movie_timescale      = 1000;
            uint  media_timescale      = video_frame_rate;
            ulong video_track_duration = Ap4Utils.ConvertTime(1000*sample_table.GetSampleCount(), media_timescale, movie_timescale);
            ulong video_media_duration = 1000*sample_table.GetSampleCount();

            // create a video track
            Track track = new Track(Track.Type.TYPE_VIDEO,
                                    sample_table,
                                    0,                    // auto-select track id
                                    movie_timescale,      // movie time scale
                                    video_track_duration, // track duration
                                    media_timescale,      // media time scale
                                    video_media_duration, // media duration
                                    language,             // language
                                    video_width<<16,      // width
                                    video_height<<16      // height
                                    );
            // Using edit list to compensate the inital cts offset
            if (max_delta != 0) {
                // create an 'edts' container
                ContainerAtom new_edts = new ContainerAtom(AtomType.EDTS);
                ElstAtom new_elst = new ElstAtom();
                ulong duration = 0;
                if (movie.GetTimeScale() == 0) {
                    duration = video_media_duration;
                } else {
                    duration = Ap4Utils.ConvertTime(1000*sample_table.GetSampleCount(), media_timescale, movie.GetTimeScale());
                }
                ElstEntry new_elst_entry = new ElstEntry(duration, (long)(max_delta*1000UL), 1);
                new_elst.AddEntry(new_elst_entry);
                new_edts.AddChild(new_elst);
                track.UseTrakAtom().AddChild(new_edts, 1);
            }
            // update the brands list
            brands.Add(format);

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();

            movie.AddTrack(track);
        }

        /*----------------------------------------------------------------------
        |   AddH265Track
        +---------------------------------------------------------------------*/
        // PORT NOTE: not optimized on .NET Framework 2.0. Compiled by Visual Studio 2010, the
        // optimizing 64-bit JIT of CLR 2.0 produced code for this method that failed with an
        // AccessViolationException before its first statement ran (intermittently, and only
        // when the track had parameters). The IL is valid and, apart from `.maxstack 28`
        // instead of 27, identical to what newer compilers emit, which does not crash; with
        // NoOptimization the crash is gone for every .maxstack value. See docs/VERIFICATION.md.
#if NET20
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
#endif
        private static void AddH265Track(Movie             movie,
                                         string            input_name,
                                         List<Parameter>   parameters,
                                         List<uint>        brands,
                                         SampleFileStorage sample_storage)
        {
            uint video_width = 0;
            uint video_height = 0;
            uint format = Ap4.SAMPLE_FORMAT_HVC1;

            ByteStream input;
            Ap4Result result = FileByteStream.Create(input_name, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file '" + input_name + "' (" + D((int)result) + "))\n");
                return;
            }

            // check if we have a language parameter
            string language = GetLanguageFromParameters(parameters, "und");
            if (language == null) return;

            // see if the frame rate is specified
            uint video_frame_rate = AP4_MUX_DEFAULT_VIDEO_FRAME_RATE*1000;
            for (int i=0; i<parameters.Count; i++) {
                if (parameters[i]._name == "frame_rate") {
                    double frame_rate = Atof(parameters[i]._value);
                    if (frame_rate == 0.0) {
                        Eprintf("ERROR: invalid video frame rate " + parameters[i]._value + "\n");
                        if (input is IDisposable) ((IDisposable)input).Dispose();
                        return;
                    }
                    video_frame_rate = DoubleToUInt(1000.0*frame_rate);
                } else if (parameters[i]._name == "format") {
                    if (parameters[i]._value == "hev1") {
                        format = Ap4.SAMPLE_FORMAT_HEV1;
                    } else if (parameters[i]._value == "hvc1") {
                        format = Ap4.SAMPLE_FORMAT_HVC1;
                    }
                }
            }

            // create a sample table
            SyntheticSampleTable sample_table = new SyntheticSampleTable();

            // allocate an array to keep track of sample order
            List<SampleOrder> sample_orders_list = new List<SampleOrder>();

            // parse the input
            HevcFrameParser parser = new HevcFrameParser();
            byte[] input_buffer = new byte[AP4_MUX_READ_BUFFER_SIZE];
            for (;;) {
                bool eos;
                uint bytes_in_buffer = 0;
                result = input.ReadPartial(input_buffer, AP4_MUX_READ_BUFFER_SIZE, out bytes_in_buffer);
                if (result.Succeeded()) {
                    eos = false;
                } else if (result == Ap4Result.ERROR_EOS) {
                    eos = true;
                } else {
                    Eprintf("ERROR: failed to read from input file\n");
                    break;
                }
                uint offset = 0;
                bool found_access_unit = false;
                do {
                    HevcFrameParser.AccessUnitInfo access_unit_info = new HevcFrameParser.AccessUnitInfo();

                    found_access_unit = false;
                    uint bytes_consumed = 0;
                    result = parser.Feed(input_buffer,
                                         (int)offset,
                                         bytes_in_buffer,
                                         out bytes_consumed,
                                         access_unit_info,
                                         eos);
                    if (result.Failed()) {
                        Eprintf("ERROR: Feed() failed (" + D((int)result) + ")\n");
                        break;
                    }
                    if (access_unit_info.nal_units.Count != 0) {
                        // we got one access unit
                        found_access_unit = true;
                        if (Options.verbose) {
                            Printf("H265 Access Unit, " + D(access_unit_info.nal_units.Count) + " NAL units, decode_order=" +
                                   D(access_unit_info.decode_order) + ", display_order=" +
                                   D(access_unit_info.display_order) + "\n");
                        }

                        // compute the total size of the sample data
                        uint sample_data_size = 0;
                        for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                            sample_data_size += 4+access_unit_info.nal_units[i].GetDataSize();
                        }

                        // store the sample data
                        ulong position = 0;
                        sample_storage.GetStream().Tell(out position);
                        for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                            sample_storage.GetStream().WriteUI32(access_unit_info.nal_units[i].GetDataSize());
                            sample_storage.GetStream().Write(access_unit_info.nal_units[i].GetData(), access_unit_info.nal_units[i].GetDataSize());
                        }

                        // add the sample to the track
                        sample_table.AddSample(sample_storage.GetStream(), position, sample_data_size, 1000, 0, 0, 0, access_unit_info.is_random_access);

                        // remember the sample order
                        sample_orders_list.Add(new SampleOrder(access_unit_info.decode_order, access_unit_info.display_order));

                        // free the memory buffers
                        access_unit_info.Reset();
                    }

                    offset += bytes_consumed;
                    bytes_in_buffer -= bytes_consumed;
                } while (bytes_in_buffer != 0 || found_access_unit);
                if (eos) break;
            }

            // adjust the sample CTS/DTS offsets based on the sample orders
            SampleOrder[] sample_orders = sample_orders_list.ToArray();
            if (sample_orders.Length > 1) {
                uint start = 0;
                for (uint i=1; i<=sample_orders.Length; i++) {
                    if (i == sample_orders.Length || sample_orders[i]._displayOrder == 0) {
                        // we got to the end of the GOP, sort it by display order
                        SortSamples(sample_orders, (int)start, i-start);
                        start = i;
                    }
                }
            }
            uint max_delta = 0;
            for (uint i=0; i<sample_orders.Length; i++) {
                if (sample_orders[i]._decodeOrder > i) {
                    uint delta =sample_orders[i]._decodeOrder-i;
                    if (delta > max_delta) {
                        max_delta = delta;
                    }
                }
            }
            for (uint i=0; i<sample_orders.Length; i++) {
                sample_table.UseSample(sample_orders[i]._decodeOrder).SetCts(1000UL*(ulong)(i+max_delta));
            }

            // check that we have at least one SPS
            HevcSequenceParameterSet sps = null;
            for (uint i=0; i<=Ap4.HEVC_SPS_MAX_ID; i++) {
                sps = parser.GetSequenceParameterSets()[i];
                if (sps != null) break;
            }
            if (sps == null) {
                Eprintf("ERROR: no sequence parameter set found in video\n");
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return;
            }

            // collect parameters from the first SPS entry
            // TODO: synthesize from multiple SPS entries if we have more than one
            byte   general_profile_space =               unchecked((byte)sps.profile_tier_level.general_profile_space);
            byte   general_tier_flag =                   unchecked((byte)sps.profile_tier_level.general_tier_flag);
            byte   general_profile =                     unchecked((byte)sps.profile_tier_level.general_profile_idc);
            uint   general_profile_compatibility_flags = sps.profile_tier_level.general_profile_compatibility_flags;
            ulong  general_constraint_indicator_flags =  sps.profile_tier_level.general_constraint_indicator_flags;
            byte   general_level =                       unchecked((byte)sps.profile_tier_level.general_level_idc);
            uint   min_spatial_segmentation =            0; // TBD (should read from VUI if present)
            byte   parallelism_type =                    0; // unknown
            byte   chroma_format =                       unchecked((byte)sps.chroma_format_idc);
            byte   luma_bit_depth =                      8; // hardcoded temporarily, should be read from the bitstream
            byte   chroma_bit_depth =                    8; // hardcoded temporarily, should be read from the bitstream
            ushort average_frame_rate =                  0; // unknown
            byte   constant_frame_rate =                 0; // unknown
            byte   num_temporal_layers =                 0; // unknown
            byte   temporal_id_nested =                  0; // unknown
            byte   nalu_length_size =                    4;

            sps.GetInfo(out video_width, out video_height);
            if (Options.verbose) {
                Printf("VIDEO: " + D(video_width) + "x" + D(video_height) + "\n");
            }

            // collect the VPS, SPS and PPS into arrays
            List<DataBuffer> vps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.HEVC_VPS_MAX_ID; i++) {
                if (parser.GetVideoParameterSets()[i] != null) {
                    vps_array.Add(new DataBuffer(parser.GetVideoParameterSets()[i].raw_bytes));
                }
            }
            List<DataBuffer> sps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.HEVC_SPS_MAX_ID; i++) {
                if (parser.GetSequenceParameterSets()[i] != null) {
                    sps_array.Add(new DataBuffer(parser.GetSequenceParameterSets()[i].raw_bytes));
                }
            }
            List<DataBuffer> pps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.HEVC_PPS_MAX_ID; i++) {
                if (parser.GetPictureParameterSets()[i] != null) {
                    pps_array.Add(new DataBuffer(parser.GetPictureParameterSets()[i].raw_bytes));
                }
            }

            // setup the video the sample descripton
            byte parameters_completeness = (byte)(format == Ap4.SAMPLE_FORMAT_HVC1 ? 1 : 0);
            HevcSampleDescription sample_description =
                new HevcSampleDescription(format,
                                          unchecked((ushort)video_width),
                                          unchecked((ushort)video_height),
                                          24,
                                          "HEVC Coding",
                                          general_profile_space,
                                          general_tier_flag,
                                          general_profile,
                                          general_profile_compatibility_flags,
                                          general_constraint_indicator_flags,
                                          general_level,
                                          min_spatial_segmentation,
                                          parallelism_type,
                                          chroma_format,
                                          luma_bit_depth,
                                          chroma_bit_depth,
                                          average_frame_rate,
                                          constant_frame_rate,
                                          num_temporal_layers,
                                          temporal_id_nested,
                                          nalu_length_size,
                                          vps_array,
                                          parameters_completeness,
                                          sps_array,
                                          parameters_completeness,
                                          pps_array,
                                          parameters_completeness);

            sample_table.AddSampleDescription(sample_description);

            uint  movie_timescale      = 1000;
            uint  media_timescale      = video_frame_rate;
            ulong video_track_duration = Ap4Utils.ConvertTime(1000*sample_table.GetSampleCount(), media_timescale, movie_timescale);
            ulong video_media_duration = 1000*sample_table.GetSampleCount();

            // create a video track
            Track track = new Track(Track.Type.TYPE_VIDEO,
                                    sample_table,
                                    0,                    // auto-select track id
                                    movie_timescale,      // movie time scale
                                    video_track_duration, // track duration
                                    media_timescale,      // media time scale
                                    video_media_duration, // media duration
                                    language,             // language
                                    video_width<<16,      // width
                                    video_height<<16      // height
                                    );

            // Use an edit list to compensate for the inital cts offset
            if (max_delta != 0) {
                // create an 'edts' container
                ContainerAtom new_edts = new ContainerAtom(AtomType.EDTS);
                ElstAtom new_elst = new ElstAtom();
                ulong duration = 0;
                if (movie.GetTimeScale() == 0) {
                    duration = video_media_duration;
                } else {
                    duration = Ap4Utils.ConvertTime(1000*sample_table.GetSampleCount(), media_timescale, movie.GetTimeScale());
                }
                ElstEntry new_elst_entry = new ElstEntry(duration, (long)(max_delta*1000UL), 1);
                new_elst.AddEntry(new_elst_entry);
                new_edts.AddChild(new_elst);
                track.UseTrakAtom().AddChild(new_edts, 1);
            }
            // update the brands list
            brands.Add(Ap4.FILE_BRAND_HVC1);

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();

            movie.AddTrack(track);
        }

        /*----------------------------------------------------------------------
        |   AddH265DoviTrack
        +---------------------------------------------------------------------*/
        private static void AddH265DoviTrack(Movie             movie,
                                             string            input_name,
                                             List<Parameter>   parameters,
                                             List<uint>        brands,
                                             SampleFileStorage sample_storage)
        {
            uint   video_width = 0;
            uint   video_height = 0;
            double frame_rate = 0.0;

            //based on the Dovi iso spec, set the following values to const
            const uint dv_major_version = 1;
            const uint dv_minor_version = 0;
            const bool dv_rpu_flag = true;
            const bool dv_el_flag = false;
            const bool dv_bl_flag = true;

            uint dv_profile = 0;
            uint dv_bl_signal_comp_id = 0;
            uint dv_level = 0;

            uint format = 0;

            ByteStream input;
            Ap4Result result = FileByteStream.Create(input_name, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file '" + input_name + "' (" + D((int)result) + "))\n");
                return;
            }

            // check if we have a language parameter
            string language = GetLanguageFromParameters(parameters, "und");
            if (language == null) return;

            // see if the frame rate/format/dv_profile/dv_bc is specified
            uint video_frame_rate = AP4_MUX_DEFAULT_VIDEO_FRAME_RATE*1000;
            for (int i=0; i<parameters.Count; i++) {
                if (parameters[i]._name == "frame_rate") {
                    frame_rate = Atof(parameters[i]._value);
                    if (frame_rate == 0.0) {
                        Eprintf("ERROR: invalid video frame rate " + parameters[i]._value + "\n");
                        if (input is IDisposable) ((IDisposable)input).Dispose();
                        return;
                    }
                    video_frame_rate = DoubleToUInt(1000.0*frame_rate);
                } else if (parameters[i]._name == "format") {
                    if (parameters[i]._value == "hvc1") {
                        format = Ap4.SAMPLE_FORMAT_HVC1;
                    } else if (parameters[i]._value == "hev1") {
                        format = Ap4.SAMPLE_FORMAT_HEV1;
                    } else if (parameters[i]._value == "dvh1") {
                        format = Ap4.SAMPLE_FORMAT_DVH1;
                    } else if (parameters[i]._value == "dvhe") {
                        format = Ap4.SAMPLE_FORMAT_DVHE;
                    }
                } else if (parameters[i]._name == "dv_profile") {
                    dv_profile = unchecked((uint)Atoi(parameters[i]._value));
                } else if (parameters[i]._name == "dv_bc") {
                    dv_bl_signal_comp_id = unchecked((uint)Atoi(parameters[i]._value));
                }
            }

            //if not set format, set the default one based on profile
            if (format == 0) {
                if (dv_profile == 5) {
                    format = Ap4.SAMPLE_FORMAT_DVH1;
                } else if (dv_profile == 8) {
                    if (dv_bl_signal_comp_id != 0) {
                        format = Ap4.SAMPLE_FORMAT_HVC1;
                    } else {
                        format = Ap4.SAMPLE_FORMAT_DVH1;
                    }
                }
            }

            // create a sample table
            SyntheticSampleTable sample_table = new SyntheticSampleTable();

            // allocate an array to keep track of sample order
            List<SampleOrder> sample_orders_list = new List<SampleOrder>();

            // parse the input
            HevcFrameParser parser = new HevcFrameParser();
            if (format == Ap4.SAMPLE_FORMAT_HEV1 || format == Ap4.SAMPLE_FORMAT_DVHE) {
                parser.SetParameterControl(true);
            } else if (format == Ap4.SAMPLE_FORMAT_HVC1 || format == Ap4.SAMPLE_FORMAT_DVH1) {
                parser.SetParameterControl(false);
            }
            byte[] input_buffer = new byte[AP4_MUX_READ_BUFFER_SIZE];
            for (;;) {
                bool eos;
                uint bytes_in_buffer = 0;
                result = input.ReadPartial(input_buffer, AP4_MUX_READ_BUFFER_SIZE, out bytes_in_buffer);
                if (result.Succeeded()) {
                    eos = false;
                } else if (result == Ap4Result.ERROR_EOS) {
                    eos = true;
                } else {
                    Eprintf("ERROR: failed to read from input file\n");
                    break;
                }
                uint offset = 0;
                bool found_access_unit = false;
                do {
                    HevcFrameParser.AccessUnitInfo access_unit_info = new HevcFrameParser.AccessUnitInfo();

                    found_access_unit = false;
                    uint bytes_consumed = 0;
                    result = parser.Feed(input_buffer,
                                         (int)offset,
                                         bytes_in_buffer,
                                         out bytes_consumed,
                                         access_unit_info,
                                         eos);
                    if (result.Failed()) {
                        Eprintf("ERROR: Feed() failed (" + D((int)result) + ")\n");
                        break;
                    }
                    if (access_unit_info.nal_units.Count != 0) {
                        // we got one access unit
                        found_access_unit = true;
                        if (Options.verbose) {
                            Printf("H265 Access Unit, " + D(access_unit_info.nal_units.Count) + " NAL units, decode_order=" +
                                   D(access_unit_info.decode_order) + ", display_order=" +
                                   D(access_unit_info.display_order) + "\n");
                        }

                        // compute the total size of the sample data
                        uint sample_data_size = 0;
                        for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                            sample_data_size += 4+access_unit_info.nal_units[i].GetDataSize();
                        }

                        // store the sample data
                        ulong position = 0;
                        sample_storage.GetStream().Tell(out position);
                        for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                            sample_storage.GetStream().WriteUI32(access_unit_info.nal_units[i].GetDataSize());
                            sample_storage.GetStream().Write(access_unit_info.nal_units[i].GetData(), access_unit_info.nal_units[i].GetDataSize());
                        }

                        // add the sample to the track
                        sample_table.AddSample(sample_storage.GetStream(), position, sample_data_size, 1000, 0, 0, 0, access_unit_info.is_random_access);

                        // remember the sample order
                        sample_orders_list.Add(new SampleOrder(access_unit_info.decode_order, access_unit_info.display_order));

                        // free the memory buffers
                        access_unit_info.Reset();
                    }

                    offset += bytes_consumed;
                    bytes_in_buffer -= bytes_consumed;
                } while (bytes_in_buffer != 0 || found_access_unit);
                if (eos) break;
            }

            // adjust the sample CTS/DTS offsets based on the sample orders
            SampleOrder[] sample_orders = sample_orders_list.ToArray();
            if (sample_orders.Length > 1) {
                uint start = 0;
                for (uint i=1; i<=sample_orders.Length; i++) {
                    if (i == sample_orders.Length || sample_orders[i]._displayOrder == 0) {
                        // we got to the end of the GOP, sort it by display order
                        SortSamples(sample_orders, (int)start, i-start);
                        start = i;
                    }
                }
            }
            uint max_delta = 0;
            for (uint i=0; i<sample_orders.Length; i++) {
                if (sample_orders[i]._decodeOrder > i) {
                    uint delta =sample_orders[i]._decodeOrder-i;
                    if (delta > max_delta) {
                        max_delta = delta;
                    }
                }
            }
            for (uint i=0; i<sample_orders.Length; i++) {
                sample_table.UseSample(sample_orders[i]._decodeOrder).SetCts(1000UL*(ulong)(i+max_delta));
            }

            // check that we have at least one SPS
            HevcSequenceParameterSet sps = null;
            for (uint i=0; i<=Ap4.HEVC_SPS_MAX_ID; i++) {
                sps = parser.GetSequenceParameterSets()[i];
                if (sps != null) break;
            }
            if (sps == null) {
                Eprintf("ERROR: no sequence parameter set found in video\n");
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return;
            }

            // collect parameters from the first SPS entry
            // TODO: synthesize from multiple SPS entries if we have more than one
            byte   general_profile_space =               unchecked((byte)sps.profile_tier_level.general_profile_space);
            byte   general_tier_flag =                   unchecked((byte)sps.profile_tier_level.general_tier_flag);
            byte   general_profile =                     unchecked((byte)sps.profile_tier_level.general_profile_idc);
            uint   general_profile_compatibility_flags = sps.profile_tier_level.general_profile_compatibility_flags;
            ulong  general_constraint_indicator_flags =  sps.profile_tier_level.general_constraint_indicator_flags;
            byte   general_level =                       unchecked((byte)sps.profile_tier_level.general_level_idc);
            uint   min_spatial_segmentation =            0; // TBD (should read from VUI if present)
            byte   parallelism_type =                    0; // unknown
            byte   chroma_format =                       unchecked((byte)sps.chroma_format_idc);
            byte   luma_bit_depth =                      8; // hardcoded temporarily, should be read from the bitstream
            byte   chroma_bit_depth =                    8; // hardcoded temporarily, should be read from the bitstream
            ushort average_frame_rate =                  0; // unknown
            byte   constant_frame_rate =                 0; // unknown
            byte   num_temporal_layers =                 0; // unknown
            byte   temporal_id_nested =                  0; // unknown
            byte   nalu_length_size =                    4;
            byte   transfer_characteristics =            unchecked((byte)sps.vui_parameters.transfer_characteristics);

            sps.GetInfo(out video_width, out video_height);
            if (Options.verbose) {
                Printf("VIDEO: " + D(video_width) + "x" + D(video_height) + "\n");
            }

            // collect the VPS, SPS and PPS into arrays
            List<DataBuffer> vps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.HEVC_VPS_MAX_ID; i++) {
                if (parser.GetVideoParameterSets()[i] != null) {
                    vps_array.Add(new DataBuffer(parser.GetVideoParameterSets()[i].raw_bytes));
                }
            }
            List<DataBuffer> sps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.HEVC_SPS_MAX_ID; i++) {
                if (parser.GetSequenceParameterSets()[i] != null) {
                    sps_array.Add(new DataBuffer(parser.GetSequenceParameterSets()[i].raw_bytes));
                }
            }
            List<DataBuffer> pps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.HEVC_PPS_MAX_ID; i++) {
                if (parser.GetPictureParameterSets()[i] != null) {
                    pps_array.Add(new DataBuffer(parser.GetPictureParameterSets()[i].raw_bytes));
                }
            }

            // get the frame rate from the VPS
            // PORT NOTE: upstream dereferences VPS 0 without a NULL check
            HevcVideoParameterSet vps = parser.GetVideoParameterSets()[0];
            uint vps_time_scale = 0;
            uint vps_num_units = 0;
            vps.GetInfo(out vps_time_scale, out vps_num_units);
            if (vps_num_units != 0) {
                frame_rate = vps_time_scale/vps_num_units;
            }

            // set dolby vision level
            ComputeDoviLevel(video_width, video_height, frame_rate, ref dv_level);

            // setup the video the sample descripton
            byte parameters_completeness = (byte)((format == Ap4.SAMPLE_FORMAT_HVC1 || format == Ap4.SAMPLE_FORMAT_DVH1) ? 1 : 0);
            HevcDoviSampleDescription sample_description =
                new HevcDoviSampleDescription(format,
                                              unchecked((ushort)video_width),
                                              unchecked((ushort)video_height),
                                              24,
                                              "DOVI Coding",
                                              general_profile_space,
                                              general_tier_flag,
                                              general_profile,
                                              general_profile_compatibility_flags,
                                              general_constraint_indicator_flags,
                                              general_level,
                                              min_spatial_segmentation,
                                              parallelism_type,
                                              chroma_format,
                                              luma_bit_depth,
                                              chroma_bit_depth,
                                              average_frame_rate,
                                              constant_frame_rate,
                                              num_temporal_layers,
                                              temporal_id_nested,
                                              nalu_length_size,
                                              vps_array,
                                              parameters_completeness,
                                              sps_array,
                                              parameters_completeness,
                                              pps_array,
                                              parameters_completeness,
                                              unchecked((byte)dv_major_version),
                                              unchecked((byte)dv_minor_version),
                                              unchecked((byte)dv_profile),
                                              unchecked((byte)dv_level),
                                              dv_rpu_flag,
                                              dv_el_flag,
                                              dv_bl_flag,
                                              unchecked((byte)dv_bl_signal_comp_id));

            sample_table.AddSampleDescription(sample_description);

            uint  movie_timescale      = 1000;
            uint  media_timescale      = video_frame_rate;
            ulong video_track_duration = Ap4Utils.ConvertTime(1000*sample_table.GetSampleCount(), media_timescale, movie_timescale);
            ulong video_media_duration = 1000*sample_table.GetSampleCount();

            // create a video track
            Track track = new Track(Track.Type.TYPE_VIDEO,
                                    sample_table,
                                    0,                    // auto-select track id
                                    movie_timescale,      // movie time scale
                                    video_track_duration, // track duration
                                    media_timescale,      // media time scale
                                    video_media_duration, // media duration
                                    language,             // language
                                    video_width<<16,      // width
                                    video_height<<16      // height
                                    );
            // Using edit list to compensate the inital cts offset
            if (max_delta != 0) {
                // create an 'edts' container
                ContainerAtom new_edts = new ContainerAtom(AtomType.EDTS);
                ElstAtom new_elst = new ElstAtom();
                ulong duration = 0;
                if (movie.GetTimeScale() == 0) {
                    duration = video_media_duration;
                } else {
                    duration = Ap4Utils.ConvertTime(1000*sample_table.GetSampleCount(), media_timescale, movie.GetTimeScale());
                }
                ElstEntry new_elst_entry = new ElstEntry(duration, (long)(max_delta*1000UL), 1);
                new_elst.AddEntry(new_elst_entry);
                new_edts.AddChild(new_elst);
                track.UseTrakAtom().AddChild(new_edts, 1);
            }
            // update the brands list
            brands.Add(format);
            if (dv_profile == 8 && dv_bl_signal_comp_id == 4) {
                if (transfer_characteristics == 18) {
                    brands.Add(Ap4.FILE_BRAND_DB4H);
                } else if (transfer_characteristics == 14) {
                    brands.Add(Ap4.FILE_BRAND_DB4G);
                }
            }

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();

            movie.AddTrack(track);
        }

        /*----------------------------------------------------------------------
        |   AddMp4Tracks
        +---------------------------------------------------------------------*/
        private static void AddMp4Tracks(Movie           movie,
                                         string          input_name,
                                         List<Parameter> parameters,
                                         List<uint>      brands)
        {
            // open the input
            ByteStream  input_stream = null;
            Ap4Result result = FileByteStream.Create(input_name,
                                                     FileByteStream.Mode.STREAM_MODE_READ,
                                                     out input_stream);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file " + input_name + " (" + D((int)result) + ")\n");
                return;
            }

            File file = new File(input_stream, true);
            s_Mp4InputStreams.Add(input_stream); // PORT NOTE: input_stream->Release() upstream, see the file header
            Movie input_movie = file.GetMovie();
            if (input_movie == null) {
                return;
            }

            // check if we have a language parameter
            string language = GetLanguageFromParameters(parameters, null);

            // check the parameters to decide which track(s) to import
            uint track_id = 0;
            for (int i=0; i<parameters.Count; i++) {
                if (parameters[i]._name == "track") {
                    if (parameters[i]._value == "audio") {
                        Track track = input_movie.GetTrack(Track.Type.TYPE_AUDIO);
                        if (track == null) {
                            Eprintf("ERROR: no audio track found in " + input_name + "\n");
                            return;
                        } else {
                            track_id = track.GetId();
                        }
                    } else if (parameters[i]._value == "video") {
                        Track track = input_movie.GetTrack(Track.Type.TYPE_VIDEO);
                        if (track == null) {
                            Eprintf("ERROR: no video track found in " + input_name + "\n");
                            return;
                        } else {
                            track_id = track.GetId();
                        }
                    } else {
                        track_id = Strtoul(parameters[i]._value);
                        if (track_id == 0) {
                            Eprintf("ERROR: invalid track ID specified");
                            return;
                        }
                    }
                }
            }

            if (Options.verbose) {
                if (track_id == 0) {
                    Printf("MP4 Import: importing all tracks from " + input_name + "\n");
                } else {
                    Printf("MP4 Import: importing track " + D(track_id) + " from " + input_name + "\n");
                }
            }

            foreach (Track track_item in input_movie.GetTracks()) {
                Track track = track_item;
                if (track_id == 0 || track.GetId() == track_id) {
                    track = track.Clone();

                    // reset the track ID so that it can be re-assigned
                    track.SetId(0);

                    // override the language if specified in the parameters
                    if (language != null) {
                        track.SetTrackLanguage(language);
                    }

                    movie.AddTrack(track);
                }
            }
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
            if (args.Length < 1) {
                return PrintUsageAndExit();
            }
            Options.verbose = false;

            string output_filename = null;
            List<string> input_names = new List<string>();

            for (int argi = 0; argi < args.Length; ) {
                string arg = args[argi++];
                if (arg == "--verbose") {
                    Options.verbose = true;
                } else if (arg == "--track") {
                    // PORT NOTE: see the file header for `--track` without a value
                    input_names.Add(argi < args.Length ? args[argi++] : null);
                } else if (output_filename == null) {
                    output_filename = arg;
                } else {
                    Eprintf("ERROR: unexpected argument '" + arg + "'\n");
                    return 1;
                }
            }

            if (input_names.Count == 0) {
                Eprintf("ERROR: no input\n");
                return 1;
            }
            if (output_filename == null) {
                Eprintf("ERROR: output filename missing\n");
                return 1;
            }

            // create the movie object to hold the tracks
            ulong creation_time = 0;
#if !NET20
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // time(NULL)
#else
            long now = Compat.UtcNowUnixTimeSeconds(); // time(NULL)
#endif
            if (now != -1) {
                // adjust the time based on the MPEG time origin
                creation_time = unchecked((ulong)now + 0x7C25B080);
            }
            Movie movie = new Movie(0, 0, creation_time, creation_time);

            // setup the brands
            List<uint> brands = new List<uint>();
            brands.Add(Ap4.FILE_BRAND_ISOM);
            brands.Add(Ap4.FILE_BRAND_MP42);

            // create a temp file to store the sample data
            SampleFileStorage  sample_storage = null;
            Ap4Result result = SampleFileStorage.Create(output_filename, out sample_storage);
            if (result.Failed()) {
                Eprintf("ERROR: failed to create temporary sample data storage (" + D((int)result) + ")\n");
                return 1;
            }

            // add all the tracks
            bool hasDovi = false;
            byte dolby_vision_ccid = 0;
            for (int i=0; i<input_names.Count; i++) {
                string  input_name = input_names[i] ?? "";
                string  input_type = null;
                string input_params = null;

                int separator = input_name.IndexOf(':');
                if (separator >= 0) {
                    input_type = input_name.Substring(0, separator);
                    input_name = input_name.Substring(separator+1);
                }
                separator = input_name.IndexOf('#');
                if (separator >= 0) {
                    input_params = input_name.Substring(separator+1);
                    input_name = input_name.Substring(0, separator);
                }

                if (input_type == null) {
                    // no type, try to guess
                    separator = input_name.LastIndexOf('.');
                    if (separator >= 0) {
                        // PORT NOTE: upstream lower-cases the extension in place (ASCII tolower),
                        // which also changes the name of the file that is opened
                        char[] chars = input_name.ToCharArray();
                        for (int j=separator+1; j<chars.Length; j++) {
                            if (chars[j] >= 'A' && chars[j] <= 'Z') chars[j] = (char)(chars[j] - 'A' + 'a');
                        }
                        input_name = new string(chars);
                        input_type = input_name.Substring(separator+1);
                        if ("264" == input_type) {
                            input_type = "h264";
                        } else if ("avc" == input_type) {
                            input_type = "h264";
                        } else if ("265" == input_type) {
                            input_type = "h265";
                        } else if ("hevc" == input_type) {
                            input_type = "h265";
                        } else if ("adts" == input_type) {
                            input_type = "aac";
                        } else if ("m4a" == input_type ||
                                   "m4v" == input_type ||
                                   "mov" == input_type) {
                            input_type = "aac";
                        }
                    } else {
                        Eprintf("ERROR: unable to determine type for input '" + input_name + "'\n");
                        sample_storage.Dispose();
                        return 1;
                    }
                }

                // parse parameters
                List<Parameter> parameters = new List<Parameter>();
                if (input_params != null) {
                    ParseParameters(input_params, parameters);
                }

                bool isDovi = false;
                for (int j=0; j<parameters.Count; j++) {
                    if (parameters[j]._name == "dv_profile") {
                        isDovi = true;
                    }
                    if (parameters[j]._name == "dv_bc") {
                        if (parameters[j]._value == "1") {
                            dolby_vision_ccid |= 0x1;
                        } else if (parameters[j]._value == "2") {
                            dolby_vision_ccid |= 0x2;
                        } else if (parameters[j]._value == "4") {
                            dolby_vision_ccid |= 0x4;
                        }
                    }
                }

                if (input_type == "h264") {
                    if (isDovi) {
                        if (CheckDoviInputParameters(parameters) != Ap4Result.SUCCESS) {
                            Eprintf("ERROR: dolby vision input parameter error\n");
                            sample_storage.Dispose();
                            return 1;
                        } else {
                            AddH264DoviTrack(movie, input_name, parameters, brands, sample_storage);
                            hasDovi = true;
                        }
                    } else {
                        AddH264Track(movie, input_name, parameters, brands, sample_storage);
                    }
                } else if (input_type == "h265") {
                    if (isDovi) {
                        if (CheckDoviInputParameters(parameters) != Ap4Result.SUCCESS) {
                            Eprintf("ERROR: dolby vision input parameter error\n");
                            sample_storage.Dispose();
                            return 1;
                        } else {
                            AddH265DoviTrack(movie, input_name, parameters, brands, sample_storage);
                            hasDovi = true;
                        }
                    } else {
                        AddH265Track(movie, input_name, parameters, brands, sample_storage);
                    }
                } else if (input_type == "aac") {
                    AddAacTrack(movie, input_name, parameters, sample_storage);
                } else if (input_type == "ac3") {
                    AddAc3Track(movie, input_name, parameters, sample_storage);
                } else if (input_type == "ec3") {
                    AddEac3Track(movie, input_name, parameters, sample_storage);
                } else if (input_type == "ac4") {
                    AddAc4Track(movie, input_name, parameters, sample_storage);
                } else if (input_type == "mp4") {
                    AddMp4Tracks(movie, input_name, parameters, brands);
                } else {
                    Eprintf("ERROR: unsupported input type '" + input_type + "'\n");
                    sample_storage.Dispose();
                    return 1;
                }
            }

            // for Dolby Vision, add the 'dby1' brand
            if (hasDovi) {
                brands.Add(Ap4.FILE_BRAND_DBY1);
            }
            // brand of Dolby Vision with CCID == 4 needs transfer characteristic to be decided later
            if ((dolby_vision_ccid & 0x1) != 0) {
                brands.Add(Ap4.FILE_BRAND_DB1P);
            }
            if ((dolby_vision_ccid & 0x2) != 0) {
                brands.Add(Ap4.FILE_BRAND_DB2G);
            }

            movie.GetMvhdAtom().SetNextTrackId((uint)movie.GetTracks().Count + 1);

            // open the output
            ByteStream  output = null;
            result = FileByteStream.Create(output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Debug("ERROR: cannot open output '" + output_filename + "' (" + D((int)result) + ")\n");
                sample_storage.Dispose();
                return 1;
            }

            {
                // create a multimedia file
                File file = new File(movie);

                // set the file type
                file.SetFileType(Ap4.FILE_BRAND_MP42, 1, brands.ToArray(), (uint)brands.Count);

                // write the file to the output
                FileWriter.Write(file, output);
            }

            // cleanup
            sample_storage.Dispose();
            if (output is IDisposable) ((IDisposable)output).Dispose();
            foreach (ByteStream stream in s_Mp4InputStreams) {
                if (stream is IDisposable) ((IDisposable)stream).Dispose();
            }

            return 0;
        }
    }
}
