// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4MehdAtom.h, Ap4MehdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_MehdAtom
    +---------------------------------------------------------------------*/
    public class MehdAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_MehdAtom::Create
        +---------------------------------------------------------------------*/
        public static MehdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new MehdAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_MehdAtom::AP4_MehdAtom
        +---------------------------------------------------------------------*/
        public MehdAtom(ulong duration) :
            base(AtomType.MEHD, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _duration = duration;
            if (duration > 0xFFFFFFFF) {
                _version = 1;
                _size32 += 4;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MehdAtom::AP4_MehdAtom
        +---------------------------------------------------------------------*/
        private MehdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.MEHD, size, version, flags)
        {
            if (_version == 0) {
                uint duration;
                stream.ReadUI32(out duration);
                _duration = duration;
            } else {
                stream.ReadUI64(out _duration);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MehdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            if (_version == 0) {
                return stream.WriteUI32((uint)_duration);
            } else {
                return stream.WriteUI64(_duration);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MehdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("duration", _duration);

            return Ap4Result.SUCCESS;
        }

        public ulong GetDuration() { return _duration; }
        public void  SetDuration(ulong duration) { _duration = duration;}

        // members
        private ulong _duration;
    }
}
