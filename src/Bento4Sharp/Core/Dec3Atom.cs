// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Dec3Atom.h, Ap4Dec3Atom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;
using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Dec3Atom
    +---------------------------------------------------------------------*/
    public class Dec3Atom : Atom
    {
        // types
        // PORT NOTE: a C# struct, so that it keeps the value semantics of the
        // upstream struct (all fields are 0 by default, like the upstream
        // default constructor).
        public struct SubStream
        {
            public uint fscod;
            public uint bsid;
            public uint bsmod;
            public uint acmod;
            public uint lfeon;
            public uint num_dep_sub;
            public uint chan_loc;
        }

        /*----------------------------------------------------------------------
        |   AP4_Dec3Atom::Create
        +---------------------------------------------------------------------*/
        public static Dec3Atom Create(uint size, ByteStream stream)
        {
            // read the raw bytes in a buffer
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            DataBuffer payload_data = new DataBuffer(payload_size);
            Ap4Result result = stream.Read(payload_data.UseData(), payload_size);
            if (result.Failed()) return null;

            // create the atom from the raw bytes
            byte[] payload = payload_data.GetData();
            return new Dec3Atom(size, payload);
        }

        /*----------------------------------------------------------------------
        |   AP4_Dec3Atom::AP4_Dec3Atom
        +---------------------------------------------------------------------*/
        public Dec3Atom(Dec3Atom other) :
            base(AtomType.DEC3, other._size32)
        {
            _dataRate = other._dataRate;
            _flagEC3ExtensionTypeA = other._flagEC3ExtensionTypeA;
            _complexityIndexTypeA = other._complexityIndexTypeA;
            _subStreams = new List<SubStream>(other._subStreams);
            _rawBytes = new DataBuffer(other._rawBytes);
        }

        /*----------------------------------------------------------------------
        |   AP4_Dec3Atom::AP4_Dec3Atom
        +---------------------------------------------------------------------*/
        public Dec3Atom() :
            base(AtomType.DEC3, Ap4.ATOM_HEADER_SIZE)
        {
            _dataRate = 0;
            _flagEC3ExtensionTypeA = 0;
            _complexityIndexTypeA = 0;
            _subStreams.Add(new SubStream());
        }

        /*----------------------------------------------------------------------
        |   AP4_Dec3Atom::AP4_Dec3Atom
        +---------------------------------------------------------------------*/
        // DSI vaiable initialize m_RawBytes (substream -> m_RawBytes)
        // PORT NOTE: upstream takes a `const SubStream*`; m_DataRate,
        // m_FlagEC3ExtensionTypeA and m_ComplexityIndexTypeA are left
        // uninitialized upstream and are 0 in the port.
        public Dec3Atom(uint au_size, SubStream substream, uint complexity_index_type_a) :
            base(AtomType.DEC3, Ap4.ATOM_HEADER_SIZE)
        {
            BitWriter bits = new BitWriter(7);          // I0 + D0 or 5.1 DD+ JOC is the most complicated stream

            uint date_rate   = au_size / 4;
            uint num_ind_sub = 0;                       // num_ind_sub is set to 0 which is ensured by E-AC-3 parser.
            bits.Write(date_rate, 13);
            bits.Write(num_ind_sub, 3);
            for (uint idx = 0; idx < num_ind_sub + 1; idx ++) {
                bits.Write(substream.fscod, 2);
                bits.Write(substream.bsid , 5);
                bits.Write(0, 1);                       // reserved
                bits.Write(0, 1);                       // asvc is always 0
                bits.Write(substream.bsmod, 3);
                bits.Write(substream.acmod, 3);
                bits.Write(substream.lfeon, 1);
                bits.Write(0, 3);                       // reserved
                bits.Write(substream.num_dep_sub, 4);
                if (substream.num_dep_sub > 0) {
                    bits.Write(substream.chan_loc, 9);
                }else{
                    bits.Write(0, 1);                   // reserved
                }
            }
            if (complexity_index_type_a != 0) {
                bits.Write(1, 8);
                bits.Write(complexity_index_type_a, 8);
            }

            _rawBytes.SetData(bits.GetData(), bits.GetBitCount() / 8);
            _size32 += _rawBytes.GetDataSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_Dec3Atom::AP4_Dec3Atom
        +---------------------------------------------------------------------*/
        // box data initialize m_Dsi (m_RawBytes -> substream)
        private Dec3Atom(uint size, byte[] payload_) :
            base(AtomType.DEC3, size)
        {
            _dataRate = 0;
            _flagEC3ExtensionTypeA = 0;
            _complexityIndexTypeA = 0;

            // PORT NOTE: upstream advances the `payload` pointer; the port keeps the
            // array and an offset (`payload`), every access is payload_[payload+n].
            int payload = 0;

            // make a copy of our configuration bytes
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            _rawBytes.SetData(payload_, payload_size);

            // sanity check
            if (payload_size < 2) return;

            // parse the payload
            _dataRate = (uint)((payload_[payload+0]<<5)|(payload_[payload+1]>>3));
            uint substream_count = (uint)(1+(payload_[payload+1]&7));
            payload      += 2;
            payload_size -= 2;
            // m_SubStreams.SetItemCount(substream_count)
            for (uint i=0; i<substream_count; i++) {
                _subStreams.Add(new SubStream());
            }
            for (uint i=0; i<substream_count; i++) {
                SubStream sub_stream = new SubStream();
                if (payload_size < 4) {
                    sub_stream.fscod       = 0;
                    sub_stream.bsid        = 0;
                    sub_stream.bsmod       = 0;
                    sub_stream.acmod       = 0;
                    sub_stream.lfeon       = 0;
                    sub_stream.num_dep_sub = 0;
                    sub_stream.chan_loc    = 0;
                    _subStreams[(int)i] = sub_stream;
                    continue;
                }
                sub_stream.fscod       = (uint)((payload_[payload+0]>>6) & 0x3);
                sub_stream.bsid        = (uint)((payload_[payload+0]>>1) & 0x1F);
                sub_stream.bsmod       = (uint)((payload_[payload+0]<<4 | payload_[payload+1]>>4) & 0x1F);
                sub_stream.acmod       = (uint)((payload_[payload+1]>>1) & 0x7);
                sub_stream.lfeon       = (uint)((payload_[payload+1]   ) & 0x1);
                sub_stream.num_dep_sub = (uint)((payload_[payload+2]>>1) & 0xF);
                if (sub_stream.num_dep_sub != 0) {
                    sub_stream.chan_loc = (uint)((payload_[payload+2]<<7 | payload_[payload+3]) & 0x1F);
                    payload      += 4;
                    payload_size -= 4;
                } else {
                    sub_stream.chan_loc = 0;
                    payload      += 3;
                    payload_size -= 3;
                }
                _subStreams[(int)i] = sub_stream;
            }
            // DD+/Atmos
            if (payload_size >= 2) {
                _flagEC3ExtensionTypeA = (uint)(payload_[payload+0] & 0x1);
                _complexityIndexTypeA  = payload_[payload+1];
                payload      += 2;
                payload_size -= 2;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Dec3Atom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return stream.Write(_rawBytes.GetData(), _rawBytes.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_Dec3Atom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("data_rate", _dataRate);
            inspector.AddField("complexity_index_type_a", _complexityIndexTypeA);
            for (int i=0; i<_subStreams.Count; i++) {
                // "[%02d]"
                string name = "[" + i.ToString("D2", CultureInfo.InvariantCulture) + "]";
                // "fscod=%d, bsid=%d, bsmod=%d, acmod=%d, lfeon=%d, num_dep_sub=%d, chan_loc=%d"
                string value =
                    "fscod="         + ((int)_subStreams[i].fscod).ToString(CultureInfo.InvariantCulture) +
                    ", bsid="        + ((int)_subStreams[i].bsid).ToString(CultureInfo.InvariantCulture) +
                    ", bsmod="       + ((int)_subStreams[i].bsmod).ToString(CultureInfo.InvariantCulture) +
                    ", acmod="       + ((int)_subStreams[i].acmod).ToString(CultureInfo.InvariantCulture) +
                    ", lfeon="       + ((int)_subStreams[i].lfeon).ToString(CultureInfo.InvariantCulture) +
                    ", num_dep_sub=" + ((int)_subStreams[i].num_dep_sub).ToString(CultureInfo.InvariantCulture) +
                    ", chan_loc="    + ((int)_subStreams[i].chan_loc).ToString(CultureInfo.InvariantCulture);
                inspector.AddField(name, value);
            }
            return Ap4Result.SUCCESS;
        }

        public override Atom Clone() { return new Dec3Atom(_size32, _rawBytes.GetData()); }

        // accessors
        public DataBuffer      GetRawBytes()   { return _rawBytes;   }
        public uint            GetDataRate()   { return _dataRate;   }
        public List<SubStream> GetSubStreams() { return _subStreams; }
        public uint            GetFlagEC3ExtensionTypeA() { return _flagEC3ExtensionTypeA; }
        public uint            GetComplexityIndexTypeA()  { return _complexityIndexTypeA; }

        // members
        private uint            _dataRate;
        private uint            _flagEC3ExtensionTypeA;
        private uint            _complexityIndexTypeA;
        private List<SubStream> _subStreams = new List<SubStream>();
        private DataBuffer      _rawBytes = new DataBuffer();
    }
}
