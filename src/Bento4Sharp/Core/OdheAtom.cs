// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4OdheAtom.h, Ap4OdheAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_OdheAtom
    +---------------------------------------------------------------------*/
    public class OdheAtom : ContainerAtom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_OdheAtom::Create
        +---------------------------------------------------------------------*/
        public static OdheAtom Create(uint        size,
                                       ByteStream  stream,
                                       AtomFactory atom_factory)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new OdheAtom(size, version, flags, stream, atom_factory);
        }

        /*----------------------------------------------------------------------
        |   AP4_OdheAtom::AP4_OdheAtom
        +---------------------------------------------------------------------*/
        private OdheAtom(uint        size,
                         byte        version,
                         uint        flags,
                         ByteStream  stream,
                         AtomFactory atom_factory) :
            base(AtomType.ODHE, size, false, version, flags)
        {
            if (size < Ap4.FULL_ATOM_HEADER_SIZE+1) return;

            // read the content type
            byte content_type_length;
            stream.ReadUI08(out content_type_length);
            if (size < Ap4.FULL_ATOM_HEADER_SIZE+1+content_type_length) return;
            byte[] content_type = new byte[256];
            stream.Read(content_type, content_type_length);
            _contentType = Ap4Utils.StringFromBytes(content_type, 0, content_type_length);

            // read the children
            uint bytes_available = size-(Ap4.FULL_ATOM_HEADER_SIZE+1+content_type_length);
            ReadChildren(atom_factory, stream, bytes_available);
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_OdheAtom::AP4_OdheAtom
        +---------------------------------------------------------------------*/
        /// <param name="content_type">Content type.</param>
        /// <param name="ohdr">ohdr atom passed with transfer of ownership semantics</param>
        public OdheAtom(string content_type, OhdrAtom ohdr) :
            base(AtomType.ODHE, (byte)0, (uint)0)
        {
            // m_ContentType(content_type): a C string, stops at the first 0
            _contentType = Ap4Utils.StringFromBytes(Ap4Utils.StringToCStringBytes(content_type));
            _size32 += 1+ContentTypeLength();
            AddChild(ohdr);
        }

        /*----------------------------------------------------------------------
        |   AP4_OdheAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the content type
            // PORT NOTE: AP4_String::GetLength() counts embedded 0 bytes (the string
            // is read with Assign(chars, size)), so all the bytes are used here and
            // not the C string length.
            byte[] content_type = Ap4Utils.StringToBytes(_contentType);
            { Ap4Result _result = stream.WriteUI08((byte)content_type.Length); if (_result.Failed()) return _result; }
            if (content_type.Length != 0) {
                { Ap4Result _result = stream.Write(content_type, (uint)content_type.Length); if (_result.Failed()) return _result; }
            }

            // write the children
            return _children.Apply(new AtomListWriter(stream));
        }

        /*----------------------------------------------------------------------
        |   AP4_OdheAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("content_type", _contentType);
            return InspectChildren(inspector);
        }

        // AP4_AtomParent methods
        /*----------------------------------------------------------------------
        |   AP4_OdheAtom::OnChildChanged
        +---------------------------------------------------------------------*/
        public override void OnChildChanged(Atom child)
        {
            // remcompute our size
            ulong size = GetHeaderSize()+1+ContentTypeLength();
            AtomSizeAdder adder = new AtomSizeAdder(size);
            _children.Apply(adder);
            size = adder.GetSize();
            SetSize(size);

            // update our parent
            if (_parent != null) _parent.OnChildChanged(this);
        }

        // methods
        public string GetContentType() { return _contentType; }

        // PORT NOTE: m_ContentType.GetLength() (byte count, embedded 0 bytes included)
        private uint ContentTypeLength() { return (uint)Ap4Utils.StringToBytes(_contentType).Length; }

        // members
        private string _contentType = "";
    }
}
