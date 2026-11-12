// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4RtpHint.h, Ap4RtpHint.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constructor size / constructor types
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint RTP_CONSTRUCTOR_SIZE = 16;

        public const byte RTP_CONSTRUCTOR_TYPE_NOOP        = 0;
        public const byte RTP_CONSTRUCTOR_TYPE_IMMEDIATE   = 1;
        public const byte RTP_CONSTRUCTOR_TYPE_SAMPLE      = 2;
        public const byte RTP_CONSTRUCTOR_TYPE_SAMPLE_DESC = 3;
    }

    /*----------------------------------------------------------------------
    |   AP4_RtpSampleData
    +---------------------------------------------------------------------*/
    public class RtpSampleData
    {
        // constructors and destructor
        /*----------------------------------------------------------------------
        |   AP4_RtpSampleData::AP4_RtpSampleData
        +---------------------------------------------------------------------*/
        public RtpSampleData(ByteStream stream, uint size)
        {
            // save the start position
            ulong start, extra_data_start;
            stream.Tell(out start);

            ushort packet_count;
            stream.ReadUI16(out packet_count);

            ushort reserved;
            stream.ReadUI16(out reserved); // later, check that reserved is 0

            // packets
            for (ushort i=0; i<packet_count; i++) {
                RtpPacket packet = new RtpPacket(stream);
                _packets.Add(packet);
            }

            // extra data
            stream.Tell(out extra_data_start);
            uint extra_data_size = unchecked(size - (uint)(extra_data_start-start));
            if (extra_data_size != 0) {
                _extraData.SetDataSize(extra_data_size);
                stream.Read(_extraData.UseData(), extra_data_size);
            }
        }

        public RtpSampleData() {}

        // methods
        /*----------------------------------------------------------------------
        |   AP4_RtpSampleData::AddPacket
        +---------------------------------------------------------------------*/
        public virtual Ap4Result AddPacket(RtpPacket packet)
        {
            _packets.Add(packet);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpSampleData::GetSize
        +---------------------------------------------------------------------*/
        public virtual uint GetSize()
        {
            // packet count and reserved
            uint result = 4;

            // packets
            // PORT NOTE: upstream assigns (`result = ...GetSize()`) instead of
            // accumulating, so the value is only the size of the last packet plus
            // the extra data. Kept as is (the only user, ToByteStream, just uses
            // it as the initial size of a growable memory stream).
            foreach (RtpPacket packet in _packets) {
                result = packet.GetSize();
            }

            // extra data
            result += _extraData.GetDataSize();

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpSampleData::ToByteStream
        +---------------------------------------------------------------------*/
        public virtual ByteStream ToByteStream()
        {
            // refresh the size
            uint size = GetSize();

            // create a memory stream
            MemoryByteStream stream = new MemoryByteStream(size);

            // write in it
            Ap4Result result = stream.WriteUI16(unchecked((ushort)_packets.Count));
            if (result.Failed()) goto bail;

            result = stream.WriteUI16(0); // reserved
            if (result.Failed()) goto bail;

            {
                foreach (RtpPacket packet in _packets) {
                    result = packet.Write(stream);
                    if (result.Failed()) goto bail;
                }
            }

            result = stream.Write(_extraData.GetData(), _extraData.GetDataSize());
            if (result.Failed()) goto bail;

            // return
            return stream;

        bail:
            return null;
        }

        // accessors
        public List<RtpPacket> GetPackets() {
            return _packets;
        }
        public DataBuffer GetExtraData() {
            return _extraData;
        }

        // members
        protected List<RtpPacket> _packets = new List<RtpPacket>();
        protected DataBuffer      _extraData = new DataBuffer();
    }

    /*----------------------------------------------------------------------
    |   AP4_RtpPacket
    +---------------------------------------------------------------------*/
    // PORT NOTE: AP4_Referenceable (AddReference/Release, m_ReferenceCount) is dropped.
    public class RtpPacket
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_RtpPacket::AP4_RtpPacket
        +---------------------------------------------------------------------*/
        public RtpPacket(ByteStream stream)
        {
            _timeStampOffset = 0;

            byte octet;

            // relative time
            uint relative_time;
            stream.ReadUI32(out relative_time);
            _relativeTime = unchecked((int)relative_time);

            // pbit and xbit
            stream.ReadUI08(out octet);
            _pBit = (octet & 0x20) != 0;
            _xBit = (octet & 0x10) != 0;

            // mbit and payload type
            stream.ReadUI08(out octet);
            _mBit = (octet & 0x80) != 0;
            _payloadType = (byte)(octet & 0x7F);

            // sequence seed
            stream.ReadUI16(out _sequenceSeed);

            // extra, bframe and repeat flags
            stream.ReadUI08(out octet);
            stream.ReadUI08(out octet); // repeat on purpose
            bool extra_flag = (octet & 0x04) != 0;

            // bframe and repeat flags
            _bFrameFlag = (octet & 0x02) != 0;
            _repeatFlag = (octet & 0x01) != 0;

            // constructor count
            ushort constructor_count;
            stream.ReadUI16(out constructor_count);

            // parse the packet extra data
            if (extra_flag) {
                // read the length
                uint extra_length;
                stream.ReadUI32(out extra_length);

                // check it
                if (extra_length < 4) return;

                // now read the entries
                extra_length -= 4;
                while (extra_length > 0) {
                    uint entry_length;
                    uint entry_tag;
                    stream.ReadUI32(out entry_length);
                    stream.ReadUI32(out entry_tag);

                    // check the entry
                    if (entry_length < 8) return;

                    // parse the single entry that's currently defined in the spec
                    if (entry_tag == RTPO && entry_length == 12) {
                        uint time_stamp_offset;
                        stream.ReadUI32(out time_stamp_offset);
                        _timeStampOffset = unchecked((int)time_stamp_offset);
                    } else {
                        // ignore it
                        ulong cur_pos;
                        stream.Tell(out cur_pos);
                        stream.Seek(cur_pos + entry_length - 8); // 8 = length + tag
                    }

                    // PORT NOTE: unsigned wrap-around when entry_length > extra_length, as upstream
                    extra_length = unchecked(extra_length - entry_length);
                }
            }

            // constructors
            for (ushort i=0; i<constructor_count; i++) {
                RtpConstructor constructor = null;
                RtpConstructorFactory.CreateConstructorFromStream(stream, out constructor);
                // PORT NOTE: as upstream, a NULL constructor is added to the list when
                // the factory fails (unknown type or read error). Upstream then
                // dereferences NULL when the list is used; the port's list is
                // List<RtpConstructor?> and its users check for null (see Write and
                // GetConstructedDataSize).
                _constructors.Add(constructor);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpPacket::AP4_RtpPacket
        +---------------------------------------------------------------------*/
        public RtpPacket(int    relative_time,
                         bool   p_bit,
                         bool   x_bit,
                         bool   m_bit,
                         byte   payload_type,
                         ushort sequence_seed,
                         int    time_stamp_offset = 0,
                         bool   bframe_flag = false,
                         bool   repeat_flag = false)
        {
            _relativeTime = relative_time;
            _pBit = p_bit;
            _xBit = x_bit;
            _mBit = m_bit;
            _payloadType = payload_type;
            _sequenceSeed = sequence_seed;
            _timeStampOffset = time_stamp_offset;
            _bFrameFlag = bframe_flag;
            _repeatFlag = repeat_flag;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_RtpPacket::Write
        +---------------------------------------------------------------------*/
        public Ap4Result Write(ByteStream stream)
        {
            // check the payload type
            if (_payloadType > 128) return Ap4Result.FAILURE;

            // now write
            Ap4Result result = stream.WriteUI32(unchecked((uint)_relativeTime));
            if (result.Failed()) return result;

            result = stream.WriteUI08((byte)(0x80 | (_pBit ? 1 : 0) << 5 | (_xBit ? 1 : 0) << 4));
            if (result.Failed()) return result;

            result = stream.WriteUI08(unchecked((byte)((_mBit ? 1 : 0) << 7 | _payloadType)));
            if (result.Failed()) return result;

            result = stream.WriteUI16(_sequenceSeed);
            if (result.Failed()) return result;

            result = stream.WriteUI08(0);
            if (result.Failed()) return result;

            // deal with extra flag
            bool extra_flag = _timeStampOffset != 0;
            result = stream.WriteUI08((byte)(0x00 | (extra_flag  ? 1 : 0) << 2
                                                  | (_bFrameFlag ? 1 : 0) << 1
                                                  | (_repeatFlag ? 1 : 0) << 0));
            if (result.Failed()) return result;


            // constructor count
            result = stream.WriteUI16(unchecked((ushort)_constructors.Count));

            // write extra data
            if (extra_flag) {
                // extra_length
                result = stream.WriteUI32(16); // 4 (extra_length) + 12 (rtpo atom)
                if (result.Failed()) return result;

                // rtpo atom
                result = stream.WriteUI32(12); // size
                if (result.Failed()) return result;
                result = stream.WriteUI32(RTPO);
                if (result.Failed()) return result;
                result = stream.WriteUI32(unchecked((uint)_timeStampOffset));
                if (result.Failed()) return result;
            }

            // constructors
            foreach (RtpConstructor constructor in _constructors) {
                // PORT NOTE: upstream dereferences NULL here (see the stream constructor)
                if (constructor == null) return Ap4Result.ERROR_INVALID_RTP_CONSTRUCTOR_TYPE;
                result = constructor.Write(stream);
                if (result.Failed()) return result;
            }
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpPacket::AddConstructor
        +---------------------------------------------------------------------*/
        public Ap4Result AddConstructor(RtpConstructor constructor)
        {
            _constructors.Add(constructor);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpPacket::GetSize
        +---------------------------------------------------------------------*/
        public uint GetSize()
        {
            uint result = (uint)(12 + ((_timeStampOffset != 0)?16:0));
            result += (uint)_constructors.Count * Ap4.RTP_CONSTRUCTOR_SIZE;
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpConstructor::GetConstructedDataSize
        +---------------------------------------------------------------------*/
        public uint GetConstructedDataSize()
        {
            // header + ssrc
            uint size = 12;

            // constructed data from constructors
            foreach (RtpConstructor  constructor in _constructors) {
                // PORT NOTE: upstream dereferences NULL here (see the stream constructor)
                if (constructor == null) continue;
                size += constructor.GetConstructedDataSize();
            }

            return size;
        }

        // Accessors
        public int GetRelativeTime() { return _relativeTime; }
        public bool GetPBit() { return _pBit; }
        public bool GetXBit() { return _xBit; }
        public bool GetMBit() { return _mBit; }
        public byte GetPayloadType() { return _payloadType; }
        public ushort GetSequenceSeed() { return _sequenceSeed; }
        public int  GetTimeStampOffset() { return _timeStampOffset; }
        public bool GetBFrameFlag() { return _bFrameFlag; }
        public bool GetRepeatFlag() { return _repeatFlag; }
        public List<RtpConstructor> GetConstructors() {
            return _constructors;
        }

        // AP4_ATOM_TYPE('r','t','p','o')
        private const uint RTPO = 0x7274706f;

        // members
        private int                   _relativeTime;
        private bool                  _pBit;
        private bool                  _xBit;
        private bool                  _mBit;
        private byte                  _payloadType;
        private ushort                _sequenceSeed;
        private int                   _timeStampOffset;
        private bool                  _bFrameFlag;
        private bool                  _repeatFlag;
        private List<RtpConstructor> _constructors = new List<RtpConstructor>();
    }

    /*----------------------------------------------------------------------
    |   AP4_RtpContructor
    +---------------------------------------------------------------------*/
    // PORT NOTE: AP4_Referenceable (AddReference/Release, m_ReferenceCount) is dropped.
    // The upstream typedef `AP4_RtpConstructor::Type` (AP4_UI08) is `byte`.
    public abstract class RtpConstructor
    {
        // constructor & destructor
        protected RtpConstructor(byte type) { _type = type; }

        // methods
        // PORT NOTE: hides object.GetType(), like Atom.GetType().
        public new byte GetType() { return _type; }

        /*----------------------------------------------------------------------
        |   AP4_RtpConstructor::Write
        +---------------------------------------------------------------------*/
        public Ap4Result Write(ByteStream stream)
        {
            Ap4Result result = stream.WriteUI08(_type);
            if (result.Failed()) return result;

            return DoWrite(stream);
        }

        public abstract uint GetConstructedDataSize();

        // methods
        protected abstract Ap4Result DoWrite(ByteStream stream);

        // members
        protected byte _type;
    }

    /*----------------------------------------------------------------------
    |   AP4_NoopRtpConstructor
    +---------------------------------------------------------------------*/
    public class NoopRtpConstructor : RtpConstructor
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_NoopRtpConstructor::AP4_NoopRtpConstructor
        +---------------------------------------------------------------------*/
        public NoopRtpConstructor(ByteStream stream) :
            base(Ap4.RTP_CONSTRUCTOR_TYPE_NOOP)
        {
            ulong cur_offset;
            stream.Tell(out cur_offset);
            stream.Seek(cur_offset+15);
        }

        public NoopRtpConstructor() : base(Ap4.RTP_CONSTRUCTOR_TYPE_NOOP) {}

        // methods
        public override uint GetConstructedDataSize() { return 0; }

        /*----------------------------------------------------------------------
        |   AP4_NoopRtpConstructor::DoWrite
        +---------------------------------------------------------------------*/
        protected override Ap4Result DoWrite(ByteStream stream)
        {
            byte[] pad = new byte[15];
            return stream.Write(pad, (uint)pad.Length);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_ImmediateRtpConstructor
    +---------------------------------------------------------------------*/
    public class ImmediateRtpConstructor : RtpConstructor
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_ImmediateRtpConstructor::AP4_ImmediateRtpConstructor
        +---------------------------------------------------------------------*/
        public ImmediateRtpConstructor(ByteStream stream) :
            base(Ap4.RTP_CONSTRUCTOR_TYPE_IMMEDIATE)
        {
            _data = new DataBuffer();

            ulong cur_offset;
            stream.Tell(out cur_offset);

            // data
            byte data_size;
            stream.ReadUI08(out data_size);
            _data.SetDataSize(data_size);
            stream.Read(_data.UseData(), data_size);

            // reposition the stream
            stream.Seek(cur_offset+15);
        }

        /*----------------------------------------------------------------------
        |   AP4_ImmediateRtpConstructor::AP4_ImmediateRtpConstructor
        +---------------------------------------------------------------------*/
        public ImmediateRtpConstructor(DataBuffer data) :
            base(Ap4.RTP_CONSTRUCTOR_TYPE_IMMEDIATE)
        {
            _data = new DataBuffer(data);
        }

        // accessors
        public DataBuffer GetData() { return _data; }

        // methods
        public override uint GetConstructedDataSize() { return _data.GetDataSize(); }

        /*----------------------------------------------------------------------
        |   AP4_ImmediateRtpConstructor::DoWrite
        +---------------------------------------------------------------------*/
        protected override Ap4Result DoWrite(ByteStream stream)
        {
            // first check that the data is not too large
            if (_data.GetDataSize() > 14) return Ap4Result.FAILURE;

            // now write
            Ap4Result result = stream.WriteUI08(unchecked((byte)_data.GetDataSize()));
            if (result.Failed()) return result;

            result = stream.Write(_data.GetData(), _data.GetDataSize());
            if (result.Failed()) return result;

            // pad
            // PORT NOTE: upstream writes an *uninitialized* stack array here
            // (AP4_Byte pad[14], never cleared), so the padding bytes of a file
            // written by the C++ library are indeterminate; the port writes zeros.
            byte[] pad = new byte[14];
            return stream.Write(pad, (uint)pad.Length-_data.GetDataSize());
        }

        // members
        protected DataBuffer _data;
    }

    /*----------------------------------------------------------------------
    |   AP4_SampleRtpConstructor
    +---------------------------------------------------------------------*/
    public class SampleRtpConstructor : RtpConstructor
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_SampleRtpConstructor::AP4_SampleRtpConstructor
        +---------------------------------------------------------------------*/
        public SampleRtpConstructor(ByteStream stream) :
            base(Ap4.RTP_CONSTRUCTOR_TYPE_SAMPLE)
        {
            // offset
            ulong cur_offset;
            stream.Tell(out cur_offset);

            // data
            stream.ReadUI08(out _trackRefIndex);
            stream.ReadUI16(out _length);
            stream.ReadUI32(out _sampleNum);
            stream.ReadUI32(out _sampleOffset);

            // reposition the stream
            stream.Seek(cur_offset+15);
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleRtpConstructor::AP4_SampleRtpConstructor
        +---------------------------------------------------------------------*/
        public SampleRtpConstructor(byte   track_ref_index,
                                    ushort length,
                                    uint   sample_num,
                                    uint   sample_offset) :
            base(Ap4.RTP_CONSTRUCTOR_TYPE_SAMPLE)
        {
            _trackRefIndex = track_ref_index;
            _length = length;
            _sampleNum = sample_num;
            _sampleOffset = sample_offset;
        }

        // accessors
        public byte GetTrackRefIndex() { return _trackRefIndex; }
        public ushort GetLength() { return _length; }
        public uint GetSampleNum() { return _sampleNum; }
        public uint GetSampleOffset() { return _sampleOffset; }

        // methods
        public override uint GetConstructedDataSize() { return _length; }

        /*----------------------------------------------------------------------
        |   AP4_SampleRtpConstructor::DoWrite
        +---------------------------------------------------------------------*/
        protected override Ap4Result DoWrite(ByteStream stream)
        {
            Ap4Result result = stream.WriteUI08(_trackRefIndex);
            if (result.Failed()) return result;

            result = stream.WriteUI16(_length);
            if (result.Failed()) return result;

            result = stream.WriteUI32(_sampleNum);
            if (result.Failed()) return result;

            result = stream.WriteUI32(_sampleOffset);
            if (result.Failed()) return result;

            result = stream.WriteUI16(1); // bytes per block
            if (result.Failed()) return result;

            return stream.WriteUI16(1); // samples per block
        }

        // members
        protected byte   _trackRefIndex;
        protected ushort _length;
        protected uint   _sampleNum;
        protected uint   _sampleOffset;
    }

    /*----------------------------------------------------------------------
    |   AP4_SampleDescRtpConstructor
    +---------------------------------------------------------------------*/
    public class SampleDescRtpConstructor : RtpConstructor
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_SampleDescRtpConstructor::AP4_SampleDescRtpConstructor
        +---------------------------------------------------------------------*/
        public SampleDescRtpConstructor(ByteStream stream) :
            base(Ap4.RTP_CONSTRUCTOR_TYPE_SAMPLE_DESC)
        {
            // offset
            ulong cur_offset;
            stream.Tell(out cur_offset);

            // data
            stream.ReadUI08(out _trackRefIndex);
            stream.ReadUI16(out _length);
            stream.ReadUI32(out _sampleDescIndex);
            stream.ReadUI32(out _sampleDescOffset);

            // reposition the stream
            stream.Seek(cur_offset+15);
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleDescRtpConstructor::AP4_SampleDescRtpConstructor
        +---------------------------------------------------------------------*/
        public SampleDescRtpConstructor(byte   track_ref_index,
                                        ushort length,
                                        uint   sample_desc_index,
                                        uint   sample_desc_offset) :
            base(Ap4.RTP_CONSTRUCTOR_TYPE_SAMPLE_DESC)
        {
            _trackRefIndex = track_ref_index;
            _length = length;
            _sampleDescIndex = sample_desc_index;
            _sampleDescOffset = sample_desc_offset;
        }

        // accessors
        public byte GetTrackRefIndex() { return _trackRefIndex; }
        public ushort GetLength() { return _length; }
        public uint GetSampleDescIndex() { return _sampleDescIndex; }
        public uint GetSampleDescOffset() { return _sampleDescOffset; }

        // methods
        public override uint GetConstructedDataSize() { return _length; }

        /*----------------------------------------------------------------------
        |   AP4_SampleDescRtpConstructor::DoWrite
        +---------------------------------------------------------------------*/
        protected override Ap4Result DoWrite(ByteStream stream)
        {
            Ap4Result result = stream.WriteUI08(_trackRefIndex);
            if (result.Failed()) return result;

            result = stream.WriteUI16(_length);
            if (result.Failed()) return result;

            result = stream.WriteUI32(_sampleDescIndex);
            if (result.Failed()) return result;

            result = stream.WriteUI32(_sampleDescOffset);
            if (result.Failed()) return result;

            return stream.WriteUI32(0); // reserved
        }

        // members
        protected byte   _trackRefIndex;
        protected ushort _length;
        protected uint   _sampleDescIndex;
        protected uint   _sampleDescOffset;
    }

    /*----------------------------------------------------------------------
    |   AP4_RtpConstructorFactory
    +---------------------------------------------------------------------*/
    public static class RtpConstructorFactory
    {
        /*----------------------------------------------------------------------
        |   AP4_RtpConstructorFactory::CreateConstructorFromStream
        +---------------------------------------------------------------------*/
        public static Ap4Result CreateConstructorFromStream(ByteStream          stream,
                                                            out RtpConstructor  constructor)
        {
            // PORT NOTE: `constructor` is an in/out reference upstream, left
            // untouched on failure; every caller initializes it to NULL.
            constructor = null;

            // read the first byte (type)
            byte type;
            Ap4Result result = stream.ReadUI08(out type);
            if (result.Failed()) return result;

            // now create the right constructor
            switch(type) {
                case Ap4.RTP_CONSTRUCTOR_TYPE_NOOP:
                    constructor = new NoopRtpConstructor(stream);
                    break;
                case Ap4.RTP_CONSTRUCTOR_TYPE_IMMEDIATE:
                    constructor = new ImmediateRtpConstructor(stream);
                    break;
                case Ap4.RTP_CONSTRUCTOR_TYPE_SAMPLE:
                    constructor = new SampleRtpConstructor(stream);
                    break;
                case Ap4.RTP_CONSTRUCTOR_TYPE_SAMPLE_DESC:
                    constructor = new SampleDescRtpConstructor(stream);
                    break;
                default:
                    return Ap4Result.ERROR_INVALID_RTP_CONSTRUCTOR_TYPE;
            }

            return Ap4Result.SUCCESS;
        }
    }
}
