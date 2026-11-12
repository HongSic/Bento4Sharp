// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4DecoderSpecificInfoDescriptor.h, Ap4DecoderSpecificInfoDescriptor.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Globalization;
using System.Text;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte DESCRIPTOR_TAG_DECODER_SPECIFIC_INFO = 0x05;
    }

    /*----------------------------------------------------------------------
    |   AP4_DecoderSpecificInfoDescriptor
    +---------------------------------------------------------------------*/
    public class DecoderSpecificInfoDescriptor : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_DecoderSpecificInfoDescriptor::AP4_DecoderSpecificInfoDescriptor
        +---------------------------------------------------------------------*/
        public DecoderSpecificInfoDescriptor(DataBuffer data) :
            base(Ap4.DESCRIPTOR_TAG_DECODER_SPECIFIC_INFO,
                 MinHeaderSize(data.GetDataSize()),
                 data.GetDataSize())
        {
            _info = new DataBuffer(data);
        }

        /*----------------------------------------------------------------------
        |   AP4_DecoderSpecificInfoDescriptor::AP4_DecoderSpecificInfoDescriptor
        +---------------------------------------------------------------------*/
        public DecoderSpecificInfoDescriptor(ByteStream stream, uint header_size, uint payload_size) :
            base(Ap4.DESCRIPTOR_TAG_DECODER_SPECIFIC_INFO,
                 header_size, payload_size)
        {
            _info = new DataBuffer();
            _info.SetDataSize(payload_size);
            stream.Read(_info.UseData(), payload_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_DecoderSpecificInfoDescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the info buffer
            if (_payloadSize != 0 && _info.GetDataSize() != 0) {
                stream.Write(_info.GetData(), _info.GetDataSize());
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DecoderSpecificInfoDescriptor::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            // PORT NOTE: the char buffer that upstream fills with
            // AP4_FormatString("%02x ") is built with a StringBuilder; the text is
            // the same (each byte is followed by a space, including the last one).
            StringBuilder info = new StringBuilder((int)(_info.GetDataSize()*3+1));
            for (uint i=0; i<_info.GetDataSize(); i++) {
                info.Append(_info.UseData()[i].ToString("x2", CultureInfo.InvariantCulture));
                info.Append(' ');
            }
            inspector.AddField("DecoderSpecificInfo", info.ToString());

            return Ap4Result.SUCCESS;
        }

        public virtual DataBuffer GetDecoderSpecificInfo() { return _info; }

        // members
        private DataBuffer _info;
    }
}
