// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Codecs/Ap4Eac3Parser.h, Ap4Eac3Parser.cpp
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
        public const uint EAC3_HEADER_SIZE             = 64;      /* The header size for E-AC-3 parser, syncinfo() + bsi() */
        public const uint EAC3_SYNC_WORD_BIG_ENDIAN    = 0x0B77;  /* Big endian, 16 sync bits */
        public const uint EAC3_SYNC_WORD_LITTLE_ENDIAN = 0x770B;  /* Little endian, 16 sync bits */

        // PORT NOTE: GLOBAL_CHANNEL_ARY and EAC3_SAMPLE_RATE_ARY have no AP4_ prefix upstream
        public static readonly byte[] GLOBAL_CHANNEL_ARY   = {2, 1, 2, 3, 3, 4, 4, 5};   /* Channel count number from acmod value */
        public static readonly uint[] EAC3_SAMPLE_RATE_ARY = {48000, 44100, 32000};      /* Sample rate from fscode vale */
    }

    /*----------------------------------------------------------------------
    |   types
    +---------------------------------------------------------------------*/
    public class Eac3Header {
        // constructor
        public Eac3Header(byte[] bytes)
        {
            BitReader bits = new BitReader(bytes, Ap4.EAC3_HEADER_SIZE);

            bits.SkipBits(16);                          // sync word
            _strmtyp     = bits.ReadBits(2);
            _substreamid = bits.ReadBits(3);
            _frmsiz      = bits.ReadBits(11);
            _frameSize   = (_frmsiz + 1) * 2;
            _fscod       = bits.ReadBits(2);
            byte numblkscod   = 0;
            uint blks_per_frm = 0;
            if (_fscod == 0x3){
               Console.Error.Write("ERROR: Half sample rate unsupported\n");
               return;
            }else{
                numblkscod = (byte)bits.ReadBits(2);
                if (numblkscod == 0x3){
                    blks_per_frm = 6;
                }else {
                    blks_per_frm = (uint)(numblkscod + 1);
                }
            }
            _acmod = bits.ReadBits(3);
            _lfeon = bits.ReadBits(1);
            _channelCount = Ap4.GLOBAL_CHANNEL_ARY[_acmod]  + _lfeon;
            _bsid  = bits.ReadBits(5);
            if (!((_bsid > 10) && (_bsid <=16))){
                Console.Error.Write("ERROR: Unsupported bitstream id\n");
                return;
            }
            /* unsigned char dialnorm = */ bits.ReadBits(5);  // dialnorm
            // unsigned char compr;
            if (bits.ReadBit() != 0) {                  // compre
                /* compr = */ bits.ReadBits(8);
            }
            if (_acmod == 0x0){                         // if 1+1 mode (dual mono, so some items need a second value)
                bits.SkipBits(5);                       // dialnorm2
                if (bits.ReadBit() != 0) {              // compr2e
                    bits.SkipBits(8);                   // compr2
                }
            }
            if (_strmtyp == 0x1) {                      // if dependent stream
                _chanmape = (uint)bits.ReadBit();
                if (_chanmape != 0) {
                    _chanmap  = bits.ReadBits(16);
                }else {
                    //TODO: Derive chanmap from the acmod and lfeon parameters
                    _chanmap = 0;
                }
            }else {
                _chanmape = 0;
                _chanmap  = 0;
            }
            // Extract mixing metadata
            // unsigned char dmixmod, ltrtcmixlev, lorocmixlev, ltrtsurmixlev, lorosurmixlev, lfemixlevcod;
            if (bits.ReadBit() != 0){                   // mixmdate
                if (_acmod  > 0x2) {
                    /* dmixmod = */ bits.ReadBits(2);
                }
                if ((_acmod & 0x1) != 0 && (_acmod > 0x2)) {
                    /* ltrtcmixlev = */ bits.ReadBits(3);
                    /* lorocmixlev = */ bits.ReadBits(3);
                }
                if ((_acmod & 0x4) != 0) {
                    /* ltrtsurmixlev = */ bits.ReadBits(3);
                    /* lorosurmixlev = */ bits.ReadBits(3);
                }
                if (_lfeon != 0) {
                    if (bits.ReadBit() != 0) {          // lfemixlevcode
                        /* lfemixlevcod = */ bits.ReadBits(5);
                    }
                }
                if (_strmtyp == 0x0) {                  // if independent stream
                    // unsigned char pgmscl, extpgmscl;
                    if (bits.ReadBit() != 0) {          // pgmscle
                        /* pgmscl = */ bits.ReadBits(6);
                    }
                    if (_acmod == 0x0){                 // if 1+1 mode (dual mono, so some items need a second value)
                        if(bits.ReadBit() != 0) {       // pgmscl2e
                            bits.SkipBits(6);           // pgmscl2
                        }
                    }
                    if(bits.ReadBit() != 0) {           // extpgmscle
                        /* extpgmscl = */ bits.ReadBits(6);
                    }
                    sbyte mixdef = (sbyte)bits.ReadBits(2);
                    if(mixdef == 0x1)       { bits.SkipBits(5) ;}   // premixcmpsel, drcsrc, premixcmpscl
                    else if (mixdef == 0x2) { bits.SkipBits(12);}   // mixdata
                    else if (mixdef == 0x3) {
                        sbyte mixdeflen = (sbyte)bits.ReadBits(5);
                        uint mixdefbits = 1;                        // the initial value represents mixdata2e
                        if(bits.ReadBit() != 0){                    // mixdata2e
                            bits.SkipBits(5);                       // premixcmpsel, drcsrc, premixcmpscl
                            mixdefbits += 5;
                            mixdefbits += 1;                        // extpgmlscle
                            if(bits.ReadBit() != 0) {
                                bits.SkipBits(4);                   // extpgmlscl
                                mixdefbits += 4;
                            }
                            mixdefbits += 1;                        // extpgmcscle
                            if(bits.ReadBit() != 0) {
                                bits.SkipBits(4);                   // extpgmcscl
                                mixdefbits += 4;
                            }
                            mixdefbits += 1;                        // extpgmrscle
                            if(bits.ReadBit() != 0) {
                                bits.SkipBits(4);                   // extpgmrscl
                                mixdefbits += 4;
                            }
                            mixdefbits += 1;                        // extpgmlsscle
                            if(bits.ReadBit() != 0) {
                                bits.SkipBits(4);                   // extpgmlsscl
                                mixdefbits += 4;
                            }
                            mixdefbits += 1;                        // extpgmrsscle
                            if(bits.ReadBit() != 0) {
                                bits.SkipBits(4);                   // extpgmrsscl
                                mixdefbits += 4;
                            }
                            mixdefbits += 1;                        // extpgmlfescle
                            if(bits.ReadBit() != 0) {
                                bits.SkipBits(4);                   // extpgmlfescl
                                mixdefbits += 4;
                            }
                            mixdefbits += 1;                        // dmixscle
                            if(bits.ReadBit() != 0) {
                                bits.SkipBits(4);                   // dmixscl
                                mixdefbits += 4;
                            }
                            mixdefbits += 1;
                            if(bits.ReadBit() != 0){                // addche
                                mixdefbits += 1;
                                if(bits.ReadBit() != 0) {           // extpgmaux1scle
                                    bits.SkipBits(4);               // extpgmaux1scl
                                    mixdefbits += 4;
                                }
                                mixdefbits += 1;
                                if(bits.ReadBit() != 0) {           // extpgmaux2scle
                                    bits.SkipBits(4);               // extpgmaux2scl
                                    mixdefbits += 4;
                                }
                            }
                        } //  if(bits.ReadBit()){   // mixdata2e
                        mixdefbits += 1;
                        if(bits.ReadBit() != 0){                    // mixdata3e
                            bits.SkipBits(5);                       // spchdat
                            mixdefbits += 5;
                            mixdefbits += 1;
                            if(bits.ReadBit() != 0){                // addspchdate
                                bits.SkipBits(7);                   // spchdat1, spchan1att
                                mixdefbits += 7;
                                mixdefbits += 1;
                                if(bits.ReadBit() != 0){            // addspdat1e
                                    bits.SkipBits(8);               // spchdat2, spchan2att
                                    mixdefbits += 8;
                                }
                            }
                        } //  if(bits.ReadBit()){   // mixdata3e
                        // PORT NOTE: int - unsigned -> unsigned, as in C++ (wraps when negative)
                        bits.SkipBits(unchecked((uint)(8 * (mixdeflen + 2)) - mixdefbits)); // mixdatafill
                    }
                    if (_acmod < 0x2) {                                 // if mono or dual mono source
                        if(bits.ReadBit() != 0){                        // paninfoe
                            bits.SkipBits(8 + 6);                       // panmean, paninfo
                        }
                        if (_acmod == 0x0) {                            // if 1+1 mode (dual mono - some items need a second value)
                            if(bits.ReadBit() != 0){                    // paninfo2e
                                bits.SkipBits(8 + 6);                   // panmean2, paninfo2
                            }
                        }
                    }
                    if (bits.ReadBit() != 0){                       // frmmixcfginfoe
                        if (blks_per_frm == 1) {
                            bits.SkipBits(5);                       // blkmixcfginfo[0]
                        }else{
                            for (uint idx = 0; idx < blks_per_frm; idx++){
                                if(bits.ReadBit() != 0){            // blkmixcfginfoe
                                    bits.SkipBits(5);               // blkmixcfginfo[blk]
                                }
                            }
                        }
                    }
                } // if (m_Strmtyp == 0x0)
            }
            _infomdate = (uint)bits.ReadBit();
            if (_infomdate != 0){
                _bsmod = bits.ReadBits(3);
                // unsigned char copyrightb, origbs, dsurexmod;
                /* copyrightb = */ bits.ReadBits(1);
                /* origbs     = */ bits.ReadBits(1);
                if (_acmod == 0x2) {                                // if in 2/0 mode
                    bits.SkipBits(4);                               // dsurmod, dheadphonmod
                }
                if (_acmod >= 0x6) {                                // if both surround channels exist
                    /* dsurexmod = */ bits.ReadBits(2);
                }
                if(bits.ReadBit() != 0){                            // audprodie
                    bits.SkipBits(8);                               // mixlevel, roomtyp, adconvtyp
                }
                if (_acmod == 0x0) {                                //if 1+1 mode (dual mono, so some items need a second value)
                    if(bits.ReadBit() != 0){                        // audprodi2e
                        bits.SkipBits(8);                           // mixlevel2, roomtyp2, adconvtyp2
                    }
                }
                if (_fscod < 0x3) {                                 // if not half sample rate
                    bits.SkipBits(1);                               // sourcefscod
                }
            } else { // if (m_Infomdate)
                _bsmod = 0;
            }
            _convsync = 1;
            if ((_strmtyp == 0x0) && (numblkscod != 0x3)){
                _convsync = bits.ReadBits(1);
            }

            if (_strmtyp == 0x2) {                                  // if bit stream converted from AC-3
                byte blkid = 0;
                if (numblkscod == 0x3) {
                    blkid = 1;
                }else {
                    blkid = (byte)bits.ReadBits(1);
                }
                if (blkid != 0) {bits.SkipBits(6); }                // frmsizecod
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
            if (_fscod == 1 || _fscod == 2) {
                Console.Error.Write("WARN: The sample rate is NOT 48 kHz\n");
            } else if (_fscod == 3) {
                return Ap4Result.FAILURE;
            }
            if (_bsid < 10 || _bsid > 16) {
                return Ap4Result.FAILURE;
            }
            if (_substreamid != 0) {
                Console.Error.Write("ERROR: Only single independent substream (I0) or single depenpent substream (D0) is allowed in a DD+ stream\n");
                return Ap4Result.FAILURE;
            }
            return Ap4Result.SUCCESS;
        }

        // PORT NOTE: members that upstream leaves uninitialized on the early-return paths are 0 here
        public uint _headSize;
        public uint _channelCount;
        public uint _frameSize;

        // E-AC-3 bsi()
        public uint _strmtyp;
        public uint _substreamid;
        public uint _frmsiz;
        public uint _fscod;
        public uint _acmod;
        public uint _lfeon;
        public uint _bsid;
        public uint _chanmape;
        public uint _chanmap;
        public uint _infomdate;
        public uint _bsmod;
        public uint _convsync;
        public uint _addbsie;
        public uint _addbsil;                    // 6 bits
        public byte[] _addbsi = new byte[65];    // (addbsil + 1)* 8

        // class methods
        /*
         * Check that two fixed headers are the same
         */
        public static bool MatchFixed(Eac3Header frame, Eac3Header next_frame)
        {
            if (frame._acmod ==  next_frame._acmod &&
                frame._bsid  ==  next_frame._bsid  &&
                frame._bsmod ==  next_frame._bsmod &&
                frame._lfeon ==  next_frame._lfeon &&
                frame._fscod ==  next_frame._fscod ) {
                return true;
            } else {
                return false;
            }
        }
    }

    // PORT NOTE: upstream C structs; classes here (filled in place by Eac3Parser.FindFrame)
    public class Eac3FrameInfo {
        public uint _channelCount;
        public uint _frameSize;
        public uint _sampleRate;
        public Dec3Atom.SubStream _eac3SubStream;
        public uint complexity_index_type_a;
    }

    public class Eac3Frame {
        public BitStream     _source;
        public Eac3FrameInfo _info = new Eac3FrameInfo();
        public uint          _littleEndian;
    }

    public class Eac3Parser {
        // constructor
        public Eac3Parser()
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

        public Ap4Result FindFrame(Eac3Frame frame)
        {
            bool       dependent_stream_exist    = false;
            uint       dependent_stream_chan_loc = 0;
            uint       dependent_stream_length   = 0;
            uint       skip_size = 0;
            uint       available;
            byte[]     raw_header = new byte[Ap4.EAC3_HEADER_SIZE];
            Ap4Result  result;

            /* align to the start of the next byte */
            _bits.ByteAlign();

            /* find a frame header */
            result = FindHeader(raw_header, ref skip_size);
            if (result.Failed()) return result;

            if (_littleEndian != 0) {
                Ap4Utils.ByteSwap16(raw_header, Ap4.EAC3_HEADER_SIZE);
            }

            /* parse the header */
            Eac3Header eac3_header = new Eac3Header(raw_header);

            /* check the header */
            result = eac3_header.Check();
            if (result.Failed()) {
                goto fail;
            }

            /* check if we have enough data to peek at the next header */
            available = _bits.GetBytesAvailable();
            if (available >= eac3_header._frameSize + Ap4.EAC3_HEADER_SIZE) {
                // enough to peek at the header of the next frame
                byte[] peek_raw_header = new byte[Ap4.EAC3_HEADER_SIZE];

                _bits.SkipBytes(eac3_header._frameSize);
                skip_size = 0;
                result = FindHeader(peek_raw_header, ref skip_size);
                if (result.Failed()) return result;
                _bits.SkipBytes(unchecked((uint)(-((int)(eac3_header._frameSize + skip_size)))));

                if (_littleEndian != 0) {
                    Ap4Utils.ByteSwap16(peek_raw_header, Ap4.EAC3_HEADER_SIZE);
                }
                /* check the header */
                Eac3Header peek_eac3_header = new Eac3Header(peek_raw_header);
                result = peek_eac3_header.Check();
                if (result.Failed()) {
                    goto fail;
                }

                // TODO: Only support 7.1-channel now
                if (peek_eac3_header._strmtyp  == 1) {
                    dependent_stream_exist = true;
                    if (peek_eac3_header._chanmape == 0){
                        goto fail;
                    }else {
                        if ((peek_eac3_header._chanmap & 0x200) != 0) {
                            dependent_stream_chan_loc  |= 0x2;
                            dependent_stream_length     = peek_eac3_header._frameSize;
                            eac3_header._channelCount += 2;
                        } else {
                            Console.Error.Write("ERROR: Only support 7.1-channel (I0 + D0). For other D0, the tool doesn't support yet.\n");
                            goto fail;
                        }
                    }
                }
                /* check that the fixed part of this header is the same as the */
                /* fixed part of the previous header                           */
                else if (!Eac3Header.MatchFixed(eac3_header, peek_eac3_header)) {
                    goto fail;
                }
            } else if (available < eac3_header._frameSize || (_bits._flags & Ap4.BITSTREAM_FLAG_EOS) == 0) {
                // not enough for a frame, or not at the end (in which case we'll want to peek at the next header)
                return Ap4Result.ERROR_NOT_ENOUGH_DATA;
            }

            /* fill in the frame info */
            frame._info._channelCount   = eac3_header._channelCount;
            if (dependent_stream_exist) {
                frame._info._frameSize  = eac3_header._frameSize + dependent_stream_length;
            }else {
                frame._info._frameSize  = eac3_header._frameSize;
            }
            frame._info._sampleRate                = Ap4.EAC3_SAMPLE_RATE_ARY[eac3_header._fscod];
            frame._info._eac3SubStream.fscod       = eac3_header._fscod;
            frame._info._eac3SubStream.bsid        = eac3_header._bsid;
            frame._info._eac3SubStream.bsmod       = eac3_header._bsmod;
            frame._info._eac3SubStream.acmod       = eac3_header._acmod;
            frame._info._eac3SubStream.lfeon       = eac3_header._lfeon;
            if (dependent_stream_exist) {
                frame._info._eac3SubStream.num_dep_sub = 1;
                frame._info._eac3SubStream.chan_loc    = dependent_stream_chan_loc;
            }else {
                frame._info._eac3SubStream.num_dep_sub = 0;
                frame._info._eac3SubStream.chan_loc    = 0;
            }

            frame._info.complexity_index_type_a = 0;
            if (eac3_header._addbsie != 0 && (eac3_header._addbsil == 1) && (eac3_header._addbsi[0] == 0x1)){
                frame._info.complexity_index_type_a = eac3_header._addbsi[1];
            }

            /* set the little endian flag */
            frame._littleEndian = _littleEndian;

            /* set the frame source */
            frame._source = _bits;

            return Ap4Result.SUCCESS;

        fail:
            return Ap4.ERROR_CORRUPTED_BITSTREAM;
        }

        // PORT NOTE: AP4_Eac3Parser::Skip is declared upstream but never defined; not ported.

        public uint GetBytesFree()
        {
            return (_bits.GetBytesFree());
        }

        public uint GetBytesAvailable()
        {
            return (_bits.GetBytesAvailable());
        }

        // methods
        private Ap4Result FindHeader(byte[] header, ref uint skip_size)
        {
            uint available = _bits.GetBytesAvailable();

            /* look for the sync pattern */
            while (available-- >= Ap4.EAC3_HEADER_SIZE) {
                _bits.PeekBytes(header, 2);

                if( ((uint)((header[0] << 8) | header[1]) == Ap4.EAC3_SYNC_WORD_BIG_ENDIAN) ||
                    ((uint)((header[0] << 8) | header[1]) == Ap4.EAC3_SYNC_WORD_LITTLE_ENDIAN) ){
                    if ((uint)((header[0] << 8) | header[1]) == Ap4.EAC3_SYNC_WORD_LITTLE_ENDIAN) {
                        _littleEndian = 1; // true
                    } else {
                        _littleEndian = 0; // false
                    }
                    /* found a sync pattern, read the entire the header */
                    _bits.PeekBytes(header, Ap4.EAC3_HEADER_SIZE);

                    return Ap4Result.SUCCESS;
                } else {
                    _bits.SkipBytes(1); // skip
                    skip_size++;
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
