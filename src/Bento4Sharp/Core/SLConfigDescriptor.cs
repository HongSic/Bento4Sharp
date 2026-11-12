// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SLConfigDescriptor.h, Ap4SLConfigDescriptor.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte DESCRIPTOR_TAG_SL_CONFIG = 0x06;
    }

    /*----------------------------------------------------------------------
    |   AP4_SLConfigDescriptor
    +---------------------------------------------------------------------*/
    public class SLConfigDescriptor : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_SLConfigDescriptor::AP4_SLConfigDescriptor
        +---------------------------------------------------------------------*/
        public SLConfigDescriptor(uint header_size = 2) :
            base(Ap4.DESCRIPTOR_TAG_SL_CONFIG, header_size, 1)
        {
            _predefined = 2;
        }

        /*----------------------------------------------------------------------
        |   AP4_SLConfigDescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the payload
            stream.WriteUI08(_predefined);

            return Ap4Result.SUCCESS;
        }

        // members
        private byte _predefined; // = 2 (fixed for MP4 files)
    }
}
