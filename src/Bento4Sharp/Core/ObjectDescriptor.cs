// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4ObjectDescriptor.h, Ap4ObjectDescriptor.cpp
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
        public const byte DESCRIPTOR_TAG_OD      = 0x01;
        public const byte DESCRIPTOR_TAG_IOD     = 0x02;
        public const byte DESCRIPTOR_TAG_MP4_OD  = 0x11;
        public const byte DESCRIPTOR_TAG_MP4_IOD = 0x10;
    }

    /*----------------------------------------------------------------------
    |   AP4_ObjectDescriptor
    +---------------------------------------------------------------------*/
    public class ObjectDescriptor : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_ObjectDescriptor::AP4_ObjectDescriptor
        +---------------------------------------------------------------------*/
        protected ObjectDescriptor(byte tag,
                                   uint header_size,
                                   uint payload_size) :
            base(tag, header_size, payload_size)
        {
            _objectDescriptorId = 0;
            _urlFlag            = false;
        }

        /*----------------------------------------------------------------------
        |   AP4_ObjectDescriptor::AP4_ObjectDescriptor
        +---------------------------------------------------------------------*/
        public ObjectDescriptor(byte tag, ushort id) :
            base(tag, 3, 2)
        {
            _objectDescriptorId = id;
            _urlFlag            = false;
        }

        /*----------------------------------------------------------------------
        |   AP4_ObjectDescriptor::AP4_ObjectDescriptor
        +---------------------------------------------------------------------*/
        public ObjectDescriptor(ByteStream stream,
                                byte       tag,
                                uint       header_size,
                                uint       payload_size) :
            base(tag, header_size, payload_size)
        {
            // read descriptor fields
            // PORT NOTE: upstream leaves m_ObjectDescriptorId and m_UrlFlag
            // uninitialized when it returns early; in the port they are 0/false.
            ushort bits;
            if (payload_size < 2) return;
            stream.ReadUI16(out bits);
            payload_size -= 2;
            _objectDescriptorId = (ushort)(bits>>6);
            _urlFlag = ((bits&(1<<5))!=0);

            if (_urlFlag) {
                byte url_length;
                if (payload_size < 1) return;
                stream.ReadUI08(out url_length);
                --payload_size;
                byte[] url = new byte[256];
                if (payload_size < url_length) return;
                stream.Read(url, url_length);
                payload_size -= url_length;
                url[url_length] = 0;
                _url = Ap4Utils.StringFromCString(url, 0, url_length);
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
        |   AP4_ObjectDescriptor::FindSubDescriptor
        +---------------------------------------------------------------------*/
        public virtual Descriptor FindSubDescriptor(byte tag)
        {
            Descriptor descriptor = null;
            Ap4Result result = _subDescriptors.Find(new DescriptorFinder(tag), out descriptor);
            if (result.Failed()) return null;
            return descriptor;
        }

        /*----------------------------------------------------------------------
        |   AP4_ObjectDescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // id and flag
            ushort bits = (ushort)((_objectDescriptorId<<6)|(_urlFlag?(1<<5):0)|0x1F);
            result = stream.WriteUI16(bits);
            if (result.Failed()) return result;

            // optional url
            if (_urlFlag) {
                stream.WriteUI08((byte)Ap4Utils.StringLength(_url));
                stream.WriteString(_url);
            }

            // write the sub descriptors
            _subDescriptors.Apply(new DescriptorListWriter(stream));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ObjectDescriptor::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            inspector.StartDescriptor("ObjectDescriptor", GetHeaderSize(), GetSize());
            inspector.AddField("id", _objectDescriptorId);
            if (_urlFlag) {
                inspector.AddField("url", _url);
            }

            // inspect children
            _subDescriptors.Apply(new DescriptorListInspector(inspector));

            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ObjectDescriptor::AddSubDescriptor
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Add a sub-descriptor.
        /// Ownership of the sub-descriptor object is transfered.
        /// </summary>
        public virtual Ap4Result AddSubDescriptor(Descriptor descriptor)
        {
            _subDescriptors.Add(descriptor);
            _payloadSize += descriptor.GetSize();

            // check that the header is still large enough to encode the payload
            // length
            uint min_header_size = MinHeaderSize(_payloadSize);
            if (min_header_size > _headerSize) _headerSize = min_header_size;

            return Ap4Result.SUCCESS;
        }

        // accessors
        public ushort GetObjectDescriptorId() { return _objectDescriptorId; }
        public bool   GetUrlFlag()            { return _urlFlag; }
        public string GetUrl()                { return _url;}

        // members
        protected ushort           _objectDescriptorId;
        protected bool             _urlFlag;
        protected string           _url = "";
        protected List<Descriptor> _subDescriptors = new List<Descriptor>();
    }

    /*----------------------------------------------------------------------
    |   AP4_InitialObjectDescriptor
    +---------------------------------------------------------------------*/
    public class InitialObjectDescriptor : ObjectDescriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_InitialObjectDescriptor::AP4_InitialObjectDescriptor
        +---------------------------------------------------------------------*/
        public InitialObjectDescriptor(
            byte   tag, // should be AP4_DESCRIPTOR_TAG_IOD or AP4_DESCRIPTOR_TAG_MP4_IOD
            ushort object_descriptor_id,
            bool   include_inline_profile_level,
            byte   od_profile_level_indication,
            byte   scene_profile_level_indication,
            byte   audio_profile_level_indication,
            byte   visual_profile_level_indication,
            byte   graphics_profile_level_indication) :
            base(tag, object_descriptor_id)
        {
            _includeInlineProfileLevelFlag  = include_inline_profile_level;
            _odProfileLevelIndication       = od_profile_level_indication;
            _sceneProfileLevelIndication    = scene_profile_level_indication;
            _audioProfileLevelIndication    = audio_profile_level_indication;
            _visualProfileLevelIndication   = visual_profile_level_indication;
            _graphicsProfileLevelIndication = graphics_profile_level_indication;
            _payloadSize = 7;
        }

        /*----------------------------------------------------------------------
        |   AP4_InitialObjectDescriptor::AP4_InitialObjectDescriptor
        +---------------------------------------------------------------------*/
        public InitialObjectDescriptor(ByteStream stream,
                                       byte       tag,
                                       uint       header_size,
                                       uint       payload_size) :
            base(tag, header_size, payload_size)
        {
            _odProfileLevelIndication       = 0;
            _sceneProfileLevelIndication    = 0;
            _audioProfileLevelIndication    = 0;
            _visualProfileLevelIndication   = 0;
            _graphicsProfileLevelIndication = 0;

            // read descriptor fields
            // PORT NOTE: upstream leaves m_IncludeInlineProfileLevelFlag
            // uninitialized when it returns early; in the port it is false.
            ushort bits;
            if (payload_size < 2) return;
            stream.ReadUI16(out bits);
            payload_size -= 2;
            _objectDescriptorId = (ushort)(bits>>6);
            _urlFlag = ((bits&(1<<5))!=0);
            _includeInlineProfileLevelFlag = ((bits&(1<<4))!=0);

            if (_urlFlag) {
                byte url_length;
                if (payload_size < 1) return;
                stream.ReadUI08(out url_length);
                --payload_size;
                byte[] url = new byte[256];
                if (payload_size < url_length) return;
                stream.Read(url, url_length);
                payload_size -= url_length;
                url[url_length] = 0;
                _url = Ap4Utils.StringFromCString(url, 0, url_length);
            } else {
                if (payload_size < 5) return;
                stream.ReadUI08(out _odProfileLevelIndication);
                stream.ReadUI08(out _sceneProfileLevelIndication);
                stream.ReadUI08(out _audioProfileLevelIndication);
                stream.ReadUI08(out _visualProfileLevelIndication);
                stream.ReadUI08(out _graphicsProfileLevelIndication);
                payload_size -= 5;
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
        |   AP4_InitialObjectDescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // id and flags
            ushort bits = (ushort)((_objectDescriptorId<<6)                 |
                                   (_urlFlag?(1<<5):0)                      |
                                   (_includeInlineProfileLevelFlag?(1<<4):0)|
                                   0xF);
            result = stream.WriteUI16(bits);
            if (result.Failed()) return result;

            // optional url
            if (_urlFlag) {
                stream.WriteUI08((byte)Ap4Utils.StringLength(_url));
                stream.WriteString(_url);
            } else {
                stream.WriteUI08(_odProfileLevelIndication);
                stream.WriteUI08(_sceneProfileLevelIndication);
                stream.WriteUI08(_audioProfileLevelIndication);
                stream.WriteUI08(_visualProfileLevelIndication);
                stream.WriteUI08(_graphicsProfileLevelIndication);
            }

            // write the sub descriptors
            _subDescriptors.Apply(new DescriptorListWriter(stream));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_InitialObjectDescriptor::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            inspector.StartDescriptor("InitialObjectDescriptor", GetHeaderSize(), GetSize());
            inspector.AddField("id", _objectDescriptorId);
            if (_urlFlag) {
                inspector.AddField("url", _url);
            } else {
                inspector.AddField("include inline profile level flag",
                                   _includeInlineProfileLevelFlag ? 1 : 0,
                                   AtomInspector.FormatHint.HINT_BOOLEAN);
                inspector.AddField("OD profile level", _odProfileLevelIndication, AtomInspector.FormatHint.HINT_HEX);
                inspector.AddField("scene profile level", _sceneProfileLevelIndication, AtomInspector.FormatHint.HINT_HEX);
                inspector.AddField("audio profile level", _audioProfileLevelIndication, AtomInspector.FormatHint.HINT_HEX);
                inspector.AddField("visual profile level", _visualProfileLevelIndication, AtomInspector.FormatHint.HINT_HEX);
                inspector.AddField("graphics profile level", _graphicsProfileLevelIndication, AtomInspector.FormatHint.HINT_HEX);
            }

            // inspect children
            _subDescriptors.Apply(new DescriptorListInspector(inspector));

            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        // accessors
        public bool GetIncludeProfileLevelFlag()        { return _includeInlineProfileLevelFlag; }
        public byte GetOdProfileLevelIndication()       { return _odProfileLevelIndication; }
        public byte GetSceneProfileLevelIndication()    { return _sceneProfileLevelIndication; }
        public byte GetAudioProfileLevelIndication()    { return _audioProfileLevelIndication; }
        public byte GetVisualProfileLevelIndication()   { return _visualProfileLevelIndication; }
        public byte GetGraphicsProfileLevelIndication() { return _graphicsProfileLevelIndication; }

        // members
        private bool _includeInlineProfileLevelFlag;
        private byte _odProfileLevelIndication;
        private byte _sceneProfileLevelIndication;
        private byte _audioProfileLevelIndication;
        private byte _visualProfileLevelIndication;
        private byte _graphicsProfileLevelIndication;
    }

    /*----------------------------------------------------------------------
    |   AP4_DescriptorUpdateCommand
    +---------------------------------------------------------------------*/
    /// <summary>
    /// This class is used for ObjectDescriptorUpdateCommand and
    /// IPMP_DescriptorUpdateCommand
    /// </summary>
    public class DescriptorUpdateCommand : Command
    {
        /*----------------------------------------------------------------------
        |   AP4_DescriptorUpdateCommand::AP4_DescriptorUpdateCommand
        +---------------------------------------------------------------------*/
        public DescriptorUpdateCommand(byte tag) :
            base(tag, 2, 0)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_DescriptorUpdateCommand::AP4_DescriptorUpdateCommand
        +---------------------------------------------------------------------*/
        public DescriptorUpdateCommand(
            ByteStream stream,
            byte       tag,
            uint       header_size,
            uint       payload_size) :
            base(tag, header_size, payload_size)
        {
            // read the descriptors
            ulong offset;
            stream.Tell(out offset);
            SubStream substream = new SubStream(stream, offset,
                                                payload_size);
            Descriptor descriptor = null;
            while (DescriptorFactory.CreateDescriptorFromStream(substream, out descriptor) == Ap4Result.SUCCESS) {
                _descriptors.Add(descriptor);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_DescriptorUpdateCommand::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the descriptors
            _descriptors.Apply(new DescriptorListWriter(stream));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DescriptorUpdateCommand::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            switch (GetTag()) {
                case Ap4.COMMAND_TAG_OBJECT_DESCRIPTOR_UPDATE:
                    inspector.StartDescriptor("ObjectDescriptorUpdate", GetHeaderSize(), GetSize());
                    break;

                case Ap4.COMMAND_TAG_IPMP_DESCRIPTOR_UPDATE:
                    inspector.StartDescriptor("IPMP_DescriptorUpdate", GetHeaderSize(), GetSize());
                    break;

                default:
                    inspector.StartDescriptor("DescriptorUpdate", GetHeaderSize(), GetSize());
                    break;
            }

            // inspect children
            _descriptors.Apply(new DescriptorListInspector(inspector));

            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DescriptorUpdateCommand::AddDescriptor
        +---------------------------------------------------------------------*/
        public virtual Ap4Result AddDescriptor(Descriptor descriptor)
        {
            _descriptors.Add(descriptor);
            _payloadSize += descriptor.GetSize();

            // check that the header is still large enough to encode the payload
            // length
            uint min_header_size = MinHeaderSize(_payloadSize);
            if (min_header_size > _headerSize) _headerSize = min_header_size;

            return Ap4Result.SUCCESS;
        }

        // accessors
        public List<Descriptor> GetDescriptors() { return _descriptors; }

        // members
        protected List<Descriptor> _descriptors = new List<Descriptor>();
    }
}
