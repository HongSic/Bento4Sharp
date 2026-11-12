// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TencAtom.h, Ap4TencAtom.cpp
// Original work Copyright 2002-2011 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_TencAtom
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream is `class AP4_TencAtom : public AP4_Atom, public AP4_CencTrackEncryption`.
    // The port holds a CencTrackEncryption and implements ICencTrackEncryption by
    // forwarding to it (see the note at the top of CommonEncryption.cs).
    public class TencAtom : Atom, ICencTrackEncryption
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_TencAtom::Create
        +---------------------------------------------------------------------*/
        public static TencAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            TencAtom tenc = new TencAtom(size, version, flags);
            Ap4Result result = tenc.Parse(stream);
            if (result.Failed()) {
                return null;
            }

            return tenc;
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_TencAtom::AP4_TencAtom
        +---------------------------------------------------------------------*/
        public TencAtom(uint   default_is_protected,
                        byte   default_per_sample_iv_size,
                        byte[] default_kid) :
            base(AtomType.TENC, Ap4.FULL_ATOM_HEADER_SIZE+20, 0, 0)
        {
            _trackEncryption = new CencTrackEncryption(0,
                                                       (byte)default_is_protected,
                                                       default_per_sample_iv_size,
                                                       default_kid);
        }

        /*----------------------------------------------------------------------
        |   AP4_TencAtom::AP4_TencAtom
        +---------------------------------------------------------------------*/
        public TencAtom(uint    default_is_protected,
                        byte    default_per_sample_iv_size,
                        byte[]  default_kid,
                        byte    default_constant_iv_size,
                        byte[]  default_constant_iv,
                        byte    default_crypt_byte_block,
                        byte    default_skip_byte_block) :
            base(AtomType.TENC, Ap4.FULL_ATOM_HEADER_SIZE+20+(default_per_sample_iv_size != 0 ? 0u : 1u+default_constant_iv_size), 1, 0)
        {
            _trackEncryption = new CencTrackEncryption(1,
                                                       (byte)default_is_protected,
                                                       default_per_sample_iv_size,
                                                       default_kid,
                                                       default_constant_iv_size,
                                                       default_constant_iv,
                                                       default_crypt_byte_block,
                                                       default_skip_byte_block);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_TencAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            return DoInspectFields(inspector);
        }

        /*----------------------------------------------------------------------
        |   AP4_TencAtom::WriteFields
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
        |   AP4_TencAtom::Create
        +---------------------------------------------------------------------*/
        private TencAtom(uint size,
                         byte version,
                         uint flags) :
            base(AtomType.TENC, size, version, flags)
        {
            _trackEncryption = new CencTrackEncryption(version);
        }

        // members
        private CencTrackEncryption _trackEncryption; // PORT NOTE: stands for the AP4_CencTrackEncryption base class
    }
}
