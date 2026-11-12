// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AvccAtom.h, Ap4AvccAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte AVC_PROFILE_BASELINE = 66;
        public const byte AVC_PROFILE_MAIN     = 77;
        public const byte AVC_PROFILE_EXTENDED = 88;
        public const byte AVC_PROFILE_HIGH     = 100;
        public const byte AVC_PROFILE_HIGH_10  = 110;
        public const byte AVC_PROFILE_HIGH_422 = 122;
        public const byte AVC_PROFILE_HIGH_444 = 144;
    }

    /*----------------------------------------------------------------------
    |   AP4_AvccAtom
    +---------------------------------------------------------------------*/
    public class AvccAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_AvccAtom::GetProfileName
        +---------------------------------------------------------------------*/
        public static string GetProfileName(byte profile)
        {
            switch (profile) {
                case Ap4.AVC_PROFILE_BASELINE: return "Baseline";
                case Ap4.AVC_PROFILE_MAIN:     return "Main";
                case Ap4.AVC_PROFILE_EXTENDED: return "Extended";
                case Ap4.AVC_PROFILE_HIGH:     return "High";
                case Ap4.AVC_PROFILE_HIGH_10:  return "High 10";
                case Ap4.AVC_PROFILE_HIGH_422: return "High 4:2:2";
                case Ap4.AVC_PROFILE_HIGH_444: return "High 4:4:4";
            }

            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_AvccAtom::Create
        +---------------------------------------------------------------------*/
        public static AvccAtom Create(uint size, ByteStream stream)
        {
            // read the raw bytes in a buffer
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            DataBuffer payload_data = new DataBuffer(payload_size);
            Ap4Result result = stream.Read(payload_data.UseData(), payload_size);
            if (result.Failed()) return null;

            // check the version
            byte[] payload = payload_data.GetData();
            // PORT NOTE: upstream reads payload[0] before checking the size (out of
            // bounds read when the payload is empty); an empty payload is rejected
            // by the size check just below anyway.
            if (payload_size == 0) return null;
            if (payload[0] != 1) {
                return null;
            }

            // check the size
            if (payload_size < 6) return null;
            uint num_seq_params = (uint)(payload[5]&31);
            uint cursor = 6;
            for (uint i=0; i<num_seq_params; i++) {
                if (cursor+2 > payload_size) return null;
                cursor = (uint)(cursor + (2+Ap4Utils.BytesToInt16BE(payload, (int)cursor)));
                if (cursor > payload_size) return null;
            }
            if (cursor+1 > payload_size) return null;
            uint num_pic_params = payload[cursor++];
            for (uint i=0; i<num_pic_params; i++) {
                if (cursor+2 > payload_size) return null;
                cursor = (uint)(cursor + (2+Ap4Utils.BytesToInt16BE(payload, (int)cursor)));
                if (cursor > payload_size) return null;
            }
            return new AvccAtom(size, payload);
        }

        /*----------------------------------------------------------------------
        |   AP4_AvccAtom::AP4_AvccAtom
        +---------------------------------------------------------------------*/
        public AvccAtom() :
            base(AtomType.AVCC, Ap4.ATOM_HEADER_SIZE)
        {
            _configurationVersion = 1;
            _profile = 0;
            _level = 0;
            _profileCompatibility = 0;
            _naluLengthSize = 0;
            _chromaFormat = 0;
            _bitDepthLumaMinus8 = 0;
            _bitDepthChromaMinus8 = 0;
            UpdateRawBytes();
            _size32 += _rawBytes.GetDataSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_AvccAtom::AP4_AvccAtom
        +---------------------------------------------------------------------*/
        public AvccAtom(AvccAtom other) :
            base(AtomType.AVCC, other._size32)
        {
            _configurationVersion = other._configurationVersion;
            _profile = other._profile;
            _level = other._level;
            _profileCompatibility = other._profileCompatibility;
            _naluLengthSize = other._naluLengthSize;
            // PORT NOTE: upstream leaves m_ChromaFormat, m_BitDepthLumaMinus8 and
            // m_BitDepthChromaMinus8 uninitialized here; they are 0 in the port.
            _rawBytes = new DataBuffer(other._rawBytes);

            // deep copy of the parameters
            int i = 0;
            for (i=0; i<other._sequenceParameters.Count; i++) {
                _sequenceParameters.Add(new DataBuffer(other._sequenceParameters[i]));
            }
            for (i=0; i<other._pictureParameters.Count; i++) {
                _pictureParameters.Add(new DataBuffer(other._pictureParameters[i]));
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AvccAtom::AP4_AvccAtom
        +---------------------------------------------------------------------*/
        private AvccAtom(uint size, byte[] payload) :
            base(AtomType.AVCC, size)
        {
            // PORT NOTE: when returning early, upstream leaves the members
            // uninitialized; they are 0 in the port.
            if (size < Ap4.ATOM_HEADER_SIZE + 7) {
                return;
            }

            // make a copy of our configuration bytes
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            _rawBytes.SetData(payload, payload_size);

            // parse the payload
            _configurationVersion = payload[0];
            _profile              = payload[1];
            _profileCompatibility = payload[2];
            _level                = payload[3];
            _naluLengthSize       = (byte)(1+(payload[4]&3));
            byte num_seq_params = (byte)(payload[5]&31);
            _sequenceParameters.EnsureCapacity(num_seq_params);
            uint cursor = 6;
            for (uint i=0; i<num_seq_params; i++) {
                if (cursor+2 <= payload_size) {
                    ushort param_length = (ushort)Ap4Utils.BytesToInt16BE(payload, (int)cursor);
                    cursor += 2;
                    if (cursor + param_length <= payload_size) {
                        _sequenceParameters.Add(new DataBuffer());
                        _sequenceParameters[_sequenceParameters.Count-1].SetData(payload, (int)cursor, param_length);
                        cursor += param_length;
                    }
                }
            }

            if (cursor >= payload_size) {
                return;
            }
            byte num_pic_params = payload[cursor++];
            _pictureParameters.EnsureCapacity(num_pic_params);
            for (uint i=0; i<num_pic_params; i++) {
                if (cursor+2 <= payload_size) {
                    ushort param_length = (ushort)Ap4Utils.BytesToInt16BE(payload, (int)cursor);
                    cursor += 2;
                    if (cursor + param_length <= payload_size) {
                        _pictureParameters.Add(new DataBuffer());
                        _pictureParameters[_pictureParameters.Count-1].SetData(payload, (int)cursor, param_length);
                        cursor += param_length;
                    }
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AvccAtom::AP4_AvccAtom
        +---------------------------------------------------------------------*/
        public AvccAtom(byte             profile,
                        byte             level,
                        byte             profile_compatibility,
                        byte             length_size,
                        byte             chroma_format,
                        byte             bit_depth_luma_minus8,
                        byte             bit_depth_chroma_minus8,
                        List<DataBuffer> sequence_parameters,
                        List<DataBuffer> picture_parameters) :
            base(AtomType.AVCC, Ap4.ATOM_HEADER_SIZE)
        {
            _configurationVersion = 1;
            _profile = profile;
            _level = level;
            _profileCompatibility = profile_compatibility;
            _naluLengthSize = length_size;
            _chromaFormat = chroma_format;
            _bitDepthLumaMinus8 = bit_depth_luma_minus8;
            _bitDepthChromaMinus8 = bit_depth_chroma_minus8;

            // deep copy of the parameters
            int i = 0;
            for (i=0; i<sequence_parameters.Count; i++) {
                _sequenceParameters.Add(new DataBuffer(sequence_parameters[i]));
            }
            for (i=0; i<picture_parameters.Count; i++) {
                _pictureParameters.Add(new DataBuffer(picture_parameters[i]));
            }

            // compute the raw bytes
            UpdateRawBytes();

            // update the size
            _size32 += _rawBytes.GetDataSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_AvccAtom::UpdateRawBytes
        +---------------------------------------------------------------------*/
        private void UpdateRawBytes()
        {
            // compute the payload size
            uint payload_size = 6;
            for (int i=0; i<_sequenceParameters.Count; i++) {
                payload_size += 2+_sequenceParameters[i].GetDataSize();
            }
            ++payload_size;
            for (int i=0; i<_pictureParameters.Count; i++) {
                payload_size += 2+_pictureParameters[i].GetDataSize();
            }
            if ((_profile == Ap4.AVC_PROFILE_HIGH) || (_profile == Ap4.AVC_PROFILE_HIGH_10) ||
                (_profile == Ap4.AVC_PROFILE_HIGH_422) || (_profile == Ap4.AVC_PROFILE_HIGH_444)) {
                    payload_size += 4;
            }
            _rawBytes.SetDataSize(payload_size);
            byte[] payload = _rawBytes.UseData();

            payload[0] = _configurationVersion;
            payload[1] = _profile;
            payload[2] = _profileCompatibility;
            payload[3] = _level;
            payload[4] = (byte)(0xFC | (_naluLengthSize-1));
            payload[5] = (byte)(0xE0 | (byte)_sequenceParameters.Count);
            uint cursor = 6;
            for (int i=0; i<_sequenceParameters.Count; i++) {
                ushort param_length = (ushort)_sequenceParameters[i].GetDataSize();
                Ap4Utils.BytesFromUInt16BE(payload, (int)cursor, param_length);
                cursor += 2;
                Array.Copy(_sequenceParameters[i].GetData(), 0, payload, (int)cursor, param_length);
                cursor += param_length;
            }
            payload[cursor++] = (byte)_pictureParameters.Count;
            for (int i=0; i<_pictureParameters.Count; i++) {
                ushort param_length = (ushort)_pictureParameters[i].GetDataSize();
                Ap4Utils.BytesFromUInt16BE(payload, (int)cursor, param_length);
                cursor += 2;
                Array.Copy(_pictureParameters[i].GetData(), 0, payload, (int)cursor, param_length);
                cursor += param_length;
            }
            if ((_profile == Ap4.AVC_PROFILE_HIGH) || (_profile == Ap4.AVC_PROFILE_HIGH_10) ||
                (_profile == Ap4.AVC_PROFILE_HIGH_422) || (_profile == Ap4.AVC_PROFILE_HIGH_444)) {
                payload[cursor++] = (byte)(0xfc | (0x03 & _chromaFormat));
                payload[cursor++] = (byte)(0xf8 | (0x07 & _bitDepthLumaMinus8));
                payload[cursor++] = (byte)(0xf8 | (0x07 & _bitDepthChromaMinus8));
                payload[cursor] = 0;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AvccAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return stream.Write(_rawBytes.GetData(), _rawBytes.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_AvccAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("Configuration Version", _configurationVersion);
            string profile_name = GetProfileName(_profile);
            if (profile_name != null) {
                inspector.AddField("Profile", profile_name);
            } else {
                inspector.AddField("Profile", _profile);
            }
            inspector.AddField("Profile Compatibility", _profileCompatibility, AtomInspector.FormatHint.HINT_HEX);
            inspector.AddField("Level", _level);
            inspector.AddField("NALU Length Size", _naluLengthSize);
            for (int i=0; i<_sequenceParameters.Count; i++) {
                inspector.AddField("Sequence Parameter", _sequenceParameters[i].GetData(), _sequenceParameters[i].GetDataSize());
            }
            for (int i=0; i<_pictureParameters.Count; i++) {
                inspector.AddField("Picture Parameter", _pictureParameters[i].GetData(), _pictureParameters[i].GetDataSize());
            }
            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte GetConfigurationVersion() { return _configurationVersion; }
        public byte GetProfile()              { return _profile; }
        public byte GetLevel()                { return _level; }
        public byte GetProfileCompatibility() { return _profileCompatibility; }
        public byte GetNaluLengthSize()       { return _naluLengthSize; }
        public List<DataBuffer> GetSequenceParameters() { return _sequenceParameters; }
        public List<DataBuffer> GetPictureParameters()  { return _pictureParameters; }
        public DataBuffer GetRawBytes()       { return _rawBytes; }
        public byte GetChromaFormat()         { return _chromaFormat; }
        public byte GetBitDepthLumaMinus8()   { return _bitDepthLumaMinus8; }
        public byte GetBitDepthChromaMinus8() { return _bitDepthChromaMinus8; }

        // members
        private byte             _configurationVersion;
        private byte             _profile;
        private byte             _level;
        private byte             _profileCompatibility;
        private byte             _naluLengthSize;
        private byte             _chromaFormat;
        private byte             _bitDepthLumaMinus8;
        private byte             _bitDepthChromaMinus8;
        private List<DataBuffer> _sequenceParameters = new List<DataBuffer>();
        private List<DataBuffer> _pictureParameters = new List<DataBuffer>();
        private DataBuffer       _rawBytes = new DataBuffer();
    }
}
