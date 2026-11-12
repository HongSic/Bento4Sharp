// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4OmaDcf.h, Ap4OmaDcf.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint PROTECTION_SCHEME_TYPE_OMA       = 0x6f646b6d; // 'odkm'
        public const uint PROTECTION_SCHEME_VERSION_OMA_20 = 0x00000200;
        public const uint OMA_DCF_BRAND_ODCF               = 0x6f646366; // 'odcf'
        public const uint OMA_DCF_BRAND_OPF2               = 0x6f706632; // 'opf2'
    }

    public enum OmaDcfCipherMode {
        OMA_DCF_CIPHER_MODE_CTR,
        OMA_DCF_CIPHER_MODE_CBC
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfAtomDecrypter
    +---------------------------------------------------------------------*/
    public class OmaDcfAtomDecrypter
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfAtomDecrypter::DecryptAtoms
        +---------------------------------------------------------------------*/
        public static Ap4Result DecryptAtoms(AtomParent                  atoms,
                                             Processor.ProgressListener  listener,
                                             BlockCipherFactory          block_cipher_factory,
                                             ProtectionKeyMap            key_map)
        {
            // default factory
            if (block_cipher_factory == null) {
                block_cipher_factory = DefaultBlockCipherFactory.Instance;
            }

            uint index = 1;
            List<Atom> children = atoms.GetChildren();
            for (int item = 0; item < children.Count; item++) {
                Atom atom = children[item];
                if (atom.GetType() != AtomType.ODRM) continue;

                // check that we have the key
                DataBuffer key = key_map.GetKey(index++);
                if (key == null) return Ap4Result.ERROR_INVALID_PARAMETERS;

                // check that we have all the atoms we need
                ContainerAtom odrm = atom as ContainerAtom;
                if (odrm == null) continue; // not enough info
                OdheAtom odhe = odrm.GetChild(AtomType.ODHE) as OdheAtom;
                if (odhe == null) continue; // not enough info
                OddaAtom odda = odrm.GetChild(AtomType.ODDA) as OddaAtom;
                if (odda == null) continue; // not enough info
                OhdrAtom ohdr = odhe.GetChild(AtomType.OHDR) as OhdrAtom;
                if (ohdr == null) continue; // not enough info

                // do nothing if the atom is not encrypted
                if (ohdr.GetEncryptionMethod() == Ap4.OMA_DCF_ENCRYPTION_METHOD_NULL) {
                    continue;
                }

                // create the byte stream
                ByteStream cipher_stream = null;
                Ap4Result result = CreateDecryptingStream(odrm,
                                                          key.GetData(),
                                                          key.GetDataSize(),
                                                          block_cipher_factory,
                                                          out cipher_stream);
                if (result.Succeeded()) {
                    // replace the odda atom's payload with the decrypted stream
                    odda.SetEncryptedPayload(cipher_stream, ohdr.GetPlaintextLength());

                    // the atom will now be in the clear
                    ohdr.SetEncryptionMethod(Ap4.OMA_DCF_ENCRYPTION_METHOD_NULL);
                    ohdr.SetPaddingScheme(Ap4.OMA_DCF_PADDING_SCHEME_NONE);
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfAtomDecrypter::CreateDecryptingStream
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Returns a byte stream that will produce the decrypted data found
        /// in the 'odda' child atom of an 'odrm' atom
        /// </summary>
        public static Ap4Result CreateDecryptingStream(
            ContainerAtom       odrm,
            byte[]              key,
            uint                key_size,
            BlockCipherFactory  block_cipher_factory,
            out ByteStream      stream)
        {
            // default return values
            stream = null;

            OdheAtom odhe = odrm.GetChild(AtomType.ODHE) as OdheAtom;
            if (odhe == null) return Ap4Result.ERROR_INVALID_FORMAT;
            OddaAtom odda = odrm.GetChild(AtomType.ODDA) as OddaAtom;
            if (odda == null) return Ap4Result.ERROR_INVALID_FORMAT;
            OhdrAtom ohdr = odhe.GetChild(AtomType.OHDR) as OhdrAtom;
            if (ohdr == null) return Ap4Result.ERROR_INVALID_FORMAT;

            // default cipher factory
            if (block_cipher_factory == null) {
                block_cipher_factory = DefaultBlockCipherFactory.Instance;
            }

            // shortcut for non-encrypted files
            if (ohdr.GetEncryptionMethod() == Ap4.OMA_DCF_ENCRYPTION_METHOD_NULL) {
                stream = odda.GetEncryptedPayload();
                return Ap4Result.SUCCESS;
            }

            // if this is part of a group, use the group key to obtain the content
            // key (note that the field called GroupKey in the spec is actually not
            // the group key but the content key encrypted with the group key...
            GrpiAtom  grpi = ohdr.GetChild(AtomType.GRPI) as GrpiAtom;
            byte[]    key_buffer = null;
            if (grpi != null) {
                // sanity check on the encrypted key size
                if (grpi.GetGroupKey().GetDataSize() < 32) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }

                // create a block cipher to decrypt the content key
                BlockCipher  block_cipher =  null;
                Ap4Result    result;

                // create a stream cipher from the block cipher
                StreamCipher stream_cipher = null;
                switch (ohdr.GetEncryptionMethod()) {
                    case Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CBC:
                        result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                   BlockCipher.CipherDirection.DECRYPT,
                                                                   BlockCipher.CipherMode.CBC,
                                                                   null,
                                                                   key,
                                                                   key_size,
                                                                   out block_cipher);
                        if (result.Failed()) return result;
                        stream_cipher = new CbcStreamCipher(block_cipher);
                        break;

                    case Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CTR: {
                        BlockCipher.CtrParams ctr_params = new BlockCipher.CtrParams();
                        ctr_params.counter_size = 16;
                        result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                   BlockCipher.CipherDirection.DECRYPT,
                                                                   BlockCipher.CipherMode.CTR,
                                                                   ctr_params,
                                                                   key,
                                                                   key_size,
                                                                   out block_cipher);
                        if (result.Failed()) return result;
                        stream_cipher = new CtrStreamCipher(block_cipher, 16);
                        break;
                    }

                    default:
                        return Ap4Result.ERROR_NOT_SUPPORTED;
                }

                // set the IV
                stream_cipher.SetIV(grpi.GetGroupKey().GetData());

                // decrypt the content key
                uint key_buffer_size = grpi.GetGroupKey().GetDataSize(); // worst case
                key_buffer = new byte[key_buffer_size];
                result = stream_cipher.ProcessBuffer(grpi.GetGroupKey().GetData(), 16,
                                                     grpi.GetGroupKey().GetDataSize()-16,
                                                     key_buffer, 0,
                                                     ref key_buffer_size,
                                                     true);
                if (result.Failed()) {
                    return result;
                }

                // point to the new key value
                key = key_buffer;
                key_size = key_buffer_size;
            }

            OmaDcfCipherMode mode;
            switch (ohdr.GetEncryptionMethod()) {
                case Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CBC:
                    mode = OmaDcfCipherMode.OMA_DCF_CIPHER_MODE_CBC;
                    break;
                case Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CTR:
                    mode = OmaDcfCipherMode.OMA_DCF_CIPHER_MODE_CTR;
                    break;
                default:
                    return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            {
                Ap4Result result;
                result = CreateDecryptingStream(mode,
                                                odda.GetEncryptedPayload(),
                                                ohdr.GetPlaintextLength(),
                                                key, key_size,
                                                block_cipher_factory,
                                                out stream);

                return result;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfAtomDecrypter::CreateDecryptingStream
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Returns a byte stream that will produce the decrypted data from
        /// an encrypted stream where the IV follows the encrypted bytes.
        /// This method is normally not called directly: most callers will call
        /// the stream factory that takes an 'odrm' atom as an input parameter
        /// </summary>
        public static Ap4Result CreateDecryptingStream(
            OmaDcfCipherMode    mode,
            ByteStream          encrypted_stream,
            ulong               cleartext_size,
            byte[]              key,
            uint                key_size,
            BlockCipherFactory  block_cipher_factory,
            out ByteStream      stream)
        {
            // default return value
            stream = null;

            // default factory
            if (block_cipher_factory == null) {
                block_cipher_factory = DefaultBlockCipherFactory.Instance;
            }

            // get the encrypted size (includes IV and padding)
            ulong encrypted_size = 0;
            Ap4Result result = encrypted_stream.GetSize(out encrypted_size);
            if (result.Failed()) return result;

            // check that the encrypted size is consistent with the cipher mode
            BlockCipher.CipherMode cipher_mode;
            if (mode == OmaDcfCipherMode.OMA_DCF_CIPHER_MODE_CBC) {
                // we need at least 16 bytes of IV and 32 bytes of data+padding
                // we also need a multiple of the block size
                if (encrypted_size < 48 || ((encrypted_size % 16) != 0)) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                cipher_mode = BlockCipher.CipherMode.CBC;
            } else if (mode == OmaDcfCipherMode.OMA_DCF_CIPHER_MODE_CTR) {
                // we need at least 16 bytes of IV
                if (encrypted_size < 16) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                cipher_mode = BlockCipher.CipherMode.CTR;
            } else {
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            // read the IV
            byte[] iv = new byte[16];
            result = encrypted_stream.Seek(0);
            if (result.Failed()) return result;
            result = encrypted_stream.Read(iv, 16);
            if (result.Failed()) return result;

            // create a sub stream with just the encrypted payload without the IV
            ByteStream sub_stream = new SubStream(encrypted_stream, 16, encrypted_size-16);

            // create the decrypting cipher
            result = DecryptingStream.Create(cipher_mode,
                                             sub_stream,
                                             cleartext_size,
                                             iv,
                                             16,
                                             key,
                                             key_size,
                                             block_cipher_factory,
                                             out stream);

            return result;
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfSampleDecrypter
    +---------------------------------------------------------------------*/
    public abstract class OmaDcfSampleDecrypter : SampleDecrypter
    {
        // factory
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfSampleDecrypter::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(ProtectedSampleDescription sample_description,
                                       byte[]                     key,
                                       uint                       key_size,
                                       BlockCipherFactory         block_cipher_factory,
                                       out OmaDcfSampleDecrypter  cipher)
        {
            // default return value
            // PORT NOTE: assigned before the parameter check (C# requires the out
            // parameter to be set on every path; upstream leaves it untouched there)
            cipher = null;

            // check the parameters
            if (key == null || block_cipher_factory == null) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            // default factory
            if (block_cipher_factory == null) {
                block_cipher_factory = DefaultBlockCipherFactory.Instance;
            }

            // get the scheme info atom
            ContainerAtom schi = sample_description.GetSchemeInfo().GetSchiAtom();
            if (schi == null) return Ap4Result.ERROR_INVALID_FORMAT;

            // get and check the cipher params
            // NOTE: we only support an IV Length less than or equal to the cipher block size,
            // and we don't know how to deal with a key indicator length != 0
            OdafAtom  odaf = schi.FindChild("odkm/odaf") as OdafAtom;
            if (odaf != null) {
                if (odaf.GetIvLength() > Ap4.CIPHER_BLOCK_SIZE) return Ap4Result.ERROR_INVALID_FORMAT;
                if (odaf.GetKeyIndicatorLength() != 0) return Ap4Result.ERROR_INVALID_FORMAT;
            } else {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // check the scheme details and create the cipher
            OhdrAtom ohdr = schi.FindChild("odkm/ohdr") as OhdrAtom;
            if (ohdr == null) return Ap4Result.ERROR_INVALID_FORMAT;
            byte encryption_method = ohdr.GetEncryptionMethod();
            if (encryption_method == Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CBC) {
                // in CBC mode, we only support IVs of the same size as the cipher block size
                if (odaf.GetIvLength() != Ap4.CIPHER_BLOCK_SIZE) return Ap4Result.ERROR_INVALID_FORMAT;

                // require RFC_2630 padding
                if (ohdr.GetPaddingScheme() != Ap4.OMA_DCF_PADDING_SCHEME_RFC_2630) {
                    return Ap4Result.ERROR_NOT_SUPPORTED;
                }

                // create the block cipher
                BlockCipher block_cipher = null;
                Ap4Result result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                     BlockCipher.CipherDirection.DECRYPT,
                                                                     BlockCipher.CipherMode.CBC,
                                                                     null,
                                                                     key,
                                                                     key_size,
                                                                     out block_cipher);
                if (result.Failed()) return result;

                // create the cipher
                cipher = new OmaDcfCbcSampleDecrypter(block_cipher,
                                                      odaf.GetSelectiveEncryption());
                return Ap4Result.SUCCESS;
            } else if (encryption_method == Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CTR) {
                // require NONE padding
                if (ohdr.GetPaddingScheme() != Ap4.OMA_DCF_PADDING_SCHEME_NONE) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }

                // create the block cipher
                BlockCipher block_cipher = null;
                BlockCipher.CtrParams ctr_params = new BlockCipher.CtrParams();
                ctr_params.counter_size = odaf.GetIvLength();
                Ap4Result result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                     BlockCipher.CipherDirection.DECRYPT,
                                                                     BlockCipher.CipherMode.CTR,
                                                                     ctr_params,
                                                                     key,
                                                                     key_size,
                                                                     out block_cipher);
                if (result.Failed()) return result;

                // create the cipher
                cipher = new OmaDcfCtrSampleDecrypter(block_cipher,
                                                      odaf.GetIvLength(),
                                                      odaf.GetSelectiveEncryption());
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }
        }

        // constructor and destructor
        public OmaDcfSampleDecrypter(uint iv_length,
                                     bool selective_encryption)
        {
            _ivLength            = iv_length;
            _keyIndicatorLength  = 0;
            _selectiveEncryption = selective_encryption;
        }

        protected uint _ivLength;
        protected uint _keyIndicatorLength;
        protected bool _selectiveEncryption;
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfCtrSampleDecrypter
    +---------------------------------------------------------------------*/
    public class OmaDcfCtrSampleDecrypter : OmaDcfSampleDecrypter
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCtrSampleDecrypter::AP4_OmaDcfCtrSampleDecrypter
        +---------------------------------------------------------------------*/
        public OmaDcfCtrSampleDecrypter(
            BlockCipher  block_cipher,
            uint         iv_length,
            bool         selective_encryption) :
            base(iv_length, selective_encryption)
        {
            _cipher = new CtrStreamCipher(block_cipher, iv_length);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCtrSampleDecrypter::DecryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result DecryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    byte[]     iv = null)
        {
            bool   is_encrypted = true;
            byte[] @in = data_in.GetData();
            int    in_pos = 0; // PORT NOTE: index into `in` (upstream advances the pointer)
            uint   in_size = data_in.GetDataSize();

            // default to 0 output
            { Ap4Result _result = data_out.SetDataSize(0); if (_result.Failed()) return _result; }

            // check the selective encryption flag
            if (_selectiveEncryption) {
                if (in_size < 1) return Ap4Result.ERROR_INVALID_FORMAT;
                is_encrypted = ((@in[0]&0x80)!=0);
                in_pos++;
            }

            // check the size
            uint header_size = (_selectiveEncryption?1u:0u)+(is_encrypted?_ivLength:0u);
            if (header_size > in_size) return Ap4Result.ERROR_INVALID_FORMAT;

            // process the sample data
            uint payload_size = in_size-header_size;
            { Ap4Result _result = data_out.Reserve(payload_size); if (_result.Failed()) return _result; }
            byte[] @out = data_out.UseData();
            if (is_encrypted) {
                // set the IV
                if (_ivLength == 16) {
                    _cipher.SetIV(@in, in_pos);
                } else {
                    byte[] iv_block = new byte[16];
                    Array.Clear(iv_block, 0, 16);
                    Array.Copy(@in, in_pos, iv_block, (int)(16-_ivLength), (int)_ivLength);
                    _cipher.SetIV(iv_block);
                }
                { Ap4Result _result = _cipher.ProcessBuffer(@in, in_pos+(int)_ivLength,
                                                            payload_size,
                                                            @out, 0); if (_result.Failed()) return _result; }
            } else {
                Array.Copy(@in, in_pos, @out, 0, (int)payload_size);
            }
            { Ap4Result _result = data_out.SetDataSize(payload_size); if (_result.Failed()) return _result; }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCtrSampleDecrypter::GetDecryptedSampleSize
        +---------------------------------------------------------------------*/
        public override uint GetDecryptedSampleSize(Sample sample)
        {
            if (_cipher == null) return 0;

            // decide if this sample is encrypted or not
            bool is_encrypted;
            if (_selectiveEncryption) {
                // read the first byte to see if the sample is encrypted or not
                byte[] h = new byte[1];
                DataBuffer peek_buffer = new DataBuffer();
                peek_buffer.SetBuffer(h, 1);
                sample.ReadData(peek_buffer, 1);
                is_encrypted = ((peek_buffer.GetData()[0]&0x80)!=0);
            } else {
                is_encrypted = true;
            }

            uint crypto_header_size = (_selectiveEncryption?1u:0u)+(is_encrypted?_ivLength:0u);
            return sample.GetSize()-crypto_header_size;
        }

        // members
        private CtrStreamCipher _cipher;
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfCbcSampleDecrypter
    +---------------------------------------------------------------------*/
    public class OmaDcfCbcSampleDecrypter : OmaDcfSampleDecrypter
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCbcSampleDecrypter::AP4_OmaDcfCbcSampleDecrypter
        +---------------------------------------------------------------------*/
        public OmaDcfCbcSampleDecrypter(
            BlockCipher  block_cipher,
            bool         selective_encryption) :
            base(Ap4.CIPHER_BLOCK_SIZE, selective_encryption)
        {
            _cipher = new CbcStreamCipher(block_cipher);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_OmaDbcCbcSampleDecrypter::DecryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result DecryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    byte[]     iv = null)
        {
            bool   is_encrypted = true;
            byte[] @in = data_in.GetData();
            int    in_pos = 0; // PORT NOTE: index into `in` (upstream advances the pointer)
            uint   in_size = data_in.GetDataSize();
            uint   out_size;

            // default to 0 output
            { Ap4Result _result = data_out.SetDataSize(0); if (_result.Failed()) return _result; }

            // check the selective encryption flag
            if (_selectiveEncryption) {
                if (in_size < 1) return Ap4Result.ERROR_INVALID_FORMAT;
                is_encrypted = ((@in[0]&0x80)!=0);
                in_pos++;
            }

            // check the size
            uint header_size = (_selectiveEncryption?1u:0u)+(is_encrypted?_ivLength:0u);
            if (header_size > in_size) return Ap4Result.ERROR_INVALID_FORMAT;

            // process the sample data
            uint payload_size = in_size-header_size;
            data_out.Reserve(payload_size);
            byte[] @out = data_out.UseData();
            if (is_encrypted) {
                // get the IV
                int iv_pos = in_pos; // PORT NOTE: `iv` points into `in` upstream
                in_pos += (int)Ap4.CIPHER_BLOCK_SIZE;

                _cipher.SetIV(@in, iv_pos);
                out_size = payload_size;
                { Ap4Result _result = _cipher.ProcessBuffer(@in, in_pos, payload_size, @out, 0, ref out_size, true); if (_result.Failed()) return _result; }
            } else {
                Array.Copy(@in, in_pos, @out, 0, (int)payload_size);
                out_size = payload_size;
            }

            { Ap4Result _result = data_out.SetDataSize(out_size); if (_result.Failed()) return _result; }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCbcSampleDecrypter::GetDecryptedSampleSize
        +---------------------------------------------------------------------*/
        public override uint GetDecryptedSampleSize(Sample sample)
        {
            if (_cipher == null) return 0;

            // decide if this sample is encrypted or not
            bool is_encrypted;
            if (_selectiveEncryption) {
                // read the first byte to see if the sample is encrypted or not
                byte[] h = new byte[1];
                DataBuffer peek_buffer = new DataBuffer();
                peek_buffer.SetBuffer(h, 1);
                sample.ReadData(peek_buffer, 1);
                is_encrypted = ((peek_buffer.GetData()[0]&0x80)!=0);
            } else {
                is_encrypted = true;
            }

            if (is_encrypted) {
                // with CBC, we need to decrypt the last block to know what the padding was
                uint crypto_header_size = (_selectiveEncryption?1u:0u)+_ivLength;
                uint encrypted_size = sample.GetSize()-crypto_header_size;
                DataBuffer encrypted = new DataBuffer();
                DataBuffer decrypted = new DataBuffer();
                uint       decrypted_size = Ap4.CIPHER_BLOCK_SIZE;
                if (sample.GetSize() < crypto_header_size+Ap4.CIPHER_BLOCK_SIZE) {
                    return 0;
                }
                uint offset = sample.GetSize()-2*Ap4.CIPHER_BLOCK_SIZE;
                if (sample.ReadData(encrypted, 2*Ap4.CIPHER_BLOCK_SIZE, offset).Failed()) {
                    return 0;
                }
                decrypted.Reserve(decrypted_size);
                _cipher.SetIV(encrypted.GetData());
                if (_cipher.ProcessBuffer(encrypted.GetData(), (int)Ap4.CIPHER_BLOCK_SIZE,
                                          Ap4.CIPHER_BLOCK_SIZE,
                                          decrypted.UseData(), 0,
                                          ref decrypted_size,
                                          true).Failed()) {
                    return 0;
                }
                uint padding_size = Ap4.CIPHER_BLOCK_SIZE-decrypted_size;
                return encrypted_size-padding_size;
            } else {
                return sample.GetSize()-(_selectiveEncryption?1u:0u);
            }
        }

        // members
        private CbcStreamCipher _cipher;
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfTrackDecrypter
    +---------------------------------------------------------------------*/
    public class OmaDcfTrackDecrypter : Processor.TrackHandler
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfTrackDecrypter::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(
            byte[]                     key,
            uint                       key_size,
            ProtectedSampleDescription sample_description,
            SampleEntry                sample_entry,
            BlockCipherFactory         block_cipher_factory,
            out OmaDcfTrackDecrypter   decrypter)
        {
            // PORT NOTE: assigned before the parameter check (C# requires the out
            // parameter to be set on every path; upstream leaves it untouched there)
            decrypter = null;

            // check and set defaults
            if (key == null) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }
            if (block_cipher_factory == null) {
                block_cipher_factory = DefaultBlockCipherFactory.Instance;
            }

            // create the cipher
            OmaDcfSampleDecrypter cipher = null;
            Ap4Result result = OmaDcfSampleDecrypter.Create(sample_description,
                                                            key,
                                                            key_size,
                                                            block_cipher_factory,
                                                            out cipher);
            if (result.Failed()) return result;

            // instantiate the object
            decrypter = new OmaDcfTrackDecrypter(cipher,
                                                 sample_entry,
                                                 sample_description.GetOriginalFormat());
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfTrackDecrypter::AP4_OmaDcfTrackDecrypter
        +---------------------------------------------------------------------*/
        private OmaDcfTrackDecrypter(OmaDcfSampleDecrypter  cipher,
                                     SampleEntry            sample_entry,
                                     uint                   original_format)
        {
            _cipher         = cipher;
            _sampleEntry    = sample_entry;
            _originalFormat = original_format;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfTrackDecrypter::GetProcessedSampleSize
        +---------------------------------------------------------------------*/
        public override uint GetProcessedSampleSize(Sample sample)
        {
            if (_cipher == null) return 0;
            return _cipher.GetDecryptedSampleSize(sample);
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfTrackDecrypter::ProcessTrack
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessTrack()
        {
            _sampleEntry.SetType(_originalFormat);
            _sampleEntry.DeleteChild(AtomType.SINF);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfDecrypter::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessSample(DataBuffer data_in,
                                                DataBuffer data_out)
        {
            return _cipher.DecryptSampleData(data_in, data_out);
        }

        // members
        private OmaDcfSampleDecrypter  _cipher;
        private SampleEntry            _sampleEntry;
        private uint                   _originalFormat;
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfSampleEncrypter
    +---------------------------------------------------------------------*/
    public abstract class OmaDcfSampleEncrypter
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfSampleEncrypter::AP4_OmaDcfSampleEncrypter
        +---------------------------------------------------------------------*/
        public OmaDcfSampleEncrypter(byte[] salt) // salt is only 8 bytes
        {
            // left-align the salt
            uint i=0;
            if (salt != null) {
                for (; i<8; i++) {
                    _salt[i] = salt[i];
                }
            }
            for (; i<_salt.Length; i++) {
                _salt[i] = 0;
            }
        }

        // methods
        public abstract Ap4Result EncryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    ulong      bso,
                                                    bool       skip_encryption);
        public abstract uint      GetEncryptedSampleSize(Sample sample);

        // members
        protected byte[] _salt = new byte[16];
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfCtrSampleEncrypter
    +---------------------------------------------------------------------*/
    public class OmaDcfCtrSampleEncrypter : OmaDcfSampleEncrypter
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCtrSampleEncrypter::AP4_OmaDcfCtrSampleEncrypter
        +---------------------------------------------------------------------*/
        public OmaDcfCtrSampleEncrypter(BlockCipher  block_cipher,
                                        byte[]       salt) : // salt is only 8 bytes
            base(salt)
        {
            _cipher = new CtrStreamCipher(block_cipher, 16);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCtrSampleEncrypter::EncryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result EncryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    ulong      counter,
                                                    bool       skip_encryption)
        {
            // setup the buffers
            byte[] @in = data_in.GetData();
            { Ap4Result _result = data_out.SetDataSize(data_in.GetDataSize()+Ap4.CIPHER_BLOCK_SIZE+1); if (_result.Failed()) return _result; }
            byte[] @out = data_out.UseData();
            int    out_pos = 0; // PORT NOTE: index into `out` (upstream advances the pointer)

            // selective encryption flag
            @out[out_pos++] = 0x80;

            // IV on 16 bytes: [SSSSSSSSXXXXXXXX]
            // where SSSSSSSS is the 64-bit salt and
            // XXXXXXXX is the 64-bit base counter
            Array.Copy(_salt, 0, @out, out_pos, 8);
            Ap4Utils.BytesFromUInt64BE(@out, out_pos+8, counter);

            // encrypt the payload
            uint data_size = data_in.GetDataSize();
            _cipher.SetIV(@out, out_pos);
            _cipher.ProcessBuffer(@in, 0, data_size, @out, out_pos+(int)Ap4.CIPHER_BLOCK_SIZE);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCtrSampleEncrypter::GetEncryptedSampleSize
        +---------------------------------------------------------------------*/
        public override uint GetEncryptedSampleSize(Sample sample)
        {
            return sample.GetSize()+Ap4.CIPHER_BLOCK_SIZE+1;
        }

        // members
        private CtrStreamCipher _cipher;
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfCbcSampleEncrypter
    +---------------------------------------------------------------------*/
    public class OmaDcfCbcSampleEncrypter : OmaDcfSampleEncrypter
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCbcSampleEncrypter::AP4_OmaDcfCbcSampleEncrypter
        +---------------------------------------------------------------------*/
        public OmaDcfCbcSampleEncrypter(BlockCipher  block_cipher,
                                        byte[]       salt) : // salt is only 8 bytes
            base(salt)
        {
            _cipher = new CbcStreamCipher(block_cipher);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCbcSampleEncrypter::EncryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result EncryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    ulong      counter,
                                                    bool       skip_encryption)
        {
            // make sure there is enough space in the output buffer
            data_out.Reserve(data_in.GetDataSize()+2*Ap4.CIPHER_BLOCK_SIZE+1);

            // setup the buffers
            uint out_size = data_in.GetDataSize()+Ap4.CIPHER_BLOCK_SIZE;
            byte[] @out = data_out.UseData();
            int    out_pos = 0; // PORT NOTE: index into `out` (upstream advances the pointer)

            // selective encryption flag
            @out[out_pos++] = 0x80;

            // IV on 16 bytes: [SSSSSSSSXXXXXXXX]
            // where SSSSSSSS is the 64-bit salt and
            // XXXXXXXX is the 64-bit base counter
            Array.Copy(_salt, 0, @out, out_pos, 8);
            Ap4Utils.BytesFromUInt64BE(@out, out_pos+8, counter);

            // encrypt the payload
            _cipher.SetIV(@out, out_pos);
            _cipher.ProcessBuffer(data_in.GetData(), 0,
                                  data_in.GetDataSize(),
                                  @out, out_pos+(int)Ap4.CIPHER_BLOCK_SIZE,
                                  ref out_size,
                                  true);
            { Ap4Result _result = data_out.SetDataSize(out_size+Ap4.CIPHER_BLOCK_SIZE+1); if (_result.Failed()) return _result; }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfCbcSampleEncrypter::GetEncryptedSampleSize
        +---------------------------------------------------------------------*/
        public override uint GetEncryptedSampleSize(Sample sample)
        {
            uint sample_size = sample.GetSize();
            uint padding_size = Ap4.CIPHER_BLOCK_SIZE-(sample_size%Ap4.CIPHER_BLOCK_SIZE);
            return sample_size+padding_size+Ap4.CIPHER_BLOCK_SIZE+1;
        }

        // members
        private CbcStreamCipher _cipher;
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfTrackEncrypter
    +---------------------------------------------------------------------*/
    // PORT NOTE: declared in Ap4OmaDcf.cpp only (not in the header), hence internal
    internal class OmaDcfTrackEncrypter : Processor.TrackHandler
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfTrackEncrypter::AP4_OmaDcfTrackEncrypter
        +---------------------------------------------------------------------*/
        public OmaDcfTrackEncrypter(
            OmaDcfCipherMode cipher_mode,
            BlockCipher      block_cipher,
            byte[]           salt,
            SampleEntry      sample_entry,
            uint             format,
            string           content_id,
            string           rights_issuer_url,
            byte[]           textual_headers,
            uint             textual_headers_size)
        {
            _sampleEntry     = sample_entry;
            _format          = format;
            _contentId       = content_id ?? "";
            _rightsIssuerUrl = rights_issuer_url ?? "";
            _textualHeaders  = new DataBuffer(textual_headers, textual_headers_size);
            _counter         = 0;

            // instantiate the cipher (fixed params for now)
            if (cipher_mode == OmaDcfCipherMode.OMA_DCF_CIPHER_MODE_CBC) {
                _cipher        = new OmaDcfCbcSampleEncrypter(block_cipher, salt);
                _cipherMode    = Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CBC;
                _cipherPadding = Ap4.OMA_DCF_PADDING_SCHEME_RFC_2630;
            } else {
                _cipher        = new OmaDcfCtrSampleEncrypter(block_cipher, salt);
                _cipherMode    = Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CTR;
                _cipherPadding = Ap4.OMA_DCF_PADDING_SCHEME_NONE;
            }
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfTrackEncrypter::GetProcessedSampleSize
        +---------------------------------------------------------------------*/
        public override uint GetProcessedSampleSize(Sample sample)
        {
            return _cipher.GetEncryptedSampleSize(sample);
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfTrackEncrypter::ProcessTrack
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessTrack()
        {
            // original format
            FrmaAtom frma = new FrmaAtom(_sampleEntry.GetType());

            // scheme info
            OdafAtom odaf = new OdafAtom(true, 0, (byte)Ap4.CIPHER_BLOCK_SIZE);
            OhdrAtom ohdr = new OhdrAtom(_cipherMode,
                                         _cipherPadding,
                                         0,
                                         _contentId,
                                         _rightsIssuerUrl,
                                         _textualHeaders.GetData(),
                                         _textualHeaders.GetDataSize());
            SchmAtom schm = new SchmAtom(Ap4.PROTECTION_SCHEME_TYPE_OMA,
                                         Ap4.PROTECTION_SCHEME_VERSION_OMA_20);

            // populate the odkm container
            ContainerAtom odkm = new ContainerAtom(AtomType.ODKM, (byte)0, (uint)0);
            odkm.AddChild(odaf);
            odkm.AddChild(ohdr);

            // populate the schi container
            ContainerAtom schi = new ContainerAtom(AtomType.SCHI);
            schi.AddChild(odkm);

            // populate the sinf container
            ContainerAtom sinf = new ContainerAtom(AtomType.SINF);
            sinf.AddChild(frma);
            sinf.AddChild(schm);
            sinf.AddChild(schi);

            // add the sinf atom to the sample description
            _sampleEntry.AddChild(sinf);

            // change the atom type of the sample description
            _sampleEntry.SetType(_format);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfTrackEncrypter::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessSample(DataBuffer data_in,
                                                DataBuffer data_out)
        {
            Ap4Result result = _cipher.EncryptSampleData(data_in,
                                                         data_out,
                                                         _counter,
                                                         false);
            if (result.Failed()) return result;

            _counter += (data_in.GetDataSize()+Ap4.CIPHER_BLOCK_SIZE-1)/Ap4.CIPHER_BLOCK_SIZE;
            return Ap4Result.SUCCESS;
        }

        // members
        private OmaDcfSampleEncrypter _cipher;
        private byte                  _cipherMode;
        private byte                  _cipherPadding;
        private SampleEntry           _sampleEntry;
        private uint                  _format;
        private string                _contentId;
        private string                _rightsIssuerUrl;
        private DataBuffer            _textualHeaders;
        private ulong                 _counter;
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfDecryptingProcessor
    +---------------------------------------------------------------------*/
    /// <summary>
    /// Use for DCF only, not PDCF. For PDCF, use the
    /// <see cref="StandardDecryptingProcessor"/> class
    /// </summary>
    public class OmaDcfDecryptingProcessor : Processor
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfDecryptingProcessor:AP4_OmaDcfDecryptingProcessor
        +---------------------------------------------------------------------*/
        public OmaDcfDecryptingProcessor(
            ProtectionKeyMap    key_map              = null,
            BlockCipherFactory  block_cipher_factory = null)
        {
            if (key_map != null) {
                // copy the keys
                _keyMap.SetKeys(key_map);
            }

            if (block_cipher_factory == null) {
                _blockCipherFactory = DefaultBlockCipherFactory.Instance;
            } else {
                _blockCipherFactory = block_cipher_factory;
            }
        }

        // accessors
        public ProtectionKeyMap GetKeyMap() { return _keyMap; }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfDecryptingProcessor:Initialize
        +---------------------------------------------------------------------*/
        public override Ap4Result Initialize(AtomParent                  top_level,
                                             ByteStream                  stream,
                                             Processor.ProgressListener  listener = null)
        {
            // decide which processor to instantiate based on the file type
            FtypAtom  ftyp = top_level.GetChild(AtomType.FTYP) as FtypAtom;
            if (ftyp != null) {
                if (ftyp.GetMajorBrand() == Ap4.OMA_DCF_BRAND_ODCF || ftyp.HasCompatibleBrand(Ap4.OMA_DCF_BRAND_ODCF)) {
                    return OmaDcfAtomDecrypter.DecryptAtoms(top_level, listener, _blockCipherFactory, _keyMap);
                } else {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
            } else {
                return Ap4Result.SUCCESS;
            }
        }

        // members
        private BlockCipherFactory _blockCipherFactory;
        private ProtectionKeyMap   _keyMap = new ProtectionKeyMap();
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDcfEncryptingProcessor
    +---------------------------------------------------------------------*/
    public class OmaDcfEncryptingProcessor : Processor
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfEncryptingProcessor:AP4_OmaDcfEncryptingProcessor
        +---------------------------------------------------------------------*/
        public OmaDcfEncryptingProcessor(OmaDcfCipherMode    cipher_mode,
                                         BlockCipherFactory  block_cipher_factory = null)
        {
            _cipherMode = cipher_mode;

            if (block_cipher_factory == null) {
                _blockCipherFactory = DefaultBlockCipherFactory.Instance;
            } else {
                _blockCipherFactory = block_cipher_factory;
            }
        }

        // accessors
        public ProtectionKeyMap GetKeyMap()      { return _keyMap;      }
        public TrackPropertyMap GetPropertyMap() { return _propertyMap; }

        // AP4_Processor methods
        /*----------------------------------------------------------------------
        |   AP4_OmaDcfEncryptingProcessor::Initialize
        +---------------------------------------------------------------------*/
        public override Ap4Result Initialize(AtomParent                  top_level,
                                             ByteStream                  stream,
                                             Processor.ProgressListener  listener = null)
        {
            FtypAtom ftyp = top_level.GetChild(AtomType.FTYP) as FtypAtom;
            if (ftyp != null) {
                // remove the atom, it will be replaced with a new one
                top_level.RemoveChild(ftyp);

                // keep the existing brand and compatible brands
                List<uint> compatible_brands = new List<uint>();
                compatible_brands.EnsureCapacity(ftyp.GetCompatibleBrands().Count+1);
                for (int i=0; i<ftyp.GetCompatibleBrands().Count; i++) {
                    compatible_brands.Add(ftyp.GetCompatibleBrands()[i]);
                }

                // add the OMA compatible brand if it is not already there
                if (!ftyp.HasCompatibleBrand(Ap4.OMA_DCF_BRAND_OPF2)) {
                    compatible_brands.Add(Ap4.OMA_DCF_BRAND_OPF2);
                }

                // create a replacement
                FtypAtom new_ftyp = new FtypAtom(ftyp.GetMajorBrand(),
                                                 ftyp.GetMinorVersion(),
                                                 compatible_brands.ToArray(),
                                                 (uint)compatible_brands.Count);
                ftyp = new_ftyp;
            } else {
                uint opf2 = Ap4.OMA_DCF_BRAND_OPF2;
                ftyp = new FtypAtom(Ap4.FTYP_BRAND_ISOM, 0, new uint[] { opf2 }, 1);
            }

            // insert the ftyp atom as the first child
            return top_level.AddChild(ftyp, 0);
        }

        /*----------------------------------------------------------------------
        |   AP4_OmaDcfEncryptingProcessor:CreateTrackHandler
        +---------------------------------------------------------------------*/
        public override Processor.TrackHandler CreateTrackHandler(TrakAtom trak_)
        {
            TrakAtom trak = trak_; // PORT NOTE: upstream dereferences trak unconditionally

            // find the stsd atom
            StsdAtom stsd = trak.FindChild("mdia/minf/stbl/stsd") as StsdAtom;

            // avoid tracks with no stsd atom (should not happen)
            if (stsd == null) return null;

            // only look at the first sample description
            SampleEntry  entry = stsd.GetSampleEntry(0);
            if (entry == null) return null;

            // create a handler for this track if we have a key for it and we know
            // how to map the type
            DataBuffer key;
            DataBuffer  iv;
            uint        format = 0;
            if (_keyMap.GetKeyAndIv(trak.GetId(), out key, out iv).Succeeded()) {
                switch (entry.GetType()) {
                    case AtomType.MP4A:
                        format = AtomType.ENCA;
                        break;

                    case AtomType.MP4V:
                    case AtomType.AVC1:
                    case AtomType.AVC2:
                    case AtomType.AVC3:
                    case AtomType.AVC4:
                    case AtomType.HEV1:
                    case AtomType.HVC1:
                        format = AtomType.ENCV;
                        break;

                    default: {
                        // try to find if this is audio or video
                        HdlrAtom  hdlr = trak.FindChild("mdia/hdlr") as HdlrAtom;
                        if (hdlr != null) {
                            switch (hdlr.GetHandlerType()) {
                                case Ap4.HANDLER_TYPE_SOUN:
                                    format = AtomType.ENCA;
                                    break;

                                case Ap4.HANDLER_TYPE_VIDE:
                                    format = AtomType.ENCV;
                                    break;
                            }
                        }
                        break;
                    }
                }
                if (format != 0) {
                    string     content_id = _propertyMap.GetProperty(trak.GetId(), "ContentId");
                    string     rights_issuer_url = _propertyMap.GetProperty(trak.GetId(), "RightsIssuerUrl");
                    DataBuffer textual_headers = new DataBuffer();
                    Ap4Result  result = _propertyMap.GetTextualHeaders(trak.GetId(), textual_headers);
                    if (result.Failed()) textual_headers.SetDataSize(0);

                    // create the block cipher
                    BlockCipher            block_cipher = null;
                    BlockCipher.CtrParams  ctr_params = new BlockCipher.CtrParams();
                    BlockCipher.CipherMode cipher_mode;
                    object                 cipher_params = null;
                    if (_cipherMode == OmaDcfCipherMode.OMA_DCF_CIPHER_MODE_CBC) {
                        cipher_mode = BlockCipher.CipherMode.CBC;
                    } else if (_cipherMode == OmaDcfCipherMode.OMA_DCF_CIPHER_MODE_CTR) {
                        cipher_mode = BlockCipher.CipherMode.CTR;
                        ctr_params.counter_size = 16;
                        cipher_params = ctr_params;
                    } else {
                        return null;
                    }
                    result = _blockCipherFactory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                              BlockCipher.CipherDirection.ENCRYPT,
                                                              cipher_mode,
                                                              cipher_params,
                                                              key.GetData(),
                                                              key.GetDataSize(),
                                                              out block_cipher);
                    if (result.Failed()) return null;
                    return new OmaDcfTrackEncrypter(_cipherMode,
                                                    block_cipher,
                                                    iv.GetData(),
                                                    entry,
                                                    format,
                                                    content_id,
                                                    rights_issuer_url,
                                                    textual_headers.GetData(),
                                                    textual_headers.GetDataSize());
                }
            }

            return null;
        }

        // members
        private OmaDcfCipherMode   _cipherMode;
        private BlockCipherFactory _blockCipherFactory;
        private ProtectionKeyMap   _keyMap = new ProtectionKeyMap();
        private TrackPropertyMap   _propertyMap = new TrackPropertyMap();
    }

    /*----------------------------------------------------------------------
    |   AP4_OmaDrmInfo
    +---------------------------------------------------------------------*/
    // PORT NOTE: an abstract class used as a second base class upstream
    // (AP4_OhdrAtom : AP4_ContainerAtom, AP4_OmaDrmInfo); an interface here, which
    // keeps the upstream name so that AP4_DYNAMIC_CAST(AP4_OmaDrmInfo, x) is
    // `x as OmaDrmInfo`.
    public interface OmaDrmInfo
    {
        string     GetContentId();
        string     GetRightsIssuerUrl();
        DataBuffer GetTextualHeaders();
    }
}
