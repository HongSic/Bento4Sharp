// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4EsDescriptor.h, Ap4EsDescriptor.cpp
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
        public const byte DESCRIPTOR_TAG_ES        = 0x03;
        public const byte DESCRIPTOR_TAG_ES_ID_INC = 0x0E;
        public const byte DESCRIPTOR_TAG_ES_ID_REF = 0x0F;

        public const int ES_DESCRIPTOR_FLAG_STREAM_DEPENDENCY = 1;
        public const int ES_DESCRIPTOR_FLAG_URL               = 2;
        public const int ES_DESCRIPTOR_FLAG_OCR_STREAM        = 4;
    }

    /*----------------------------------------------------------------------
    |   AP4_EsDescriptor
    +---------------------------------------------------------------------*/
    public class EsDescriptor : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_EsDescriptor::AP4_EsDescriptor
        +---------------------------------------------------------------------*/
        public EsDescriptor(ushort es_id) :
            base(Ap4.DESCRIPTOR_TAG_ES, 2, 2+1)
        {
            _esId           = es_id;
            _ocrEsId        = 0;
            _flags          = 0;
            _streamPriority = 0;
            _dependsOn      = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_EsDescriptor::AP4_EsDescriptor
        +---------------------------------------------------------------------*/
        public EsDescriptor(ByteStream stream,
                            uint       header_size,
                            uint       payload_size) :
            base(Ap4.DESCRIPTOR_TAG_ES, header_size, payload_size)
        {
            // read descriptor fields
            // PORT NOTE: when upstream returns early in this constructor the
            // members not yet read are left uninitialized; in the port they are zero.
            if (payload_size < 3) return;
            stream.ReadUI16(out _esId);
            byte bits;
            stream.ReadUI08(out bits);
            payload_size -= 3;
            _flags = (uint)((bits>>5)&7);
            _streamPriority = (byte)(bits&0x1F);
            if ((_flags & Ap4.ES_DESCRIPTOR_FLAG_STREAM_DEPENDENCY) != 0) {
                if (payload_size < 2) return;
                stream.ReadUI16(out _dependsOn);
                payload_size -= 2;
            }  else {
                _dependsOn = 0;
            }
            if ((_flags & Ap4.ES_DESCRIPTOR_FLAG_URL) != 0) {
                byte url_length;
                if (payload_size < 1) return;
                stream.ReadUI08(out url_length);
                --payload_size;
                if (url_length != 0) {
                    if (payload_size < url_length) return;
                    byte[] url = new byte[url_length+1];
                    stream.Read(url, url_length);
                    url[url_length] = 0;
                    _url = Ap4Utils.StringFromCString(url, 0, url_length);
                    payload_size -= url_length;
                }
            }
            if ((_flags & Ap4.ES_DESCRIPTOR_FLAG_OCR_STREAM) != 0) {
                if (payload_size < 2) return;
                stream.ReadUI16(out _ocrEsId);
                payload_size -= 2;
            } else {
                _ocrEsId = 0;
            }

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
        |   AP4_EsDescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // es id
            result = stream.WriteUI16(_esId);
            if (result.Failed()) return result;

            // flags and other bits
            byte bits = (byte)(_streamPriority | (byte)(_flags<<5));
            result = stream.WriteUI08(bits);
            if (result.Failed()) return result;

            // optional fields
            if ((_flags & Ap4.ES_DESCRIPTOR_FLAG_STREAM_DEPENDENCY) != 0) {
                result = stream.WriteUI16(_dependsOn);
                if (result.Failed()) return result;
            }
            if ((_flags & Ap4.ES_DESCRIPTOR_FLAG_URL) != 0) {
                result = stream.WriteUI08((byte)Ap4Utils.StringLength(_url));
                if (result.Failed()) return result;
                result = stream.WriteString(_url);
                if (result.Failed()) return result;
                result = stream.WriteUI08(0);
                if (result.Failed()) return result;
            }
            if ((_flags & Ap4.ES_DESCRIPTOR_FLAG_OCR_STREAM) != 0) {
                result = stream.WriteUI16(_ocrEsId);
                if (result.Failed()) return result;
            }

            // write the sub descriptors
            _subDescriptors.Apply(new DescriptorListWriter(stream));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EsDescriptor::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            inspector.StartDescriptor("ESDescriptor", GetHeaderSize(), GetSize());
            inspector.AddField("es_id", _esId);
            inspector.AddField("stream_priority", _streamPriority);

            // inspect children
            _subDescriptors.Apply(new DescriptorListInspector(inspector));

            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EsDescriptor::AddSubDescriptor
        +---------------------------------------------------------------------*/
        public virtual Ap4Result AddSubDescriptor(Descriptor descriptor)
        {
            _subDescriptors.Add(descriptor);
            _payloadSize += descriptor.GetSize();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EsDescriptor::GetDecoderConfigDescriptor
        +---------------------------------------------------------------------*/
        public virtual DecoderConfigDescriptor GetDecoderConfigDescriptor()
        {
            // find the decoder config descriptor
            Descriptor  descriptor = null;
            Ap4Result result =
                _subDescriptors.Find(new DescriptorFinder(Ap4.DESCRIPTOR_TAG_DECODER_CONFIG),
                                     out descriptor);

            // return it
            if (result.Succeeded()) {
                return descriptor as DecoderConfigDescriptor;
            } else {
                return null;
            }
        }

        // members
        private ushort           _esId;
        private ushort           _ocrEsId;
        private uint             _flags;
        private byte             _streamPriority;
        private ushort           _dependsOn;
        private string           _url = "";
        private List<Descriptor> _subDescriptors = new List<Descriptor>();
    }

    /*----------------------------------------------------------------------
    |   AP4_EsIdIncDescriptor
    +---------------------------------------------------------------------*/
    public class EsIdIncDescriptor : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_EsIdIncDescriptor::AP4_EsIdIncDescriptor
        +---------------------------------------------------------------------*/
        public EsIdIncDescriptor(uint track_id) :
            base(Ap4.DESCRIPTOR_TAG_ES_ID_INC, 2, 4)
        {
            _trackId = track_id;
        }

        /*----------------------------------------------------------------------
        |   AP4_EsIdIncDescriptor::AP4_EsIdIncDescriptor
        +---------------------------------------------------------------------*/
        public EsIdIncDescriptor(ByteStream stream,
                                 uint       header_size,
                                 uint       payload_size) :
            base(Ap4.DESCRIPTOR_TAG_ES_ID_INC, header_size, payload_size)
        {
            _trackId = 0;

            // read the track id
            stream.ReadUI32(out _trackId);
        }

        /*----------------------------------------------------------------------
        |   AP4_EsIdIncescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // track id
            return stream.WriteUI32(_trackId);
        }

        /*----------------------------------------------------------------------
        |   AP4_EsIdIncDescriptor::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            inspector.StartDescriptor("ES_ID_Inc", GetHeaderSize(), GetSize());
            inspector.AddField("track_id", _trackId);
            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint GetTrackId() { return _trackId; }

        // members
        private uint _trackId;
    }

    /*----------------------------------------------------------------------
    |   AP4_EsIdRefDescriptor
    +---------------------------------------------------------------------*/
    public class EsIdRefDescriptor : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_EsIdRefDescriptor::AP4_EsIdRefDescriptor
        +---------------------------------------------------------------------*/
        public EsIdRefDescriptor(ushort ref_index) :
            base(Ap4.DESCRIPTOR_TAG_ES_ID_REF, 2, 2)
        {
            _refIndex = ref_index;
        }

        /*----------------------------------------------------------------------
        |   AP4_EsIdRefDescriptor::AP4_EsIdRefDescriptor
        +---------------------------------------------------------------------*/
        public EsIdRefDescriptor(ByteStream stream,
                                 uint       header_size,
                                 uint       payload_size) :
            base(Ap4.DESCRIPTOR_TAG_ES_ID_REF, header_size, payload_size)
        {
            _refIndex = 0;

            // read the ref index
            stream.ReadUI16(out _refIndex);
        }

        /*----------------------------------------------------------------------
        |   AP4_EsIdRefDescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // ref index
            return stream.WriteUI16(_refIndex);
        }

        /*----------------------------------------------------------------------
        |   AP4_EsIdRefDescriptor::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            inspector.StartDescriptor("ES_ID_Ref", GetHeaderSize(), GetSize());
            inspector.AddField("ref_index", _refIndex);
            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        // accessors
        public ushort GetRefIndex() { return _refIndex; }

        // members
        private ushort _refIndex;
    }
}
