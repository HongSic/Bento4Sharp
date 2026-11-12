// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SbgpAtom.h, Ap4SbgpAtom.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SbgpAtom
    +---------------------------------------------------------------------*/
    public class SbgpAtom : Atom
    {
        // types
        // PORT NOTE: a class (not a struct) so that entries held in a List<T> can
        // be modified in place, as upstream does through AP4_Array<T>::operator[].
        public class Entry {
            public uint sample_count;
            public uint group_description_index;
        }

        /*----------------------------------------------------------------------
        |   AP4_SbgpAtom::Create
        +---------------------------------------------------------------------*/
        public static SbgpAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new SbgpAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_SbgpAtom::AP4_SbgpAtom
        +---------------------------------------------------------------------*/
        public SbgpAtom() :
            base(AtomType.SBGP, Ap4.FULL_ATOM_HEADER_SIZE+8, 0, 0)
        {
            _groupingType = 0;
            _groupingTypeParameter = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_SbgpAtom::AP4_SbgpAtom
        +---------------------------------------------------------------------*/
        private SbgpAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.SBGP, size, version, flags)
        {
            _groupingType = 0;
            _groupingTypeParameter = 0;

            if (size < Ap4.FULL_ATOM_HEADER_SIZE + 4) return;
            uint remains = size-Ap4.FULL_ATOM_HEADER_SIZE;
            stream.ReadUI32(out _groupingType);
            remains -= 4;

            if (version >= 1) {
                if (remains < 4) return;
                stream.ReadUI32(out _groupingTypeParameter);
                remains -= 4;
            }

            if (remains < 4) return;
            uint entry_count = 0;
            Ap4Result result = stream.ReadUI32(out entry_count);
            if (result.Failed()) return;
            remains -= 4;

            if (remains < (ulong)entry_count*8) {
                return;
            }
            // m_Entries.SetItemCount(entry_count)
            _entries.Capacity = (int)entry_count;
            for (uint i=0; i<entry_count; i++) {
                Entry entry = new Entry();
                stream.ReadUI32(out entry.sample_count);
                stream.ReadUI32(out entry.group_description_index);
                _entries.Add(entry);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SbgpAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.WriteUI32(_groupingType);
            if (result.Failed()) return result;

            if (_version >= 1) {
                result = stream.WriteUI32(_groupingTypeParameter);
                if (result.Failed()) return result;
            }

            result = stream.WriteUI32((uint)_entries.Count);
            if (result.Failed()) return result;

            for (int i=0; i<_entries.Count; i++) {
                result = stream.WriteUI32(_entries[i].sample_count);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_entries[i].group_description_index);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SbgpAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string fourcc = Ap4Utils.FormatFourChars(_groupingType);
            inspector.AddField("grouping_type", fourcc);
            if (_version >= 1) {
                inspector.AddField("grouping_type_parameter", _groupingTypeParameter);
            }
            inspector.AddField("entry_count", (uint)_entries.Count);

            if (inspector.GetVerbosity() >= 2) {
                inspector.StartArray("entries", (uint)_entries.Count);
                for (int i=0; i<_entries.Count; i++) {
                    inspector.StartObject(null, 2, true);
                    inspector.AddField("sample_count",            _entries[i].sample_count);
                    inspector.AddField("group_description_index", _entries[i].group_description_index);
                    inspector.EndObject();
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint GetGroupingType()          { return _groupingType;          }
        public uint GetGroupingTypeParameter() { return _groupingTypeParameter; }
        public List<Entry> GetEntries()        { return _entries;               }

        // members
        private uint _groupingType;
        private uint _groupingTypeParameter;
        private List<Entry> _entries = new List<Entry>();
    }
}
