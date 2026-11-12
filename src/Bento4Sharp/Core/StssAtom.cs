// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4StssAtom.h, Ap4StssAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_StssAtom
    +---------------------------------------------------------------------*/
    public class StssAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_StssAtom::Create
        +---------------------------------------------------------------------*/
        public static StssAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new StssAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_StssAtom::AP4_StssAtom
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream does not initialize m_LookupCache in this
        // constructor; it is 0 here.
        public StssAtom() :
            base(AtomType.STSS, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_StssAtom::AP4_StssAtom
        +---------------------------------------------------------------------*/
        private StssAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.STSS, size, version, flags)
        {
            _lookupCache = 0;

            if (size - Ap4.ATOM_HEADER_SIZE < 4) return;

            uint entry_count;
            stream.ReadUI32(out entry_count);

            // check for bogus values
            if ((size - Ap4.ATOM_HEADER_SIZE - 4) / 4 < entry_count) return;

            // read the table into a local array for conversion
            byte[] buffer = new byte[entry_count*4];
            Ap4Result result = stream.Read(buffer, entry_count*4);
            if (result.Failed()) {
                return;
            }
            // m_Entries.SetItemCount(entry_count)
            _entries.Capacity = (int)entry_count;
            for (uint i=0; i<entry_count; i++) {
                _entries.Add(Ap4Utils.BytesToUInt32BE(buffer, (int)(i*4)));
            }
        }

        // methods
        public List<uint> GetEntries() { return _entries; }

        /*----------------------------------------------------------------------
        |   AP4_StssAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // entry count
            uint entry_count = (uint)_entries.Count;
            result = stream.WriteUI32(entry_count);
            if (result.Failed()) return result;

            // entries
            for (uint i=0; i<entry_count; i++) {
                result = stream.WriteUI32(_entries[(int)i]);
                if (result.Failed()) return result;
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_StssAtom::AddEntry
        +---------------------------------------------------------------------*/
        public Ap4Result AddEntry(uint sample)
        {
            _entries.Add(sample);
            _size32 += 4;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StssAtom::IsSampleSync
        +---------------------------------------------------------------------*/
        public virtual bool IsSampleSync(uint sample)
        {
            uint entry_index = 0;

            // check bounds
            if (sample == 0 || _entries.Count == 0) return false;

            // see if we can start from the cached index
            if (_entries[(int)_lookupCache] <= sample) {
                entry_index = _lookupCache;
            }

            // do a linear search
            while (entry_index < (uint)_entries.Count &&
                   _entries[(int)entry_index] <= sample) {
                if (_entries[(int)entry_index] == sample) {
                    _lookupCache = entry_index;
                    return true;
                }
                entry_index++;
            }

            return false;
        }

        /*----------------------------------------------------------------------
        |   AP4_StssAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("entry_count", (uint)_entries.Count);

            return Ap4Result.SUCCESS;
        }

        // members
        private List<uint> _entries = new List<uint>();
        private uint       _lookupCache;
    }
}
