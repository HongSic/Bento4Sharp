// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Codecs/Ap4BitStream.h, Ap4BitStream.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const int ERROR_BASE_BITSTREAM = -10000;

        // the max frame size we can handle
        public const uint BITSTREAM_BUFFER_SIZE = 32768;

        // flags
        public const uint BITSTREAM_FLAG_EOS = 0x01;

        // error codes
        // PORT NOTE: Ap4Result is an enum that does not declare these values; they are
        // exposed as Ap4Result constants with the upstream numeric values.
        public const Ap4Result ERROR_CORRUPTED_BITSTREAM    = (Ap4Result)(ERROR_BASE_BITSTREAM - 0);
        public const Ap4Result ERROR_NOT_ENOUGH_FREE_BUFFER = (Ap4Result)(ERROR_BASE_BITSTREAM - 1);

        /* use long by default */
        public const uint WORD_BITS  = 32;
        public const uint WORD_BYTES = 4;
    }

    /*----------------------------------------------------------------------
    |   For efficiency reasons, this bitstream library only handles
    |   data buffers that are a power of 2 in size
    +---------------------------------------------------------------------*/
    /*----------------------------------------------------------------------
    |   BitStream
    +---------------------------------------------------------------------*/
    public class BitStream
    {
        // constructor
        public BitStream()
        {
            _buffer = new byte[Ap4.BITSTREAM_BUFFER_SIZE];
            Reset();
        }

        // macros
        // PORT NOTE: AP4_BIT_MASK is only ever evaluated with n <= 31 (see the call sites),
        // where the int and uint computations agree.
        private static uint BIT_MASK(uint n) { return (1u << (int)n) - 1; }
        private static uint POINTER_VAL(uint offset) { return offset & (Ap4.BITSTREAM_BUFFER_SIZE - 1); }
        private static uint POINTER_OFFSET(uint pointer, uint offset) { return POINTER_VAL(pointer + offset); }

        // methods
        public Ap4Result Reset()
        {
            _in         = 0;
            _out        = 0;
            _bitsCached = 0;
            _cache      = 0;
            _flags      = 0;

            return Ap4Result.SUCCESS;
        }

        public uint GetContiguousBytesFree()
        {
            return
                (_in < _out) ?
                (_out - _in - 1) :
                (_out == 0 ? (Ap4.BITSTREAM_BUFFER_SIZE - _in - 1) :
                             (Ap4.BITSTREAM_BUFFER_SIZE - _in));
        }

        public uint GetBytesFree()
        {
            return
                (_in < _out) ?
                (_out - _in - 1) :
                (Ap4.BITSTREAM_BUFFER_SIZE + (_out - _in) - 1);
        }

        public Ap4Result WriteBytes(byte[] bytes, uint byte_count)
        {
            return WriteBytes(bytes, 0, byte_count);
        }

        public Ap4Result WriteBytes(byte[] bytes, int offset, uint byte_count)
        {
            /* check parameters */
            if (byte_count == 0) return Ap4Result.SUCCESS;
            if (bytes == null) return Ap4Result.ERROR_INVALID_PARAMETERS;

            /* check that we have enough space */
            if (GetBytesFree() < byte_count) {
                return Ap4Result.FAILURE;
            }

            /* write the bytes */
            if (_in < _out) {
                Array.Copy(bytes, offset, _buffer, (int)_in, (int)byte_count);
                _in = POINTER_OFFSET(_in, byte_count);
            } else {
                uint chunk = Ap4.BITSTREAM_BUFFER_SIZE - _in;
                if (chunk > byte_count) chunk = byte_count;

                Array.Copy(bytes, offset, _buffer, (int)_in, (int)chunk);
                _in = POINTER_OFFSET(_in, chunk);

                if (chunk != byte_count) {
                    Array.Copy(bytes, offset + (int)chunk, _buffer, (int)_in, (int)(byte_count - chunk));
                    _in = POINTER_OFFSET(_in, byte_count - chunk);
                }
            }

            return Ap4Result.SUCCESS;
        }

        public uint GetContiguousBytesAvailable()
        {
            return
                (_out <= _in) ?
                (_in - _out) :
                (Ap4.BITSTREAM_BUFFER_SIZE - _out);
        }

        public uint GetBytesAvailable()
        {
            return
                (_out <= _in) ?
                (_in - _out) :
                (_in + (Ap4.BITSTREAM_BUFFER_SIZE - _out));
        }

        public Ap4Result ReadBytes(byte[] bytes, uint byte_count)
        {
            return ReadBytes(bytes, 0, byte_count);
        }

        public Ap4Result ReadBytes(byte[] bytes, int offset, uint byte_count)
        {
            if (byte_count == 0 || bytes == null) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            /* Gets bytes from the cache */
            ByteAlign();
            while (_bitsCached > 0 && byte_count > 0) {
                bytes[offset] = (byte)ReadBits(8);
                ++offset;
                --byte_count;
            }

            /* Get other bytes */
            if (byte_count > 0) {
                if (_out < _in) {
                    Array.Copy(_buffer, (int)_out, bytes, offset, (int)byte_count);
                    _out = POINTER_OFFSET(_out, byte_count);
                } else {
                    uint chunk = Ap4.BITSTREAM_BUFFER_SIZE - _out;
                    if (chunk >= byte_count) chunk = byte_count;

                    Array.Copy(_buffer, (int)_out, bytes, offset, (int)chunk);
                    _out = POINTER_OFFSET(_out, chunk);

                    if (chunk != byte_count) {
                        Array.Copy(_buffer, (int)_out, bytes, offset + (int)chunk, (int)(byte_count - chunk));
                        _out = POINTER_OFFSET(_out, byte_count - chunk);
                    }
                }
            }

            return Ap4Result.SUCCESS;
        }

        public byte PeekByte()
        {
            uint extra_bits = _bitsCached & 7;
            return (byte)(PeekBits(extra_bits + 8) & 0xFF);
        }

        public Ap4Result PeekBytes(byte[] bytes, uint byte_count)
        {
            return PeekBytes(bytes, 0, byte_count);
        }

        public Ap4Result PeekBytes(byte[] bytes, int offset, uint byte_count)
        {
            int bits_cached_byte;

            if (byte_count == 0 || bytes == null) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            /* Gets bytes from the cache */
            bits_cached_byte = (int)(_bitsCached & ~7u);
            while (bits_cached_byte > 0 && byte_count > 0) {
                bytes[offset] = (byte)((_cache >> bits_cached_byte) & 0xFF);
                ++offset;
                --byte_count;
                bits_cached_byte -= 8;
            }

            /* Get other bytes */
            if (byte_count > 0) {
                if (_in > _out) {
                    Array.Copy(_buffer, (int)_out, bytes, offset, (int)byte_count);
                } else {
                    uint @out = _out;
                    uint chunk = Ap4.BITSTREAM_BUFFER_SIZE - @out;
                    if (chunk >= byte_count) {
                        chunk = byte_count;
                    }

                    Array.Copy(_buffer, (int)@out, bytes, offset, (int)chunk);
                    @out = POINTER_OFFSET(@out, chunk);

                    if (chunk != byte_count) {
                        Array.Copy(_buffer, (int)@out, bytes, offset + (int)chunk, (int)(byte_count - chunk));
                    }
                }
            }

            return Ap4Result.SUCCESS;
        }

        public int ReadBit()
        {
            uint result;
            if (_bitsCached == 0) {
                /* the cache is empty */

                /* read the next word into the cache */
                _cache = ReadCache();
                _out = POINTER_OFFSET(_out, Ap4.WORD_BYTES);
                _bitsCached = Ap4.WORD_BITS - 1;

                /* return the first bit */
                result = _cache >> (int)(Ap4.WORD_BITS - 1);
            } else {
                /* get the bit from the cache */
                result = (_cache >> (int)(--_bitsCached)) & 1;
            }
            return (int)result;
        }

        public uint ReadBits(uint n)
        {
            uint result;
            if (_bitsCached >= n) {
                /* we have enough bits in the cache to satisfy the request */
                _bitsCached -= n;
                result = (_cache >> (int)_bitsCached) & BIT_MASK(n);
            } else {
                /* not enough bits in the cache */
                uint word;

                /* read the next word */
                {
                    word = ReadCache();
                    _out = POINTER_OFFSET(_out, Ap4.WORD_BYTES);
                }

                /* combine the new word and the cache, and update the state */
                {
                    uint cache = _cache & BIT_MASK(_bitsCached);
                    n -= _bitsCached;
                    _bitsCached = Ap4.WORD_BITS - n;
                    // PORT NOTE: for n == 32 upstream shifts a 32-bit value by 32 (undefined in C++,
                    // a shift by 0 on x86). C# masks the shift count to 5 bits, i.e. the same result;
                    // `cache` is 0 in that case anyway.
                    result = (word >> (int)_bitsCached) | (cache << (int)n);
                    _cache = word;
                }
            }

            return result;
        }

        public int PeekBit()
        {
            /* the cache is empty */
            if (_bitsCached == 0) {
                /* read the next word into the cache */
                uint cache = ReadCache();

                /* return the first bit */
                return (int)(cache >> (int)(Ap4.WORD_BITS - 1));
            } else {
                /* get the bit from the cache */
                return (int)((_cache >> (int)(_bitsCached - 1)) & 1);
            }
        }

        public uint PeekBits(uint n)
        {
            /* we have enough bits in the cache to satisfy the request */
            if (_bitsCached >= n) {
                return (_cache >> (int)(_bitsCached - n)) & BIT_MASK(n);
            } else {
                /* not enough bits in the cache, read the next word */
                uint word = ReadCache();

                /* combine the new word and the cache, and update the state */
                uint cache = _cache & BIT_MASK(_bitsCached);
                n -= _bitsCached;
                return (word >> (int)(Ap4.WORD_BITS - n)) | (cache << (int)n);
            }
        }

        public Ap4Result SkipBytes(uint byte_count)
        {
            _out = POINTER_OFFSET(_out, byte_count);
            return Ap4Result.SUCCESS;
        }

        public void SkipBit()
        {
            if (_bitsCached == 0) {
                _cache = ReadCache();
                _out = POINTER_OFFSET(_out, Ap4.WORD_BYTES);
                _bitsCached = Ap4.WORD_BITS - 1;
            } else {
                --_bitsCached;
            }
        }

        public void SkipBits(uint n)
        {
            if (n <= _bitsCached) {
                _bitsCached -= n;
            } else {
                n -= _bitsCached;
                while (n >= Ap4.WORD_BITS) {
                    _out = POINTER_OFFSET(_out, Ap4.WORD_BYTES);
                    n -= Ap4.WORD_BITS;
                }
                if (n != 0) {
                    _cache = ReadCache();
                    _bitsCached = Ap4.WORD_BITS - n;
                    _out = POINTER_OFFSET(_out, Ap4.WORD_BYTES);
                } else {
                    _bitsCached = 0;
                    _cache = 0;
                }
            }
        }

        public Ap4Result ByteAlign()
        {
            uint to_flush = _bitsCached & 7;
            if (to_flush > 0) SkipBits(to_flush);

            return Ap4Result.SUCCESS;
        }

        // members
        public byte[] _buffer;
        public uint   _in;
        public uint   _out;
        public uint   _cache;
        public uint   _bitsCached;
        public uint   _flags;

        // methods
        private uint ReadCache()
        {
            uint pos = _out;
            uint cache;

            if (pos <= Ap4.BITSTREAM_BUFFER_SIZE - Ap4.WORD_BYTES) {
                cache =   (((uint)_buffer[pos    ]) << 24)
                        | (((uint)_buffer[pos + 1]) << 16)
                        | (((uint)_buffer[pos + 2]) <<  8)
                        | (((uint)_buffer[pos + 3])      );
            } else {
                cache =   (((uint)_buffer[                   pos    ]) << 24)
                        | (((uint)_buffer[POINTER_OFFSET(pos, 1)]) << 16)
                        | (((uint)_buffer[POINTER_OFFSET(pos, 2)]) <<  8)
                        | (((uint)_buffer[POINTER_OFFSET(pos, 3)])      );
            }
            return cache;
        }
    }
}
