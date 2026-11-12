// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4MfhdAtom.h, Ap4MfhdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_MfhdAtom
    +---------------------------------------------------------------------*/
    public class MfhdAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_MfhdAtom::Create
        +---------------------------------------------------------------------*/
        public static MfhdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 0) return null;
            return new MfhdAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_MfhdAtom::AP4_MfhdAtom
        +---------------------------------------------------------------------*/
        public MfhdAtom(uint sequence_number) :
            base(AtomType.MFHD, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _sequenceNumber = sequence_number;
        }

        /*----------------------------------------------------------------------
        |   AP4_MfhdAtom::AP4_MfhdAtom
        +---------------------------------------------------------------------*/
        private MfhdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.MFHD, size, version, flags)
        {
            _sequenceNumber = 0;
            stream.ReadUI32(out _sequenceNumber);
        }

        /*----------------------------------------------------------------------
        |   AP4_MfhdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return stream.WriteUI32(_sequenceNumber);
        }

        /*----------------------------------------------------------------------
        |   AP4_MfhdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("sequence number", _sequenceNumber);

            return Ap4Result.SUCCESS;
        }

        public uint GetSequenceNumber()                     { return _sequenceNumber;            }
        public void SetSequenceNumber(uint sequence_number) { _sequenceNumber = sequence_number; }

        // members
        private uint _sequenceNumber;
    }
}
