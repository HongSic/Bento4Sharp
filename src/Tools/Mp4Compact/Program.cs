// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Compact/Mp4Compact.cpp
// Original work Copyright 2002-2011 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream and
//   fprintf(stderr) through the raw standard error stream, so that the bytes
//   written are exactly those of upstream ('\n' line endings, no encoding
//   translation).
// - the AP4_CompactingProcessor destructor (which prints the total) is
//   ported as IDisposable.Dispose(), called where upstream deletes the object.

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Compact
{

    /*----------------------------------------------------------------------
    |   AP4_CompactingProcessor
    +---------------------------------------------------------------------*/
    internal class CompactingProcessor : Processor, IDisposable
    {
        //  inner classes
        public new class TrackHandler : Processor.TrackHandler
        {
            public TrackHandler(CompactingProcessor outer, TrakAtom trak_atom)
            {
                _outer = outer;
                _trakAtom = trak_atom;
                _stszAtom = null;
            }

            /*----------------------------------------------------------------------
            |   AP4_CompactingProcessor::TrackHandler::ProcessTrack
            +---------------------------------------------------------------------*/
            public override Ap4Result ProcessTrack()
            {
                // find the stsz atom
                ContainerAtom stbl = _trakAtom.FindChild("mdia/minf/stbl") as ContainerAtom;
                if (stbl == null) return Ap4Result.SUCCESS;
                StszAtom stsz = stbl.GetChild(AtomType.STSZ) as StszAtom;
                if (stsz == null) return Ap4Result.SUCCESS;

                // check if we can reduce the size of stsz by changing it to stz2
                uint max_size = 0;
                for (uint i=1; i<=stsz.GetSampleCount(); i++) {
                    uint sample_size;
                    stsz.GetSampleSize(i, out sample_size);
                    if (sample_size > max_size) {
                        max_size = sample_size;
                    }
                }
                byte field_size = 0;
                if (max_size <= 0xFF) {
                    field_size = 1;
                } else if (max_size <= 0xFFFF) {
                    field_size = 2;
                }
                if (_outer._verbose) Program.Printf("Track " + Program.D(_trakAtom.GetId()) + ": ");
                if (field_size == 0) {
                    if (_outer._verbose) {
                        Program.Printf("no stz2 reduction possible\n");
                    }
                    return Ap4Result.SUCCESS;
                } else {
                    if (_outer._verbose) {
                        uint reduction = unchecked((uint)(4-field_size)*stsz.GetSampleCount());
                        Program.Printf("stz2 reduction = " + Program.D(reduction) + " bytes\n");
                        _outer._sizeReduction += reduction;
                    }
                }

                // detach the original stsz atom so we can destroy it later
                _stszAtom = stsz;
                stsz.Detach();

                // create an stz2 atom and populate its entries
                Stz2Atom stz2 = new Stz2Atom(field_size);
                for (uint i=1; i<=_stszAtom.GetSampleCount(); i++) {
                    uint sample_size;
                    _stszAtom.GetSampleSize(i, out sample_size);
                    stz2.AddEntry(sample_size);
                }
                stbl.AddChild(stz2);

                return Ap4Result.SUCCESS;
            }

            public override Ap4Result ProcessSample(DataBuffer data_in,
                                                    DataBuffer data_out) {
                return data_out.SetData(data_in.GetData(), data_in.GetDataSize());
            }

            private CompactingProcessor _outer;
            private TrakAtom            _trakAtom;
            private StszAtom            _stszAtom;
        }

        // methods
        public CompactingProcessor(bool verbose) { _verbose = verbose; _sizeReduction = 0; }

        /*----------------------------------------------------------------------
        |   AP4_CompactingProcessor::~AP4_CompactingProcessor
        +---------------------------------------------------------------------*/
        public void Dispose()
        {
            if (_verbose) {
                Program.Printf("Total stz2 reduction = " + Program.D(_sizeReduction) + " bytes\n");
            }
        }

        public override Processor.TrackHandler CreateTrackHandler(TrakAtom trak_atom) {
            return new TrackHandler(this, trak_atom);
        }

        // members
        public bool _verbose;
        public uint _sizeReduction;
    }

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Compacter - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2011 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static ByteStream s_Stdout;
        private static System.IO.Stream s_Stderr;

        internal static void Printf(string text)
        {
            if (s_Stdout == null) {
                FileByteStream.Create("-stdout", FileByteStream.Mode.STREAM_MODE_WRITE, out s_Stdout);
            }
            if (s_Stdout != null) s_Stdout.WriteString(text);
        }

        private static void FlushStdout()
        {
            if (s_Stdout is IDisposable) ((IDisposable)s_Stdout).Dispose();
            s_Stdout = null;
        }

        private static void Eprintf(string text)
        {
            if (s_Stderr == null) s_Stderr = Console.OpenStandardError();
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stderr.Write(bytes, 0, bytes.Length);
            s_Stderr.Flush();
        }

        internal static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        internal static string D(uint value) { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value

        // printf("%s") of a possibly NULL string
        private static string S(string value) { return value ?? "(null)"; }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\n" +
                    "usage: mp4compact [options] <input> <output>\n" +
                    "Options:\n" +
                    "  --verbose\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            int exit_code = Run(args);
            FlushStdout();
            return exit_code;
        }

        private static int Run(string[] args)
        {
            if (args.Length == 0) return PrintUsageAndExit();

            // parse options
            string    input_filename  = null;
            string    output_filename = null;
            bool      verbose         = false;
            Ap4Result result;

            // parse the command line arguments
            for (int argi = 0; argi < args.Length; argi++) {
                string arg = args[argi];
                if (arg == "--verbose") {
                    verbose = true;
                } else if (input_filename == null) {
                    input_filename = arg;
                } else if (output_filename == null) {
                    output_filename = arg;
                } else {
                    Eprintf("ERROR: unexpected argument (" + arg + ")\n");
                    return 1;
                }
            }

            // create the input stream
            ByteStream input = null;
            result = FileByteStream.Create(input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file (" + S(input_filename) + ")\n");
                return 1;
            }

            // create the output stream
            ByteStream output = null;
            result = FileByteStream.Create(output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file (" + S(output_filename) + ")\n");
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return 1;
            }

            // process the file
            CompactingProcessor processor = new CompactingProcessor(verbose);
            result = processor.Process(input, output, (Processor.ProgressListener)null);
            if (result.Failed()) {
                Eprintf("ERROR: failed to process the file (" + D((int)result) + ")\n");
            }

            // cleanup
            processor.Dispose();
            if (input is IDisposable) ((IDisposable)input).Dispose();
            if (output is IDisposable) ((IDisposable)output).Dispose();

            return 0;
        }
    }
}
