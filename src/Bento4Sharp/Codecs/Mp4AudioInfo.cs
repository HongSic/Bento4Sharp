// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Codecs/Ap4Mp4AudioInfo.h, Ap4Mp4AudioInfo.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Mp4AudioDsiParser
    +---------------------------------------------------------------------*/
    internal class Mp4AudioDsiParser
    {
        public Mp4AudioDsiParser(byte[] data, uint data_size)
        {
            _data     = new DataBuffer(data, data_size);
            _position = 0;
        }

        public uint BitsLeft() { return 8*_data.GetDataSize()-_position; }
        public uint ReadBits(uint n)
        {
            uint result = 0;
            byte[] data = _data.GetData();
            uint data_size = _data.GetDataSize();
            while (n != 0) {
                uint bits_avail = 8-(_position%8);
                uint chunk_size = bits_avail >= n ? n : bits_avail;
                // PORT NOTE: upstream reads out of bounds when asked for more bits
                // than BitsLeft() (two call sites do not check); the port reads
                // zeros instead.
                uint data_byte = (_position/8) < data_size ? data[_position/8] : 0u;
                uint chunk_bits = (data_byte>>(int)(bits_avail-chunk_size))&((1u<<(int)chunk_size)-1);
                result = (result << (int)chunk_size) | chunk_bits;
                n -= chunk_size;
                _position += chunk_size;
            }

            return result;
        }

        private DataBuffer _data;
        private uint       _position;
    }

    /*----------------------------------------------------------------------
    |   AP4_Mp4AudioDecoderConfig
    +---------------------------------------------------------------------*/
    /// <summary>
    /// Detailed AAC decoder configuration information.
    /// This information is necessary in order to create a decoder object.
    /// It is normally obtained from the DecoderSpecificInfo field of the
    /// DecoderConfigDescriptor descriptor carried in the sample description
    /// for the audio samples. See 14496-1, subpart 2, p 2.6.6 for details.
    /// </summary>
    public class Mp4AudioDecoderConfig
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const uint AAC_MAX_SAMPLING_FREQUENCY_INDEX = 12;
        private static readonly uint[] AacSamplingFreqTable = new uint[13]
        {
            96000, 88200, 64000, 48000,
            44100, 32000, 24000, 22050,
            16000, 12000, 11025, 8000,
            7350
        };

        /// <summary>
        /// Channel configuration for multichannel audio buffers.
        /// </summary>
        public enum ChannelConfiguration {
            /// <summary>No channel (not used)</summary>
            CHANNEL_CONFIG_NONE   = 0,
            /// <summary>Mono (single audio channel)</summary>
            CHANNEL_CONFIG_MONO   = 1,
            /// <summary>Stereo (Two audio channels)</summary>
            CHANNEL_CONFIG_STEREO = 2,
            /// <summary>Stereo plus one center channel</summary>
            CHANNEL_CONFIG_STEREO_PLUS_CENTER = 3,
            /// <summary>Stereo plus one center and one read channel</summary>
            CHANNEL_CONFIG_STEREO_PLUS_CENTER_PLUS_REAR_MONO = 4,
            /// <summary>Five channels</summary>
            CHANNEL_CONFIG_FIVE = 5,
            /// <summary>Five channels plus one low frequency channel</summary>
            CHANNEL_CONFIG_FIVE_PLUS_ONE = 6,
            /// <summary>Seven channels plus one low frequency channel</summary>
            CHANNEL_CONFIG_SEVEN_PLUS_ONE = 7,
            CHANNEL_CONFIG_UNSUPPORTED
        }

        /// <summary>Extension details</summary>
        // PORT NOTE: anonymous struct upstream (the type of m_Extension)
        public class Extension
        {
            /// <summary>SBR is present</summary>
            public bool _sbrPresent;
            /// <summary>PS is present</summary>
            public bool _psPresent;
            /// <summary>Extension object type</summary>
            public byte _objectType;
            /// <summary>Sampling frequency index of the extension</summary>
            public uint _samplingFrequencyIndex;
            /// <summary>Sampling frequency of the extension</summary>
            public uint _samplingFrequency;
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_Mp4AudioDecoderConfig::AP4_Mp4AudioDecoderConfig
        +---------------------------------------------------------------------*/
        public Mp4AudioDecoderConfig()
        {
            Reset();
        }

        /*----------------------------------------------------------------------
        |   AP4_Mp4AudioDecoderConfig::Reset
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Reset all members to default values (0)
        /// </summary>
        public void Reset()
        {
            _objectType             = 0;
            _samplingFrequencyIndex = 0;
            _samplingFrequency      = 0;
            _channelCount           = 0;
            _channelConfiguration   = ChannelConfiguration.CHANNEL_CONFIG_NONE;
            _frameLengthFlag        = false;
            _dependsOnCoreCoder     = false;
            _coreCoderDelay         = 0;
            _extension._sbrPresent = false;
            _extension._psPresent  = false;
            _extension._objectType = 0;
            _extension._samplingFrequencyIndex = 0;
            _extension._samplingFrequency      = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mp4AudioDecoderConfig::ParseAudioObjectType
        +---------------------------------------------------------------------*/
        private Ap4Result ParseAudioObjectType(Mp4AudioDsiParser parser, ref byte object_type)
        {
            if (parser.BitsLeft() < 5) return Ap4Result.ERROR_INVALID_FORMAT;
            object_type = (byte)parser.ReadBits(5);
            if ((int)object_type == 31) {
                if (parser.BitsLeft() < 6) return Ap4Result.ERROR_INVALID_FORMAT;
                object_type = (byte)(32 + parser.ReadBits(6));
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mp4AudioDecoderConfig::ParseExtension
        +---------------------------------------------------------------------*/
        private Ap4Result ParseExtension(Mp4AudioDsiParser parser)
        {
            if (parser.BitsLeft() < 16) return Ap4Result.ERROR_INVALID_FORMAT;
            uint sync_extension_type = parser.ReadBits(11);
            if (sync_extension_type == 0x2b7) {
                Ap4Result result = ParseAudioObjectType(parser, ref _extension._objectType);
                if (result.Failed()) return result;
                if (_extension._objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_SBR) {
                    if (parser.BitsLeft() < 1) return Ap4Result.ERROR_INVALID_FORMAT;
                    _extension._sbrPresent = (parser.ReadBits(1) == 1);
                    if (_extension._sbrPresent) {
                        result = ParseSamplingFrequency(parser,
                                                        ref _extension._samplingFrequencyIndex,
                                                        ref _extension._samplingFrequency);
                        if (result.Failed()) return result;
                        if (parser.BitsLeft() >= 12) {
                            sync_extension_type = parser.ReadBits(11);
                            if (sync_extension_type == 0x548) {
                                _extension._psPresent = (parser.ReadBits(1) == 1);
                            }
                        }
                    }
                } else if (_extension._objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_BSAC) {
                    if (parser.BitsLeft() < 1+4) return Ap4Result.ERROR_INVALID_FORMAT;
                    _extension._sbrPresent = (parser.ReadBits(1) == 1);
                    if (_extension._sbrPresent) {
                        result = ParseSamplingFrequency(parser,
                                                        ref _extension._samplingFrequencyIndex,
                                                        ref _extension._samplingFrequency);
                        if (result.Failed()) return result;
                    }
                    parser.ReadBits(4); // extensionChannelConfiguration
                }
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mp4AudioDecoderConfig::ParseGASpecificInfo
        +---------------------------------------------------------------------*/
        private Ap4Result ParseGASpecificInfo(Mp4AudioDsiParser parser)
        {
            if (parser.BitsLeft() < 2) return Ap4Result.ERROR_INVALID_FORMAT;
            _frameLengthFlag = (parser.ReadBits(1) == 1);
            _dependsOnCoreCoder = (parser.ReadBits(1) == 1);
            if (_dependsOnCoreCoder) {
                if (parser.BitsLeft() < 14) return Ap4Result.ERROR_INVALID_FORMAT;
                _coreCoderDelay = parser.ReadBits(14);
            } else {
                _coreCoderDelay = 0;
            }
            if (parser.BitsLeft() < 1) return Ap4Result.ERROR_INVALID_FORMAT;
            uint extensionFlag = parser.ReadBits(1);
            if (_channelConfiguration == ChannelConfiguration.CHANNEL_CONFIG_NONE) {
                /*program_config_element (); */
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }
            if (_objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_SCALABLE ||
                _objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_SCALABLE) {
                if (parser.BitsLeft() < 3) return Ap4Result.ERROR_INVALID_FORMAT;
                parser.ReadBits(3); // layerNr
            }
            if (extensionFlag != 0) {
                if (_objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_BSAC) {
                    if (parser.BitsLeft() < 16) return Ap4Result.ERROR_INVALID_FORMAT;
                    parser.ReadBits(16); // numOfSubFrame (5); layer_length (11)
                }
                if (_objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LC       ||
                    _objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_SCALABLE ||
                    _objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LD) {
                    if (parser.BitsLeft() < 3) return Ap4Result.ERROR_INVALID_FORMAT;
                    parser.ReadBits(3); // aacSectionDataResilienceFlag (1)
                                        // aacScalefactorDataResilienceFlag (1)
                                        // aacSpectralDataResilienceFlag (1)
                }
                if (parser.BitsLeft() < 1) return Ap4Result.ERROR_INVALID_FORMAT;
                uint extensionFlag3 = parser.ReadBits(1);
                if (extensionFlag3 != 0) {
                    return Ap4Result.ERROR_NOT_SUPPORTED;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mp4AudioDecoderConfig::ParseSamplingFrequency
        +---------------------------------------------------------------------*/
        private Ap4Result ParseSamplingFrequency(Mp4AudioDsiParser parser,
                                                 ref uint          sampling_frequency_index,
                                                 ref uint          sampling_frequency)
        {
            if (parser.BitsLeft() < 4) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            sampling_frequency_index = parser.ReadBits(4);
            if (sampling_frequency_index == 0xF) {
                if (parser.BitsLeft() < 24) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                sampling_frequency = parser.ReadBits(24);
            } else if (sampling_frequency_index <= AAC_MAX_SAMPLING_FREQUENCY_INDEX) {
                sampling_frequency = AacSamplingFreqTable[sampling_frequency_index];
            } else {
                sampling_frequency = 0;
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mp4AudioDecoderConfig::Parse
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Parser a DecoderSpecificInfo buffer
        /// </summary>
        public Ap4Result Parse(byte[] data,
                               uint   data_size)
        {
            Ap4Result         result;
            Mp4AudioDsiParser bits = new Mp4AudioDsiParser(data, data_size);

            // default config
            Reset();

            // parse the audio object type
            result = ParseAudioObjectType(bits, ref _objectType);
            if (result.Failed()) return result;

            // parse the sampling frequency
            result = ParseSamplingFrequency(bits,
                                            ref _samplingFrequencyIndex,
                                            ref _samplingFrequency);
            if (result.Failed()) return result;

            if (bits.BitsLeft() < 4) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            _channelConfiguration = (ChannelConfiguration)bits.ReadBits(4);
            _channelCount = (uint)_channelConfiguration;
            if (_channelCount == 7) {
                _channelCount = 8;
            } else if (_channelCount > 7) {
                _channelCount = 0;
            }

            if (_objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_SBR ||
                _objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_PS) {
                _extension._objectType = Ap4.MPEG4_AUDIO_OBJECT_TYPE_SBR;
                _extension._sbrPresent = true;
                _extension._psPresent  = _objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_PS;
                result = ParseSamplingFrequency(bits,
                                                ref _extension._samplingFrequencyIndex,
                                                ref _extension._samplingFrequency);
                if (result.Failed()) return result;
                result = ParseAudioObjectType(bits, ref _objectType);
                if (result.Failed()) return result;
                if (_objectType == Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_BSAC) {
                    if (bits.BitsLeft() < 4) return Ap4Result.ERROR_INVALID_FORMAT;
                    bits.ReadBits(4); // extensionChannelConfiguration (4)
                }
            } else {
                _extension._objectType             = 0;
                _extension._samplingFrequency      = 0;
                _extension._samplingFrequencyIndex = 0;
                _extension._sbrPresent             = false;
                _extension._psPresent              = false;
            }

            switch (_objectType) {
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_MAIN:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_LC:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_SSR:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_LTP:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_SCALABLE:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_TWINVQ:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LC:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LTP:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_SCALABLE:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_AAC_LD:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_TWINVQ:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_ER_BSAC:
                case Ap4.MPEG4_AUDIO_OBJECT_TYPE_USAC:
                    result = ParseGASpecificInfo(bits);
                    if (result == Ap4Result.SUCCESS) {
                        if (_extension._objectType !=  Ap4.MPEG4_AUDIO_OBJECT_TYPE_SBR &&
                            bits.BitsLeft() >= 16) {
                            result = ParseExtension(bits);
                        }
                    }
                    if (result == Ap4Result.ERROR_NOT_SUPPORTED) {
                        // not a fatal error
                        result = Ap4Result.SUCCESS;
                    }
                    if (result != Ap4Result.SUCCESS) return result;
                    break;

                default:
                    return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            return Ap4Result.SUCCESS;
        }

        // members
        /// <summary>Type identifier for the audio data</summary>
        public byte                 _objectType;
        /// <summary>Index of the sampling frequency in the sampling frequency table</summary>
        public uint                 _samplingFrequencyIndex;
        /// <summary>Sampling frequency</summary>
        public uint                 _samplingFrequency;
        /// <summary>Number of audio channels</summary>
        public uint                 _channelCount;
        /// <summary>Channel configuration</summary>
        public ChannelConfiguration _channelConfiguration;
        /// <summary>Frame Length Flag</summary>
        public bool                 _frameLengthFlag;
        /// <summary>Depends on Core Coder</summary>
        public bool                 _dependsOnCoreCoder;
        /// <summary>Core Code delay</summary>
        public uint                 _coreCoderDelay;
        /// <summary>Extension details</summary>
        public readonly Extension   _extension = new Extension();
    }
}
