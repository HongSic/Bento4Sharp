// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4OdafAtom.h, Ap4OdafAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_OdafAtom
    +---------------------------------------------------------------------*/
    public class OdafAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_OdafAtom::Create
        +---------------------------------------------------------------------*/
        public static OdafAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new OdafAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_OdafAtom::AP4_OdafAtom
        +---------------------------------------------------------------------*/
        public OdafAtom(bool selective_encryption,
                        byte key_length_indicator,
                        byte iv_length) :
            base(AtomType.ODAF, Ap4.FULL_ATOM_HEADER_SIZE+3, 0, 0)
        {
            _selectiveEncryption = selective_encryption;
            _keyIndicatorLength  = key_length_indicator;
            _ivLength            = iv_length;
        }

        /*----------------------------------------------------------------------
        |   AP4_OdafAtom::AP4_OdafAtom
        +---------------------------------------------------------------------*/
        private OdafAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.ODAF, size, version, flags)
        {
            _keyIndicatorLength = 0;
            _ivLength           = 0;

            byte s;
            stream.ReadUI08(out s);
            _selectiveEncryption = ((s&0x80) != 0);
            stream.ReadUI08(out _keyIndicatorLength);
            stream.ReadUI08(out _ivLength);
        }

        /*----------------------------------------------------------------------
        |   AP4_OdafAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // selective encryption
            result = stream.WriteUI08(_selectiveEncryption ? (byte)0x80 : (byte)0);
            if (result.Failed()) return result;

            // key indicator length
            result = stream.WriteUI08(_keyIndicatorLength);
            if (result.Failed()) return result;

            // IV length
            result = stream.WriteUI08(_ivLength);
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OdafAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("selective_encryption", _selectiveEncryption ? 1 : 0);
            inspector.AddField("key_indicator_length", _keyIndicatorLength);
            inspector.AddField("IV_length", _ivLength);

            return Ap4Result.SUCCESS;
        }

        // accessors
        public bool GetSelectiveEncryption() { return _selectiveEncryption; }
        public byte GetKeyIndicatorLength()  { return _keyIndicatorLength;  }
        public byte GetIvLength()            { return _ivLength;            }

        // members
        private bool _selectiveEncryption;
        private byte _keyIndicatorLength;
        private byte _ivLength;
    }
}
