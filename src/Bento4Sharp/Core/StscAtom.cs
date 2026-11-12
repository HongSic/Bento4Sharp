// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4StscAtom.h, Ap4StscAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_StscTableEntry
    +---------------------------------------------------------------------*/
    // PORT NOTE: a class (not a struct) so that entries held in a List<T> can be
    // modified in place, as upstream does through AP4_Array<T>::operator[].
    public class StscTableEntry
    {
        public StscTableEntry()
        {
            _firstChunk = 0;
            _firstSample = 0;
            _chunkCount = 0;
            _samplesPerChunk = 0;
            _sampleDescriptionIndex = 0;
        }
        public StscTableEntry(uint first_chunk,
                              uint first_sample,
                              uint samples_per_chunk,
                              uint sample_description_index)
        {
            _firstChunk = first_chunk;
            _firstSample = first_sample;
            _chunkCount = 0;
            _samplesPerChunk = samples_per_chunk;
            _sampleDescriptionIndex = sample_description_index;
        }
        public StscTableEntry(uint first_chunk,
                              uint first_sample,
                              uint chunk_count,
                              uint samples_per_chunk,
                              uint sample_description_index)
        {
            _firstChunk = first_chunk;
            _firstSample = first_sample;
            _chunkCount = chunk_count;
            _samplesPerChunk = samples_per_chunk;
            _sampleDescriptionIndex = sample_description_index;
        }
        public uint _firstChunk;
        public uint _firstSample;  // computed (not in file)
        public uint _chunkCount;   // computed (not in file)
        public uint _samplesPerChunk;
        public uint _sampleDescriptionIndex;
    }

    /*----------------------------------------------------------------------
    |   AP4_StscAtom
    +---------------------------------------------------------------------*/
    public class StscAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_StscAtom::Create
        +---------------------------------------------------------------------*/
        public static StscAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new StscAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_StscAtom::AP4_StscAtom
        +---------------------------------------------------------------------*/
        public StscAtom() :
            base(AtomType.STSC, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _cachedChunkGroup = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_StscAtom::AP4_StscAtom
        +---------------------------------------------------------------------*/
        private StscAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.STSC, size, version, flags)
        {
            _cachedChunkGroup = 0;

            uint first_sample = 1;
            uint entry_count;
            if (size - Ap4.ATOM_HEADER_SIZE < 4) return;
            stream.ReadUI32(out entry_count);
            if ((size - Ap4.ATOM_HEADER_SIZE - 4)/12 < entry_count) {
                return;
            }
            // m_Entries.SetItemCount(entry_count)
            _entries.Capacity = (int)entry_count;
            for (uint i=0; i<entry_count; i++) {
                _entries.Add(new StscTableEntry());
            }
            byte[] buffer = new byte[entry_count*12];
            Ap4Result result = stream.Read(buffer, entry_count*12);
            if (result.Failed()) {
                return;
            }
            for (uint i=0; i<entry_count; i++) {
                uint first_chunk              = Ap4Utils.BytesToUInt32BE(buffer, (int)(i*12  ));
                uint samples_per_chunk        = Ap4Utils.BytesToUInt32BE(buffer, (int)(i*12+4));
                uint sample_description_index = Ap4Utils.BytesToUInt32BE(buffer, (int)(i*12+8));
                if (i != 0) {
                    int prev = (int)(i-1);
                    _entries[prev]._chunkCount = first_chunk-_entries[prev]._firstChunk;
                    first_sample += _entries[prev]._chunkCount * _entries[prev]._samplesPerChunk;
                }
                _entries[(int)i]._chunkCount             = 0; // not known yet
                _entries[(int)i]._firstChunk             = first_chunk;
                _entries[(int)i]._firstSample            = first_sample;
                _entries[(int)i]._samplesPerChunk        = samples_per_chunk;
                _entries[(int)i]._sampleDescriptionIndex = sample_description_index;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_StscAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // entry count
            uint entry_count = (uint)_entries.Count;
            result = stream.WriteUI32(entry_count);

            // entries
            // PORT NOTE: as upstream, the results of the entry writes are not
            // assigned to `result` (only the entry count write is checked).
            for (uint i=0; i<entry_count; i++) {
                stream.WriteUI32(_entries[(int)i]._firstChunk);
                if (result.Failed()) return result;
                stream.WriteUI32(_entries[(int)i]._samplesPerChunk);
                if (result.Failed()) return result;
                stream.WriteUI32(_entries[(int)i]._sampleDescriptionIndex);
                if (result.Failed()) return result;
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_StscAtom::AddEntry
        +---------------------------------------------------------------------*/
        public virtual Ap4Result AddEntry(uint chunk_count,
                                          uint samples_per_chunk,
                                          uint sample_description_index)
        {
            uint first_chunk;
            uint first_sample;
            uint entry_count = (uint)_entries.Count;
            if (entry_count == 0) {
                // first entry
                first_chunk = 1;
                first_sample = 1;
            } else {
                first_chunk = _entries[(int)(entry_count-1)]._firstChunk+
                              _entries[(int)(entry_count-1)]._chunkCount;
                first_sample = _entries[(int)(entry_count-1)]._firstSample+
                               _entries[(int)(entry_count-1)]._chunkCount*
                               _entries[(int)(entry_count-1)]._samplesPerChunk;
            }
            _entries.Add(new StscTableEntry(first_chunk, first_sample, chunk_count, samples_per_chunk, sample_description_index));

            // update the atom size
            _size32 += 12;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StscAtom::GetChunkForSample
        +---------------------------------------------------------------------*/
        // PORT NOTE: the out parameters are zeroed on every failure path (upstream
        // leaves them untouched when it returns AP4_ERROR_INVALID_FORMAT).
        public virtual Ap4Result GetChunkForSample(uint     sample,
                                                   out uint chunk,
                                                   out uint skip,
                                                   out uint sample_description_index)
        {
            chunk = 0;
            skip = 0;
            sample_description_index = 0;

            // preconditions
            Ap4.ASSERT(sample > 0);

            // decide whether to start the search from the cached index
            // or from the start
            uint group;
            if (_cachedChunkGroup < (uint)_entries.Count &&
                _entries[(int)_cachedChunkGroup]._firstSample <= sample) {
                group = _cachedChunkGroup;
            } else {
                group = 0;
            }

            // find which group of chunk contains this one
            while (group < (uint)_entries.Count) {
                StscTableEntry entry = _entries[(int)group];
                uint sample_count =
                    entry._chunkCount*entry._samplesPerChunk;
                if (sample_count == 0) {
                    // unlimited samples in this group (last group)
                    if (entry._firstSample > sample) {
                        // something is wrong
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                } else {
                    // normal group
                    if (entry._firstSample + sample_count <= sample) {
                        // the sample is not in this group
                        group++;
                        continue;
                    }
                }

                // the sample is in this group
                if (entry._samplesPerChunk == 0) {
                    // something is wrong
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                uint chunk_offset =
                    ((sample-entry._firstSample) /
                    entry._samplesPerChunk);
                chunk = entry._firstChunk + chunk_offset;
                skip = sample -
                    (entry._firstSample +
                     entry._samplesPerChunk*chunk_offset);
                sample_description_index = entry._sampleDescriptionIndex;

                // cache the result (to accelerate finding the right group
                // next time around)
                _cachedChunkGroup = group;

                return Ap4Result.SUCCESS;
            }

            // chunk not found
            chunk = 0;
            skip = 0;
            sample_description_index = 0;
            return Ap4Result.ERROR_OUT_OF_RANGE;
        }

        /*----------------------------------------------------------------------
        |   AP4_StscAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("entry_count", (uint)_entries.Count);

            // dump table entries
            if (inspector.GetVerbosity() >= 1) {
                inspector.StartArray("entries", (uint)_entries.Count);
                for (int i=0; i<_entries.Count; i++) {
                    inspector.StartObject(null, 5, true);
                    inspector.AddField("first_chunk",       _entries[i]._firstChunk);
                    inspector.AddField("first_sample",      _entries[i]._firstSample);
                    inspector.AddField("chunk_count",       _entries[i]._chunkCount);
                    inspector.AddField("samples_per_chunk", _entries[i]._samplesPerChunk);
                    inspector.AddField("sample_desc_index", _entries[i]._sampleDescriptionIndex);
                    inspector.EndObject();
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private List<StscTableEntry> _entries = new List<StscTableEntry>();
        private uint                 _cachedChunkGroup;
    }
}
