// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Pssh/Mp4Pssh.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - upstream never closes its files on the early `return 1` paths (the C
//   runtime does it at exit); here every stream that was opened is disposed
//   when Main returns.

using System;
using System.Collections.Generic;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Pssh
{

    /*----------------------------------------------------------------------
    |   AP4_EditingProcessor
    +---------------------------------------------------------------------*/
    internal class EditingProcessor : Processor
    {
        // types
        public class Command {
            // types
            public enum Type {
                TYPE_INSERT,
                TYPE_REMOVE,
                TYPE_REPLACE
            }

            // constructor
            public Command(Type        type,
                           byte[]      system_id,
                           DataBuffer  pssh_data)
            {
                _type = type;
                Array.Copy(system_id, 0, _systemId, 0, 16);
                _pssh = new PsshAtom(system_id);
                if (pssh_data != null) {
                    _pssh.SetData(pssh_data.GetData(), pssh_data.GetDataSize());
                }
            }

            // members
            public Type     _type;
            public byte[]   _systemId = new byte[16];
            public PsshAtom _pssh;
        }

        /*----------------------------------------------------------------------
        |   FindPssh
        +---------------------------------------------------------------------*/
        private static PsshAtom FindPssh(byte[] system_id, AtomParent top_level)
        {
            ContainerAtom moov = top_level.GetChild(AtomType.MOOV) as ContainerAtom;
            if (moov == null) {
                return null;
            }
            foreach (Atom atom in moov.GetChildren()) {
                if (atom.GetType() == AtomType.PSSH) {
                    PsshAtom pssh = atom as PsshAtom;
#if NET6_0_OR_GREATER
                    if (pssh != null && new ReadOnlySpan<byte>(pssh.GetSystemId(), 0, 16).SequenceEqual(new ReadOnlySpan<byte>(system_id, 0, 16))) {
#else
                    if (pssh != null && Compat.SequenceEqual(pssh.GetSystemId(), 0, system_id, 0, 16)) {
#endif
                        return pssh;
                    }
                }
            }

            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::AddCommand
        +---------------------------------------------------------------------*/
        public Ap4Result AddCommand(Command.Type type,
                                    byte[]       system_id,
                                    DataBuffer   pssh_data)
        {
            _commands.Add(new Command(type, system_id, pssh_data));
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::Initialize
        +---------------------------------------------------------------------*/
        public override Ap4Result Initialize(AtomParent        top_level,
                                             ByteStream        stream,
                                             ProgressListener  listener = null)
        {
            Ap4Result result;

            foreach (Command command in _commands) {
                switch (command._type) {
                  case Command.Type.TYPE_REMOVE:
                    result = DoRemove(command, top_level);
                    if (result.Failed()) return result;
                    break;

                  case Command.Type.TYPE_INSERT:
                    result = DoInsert(command, top_level);
                    if (result.Failed()) return result;
                    break;

                  case Command.Type.TYPE_REPLACE:
                    result = DoReplace(command, top_level);
                    if (result.Failed()) return result;
                    break;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::DoRemove
        +---------------------------------------------------------------------*/
        private Ap4Result DoRemove(Command command, AtomParent top_level)
        {
            PsshAtom pssh = FindPssh(command._systemId, top_level);
            if (pssh == null) {
                Program.Eprintf("ERROR: no 'pssh' box with the requested ID was found\n");
                return Ap4Result.FAILURE;
            }

            pssh.Detach();
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::DoInsert
        +---------------------------------------------------------------------*/
        private Ap4Result DoInsert(Command command, AtomParent top_level)
        {
            ContainerAtom moov = top_level.GetChild(AtomType.MOOV) as ContainerAtom;

            // check that we have a place to insert into
            if (moov == null) {
                Program.Eprintf("ERROR: no 'moov' atom found\n");
                return Ap4Result.FAILURE;
            }

            return moov.AddChild(command._pssh);
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::DoReplace
        +---------------------------------------------------------------------*/
        private Ap4Result DoReplace(Command command, AtomParent top_level)
        {
            ContainerAtom moov = top_level.GetChild(AtomType.MOOV) as ContainerAtom;

            // check that we have a place to insert into
            if (moov == null) {
                Program.Eprintf("ERROR: no 'moov' atom found\n");
                return Ap4Result.FAILURE;
            }

            // remove the existing one
            PsshAtom  pssh = FindPssh(command._systemId, top_level);
            if (pssh != null) {
                pssh.Detach();
            }

            // insert the new one
            return moov.AddChild(command._pssh);
        }

        // members
        private List<Command> _commands = new List<Command>();
        private AtomParent    _topLevelParent = new AtomParent();
    }

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 PSSH Editor - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2014 Axiomatic Systems, LLC";

        private const uint MP4_PSSH_MAX_DATA_SIZE = 8*1024*1024; // 8MB

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: fprintf(stderr) replacement)
        +---------------------------------------------------------------------*/
        private static readonly List<ByteStream> s_OpenStreams = new List<ByteStream>();

        // PORT NOTE: fprintf(stderr) writes the raw bytes of the text (file names
        // given on the command line included), not the console encoding.
        private static System.IO.Stream s_Stderr;

        internal static void Eprintf(string text)
        {
            if (s_Stderr == null) s_Stderr = Console.OpenStandardError();
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stderr.Write(bytes, 0, bytes.Length);
            s_Stderr.Flush();
        }

        private static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(Ap4Result value) { return D((int)value); }

        // AP4_FileByteStream::Create + bookkeeping (see the PORT NOTES above)
        private static Ap4Result CreateFileByteStream(string name, FileByteStream.Mode mode, out ByteStream stream)
        {
            Ap4Result result = FileByteStream.Create(name, mode, out stream);
            if (result.Succeeded() && stream != null) s_OpenStreams.Add(stream);
            return result;
        }

        // stream->Release()
        private static void Release(ByteStream stream)
        {
            if (stream == null) return;
            s_OpenStreams.Remove(stream);
            if (stream is IDisposable) ((IDisposable)stream).Dispose();
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(
                    BANNER +
                    "\n\nusage: mp4pssh [commands] <input> <output>\n" +
                    "    where commands include one or more of:\n" +
                    "    --insert <system-id-hex>:<pssh-data-file>\n" +
                    "    --remove <system-id-hex>\n" +
                    "    --replace <system-id-hex>:<pssh-data-file>\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   LoadFile
        +---------------------------------------------------------------------*/
        private static Ap4Result LoadFile(string filename, DataBuffer buffer)
        {
            // create the input stream
            ByteStream input = null;
            Ap4Result result = CreateFileByteStream(filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file (" + filename + ")\n");
                return result;
            }
            ulong file_size = 0;
            result = input.GetSize(out file_size);
            if (result.Failed()) {
                Eprintf("ERROR: cannot get file size (" + D(result) + ")\n");
                Release(input);
                return result;
            }
            if (file_size > MP4_PSSH_MAX_DATA_SIZE) {
                Eprintf("ERROR: data file size too large\n");
                Release(input);
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }
            buffer.SetDataSize((uint)file_size);

            result = input.Read(buffer.UseData(), (uint)file_size);
            if (result.Failed()) {
                Eprintf("ERROR: failed to read data from file (" + D(result) + ")\n");
                Release(input);
                return result;
            }

            Release(input);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            int exit_code;
            try {
                exit_code = Run(args);
            } finally {
                foreach (ByteStream stream in s_OpenStreams.ToArray()) {
                    Release(stream);
                }
            }
            return exit_code;
        }

        private static int Run(string[] args)
        {
            if (args.Length+1 < 3) {
                return PrintUsageAndExit();
            }

            // create initial objects
            EditingProcessor processor = new EditingProcessor();

            // parse arguments
            string input_filename = null;
            string output_filename = null;
            int argi = 0; // PORT NOTE: index of the current argument (`argv` walk upstream)
            string arg;
            Ap4Result result;
            while ((arg = argi < args.Length ? args[argi++] : null) != null) {
                if (arg == "--insert") {
                    string param = argi < args.Length ? args[argi++] : null;
                    if (param == null) {
                        Eprintf("ERROR: missing argument for --insert command\n");
                        return 1;
                    }
                    string system_id_hex;
                    string file_path;
                    if (Ap4Utils.SplitArgs(param, out system_id_hex, out file_path).Succeeded()) {
                        if (file_path.Length == 0 || file_path[0] == '\0') {
                            // empty file path
                            Eprintf("ERROR: missing file path in --insert argument\n");
                            return 1;
                        }
                        DataBuffer pssh_data = new DataBuffer();
                        byte[]     system_id = new byte[16];
                        result = LoadFile(file_path, pssh_data);
                        if (result.Failed()) return 1;
                        result = Ap4Utils.ParseHex(system_id_hex, system_id, 16);
                        if (result.Failed()) {
                            Eprintf("ERROR: invalid system ID (" + system_id_hex + ")\n");
                            return 1;
                        }
                        processor.AddCommand(EditingProcessor.Command.Type.TYPE_INSERT, system_id, pssh_data);
                    } else {
                        Eprintf("ERROR: invalid format for --insert command argument\n");
                        return 1;
                    }
                } else if (arg == "--remove") {
                    string system_id_hex = argi < args.Length ? args[argi++] : null;
                    if (system_id_hex == null) {
                        Eprintf("ERROR: missing argument for --remove command\n");
                        return 1;
                    }
                    byte[] system_id = new byte[16];
                    result = Ap4Utils.ParseHex(system_id_hex, system_id, 16);
                    if (result.Failed()) {
                        Eprintf("ERROR: invalid system ID (" + system_id_hex + ")\n");
                        return 1;
                    }
                    processor.AddCommand(EditingProcessor.Command.Type.TYPE_REMOVE, system_id, null);
                } else if (arg == "--replace") {
                    string param = argi < args.Length ? args[argi++] : null;
                    if (param == null) {
                        Eprintf("ERROR: missing argument for --replace command\n");
                        return 1;
                    }
                    string system_id_hex;
                    string file_path;
                    if (Ap4Utils.SplitArgs(param, out system_id_hex, out file_path).Succeeded()) {
                        if (file_path.Length == 0 || file_path[0] == '\0') {
                            // empty file path
                            Eprintf("ERROR: missing file path in --insert argument\n");
                            return 1;
                        }
                        DataBuffer pssh_data = new DataBuffer();
                        byte[]     system_id = new byte[16];
                        result = LoadFile(file_path, pssh_data);
                        if (result.Failed()) return 1;
                        result = Ap4Utils.ParseHex(system_id_hex, system_id, 16);
                        if (result.Failed()) {
                            Eprintf("ERROR: invalid system ID (" + system_id_hex + ")\n");
                            return 1;
                        }
                        processor.AddCommand(EditingProcessor.Command.Type.TYPE_REPLACE, system_id, pssh_data);
                    } else {
                        Eprintf("ERROR: invalid format for --replace command argument\n");
                        return 1;
                    }
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
            if (input_filename == null) {
                Eprintf("ERROR: missing input filename\n");
                return 1;
            }
            if (output_filename == null) {
                Eprintf("ERROR: missing output filename\n");
                return 1;
            }

            // create the input stream
            ByteStream input = null;
            result = CreateFileByteStream(input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file (" + input_filename + ")\n");
                return 1;
            }

            // create the output stream
            ByteStream output = null;
            result = CreateFileByteStream(output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file (" + output_filename + ")\n");
                Release(input);
                return 1;
            }

            // process!
            processor.Process(input, output);

            // cleanup
            Release(input);
            Release(output);

            return 0;
        }
    }
}
