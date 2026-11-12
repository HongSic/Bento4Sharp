// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4CttsAtom.h, Ap4CttsAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_CttsTableEntry
    +---------------------------------------------------------------------*/
    // PORT NOTE: a class (not a struct), see StscTableEntry.
    public class CttsTableEntry
    {
        public CttsTableEntry()
        {
            _sampleCount = 0;
            _sampleOffset = 0;
        }
        public CttsTableEntry(uint sample_count,
                              uint sample_offset)
        {
            _sampleCount = sample_count;
            _sampleOffset = sample_offset;
        }

        public uint _sampleCount;
        public uint _sampleOffset;
    }

    /*----------------------------------------------------------------------
    |   AP4_CttsAtom
    +---------------------------------------------------------------------*/
    public class CttsAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_CttsAtom::Create
        +---------------------------------------------------------------------*/
        public static CttsAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new CttsAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_CttsAtom::AP4_CttsAtom
        +---------------------------------------------------------------------*/
        public CttsAtom() :
            base(AtomType.CTTS, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _lookupCache.sample      = 0;
            _lookupCache.entry_index = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_CttsAtom::AP4_CttsAtom
        +---------------------------------------------------------------------*/
        private CttsAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.CTTS, size, version, flags)
        {
            _lookupCache.sample      = 0;
            _lookupCache.entry_index = 0;

            if (size < Ap4.FULL_ATOM_HEADER_SIZE + 4) {
                return;
            }

            // read the number of entries
            uint entry_count;
            stream.ReadUI32(out entry_count);

            // check that there's enough data
            if (((size - Ap4.FULL_ATOM_HEADER_SIZE - 4) / 8) < entry_count) {
                return;
            }

            // read the entries
            // m_Entries.SetItemCount(entry_count)
            _entries.Capacity = (int)entry_count;
            for (uint i=0; i<entry_count; i++) {
                _entries.Add(new CttsTableEntry());
            }
            byte[] buffer = new byte[entry_count*8];
            Ap4Result result = stream.Read(buffer, entry_count*8);
            if (result.Failed()) {
                return;
            }
            //bool use_quicktime_format = false;
            //AP4_SI32 quicktime_min_offset = 0;
            for (uint i=0; i<entry_count; i++) {
                _entries[(int)i]._sampleCount = Ap4Utils.BytesToUInt32BE(buffer, (int)(i*8  ));
                uint offset                   = Ap4Utils.BytesToUInt32BE(buffer, (int)(i*8+4));
                //if (offset & 0x80000000) {
                //    use_quicktime_format = true;
                //    AP4_SI32 noffset = (AP4_SI32)offset;
                //    if (noffset < quicktime_min_offset) quicktime_min_offset = noffset;
                //}
                _entries[(int)i]._sampleOffset = offset;
            }

            // in the quicktime format, the offsets can be positive or negative, so
            // we need to adjust for them here
            //if (use_quicktime_format) {
            //    for (unsigned i=0; i<entry_count; i++) {
            //        m_Entries[i].m_SampleOffset -= quicktime_min_offset;
            //    }
            //}
        }

        /*----------------------------------------------------------------------
        |   AP4_CttsAtom::AddEntry
        +---------------------------------------------------------------------*/
        public Ap4Result AddEntry(uint count, uint cts_offset)
        {
            _entries.Add(new CttsTableEntry(count, cts_offset));
            _size32 += 8;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CttsAtom::GetCtsOffset
        +---------------------------------------------------------------------*/
        public Ap4Result GetCtsOffset(uint sample, out uint cts_offset)
        {
            // default value
            cts_offset = 0;

            // sample indexes start at 1
            if (sample == 0) return Ap4Result.ERROR_OUT_OF_RANGE;

            // check the lookup cache
            uint lookup_start = 0;
            uint sample_start = 0;
            if (sample >= _lookupCache.sample) {
                // start from the cached entry
                lookup_start = _lookupCache.entry_index;
                sample_start = _lookupCache.sample;
            }

            for (uint i = lookup_start; i < (uint)_entries.Count; i++) {
                CttsTableEntry entry = _entries[(int)i];

                // check if we have reached the sample
                if (sample <= sample_start+entry._sampleCount) {
                    // we are within the sample range for the current entry
                    cts_offset = entry._sampleOffset;

                    // update the lookup cache
                    _lookupCache.entry_index = i;
                    _lookupCache.sample      = sample_start;

                    return Ap4Result.SUCCESS;
                }

                // update the upper bound
                sample_start += entry._sampleCount;
            }

            // sample is greater than the number of samples
            return Ap4Result.ERROR_OUT_OF_RANGE;
        }

        /*----------------------------------------------------------------------
        |   AP4_CttsAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // write the entry count
            uint entry_count = (uint)_entries.Count;
            result = stream.WriteUI32(entry_count);
            if (result.Failed()) return result;

            // write the entries
            for (uint i=0; i<entry_count; i++) {
                // sample count
                result = stream.WriteUI32(_entries[(int)i]._sampleCount);
                if (result.Failed()) return result;

                // time offset
                result = stream.WriteUI32(_entries[(int)i]._sampleOffset);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CttsAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("entry_count", (uint)_entries.Count);

            if (inspector.GetVerbosity() >= 2) {
                inspector.StartArray("entries", (uint)_entries.Count);
                for (int i=0; i<_entries.Count; i++) {
                    inspector.StartObject(null, 2, true);
                    inspector.AddField("count",  _entries[i]._sampleCount);
                    inspector.AddField("offset", _entries[i]._sampleOffset);
                    inspector.EndObject();
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private List<CttsTableEntry> _entries = new List<CttsTableEntry>();
        private struct LookupCache {
            public uint sample;
            public uint entry_index;
        }
        private LookupCache _lookupCache;
    }
}
