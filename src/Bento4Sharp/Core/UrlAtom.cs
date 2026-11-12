// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4UrlAtom.h, Ap4UrlAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_UrlAtom
    +---------------------------------------------------------------------*/
    public class UrlAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_UrlAtom::Create
        +---------------------------------------------------------------------*/
        public static UrlAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new UrlAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_UrlAtom::AP4_UrlAtom
        +---------------------------------------------------------------------*/
        public UrlAtom() : // local ref only (no URL string)
            base(AtomType.URL, Ap4.FULL_ATOM_HEADER_SIZE, 0, 1)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_UrlAtom::AP4_UrlAtom
        +---------------------------------------------------------------------*/
        private UrlAtom(uint       size,
                        byte       version,
                        uint       flags,
                        ByteStream stream) :
            base(AtomType.URL, size, version, flags)
        {
            if ((_flags & 1) == 0) {
                // not self contained
                uint str_size = size-Ap4.FULL_ATOM_HEADER_SIZE;
                if (str_size > 0) {
                    byte[] str = new byte[str_size];
                    stream.Read(str, str_size);
                    str[str_size-1] = 0; // force null-termination
                    _url = Ap4Utils.StringFromCString(str, 0, str_size);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_UrlAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            if ((_flags & 1) != 0) {
                // local ref (self contained)
                return Ap4Result.SUCCESS;
            } else {
                // url (not self contained)
                if (_size32 > Ap4.FULL_ATOM_HEADER_SIZE) {
                    // stream.Write(m_Url.GetChars(), m_Url.GetLength()+1)
                    Ap4Result result = stream.WriteString(_url);
                    if (result.Failed()) return result;
                    result = stream.WriteUI08(0);
                    if (result.Failed()) return result;

                    // pad with zeros if necessary
                    uint padding = _size32-(Ap4.FULL_ATOM_HEADER_SIZE+Ap4Utils.StringLength(_url)+1);
                    while (padding-- != 0) stream.WriteUI08(0);
                }
                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_UrlAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            if ((_flags & 1) != 0) {
                inspector.AddField("location", "[local to file]");
            } else {
                inspector.AddField("location", _url);
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private string _url = "";
    }
}
