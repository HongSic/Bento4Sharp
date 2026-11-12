// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Codecs/Ap4AdtsParser.h, Ap4AdtsParser.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint ADTS_HEADER_SIZE  = 7;
        public const uint ADTS_SYNC_MASK    = 0xFFF6; /* 12 sync bits plus 2 layer bits */
        public const uint ADTS_SYNC_PATTERN = 0xFFF0; /* 12 sync bits=1 layer=0         */

        public static readonly uint[] AdtsSamplingFrequencyTable = new uint[16]
        {
            96000,
            88200,
            64000,
            48000,
            44100,
            32000,
            24000,
            22050,
            16000,
            12000,
            11025,
            8000,
            7350,
            0,      /* Reserved */
            0,      /* Reserved */
            0       /* Escape code */
        };
    }

    /*----------------------------------------------------------------------
    |   types
    +---------------------------------------------------------------------*/
    public class AdtsHeader
    {
        // constructor
        public AdtsHeader(byte[] bytes)
        {
            // fixed part
            _id                     = (uint)(( bytes[1] & 0x08) >> 3);
            _protectionAbsent       = (uint)(  bytes[1] & 0x01);
            _profileObjectType      = (uint)(( bytes[2] & 0xC0) >> 6);
            _samplingFrequencyIndex = (uint)(( bytes[2] & 0x3C) >> 2);
            _channelConfiguration   = (uint)(((bytes[2] & 0x01) << 2) |
                                             ((bytes[3] & 0xC0) >> 6));

            // variable part
            _frameLength = ((uint)(bytes[3] & 0x03) << 11) |
                           ((uint)(bytes[4]       ) <<  3) |
                           ((uint)(bytes[5] & 0xE0) >>  5);
            _rawDataBlocks = (uint)(bytes[6] & 0x03);
        }

        // methods
        public Ap4Result Check()
        {
            // check that the sampling frequency index is valid
            if (_samplingFrequencyIndex >= 0xD) {
                return Ap4Result.FAILURE;
            }

            /* MPEG2 does not use all profiles */
            if (_id == 1 && _profileObjectType == 3) {
                return Ap4Result.FAILURE;
            }

            return Ap4Result.SUCCESS;
        }

        // members

        // fixed part
        public uint _id;
        public uint _protectionAbsent;
        public uint _profileObjectType;
        public uint _samplingFrequencyIndex;
        public uint _channelConfiguration;

        // variable part
        public uint _frameLength;
        public uint _rawDataBlocks;

        // class methods
        /*
         * Check that two fixed headers are the same
         */
        public static bool MatchFixed(byte[] a, byte[] b)
        {
            if (a[0]         ==  b[0] &&
                a[1]         ==  b[1] &&
                a[2]         ==  b[2] &&
               (a[3] & 0xF0) == (b[3] & 0xF0)) {
                return true;
            } else {
                return false;
            }
        }
    }

    public enum AacStandard {
        AAC_STANDARD_MPEG2,
        AAC_STANDARD_MPEG4
    }

    public enum AacProfile {
        AAC_PROFILE_MAIN,
        AAC_PROFILE_LC,
        AAC_PROFILE_SSR,
        AAC_PROFILE_LTP
    }

    // PORT NOTE: upstream C structs; classes here (filled in place by AdtsParser.FindFrame)
    public class AacFrameInfo {
        public AacStandard _standard;
        public AacProfile  _profile;
        public uint        _samplingFrequencyIndex;
        public uint        _samplingFrequency;
        public uint        _channelConfiguration;
        public uint        _frameLength;
    }

    public class AacFrame {
        public BitStream    _source;
        public AacFrameInfo _info = new AacFrameInfo();
    }

    public class AdtsParser
    {
        // constructor
        public AdtsParser()
        {
            _bits = new BitStream();
            _frameCount = 0;
        }

        // methods
        public Ap4Result Reset()
        {
            _frameCount = 0;

            return Ap4Result.SUCCESS;
        }

        public Ap4Result Feed(byte[]   buffer,
                              ref uint buffer_size,
                              uint     flags = 0)
        {
            return Feed(buffer, 0, ref buffer_size, flags);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer.
        // Upstream takes `AP4_Size* buffer_size` (in: bytes offered, out: bytes accepted), which may
        // be NULL; a `ref uint` cannot be null, so that case is covered by buffer == null.
        public Ap4Result Feed(byte[]   buffer,
                              int      offset,
                              ref uint buffer_size,
                              uint     flags = 0)
        {
            uint free_space;

            /* update flags */
            _bits._flags = flags;

            /* possible shortcut */
            if (buffer == null ||
                buffer_size == 0) {
                return Ap4Result.SUCCESS;
            }

            /* see how much data we can write */
            free_space = _bits.GetBytesFree();
            if (buffer_size > free_space) buffer_size = free_space;
            if (buffer_size == 0) return Ap4Result.SUCCESS;

            /* write the data */
            return _bits.WriteBytes(buffer, offset, buffer_size);
        }

        public Ap4Result FindFrame(AacFrame frame)
        {
            uint       available;
            byte[]     raw_header = new byte[Ap4.ADTS_HEADER_SIZE];
            Ap4Result  result;

            /* align to the start of the next byte */
            _bits.ByteAlign();

            /* find a frame header */
            result = FindHeader(raw_header);
            if (result.Failed()) return result;

            /* parse the header */
            AdtsHeader adts_header = new AdtsHeader(raw_header);

            /* check the header */
            result = adts_header.Check();
            if (result.Failed()) goto fail;

            /* check if we have enough data to peek at the next header */
            available = _bits.GetBytesAvailable();
            if (available >= adts_header._frameLength+Ap4.ADTS_HEADER_SIZE) {
                // enough to peek at the header of the next frame
                byte[] peek_raw_header = new byte[Ap4.ADTS_HEADER_SIZE];

                _bits.SkipBytes(adts_header._frameLength);
                _bits.PeekBytes(peek_raw_header, Ap4.ADTS_HEADER_SIZE);
                _bits.SkipBytes(unchecked((uint)(-((int)adts_header._frameLength))));

                /* check the header */
                AdtsHeader peek_adts_header = new AdtsHeader(peek_raw_header);
                result = peek_adts_header.Check();
                if (result.Failed()) goto fail;

                /* check that the fixed part of this header is the same as the */
                /* fixed part of the previous header                           */
                if (!AdtsHeader.MatchFixed(peek_raw_header, raw_header)) {
                    goto fail;
                }
            } else if (available < adts_header._frameLength || (_bits._flags & Ap4.BITSTREAM_FLAG_EOS) == 0) {
                // not enough for a frame, or not at the end (in which case we'll want to peek at the next header)
                return Ap4Result.ERROR_NOT_ENOUGH_DATA;
            }

            _bits.SkipBytes(Ap4.ADTS_HEADER_SIZE);

            /* fill in the frame info */
            frame._info._standard = (adts_header._id == 1 ?
                                     AacStandard.AAC_STANDARD_MPEG2 :
                                     AacStandard.AAC_STANDARD_MPEG4);
            switch (adts_header._profileObjectType) {
                case 0:
                    frame._info._profile = AacProfile.AAC_PROFILE_MAIN;
                    break;

                case 1:
                    frame._info._profile = AacProfile.AAC_PROFILE_LC;
                    break;

                case 2:
                    frame._info._profile = AacProfile.AAC_PROFILE_SSR;
                    break;

                case 3:
                    frame._info._profile = AacProfile.AAC_PROFILE_LTP;
                    break;
            }

            frame._info._frameLength = adts_header._frameLength-Ap4.ADTS_HEADER_SIZE;
            frame._info._channelConfiguration = adts_header._channelConfiguration;
            frame._info._samplingFrequencyIndex = adts_header._samplingFrequencyIndex;
            frame._info._samplingFrequency = Ap4.AdtsSamplingFrequencyTable[adts_header._samplingFrequencyIndex];

            /* skip crc if present */
            if (adts_header._protectionAbsent == 0) {
                _bits.SkipBits(16);
            }

            /* set the frame source */
            frame._source = _bits;

            return Ap4Result.SUCCESS;

        fail:
            /* skip the header and return (only skip the first byte in  */
            /* case this was a false header that hides one just after)  */
            //m_Bits.SkipBytes(-(AP4_ADTS_HEADER_SIZE-1));
            return Ap4.ERROR_CORRUPTED_BITSTREAM;
        }

        // PORT NOTE: AP4_AdtsParser::Skip is declared upstream but never defined; not ported.

        public uint GetBytesFree()
        {
            return (_bits.GetBytesFree());
        }

        public uint GetBytesAvailable()
        {
            return (_bits.GetBytesAvailable());
        }

        // methods
        private Ap4Result FindHeader(byte[] header)
        {
            uint available = _bits.GetBytesAvailable();

            /* look for the sync pattern */
            while (available-- >= Ap4.ADTS_HEADER_SIZE) {
                _bits.PeekBytes(header, 2);

                if ((((uint)(header[0] << 8) | header[1]) & Ap4.ADTS_SYNC_MASK) == Ap4.ADTS_SYNC_PATTERN) {
                    /* found a sync pattern, read the entire the header */
                    _bits.PeekBytes(header, Ap4.ADTS_HEADER_SIZE);

                    return Ap4Result.SUCCESS;
                } else {
                    _bits.SkipBytes(1); // skip
                }
            }

            return Ap4Result.ERROR_NOT_ENOUGH_DATA;
        }

        // members
        private BitStream _bits;
        private uint      _frameCount;
    }
}
