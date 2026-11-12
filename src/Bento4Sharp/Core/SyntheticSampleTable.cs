// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SyntheticSampleTable.h, Ap4SyntheticSampleTable.cpp
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
        public const uint SYNTHETIC_SAMPLE_TABLE_DEFAULT_CHUNK_SIZE = 10;
    }

    /*----------------------------------------------------------------------
    |   AP4_SyntheticSampleTable
    +---------------------------------------------------------------------*/
    public class SyntheticSampleTable : SampleTable
    {
        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::AP4_SyntheticSampleTable
        +---------------------------------------------------------------------*/
        public SyntheticSampleTable(uint chunk_size
                                    = Ap4.SYNTHETIC_SAMPLE_TABLE_DEFAULT_CHUNK_SIZE)
        {
            _chunkSize = chunk_size != 0 ? chunk_size : Ap4.SYNTHETIC_SAMPLE_TABLE_DEFAULT_CHUNK_SIZE;
            _lookupCache._sample = 0;
            _lookupCache._chunk  = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::GetSample
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSample(uint sample_index, Sample sample)
        {
            if (sample_index >= (uint)_samples.Count) return Ap4Result.ERROR_OUT_OF_RANGE;

            sample.Assign(_samples[(int)sample_index]);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::GetSampleCount
        +---------------------------------------------------------------------*/
        public override uint GetSampleCount()
        {
            return (uint)_samples.Count;
        }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::GetSampleChunkPosition
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSampleChunkPosition(
            uint     sample_index,
            out uint chunk_index,
            out uint position_in_chunk)
        {
            // default values
            chunk_index       = 0;
            position_in_chunk = 0;

            // check parameters
            if (sample_index >= (uint)_samples.Count) return Ap4Result.ERROR_OUT_OF_RANGE;

            // look for the chunk
            uint sample_cursor = 0;
            uint chunk_cursor  = 0;
            if (sample_index >= _lookupCache._sample) {
                sample_cursor = _lookupCache._sample;
                chunk_cursor  = _lookupCache._chunk;
            }
            for (;
                 chunk_cursor < (uint)_samplesInChunk.Count;
                 sample_cursor += _samplesInChunk[(int)chunk_cursor++]) {
                if (sample_cursor+_samplesInChunk[(int)chunk_cursor] > sample_index) {
                    chunk_index = chunk_cursor;
                    position_in_chunk = sample_index-sample_cursor;
                    _lookupCache._sample = sample_cursor;
                    _lookupCache._chunk  = chunk_cursor;
                    return Ap4Result.SUCCESS;
                }
            }

            return Ap4Result.ERROR_OUT_OF_RANGE;
        }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::GetSampleDescriptionCount
        +---------------------------------------------------------------------*/
        public override uint GetSampleDescriptionCount()
        {
            return (uint)_sampleDescriptions.Count;
        }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::GetSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription GetSampleDescription(uint index)
        {
            // m_SampleDescriptions.Get(index, holder)
            if (index < (uint)_sampleDescriptions.Count) {
                SampleDescriptionHolder holder = _sampleDescriptions[(int)index];
                return holder._sampleDescription;
            } else {
                return null;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::AddSampleDescription
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Add a sample description to the sample table.
        /// Each added sample description will have the next available index, starting at 0
        /// </summary>
        /// <param name="description">Pointer to the sample description to add</param>
        /// <param name="transfer_ownership">Boolean flag indicating whether the ownership of the
        /// sample description object is transfered to the sample table object (true by default).
        /// If true, the sample table object will own the sample description object, and will
        /// delete it when it is itself deleted. If false, the ownership remains with the caller,
        /// and only a referencing pointer is kept, thus the caller must ensure that the object
        /// is not deleted before the sample table is deleted.</param>
        public virtual Ap4Result AddSampleDescription(SampleDescription  description,
                                                      bool               transfer_ownership = true)
        {
            _sampleDescriptions.Add(new SampleDescriptionHolder(description, transfer_ownership));
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::AddSample
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Add a sample to the sample table, where the sample duration is given
        /// </summary>
        /// <param name="data_stream">The byte stream that contains the sample data. The sample
        /// object added to the track will keep a reference to that byte stream.</param>
        /// <param name="offset">Position of the first byte of sample data within the stream</param>
        /// <param name="size">Size in bytes of the sample data</param>
        /// <param name="duration">Duration of the sample (in the timescale of the media). This
        /// value can be 0 if the duration is not known. In that case, the next sample
        /// added to the table MUST have a non-zero value for the DTS (decoding timestamp),
        /// which will allow the actual duration of this sample to be computed.</param>
        /// <param name="description_index">Index of the sample description that applies to
        /// this sample (typically 0).</param>
        /// <param name="dts">DTS (decoding timestamp) of the sample. If this value is 0, and there
        /// already are samples in the table, the DTS of the sample will be automatically
        /// computed based on the DTS and duration of the preceding sample. If this value is
        /// not equal to the DTS+duration of the preceding sample, the duration of the
        /// preceding sample is automatically adjusted, unless it has a non-zero value, in which
        /// case <see cref="Ap4Result.ERROR_INVALID_PARAMETERS"/> is returned.
        /// The DTS of the first sample in the table MUST always be 0.</param>
        /// <param name="cts_delta">Difference between the CTS (composition/display timestamp) and DTS
        /// (decoding timestamp) of the sample (in the timescale of the media)</param>
        /// <param name="sync">Boolean flag indicating whether this is a sync sample or not.</param>
        public virtual Ap4Result AddSample(ByteStream data_stream,
                                           ulong      offset,
                                           uint       size,
                                           uint       duration,
                                           uint       description_index,
                                           ulong      dts,
                                           uint       cts_delta,
                                           bool       sync)
        {
            // decide if we need to start a new chunk or increment the last one
            if (_samplesInChunk.Count == 0 ||
                _samplesInChunk[_samplesInChunk.Count-1] >= _chunkSize ||
                _samples.Count == 0 ||
                _samples[_samples.Count-1].GetDescriptionIndex() != description_index) {
                _samplesInChunk.Add(1);
            } else {
                ++_samplesInChunk[_samplesInChunk.Count-1];
            }

            // compute the timestamps
            if (_samples.Count > 0) {
                Sample prev_sample = _samples[_samples.Count-1];
                if (dts == 0) {
                    if (prev_sample.GetDuration() == 0) {
                        // can't compute the DTS for this sample
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                    dts = prev_sample.GetDts()+prev_sample.GetDuration();
                } else {
                    if (prev_sample.GetDuration() == 0) {
                        // update the previous sample
                        if (dts <= prev_sample.GetDts()) {
                            return Ap4Result.ERROR_INVALID_PARAMETERS;
                        }
                        prev_sample.SetDuration((uint)(dts-prev_sample.GetDts()));
                    } else {
                        if (dts != prev_sample.GetDts()+prev_sample.GetDuration()) {
                            // mismatch
                            return Ap4Result.ERROR_INVALID_PARAMETERS;
                        }
                    }
                }
            }

            // add the sample to the table
            Sample sample = new Sample(data_stream, offset, size, duration, description_index, dts, cts_delta, sync);
            _samples.Add(sample);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::AddSample
        +---------------------------------------------------------------------*/
        public virtual Ap4Result AddSample(Sample sample)
        {
            // the table keeps its own copy, as upstream (AP4_Array<AP4_Sample>::Append)
            _samples.Add(new Sample(sample));
            return Ap4Result.SUCCESS;
        }

        /// <summary>
        /// Get a reference to a sample for the purpose of modifying it
        /// </summary>
        public Sample UseSample(uint index) { return _samples[(int)index]; }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::GetSampleIndexForTimeStamp
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSampleIndexForTimeStamp(ulong    ts,
                                                             out uint index)
        {
            // PORT NOTE: upstream leaves `index` untouched; an `out` must be assigned.
            index = 0;
            return Ap4Result.ERROR_NOT_SUPPORTED;
        }

        /*----------------------------------------------------------------------
        |   AP4_SyntheticSampleTable::GetNearestSyncSampleIndex
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream declares this override without the `before=true`
        // default; the default of the base class declaration is kept here.
        // An out-of-range sample_index is undefined behaviour upstream (unchecked
        // array access); here it throws ArgumentOutOfRangeException.
        public override uint GetNearestSyncSampleIndex(uint sample_index, bool before = true)
        {
            if (before) {
                for (int i=(int)sample_index; i>=0; i--) {
                    if (_samples[i].IsSync()) return (uint)i;
                }
                // not found?
                return 0;
            } else {
                uint entry_count = (uint)_samples.Count;
                for (uint i=sample_index; i<entry_count; i++) {
                    if (_samples[(int)i].IsSync()) return i;
                }
                // not found?
                return (uint)_samples.Count;
            }
        }

        // classes
        private class SampleDescriptionHolder {
            public SampleDescriptionHolder(SampleDescription description, bool is_owned) {
                _sampleDescription = description;
                _isOwned = is_owned;
            }
            public SampleDescription  _sampleDescription;
            public bool               _isOwned;
        }

        private struct LookupCache {
            public uint _sample;
            public uint _chunk;
        }

        // members
        private List<Sample>                  _samples = new List<Sample>();
        private List<SampleDescriptionHolder> _sampleDescriptions = new List<SampleDescriptionHolder>();
        private uint                          _chunkSize;
        private List<uint>                    _samplesInChunk = new List<uint>();
        private LookupCache                   _lookupCache;
    }
}
