// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4IproAtom.h, Ap4IproAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_IproAtom
    +---------------------------------------------------------------------*/
    public class IproAtom : ContainerAtom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_IproAtom::Create
        +---------------------------------------------------------------------*/
        public static IproAtom Create(uint        size,
                                       ByteStream  stream,
                                       AtomFactory atom_factory)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new IproAtom(size, version, flags, stream, atom_factory);
        }

        /*----------------------------------------------------------------------
        |   AP4_IproAtom::AP4_IproAtom
        +---------------------------------------------------------------------*/
        private IproAtom(uint        size,
                         byte        version,
                         uint        flags,
                         ByteStream  stream,
                         AtomFactory atom_factory) :
            base(AtomType.IPRO, size, false, version, flags)
        {
            if (size < Ap4.FULL_ATOM_HEADER_SIZE + 2) {
                return;
            }

            // read the number of entries
            ushort entry_count;
            stream.ReadUI16(out entry_count);

            // read all entries
            ulong bytes_available = size-Ap4.FULL_ATOM_HEADER_SIZE-2;
            for (uint i=0; i<entry_count; i++) {
                Atom  atom;
                if (atom_factory.CreateAtomFromStream(stream,
                                                      ref bytes_available,
                                                      out atom).Succeeded()) {
                    atom.SetParent(this);
                    _children.Add(atom);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_IproAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // entry count
            result = stream.WriteUI16((ushort)_children.Count);
            if (result.Failed()) return result;

            // entries
            return _children.Apply(new AtomListWriter(stream));
        }

        /*----------------------------------------------------------------------
        |   AP4_IproAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("entry-count", (uint)_children.Count);

            // inspect children
            _children.Apply(new AtomListInspector(inspector));

            return Ap4Result.SUCCESS;
        }
    }
}
