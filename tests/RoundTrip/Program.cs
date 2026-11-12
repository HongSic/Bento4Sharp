// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Test helper (no upstream equivalent): parses a file with Bento4.File and
// writes all its top-level atoms back out. The output must be byte-identical
// to the input (checked by tests/compare-with-reference.sh).
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using Bento4;

namespace Bento4Sharp.Tests.RoundTrip
{

    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length != 2) {
                Console.Error.Write("usage: roundtrip <input> <output>\n");
                return 1;
            }

            ByteStream input;
            Ap4Result result = FileByteStream.Create(args[0], FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed() || input == null) {
                Console.Error.Write("ERROR: cannot open input (" + (int)result + ")\n");
                return 1;
            }
            ByteStream output;
            result = FileByteStream.Create(args[1], FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed() || output == null) {
                Console.Error.Write("ERROR: cannot open output (" + (int)result + ")\n");
                return 1;
            }

            // the input stream must stay open until the atoms are written: some
            // atoms (mdat...) reference their payload in the source stream
            int exit_code = 0;
            File file = new File(input);
            foreach (Atom atom in file.GetTopLevelAtoms()) {
                result = atom.Write(output);
                if (result.Failed()) {
                    Console.Error.Write("ERROR: failed to write atom (" + (int)result + ")\n");
                    exit_code = 1;
                    break;
                }
            }

            if (output is IDisposable) ((IDisposable)output).Dispose();
            if (input is IDisposable) ((IDisposable)input).Dispose();
            return exit_code;
        }
    }
}
