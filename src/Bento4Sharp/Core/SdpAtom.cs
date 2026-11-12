// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SdpAtom.h, Ap4SdpAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SdpAtom
    +---------------------------------------------------------------------*/
    public class SdpAtom : Atom
    {
        // class methods
        public static SdpAtom Create(uint size, ByteStream stream) {
            return new SdpAtom(size, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_SdpAtom::AP4_SdpAtom
        +---------------------------------------------------------------------*/
        public SdpAtom(string sdp_text) :
            base(AtomType.SDP_, Ap4.ATOM_HEADER_SIZE)
        {
            _sdpText = sdp_text ?? "";
            _size32 += Ap4Utils.StringLength(_sdpText)+1;
        }

        /*----------------------------------------------------------------------
        |   AP4_SdpAtom::AP4_SdpAtom
        +---------------------------------------------------------------------*/
        private SdpAtom(uint size, ByteStream stream) :
            base(AtomType.SDP_, size)
        {
            // sdptext
            uint str_size = size-Ap4.ATOM_HEADER_SIZE;
            if (str_size > 0) {
                byte[] str = new byte[str_size+1];
                stream.Read(str, str_size);
                str[str_size] = 0; // force null-termination
                _sdpText = Ap4Utils.StringFromCString(str, 0, str_size+1);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SdpAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // sdptext
            Ap4Result result = stream.WriteString(_sdpText);
            if (result.Failed()) return result;

            // pad with zeros if necessary
            uint padding = _size32-(Ap4.ATOM_HEADER_SIZE+Ap4Utils.StringLength(_sdpText));
            while (padding-- != 0) stream.WriteUI08(0);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SdpAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("sdp_text", _sdpText);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SdpAtom::GetSdpText
        +---------------------------------------------------------------------*/
        public string GetSdpText()
        {
            return _sdpText;
        }

        // members
        private string _sdpText = "";
    }
}
