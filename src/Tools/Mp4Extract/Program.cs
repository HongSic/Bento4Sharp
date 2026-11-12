// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Extract/Mp4Extract.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - fprintf(stderr) output is written as raw bytes ('\n' line endings, no
//   encoding translation).

using System;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Extract
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Atom Extractor - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2008 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: fprintf(stderr) replacement)
        +---------------------------------------------------------------------*/
        private static System.IO.Stream s_Stderr;

        private static void Eprintf(string text)
        {
            if (s_Stderr == null) s_Stderr = Console.OpenStandardError();
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stderr.Write(bytes, 0, bytes.Length);
            s_Stderr.Flush();
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp4extract [options] <atom_path> <input> <output>\n" +
                    "    options:\n" +
                    "    --payload-only : omit the atom header\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            if (args.Length < 3) {
                return PrintUsageAndExit();
            }

            // parse arguments
            string atom_path       = null;
            string input_filename  = null;
            string  output_filename = null;
            bool    payload_only    = false;
            for (int argi = 0; argi < args.Length; argi++) {
                string arg = args[argi];
                if (arg == "--payload-only") {
                    payload_only = true;
                } else if (atom_path == null) {
                    atom_path = arg;
                } else if (input_filename == null) {
                    input_filename = arg;
                } else if (output_filename == null) {
                    output_filename = arg;
                } else {
                    Eprintf("ERROR: invalid command line argument (" + arg + ")\n");
                    return 1;
                }
            }

            // check arguments
            if (atom_path == null) {
                Eprintf("ERROR: missing atom path\n");
                return 1;
            }
            if (input_filename == null) {
                Eprintf("ERROR: missing input filename\n");
                return 1;
            }
            if (output_filename == null) {
                Eprintf("ERROR: missing output filename\n");
                return 1;
            }

            // create the input stream
            Ap4Result result;
            ByteStream input = null;
            result = FileByteStream.Create(input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file (" + input_filename + ")\n");
                return 1;
            }

            // parse the atoms
            AtomParent top_level = new AtomParent();
            Atom atom;
            DefaultAtomFactory atom_factory = new DefaultAtomFactory();
            while (atom_factory.CreateAtomFromStream(input, out atom) == Ap4Result.SUCCESS) {
                top_level.AddChild(atom);
            }

            // release the input
            // PORT NOTE: upstream releases its reference here, but the atoms that
            // were parsed keep their own references to the stream (the payload of
            // unknown atoms is read when the atom is written), so the file is only
            // closed after the atom has been written.

            // find the atom
            atom = top_level.FindChild(atom_path);
            if (atom == null) {
                Eprintf("ERROR: atom '" + atom_path + "' not found\n");
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return 1;
            }

            // create the output stream
            ByteStream output = null;
            result = FileByteStream.Create(output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file (" + output_filename + ")\n");
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return 1;
            }

            // write the atom
            if (payload_only) {
                atom.WriteFields(output);
            } else {
                atom.Write(output);
            }

            // cleanup
            if (output is IDisposable) ((IDisposable)output).Dispose();
            if (input is IDisposable) ((IDisposable)input).Dispose();

            return 0;
        }
    }
}
