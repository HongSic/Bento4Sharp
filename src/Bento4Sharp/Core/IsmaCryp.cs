// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4IsmaCryp.h, Ap4IsmaCryp.cpp
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
        public const uint PROTECTION_SCHEME_TYPE_IAEC = 0x69414543; // 'iAEC'
    }

    /*----------------------------------------------------------------------
    |   AP4_IsmaCipher
    +---------------------------------------------------------------------*/
    public class IsmaCipher : SampleDecrypter
    {
        /*----------------------------------------------------------------------
        |   AP4_IsmaCipher::CreateSampleDecrypter
        +---------------------------------------------------------------------*/
        // factory
        public static Ap4Result
        CreateSampleDecrypter(ProtectedSampleDescription sample_description,
                              byte[]                     key,
                              uint                       key_size,
                              BlockCipherFactory         block_cipher_factory,
                              out IsmaCipher             decrypter)
        {
            // check parameters
            decrypter = null; // PORT NOTE: `out` must be assigned before the early return
            if (key == null || block_cipher_factory == null) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }
            decrypter = null;

            // create the cipher
            BlockCipher block_cipher = null;
            BlockCipher.CtrParams ctr_params = new BlockCipher.CtrParams();
            ctr_params.counter_size = 8;
            Ap4Result result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                 BlockCipher.CipherDirection.DECRYPT,
                                                                 BlockCipher.CipherMode.CTR,
                                                                 ctr_params,
                                                                 key,
                                                                 key_size,
                                                                 out block_cipher);
            if (result.Failed()) return result;

            // get the scheme info atom
            ContainerAtom schi = sample_description.GetSchemeInfo().GetSchiAtom();
            if (schi == null) return Ap4Result.ERROR_INVALID_FORMAT;

            // get the cipher params
            IsfmAtom isfm = schi.FindChild("iSFM") as IsfmAtom;
            if (isfm == null) return Ap4Result.ERROR_INVALID_FORMAT;

            // get the salt
            IsltAtom salt = schi.FindChild("iSLT") as IsltAtom;

            // instantiate the decrypter
            decrypter = new IsmaCipher(block_cipher,
                                       salt != null ? salt.GetSalt() : null,
                                       isfm.GetIvLength(),
                                       isfm.GetKeyIndicatorLength(),
                                       isfm.GetSelectiveEncryption());
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaCrypCipher::AP4_IsmaCipher
        +---------------------------------------------------------------------*/
        // constructor and destructor
        public IsmaCipher(BlockCipher block_cipher,
                          byte[]      salt,
                          byte        iv_length,
                          byte        key_indicator_length,
                          bool        selective_encryption)
        {
            _ivLength = iv_length;
            _keyIndicatorLength = key_indicator_length;
            _selectiveEncryption = selective_encryption;

            // NOTE: we do not handle key indicators yet, so there is only one key.

            // copy the salt
            if (salt != null) {
                // PORT NOTE: upstream copies 8 bytes unconditionally; an (empty)
                // DataBuffer's GetData() is NULL upstream but an empty array here,
                // so the copy is limited to what the array holds (rest stays 0).
                Array.Copy(salt, _salt, Math.Min(8, salt.Length));
            } else {
                Array.Clear(_salt, 0, 8);
            }

            // create a cipher
            _cipher = new CtrStreamCipher(block_cipher, iv_length);
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaCipher::GetDecryptedSampleSize
        +---------------------------------------------------------------------*/
        public override uint
        GetDecryptedSampleSize(Sample sample)
        {
            uint isma_header_size = (uint)_keyIndicatorLength + (uint)_ivLength;
            if (_selectiveEncryption) {
                ++isma_header_size;
            }
            return sample.GetSize()-isma_header_size;
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaCipher::DecryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result
        DecryptSampleData(DataBuffer data_in,
                          DataBuffer data_out,
                          byte[]     iv_ = null)
        {
            bool   is_encrypted = true;
            byte[] @in = data_in.GetData();
            int    in_pos = 0;
            uint   in_size = data_in.GetDataSize();
            uint   header_size;

            // default to 0 output
            data_out.SetDataSize(0);

            // check the selective encryption flag
            if (in_size < 1) return Ap4Result.ERROR_INVALID_FORMAT;
            if (_selectiveEncryption) {
                is_encrypted = ((@in[0]&0x80)!=0);
                in_pos++;
            }

            // check the header size
            header_size = (_selectiveEncryption?1u:0u)+
                          (is_encrypted?(uint)_keyIndicatorLength+(uint)_ivLength:0u);
            if (header_size > in_size) return Ap4Result.ERROR_INVALID_FORMAT;

            // process the sample data
            uint payload_size = in_size-header_size;
            data_out.SetDataSize(payload_size);
            byte[] @out = data_out.UseData();
            int    out_pos = 0;
            if (is_encrypted) {
                // get the IV
                int iv_start = in_pos;
                in_pos += _ivLength;

                // get the key indicator (we only support up to 32 bits)
                uint to_read = _keyIndicatorLength;
                while (to_read > 4) {
                    // skip anything above 4 bytes
                    to_read--;
                    in_pos++;
                }
                uint key_indicator = 0;
                while (to_read-- != 0) {
                    key_indicator = (key_indicator<<8) | @in[in_pos++];
                    header_size++;
                }
                // we only support key indicator = 0 for now... (TODO)
                if (key_indicator != 0) {
                    return Ap4Result.ERROR_NOT_SUPPORTED;
                }

                {
                    byte[] iv = new byte[16];
                    Array.Copy(_salt, iv, 8);
                    byte[] bso_bytes = new byte[8] {0,0,0,0,0,0,0,0};
                    if (_ivLength <= 8) {
                        Array.Copy(@in, iv_start, bso_bytes, 8-_ivLength, _ivLength);
                    }
                    ulong bso = Ap4Utils.BytesToUInt64BE(bso_bytes);
                    if ((bso%16) != 0) {
                        // not block-aligned
                        Ap4Utils.BytesFromUInt64BE(iv, 8, bso/16);
                        _cipher.SetIV(iv);
                        byte[] zero = new byte[16] {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
                        // PORT NOTE: upstream declares zero_enc[16] but indexes it with
                        // offset+i, which runs past the end (stack over-read) when
                        // offset > 8; the buffer is extended with zeros here so the
                        // same indexes are valid instead of throwing.
                        byte[] zero_enc = new byte[32];
                        _cipher.ProcessBuffer(zero, 0, 16, zero_enc, 0);
                        uint offset = (uint)(bso%16);
                        uint chunk = offset;
                        if (chunk > payload_size) chunk = payload_size;
                        for (uint i=0; i<chunk; i++) {
                            @out[out_pos+i] = (byte)(zero_enc[offset+i]^@in[in_pos+i]);
                        }
                        out_pos      += (int)chunk;
                        in_pos       += (int)chunk;
                        bso          += chunk;
                        payload_size -= chunk;
                    }
                    if (payload_size != 0) {
                        Ap4Utils.BytesFromUInt64BE(iv, 8, bso/16);
                        _cipher.SetIV(iv);
                        _cipher.ProcessBuffer(@in, in_pos, payload_size, @out, out_pos);
                    }
                }
            } else {
#if !NETSTANDARD1_3
                Array.Copy(@in, in_pos, @out, out_pos, payload_size);
#else
                Compat.ArrayCopy(@in, in_pos, @out, out_pos, payload_size);
#endif
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaCipher::EncryptSampleData
        +---------------------------------------------------------------------*/
        public Ap4Result
        EncryptSampleData(DataBuffer data_in,
                          DataBuffer data_out,
                          uint       block_counter)
        {
            // setup the buffers
            byte[] @in = data_in.GetData();
            data_out.SetDataSize(data_in.GetDataSize()+8);
            byte[] @out = data_out.UseData();

            // IV on 8 bytes (the IV is a Byte Stream Offset, not the block counter)
            Ap4Utils.BytesFromUInt64BE(@out, (ulong)unchecked(block_counter*16));

            // encrypt the payload
            byte[] iv = new byte[16];
            Array.Copy(_salt, iv, 8);
            Ap4Utils.BytesFromUInt64BE(iv, 8, block_counter);
            _cipher.SetIV(iv);
            uint data_size = data_in.GetDataSize();
            _cipher.ProcessBuffer(@in, 0, data_size, @out, 8);

            return Ap4Result.SUCCESS;
        }

        public byte[]          GetSalt()     { return _salt;     }
        public CtrStreamCipher GetCipher()   { return _cipher;   }
        public byte            GetIvLength() { return _ivLength; }
        public byte            GetKeyIndicatorLength() { return _keyIndicatorLength; }
        public bool            GetSelectiveEncryption(){ return _selectiveEncryption;}

        // members
        private CtrStreamCipher _cipher;
        private byte[]          _salt = new byte[8];
        private byte            _ivLength;
        private byte            _keyIndicatorLength;
        private bool            _selectiveEncryption;
    }

    /*----------------------------------------------------------------------
    |   AP4_IsmaTrackDecrypter
    +---------------------------------------------------------------------*/
    public class IsmaTrackDecrypter : Processor.TrackHandler
    {
        /*----------------------------------------------------------------------
        |   AP4_IsmaTrackDecrypter::Create
        +---------------------------------------------------------------------*/
        // construction
        public static Ap4Result
        Create(byte[]                     key,
               uint                       key_size,
               ProtectedSampleDescription sample_description,
               SampleEntry                sample_entry,
               BlockCipherFactory         block_cipher_factory,
               out IsmaTrackDecrypter     decrypter)
        {
            // instantiate the cipher
            IsmaCipher cipher = null;
            decrypter          = null;
            Ap4Result result = IsmaCipher.CreateSampleDecrypter(sample_description,
                                                                key,
                                                                key_size,
                                                                block_cipher_factory,
                                                                out cipher);
            if (result.Failed()) return result;

            // instanciate the object
            decrypter = new IsmaTrackDecrypter(cipher,
                                               sample_entry,
                                               sample_description.GetOriginalFormat());
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaTrackDecrypter::AP4_IsmaTrackDecrypter
        +---------------------------------------------------------------------*/
        private IsmaTrackDecrypter(IsmaCipher  cipher,
                                   SampleEntry sample_entry,
                                   uint        original_format)
        {
            _cipher = cipher;
            _sampleEntry = sample_entry;
            _originalFormat = original_format;
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaTrackDecrypter::GetProcessedSampleSize
        +---------------------------------------------------------------------*/
        public override uint
        GetProcessedSampleSize(Sample sample)
        {
            return _cipher.GetDecryptedSampleSize(sample);
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaTrackDecrypter::ProcessTrack
        +---------------------------------------------------------------------*/
        public override Ap4Result
        ProcessTrack()
        {
            _sampleEntry.SetType(_originalFormat);
            _sampleEntry.DeleteChild(AtomType.SINF);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaDecrypter::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result
        ProcessSample(DataBuffer data_in,
                      DataBuffer data_out)
        {
            return _cipher.DecryptSampleData(data_in, data_out);
        }

        // members
        private IsmaCipher  _cipher;
        private SampleEntry _sampleEntry;
        private uint        _originalFormat;
    }

    /*----------------------------------------------------------------------
    |   AP4_IsmaTrackEncrypter
    +---------------------------------------------------------------------*/
    internal class IsmaTrackEncrypter : Processor.TrackHandler
    {
        /*----------------------------------------------------------------------
        |   AP4_IsmaTrackEncrypter::AP4_IsmaTrackEncrypter
        +---------------------------------------------------------------------*/
        // constructor
        public IsmaTrackEncrypter(string      kms_uri,
                                  BlockCipher block_cipher,
                                  byte[]      salt,
                                  SampleEntry sample_entry,
                                  uint        format)
        {
            _kmsUri = kms_uri ?? "";
            _sampleEntry = sample_entry;
            _format = format;
            _blockCounter = 0;

            // instantiate the cipher (fixed params for now)
            _cipher = new IsmaCipher(block_cipher, salt, 8, 0, false);
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaTrackEncrypter::GetProcessedSampleSize
        +---------------------------------------------------------------------*/
        public override uint
        GetProcessedSampleSize(Sample sample)
        {
            return sample.GetSize()+8; //fixed header size for now
        }

        /*----------------------------------------------------------------------
        |   AP4_IsmaTrackEncrypter::ProcessTrack
        +---------------------------------------------------------------------*/
        public override Ap4Result
        ProcessTrack()
        {
            // original format
            FrmaAtom frma = new FrmaAtom(_sampleEntry.GetType());

            // scheme
            SchmAtom schm = new SchmAtom(Ap4.PROTECTION_SCHEME_TYPE_IAEC, 1);

            // scheme info
            IkmsAtom      ikms = new IkmsAtom(_kmsUri);
            IsfmAtom      isfm = new IsfmAtom(_cipher.GetSelectiveEncryption(),
                                              _cipher.GetKeyIndicatorLength(),
                                              _cipher.GetIvLength());
            IsltAtom      islt = new IsltAtom(_cipher.GetSalt());

            // populate the schi container
            ContainerAtom schi = new ContainerAtom(AtomType.SCHI);
            schi.AddChild(ikms);
            schi.AddChild(isfm);
            schi.AddChild(islt);

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
        |   AP4_IsmaTrackEncrypter::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result
        ProcessSample(DataBuffer data_in,
                      DataBuffer data_out)
        {
            Ap4Result result = _cipher.EncryptSampleData(data_in, data_out, _blockCounter);
            if (result.Failed()) return result;

            _blockCounter += (data_in.GetDataSize()+Ap4.CIPHER_BLOCK_SIZE-1)/Ap4.CIPHER_BLOCK_SIZE;
            return Ap4Result.SUCCESS;
        }

        // members
        private string      _kmsUri;
        private IsmaCipher  _cipher;
        private SampleEntry _sampleEntry;
        private uint        _format;
        private uint        _blockCounter;
    }

    /*----------------------------------------------------------------------
    |   AP4_IsmaEncryptingProcessor
    +---------------------------------------------------------------------*/
    public class IsmaEncryptingProcessor : Processor
    {
        /*----------------------------------------------------------------------
        |   AP4_IsmaEncryptingProcessor::AP4_IsmaEncryptingProcessor
        +---------------------------------------------------------------------*/
        // constructors and destructor
        public IsmaEncryptingProcessor(string              kms_uri,
                                       BlockCipherFactory  block_cipher_factory = null)
        {
            _kmsUri = kms_uri ?? "";
            if (block_cipher_factory == null) {
                _blockCipherFactory = DefaultBlockCipherFactory.Instance;
            } else {
                _blockCipherFactory = block_cipher_factory;
            }
        }

        // accessors
        public ProtectionKeyMap GetKeyMap() { return _keyMap; }

        /*----------------------------------------------------------------------
        |   AP4_IsmaEncryptingProcessor:CreateTrackHandler
        +---------------------------------------------------------------------*/
        // methods
        public override Processor.TrackHandler
        CreateTrackHandler(TrakAtom trak)
        {
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
            DataBuffer  salt;
            uint        format = 0;
            if (_keyMap.GetKeyAndIv(trak.GetId(), out key, out salt).Succeeded()) {
                switch (entry.GetType()) {
                    case AtomType.MP4A:
                        format = AtomType.ENCA;
                        break;

                    case AtomType.MP4V:
                    case AtomType.AVC1:
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
                    // create the block cipher
                    BlockCipher block_cipher = null;
                    BlockCipher.CtrParams ctr_params = new BlockCipher.CtrParams();
                    ctr_params.counter_size = 8;
                    Ap4Result result = _blockCipherFactory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                        BlockCipher.CipherDirection.ENCRYPT,
                                                                        BlockCipher.CipherMode.CTR,
                                                                        ctr_params,
                                                                        key.GetData(),
                                                                        key.GetDataSize(),
                                                                        out block_cipher);
                    if (result.Failed()) return null;

                    // create the encrypter
                    return new IsmaTrackEncrypter(_kmsUri,
                                                  block_cipher,
                                                  salt.GetData(),
                                                  entry,
                                                  format);
                }
            }

            return null;
        }

        // members
        private ProtectionKeyMap   _keyMap = new ProtectionKeyMap();
        private string             _kmsUri;
        private BlockCipherFactory _blockCipherFactory;
    }
}
