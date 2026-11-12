// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SthdAtom.h, Ap4SthdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SthdAtom
    +---------------------------------------------------------------------*/
    public class SthdAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_SthdAtom::Create
        +---------------------------------------------------------------------*/
        public static SthdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new SthdAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_SthdAtom::AP4_SthdAtom
        +---------------------------------------------------------------------*/
        public SthdAtom() :
            base(AtomType.STHD, Ap4.FULL_ATOM_HEADER_SIZE, 0, 0)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_SthdAtom::AP4_SthdAtom
        +---------------------------------------------------------------------*/
        private SthdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.STHD, size, version, flags)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_SthdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return Ap4Result.SUCCESS;
        }
    }
}
