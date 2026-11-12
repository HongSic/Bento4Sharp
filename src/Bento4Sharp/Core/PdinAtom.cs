// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4PdinAtom.h, Ap4PdinAtom.cpp
// Original work Copyright 2002-2011 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;
using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_PdinAtom
    +---------------------------------------------------------------------*/
    public class PdinAtom : Atom
    {
        // inner classes
        // PORT NOTE: a class (reference type) so that entries returned by
        // GetEntries() can be modified in place like the elements of the upstream
        // AP4_Array.
        public class Entry {
            public Entry() { _rate = 0; _initialDelay = 0; }
            public uint _rate;
            public uint _initialDelay;
        }

        // class methods
        /*----------------------------------------------------------------------
        |   AP4_PdinAtom::Create
        +---------------------------------------------------------------------*/
        public static PdinAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new PdinAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_PdinAtom::AP4_PdinAtom
        +---------------------------------------------------------------------*/
        public PdinAtom() :
            base(AtomType.PDIN, Ap4.FULL_ATOM_HEADER_SIZE, 0, 0)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_PdinAtom::AddEntry
        +---------------------------------------------------------------------*/
        public Ap4Result AddEntry(uint rate, uint initial_delay)
        {
            Entry entry = new Entry();
            entry._rate = rate;
            entry._initialDelay = initial_delay;
            _entries.Add(entry);

            SetSize(Ap4.FULL_ATOM_HEADER_SIZE+(uint)_entries.Count*8);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_PdinAtom::AP4_PdinAtom
        +---------------------------------------------------------------------*/
        private PdinAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.PDIN, size, version, flags)
        {
            uint entry_count = (size-Ap4.FULL_ATOM_HEADER_SIZE)/8;
            // m_Entries.SetItemCount(entry_count);
            for (uint i=0; i<entry_count; i++) {
                _entries.Add(new Entry());
            }
            for (uint i=0; i<entry_count; i++) {
                stream.ReadUI32(out _entries[(int)i]._rate);
                stream.ReadUI32(out _entries[(int)i]._initialDelay);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_PdinAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            for (int i=0; i<_entries.Count; i++) {
                result = stream.WriteUI32(_entries[i]._rate);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_entries[i]._initialDelay);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_PdinAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string header;
            for (int i=0; i<_entries.Count; i++) {
                header = "rate(" + i.ToString(CultureInfo.InvariantCulture) + ")";
                inspector.AddField(header, _entries[i]._rate);
                header = "initial_delay(" + i.ToString(CultureInfo.InvariantCulture) + ")";
                inspector.AddField(header, _entries[i]._initialDelay);
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public List<Entry> GetEntries() { return _entries; }

        // members
        private List<Entry> _entries = new List<Entry>();
    }
}
