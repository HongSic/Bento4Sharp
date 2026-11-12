// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SaioAtom.h, Ap4SaioAtom.cpp
// Original work Copyright 2002-2011 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SaioAtom
    +---------------------------------------------------------------------*/
    public class SaioAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_SaioAtom::Create
        +---------------------------------------------------------------------*/
        public static SaioAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new SaioAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_SaioAtom::AP4_SaioAtom
        +---------------------------------------------------------------------*/
        public SaioAtom() :
            base(AtomType.SAIO, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _auxInfoType          = 0;
            _auxInfoTypeParameter = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_SaioAtom::AddEntry
        +---------------------------------------------------------------------*/
        public Ap4Result AddEntry(ulong offset)
        {
            _entries.Add(offset);
            uint extra = (_flags&1) != 0?8u:0u;
            SetSize((ulong)(Ap4.FULL_ATOM_HEADER_SIZE+extra+4+(uint)_entries.Count*(_version==0?4u:8u)));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SaioAtom::SetEntry
        +---------------------------------------------------------------------*/
        public Ap4Result SetEntry(uint entry_index, ulong offset)
        {
            if ((uint)_entries.Count <= entry_index) return Ap4Result.ERROR_OUT_OF_RANGE;
            _entries[(int)entry_index] = offset;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SaioAtom::AP4_SaioAtom
        +---------------------------------------------------------------------*/
        private SaioAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.SAIO, size, version, flags)
        {
            _auxInfoType          = 0;
            _auxInfoTypeParameter = 0;

            int remains = unchecked((int)(size-Ap4.FULL_ATOM_HEADER_SIZE));
            if ((flags & 1) != 0) {
                if (remains < 8) return;
                stream.ReadUI32(out _auxInfoType);
                stream.ReadUI32(out _auxInfoTypeParameter);
                remains -= 8;
            }
            if (remains < 4) return;
            uint entry_count = 0;
            Ap4Result result = stream.ReadUI32(out entry_count);
            if (result.Failed()) return;
            remains -= 4;
            if (remains < (long)entry_count*(_version==0?4:8)) {
                return;
            }
            // m_Entries.SetItemCount(entry_count)
            _entries.Capacity = (int)entry_count;
            for (uint i=0; i<entry_count; i++) {
                _entries.Add(0);
            }
            for (uint i=0; i<entry_count; i++) {
                if (_version == 0) {
                    uint entry = 0;
                    result = stream.ReadUI32(out entry);
                    if (result.Failed()) return;
                    _entries[(int)i] = entry;
                } else {
                    ulong entry = 0;
                    result = stream.ReadUI64(out entry);
                    if (result.Failed()) return;
                    _entries[(int)i] = entry;
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SaioAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            if ((_flags&1) != 0) {
                result = stream.WriteUI32(_auxInfoType);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_auxInfoTypeParameter);
                if (result.Failed()) return result;
            }

            result = stream.WriteUI32((uint)_entries.Count);
            if (result.Failed()) return result;
            for (int i=0; i<_entries.Count; i++) {
                if (_version == 0) {
                    result = stream.WriteUI32((uint)_entries[i]);
                    if (result.Failed()) return result;
                } else {
                    result = stream.WriteUI64(_entries[i]);
                    if (result.Failed()) return result;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SaioAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            if ((_flags&1) != 0) {
                inspector.AddField("aux info type", _auxInfoType, AtomInspector.FormatHint.HINT_HEX);
                inspector.AddField("aux info type parameter", _auxInfoTypeParameter, AtomInspector.FormatHint.HINT_HEX);
            }
            inspector.AddField("entry_count", (uint)_entries.Count);

            if (inspector.GetVerbosity() >= 2) {
                inspector.StartArray("entries", (uint)_entries.Count);
                for (int i=0; i<_entries.Count; i++) {
                    inspector.AddField(null, _entries[i]);
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint        GetAuxInfoType() { return _auxInfoType; }
        public uint        GetAuxInfoTypeParameter() { return _auxInfoTypeParameter; }
        public List<ulong> GetEntries() { return _entries; }

        // members
        private uint        _auxInfoType;
        private uint        _auxInfoTypeParameter;
        private List<ulong> _entries = new List<ulong>();
    }
}
