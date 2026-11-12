// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Debug.h, Ap4Debug.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public static partial class Ap4
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        public const int DEBUG_MAX_BUFFER = 1024;

        /*----------------------------------------------------------------------
        |   AP4_Print
        +---------------------------------------------------------------------*/
        // PORT NOTE: printf("%s", message) writes the bytes of the message as
        // they are. Console.Out would re-encode the text with the console code
        // page (and replace what cannot be encoded), so the bytes are written to
        // the standard output stream directly, unbuffered, after flushing
        // Console.Out so that the output stays in order.
        private static System.IO.Stream s_Stdout;

        private static void Print(byte[] message, int size)
        {
            System.Console.Out.Flush();
            if (s_Stdout == null) {
                s_Stdout = System.Console.OpenStandardOutput();
            }
            s_Stdout.Write(message, 0, size);
            s_Stdout.Flush();
        }

        /*----------------------------------------------------------------------
        |   AP4_Debug
        +---------------------------------------------------------------------*/
        /// <summary>
        /// AP4_Debug. Like upstream this is NOT compiled out in release builds:
        /// the message is printed on the standard output.
        /// </summary>
        // PORT NOTE: upstream takes a printf format + varargs; callers format the
        // message themselves here (string interpolation yielding the same text).
        // The truncation to the 1024 byte buffer of upstream is reproduced: the
        // formatted message is a C string (it ends at the first 0 byte) of at
        // most 1023 bytes.
        public static void Debug(string message)
        {
            byte[] buffer = Ap4Utils.StringToCStringBytes(message);
            int size = buffer.Length;
            if (size > DEBUG_MAX_BUFFER - 1) {
                size = DEBUG_MAX_BUFFER - 1;
            }
            Print(buffer, size);
        }

        /*----------------------------------------------------------------------
        |   AP4_ASSERT
        +---------------------------------------------------------------------*/
        /// <summary>AP4_ASSERT: compiled out unless DEBUG is defined, like assert() under NDEBUG.</summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void ASSERT(bool condition)
        {
            System.Diagnostics.Debug.Assert(condition);
        }
    }
}
