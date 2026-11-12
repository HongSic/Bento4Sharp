// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Not ported from upstream: support code for the older target frameworks.
// Licensed under the GNU General Public License v2 or later. See LICENSE.

// PORT NOTE: the library is written against the .NET 8 base class library.
// This file and Compat.Extensions.cs hold what the other targets
// (netstandard2.0, netstandard1.3, net20) need to compile the same sources
// without any extra package:
//  - here: helpers used by the few call sites that need an `#if` because the
//    member is static or takes a Span (the net8.0 branch of those sites is the
//    original code). This file is also compiled into each tool for those
//    targets (see Directory.Build.props);
//  - in Compat.Extensions.cs (library only): extension methods standing in
//    for instance members that the old BCL lacks, so that the call sites are
//    identical for every target.
// Nothing in this file is compiled for net8.0.

#if !NET6_0_OR_GREATER

using System;
using System.Collections.Generic;

namespace Bento4
{

    internal static partial class Compat
    {
        /// <summary>Array.MaxLength (.NET 6+): the largest array the runtime allocates.</summary>
        public const int ArrayMaxLength = 0x7FFFFFC7;

        /// <summary>MemoryExtensions.SequenceEqual on two whole arrays (a null array is an empty one, as with spans).</summary>
        public static bool SequenceEqual(byte[] a, byte[] b)
        {
            int a_length = a == null ? 0 : a.Length;
            int b_length = b == null ? 0 : b.Length;
            if (a_length != b_length) return false;
            for (int i = 0; i < a_length; i++) {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        /// <summary>a.AsSpan(a_offset, size).SequenceEqual(b.AsSpan(b_offset, size)), with the same range checks.</summary>
        public static bool SequenceEqual(byte[] a, int a_offset, byte[] b, int b_offset, int size)
        {
            if (a == null || (uint)a_offset > (uint)a.Length || (uint)size > (uint)(a.Length - a_offset)) {
                throw new ArgumentOutOfRangeException("a");
            }
            if (b == null || (uint)b_offset > (uint)b.Length || (uint)size > (uint)(b.Length - b_offset)) {
                throw new ArgumentOutOfRangeException("b");
            }
            for (int i = 0; i < size; i++) {
                if (a[a_offset + i] != b[b_offset + i]) return false;
            }
            return true;
        }

        /// <summary>RandomNumberGenerator.Fill(new Span&lt;byte&gt;(buffer, 0, size)).</summary>
        public static void FillRandom(byte[] buffer, int size)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if ((uint)size > (uint)buffer.Length) throw new ArgumentOutOfRangeException("size");
            if (size == 0) return;
            System.Security.Cryptography.RandomNumberGenerator rng = Rng;
            if (size == buffer.Length) {
                lock (rng) rng.GetBytes(buffer);
            } else {
                // GetBytes(byte[], int, int) does not exist before .NET Framework 4.6
                byte[] bytes = new byte[size];
                lock (rng) rng.GetBytes(bytes);
                Array.Copy(bytes, 0, buffer, 0, size);
            }
        }

        // PORT NOTE: never disposed: RandomNumberGenerator is not IDisposable on
        // .NET Framework 2.0, and one instance serves the whole process.
        private static readonly System.Security.Cryptography.RandomNumberGenerator Rng =
            System.Security.Cryptography.RandomNumberGenerator.Create();

        /*----------------------------------------------------------------------
        |   floating point
        |
        |   .NET Core 3.0+ formats and parses doubles exactly (like the C library
        |   that upstream uses). .NET Framework does not: ToString("F6") works
        |   from 15 significant digits, double.Parse is not always correctly
        |   rounded and reports an overflow instead of returning infinity. The
        |   functions below give the .NET 8 results on every runtime.
        +---------------------------------------------------------------------*/

        /// <summary>double.IsNegative (.NET Core 3.0+): true for negative values, -0.0 and negative NaNs.</summary>
        public static bool IsNegative(double value)
        {
            return BitConverter.DoubleToInt64Bits(value) < 0;
        }

        /// <summary>
        /// value.ToString("F" + precision, CultureInfo.InvariantCulture) as .NET 8
        /// does it: the exact decimal expansion of the value, rounded to
        /// <paramref name="precision"/> decimals (exact halfway cases to even),
        /// with a '-' sign for every negative value including -0.0.
        /// Only for finite values.
        /// </summary>
        public static string FormatFixed(double value, int precision)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException("value");
            if (precision < 0) throw new ArgumentOutOfRangeException("precision");

            // value = mantissa * 2^exponent
            long  bits            = BitConverter.DoubleToInt64Bits(value);
            bool  negative        = bits < 0;
            int   biased_exponent = (int)((bits >> 52) & 0x7FF);
            ulong mantissa        = (ulong)bits & 0x000FFFFFFFFFFFFFUL;
            int   exponent;
            if (biased_exponent == 0) {
                exponent = -1074;
            } else {
                mantissa |= 0x0010000000000000UL;
                exponent = biased_exponent - 1075;
            }

            // n = round(mantissa * 10^precision * 2^exponent)
            BigNat n = new BigNat(mantissa);
            for (int i = 0; i < precision; i++) n.Multiply(10);
            if (exponent >= 0) {
                n.ShiftLeft(exponent);
            } else {
                bool half = n.GetBit(-exponent - 1);
                bool rest = n.HasBitsBelow(-exponent - 1);
                n.ShiftRight(-exponent);
                if (half && (rest || n.GetBit(0))) n.Add(1);
            }

            string digits = n.ToString();
            System.Text.StringBuilder text = new System.Text.StringBuilder(digits.Length + precision + 3);
            if (negative) text.Append('-');
            if (precision == 0) {
                text.Append(digits);
            } else {
                if (digits.Length <= precision) {
                    text.Append("0.");
                    text.Append('0', precision - digits.Length);
                    text.Append(digits);
                } else {
                    text.Append(digits, 0, digits.Length - precision);
                    text.Append('.');
                    text.Append(digits, digits.Length - precision, precision);
                }
            }
            return text.ToString();
        }

        /// <summary>Math.ScaleB (.NET Core 3.0+): x * 2^n, rounded once.</summary>
        public static double ScaleB(double x, int n)
        {
            // (the algorithm of the scalbn of musl: the intermediate products are
            // exact, only the last multiplication can round)
            double two_1023       = BitConverter.Int64BitsToDouble(0x7FEL << 52);            // 2^1023
            double two_minus_969  = BitConverter.Int64BitsToDouble((long)(0x3FF - 969) << 52); // 2^-1022 * 2^53
            if (n > 1023) {
                x *= two_1023;
                n -= 1023;
                if (n > 1023) {
                    x *= two_1023;
                    n -= 1023;
                    if (n > 1023) n = 1023;
                }
            } else if (n < -1022) {
                x *= two_minus_969;
                n += 1022 - 53;
                if (n < -1022) {
                    x *= two_minus_969;
                    n += 1022 - 53;
                    if (n < -1022) n = -1022;
                }
            }
            return x * BitConverter.Int64BitsToDouble((long)(0x3FF + n) << 52);
        }

        /// <summary>
        /// double.TryParse(text, AllowLeadingSign | AllowDecimalPoint | AllowExponent,
        /// CultureInfo.InvariantCulture, out value) as .NET 8 does it: the nearest
        /// double (halfway cases to even), infinity on overflow, a signed zero on
        /// underflow. <paramref name="text"/> must be
        /// [+-] digits [. digits] [(e|E) [+-] digits] with at least one mantissa
        /// digit; false is returned otherwise.
        /// </summary>
        public static bool TryParseDouble(string text, out double value)
        {
            value = 0.0;
            if (text == null) return false;
            int i = 0;
            bool negative = false;
            if (i < text.Length && (text[i] == '+' || text[i] == '-')) {
                negative = text[i] == '-';
                ++i;
            }

            // mantissa digits -> a big integer and a decimal exponent
            BigNat mantissa = new BigNat(0);
            int  digit_count = 0;         // mantissa digits seen
            long decimal_exponent = 0;
            bool after_point = false;
            for (; i < text.Length; i++) {
                char c = text[i];
                if (c == '.' && !after_point) {
                    after_point = true;
                    continue;
                }
                if (c < '0' || c > '9') break;
                ++digit_count;
                mantissa.Multiply(10);
                mantissa.Add((uint)(c - '0'));
                if (after_point) --decimal_exponent;
            }
            if (digit_count == 0) return false;
            if (i < text.Length && (text[i] == 'e' || text[i] == 'E')) {
                ++i;
                bool negative_exponent = false;
                if (i < text.Length && (text[i] == '+' || text[i] == '-')) {
                    negative_exponent = text[i] == '-';
                    ++i;
                }
                if (i >= text.Length) return false;
                long e = 0;
                for (; i < text.Length; i++) {
                    char c = text[i];
                    if (c < '0' || c > '9') return false;
                    if (e < 100000000) e = e*10 + (c - '0');
                }
                decimal_exponent += negative_exponent ? -e : e;
            }
            if (i != text.Length) return false;

            if (mantissa.IsZero) {
                value = negative ? -0.0 : 0.0;
                return true;
            }

            // quick answers for exponents that are far out of range (this also
            // bounds the size of the numbers below)
            long magnitude = mantissa.ToString().Length + decimal_exponent; // value < 10^magnitude
            if (magnitude > 310) {
                value = negative ? double.NegativeInfinity : double.PositiveInfinity;
                return true;
            }
            if (magnitude < -330) {
                value = negative ? -0.0 : 0.0;
                return true;
            }

            // value = numerator / denominator
            BigNat numerator = mantissa;
            BigNat denominator = new BigNat(1);
            for (long x = 0; x < decimal_exponent; x++) numerator.Multiply(10);
            for (long x = 0; x > decimal_exponent; x--) denominator.Multiply(10);

            // scale so that the quotient has 65 to 67 significant bits:
            // quotient = floor(value * 2^shift)
            int shift = 66 - (numerator.BitLength - denominator.BitLength);
            if (shift >= 0) numerator.ShiftLeft(shift); else denominator.ShiftLeft(-shift);
            ulong quotient_high = 0; // bits 64..
            ulong quotient = 0;
            for (int bit = 68; bit >= 0; bit--) {
                BigNat t = denominator.Clone();
                t.ShiftLeft(bit);
                if (BigNat.Compare(numerator, t) >= 0) {
                    numerator.Subtract(t);
                    if (bit >= 64) quotient_high |= 1UL << (bit - 64); else quotient |= 1UL << bit;
                }
            }
            bool sticky = !numerator.IsZero;

            // reduce to a 64 bit quotient (its top bit is then set)
            while (quotient_high != 0) {
                if ((quotient & 1) != 0) sticky = true;
                quotient = (quotient >> 1) | (quotient_high << 63);
                quotient_high >>= 1;
                --shift;
            }
            int quotient_bits = 64;
            while ((quotient >> (quotient_bits - 1)) == 0) --quotient_bits;

            // keep 53 bits (fewer for subnormal values), rounding half to even
            int msb_exponent = quotient_bits - 1 - shift;           // 2^msb_exponent <= value
            int keep = 53;
            if (msb_exponent < -1022) keep = 53 - (-1022 - msb_exponent);
            int drop = quotient_bits - keep;
            ulong kept;
            if (drop <= 0) {
                kept = quotient;        // exact
                drop = 0;
            } else if (drop > 64) {
                kept = 0;               // less than half of the smallest subnormal
            } else {
                bool half;
                bool rest;
                if (drop == 64) {
                    kept = 0;
                    half = (quotient >> 63) != 0;
                    rest = (quotient & 0x7FFFFFFFFFFFFFFFUL) != 0 || sticky;
                } else {
                    kept = quotient >> drop;
                    half = ((quotient >> (drop - 1)) & 1) != 0;
                    rest = (quotient & ((1UL << (drop - 1)) - 1)) != 0 || sticky;
                }
                if (half && (rest || (kept & 1) != 0)) ++kept;
            }

            // kept <= 2^53 and kept * 2^(drop - shift) is a double (or overflows)
            double result = ScaleB((double)kept, drop - shift);
            value = negative ? -result : result;
            return true;
        }

        /// <summary>A non negative big integer, just enough for the conversions above.</summary>
        private sealed class BigNat
        {
            private uint[] _words = new uint[4]; // little endian
            private int    _count;               // words in use (no leading zero word)

            public BigNat(ulong value)
            {
                _words[0] = (uint)value;
                _words[1] = (uint)(value >> 32);
                _count = _words[1] != 0 ? 2 : (_words[0] != 0 ? 1 : 0);
            }

            public BigNat Clone()
            {
                BigNat copy = new BigNat(0);
                copy._words = new uint[_words.Length];
                Array.Copy(_words, 0, copy._words, 0, _count);
                copy._count = _count;
                return copy;
            }

            public bool IsZero { get { return _count == 0; } }

            public int BitLength
            {
                get {
                    if (_count == 0) return 0;
                    int bits = 32;
                    uint top = _words[_count - 1];
                    while ((top >> (bits - 1)) == 0) --bits;
                    return (_count - 1)*32 + bits;
                }
            }

            private void Reserve(int count)
            {
                if (_words.Length < count) {
                    uint[] words = new uint[Math.Max(count, _words.Length*2)];
                    Array.Copy(_words, 0, words, 0, _count);
                    _words = words;
                }
            }

            private void Trim()
            {
                while (_count > 0 && _words[_count - 1] == 0) --_count;
            }

            public void Multiply(uint factor)
            {
                ulong carry = 0;
                for (int i = 0; i < _count; i++) {
                    ulong product = (ulong)_words[i]*factor + carry;
                    _words[i] = (uint)product;
                    carry = product >> 32;
                }
                if (carry != 0) {
                    Reserve(_count + 1);
                    _words[_count++] = (uint)carry;
                }
                Trim();
            }

            public void Add(uint addend)
            {
                ulong carry = addend;
                for (int i = 0; i < _count && carry != 0; i++) {
                    ulong sum = (ulong)_words[i] + carry;
                    _words[i] = (uint)sum;
                    carry = sum >> 32;
                }
                if (carry != 0) {
                    Reserve(_count + 1);
                    _words[_count++] = (uint)carry;
                }
            }

            /// <summary>this -= other (other must not be larger than this).</summary>
            public void Subtract(BigNat other)
            {
                long borrow = 0;
                for (int i = 0; i < _count; i++) {
                    long difference = (long)_words[i] - (i < other._count ? (long)other._words[i] : 0) - borrow;
                    if (difference < 0) {
                        difference += 0x100000000L;
                        borrow = 1;
                    } else {
                        borrow = 0;
                    }
                    _words[i] = (uint)difference;
                }
                if (borrow != 0) throw new InvalidOperationException("BigNat.Subtract: negative result");
                Trim();
            }

            public static int Compare(BigNat a, BigNat b)
            {
                if (a._count != b._count) return a._count < b._count ? -1 : 1;
                for (int i = a._count - 1; i >= 0; i--) {
                    if (a._words[i] != b._words[i]) return a._words[i] < b._words[i] ? -1 : 1;
                }
                return 0;
            }

            public void ShiftLeft(int bits)
            {
                if (_count == 0 || bits == 0) return;
                int word_shift = bits/32;
                int bit_shift  = bits%32;
                int new_count  = _count + word_shift + 1;
                uint[] words = new uint[new_count];
                for (int i = 0; i < _count; i++) {
                    ulong shifted = (ulong)_words[i] << bit_shift;
                    words[i + word_shift]     |= (uint)shifted;
                    words[i + word_shift + 1] |= (uint)(shifted >> 32);
                }
                _words = words;
                _count = new_count;
                Trim();
            }

            public void ShiftRight(int bits)
            {
                int word_shift = bits/32;
                int bit_shift  = bits%32;
                if (word_shift >= _count) {
                    _count = 0;
                    return;
                }
                int new_count = _count - word_shift;
                for (int i = 0; i < new_count; i++) {
                    ulong pair = _words[i + word_shift];
                    if (i + word_shift + 1 < _count) pair |= (ulong)_words[i + word_shift + 1] << 32;
                    _words[i] = (uint)(pair >> bit_shift);
                }
                _count = new_count;
                Trim();
            }

            public bool GetBit(int bit)
            {
                int word = bit/32;
                if (word >= _count) return false;
                return ((_words[word] >> (bit%32)) & 1) != 0;
            }

            /// <summary>True when one of the bits 0..bit-1 is set.</summary>
            public bool HasBitsBelow(int bit)
            {
                int word = bit/32;
                for (int i = 0; i < word && i < _count; i++) {
                    if (_words[i] != 0) return true;
                }
                if (word >= _count || bit%32 == 0) return false;
                return (_words[word] & ((1u << (bit%32)) - 1)) != 0;
            }

            /// <summary>Decimal digits ("0" for zero).</summary>
            public override string ToString()
            {
                if (_count == 0) return "0";
                uint[] words = new uint[_count];
                Array.Copy(_words, 0, words, 0, _count);
                int count = _count;
                List<uint> groups = new List<uint>(); // base 10^9, little endian
                while (count > 0) {
                    ulong remainder = 0;
                    for (int i = count - 1; i >= 0; i--) {
                        ulong dividend = (remainder << 32) | words[i];
                        words[i] = (uint)(dividend/1000000000UL);
                        remainder = dividend%1000000000UL;
                    }
                    groups.Add((uint)remainder);
                    while (count > 0 && words[count - 1] == 0) --count;
                }
                System.Text.StringBuilder text = new System.Text.StringBuilder(groups.Count*9);
                text.Append(groups[groups.Count - 1].ToString(System.Globalization.CultureInfo.InvariantCulture));
                for (int i = groups.Count - 2; i >= 0; i--) {
                    text.Append(groups[i].ToString("D9", System.Globalization.CultureInfo.InvariantCulture));
                }
                return text.ToString();
            }
        }

#if NET20
        /// <summary>
        /// DateTimeOffset.UtcNow.ToUnixTimeSeconds() (.NET Framework 4.6+), from
        /// DateTime: DateTimeOffset does not exist in .NET Framework 2.0 (it came
        /// with 2.0 SP1).
        /// </summary>
        public static long UtcNowUnixTimeSeconds()
        {
            // 62135596800 = seconds from 0001-01-01 to 1970-01-01
            long seconds = DateTime.UtcNow.Ticks/TimeSpan.TicksPerSecond;
            return seconds - 62135596800L;
        }
#endif

#if NETSTANDARD1_3
        /// <summary>Array.Copy with 64 bit offsets and length (not in .NET Standard 1.3).</summary>
        public static void ArrayCopy(Array source, long source_offset, Array destination, long destination_offset, long length)
        {
            if (source_offset < 0 || source_offset > int.MaxValue) throw new ArgumentOutOfRangeException("source_offset");
            if (destination_offset < 0 || destination_offset > int.MaxValue) throw new ArgumentOutOfRangeException("destination_offset");
            if (length < 0 || length > int.MaxValue) throw new ArgumentOutOfRangeException("length");
            Array.Copy(source, (int)source_offset, destination, (int)destination_offset, (int)length);
        }
#endif
    }

}
#endif
