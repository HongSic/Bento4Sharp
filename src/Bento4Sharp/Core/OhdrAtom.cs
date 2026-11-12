// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4OhdrAtom.h, Ap4OhdrAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte OMA_DCF_ENCRYPTION_METHOD_NULL    = 0;
        public const byte OMA_DCF_ENCRYPTION_METHOD_AES_CBC = 1;
        public const byte OMA_DCF_ENCRYPTION_METHOD_AES_CTR = 2;

        public const byte OMA_DCF_PADDING_SCHEME_NONE     = 0;
        public const byte OMA_DCF_PADDING_SCHEME_RFC_2630 = 1;
    }

    /*----------------------------------------------------------------------
    |   AP4_OhdrAtom
    +---------------------------------------------------------------------*/
    public class OhdrAtom : ContainerAtom, OmaDrmInfo
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_OhdrAtom::Create
        +---------------------------------------------------------------------*/
        public static OhdrAtom Create(uint        size,
                                       ByteStream  stream,
                                       AtomFactory atom_factory)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new OhdrAtom(size, version, flags, stream, atom_factory);
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_OhdrAtom::AP4_OhdrAtom
        +---------------------------------------------------------------------*/
        public OhdrAtom(byte    encryption_method,
                        byte    padding_scheme,
                        ulong   plaintext_length,
                        string  content_id,
                        string rights_issuer_url,
                        byte[]  textual_headers,
                        uint    textual_headers_size) :
            base(AtomType.OHDR, (byte)0, (uint)0)
        {
            _encryptionMethod = encryption_method;
            _paddingScheme    = padding_scheme;
            _plaintextLength  = plaintext_length;
            // m_ContentId(content_id), m_RightsIssuerUrl(rights_issuer_url): C strings, stop at the first 0
            _contentId        = Ap4Utils.StringFromBytes(Ap4Utils.StringToCStringBytes(content_id));
            _rightsIssuerUrl  = Ap4Utils.StringFromBytes(Ap4Utils.StringToCStringBytes(rights_issuer_url));
            _textualHeaders   = new DataBuffer(textual_headers, textual_headers_size);

            _size32 += 1+1+8+2+2+2+Length(_contentId)+Length(_rightsIssuerUrl)+textual_headers_size;
        }

        /*----------------------------------------------------------------------
        |   AP4_OhdrAtom::AP4_OhdrAtom
        +---------------------------------------------------------------------*/
        private OhdrAtom(uint        size,
                         byte        version,
                         uint        flags,
                         ByteStream  stream,
                         AtomFactory atom_factory) :
            base(AtomType.OHDR, size, false, version, flags)
        {
            // encryption method
            stream.ReadUI08(out _encryptionMethod);

            // padding scheme
            stream.ReadUI08(out _paddingScheme);

            // plaintext length
            stream.ReadUI64(out _plaintextLength);

            // string lengths
            ushort content_id_length;
            ushort rights_issuer_url_length;
            ushort textual_headers_length;
            stream.ReadUI16(out content_id_length);
            stream.ReadUI16(out rights_issuer_url_length);
            stream.ReadUI16(out textual_headers_length);

            // content id
            byte[] buffer = new byte[content_id_length];
            stream.Read(buffer, content_id_length);
            _contentId = Ap4Utils.StringFromBytes(buffer, 0, content_id_length);

            // rights issuer url
            buffer = new byte[rights_issuer_url_length];
            stream.Read(buffer, rights_issuer_url_length);
            _rightsIssuerUrl = Ap4Utils.StringFromBytes(buffer, 0, rights_issuer_url_length);

            // textual headers
            buffer = new byte[textual_headers_length];
            stream.Read(buffer, textual_headers_length);
            _textualHeaders.SetData(buffer, textual_headers_length);

            // read the children
            uint bytes_used = Ap4.FULL_ATOM_HEADER_SIZE+1+1+8+2+2+2+(uint)content_id_length+(uint)rights_issuer_url_length+(uint)textual_headers_length;
            if (bytes_used <= size) {
                ReadChildren(atom_factory, stream, size-bytes_used);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_OhdrAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // PORT NOTE: AP4_String::GetLength() counts embedded 0 bytes (the strings
            // are read with Assign(chars, size)), so all the bytes are used here and
            // not the C string length.
            byte[] content_id        = Ap4Utils.StringToBytes(_contentId);
            byte[] rights_issuer_url = Ap4Utils.StringToBytes(_rightsIssuerUrl);
            { Ap4Result _result = stream.WriteUI08(_encryptionMethod); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.WriteUI08(_paddingScheme); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.WriteUI64(_plaintextLength); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.WriteUI16((ushort)content_id.Length); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.WriteUI16((ushort)rights_issuer_url.Length); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.WriteUI16((ushort)_textualHeaders.GetDataSize()); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.Write(content_id, (uint)content_id.Length); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.Write(rights_issuer_url, (uint)rights_issuer_url.Length); if (_result.Failed()) return _result; }
            { Ap4Result _result = stream.Write(_textualHeaders.GetData(), _textualHeaders.GetDataSize()); if (_result.Failed()) return _result; }

            // write the children
            return _children.Apply(new AtomListWriter(stream));
        }

        /*----------------------------------------------------------------------
        |   AP4_OhdrAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("encryption_method", _encryptionMethod);
            inspector.AddField("padding_scheme",    _paddingScheme);
            inspector.AddField("plaintext_length",  (uint)_plaintextLength);
            inspector.AddField("content_id",        _contentId);
            inspector.AddField("rights_issuer_url", _rightsIssuerUrl);

            {
                DataBuffer output_buffer = new DataBuffer();
                Ap4Result  result;

                result = output_buffer.Reserve(1+_textualHeaders.GetDataSize());
                if (result.Failed()) {
                    inspector.AddField("textual_headers",
                        _textualHeaders.UseData(),
                        _textualHeaders.GetDataSize(),
                        AtomInspector.FormatHint.HINT_HEX);
                } else {
                    uint   data_len = _textualHeaders.GetDataSize();
                    byte[] textual_headers_string;
                    int    curr;

                    output_buffer.SetData(_textualHeaders.GetData(), _textualHeaders.GetDataSize());
                    textual_headers_string = output_buffer.UseData();
                    curr = 0;
                    textual_headers_string[_textualHeaders.GetDataSize()] = (byte)'\0';
                    while(curr < data_len) {
                        if ((byte)'\0' == textual_headers_string[curr]) {
                            textual_headers_string[curr] = (byte)'\n';
                        }
                        curr++;
                    }
                    inspector.AddField("textual_headers", Ap4Utils.StringFromCString(textual_headers_string, 0, data_len+1));
                }
            }

            return InspectChildren(inspector);
        }

        /*----------------------------------------------------------------------
        |   AP4_OhdrAtom::Clone
        +---------------------------------------------------------------------*/
        public override Atom Clone()
        {
            OhdrAtom clone;
            clone = new OhdrAtom(_encryptionMethod,
                                 _paddingScheme,
                                 _plaintextLength,
                                 _contentId,
                                 _rightsIssuerUrl,
                                 _textualHeaders.GetData(),
                                 _textualHeaders.GetDataSize());

            for (int i=0; i<_children.Count; i++) {
                Atom child_clone = _children[i].Clone();
                if (child_clone != null) clone.AddChild(child_clone);
            }

            return clone;
        }

        // accessors
        public byte  GetEncryptionMethod() { return _encryptionMethod; }
        public void  SetEncryptionMethod(byte encryption_method) { _encryptionMethod = encryption_method; }
        public byte  GetPaddingScheme()    { return _paddingScheme;    }
        public void  SetPaddingScheme(byte padding_scheme) { _paddingScheme = padding_scheme; }
        public ulong GetPlaintextLength()  { return _plaintextLength;  }

        // AP4_OmaDrmInfo implementation
        public string     GetContentId()       { return _contentId;       }
        public string     GetRightsIssuerUrl() { return _rightsIssuerUrl; }
        public DataBuffer GetTextualHeaders()  { return _textualHeaders;  }

        // PORT NOTE: AP4_String::GetLength() (byte count, embedded 0 bytes included)
        private static uint Length(string str) { return (uint)Ap4Utils.StringToBytes(str).Length; }

        // members
        private byte       _encryptionMethod;
        private byte       _paddingScheme;
        private ulong      _plaintextLength;
        private string     _contentId = "";
        private string     _rightsIssuerUrl = "";
        private DataBuffer _textualHeaders = new DataBuffer();
    }
}
