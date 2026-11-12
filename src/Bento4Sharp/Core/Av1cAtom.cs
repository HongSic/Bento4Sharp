// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Av1cAtom.h, Ap4Av1cAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte AV1_PROFILE_MAIN         = 0;
        public const byte AV1_PROFILE_HIGH         = 1;
        public const byte AV1_PROFILE_PROFESSIONAL = 2;
    }

    /*----------------------------------------------------------------------
    |   AP4_Av1cAtom
    +---------------------------------------------------------------------*/
    public class Av1cAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_Av1cAtom::GetProfileName
        +---------------------------------------------------------------------*/
        public static string GetProfileName(byte profile)
        {
            switch (profile) {
                case Ap4.AV1_PROFILE_MAIN:         return "Main";
                case Ap4.AV1_PROFILE_HIGH:         return "High";
                case Ap4.AV1_PROFILE_PROFESSIONAL: return "Professional";
                default:                           return null;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Av1cAtom::Create
        +---------------------------------------------------------------------*/
        public static Av1cAtom Create(uint size, ByteStream stream)
        {
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            if (payload_size < 4) {
                return null;
            }

            byte[] bits = new byte[4];
            Ap4Result result = stream.Read(bits, 4);
            if (result.Failed()) {
                return null;
            }

            byte version                            = (byte)(bits[0] & 0x7F);
            byte seq_profile                        = (byte)(bits[1] >> 5);
            byte seq_level_idx_0                    = (byte)(bits[1] & 0x1F);
            byte seq_tier_0                         = (byte)((bits[2] >> 7) & 1);
            byte high_bitdepth                      = (byte)((bits[2] >> 6) & 1);
            byte twelve_bit                         = (byte)((bits[2] >> 5) & 1);
            byte monochrome                         = (byte)((bits[2] >> 4) & 1);
            byte chroma_subsampling_x               = (byte)((bits[2] >> 3) & 1);
            byte chroma_subsampling_y               = (byte)((bits[2] >> 2) & 1);
            byte chroma_sample_position             = (byte)((bits[2]     ) & 3);
            byte initial_presentation_delay_present = (byte)((bits[3] >> 4) & 1);
            byte initial_presentation_delay_minus_one;
            if (initial_presentation_delay_present != 0) {
                initial_presentation_delay_minus_one = (byte)((bits[3] >> 4) & 0x0F);
            } else {
                initial_presentation_delay_minus_one = 0;
            }

            DataBuffer config_obus = new DataBuffer();
            if (payload_size > 4) {
                config_obus.SetDataSize(payload_size - 4);
                result = stream.Read(config_obus.UseData(), payload_size - 4);
                if (result.Failed()) {
                    return null;
                }
            }
            return new Av1cAtom(version,
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
                                config_obus.GetData(),
                                config_obus.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_Av1cAtom::AP4_Av1cAtom
        +---------------------------------------------------------------------*/
        public Av1cAtom() :
            base(AtomType.AV1C, Ap4.ATOM_HEADER_SIZE)
        {
            _av1cVersion = 1;
            _seqProfile = 0;
            _seqLevelIdx0 = 0;
            _seqTier0 = 0;
            _highBitDepth = 0;
            _twelveBit = 0;
            _monochrome = 0;
            _chromaSubsamplingX = 0;
            _chromaSubsamplingY = 0;
            _chromaSamplePosition = 0;
            _initialPresentationDelayPresent = 0;
            _initialPresentationDelayMinusOne = 0;
            _size32 += 4;
        }

        /*----------------------------------------------------------------------
        |   AP4_Av1cAtom::AP4_Av1cAtom
        +---------------------------------------------------------------------*/
        public Av1cAtom(byte    version,
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
            base(AtomType.AV1C, Ap4.ATOM_HEADER_SIZE)
        {
            _av1cVersion = version;
            _seqProfile = seq_profile;
            _seqLevelIdx0 = seq_level_idx_0;
            _seqTier0 = seq_tier_0;
            _highBitDepth = high_bitdepth;
            _twelveBit = twelve_bit;
            _monochrome = monochrome;
            _chromaSubsamplingX = chroma_subsampling_x;
            _chromaSubsamplingY = chroma_subsampling_y;
            _chromaSamplePosition = chroma_sample_position;
            _initialPresentationDelayPresent = initial_presentation_delay_present;
            _initialPresentationDelayMinusOne = initial_presentation_delay_minus_one;
            _size32 += 4 + config_obus_size;
            if (config_obus != null && config_obus_size != 0) {
                _configObus.SetData(config_obus, config_obus_size);
            }
        }

        // PORT NOTE: the private constructor AP4_Av1cAtom(AP4_UI32 size, const AP4_UI08* payload)
        // is declared but never defined upstream; it is not ported.

        /*----------------------------------------------------------------------
        |   AP4_Av1cAtom::AP4_Av1cAtom (implicit C++ copy constructor)
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream has no user-written copy constructor; callers
        // (`new AP4_Av1cAtom(*av1c)` in Ap4SampleDescription.cpp) use the implicit
        // member-wise one, which copies every AP4_Atom member as well, INCLUDING
        // m_Parent. So, as upstream, the copy of an atom that has a parent also
        // "has" that parent (AddChild() of the copy then fails with
        // ERROR_INVALID_PARAMETERS). m_Children is not copied (a leaf atom has none).
        public Av1cAtom(Av1cAtom other) :
            base(other._type, other._size32)
        {
            _size64  = other._size64;
            _isFull  = other._isFull;
            _version = other._version;
            _flags   = other._flags;
            _parent  = other._parent;
            _av1cVersion = other._av1cVersion;
            _seqProfile = other._seqProfile;
            _seqLevelIdx0 = other._seqLevelIdx0;
            _seqTier0 = other._seqTier0;
            _highBitDepth = other._highBitDepth;
            _twelveBit = other._twelveBit;
            _monochrome = other._monochrome;
            _chromaSubsamplingX = other._chromaSubsamplingX;
            _chromaSubsamplingY = other._chromaSubsamplingY;
            _chromaSamplePosition = other._chromaSamplePosition;
            _initialPresentationDelayPresent = other._initialPresentationDelayPresent;
            _initialPresentationDelayMinusOne = other._initialPresentationDelayMinusOne;
            _configObus = new DataBuffer(other._configObus);
        }

        /*----------------------------------------------------------------------
        |   AP4_Av1cAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            byte[] bits = new byte[4];
            bits[0] = (byte)((1 << 7) | _av1cVersion);
            bits[1] = (byte)((_seqProfile << 5) | _seqLevelIdx0);
            bits[2] = (byte)((_seqTier0           << 7) |
                             (_highBitDepth       << 6) |
                             (_twelveBit          << 5) |
                             (_monochrome         << 4) |
                             (_chromaSubsamplingX << 3) |
                             (_chromaSubsamplingY << 2) |
                             (_chromaSamplePosition   ));
            bits[3] = (byte)((_initialPresentationDelayPresent << 4) | _initialPresentationDelayMinusOne);
            Ap4Result result = stream.Write(bits, 4);
            if (result.Failed()) return result;
            if (_configObus.GetDataSize() != 0) {
                result = stream.Write(_configObus.GetData(), _configObus.GetDataSize());
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Av1cAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("version",                    _av1cVersion);
            inspector.AddField("seq_profile",                _seqProfile);
            inspector.AddField("seq_level_idx_0",            _seqLevelIdx0);
            inspector.AddField("seq_tier_0",                 _seqTier0);
            inspector.AddField("high_bitdepth",              _highBitDepth);
            inspector.AddField("twelve_bit",                 _twelveBit);
            inspector.AddField("monochrome",                 _monochrome);
            inspector.AddField("chroma_subsampling_x",       _chromaSubsamplingX);
            inspector.AddField("chroma_subsampling_y",       _chromaSubsamplingY);
            inspector.AddField("chroma_sample_position",     _chromaSamplePosition);
            inspector.AddField("initial_presentation_delay", (ulong)(_initialPresentationDelayPresent != 0 ? _initialPresentationDelayMinusOne + 1 : 0));
            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte GetSeqProfile()                       { return _seqProfile; }
        public byte GetSeqLevelIdx0()                     { return _seqLevelIdx0; }
        public byte GetSeqTier0()                         { return _seqTier0; }
        public byte GetHighBitDepth()                     { return _highBitDepth; }
        public byte GetTwelveBit()                        { return _twelveBit; }
        public byte GetMonochrome()                       { return _monochrome; }
        public byte GetChromaSubsamplingX()               { return _chromaSubsamplingX; }
        public byte GetChromaSubsamplingY()               { return _chromaSubsamplingY; }
        public byte GetChromaSamplePosition()             { return _chromaSamplePosition; }
        public byte GetInitialPresentationDelayPresent()  { return _initialPresentationDelayPresent; }
        public byte GetInitialPresentationDelayMinusOne() { return _initialPresentationDelayMinusOne; }
        public DataBuffer GetConfigObus()                 { return _configObus; }

        // members
        // PORT NOTE: m_Version upstream, where it hides AP4_Atom::m_Version (the
        // full-atom version, unused here: av1C is not a full atom). Renamed so
        // that the port does not shadow Atom._version.
        private byte       _av1cVersion;
        private byte       _seqProfile;
        private byte       _seqLevelIdx0;
        private byte       _seqTier0;
        private byte       _highBitDepth;
        private byte       _twelveBit;
        private byte       _monochrome;
        private byte       _chromaSubsamplingX;
        private byte       _chromaSubsamplingY;
        private byte       _chromaSamplePosition;
        private byte       _initialPresentationDelayPresent;
        private byte       _initialPresentationDelayMinusOne;
        private DataBuffer _configObus = new DataBuffer();
    }
}
