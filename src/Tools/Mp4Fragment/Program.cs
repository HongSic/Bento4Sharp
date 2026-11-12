// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Fragment/Mp4Fragment.cpp
// Original work Copyright 2002-2019 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream and
//   fprintf(stderr) through the raw standard error stream, so that the bytes
//   written are exactly those of upstream ('\n' line endings, no encoding
//   translation).
// - the _MSC_VER strtof adaptation is not ported.

using System;
using System.Collections.Generic;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Fragment
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    internal enum ForceSyncMode {
        AP4_FRAGMENTER_FORCE_SYNC_MODE_NONE,
        AP4_FRAGMENTER_FORCE_SYNC_MODE_AUTO,
        AP4_FRAGMENTER_FORCE_SYNC_MODE_ALL
    }

    /*----------------------------------------------------------------------
    |   options
    +---------------------------------------------------------------------*/
    internal static class Options {
        public static uint          verbosity;
        public static bool          trim;
        public static bool          debug;
        public static bool          no_tfdt;
        public static double        tfdt_start;
        public static uint          sequence_number_start;
        public static ForceSyncMode force_i_frame_sync;
        public static bool          no_zero_elst;
    }

    /*----------------------------------------------------------------------
    |   SampleArray
    +---------------------------------------------------------------------*/
    internal class SampleArray {
        public SampleArray(Track track)
        {
            _track = track;
            _sampleCount = _track.GetSampleCount();
            if (_sampleCount != 0) {
                _forcedSync = new bool[_sampleCount];
                for (uint i=0; i<_sampleCount; i++) {
                    _forcedSync[i] = false;
                }
            } else {
                _forcedSync = null;
            }
        }

        public virtual uint GetSampleCount() {
            return _sampleCount;
        }
        public virtual Ap4Result GetSample(uint index, Sample sample) {
            Ap4Result result = _track.GetSample(index, sample);
            if (result.Succeeded()) {
                if (_forcedSync[index]) {
                    sample.SetSync(true);
                }
            }
            return result;
        }
        public virtual Ap4Result AddSample(Sample sample) {
            return Ap4Result.ERROR_NOT_SUPPORTED;
        }
        public virtual void ForceSync(uint index) {
            if (index < _sampleCount) {
                _forcedSync[index] = true;
            }
        }

        protected Track   _track;
        protected uint    _sampleCount;
        protected bool[]  _forcedSync;
    }

    /*----------------------------------------------------------------------
    |   CachedSampleArray
    +---------------------------------------------------------------------*/
    internal class CachedSampleArray : SampleArray {
        public CachedSampleArray(Track track) :
            base(track) {}

        public override uint GetSampleCount() {
            return (uint)_samples.Count;
        }
        public override Ap4Result GetSample(uint index, Sample sample) {
            if (index >= (uint)_samples.Count) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            } else {
                sample.Assign(_samples[(int)index]);
                return Ap4Result.SUCCESS;
            }
        }
        public override Ap4Result AddSample(Sample sample) {
            _samples.Add(new Sample(sample)); // m_Samples.Append(sample): copy by value
            return Ap4Result.SUCCESS;
        }

        protected List<Sample> _samples = new List<Sample>();
    }

    /*----------------------------------------------------------------------
    |   TrackCursor
    +---------------------------------------------------------------------*/
    internal class TrackCursor
    {
        /*----------------------------------------------------------------------
        |   TrackCursor::TrackCursor
        +---------------------------------------------------------------------*/
        public TrackCursor(Track track, SampleArray samples)
        {
            _track = track;
            _samples = samples;
            _sampleIndex = 0;
            _fragmentIndex = 0;
            _timestamp = 0;
            _unscaledTimestamp = 0;
            _eos = false;
            _tfra = new TfraAtom(0);
        }

        /*----------------------------------------------------------------------
        |   TrackCursor::Init
        +---------------------------------------------------------------------*/
        public Ap4Result Init()
        {
            return _samples.GetSample(0, _sample);
        }

        /*----------------------------------------------------------------------
        |   TrackCursor::SetSampleIndex
        +---------------------------------------------------------------------*/
        public Ap4Result SetSampleIndex(uint sample_index)
        {
            _sampleIndex = sample_index;

            // check if we're at the end
            if (sample_index >= _samples.GetSampleCount()) {
                ulong end_dts = _sample.GetDts()+_sample.GetDuration();
                _sample.Reset();
                _sample.SetDts(end_dts);
                _eos = true;
            } else {
                return _samples.GetSample(_sampleIndex, _sample);
            }

            return Ap4Result.SUCCESS;
        }

        public Track       _track;
        public SampleArray _samples;
        public uint        _sampleIndex;
        public uint        _fragmentIndex;
        public Sample      _sample = new Sample();
        public ulong       _timestamp;
        public ulong       _unscaledTimestamp;
        public bool        _eos;
        public TfraAtom    _tfra;
    }

    /*----------------------------------------------------------------------
    |   FragmentInfo
    +---------------------------------------------------------------------*/
    internal class FragmentInfo {
        public FragmentInfo(SampleArray samples, TfraAtom tfra, ulong timestamp, ContainerAtom moof)
        {
            _samples = samples;
            _tfra = tfra;
            _timestamp = timestamp;
            _duration = 0;
            _moof = moof;
            _moofPosition = 0;
            _mdatSize = 0;
        }

        public SampleArray   _samples;
        public TfraAtom      _tfra;
        public ulong         _timestamp;
        public uint          _duration;
        public List<uint>    _sampleIndexes = new List<uint>();
        public ContainerAtom _moof;
        public ulong         _moofPosition;
        public uint          _mdatSize;
    }

    /*----------------------------------------------------------------------
    |   IndexSegmentInfo
    +---------------------------------------------------------------------*/
    internal class IndexedSegmentInfo {
        public IndexedSegmentInfo() { _size = 0; _duration = 0; }
        public uint _size;
        public uint _duration;
    }

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Fragmenter - Version 1.7.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2020 Axiomatic Systems, LLC";

        private const uint AP4_FRAGMENTER_DEFAULT_FRAGMENT_DURATION   = 2000; // ms
        private const uint AP4_FRAGMENTER_MAX_AUTO_FRAGMENT_DURATION  = 40000;
        private const uint AP4_FRAGMENTER_OUTPUT_MOVIE_TIMESCALE      = 1000;

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static ByteStream s_Stdout;
        private static System.IO.Stream s_Stderr;

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
            if (s_Stderr == null) s_Stderr = Console.OpenStandardError();
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stderr.Write(bytes, 0, bytes.Length);
            s_Stderr.Flush();
        }

        private static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(uint value) { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value
        private static string D(Ap4Result value) { return D((int)value); }
        private static string LLD(ulong value) { return unchecked((long)value).ToString(CultureInfo.InvariantCulture); } // %lld of an unsigned value

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

        // strtol(str, NULL, 10)
        private static long Strtol(string str)
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
            ulong limit = negative ? 0x8000000000000000UL : 0x7FFFFFFFFFFFFFFFUL;
            while (i < str.Length && str[i] >= '0' && str[i] <= '9') {
                ulong digit = (ulong)(str[i]-'0');
                if (value > (limit-digit)/10) {
                    overflow = true;
                } else {
                    value = value*10+digit;
                }
                ++i;
            }
            if (overflow) return negative ? long.MinValue : long.MaxValue;
            return negative ? unchecked(-(long)value) : (long)value;
        }

        // strtod(str, NULL)
        // PORT NOTE: decimal and hexadecimal floating point, inf and nan.
        private static int HexDigit(string str, int i)
        {
            if (i >= str.Length) return -1;
            char c = str[i];
            if (c >= '0' && c <= '9') return c-'0';
            if (c >= 'a' && c <= 'f') return c-'a'+10;
            if (c >= 'A' && c <= 'F') return c-'A'+10;
            return -1;
        }

        private static double Strtod(string str)
        {
            int i = 0;
            while (i < str.Length && (str[i] == ' ' || (str[i] >= '\t' && str[i] <= '\r'))) ++i;
            int start = i;
            bool negative = false;
            if (i < str.Length && (str[i] == '+' || str[i] == '-')) {
                negative = str[i] == '-';
                ++i;
            }
            if (string.Compare(str, i, "infinity", 0, 8, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(str, i, "inf", 0, 3, StringComparison.OrdinalIgnoreCase) == 0) {
                return negative ? double.NegativeInfinity : double.PositiveInfinity;
            }
            if (string.Compare(str, i, "nan", 0, 3, StringComparison.OrdinalIgnoreCase) == 0) {
                return double.NaN;
            }
            if (i+1 < str.Length && str[i] == '0' && (str[i+1] == 'x' || str[i+1] == 'X') &&
                (HexDigit(str, i+2) >= 0 || (i+2 < str.Length && str[i+2] == '.' && HexDigit(str, i+3) >= 0))) {
                // hexadecimal floating point: 0x<hex digits>[.<hex digits>][p<decimal exponent>]
                i += 2;
                double mantissa = 0.0;
                int    exponent = 0;
                while (HexDigit(str, i) >= 0) {
                    mantissa = mantissa*16.0+HexDigit(str, i++);
                }
                if (i < str.Length && str[i] == '.') {
                    ++i;
                    while (HexDigit(str, i) >= 0) {
                        mantissa = mantissa*16.0+HexDigit(str, i++);
                        exponent -= 4;
                    }
                }
                if (i < str.Length && (str[i] == 'p' || str[i] == 'P')) {
                    int j = i+1;
                    bool negative_exponent = false;
                    if (j < str.Length && (str[j] == '+' || str[j] == '-')) {
                        negative_exponent = str[j] == '-';
                        ++j;
                    }
                    if (j < str.Length && str[j] >= '0' && str[j] <= '9') {
                        int e = 0;
                        while (j < str.Length && str[j] >= '0' && str[j] <= '9') {
                            if (e < 100000) e = e*10+(str[j]-'0');
                            ++j;
                        }
                        exponent += negative_exponent ? -e : e;
                    }
                }
#if NET6_0_OR_GREATER
                double hex_value = Math.ScaleB(mantissa, exponent);
#else
                double hex_value = Compat.ScaleB(mantissa, exponent);
#endif
                return negative ? -hex_value : hex_value;
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
                                 NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                                 CultureInfo.InvariantCulture,
                                 out value)) {
#else
            if (!Compat.TryParseDouble(str.Substring(start, i-start), out value)) {
#endif
                return 0.0;
            }
            return value;
        }

        // PORT NOTE: (AP4_UI64)double and (unsigned int)double. Out of range
        // values are undefined behavior in C++; the port returns 0 for them
        // (and for NaN), which is what the reference build of the original does.
        private static ulong DoubleToUI64(double value)
        {
            if (double.IsNaN(value) || value <= 0.0) return 0;
            if (value >= 18446744073709551616.0) return 0;
            return (ulong)value;
        }
        private static uint DoubleToUI32(double value)
        {
            if (double.IsNaN(value) || value <= 0.0) return 0;
            if (value >= 4294967296.0) return 0;
            return (uint)value;
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp4fragment [options] <input> <output>\n" +
                    "options are:\n" +
                    "  --verbosity <n> sets the verbosity (details) level to <n> (between 0 and 3)\n" +
                    "  --debug enable debugging information output\n" +
                    "  --quiet don't print out notice messages\n" +
                    "  --fragment-duration <milliseconds> (default = automatic)\n" +
                    "  --timescale <n> (use 10000000 for Smooth Streaming compatibility)\n" +
                    "  --track <track-id or type> only include media from one track (pass a track ID, 'audio', 'video' or 'subtitles')\n" +
                    "  --index (re)create the segment index\n" +
                    "  --trim trim excess media in longer tracks\n" +
                    "  --no-tfdt don't add 'tfdt' boxes in the fragments (may be needed for legacy Smooth Streaming clients)\n" +
                    "  --tfdt-start <start> value of the first tfdt timestamp, expressed as a floating point number in seconds\n" +
                    "  --sequence-number-start <start> value of the first segment sequence number (default: 1)\n" +
                    "  --force-i-frame-sync <auto|all> treat all I-frames as sync samples (for open-gop sequences)\n" +
                    "    'auto' only forces the flag if an open-gop source is detected, 'all' forces the flag in all cases\n" +
                    "  --copy-udta copy the moov/udta atom from input to output\n" +
                    "  --no-zero-elst don't set the last edit list entry to 0 duration\n" +
                    "  --trun-version-zero set the 'trun' box version to zero (default version: 1)\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   Fragment
        +---------------------------------------------------------------------*/
        private static void
        Fragment(File              input_file,
                 ByteStream        output_stream,
                 List<TrackCursor> cursors,
                 uint              fragment_duration,
                 uint              timescale,
                 bool              create_segment_index,
                 bool              copy_udta,
                 bool              trun_version_one)
        {
            List<FragmentInfo>       fragments = new List<FragmentInfo>();
            List<IndexedSegmentInfo> indexed_segments = new List<IndexedSegmentInfo>();
            IndexedSegmentInfo       current_indexed_segment = null;
            Ap4Result                result;

            // get the movie
            Movie  input_movie = input_file.GetMovie();
            if (input_movie == null) {
                Eprintf("ERROR: no moov found in the input file\n");
                return;
            }

            // create the output file object
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
            Movie output_movie = new Movie(AP4_FRAGMENTER_OUTPUT_MOVIE_TIMESCALE, 0, creation_time, creation_time);

            // create an mvex container
            ContainerAtom mvex = new ContainerAtom(AtomType.MVEX);
            MehdAtom      mehd = new MehdAtom(0);
            mvex.AddChild(mehd);

            // add an output track for each track in the input file
            for (int i=0; i<cursors.Count; i++) {
                Track track = cursors[i]._track;

                result = cursors[i].Init();
                if (result.Failed()) {
                    Eprintf("ERROR: failed to init sample cursor (" + D(result) + "), skipping track " + D(track.GetId()) + "\n");
                    return;
                }

                // create a sample table (with no samples) to hold the sample description
                SyntheticSampleTable sample_table = new SyntheticSampleTable();
                for (uint j=0; j<track.GetSampleDescriptionCount(); j++) {
                    SampleDescription sample_description = track.GetSampleDescription(j);
                    sample_table.AddSampleDescription(sample_description, false);
                }

                // create the track
                Track output_track = new Track(sample_table,
                                               track.GetId(),
                                               timescale != 0?timescale:AP4_FRAGMENTER_OUTPUT_MOVIE_TIMESCALE,
                                               Ap4Utils.ConvertTime(track.GetDuration(),
                                                                    input_movie.GetTimeScale(),
                                                                    timescale != 0?timescale:AP4_FRAGMENTER_OUTPUT_MOVIE_TIMESCALE),
                                               timescale != 0?timescale:track.GetMediaTimeScale(),
                                               0,//track->GetMediaDuration(),
                                               track);

                // add an edit list if needed
                if (!trun_version_one) {
                  TrakAtom trak = track.GetTrakAtom();
                  if (trak != null) {
                      ContainerAtom edts = trak.GetChild(AtomType.EDTS) as ContainerAtom;
                      if (edts != null) {
                          // create an 'edts' container
                          ContainerAtom new_edts = new ContainerAtom(AtomType.EDTS);

                          // create a new 'edts' for each original 'edts'
                          foreach (Atom edts_entry in edts.GetChildren()) {
                              ElstAtom  elst = edts_entry as ElstAtom;
                              ElstAtom new_elst = new ElstAtom();

                              // adjust the fields to match the correct timescale
                              for (int j=0; j<elst.GetEntries().Count; j++) {
                                  ElstEntry elst_entry = elst.GetEntries()[j];
                                  ElstEntry new_elst_entry = new ElstEntry(elst_entry._segmentDuration,
                                                                           elst_entry._mediaTime,
                                                                           elst_entry._mediaRate);
                                  if (j == elst.GetEntries().Count - 1 &&
                                      new_elst_entry._segmentDuration == track.GetDuration() &&
                                      !Options.no_zero_elst) {
                                      // if this is the last entry, make the segment duration 0 (i.e last until the end)
                                      // in order to be compliant with the CMAF specification
                                      new_elst_entry._segmentDuration = 0;
                                  } else {
                                      new_elst_entry._segmentDuration = Ap4Utils.ConvertTime(new_elst_entry._segmentDuration,
                                                                                             input_movie.GetTimeScale(),
                                                                                             AP4_FRAGMENTER_OUTPUT_MOVIE_TIMESCALE);
                                  }
                                  if (new_elst_entry._mediaTime > 0 && timescale != 0) {
                                      new_elst_entry._mediaTime = unchecked((long)Ap4Utils.ConvertTime((ulong)new_elst_entry._mediaTime,
                                                                                                       track.GetMediaTimeScale(),
                                                                                                       timescale != 0?timescale:track.GetMediaTimeScale()));

                                  }
                                  new_elst.AddEntry(new_elst_entry);
                              }

                              // add the 'elst' to the 'edts' container
                              new_edts.AddChild(new_elst);
                          }

                          // add the edit list to the output track (just after the 'tkhd' atom)
                          output_track.UseTrakAtom().AddChild(new_edts, 1);
                      }
                  }
                }

                // add the track to the output
                output_movie.AddTrack(output_track);

                // add a trex entry to the mvex container
                TrexAtom trex = new TrexAtom(track.GetId(),
                                             1,
                                             0,
                                             0,
                                             0);
                mvex.AddChild(trex);
            }

            // select the anchor cursor
            TrackCursor anchor_cursor = null;
            if (cursors.Count == 1) {
                // only one track, that's our anchor
                anchor_cursor = cursors[0];
            }
            if (anchor_cursor == null) {
                for (int i=0; i<cursors.Count; i++) {
                    // use this as the anchor track if it is the first video track
                    if (cursors[i]._track.GetType() == Track.Type.TYPE_VIDEO) {
                        anchor_cursor = cursors[i];
                        break;
                    }
                }
            }
            if (anchor_cursor == null) {
                // no video track to anchor with, pick the first audio track
                for (int i=0; i<cursors.Count; i++) {
                    if (cursors[i]._track.GetType() == Track.Type.TYPE_AUDIO) {
                        anchor_cursor = cursors[i];
                        break;
                    }
                }
                // no audio track to anchor with, pick the first subtitles track
                for (int i=0; i<cursors.Count; i++) {
                    if (cursors[i]._track.GetType() == Track.Type.TYPE_SUBTITLES) {
                        anchor_cursor = cursors[i];
                        break;
                    }
                }
            }
            if (anchor_cursor == null) {
                // this should never happen
                Eprintf("ERROR: no anchor track\n");
                return;
            }
            if (Options.debug) {
                Printf("Using track ID " + D(anchor_cursor._track.GetId()) + " as anchor\n");
            }

            // decide which tracks to index and in which order
            TrackCursor indexed_cursor = anchor_cursor;

            // update the mehd duration
            mehd.SetDuration(output_movie.GetDuration());

            // add the mvex container to the moov container
            output_movie.GetMoovAtom().AddChild(mvex);

            // copy the moov/udta atom to the moov container
            if (copy_udta) {
                Atom udta = input_movie.GetMoovAtom().GetChild(AtomType.UDTA);
                if (udta != null) {
                    output_movie.GetMoovAtom().AddChild(udta.Clone());
                }
            }

            // compute all the fragments
            uint sequence_number = Options.sequence_number_start;
            for(;;) {
                TrackCursor cursor = null;

                // pick the first track with a fragment index lower than the anchor's
                for (int i=0; i<cursors.Count; i++) {
                    if (cursors[i]._eos) continue;
                    if (cursors[i]._fragmentIndex < anchor_cursor._fragmentIndex) {
                        cursor = cursors[i];
                        break;
                    }
                }

                // check if we found a non-anchor cursor to use
                if (cursor == null) {
                    // the anchor should be used in this round, check if we can use it
                    if (anchor_cursor._eos) {
                        // the anchor is done, pick a new anchor unless we need to trim
                        anchor_cursor = null;
                        if (!Options.trim) {
                            for (int i=0; i<cursors.Count; i++) {
                                if (cursors[i]._eos) continue;
                                if (anchor_cursor == null ||
                                    cursors[i]._track.GetType() == Track.Type.TYPE_VIDEO ||
                                    cursors[i]._track.GetType() == Track.Type.TYPE_AUDIO) {
                                    anchor_cursor = cursors[i];
                                    if (Options.debug) {
                                        Printf("+++ New anchor: Track ID " + D(anchor_cursor._track.GetId()) + "\n");
                                    }
                                }
                            }
                        }
                    }
                    cursor = anchor_cursor;
                }
                if (cursor == null) break; // all done

                // decide how many samples go into this fragment
                ulong target_dts;
                if (cursor == anchor_cursor) {
                    // compute the current dts in milliseconds
                    ulong anchor_dts_ms = Ap4Utils.ConvertTime(cursor._sample.GetDts(),
                                                               cursor._track.GetMediaTimeScale(),
                                                               1000);
                    // round to the nearest multiple of fragment_duration
                    ulong anchor_position = (anchor_dts_ms + (fragment_duration/2))/fragment_duration;

                    // pick the next fragment_duration multiple at our target
                    target_dts = Ap4Utils.ConvertTime(fragment_duration*(anchor_position+1),
                                                      1000,
                                                      cursor._track.GetMediaTimeScale());
                } else {
                    target_dts = Ap4Utils.ConvertTime(anchor_cursor._sample.GetDts(),
                                                      anchor_cursor._track.GetMediaTimeScale(),
                                                      cursor._track.GetMediaTimeScale());
                    if (target_dts <= cursor._sample.GetDts()) {
                        // we must be at the end, past the last anchor sample, just use the target duration
                        target_dts = Ap4Utils.ConvertTime((ulong)fragment_duration*(cursor._fragmentIndex+1),
                                                          1000,
                                                          cursor._track.GetMediaTimeScale());

                        if (target_dts <= cursor._sample.GetDts()) {
                            // we're still behind, there may have been an alignment/rounding error, just advance by one segment duration
                            target_dts = cursor._sample.GetDts()+Ap4Utils.ConvertTime(fragment_duration,
                                                                                      1000,
                                                                                      cursor._track.GetMediaTimeScale());
                        }
                    }
                }

                uint end_sample_index = cursor._samples.GetSampleCount();
                ulong smallest_diff = 0xFFFFFFFFFFFFFFFFUL;
                Sample sample = new Sample();
                for (uint i=cursor._sampleIndex+1; i<=cursor._samples.GetSampleCount(); i++) {
                    ulong dts;
                    if (i < cursor._samples.GetSampleCount()) {
                        result = cursor._samples.GetSample(i, sample);
                        if (result.Failed()) {
                            Eprintf("ERROR: failed to get sample " + D(i) + " (" + D(result) + ")\n");
                            return;
                        }
                        if (!sample.IsSync()) continue; // only look for sync samples
                        dts = sample.GetDts();
                    } else {
                        result = cursor._samples.GetSample(i-1, sample);
                        if (result.Failed()) {
                            Eprintf("ERROR: failed to get sample " + D(i-1) + " (" + D(result) + ")\n");
                            return;
                        }
                        dts = sample.GetDts()+sample.GetDuration();
                    }
                    long diff = unchecked((long)(dts-target_dts));
                    ulong abs_diff = unchecked((ulong)(diff<0?-diff:diff));
                    if (abs_diff < smallest_diff) {
                        // this sample is the closest to the target so far
                        end_sample_index = i;
                        smallest_diff = abs_diff;
                    }
                    if (diff >= 0) {
                        // this sample is past the target, it is not going to get any better, stop looking
                        break;
                    }
                }
                if (cursor._eos) continue;

                if (Options.debug) {
                    if (cursor == anchor_cursor) {
                        Printf("====");
                    } else {
                        Printf("----");
                    }
                    Printf(" Track ID " + D(cursor._track.GetId()) +
                           " - dts=" + LLD(cursor._sample.GetDts()) +
                           ", target=" + LLD(target_dts) +
                           ", start=" + D(cursor._sampleIndex) +
                           ", end=" + D(end_sample_index) + "/" + D(cursor._track.GetSampleCount()) + "\n");
                }

                // emit a fragment for the selected track
                if (Options.verbosity > 1) {
                    Printf("fragment: track ID " + D(cursor._track.GetId()) + "\n");
                }

                // decide which sample description index to use
                // (this is not very sophisticated, we only look at the sample description
                // index of the first sample in the group, which may not be correct. This
                // should be fixed later)
                uint sample_desc_index = cursor._sample.GetDescriptionIndex();

                // set initial flag values
                uint tfhd_flags = Ap4.TFHD_FLAG_DEFAULT_BASE_IS_MOOF | Ap4.TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT;
                uint trun_flags = Ap4.TRUN_FLAG_DATA_OFFSET_PRESENT |
                                  Ap4.TRUN_FLAG_SAMPLE_SIZE_PRESENT;
                uint sync_sample_flags = 0;
                uint non_sync_sample_flags = 0x10000; // 0x10000 -> sample_is_non_sync
                if (cursor._track.GetType() == Track.Type.TYPE_VIDEO ||
                    (cursor._track.GetSampleDescriptionCount() > 0 && cursor._track.GetSampleDescription(0) != null &&
                     cursor._track.GetSampleDescription(0).GetFormat() == Ap4.SAMPLE_FORMAT_AC_4)) {
                    non_sync_sample_flags |= 0x1000000; // sample_depends_on=1 (not I frame)
                    sync_sample_flags     |= 0x2000000; // sample_depends_on=2 (I frame)
                }

                // setup the moof structure
                ContainerAtom moof = new ContainerAtom(AtomType.MOOF);
                MfhdAtom mfhd = new MfhdAtom(sequence_number++);
                moof.AddChild(mfhd);
                ContainerAtom traf = new ContainerAtom(AtomType.TRAF);
                TfhdAtom tfhd = new TfhdAtom(tfhd_flags,
                                             cursor._track.GetId(),
                                             0,
                                             sample_desc_index+1,
                                             0,
                                             0,
                                             0);
                traf.AddChild(tfhd);
                if (!Options.no_tfdt) {
                    TfdtAtom tfdt = new TfdtAtom(1, unchecked(cursor._timestamp + DoubleToUI64(Options.tfdt_start * (double)cursor._track.GetMediaTimeScale())));
                    traf.AddChild(tfdt);
                }

                // create the `trun` and `traf` atoms
                TrunAtom trun = new TrunAtom(trun_flags, 0, 0);
                uint initial_offset = 0;
                if (trun_version_one) {
                    trun.SetVersion(1);
                    initial_offset = cursor._sample.GetCtsDelta();
                }
                traf.AddChild(trun);
                moof.AddChild(traf);

                // create a new FragmentInfo object to store the fragment details
                FragmentInfo fragment = new FragmentInfo(cursor._samples, cursor._tfra, cursor._timestamp, moof);
                fragments.Add(fragment);

                // add samples to the fragment
                uint sample_count = 0;
                List<TrunAtom.Entry> trun_entries = new List<TrunAtom.Entry>();
                fragment._mdatSize = Ap4.ATOM_HEADER_SIZE;
                uint constant_sample_duration = 0;
                bool all_sample_durations_equal = true;
                bool all_samples_are_sync = true;
                bool only_first_sample_is_sync = true;
                for (;;) {
                    // if we have one non-zero CTS delta, we'll need to express it
                    if (cursor._sample.GetCtsDelta() != 0) {
                        trun_flags |= Ap4.TRUN_FLAG_SAMPLE_COMPOSITION_TIME_OFFSET_PRESENT;
                    }

                    // add one sample
                    TrunAtom.Entry trun_entry = new TrunAtom.Entry();
                    trun_entries.Add(trun_entry);
                    ulong next_unscaled_timestamp = cursor._unscaledTimestamp+cursor._sample.GetDuration();
                    ulong next_scaled_timestamp   = timescale != 0?
                                                    Ap4Utils.ConvertTime(next_unscaled_timestamp,
                                                                         cursor._track.GetMediaTimeScale(),
                                                                         timescale):
                                                    next_unscaled_timestamp;
                    trun_entry.sample_duration                = unchecked((uint)(next_scaled_timestamp-cursor._timestamp));
                    trun_entry.sample_size                    = cursor._sample.GetSize();
                    trun_entry.sample_flags                   = cursor._sample.IsSync() ? sync_sample_flags : non_sync_sample_flags;
                    trun_entry.sample_composition_time_offset = timescale != 0?
                                                                unchecked((uint)Ap4Utils.ConvertTime(cursor._sample.GetCtsDelta(),
                                                                                                     cursor._track.GetMediaTimeScale(),
                                                                                                     timescale)):
                                                                cursor._sample.GetCtsDelta();

                    if (trun.GetVersion() == 1) {
                        trun_entry.sample_composition_time_offset = unchecked(trun_entry.sample_composition_time_offset - initial_offset);
                    }
                    fragment._sampleIndexes.Add(cursor._sampleIndex);
                    fragment._mdatSize = unchecked(fragment._mdatSize + trun_entry.sample_size);
                    fragment._duration = unchecked(fragment._duration + trun_entry.sample_duration);

                    // check if the durations are all the same
                    if (all_sample_durations_equal) {
                        if (constant_sample_duration == 0) {
                            constant_sample_duration = trun_entry.sample_duration;
                        } else {
                            if (constant_sample_duration != trun_entry.sample_duration) {
                                all_sample_durations_equal = false;
                            }
                        }
                    }

                    // update flag metadata
                    if (cursor._sample.IsSync()) {
                        if (sample_count != 0) {
                            only_first_sample_is_sync = false;
                        }
                    } else {
                        all_samples_are_sync = false;
                    }

                    // next sample
                    cursor._unscaledTimestamp = next_unscaled_timestamp;
                    cursor._timestamp         = next_scaled_timestamp;
                    result = cursor.SetSampleIndex(cursor._sampleIndex+1);
                    if (result.Failed()) {
                        Eprintf("ERROR: failed to get sample " + D(cursor._sampleIndex+1) + " (" + D(result) + ")\n");
                        return;
                    }
                    sample_count++;
                    if (cursor._eos) {
                        if (Options.debug) {
                            Printf("[Track ID " + D(cursor._track.GetId()) + " has reached the end]\n");
                        }
                        break;
                    }
                    if (cursor._sampleIndex >= end_sample_index) {
                        break; // done with this fragment
                    }
                }
                if (Options.verbosity > 2) {
                    Printf(" " + D(sample_count) + " samples\n");
                    Printf(" constant sample duration: " + (all_sample_durations_equal?"yes":"no") + "\n");
                }

                // update the flags
                if (only_first_sample_is_sync) {
                    trun_flags |= Ap4.TRUN_FLAG_FIRST_SAMPLE_FLAGS_PRESENT;
                    trun.SetFirstSampleFlags(sync_sample_flags);
                    tfhd_flags |= Ap4.TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT;
                    tfhd.SetDefaultSampleFlags(non_sync_sample_flags);
                } else {
                    if (all_samples_are_sync) {
                        tfhd_flags |= Ap4.TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT;
                        tfhd.SetDefaultSampleFlags(0);
                    } else {
                        trun_flags |= Ap4.TRUN_FLAG_SAMPLE_FLAGS_PRESENT;
                    }
                }

                if (all_sample_durations_equal) {
                    tfhd_flags |= Ap4.TFHD_FLAG_DEFAULT_SAMPLE_DURATION_PRESENT;
                    tfhd.SetDefaultSampleDuration(constant_sample_duration);
                } else {
                    trun_flags |= Ap4.TRUN_FLAG_SAMPLE_DURATION_PRESENT;
                }

                tfhd.UpdateFlags(tfhd_flags);
                trun.UpdateFlags(trun_flags);

                // update moof and children
                trun.SetEntries(trun_entries);
                trun.SetDataOffset(unchecked((int)((uint)moof.GetSize()+Ap4.ATOM_HEADER_SIZE)));

                // keep track of fragments that will be part of the index
                if (cursor == anchor_cursor) {
                    // start a new segment
                    current_indexed_segment = new IndexedSegmentInfo();
                    indexed_segments.Add(current_indexed_segment);
                    current_indexed_segment._duration = fragment._duration;
                }
                if (current_indexed_segment != null) {
                    current_indexed_segment._size = unchecked(current_indexed_segment._size + (uint)(fragment._moof.GetSize()+fragment._mdatSize));
                }

                // advance the cursor's fragment index
                ++cursor._fragmentIndex;
            }

            // write the ftyp atom
            FtypAtom ftyp = input_file.GetFileType();
            if (ftyp != null) {
                // keep the existing brand and compatible brands
                List<uint> compatible_brands = new List<uint>(ftyp.GetCompatibleBrands().Count+1);
                for (int i=0; i<ftyp.GetCompatibleBrands().Count; i++) {
                    compatible_brands.Add(ftyp.GetCompatibleBrands()[i]);
                }

                // add the compatible brand if it is not already there
                if (!ftyp.HasCompatibleBrand(Ap4.FILE_BRAND_ISO5)) {
                    compatible_brands.Add(Ap4.FILE_BRAND_ISO5);
                }

                // create a replacement
                FtypAtom new_ftyp = new FtypAtom(ftyp.GetMajorBrand(),
                                                 ftyp.GetMinorVersion(),
                                                 compatible_brands.ToArray(),
                                                 (uint)compatible_brands.Count);
                ftyp = new_ftyp;
            } else {
                uint[] compat = new uint[2] {
                    Ap4.FILE_BRAND_ISOM,
                    Ap4.FILE_BRAND_ISO5
                };
                ftyp = new FtypAtom(Ap4.FTYP_BRAND_MP42, 0, compat, 2);
            }
            ftyp.Write(output_stream);

            // write the moov atom
            output_movie.GetMoovAtom().Write(output_stream);

            // write the (not-yet fully computed) indexes if needed
            SidxAtom  sidx = null;
            ulong     sidx_position = 0;
            output_stream.Tell(out sidx_position);
            if (create_segment_index) {
                uint sidx_timescale = timescale != 0 ? timescale : indexed_cursor._track.GetMediaTimeScale();
                ulong earliest_presentation_time = DoubleToUI64(Options.tfdt_start * (double)sidx_timescale);
                sidx = new SidxAtom(indexed_cursor._track.GetId(),
                                    sidx_timescale,
                                    earliest_presentation_time,
                                    0);
                // reserve space for the entries now, but they will be computed and updated later
                sidx.SetReferenceCount((uint)indexed_segments.Count);
                sidx.Write(output_stream);
            }

            // write all fragments
            foreach (FragmentInfo fragment in fragments) {
                // remember the time and position of this fragment
                output_stream.Tell(out fragment._moofPosition);
                fragment._tfra.AddEntry(fragment._timestamp, fragment._moofPosition);

                // write the moof
                fragment._moof.Write(output_stream);

                // write mdat
                output_stream.WriteUI32(fragment._mdatSize);
                output_stream.WriteUI32(AtomType.MDAT);
                DataBuffer sample_data = new DataBuffer();
                Sample     sample = new Sample();
                for (int i=0; i<fragment._sampleIndexes.Count; i++) {
                    // get the sample
                    result = fragment._samples.GetSample(fragment._sampleIndexes[i], sample);
                    if (result.Failed()) {
                        Eprintf("ERROR: failed to get sample " + D(fragment._sampleIndexes[i]) + " (" + D(result) + ")\n");
                        return;
                    }

                    // read the sample data
                    result = sample.ReadData(sample_data);
                    if (result.Failed()) {
                        Eprintf("ERROR: failed to read sample data for sample " + D(fragment._sampleIndexes[i]) + " (" + D(result) + ")\n");
                        return;
                    }

                    // write the sample data
                    result = output_stream.Write(sample_data.GetData(), sample_data.GetDataSize());
                    if (result.Failed()) {
                        Eprintf("ERROR: failed to write sample data (" + D(result) + ")\n");
                        return;
                    }
                }
            }

            // update the index and re-write it if needed
            if (create_segment_index) {
                uint segment_index = 0;
                SidxAtom.Reference reference = new SidxAtom.Reference();
                foreach (IndexedSegmentInfo segment in indexed_segments) {
                    // update the sidx entry
                    reference._referencedSize     = segment._size;
                    reference._subsegmentDuration = segment._duration;
                    reference._startsWithSap      = true;
                    reference._sapType            = 1;
                    sidx.SetReference(segment_index++, reference);
                }
                ulong here = 0;
                output_stream.Tell(out here);
                output_stream.Seek(sidx_position);
                sidx.Write(output_stream);
                output_stream.Seek(here);
            }

            // create an mfra container and write out the index
            ContainerAtom mfra = new ContainerAtom(AtomType.MFRA);
            for (int i=0; i<cursors.Count; i++) {
                mfra.AddChild(cursors[i]._tfra);
                cursors[i]._tfra = null;
            }
            MfroAtom mfro = new MfroAtom(unchecked((uint)mfra.GetSize()+16));
            mfra.AddChild(mfro);
            result = mfra.Write(output_stream);
            if (result.Failed()) {
                Eprintf("ERROR: failed to write 'mfra' (" + D(result) + ")\n");
                return;
            }
        }

        /*----------------------------------------------------------------------
        |   AutoDetectFragmentDuration
        +---------------------------------------------------------------------*/
        private static uint
        AutoDetectFragmentDuration(TrackCursor cursor)
        {
            Sample sample = new Sample();
            uint   sample_count = cursor._samples.GetSampleCount();

            // get the first sample as the starting point
            Ap4Result result = cursor._samples.GetSample(0, sample);
            if (result.Failed()) {
                Eprintf("ERROR: failed to read first sample\n");
                return 0;
            }
            if (!sample.IsSync()) {
                Eprintf("ERROR: first sample is not an I frame\n");
                return 0;
            }

            for (uint interval = 1; interval < sample_count; interval++) {
                bool irregular = false;
                uint sync_count = 0;
                uint i;
                for (i = 0; i < sample_count; i += interval) {
                    result = cursor._samples.GetSample(i, sample);
                    if (result.Failed()) {
                        Eprintf("ERROR: failed to read sample " + D(i) + "\n");
                        return 0;
                    }
                    if (!sample.IsSync()) {
                        irregular = true;
                        break;
                    }
                    ++sync_count;
                }
                if (sync_count < 1) continue;
                if (!irregular) {
                    // found a pattern
                    ulong duration = sample.GetDts();
                    double fps = (double)unchecked(interval*(sync_count-1))/((double)duration/(double)cursor._track.GetMediaTimeScale());
                    if (Options.verbosity > 0) {
                        Printf("found regular I-frame interval: " + D(interval) + " frames (at " +
#if NET6_0_OR_GREATER
                               ((double)(float)fps).ToString("F3", CultureInfo.InvariantCulture) + " frames per second)\n");
#else
                               // (Compat.FormatFixed is for finite values; the text of the others is the same on every runtime)
                               (float.IsNaN((float)fps) || float.IsInfinity((float)fps) ? ((double)(float)fps).ToString("F3", CultureInfo.InvariantCulture)
                                                                             : Compat.FormatFixed((double)(float)fps, 3)) + " frames per second)\n");
#endif
                    }
                    return DoubleToUI32(1000.0*(double)interval/fps);
                }
            }

            return 0;
        }

        /*----------------------------------------------------------------------
        |   AutoDetectAudioFragmentDuration
        +---------------------------------------------------------------------*/
        private static uint
        AutoDetectAudioFragmentDuration(ByteStream stream, TrackCursor cursor)
        {
            // remember where we are in the stream
            ulong where = 0;
            stream.Tell(out where);
            ulong stream_size = 0;
            stream.GetSize(out stream_size);
            ulong bytes_available = unchecked(stream_size-where);

            ulong fragment_count = 0;
            uint  last_fragment_size = 0;
            Atom  atom = null;
            DefaultAtomFactory atom_factory = new DefaultAtomFactory();
            while (atom_factory.CreateAtomFromStream(stream, ref bytes_available, out atom).Succeeded()) {
                if (atom != null && atom.GetType() == AtomType.MOOF) {
                    ContainerAtom moof = atom as ContainerAtom;
                    TfhdAtom tfhd = moof.FindChild("traf/tfhd") as TfhdAtom;
                    if (tfhd != null && tfhd.GetTrackId() == cursor._track.GetId()) {
                        ++fragment_count;
                        TrunAtom trun = moof.FindChild("traf/trun") as TrunAtom;
                        if (trun != null) {
                            last_fragment_size = (uint)trun.GetEntries().Count;
                        }
                    }
                }
                atom = null;
            }

            // restore the stream to its original position
            stream.Seek(where);

            // decide if we can infer an fragment size
            if (fragment_count == 0 || cursor._samples.GetSampleCount() == 0) {
                return 0;
            }
            // don't count the last fragment if we have more than one
            if (fragment_count > 1 && last_fragment_size != 0) {
                --fragment_count;
            }
            if (fragment_count <= 1 || cursor._samples.GetSampleCount() < last_fragment_size) {
                last_fragment_size = 0;
            }
            Sample sample = new Sample();
            ulong total_duration = 0;
            for (uint i=0; i<cursor._samples.GetSampleCount()-last_fragment_size; i++) {
                cursor._samples.GetSample(i, sample);
                total_duration += sample.GetDuration();
            }
            return unchecked((uint)Ap4Utils.ConvertTime(total_duration/fragment_count, cursor._track.GetMediaTimeScale(), 1000));
        }

        /*----------------------------------------------------------------------
        |   ReadGolomb
        +---------------------------------------------------------------------*/
        private static uint
        ReadGolomb(BitStream bits)
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
        |   IsIFrame
        +---------------------------------------------------------------------*/
        private static bool
        IsIFrame(Sample sample, AvcSampleDescription avc_desc) {
            DataBuffer sample_data = new DataBuffer();
            if (sample.ReadData(sample_data).Failed()) {
                return false;
            }

            // PORT NOTE: `data` is an offset into the sample buffer. Upstream reads
            // through a raw pointer and may read past the end of the sample when
            // the data is malformed; the port treats out of bounds bytes as 0.
            byte[] buffer = sample_data.GetData();
            uint   buffer_size = sample_data.GetDataSize();
            uint   data = 0;
            uint   size = buffer_size;

            while (size >= avc_desc.GetNaluLengthSize()) {
                uint nalu_length = 0;
                if (avc_desc.GetNaluLengthSize() == 1) {
                    nalu_length = ByteAt(buffer, buffer_size, data++);
                    --size;
                } else if (avc_desc.GetNaluLengthSize() == 2) {
                    nalu_length = ((uint)ByteAt(buffer, buffer_size, data) << 8) |
                                   (uint)ByteAt(buffer, buffer_size, data+1);
                    data += 2;
                    size -= 2;
                } else if (avc_desc.GetNaluLengthSize() == 4) {
                    nalu_length = ((uint)ByteAt(buffer, buffer_size, data  ) << 24) |
                                  ((uint)ByteAt(buffer, buffer_size, data+1) << 16) |
                                  ((uint)ByteAt(buffer, buffer_size, data+2) <<  8) |
                                   (uint)ByteAt(buffer, buffer_size, data+3);
                    data += 4;
                    size -= 4;
                } else {
                    return false;
                }
                if (nalu_length <= size) {
                    size -= nalu_length;
                } else {
                    size = 0;
                }

                switch (ByteAt(buffer, buffer_size, data) & 0x1F) {
                    case 1: {
                        BitStream bits = new BitStream();
                        byte[] slice_header = new byte[8];
                        for (uint i=0; i<8; i++) {
                            slice_header[i] = ByteAt(buffer, buffer_size, unchecked(data+1+i));
                        }
                        bits.WriteBytes(slice_header, 8);
                        ReadGolomb(bits);
                        uint slice_type = ReadGolomb(bits);
                        if (slice_type == 2 || slice_type == 7) {
                            return true;
                        } else {
                            return false; // only show first slice type
                        }
                    }

                    case 5:
                        return true;
                }

                data = unchecked(data+nalu_length);
            }

            return false;
        }

        private static byte ByteAt(byte[] buffer, uint buffer_size, uint offset)
        {
            return offset < buffer_size ? buffer[offset] : (byte)0;
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

            // init the variables
            string   input_filename                = null;
            string   output_filename               = null;
            string   track_selector                = null;
            uint     fragment_duration             = 0;
            bool     auto_detect_fragment_duration = true;
            bool     create_segment_index          = false;
            bool     quiet                         = false;
            bool     copy_udta                     = false;
            bool     trun_version_one              = true;
            uint     timescale                     = 0;
            Ap4Result result;

            Options.verbosity             = 1;
            Options.debug                 = false;
            Options.trim                  = false;
            Options.no_tfdt               = false;
            Options.tfdt_start            = 0.0;
            Options.sequence_number_start = 1;
            Options.force_i_frame_sync    = ForceSyncMode.AP4_FRAGMENTER_FORCE_SYNC_MODE_NONE;

            // parse the command line
            int argi = 0;
            while (argi < args.Length) {
                string arg = args[argi++];
                if (arg == "--verbosity") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --verbosity option\n");
                        return 1;
                    }
                    Options.verbosity = unchecked((uint)Strtoul(arg));
                } else if (arg == "--debug") {
                    Options.debug = true;
                } else if (arg == "--index") {
                    create_segment_index = true;
                } else if (arg == "--quiet") {
                    quiet = true;
                } else if (arg == "--trim") {
                    Options.trim = true;
                } else if (arg == "--no-tfdt") {
                    Options.no_tfdt = true;
                } else if (arg == "--tfdt-start") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --tfdt-start option\n");
                        return 1;
                    }
                    Options.tfdt_start = Strtod(arg);
                } else if (arg == "--sequence-number-start") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --sequence-number-start option\n");
                        return 1;
                    }
                    Options.sequence_number_start = unchecked((uint)Strtoul(arg));
                } else if (arg == "--force-i-frame-sync") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --fragment-duration option\n");
                        return 1;
                    }
                    if (arg == "all") {
                        Options.force_i_frame_sync = ForceSyncMode.AP4_FRAGMENTER_FORCE_SYNC_MODE_ALL;
                    } else if (arg == "auto") {
                        Options.force_i_frame_sync = ForceSyncMode.AP4_FRAGMENTER_FORCE_SYNC_MODE_AUTO;
                    } else {
                        Eprintf("ERROR: unknown mode for --force-i-frame-sync\n");
                        return 1;
                    }
                } else if (arg == "--fragment-duration") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --fragment-duration option\n");
                        return 1;
                    }
                    fragment_duration = unchecked((uint)Strtoul(arg));
                    auto_detect_fragment_duration = false;
                } else if (arg == "--timescale") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --timescale option\n");
                        return 1;
                    }
                    timescale = unchecked((uint)Strtoul(arg));
                } else if (arg == "--track") {
                    track_selector = argi < args.Length ? args[argi++] : null;
                    if (track_selector == null) {
                        Eprintf("ERROR: missing argument after --track option\n");
                        return 1;
                    }
                } else if (arg == "--trun-version-zero") {
                    trun_version_one = false;
                } else if (arg == "--copy-udta") {
                    copy_udta = true;
                } else if (arg == "--no-zero-elst") {
                    Options.no_zero_elst = true;
                } else {
                    if (input_filename == null) {
                        input_filename = arg;
                    } else if (output_filename == null) {
                        output_filename = arg;
                    } else {
                        Eprintf("ERROR: unexpected argument '" + arg + "'\n");
                        return 1;
                    }
                }
            }
            if (Options.debug && Options.verbosity == 0) {
                Options.verbosity = 1;
            }

            if (input_filename == null) {
                Eprintf("ERROR: no input specified\n");
                return 1;
            }
            ByteStream input_stream = null;
            result = FileByteStream.Create(input_filename,
                                           FileByteStream.Mode.STREAM_MODE_READ,
                                           out input_stream);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input (" + D(result) + ")\n");
                return 1;
            }
            if (output_filename == null) {
                Eprintf("ERROR: no output specified\n");
                return 1;
            }
            ByteStream output_stream = null;
            result = FileByteStream.Create(output_filename,
                                           FileByteStream.Mode.STREAM_MODE_WRITE,
                                           out output_stream);
            if (result.Failed()) {
                Eprintf("ERROR: cannot create/open output (" + D(result) + ")\n");
                return 1;
            }

            // PORT NOTE: the body below is upstream's main() from this point on;
            // it is wrapped so that the streams are closed on every return path
            // (upstream relies on the C runtime flushing open files at exit).
            try {
                return Run2(input_stream, output_stream, track_selector, fragment_duration,
                            auto_detect_fragment_duration, create_segment_index, quiet, copy_udta,
                            trun_version_one, timescale);
            } finally {
                // cleanup and exit
                if (input_stream is IDisposable) ((IDisposable)input_stream).Dispose();
                if (output_stream is IDisposable) ((IDisposable)output_stream).Dispose();
            }
        }

        private static int Run2(ByteStream input_stream,
                                ByteStream output_stream,
                                string     track_selector,
                                uint       fragment_duration,
                                bool       auto_detect_fragment_duration,
                                bool       create_segment_index,
                                bool       quiet,
                                bool       copy_udta,
                                bool       trun_version_one,
                                uint       timescale)
        {
            Ap4Result result;

            // parse the input MP4 file (moov only)
            File input_file = new File(input_stream, true);

            // check the file for basic properties
            if (input_file.GetMovie() == null) {
                Eprintf("ERROR: no movie found in the file\n");
                return 1;
            }
            if (!quiet && input_file.GetMovie().HasFragments()) {
                Eprintf("NOTICE: file is already fragmented, it will be re-fragmented\n");
            }

            // create a cusor list to keep track of the tracks we will read from
            List<TrackCursor> cursors = new List<TrackCursor>();

            // iterate over all tracks
            TrackCursor   video_track           = null;
            TrackCursor   audio_track           = null;
            TrackCursor   subtitles_track       = null;
            TrackCursor   selected_track        = null;
            uint          video_track_count     = 0;
            uint          audio_track_count     = 0;
            uint          subtitles_track_count = 0;
            foreach (Track track in input_file.GetMovie().GetTracks()) {
                // sanity check
                if (track.GetSampleCount() == 0 && !input_file.GetMovie().HasFragments()) {
                    Eprintf("WARNING: track " + D(track.GetId()) + " has no samples, it will be skipped\n");
                    continue;
                }

                // create a sample array for this track
                SampleArray sample_array;
                if (input_file.GetMovie().HasFragments()) {
                    sample_array = new CachedSampleArray(track);
                } else {
                    sample_array = new SampleArray(track);
                }

                // create a cursor for the track
                TrackCursor cursor = new TrackCursor(track, sample_array);
                cursor._tfra.SetTrackId(track.GetId());
                cursors.Add(cursor);

                if (track.GetType() == Track.Type.TYPE_VIDEO) {
                    if (video_track != null) {
                        Eprintf("WARNING: more than one video track found\n");
                    } else {
                        video_track = cursor;
                    }
                    video_track_count++;
                } else if (track.GetType() == Track.Type.TYPE_AUDIO) {
                    if (audio_track == null) {
                        audio_track = cursor;
                    }
                    audio_track_count++;
                } else if (track.GetType() == Track.Type.TYPE_SUBTITLES) {
                    if (subtitles_track == null) {
                        subtitles_track = cursor;
                    }
                    subtitles_track_count++;
                }
            }

            if (cursors.Count == 0) {
                Eprintf("ERROR: no valid track found\n");
                return 1;
            }

            if (track_selector != null) {
                if (track_selector.StartsWith("audio", StringComparison.Ordinal)) {
                    if (audio_track != null) {
                        selected_track = audio_track;
                    } else {
                        Eprintf("ERROR: no audio track found\n");
                        return 1;
                    }
                } else if (track_selector.StartsWith("video", StringComparison.Ordinal)) {
                    if (video_track != null) {
                        selected_track = video_track;
                    } else {
                        Eprintf("ERROR: no video track found\n");
                        return 1;
                    }
                } else if (track_selector.StartsWith("subtitles", StringComparison.Ordinal)) {
                    if (subtitles_track != null) {
                        selected_track = subtitles_track;
                    } else {
                        Eprintf("ERROR: no subtitles track found\n");
                        return 1;
                    }
                } else {
                    uint selected_track_id = unchecked((uint)Strtol(track_selector));
                    for (int i=0; i<cursors.Count; i++) {
                        if (cursors[i]._track.GetId() == selected_track_id) {
                            selected_track = cursors[i];
                            break;
                        }
                    }
                    if (selected_track == null) {
                        Eprintf("ERROR: track not found\n");
                        return 1;
                    }
                }
            }

            if (video_track_count == 0 && audio_track_count == 0 && subtitles_track_count == 0) {
                Eprintf("ERROR: no audio, video, or subtitles track in the file\n");
                return 1;
            }
            AvcSampleDescription avc_desc = null;
            if (video_track != null && (Options.force_i_frame_sync != ForceSyncMode.AP4_FRAGMENTER_FORCE_SYNC_MODE_NONE)) {
                // that feature is only supported for AVC
                SampleDescription  sdesc = video_track._track.GetSampleDescription(0);
                if (sdesc != null) {
                    avc_desc = sdesc as AvcSampleDescription;
                }
                if (avc_desc == null) {
                    Eprintf("--force-i-frame-sync can only be used with AVC/H.264 video\n");
                    return 1;
                }
            }

            // remember where the stream was
            ulong position;
            input_stream.Tell(out position);

            // for fragmented input files, we need to populate the sample arrays
            if (input_file.GetMovie().HasFragments()) {
                LinearReader reader = new LinearReader(input_file.GetMovie(), input_stream);
                for (int i=0; i<cursors.Count; i++) {
                    reader.EnableTrack(cursors[i]._track.GetId());
                }
                uint track_id;
                Sample sample = new Sample();
                do {
                    result = reader.GetNextSample(sample, out track_id);
                    if (result.Succeeded()) {
                        for (int i=0; i<cursors.Count; i++) {
                            if (cursors[i]._track.GetId() == track_id) {
                                cursors[i]._samples.AddSample(sample);
                                break;
                            }
                        }
                    }
                } while (result.Succeeded());

            } else if (video_track != null && (Options.force_i_frame_sync != ForceSyncMode.AP4_FRAGMENTER_FORCE_SYNC_MODE_NONE)) {
                Sample sample = new Sample();
                if (Options.force_i_frame_sync == ForceSyncMode.AP4_FRAGMENTER_FORCE_SYNC_MODE_AUTO) {
                    // detect if this looks like an open-gop source
                    for (uint i=1; i<video_track._samples.GetSampleCount(); i++) {
                        if (video_track._samples.GetSample(i, sample).Succeeded()) {
                            if (sample.IsSync()) {
                                // we found a sync i-frame, assume this is *not* an open-gop source
                                Options.force_i_frame_sync = ForceSyncMode.AP4_FRAGMENTER_FORCE_SYNC_MODE_NONE;
                                if (Options.debug) {
                                    Printf("this does not look like an open-gop source, not forcing i-frame sync flags\n");
                                }
                                break;
                            }
                        }
                    }
                }
                if (Options.force_i_frame_sync != ForceSyncMode.AP4_FRAGMENTER_FORCE_SYNC_MODE_NONE) {
                    for (uint i=0; i<video_track._samples.GetSampleCount(); i++) {
                        if (video_track._samples.GetSample(i, sample).Succeeded()) {
                            if (IsIFrame(sample, avc_desc)) {
                                video_track._samples.ForceSync(i);
                            }
                        }
                    }
                }
            }

            // return the stream to its original position
            input_stream.Seek(position);

            // auto-detect the fragment duration if needed
            if (auto_detect_fragment_duration) {
                if (video_track != null) {
                    fragment_duration = AutoDetectFragmentDuration(video_track);
                } else if (audio_track != null && input_file.GetMovie().HasFragments()) {
                    fragment_duration = AutoDetectAudioFragmentDuration(input_stream, audio_track);
                }
                if (fragment_duration == 0) {
                    if (Options.verbosity > 0) {
                        Eprintf("unable to autodetect fragment duration, using default\n");
                    }
                    fragment_duration = AP4_FRAGMENTER_DEFAULT_FRAGMENT_DURATION;
                } else if (fragment_duration > AP4_FRAGMENTER_MAX_AUTO_FRAGMENT_DURATION) {
                    if (Options.verbosity > 0) {
                        Eprintf("auto-detected fragment duration too large, using default\n");
                    }
                    fragment_duration = AP4_FRAGMENTER_DEFAULT_FRAGMENT_DURATION;
                }
            }

            // fragment the file
            List<TrackCursor> tracks_to_fragment = new List<TrackCursor>();
            if (selected_track != null) {
                tracks_to_fragment.Add(selected_track);
            } else {
                tracks_to_fragment = new List<TrackCursor>(cursors);
            }
            Fragment(input_file, output_stream, tracks_to_fragment, fragment_duration, timescale, create_segment_index, copy_udta, trun_version_one);

            return 0;
        }
    }
}
