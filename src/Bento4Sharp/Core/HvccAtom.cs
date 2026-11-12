// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4HvccAtom.h, Ap4HvccAtom.cpp
// Original work Copyright 2002-2016 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte HEVC_PROFILE_MAIN               = 1;
        public const byte HEVC_PROFILE_MAIN_10            = 2;
        public const byte HEVC_PROFILE_MAIN_STILL_PICTURE = 3;
        public const byte HEVC_PROFILE_REXT               = 4;

        public const byte HEVC_CHROMA_FORMAT_MONOCHROME = 0;
        public const byte HEVC_CHROMA_FORMAT_420        = 1;
        public const byte HEVC_CHROMA_FORMAT_422        = 2;
        public const byte HEVC_CHROMA_FORMAT_444        = 3;
    }

    /*----------------------------------------------------------------------
    |   AP4_HvccAtom
    +---------------------------------------------------------------------*/
    public class HvccAtom : Atom
    {
        // types
        public class Sequence
        {
            public Sequence() {}

            // PORT NOTE: upstream Sequence is a value type that is deep-copied when
            // appended to an AP4_Array; this copy constructor does the same.
            public Sequence(Sequence other)
            {
                _arrayCompleteness = other._arrayCompleteness;
                _reserved          = other._reserved;
                _naluType          = other._naluType;
                for (int i=0; i<other._nalus.Count; i++) {
                    _nalus.Add(new DataBuffer(other._nalus[i]));
                }
            }

            public byte             _arrayCompleteness;
            public byte             _reserved;
            public byte             _naluType;
            public List<DataBuffer> _nalus = new List<DataBuffer>();
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::GetProfileName
        +---------------------------------------------------------------------*/
        public static string GetProfileName(byte profile_space, byte profile)
        {
            if (profile_space != 0) {
                return null;
            }
            switch (profile) {
                case Ap4.HEVC_PROFILE_MAIN:               return "Main";
                case Ap4.HEVC_PROFILE_MAIN_10:            return "Main 10";
                case Ap4.HEVC_PROFILE_MAIN_STILL_PICTURE: return "Main Still Picture";
                case Ap4.HEVC_PROFILE_REXT:               return "Rext";
            }

            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::GetChromaFormatName
        +---------------------------------------------------------------------*/
        public static string GetChromaFormatName(byte chroma_format)
        {
            switch (chroma_format) {
                case Ap4.HEVC_CHROMA_FORMAT_MONOCHROME: return "Monochrome";
                case Ap4.HEVC_CHROMA_FORMAT_420:        return "4:2:0";
                case Ap4.HEVC_CHROMA_FORMAT_422:        return "4:2:2";
                case Ap4.HEVC_CHROMA_FORMAT_444:        return "4:4:4";
            }

            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::Create
        +---------------------------------------------------------------------*/
        public static HvccAtom Create(uint size, ByteStream stream)
        {
            // read the raw bytes in a buffer
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            DataBuffer payload_data = new DataBuffer(payload_size);
            Ap4Result result = stream.Read(payload_data.UseData(), payload_size);
            if (result.Failed()) return null;

            return new HvccAtom(size, payload_data.GetData());
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::AP4_HvccAtom
        +---------------------------------------------------------------------*/
        public HvccAtom() :
            base(AtomType.HVCC, Ap4.ATOM_HEADER_SIZE)
        {
            _configurationVersion = 1;
            _generalProfileSpace = 0;
            _generalTierFlag = 0;
            _generalProfile = 0;
            _generalProfileCompatibilityFlags = 0;
            _generalConstraintIndicatorFlags = 0;
            _generalLevel = 0;
            _reserved1 = 0;
            _minSpatialSegmentation = 0;
            _reserved2 = 0;
            _parallelismType = 0;
            _reserved3 = 0;
            _chromaFormat = 0;
            _reserved4 = 0;
            _lumaBitDepth = 8;
            _reserved5 = 0;
            _chromaBitDepth = 8;
            _averageFrameRate = 0;
            _constantFrameRate = 0;
            _numTemporalLayers = 0;
            _temporalIdNested = 0;
            _naluLengthSize = 4;
            UpdateRawBytes();
            _size32 += _rawBytes.GetDataSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::AP4_HvccAtom
        +---------------------------------------------------------------------*/
        public HvccAtom(HvccAtom other) :
            base(AtomType.HVCC, other._size32)
        {
            _configurationVersion = other._configurationVersion;
            _generalProfileSpace = other._generalProfileSpace;
            _generalTierFlag = other._generalTierFlag;
            _generalProfile = other._generalProfile;
            _generalProfileCompatibilityFlags = other._generalProfileCompatibilityFlags;
            _generalConstraintIndicatorFlags = other._generalConstraintIndicatorFlags;
            _generalLevel = other._generalLevel;
            _reserved1 = other._reserved1;
            _minSpatialSegmentation = other._minSpatialSegmentation;
            _reserved2 = other._reserved2;
            _parallelismType = other._parallelismType;
            _reserved3 = other._reserved3;
            _chromaFormat = other._chromaFormat;
            _reserved4 = other._reserved4;
            _lumaBitDepth = other._lumaBitDepth;
            _reserved5 = other._reserved5;
            _chromaBitDepth = other._chromaBitDepth;
            _averageFrameRate = other._averageFrameRate;
            _constantFrameRate = other._constantFrameRate;
            _numTemporalLayers = other._numTemporalLayers;
            _temporalIdNested = other._temporalIdNested;
            _naluLengthSize = other._naluLengthSize;
            _rawBytes = new DataBuffer(other._rawBytes);

            // deep copy of the parameters
            int i = 0;
            for (i=0; i<other._sequences.Count; i++) {
                _sequences.Add(new Sequence(other._sequences[i]));
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::AP4_HvccAtom
        +---------------------------------------------------------------------*/
        public HvccAtom(byte             general_profile_space,
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
            base(AtomType.HVCC, Ap4.ATOM_HEADER_SIZE)
        {
            _configurationVersion = 1;
            _generalProfileSpace = general_profile_space;
            _generalTierFlag = general_tier_flag;
            _generalProfile = general_profile;
            _generalProfileCompatibilityFlags = general_profile_compatibility_flags;
            _generalConstraintIndicatorFlags = general_constraint_indicator_flags;
            _generalLevel = general_level;
            _reserved1 = 0;
            _minSpatialSegmentation = min_spatial_segmentation;
            _reserved2 = 0;
            _parallelismType = parallelism_type;
            _reserved3 = 0;
            _chromaFormat = chroma_format;
            _reserved4 = 0;
            _lumaBitDepth = luma_bit_depth;
            _reserved5 = 0;
            _chromaBitDepth = chroma_bit_depth;
            _averageFrameRate = average_frame_rate;
            _constantFrameRate = constant_frame_rate;
            _numTemporalLayers = num_temporal_layers;
            _temporalIdNested = temporal_id_nested;
            _naluLengthSize = nalu_length_size;

            // deep copy of the parameters
            // PORT NOTE: Sequence::m_Reserved is left uninitialized upstream; 0 here.
            HvccAtom.Sequence vps_sequence = new HvccAtom.Sequence();
            vps_sequence._naluType = (byte)Ap4.HEVC_NALU_TYPE_VPS_NUT;
            vps_sequence._arrayCompleteness = video_parameters_completeness;
            for (int i=0; i<video_parameters.Count; i++) {
                vps_sequence._nalus.Add(new DataBuffer(video_parameters[i]));
            }
            if (vps_sequence._nalus.Count != 0) {
                _sequences.Add(new Sequence(vps_sequence));
            }

            HvccAtom.Sequence sps_sequence = new HvccAtom.Sequence();
            sps_sequence._naluType = (byte)Ap4.HEVC_NALU_TYPE_SPS_NUT;
            sps_sequence._arrayCompleteness = sequence_parameters_completeness;
            for (int i=0; i<sequence_parameters.Count; i++) {
                sps_sequence._nalus.Add(new DataBuffer(sequence_parameters[i]));
            }
            if (sps_sequence._nalus.Count != 0) {
                _sequences.Add(new Sequence(sps_sequence));
            }

            HvccAtom.Sequence pps_sequence = new HvccAtom.Sequence();
            pps_sequence._naluType = (byte)Ap4.HEVC_NALU_TYPE_PPS_NUT;
            pps_sequence._arrayCompleteness = picture_parameters_completeness;
            for (int i=0; i<picture_parameters.Count; i++) {
                pps_sequence._nalus.Add(new DataBuffer(picture_parameters[i]));
            }
            if (pps_sequence._nalus.Count != 0) {
                _sequences.Add(new Sequence(pps_sequence));
            }

            UpdateRawBytes();
            _size32 += _rawBytes.GetDataSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::AP4_HvccAtom
        +---------------------------------------------------------------------*/
        private HvccAtom(uint size, byte[] payload) :
            base(AtomType.HVCC, size)
        {
            // make a copy of our configuration bytes
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;

            // keep a raw copy
            // PORT NOTE: when returning early, upstream leaves the members
            // uninitialized; they are 0 in the port.
            if (payload_size < 23) return;
            _rawBytes.SetData(payload, payload_size);

            // parse the payload
            _configurationVersion   = payload[0];
            _generalProfileSpace    = (byte)((payload[1]>>6) & 0x03);
            _generalTierFlag        = (byte)((payload[1]>>5) & 0x01);
            _generalProfile         = (byte)((payload[1]   ) & 0x1F);
            _generalProfileCompatibilityFlags = Ap4Utils.BytesToUInt32BE(payload, 2);
            _generalConstraintIndicatorFlags  = (((ulong)Ap4Utils.BytesToUInt32BE(payload, 6))<<16) | Ap4Utils.BytesToUInt16BE(payload, 10);
            _generalLevel           = payload[12];
            _reserved1              = (byte)((payload[13]>>4) & 0x0F);
            _minSpatialSegmentation = (uint)(Ap4Utils.BytesToUInt16BE(payload, 13) & 0x0FFF);
            _reserved2              = (byte)((payload[15]>>2) & 0x3F);
            _parallelismType        = (byte)(payload[15] & 0x03);
            _reserved3              = (byte)((payload[16]>>2) & 0x3F);
            _chromaFormat           = (byte)(payload[16] & 0x03);
            _reserved4              = (byte)((payload[17]>>3) & 0x1F);
            _lumaBitDepth           = (byte)(8+(payload[17] & 0x07));
            _reserved5              = (byte)((payload[18]>>3) & 0x1F);
            _chromaBitDepth         = (byte)(8+(payload[18] & 0x07));
            _averageFrameRate       = Ap4Utils.BytesToUInt16BE(payload, 19);
            _constantFrameRate      = (byte)((payload[21]>>6) & 0x03);
            _numTemporalLayers      = (byte)((payload[21]>>3) & 0x07);
            _temporalIdNested       = (byte)((payload[21]>>2) & 0x01);
            _naluLengthSize         = (byte)(1+(payload[21] & 0x03));

            byte num_seq = payload[22];
            // m_Sequences.SetItemCount(num_seq)
            for (uint i=0; i<num_seq; i++) {
                _sequences.Add(new Sequence());
            }
            uint cursor = 23;
            for (uint i=0; i<num_seq; i++) {
                Sequence seq = _sequences[(int)i];
                if (cursor+1 > payload_size) break;
                seq._arrayCompleteness = (byte)((payload[cursor] >> 7) & 0x01);
                seq._reserved          = (byte)((payload[cursor] >> 6) & 0x01);
                seq._naluType          = (byte)(payload[cursor] & 0x3F);
                cursor += 1;

                if (cursor+2 > payload_size) break;
                ushort nalu_count = Ap4Utils.BytesToUInt16BE(payload, (int)cursor);
                // seq.m_Nalus.SetItemCount(nalu_count)
                for (uint j=0; j<nalu_count; j++) {
                    seq._nalus.Add(new DataBuffer());
                }
                cursor += 2;

                for (uint j=0; j<nalu_count; j++) {
                    if (cursor+2 > payload_size) break;
                    uint nalu_length = Ap4Utils.BytesToUInt16BE(payload, (int)cursor);
                    cursor += 2;
                    if (cursor + nalu_length > payload_size) break;
                    seq._nalus[(int)j].SetData(payload, (int)cursor, nalu_length);
                    cursor += nalu_length;
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::UpdateRawBytes
        +---------------------------------------------------------------------*/
        private void UpdateRawBytes()
        {
            BitWriter bits = new BitWriter(23);
            bits.Write(_configurationVersion, 8);
            bits.Write(_generalProfileSpace, 2);
            bits.Write(_generalTierFlag, 1);
            bits.Write(_generalProfile, 5);
            bits.Write(_generalProfileCompatibilityFlags, 32);
            bits.Write((uint)(_generalConstraintIndicatorFlags>>32), 16);
            bits.Write((uint)(_generalConstraintIndicatorFlags), 32);
            bits.Write(_generalLevel, 8);
            bits.Write(0xFF, 4);
            bits.Write(_minSpatialSegmentation, 12);
            bits.Write(0xFF, 6);
            bits.Write(_parallelismType, 2);
            bits.Write(0xFF, 6);
            bits.Write(_chromaFormat, 2);
            bits.Write(0xFF, 5);
            bits.Write((uint)(_lumaBitDepth >= 8 ? _lumaBitDepth - 8 : 0), 3);
            bits.Write(0xFF, 5);
            bits.Write((uint)(_chromaBitDepth >= 8 ? _chromaBitDepth - 8 : 0), 3);
            bits.Write(_averageFrameRate, 16);
            bits.Write(_constantFrameRate, 2);
            bits.Write(_numTemporalLayers, 3);
            bits.Write(_temporalIdNested, 1);
            bits.Write((uint)(_naluLengthSize > 0 ? _naluLengthSize - 1 : 0), 2);
            bits.Write((uint)_sequences.Count, 8);
            _rawBytes.SetData(bits.GetData(), 23);

            for (int i=0; i<_sequences.Count; i++) {
                byte[] bytes = new byte[3];
                bytes[0] = (byte)((_sequences[i]._arrayCompleteness != 0 ? (1<<7) : 0) | _sequences[i]._naluType);
                Ap4Utils.BytesFromUInt16BE(bytes, 1, (ushort)_sequences[i]._nalus.Count);
                _rawBytes.AppendData(bytes, 3);
                for (int j=0; j<_sequences[i]._nalus.Count; j++) {
                    byte[] size = new byte[2];
                    Ap4Utils.BytesFromUInt16BE(size, 0, (ushort)_sequences[i]._nalus[j].GetDataSize());
                    _rawBytes.AppendData(size, 2);
                    _rawBytes.AppendData(_sequences[i]._nalus[j].GetData(), _sequences[i]._nalus[j].GetDataSize());
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return stream.Write(_rawBytes.GetData(), _rawBytes.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_HvccAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("Configuration Version", _configurationVersion);
            inspector.AddField("Profile Space", _generalProfileSpace);
            string profile_name = GetProfileName(_generalProfileSpace, _generalProfile);
            if (profile_name != null) {
                inspector.AddField("Profile", profile_name);
            } else {
                inspector.AddField("Profile", _generalProfile);
            }
            inspector.AddField("Tier", _generalTierFlag);
            inspector.AddField("Profile Compatibility", _generalProfileCompatibilityFlags, AtomInspector.FormatHint.HINT_HEX);
            inspector.AddField("Constraint", _generalConstraintIndicatorFlags, AtomInspector.FormatHint.HINT_HEX);
            inspector.AddField("Level", _generalLevel);
            inspector.AddField("Min Spatial Segmentation", _minSpatialSegmentation);
            inspector.AddField("Parallelism Type", _parallelismType);
            inspector.AddField("Chroma Format", _chromaFormat);
            inspector.AddField("Chroma Depth", _chromaBitDepth);
            inspector.AddField("Luma Depth", _lumaBitDepth);
            inspector.AddField("Average Frame Rate", _averageFrameRate);
            inspector.AddField("Constant Frame Rate", _constantFrameRate);
            inspector.AddField("Number Of Temporal Layers", _numTemporalLayers);
            inspector.AddField("Temporal Id Nested", _temporalIdNested);
            inspector.AddField("NALU Length Size", _naluLengthSize);
            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte   GetConfigurationVersion()             { return _configurationVersion; }
        public byte   GetGeneralProfileSpace()              { return _generalProfileSpace; }
        public byte   GetGeneralTierFlag()                  { return _generalTierFlag; }
        public byte   GetGeneralProfile()                   { return _generalProfile; }
        public uint   GetGeneralProfileCompatibilityFlags() { return _generalProfileCompatibilityFlags; }
        public ulong  GetGeneralConstraintIndicatorFlags()  { return _generalConstraintIndicatorFlags; }
        public byte   GetGeneralLevel()                     { return _generalLevel; }
        public uint   GetMinSpatialSegmentation()           { return _minSpatialSegmentation; }
        public byte   GetParallelismType()                  { return _parallelismType; }
        public byte   GetChromaFormat()                     { return _chromaFormat; }
        public byte   GetLumaBitDepth()                     { return _lumaBitDepth; }
        public byte   GetChromaBitDepth()                   { return _chromaBitDepth; }
        public ushort GetAverageFrameRate()                 { return _averageFrameRate; }
        public byte   GetConstantFrameRate()                { return _constantFrameRate; }
        public byte   GetNumTemporalLayers()                { return _numTemporalLayers; }
        public byte   GetTemporalIdNested()                 { return _temporalIdNested; }
        public byte   GetNaluLengthSize()                   { return _naluLengthSize; }
        public List<Sequence> GetSequences()                { return _sequences; }
        public DataBuffer GetRawBytes()                     { return _rawBytes; }

        // members
        private byte           _configurationVersion;
        private byte           _generalProfileSpace;
        private byte           _generalTierFlag;
        private byte           _generalProfile;
        private uint           _generalProfileCompatibilityFlags;
        private ulong          _generalConstraintIndicatorFlags;
        private byte           _generalLevel;
        private byte           _reserved1;
        private uint           _minSpatialSegmentation;
        private byte           _reserved2;
        private byte           _parallelismType;
        private byte           _reserved3;
        private byte           _chromaFormat;
        private byte           _reserved4;
        private byte           _lumaBitDepth;
        private byte           _reserved5;
        private byte           _chromaBitDepth;
        private ushort         _averageFrameRate;
        private byte           _constantFrameRate;
        private byte           _numTemporalLayers;
        private byte           _temporalIdNested;
        private byte           _naluLengthSize;
        private List<Sequence> _sequences = new List<Sequence>();
        private DataBuffer     _rawBytes = new DataBuffer();
    }
}
