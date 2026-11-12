// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4IkmsAtom.h, Ap4IkmsAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_IkmsAtom
    +---------------------------------------------------------------------*/
    public class IkmsAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_IkmsAtom::Create
        +---------------------------------------------------------------------*/
        public static IkmsAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new IkmsAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_IkmsAtom::AP4_IkmsAtom
        +---------------------------------------------------------------------*/
        public IkmsAtom(string  kms_uri,
                        uint    kms_id = 0,
                        uint    kms_version = 0) :
            base(AtomType.IKMS, Ap4.FULL_ATOM_HEADER_SIZE, 0, 0)
        {
            // PORT NOTE: AP4_String(const char*) stops at the first 0 byte
            _kmsUri = Ap4Utils.StringFromBytes(Ap4Utils.StringToCStringBytes(kms_uri));
            _kmsId = kms_id;
            _kmsVersion = kms_version;
            _size32 += Ap4Utils.StringLength(_kmsUri)+1;
        }

        /*----------------------------------------------------------------------
        |   AP4_IkmsAtom::AP4_IkmsAtom
        +---------------------------------------------------------------------*/
        private IkmsAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.IKMS, size, version, flags)
        {
            _kmsUri = "";
            uint string_size = size-Ap4.FULL_ATOM_HEADER_SIZE;
            if (_version == 1 && string_size >= 8) {
                string_size -= 8;
                stream.ReadUI32(out _kmsId);
                stream.ReadUI32(out _kmsVersion);
            } else {
                _kmsId      = 0;
                _kmsVersion = 0;
            }
            if (string_size != 0) {
                byte[] str = new byte[string_size];
                stream.Read(str, string_size);
                str[string_size-1] = 0; // force null-termination
                _kmsUri = Ap4Utils.StringFromCString(str, 0, string_size);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_IkmsAtom::Clone
        +---------------------------------------------------------------------*/
        public override Atom Clone()
        {
            return new IkmsAtom(_kmsUri, _kmsId, _kmsVersion);
        }

        /*----------------------------------------------------------------------
        |   AP4_IkmsAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // handler version 1
            if (_version == 1) {
                stream.WriteUI32(_kmsId);
                stream.WriteUI32(_kmsVersion);
            }

            // kms uri
            // PORT NOTE: stream.Write(m_KmsUri.GetChars(), m_KmsUri.GetLength()+1): the
            // string and its terminator are written with one call, as upstream.
            byte[] uri = Ap4Utils.StringToCStringBytes(_kmsUri);
            byte[] uri_z = new byte[uri.Length+1];
            System.Array.Copy(uri, uri_z, uri.Length);
            Ap4Result result = stream.Write(uri_z, (uint)uri_z.Length);
            if (result.Failed()) return result;

            // pad with zeros if necessary
            uint padding = _size32-(Ap4.FULL_ATOM_HEADER_SIZE+Ap4Utils.StringLength(_kmsUri)+1);
            if (_version == 1) padding-=8;
            while (padding-- != 0) stream.WriteUI08(0);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_IkmsAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            if (_version == 1) {
                string id = Ap4Utils.FormatFourChars(_kmsId);
                inspector.AddField("kms_id", id);
                inspector.AddField("kms_version", _kmsVersion);
            }
            inspector.AddField("kms_uri", _kmsUri);

            return Ap4Result.SUCCESS;
        }

        // accessors
        public string GetKmsUri()     { return _kmsUri;     }
        public uint   GetKmsId()      { return _kmsId;      }
        public uint   GetKmsVersion() { return _kmsVersion; }

        // members
        private string _kmsUri;
        private uint   _kmsId;
        private uint   _kmsVersion;
    }
}
