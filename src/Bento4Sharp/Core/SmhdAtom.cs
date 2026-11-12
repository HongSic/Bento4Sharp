// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SmhdAtom.h, Ap4SmhdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SmhdAtom
    +---------------------------------------------------------------------*/
    public class SmhdAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_SmhdAtom::Create
        +---------------------------------------------------------------------*/
        public static SmhdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new SmhdAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_SmhdAtom::AP4_SmhdAtom
        +---------------------------------------------------------------------*/
        public SmhdAtom(ushort balance) :
            base(AtomType.SMHD, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _balance = balance;
            _reserved = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_SmhdAtom::AP4_SmhdAtom
        +---------------------------------------------------------------------*/
        private SmhdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.SMHD, size, version, flags)
        {
            stream.ReadUI16(out _balance);
            stream.ReadUI16(out _reserved);
        }

        /*----------------------------------------------------------------------
        |   AP4_SmhdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // balance
            result = stream.WriteUI16(_balance);
            if (result.Failed()) return result;

            // reserved
            result = stream.WriteUI16(_reserved);
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SmhdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("balance", _balance);

            return Ap4Result.SUCCESS;
        }

        // members
        private ushort _balance;
        private ushort _reserved;
    }
}
