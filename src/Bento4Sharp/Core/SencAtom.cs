// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SencAtom.h, Ap4SencAtom.cpp
// Original work Copyright 2002-2011 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SencAtom
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream is `class AP4_SencAtom : public AP4_Atom, public AP4_CencSampleEncryption`.
    // The port holds a CencSampleEncryption (whose "outer" atom is this atom) and
    // implements ICencSampleEncryption by forwarding to it (see the note at the
    // top of CommonEncryption.cs).
    public class SencAtom : Atom, ICencSampleEncryption
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_SencAtom::Create
        +---------------------------------------------------------------------*/
        public static SencAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new SencAtom(size, version, flags, stream);
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_SencAtom::AP4_SencAtom
        +---------------------------------------------------------------------*/
        public SencAtom(byte iv_size = 16) :
            base(AtomType.SENC, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _sampleEncryption = new CencSampleEncryption(this, iv_size);
            // (upstream also does `m_Outer = *this;` here, which assigns the atom to itself)
        }

        /*----------------------------------------------------------------------
        |   AP4_SencAtom::AP4_SencAtom
        +---------------------------------------------------------------------*/
        public SencAtom(uint   algorithm_id,
                        byte   per_sample_iv_size,
                        byte[] kid) :
            base(AtomType.SENC, Ap4.FULL_ATOM_HEADER_SIZE+20+4, 0,
                 Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_OVERRIDE_TRACK_ENCRYPTION_DEFAULTS)
        {
            _sampleEncryption = new CencSampleEncryption(this, algorithm_id, per_sample_iv_size, kid);
        }

        /*----------------------------------------------------------------------
        |   AP4_SencAtom::AP4_SencAtom
        +---------------------------------------------------------------------*/
        public SencAtom(byte    per_sample_iv_size,
                        byte    constant_iv_size,
                        byte[]  constant_iv,
                        byte    crypt_byte_block,
                        byte    skip_byte_block) :
            base(AtomType.SENC, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _sampleEncryption = new CencSampleEncryption(this,
                                                         per_sample_iv_size,
                                                         constant_iv_size,
                                                         constant_iv,
                                                         crypt_byte_block,
                                                         skip_byte_block);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_SencAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            return DoInspectFields(inspector);
        }

        /*----------------------------------------------------------------------
        |   AP4_SencAtom::WriteFields
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

        /*----------------------------------------------------------------------
        |   AP4_SencAtom::AP4_SencAtom
        +---------------------------------------------------------------------*/
        private SencAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.SENC, size, version, flags)
        {
            _sampleEncryption = new CencSampleEncryption(this, size, stream);
        }

        // members
        private CencSampleEncryption _sampleEncryption; // PORT NOTE: stands for the AP4_CencSampleEncryption base class
    }
}
