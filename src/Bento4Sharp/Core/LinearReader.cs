// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4LinearReader.h, Ap4LinearReader.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint LINEAR_READER_INITIALIZED = 1;
        public const uint LINEAR_READER_FLAG_EOS    = 2;
    }

    /*----------------------------------------------------------------------
    |   AP4_LinearReader
    +---------------------------------------------------------------------*/
    public class LinearReader
    {
        /*----------------------------------------------------------------------
        |   AP4_LinearReader::AP4_LinearReader
        +---------------------------------------------------------------------*/
        public LinearReader(Movie       movie,
                            ByteStream  fragment_stream = null)
        {
            _movie = movie;
            _fragment = null;
            _fragmentStream = fragment_stream;
            _currentFragmentPosition = 0;
            _nextFragmentPosition = 0;
            _bufferFullness = 0;
            _bufferFullnessPeak = 0;
            _mfra = null;

            _hasFragments = movie.HasFragments();
            if (fragment_stream != null) {
                fragment_stream.Tell(out _currentFragmentPosition);
                _nextFragmentPosition = _currentFragmentPosition;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::EnableTrack
        +---------------------------------------------------------------------*/
        public Ap4Result EnableTrack(uint track_id)
        {
            // check if we don't already have this
            if (FindTracker(track_id) != null) return Ap4Result.SUCCESS;

            // find the track in the movie
            Track track = _movie.GetTrack(track_id);
            if (track == null) return Ap4Result.ERROR_NO_SUCH_ITEM;

            // process this track
            return ProcessTrack(track);
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::FlushQueue
        +---------------------------------------------------------------------*/
        protected void FlushQueue(Tracker tracker)
        {
            // empty any queued samples
            for (int item = 0; item < tracker._samples.Count; item++) {
                SampleBuffer buffer = tracker._samples[item];
                _bufferFullness -= buffer._data.GetDataSize();
            }
            tracker._samples.Clear();
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::FlushQueues
        +---------------------------------------------------------------------*/
        protected void FlushQueues()
        {
            for (int i=0; i<_trackers.Count; i++) {
                FlushQueue(_trackers[i]);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::SetSampleIndex
        +---------------------------------------------------------------------*/
        public Ap4Result SetSampleIndex(uint track_id, uint sample_index)
        {
            Tracker tracker = FindTracker(track_id);
            if (tracker == null) return Ap4Result.ERROR_INVALID_PARAMETERS;
            Ap4.ASSERT(tracker._sampleTable != null);
            tracker._nextSample = null;
            if (sample_index >= tracker._sampleTable.GetSampleCount()) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }
            tracker._eos = false;
            tracker._nextSampleIndex = sample_index;

            // empty any queued samples
            for (int item = 0; item < tracker._samples.Count; item++) {
                SampleBuffer buffer = tracker._samples[item];
                _bufferFullness -= buffer._data.GetDataSize();
            }
            tracker._samples.Clear();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::SeekTo
        +---------------------------------------------------------------------*/
        // PORT NOTE: `AP4_UI32* actual_time_ms = 0` -> two overloads; the one
        // without the out parameter behaves like upstream called with NULL.
        public Ap4Result SeekTo(uint time_ms)
        {
            uint unused1;
            return SeekTo(time_ms, out unused1);
        }

        public Ap4Result SeekTo(uint time_ms, out uint actual_time_ms)
        {
            actual_time_ms = time_ms; // default

            // we only support fragmented sources for now
            if (!_hasFragments) return Ap4Result.ERROR_NOT_SUPPORTED;

            // look for a fragment index
            if (_mfra == null) {
                if (_fragmentStream != null) {
                    // get the size of the stream (needed)
                    ulong stream_size = 0;
                    _fragmentStream.GetSize(out stream_size);

                    if (stream_size > 12) {
                        // remember where we are
                        ulong here;
                        _fragmentStream.Tell(out here);

                        // read the last 12 bytes
                        byte[] mfro = new byte[12];
                        Ap4Result result = _fragmentStream.Seek(stream_size-12);
                        if (result.Succeeded()) {
                            result = _fragmentStream.Read(mfro, 12);
                        }
                        if (result.Succeeded() && mfro[0] == (byte)'m' && mfro[1] == (byte)'f' && mfro[2] == (byte)'r' && mfro[3] == (byte)'o') {
                            uint mfra_size = Ap4Utils.BytesToUInt32BE(mfro, 8);
                            if ((ulong)mfra_size < stream_size) {
                                result = _fragmentStream.Seek(stream_size-mfra_size);
                                if (result.Succeeded()) {
                                    Atom  mfra = null;
                                    ulong available = mfra_size;
                                    DefaultAtomFactory atom_factory = new DefaultAtomFactory();
                                    atom_factory.CreateAtomFromStream(_fragmentStream, ref available, out mfra);
                                    _mfra = mfra as ContainerAtom;
                                }
                            }
                        }
                        if (result.Succeeded()) {
                            result = _fragmentStream.Seek(here);
                        }
                    }
                }
            }

            // return now if we have not found an index
            if (_mfra == null) {
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            // look for the earliest fragment referenced by an entry with the largest timestamp that's
            // before or equal to the requested time
            int best_entry = -1;
            for (int t=0; t<_trackers.Count; t++) {
                // find the tfra index for this track
                TfraAtom tfra = null;
                List<Atom> children = _mfra.GetChildren();
                for (int item = 0; item < children.Count; item++) {
                    if (children[item].GetType() == AtomType.TFRA) {
                        // PORT NOTE: upstream uses an unchecked C cast here; a 'tfra'
                        // child that is not a TfraAtom is skipped instead.
                        TfraAtom tfra_ = children[item] as TfraAtom;
                        if (tfra_ != null && tfra_.GetTrackId() == _trackers[t]._track.GetId()) {
                            tfra = tfra_;
                            break;
                        }
                    }
                }
                if (tfra == null) {
                    return Ap4Result.ERROR_NOT_SUPPORTED;
                }
                List<TfraAtom.Entry> entries = tfra.GetEntries();

                ulong media_time = Ap4Utils.ConvertTime(time_ms, 1000, _trackers[t]._track.GetMediaTimeScale());
                int entry = -1;
                for (int i=0; i<(int)entries.Count; i++) {
                    if (entries[i]._time > media_time) break;
                    entry = i;
                }
                if (entry >= 0) {
                    if (best_entry == -1) {
                        best_entry = entry;
                    } else if (entries[entry]._moofOffset < entries[best_entry]._moofOffset) {
                        best_entry = entry;
                    }

                    // update our position
                    // report the actual time we found (in milliseconds)
                    actual_time_ms = (uint)Ap4Utils.ConvertTime(entries[best_entry]._time, _trackers[t]._track.GetMediaTimeScale(), 1000);
                    _nextFragmentPosition = entries[best_entry]._moofOffset;
                }
            }

            // check that we found something
            if (best_entry == -1) {
                return Ap4Result.FAILURE;
            }

            // flush any queued samples
            FlushQueues();

            // reset tracker states
            for (int i=0; i<_trackers.Count; i++) {
                _trackers[i]._sampleTable     = null;
                _trackers[i]._nextSample      = null;
                _trackers[i]._nextSampleIndex = 0;
                _trackers[i]._eos             = false;
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint  GetBufferFullness() { return _bufferFullness; }
        public ulong GetCurrentFragmentPosition() { return _currentFragmentPosition; }

        // classes
        public abstract class SampleReader {
            public abstract Ap4Result ReadSampleData(Sample sample, DataBuffer sample_data);
        }

        protected class SampleBuffer {
            public SampleBuffer(Sample sample) { _sample = sample; }
            public Sample     _sample;
            public DataBuffer _data = new DataBuffer();
        }

        protected class Tracker {
            public Tracker(Track track) {
                _eos = false;
                _track = track;
                _sampleTable = null;
                _sampleTableIsOwned = false;
                _nextSample = null;
                _nextSampleIndex = 0;
                _nextDts = 0;
                _reader = null;
                _seekPoint._pending      = false;
                _seekPoint._time         = 0;
                _seekPoint._moofOffset   = 0;
                _seekPoint._trafNumber   = 0;
                _seekPoint._trunNumber   = 0;
                _seekPoint._sampleNumber = 0;
            }
            public Tracker(Tracker other) {
                _eos = other._eos;
                _track = other._track;
                _sampleTable = other._sampleTable;
                _sampleTableIsOwned = false;
                _nextSample = null;
                _nextSampleIndex = other._nextSampleIndex;
                _nextDts = other._nextDts;
                _reader = other._reader;
                _seekPoint = other._seekPoint;
            } // don't copy samples

            public struct SeekPoint {
                public bool  _pending;
                public ulong _time;
                public ulong _moofOffset;
                public uint  _trafNumber;
                public uint  _trunNumber;
                public uint  _sampleNumber;
            }

            public bool               _eos;
            public Track              _track;
            public SampleTable        _sampleTable;
            public bool               _sampleTableIsOwned;
            public Sample             _nextSample;
            public uint               _nextSampleIndex;
            public ulong              _nextDts;
            public List<SampleBuffer> _samples = new List<SampleBuffer>();
            public SampleReader       _reader;
            public SeekPoint          _seekPoint;
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::ProcessTrack
        +---------------------------------------------------------------------*/
        // methods that can be overridden
        protected virtual Ap4Result ProcessTrack(Track track)
        {
            // create a new entry for the track
            Tracker tracker = new Tracker(track);
            tracker._sampleTable = track.GetSampleTable();
            _trackers.Add(tracker);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::ProcessMoof
        +---------------------------------------------------------------------*/
        protected virtual Ap4Result ProcessMoof(ContainerAtom moof,
                                                ulong         moof_offset,
                                                ulong         mdat_payload_offset)
        {
            Ap4Result result;

            // create a new fragment
            _fragment = new MovieFragment(moof);

            // update the trackers
            List<uint> ids = new List<uint>();
            _fragment.GetTrackIds(ids);
            for (int i=0; i<_trackers.Count; i++) {
                Tracker tracker = _trackers[i];
                tracker._sampleTable = null;
                tracker._nextSampleIndex = 0;
                for (int j=0; j<ids.Count; j++) {
                    if (ids[j] == tracker._track.GetId()) {
                        FragmentSampleTable sample_table = null;
                        result = _fragment.CreateSampleTable(_movie,
                                                             ids[j],
                                                             _fragmentStream,
                                                             moof_offset,
                                                             mdat_payload_offset,
                                                             tracker._nextDts,
                                                             out sample_table);
                        if (result.Failed()) return result;
                        tracker._sampleTable = sample_table;
                        tracker._sampleTableIsOwned = true;
                        tracker._eos = false;
                        break;
                    }
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::AdvanceFragment
        +---------------------------------------------------------------------*/
        protected Ap4Result AdvanceFragment()
        {
            Ap4Result result;

            // PORT NOTE: upstream dereferences m_FragmentStream for the Seek below
            // before it checks it for NULL (a crash in C++). The check is done
            // first here so that a missing stream yields ERROR_INVALID_STATE.
            if (_fragmentStream == null) return Ap4Result.ERROR_INVALID_STATE;

            // go the the start of the next fragment
            result = _fragmentStream.Seek(_nextFragmentPosition);
            if (result.Failed()) return result;
            _currentFragmentPosition = _nextFragmentPosition;

            // read atoms until we find a moof
            Ap4.ASSERT(_hasFragments);
            DefaultAtomFactory atom_factory = new DefaultAtomFactory();
            do {
                Atom  atom = null;
                ulong last_position = 0;
                _fragmentStream.Tell(out last_position);
                result = atom_factory.CreateAtomFromStream(_fragmentStream, out atom);
                if (result.Succeeded()) {
                    if (atom.GetType() == AtomType.MOOF) {
                        ContainerAtom moof = atom as ContainerAtom;
                        if (moof != null) {
                            // remember where the moof started
                            _currentFragmentPosition = last_position;

                            // remember where we are in the stream
                            ulong position = 0;
                            _fragmentStream.Tell(out position);

                            // process the movie fragment
                            result = ProcessMoof(moof, position-atom.GetSize(), position+8);
                            if (result.Failed()) return result;

                            // compute where the next fragment will be
                            uint size;
                            uint type;
                            _fragmentStream.Tell(out position);
                            result = _fragmentStream.ReadUI32(out size);
                            if (result.Failed()) return Ap4Result.SUCCESS; // can't read more
                            result = _fragmentStream.ReadUI32(out type);
                            if (result.Failed()) return Ap4Result.SUCCESS; // can't read more
                            if (size == 0) {
                                _nextFragmentPosition = 0;
                            } else if (size == 1) {
                                ulong size_64 = 0;
                                result = _fragmentStream.ReadUI64(out size_64);
                                if (result.Failed()) return Ap4Result.SUCCESS; // can't read more
                                _nextFragmentPosition = position+size_64;
                            } else {
                                _nextFragmentPosition = position+size;
                            }
                            return Ap4Result.SUCCESS;
                        }
                    }
                }
            } while (result.Succeeded());

            return Ap4Result.ERROR_EOS;
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::Advance
        +---------------------------------------------------------------------*/
        protected Ap4Result Advance(bool read_data = true)
        {
            ulong min_offset = ulong.MaxValue; // (AP4_UI64)(-1)
            Tracker next_tracker = null;
            for (;;) {
                for (int i=0; i<_trackers.Count; i++) {
                    Tracker tracker = _trackers[i];
                    if (tracker._eos) continue;
                    if (tracker._sampleTable == null) continue;

                    // get the next sample unless we have it already
                    if (tracker._nextSample == null) {
                        if (tracker._nextSampleIndex >= tracker._sampleTable.GetSampleCount()) {
                            if (!_hasFragments) tracker._eos = true;
                            if (tracker._sampleTableIsOwned) {
                                tracker._sampleTable = null;
                            }
                            continue;
                        }
                        tracker._nextSample = new Sample();
                        Ap4Result result = tracker._sampleTable.GetSample(tracker._nextSampleIndex, tracker._nextSample);
                        if (result.Failed()) {
                            tracker._eos = true;
                            tracker._nextSample = null;
                            continue;
                        }
                        tracker._nextDts += tracker._nextSample.GetDuration();
                    }
                    Ap4.ASSERT(tracker._nextSample != null);

                    ulong offset = tracker._nextSample.GetOffset();
                    if (offset < min_offset) {
                        min_offset = offset;
                        next_tracker = tracker;
                    }
                }

                if (next_tracker != null) break;
                if (_hasFragments) {
                    Ap4Result result = AdvanceFragment();
                    if (result.Failed()) return result;
                } else {
                    break;
                }
            }

            if (next_tracker != null) {
                // read the sample into a buffer
                Ap4.ASSERT(next_tracker._nextSample != null);
                SampleBuffer buffer = new SampleBuffer(next_tracker._nextSample);
                Ap4Result result;
                if (read_data) {
                    if (next_tracker._reader != null) {
                        result = next_tracker._reader.ReadSampleData(buffer._sample, buffer._data);
                    } else {
                        result = buffer._sample.ReadData(buffer._data);
                    }
                    if (result.Failed()) {
                        next_tracker._nextSample = null;
                        return result;
                    }

                    // detach the sample from its source now that we've read its data
                    buffer._sample.Detach();
                }

                // add the buffer to the queue
                next_tracker._samples.Add(buffer);
                _bufferFullness += buffer._data.GetDataSize();
                if (_bufferFullness > _bufferFullnessPeak) {
                    _bufferFullnessPeak = _bufferFullness;
                }
                next_tracker._nextSample = null;
                next_tracker._nextSampleIndex++;
                return Ap4Result.SUCCESS;
            }

            return Ap4Result.ERROR_EOS;
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::PopSample
        +---------------------------------------------------------------------*/
        protected bool PopSample(Tracker     tracker,
                                 Sample      sample,
                                 DataBuffer  sample_data)
        {
            SampleBuffer head = null;
            // tracker->m_Samples.PopHead(head)
            if (tracker._samples.Count != 0) {
                head = tracker._samples[0];
                tracker._samples.RemoveAt(0);
            }
            if (head != null) {
                Ap4.ASSERT(head._sample != null);
                sample.Assign(head._sample);
                if (sample_data != null) {
                    sample_data.SetData(head._data.GetData(), head._data.GetDataSize());
                }
                Ap4.ASSERT(_bufferFullness >= head._data.GetDataSize());
                _bufferFullness -= head._data.GetDataSize();
                return true;
            }

            return false;
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::ReadNextSample
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Read the next sample in storage order from a specific track.
        /// </summary>
        public Ap4Result ReadNextSample(uint       track_id,
                                        Sample     sample,
                                        DataBuffer sample_data)
        {
            if (_trackers.Count == 0) {
                return Ap4Result.ERROR_NO_SUCH_ITEM;
            }

            // look for a sample from a specific track
            Tracker tracker = FindTracker(track_id);
            if (tracker == null) return Ap4Result.ERROR_INVALID_PARAMETERS;
            for(;;) {
                // pop a sample if we can
                if (PopSample(tracker, sample, sample_data)) return Ap4Result.SUCCESS;

                // don't continue if we've reached the end of that tracker
                if (tracker._eos) return Ap4Result.ERROR_EOS;

                Ap4Result result = Advance();
                if (result.Failed()) return result;
            }

            // unreachable - return AP4_ERROR_EOS;
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::ReadNextSample
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream overloads ReadNextSample on `AP4_DataBuffer*` vs
        // `AP4_DataBuffer&`, which C# cannot distinguish; the protected pointer
        // variant (sample_data may be null) is named ReadNextSampleInternal.
        protected Ap4Result ReadNextSampleInternal(Sample      sample,
                                                   DataBuffer  sample_data,
                                                   out uint    track_id)
        {
            if (_trackers.Count == 0) {
                track_id = 0;
                return Ap4Result.ERROR_NO_SUCH_ITEM;
            }

            // PORT NOTE: upstream leaves track_id untouched on the failure paths below; an `out` must be assigned.
            track_id = 0;

            // return the oldest buffered sample, if any
            ulong min_offset = ulong.MaxValue; // (AP4_UI64)(-1)
            Tracker next_tracker = null;
            for (;;) {
                for (int i=0; i<_trackers.Count; i++) {
                    Tracker tracker = _trackers[i];
                    if (tracker._eos) continue;

                    if (tracker._samples.Count != 0) {
                        ulong offset = tracker._samples[0]._sample.GetOffset();
                        if (offset < min_offset) {
                            min_offset = offset;
                            next_tracker = tracker;
                        }
                    }
                }

                // return the sample if we have found a tracker
                if (next_tracker != null) {
                    PopSample(next_tracker, sample, sample_data);
                    track_id = next_tracker._track.GetId();
                    return Ap4Result.SUCCESS;
                }

                // nothing found, read one more sample
                Ap4Result result = Advance(sample_data != null);
                if (result.Failed()) return result;
            }

            // unreachable - return AP4_ERROR_EOS;
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::ReadNextSample
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Read the next sample in storage order, from any track.
        /// track_id is updated to reflect the track from which the sample was read.
        /// </summary>
        public Ap4Result ReadNextSample(Sample     sample,
                                        DataBuffer sample_data,
                                        out uint   track_id)
        {
            return ReadNextSampleInternal(sample, sample_data, out track_id);
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::GetNextSample
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Get the next sample in storage order, without reading the sample data,
        /// from any track. track_id is updated to reflect the track from which the
        /// sample was read.
        /// </summary>
        public Ap4Result GetNextSample(Sample sample, out uint track_id)
        {
            return ReadNextSampleInternal(sample, null, out track_id);
        }

        /*----------------------------------------------------------------------
        |   AP4_LinearReader::FindTracker
        +---------------------------------------------------------------------*/
        protected Tracker FindTracker(uint track_id)
        {
            for (int i=0; i<_trackers.Count; i++) {
                if (_trackers[i]._track.GetId() == track_id) return _trackers[i];
            }

            // not found
            return null;
        }

        // members
        protected Movie          _movie;
        protected bool           _hasFragments;
        protected MovieFragment  _fragment;
        protected ByteStream     _fragmentStream;
        protected ulong          _currentFragmentPosition;
        protected ulong          _nextFragmentPosition;
        protected List<Tracker>  _trackers = new List<Tracker>();
        protected uint           _bufferFullness;
        protected uint           _bufferFullnessPeak;
        protected ContainerAtom  _mfra;
    }

    /*----------------------------------------------------------------------
    |   AP4_DecryptingSampleReader
    +---------------------------------------------------------------------*/
    public class DecryptingSampleReader : LinearReader.SampleReader
    {
        public DecryptingSampleReader(SampleDecrypter decrypter, bool transfer_ownership) {
            _decrypterIsOwned = transfer_ownership;
            _decrypter = decrypter;
        }

        /*----------------------------------------------------------------------
        |   AP4_DecryptingSampleReader::ReadSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result ReadSampleData(Sample     sample,
                                                 DataBuffer sample_data)
        {
            Ap4Result result = sample.ReadData(_dataBuffer);
            if (result.Failed()) return result;

            return _decrypter.DecryptSampleData(_dataBuffer, sample_data);
        }

        public bool             _decrypterIsOwned;
        public DataBuffer       _dataBuffer = new DataBuffer();
        public SampleDecrypter  _decrypter;
    }
}
