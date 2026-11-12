// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Crypto/Ap4AesBlockCipher.h, Ap4AesBlockCipher.cpp
// Original work Copyright 2005-2008 Axiomatic Systems, LLC
// Portions (c) 2001, Dr Brian Gladman
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    // PORT NOTE (applies to the whole file):
    // - upstream embeds Brian Gladman's table based AES implementation
    //   (aes_ctx, aes_enc_key, aes_dec_key, aes_enc_blk, aes_dec_blk). As decided
    //   in docs/PORTING.md the tables are not ported: the block transform is done
    //   by System.Security.Cryptography.Aes (ECB, no padding, one block at a
    //   time; on .NET Framework 2.0, which has no Aes class, by RijndaelManaged
    //   with a 128 bit block, which is the same cipher). The class API and the cipher mode logic (CBC chaining, CTR counter
    //   handling) are ported line by line.
    // - the optional AES-NI CTR implementation (AP4_AesNICtrBlockCipher, only
    //   compiled upstream when __AES__ and __SSE4_2__ are defined) is not ported;
    //   the portable AP4_AesCtrBlockCipher is always used.

    /*----------------------------------------------------------------------
    |   AES constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint AES_BLOCK_SIZE = 16;
        public const uint AES_KEY_LENGTH = 16;
    }

    /*----------------------------------------------------------------------
    |   AES types
    +---------------------------------------------------------------------*/
    // PORT NOTE: replaces upstream `struct aes_ctx` (the key schedule); wraps the
    // platform AES implementation.
    internal sealed class AesCtx
    {
        public AesCtx(byte[] key)
        {
            byte[] k = new byte[Ap4.AES_KEY_LENGTH];
            Array.Copy(key, 0, k, 0, (int)Ap4.AES_KEY_LENGTH);
#if !NET20
            _aes = System.Security.Cryptography.Aes.Create();
#else
            // PORT NOTE: System.Security.Cryptography.Aes is in System.Core
            // (.NET Framework 3.5). AES is Rijndael with a 128 bit block.
            _aes = new System.Security.Cryptography.RijndaelManaged();
            _aes.BlockSize = (int)Ap4.AES_BLOCK_SIZE*8;
            _aes.KeySize = (int)Ap4.AES_KEY_LENGTH*8;
#endif
            _aes.Key = k;
#if !NET6_0_OR_GREATER
            // (EncryptEcb/DecryptEcb take the mode and padding as arguments)
            _aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            _aes.Padding = System.Security.Cryptography.PaddingMode.None;
#endif
        }

#if NET6_0_OR_GREATER
        // aes_enc_blk
        public void EncBlk(byte[] in_blk, int in_offset, byte[] out_blk, int out_offset)
        {
            // the input is fully read before the output is written, as upstream
            // (state_in ... state_out), so in-place operation behaves the same
            _aes.EncryptEcb(new ReadOnlySpan<byte>(in_blk, in_offset, (int)Ap4.AES_BLOCK_SIZE),
                            new Span<byte>(_tmp),
                            System.Security.Cryptography.PaddingMode.None);
            Array.Copy(_tmp, 0, out_blk, out_offset, (int)Ap4.AES_BLOCK_SIZE);
        }

        // aes_dec_blk
        public void DecBlk(byte[] in_blk, int in_offset, byte[] out_blk, int out_offset)
        {
            _aes.DecryptEcb(new ReadOnlySpan<byte>(in_blk, in_offset, (int)Ap4.AES_BLOCK_SIZE),
                            new Span<byte>(_tmp),
                            System.Security.Cryptography.PaddingMode.None);
            Array.Copy(_tmp, 0, out_blk, out_offset, (int)Ap4.AES_BLOCK_SIZE);
        }
#else
        // PORT NOTE: Aes.EncryptEcb/DecryptEcb do not exist on the older targets:
        // the single block goes through an ECB/no padding transform, created once
        // per direction on first use and reused for every block.

        // aes_enc_blk
        public void EncBlk(byte[] in_blk, int in_offset, byte[] out_blk, int out_offset)
        {
            if (_encryptor == null) _encryptor = _aes.CreateEncryptor();
            TransformBlk(_encryptor, in_blk, in_offset, out_blk, out_offset);
        }

        // aes_dec_blk
        public void DecBlk(byte[] in_blk, int in_offset, byte[] out_blk, int out_offset)
        {
            if (_decryptor == null) _decryptor = _aes.CreateDecryptor();
            TransformBlk(_decryptor, in_blk, in_offset, out_blk, out_offset);
        }

        private void TransformBlk(System.Security.Cryptography.ICryptoTransform transform,
                                  byte[] in_blk, int in_offset, byte[] out_blk, int out_offset)
        {
            // the input is fully read before the output is written (see above)
            int count = transform.TransformBlock(in_blk, in_offset, (int)Ap4.AES_BLOCK_SIZE, _tmp, 0);
            if (count != (int)Ap4.AES_BLOCK_SIZE) {
                throw new InvalidOperationException("AES block transform returned " + count + " bytes");
            }
            Array.Copy(_tmp, 0, out_blk, out_offset, (int)Ap4.AES_BLOCK_SIZE);
        }

        private System.Security.Cryptography.ICryptoTransform _encryptor;
        private System.Security.Cryptography.ICryptoTransform _decryptor;
#endif

#if !NET20
        private readonly System.Security.Cryptography.Aes _aes;
#else
        private readonly System.Security.Cryptography.SymmetricAlgorithm _aes;
#endif
        private readonly byte[] _tmp = new byte[Ap4.AES_BLOCK_SIZE];
    }

    /*----------------------------------------------------------------------
    |   AP4_AesBlockCipher class
    +---------------------------------------------------------------------*/
    public abstract class AesBlockCipher : BlockCipher
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_AesBlockCipher::Create
        +---------------------------------------------------------------------*/
        // PORT NOTE: `const void* mode_params` is an `object?`: null, or a
        // BlockCipher.CtrParams / BlockCipherFactory.CtrParams for the CTR mode.
        public static Ap4Result Create(byte[]              key,
                                       CipherDirection     direction,
                                       CipherMode          mode,
                                       object              mode_params,
                                       out AesBlockCipher  cipher)
        {
            cipher = null;

            // PORT NOTE: upstream reads AP4_AES_KEY_LENGTH bytes from `key` without
            // any check (its callers check); a null or short key is undefined
            // behaviour there and is reported as an error here.
            if (key == null || key.Length < Ap4.AES_KEY_LENGTH) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            AesCtx context;

            switch (mode) {
                case BlockCipher.CipherMode.CBC:
                    // PORT NOTE: aes_enc_key/aes_dec_key: the platform AES has a
                    // single key setup for both directions
                    context = new AesCtx(key);
                    cipher = new AesCbcBlockCipher(direction, context);
                    break;

                case BlockCipher.CipherMode.CTR: {
                    context = new AesCtx(key);
                    uint counter_size = 16;
                    BlockCipher.CtrParams ctr_params;
                    BlockCipherFactory.CtrParams factory_ctr_params;
                    if (mode_params is BlockCipher.CtrParams) {
                        ctr_params = (BlockCipher.CtrParams)mode_params;
                        counter_size = ctr_params.counter_size;
                    } else if (mode_params is BlockCipherFactory.CtrParams) {
                        factory_ctr_params = (BlockCipherFactory.CtrParams)mode_params;
                        counter_size = factory_ctr_params.counter_size;
                    }
                    cipher = new AesCtrBlockCipher(direction, counter_size, context);
                    break;
                }

                default:
                    return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            return Ap4Result.SUCCESS;
        }

        public override CipherDirection GetDirection() { return _direction; }

        // constructor
        internal AesBlockCipher(CipherDirection direction,
                                CipherMode      mode,
                                AesCtx          context)
        {
            _direction = direction;
            _mode = mode;
            _context = context;
        }

        // members
        protected CipherDirection _direction;
        protected CipherMode      _mode;
        internal  AesCtx          _context;
    }

    /*----------------------------------------------------------------------
    |   AP4_AesCbcBlockCipher
    +---------------------------------------------------------------------*/
    internal sealed class AesCbcBlockCipher : AesBlockCipher
    {
        public AesCbcBlockCipher(CipherDirection direction,
                                 AesCtx          context) :
            base(direction, CipherMode.CBC, context) {}

        // AP4_BlockCipher methods
        /*----------------------------------------------------------------------
        |   AP4_AesCbcBlockCipher::Process
        +---------------------------------------------------------------------*/
        public override Ap4Result Process(byte[]  input,
                                          int     input_offset,
                                          uint    input_size,
                                          byte[]  output,
                                          int     output_offset,
                                          byte[]  iv)
        {
            // check the parameters
            if ((input_size%Ap4.AES_BLOCK_SIZE) != 0) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            // setup the chaining block from the IV
            byte[] chaining_block = new byte[Ap4.AES_BLOCK_SIZE];
            if (iv != null) {
                Array.Copy(iv, 0, chaining_block, 0, (int)Ap4.AES_BLOCK_SIZE);
            } else {
                Array.Clear(chaining_block, 0, (int)Ap4.AES_BLOCK_SIZE);
            }

            // process all blocks
            uint block_count = input_size/Ap4.AES_BLOCK_SIZE;
            if (_direction == CipherDirection.ENCRYPT) {
                byte[] block = new byte[Ap4.AES_BLOCK_SIZE];
                for (uint i=0; i<block_count; i++) {
                    for (uint j=0; j<Ap4.AES_BLOCK_SIZE; j++) {
                        block[j] = (byte)(input[input_offset+j] ^ chaining_block[j]);
                    }
                    _context.EncBlk(block, 0, output, output_offset);
                    Array.Copy(output, output_offset, chaining_block, 0, (int)Ap4.AES_BLOCK_SIZE);
                    input_offset  += (int)Ap4.AES_BLOCK_SIZE;
                    output_offset += (int)Ap4.AES_BLOCK_SIZE;
                }
            } else {
                for (uint i=0; i<block_count; i++) {
                    _context.DecBlk(input, input_offset, output, output_offset);
                    for (uint j=0; j<Ap4.AES_BLOCK_SIZE; j++) {
                        output[output_offset+j] ^= chaining_block[j];
                    }
                    Array.Copy(input, input_offset, chaining_block, 0, (int)Ap4.AES_BLOCK_SIZE);
                    input_offset  += (int)Ap4.AES_BLOCK_SIZE;
                    output_offset += (int)Ap4.AES_BLOCK_SIZE;
                }
            }

            return Ap4Result.SUCCESS;
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_AesCtrBlockCipher
    +---------------------------------------------------------------------*/
    internal sealed class AesCtrBlockCipher : AesBlockCipher
    {
        public AesCtrBlockCipher(CipherDirection direction,
                                 uint            counter_size,
                                 AesCtx          context) :
            base(direction, CipherMode.CTR, context)
            /*m_CounterSize(counter_size)*/ {}

        // AP4_BlockCipher methods
        /*----------------------------------------------------------------------
        |   AP4_AesCtrBlockCipher::Process
        +---------------------------------------------------------------------*/
        public override Ap4Result Process(byte[]  input,
                                          int     input_offset,
                                          uint    input_size,
                                          byte[]  output,
                                          int     output_offset,
                                          byte[]  iv)
        {
            // copy the iv into the counter
            byte[] counter = new byte[Ap4.AES_BLOCK_SIZE];
            if (iv != null) {
                Array.Copy(iv, 0, counter, 0, (int)Ap4.AES_BLOCK_SIZE);
            } else {
                Array.Clear(counter, 0, (int)Ap4.AES_BLOCK_SIZE);
            }

            // process all blocks
            byte[] block = new byte[Ap4.AES_BLOCK_SIZE];
            while (input_size != 0) {
                _context.EncBlk(counter, 0, block, 0);
                uint chunk = input_size>=Ap4.AES_BLOCK_SIZE?Ap4.AES_BLOCK_SIZE:input_size;
                for (uint j=0; j<chunk; j++) {
                    output[output_offset+j] = (byte)(input[input_offset+j]^block[j]);
                }
                input_size -= chunk;
                if (input_size != 0) {
                    // increment the counter
                    // (as upstream, the carry never propagates into counter[0])
                    for (int x=(int)Ap4.AES_BLOCK_SIZE-1; x != 0; --x) {
                        if (counter[x] == 255) {
                            counter[x] = 0;
                            continue;
                        } else {
                            ++counter[x];
                            break;
                        }
                    }

                    // move to the next block
                    input_offset  += (int)Ap4.AES_BLOCK_SIZE;
                    output_offset += (int)Ap4.AES_BLOCK_SIZE;
                }
            }
            return Ap4Result.SUCCESS;
        }

    //private:
    //    unsigned int m_CounterSize;
    }
}
