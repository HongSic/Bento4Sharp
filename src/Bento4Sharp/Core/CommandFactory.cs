// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4CommandFactory.h, Ap4CommandFactory.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_CommandFactory
    +---------------------------------------------------------------------*/
    public class CommandFactory
    {
        /*----------------------------------------------------------------------
        |   AP4_CommandFactory::CreateCommandFromStream
        +---------------------------------------------------------------------*/
        public static Ap4Result CreateCommandFromStream(ByteStream   stream,
                                                        out Command  command)
        {
            Ap4Result result;

            // NULL by default
            command = null;

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

            // create the command
            switch (tag) {
              case Ap4.COMMAND_TAG_OBJECT_DESCRIPTOR_UPDATE:
              case Ap4.COMMAND_TAG_IPMP_DESCRIPTOR_UPDATE:
                command = new DescriptorUpdateCommand(stream, tag, header_size, payload_size);
                break;

              default:
                command = new UnknownCommand(stream, tag, header_size, payload_size);
                break;
            }

            // skip to the end of the descriptor
            stream.Seek(offset+header_size+payload_size);

            return Ap4Result.SUCCESS;
        }
    }
}
