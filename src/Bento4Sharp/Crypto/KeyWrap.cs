// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Crypto/Ap4KeyWrap.h, Ap4KeyWrap.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

/*
 Portions of this code are based on the code of LibTomCrypt
 that was released into public domain by Tom St Denis.
*/

using System;

namespace Bento4
{

    // PORT NOTE: the free functions AP4_AesKeyWrap / AP4_AesKeyUnwrap are
    // Ap4.AesKeyWrap / Ap4.AesKeyUnwrap.
    public static partial class Ap4
    {
        /*----------------------------------------------------------------------
        |   AP4_AesKeyWrap
        +---------------------------------------------------------------------*/
        public static Ap4Result AesKeyWrap(byte[]     kek,
                                           byte[]     cleartext_key,
                                           uint       cleartext_key_size,
                                           DataBuffer wrapped_key)
        {
            // check parameters
            if ((cleartext_key_size % 8) != 0) {
                // not a multiple of 64 bits
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            // the output size is (n+1)*64 bits
            // where n is the number of 64-bit blocks
            // of the cleartext key
            uint n = cleartext_key_size/8;
            wrapped_key.SetDataSize((n+1)*8);

            // Step 1. Initialize variables.
            // Set A = IV, an initial value (0xA6)
            // For i = 1 to n
            //     R[i] = P[i]
            byte[] a = wrapped_key.UseData();   // `a` is at offset 0
            for (int x=0; x<8; x++) a[x] = 0xA6;
            int r = 8;                          // `r` is an offset in the same buffer
            Array.Copy(cleartext_key, 0, a, r, (int)cleartext_key_size);

            // Step 2. Calculate intermediate values.
            // For j = 0 to 5
            //     For i=1 to n
            //         B = AES(K, A | R[i])
            //         A = MSB(64, B) ^ t where t = (n*j)+i
            //         R[i] = LSB(64, B)
            AesBlockCipher block_cipher = null;
            Ap4Result result = AesBlockCipher.Create(kek,
                                                     BlockCipher.CipherDirection.ENCRYPT,
                                                     BlockCipher.CipherMode.CBC,
                                                     null,
                                                     out block_cipher);
            if (result.Failed()) return result;
            for (uint j=0; j <= 5; j++) {
                r = 8;
                for (uint i=1; i<=n; i++) {
                    byte[] workspace = new byte[16];
                    byte[] b = new byte[16];
                    Array.Copy(a, 0, workspace, 0, 8);
                    Array.Copy(a, r, workspace, 8, 8);
                    block_cipher.Process(workspace, 16, b, null);
                    Array.Copy(b, 0, a, 0, 8);
                    a[7] ^= (byte)(n*j+i);
                    Array.Copy(b, 8, a, r, 8);
                    r += 8;
                }
            }

            // Step 3. Output the results.
            // (Nothing to do here since we've worked in-place
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AesKeyUnwrap
        +---------------------------------------------------------------------*/
        public static Ap4Result AesKeyUnwrap(byte[]     kek,
                                             byte[]     wrapped_key,
                                             uint       wrapped_key_size,
                                             DataBuffer cleartext_key)
        {
            // check parameters
            if ((wrapped_key_size % 8) != 0 || (wrapped_key_size < 24)) {
                // not a multiple of 64 bits or too small
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            // setup the output buffer
            uint n = (wrapped_key_size/8)-1;
            cleartext_key.SetDataSize(n*8);

            // Step 1. Initialize variables.
            // Set A = C[0]
            // For i = 1 to n
            //     R[i] = C[i]
            byte[] a = new byte[8];
            Array.Copy(wrapped_key, 0, a, 0, 8);
            byte[] r_data = cleartext_key.UseData();
            int r = 0;                          // `r` is an offset in r_data
            Array.Copy(wrapped_key, 8, r_data, r, (int)(8*n));

            // Step 2. Compute intermediate values.
            // For j = 5 to 0
            //   For i = n to 1
            //     B = AES-1(K, (A ^ t) | R[i]) where t = n*j+i
            //     A = MSB(64, B)
            //     R[i] = LSB(64, B)
            AesBlockCipher block_cipher = null;
            Ap4Result result = AesBlockCipher.Create(kek,
                                                     BlockCipher.CipherDirection.DECRYPT,
                                                     BlockCipher.CipherMode.CBC,
                                                     null,
                                                     out block_cipher);
            if (result.Failed()) return result;
            for (int j=5; j>=0; j--) {
                r = (int)((n-1)*8);
                for (int i=(int)n; i>=1; i--) {
                    byte[] workspace = new byte[16];
                    byte[] b = new byte[16];
                    Array.Copy(a, 0, workspace, 0, 8);
                    workspace[7] ^= (byte)((n*(uint)j)+(uint)i);
                    Array.Copy(r_data, r, workspace, 8, 8);
                    block_cipher.Process(workspace, 16, b, null);
                    Array.Copy(b, 0, a, 0, 8);
                    Array.Copy(b, 8, r_data, r, 8);
                    r -= 8;
                }
            }

            // Step 3. Output results.
            // If A is an appropriate initial value (see 2.2.3),
            // Then
            //   For i = 1 to n
            //     P[i] = R[i]
            // Else
            // Return an error
            for (uint i=0; i<8; i++) {
                if (a[i] != 0xA6) {
                    cleartext_key.SetDataSize(0);
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
            }

            return Ap4Result.SUCCESS;
        }
    }
}
