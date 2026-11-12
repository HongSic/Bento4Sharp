// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Split/Mp4Split.cpp
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - fprintf(stderr) output is written as raw bytes ('\n' line endings, no
//   encoding translation).
// - sprintf(segment_name, pattern, p[0], p[1]) is implemented by Sprintf()
//   below, which supports the integer conversions of printf.
// - upstream relies on the C runtime flushing open files when main() returns
//   early; the port closes the streams explicitly on every return path.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Split
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Fragment Splitter - Version 1.2\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2016 Axiomatic Systems, LLC";

        private const string AP4_SPLIT_DEFAULT_INIT_SEGMENT_NAME  = "init.mp4";
        private const string AP4_SPLIT_DEFAULT_MEDIA_SEGMENT_NAME = "segment-%llu.%04llu.m4s";
        private const string AP4_SPLIT_DEFAULT_PATTERN_PARAMS     = "IN";

        private const uint AP4_SPLIT_MAX_TRACK_IDS = 32;

        /*----------------------------------------------------------------------
        |   options
        +---------------------------------------------------------------------*/
        private static class Options {
            public static bool    verbose;
            public static string  input;
            public static string  init_segment_name = "";
            public static string  media_segment_name = "";
            public static string  pattern_params = "";
            public static uint    start_number;
            public static uint[]  track_ids = new uint[AP4_SPLIT_MAX_TRACK_IDS];
            public static uint    track_id_count;
            public static bool    audio_only;
            public static bool    video_only;
            public static bool    init_only;
        }

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: fprintf(stderr) / strtoul / sprintf replacements)
        +---------------------------------------------------------------------*/
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

        // strtoul(str, NULL, 10)
        private static ulong Strtoul(string str)
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
                ulong digit = (ulong)(str[i]-'0');
                if (value > (ulong.MaxValue-digit)/10) {
                    overflow = true;
                } else {
                    value = value*10+digit;
                }
                ++i;
            }
            if (overflow) return ulong.MaxValue;
            return negative ? unchecked((ulong)(-(long)value)) : value;
        }

        // sprintf(buffer, format, args[0], args[1], ...) where all the arguments
        // are 64-bit integers.
        // PORT NOTE: supports flags, width, precision, length modifiers and the
        // d i u x X o c conversions. Each conversion consumes one argument
        // (missing arguments read as 0); conversions without an 'll'/'j'
        // modifier use the low bits of the argument.
        private static string Sprintf(string format, ulong[] args, int arg_count)
        {
            StringBuilder output = new StringBuilder();
            int next_arg = 0;
            int i = 0;
            while (i < format.Length) {
                char c = format[i++];
                if (c != '%') {
                    output.Append(c);
                    continue;
                }
                int spec_start = i-1;
                if (i < format.Length && format[i] == '%') {
                    output.Append('%');
                    ++i;
                    continue;
                }

                // flags
                bool left = false, plus = false, space = false, alt = false, zero = false;
                for (; i < format.Length; i++) {
                    char f = format[i];
                    if      (f == '-') left = true;
                    else if (f == '+') plus = true;
                    else if (f == ' ') space = true;
                    else if (f == '#') alt = true;
                    else if (f == '0') zero = true;
                    else break;
                }

                // width
                int width = 0;
                while (i < format.Length && format[i] >= '0' && format[i] <= '9') {
                    width = width*10+(format[i++]-'0');
                }

                // precision
                int precision = -1;
                if (i < format.Length && format[i] == '.') {
                    ++i;
                    precision = 0;
                    while (i < format.Length && format[i] >= '0' && format[i] <= '9') {
                        precision = precision*10+(format[i++]-'0');
                    }
                }

                // length modifier
                int bits = 32;
                if (i < format.Length) {
                    if (format[i] == 'l') {
                        ++i;
                        if (i < format.Length && format[i] == 'l') {
                            ++i;
                            bits = 64;
                        } else {
                            bits = 32; // PORT NOTE: 'long' of the reference (32-bit) build
                        }
                    } else if (format[i] == 'h') {
                        ++i;
                        bits = 16;
                        if (i < format.Length && format[i] == 'h') {
                            ++i;
                            bits = 8;
                        }
                    } else if (format[i] == 'j' || format[i] == 'q') {
                        ++i;
                        bits = 64;
                    } else if (format[i] == 'z' || format[i] == 't') {
                        ++i;
                        bits = 32;
                    }
                }

                if (i >= format.Length) {
                    // incomplete conversion: copy as-is
                    output.Append(format, spec_start, format.Length-spec_start);
                    break;
                }
                char conv = format[i++];
                ulong arg;
                string digits;
                string prefix = "";
                switch (conv) {
                    case 'd':
                    case 'i': {
                        arg = next_arg < arg_count ? args[next_arg] : 0; ++next_arg;
                        long value;
                        switch (bits) {
                            case 8:  value = unchecked((sbyte)arg); break;
                            case 16: value = unchecked((short)arg); break;
                            case 32: value = unchecked((int)arg);   break;
                            default: value = unchecked((long)arg);  break;
                        }
                        if (value < 0) {
                            prefix = "-";
                            digits = unchecked((ulong)(-value)).ToString(CultureInfo.InvariantCulture);
                        } else {
                            if (plus) prefix = "+"; else if (space) prefix = " ";
                            digits = value.ToString(CultureInfo.InvariantCulture);
                        }
                        break;
                    }

                    case 'u':
                    case 'x':
                    case 'X':
                    case 'o': {
                        arg = next_arg < arg_count ? args[next_arg] : 0; ++next_arg;
                        ulong value;
                        switch (bits) {
                            case 8:  value = (byte)arg;   break;
                            case 16: value = (ushort)arg; break;
                            case 32: value = (uint)arg;   break;
                            default: value = arg;         break;
                        }
                        if (conv == 'u') {
                            digits = value.ToString(CultureInfo.InvariantCulture);
                        } else if (conv == 'x') {
                            digits = value.ToString("x", CultureInfo.InvariantCulture);
                            if (alt && value != 0) prefix = "0x";
                        } else if (conv == 'X') {
                            digits = value.ToString("X", CultureInfo.InvariantCulture);
                            if (alt && value != 0) prefix = "0X";
                        } else {
                            digits = Convert.ToString(unchecked((long)value), 8);
                            if (alt && digits[0] != '0') digits = "0" + digits;
                        }
                        break;
                    }

                    case 'c': {
                        arg = next_arg < arg_count ? args[next_arg] : 0; ++next_arg;
                        string ch = ((char)(byte)arg).ToString();
                        if (left) {
                            output.Append(ch.PadRight(width));
                        } else {
                            output.Append(ch.PadLeft(width));
                        }
                        continue;
                    }

                    default:
                        // unsupported conversion: copy as-is
                        output.Append(format, spec_start, i-spec_start);
                        continue;
                }

                // precision: minimum number of digits
                if (precision >= 0) {
                    if (precision == 0 && digits == "0") digits = "";
                    if (digits.Length < precision) digits = new string('0', precision-digits.Length) + digits;
                    zero = false;
                }
                int length = prefix.Length+digits.Length;
                if (left) {
                    output.Append(prefix).Append(digits);
                    if (length < width) output.Append(' ', width-length);
                } else if (zero) {
                    output.Append(prefix);
                    if (length < width) output.Append('0', width-length);
                    output.Append(digits);
                } else {
                    if (length < width) output.Append(' ', width-length);
                    output.Append(prefix).Append(digits);
                }
            }
            return output.ToString();
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp4split [options] <input>\n" +
                    "Options:\n" +
                    "  --verbose : print verbose information when running\n" +
                    "  --init-segment <filename> : name of init segment (default: init.mp4)\n" +
                    "  --init-only : only output the init segment (no media segments)\n" +
                    "  --media-segment <filename-pattern> (default: segment-%llu.%04llu.m4s)\n" +
                    "    NOTE: all parameters are 64-bit integers, use %llu in the pattern\n" +
                    "  --start-number <n> : start numbering segments at <n> (default=1)\n" +
                    "  --pattern-parameters <params> : one or more selector letter (default: IN)\n" +
                    "     I: track ID\n" +
                    "     N: segment number\n" +
                    "  --track-id <track-id> : only output segments with this track ID\n" +
                    "     More than one track IDs can be specified if <track-id> is a comma-separated\n" +
                    "     list of track IDs\n" +
                    "  --audio : only output audio segments\n" +
                    "  --video : only output video segments\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   NextFragmentIndex
        +---------------------------------------------------------------------*/
        private static List<uint> TrackIds = new List<uint>();
        private static List<uint> TrackCounters = new List<uint>();

        private static uint
        NextFragmentIndex(uint track_id)
        {
            int track_index = -1;
            for (int i=0; i<TrackIds.Count; i++) {
                if (TrackIds[i] == track_id) {
                    track_index = i;
                    break;
                }
            }
            if (track_index == -1) {
                track_index = TrackCounters.Count;
                TrackIds.Add(track_id);
                TrackCounters.Add(0);
            }
            return TrackCounters[track_index]++;
        }

        /*----------------------------------------------------------------------
        |   ParseTrackIds
        +---------------------------------------------------------------------*/
        private static bool
        ParseTrackIds(string ids)
        {
            int start = 0;
            for (int separator = 0; ; ++separator) {
                // PORT NOTE: the end of the string plays the role of the NUL terminator
                bool the_end = (separator >= ids.Length || ids[separator] == '\0');
                if (the_end || ids[separator] == ',') {
                    if (Options.track_id_count >= AP4_SPLIT_MAX_TRACK_IDS) {
                        return false;
                    }
                    Options.track_ids[Options.track_id_count++] = unchecked((uint)Strtoul(ids.Substring(start, separator-start)));
                    if (the_end) break;
                    start = separator+1;
                }
            }

            return true;
        }

        /*----------------------------------------------------------------------
        |   TrackIdMatches
        +---------------------------------------------------------------------*/
        private static bool
        TrackIdMatches(uint track_id)
        {
            if (Options.track_id_count == 0) return true;
            for (uint i=0; i<Options.track_id_count; i++) {
                if (Options.track_ids[i] == track_id) return true;
            }

            return false;
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        private static ByteStream s_Input;
        private static ByteStream s_Output;

        public static int Main(string[] args)
        {
            try {
                return Run(args);
            } finally {
                // PORT NOTE: closes (flushes) the streams left open by early returns
                if (s_Output is IDisposable) ((IDisposable)s_Output).Dispose();
                if (s_Input is IDisposable) ((IDisposable)s_Input).Dispose();
            }
        }

        private static int Run(string[] argv)
        {
            if (argv.Length < 1) {
                return PrintUsageAndExit();
            }

            // default options
            Options.verbose                = false;
            Options.input                  = null;
            Options.init_segment_name      = AP4_SPLIT_DEFAULT_INIT_SEGMENT_NAME;
            Options.media_segment_name     = AP4_SPLIT_DEFAULT_MEDIA_SEGMENT_NAME;
            Options.pattern_params         = AP4_SPLIT_DEFAULT_PATTERN_PARAMS;
            Options.start_number           = 1;
            Options.track_id_count         = 0;
            Options.audio_only             = false;
            Options.video_only             = false;
            Options.init_only              = false;

            // parse command line
            Ap4Result result;
            int args = 0;
            while (args < argv.Length) {
                string arg = argv[args++];
                if (arg == "--verbose") {
                    Options.verbose = true;
                } else if (arg == "--init-segment") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: missing argument after --init-segment option\n");
                        return 1;
                    }
                    Options.init_segment_name = argv[args++];
                } else if (arg == "--media-segment") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: missing argument after --media-segment option\n");
                        return 1;
                    }
                    Options.media_segment_name = argv[args++];
                } else if (arg == "--pattern-parameters") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: missing argument after --pattern-params option\n");
                        return 1;
                    }
                    Options.pattern_params = argv[args++];
                } else if (arg == "--track-id") {
                    // PORT NOTE: upstream dereferences a NULL pointer when the
                    // argument is missing; the port reports an invalid argument.
                    if (args >= argv.Length || !ParseTrackIds(argv[args++])) {
                        Eprintf("ERROR: invalid argument for --track-id\n");
                        return 1;
                    }
                } else if (arg == "--start-number") {
                    // PORT NOTE: upstream dereferences a NULL pointer when the
                    // argument is missing; the port reports a missing argument.
                    if (args >= argv.Length) {
                        Eprintf("ERROR: missing argument after --start-number option\n");
                        return 1;
                    }
                    Options.start_number = unchecked((uint)Strtoul(argv[args++]));
                } else if (arg == "--init-only") {
                    Options.init_only = true;
                } else if (arg == "--audio") {
                    Options.audio_only = true;
                } else if (arg == "--video") {
                    Options.video_only = true;
                } else if (Options.input == null) {
                    Options.input = arg;
                } else {
                    Eprintf("ERROR: unexpected argument\n");
                    return 1;
                }
            }

            // check args
            if (Options.input == null) {
                Eprintf("ERROR: missing input file name\n");
                return 1;
            }
            if ((Options.audio_only           && (Options.video_only || Options.track_id_count != 0)) ||
                (Options.video_only           && (Options.audio_only || Options.track_id_count != 0)) ||
                (Options.track_id_count != 0  && (Options.audio_only || Options.video_only         ))) {
                Eprintf("ERROR: --audio, --video and --track-id options are mutualy exclusive\n");
                return 1;
            }
            if (Ap4Utils.StringLength(Options.pattern_params) < 1) {
                Eprintf("ERROR: --pattern-params argument is too short\n");
                return 1;
            }
            if (Ap4Utils.StringLength(Options.pattern_params) > 2) {
                Eprintf("ERROR: --pattern-params argument is too long\n");
                return 1;
            }
            for (int cursor = 0; cursor < Options.pattern_params.Length && Options.pattern_params[cursor] != '\0'; ++cursor) {
                if (Options.pattern_params[cursor] != 'I' && Options.pattern_params[cursor] != 'N') {
                    Eprintf("ERROR: invalid pattern parameter '" + Options.pattern_params[cursor] + "'\n");
                    return 1;
                }
            }

            // create the input stream
            ByteStream input = null;
            result = FileByteStream.Create(Options.input, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input (" + D(result) + ")\n");
                return 1;
            }
            s_Input = input;

            // get the movie
            File file = new File(input, true);
            Movie movie = file.GetMovie();
            if (movie == null) {
                Eprintf("no movie found in file\n");
                return 1;
            }

            // filter tracks if required
            if (Options.audio_only) {
                Track track = movie.GetTrack(Track.Type.TYPE_AUDIO);
                if (track == null) {
                    Eprintf("--audio option specified, but no audio track found\n");
                    return 1;
                }
                Options.track_ids[0]   = track.GetId();
                Options.track_id_count = 1;
            } else if (Options.video_only) {
                Track track = movie.GetTrack(Track.Type.TYPE_VIDEO);
                if (track == null) {
                    Eprintf("--video option specified, but no video track found\n");
                    return 1;
                }
                Options.track_ids[0]   = track.GetId();
                Options.track_id_count = 1;
            } else if (Options.track_id_count != 0) {
                for (uint i=0; i<Options.track_id_count; i++) {
                    Track track = movie.GetTrack(Options.track_ids[i]);
                    if (track == null) {
                        Eprintf("--track-id option specified, but no such track found\n");
                        return 1;
                    }
                }
            }

            // save the init segment
            ByteStream  output = null;
            result = FileByteStream.Create(Options.init_segment_name, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file (" + D(result) + ")\n");
                return 1;
            }
            s_Output = output;
            FtypAtom ftyp = file.GetFileType();
            if (ftyp != null) {
                result = ftyp.Write(output);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot write ftyp segment (" + D(result) + ")\n");
                    return 1;
                }
            }
            if (Options.track_id_count != 0) {
                MoovAtom moov = movie.GetMoovAtom();

                // only keep the 'trak' atom that we need
                // PORT NOTE: iterates over a copy of the list, since children are
                // detached while iterating.
                foreach (Atom atom in new List<Atom>(moov.GetChildren())) {
                    if (atom.GetType() == AtomType.TRAK) {
                        TkhdAtom tkhd = atom.GetChild(AtomType.TKHD) as TkhdAtom;
                        if (tkhd != null && !TrackIdMatches(tkhd.GetTrackId())) {
                            atom.Detach();
                        }
                    }
                }

                // only keep the 'trex' atom that we need
                ContainerAtom mvex = moov.GetChild(AtomType.MVEX) as ContainerAtom;
                if (mvex != null) {
                    foreach (Atom atom in new List<Atom>(mvex.GetChildren())) {
                        if (atom.GetType() == AtomType.TREX) {
                            TrexAtom trex = atom as TrexAtom;
                            if (trex != null && !TrackIdMatches(trex.GetTrackId())) {
                                atom.Detach();
                            }
                        }
                    }
                }
            }
            result = movie.GetMoovAtom().Write(output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot write init segment (" + D(result) + ")\n");
                return 1;
            }

            {
                Atom atom = null;
                uint track_id = 0;
                DefaultAtomFactory atom_factory = new DefaultAtomFactory();
                for (;!Options.init_only;) {
                    // process the next atom
                    result = atom_factory.CreateAtomFromStream(input, out atom);
                    if (result.Failed()) break;

                    if (atom.GetType() == AtomType.MOOF) {
                        ContainerAtom moof = atom as ContainerAtom;

                        uint traf_count = 0;
                        ContainerAtom traf = null;
                        do {
                            traf = moof.GetChild(AtomType.TRAF, traf_count) as ContainerAtom;
                            if (traf == null) break;
                            TfhdAtom tfhd = traf.GetChild(AtomType.TFHD) as TfhdAtom;
                            if (tfhd == null) {
                                Eprintf("ERROR: invalid media format\n");
                                return 1;
                            }
                            track_id = tfhd.GetTrackId();
                            traf_count++;
                        } while (traf != null);

                        // check if this fragment has more than one traf
                        if (traf_count > 1) {
                            if (Options.audio_only) {
                                Eprintf("ERROR: --audio option incompatible with multi-track fragments");
                                return 1;
                            }
                            if (Options.video_only) {
                                Eprintf("ERROR: --video option incompatible with multi-track fragments");
                                return 1;
                            }
                            track_id = 0;
                        }

                        // open a new file for this fragment if this moof is a segment start
                        string segment_name;
                        if (Options.track_id_count == 0 || track_id == Options.track_ids[0]) {
                            if (output != null) {
                                if (output is IDisposable) ((IDisposable)output).Dispose();
                                output = null;
                                s_Output = null;
                            }

                            ulong[] p = new ulong[2] {0,0};
                            uint params_len = Ap4Utils.StringLength(Options.pattern_params);
                            for (uint i=0; i<params_len; i++) {
                                if (Options.pattern_params[(int)i] == 'I') {
                                    p[i] = track_id;
                                } else if (Options.pattern_params[(int)i] == 'N') {
                                    p[i] = NextFragmentIndex(track_id)+Options.start_number;
                                }
                            }
                            switch (params_len) {
                                case 1:
                                    segment_name = Sprintf(Options.media_segment_name, p, 1);
                                    break;
                                case 2:
                                    segment_name = Sprintf(Options.media_segment_name, p, 2);
                                    break;
                                default:
                                    segment_name = "";
                                    break;
                            }
                            result = FileByteStream.Create(segment_name, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
                            if (result.Failed()) {
                                Eprintf("ERROR: cannot open output file (" + D(result) + ")\n");
                                return 1;
                            }
                            s_Output = output;
                        }
                    }

                    // write the atom
                    if (output != null && atom.GetType() != AtomType.MFRA && TrackIdMatches(track_id)) {
                        atom.Write(output);
                    }
                }
            }

            // cleanup
            // (the streams are released by Main)

            return 0;
        }
    }
}
