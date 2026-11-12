// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SampleEntry.h, Ap4SampleEntry.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    // PORT NOTE (applies to the whole file):
    // - upstream declares ReadFields/WriteFields/InspectFields/GetFieldsSize as
    //   protected in the sample entry classes; WriteFields and InspectFields
    //   override public methods of Atom and C# cannot reduce the visibility of
    //   an override, so those two stay public.
    // - the stream constructors call the virtual Read() (which calls the virtual
    //   ReadFields() and GetFieldsSize()) from the constructor. In C++ such a
    //   call dispatches to the class under construction, in C# to the most
    //   derived class. This is equivalent here because no class overrides those
    //   methods below the class whose constructor calls Read().

    /*----------------------------------------------------------------------
    |   AP4_SampleEntry
    +---------------------------------------------------------------------*/
    public class SampleEntry : ContainerAtom
    {
        // methods
        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::AP4_SampleEntry
        +---------------------------------------------------------------------*/
        public SampleEntry(uint format, AtomParent details = null) :
            base(format)
        {
            _dataReferenceIndex = 1;
            _reserved1[0] = 0;
            _reserved1[1] = 0;
            _reserved1[2] = 0;
            _reserved1[3] = 0;
            _reserved1[4] = 0;
            _reserved1[5] = 0;
            _size32 += 8;

            if (details != null) {
                details.CopyChildren(this);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::AP4_SampleEntry
        +---------------------------------------------------------------------*/
        protected SampleEntry(uint format,
                              uint size) :
            base(format, (ulong)size, false)
        {
            _dataReferenceIndex = 1;
            _reserved1[0] = 0;
            _reserved1[1] = 0;
            _reserved1[2] = 0;
            _reserved1[3] = 0;
            _reserved1[4] = 0;
            _reserved1[5] = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::AP4_SampleEntry
        +---------------------------------------------------------------------*/
        public SampleEntry(uint        format,
                           uint        size,
                           ByteStream  stream,
                           AtomFactory atom_factory) :
            base(format, (ulong)size, false)
        {
            Read(stream, atom_factory);
        }

        public ushort GetDataReferenceIndex() { return _dataReferenceIndex; }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::Clone
        +---------------------------------------------------------------------*/
        public override Atom Clone()
        {
            // PORT NOTE: upstream is `return this->AP4_Atom::Clone();`. C# cannot
            // call the implementation of a grandparent class (base.Clone() would
            // be ContainerAtom.Clone), so the body of Atom.Clone() (Core/Atom.cs)
            // is repeated here.
            Atom  clone = null;

            // check the size (refuse to clone atoms that are too large)
            ulong size = GetSize();
            if (size > Ap4.ATOM_MAX_CLONE_SIZE) return null;

            // create a memory byte stream to which we can serialize
            MemoryByteStream mbs = new MemoryByteStream((uint)GetSize());

            // serialize to memory
            if (Write(mbs).Failed()) {
                return null;
            }

            // create the clone from the serialized form
            mbs.Seek(0);
            DefaultAtomFactory atom_factory = new DefaultAtomFactory();
            atom_factory.CreateAtomFromStream(mbs, out clone);

            return clone;
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::Read
        +---------------------------------------------------------------------*/
        protected virtual void Read(ByteStream stream, AtomFactory atom_factory)
        {
            // read the fields before the children atoms
            ReadFields(stream);

            // read children atoms (ex: esds and maybe others)
            // NOTE: not all sample entries have children atoms
            uint payload_size = (uint)(GetSize()-GetHeaderSize());
            uint fields_size = GetFieldsSize();
            if (payload_size > fields_size) {
                ReadChildren(atom_factory, stream, payload_size-fields_size);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::GetFieldsSize
        +---------------------------------------------------------------------*/
        protected virtual uint GetFieldsSize()
        {
            return 8;
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::ReadFields
        +---------------------------------------------------------------------*/
        protected virtual Ap4Result ReadFields(ByteStream stream)
        {
            stream.Read(_reserved1, (uint)_reserved1.Length);
            stream.ReadUI16(out _dataReferenceIndex);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // reserved1
            result = stream.Write(_reserved1, (uint)_reserved1.Length);
            if (result.Failed()) return result;

            // data reference index
            result = stream.WriteUI16(_dataReferenceIndex);
            if (result.Failed()) return result;

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::Write
        +---------------------------------------------------------------------*/
        public override Ap4Result Write(ByteStream stream)
        {
            Ap4Result result;

            // write the header
            result = WriteHeader(stream);
            if (result.Failed()) return result;

            // write the fields
            result = WriteFields(stream);
            if (result.Failed()) return result;

            // write the children atoms
            return _children.Apply(new AtomListWriter(stream));
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("data_reference_index", _dataReferenceIndex);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            // inspect the header
            InspectHeader(inspector);

            // inspect the fields
            InspectFields(inspector);

            // inspect children
            _children.Apply(new AtomListInspector(inspector));

            // finish
            inspector.EndAtom();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::OnChildChanged
        +---------------------------------------------------------------------*/
        public override void OnChildChanged(Atom child)
        {
            // recompute our size
            ulong size = GetHeaderSize()+GetFieldsSize();
            AtomSizeAdder adder = new AtomSizeAdder(size);
            _children.Apply(adder);
            size = adder.GetSize();
            _size32 = (uint)size;

            // update our parent
            if (_parent != null) _parent.OnChildChanged(this);
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public virtual SampleDescription ToSampleDescription()
        {
            return new SampleDescription(SampleDescription.Type.TYPE_UNKNOWN, _type, this);
        }

        // members
        protected byte[] _reserved1 = new byte[6]; // = 0
        protected ushort _dataReferenceIndex;
    }

    /*----------------------------------------------------------------------
    |   AP4_UnknownSampleEntry
    +---------------------------------------------------------------------*/
    public class UnknownSampleEntry : SampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleEntry::AP4_UnknownSampleEntry
        +---------------------------------------------------------------------*/
        public UnknownSampleEntry(uint       type,
                                  uint       size,
                                  ByteStream stream) :
            base(type, size)
        {
            if (size > Ap4.ATOM_HEADER_SIZE+base.GetFieldsSize()) {
                _payload.SetDataSize(size-(Ap4.ATOM_HEADER_SIZE+base.GetFieldsSize()));
                ReadFields(stream);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleEntry::AP4_UnknownSampleEntry
        +---------------------------------------------------------------------*/
        public UnknownSampleEntry(uint       type,
                                  DataBuffer payload) :
            base(type)
        {
            _payload = new DataBuffer(payload);
            _size32 += payload.GetDataSize();
        }

        // AP4_Atom methods
        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleEntry::Clone
        +---------------------------------------------------------------------*/
        public override Atom Clone()
        {
            return new UnknownSampleEntry(_type, _payload);
        }

        // AP4_SampleEntry methods
        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            return new UnknownSampleDescription(this);
        }

        // accessors
        public DataBuffer GetPayload() { return _payload; }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleEntry::GetFieldsSize
        +---------------------------------------------------------------------*/
        protected override uint GetFieldsSize()
        {
            return base.GetFieldsSize()+_payload.GetDataSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleEntry::ReadFields
        +---------------------------------------------------------------------*/
        protected override Ap4Result ReadFields(ByteStream stream)
        {
            // sample entry
            Ap4Result result = base.ReadFields(stream);
            if (result.Failed()) return result;

            // read the payload
            return stream.Read(_payload.UseData(), _payload.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleEntry::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // write the fields of the base class
            result = base.WriteFields(stream);
            if (result.Failed()) return result;

            // write the payload
            return stream.Write(_payload.GetData(), _payload.GetDataSize());
        }

        // members
        protected DataBuffer _payload = new DataBuffer();
    }

    /*----------------------------------------------------------------------
    |   AP4_MpegSystemSampleEntry
    +---------------------------------------------------------------------*/
    public class MpegSystemSampleEntry : SampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_MpegSystemSampleEntry::AP4_MpegSystemSampleEntry
        +---------------------------------------------------------------------*/
        public MpegSystemSampleEntry(
            uint          type,
            EsDescriptor  descriptor) :
            base(type)
        {
            if (descriptor != null) AddChild(new EsdsAtom(descriptor));
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegSystemSampleEntry::AP4_MpegSystemSampleEntry
        +---------------------------------------------------------------------*/
        public MpegSystemSampleEntry(
            uint        type,
            uint        size,
            ByteStream  stream,
            AtomFactory atom_factory) :
            base(type, size, stream, atom_factory)
        {
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MpegSystemSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            return new MpegSystemSampleDescription(
                GetChild(AtomType.ESDS) as EsdsAtom);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_Mp4sSampleEntry
    +---------------------------------------------------------------------*/
    public class Mp4sSampleEntry : MpegSystemSampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_Mp4sSampleEntry::AP4_Mp4sSampleEntry
        +---------------------------------------------------------------------*/
        public Mp4sSampleEntry(EsDescriptor descriptor) :
            base(AtomType.MP4S, descriptor)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_Mp4sSampleEntry::AP4_Mp4sSampleEntry
        +---------------------------------------------------------------------*/
        public Mp4sSampleEntry(uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(AtomType.MP4S, size, stream, atom_factory)
        {
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_Mp4sSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // create a sample description
            return new MpegSystemSampleDescription(
                GetChild(AtomType.ESDS) as EsdsAtom);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_AudioSampleEntry
    +---------------------------------------------------------------------*/
    public class AudioSampleEntry : SampleEntry
    {
        // methods
        /*----------------------------------------------------------------------
        |   AP4_AudioSampleEntry::AP4_AudioSampleEntry
        +---------------------------------------------------------------------*/
        public AudioSampleEntry(uint   format,
                                uint   sample_rate,
                                ushort sample_size,
                                ushort channel_count) :
            base(format)
        {
            _qtVersion = 0;
            _qtRevision = 0;
            _qtVendor = 0;
            _channelCount = channel_count;
            _sampleSize = sample_size;
            _qtCompressionId = 0;
            _qtPacketSize = 0;
            _sampleRate = sample_rate;
            _qtV1SamplesPerPacket = 0;
            _qtV1BytesPerPacket = 0;
            _qtV1BytesPerFrame = 0;
            _qtV1BytesPerSample = 0;
            _qtV2StructSize = 0;
            _qtV2SampleRate64 = 0.0;
            _qtV2ChannelCount = 0;
            _qtV2Reserved = 0;
            _qtV2BitsPerChannel = 0;
            _qtV2FormatSpecificFlags = 0;
            _qtV2BytesPerAudioPacket = 0;
            _qtV2LPCMFramesPerAudioPacket = 0;
            _size32 += 20;
        }

        /*----------------------------------------------------------------------
        |   AP4_AudioSampleEntry::AP4_AudioSampleEntry
        +---------------------------------------------------------------------*/
        public AudioSampleEntry(uint        format,
                                uint        size,
                                ByteStream  stream,
                                AtomFactory atom_factory) :
            base(format, size)
        {
            Read(stream, atom_factory);
        }

        /*----------------------------------------------------------------------
        |   AP4_AudioSampleEntry::GetFieldsSize
        +---------------------------------------------------------------------*/
        protected override uint GetFieldsSize()
        {
            uint size = base.GetFieldsSize()+20;
            if (_qtVersion == 1) {
                size += 16;
            } else if (_qtVersion == 2) {
                size += 36+_qtV2Extension.GetDataSize();
            }

            return size;
        }

        // accessors
        /*----------------------------------------------------------------------
        |   AP4_AudioSampleEntry::GetSampleRate
        +---------------------------------------------------------------------*/
        public virtual uint GetSampleRate()
        {
            if (_qtVersion == 2) {
                return unchecked((uint)(_qtV2SampleRate64));
            } else {
                return _sampleRate>>16;
            }
        }

        public ushort GetSampleSize() { return _sampleSize; }

        /*----------------------------------------------------------------------
        |   AP4_AudioSampleEntry::GetChannelCount
        +---------------------------------------------------------------------*/
        public ushort GetChannelCount()
        {
            if (_qtVersion == 2) {
                return (ushort)_qtV2ChannelCount;
            } else {
                return _channelCount;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AudioSampleEntry::ReadFields
        +---------------------------------------------------------------------*/
        protected override Ap4Result ReadFields(ByteStream stream)
        {
            // sample entry
            Ap4Result result = base.ReadFields(stream);
            if (result < 0) return result;

            // read the fields of this class
            stream.ReadUI16(out _qtVersion);
            stream.ReadUI16(out _qtRevision);
            stream.ReadUI32(out _qtVendor);
            stream.ReadUI16(out _channelCount);
            stream.ReadUI16(out _sampleSize);
            stream.ReadUI16(out _qtCompressionId);
            stream.ReadUI16(out _qtPacketSize);
            stream.ReadUI32(out _sampleRate);

            // if this is a QT V1 entry, read the extension
            if (_qtVersion == 1) {
                stream.ReadUI32(out _qtV1SamplesPerPacket);
                stream.ReadUI32(out _qtV1BytesPerPacket);
                stream.ReadUI32(out _qtV1BytesPerFrame);
                stream.ReadUI32(out _qtV1BytesPerSample);
            } else if (_qtVersion == 2) {
                stream.ReadUI32(out _qtV2StructSize);
                stream.ReadDouble(out _qtV2SampleRate64);
                stream.ReadUI32(out _qtV2ChannelCount);
                stream.ReadUI32(out _qtV2Reserved);
                stream.ReadUI32(out _qtV2BitsPerChannel);
                stream.ReadUI32(out _qtV2FormatSpecificFlags);
                stream.ReadUI32(out _qtV2BytesPerAudioPacket);
                stream.ReadUI32(out _qtV2LPCMFramesPerAudioPacket);
                if (_qtV2StructSize > 72) {
                    uint ext_size = _qtV2StructSize-72;
                    _qtV2Extension.SetDataSize(ext_size);
                    stream.Read(_qtV2Extension.UseData(), ext_size);
                }
                _qtV1SamplesPerPacket =
                _qtV1BytesPerPacket   =
                _qtV1BytesPerFrame    =
                _qtV1BytesPerSample   = 0;
            } else {
                _qtV1SamplesPerPacket         = 0;
                _qtV1BytesPerPacket           = 0;
                _qtV1BytesPerFrame            = 0;
                _qtV1BytesPerSample           = 0;
                _qtV2StructSize               = 0;
                _qtV2SampleRate64             = 0.0;
                _qtV2ChannelCount             = 0;
                _qtV2Reserved                 = 0;
                _qtV2BitsPerChannel           = 0;
                _qtV2FormatSpecificFlags      = 0;
                _qtV2BytesPerAudioPacket      = 0;
                _qtV2LPCMFramesPerAudioPacket = 0;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AudioSampleEntry::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // write the fields of the base class
            result = base.WriteFields(stream);
            if (result.Failed()) return result;

            // QT version
            result = stream.WriteUI16(_qtVersion);
            if (result.Failed()) return result;

            // QT revision
            result = stream.WriteUI16(_qtRevision);
            if (result.Failed()) return result;

            // QT vendor
            result = stream.WriteUI32(_qtVendor);
            if (result.Failed()) return result;

            // channel count
            result = stream.WriteUI16(_channelCount);
            if (result.Failed()) return result;

            // sample size
            result = stream.WriteUI16(_sampleSize);
            if (result.Failed()) return result;

            // QT compression ID
            result = stream.WriteUI16(_qtCompressionId);
            if (result.Failed()) return result;

            // QT packet size
            result = stream.WriteUI16(_qtPacketSize);
            if (result.Failed()) return result;

            // sample rate
            result = stream.WriteUI32(_sampleRate);
            if (result.Failed()) return result;

            if (_qtVersion == 1) {
                result = stream.WriteUI32(_qtV1SamplesPerPacket);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_qtV1BytesPerPacket);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_qtV1BytesPerFrame);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_qtV1BytesPerSample);
                if (result.Failed()) return result;
            } else if (_qtVersion == 2) {
                stream.WriteUI32(_qtV2StructSize);
                stream.WriteDouble(_qtV2SampleRate64);
                stream.WriteUI32(_qtV2ChannelCount);
                stream.WriteUI32(_qtV2Reserved);
                stream.WriteUI32(_qtV2BitsPerChannel);
                stream.WriteUI32(_qtV2FormatSpecificFlags);
                stream.WriteUI32(_qtV2BytesPerAudioPacket);
                stream.WriteUI32(_qtV2LPCMFramesPerAudioPacket);
                if (_qtV2Extension.GetDataSize() != 0) {
                    stream.Write(_qtV2Extension.GetData(),
                                 _qtV2Extension.GetDataSize());
                }
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_AudioSampleEntry::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            // dump the fields from the base class
            base.InspectFields(inspector);

            // fields
            inspector.AddField("channel_count", GetChannelCount());
            inspector.AddField("sample_size", GetSampleSize());
            inspector.AddField("sample_rate", GetSampleRate());
            if (_qtVersion != 0) {
                inspector.AddField("qt_version", _qtVersion);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AudioSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // create a sample description
            return new GenericAudioSampleDescription(
                _type,
                GetSampleRate(),
                GetSampleSize(),
                GetChannelCount(),
                this);
        }

        // members
        protected ushort _qtVersion;       // 0, 1 or 2
        protected ushort _qtRevision;      // 0
        protected uint   _qtVendor;        // 0
        protected ushort _channelCount;
        protected ushort _sampleSize;
        protected ushort _qtCompressionId; // 0 or -2
        protected ushort _qtPacketSize;    // always 0
        protected uint   _sampleRate;      // 16.16 fixed point

        protected uint _qtV1SamplesPerPacket;
        protected uint _qtV1BytesPerPacket;
        protected uint _qtV1BytesPerFrame;
        protected uint _qtV1BytesPerSample;

        protected uint   _qtV2StructSize;
        protected double _qtV2SampleRate64;
        protected uint   _qtV2ChannelCount;
        protected uint   _qtV2Reserved;
        protected uint   _qtV2BitsPerChannel;
        protected uint   _qtV2FormatSpecificFlags;
        protected uint   _qtV2BytesPerAudioPacket;
        protected uint   _qtV2LPCMFramesPerAudioPacket;
        protected DataBuffer _qtV2Extension = new DataBuffer();
    }

    /*----------------------------------------------------------------------
    |   AP4_MpegAudioSampleEntry
    +---------------------------------------------------------------------*/
    public class MpegAudioSampleEntry : AudioSampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_MpegAudioSampleEntry::AP4_MpegAudioSampleEntry
        +---------------------------------------------------------------------*/
        public MpegAudioSampleEntry(
            uint          type,
            uint          sample_rate,
            ushort        sample_size,
            ushort        channel_count,
            EsDescriptor  descriptor) :
            base(type, sample_rate, sample_size, channel_count)
        {
            if (descriptor != null) AddChild(new EsdsAtom(descriptor));
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegAudioSampleEntry::AP4_MpegAudioSampleEntry
        +---------------------------------------------------------------------*/
        public MpegAudioSampleEntry(
            uint        type,
            uint        size,
            ByteStream  stream,
            AtomFactory atom_factory) :
            base(type, size, stream, atom_factory)
        {
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MpegAudioSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // find the esds atom
            EsdsAtom esds = GetChild(AtomType.ESDS) as EsdsAtom;
            if (esds == null) {
                // check if this is a quicktime style sample description
                if (_qtVersion > 0) {
                    esds = FindChild("wave/esds") as EsdsAtom;
                }
            }

            // create a sample description
            return new MpegAudioSampleDescription(GetSampleRate(),
                                                  GetSampleSize(),
                                                  GetChannelCount(),
                                                  esds);
        }
    }

    /*----------------------------------------------------------------------
     |   AP4_Ac3SampleEntry
     +---------------------------------------------------------------------*/
    public class Ac3SampleEntry : AudioSampleEntry
    {
        /*----------------------------------------------------------------------
         |   AP4_Ac3SampleEntry::AP4_Ac3SampleEntry
         +---------------------------------------------------------------------*/
        public Ac3SampleEntry(uint        format,
                              uint        sample_rate,
                              ushort      sample_size,
                              ushort      channel_count,
                              AtomParent  details) :
            base(format, sample_rate, sample_size, channel_count)
        {
            if (details != null) {
                Dac3Atom dac3 = details.GetChild(AtomType.DAC3) as Dac3Atom;
                if (dac3 != null) {
                    AddChild(new Dac3Atom(dac3));
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac3SampleEntry::AP4_Ac3SampleEntry
        +---------------------------------------------------------------------*/
        public Ac3SampleEntry(uint        type,
                              uint        size,
                              ByteStream  stream,
                              AtomFactory atom_factory) :
            base(type, size, stream, atom_factory)
        {
        }

        // inherited from AP4_SampleEntry
        /*----------------------------------------------------------------------
         |   AP4_Ac3SampleEntry::ToSampleDescription
         +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // find the dac3 atom
            Dac3Atom dac3 = GetChild(AtomType.DAC3) as Dac3Atom;
            if (dac3 == null) {
                return null;
            }else{
                return new Ac3SampleDescription(GetSampleRate(),
                                                GetSampleSize(),
                                                GetChannelCount(),
                                                dac3);
            }
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_Eac3SampleEntry
    +---------------------------------------------------------------------*/
    public class Eac3SampleEntry : AudioSampleEntry
    {
        /*----------------------------------------------------------------------
        |   AP4_Eac3SampleEntry::AP4_Eac3SampleEntry
        +---------------------------------------------------------------------*/
        public Eac3SampleEntry(uint        format,
                               uint        sample_rate,
                               ushort      sample_size,
                               ushort      channel_count,
                               AtomParent  details) :
            base(format, sample_rate, sample_size, channel_count)
        {
            if (details != null) {
                Dec3Atom dec3 = details.GetChild(AtomType.DEC3) as Dec3Atom;
                if (dec3 != null) {
                    AddChild(new Dec3Atom(dec3));
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Eac3SampleEntry::AP4_Eac3SampleEntry
        +---------------------------------------------------------------------*/
        public Eac3SampleEntry(uint        type,
                               uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(type, size, stream, atom_factory)
        {
        }

        // inherited from AP4_SampleEntry
        /*----------------------------------------------------------------------
        |   AP4_Eac3SampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // find the dec3 atom
            Dec3Atom dec3 = GetChild(AtomType.DEC3) as Dec3Atom;
            if (dec3 == null) {
                // shall never happen
                return new Eac3SampleDescription();
            }else{
                 return new Eac3SampleDescription(GetSampleRate(),
                                                  GetSampleSize(),
                                                  GetChannelCount(),
                                                  dec3);
            }
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_Ac4SampleEntry
    +---------------------------------------------------------------------*/
    public class Ac4SampleEntry : AudioSampleEntry
    {
        /*----------------------------------------------------------------------
        |   AP4_Ac4SampleEntry::AP4_Ac4SampleEntry
        +---------------------------------------------------------------------*/
        public Ac4SampleEntry(uint        format,
                              uint        sample_rate,
                              ushort      sample_size,
                              ushort      channel_count,
                              AtomParent  details) :
            base(format, sample_rate, sample_size, channel_count)
        {
            if (details != null) {
                Dac4Atom dac4 = details.GetChild(AtomType.DAC4) as Dac4Atom;
                if (dac4 != null) {
                    // PORT NOTE: upstream would crash on a NULL clone
                    Atom dac4_clone = dac4.Clone();
                    if (dac4_clone != null) AddChild(dac4_clone);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac4SampleEntry::AP4_Ac4SampleEntry
        +---------------------------------------------------------------------*/
        public Ac4SampleEntry(uint        type,
                              uint        size,
                              ByteStream  stream,
                              AtomFactory atom_factory) :
            base(type, size, stream, atom_factory)
        {
        }

        // inherited from AP4_SampleEntry
        /*----------------------------------------------------------------------
        |   AP4_Ac4SampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // find the dac4 atom
            Dac4Atom dac4 = GetChild(AtomType.DAC4) as Dac4Atom;
            if (dac4 == null) {
                return null;
            } else{
                 return new Ac4SampleDescription(GetSampleRate(),
                                                 GetSampleSize(),
                                                 GetChannelCount(),
                                                 dac4);
            }
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_MlpSampleEntry
    +---------------------------------------------------------------------*/
    public class MlpSampleEntry : AudioSampleEntry
    {
        /*----------------------------------------------------------------------
        |   AP4_MlpSampleEntry::AP4_MlpSampleEntry
        +---------------------------------------------------------------------*/
        public MlpSampleEntry(uint        format,
                              uint        sample_rate,
                              ushort      sample_size,
                              ushort      channel_count,
                              AtomParent  details) :
            base(format, sample_rate, sample_size, channel_count)
        {
            if (details != null) {
                DmlpAtom dmlp = details.GetChild(AtomType.DMLP) as DmlpAtom;
                if (dmlp != null) {
                    AddChild(new DmlpAtom(dmlp));
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MlpSampleEntry::AP4_MlpSampleEntry
        +---------------------------------------------------------------------*/
        public MlpSampleEntry(uint        type,
                              uint        size,
                              ByteStream  stream,
                              AtomFactory atom_factory) :
            base(type, size, stream, atom_factory)
        {
        }

        // inherited from AP4_SampleEntry
        /*----------------------------------------------------------------------
        |   AP4_MlpSampleEntry::GetSampleRate
        +---------------------------------------------------------------------*/
        public override uint GetSampleRate()
        {
            return _sampleRate;
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac4SampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // find the dmlp atom
            DmlpAtom dmlp = GetChild(AtomType.DMLP) as DmlpAtom;
            if (dmlp == null) {
                return null;
            }

            return new TrueHdSampleDescription(GetSampleRate(),
                                               GetSampleSize(),
                                               GetChannelCount(),
                                               dmlp);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_Mp4aSampleEntry
    +---------------------------------------------------------------------*/
    public class Mp4aSampleEntry : MpegAudioSampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_Mp4aSampleEntry::AP4_Mp4aSampleEntry
        +---------------------------------------------------------------------*/
        public Mp4aSampleEntry(uint          sample_rate,
                               ushort        sample_size,
                               ushort        channel_count,
                               EsDescriptor  descriptor) :
            base(AtomType.MP4A,
                 sample_rate,
                 sample_size,
                 channel_count,
                 descriptor)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_Mp4aSampleEntry::AP4_Mp4aSampleEntry
        +---------------------------------------------------------------------*/
        public Mp4aSampleEntry(uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(AtomType.MP4A, size, stream, atom_factory)
        {
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_VisualSampleEntry
    +---------------------------------------------------------------------*/
    public class VisualSampleEntry : SampleEntry
    {
        // methods
        /*----------------------------------------------------------------------
        |   AP4_VisualSampleEntry::AP4_VisualSampleEntry
        +---------------------------------------------------------------------*/
        public VisualSampleEntry(
            uint        format,
            ushort      width,
            ushort      height,
            ushort      depth,
            string      compressor_name,
            AtomParent  details = null) :
            base(format, details)
        {
            _predefined1 = 0;
            _reserved2 = 0;
            _width = width;
            _height = height;
            _horizResolution = 0x00480000;
            _vertResolution = 0x00480000;
            _reserved3 = 0;
            _frameCount = 1;
            _compressorName = compressor_name ?? "";
            _depth = depth;
            _predefined3 = 0xFFFF;
            System.Array.Clear(_predefined2, 0, _predefined2.Length);
            _size32 += 70;
        }

        /*----------------------------------------------------------------------
        |   AP4_VisualSampleEntry::AP4_VisualSampleEntry
        +---------------------------------------------------------------------*/
        public VisualSampleEntry(uint        format,
                                 uint        size,
                                 ByteStream  stream,
                                 AtomFactory atom_factory) :
            base(format, size)
        {
            Read(stream, atom_factory);
        }

        // accessors
        public ushort GetWidth()          { return _width;  }
        public ushort GetHeight()         { return _height; }
        public uint   GetHorizResolution(){ return _horizResolution;  }
        public uint   GetVertResolution() { return _vertResolution; }
        public ushort GetDepth()          { return _depth;  }
        public string GetCompressorName() { return _compressorName; }

        /*----------------------------------------------------------------------
        |   AP4_VisualSampleEntry::GetFieldsSize
        +---------------------------------------------------------------------*/
        protected override uint GetFieldsSize()
        {
            return base.GetFieldsSize()+70;
        }

        /*----------------------------------------------------------------------
        |   AP4_VisualSampleEntry::ReadFields
        +---------------------------------------------------------------------*/
        protected override Ap4Result ReadFields(ByteStream stream)
        {
            // sample entry
            Ap4Result result = base.ReadFields(stream);
            if (result < 0) return result;

            // read fields from this class
            stream.ReadUI16(out _predefined1);
            stream.ReadUI16(out _reserved2);
            stream.Read(_predefined2, (uint)_predefined2.Length);
            stream.ReadUI16(out _width);
            stream.ReadUI16(out _height);
            stream.ReadUI32(out _horizResolution);
            stream.ReadUI32(out _vertResolution);
            stream.ReadUI32(out _reserved3);
            stream.ReadUI16(out _frameCount);

            byte[] compressor_name = new byte[33];
            compressor_name[32] = 0;
            stream.Read(compressor_name, 32);
            byte name_length = compressor_name[0];
            if (name_length < 32) {
                compressor_name[name_length+1] = 0; // force null termination
                _compressorName = Ap4Utils.StringFromCString(compressor_name, 1, 32);
            }

            stream.ReadUI16(out _depth);
            stream.ReadUI16(out _predefined3);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_VisualSampleEntry::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // write the fields of the base class
            result = base.WriteFields(stream);
            if (result.Failed()) return result;

            // predefined1
            result = stream.WriteUI16(_predefined1);
            if (result.Failed()) return result;

            // reserved2
            result = stream.WriteUI16(_reserved2);
            if (result.Failed()) return result;

            // predefined2
            result = stream.Write(_predefined2, (uint)_predefined2.Length);
            if (result.Failed()) return result;

            // width
            result = stream.WriteUI16(_width);
            if (result.Failed()) return result;

            // height
            result = stream.WriteUI16(_height);
            if (result.Failed()) return result;

            // horizontal resolution
            result = stream.WriteUI32(_horizResolution);
            if (result.Failed()) return result;

            // vertical resolution
            result = stream.WriteUI32(_vertResolution);
            if (result.Failed()) return result;

            // reserved3
            result = stream.WriteUI32(_reserved3);
            if (result.Failed()) return result;

            // frame count
            result = stream.WriteUI16(_frameCount);
            if (result.Failed()) return result;

            // compressor name
            byte[] compressor_name = new byte[32];
            byte[] compressor_name_chars = Ap4Utils.StringToCStringBytes(_compressorName);
            uint name_length = Ap4Utils.StringLength(_compressorName);
            if (name_length > 31) name_length = 31;
            compressor_name[0] = (byte)name_length;
            for (uint i=0; i<name_length; i++) {
                compressor_name[i+1] = compressor_name_chars[i];
            }
            for (uint i=name_length+1; i<32; i++) {
                compressor_name[i] = 0;
            }
            result = stream.Write(compressor_name, 32);
            if (result.Failed()) return result;

            // depth
            result = stream.WriteUI16(_depth);
            if (result.Failed()) return result;

            // predefined3
            result = stream.WriteUI16(_predefined3);
            if (result.Failed()) return result;

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_VisualSampleEntry::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            // dump the fields of the base class
            base.InspectFields(inspector);

            // fields
            inspector.AddField("width", _width);
            inspector.AddField("height", _height);
            inspector.AddField("compressor", _compressorName);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_VisualSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // create a sample description
            return new GenericVideoSampleDescription(
                _type,
                _width,
                _height,
                _depth,
                _compressorName,
                this);
        }

        //members
        protected ushort _predefined1;     // = 0
        protected ushort _reserved2;       // = 0
        protected byte[] _predefined2 = new byte[12]; // = 0
        protected ushort _width;
        protected ushort _height;
        protected uint   _horizResolution; // = 0x00480000 (72 dpi)
        protected uint   _vertResolution;  // = 0x00480000 (72 dpi)
        protected uint   _reserved3;       // = 0
        protected ushort _frameCount;      // = 1
        protected string _compressorName = "";
        protected ushort _depth;           // = 0x0018
        protected ushort _predefined3;     // = 0xFFFF
    }

    /*----------------------------------------------------------------------
    |   AP4_MpegVideoSampleEntry
    +---------------------------------------------------------------------*/
    public class MpegVideoSampleEntry : VisualSampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_MpegVideoSampleEntry::AP4_MpegVideoSampleEntry
        +---------------------------------------------------------------------*/
        public MpegVideoSampleEntry(
            uint          type,
            ushort        width,
            ushort        height,
            ushort        depth,
            string        compressor_name,
            EsDescriptor  descriptor) :
            base(type,
                 width,
                 height,
                 depth,
                 compressor_name)
        {
            if (descriptor != null) AddChild(new EsdsAtom(descriptor));
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegVideoSampleEntry::AP4_MpegVideoSampleEntry
        +---------------------------------------------------------------------*/
        public MpegVideoSampleEntry(
            uint        type,
            uint        size,
            ByteStream  stream,
            AtomFactory atom_factory) :
            base(type, size, stream, atom_factory)
        {
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MpegVideoSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // create a sample description
            return new MpegVideoSampleDescription(
                _width,
                _height,
                _depth,
                _compressorName,
                GetChild(AtomType.ESDS) as EsdsAtom);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_Mp4vSampleEntry
    +---------------------------------------------------------------------*/
    public class Mp4vSampleEntry : MpegVideoSampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_Mp4vSampleEntry::AP4_Mp4vSampleEntry
        +---------------------------------------------------------------------*/
        public Mp4vSampleEntry(ushort        width,
                               ushort        height,
                               ushort        depth,
                               string        compressor_name,
                               EsDescriptor  descriptor) :
            base(AtomType.MP4V,
                 width,
                 height,
                 depth,
                 compressor_name,
                 descriptor)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_Mp4vSampleEntry::AP4_Mp4aSampleEntry
        +---------------------------------------------------------------------*/
        public Mp4vSampleEntry(uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(AtomType.MP4V, size, stream, atom_factory)
        {
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_AvcSampleEntry
    +---------------------------------------------------------------------*/
    public class AvcSampleEntry : VisualSampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_AvcSampleEntry::AP4_AvcSSampleEntry
        +---------------------------------------------------------------------*/
        public AvcSampleEntry(uint        format, // avc1, avc2, avc3, avc4
                              ushort      width,
                              ushort      height,
                              ushort      depth,
                              string      compressor_name,
                              AtomParent  details) :
            base(format,
                 width,
                 height,
                 depth,
                 compressor_name,
                 details)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_AvcSampleEntry::AP4_AvcSampleEntry
        +---------------------------------------------------------------------*/
        public AvcSampleEntry(uint        format, // avc1, avc2, avc3, avc4
                              uint        size,
                              ByteStream  stream,
                              AtomFactory atom_factory) :
            base(format, size, stream, atom_factory)
        {
        }

        // inherited from AP4_SampleEntry
        /*----------------------------------------------------------------------
        |   AP4_AvcSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            return new AvcSampleDescription(
                _type,
                _width,
                _height,
                _depth,
                _compressorName,
                this);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_HevcSampleEntry
    +---------------------------------------------------------------------*/
    public class HevcSampleEntry : VisualSampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_HevcSampleEntry::AP4_HevcSampleEntry
        +---------------------------------------------------------------------*/
        public HevcSampleEntry(uint        format, // hvc1, hev1
                               ushort      width,
                               ushort      height,
                               ushort      depth,
                               string      compressor_name,
                               AtomParent  details) :
            base(format,
                 width,
                 height,
                 depth,
                 compressor_name,
                 details)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_HevcSampleEntry::AP4_HevcSampleEntry
        +---------------------------------------------------------------------*/
        public HevcSampleEntry(uint        format, // hvc1, hev1
                               uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(format, size, stream, atom_factory)
        {
        }

        // inherited from AP4_SampleEntry
        /*----------------------------------------------------------------------
        |   AP4_HevcSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            return new HevcSampleDescription(
                _type,
                _width,
                _height,
                _depth,
                _compressorName,
                this);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_Av1SampleEntry
    +---------------------------------------------------------------------*/
    public class Av1SampleEntry : VisualSampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_Av1SampleEntry::AP4_Av1SampleEntry
        +---------------------------------------------------------------------*/
        public Av1SampleEntry(uint        format, // av01
                              ushort      width,
                              ushort      height,
                              ushort      depth,
                              string      compressor_name,
                              AtomParent  details) :
            base(format,
                 width,
                 height,
                 depth,
                 compressor_name,
                 details)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_Av1SampleEntry::AP4_Av1SampleEntry
        +---------------------------------------------------------------------*/
        public Av1SampleEntry(uint        format, // av01
                              uint        size,
                              ByteStream  stream,
                              AtomFactory atom_factory) :
            base(format, size, stream, atom_factory)
        {
        }

        // inherited from AP4_SampleEntry
        /*----------------------------------------------------------------------
        |   AP4_Av1SampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            return new Av1SampleDescription(
                _type,
                _width,
                _height,
                _depth,
                _compressorName,
                this);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_RtpHintSampleEntry
    +---------------------------------------------------------------------*/
    public class RtpHintSampleEntry : SampleEntry
    {
        // methods
        /*----------------------------------------------------------------------
        |   AP4_RtpHintSampleEntry::AP4_RtpHintSampleEntry
        +---------------------------------------------------------------------*/
        public RtpHintSampleEntry(ushort hint_track_version,
                                  ushort highest_compatible_version,
                                  uint   max_packet_size,
                                  uint   timescale):
            base(AtomType.RTP_)
        {
            _hintTrackVersion = hint_track_version;
            _highestCompatibleVersion = highest_compatible_version;
            _maxPacketSize = max_packet_size;

            // build an atom for timescale
            AddChild(new TimsAtom(timescale));
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpHintSampleEntry::AP4_RtpHintSampleEntry
        +---------------------------------------------------------------------*/
        public RtpHintSampleEntry(uint        size,
                                  ByteStream  stream,
                                  AtomFactory atom_factory):
            base(AtomType.RTP_, size)
        {
            Read(stream, atom_factory);
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpHintSampleEntry::GetFieldsSize
        +---------------------------------------------------------------------*/
        protected override uint GetFieldsSize()
        {
            return base.GetFieldsSize()+8;
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpHintSampleEntry::ReadFields
        +---------------------------------------------------------------------*/
        protected override Ap4Result ReadFields(ByteStream stream)
        {
            // sample entry
            Ap4Result result = base.ReadFields(stream);
            if (result < 0) return result;

            // data
            result = stream.ReadUI16(out _hintTrackVersion);
            if (result.Failed()) return result;
            result = stream.ReadUI16(out _highestCompatibleVersion);
            if (result.Failed()) return result;
            result = stream.ReadUI32(out _maxPacketSize);
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpHintSampleEntry::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // sample entry
            Ap4Result result = base.WriteFields(stream);
            if (result.Failed()) return result;

            // data
            result = stream.WriteUI16(_hintTrackVersion);
            if (result.Failed()) return result;
            result = stream.WriteUI16(_highestCompatibleVersion);
            if (result.Failed()) return result;
            result = stream.WriteUI32(_maxPacketSize);
            if (result.Failed()) return result;

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpHintSampleEntry::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            // sample entry
            base.InspectFields(inspector);

            // fields
            inspector.AddField("hint_track_version", _hintTrackVersion);
            inspector.AddField("highest_compatible_version", _highestCompatibleVersion);
            inspector.AddField("max_packet_size", _maxPacketSize);

            return Ap4Result.SUCCESS;
        }

        // members
        protected ushort _hintTrackVersion;
        protected ushort _highestCompatibleVersion;
        protected uint   _maxPacketSize;
    }

    /*----------------------------------------------------------------------
    |   AP4_SubtitleSampleEntry
    +---------------------------------------------------------------------*/
    public class SubtitleSampleEntry : SampleEntry
    {
        // methods
        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleEntry::AP4_SubtitleSampleEntry
        +---------------------------------------------------------------------*/
        public SubtitleSampleEntry(
            uint    format,
            string  namespce,
            string schema_location,
            string image_mime_type) :
            base(format)
        {
            _namespace = namespce ?? "";
            _schemaLocation = schema_location ?? "";
            _imageMimeType = image_mime_type ?? "";
            SetSize(_size32+Ap4Utils.StringLength(_namespace)+1+Ap4Utils.StringLength(_schemaLocation)+1+Ap4Utils.StringLength(_imageMimeType)+1);
        }

        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleEntry::AP4_SubtitleSampleEntry
        +---------------------------------------------------------------------*/
        public SubtitleSampleEntry(uint        format,
                                   uint        size,
                                   ByteStream  stream,
                                   AtomFactory atom_factory) :
            base(format, size)
        {
            Read(stream, atom_factory);
        }

        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleEntry::GetFieldsSize
        +---------------------------------------------------------------------*/
        protected override uint GetFieldsSize()
        {
            return base.GetFieldsSize() +
                3                                         +
                Ap4Utils.StringLength(_namespace)         +
                Ap4Utils.StringLength(_schemaLocation)    +
                Ap4Utils.StringLength(_imageMimeType);
        }

        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleEntry::ReadFields
        +---------------------------------------------------------------------*/
        protected override Ap4Result ReadFields(ByteStream stream)
        {
            // sample entry
            Ap4Result result = base.ReadFields(stream);
            if (result < 0) return result;

            // read fields from this class
            result = stream.ReadNullTerminatedString(out _namespace);
            if (result.Failed()) return result;
            result = stream.ReadNullTerminatedString(out _schemaLocation);
            if (result.Failed()) return result;
            result = stream.ReadNullTerminatedString(out _imageMimeType);
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleEntry::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // write the fields of the base class
            result = base.WriteFields(stream);
            if (result.Failed()) return result;

            // write fields from this class
            result = stream.WriteString(_namespace);
            if (result.Failed()) return result;
            result = stream.WriteUI08(0);
            if (result.Failed()) return result;
            result = stream.WriteString(_schemaLocation);
            if (result.Failed()) return result;
            result = stream.WriteUI08(0);
            if (result.Failed()) return result;
            result = stream.WriteString(_imageMimeType);
            if (result.Failed()) return result;
            result = stream.WriteUI08(0);
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleEntry::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            // dump the fields of the base class
            base.InspectFields(inspector);

            // fields
            inspector.AddField("namespace",       _namespace);
            inspector.AddField("schema_location", _schemaLocation);
            inspector.AddField("image_mime_type", _imageMimeType);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // create a sample description
            return new SubtitleSampleDescription(_type,
                                                 _namespace,
                                                 _schemaLocation,
                                                 _imageMimeType);
        }

        // members
        protected string _namespace = "";
        protected string _schemaLocation = "";
        protected string _imageMimeType = "";
    }
}
