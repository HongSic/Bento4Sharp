// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Constants.h, Ap4Types.h, Ap4Version.h, Ap4Config.h
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /// <summary>
    /// Home of the global AP4_xxx constants and of the few global functions
    /// that have no better owner. Any ported file may add members to this
    /// partial class when upstream declares them in its header.
    /// </summary>
    // PORT NOTE: Ap4Constants.h only defines NULL and Ap4Types.h only defines
    // typedefs (mapped to C# primitive types, see docs/PORTING.md), so nothing
    // of them remains here.
    public static partial class Ap4
    {
        /*----------------------------------------------------------------------
        |   version constants (Ap4Version.h)
        +---------------------------------------------------------------------*/
        /// <summary>Version number of the SDK (AP4_VERSION)</summary>
        public const uint   VERSION        = 0x01060000;
        public const string VERSION_STRING = "1.6.0.0";

        /*----------------------------------------------------------------------
        |   byte order (Ap4Config.h)
        +---------------------------------------------------------------------*/
        public const int PLATFORM_BYTE_ORDER_BIG_ENDIAN    = 0;
        public const int PLATFORM_BYTE_ORDER_LITTLE_ENDIAN = 1;

        // PORT NOTE: a compile-time #define upstream; resolved at runtime here.
        public static readonly int PLATFORM_BYTE_ORDER =
            System.BitConverter.IsLittleEndian ? PLATFORM_BYTE_ORDER_LITTLE_ENDIAN : PLATFORM_BYTE_ORDER_BIG_ENDIAN;
    }
}
