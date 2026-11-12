// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4FrmaAtom.h, Ap4FrmaAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_FrmaAtom
    +---------------------------------------------------------------------*/
    public class FrmaAtom : Atom
    {
        // class methods
        public static FrmaAtom Create(uint size, ByteStream stream)
        {
            return new FrmaAtom(size, stream);
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_FrmaAtom::AP4_FrmaAtom
        +---------------------------------------------------------------------*/
        public FrmaAtom(uint original_format) :
            base(AtomType.FRMA, Ap4.ATOM_HEADER_SIZE+4)
        {
            _originalFormat = original_format;
        }

        /*----------------------------------------------------------------------
        |   AP4_FrmaAtom::AP4_FrmaAtom
        +---------------------------------------------------------------------*/
        private FrmaAtom(uint size, ByteStream stream) :
            base(AtomType.FRMA, size)
        {
            _originalFormat = 0;
            stream.ReadUI32(out _originalFormat);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_FrmaAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the original format
            return stream.WriteUI32(_originalFormat);
        }

        /*----------------------------------------------------------------------
        |   AP4_FrmaAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string format = Ap4Utils.FormatFourChars(_originalFormat);
            inspector.AddField("original_format", format);

            return Ap4Result.SUCCESS;
        }

        public uint GetOriginalFormat() { return _originalFormat; }

        // members
        private uint _originalFormat;
    }
}
