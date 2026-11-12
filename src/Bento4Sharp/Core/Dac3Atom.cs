// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Dac3Atom.h, Ap4Dac3Atom.cpp
// Original work Copyright 2002-2019 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Dac3Atom
    +---------------------------------------------------------------------*/
    public class Dac3Atom : Atom
    {
        // types
        public struct StreamInfo
        {
            public uint fscod;
            public uint bsid;
            public uint bsmod;
            public uint acmod;
            public uint lfeon;
            public uint bit_rate_code;
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac3Atom::Create
        +---------------------------------------------------------------------*/
        public static Dac3Atom Create(uint size, ByteStream stream)
        {
            // read the raw bytes in a buffer
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            DataBuffer payload_data = new DataBuffer(payload_size);
            Ap4Result result = stream.Read(payload_data.UseData(), payload_size);
            if (result.Failed()) return null;

            // check the version
            byte[] payload = payload_data.GetData();
            return new Dac3Atom(size, payload);
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac3Atom::AP4_Dac3Atom
        +---------------------------------------------------------------------*/
        public Dac3Atom(Dac3Atom other) :
            base(AtomType.DAC3, other._size32)
        {
            _dataRate = other._dataRate;
            _streamInfo = other._streamInfo;
            _rawBytes = new DataBuffer(other._rawBytes);
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac3Atom::AP4_Dac3Atom
        +---------------------------------------------------------------------*/
        // DSI vaiable initialize m_RawBytes (SpecificBoxInfo -> m_RawBytes)
        // PORT NOTE: upstream takes a `const StreamInfo*` (named m_StreamInfo, which
        // shadows the member); the member m_StreamInfo and m_DataRate are left
        // uninitialized upstream and are 0 in the port.
        public Dac3Atom(StreamInfo stream_info) :
            base(AtomType.DAC3, Ap4.ATOM_HEADER_SIZE)
        {
            BitWriter bits = new BitWriter(3);
            bits.Write(stream_info.fscod, 2);
            bits.Write(stream_info.bsid, 5);
            bits.Write(stream_info.bsmod, 3);
            bits.Write(stream_info.acmod, 3);
            bits.Write(stream_info.lfeon, 1);
            bits.Write(stream_info.bit_rate_code, 5);
            bits.Write(0, 5);  // reserved

            _rawBytes.SetData(bits.GetData(), bits.GetBitCount() / 8);
            _size32 += _rawBytes.GetDataSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac3Atom::AP4_Dac3Atom
        +---------------------------------------------------------------------*/
        // box data initialize m_Dsi (m_RawBytes -> SpecificBoxInfo)
        private Dac3Atom(uint size, byte[] payload) :
            base(AtomType.DAC3, size)
        {
            _dataRate = 0;

            // make a copy of our configuration bytes
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            _rawBytes.SetData(payload, payload_size);

            // sanity check
            if (payload_size < 3) {
                _streamInfo = new StreamInfo();
                return;
            }

            // parse the payload
            _dataRate = (uint)((payload[0]<<5)|(payload[1]>>3));
            _streamInfo.fscod         = (uint)((payload[0]>>6) & 0x03);
            _streamInfo.bsid          = (uint)((payload[0]>>1) & 0x1F);
            _streamInfo.bsmod         = (uint)(((payload[0]<<2) | (payload[1]>>6)) & 0x07);
            _streamInfo.acmod         = (uint)((payload[1]>>3) & 0x07);
            _streamInfo.lfeon         = (uint)((payload[1]>>2) & 0x01);
            _streamInfo.bit_rate_code = (uint)(((payload[1]<<3) | (payload[2]>>5)) & 0x1F);

            uint[] bit_rate_table = new uint[19] {
                32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 448, 512, 576, 640
            };
            if (_streamInfo.bit_rate_code < bit_rate_table.Length) {
                _dataRate = bit_rate_table[_streamInfo.bit_rate_code];
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac3Atom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return stream.Write(_rawBytes.GetData(), _rawBytes.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac3Atom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("data_rate", _dataRate);
            inspector.AddField("fscod", _streamInfo.fscod);
            inspector.AddField("bsid", _streamInfo.bsid);
            inspector.AddField("bsmod", _streamInfo.bsmod);
            inspector.AddField("acmod", _streamInfo.acmod);
            inspector.AddField("lfeon", _streamInfo.lfeon);

            return Ap4Result.SUCCESS;
        }

        public override Atom Clone() { return new Dac3Atom(_size32, _rawBytes.GetData()); }

        // accessors
        public DataBuffer GetRawBytes()   { return _rawBytes;   }
        public uint       GetDataRate()   { return _dataRate;   }
        public StreamInfo GetStreamInfo() { return _streamInfo; }

        // members
        private uint       _dataRate;
        private StreamInfo _streamInfo;
        private DataBuffer _rawBytes = new DataBuffer();
    }
}
