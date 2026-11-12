// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4DecoderConfigDescriptor.h, Ap4DecoderConfigDescriptor.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte DESCRIPTOR_TAG_DECODER_CONFIG = 0x04;
    }

    /*----------------------------------------------------------------------
    |   AP4_DecoderConfigDescriptor
    +---------------------------------------------------------------------*/
    public class DecoderConfigDescriptor : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_DecoderConfigDescriptor::AP4_DecoderConfigDescriptor
        +---------------------------------------------------------------------*/
        public DecoderConfigDescriptor(byte stream_type,
                                       byte oti,
                                       uint buffer_size,
                                       uint max_bitrate,
                                       uint avg_bitrate,
                                       DecoderSpecificInfoDescriptor dsi) :
            base(Ap4.DESCRIPTOR_TAG_DECODER_CONFIG, 2, 13)
        {
            _streamType           = stream_type;
            _objectTypeIndication = oti;
            _upStream             = false;
            _bufferSize           = buffer_size;
            _maxBitrate           = max_bitrate;
            _averageBitrate       = avg_bitrate;
            if (dsi != null) {
                _subDescriptors.Add(dsi);
                _payloadSize += dsi.GetSize();
                _headerSize = MinHeaderSize(_payloadSize);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_DecoderConfigDescriptor::AP4_DecoderConfigDescriptor
        +---------------------------------------------------------------------*/
        public DecoderConfigDescriptor(ByteStream stream, uint header_size, uint payload_size) :
            base(Ap4.DESCRIPTOR_TAG_DECODER_CONFIG,
                 header_size,
                 payload_size)
        {
            // read descriptor fields
            // PORT NOTE: when upstream returns early here its members are left
            // uninitialized; in the port they are zero/false.
            if (payload_size < 13) return;
            stream.ReadUI08(out _objectTypeIndication);
            byte bits;
            stream.ReadUI08(out bits);
            _streamType = (byte)((bits>>2)&0x3F);
            _upStream   = (bits&2) != 0 ? true:false;
            stream.ReadUI24(out _bufferSize);
            stream.ReadUI32(out _maxBitrate);
            stream.ReadUI32(out _averageBitrate);
            payload_size -= 13;

            // read other descriptors
            ulong offset;
            stream.Tell(out offset);
            SubStream substream = new SubStream(stream, offset, payload_size);
            Descriptor descriptor = null;
            while (DescriptorFactory.CreateDescriptorFromStream(substream,
                                                                out descriptor)
                   == Ap4Result.SUCCESS) {
                _subDescriptors.Add(descriptor);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_DecoderConfigDescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            stream.WriteUI08(_objectTypeIndication);
            byte bits = (byte)((_streamType<<2) | (_upStream? 2 : 0) | 1);
            stream.WriteUI08(bits);
            stream.WriteUI24(_bufferSize);
            stream.WriteUI32(_maxBitrate);
            stream.WriteUI32(_averageBitrate);

            _subDescriptors.Apply(new DescriptorListWriter(stream));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DecoderConfigDescriptor::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            inspector.StartDescriptor("DecoderConfig", GetHeaderSize(), GetSize());
            inspector.AddField("stream_type", _streamType);
            inspector.AddField("object_type", _objectTypeIndication);
            inspector.AddField("up_stream", _upStream ? 1 : 0);
            inspector.AddField("buffer_size", _bufferSize);
            inspector.AddField("max_bitrate", _maxBitrate);
            inspector.AddField("avg_bitrate", _averageBitrate);

            // inspect children
            _subDescriptors.Apply(new DescriptorListInspector(inspector));

            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DecoderConfigDescriptor::GetDecoderSpecificInfoDescriptor
        +---------------------------------------------------------------------*/
        public virtual DecoderSpecificInfoDescriptor GetDecoderSpecificInfoDescriptor()
        {
            // find the decoder specific info
            Descriptor  descriptor = null;
            Ap4Result result =
                _subDescriptors.Find(new DescriptorFinder(Ap4.DESCRIPTOR_TAG_DECODER_SPECIFIC_INFO),
                                     out descriptor);

            // return it
            if (result.Succeeded()) {
                return descriptor as DecoderSpecificInfoDescriptor;
            } else {
                return null;
            }
        }

        // accessors
        public virtual byte GetObjectTypeIndication() {
            return _objectTypeIndication;
        }
        public virtual byte GetStreamType() { return _streamType; }
        public virtual uint GetBufferSize() { return _bufferSize; }
        public virtual uint GetMaxBitrate() { return _maxBitrate; }
        public virtual uint GetAvgBitrate() { return _averageBitrate; }

        // members
        private byte             _streamType;
        private byte             _objectTypeIndication;
        private bool             _upStream;
        private uint             _bufferSize;
        private uint             _maxBitrate;
        private uint             _averageBitrate;
        private List<Descriptor> _subDescriptors = new List<Descriptor>();
    }
}
