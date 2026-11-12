// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp42Ts/Mp42Ts.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream so that
//   the bytes written are exactly those of upstream ('\n' line endings, no
//   encoding translation).
// - sprintf(buffer, <user supplied pattern>, n) is done by Sprintf(), a small
//   implementation of the printf integer conversions.
// - upstream relies on exit() to flush and close the files that are still
//   open when main() returns early: the port keeps a list of the output
//   files it opened and closes the remaining ones when Main returns.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Bento4;

namespace Bento4Sharp.Tools.Mp42Ts
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 To MPEG2-TS File Converter - Version 1.3\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2018 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   options
        +---------------------------------------------------------------------*/
        private static class Options {
            public static uint    pmt_pid;
            public static uint    audio_pid;
            public static uint    video_pid;
            public static bool    verbose;
            public static string  playlist;
            public static uint    playlist_hls_version;
            public static string  input;
            public static string  output;
            public static uint    segment_duration;
            public static uint    segment_duration_threshold;
            public static uint    pcr_offset;
        }

        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const uint DefaultSegmentDurationThreshold = 50; // milliseconds

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static ByteStream s_Stdout;
        private static readonly List<ByteStream> s_OpenFiles = new List<ByteStream>();

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

        // output->Release()
        private static void Release(ByteStream stream)
        {
            if (stream == null) return;
            s_OpenFiles.Remove(stream);
            if (stream is IDisposable) ((IDisposable)stream).Dispose();
        }

        // what exit() does with the files that are still open
        private static void CloseOpenFiles()
        {
            foreach (ByteStream stream in s_OpenFiles) {
                if (stream is IDisposable) ((IDisposable)stream).Dispose();
            }
            s_OpenFiles.Clear();
        }

        private static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(uint value) { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value
        private static string U(uint value) { return value.ToString(CultureInfo.InvariantCulture); }

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
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp42ts [options] <input> <output>\n" +
                    "Options:\n" +
                    "  --pmt-pid <pid>   (default: 0x100)\n" +
                    "  --audio-pid <pid> (default: 0x101)\n" +
                    "  --video-pid <pid> (default: 0x102)\n" +
                    "  --segment <segment-duration-in-seconds>\n" +
                    "    [with this option, the <output> name must be a 'printf' template,\n" +
                    "     like \"seg-%d.ts\"]\n" +
                    "  --segment-duration-threshold in ms (default = 50)\n" +
                    "    [only used with the --segment option]\n" +
                    "  --pcr-offset <offset> in units of 90kHz (default 10000)\n" +
                    "  --verbose\n" +
                    "  --playlist <filename>\n" +
                    "  --playlist-hls-version <n> (default=3)\n");
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
            s_OpenFiles.Add(output);

            return output;
        }

        /*----------------------------------------------------------------------
        |   ReadSample
        +---------------------------------------------------------------------*/
        private static Ap4Result ReadSample(SampleReader reader,
                                            Track        track,
                                            Sample       sample,
                                            DataBuffer   sample_data,
                                            ref double   ts,
                                            ref bool     eos)
        {
            Ap4Result result = reader.ReadSample(sample, sample_data);
            if (result.Failed()) {
                if (result == Ap4Result.ERROR_EOS) {
                    eos = true;
                } else {
                    return result;
                }
            }
            if (sample_data.GetDataSize() == 0) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            ts = (double)sample.GetDts()/(double)track.GetMediaTimeScale();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   WriteSamples
        +---------------------------------------------------------------------*/
        private static Ap4Result WriteSamples(Mpeg2TsWriter               writer,
                                              Track                       audio_track,
                                              SampleReader                audio_reader,
                                              Mpeg2TsWriter.SampleStream  audio_stream,
                                              Track                       video_track,
                                              SampleReader                video_reader,
                                              Mpeg2TsWriter.SampleStream  video_stream,
                                              uint                        segment_duration_threshold)
        {
            Sample       audio_sample = new Sample();
            DataBuffer   audio_sample_data = new DataBuffer();
            uint         audio_sample_count = 0;
            double       audio_ts = 0.0;
            bool         audio_eos = false;
            Sample       video_sample = new Sample();
            DataBuffer   video_sample_data = new DataBuffer();
            uint         video_sample_count = 0;
            double       video_ts = 0.0;
            bool         video_eos = false;
            double       last_ts = 0.0;
            uint         segment_number = 0;
            double       segment_duration = 0.0;
            ByteStream   output = null;
            ByteStream   playlist = null;
            string       string_buffer;
            Ap4Result    result = Ap4Result.SUCCESS;
            List<double> segment_durations = new List<double>();

            // prime the samples
            if (audio_reader != null) {
                result = ReadSample(audio_reader, audio_track, audio_sample, audio_sample_data, ref audio_ts, ref audio_eos);
                if (result.Failed()) goto end;
            }
            if (video_reader != null) {
                result = ReadSample(video_reader, video_track, video_sample, video_sample_data, ref video_ts, ref video_eos);
                if (result.Failed()) goto end;
            }

            for (;;) {
                bool sync_sample = false;
                Track chosen_track = null;
                if (audio_track != null && !audio_eos) {
                    chosen_track = audio_track;
                    if (video_track == null) sync_sample = true;
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
                if (chosen_track == null) break;

                // check if we need to start a new segment
                if (Options.segment_duration != 0 && sync_sample) {
                    if (video_track != null) {
                        segment_duration = video_ts - last_ts;
                    } else {
                        segment_duration = audio_ts - last_ts;
                    }
                    if (segment_duration >= (double)Options.segment_duration - (double)segment_duration_threshold/1000.0) {
                        if (video_track != null) {
                            last_ts = video_ts;
                        } else {
                            last_ts = audio_ts;
                        }
                        if (output != null) {
                            segment_durations.Add(segment_duration);
                            if (Options.verbose) {
                                Printf("Segment " + D(segment_number) + ", duration=" + F(segment_duration, 2) + ", " +
                                       D(audio_sample_count) + " audio samples, " +
                                       D(video_sample_count) + " video samples\n");
                            }
                            Release(output);
                            output = null;
                            ++segment_number;
                            audio_sample_count = 0;
                            video_sample_count = 0;
                        }
                    }
                }
                if (output == null) {
                    output = OpenOutput(Options.output, segment_number);
                    if (output == null) return Ap4Result.ERROR_CANNOT_OPEN_FILE;
                    writer.WritePAT(output);
                    writer.WritePMT(output);
                }

                // write the samples out and advance to the next sample
                if (chosen_track == audio_track) {
                    result = audio_stream.WriteSample(audio_sample,
                                                       audio_sample_data,
                                                       audio_track.GetSampleDescription(audio_sample.GetDescriptionIndex()),
                                                       video_track==null,
                                                       output);
                    if (result.Failed()) goto end;

                    result = ReadSample(audio_reader, audio_track, audio_sample, audio_sample_data, ref audio_ts, ref audio_eos);
                    if (result.Failed()) goto end;
                    ++audio_sample_count;
                } else if (chosen_track == video_track) {
                    result = video_stream.WriteSample(video_sample,
                                                       video_sample_data,
                                                       video_track.GetSampleDescription(video_sample.GetDescriptionIndex()),
                                                       true,
                                                       output);
                    if (result.Failed()) goto end;

                    result = ReadSample(video_reader, video_track, video_sample, video_sample_data, ref video_ts, ref video_eos);
                    if (result.Failed()) goto end;
                    ++video_sample_count;
                } else {
                    break;
                }
            }

            // finish the last segment
            if (output != null) {
                if (video_track != null) {
                    segment_duration = video_ts - last_ts;
                } else {
                    segment_duration = audio_ts - last_ts;
                }
                segment_durations.Add(segment_duration);
                if (Options.verbose) {
                    Printf("Segment " + D(segment_number) + ", duration=" + F(segment_duration, 2) + ", " +
                           D(audio_sample_count) + " audio samples, " +
                           D(video_sample_count) + " video samples\n");
                }
                Release(output);
                output = null;
                ++segment_number;
                audio_sample_count = 0;
                video_sample_count = 0;
            }

            // create the playlist file if needed
            if (Options.playlist != null) {
                playlist = OpenOutput(Options.playlist, 0);
                if (playlist == null) goto end;

                uint target_duration = 0;
                for (int i=0; i<segment_durations.Count; i++) {
                    if ((uint)(segment_durations[i]+0.5) > target_duration) {
                        target_duration = (uint)segment_durations[i];
                    }
                }

                playlist.WriteString("#EXTM3U\r\n");
                if (Options.playlist_hls_version > 1) {
                    string_buffer = "#EXT-X-VERSION:" + D(Options.playlist_hls_version) + "\r\n";
                    playlist.WriteString(string_buffer);
                }
                playlist.WriteString("#EXT-X-MEDIA-SEQUENCE:0\r\n");
                playlist.WriteString("#EXT-X-TARGETDURATION:");
                string_buffer = D(target_duration) + "\r\n\r\n";
                playlist.WriteString(string_buffer);

                for (int i=0; i<segment_durations.Count; i++) {
                    if (Options.playlist_hls_version >= 3) {
                        string_buffer = "#EXTINF:" + F(segment_durations[i]) + ",\r\n";
                    } else {
                        string_buffer = "#EXTINF:" + U((uint)(segment_durations[i]+0.5)) + ",\r\n";
                    }
                    playlist.WriteString(string_buffer);
                    string_buffer = Sprintf(Options.output, (uint)i);
                    playlist.WriteString(string_buffer);
                    playlist.WriteString("\r\n");
                }

                playlist.WriteString("\r\n#EXT-X-ENDLIST\r\n");
                Release(playlist);
            }

            if (Options.verbose) {
                if (video_track != null) {
                    segment_duration = video_ts - last_ts;
                } else {
                    segment_duration = audio_ts - last_ts;
                }
                Printf("Conversion complete, duration=" + F(segment_duration, 2) + " secs\n");
            }

        end:
            if (output != null) Release(output);

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
            if (argv.Length < 2) {
                return PrintUsageAndExit();
            }

            // default options
            Options.segment_duration           = 0;
            Options.pmt_pid                    = 0x100;
            Options.audio_pid                  = 0x101;
            Options.video_pid                  = 0x102;
            Options.verbose                    = false;
            Options.playlist                   = null;
            Options.playlist_hls_version       = 3;
            Options.input                      = null;
            Options.output                     = null;
            Options.segment_duration_threshold = DefaultSegmentDurationThreshold;
            Options.pcr_offset                 = (uint)Ap4.MPEG2_TS_DEFAULT_PCR_OFFSET;

            // parse command line
            Ap4Result result;
            int args = 0;
            while (args < argv.Length) {
                string arg = argv[args++];
                if (arg == "--segment") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: --segment requires a number\n");
                        return 1;
                    }
                    Options.segment_duration = Strtoul(argv[args++]);
                } else if (arg == "--segment-duration-threshold") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: --segment-duration-threshold requires a number\n");
                        return 1;
                    }
                    Options.segment_duration_threshold = Strtoul(argv[args++]);
                } else if (arg == "--verbose") {
                    Options.verbose = true;
                } else if (arg == "--pmt-pid") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: --pmt-pid requires a number\n");
                        return 1;
                    }
                    Options.pmt_pid = Strtoul(argv[args++]);
                } else if (arg == "--audio-pid") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: --audio-pid requires a number\n");
                        return 1;
                    }
                    Options.audio_pid = Strtoul(argv[args++]);
                } else if (arg == "--video-pid") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: --video-pid requires a number\n");
                        return 1;
                    }
                    Options.video_pid = Strtoul(argv[args++]);
                } else if (arg == "--pcr-offset") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: --pcr-offset requires a number\n");
                        return 1;
                    }
                    Options.pcr_offset = Strtoul(argv[args++]);
                } else if (arg == "--playlist") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: --playlist requires a filename\n");
                        return 1;
                    }
                    Options.playlist = argv[args++];
                } else if (arg == "--playlist-hls-version") {
                    if (args >= argv.Length) {
                        Eprintf("ERROR: --playlist-hls-version requires a number\n");
                        return 1;
                    }
                    Options.playlist_hls_version = Strtoul(argv[args++]);
                    if (Options.playlist_hls_version ==0) {
                        Eprintf("ERROR: --playlist-hls-version requires number > 0\n");
                        return 1;
                    }
                } else if (Options.input == null) {
                    Options.input = arg;
                } else if (Options.output == null) {
                    Options.output = arg;
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
            if (Options.output == null) {
                Eprintf("ERROR: missing output file name\n");
                return 1;
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
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return 1;
            }

            // get the audio and video tracks
            Track  audio_track = movie.GetTrack(Track.Type.TYPE_AUDIO);
            Track video_track = movie.GetTrack(Track.Type.TYPE_VIDEO);
            if (audio_track == null && video_track == null) {
                Eprintf("ERROR: no suitable tracks found\n");
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return 1;
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

            // create an MPEG2 TS Writer
            Mpeg2TsWriter writer = new Mpeg2TsWriter(unchecked((ushort)Options.pmt_pid));
            Mpeg2TsWriter.SampleStream audio_stream = null;
            Mpeg2TsWriter.SampleStream video_stream = null;

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
                    stream_type = Ap4.MPEG2_STREAM_TYPE_ISO_IEC_13818_7;
                    stream_id   = Ap4.MPEG2_TS_DEFAULT_STREAM_ID_AUDIO;
                } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AC_3) {
                    stream_type = Ap4.MPEG2_STREAM_TYPE_ATSC_AC3;
                    stream_id   = Ap4.MPEG2_TS_STREAM_ID_PRIVATE_STREAM_1;
                } else if ( sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_EC_3) {
                    stream_type = Ap4.MPEG2_STREAM_TYPE_ATSC_EAC3;
                    stream_id   = Ap4.MPEG2_TS_STREAM_ID_PRIVATE_STREAM_1;
                } else {
                    Eprintf("ERROR: audio codec not supported\n");
                    goto end;
                }

                result = writer.SetAudioStream(audio_track.GetMediaTimeScale(),
                                               (byte)stream_type,
                                               (ushort)stream_id,
                                               out audio_stream,
                                               unchecked((ushort)Options.audio_pid),
                                               null, 0,
                                               Options.pcr_offset);
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
                    stream_type = Ap4.MPEG2_STREAM_TYPE_AVC;
                } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_HEV1 ||
                           sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_HVC1 ||
                           sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_DVHE ||
                           sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_DVH1) {
                    stream_type = Ap4.MPEG2_STREAM_TYPE_HEVC;
                } else {
                    Eprintf("ERROR: video codec not supported\n");
                    goto end;
                }
                result = writer.SetVideoStream(video_track.GetMediaTimeScale(),
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

            result = WriteSamples(writer,
                                  audio_track, audio_reader, audio_stream,
                                  video_track, video_reader, video_stream,
                                  Options.segment_duration_threshold);
            if (result.Failed()) {
                Eprintf("ERROR: failed to write samples (" + D((int)result) + ")\n");
            }

        end:
            if (input is IDisposable) ((IDisposable)input).Dispose();

            return result == Ap4Result.SUCCESS?0:1;
        }
    }
}
