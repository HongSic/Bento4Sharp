// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TimsAtom.h, Ap4TimsAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_TimsAtom
    +---------------------------------------------------------------------*/
    public class TimsAtom : Atom
    {
        // class methods
        public static TimsAtom Create(uint size, ByteStream stream) {
            return new TimsAtom(size, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_TimsAtom::AP4_TimsAtom
        +---------------------------------------------------------------------*/
        public TimsAtom(uint timescale) :
            base(AtomType.TIMS, Ap4.ATOM_HEADER_SIZE+4)
        {
            _timeScale = timescale;
        }

        /*----------------------------------------------------------------------
        |   AP4_TimsAtom::AP4_TimsAtom
        +---------------------------------------------------------------------*/
        private TimsAtom(uint size, ByteStream stream) :
            base(AtomType.TIMS, size)
        {
            stream.ReadUI32(out _timeScale);
        }

        /*----------------------------------------------------------------------
        |   AP4_TimsAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // timescale
            return stream.WriteUI32(_timeScale);
        }

        /*----------------------------------------------------------------------
        |   AP4_TimsAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("timescale", _timeScale);

            return Ap4Result.SUCCESS;
        }

        // accessors
        public virtual uint GetTimeScale() { return _timeScale; }

        // members
        private uint _timeScale;
    }
}
