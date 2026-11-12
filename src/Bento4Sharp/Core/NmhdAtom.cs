// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4NmhdAtom.h, Ap4NmhdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_NmhdAtom
    +---------------------------------------------------------------------*/
    public class NmhdAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_NmhdAtom::Create
        +---------------------------------------------------------------------*/
        public static NmhdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new NmhdAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_NmhdAtom::AP4_NmhdAtom
        +---------------------------------------------------------------------*/
        public NmhdAtom() :
            base(AtomType.NMHD, Ap4.FULL_ATOM_HEADER_SIZE, 0, 0)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_NmhdAtom::AP4_NmhdAtom
        +---------------------------------------------------------------------*/
        private NmhdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.NMHD, size, version, flags)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_NmhdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return Ap4Result.SUCCESS;
        }
    }
}
