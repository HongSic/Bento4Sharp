// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4GrpiAtom.h, Ap4GrpiAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_GrpiAtom
    +---------------------------------------------------------------------*/
    public class GrpiAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_GrpiAtom::Create
        +---------------------------------------------------------------------*/
        public static GrpiAtom Create(uint       size,
                                       ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new GrpiAtom(size, version, flags, stream);
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_GrpiAtom::AP4_GrpiAtom
        +---------------------------------------------------------------------*/
        public GrpiAtom(byte    key_encryption_method,
                        string  group_id,
                        byte[]  group_key,
                        uint    group_key_length) :
            base(AtomType.GRPI, Ap4.FULL_ATOM_HEADER_SIZE, 0, 0)
        {
            _keyEncryptionMethod = key_encryption_method;
            // m_GroupId(group_id): a C string, stops at the first 0
            _groupId             = Ap4Utils.StringFromBytes(Ap4Utils.StringToCStringBytes(group_id));
            _groupKey            = new DataBuffer(group_key, group_key_length);

            _size32 += 2+1+2+(uint)Ap4Utils.StringToBytes(_groupId).Length+group_key_length;
        }

        /*----------------------------------------------------------------------
        |   AP4_GrpiAtom::AP4_GrpiAtom
        +---------------------------------------------------------------------*/
        private GrpiAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.GRPI, size, false, version, flags)
        {
            _keyEncryptionMethod = 0;

            // group id length
            ushort group_id_length = 0;
            stream.ReadUI16(out group_id_length);

            // encryption method
            stream.ReadUI08(out _keyEncryptionMethod);

            // group key length
            ushort group_key_length = 0;
            stream.ReadUI16(out group_key_length);

            // group id
            byte[] group_id = new byte[group_id_length];
            stream.Read(group_id, group_id_length);
            _groupId = Ap4Utils.StringFromBytes(group_id, 0, group_id_length);

            // group key
            _groupKey.SetDataSize(group_key_length);
            stream.Read(_groupKey.UseData(), group_key_length);
        }

        /*----------------------------------------------------------------------
        |   AP4_GrpiAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // PORT NOTE: AP4_String::GetLength() counts embedded 0 bytes (the string
            // is read with Assign(chars, size)), so all the bytes are used here and
            // not the C string length.
            byte[] group_id = Ap4Utils.StringToBytes(_groupId);
            { Ap4Result _result = stream.WriteUI16((ushort)group_id.Length); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.WriteUI08(_keyEncryptionMethod); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.WriteUI16((ushort)_groupKey.GetDataSize()); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.Write(group_id, (uint)group_id.Length); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.Write(_groupKey.GetData(), _groupKey.GetDataSize()); if (_result.Failed()) return _result; }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_GrpiAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("key encryption method", _keyEncryptionMethod);
            inspector.AddField("group id",              _groupId);
            inspector.AddField("group key",             _groupKey.GetData(),
                                                        _groupKey.GetDataSize());

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_GrpiAtom::Clone
        +---------------------------------------------------------------------*/
        public override Atom Clone()
        {
            return new GrpiAtom(_keyEncryptionMethod,
                                _groupId,
                                _groupKey.GetData(),
                                _groupKey.GetDataSize());
        }

        // accessors
        public byte       GetKeyEncryptionMethod() { return _keyEncryptionMethod; }
        public void       SetKeyEncryptionMethod(byte encryption_method) { _keyEncryptionMethod = encryption_method; }
        public string     GetGroupId()  { return _groupId; }
        public DataBuffer GetGroupKey() { return _groupKey;  }

        // members
        private byte       _keyEncryptionMethod;
        private string     _groupId = "";
        private DataBuffer _groupKey = new DataBuffer();
    }
}
