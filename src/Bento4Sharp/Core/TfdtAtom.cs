// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TfdtAtom.h, Ap4TfdtAtom.cpp
// Original work Copyright 2002-2011 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_TfdtAtom
    +---------------------------------------------------------------------*/
    public class TfdtAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_TfdtAtom::Create
        +---------------------------------------------------------------------*/
        public static TfdtAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new TfdtAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_TfdtAtom::AP4_TfdtAtom
        +---------------------------------------------------------------------*/
        public TfdtAtom(byte  version,
                        ulong base_media_decode_time) :
            base(AtomType.TFDT, Ap4.FULL_ATOM_HEADER_SIZE+(version==0?4u:8u), version, 0)
        {
            _baseMediaDecodeTime = base_media_decode_time;
        }

        /*----------------------------------------------------------------------
        |   AP4_TfdtAtom::AP4_TfdtAtom
        +---------------------------------------------------------------------*/
        private TfdtAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.TFDT, size, version, flags)
        {
            if (version == 0) {
                uint value = 0;
                stream.ReadUI32(out value);
                _baseMediaDecodeTime = value;
            } else if (version == 1) {
                stream.ReadUI64(out _baseMediaDecodeTime);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TfdtAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            if (_version == 0) {
                result = stream.WriteUI32((uint)_baseMediaDecodeTime);
            } else if (_version == 1) {
                result = stream.WriteUI64(_baseMediaDecodeTime);
            } else {
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TfdtAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("base media decode time", _baseMediaDecodeTime);

            return Ap4Result.SUCCESS;
        }

        public ulong GetBaseMediaDecodeTime() {
            return _baseMediaDecodeTime;
        }
        public void SetBaseMediaDecodeTime(ulong base_media_decode_time) {
            _baseMediaDecodeTime = base_media_decode_time;
        }

        // members
        private ulong _baseMediaDecodeTime;
    }
}
