// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Edit/Mp4Edit.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - fprintf(stderr) output is written as raw bytes ('\n' line endings, no
//   encoding translation).
// - upstream splits the command line arguments in place (AP4_SplitArgs
//   writes NUL bytes in the argv strings); the port works on string copies.

using System;
using System.Collections.Generic;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Edit
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
            public Command(Type type, string atom_path,
                                      string atom_source,
                                      int    position = -1)
            {
                _type = type;
                _atomPath = atom_path;
                _atomSource = atom_source;
                _position = position;
            }

            // members
            public Type   _type;
            public string _atomPath;
            public string _atomSource;
            public int    _position;
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::AddCommand
        +---------------------------------------------------------------------*/
        public Ap4Result
        AddCommand(Command.Type type,
                   string       atom_path,
                   string       file_path,
                   int          position = -1)
        {
            _commands.Add(new Command(type, atom_path, file_path, position));
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::Initialize
        +---------------------------------------------------------------------*/
        public override Ap4Result
        Initialize(AtomParent        top_level,
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
        private Ap4Result
        DoRemove(Command command, AtomParent top_level)
        {
            Atom atom = top_level.FindChild(command._atomPath);
            if (atom == null) {
                Program.Eprintf("ERROR: atom '" + command._atomPath + "' not found\n");
                return Ap4Result.FAILURE;
            } else {
                atom.Detach();
                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::InsertAtom
        +---------------------------------------------------------------------*/
        private Ap4Result
        InsertAtom(string     atom_source,
                   AtomParent container,
                   int        position)
        {
            string file_path = atom_source;
            int separator = atom_source.IndexOf('#');
            byte[] atom_uuid = new byte[16];
            bool is_uuid = false;
            if (separator == 32) {
                // uuid
                Ap4Utils.ParseHex(atom_source, atom_uuid, 16);
                file_path = atom_source.Substring(separator + 1);
                is_uuid = true;
            }

            Atom child = null;
            if (is_uuid) {
                // open the payload
                ByteStream  payload = null;
                Ap4Result result = FileByteStream.Create(file_path, FileByteStream.Mode.STREAM_MODE_READ, out payload);
                if (result.Failed()) {
                    Program.Eprintf("ERROR: cannot open atom file (" + file_path + ")\n");
                    return result;
                }

                // check the size
                ulong payload_size = 0;
                result = payload.GetSize(out payload_size);
                if (result.Failed()) {
                    Program.Eprintf("ERROR: cannot get atom file size\n");
                    if (payload is IDisposable) ((IDisposable)payload).Dispose();
                    return result;
                }
                if (payload_size > Program.AP4_MP4EDIT_MAX_PAYLOAD_SIZE) {
                    Program.Eprintf("ERROR: atom payload too large\n");
                    if (payload is IDisposable) ((IDisposable)payload).Dispose();
                    return Ap4Result.ERROR_OUT_OF_RANGE;
                }

                // synthesize a uuid atom
                child = new UnknownUuidAtom(payload_size + Ap4.ATOM_HEADER_SIZE + 16, atom_uuid, payload);
                if (payload is IDisposable) ((IDisposable)payload).Dispose();
            } else {
                // read the atom to insert
                ByteStream  input = null;
                Ap4Result result = FileByteStream.Create(file_path, FileByteStream.Mode.STREAM_MODE_READ, out input);
                if (result.Failed()) {
                    Program.Eprintf("ERROR: cannot open atom file (" + file_path + ")\n");
                    return Ap4Result.FAILURE;
                }

                DefaultAtomFactory atom_factory = new DefaultAtomFactory();
                result = atom_factory.CreateAtomFromStream(input, out child);
                // PORT NOTE: upstream releases its reference to the stream here,
                // but the atom that was created may keep its own reference (the
                // payload of an unknown atom is read when the atom is written), so
                // the file stays open until the end of the process.
                _atomSources.Add(input);
                if (result.Failed()) {
                    Program.Eprintf("ERROR: failed to create atom\n");
                    return Ap4Result.FAILURE;
                }
            }

            // insert the atom
            {
                // PORT NOTE: upstream passes the NULL pointer along when the atom
                // source is empty (AP4_AtomParent::AddChild then crashes).
                Ap4Result result = container.AddChild(child, position);
                if (result.Failed()) {
                    Program.Eprintf("ERROR: failed to insert atom\n");
                    return result;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::DoInsert
        +---------------------------------------------------------------------*/
        private Ap4Result
        DoInsert(Command command, AtomParent top_level)
        {
            AtomParent parent = null;
            if (Ap4Utils.StringLength(command._atomPath) == 0) {
                // insert into the toplevel list
                parent = top_level;
            } else {
                // find the atom to insert into
                Atom atom = top_level.FindChild(command._atomPath, true);
                if (atom == null) {
                    Program.Eprintf("ERROR: atom '" + command._atomPath + "' not found\n");
                    return Ap4Result.FAILURE;
                }

                // check that the atom is a container
                parent = atom as ContainerAtom; // AP4_DYNAMIC_CAST(AP4_AtomParent, atom)
            }

            // check that we have a place to insert into
            if (parent == null) {
                Program.Eprintf("ERROR: atom '" + command._atomPath + "' is not a container\n");
                return Ap4Result.FAILURE;
            }

            return InsertAtom(command._atomSource, parent, command._position);
        }

        /*----------------------------------------------------------------------
        |   AP4_EditingProcessor::DoReplace
        +---------------------------------------------------------------------*/
        private Ap4Result
        DoReplace(Command command, AtomParent top_level)
        {
            // remove the atom
            Atom atom = top_level.FindChild(command._atomPath);
            if (atom == null) {
                Program.Eprintf("ERROR: atom '" + command._atomPath + "' not found\n");
                return Ap4Result.FAILURE;
            } else {
                // find the position of the atom in the parent
                AtomParent parent = atom.GetParent();
                int position = 0;
                foreach (Atom list_item in parent.GetChildren()) {
                    if (list_item == atom) break;
                    position++;
                }

                // remove the atom from the parent
                atom.Detach();

                // insert the replacement
                return InsertAtom(command._atomSource, parent, position);
            }
        }

        /// <summary>
        /// PORT NOTE: closes the atom source files kept open by InsertAtom.
        /// </summary>
        public void CloseAtomSources()
        {
            foreach (ByteStream stream in _atomSources) {
                if (stream is IDisposable) ((IDisposable)stream).Dispose();
            }
            _atomSources.Clear();
        }

        // members
        private List<Command>    _commands = new List<Command>();
        private List<ByteStream> _atomSources = new List<ByteStream>();
    }

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 File Editor - Version 1.2\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2017 Axiomatic Systems, LLC";

        internal const ulong AP4_MP4EDIT_MAX_PAYLOAD_SIZE = 0xFFFFFFFFUL;

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: fprintf(stderr) / strtoul replacements)
        +---------------------------------------------------------------------*/
        private static System.IO.Stream s_Stderr;

        internal static void Eprintf(string text)
        {
            if (s_Stderr == null) s_Stderr = Console.OpenStandardError();
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stderr.Write(bytes, 0, bytes.Length);
            s_Stderr.Flush();
        }

        // strtoul(str, NULL, 10)
        private static ulong Strtoul(string str)
        {
            int i = 0;
            while (i < str.Length && (str[i] == ' ' || (str[i] >= '\t' && str[i] <= '\r'))) ++i;
            bool negative = false;
            if (i < str.Length && (str[i] == '+' || str[i] == '-')) {
                negative = str[i] == '-';
                ++i;
            }
            ulong value = 0;
            bool overflow = false;
            while (i < str.Length && str[i] >= '0' && str[i] <= '9') {
                ulong digit = (ulong)(str[i]-'0');
                if (value > (ulong.MaxValue-digit)/10) {
                    overflow = true;
                } else {
                    value = value*10+digit;
                }
                ++i;
            }
            if (overflow) return ulong.MaxValue;
            return negative ? unchecked((ulong)(-(long)value)) : value;
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp4edit [commands] <input> <output>\n" +
                    "    where commands include one or more of:\n" +
                    "    --insert <atom_path>:<atom_source>[:<position>]\n" +
                    "    --remove <atom_path>\n" +
                    "    --replace <atom_path>:<atom_source>\n" +
                    "\n" +
                    "    and <atom_source> may be either a filename for a file\n" +
                    "    that contains the atom data (header and payload), or\n" +
                    "    <uuid>#<file> with <uuid> specifying an atom uuid type,\n" +
                    "    as a 32-character hex value, and <file> a file with the atom\n" +
                    "    payload only.\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            if (args.Length < 2) {
                return PrintUsageAndExit();
            }

            // create initial objects
            EditingProcessor processor = new EditingProcessor();

            // parse arguments
            string input_filename = null;
            string output_filename = null;
            int argi = 0;
            while (argi < args.Length) {
                string arg = args[argi++];
                if (arg == "--insert") {
                    string param = argi < args.Length ? args[argi++] : null;
                    if (param == null) {
                        Eprintf("ERROR: missing argument for --insert command\n");
                        return 1;
                    }
                    string atom_path;
                    string file_path;
                    if (Ap4Utils.SplitArgs(param, out atom_path, out file_path).Succeeded()) {
                        if (file_path.Length == 0 || file_path[0] == '\0') {
                            // empty file path
                            Eprintf("ERROR: missing file path in --insert argument\n");
                            return 1;
                        }
                        bool is_windows_path = false;
                        if (file_path.Length >= 3 &&
                            ((file_path[0] >= 'a' && file_path[0] <= 'z') ||
                             (file_path[0] >= 'A' && file_path[0] <= 'Z')) &&
                            file_path[1] == ':' && file_path[2] == '\\') {
                            is_windows_path = true;
                            file_path = file_path.Substring(0, 1) + "@" + file_path.Substring(2); // temporary replacement so that the next split works
                        }
                        int position = -1;
                        string position_str = null;
                        string split_file_path;
                        string split_position_str;
                        if (Ap4Utils.SplitArgs(file_path, out split_file_path, out split_position_str).Succeeded()) {
                            file_path = split_file_path;
                            position_str = split_position_str;
                            if (position_str != null) {
                                position = unchecked((int)Strtoul(position_str));
                            }
                        }
                        if (is_windows_path) {
                            file_path = file_path.Substring(0, 1) + ":" + file_path.Substring(2);
                        }
                        processor.AddCommand(EditingProcessor.Command.Type.TYPE_INSERT, atom_path, file_path, position);
                    } else {
                        Eprintf("ERROR: invalid format for --insert command argument\n");
                        return 1;
                    }
                } else if (arg == "--remove") {
                    string atom_path = argi < args.Length ? args[argi++] : null;
                    if (atom_path == null) {
                        Eprintf("ERROR: missing argument for --remove command\n");
                        return 1;
                    }
                    processor.AddCommand(EditingProcessor.Command.Type.TYPE_REMOVE, atom_path, "");
                } else if (arg == "--replace") {
                    string param = argi < args.Length ? args[argi++] : null;
                    if (param == null) {
                        Eprintf("ERROR: missing argument for --replace command\n");
                        return 1;
                    }
                    string atom_path;
                    string file_path;
                    if (Ap4Utils.SplitArgs(param, out atom_path, out file_path).Succeeded()) {
                        processor.AddCommand(EditingProcessor.Command.Type.TYPE_REPLACE, atom_path, file_path);
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
            Ap4Result result;
            ByteStream input = null;
            result = FileByteStream.Create(input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file (" + input_filename + ")\n");
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

            // process!
            processor.Process(input, output);

            // cleanup
            processor.CloseAtomSources();
            if (input is IDisposable) ((IDisposable)input).Dispose();
            if (output is IDisposable) ((IDisposable)output).Dispose();

            return 0;
        }
    }
}
