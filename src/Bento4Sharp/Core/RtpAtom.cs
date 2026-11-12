// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4RtpAtom.h, Ap4RtpAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_RtpAtom
    +---------------------------------------------------------------------*/
    public class RtpAtom : Atom
    {
        // class methods
        public static RtpAtom Create(uint size, ByteStream stream) {
            return new RtpAtom(size, stream);
        }

        // methods
        public string GetSdpText() { return _sdpText; }

        /*----------------------------------------------------------------------
        |   AP4_RtpAtom::AP4_RtpAtom
        +---------------------------------------------------------------------*/
        private RtpAtom(uint size, ByteStream stream) :
            base(AtomType.RTP_, size)
        {
            // desc format
            stream.ReadUI32(out _descriptionFormat);

            // sdptext
            if (size >= Ap4.ATOM_HEADER_SIZE+4) {
                // PORT NOTE: upstream uses a signed int for str_size.
                int str_size = (int)(size-(Ap4.ATOM_HEADER_SIZE+4));
                if (str_size != 0) {
                    byte[] str = new byte[str_size+1];
                    stream.Read(str, (uint)str_size);
                    str[str_size] = 0; // force null-termination
                    _sdpText = Ap4Utils.StringFromCString(str, 0, (uint)str_size+1);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // description format
            result = stream.WriteUI32(_descriptionFormat);
            if (result.Failed()) return result;

            // sdp text
            result = stream.WriteString(_sdpText);
            if (result.Failed()) return result;

            // pad with zeros if necessary
            uint padding = _size32-(Ap4.ATOM_HEADER_SIZE+4+Ap4Utils.StringLength(_sdpText));
            while (padding-- != 0) stream.WriteUI08(0);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_RtpAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string format_string = Ap4Utils.FormatFourChars(_descriptionFormat);
            inspector.AddField("description_format", format_string);
            inspector.AddField("sdp_text", _sdpText);

            return Ap4Result.SUCCESS;
        }

        // members
        private uint   _descriptionFormat;
        private string _sdpText = "";
    }
}
