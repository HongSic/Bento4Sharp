// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4MfroAtom.h, Ap4MfroAtom.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_MfroAtom
    +---------------------------------------------------------------------*/
    public class MfroAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_MfroAtom::Create
        +---------------------------------------------------------------------*/
        public static MfroAtom Create(uint size, ByteStream stream)
        {
            byte version = 0;
            uint flags   = 0;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            Ap4Result result = ReadFullHeader(stream, out version, out flags);
            if (result.Failed()) return null;
            if (version != 0) return null;
            return new MfroAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_MfroAtom::AP4_MfroAtom
        +---------------------------------------------------------------------*/
        public MfroAtom(uint mfra_size) :
            base(AtomType.MFRO, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _mfraSize = mfra_size;
        }

        /*----------------------------------------------------------------------
        |   AP4_MfroAtom::AP4_MfroAtom
        +---------------------------------------------------------------------*/
        private MfroAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.MFRO, size, version, flags)
        {
            _mfraSize = 0;
            stream.ReadUI32(out _mfraSize);
        }

        /*----------------------------------------------------------------------
        |   AP4_MfroAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return stream.WriteUI32(_mfraSize);
        }

        /*----------------------------------------------------------------------
        |   AP4_MfroAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("mfra_size", _mfraSize);

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint GetMfraSize() { return _mfraSize; }

        // members
        private uint _mfraSize;
    }
}
