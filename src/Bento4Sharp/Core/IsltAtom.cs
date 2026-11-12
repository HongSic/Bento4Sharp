// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4IsltAtom.h, Ap4IsltAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_IsltAtom
    +---------------------------------------------------------------------*/
    public class IsltAtom : Atom
    {
        // class methods
        public static IsltAtom Create(uint size, ByteStream stream) {
            if (size != Ap4.ATOM_HEADER_SIZE+8) return null;
            return new IsltAtom(size, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_IsltAtom::AP4_IsltAtom
        +---------------------------------------------------------------------*/
        public IsltAtom(byte[] salt) :
            base(AtomType.ISLT, Ap4.ATOM_HEADER_SIZE+8)
        {
            for (uint i=0; i<8; i++) {
                _salt[i] = salt[i];
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_IsltAtom::AP4_IsltAtom
        +---------------------------------------------------------------------*/
        private IsltAtom(uint size, ByteStream stream) :
            base(AtomType.ISLT, size)
        {
            stream.Read(_salt, 8);
        }

        /*----------------------------------------------------------------------
        |   AP4_IsltAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return stream.Write(_salt, 8);
        }

        /*----------------------------------------------------------------------
        |   AP4_IsltAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("salt", _salt, 8);

            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte[] GetSalt() { return _salt; }

        // members
        private byte[] _salt = new byte[8];
    }
}
