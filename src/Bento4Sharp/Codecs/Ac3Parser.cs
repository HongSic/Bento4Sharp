// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Codecs/Ap4Ac3Parser.h, Ap4Ac3Parser.cpp
// Original work Copyright 2002-2020 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
     |   constants
     +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint AC3_HEADER_SIZE             = 32;      /* The header size for AC-3 parser, syncinfo() + bsi() */
        public const uint AC3_SYNC_WORD_BIG_ENDIAN    = 0x0B77;  /* Big endian, 16 sync bits */
        public const uint AC3_SYNC_WORD_LITTLE_ENDIAN = 0x770B;  /* Little endian, 16 sync bits */

        // PORT NOTE: the second row is copied verbatim from upstream, including the missing comma
        // ("558696" instead of "558,696"): that row therefore has 37 initializers upstream and its
        // last element is implicitly 0, which is written out here.
        public static readonly uint[,] FRAME_SIZE_CODE_ARY_AC3 = new uint[3,38] {
            {64,64,80,80,96,96,112,112,128,128,160,160,192,192,224,224,256,256,320,320,384,384,448,448,512,512,640,640,768,768,896,896,1024,1024,1152,1152,1280,1280},
            {69,70,87,88,104,105,121,122,139,140,174,175,208,209,243,244,278,279,348,349,417,418,487,488,557,558696,697,835,836,976,977,1114,1115,1253,1254,1393,1394,0},
            {96,96,120,120,144,144,168,168,192,192,240,240,288,288,336,336,384,384,480,480,576,576,672,672,768,768,960,960,1152,1152,1344,1344,1536,1536,1728,1728,1920,1920}};

        public static readonly uint[] FSCOD_AC3 = new uint[4] {48000, 44100, 32000, 0};
    }

    /*----------------------------------------------------------------------
     |   types
     +---------------------------------------------------------------------*/
    public class Ac3Header {
        // constructor
        public Ac3Header(byte[] bytes)
        {
            BitReader bits = new BitReader(bytes, Ap4.AC3_HEADER_SIZE);

            bits.SkipBits(16);                  // sync word
            bits.SkipBits(16);                  // crc1
            _fscod       = bits.ReadBits(2);
            _frmsizecod  = bits.ReadBits(6);    // frmsizecod
            if (_fscod >= (uint)Ap4.FRAME_SIZE_CODE_ARY_AC3.GetLength(0) ||
                _frmsizecod >= (uint)Ap4.FRAME_SIZE_CODE_ARY_AC3.GetLength(1)) {
                _frameSize = 0;
                // invalid frame size code, set bsid to an invalid value to make sure the header check will fail
                _bsid = 0xFF;
                return;
            }
            _frameSize   = Ap4.FRAME_SIZE_CODE_ARY_AC3[_fscod, _frmsizecod] * 2;
            _bsid        = bits.ReadBits(5);
            _bsmod       = bits.ReadBits(3);
            _acmod       = bits.ReadBits(3);
            if((_acmod & 0x1) != 0 && (_acmod != 0x1)){
                bits.SkipBits(2);               //cmixlev
            }
            if((_acmod & 0x4) != 0){
                bits.SkipBits(2);               //surmixlev
            }
            if(_acmod == 0x2){
                bits.SkipBits(2);               //dsurmod
            }
            _lfeon         = (uint)bits.ReadBit();
            if (_acmod >= (uint)Ap4.GLOBAL_CHANNEL_ARY.Length) {
                // invalid acmod, set bsid to an invalid value to make sure the header check will fail
                _bsid = 0xFF;
                return;
            }
            _channelCount  = Ap4.GLOBAL_CHANNEL_ARY[_acmod] + _lfeon;

            bits.SkipBits(5);                   // dialnorm
            if(bits.ReadBit() != 0){            // compre
                bits.SkipBits(8);               // compr
            }
            if(bits.ReadBit() != 0){            // langcode
                bits.SkipBits(8);               // langcod
            }
            if(bits.ReadBit() != 0){            // audprodie
                bits.SkipBits(5);               // mixlevel
                bits.SkipBits(2);               // roomtyp
            }
            if (_acmod == 0){
                bits.SkipBits(5);               // dialnorm2
                if(bits.ReadBit() != 0){        // compr2e
                    bits.SkipBits(8);           // compr2
                }
                if(bits.ReadBit() != 0){        // langcod2e
                    bits.SkipBits(8);           // langcod2
                }
                if(bits.ReadBit() != 0){        // audprodi2e
                    bits.SkipBits(5);           // mixlevel2
                    bits.SkipBits(2);           // roomtyp2
                }
            }
            bits.SkipBits(1);                   // copyrightb
            bits.SkipBits(1);                   // origbs
            if(bits.ReadBit() != 0){            // timecod1e
                bits.SkipBits(14);              // timecod1
            }
            if(bits.ReadBit() != 0){            //timecod2e
                bits.SkipBits(14);              // timecod2
            }
            _addbsie = (uint)bits.ReadBit();
            if (_addbsie != 0){
                _addbsil = bits.ReadBits(6);
                for (uint idx = 0 ; idx < (_addbsil + 1); idx ++){
                    _addbsi[idx] = (byte)bits.ReadBits(8);
                }
            } else {
                _addbsil = 0;
                Array.Clear(_addbsi, 0, _addbsi.Length);
            }
            _headSize = (bits.GetBitsRead() / 8) + ((bits.GetBitsRead() % 8 == 0) ? 0u: 1u);
        }

        // methods
        public Ap4Result Check()
        {
            if (_bsid > 8) {
                return Ap4Result.FAILURE;
            }

            return Ap4Result.SUCCESS;
        }

        // PORT NOTE: members that upstream leaves uninitialized on the early-return paths are 0 here
        public uint _headSize;
        public uint _frameSize;
        public uint _channelCount;

        // AC-3 sysninfo()
        public uint _fscod;
        public uint _frmsizecod;

        // AC-3 bsi()
        public uint _bsid;
        public uint _bsmod;
        public uint _acmod;
        public uint _lfeon;
        public uint _addbsie;
        public uint _addbsil;                    // 6 bits
        public byte[] _addbsi = new byte[65];    // (addbsil+1)×8  (PORT NOTE: `m_addbsi` upstream)

        // class methods
        /*
         * Check that two fixed headers are the same
         */
        public static bool MatchFixed(Ac3Header frame, Ac3Header next_frame)
        {
            return true;
        }
    }

    // PORT NOTE: upstream C structs; classes here (filled in place by Ac3Parser.FindFrame)
    public class Ac3FrameInfo {
        public uint _channelCount;
        public uint _frameSize;
        public uint _sampleRate;
        public Dac3Atom.StreamInfo _ac3StreamInfo;
    }

    public class Ac3Frame {
        public BitStream    _source;
        public Ac3FrameInfo _info = new Ac3FrameInfo();
        public uint         _littleEndian;
    }

    public class Ac3Parser {
        // constructor
        public Ac3Parser()
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

        public Ap4Result FindFrame(Ac3Frame frame)
        {
            uint       available;
            byte[]     raw_header = new byte[Ap4.AC3_HEADER_SIZE];
            Ap4Result  result;

            /* align to the start of the next byte */
            _bits.ByteAlign();

            /* find a frame header */
            result = FindHeader(raw_header);
            if (result.Failed()) return result;

            if (_littleEndian != 0) {
                Ap4Utils.ByteSwap16(raw_header, Ap4.AC3_HEADER_SIZE);
            }

            /* parse the header */
            Ac3Header ac3_header = new Ac3Header(raw_header);

            /* check the header */
            result = ac3_header.Check();
            if (result.Failed()) {
                _bits.SkipBytes(2);
                goto fail;
            }

            /* check if we have enough data to peek at the next header */
            available = _bits.GetBytesAvailable();
            if (available >= ac3_header._frameSize + Ap4.AC3_HEADER_SIZE) {
                // enough to peek at the header of the next frame
                byte[] peek_raw_header = new byte[Ap4.AC3_HEADER_SIZE];

                _bits.SkipBytes(ac3_header._frameSize);
                _bits.PeekBytes(peek_raw_header, Ap4.AC3_HEADER_SIZE);
                _bits.SkipBytes(unchecked((uint)(-((int)ac3_header._frameSize))));

                if (_littleEndian != 0) {
                    Ap4Utils.ByteSwap16(peek_raw_header, Ap4.AC3_HEADER_SIZE);
                }

                /* check the header */
                Ac3Header peek_ac3_header = new Ac3Header(peek_raw_header);
                result = peek_ac3_header.Check();
                if (result.Failed()) {
                    _bits.SkipBytes(ac3_header._frameSize + 2);
                    goto fail;
                }

                /* check that the fixed part of this header is the same as the */
                /* fixed part of the previous header                           */
                else if (!Ac3Header.MatchFixed(ac3_header, peek_ac3_header)) {
                    _bits.SkipBytes(ac3_header._frameSize + 2);
                    goto fail;
                }
            } else if (available < ac3_header._frameSize || (_bits._flags & Ap4.BITSTREAM_FLAG_EOS) == 0) {
                // not enough for a frame, or not at the end (in which case we'll want to peek at the next header)
                return Ap4Result.ERROR_NOT_ENOUGH_DATA;
            }

            frame._info._channelCount                 = ac3_header._channelCount;
            frame._info._sampleRate                   = Ap4.FSCOD_AC3[ac3_header._fscod];
            frame._info._frameSize                    = ac3_header._frameSize;
            frame._info._ac3StreamInfo.fscod          = ac3_header._fscod;
            frame._info._ac3StreamInfo.bsid           = ac3_header._bsid;
            frame._info._ac3StreamInfo.bsmod          = ac3_header._bsmod;
            frame._info._ac3StreamInfo.acmod          = ac3_header._acmod;
            frame._info._ac3StreamInfo.lfeon          = ac3_header._lfeon;
            frame._info._ac3StreamInfo.bit_rate_code  = ac3_header._frmsizecod / 2;

            frame._littleEndian = _littleEndian;

            /* set the frame source */
            frame._source = _bits;

            return Ap4Result.SUCCESS;

        fail:
            /* skip the header and return (only skip the first byte in  */
            /* case this was a false header that hides one just after)  */
            //m_Bits.SkipBytes(-(AP4_ADTS_HEADER_SIZE-1));
            return Ap4.ERROR_CORRUPTED_BITSTREAM;
        }

        // PORT NOTE: AP4_Ac3Parser::Skip is declared upstream but never defined; not ported.

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
            while (available-- >= Ap4.AC3_HEADER_SIZE) {
                _bits.PeekBytes(header, 2);  // ac3 header begins with 0x0B77

                if( ((uint)((header[0] << 8) | header[1]) == Ap4.AC3_SYNC_WORD_BIG_ENDIAN) ||
                    ((uint)((header[0] << 8) | header[1]) == Ap4.AC3_SYNC_WORD_LITTLE_ENDIAN) ){
                    // Little Endian
                    if ((uint)((header[0] << 8) | header[1]) == Ap4.AC3_SYNC_WORD_LITTLE_ENDIAN) {
                        _littleEndian = 1; // true
                    } else {
                        _littleEndian = 0; // false
                    }
                    /* found a sync pattern, read the entire the header */
                    _bits.PeekBytes(header, Ap4.AC3_HEADER_SIZE);

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
        private uint      _littleEndian;   // AP4_Flags upstream, assigned true/false
    }
}
