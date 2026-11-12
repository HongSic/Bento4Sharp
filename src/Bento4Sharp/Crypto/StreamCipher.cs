// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Crypto/Ap4StreamCipher.h, Ap4StreamCipher.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    // PORT NOTE (applies to the whole file):
    // - `const AP4_UI08* in, AP4_Size in_size, AP4_UI08* out` become
    //   `byte[] @in, int in_offset, uint in_size, byte[] @out, int out_offset`
    //   (the virtual method), with a non-virtual twin without offsets.
    // - `AP4_Size* out_size` is an in/out parameter (capacity of `out` on input,
    //   bytes produced on output): `ref uint out_size`. Only AP4_CtrStreamCipher
    //   accepts a NULL out_size: it has extra overloads without that parameter.
    // - `AP4_Cardinal* preroll` is `out uint preroll` (set to 0 on the paths
    //   where upstream does not write it). Only AP4_CtrStreamCipher accepts a
    //   NULL preroll: it has an extra overload without that parameter.
    // - `const AP4_UI08* iv` is a `byte[]?` whose first 16 bytes are used;
    //   SetIV(iv, offset) is a port addition for call sites that pass a pointer
    //   into a larger buffer.

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        // we only support this for now
        public const uint CIPHER_BLOCK_SIZE = 16;
    }

    /*----------------------------------------------------------------------
    |   AP4_StreamCipher interface
    +---------------------------------------------------------------------*/
    public abstract class StreamCipher
    {
        // methods
        public abstract ulong GetStreamOffset();

        public abstract Ap4Result ProcessBuffer(byte[]   @in,
                                                int      in_offset,
                                                uint     in_size,
                                                byte[]   @out,
                                                int      out_offset,
                                                ref uint out_size,
                                                bool     is_last_buffer = false);

        public Ap4Result ProcessBuffer(byte[]   @in,
                                       uint     in_size,
                                       byte[]   @out,
                                       ref uint out_size,
                                       bool     is_last_buffer = false)
        {
            return ProcessBuffer(@in, 0, in_size, @out, 0, ref out_size, is_last_buffer);
        }

        // preroll gives the number of bytes you have to preroll your input and feed
        // it through ProcessBuffer (in one shot) in order to be able to spit out
        // the output at the given offset
        public abstract Ap4Result SetStreamOffset(ulong    offset,
                                                  out uint preroll);

        public abstract Ap4Result SetIV(byte[] iv);
        public abstract byte[] GetIV();

        // PORT NOTE: addition, for `SetIV(pointer + n)` call sites
        public Ap4Result SetIV(byte[] iv, int offset)
        {
            byte[] iv_copy = new byte[Ap4.CIPHER_BLOCK_SIZE];
            Array.Copy(iv, offset, iv_copy, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
            return SetIV(iv_copy);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_CtrStreamCipher
    +---------------------------------------------------------------------*/
    public class CtrStreamCipher : StreamCipher
    {
        // methods

        /*----------------------------------------------------------------------
        |   AP4_CtrStreamCipher::AP4_CtrStreamCipher
        +---------------------------------------------------------------------*/
        /// <summary>
        /// The block cipher is passed with transfer of ownership (it will
        /// be destroyed when this object is destroyed).
        /// </summary>
        public CtrStreamCipher(BlockCipher  block_cipher,
                               uint         counter_size)
        {
            _streamOffset = 0;
            _counterSize = counter_size;
            _cacheValid = false;
            _blockCipher = block_cipher;

            if (_counterSize > 16) _counterSize = 16;

            // reset the stream offset
            Array.Clear(_iV, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
            SetStreamOffset(0);
            SetIV(null);
        }

        // AP4_StreamCipher implementation
        /*----------------------------------------------------------------------
        |   AP4_CtrStreamCipher::SetIV
        +---------------------------------------------------------------------*/
        public override Ap4Result SetIV(byte[] iv)
        {
            if (iv != null) {
                Array.Copy(iv, 0, _iV, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
            } else {
                Array.Clear(_iV, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
            }

            // for the stream offset back to 0
            _cacheValid = false;
            return SetStreamOffset(0);
        }

        public override byte[] GetIV()  { return _iV; }

        /*----------------------------------------------------------------------
        |   AP4_CtrStreamCipher::SetStreamOffset
        +---------------------------------------------------------------------*/
        public override Ap4Result SetStreamOffset(ulong    offset,
                                                  out uint preroll)
        {
            // no preroll in CTR mode
            // PORT NOTE: upstream leaves *preroll untouched when the offset does
            // not change; an `out` parameter must always be assigned
            preroll = 0;

            return SetStreamOffset(offset);
        }

        // PORT NOTE: SetStreamOffset(offset, NULL)
        public Ap4Result SetStreamOffset(ulong offset)
        {
            // do nothing if we're already at that offset
            if (offset == _streamOffset) return Ap4Result.SUCCESS;

            // update the offset
            _cacheValid = false;
            _streamOffset = offset;

            return Ap4Result.SUCCESS;
        }

        public override ulong GetStreamOffset() { return _streamOffset; }

        /*----------------------------------------------------------------------
        |   AP4_CtrStreamCipher::ComputeCounter
        +---------------------------------------------------------------------*/
        private void ComputeCounter(ulong  stream_offset,
                                    byte[] counter_block)
        {
            // setup counter offset bytes
            ulong counter_offset = stream_offset/Ap4.CIPHER_BLOCK_SIZE;
            byte[] counter_offset_bytes = new byte[8];
            Ap4Utils.BytesFromUInt64BE(counter_offset_bytes, counter_offset);

            // compute the new counter
            uint carry = 0;
            for (uint i=0; i<_counterSize; i++) {
                uint o = Ap4.CIPHER_BLOCK_SIZE-1-i;
                uint x = _iV[o];
                uint y = (i<8)?counter_offset_bytes[7-i]:0u;
                uint sum = x+y+carry;
                counter_block[o] = (byte)(sum&0xFF);
                carry = ((sum >= 0x100)?1u:0u);
            }
            for (uint i=_counterSize; i<Ap4.CIPHER_BLOCK_SIZE; i++) {
                uint o = Ap4.CIPHER_BLOCK_SIZE-1-i;
                counter_block[o] = _iV[o];
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_CtrStreamCipher::ProcessBuffer
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessBuffer(byte[]   @in,
                                                int      in_offset,
                                                uint     in_size,
                                                byte[]   @out,
                                                int      out_offset,
                                                ref uint out_size,
                                                bool     is_last_buffer = false)
        {
            return ProcessBufferImpl(@in, in_offset, in_size, @out, out_offset, true, ref out_size);
        }

        // PORT NOTE: ProcessBuffer(in, in_size, out) with out_size == NULL
        public Ap4Result ProcessBuffer(byte[] @in,
                                       int    in_offset,
                                       uint   in_size,
                                       byte[] @out,
                                       int    out_offset)
        {
            uint out_size = 0;
            return ProcessBufferImpl(@in, in_offset, in_size, @out, out_offset, false, ref out_size);
        }

        public Ap4Result ProcessBuffer(byte[] @in,
                                       uint   in_size,
                                       byte[] @out)
        {
            uint out_size = 0;
            return ProcessBufferImpl(@in, 0, in_size, @out, 0, false, ref out_size);
        }

        // PORT NOTE: `has_out_size` is `out_size != NULL`
        private Ap4Result ProcessBufferImpl(byte[] @in,
                                        int      in_offset,
                                        uint     in_size,
                                        byte[]   @out,
                                        int      out_offset,
                                        bool     has_out_size,
                                        ref uint out_size)
        {
            if (_blockCipher == null) return Ap4Result.ERROR_INVALID_STATE;

            if (has_out_size && out_size < in_size) {
                out_size = in_size;
                return Ap4Result.ERROR_BUFFER_TOO_SMALL;
            }

            // in CTR mode, the output is the same size as the input
            if (has_out_size) out_size = in_size;

            // deal with inputs not aligned on block boundaries
            if ((_streamOffset%Ap4.CIPHER_BLOCK_SIZE) != 0) {
                uint cache_offset = (uint)(_streamOffset%Ap4.CIPHER_BLOCK_SIZE);
                if (!_cacheValid) {
                    // fill the cache
                    byte[] block = new byte[Ap4.CIPHER_BLOCK_SIZE]; // = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}
                    byte[] counter_block = new byte[Ap4.CIPHER_BLOCK_SIZE];
                    ComputeCounter(_streamOffset-cache_offset, counter_block);
                    Ap4Result result = _blockCipher.Process(block, Ap4.CIPHER_BLOCK_SIZE, _cacheBlock, counter_block);
                    if (result.Failed()) {
                        if (has_out_size) out_size = 0;
                        return result;
                    }
                    _cacheValid = true;
                }
                uint partial = Ap4.CIPHER_BLOCK_SIZE-cache_offset;
                if (partial > in_size) partial = in_size;
                for (uint i=0; i<partial; i++) {
                    @out[out_offset+i] = (byte)(@in[in_offset+i]^_cacheBlock[i+cache_offset]);
                }

                // advance to the end of the partial block
                _streamOffset += partial;
                in_offset     += (int)partial;
                out_offset    += (int)partial;
                in_size       -= partial;
            }

            // process all the remaining bytes in the buffer
            if (in_size != 0) {
                // the cache won't be valid anymore
                _cacheValid = false;

                // compute the counter
                byte[] counter_block = new byte[Ap4.CIPHER_BLOCK_SIZE];
                ComputeCounter(_streamOffset, counter_block);

                // process the data
                Ap4Result result = _blockCipher.Process(@in, in_offset, in_size, @out, out_offset, counter_block);
                if (result.Failed()) {
                    if (has_out_size) out_size = 0;
                    return result;
                }
                _streamOffset += in_size;
                return result;
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private ulong        _streamOffset;
        private uint         _counterSize;
        private byte[]       _iV = new byte[Ap4.CIPHER_BLOCK_SIZE];
        private byte[]       _cacheBlock = new byte[Ap4.CIPHER_BLOCK_SIZE];
        private bool         _cacheValid;
        private BlockCipher  _blockCipher;
    }

    /*----------------------------------------------------------------------
    |   AP4_CbcStreamCipher
    +---------------------------------------------------------------------*/
    public class CbcStreamCipher : StreamCipher
    {
        // methods

        /*----------------------------------------------------------------------
        |   AP4_CbcStreamCipher::AP4_CbcStreamCipher
        +---------------------------------------------------------------------*/
        /// <summary>
        /// The block cipher is passed with transfer of ownership (it will
        /// be destroyed when this object is destroyed).
        /// </summary>
        public CbcStreamCipher(BlockCipher block_cipher)
        {
            _streamOffset = 0;
            _outputSkip = 0;
            _inBlockFullness = 0;
            _chainBlockFullness = Ap4.CIPHER_BLOCK_SIZE;
            _blockCipher = block_cipher;
            _eos = false;

            Array.Clear(_iv, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
            Array.Clear(_chainBlock, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
        }

        // AP4_StreamCipher implementation
        /*----------------------------------------------------------------------
        |   AP4_CbcStreamCipher::SetIV
        +---------------------------------------------------------------------*/
        public override Ap4Result SetIV(byte[] iv)
        {
            // PORT NOTE: upstream copies from `iv` without a null check
            Array.Copy(iv, 0, _iv, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
            _streamOffset = 0;
            _eos = false;
            Array.Copy(_iv, 0, _chainBlock, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
            _chainBlockFullness = Ap4.CIPHER_BLOCK_SIZE;
            _inBlockFullness = 0;
            _outputSkip = 0;
            return Ap4Result.SUCCESS;
        }

        public override byte[] GetIV() { return _iv; }

        /*----------------------------------------------------------------------
        |   AP4_CbcStreamCipher::SetStreamOffset
        +---------------------------------------------------------------------*/
        public override Ap4Result SetStreamOffset(ulong    offset,
                                                  out uint preroll)
        {
            preroll = 0;

            // does not make sense for encryption
            if (_blockCipher.GetDirection() == BlockCipher.CipherDirection.ENCRYPT) {
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            // check params
            // PORT NOTE: `if (preroll == NULL) return AP4_ERROR_INVALID_PARAMETERS;`
            // cannot happen with an `out` parameter

            // reset the end of stream flag
            _eos = false;

            // invalidate the chain block
            _chainBlockFullness = 0;

            // flush cached data
            _inBlockFullness = 0;

            // compute the preroll
            if (offset < Ap4.CIPHER_BLOCK_SIZE) {
                Array.Copy(_iv, 0, _chainBlock, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
                _chainBlockFullness = Ap4.CIPHER_BLOCK_SIZE;
                preroll = (uint)offset;
            } else {
                preroll = (uint) ((offset%Ap4.CIPHER_BLOCK_SIZE) + Ap4.CIPHER_BLOCK_SIZE);
            }

            _streamOffset = offset-preroll;
            _outputSkip   = (uint)(offset%Ap4.CIPHER_BLOCK_SIZE);
            return Ap4Result.SUCCESS;
        }

        public override ulong GetStreamOffset() { return _streamOffset; }

        /*----------------------------------------------------------------------
        |   AP4_CbcStreamCipher::EncryptBuffer
        +---------------------------------------------------------------------*/
        private Ap4Result EncryptBuffer(byte[]   @in,
                                        int      in_offset,
                                        uint     in_size,
                                        byte[]   @out,
                                        int      out_offset,
                                        ref uint out_size,
                                        bool     is_last_buffer)
        {
            // we don't do much checking here because this method is only called
            // from ProcessBuffer(), which does all the parameter checking

            // compute how many blocks we span
            ulong start_block   = (_streamOffset-_inBlockFullness)/Ap4.CIPHER_BLOCK_SIZE;
            ulong end_block     = (_streamOffset+in_size)/Ap4.CIPHER_BLOCK_SIZE;
            uint  blocks_needed = (uint)(end_block-start_block);

            // compute how many blocks we will need to produce
            if (is_last_buffer) {
                ++blocks_needed;
            }
            if (out_size < blocks_needed*Ap4.CIPHER_BLOCK_SIZE) {
                out_size = blocks_needed*Ap4.CIPHER_BLOCK_SIZE;
                return Ap4Result.ERROR_BUFFER_TOO_SMALL;
            }
            out_size = blocks_needed*Ap4.CIPHER_BLOCK_SIZE;

            // finish any incomplete block from a previous call
            uint offset = (uint)(_streamOffset%Ap4.CIPHER_BLOCK_SIZE);
            Ap4.ASSERT(_inBlockFullness == offset);
            if (offset != 0) {
                uint chunk = Ap4.CIPHER_BLOCK_SIZE-offset;
                if (chunk > in_size) chunk = in_size;
                for (uint x=0; x<chunk; x++) {
                    _inBlock[x+offset] = @in[in_offset+x];
                }
                in_offset        += (int)chunk;
                in_size          -= chunk;
                _streamOffset    += chunk;
                _inBlockFullness += chunk;
                if (offset+chunk == Ap4.CIPHER_BLOCK_SIZE) {
                    // we have filled the input block, encrypt it
                    Ap4Result result = _blockCipher.Process(_inBlock, 0, Ap4.CIPHER_BLOCK_SIZE, @out, out_offset, _chainBlock);
                    Array.Copy(@out, out_offset, _chainBlock, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
                    _inBlockFullness = 0;
                    if (result.Failed()) {
                        out_size = 0;
                        return result;
                    }
                    out_offset += (int)Ap4.CIPHER_BLOCK_SIZE;
                }
            }

            // encrypt the whole blocks
            uint block_count = in_size/Ap4.CIPHER_BLOCK_SIZE;
            if (block_count != 0) {
                Ap4.ASSERT(_inBlockFullness == 0);
                uint blocks_size = block_count*Ap4.CIPHER_BLOCK_SIZE;
                Ap4Result result = _blockCipher.Process(@in, in_offset, blocks_size, @out, out_offset, _chainBlock);
                Array.Copy(@out, out_offset+(int)blocks_size-(int)Ap4.CIPHER_BLOCK_SIZE, _chainBlock, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
                if (result.Failed()) {
                    out_size = 0;
                    return result;
                }
                in_offset     += (int)blocks_size;
                out_offset    += (int)blocks_size;
                in_size       -= blocks_size;
                _streamOffset += blocks_size;
            }

            // deal with what's left
            if (in_size != 0) {
                Ap4.ASSERT(in_size < Ap4.CIPHER_BLOCK_SIZE);
                for (uint x=0; x<in_size; x++) {
                    _inBlock[x+_inBlockFullness] = @in[in_offset+x];
                }
                _inBlockFullness += in_size;
                _streamOffset    += in_size;
            }

            // pad if needed
            if (is_last_buffer) {
                Ap4.ASSERT(_inBlockFullness == _streamOffset%Ap4.CIPHER_BLOCK_SIZE);
                byte pad_byte = (byte)(Ap4.CIPHER_BLOCK_SIZE-(byte)(_streamOffset%Ap4.CIPHER_BLOCK_SIZE));
                for (uint x=Ap4.CIPHER_BLOCK_SIZE-pad_byte; x<Ap4.CIPHER_BLOCK_SIZE; x++) {
                    _inBlock[x] = pad_byte;
                }
                Ap4Result result = _blockCipher.Process(_inBlock, 0, Ap4.CIPHER_BLOCK_SIZE, @out, out_offset, _chainBlock);
                Array.Copy(@out, out_offset, _chainBlock, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
                _inBlockFullness = 0;
                if (result.Failed()) {
                    out_size = 0;
                    return result;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CbcStreamCipher::DecryptBuffer
        +---------------------------------------------------------------------*/
        private Ap4Result DecryptBuffer(byte[]   @in,
                                        int      in_offset,
                                        uint     in_size,
                                        byte[]   @out,
                                        int      out_offset,
                                        ref uint out_size,
                                        bool     is_last_buffer)
        {
            // we don't do much checking here because this method is only called
            // from ProcessBuffer(), which does all the parameter checking

            // deal chain-block buffering
            if (_chainBlockFullness != Ap4.CIPHER_BLOCK_SIZE) {
                uint needed = Ap4.CIPHER_BLOCK_SIZE-_chainBlockFullness;
                uint chunk = (in_size > needed) ? needed : in_size;
                Array.Copy(@in, in_offset, _chainBlock, (int)_chainBlockFullness, (int)chunk);
                in_size             -= chunk;
                in_offset           += (int)chunk;
                _chainBlockFullness += chunk;
                _streamOffset       += chunk;
                if (_chainBlockFullness != Ap4.CIPHER_BLOCK_SIZE) {
                    // not enough to continue
                    out_size = 0;
                    return Ap4Result.SUCCESS;
                }
            }
            Ap4.ASSERT(_chainBlockFullness == Ap4.CIPHER_BLOCK_SIZE);

            // compute how many blocks we span
            ulong start_block   = (_streamOffset-_inBlockFullness)/Ap4.CIPHER_BLOCK_SIZE;
            ulong end_block     = (_streamOffset+in_size)/Ap4.CIPHER_BLOCK_SIZE;
            uint  blocks_needed = (uint)(end_block-start_block);

            // compute how many blocks we will need to produce
            if (out_size < blocks_needed*Ap4.CIPHER_BLOCK_SIZE) {
                out_size = blocks_needed*Ap4.CIPHER_BLOCK_SIZE;
                return Ap4Result.ERROR_BUFFER_TOO_SMALL;
            }
            out_size = blocks_needed*Ap4.CIPHER_BLOCK_SIZE;
            if (blocks_needed != 0 && _outputSkip != 0) out_size -= _outputSkip;

            // shortcut
            if (in_size == 0) return Ap4Result.SUCCESS;

            // deal with in-block buffering
            // NOTE: if we have bytes to skip on output, always use the in-block buffer for
            // the first block, even if we're aligned, this makes the code simpler
            Ap4.ASSERT(_inBlockFullness < Ap4.CIPHER_BLOCK_SIZE);
            if (_outputSkip != 0 || _inBlockFullness != 0) {
                uint needed = Ap4.CIPHER_BLOCK_SIZE-_inBlockFullness;
                uint chunk = (in_size > needed) ? needed : in_size;
                Array.Copy(@in, in_offset, _inBlock, (int)_inBlockFullness, (int)chunk);
                in_size          -= chunk;
                in_offset        += (int)chunk;
                _inBlockFullness += chunk;
                _streamOffset    += chunk;
                if (_inBlockFullness != Ap4.CIPHER_BLOCK_SIZE) {
                    // not enough to continue
                    out_size = 0;
                    return Ap4Result.SUCCESS;
                }
                byte[] out_block = new byte[Ap4.CIPHER_BLOCK_SIZE];
                Ap4Result result = _blockCipher.Process(_inBlock, Ap4.CIPHER_BLOCK_SIZE, out_block, _chainBlock);
                _inBlockFullness = 0;
                if (result.Failed()) {
                    out_size = 0;
                    return result;
                }
                Array.Copy(_inBlock, 0, _chainBlock, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
                if (_outputSkip != 0) {
                    Ap4.ASSERT(_outputSkip < Ap4.CIPHER_BLOCK_SIZE);
                    Array.Copy(out_block, (int)_outputSkip, @out, out_offset, (int)(Ap4.CIPHER_BLOCK_SIZE-_outputSkip));
                    out_offset += (int)(Ap4.CIPHER_BLOCK_SIZE-_outputSkip);
                    _outputSkip = 0;
                } else {
                    Array.Copy(out_block, 0, @out, out_offset, (int)Ap4.CIPHER_BLOCK_SIZE);
                    out_offset += (int)Ap4.CIPHER_BLOCK_SIZE;
                }
            }
            Ap4.ASSERT(_inBlockFullness == 0);
            Ap4.ASSERT(_outputSkip == 0);

            // process full blocks
            uint block_count = in_size/Ap4.CIPHER_BLOCK_SIZE;
            if (block_count != 0) {
                uint blocks_size = block_count*Ap4.CIPHER_BLOCK_SIZE;
                Ap4Result result = _blockCipher.Process(@in, in_offset, blocks_size, @out, out_offset, _chainBlock);
                Array.Copy(@in, in_offset+(int)blocks_size-(int)Ap4.CIPHER_BLOCK_SIZE, _chainBlock, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
                if (result.Failed()) {
                    out_size = 0;
                    return result;
                }
                in_offset     += (int)blocks_size;
                out_offset    += (int)blocks_size;
                in_size       -= blocks_size;
                _streamOffset += blocks_size;
            }

            // buffer partial block leftovers
            if (in_size != 0) {
                Ap4.ASSERT(in_size < Ap4.CIPHER_BLOCK_SIZE);
                Array.Copy(@in, in_offset, _inBlock, 0, (int)in_size);
                _inBlockFullness = in_size;
                _streamOffset   += in_size;
            }

            // deal with padding
            if (is_last_buffer) {
                // PORT NOTE: upstream reads `*(out-1)` even when no byte was
                // produced by this call and `out` still points to the start of
                // the caller's buffer (out-of-bounds read, undefined behaviour);
                // that case is reported as an invalid format here.
                if (out_offset < 1) {
                    out_size = 0;
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                byte pad_byte = @out[out_offset-1];
                if (pad_byte > Ap4.CIPHER_BLOCK_SIZE ||
                    out_size < pad_byte) {
                    out_size = 0;
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                out_size -= pad_byte;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CbcStreamCipher::ProcessBuffer
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessBuffer(byte[]   @in,
                                                int      in_offset,
                                                uint     in_size,
                                                byte[]   @out,
                                                int      out_offset,
                                                ref uint out_size,
                                                bool     is_last_buffer = false)
        {
            // check the parameters
            // PORT NOTE: `if (out_size == NULL) return AP4_ERROR_INVALID_PARAMETERS;`
            // cannot happen with a `ref` parameter

            // check the state
            if (_blockCipher == null || _eos) {
                out_size = 0;
                return Ap4Result.ERROR_INVALID_STATE;
            }
            if (is_last_buffer) _eos = true;

            if (_blockCipher.GetDirection() == BlockCipher.CipherDirection.ENCRYPT) {
                return EncryptBuffer(@in, in_offset, in_size, @out, out_offset, ref out_size, is_last_buffer);
            } else {
                return DecryptBuffer(@in, in_offset, in_size, @out, out_offset, ref out_size, is_last_buffer);
            }
        }

        // members
        private ulong        _streamOffset;
        private uint         _outputSkip;
        private byte[]       _inBlock = new byte[Ap4.CIPHER_BLOCK_SIZE];
        private uint         _inBlockFullness;
        private byte[]       _chainBlock = new byte[Ap4.CIPHER_BLOCK_SIZE];
        private uint         _chainBlockFullness;
        private byte[]       _iv = new byte[Ap4.CIPHER_BLOCK_SIZE];
        private BlockCipher  _blockCipher;
        private bool         _eos;
    }

    /*----------------------------------------------------------------------
    |   AP4_PatternStreamCipher
    +---------------------------------------------------------------------*/
    public class PatternStreamCipher : StreamCipher
    {
        // methods

        /*----------------------------------------------------------------------
        |   AP4_PatternStreamCipher
        +---------------------------------------------------------------------*/
        /// <summary>
        /// The stream cipher is passed with transfer of ownership (it will
        /// be destroyed when this object is destroyed).
        /// </summary>
        public PatternStreamCipher(StreamCipher cipher,
                                   byte         crypt_byte_block,
                                   byte         skip_byte_block)
        {
            _cipher = cipher;
            _cryptByteBlock = crypt_byte_block;
            _skipByteBlock = skip_byte_block;
            _streamOffset = 0;
        }

        // AP4_StreamCipher implementation
        /*----------------------------------------------------------------------
        |   AP4_PatternStreamCipher::SetStreamOffset
        +---------------------------------------------------------------------*/
        public override Ap4Result SetStreamOffset(ulong    offset,
                                                  out uint preroll)
        {
            preroll = 0;
            return Ap4Result.ERROR_NOT_SUPPORTED;
        }

        public override ulong GetStreamOffset() { return _streamOffset; }

        /*----------------------------------------------------------------------
        |   AP4_PatternStreamCipher
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessBuffer(byte[]   @in,
                                                int      in_offset,
                                                uint     in_size,
                                                byte[]   @out,
                                                int      out_offset,
                                                ref uint out_size,
                                                bool     is_last_buffer = false)
        {
            // set default return values
            out_size = 0;

            // check that the range is block-aligned (required by the spec for pattern encryption)
            if ((_streamOffset % 16) != 0) return Ap4Result.ERROR_INVALID_FORMAT;

            // compute where we are in the pattern
            uint pattern_span     = (uint)(_cryptByteBlock+_skipByteBlock);
            uint block_position   = (uint)(_streamOffset/16);
            uint pattern_position = block_position % pattern_span;

            // process the range
            while (out_size < in_size) {
                uint crypt_size = 0;
                uint skip_size  = (uint)(_skipByteBlock*16);
                if (pattern_position < _cryptByteBlock) {
                    // in the encrypted part
                    crypt_size = (_cryptByteBlock-pattern_position)*16;
                } else {
                    // in the skipped part
                    skip_size = (pattern_span-pattern_position)*16;
                }

                // clip
                uint remain = in_size-out_size;
                if (crypt_size > remain) {
                    crypt_size = 16*(remain/16);
                    skip_size  = remain-crypt_size;
                }
                if (crypt_size+skip_size > remain) {
                    skip_size = remain-crypt_size;
                }

                // encrypted part
                if (crypt_size != 0) {
                    uint in_chunk_size  = crypt_size;
                    uint out_chunk_size = crypt_size;

                    Ap4Result result = _cipher.ProcessBuffer(@in, in_offset, in_chunk_size, @out, out_offset, ref out_chunk_size);
                    if (result.Failed()) return result;
                    // check that we got back what we expectected
                    if (out_chunk_size != in_chunk_size) {
                        return Ap4Result.ERROR_INTERNAL;
                    }
                    in_offset     += (int)crypt_size;
                    out_offset    += (int)crypt_size;
                    out_size      += crypt_size;
                    _streamOffset += crypt_size;
                }

                // skipped part
                if (skip_size != 0) {
                    Array.Copy(@in, in_offset, @out, out_offset, (int)skip_size);
                    in_offset     += (int)skip_size;
                    out_offset    += (int)skip_size;
                    out_size      += skip_size;
                    _streamOffset += skip_size;
                }

                // we're now at the start of a new pattern
                pattern_position = 0;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_PatternStreamCipher
        +---------------------------------------------------------------------*/
        public override Ap4Result SetIV(byte[] iv)
        {
            _streamOffset = 0;
            return _cipher.SetIV(iv);
        }

        /*----------------------------------------------------------------------
        |   AP4_PatternStreamCipher
        +---------------------------------------------------------------------*/
        public override byte[] GetIV()
        {
            return _cipher.GetIV();
        }

        // members
        private StreamCipher _cipher;
        private byte         _cryptByteBlock;
        private byte         _skipByteBlock;
        private ulong        _streamOffset;
    }
}
