// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SampleSource.h, Ap4SampleSource.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SampleSource
    +---------------------------------------------------------------------*/
    public abstract class SampleSource
    {
        /// <summary>
        /// Return the timscale of the sample's media
        /// </summary>
        public abstract uint GetTimeScale();

        /// <summary>
        /// Return the duration in milliseconds
        /// </summary>
        public abstract uint GetDurationMs();

        /// <summary>
        /// Read the next sample from the source
        /// </summary>
        public abstract Ap4Result ReadNextSample(Sample sample, DataBuffer buffer, out uint track_id);

        /// <summary>
        /// Seek to the sample closest to a specific timestamp in milliseconds
        /// </summary>
        public abstract Ap4Result SeekToTime(uint time_ms, bool before=true);

        /// <summary>
        /// Return a sample description by index.
        /// Returns NULL if there is no sample description with the requested index.
        /// </summary>
        public abstract SampleDescription GetSampleDescription(uint indx);
    }

    /*----------------------------------------------------------------------
    |   AP4_TrackSampleSource
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream re-declares SeekToTime as pure virtual (`= 0`) in
    // AP4_TrackSampleSource while still providing a body for it, which makes the
    // C++ class abstract. C# cannot give an abstract method a body, so the
    // method is a normal override here and the class is instantiable.
    public class TrackSampleSource : SampleSource
    {
        /*----------------------------------------------------------------------
        |   AP4_TrackSampleSource
        +---------------------------------------------------------------------*/
        public TrackSampleSource(Track track)
        {
            _track = track;
            _sampleIndex = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrackSampleSource::GetTimeScale
        +---------------------------------------------------------------------*/
        public override uint GetTimeScale()
        {
            return _track.GetMediaTimeScale();
        }

        /*----------------------------------------------------------------------
        |   AP4_TrackSampleSource::GetDurationMs
        +---------------------------------------------------------------------*/
        public override uint GetDurationMs()
        {
            return _track.GetDurationMs();
        }

        /*----------------------------------------------------------------------
        |   AP4_TrackSampleSource::ReadNextSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ReadNextSample(Sample sample, DataBuffer buffer, out uint track_id)
        {
            Ap4Result result = _track.ReadSample(_sampleIndex, sample, buffer);
            if (result.Succeeded()) {
                ++_sampleIndex;
                track_id = _track.GetId();
            } else {
                track_id = 0;
            }
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrackSampleSource::SeekToTime
        +---------------------------------------------------------------------*/
        public override Ap4Result SeekToTime(uint time_ms, bool before = true)
        {
            uint sample_index = 0;
            Ap4Result result = _track.GetSampleIndexForTimeStampMs(time_ms, out sample_index);
            if (result.Failed()) return result;
            if (sample_index >= _track.GetSampleCount()) return Ap4Result.ERROR_OUT_OF_RANGE;
            sample_index = _track.GetNearestSyncSampleIndex(sample_index, before);
            if (sample_index >= _track.GetSampleCount()) return Ap4Result.ERROR_OUT_OF_RANGE;
            _sampleIndex = sample_index;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrackSampleSource::GetSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription GetSampleDescription(uint indx)
        {
            return _track.GetSampleDescription(indx);
        }

        private Track _track;
        private uint  _sampleIndex;
    }
}
