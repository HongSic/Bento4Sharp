// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Crypto/Ap4Hmac.h, Ap4Hmac.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

/*
 Portions of this code are based on the code of LibTomCrypt
 that was released into public domain by Tom St Denis.
*/

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Hmac
    +---------------------------------------------------------------------*/
    public abstract class Hmac
    {
        // types
        public enum Algorithm {
            SHA256
        }

        // class methods
        /*----------------------------------------------------------------------
        |   AP4_Hmac::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(Algorithm algorithm,
                                       byte[]    key,
                                       uint      key_size,
                                       out Hmac  hmac)
        {
            switch (algorithm) {
                case Algorithm.SHA256: hmac = new HmacSha256(key, key_size); return Ap4Result.SUCCESS;
                default: hmac = null; return Ap4Result.ERROR_NOT_SUPPORTED;
            }
        }

        // methods
        public abstract Ap4Result Update(byte[] data, int offset, uint data_size);
        public Ap4Result Update(byte[] data, uint data_size) { return Update(data, 0, data_size); }
        public abstract Ap4Result Final(DataBuffer mac);
    }

    /*----------------------------------------------------------------------
    |   AP4_DigestSha256
    +---------------------------------------------------------------------*/
    internal class DigestSha256
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        public const uint AP4_SHA256_BLOCK_SIZE = 64;

        private static readonly uint[] AP4_Sha256_K = new uint[64] {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b,
            0x59f111f1, 0x923f82a4, 0xab1c5ed5, 0xd807aa98, 0x12835b01,
            0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7,
            0xc19bf174, 0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc,
            0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da, 0x983e5152,
            0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147,
            0x06ca6351, 0x14292967, 0x27b70a85, 0x2e1b2138, 0x4d2c6dfc,
            0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819,
            0xd6990624, 0xf40e3585, 0x106aa070, 0x19a4c116, 0x1e376c08,
            0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f,
            0x682e6ff3, 0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
            0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
        };

        /*----------------------------------------------------------------------
        |   AP4_DigestSha256::AP4_DigestSha256
        +---------------------------------------------------------------------*/
        public DigestSha256()
        {
            _length = 0;
            _pending = 0;
            _state[0] = 0x6A09E667;
            _state[1] = 0xBB67AE85;
            _state[2] = 0x3C6EF372;
            _state[3] = 0xA54FF53A;
            _state[4] = 0x510E527F;
            _state[5] = 0x9B05688C;
            _state[6] = 0x1F83D9AB;
            _state[7] = 0x5BE0CD19;
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        /*----------------------------------------------------------------------
        |   local macros
        +---------------------------------------------------------------------*/
        private static uint AP4_Sha256_RORc(uint x, int y) { return (x >> (y & 31)) | (x << (32 - (y & 31))); }
        private static uint AP4_Sha256_Ch(uint x, uint y, uint z)  { return (z ^ (x & (y ^ z))); }
        private static uint AP4_Sha256_Maj(uint x, uint y, uint z) { return (((x | y) & z) | (x & y)); }
        private static uint AP4_Sha256_S(uint x, int n)            { return AP4_Sha256_RORc(x, n); }
        private static uint AP4_Sha256_R(uint x, int n)            { return x >> n; }
        private static uint AP4_Sha256_Sigma0(uint x)              { return (AP4_Sha256_S(x,  2) ^ AP4_Sha256_S(x, 13) ^ AP4_Sha256_S(x, 22)); }
        private static uint AP4_Sha256_Sigma1(uint x)              { return (AP4_Sha256_S(x,  6) ^ AP4_Sha256_S(x, 11) ^ AP4_Sha256_S(x, 25)); }
        private static uint AP4_Sha256_Gamma0(uint x)              { return (AP4_Sha256_S(x,  7) ^ AP4_Sha256_S(x, 18) ^ AP4_Sha256_R(x,  3)); }
        private static uint AP4_Sha256_Gamma1(uint x)              { return (AP4_Sha256_S(x, 17) ^ AP4_Sha256_S(x, 19) ^ AP4_Sha256_R(x, 10)); }

        /*----------------------------------------------------------------------
        |   AP4_DigestSha256::CompressBlock
        +---------------------------------------------------------------------*/
        private void CompressBlock(byte[] block, int offset)
        {
            uint[] S = new uint[8];
            uint[] W = new uint[64];

            /* copy the state into S */
            for (uint i = 0; i < 8; i++) {
                S[i] = _state[i];
            }

            /* copy the 512-bit block into W[0..15] */
            for (uint i = 0; i < 16; i++) {
                W[i] = Ap4Utils.BytesToUInt32BE(block, offset+(int)(4*i));
            }

            /* fill W[16..63] */
            for (uint i = 16; i < AP4_SHA256_BLOCK_SIZE; i++) {
                W[i] = AP4_Sha256_Gamma1(W[i-2]) + W[i-7] + AP4_Sha256_Gamma0(W[i-15]) + W[i-16];
            }

            /* compress */
            uint t, t0, t1;
            for (uint i = 0; i < AP4_SHA256_BLOCK_SIZE; ++i) {
                t0 = S[7] + AP4_Sha256_Sigma1(S[4]) + AP4_Sha256_Ch(S[4], S[5], S[6]) + AP4_Sha256_K[i] + W[i];
                t1 = AP4_Sha256_Sigma0(S[0]) + AP4_Sha256_Maj(S[0], S[1], S[2]);
                S[3] += t0;
                S[7]  = t0 + t1;
                t = S[7]; S[7] = S[6]; S[6] = S[5]; S[5] = S[4];
                S[4] = S[3]; S[3] = S[2]; S[2] = S[1]; S[1] = S[0]; S[0] = t;
            }

            /* feedback */
            for (uint i = 0; i < 8; i++) {
                _state[i] = _state[i] + S[i];
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_DigestSha256::Update
        +---------------------------------------------------------------------*/
        public virtual Ap4Result Update(byte[] data, int offset, uint data_size)
        {
            while (data_size > 0) {
                if (_pending == 0 && data_size >= AP4_SHA256_BLOCK_SIZE) {
                    CompressBlock(data, offset);
                    _length   += AP4_SHA256_BLOCK_SIZE * 8;
                    offset    += (int)AP4_SHA256_BLOCK_SIZE;
                    data_size -= AP4_SHA256_BLOCK_SIZE;
                } else {
                    uint chunk = data_size;
                    if (chunk > (AP4_SHA256_BLOCK_SIZE - _pending)) {
                        chunk = AP4_SHA256_BLOCK_SIZE - _pending;
                    }
                    Array.Copy(data, offset, _buffer, (int)_pending, (int)chunk);
                    _pending  += chunk;
                    offset    += (int)chunk;
                    data_size -= chunk;
                    if (_pending == AP4_SHA256_BLOCK_SIZE) {
                        CompressBlock(_buffer, 0);
                        _length += 8 * AP4_SHA256_BLOCK_SIZE;
                        _pending = 0;
                    }
                }
            }

            return Ap4Result.SUCCESS;
        }

        public Ap4Result Update(byte[] data, uint data_size) { return Update(data, 0, data_size); }

        /*----------------------------------------------------------------------
        |   AP4_DigestSha256::Final
        +---------------------------------------------------------------------*/
        public virtual Ap4Result Final(DataBuffer digest)
        {
            /* increase the length of the message */
            _length += _pending * 8;

            /* append the '1' bit */
            _buffer[_pending++] = 0x80;

            /* if the length is currently above 56 bytes we append zeros
             * then compress.  Then we can fall back to padding zeros and length
             * encoding like normal.
             */
            if (_pending > 56) {
                while (_pending < 64) {
                    _buffer[_pending++] = 0;
                }
                CompressBlock(_buffer, 0);
                _pending = 0;
            }

            /* pad upto 56 bytes of zeroes */
            while (_pending < 56) {
                _buffer[_pending++] = 0;
            }

            /* store length */
            Ap4Utils.BytesFromUInt64BE(_buffer, 56, _length);
            CompressBlock(_buffer, 0);

            /* copy output */
            digest.SetDataSize(32);
            byte[] @out = digest.UseData();
            int out_offset = 0;
            for (uint i = 0; i < 8; i++) {
                Ap4Utils.BytesFromUInt32BE(@out, out_offset, _state[i]);
                out_offset += 4;
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private ulong  _length;
        private uint   _pending;
        private uint[] _state = new uint[8];
        private byte[] _buffer = new byte[64];
    }

    /*----------------------------------------------------------------------
    |   AP4_HmacSha256
    |
    |   compute SHA256(key XOR opad, SHA256(key XOR ipad, data))
    |   key is the MAC key
    |   ipad is the byte 0x36 repeated 64 times
    |   opad is the byte 0x5c repeated 64 times
    |   and data is the data to authenticate
    |
    +---------------------------------------------------------------------*/
    internal class HmacSha256 : Hmac
    {
        /*----------------------------------------------------------------------
        |   AP4_HmacSha256::AP4_HmacSha256
        +---------------------------------------------------------------------*/
        public HmacSha256(byte[] key, uint key_size)
        {
            byte[] workspace = new byte[DigestSha256.AP4_SHA256_BLOCK_SIZE];

            /* if the key is larger than the block size, use a digest of the key */
            if (key_size > DigestSha256.AP4_SHA256_BLOCK_SIZE) {
                DigestSha256 kdigest = new DigestSha256();
                kdigest.Update(key, key_size);
                DataBuffer hk = new DataBuffer();
                kdigest.Final(hk);
                key = hk.GetData();
                key_size = hk.GetDataSize();
            }

            /* compute key XOR ipad */
            for (uint i = 0; i < key_size; i++) {
                workspace[i] = (byte)(key[i] ^ 0x36);
            }
            for (uint i = key_size; i < DigestSha256.AP4_SHA256_BLOCK_SIZE; i++) {
                workspace[i] = 0x36;
            }

            /* start the inner digest with (key XOR ipad) */
            _innerDigest.Update(workspace, DigestSha256.AP4_SHA256_BLOCK_SIZE);

            /* compute key XOR opad */
            for (uint i = 0; i < key_size; i++) {
                workspace[i] = (byte)(key[i] ^ 0x5c);
            }
            for (uint i = key_size; i < DigestSha256.AP4_SHA256_BLOCK_SIZE; i++) {
                workspace[i] = 0x5c;
            }

            /* start the outer digest with (key XOR opad) */
            _outerDigest.Update(workspace, DigestSha256.AP4_SHA256_BLOCK_SIZE);
        }

        // AP4_Hmac methods
        public override Ap4Result Update(byte[] data, int offset, uint data_size) {
            return _innerDigest.Update(data, offset, data_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_HmacSha256::Final
        +---------------------------------------------------------------------*/
        public override Ap4Result Final(DataBuffer mac)
        {
            /* finish the outer digest with the value of the inner digest */
            DataBuffer inner = new DataBuffer();
            _innerDigest.Final(inner);
            _outerDigest.Update(inner.GetData(), inner.GetDataSize());

            /* return the value of the outer digest */
            return _outerDigest.Final(mac);
        }

        private DigestSha256 _innerDigest = new DigestSha256();
        private DigestSha256 _outerDigest = new DigestSha256();
    }
}
