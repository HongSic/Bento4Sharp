// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Piff.h, Ap4Piff.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint PROTECTION_SCHEME_TYPE_PIFF = 0x70696666; // 'piff'
        public const uint PROTECTION_SCHEME_VERSION_PIFF_10 = 0x00010000;
        public const uint PROTECTION_SCHEME_VERSION_PIFF_11 = 0x00010001;
        public const uint PIFF_BRAND = 0x70696666; // 'piff'

        public static readonly byte[] UUID_PIFF_TRACK_ENCRYPTION_ATOM = new byte[16] {
            0x89, 0x74, 0xdb, 0xce, 0x7b, 0xe7, 0x4c, 0x51, 0x84, 0xf9, 0x71, 0x48, 0xf9, 0x88, 0x25, 0x54
        };
        public static readonly byte[] UUID_PIFF_SAMPLE_ENCRYPTION_ATOM = new byte[16] {
            0xA2, 0x39, 0x4F, 0x52, 0x5A, 0x9B, 0x4f, 0x14, 0xA2, 0x44, 0x6C, 0x42, 0x7C, 0x64, 0x8D, 0xF4
        };
    }

    public enum PiffCipherMode {
        PIFF_CIPHER_MODE_CTR,
        PIFF_CIPHER_MODE_CBC
    }

    /*----------------------------------------------------------------------
    |   AP4_PiffTrackEncryptionAtom
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream is `class AP4_PiffTrackEncryptionAtom : public AP4_UuidAtom, public AP4_CencTrackEncryption`.
    // The port holds a CencTrackEncryption and implements ICencTrackEncryption by
    // forwarding to it (see the note at the top of CommonEncryption.cs).
    public class PiffTrackEncryptionAtom : UuidAtom, ICencTrackEncryption
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_PiffTrackEncryptionAtom::Create
        +---------------------------------------------------------------------*/
        public static PiffTrackEncryptionAtom Create(uint size, ByteStream stream)
        {
            byte version = 0;
            uint flags   = 0;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            Ap4Result result = Atom.ReadFullHeader(stream, out version, out flags);
            if (result.Failed()) return null;
            if (version != 0) return null;
            PiffTrackEncryptionAtom piff = new PiffTrackEncryptionAtom(size, version, flags);
            result = piff.Parse(stream);
            if (result.Failed()) {
                return null;
            }
            return piff;
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_PiffTrackEncryptionAtom::AP4_PiffTrackEncryptionAtom
        +---------------------------------------------------------------------*/
        public PiffTrackEncryptionAtom(uint   default_algorithm_id,
                                       byte   default_iv_size,
                                       byte[] default_kid) :
            base(Ap4.FULL_UUID_ATOM_HEADER_SIZE+20, Ap4.UUID_PIFF_TRACK_ENCRYPTION_ATOM, 0, 0)
        {
            _trackEncryption = new CencTrackEncryption(0,
                                                       (byte)default_algorithm_id,
                                                       default_iv_size,
                                                       default_kid);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_PiffTrackEncryptionAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            return DoInspectFields(inspector);
        }

        /*----------------------------------------------------------------------
        |   AP4_PiffTrackEncryptionAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return DoWriteFields(stream);
        }

        // AP4_CencTrackEncryption (forwarded to the held object)
        public CencTrackEncryption GetTrackEncryption()           { return _trackEncryption; } // PORT addition
        public Ap4Result Parse(ByteStream stream)                 { return _trackEncryption.Parse(stream); }
        public Ap4Result DoInspectFields(AtomInspector inspector) { return _trackEncryption.DoInspectFields(inspector); }
        public Ap4Result DoWriteFields(ByteStream stream)         { return _trackEncryption.DoWriteFields(stream); }
        public uint      GetDefaultIsProtected()                  { return _trackEncryption.GetDefaultIsProtected();     }
        public byte      GetDefaultPerSampleIvSize()              { return _trackEncryption.GetDefaultPerSampleIvSize(); }
        public byte      GetDefaultConstantIvSize()               { return _trackEncryption.GetDefaultConstantIvSize();  }
        public byte[]    GetDefaultConstantIv()                   { return _trackEncryption.GetDefaultConstantIv();      }
        public byte[]    GetDefaultKid()                          { return _trackEncryption.GetDefaultKid();             }
        public byte      GetDefaultCryptByteBlock()               { return _trackEncryption.GetDefaultCryptByteBlock();  }
        public byte      GetDefaultSkipByteBlock()                { return _trackEncryption.GetDefaultSkipByteBlock();   }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_PiffTrackEncryptionAtom::AP4_PiffTrackEncryptionAtom
        +---------------------------------------------------------------------*/
        private PiffTrackEncryptionAtom(uint size,
                                        byte version,
                                        uint flags) :
            base(size, Ap4.UUID_PIFF_TRACK_ENCRYPTION_ATOM, version, flags)
        {
            _trackEncryption = new CencTrackEncryption(version);
        }

        // members
        private CencTrackEncryption _trackEncryption; // PORT NOTE: stands for the AP4_CencTrackEncryption base class
    }

    /*----------------------------------------------------------------------
    |   AP4_PiffSampleEncryptionAtom
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream is `class AP4_PiffSampleEncryptionAtom : public AP4_UuidAtom, public AP4_CencSampleEncryption`.
    // The port holds a CencSampleEncryption (whose "outer" atom is this atom) and
    // implements ICencSampleEncryption by forwarding to it (see the note at the
    // top of CommonEncryption.cs).
    public class PiffSampleEncryptionAtom : UuidAtom, ICencSampleEncryption
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_PiffSampleEncryptionAtom::Create
        +---------------------------------------------------------------------*/
        public static PiffSampleEncryptionAtom Create(uint size, ByteStream stream)
        {
            byte version = 0;
            uint flags   = 0;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            Ap4Result result = Atom.ReadFullHeader(stream, out version, out flags);
            if (result.Failed()) return null;
            if (version != 0) return null;
            return new PiffSampleEncryptionAtom(size, version, flags, stream);
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_PiffSampleEncryptionAtom::AP4_PiffSampleEncryptionAtom
        +---------------------------------------------------------------------*/
        public PiffSampleEncryptionAtom(byte iv_size) :
            base(Ap4.FULL_UUID_ATOM_HEADER_SIZE+4, Ap4.UUID_PIFF_SAMPLE_ENCRYPTION_ATOM, 0, 0)
        {
            _sampleEncryption = new CencSampleEncryption(this, iv_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_PiffSampleEncryptionAtom::AP4_PiffSampleEncryptionAtom
        +---------------------------------------------------------------------*/
        public PiffSampleEncryptionAtom(uint   algorithm_id,
                                        byte   iv_size,
                                        byte[] kid) :
            base(Ap4.FULL_UUID_ATOM_HEADER_SIZE+20+4, Ap4.UUID_PIFF_SAMPLE_ENCRYPTION_ATOM, 0,
                 Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_OVERRIDE_TRACK_ENCRYPTION_DEFAULTS)
        {
            _sampleEncryption = new CencSampleEncryption(this, algorithm_id, iv_size, kid);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_PiffSampleEncryptionAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            return DoInspectFields(inspector);
        }

        /*----------------------------------------------------------------------
        |   AP4_PiffSampleEncryptionAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return DoWriteFields(stream);
        }

        // AP4_CencSampleEncryption (forwarded to the held object)
        public CencSampleEncryption GetSampleEncryption()         { return _sampleEncryption; } // PORT addition
        public Ap4Result DoInspectFields(AtomInspector inspector) { return _sampleEncryption.DoInspectFields(inspector); }
        public Ap4Result DoWriteFields(ByteStream stream)         { return _sampleEncryption.DoWriteFields(stream); }
        public Atom      GetOuter()                               { return _sampleEncryption.GetOuter(); }
        public uint      GetAlgorithmId()                         { return _sampleEncryption.GetAlgorithmId(); }
        public byte      GetPerSampleIvSize()                     { return _sampleEncryption.GetPerSampleIvSize(); }
        public Ap4Result SetPerSampleIvSize(byte iv_size)         { return _sampleEncryption.SetPerSampleIvSize(iv_size); }
        public byte      GetConstantIvSize()                      { return _sampleEncryption.GetConstantIvSize(); }
        public Ap4Result SetConstantIvSize(byte iv_size)          { return _sampleEncryption.SetConstantIvSize(iv_size); }
        public byte      GetCryptByteBlock()                      { return _sampleEncryption.GetCryptByteBlock(); }
        public byte      GetSkipByteBlock()                       { return _sampleEncryption.GetSkipByteBlock(); }
        public byte[]    GetConstantIv()                          { return _sampleEncryption.GetConstantIv(); }
        public byte[]    GetKid()                                 { return _sampleEncryption.GetKid(); }
        public uint      GetSampleInfoCount()                     { return _sampleEncryption.GetSampleInfoCount(); }
        public Ap4Result AddSampleInfo(byte[] iv, DataBuffer subsample_info) { return _sampleEncryption.AddSampleInfo(iv, subsample_info); }
        public Ap4Result SetSampleInfosSize(uint size)            { return _sampleEncryption.SetSampleInfosSize(size); }
        public Ap4Result CreateSampleInfoTable(byte                     flags,
                                               byte                     default_crypt_byte_block,
                                               byte                     default_skip_byte_block,
                                               byte                     default_per_sample_iv_size,
                                               byte                     default_constant_iv_size,
                                               byte[]                   default_constant_iv,
                                               out CencSampleInfoTable  table)
        {
            return _sampleEncryption.CreateSampleInfoTable(flags,
                                                           default_crypt_byte_block,
                                                           default_skip_byte_block,
                                                           default_per_sample_iv_size,
                                                           default_constant_iv_size,
                                                           default_constant_iv,
                                                           out table);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_PiffSampleEncryptionAtom::AP4_PiffSampleEncryptionAtom
        +---------------------------------------------------------------------*/
        private PiffSampleEncryptionAtom(uint       size,
                                         byte       version,
                                         uint       flags,
                                         ByteStream stream) :
            base(size, Ap4.UUID_PIFF_SAMPLE_ENCRYPTION_ATOM, version, flags)
        {
            _sampleEncryption = new CencSampleEncryption(this, size, stream);
        }

        // members
        private CencSampleEncryption _sampleEncryption; // PORT NOTE: stands for the AP4_CencSampleEncryption base class
    }
}
