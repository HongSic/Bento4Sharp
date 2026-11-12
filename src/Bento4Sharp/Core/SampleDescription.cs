// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SampleDescription.h, Ap4SampleDescription.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;
using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint SAMPLE_FORMAT_MP4A     = 0x6d703461; // 'mp4a'
        public const uint SAMPLE_FORMAT_MP4V     = 0x6d703476; // 'mp4v'
        public const uint SAMPLE_FORMAT_MP4S     = 0x6d703473; // 'mp4s'
        public const uint SAMPLE_FORMAT_AVC1     = 0x61766331; // 'avc1'
        public const uint SAMPLE_FORMAT_AVC2     = 0x61766332; // 'avc2'
        public const uint SAMPLE_FORMAT_AVC3     = 0x61766333; // 'avc3'
        public const uint SAMPLE_FORMAT_AVC4     = 0x61766334; // 'avc4'
        public const uint SAMPLE_FORMAT_DVAV     = 0x64766176; // 'dvav'
        public const uint SAMPLE_FORMAT_DVA1     = 0x64766131; // 'dva1'
        public const uint SAMPLE_FORMAT_HVC1     = 0x68766331; // 'hvc1'
        public const uint SAMPLE_FORMAT_HEV1     = 0x68657631; // 'hev1'
        public const uint SAMPLE_FORMAT_DVHE     = 0x64766865; // 'dvhe'
        public const uint SAMPLE_FORMAT_DVH1     = 0x64766831; // 'dvh1'
        public const uint SAMPLE_FORMAT_AV01     = 0x61763031; // 'av01'
        public const uint SAMPLE_FORMAT_ALAC     = 0x616c6163; // 'alac'
        public const uint SAMPLE_FORMAT_OWMA     = 0x6f776d61; // 'owma'
        public const uint SAMPLE_FORMAT_OVC1     = 0x6f766331; // 'ovc1'
        public const uint SAMPLE_FORMAT_AVCP     = 0x61766370; // 'avcp'
        public const uint SAMPLE_FORMAT_DRAC     = 0x64726163; // 'drac'
        public const uint SAMPLE_FORMAT_DRA1     = 0x64726131; // 'dra1'
        public const uint SAMPLE_FORMAT_AC_3     = 0x61632d33; // 'ac-3'
        public const uint SAMPLE_FORMAT_AC_4     = 0x61632d34; // 'ac-4'
        public const uint SAMPLE_FORMAT_EC_3     = 0x65632d33; // 'ec-3'
        public const uint SAMPLE_FORMAT_MLPA     = 0x6d6c7061; // 'mlpa'
        public const uint SAMPLE_FORMAT_DTSC     = 0x64747363; // 'dtsc'
        public const uint SAMPLE_FORMAT_DTSH     = 0x64747368; // 'dtsh'
        public const uint SAMPLE_FORMAT_DTSL     = 0x6474736c; // 'dtsl'
        public const uint SAMPLE_FORMAT_DTSE     = 0x64747365; // 'dtse'
        public const uint SAMPLE_FORMAT_G726     = 0x67373236; // 'g726'
        public const uint SAMPLE_FORMAT_MJP2     = 0x6d6a7032; // 'mjp2'
        public const uint SAMPLE_FORMAT_OKSD     = 0x6f6b7364; // 'oksd'
        public const uint SAMPLE_FORMAT_RAW_     = 0x72617720; // 'raw '
        public const uint SAMPLE_FORMAT_RTP_     = 0x72747020; // 'rtp '
        public const uint SAMPLE_FORMAT_S263     = 0x73323633; // 's263'
        public const uint SAMPLE_FORMAT_SAMR     = 0x73616d72; // 'samr'
        public const uint SAMPLE_FORMAT_SAWB     = 0x73617762; // 'sawb'
        public const uint SAMPLE_FORMAT_SAWP     = 0x73617770; // 'sawp'
        public const uint SAMPLE_FORMAT_SEVC     = 0x73657663; // 'sevc'
        public const uint SAMPLE_FORMAT_SQCP     = 0x73716370; // 'sqcp'
        public const uint SAMPLE_FORMAT_SRTP     = 0x73727470; // 'srtp'
        public const uint SAMPLE_FORMAT_SSMV     = 0x73736d76; // 'ssmv'
        public const uint SAMPLE_FORMAT_TEXT     = 0x74657478; // 'tetx'
        public const uint SAMPLE_FORMAT_TWOS     = 0x74776f73; // 'twos'
        public const uint SAMPLE_FORMAT_TX3G     = 0x74783367; // 'tx3g'
        public const uint SAMPLE_FORMAT_VC_1     = 0x76632d31; // 'vc-1'
        public const uint SAMPLE_FORMAT_XML_     = 0x786d6c20; // 'xml '
        public const uint SAMPLE_FORMAT_STPP     = 0x73747070; // 'stpp'
        public const uint SAMPLE_FORMAT_FLAC     = 0x664c6143; // 'fLaC'
        public const uint SAMPLE_FORMAT_OPUS     = 0x4f707573; // 'Opus'
        public const uint SAMPLE_FORMAT_VP8      = 0x76703038; // 'vp08'
        public const uint SAMPLE_FORMAT_VP9      = 0x76703039; // 'vp09'
        public const uint SAMPLE_FORMAT_VP10     = 0x76703130; // 'vp10'
        public const uint SAMPLE_COLOR_TYPE_NCLX = 0x6e636c78; // 'nclx'

        /*----------------------------------------------------------------------
        |  AP4_GetFormatName
        +---------------------------------------------------------------------*/
        public static string GetFormatName(uint format)
        {
            switch (format) {
                case Ap4.SAMPLE_FORMAT_MP4A: return "MPEG-4 Audio";
                case Ap4.SAMPLE_FORMAT_MP4V: return "MPEG-4 Video";
                case Ap4.SAMPLE_FORMAT_MP4S: return "MPEG-4 Systems";
                case Ap4.SAMPLE_FORMAT_ALAC: return "Apple Lossless Audio";
                case Ap4.SAMPLE_FORMAT_AVC1: return "H.264";
                case Ap4.SAMPLE_FORMAT_AVC2: return "H.264";
                case Ap4.SAMPLE_FORMAT_AVC3: return "H.264";
                case Ap4.SAMPLE_FORMAT_AVC4: return "H.264";
                case Ap4.SAMPLE_FORMAT_DVAV: return "Dolby Vision (H.264)";
                case Ap4.SAMPLE_FORMAT_DVA1: return "Dolby Vision (H.264)";
                case Ap4.SAMPLE_FORMAT_HEV1: return "H.265";
                case Ap4.SAMPLE_FORMAT_HVC1: return "H.265";
                case Ap4.SAMPLE_FORMAT_DVH1: return "Dolby Vision (H.265)";
                case Ap4.SAMPLE_FORMAT_DVHE: return "Dolby Vision (H.265)";
                case Ap4.SAMPLE_FORMAT_AV01: return "AV1";
                case Ap4.SAMPLE_FORMAT_OVC1: return "VC-1";
                case Ap4.SAMPLE_FORMAT_OWMA: return "WMA";
                case Ap4.SAMPLE_FORMAT_AC_3: return "Dolby Digital (AC-3)";
                case Ap4.SAMPLE_FORMAT_EC_3: return "Dolby Digital Plus (Enhanced AC-3)";
                case Ap4.SAMPLE_FORMAT_AC_4: return "Dolby AC-4";
                case Ap4.SAMPLE_FORMAT_MLPA: return "Dolby TrueHD";
                case Ap4.SAMPLE_FORMAT_DTSC: return "DTS";
                case Ap4.SAMPLE_FORMAT_DTSH: return "DTS-HD";
                case Ap4.SAMPLE_FORMAT_DTSL: return "DTS-HD Lossless";
                case Ap4.SAMPLE_FORMAT_DTSE: return "DTS Low Bitrate";
                case Ap4.SAMPLE_FORMAT_AVCP: return "Advanced Video Coding Parameters";
                case Ap4.SAMPLE_FORMAT_DRAC: return "Dirac";
                case Ap4.SAMPLE_FORMAT_DRA1: return "DRA Audio";
                case Ap4.SAMPLE_FORMAT_G726: return "G726";
                case Ap4.SAMPLE_FORMAT_MJP2: return "Motion JPEG 2000";
                case Ap4.SAMPLE_FORMAT_OKSD: return "OMA Keys";
                case Ap4.SAMPLE_FORMAT_RAW_: return "Uncompressed Audio";
                case Ap4.SAMPLE_FORMAT_RTP_: return "RTP Hints";
                case Ap4.SAMPLE_FORMAT_S263: return "H.263";
                case Ap4.SAMPLE_FORMAT_SAMR: return "Narrowband AMR";
                case Ap4.SAMPLE_FORMAT_SAWB: return "Wideband AMR";
                case Ap4.SAMPLE_FORMAT_SAWP: return "Extended AMR";
                case Ap4.SAMPLE_FORMAT_SEVC: return "EVRC Voice";
                case Ap4.SAMPLE_FORMAT_SQCP: return "13K Voice";
                case Ap4.SAMPLE_FORMAT_SRTP: return "SRTP Hints";
                case Ap4.SAMPLE_FORMAT_SSMV: return "SMV Voice";
                case Ap4.SAMPLE_FORMAT_TEXT: return "Textual Metadata";
                case Ap4.SAMPLE_FORMAT_TWOS: return "Uncompressed 16-bit Audio";
                case Ap4.SAMPLE_FORMAT_TX3G: return "Timed Text";
                case Ap4.SAMPLE_FORMAT_VC_1: return "SMPTE VC-1";
                case Ap4.SAMPLE_FORMAT_XML_: return "XML Metadata";
                case Ap4.SAMPLE_FORMAT_STPP: return "Timed Text";
                case Ap4.SAMPLE_FORMAT_VP8: return "VP8";
                case Ap4.SAMPLE_FORMAT_VP9: return "VP9";
                case Ap4.SAMPLE_FORMAT_VP10: return "VP10";
                default: return null;
            }
        }

        // AP4_MpegSampleDescription::StreamType
        public const byte STREAM_TYPE_FORBIDDEN        = 0x00;
        public const byte STREAM_TYPE_OD               = 0x01;
        public const byte STREAM_TYPE_CR               = 0x02;
        public const byte STREAM_TYPE_BIFS             = 0x03;
        public const byte STREAM_TYPE_VISUAL           = 0x04;
        public const byte STREAM_TYPE_AUDIO            = 0x05;
        public const byte STREAM_TYPE_MPEG7            = 0x06;
        public const byte STREAM_TYPE_IPMP             = 0x07;
        public const byte STREAM_TYPE_OCI              = 0x08;
        public const byte STREAM_TYPE_MPEGJ            = 0x09;
        public const byte STREAM_TYPE_TEXT             = 0x0D;

        // AP4_MpegSampleDescription::OTI
        public const byte OTI_MPEG4_SYSTEM             = 0x01;
        public const byte OTI_MPEG4_SYSTEM_COR         = 0x02;
        public const byte OTI_MPEG4_TEXT               = 0x08;
        public const byte OTI_MPEG4_VISUAL             = 0x20;
        public const byte OTI_MPEG4_AUDIO              = 0x40;
        public const byte OTI_MPEG2_VISUAL_SIMPLE      = 0x60;
        public const byte OTI_MPEG2_VISUAL_MAIN        = 0x61;
        public const byte OTI_MPEG2_VISUAL_SNR         = 0x62;
        public const byte OTI_MPEG2_VISUAL_SPATIAL     = 0x63;
        public const byte OTI_MPEG2_VISUAL_HIGH        = 0x64;
        public const byte OTI_MPEG2_VISUAL_422         = 0x65;
        public const byte OTI_MPEG2_AAC_AUDIO_MAIN     = 0x66;
        public const byte OTI_MPEG2_AAC_AUDIO_LC       = 0x67;
        public const byte OTI_MPEG2_AAC_AUDIO_SSRP     = 0x68;
        public const byte OTI_MPEG2_PART3_AUDIO        = 0x69;
        public const byte OTI_MPEG1_VISUAL             = 0x6A;
        public const byte OTI_MPEG1_AUDIO              = 0x6B;
        public const byte OTI_JPEG                     = 0x6C;
        public const byte OTI_PNG                      = 0x6D;
        public const byte OTI_JPEG2000                 = 0x6E;
        public const byte OTI_EVRC_VOICE               = 0xA0;
        public const byte OTI_SMV_VOICE                = 0xA1;
        public const byte OTI_3GPP2_CMF                = 0xA2;
        public const byte OTI_SMPTE_VC1                = 0xA3;
        public const byte OTI_DIRAC_VIDEO              = 0xA4;
        public const byte OTI_AC3_AUDIO                = 0xA5;
        public const byte OTI_EAC3_AUDIO               = 0xA6;
        public const byte OTI_DRA_AUDIO                = 0xA7;
        public const byte OTI_G719_AUDIO               = 0xA8;
        public const byte OTI_DTS_AUDIO                = 0xA9;
        public const byte OTI_DTS_HIRES_AUDIO          = 0xAA;
        public const byte OTI_DTS_MASTER_AUDIO         = 0xAB;
        public const byte OTI_DTS_EXPRESS_AUDIO        = 0xAC;
        public const byte OTI_OPUS_AUDIO               = 0xAD;
        public const byte OTI_VP9_VIDEO                = 0xB1;
        public const byte OTI_VORBIS_AUDIO             = 0xDD; // Not standard!
        public const byte OTI_13K_VOICE                = 0xE1;

        /// <summary>AAC Main Profile</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_AAC_MAIN             = 1;
        /// <summary>AAC Low Complexity</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_AAC_LC               = 2;
        /// <summary>AAC Scalable Sample Rate</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_AAC_SSR              = 3;
        /// <summary>AAC Long Term Predictor</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_AAC_LTP              = 4;
        /// <summary>Spectral Band Replication</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_SBR                  = 5;
        /// <summary>AAC Scalable</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_AAC_SCALABLE         = 6;
        /// <summary>Twin VQ</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_TWINVQ               = 7;
        /// <summary>CELP</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_CELP                 = 8;
        /// <summary>HVXC</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_HVXC                 = 9;
        /// <summary>TTSI</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_TTSI                 = 12;
        /// <summary>Main Synthetic</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_MAIN_SYNTHETIC       = 13;
        /// <summary>WavetableSynthesis</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_WAVETABLE_SYNTHESIS  = 14;
        /// <summary>General MIDI</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_GENERAL_MIDI         = 15;
        /// <summary>Algorithmic Synthesis</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ALGORITHMIC_SYNTHESIS = 16;
        /// <summary>Error Resilient AAC Low Complexity</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LC            = 17;
        /// <summary>Error Resilient AAC Long Term Prediction</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LTP           = 19;
        /// <summary>Error Resilient AAC Scalable</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_SCALABLE      = 20;
        /// <summary>Error Resilient Twin VQ</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_TWINVQ            = 21;
        /// <summary>Error Resilient Bit Sliced Arithmetic Coding</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_BSAC              = 22;
        /// <summary>Error Resilient AAC Low Delay</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LD            = 23;
        /// <summary>Error Resilient CELP</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_CELP              = 24;
        /// <summary>Error Resilient HVXC</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_HVXC              = 25;
        /// <summary>Error Resilient HILN</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_HILN              = 26;
        /// <summary>Error Resilient Parametric</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_PARAMETRIC        = 27;
        /// <summary>SSC</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_SSC                  = 28;
        /// <summary>Parametric Stereo</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_PS                   = 29;
        /// <summary>MPEG Surround</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_MPEG_SURROUND        = 30;
        /// <summary>MPEG Layer 1</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_LAYER_1              = 32;
        /// <summary>MPEG Layer 2</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_LAYER_2              = 33;
        /// <summary>MPEG Layer 3</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_LAYER_3              = 34;
        /// <summary>DST Direct Stream Transfer</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_DST                  = 35;
        /// <summary>ALS Lossless Coding</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ALS                  = 36;
        /// <summary>SLS Scalable Lossless Coding</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_SLS                  = 37;
        /// <summary>SLS Sclable Lossless Coding Non-Core</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_SLS_NON_CORE         = 38;
        /// <summary>Error Resilient AAC ELD</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_ELD           = 39;
        /// <summary>SMR Simple</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_SMR_SIMPLE           = 40;
        /// <summary>SMR Main</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_SMR_MAIN             = 41;
        /// <summary>USAC</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_USAC                 = 42;
        /// <summary>SAOC</summary>
        public const byte MPEG4_AUDIO_OBJECT_TYPE_SAOC                 = 43;
    }

    /*----------------------------------------------------------------------
    |   AP4_SampleDescription
    +---------------------------------------------------------------------*/
    public class SampleDescription
    {
        // type constants of the sample description
        public enum Type {
            TYPE_UNKNOWN   = 0x00,
            TYPE_MPEG      = 0x01,
            TYPE_PROTECTED = 0x02,
            TYPE_SUBTITLES = 0x03,
            TYPE_AVC       = 0x04,
            TYPE_HEVC      = 0x05,
            TYPE_AV1       = 0x06,
            TYPE_AC3       = 0x07,
            TYPE_EAC3      = 0x08,
            TYPE_AC4       = 0x09,
            TYPE_TRUEHD    = 0x0A
        }

        // constructors & destructor
        /*----------------------------------------------------------------------
        |   AP4_SampleDescription::AP4_SampleDescription
        +---------------------------------------------------------------------*/
        public SampleDescription(Type        type,
                                 uint        format,
                                 AtomParent  details)
        {
            _type = type;
            _format = format;
            if (details != null) {
                List<Atom> children = details.GetChildren();
                for (int i = 0; i < children.Count; i++) {
                    Atom atom = children[i];
                    if (atom != null) {
                        Atom clone = atom.Clone();
                        if (clone != null) _details.AddChild(clone);
                    }
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleDescription::Clone
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream is `Clone(AP4_Result* result = NULL)`; the port has
        // the virtual Clone(out result) and this overload for callers that pass
        // no result pointer.
        public SampleDescription Clone()
        {
            Ap4Result unused1;
            return Clone(out unused1);
        }

        public virtual SampleDescription Clone(out Ap4Result result)
        {
            result = Ap4Result.SUCCESS;
            Atom atom = ToAtom();
            if (atom == null) {
                result = Ap4Result.FAILURE;
                return null;
            }

            // serialize the atom to a buffer
            MemoryByteStream mbs = new MemoryByteStream((uint)atom.GetSize());
            atom.Write(mbs);
            atom = null;

            // parse the buffer back to an atom
            mbs.Seek(0);
            AtomFactory factory = new AtomFactory();
            factory.PushContext(AtomType.STSD);
            Atom atom_clone = null;
            Ap4Result lresult = factory.CreateAtomFromStream(mbs, out atom_clone);
            factory.PopContext();
            result = lresult;
            if (lresult.Failed()) return null;

            // convert the atom clone to a sample description
            SampleEntry sample_entry = atom_clone as SampleEntry;
            if (sample_entry == null) {
                result = Ap4Result.ERROR_INTERNAL;
                return null;
            }

            SampleDescription clone = sample_entry.ToSampleDescription();
            if (clone == null) {
                result = Ap4Result.ERROR_INTERNAL;
            }

            return clone;
        }

        // accessors
        // PORT NOTE: hides object.GetType(), like Atom.GetType()
        public new Type   GetType()    { return _type;    }
        public uint       GetFormat()  { return _format;  }
        public AtomParent GetDetails() { return _details; }

        // info
        /*----------------------------------------------------------------------
        |   AP4_SampleDescription::GetCodecString
        +---------------------------------------------------------------------*/
        public virtual Ap4Result GetCodecString(out string codec)
        {
            // PORT NOTE: `codec.Assign(coding, 4)` always takes the 4 bytes of
            // the format, including 0 bytes (Ap4Utils.FormatFourChars would stop
            // at the first one)
            byte[] coding = new byte[4];
            Ap4Utils.BytesFromUInt32BE(coding, _format);
            codec = Ap4Utils.StringFromBytes(coding, 0, 4);

            return Ap4Result.SUCCESS;
        }

        // factories
        /*----------------------------------------------------------------------
        |   AP4_SampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public virtual Atom ToAtom()
        {
            return new SampleEntry(_format);
        }

        protected Type       _type;
        protected uint       _format;
        protected AtomParent _details = new AtomParent();
    }

    /*----------------------------------------------------------------------
    |   AP4_UnknownSampleDescription
    +---------------------------------------------------------------------*/
    public class UnknownSampleDescription : SampleDescription
    {
        // this constructor takes makes a copy of the atom passed as an argument
        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleDescription::AP4_UnknownSampleDescription
        +---------------------------------------------------------------------*/
        public UnknownSampleDescription(Atom atom) :
            base(SampleDescription.Type.TYPE_UNKNOWN,
                 atom.GetType(),
                 null)
        {
            _atom = atom.Clone();
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleDescription::Clone
        +---------------------------------------------------------------------*/
        public override SampleDescription Clone(out Ap4Result result)
        {
            Atom atom_clone = null;
            if (_atom != null) {
                atom_clone = _atom.Clone();
            }
            if (atom_clone == null) {
                result = Ap4Result.FAILURE;
                return null;
            }
            result = Ap4Result.SUCCESS;
            return new UnknownSampleDescription(atom_clone);
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownSampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            if (_atom != null) {
                return _atom.Clone();
            } else {
                return null;
            }
        }

        // accessor
        public Atom GetAtom() { return _atom; }

        private Atom _atom;
    }

    /*----------------------------------------------------------------------
    |   AP4_AudioSampleDescription  // MIXIN class
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream uses multiple inheritance (a sample description class
    // derives from AP4_SampleDescription and from one of the two MIXIN classes
    // AP4_AudioSampleDescription / AP4_VideoSampleDescription). C# has no
    // multiple inheritance of classes, so the mixins are interfaces that keep
    // the upstream names (`AP4_DYNAMIC_CAST(AP4_AudioSampleDescription, desc)`
    // is still `desc as AudioSampleDescription`), and each class that upstream
    // derives from a mixin declares the mixin's members and accessors itself.
    public interface AudioSampleDescription
    {
        // accessors
        uint   GetSampleRate();
        ushort GetSampleSize();
        ushort GetChannelCount();
    }

    /*----------------------------------------------------------------------
    |   AP4_VideoSampleDescription  // MIXIN class
    +---------------------------------------------------------------------*/
    public interface VideoSampleDescription
    {
        // accessors
        ushort GetWidth();
        ushort GetHeight();
        ushort GetDepth();
        string GetCompressorName();
    }

    /*----------------------------------------------------------------------
    |   AP4_GenericAudioSampleDescription
    +---------------------------------------------------------------------*/
    public class GenericAudioSampleDescription : SampleDescription,
                                                 AudioSampleDescription
    {
        // constructors
        public GenericAudioSampleDescription(uint        format,
                                             uint        sample_rate,
                                             ushort      sample_size,
                                             ushort      channel_count,
                                             AtomParent  details) :
            base(Type.TYPE_UNKNOWN, format, details)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
        }

        // inherited from AP4_SampleDescription
        /*----------------------------------------------------------------------
        |   AP4_GenericAudioSampleDescription
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            AudioSampleEntry sample_entry = new AudioSampleEntry(_format,
                                                                 _sampleRate<<16,
                                                                 _sampleSize,
                                                                 _channelCount);
            AtomParent details = _details;
            List<Atom> children = details.GetChildren();
            for (int i = 0; i < children.Count; i++) {
                Atom child = children[i];
                // PORT NOTE: upstream would crash on a NULL clone
                Atom child_clone = child.Clone();
                if (child_clone != null) sample_entry.AddChild(child_clone);
            }
            return sample_entry;
        }

        // AP4_AudioSampleDescription accessors
        public uint   GetSampleRate()   { return _sampleRate;   }
        public ushort GetSampleSize()   { return _sampleSize;   }
        public ushort GetChannelCount() { return _channelCount; }

        // AP4_AudioSampleDescription members
        protected uint   _sampleRate;
        protected ushort _sampleSize;
        protected ushort _channelCount;
    }

    /*----------------------------------------------------------------------
    |   AP4_GenericVideoSampleDescription
    +---------------------------------------------------------------------*/
    public class GenericVideoSampleDescription : SampleDescription,
                                                 VideoSampleDescription
    {
        // constructor
        public GenericVideoSampleDescription(uint        format,
                                             ushort      width,
                                             ushort      height,
                                             ushort      depth,
                                             string      compressor_name,
                                             AtomParent  details) :
            base(Type.TYPE_UNKNOWN, format, details)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
        }

        // inherited from AP4_SampleDescription
        /*----------------------------------------------------------------------
        |   AP4_GenericVideoSampleDescription::GetCodecString
        +---------------------------------------------------------------------*/
        public override Ap4Result GetCodecString(out string codec)
        {
            // VPx override
            VpccAtom vpcc = _details.GetChild(AtomType.VPCC) as VpccAtom;
            if (vpcc != null) {
                return vpcc.GetCodecString(GetFormat(), out codec);
            }

            return base.GetCodecString(out codec);
        }

        /*----------------------------------------------------------------------
        |   AP4_GenericVideoSampleDescription
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            VisualSampleEntry sample_entry = new VisualSampleEntry(_format,
                                                                   _width,
                                                                   _height,
                                                                   _depth,
                                                                   _compressorName);
            AtomParent details = _details;
            List<Atom> children = details.GetChildren();
            for (int i = 0; i < children.Count; i++) {
                Atom child = children[i];
                // PORT NOTE: upstream would crash on a NULL clone
                Atom child_clone = child.Clone();
                if (child_clone != null) sample_entry.AddChild(child_clone);
            }
            return sample_entry;
        }

        // AP4_VideoSampleDescription accessors
        public ushort GetWidth()          { return _width;  }
        public ushort GetHeight()         { return _height; }
        public ushort GetDepth()          { return _depth;  }
        public string GetCompressorName() { return _compressorName; }

        // AP4_VideoSampleDescription members
        protected ushort _width;
        protected ushort _height;
        protected ushort _depth;
        protected string _compressorName;
    }

    /*----------------------------------------------------------------------
    |   AP4_AvcSampleDescription
    +---------------------------------------------------------------------*/
    public class AvcSampleDescription : SampleDescription,
                                        VideoSampleDescription
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_AvcSampleDescription::AP4_AvcSampleDescription
        +---------------------------------------------------------------------*/
        public AvcSampleDescription(uint             format,
                                    ushort           width,
                                    ushort           height,
                                    ushort           depth,
                                    string           compressor_name,
                                    byte             profile,
                                    byte             level,
                                    byte             profile_compatibility,
                                    byte             length_size,
                                    byte             chroma_format,
                                    byte             bit_depth_luma_minus8,
                                    byte             bit_depth_chroma_minus8,
                                    List<DataBuffer> sequence_parameters,
                                    List<DataBuffer> picture_parameters) :
            base(Type.TYPE_AVC, format, null)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
            _avccAtom = new AvccAtom(profile,
                                     level,
                                     profile_compatibility,
                                     length_size,
                                     chroma_format,
                                     bit_depth_luma_minus8,
                                     bit_depth_chroma_minus8,
                                     sequence_parameters,
                                     picture_parameters);
            _details.AddChild(_avccAtom);
        }

        /*----------------------------------------------------------------------
        |   AP4_AvcSampleDescription::AP4_AvcSampleDescription
        +---------------------------------------------------------------------*/
        public AvcSampleDescription(uint      format, // avc1, avc2, avc3 or avc4
                                    ushort    width,
                                    ushort    height,
                                    ushort    depth,
                                    string    compressor_name,
                                    AvccAtom  avcc) :
            base(Type.TYPE_AVC, format, null)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
            if (avcc != null) {
                _avccAtom = new AvccAtom(avcc);
            } else {
                // should never happen
                _avccAtom = new AvccAtom();
            }
            _details.AddChild(_avccAtom);
        }

        /*----------------------------------------------------------------------
        |   AP4_AvcSampleDescription::AP4_AvcSampleDescription
        +---------------------------------------------------------------------*/
        public AvcSampleDescription(uint        format, // avc1, avc2, avc3 or avc4
                                    ushort      width,
                                    ushort      height,
                                    ushort      depth,
                                    string      compressor_name,
                                    AtomParent  details) :
            base(Type.TYPE_AVC, format, details)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
            AvccAtom avcc = _details.GetChild(AtomType.AVCC) as AvccAtom;
            if (avcc != null) {
                _avccAtom = new AvccAtom(avcc);
            } else {
                // shoud never happen
                _avccAtom = new AvccAtom();
            }
            _details.AddChild(_avccAtom);
        }

        // accessors
        public byte GetConfigurationVersion() { return _avccAtom.GetConfigurationVersion(); }
        public byte GetProfile() { return _avccAtom.GetProfile(); }
        public byte GetLevel() { return _avccAtom.GetLevel(); }
        public byte GetProfileCompatibility() { return _avccAtom.GetProfileCompatibility(); }
        public byte GetNaluLengthSize() { return _avccAtom.GetNaluLengthSize(); }
        public List<DataBuffer> GetSequenceParameters() {return _avccAtom.GetSequenceParameters(); }
        public List<DataBuffer> GetPictureParameters() { return _avccAtom.GetPictureParameters(); }
        public DataBuffer GetRawBytes() { return _avccAtom.GetRawBytes(); }
        public byte GetChromaFormat() { return _avccAtom.GetChromaFormat(); }
        public byte GetBitDepthLumaMinus8() { return _avccAtom.GetBitDepthLumaMinus8(); }
        public byte GetBitDepthChromaMinus8() { return _avccAtom.GetBitDepthChromaMinus8(); }

        // inherited from AP4_SampleDescription
        /*----------------------------------------------------------------------
        |   AP4_AvcSampleDescription::GetCodecString
        +---------------------------------------------------------------------*/
        public override Ap4Result GetCodecString(out string codec) {
            string coding = Ap4Utils.FormatFourChars(GetFormat());
            // "%s.%02X%02X%02X"
            string workspace = coding + "." +
                               GetProfile().ToString("X2", CultureInfo.InvariantCulture) +
                               GetProfileCompatibility().ToString("X2", CultureInfo.InvariantCulture) +
                               GetLevel().ToString("X2", CultureInfo.InvariantCulture);

            // Dolby Vision override
            DvccAtom dvcc = _details.GetChild(AtomType.DVCC) as DvccAtom;
            if (dvcc != null) {
                return dvcc.GetCodecString(workspace, GetFormat(), out codec);
            }

            codec = workspace;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AvcSampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new AvcSampleEntry(_format,
                                      _width,
                                      _height,
                                      _depth,
                                      _compressorName,
                                      _details);
        }

        // static methods
        public static string GetProfileName(byte profile) {
            return AvccAtom.GetProfileName(profile);
        }

        // AP4_VideoSampleDescription accessors
        public ushort GetWidth()          { return _width;  }
        public ushort GetHeight()         { return _height; }
        public ushort GetDepth()          { return _depth;  }
        public string GetCompressorName() { return _compressorName; }

        // AP4_VideoSampleDescription members
        protected ushort _width;
        protected ushort _height;
        protected ushort _depth;
        protected string _compressorName;

        private AvccAtom _avccAtom;
    }

    /*----------------------------------------------------------------------
    |   AP4_AvcDoviSampleDescription
    +---------------------------------------------------------------------*/
    public class AvcDoviSampleDescription : AvcSampleDescription
    {
        /*----------------------------------------------------------------------
        |   AP4_AvcDoviSampleDescription::AP4_AvcDoviSampleDescription
        +---------------------------------------------------------------------*/
        public AvcDoviSampleDescription(uint             format,
                                        ushort           width,
                                        ushort           height,
                                        ushort           depth,
                                        string           compressor_name,
                                        byte             profile,
                                        byte             level,
                                        byte             profile_compatibility,
                                        byte             length_size,
                                        List<DataBuffer> sequence_parameters,
                                        List<DataBuffer> picture_parameters,
                                        byte             chroma_format,
                                        byte             bit_depth_luma_minus8,
                                        byte             bit_depth_chroma_minus8,
                                        byte             dv_version_major,
                                        byte             dv_version_minor,
                                        byte             dv_profile,
                                        byte             dv_level,
                                        bool             rpu_present_flag,
                                        bool             el_present_flag,
                                        bool             bl_present_flag,
                                        byte             dv_bl_signal_compatibility_id) :
            base(format,
                 width,
                 height,
                 depth,
                 compressor_name,
                 profile,
                 level,
                 profile_compatibility,
                 length_size,
                 chroma_format,
                 bit_depth_luma_minus8,
                 bit_depth_chroma_minus8,
                 sequence_parameters,
                 picture_parameters)
        {
            _dvccAtom = new DvccAtom(dv_version_major,
                                     dv_version_minor,
                                     dv_profile,
                                     dv_level,
                                     rpu_present_flag,
                                     el_present_flag,
                                     bl_present_flag,
                                     dv_bl_signal_compatibility_id);
            _details.AddChild(_dvccAtom);
        }

        private DvccAtom _dvccAtom;
    }

    /*----------------------------------------------------------------------
    |   AP4_HevcSampleDescription
    +---------------------------------------------------------------------*/
    public class HevcSampleDescription : SampleDescription,
                                         VideoSampleDescription
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_HevcSampleDescription::AP4_HevcSampleDescription
        +---------------------------------------------------------------------*/
        public HevcSampleDescription(uint      format, // hvc1 or hev1
                                     ushort    width,
                                     ushort    height,
                                     ushort    depth,
                                     string    compressor_name,
                                     HvccAtom  hvcc) :
            base(Type.TYPE_HEVC, format, null)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
            if (hvcc != null) {
                _hvccAtom = new HvccAtom(hvcc);
            } else {
                // should never happen
                _hvccAtom = new HvccAtom();
            }
            _details.AddChild(_hvccAtom);
        }

        /*----------------------------------------------------------------------
        |   AP4_HevcSampleDescription::AP4_HevcSampleDescription
        +---------------------------------------------------------------------*/
        public HevcSampleDescription(uint        format, // hvc1 or hev1
                                     ushort      width,
                                     ushort      height,
                                     ushort      depth,
                                     string      compressor_name,
                                     AtomParent  details) :
            base(Type.TYPE_HEVC, format, details)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
            HvccAtom hvcc = _details.GetChild(AtomType.HVCC) as HvccAtom;
            if (hvcc != null) {
                _hvccAtom = new HvccAtom(hvcc);
            } else {
                // shoud never happen
                _hvccAtom = new HvccAtom();
            }
            _details.AddChild(_hvccAtom);
        }

        /*----------------------------------------------------------------------
        |   AP4_HevcSampleDescription::AP4_HevcSampleDescription
        +---------------------------------------------------------------------*/
        public HevcSampleDescription(uint             format,
                                     ushort           width,
                                     ushort           height,
                                     ushort           depth,
                                     string           compressor_name,
                                     byte             general_profile_space,
                                     byte             general_tier_flag,
                                     byte             general_profile,
                                     uint             general_profile_compatibility_flags,
                                     ulong            general_constraint_indicator_flags,
                                     byte             general_level,
                                     uint             min_spatial_segmentation,
                                     byte             parallelism_type,
                                     byte             chroma_format,
                                     byte             luma_bit_depth,
                                     byte             chroma_bit_depth,
                                     ushort           average_frame_rate,
                                     byte             constant_frame_rate,
                                     byte             num_temporal_layers,
                                     byte             temporal_id_nested,
                                     byte             nalu_length_size,
                                     List<DataBuffer> video_parameters,
                                     byte             video_parameters_completeness,
                                     List<DataBuffer> sequence_parameters,
                                     byte             sequence_parameters_completeness,
                                     List<DataBuffer> picture_parameters,
                                     byte             picture_parameters_completeness) :
            base(Type.TYPE_HEVC, format, null)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
            _hvccAtom = new HvccAtom(general_profile_space,
                                     general_tier_flag,
                                     general_profile,
                                     general_profile_compatibility_flags,
                                     general_constraint_indicator_flags,
                                     general_level,
                                     min_spatial_segmentation,
                                     parallelism_type,
                                     chroma_format,
                                     luma_bit_depth,
                                     chroma_bit_depth,
                                     average_frame_rate,
                                     constant_frame_rate,
                                     num_temporal_layers,
                                     temporal_id_nested,
                                     nalu_length_size,
                                     video_parameters,
                                     video_parameters_completeness,
                                     sequence_parameters,
                                     sequence_parameters_completeness,
                                     picture_parameters,
                                     picture_parameters_completeness);
            _details.AddChild(_hvccAtom);
        }

        // accessors
        public byte   GetConfigurationVersion()             { return _hvccAtom.GetConfigurationVersion(); }
        public byte   GetGeneralProfileSpace()              { return _hvccAtom.GetGeneralProfileSpace(); }
        public byte   GetGeneralTierFlag()                  { return _hvccAtom.GetGeneralTierFlag(); }
        public byte   GetGeneralProfile()                   { return _hvccAtom.GetGeneralProfile(); }
        public uint   GetGeneralProfileCompatibilityFlags() { return _hvccAtom.GetGeneralProfileCompatibilityFlags(); }
        public ulong  GetGeneralConstraintIndicatorFlags()  { return _hvccAtom.GetGeneralConstraintIndicatorFlags(); }
        public byte   GetGeneralLevel()                     { return _hvccAtom.GetGeneralLevel(); }
        public uint   GetMinSpatialSegmentation()           { return _hvccAtom.GetMinSpatialSegmentation(); }
        public byte   GetParallelismType()                  { return _hvccAtom.GetParallelismType(); }
        public byte   GetChromaFormat()                     { return _hvccAtom.GetChromaFormat(); }
        public byte   GetLumaBitDepth()                     { return _hvccAtom.GetLumaBitDepth(); }
        public byte   GetChromaBitDepth()                   { return _hvccAtom.GetChromaBitDepth(); }
        public ushort GetAverageFrameRate()                 { return _hvccAtom.GetAverageFrameRate(); }
        public byte   GetConstantFrameRate()                { return _hvccAtom.GetConstantFrameRate(); }
        public byte   GetNumTemporalLayers()                { return _hvccAtom.GetNumTemporalLayers(); }
        public byte   GetTemporalIdNested()                 { return _hvccAtom.GetTemporalIdNested(); }
        public byte   GetNaluLengthSize()                   { return _hvccAtom.GetNaluLengthSize(); }
        public List<HvccAtom.Sequence> GetSequences()       { return _hvccAtom.GetSequences(); }
        public DataBuffer GetRawBytes()                     { return _hvccAtom.GetRawBytes(); }

        /*----------------------------------------------------------------------
        |   ReverseBits
        +---------------------------------------------------------------------*/
        private static uint ReverseBits(uint bits)
        {
            uint count = sizeof(uint) * 8;
            uint reverse_bits = 0;

            while (bits != 0) {
               reverse_bits = (reverse_bits << 1) | (bits & 1);
               bits >>= 1;
               count--;
            }
            return (count < 32) ? (reverse_bits << (int)count) : 0;
        }

        // inherited from AP4_SampleDescription
        /*----------------------------------------------------------------------
        |   AP4_HevcSampleDescription:GetCodecString
        +---------------------------------------------------------------------*/
        public override Ap4Result GetCodecString(out string codec) {
            string coding = Ap4Utils.FormatFourChars(GetFormat());
            string profile_space = "";
            if (GetGeneralProfileSpace() > 0 && GetGeneralProfileSpace() <= 3) {
                profile_space = ((char)('A'+GetGeneralProfileSpace()-1)).ToString();
            }
            ulong constraints = GetGeneralConstraintIndicatorFlags();
            while (constraints != 0 && ((constraints & 0xFF) == 0)) {
                constraints >>= 8;
            }
            // "%s.%s%d.%X.%c%d.%llx"
            string workspace = coding + "." +
                               profile_space +
                               GetGeneralProfile().ToString(CultureInfo.InvariantCulture) + "." +
                               ReverseBits(GetGeneralProfileCompatibilityFlags()).ToString("X", CultureInfo.InvariantCulture) + "." +
                               (GetGeneralTierFlag() != 0?'H':'L') +
                               GetGeneralLevel().ToString(CultureInfo.InvariantCulture) + "." +
                               constraints.ToString("x", CultureInfo.InvariantCulture);

            // Dolby Vision override
            DvccAtom dvcc = _details.GetChild(AtomType.DVCC) as DvccAtom;
            if (dvcc != null) {
                return dvcc.GetCodecString(workspace, GetFormat(), out codec);
            }

            codec = workspace;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_HevcSampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new HevcSampleEntry(_format,
                                       _width,
                                       _height,
                                       _depth,
                                       _compressorName,
                                       _details);
        }

        // static methods
        public static string GetProfileName(byte profile_space, byte profile) {
            return HvccAtom.GetProfileName(profile_space, profile);
        }

        // AP4_VideoSampleDescription accessors
        public ushort GetWidth()          { return _width;  }
        public ushort GetHeight()         { return _height; }
        public ushort GetDepth()          { return _depth;  }
        public string GetCompressorName() { return _compressorName; }

        // AP4_VideoSampleDescription members
        protected ushort _width;
        protected ushort _height;
        protected ushort _depth;
        protected string _compressorName;

        private HvccAtom _hvccAtom;
    }

    /*----------------------------------------------------------------------
    |   AP4_HevcDoviSampleDescription
    +---------------------------------------------------------------------*/
    public class HevcDoviSampleDescription : HevcSampleDescription
    {
        /*----------------------------------------------------------------------
        |   AP4_HevcDoviSampleDescription::AP4_HevcDoviSampleDescription
        +---------------------------------------------------------------------*/
        public HevcDoviSampleDescription(uint             format,
                                         ushort           width,
                                         ushort           height,
                                         ushort           depth,
                                         string           compressor_name,
                                         byte             general_profile_space,
                                         byte             general_tier_flag,
                                         byte             general_profile,
                                         uint             general_profile_compatibility_flags,
                                         ulong            general_constraint_indicator_flags,
                                         byte             general_level,
                                         uint             min_spatial_segmentation,
                                         byte             parallelism_type,
                                         byte             chroma_format,
                                         byte             luma_bit_depth,
                                         byte             chroma_bit_depth,
                                         ushort           average_frame_rate,
                                         byte             constant_frame_rate,
                                         byte             num_temporal_layers,
                                         byte             temporal_id_nested,
                                         byte             nalu_length_size,
                                         List<DataBuffer> video_parameters,
                                         byte             video_parameters_completeness,
                                         List<DataBuffer> sequence_parameters,
                                         byte             sequence_parameters_completeness,
                                         List<DataBuffer> picture_parameters,
                                         byte             picture_parameters_completeness,
                                         byte             dv_version_major,
                                         byte             dv_version_minor,
                                         byte             dv_profile,
                                         byte             dv_level,
                                         bool             rpu_present_flag,
                                         bool             el_present_flag,
                                         bool             bl_present_flag,
                                         byte             dv_bl_signal_compatibility_id) :
            base(format,
                 width,
                 height,
                 depth,
                 compressor_name,
                 general_profile_space,
                 general_tier_flag,
                 general_profile,
                 general_profile_compatibility_flags,
                 general_constraint_indicator_flags,
                 general_level,
                 min_spatial_segmentation,
                 parallelism_type,
                 chroma_format,
                 luma_bit_depth,
                 chroma_bit_depth,
                 average_frame_rate,
                 constant_frame_rate,
                 num_temporal_layers,
                 temporal_id_nested,
                 nalu_length_size,
                 video_parameters,
                 video_parameters_completeness,
                 sequence_parameters,
                 sequence_parameters_completeness,
                 picture_parameters,
                 picture_parameters_completeness)
        {
            _dvccAtom = new DvccAtom(dv_version_major,
                                     dv_version_minor,
                                     dv_profile,
                                     dv_level,
                                     rpu_present_flag,
                                     el_present_flag,
                                     bl_present_flag,
                                     dv_bl_signal_compatibility_id);
            _details.AddChild(_dvccAtom);
        }

        private DvccAtom _dvccAtom;
    }

    /*----------------------------------------------------------------------
    |   AP4_Av1SampleDescription
    +---------------------------------------------------------------------*/
    public class Av1SampleDescription : SampleDescription,
                                        VideoSampleDescription
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_Av1SampleDescription::AP4_Av1SampleDescription
        +---------------------------------------------------------------------*/
        public Av1SampleDescription(uint    format,
                                    ushort  width,
                                    ushort  height,
                                    ushort  depth,
                                    string  compressor_name,
                                    byte    version,
                                    byte    seq_profile,
                                    byte    seq_level_idx_0,
                                    byte    seq_tier_0,
                                    byte    high_bitdepth,
                                    byte    twelve_bit,
                                    byte    monochrome,
                                    byte    chroma_subsampling_x,
                                    byte    chroma_subsampling_y,
                                    byte    chroma_sample_position,
                                    byte    initial_presentation_delay_present,
                                    byte    initial_presentation_delay_minus_one,
                                    byte[]  config_obus,
                                    uint    config_obus_size) :
            base(Type.TYPE_AV1, format, null)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
            _av1cAtom = new Av1cAtom(version,
                                     seq_profile,
                                     seq_level_idx_0,
                                     seq_tier_0,
                                     high_bitdepth,
                                     twelve_bit,
                                     monochrome,
                                     chroma_subsampling_x,
                                     chroma_subsampling_y,
                                     chroma_sample_position,
                                     initial_presentation_delay_present,
                                     initial_presentation_delay_minus_one,
                                     config_obus,
                                     config_obus_size);
            _details.AddChild(_av1cAtom);
        }

        /*----------------------------------------------------------------------
        |   AP4_Av1SampleDescription::AP4_Av1SampleDescription
        +---------------------------------------------------------------------*/
        public Av1SampleDescription(uint      format, // av01
                                    ushort    width,
                                    ushort    height,
                                    ushort    depth,
                                    string    compressor_name,
                                    Av1cAtom  av1c) :
            base(Type.TYPE_AV1, format, null)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
            if (av1c != null) {
                _av1cAtom = new Av1cAtom(av1c);
            } else {
                // should never happen
                _av1cAtom = new Av1cAtom();
            }
            _details.AddChild(_av1cAtom);
        }

        /*----------------------------------------------------------------------
        |   AP4_Av1SampleDescription::AP4_Av1SampleDescription
        +---------------------------------------------------------------------*/
        public Av1SampleDescription(uint        format, // av01
                                    ushort      width,
                                    ushort      height,
                                    ushort      depth,
                                    string      compressor_name,
                                    AtomParent  details) :
            base(Type.TYPE_AV1, format, details)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
            Av1cAtom av1c = _details.GetChild(AtomType.AV1C) as Av1cAtom;
            if (av1c != null) {
                _av1cAtom = new Av1cAtom(av1c);
            } else {
                // shoud never happen
                _av1cAtom = new Av1cAtom();
            }
            _details.AddChild(_av1cAtom);
        }

        // accessors
        public byte GetSeqProfile()                       { return _av1cAtom.GetSeqProfile();                       }
        public byte GetSeqLevelIdx0()                     { return _av1cAtom.GetSeqLevelIdx0();                     }
        public byte GetSeqTier0()                         { return _av1cAtom.GetSeqTier0();                         }
        public byte GetHighBitDepth()                     { return _av1cAtom.GetHighBitDepth();                     }
        public byte GetTwelveBit()                        { return _av1cAtom.GetTwelveBit();                        }
        public byte GetMonochrome()                       { return _av1cAtom.GetMonochrome();                       }
        public byte GetChromaSubsamplingX()               { return _av1cAtom.GetChromaSubsamplingX();               }
        public byte GetChromaSubsamplingY()               { return _av1cAtom.GetChromaSubsamplingY();               }
        public byte GetChromaSamplePosition()             { return _av1cAtom.GetChromaSamplePosition();             }
        public byte GetInitialPresentationDelayPresent()  { return _av1cAtom.GetInitialPresentationDelayPresent();  }
        public byte GetInitialPresentationDelayMinusOne() { return _av1cAtom.GetInitialPresentationDelayMinusOne(); }
        public DataBuffer GetConfigObus()                 { return _av1cAtom.GetConfigObus();                       }

        // inherited from AP4_SampleDescription
        /*----------------------------------------------------------------------
        |   AP4_Av1cSampleDescription::GetCodecString
        +---------------------------------------------------------------------*/
        public override Ap4Result GetCodecString(out string codec) {
            byte bit_depth = (byte)((this.GetHighBitDepth() == 0) ? 8: (this.GetTwelveBit() == 0 ? 10 : 12));
            byte color_primaries = 1;
            byte transfer_characteristics = 1;
            byte matrix_coefficients = 1;
            byte video_full_range_flag = 0;
            string coding = Ap4Utils.FormatFourChars(GetFormat());
            // "%s.%d.%02d%c.%02d.%d.%d%d%d.%02d.%02d.%02d.%d"
            string workspace = coding + "." +
                               this.GetSeqProfile().ToString(CultureInfo.InvariantCulture) + "." +
                               this.GetSeqLevelIdx0().ToString("D2", CultureInfo.InvariantCulture) +
                               (this.GetSeqTier0() == 0 ? 'M' : 'H') + "." +
                               bit_depth.ToString("D2", CultureInfo.InvariantCulture) + "." +
                               this.GetMonochrome().ToString(CultureInfo.InvariantCulture) + "." +
                               this.GetChromaSubsamplingX().ToString(CultureInfo.InvariantCulture) +
                               this.GetChromaSubsamplingY().ToString(CultureInfo.InvariantCulture) +
                               ((this.GetChromaSubsamplingX() == 1 && this.GetChromaSubsamplingY() == 1) ?
                                (int)this.GetChromaSamplePosition() : 0).ToString(CultureInfo.InvariantCulture) + "." +
                               color_primaries.ToString("D2", CultureInfo.InvariantCulture) + "." +
                               transfer_characteristics.ToString("D2", CultureInfo.InvariantCulture) + "." +
                               matrix_coefficients.ToString("D2", CultureInfo.InvariantCulture) + "." +
                               video_full_range_flag.ToString(CultureInfo.InvariantCulture);
            codec = workspace;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Av1SampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new Av1SampleEntry(_format,
                                      _width,
                                      _height,
                                      _depth,
                                      _compressorName,
                                      _details);
        }

        // static methods
        public static string GetProfileName(byte profile) {
            return Av1cAtom.GetProfileName(profile);
        }

        // AP4_VideoSampleDescription accessors
        public ushort GetWidth()          { return _width;  }
        public ushort GetHeight()         { return _height; }
        public ushort GetDepth()          { return _depth;  }
        public string GetCompressorName() { return _compressorName; }

        // AP4_VideoSampleDescription members
        protected ushort _width;
        protected ushort _height;
        protected ushort _depth;
        protected string _compressorName;

        private Av1cAtom _av1cAtom;
    }

    /*----------------------------------------------------------------------
    |   AP4_MpegSampleDescription
    +---------------------------------------------------------------------*/
    public class MpegSampleDescription : SampleDescription
    {
        // types
        // PORT NOTE: `typedef AP4_UI08 StreamType;` and `typedef AP4_UI08 OTI;`
        // are plain `byte` in the port.

        // class methods
        /*----------------------------------------------------------------------
        |   AP4_MpegSampleDescription::GetStreamTypeString
        +---------------------------------------------------------------------*/
        public static string GetStreamTypeString(byte type)
        {
            switch (type) {
                case Ap4.STREAM_TYPE_FORBIDDEN: return "INVALID";
                case Ap4.STREAM_TYPE_OD: return "Object Descriptor";
                case Ap4.STREAM_TYPE_CR: return "CR";
                case Ap4.STREAM_TYPE_BIFS: return "BIFS";
                case Ap4.STREAM_TYPE_VISUAL: return "Visual";
                case Ap4.STREAM_TYPE_AUDIO: return "Audio";
                case Ap4.STREAM_TYPE_MPEG7: return "MPEG-7";
                case Ap4.STREAM_TYPE_IPMP: return "IPMP";
                case Ap4.STREAM_TYPE_OCI: return "OCI";
                case Ap4.STREAM_TYPE_MPEGJ: return "MPEG-J";
                default: return "UNKNOWN";
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegSampleDescription::GetObjectTypeString
        +---------------------------------------------------------------------*/
        public static string GetObjectTypeString(byte oti)
        {
            switch (oti) {
                case Ap4.OTI_MPEG4_SYSTEM: return "MPEG-4 System";
                case Ap4.OTI_MPEG4_SYSTEM_COR: return "MPEG-4 System COR";
                case Ap4.OTI_MPEG4_VISUAL: return "MPEG-4 Video";
                case Ap4.OTI_MPEG4_AUDIO: return "MPEG-4 Audio";
                case Ap4.OTI_MPEG2_VISUAL_SIMPLE: return "MPEG-2 Video Simple Profile";
                case Ap4.OTI_MPEG2_VISUAL_MAIN: return "MPEG-2 Video Main Profile";
                case Ap4.OTI_MPEG2_VISUAL_SNR: return "MPEG-2 Video SNR";
                case Ap4.OTI_MPEG2_VISUAL_SPATIAL: return "MPEG-2 Video Spatial";
                case Ap4.OTI_MPEG2_VISUAL_HIGH: return "MPEG-2 Video High";
                case Ap4.OTI_MPEG2_VISUAL_422: return "MPEG-2 Video 4:2:2";
                case Ap4.OTI_MPEG2_AAC_AUDIO_MAIN: return "MPEG-2 Audio AAC Main Profile";
                case Ap4.OTI_MPEG2_AAC_AUDIO_LC: return "MPEG-2 Audio AAC Low Complexity";
                case Ap4.OTI_MPEG2_AAC_AUDIO_SSRP: return "MPEG-2 Audio AAC SSRP";
                case Ap4.OTI_MPEG2_PART3_AUDIO: return "MPEG-2 Audio Part-3";
                case Ap4.OTI_MPEG1_VISUAL: return "MPEG-1 Video";
                case Ap4.OTI_MPEG1_AUDIO: return "MPEG-1 Audio";
                case Ap4.OTI_JPEG: return "JPEG";
                case Ap4.OTI_JPEG2000: return "JPEG-2000";
                case Ap4.OTI_EVRC_VOICE: return "EVRC Voice";
                case Ap4.OTI_SMV_VOICE: return "SMV Voice";
                case Ap4.OTI_3GPP2_CMF: return "3GPP2 CMF";
                case Ap4.OTI_SMPTE_VC1: return "SMPTE VC1 Video";
                case Ap4.OTI_DIRAC_VIDEO: return "Dirac Video";
                case Ap4.OTI_AC3_AUDIO: return "AC3 Audio";
                case Ap4.OTI_EAC3_AUDIO: return "E-AC3 Audio";
                case Ap4.OTI_DRA_AUDIO: return "DRA Audio";
                case Ap4.OTI_G719_AUDIO: return "G.719 Audio";
                case Ap4.OTI_DTS_AUDIO: return "DTS Audio";
                case Ap4.OTI_DTS_HIRES_AUDIO: return "DTS High Resolution Audio";
                case Ap4.OTI_DTS_MASTER_AUDIO: return "DTS Master Audio";
                case Ap4.OTI_DTS_EXPRESS_AUDIO: return "DTS Express/LBR Audio";
                case Ap4.OTI_OPUS_AUDIO: return "Opus Audio";
                case Ap4.OTI_VP9_VIDEO: return "VP9 Video";
                case Ap4.OTI_VORBIS_AUDIO: return "Vorbis Audio";
                case Ap4.OTI_13K_VOICE: return "13K Voice";
                default: return "UNKNOWN";
            }
        }

        // constructors & destructor
        /*----------------------------------------------------------------------
        |   AP4_MpegSampleDescription::AP4_MpegSampleDescription
        +---------------------------------------------------------------------*/
        public MpegSampleDescription(uint      format,
                                     EsdsAtom  esds) :
            base(Type.TYPE_MPEG, format, null)
        {
            _streamType = 0;
            _objectTypeId = 0;
            _bufferSize = 0;
            _maxBitrate = 0;
            _avgBitrate = 0;
            if (esds != null) {
                // get the es descriptor
                EsDescriptor  es_desc = esds.GetEsDescriptor();
                if (es_desc == null) return;

                // get the decoder config descriptor
                DecoderConfigDescriptor dc_desc =
                    es_desc.GetDecoderConfigDescriptor();
                if (dc_desc != null) {
                    _streamType   = dc_desc.GetStreamType();
                    _objectTypeId = dc_desc.GetObjectTypeIndication();
                    _bufferSize   = dc_desc.GetBufferSize();
                    _maxBitrate   = dc_desc.GetMaxBitrate();
                    _avgBitrate   = dc_desc.GetAvgBitrate();
                    DecoderSpecificInfoDescriptor dsi_desc =
                        dc_desc.GetDecoderSpecificInfoDescriptor();
                    if (dsi_desc != null) {
                        _decoderInfo.SetData(dsi_desc.GetDecoderSpecificInfo().GetData(),
                                             dsi_desc.GetDecoderSpecificInfo().GetDataSize());
                    }
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegSampleDescription::AP4_MpegSampleDescription
        +---------------------------------------------------------------------*/
        public MpegSampleDescription(
            uint        format,
            byte        stream_type,
            byte        oti,
            DataBuffer  decoder_info,
            uint        buffer_size,
            uint        max_bitrate,
            uint        avg_bitrate) :
            base(Type.TYPE_MPEG, format, null)
        {
            _streamType = stream_type;
            _objectTypeId = oti;
            _bufferSize = buffer_size;
            _maxBitrate = max_bitrate;
            _avgBitrate = avg_bitrate;
            if (decoder_info != null) {
                _decoderInfo.SetData(decoder_info.GetData(), decoder_info.GetDataSize());
            }
        }

        // accessors
        public byte GetStreamType()   { return _streamType; }
        public byte GetObjectTypeId() { return _objectTypeId; }
        public uint GetBufferSize()   { return _bufferSize; }
        public uint GetMaxBitrate()   { return _maxBitrate; }
        public uint GetAvgBitrate()   { return _avgBitrate; }
        public DataBuffer GetDecoderInfo() { return _decoderInfo; }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MpegSampleDescription::CreateEsDescriptor
        +---------------------------------------------------------------------*/
        public EsDescriptor CreateEsDescriptor()
        {
            EsDescriptor desc = new EsDescriptor(0);
            DecoderSpecificInfoDescriptor dsi_desc;
            if (_decoderInfo.GetDataSize() != 0) {
                dsi_desc = new DecoderSpecificInfoDescriptor(_decoderInfo);
            } else {
                dsi_desc = null;
            }
            DecoderConfigDescriptor decoder_config =
                new DecoderConfigDescriptor(_streamType,
                _objectTypeId,
                _bufferSize,
                _maxBitrate,
                _avgBitrate,
                dsi_desc);
            desc.AddSubDescriptor(decoder_config);

            // add a fixed SL Config
            desc.AddSubDescriptor(new SLConfigDescriptor());

            return desc;
        }

        // members
        // PORT NOTE: upstream AP4_MpegSampleDescription declares its own m_Format,
        // which shadows AP4_SampleDescription::m_Format and is never initialized
        // nor read upstream. It is kept for fidelity: in this class and below,
        // `_format` is this member (always 0); use GetFormat() for the format.
        protected new uint   _format;
        protected byte       _streamType;
        protected byte       _objectTypeId;
        protected uint       _bufferSize;
        protected uint       _maxBitrate;
        protected uint       _avgBitrate;
        protected DataBuffer _decoderInfo = new DataBuffer();
    }

    /*----------------------------------------------------------------------
    |   AP4_MpegSystemSampleDescription
    +---------------------------------------------------------------------*/
    public class MpegSystemSampleDescription : MpegSampleDescription
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_MpegSystemSampleDescription::AP4_MpegSystemSampleDescription
        +---------------------------------------------------------------------*/
        public MpegSystemSampleDescription(EsdsAtom esds) :
            base(AtomType.MP4S, esds)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegSystemSampleDescription::AP4_MpegSystemSampleDescription
        +---------------------------------------------------------------------*/
        public MpegSystemSampleDescription(
            byte        stream_type,
            byte        oti,
            DataBuffer  decoder_info,
            uint        buffer_size,
            uint        max_bitrate,
            uint        avg_bitrate) :
            base(AtomType.MP4S,
                 stream_type,
                 oti,
                 decoder_info,
                 buffer_size,
                 max_bitrate,
                 avg_bitrate)
        {
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MpegSystemSampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new Mp4sSampleEntry(CreateEsDescriptor());
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_MpegAudioSampleDescription
    +---------------------------------------------------------------------*/
    public class MpegAudioSampleDescription : MpegSampleDescription,
                                              AudioSampleDescription
    {
        // types
        // PORT NOTE: `typedef AP4_UI08 Mpeg4AudioObjectType;` is plain `byte`.

        // class methods
        /*----------------------------------------------------------------------
        |   AP4_MpegAudioSampleDescription::GetMpeg4AudioObjectTypeString
        +---------------------------------------------------------------------*/
        public static string GetMpeg4AudioObjectTypeString(byte type)
        {
            switch (type) {
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_MAIN: return "AAC Main Profile";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_LC: return "AAC Low Complexity";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_SSR: return "AAC Scalable Sample Rate";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_LTP: return "AAC Long Term Predictor";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_SBR: return "Spectral Band Replication";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_SCALABLE: return "AAC Scalable";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_TWINVQ: return "Twin VQ";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_CELP: return "CELP";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_HVXC: return "HVXC";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_TTSI: return "TTSI";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_MAIN_SYNTHETIC: return "Main Synthetic";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_WAVETABLE_SYNTHESIS: return "Wavetable Synthesis";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_GENERAL_MIDI: return "General MIDI";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ALGORITHMIC_SYNTHESIS: return "Algorithmic Synthesis";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LC: return "Error Resilient AAC Low Complexity";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LTP: return "Error Resilient AAC Long Term Prediction";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_SCALABLE: return "Error Resilient AAC Scalable";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_TWINVQ: return "Error Resilient Twin VQ";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_BSAC: return "Error Resilient Bit Sliced Arithmetic Coding";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LD: return "Error Resilient AAC Low Delay";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_CELP: return "Error Resilient CELP";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_HVXC: return "Error Resilient HVXC";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_HILN: return "Error Resilient HILN";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_PARAMETRIC: return "Error Resilient Parametric";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_SSC: return "SSC";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_PS: return "Parametric Stereo";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_MPEG_SURROUND: return "MPEG Surround";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_LAYER_1: return "MPEG Layer 1";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_LAYER_2: return "MPEG Layer 2";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_LAYER_3: return "MPEG Layer 3";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_DST: return "Direct Stream Transfer";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ALS: return "ALS Lossless Coding";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_SLS: return "SLS Scalable Lossless Coding";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_SLS_NON_CORE: return "SLS Scalable Lossless Coding (Non Core)";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_ELD: return "Error Resilient AAC ELD";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_SMR_SIMPLE: return "SMR Simple";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_SMR_MAIN: return "SMR Main";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_USAC: return "USAC";
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_SAOC: return "SAOC";
                default: return "UNKNOWN";
            }
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_MpegAudioSampleDescription::AP4_MpegAudioSampleDescription
        +---------------------------------------------------------------------*/
        public MpegAudioSampleDescription(
            uint      sample_rate,
            ushort    sample_size,
            ushort    channel_count,
            EsdsAtom  esds) :
            base(AtomType.MP4A, esds)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegAudioSampleDescription::AP4_MpegAudioSampleDescription
        +---------------------------------------------------------------------*/
        public MpegAudioSampleDescription(
            byte        oti,
            uint        sample_rate,
            ushort      sample_size,
            ushort      channel_count,
            DataBuffer  decoder_info,
            uint        buffer_size,
            uint        max_bitrate,
            uint        avg_bitrate) :
            base(AtomType.MP4A,
                 Ap4.STREAM_TYPE_AUDIO,
                 oti,
                 decoder_info, buffer_size,
                 max_bitrate, avg_bitrate)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
        }

        // inherited from AP4_SampleDescription
        /*----------------------------------------------------------------------
        |   AP4_MpegAudioSampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new Mp4aSampleEntry(_sampleRate<<16,
                                       _sampleSize,
                                       _channelCount,
                                       CreateEsDescriptor());
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegAudioSampleDescription::GetCodecString
        +---------------------------------------------------------------------*/
        public override Ap4Result GetCodecString(out string codec)
        {
            string coding = Ap4Utils.FormatFourChars(GetFormat());
            string workspace = "";
            if (GetFormat() == Ap4.SAMPLE_FORMAT_MP4A) {
                if (GetObjectTypeId() == Ap4.OTI_MPEG4_AUDIO) {
                    byte object_type = GetMpeg4AudioObjectType();
                    if (object_type == Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_LC) {
                        DataBuffer dsi = GetDecoderInfo();
                        if (dsi.GetDataSize() != 0) {
                            Mp4AudioDecoderConfig dec_config = new Mp4AudioDecoderConfig();
                            Ap4Result result = dec_config.Parse(dsi.GetData(), dsi.GetDataSize());
                            if (result.Succeeded()) {
                                if (dec_config._extension._psPresent) {
                                    object_type = Ap4.MPEG4_AUDIO_OBJECT_TYPE_PS;
                                } else if (dec_config._extension._sbrPresent) {
                                    object_type = Ap4.MPEG4_AUDIO_OBJECT_TYPE_SBR;
                                }
                            }
                        }
                    }
                    // "%s.%02X.%d"
                    workspace = coding + "." +
                                ((int)GetObjectTypeId()).ToString("X2", CultureInfo.InvariantCulture) + "." +
                                object_type.ToString(CultureInfo.InvariantCulture);
                } else {
                    // "%s.%02X"
                    workspace = coding + "." +
                                ((int)GetObjectTypeId()).ToString("X2", CultureInfo.InvariantCulture);
                }
            }

            codec = workspace;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegAudioSampleDescription::GetMpeg4AudioObjectType
        +---------------------------------------------------------------------*/
        /// <summary>
        /// For sample descriptions of MPEG-4 audio tracks (i.e <see cref="MpegSampleDescription.GetObjectTypeId"/>
        /// returns <see cref="Ap4.OTI_MPEG4_AUDIO"/>), this method returns the MPEG4 Audio Object
        /// Type. For other sample descriptions, this method returns 0.
        /// </summary>
        public byte GetMpeg4AudioObjectType()
        {
            if (_objectTypeId == Ap4.OTI_MPEG4_AUDIO &&
                _decoderInfo.GetDataSize() >= 1) {
                byte type = (byte)(_decoderInfo.GetData()[0]>>3);
                if (type == 31) {
                    if (_decoderInfo.GetDataSize() < 2) return 0;
                    type = (byte)(32+(((_decoderInfo.GetData()[0]&0x07)<<3) |
                                      ((_decoderInfo.GetData()[1]&0xE0)>>5)));
                }
                return type;
            } else {
                return 0;
            }
        }

        // AP4_AudioSampleDescription accessors
        public uint   GetSampleRate()   { return _sampleRate;   }
        public ushort GetSampleSize()   { return _sampleSize;   }
        public ushort GetChannelCount() { return _channelCount; }

        // AP4_AudioSampleDescription members
        protected uint   _sampleRate;
        protected ushort _sampleSize;
        protected ushort _channelCount;
    }

    /*----------------------------------------------------------------------
     |   AP4_Ac3SampleDescription
     +---------------------------------------------------------------------*/
    public class Ac3SampleDescription : SampleDescription,
                                        AudioSampleDescription
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_Ac3SampleDescription::AP4_Ac3SampleDescription
        +---------------------------------------------------------------------*/
        public Ac3SampleDescription(uint      sample_rate,
                                    ushort    sample_size,
                                    ushort    channel_count,
                                    Dac3Atom  dac3):
            base(Type.TYPE_AC3, Ap4.SAMPLE_FORMAT_AC_3, null)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            _dac3Atom = null;
            if (dac3 != null) {
                _dac3Atom = new Dac3Atom(dac3);
                _details.AddChild(_dac3Atom);
            } else {
                // TODO: add default construtor, m_Dac3Atom = new AP4_Dac3Atom() / should never happen
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac3SampleDescription::AP4_Ac3SampleDescription
        +---------------------------------------------------------------------*/
        public Ac3SampleDescription(uint        sample_rate,
                                    ushort      sample_size,
                                    ushort      channel_count,
                                    AtomParent  details) :
            base(Type.TYPE_AC3, Ap4.SAMPLE_FORMAT_AC_3, details)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            _dac3Atom = null;
            Dac3Atom dac3 = _details.GetChild(AtomType.DAC3) as Dac3Atom;
            if (dac3 != null) {
                _dac3Atom = dac3;
            } else {
                // TODO: add default construtor, m_Dac3Atom = new AP4_Dac3Atom() / should never happen
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac3SampleDescription::AP4_Ac3SampleDescription
        +---------------------------------------------------------------------*/
        public Ac3SampleDescription(uint                sample_rate,
                                    ushort              sample_size,
                                    ushort              channel_count,
                                    uint                size, // DSI size
                                    Dac3Atom.StreamInfo ac3_stream_info):
            base(Type.TYPE_AC3, Ap4.SAMPLE_FORMAT_AC_3, null)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            _dac3Atom = new Dac3Atom(ac3_stream_info);
            _details.AddChild(_dac3Atom);
        }

        // inherited from AP4_SampleDescription
        public override Ap4Result GetCodecString(out string codec) { codec = "ac-3"; return Ap4Result.SUCCESS; }

        /*----------------------------------------------------------------------
        |   AP4_Ac3SampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new Ac3SampleEntry(_format,
                                      _sampleRate<<16,
                                      _sampleSize,
                                      _channelCount,
                                      _details);
        }

        // AP4_AudioSampleDescription accessors
        public uint   GetSampleRate()   { return _sampleRate;   }
        public ushort GetSampleSize()   { return _sampleSize;   }
        public ushort GetChannelCount() { return _channelCount; }

        // AP4_AudioSampleDescription members
        protected uint   _sampleRate;
        protected ushort _sampleSize;
        protected ushort _channelCount;

        private Dac3Atom _dac3Atom;
    }

    /*----------------------------------------------------------------------
    |   AP4_Eac3SampleDescription
    +---------------------------------------------------------------------*/
    public class Eac3SampleDescription : SampleDescription,
                                         AudioSampleDescription
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_Eac3SampleDescription::AP4_Eac3SampleDescription
        +---------------------------------------------------------------------*/
        public Eac3SampleDescription():
            base(Type.TYPE_EAC3, Ap4.SAMPLE_FORMAT_EC_3, null)
        {
            _sampleRate = 48000;
            _sampleSize = 16;
            _channelCount = 2;
            // PORT NOTE: upstream leaves m_Dec3Atom uninitialized here
            _dec3Atom = null;
            _details.AddChild(new Dec3Atom());
        }

        /*----------------------------------------------------------------------
        |   AP4_Eac3SampleDescription::AP4_Eac3SampleDescription
        +---------------------------------------------------------------------*/
        public Eac3SampleDescription(uint      sample_rate,
                                     ushort    sample_size,
                                     ushort    channel_count,
                                     Dec3Atom  dec3):
            base(Type.TYPE_EAC3, Ap4.SAMPLE_FORMAT_EC_3, null)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            if (dec3 != null) {
                _dec3Atom = new Dec3Atom(dec3);
            } else {
                // shoud never happen
                _dec3Atom = new Dec3Atom();
            }
            _details.AddChild(_dec3Atom);
        }

        /*----------------------------------------------------------------------
        |   AP4_Eac3SampleDescription::AP4_Eac3SampleDescription
        +---------------------------------------------------------------------*/
        public Eac3SampleDescription(uint        sample_rate,
                                     ushort      sample_size,
                                     ushort      channel_count,
                                     AtomParent  details) :
            base(Type.TYPE_EAC3, Ap4.SAMPLE_FORMAT_EC_3, details)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            _dec3Atom = null;
            Dec3Atom eac3 = _details.GetChild(AtomType.DEC3) as Dec3Atom;
            if (eac3 != null) {
                _dec3Atom = new Dec3Atom(eac3);
            } else {
                // shoud never happen
                _dec3Atom = new Dec3Atom();
            }
            _details.AddChild(_dec3Atom);
        }

        /*----------------------------------------------------------------------
        |   AP4_Eac3SampleDescription::AP4_Eac3SampleDescription
        +---------------------------------------------------------------------*/
        public Eac3SampleDescription(uint               sample_rate,
                                     ushort             sample_size,
                                     ushort             channel_count,
                                     uint               size, // DSI size
                                     Dec3Atom.SubStream subStream,
                                     uint               complexity_index_type_a):
            base(Type.TYPE_EAC3, Ap4.SAMPLE_FORMAT_EC_3, null)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            _dec3Atom = new Dec3Atom(size, subStream, complexity_index_type_a);
            _details.AddChild(_dec3Atom);
        }

        // inherited from AP4_SampleDescription
        public override Ap4Result GetCodecString(out string codec) { codec = "ec-3"; return Ap4Result.SUCCESS; }

        /*----------------------------------------------------------------------
        |   AP4_Eac3SampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new Eac3SampleEntry(_format,
                                       _sampleRate<<16,
                                       _sampleSize,
                                       _channelCount,
                                       _details);
        }

        // AP4_AudioSampleDescription accessors
        public uint   GetSampleRate()   { return _sampleRate;   }
        public ushort GetSampleSize()   { return _sampleSize;   }
        public ushort GetChannelCount() { return _channelCount; }

        // AP4_AudioSampleDescription members
        protected uint   _sampleRate;
        protected ushort _sampleSize;
        protected ushort _channelCount;

        private Dec3Atom _dec3Atom;
    }

    /*----------------------------------------------------------------------
    |   AP4_Ac4SampleDescription
    +---------------------------------------------------------------------*/
    public class Ac4SampleDescription : SampleDescription,
                                        AudioSampleDescription
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_Ac4SampleDescription::AP4_Ac4SampleDescription
        +---------------------------------------------------------------------*/
        public Ac4SampleDescription(uint      sample_rate,
                                    ushort    sample_size,
                                    ushort    channel_count,
                                    Dac4Atom  dac4):
            base(Type.TYPE_AC4, Ap4.SAMPLE_FORMAT_AC_4, null)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            _dac4Atom = null;
            if (dac4 != null) {
                _dac4Atom = dac4.CloneConst();
                _details.AddChild(_dac4Atom);
            } else {
                // TODO: add default construtor, m_Dac4Atom = new AP4_Dac4Atom() / should never happen
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac4SampleDescription::AP4_Ac4SampleDescription
        +---------------------------------------------------------------------*/
        public Ac4SampleDescription(uint        sample_rate,
                                    ushort      sample_size,
                                    ushort      channel_count,
                                    AtomParent  details) :
            base(Type.TYPE_AC4, Ap4.SAMPLE_FORMAT_AC_4, details)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            _dac4Atom = null;
            // (upstream looks for a child of type AP4_ATOM_TYPE_AC_4 here, not DAC4)
            Dac4Atom ac4 = _details.GetChild(AtomType.AC_4) as Dac4Atom;
            if (ac4 != null) {
                _dac4Atom = ac4;
            } else {
                // TODO: add default construtor, m_Dac4Atom = new AP4_Dac4Atom() / should never happen
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac4SampleDescription::AP4_Ac4SampleDescription
        +---------------------------------------------------------------------*/
        public Ac4SampleDescription(uint            sample_rate,
                                    ushort          sample_size,
                                    ushort          channel_count,
                                    uint            size, // DSI size
                                    Dac4Atom.Ac4Dsi ac4Dsi):
            base(Type.TYPE_AC4, Ap4.SAMPLE_FORMAT_AC_4, null)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            _dac4Atom = new Dac4Atom(size, ac4Dsi);
            _details.AddChild(_dac4Atom);
        }

        // inherited from AP4_SampleDescription
        public override Ap4Result GetCodecString(out string codec) {
            // PORT NOTE: upstream dereferences m_Dac4Atom without a check (it is
            // NULL when the constructor found no dac4 atom); the port returns an
            // empty string in that case.
            codec = "";
            if (_dac4Atom != null) _dac4Atom.GetCodecString(out codec);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac4SampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new Ac4SampleEntry(_format,
                                      _sampleRate<<16,
                                      _sampleSize,
                                      _channelCount,
                                      _details);
        }

        // AP4_AudioSampleDescription accessors
        public uint   GetSampleRate()   { return _sampleRate;   }
        public ushort GetSampleSize()   { return _sampleSize;   }
        public ushort GetChannelCount() { return _channelCount; }

        // AP4_AudioSampleDescription members
        protected uint   _sampleRate;
        protected ushort _sampleSize;
        protected ushort _channelCount;

        private Dac4Atom _dac4Atom;
    }

    /*----------------------------------------------------------------------
    |   AP4_TrueHdSampleDescription
    +---------------------------------------------------------------------*/
    public class TrueHdSampleDescription : SampleDescription,
                                           AudioSampleDescription
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_TrueHdSampleDescription::AP4_TrueHdSampleDescription
        +---------------------------------------------------------------------*/
        public TrueHdSampleDescription(uint      sample_rate,
                                       ushort    sample_size,
                                       ushort    channel_count,
                                       DmlpAtom  dmlp):
            base(Type.TYPE_TRUEHD, Ap4.SAMPLE_FORMAT_MLPA, null)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            _dmlpAtom = null;
            if (dmlp != null) {
                _dmlpAtom = new DmlpAtom(dmlp);
                _details.AddChild(_dmlpAtom);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrueHdSampleDescription::AP4_TrueHdSampleDescription
        +---------------------------------------------------------------------*/
        public TrueHdSampleDescription(uint        sample_rate,
                                       ushort      sample_size,
                                       ushort      channel_count,
                                       AtomParent  details) :
            base(Type.TYPE_TRUEHD, Ap4.SAMPLE_FORMAT_MLPA, details)
        {
            _sampleRate = sample_rate;
            _sampleSize = sample_size;
            _channelCount = channel_count;
            DmlpAtom dmlp = _details.GetChild(AtomType.DMLP) as DmlpAtom;
            if (dmlp != null) {
                _dmlpAtom = new DmlpAtom(dmlp);
            } else {
                _dmlpAtom = new DmlpAtom(0, 0);
            }
            _details.AddChild(_dmlpAtom);
        }

        // inherited from AP4_SampleDescription
        public override Ap4Result GetCodecString(out string codec) {
            // PORT NOTE: upstream dereferences m_DmlpAtom without a check (it is
            // NULL when the first constructor got no dmlp atom); the port
            // returns an empty string in that case.
            codec = "";
            if (_dmlpAtom != null) _dmlpAtom.GetCodecString(out codec);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrueHdSampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new MlpSampleEntry(_format,
                                      _sampleRate,
                                      _sampleSize,
                                      _channelCount,
                                      _details);
        }

        // AP4_AudioSampleDescription accessors
        public uint   GetSampleRate()   { return _sampleRate;   }
        public ushort GetSampleSize()   { return _sampleSize;   }
        public ushort GetChannelCount() { return _channelCount; }

        // AP4_AudioSampleDescription members
        protected uint   _sampleRate;
        protected ushort _sampleSize;
        protected ushort _channelCount;

        private DmlpAtom _dmlpAtom;
    }

    /*----------------------------------------------------------------------
    |   AP4_MpegVideoSampleDescription
    +---------------------------------------------------------------------*/
    public class MpegVideoSampleDescription : MpegSampleDescription,
                                              VideoSampleDescription
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_MpegVideoSampleDescription::AP4_MpegVideoSampleDescription
        +---------------------------------------------------------------------*/
        public MpegVideoSampleDescription(
            ushort    width,
            ushort    height,
            ushort    depth,
            string    compressor_name,
            EsdsAtom  esds) :

            base(AtomType.MP4V, esds)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
        }

        /*----------------------------------------------------------------------
        |   AP4_MpegVideoSampleDescription::AP4_MpegVideoSampleDescription
        +---------------------------------------------------------------------*/
        public MpegVideoSampleDescription(
            byte        oti,
            ushort      width,
            ushort      height,
            ushort      depth,
            string      compressor_name,
            DataBuffer  decoder_info,
            uint        buffer_size,
            uint        max_bitrate,
            uint        avg_bitrate) :
            base(AtomType.MP4V,
                 Ap4.STREAM_TYPE_VISUAL,
                 oti,
                 decoder_info,
                 buffer_size,
                 max_bitrate,
                 avg_bitrate)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _compressorName = compressor_name ?? "";
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MpegVideoSampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new Mp4vSampleEntry(_width,
                                       _height,
                                       _depth,
                                       _compressorName,
                                       CreateEsDescriptor());
        }

        // AP4_VideoSampleDescription accessors
        public ushort GetWidth()          { return _width;  }
        public ushort GetHeight()         { return _height; }
        public ushort GetDepth()          { return _depth;  }
        public string GetCompressorName() { return _compressorName; }

        // AP4_VideoSampleDescription members
        protected ushort _width;
        protected ushort _height;
        protected ushort _depth;
        protected string _compressorName;
    }

    /*----------------------------------------------------------------------
    |   AP4_SubtitleSampleDescription
    +---------------------------------------------------------------------*/
    public class SubtitleSampleDescription : SampleDescription
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleDescription::AP4_SubtitleSampleDescription
        +---------------------------------------------------------------------*/
        public SubtitleSampleDescription(uint    format,
                                         string  namespce,
                                         string schema_location,
                                         string image_mime_type) :
            base(SampleDescription.Type.TYPE_SUBTITLES, format, null)
        {
            _namespace = namespce ?? "";
            _schemaLocation = schema_location ?? "";
            _imageMimeType = image_mime_type ?? "";
        }

        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleDescription::Clone
        +---------------------------------------------------------------------*/
        public override SampleDescription Clone(out Ap4Result result)
        {
            result = Ap4Result.SUCCESS;
            return new SubtitleSampleDescription(_format,
                                                 _namespace,
                                                 _schemaLocation,
                                                 _imageMimeType);
        }

        /*----------------------------------------------------------------------
        |   AP4_SubtitleSampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            return new SubtitleSampleEntry(_format,
                                           _namespace,
                                           _schemaLocation,
                                           _imageMimeType);
        }

        // accessor
        public string GetNamespace()      { return _namespace;      }
        public string GetSchemaLocation() { return _schemaLocation; }
        public string GetImageMimeType()  { return _imageMimeType;  }

        private string _namespace;
        private string _schemaLocation;
        private string _imageMimeType;
    }
}
