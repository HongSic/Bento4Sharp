// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4DrefAtom.h, Ap4DrefAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_DrefAtom
    +---------------------------------------------------------------------*/
    public class DrefAtom : ContainerAtom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_DrefAtom::Create
        +---------------------------------------------------------------------*/
        public static DrefAtom Create(uint        size,
                                       ByteStream  stream,
                                       AtomFactory atom_factory)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new DrefAtom(size, version, flags, stream, atom_factory);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_DrefAtom::AP4_DrefAtom
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream calls AP4_ContainerAtom(AP4_ATOM_TYPE_DREF, (AP4_UI32)0, (AP4_UI32)0),
        // which C++ overload resolution binds to the (type, version, flags) constructor.
        // As upstream, the refs are added to the child list directly (no SetParent).
        public DrefAtom(Atom[] refs, uint refs_count) :
            base(AtomType.DREF, (byte)0, (uint)0)
        {
            _size32 += 4;
            for (uint i=0; i<refs_count; i++) {
                _children.Add(refs[i]);
                _size32 += (uint)refs[i].GetSize();
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_DrefAtom::AP4_DrefAtom
        +---------------------------------------------------------------------*/
        private DrefAtom(uint        size,
                         byte        version,
                         uint        flags,
                         ByteStream  stream,
                         AtomFactory atom_factory) :
            base(AtomType.DREF, size, false, version, flags)
        {
            if (size <= Ap4.FULL_ATOM_HEADER_SIZE + 4) {
                return;
            }

            // read the number of entries
            uint entry_count;
            Ap4Result result = stream.ReadUI32(out entry_count);
            if (result.Failed()) return;

            // read children
            ulong bytes_available = size - (Ap4.FULL_ATOM_HEADER_SIZE + 4);
            while (entry_count-- != 0) {
                Atom atom;
                while (atom_factory.CreateAtomFromStream(stream,
                                                         ref bytes_available,
                                                         out atom).Succeeded()) {
                    _children.Add(atom);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_DrefAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the number of entries
            Ap4Result result = stream.WriteUI32((uint)_children.Count);
            if (result.Failed()) return result;

            // write the children
            return _children.Apply(new AtomListWriter(stream));
        }
    }
}
