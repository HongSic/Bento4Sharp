// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Utils.h, Ap4Utils.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   global options
    +---------------------------------------------------------------------*/
    public static class GlobalOptions
    {
        private class Entry
        {
            public string _name = "";
            public string _value = "";
        }

        private static List<Entry> g_Entries = null;

        /*----------------------------------------------------------------------
        |   AP4_GlobalOptions::GetEntry
        +---------------------------------------------------------------------*/
        private static Entry GetEntry(string name, bool autocreate)
        {
            if (g_Entries == null) {
                g_Entries = new List<Entry>();
            }
            foreach (Entry item in g_Entries) {
                if (item._name == name) return item;
            }

            if (autocreate) {
                Entry new_entry = new Entry();
                new_entry._name = name;
                g_Entries.Add(new_entry);
                return new_entry;
            } else {
                return null;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_GlobalOptions::SetBool
        +---------------------------------------------------------------------*/
        public static void SetBool(string name, bool value)
        {
            Entry entry = GetEntry(name, true);
            entry._value = value ? "true" : "false";
        }

        /*----------------------------------------------------------------------
        |   AP4_GlobalOptions::GetBool
        +---------------------------------------------------------------------*/
        public static bool GetBool(string name)
        {
            Entry entry = GetEntry(name, false);
            if (entry != null) {
                return entry._value == "true";
            } else {
                return false;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_GlobalOptions::SetString
        +---------------------------------------------------------------------*/
        public static void SetString(string name, string value)
        {
            Entry entry = GetEntry(name, true);
            entry._value = value;
        }

        /*----------------------------------------------------------------------
        |   AP4_GlobalOptions::GetString
        +---------------------------------------------------------------------*/
        public static string GetString(string name)
        {
            Entry entry = GetEntry(name, false);
            if (entry != null) {
                return entry._value;
            } else {
                return null;
            }
        }
    }

    /// <summary>
    /// The free functions of Ap4Utils.h (AP4_Xxx -> Ap4Utils.Xxx), plus the
    /// string helpers that implement the string encoding policy of the port.
    /// </summary>
    public static class Ap4Utils
    {
        /*----------------------------------------------------------------------
        |   AP4_BytesToDoubleBE
        +---------------------------------------------------------------------*/
        public static double BytesToDoubleBE(byte[] bytes, int offset = 0)
        {
            ulong i_value = BytesToUInt64BE(bytes, offset);
#if NET6_0_OR_GREATER
            return BitConverter.UInt64BitsToDouble(i_value);
#else
            return BitConverter.Int64BitsToDouble(unchecked((long)i_value));
#endif
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesToUInt64BE
        +---------------------------------------------------------------------*/
        public static ulong BytesToUInt64BE(byte[] bytes, int offset = 0)
        {
            return
                ( ((ulong)bytes[offset+0])<<56 ) |
                ( ((ulong)bytes[offset+1])<<48 ) |
                ( ((ulong)bytes[offset+2])<<40 ) |
                ( ((ulong)bytes[offset+3])<<32 ) |
                ( ((ulong)bytes[offset+4])<<24 ) |
                ( ((ulong)bytes[offset+5])<<16 ) |
                ( ((ulong)bytes[offset+6])<<8  ) |
                ( ((ulong)bytes[offset+7])     );
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesFromDoubleBE
        +---------------------------------------------------------------------*/
        public static void BytesFromDoubleBE(byte[] bytes, double value)
        {
            BytesFromDoubleBE(bytes, 0, value);
        }

        public static void BytesFromDoubleBE(byte[] bytes, int offset, double value)
        {
#if NET6_0_OR_GREATER
            ulong i_value = BitConverter.DoubleToUInt64Bits(value);
#else
            ulong i_value = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
#endif
            BytesFromUInt64BE(bytes, offset, i_value);
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesFromUInt64BE
        +---------------------------------------------------------------------*/
        public static void BytesFromUInt64BE(byte[] bytes, ulong value)
        {
            BytesFromUInt64BE(bytes, 0, value);
        }

        public static void BytesFromUInt64BE(byte[] bytes, int offset, ulong value)
        {
            bytes[offset+0] = (byte)((value >> 56) & 0xFF);
            bytes[offset+1] = (byte)((value >> 48) & 0xFF);
            bytes[offset+2] = (byte)((value >> 40) & 0xFF);
            bytes[offset+3] = (byte)((value >> 32) & 0xFF);
            bytes[offset+4] = (byte)((value >> 24) & 0xFF);
            bytes[offset+5] = (byte)((value >> 16) & 0xFF);
            bytes[offset+6] = (byte)((value >>  8) & 0xFF);
            bytes[offset+7] = (byte)((value      ) & 0xFF);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteSwap16
        +---------------------------------------------------------------------*/
        public static void ByteSwap16(byte[] bytes, uint count)
        {
            ByteSwap16(bytes, 0, count);
        }

        public static void ByteSwap16(byte[] bytes, int offset, uint count)
        {
            Ap4.ASSERT((count & 1) == 0);
            for (uint i = 0; i < count / 2; i++) {
                byte tmp = bytes[offset];
                bytes[offset] = bytes[offset+1];
                bytes[offset+1] = tmp;
                offset += 2;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesToUInt32BE
        +---------------------------------------------------------------------*/
        public static uint BytesToUInt32BE(byte[] bytes, int offset = 0)
        {
            return
                ( ((uint)bytes[offset+0])<<24 ) |
                ( ((uint)bytes[offset+1])<<16 ) |
                ( ((uint)bytes[offset+2])<<8  ) |
                ( ((uint)bytes[offset+3])     );
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesToInt32BE
        +---------------------------------------------------------------------*/
        public static int BytesToInt32BE(byte[] bytes, int offset = 0)
        {
            return (int)BytesToUInt32BE(bytes, offset);
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesToUInt24BE
        +---------------------------------------------------------------------*/
        public static uint BytesToUInt24BE(byte[] bytes, int offset = 0)
        {
            return
                ( ((uint)bytes[offset+0])<<16 ) |
                ( ((uint)bytes[offset+1])<<8  ) |
                ( ((uint)bytes[offset+2])     );
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesToUInt16BE
        +---------------------------------------------------------------------*/
        public static ushort BytesToUInt16BE(byte[] bytes, int offset = 0)
        {
            return (ushort)(
                ( ((uint)bytes[offset+0])<<8  ) |
                ( ((uint)bytes[offset+1])     ));
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesToInt16BE
        +---------------------------------------------------------------------*/
        public static short BytesToInt16BE(byte[] bytes, int offset = 0)
        {
            return (short)BytesToUInt16BE(bytes, offset);
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesFromUInt32BE
        +---------------------------------------------------------------------*/
        public static void BytesFromUInt32BE(byte[] bytes, uint value)
        {
            BytesFromUInt32BE(bytes, 0, value);
        }

        public static void BytesFromUInt32BE(byte[] bytes, int offset, uint value)
        {
            bytes[offset+0] = (byte)((value >> 24)&0xFF);
            bytes[offset+1] = (byte)((value >> 16)&0xFF);
            bytes[offset+2] = (byte)((value >>  8)&0xFF);
            bytes[offset+3] = (byte)((value      )&0xFF);
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesFromUInt24BE
        +---------------------------------------------------------------------*/
        public static void BytesFromUInt24BE(byte[] bytes, uint value)
        {
            BytesFromUInt24BE(bytes, 0, value);
        }

        public static void BytesFromUInt24BE(byte[] bytes, int offset, uint value)
        {
            bytes[offset+0] = (byte)((value >> 16)&0xFF);
            bytes[offset+1] = (byte)((value >>  8)&0xFF);
            bytes[offset+2] = (byte)((value      )&0xFF);
        }

        /*----------------------------------------------------------------------
        |   AP4_BytesFromUInt16BE
        +---------------------------------------------------------------------*/
        public static void BytesFromUInt16BE(byte[] bytes, ushort value)
        {
            BytesFromUInt16BE(bytes, 0, value);
        }

        public static void BytesFromUInt16BE(byte[] bytes, int offset, ushort value)
        {
            bytes[offset+0] = (byte)((value >> 8)&0xFF);
            bytes[offset+1] = (byte)((value     )&0xFF);
        }

        /*----------------------------------------------------------------------
        |   AP4_DurationMsFromUnits
        +---------------------------------------------------------------------*/
        public static uint DurationMsFromUnits(ulong units, uint units_per_second)
        {
            if (units_per_second == 0) return 0;
            return (uint)(((double)units*1000.0)/(double)units_per_second);
        }

        /*----------------------------------------------------------------------
        |   AP4_ConvertTime
        +---------------------------------------------------------------------*/
        public static ulong ConvertTime(ulong time_value,
                                        uint  from_time_scale,
                                        uint  to_time_scale)
        {
            if (from_time_scale == 0) return 0;
            double ratio = (double)to_time_scale/(double)from_time_scale;
            return ((ulong)(0.5+(double)time_value*ratio));
        }

        /*----------------------------------------------------------------------
        |   AP4_System_GenerateRandomBytes
        +---------------------------------------------------------------------*/
        // PORT NOTE: declared in Ap4Utils.h, implemented upstream in
        // System/Win32/Ap4Win32Random.cpp and System/Posix/Ap4PosixRandom.cpp.
        public static Ap4Result System_GenerateRandomBytes(byte[] buffer, uint buffer_size)
        {
#if NET6_0_OR_GREATER
            System.Security.Cryptography.RandomNumberGenerator.Fill(new Span<byte>(buffer, 0, (int)buffer_size));
#else
            Compat.FillRandom(buffer, (int)buffer_size);
#endif
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_FormatFourChars
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Returns the 4 bytes of <paramref name="value"/> as a string, with the
        /// semantics of the C string that upstream produces: the result stops at
        /// the first zero byte (so it can be shorter than 4 chars), and bytes are
        /// decoded with <see cref="StringFromBytes(byte[], int, uint)"/>.
        /// </summary>
        public static string FormatFourChars(uint value)
        {
            byte[] str = new byte[4];
            str[0] = (byte)((value >> 24) & 0xFF);
            str[1] = (byte)((value >> 16) & 0xFF);
            str[2] = (byte)((value >>  8) & 0xFF);
            str[3] = (byte)((value      ) & 0xFF);
            return StringFromCString(str, 0, 4);
        }

        /*----------------------------------------------------------------------
        |   AP4_FormatFourCharsPrintable
        +---------------------------------------------------------------------*/
        /// <summary>Always 4 chars; bytes outside 0x20..0x7E are replaced by '.'</summary>
        public static string FormatFourCharsPrintable(uint value)
        {
            char[] str = new char[4];
            str[0] = (char)((value >> 24) & 0xFF);
            str[1] = (char)((value >> 16) & 0xFF);
            str[2] = (char)((value >>  8) & 0xFF);
            str[3] = (char)((value      ) & 0xFF);
            for (int i=0; i<4; i++) {
                // PORT NOTE: upstream tests a (signed) char: bytes >= 0x80 are
                // negative, hence < ' ', and are also replaced.
                if (str[i]<' ' || str[i] >= 127) {
                    str[i] = '.';
                }
            }
            return new string(str);
        }

        /*----------------------------------------------------------------------
        |   AP4_SplitArgs
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream splits the string in place. Here arg0 and arg1 are
        // returned as new strings. On failure arg0 is the whole input and arg1 is
        // an empty string (unassigned upstream).
        public static Ap4Result SplitArgs(string arg, out string arg0, out string arg1)
        {
            int c = 0;
            while (c < arg.Length && arg[c] != '\0' && arg[c] != ':') {
                c++;
            }
            if (c < arg.Length && arg[c] == ':') {
                arg0 = arg.Substring(0, c);
                arg1 = arg.Substring(c+1);
                return Ap4Result.SUCCESS;
            } else {
                arg0 = arg;
                arg1 = "";
                return Ap4Result.FAILURE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SplitArgs
        +---------------------------------------------------------------------*/
        public static Ap4Result SplitArgs(string arg, out string arg0, out string arg1, out string arg2)
        {
            arg2 = "";
            Ap4Result result = SplitArgs(arg, out arg0, out arg1);
            if (result.Failed()) return result;
            return SplitArgs(arg1, out arg1, out arg2);
        }

        /*----------------------------------------------------------------------
        |   AP4_HexNibble
        +---------------------------------------------------------------------*/
        public static byte HexNibble(char c)
        {
            switch (c) {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case 'a': case 'A': return 10;
                case 'b': case 'B': return 11;
                case 'c': case 'C': return 12;
                case 'd': case 'D': return 13;
                case 'e': case 'E': return 14;
                case 'f': case 'F': return 15;
                default: return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_NibbleHex
        +---------------------------------------------------------------------*/
        public static char NibbleHex(uint nibble)
        {
            if (nibble < 10) {
                return (char)('0'+nibble);
            } else if (nibble < 16) {
                return (char)('A'+(nibble-10));
            } else {
                return ' ';
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_ParseHex
        +---------------------------------------------------------------------*/
        public static Ap4Result ParseHex(string hex, byte[] bytes, uint count)
        {
            return ParseHex(hex, 0, bytes, count);
        }

        /// <summary>Same as ParseHex, starting at char <paramref name="hex_offset"/> of <paramref name="hex"/> (upstream: pointer into a string).</summary>
        public static Ap4Result ParseHex(string hex, int hex_offset, byte[] bytes, uint count)
        {
            // AP4_StringLength(hex)
            uint length = 0;
            while (hex_offset+length < hex.Length && hex[(int)(hex_offset+length)] != '\0') ++length;
            if (length < 2*count) return Ap4Result.ERROR_INVALID_PARAMETERS;
            for (uint i=0; i<count; i++) {
                bytes[i] = (byte)((HexNibble(hex[(int)(hex_offset+2*i)]) << 4) | (HexNibble(hex[(int)(hex_offset+2*i+1)])));
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_FormatHex
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream writes 2*data_size chars in a caller supplied buffer
        // (without terminator); the port returns them as a string. Upper case.
        public static Ap4Result FormatHex(byte[] data, uint data_size, out string hex)
        {
            return FormatHex(data, 0, data_size, out hex);
        }

        public static Ap4Result FormatHex(byte[] data, int offset, uint data_size, out string hex)
        {
            char[] chars = new char[2*data_size];
            int h = 0;
            for (uint i=0; i<data_size; i++) {
                chars[h++] = NibbleHex((uint)(data[offset+i]>>4));
                chars[h++] = NibbleHex((uint)(data[offset+i]&0x0F));
            }
            hex = new string(chars);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ParseIntegerU
        +---------------------------------------------------------------------*/
        public static uint ParseIntegerU(string str)
        {
            if (str == null) {
                return 0;
            }

            // parse the digits
            uint value = 0;
            foreach (char c in str) {
                if (c == '\0') break;
                if (c >= '0' && c <= '9') {
                    value = 10*value + (uint)(c-'0');
                } else {
                    return 0;
                }
            }

            return value;
        }

        /*----------------------------------------------------------------------
        |   string helpers (PORT NOTE: not in upstream)
        |
        |   Upstream strings (AP4_String, char*) are raw bytes. The port uses
        |   System.String and converts with the functions below, which decode
        |   and encode UTF-8 *losslessly*:
        |     - valid UTF-8 sequences are decoded to the matching chars
        |       (surrogate pairs for code points above U+FFFF);
        |     - every byte that is not part of a valid UTF-8 sequence (stray
        |       0x80..0xFF bytes, overlong forms, encoded surrogates, truncated
        |       sequences) is decoded to the lone low surrogate U+DC00+byte, i.e.
        |       U+DC80..U+DCFF (the 'surrogateescape' scheme of Python);
        |     - encoding does the reverse: a lone U+DC80..U+DCFF becomes the
        |       original single byte; any other unpaired surrogate (which cannot
        |       come from decoding) becomes EF BF BD (U+FFFD).
        |   So bytes -> string -> bytes is the identity for ANY byte sequence,
        |   and string -> bytes -> string is the identity for any well-formed
        |   string.
        +---------------------------------------------------------------------*/

        /// <summary>
        /// Decodes <paramref name="size"/> bytes (AP4_String(const char*, AP4_Size)
        /// semantics: embedded zero bytes are kept as U+0000).
        /// </summary>
        public static string StringFromBytes(byte[] bytes, int offset, uint size)
        {
            if (size == 0) return "";
            int end = offset + (int)size;

            // shortcut for pure ASCII
            bool ascii = true;
            for (int x = offset; x < end; x++) {
                if (bytes[x] >= 0x80) { ascii = false; break; }
            }
            if (ascii) {
#if NET6_0_OR_GREATER
                return string.Create((int)size, new System.ValueTuple<byte[], int>(bytes, offset), (span, state) => {
                    for (int x = 0; x < span.Length; x++) span[x] = (char)state.Item1[state.Item2 + x];
                });
#else
                char[] chars = new char[size];
                for (int x = 0; x < chars.Length; x++) chars[x] = (char)bytes[offset + x];
                return new string(chars);
#endif
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder((int)size);
            int i = offset;
            while (i < end) {
                uint b0 = bytes[i];
                if (b0 < 0x80) {
                    sb.Append((char)b0);
                    i += 1;
                    continue;
                }
                int avail = end - i;
                if (b0 >= 0xC2 && b0 <= 0xDF) {
                    if (avail >= 2 && IsCont(bytes[i+1])) {
                        sb.Append((char)(((b0 & 0x1F) << 6) | (uint)(bytes[i+1] & 0x3F)));
                        i += 2;
                        continue;
                    }
                } else if (b0 >= 0xE0 && b0 <= 0xEF) {
                    if (avail >= 3 && IsCont(bytes[i+1]) && IsCont(bytes[i+2])) {
                        uint b1 = bytes[i+1];
                        bool ok = true;
                        if (b0 == 0xE0 && b1 < 0xA0) ok = false; // overlong
                        if (b0 == 0xED && b1 > 0x9F) ok = false; // surrogates
                        if (ok) {
                            sb.Append((char)(((b0 & 0x0F) << 12) | ((b1 & 0x3F) << 6) | (uint)(bytes[i+2] & 0x3F)));
                            i += 3;
                            continue;
                        }
                    }
                } else if (b0 >= 0xF0 && b0 <= 0xF4) {
                    if (avail >= 4 && IsCont(bytes[i+1]) && IsCont(bytes[i+2]) && IsCont(bytes[i+3])) {
                        uint b1 = bytes[i+1];
                        bool ok = true;
                        if (b0 == 0xF0 && b1 < 0x90) ok = false; // overlong
                        if (b0 == 0xF4 && b1 > 0x8F) ok = false; // > U+10FFFF
                        if (ok) {
                            uint cp = ((b0 & 0x07) << 18) | ((b1 & 0x3F) << 12) | ((uint)(bytes[i+2] & 0x3F) << 6) | (uint)(bytes[i+3] & 0x3F);
                            cp -= 0x10000;
                            sb.Append((char)(0xD800 + (cp >> 10)));
                            sb.Append((char)(0xDC00 + (cp & 0x3FF)));
                            i += 4;
                            continue;
                        }
                    }
                }
                // invalid byte: escape it
                sb.Append((char)(0xDC00 + b0));
                i += 1;
            }
            return sb.ToString();
        }

        private static bool IsCont(byte b) { return (b & 0xC0) == 0x80; }

        /// <summary>Decodes a whole array (see <see cref="StringFromBytes(byte[], int, uint)"/>).</summary>
        public static string StringFromBytes(byte[] bytes)
        {
            return StringFromBytes(bytes, 0, (uint)bytes.Length);
        }

        /// <summary>
        /// Decodes a C string stored in a buffer (AP4_String(const char*)
        /// semantics): stops at the first zero byte, or after
        /// <paramref name="max_size"/> bytes if there is none.
        /// </summary>
        public static string StringFromCString(byte[] bytes, int offset, uint max_size)
        {
            uint size = 0;
            while (size < max_size && bytes[offset+size] != 0) ++size;
            return StringFromBytes(bytes, offset, size);
        }

        /// <summary>Encodes all the chars of a string (no terminator is added; embedded U+0000 are kept).</summary>
        public static byte[] StringToBytes(string str)
        {
#if !NET20
            if (string.IsNullOrEmpty(str)) return Array.Empty<byte>();
#else
            if (string.IsNullOrEmpty(str)) return new byte[0];
#endif

            // compute the size
            int size = 0;
            bool ascii = true;
            for (int i = 0; i < str.Length; i++) {
                char c = str[i];
                if (c < 0x80) {
                    size += 1;
                } else {
                    ascii = false;
                    if (c < 0x800) {
                        size += 2;
                    } else if (char.IsHighSurrogate(c) && i+1 < str.Length && char.IsLowSurrogate(str[i+1])) {
                        size += 4;
                        ++i;
                    } else if (c >= 0xDC80 && c <= 0xDCFF) {
                        size += 1; // escaped byte
                    } else {
                        size += 3; // BMP char, or unpaired surrogate (-> U+FFFD)
                    }
                }
            }

            byte[] bytes = new byte[size];
            if (ascii) {
                for (int i = 0; i < str.Length; i++) bytes[i] = (byte)str[i];
                return bytes;
            }

            int o = 0;
            for (int i = 0; i < str.Length; i++) {
                uint c = str[i];
                if (c < 0x80) {
                    bytes[o++] = (byte)c;
                } else if (c < 0x800) {
                    bytes[o++] = (byte)(0xC0 | (c >> 6));
                    bytes[o++] = (byte)(0x80 | (c & 0x3F));
                } else if (char.IsHighSurrogate((char)c) && i+1 < str.Length && char.IsLowSurrogate(str[i+1])) {
                    uint cp = 0x10000 + ((c - 0xD800) << 10) + ((uint)str[i+1] - 0xDC00);
                    ++i;
                    bytes[o++] = (byte)(0xF0 | (cp >> 18));
                    bytes[o++] = (byte)(0x80 | ((cp >> 12) & 0x3F));
                    bytes[o++] = (byte)(0x80 | ((cp >> 6) & 0x3F));
                    bytes[o++] = (byte)(0x80 | (cp & 0x3F));
                } else if (c >= 0xDC80 && c <= 0xDCFF) {
                    bytes[o++] = (byte)(c - 0xDC00);
                } else {
                    if (c >= 0xD800 && c <= 0xDFFF) c = 0xFFFD;
                    bytes[o++] = (byte)(0xE0 | (c >> 12));
                    bytes[o++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                    bytes[o++] = (byte)(0x80 | (c & 0x3F));
                }
            }
            return bytes;
        }

        /// <summary>
        /// Encodes a string as a C string: the result stops before the first
        /// U+0000, if any (no terminator is added).
        /// </summary>
        public static byte[] StringToCStringBytes(string str)
        {
#if !NET20
            if (string.IsNullOrEmpty(str)) return Array.Empty<byte>();
#else
            if (string.IsNullOrEmpty(str)) return new byte[0];
#endif
            int zero = str.IndexOf('\0');
            if (zero >= 0) str = str.Substring(0, zero);
            return StringToBytes(str);
        }

        /// <summary>
        /// AP4_StringLength / strlen / AP4_String::GetLength(): the number of
        /// BYTES of the encoded string, up to (not including) the first U+0000.
        /// Use this, never string.Length, when computing atom sizes.
        /// </summary>
        public static uint StringLength(string str)
        {
            if (string.IsNullOrEmpty(str)) return 0;
            uint size = 0;
            for (int i = 0; i < str.Length; i++) {
                char c = str[i];
                if (c == 0) break;
                if (c < 0x80) {
                    size += 1;
                } else if (c < 0x800) {
                    size += 2;
                } else if (char.IsHighSurrogate(c) && i+1 < str.Length && char.IsLowSurrogate(str[i+1])) {
                    size += 4;
                    ++i;
                } else if (c >= 0xDC80 && c <= 0xDCFF) {
                    size += 1;
                } else {
                    size += 3;
                }
            }
            return size;
        }

        /// <summary>
        /// printf("%f", value): 6 decimals, invariant culture, "inf"/"-inf"/"nan"
        /// for non finite values.
        /// </summary>
        public static string FormatFloat(double value)
        {
            if (double.IsNaN(value)) return "nan";
            if (double.IsPositiveInfinity(value)) return "inf";
            if (double.IsNegativeInfinity(value)) return "-inf";
#if NET6_0_OR_GREATER
            return value.ToString("F6", CultureInfo.InvariantCulture);
#else
            return Compat.FormatFixed(value, 6);
#endif
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_BitWriter
    +---------------------------------------------------------------------*/
    public class BitWriter
    {
        public BitWriter(uint size)
        {
            _dataSize = size;
            _bitCount = 0;
            if (size != 0) {
                _data = new byte[size];
            } else {
                // PORT NOTE: NULL upstream
#if !NET20
                _data = Array.Empty<byte>();
#else
                _data = new byte[0];
#endif
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_BitWriter::Write
        +---------------------------------------------------------------------*/
        public void Write(uint bits, uint bit_count)
        {
            byte[] data = _data;
            if (_bitCount+bit_count > _dataSize*8) return;
            uint data_index = _bitCount/8;
            uint space = 8-(_bitCount%8);
            while (bit_count != 0) {
                uint mask = bit_count==32 ? 0xFFFFFFFF : (uint)((1<<(int)bit_count)-1);
                if (bit_count <= space) {
                    data[data_index] |= (byte)((bits&mask) << (int)(space-bit_count));
                    _bitCount += bit_count;
                    return;
                } else {
                    data[data_index] |= (byte)((bits&mask) >> (int)(bit_count-space));
                    ++data_index;
                    _bitCount += space;
                    bit_count  -= space;
                    space       = 8;
                }
            }
        }

        public uint GetBitCount() { return _bitCount; }

        /// <summary>The whole buffer (its Length is the size given to the constructor).</summary>
        public byte[] GetData() { return _data; }

        private byte[] _data;
        private uint   _dataSize;
        private uint   _bitCount;
    }

    /*----------------------------------------------------------------------
    |   AP4_BitReader
    +---------------------------------------------------------------------*/
    public class BitReader
    {
        /*----------------------------------------------------------------------
        |   types and macros
        +---------------------------------------------------------------------*/
        // typedef unsigned int BitsWord;  -> uint
        private const uint AP4_WORD_BITS  = 32;
        private const uint AP4_WORD_BYTES = 4;
        // PORT NOTE: (1<<32) is undefined in C++; C# masks the shift count to 5
        // bits like x86 does, so both yield the same values.
        private static uint AP4_BIT_MASK(uint n) { return (uint)((1<<(int)n)-1); }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::AP4_BitReader
        +---------------------------------------------------------------------*/
        public BitReader(byte[] data, uint data_size) : this(data, 0, data_size)
        {
        }

        /// <summary>PORT NOTE: overload for upstream call sites that pass a pointer into a buffer.</summary>
        public BitReader(byte[] data, int offset, uint data_size)
        {
            _position = 0;
            _cache = 0;
            _bitsCached = 0;

            // make the buffer an integral mulitple of the word size
            _buffer.SetBufferSize(AP4_WORD_BYTES*((data_size+AP4_WORD_BYTES-1)/AP4_WORD_BYTES));
            _buffer.SetData(data, offset, data_size);
            if (_buffer.GetDataSize() != _buffer.GetBufferSize()) {
                Array.Clear(_buffer.UseData(), (int)_buffer.GetDataSize(), (int)(_buffer.GetBufferSize()-_buffer.GetDataSize()));
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::Reset
        +---------------------------------------------------------------------*/
        public Ap4Result Reset()
        {
            _position   = 0;
            _cache      = 0;
            _bitsCached = 0;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::GetBitsRead
        +---------------------------------------------------------------------*/
        public uint GetBitsRead()
        {
            return 8*_position - _bitsCached;
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::ReadCache
        +---------------------------------------------------------------------*/
        private uint ReadCache()
        {
            // stop reading past the end of the buffer when more bits are requested
            // than the input contains; the missing bytes read as zero
            if (_position+AP4_WORD_BYTES > _buffer.GetBufferSize()) {
                return 0;
            }
            byte[] out_ptr = _buffer.GetData();
            uint p = _position;
            return (((uint) out_ptr[p+0]) << 24) |
                   (((uint) out_ptr[p+1]) << 16) |
                   (((uint) out_ptr[p+2]) <<  8) |
                   (((uint) out_ptr[p+3])      );
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::ReadBits
        +---------------------------------------------------------------------*/
        public uint ReadBits(uint n)
        {
            if (n == 0) return 0;

            uint result;
            if (_bitsCached >= n) {
                /* we have enough bits in the cache to satisfy the request */
                _bitsCached -= n;
                result = (_cache >> (int)_bitsCached) & AP4_BIT_MASK(n);
            } else {
                /* not enough bits in the cache */
                uint word = ReadCache();
                _position += AP4_WORD_BYTES;

                /* combine the new word and the cache, and update the state */
                uint cache = _cache & AP4_BIT_MASK(_bitsCached);
                n -= _bitsCached;
                _bitsCached = AP4_WORD_BITS - n;
                result = _bitsCached != 0 ? (word >> (int)_bitsCached) | (cache << (int)n) : word;
                _cache = word;
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::ReadBit
        +---------------------------------------------------------------------*/
        public int ReadBit()
        {
            uint result;
            if (_bitsCached == 0) {
                /* the cache is empty */

                /* read the next word into the cache */
                _cache = ReadCache();
                _position += AP4_WORD_BYTES;
                _bitsCached = AP4_WORD_BITS - 1;

                /* return the first bit */
                result = _cache >> (int)(AP4_WORD_BITS - 1);
            } else {
                /* get the bit from the cache */
                result = (_cache >> (int)(--_bitsCached)) & 1;
            }
            return (int)result;
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::PeekBits
        +---------------------------------------------------------------------*/
        public uint PeekBits(uint n)
        {
           /* we have enough bits in the cache to satisfy the request */
           if (_bitsCached >= n) {
              return (_cache >> (int)(_bitsCached - n)) & AP4_BIT_MASK(n);
           } else {
              /* not enough bits in the cache, read the next word */
              uint word = ReadCache();

              /* combine the new word and the cache, and update the state */
              uint cache = _cache & AP4_BIT_MASK(_bitsCached);
              n -= _bitsCached;
              return (word >> (int)(AP4_WORD_BITS - n)) | (cache << (int)n);
           }
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::PeekBit
        +---------------------------------------------------------------------*/
        public int PeekBit()
        {
           /* the cache is empty */
           if (_bitsCached == 0) {
              /* read the next word into the cache */
              uint cache = ReadCache();

              /* return the first bit */
              return (int)(cache >> (int)(AP4_WORD_BITS - 1));
           } else {
              /* get the bit from the cache */
              return (int)((_cache >> (int)(_bitsCached-1)) & 1);
           }
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::SkipBits
        +---------------------------------------------------------------------*/
        public void SkipBits(uint n)
        {
           if (n <= _bitsCached) {
              _bitsCached -= n;
           } else {
              n -= _bitsCached;
              while (n >= AP4_WORD_BITS) {
                 _position += AP4_WORD_BYTES;
                 n -= AP4_WORD_BITS;
              }
              if (n != 0) {
                 _cache = ReadCache();
                 _bitsCached = AP4_WORD_BITS-n;
                 _position += AP4_WORD_BYTES;
              } else {
                 _bitsCached = 0;
                 _cache = 0;
              }
           }
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::SkipBit
        +---------------------------------------------------------------------*/
        public void SkipBit()
        {
           if (_bitsCached == 0) {
              _cache = ReadCache();
              _position += AP4_WORD_BYTES;
              _bitsCached = AP4_WORD_BITS - 1;
           } else {
              --_bitsCached;
           }
        }

        /*----------------------------------------------------------------------
        |   AP4_BitReader::GetPosition
        +---------------------------------------------------------------------*/
        public uint GetBitsPosition()
        {
            return _position * 8 - _bitsCached;
        }

        // PORT NOTE: AP4_BitReader::SkipBytes is declared in Ap4Utils.h but has
        // no implementation upstream; it is not ported.

        // members
        private DataBuffer _buffer = new DataBuffer();
        private uint       _position;
        private uint       _cache;
        private uint       _bitsCached;
    }
}
