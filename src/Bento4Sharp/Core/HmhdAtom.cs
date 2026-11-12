// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4HmhdAtom.h, Ap4HmhdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_HmhdAtom
    +---------------------------------------------------------------------*/
    public class HmhdAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_HmhdAtom::Create
        +---------------------------------------------------------------------*/
        public static HmhdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new HmhdAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_HmhdAtom::AP4_HmhdAtom
        +---------------------------------------------------------------------*/
        private HmhdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.HMHD, size, version, flags)
        {
            _maxPduSize = 0;
            _avgPduSize = 0;
            _maxBitrate = 0;
            _avgBitrate = 0;
            _reserved = 0;

            stream.ReadUI16(out _maxPduSize);
            stream.ReadUI16(out _avgPduSize);
            stream.ReadUI32(out _maxBitrate);
            stream.ReadUI32(out _avgBitrate);
            stream.ReadUI32(out _reserved);
        }

        /*----------------------------------------------------------------------
        |   AP4_HmhdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // max pdu size
            result = stream.WriteUI16(_maxPduSize);
            if (result.Failed()) return result;

            // avg pdu size
            result = stream.WriteUI16(_avgPduSize);
            if (result.Failed()) return result;

            // max bitrate
            result = stream.WriteUI32(_maxBitrate);
            if (result.Failed()) return result;

            // avg bitrate
            result = stream.WriteUI32(_avgBitrate);
            if (result.Failed()) return result;

            // reserved
            result = stream.WriteUI32(_reserved);
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_HmhdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("max_pdu_size", _maxPduSize);
            inspector.AddField("avg_pdu_size", _avgPduSize);
            inspector.AddField("max_bitrate",  _maxBitrate);
            inspector.AddField("avg_bitrate",  _avgBitrate);

            return Ap4Result.SUCCESS;
        }

        // members
        private ushort _maxPduSize;
        private ushort _avgPduSize;
        private uint   _maxBitrate;
        private uint   _avgBitrate;
        private uint   _reserved;
    }
}
