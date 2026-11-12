// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4DescriptorFactory.h, Ap4DescriptorFactory.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_DescriptorFactory
    +---------------------------------------------------------------------*/
    public class DescriptorFactory
    {
        /*----------------------------------------------------------------------
        |   AP4_DescriptorFactory::CreateDescriptorFromStream
        +---------------------------------------------------------------------*/
        public static Ap4Result CreateDescriptorFromStream(ByteStream      stream,
                                                           out Descriptor  descriptor)
        {
            Ap4Result result;

            // NULL by default
            descriptor = null;

            // remember current stream offset
            ulong offset;
            stream.Tell(out offset);

            // read descriptor tag
            byte tag;
            result = stream.ReadUI08(out tag);
            if (result.Failed()) {
                stream.Seek(offset);
                return result;
            }

            // read descriptor size
            uint payload_size = 0;
            uint header_size = 1;
            uint max  = 4;
            byte ext  = 0;
            do {
                header_size++;
                result = stream.ReadUI08(out ext);
                if (result.Failed()) {
                    stream.Seek(offset);
                    return result;
                }
                payload_size = (payload_size<<7) + (uint)(ext&0x7F);
            } while (--max != 0 && (ext&0x80) != 0);

            // create the descriptor
            if (payload_size != 0) {
                switch (tag) {
                  case Ap4.DESCRIPTOR_TAG_OD:
                  case Ap4.DESCRIPTOR_TAG_MP4_OD:
                    descriptor = new ObjectDescriptor(stream, tag, header_size, payload_size);
                    break;

                  case Ap4.DESCRIPTOR_TAG_IOD:
                  case Ap4.DESCRIPTOR_TAG_MP4_IOD:
                    descriptor = new InitialObjectDescriptor(stream, tag, header_size, payload_size);
                    break;

                  case Ap4.DESCRIPTOR_TAG_ES_ID_INC:
                    descriptor = new EsIdIncDescriptor(stream, header_size, payload_size);
                    break;

                  case Ap4.DESCRIPTOR_TAG_ES_ID_REF:
                    descriptor = new EsIdRefDescriptor(stream, header_size, payload_size);
                    break;

                  case Ap4.DESCRIPTOR_TAG_ES:
                    descriptor = new EsDescriptor(stream, header_size, payload_size);
                    break;

                  case Ap4.DESCRIPTOR_TAG_DECODER_CONFIG:
                    descriptor = new DecoderConfigDescriptor(stream, header_size, payload_size);
                    break;

                  case Ap4.DESCRIPTOR_TAG_DECODER_SPECIFIC_INFO:
                    descriptor = new DecoderSpecificInfoDescriptor(stream, header_size, payload_size);
                    break;

                  case Ap4.DESCRIPTOR_TAG_SL_CONFIG:
                    if (payload_size != 1) return Ap4Result.ERROR_INVALID_FORMAT;
                    descriptor = new SLConfigDescriptor(header_size);
                    break;

                  case Ap4.DESCRIPTOR_TAG_IPMP_DESCRIPTOR_POINTER:
                    descriptor = new IpmpDescriptorPointer(stream, header_size, payload_size);
                    break;

                  case Ap4.DESCRIPTOR_TAG_IPMP_DESCRIPTOR:
                    descriptor = new IpmpDescriptor(stream, header_size, payload_size);
                    break;

                  default:
                    descriptor = new UnknownDescriptor(stream, tag, header_size, payload_size);
                    break;
                }
            } else {
                stream.Seek(offset);
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // skip to the end of the descriptor
            stream.Seek(offset+header_size+payload_size);

            return Ap4Result.SUCCESS;
        }
    }
}
