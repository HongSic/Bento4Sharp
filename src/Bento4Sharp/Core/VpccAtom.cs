// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4VpccAtom.h, Ap4VpccAtom.cpp
// Original work Copyright 2002-2020 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_VpccAtom
    +---------------------------------------------------------------------*/
    public class VpccAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_VpccAtom::Create
        +---------------------------------------------------------------------*/
        public static VpccAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            uint payload_size = size - Ap4.FULL_ATOM_HEADER_SIZE;
            if (payload_size < 8) {
                return null;
            }

            byte profile;
            stream.ReadUI08(out profile);
            byte level;
            stream.ReadUI08(out level);
            byte @byte;
            stream.ReadUI08(out @byte);
            byte bit_depth = (byte)((@byte >> 4) & 0XF);
            byte chroma_subsampling = (byte)((@byte >> 1) & 7);
            bool video_full_range_flag = (@byte & 1) != 0;
            byte colour_primaries;
            stream.ReadUI08(out colour_primaries);
            byte transfer_characteristics;
            stream.ReadUI08(out transfer_characteristics);
            byte matrix_coefficients;
            stream.ReadUI08(out matrix_coefficients);
            ushort codec_initialization_data_size = 0;
            stream.ReadUI16(out codec_initialization_data_size);
            if (codec_initialization_data_size > payload_size - 8) {
                return null;
            }
            // PORT NOTE: as upstream, the codec initialization data is never read
            // from the stream: the buffer is only sized (its content is
            // uninitialized memory upstream, zeros in the port).
            DataBuffer codec_initialization_data = new DataBuffer();
            Ap4Result result = codec_initialization_data.SetDataSize(codec_initialization_data_size);
            if (result.Failed()) {
                return null;
            }
            return new VpccAtom(profile,
                                level,
                                bit_depth,
                                chroma_subsampling,
                                video_full_range_flag,
                                colour_primaries,
                                transfer_characteristics,
                                matrix_coefficients,
                                codec_initialization_data.GetData(),
                                codec_initialization_data.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_VpccAtom::AP4_VpccAtom
        +---------------------------------------------------------------------*/
        public VpccAtom(byte    profile,
                        byte    level,
                        byte    bit_depth,
                        byte    chroma_subsampling,
                        bool    videofull_range_flag,
                        byte    colour_primaries,
                        byte    transfer_characteristics,
                        byte    matrix_coefficients,
                        byte[]  codec_initialization_data,
                        uint    codec_initialization_data_size) :
            base(AtomType.VPCC, Ap4.FULL_ATOM_HEADER_SIZE + 8 + codec_initialization_data_size, 1, 0)
        {
            _profile = profile;
            _level = level;
            _bitDepth = bit_depth;
            _chromaSubsampling = chroma_subsampling;
            _videoFullRangeFlag = videofull_range_flag;
            _colourPrimaries = colour_primaries;
            _transferCharacteristics = transfer_characteristics;
            _matrixCoefficients = matrix_coefficients;
            if (codec_initialization_data != null && codec_initialization_data_size != 0) {
                _codecIntializationData.SetData(codec_initialization_data, codec_initialization_data_size);
            }
        }

        // PORT NOTE: the private constructor AP4_VpccAtom(AP4_UI32 size, const AP4_UI08* payload)
        // is declared but never defined upstream; it is not ported.

        /*----------------------------------------------------------------------
        |   AP4_VpccAtom::GetCodecString
        +---------------------------------------------------------------------*/
        public Ap4Result GetCodecString(uint container_type, out string codec)
        {
            string type_name = Ap4Utils.FormatFourChars(container_type);

            // "%s.%02d.%02d.%02d.%02d.%02d.%02d.%02d.%02d"
            string @string = type_name + "." +
                             _profile.ToString("D2", CultureInfo.InvariantCulture) + "." +
                             _level.ToString("D2", CultureInfo.InvariantCulture) + "." +
                             _bitDepth.ToString("D2", CultureInfo.InvariantCulture) + "." +
                             _chromaSubsampling.ToString("D2", CultureInfo.InvariantCulture) + "." +
                             _colourPrimaries.ToString("D2", CultureInfo.InvariantCulture) + "." +
                             _transferCharacteristics.ToString("D2", CultureInfo.InvariantCulture) + "." +
                             _matrixCoefficients.ToString("D2", CultureInfo.InvariantCulture) + "." +
                             (_videoFullRangeFlag ? 1 : 0).ToString("D2", CultureInfo.InvariantCulture);

            codec = @string;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_VpccAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            stream.WriteUI08(_profile);
            stream.WriteUI08(_level);
            stream.WriteUI08((byte)((_bitDepth << 4) | (_chromaSubsampling << 1) | (_videoFullRangeFlag ? 1 : 0)));
            stream.WriteUI08(_colourPrimaries);
            stream.WriteUI08(_transferCharacteristics);
            stream.WriteUI08(_matrixCoefficients);
            stream.WriteUI16((ushort)_codecIntializationData.GetDataSize());
            stream.Write(_codecIntializationData.GetData(), _codecIntializationData.GetDataSize());

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_VpccAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("profile", _profile);
            inspector.AddField("level", _level);
            inspector.AddField("bit depth", _bitDepth);
            inspector.AddField("chroma subsampling", _chromaSubsampling);
            inspector.AddField("video full range flag", (ulong)(_videoFullRangeFlag ? 1 : 0));
            inspector.AddField("colour primaries", _colourPrimaries);
            inspector.AddField("matrix coefficients", _matrixCoefficients);
            inspector.AddField("codec initialization data",
                               _codecIntializationData.GetData(),
                               _codecIntializationData.GetDataSize());
            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte       GetProfile()                 { return _profile;                 }
        public byte       GetLevel()                   { return _level;                   }
        public byte       GetBitDepth()                { return _bitDepth;                }
        public byte       GetChromaSubsampling()       { return _chromaSubsampling;       }
        public bool       GetVideoFullRangeFlag()      { return _videoFullRangeFlag;      }
        public byte       GetColourPrimaries()         { return _colourPrimaries;         }
        public byte       GetTransferCharacteristics() { return _transferCharacteristics; }
        public byte       GetMatrixCoefficients()      { return _matrixCoefficients;      }
        public DataBuffer GetCodecInitializationData() { return _codecIntializationData;  }

        // members
        private byte       _profile;
        private byte       _level;
        private byte       _bitDepth;
        private byte       _chromaSubsampling;
        private bool       _videoFullRangeFlag;
        private byte       _colourPrimaries;
        private byte       _transferCharacteristics;
        private byte       _matrixCoefficients;
        private DataBuffer _codecIntializationData = new DataBuffer();
    }
}
