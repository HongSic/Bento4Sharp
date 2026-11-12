// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomSampleTable.h, Ap4AtomSampleTable.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_AtomSampleTable
    +---------------------------------------------------------------------*/
    public class AtomSampleTable : SampleTable
    {
        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::AP4_AtomSampleTable
        +---------------------------------------------------------------------*/
        public AtomSampleTable(ContainerAtom stbl,
                               ByteStream    sample_stream)
        {
            _sampleStream = sample_stream;
            _stscAtom = stbl.GetChild(AtomType.STSC) as StscAtom;
            _stcoAtom = stbl.GetChild(AtomType.STCO) as StcoAtom;
            _stszAtom = stbl.GetChild(AtomType.STSZ) as StszAtom;
            _stz2Atom = stbl.GetChild(AtomType.STZ2) as Stz2Atom;
            _cttsAtom = stbl.GetChild(AtomType.CTTS) as CttsAtom;
            _sttsAtom = stbl.GetChild(AtomType.STTS) as SttsAtom;
            _stssAtom = stbl.GetChild(AtomType.STSS) as StssAtom;
            _stsdAtom = stbl.GetChild(AtomType.STSD) as StsdAtom;
            _co64Atom = stbl.GetChild(AtomType.CO64) as Co64Atom;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::GetSample
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSample(uint   index,
                                            Sample sample)
        {
            Ap4Result result;

            // check that we have an stsc atom
            if (_stscAtom == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // check that we have a chunk offset table
            if (_stcoAtom == null && _co64Atom == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // MP4 uses 1-based indexes internally, so adjust by one
            index++;

            // find out in which chunk this sample is located
            uint chunk, skip, desc;
            result = _stscAtom.GetChunkForSample(index, out chunk, out skip, out desc);
            if (result.Failed()) return result;

            // check that the result is within bounds
            if (skip > index) return Ap4Result.ERROR_INTERNAL;

            // get the atom offset for this chunk
            ulong offset;
            if (_stcoAtom != null) {
                uint offset_32;
                result = _stcoAtom.GetChunkOffset(chunk, out offset_32);
                offset = offset_32;
            } else {
                result = _co64Atom.GetChunkOffset(chunk, out offset);
            }
            if (result.Failed()) return result;

            // compute the additional offset inside the chunk
            for (uint i = index-skip; i < index; i++) {
                uint size = 0;
                if (_stszAtom != null) {
                    result = _stszAtom.GetSampleSize(i, out size);
                } else if (_stz2Atom != null) {
                    result = _stz2Atom.GetSampleSize(i, out size);
                } else {
                    result = Ap4Result.ERROR_INVALID_FORMAT;
                }
                if (result.Failed()) return result;
                offset += size;
            }

            // set the description index
            sample.SetDescriptionIndex(desc-1); // adjust for 0-based indexes

            // set the dts and cts
            uint  cts_offset = 0;
            ulong dts        = 0;
            uint  duration   = 0;
            if (_sttsAtom != null) {
                result = _sttsAtom.GetDts(index, out dts, out duration);
                if (result.Failed()) return result;
            }
            sample.SetDuration(duration);
            sample.SetDts(dts);
            if (_cttsAtom == null) {
                sample.SetCts(dts);
            } else {
                result = _cttsAtom.GetCtsOffset(index, out cts_offset);
                if (result.Failed()) return result;
                sample.SetCtsDelta(cts_offset);
            }

            // set the size
            uint sample_size = 0;
            if (_stszAtom != null) {
                result = _stszAtom.GetSampleSize(index, out sample_size);
            } else if (_stz2Atom != null) {
                result = _stz2Atom.GetSampleSize(index, out sample_size);
            } else {
                result = Ap4Result.ERROR_INVALID_FORMAT;
            }
            if (result.Failed()) return result;
            sample.SetSize(sample_size);

            // set the sync flag
            if (_stssAtom == null) {
                sample.SetSync(true);
            } else {
                sample.SetSync(_stssAtom.IsSampleSync(index));
            }

            // set the offset
            sample.SetOffset(offset);

            // set the data stream
            sample.SetDataStream(_sampleStream);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::GetSampleCount
        +---------------------------------------------------------------------*/
        public override uint GetSampleCount()
        {
            if (_stszAtom != null) {
                return _stszAtom.GetSampleCount();
            } else if (_stz2Atom != null) {
                return _stz2Atom.GetSampleCount();
            } else {
                return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::GetSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription GetSampleDescription(uint index)
        {
            return _stsdAtom != null ? _stsdAtom.GetSampleDescription(index) : null;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::GetSampleDescriptionCount
        +---------------------------------------------------------------------*/
        public override uint GetSampleDescriptionCount()
        {
            return _stsdAtom != null ? _stsdAtom.GetSampleDescriptionCount() : 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::GetSampleChunkPosition
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSampleChunkPosition(uint     sample_index,
                                                         out uint chunk_index,
                                                         out uint position_in_chunk)
        {
            // default values
            chunk_index       = 0;
            position_in_chunk = 0;

            uint sample_description_index;
            return GetChunkForSample(sample_index,
                                     out chunk_index,
                                     out position_in_chunk,
                                     out sample_description_index);
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::GetChunkForSample
        +---------------------------------------------------------------------*/
        public virtual Ap4Result GetChunkForSample(uint     sample_index,
                                                   out uint chunk_index,
                                                   out uint position_in_chunk,
                                                   out uint sample_description_index)
        {
            // default values
            chunk_index              = 0;
            position_in_chunk        = 0;
            sample_description_index = 0;

            // check that we an stsc atom
            if (_stscAtom == null) return Ap4Result.ERROR_INVALID_STATE;

            // get the chunk info from the stsc atom
            uint chunk = 0;
            Ap4Result result = _stscAtom.GetChunkForSample(sample_index+1, // the atom API is 1-based
                                                           out chunk,
                                                           out position_in_chunk,
                                                           out sample_description_index);
            if (result.Failed()) return result;
            if (chunk == 0) return Ap4Result.ERROR_INTERNAL;

            // the atom sample and chunk indexes are 1-based, so we need to translate
            chunk_index = chunk-1;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::GetChunkOffset
        +---------------------------------------------------------------------*/
        public virtual Ap4Result GetChunkOffset(uint      chunk_index,
                                                out ulong offset)
        {
            if (_stcoAtom != null) {
                uint offset_32;
                Ap4Result result = _stcoAtom.GetChunkOffset(chunk_index+1, out offset_32);
                if (result.Succeeded()) {
                    offset = offset_32;
                } else {
                    offset = 0;
                }
                return result;
            } else if (_co64Atom != null) {
                return _co64Atom.GetChunkOffset(chunk_index+1, out offset);
            } else {
                offset = 0;
                return Ap4Result.FAILURE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::SetChunkOffset
        +---------------------------------------------------------------------*/
        public virtual Ap4Result SetChunkOffset(uint  chunk_index,
                                                ulong offset)
        {
            if (_stcoAtom != null) {
                if ((offset >> 32) != 0) return Ap4Result.ERROR_OUT_OF_RANGE;
                return _stcoAtom.SetChunkOffset(chunk_index+1, (uint)offset);
            } else if (_co64Atom != null) {
                return _co64Atom.SetChunkOffset(chunk_index+1, offset);
            } else {
                return Ap4Result.FAILURE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::SetSampleSize
        +---------------------------------------------------------------------*/
        public virtual Ap4Result SetSampleSize(uint sample_index, uint size)
        {
            if (_stszAtom != null) {
                return _stszAtom.SetSampleSize(sample_index+1, size);
            } else if (_stz2Atom != null) {
                return _stz2Atom.SetSampleSize(sample_index+1, size);
            } else {
                return Ap4Result.FAILURE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::GetSampleIndexForTimeStamp
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSampleIndexForTimeStamp(ulong    ts,
                                                             out uint sample_index)
        {
            // PORT NOTE: upstream leaves `sample_index` untouched when there is no stts; an `out` must be assigned.
            sample_index = 0;
            return _sttsAtom != null ? _sttsAtom.GetSampleIndexForTimeStamp(ts, out sample_index)
                                     : Ap4Result.FAILURE;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomSampleTable::GetNearestSyncSampleIndex
        +---------------------------------------------------------------------*/
        public override uint GetNearestSyncSampleIndex(uint sample_index, bool before = true)
        {
            // if we don't have an stss table, all samples match
            if (_stssAtom == null) return sample_index;

            sample_index += 1; // the table is 1-based
            List<uint> entries = _stssAtom.GetEntries();
            uint entry_count = (uint)entries.Count;
            if (before) {
                uint cursor = 0;
                for (int i=0; i<entry_count; i++) {
                    if (entries[i] >= sample_index) return cursor;
                    if (entries[i] != 0) cursor = entries[i]-1;
                }

                // not found?
                return cursor;
            } else {
                for (int i=0; i<entry_count; i++) {
                    if (entries[i] >= sample_index) {
                        return entries[i] != 0 ? entries[i]-1 : sample_index-1;
                    }
                }

                // not found?
                return GetSampleCount();
            }
        }

        // members
        private ByteStream _sampleStream;
        private StscAtom   _stscAtom;
        private StcoAtom   _stcoAtom;
        private StszAtom   _stszAtom;
        private Stz2Atom   _stz2Atom;
        private SttsAtom   _sttsAtom;
        private CttsAtom   _cttsAtom;
        private StsdAtom   _stsdAtom;
        private StssAtom   _stssAtom;
        private Co64Atom   _co64Atom;
    }
}
