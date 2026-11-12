// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Tag/Mp4Tag.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream so that
//   the bytes written are exactly those of upstream ('\n' line endings, no
//   encoding translation).
// - exit(1) inside PrintUsageAndExit() is ported as an ExitException caught
//   in Main (so that stdout is flushed and files are closed, like the C
//   runtime does at exit).
// - strtol()/strtoul() work on a 32-bit `long`, as in the reference build of
//   the original tool (see Strtol below); the library keeps the value in a
//   64-bit C# `long` (AP4_IntegerMetaDataValue).

using System;
using System.Collections.Generic;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Tag
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 File Tagger - Version 1.2 " +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2008 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) / exit replacements)
        +---------------------------------------------------------------------*/
        private static ByteStream s_Stdout;

        private static void Printf(string text)
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

        // PORT NOTE: fprintf(stderr) writes the raw bytes of the text (file names
        // given on the command line included), not the console encoding.
        private static System.IO.Stream s_Stderr;

        private static void Eprintf(string text)
        {
            if (s_Stderr == null) s_Stderr = Console.OpenStandardError();
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stderr.Write(bytes, 0, bytes.Length);
            s_Stderr.Flush();
        }

        private static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(Ap4Result value) { return D((int)value); }

        private sealed class ExitException : Exception
        {
            public ExitException(int code) { _code = code; }
            public int _code;
        }

        // strtoul(str, NULL, 10)
        // PORT NOTE: `unsigned long` is 32 bits wide here, as in the reference
        // build of the original tools (wasm32) and in native Windows builds
        // (it is 64 bits wide on 64-bit Linux/macOS): values that do not fit
        // in 32 bits give ULONG_MAX = 0xFFFFFFFF.
        private static uint Strtoul(string str)
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
                value = value*10+(ulong)(str[i]-'0');
                if (value > uint.MaxValue) {
                    overflow = true;
                    value = uint.MaxValue;
                }
                ++i;
            }
            if (overflow) return uint.MaxValue;
            return negative ? unchecked((uint)(-(long)value)) : (uint)value;
        }

        // strtol(str, NULL, 10)
        // PORT NOTE: `long` is 32 bits wide here, as in the reference build of the
        // original tools (wasm32) and in native Windows builds (it is 64 bits wide
        // on 64-bit Linux/macOS): values out of range give LONG_MIN/LONG_MAX.
        private static long Strtol(string str)
        {
            int i = 0;
            while (i < str.Length && (str[i] == ' ' || (str[i] >= '\t' && str[i] <= '\r'))) ++i;
            bool negative = false;
            if (i < str.Length && (str[i] == '+' || str[i] == '-')) {
                negative = str[i] == '-';
                ++i;
            }
            ulong limit = negative ? (ulong)int.MaxValue+1 : (ulong)int.MaxValue;
            ulong value = 0;
            bool overflow = false;
            while (i < str.Length && str[i] >= '0' && str[i] <= '9') {
                value = value*10+(ulong)(str[i]-'0');
                if (value > limit) {
                    overflow = true;
                    value = limit;
                }
                ++i;
            }
            if (overflow) return negative ? int.MinValue : int.MaxValue;
            return negative ? -(long)value : (long)value;
        }

        /*----------------------------------------------------------------------
        |   types
        +---------------------------------------------------------------------*/
        private class Command
        {
            // types
            public enum Type {
                TYPE_SHOW_TAGS,
                TYPE_LIST_KEYS,
                TYPE_LIST_SYMBOLS,
                TYPE_SET,
                TYPE_ADD,
                TYPE_REMOVE,
                TYPE_EXTRACT
            }

            // constructors
            public Command(Type    type,
                           string  arg1 = null,
                           string arg2 = null)
            {
                _type = type;
                _arg1 = arg1 ?? "";
                _arg2 = arg2 ?? "";
            }

            // members
            public Type   _type;
            public string _arg1;
            public string _arg2;
        }

        private struct TableCell {
            public TableCell(string label, uint width) { this.label = label; this.width = width; }
            public string label;
            public uint   width;
        }

        /*----------------------------------------------------------------------
        |   options
        +---------------------------------------------------------------------*/
        private static class Options {
            public static string        input_filename;
            public static string        output_filename;
            public static List<Command> commands = new List<Command>();
            public static bool          need_input;
            public static bool          need_output;
        }

        private const int LINE_WIDTH = 79;

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static void PrintUsageAndExit()
        {
            Eprintf(
                    BANNER +
                    "\n\nusage: mp4tag [options] [commands...] <input> [<output>]\n" +
                    "commands:\n" +
                    "  --help            print this usage information\n" +
                    "  --show-tags       show tags found in the input file\n" +
                    "  --list-symbols    list all the builtin symbols\n" +
                    "  --list-keys       list all the builtin symbolic key names\n" +
                    "  --set <key>:<type>:<value>\n" +
                    "      set a tag (if the tag does not already exist, set behaves like add)\n" +
                    "  --add <key>:<type>:<value>\n" +
                    "      set/add a tag\n" +
                    "      where <type> is:\n" +
                    "        S    if <value> is a UTF-8 string\n" +
                    "        LS   if <value> is a UTF-8 string with a language code (see notes)\n" +
                    "        I8   if <value> is an 8-bit integer\n" +
                    "        I16  if <value> is a 16-bit integer\n" +
                    "        I32  if <value> is a 32-bit integer\n" +
                    "        JPEG if <value> is the name of a JPEG file\n" +
                    "        GIF  if <value> is the name of a GIF file\n" +
                    "        B    if <value> is a binary string (see notes)\n" +
                    "        Z    if <value> is the name of a builtin symbol\n" +
                    "  --remove <key>\n" +
                    "       remove a tag\n" +
                    "  --extract <key>:<file>\n" +
                    "       extract the value of a tag and save it to a file\n" +
                    "\n" +
                    "NOTES:\n" +
                    "  In all commands with a <key> argument, except for '--add', <key> can be \n" +
                    "  <key-name> or <key-name>#n where n is the zero-based index of the key when\n" +
                    "  there is more than one key with the same name (ex: multiple images for cover\n" +
                    "  art).\n" +
                    "  A <key-name> has the form <namespace>/<name> or simply <name> (in which case\n" +
                    "  the namespace defaults to 'meta').\n" +
                    "  The <name> of a key is either one of a symbolic keys\n" +
                    "  (see --list-keys) or a 4-character atom name.\n" +
                    "  The namespace can be 'meta' for itunes-style metadata or 'dcf' for\n" +
                    "  OMA-DCF-style metadata, or a user-defined long-form namespace\n" +
                    "  (ex: com.mycompany.foo).\n" +
                    "  Binary strings can be expressed as a normal string of ASCII characters\n" +
                    "  prefixed by a + character if all the bytes fall in the ASCII range,\n" +
                    "  or hex-encoded prefixed by a # character (ex: +hello, or #0FC4)\n" +
                    "  Strings with a language code are expressed as: <lang>:<string>,\n" +
                    "  where <lang> is a 3 character language code (ex: eng:hello)\n");
            throw new ExitException(1);
        }

        /*----------------------------------------------------------------------
        |   ParseCommandLine
        +---------------------------------------------------------------------*/
        private static void ParseCommandLine(string[] argv)
        {
            int argc = argv.Length;
            for (int i=0; i<argc; i++) {
                if ("--help" == argv[i]) {
                    PrintUsageAndExit();
                } else if ("--show-tags" == argv[i]) {
                    Options.commands.Add(new Command(Command.Type.TYPE_SHOW_TAGS));
                    Options.need_input = true;
                } else if ("--list-keys" == argv[i]) {
                    Options.commands.Add(new Command(Command.Type.TYPE_LIST_KEYS));
                } else if ("--list-symbols" == argv[i]) {
                    Options.commands.Add(new Command(Command.Type.TYPE_LIST_SYMBOLS));
                } else if ("--add" == argv[i]) {
                    if (i == argc-1) {
                        Eprintf("ERROR: missing argument after --add command");
                        PrintUsageAndExit();
                    }
                    Options.commands.Add(new Command(Command.Type.TYPE_ADD, argv[++i]));
                    Options.need_input  = true;
                    Options.need_output = true;
                } else if ("--set" == argv[i]) {
                    if (i == argc-1) {
                        Eprintf("ERROR: missing argument after --set command");
                        PrintUsageAndExit();
                    }
                    Options.commands.Add(new Command(Command.Type.TYPE_SET, argv[++i]));
                    Options.need_input  = true;
                    Options.need_output = true;
                } else if ("--remove" == argv[i]) {
                    if (i == argc-1) {
                        Eprintf("ERROR: missing argument after --remove command");
                        PrintUsageAndExit();
                    }
                    Options.commands.Add(new Command(Command.Type.TYPE_REMOVE, argv[++i]));
                    Options.need_input  = true;
                    Options.need_output = true;
                } else if ("--extract" == argv[i]) {
                    if (i == argc-1) {
                        Eprintf("ERROR: missing argument after --extract command");
                        PrintUsageAndExit();
                    }
                    Options.commands.Add(new Command(Command.Type.TYPE_EXTRACT, argv[++i]));
                    Options.need_input  = true;
                } else {
                    if (Options.input_filename == null) {
                        Options.input_filename = argv[i];
                    } else if (Options.output_filename == null) {
                        Options.output_filename = argv[i];
                    } else {
                        Eprintf("ERROR: unpexpected option \"" + argv[i] + "\"\n");
                        PrintUsageAndExit();
                    }
                }
            }

            if (Options.commands.Count == 0) {
                if (Options.input_filename != null) {
                    Options.commands.Add(new Command(Command.Type.TYPE_SHOW_TAGS));
                    Options.need_input = true;
                } else {
                    PrintUsageAndExit();
                }
            }
        }

        /*----------------------------------------------------------------------
        |   PrintTableSeparator
        +---------------------------------------------------------------------*/
        private static void PrintTableSeparator(TableCell[] cells, uint cell_count)
        {
            int c = 0;
            while (cell_count-- != 0) {
                Printf("+");
                for (uint i=0; i<cells[c].width; i++) {
                    Printf("-");
                }
                c++;
            }
            Printf("+\n");
        }

        /*----------------------------------------------------------------------
        |   PrintTableRow
        +---------------------------------------------------------------------*/
        private static void PrintTableRow(TableCell[] cells, uint cell_count)
        {
            int c = 0;
            while (cell_count-- != 0) {
                Printf("|");
                Printf(cells[c].label);
                for (uint x=Ap4Utils.StringLength(cells[c].label); x<cells[c].width; x++) {
                    Printf(" ");
                }
                c++;
            }
            Printf("|\n");
        }

        /*----------------------------------------------------------------------
        |   TypeCode
        +---------------------------------------------------------------------*/
        private static string TypeCode(MetaData.Value.Type type) {
            switch (type) {
                case MetaData.Value.Type.TYPE_BINARY:         return "B";
                case MetaData.Value.Type.TYPE_STRING_UTF_8:   return "S";
                case MetaData.Value.Type.TYPE_STRING_UTF_16:  return "W";
                case MetaData.Value.Type.TYPE_INT_08_BE:      return "I8";
                case MetaData.Value.Type.TYPE_INT_16_BE:      return "I16";
                case MetaData.Value.Type.TYPE_INT_32_BE:      return "I32";
                case MetaData.Value.Type.TYPE_FLOAT_32_BE:    return "F32";
                case MetaData.Value.Type.TYPE_FLOAT_64_BE:    return "F64";
                case MetaData.Value.Type.TYPE_JPEG:           return "JPEG";
                case MetaData.Value.Type.TYPE_GIF:            return "GIF";
                default: return "?";
            }
        }

        /*----------------------------------------------------------------------
        |   MeaningCode
        +---------------------------------------------------------------------*/
        private static string MeaningCode(MetaData.Value.Meaning meaning) {
            switch (meaning) {
                case MetaData.Value.Meaning.MEANING_BOOLEAN:    return "/Bool";
                case MetaData.Value.Meaning.MEANING_ID3_GENRE:  return "/ID3";
                default: return "";
            }
        }

        /*----------------------------------------------------------------------
        |   ListKeys
        +---------------------------------------------------------------------*/
        private static void ListKeys()
        {
            // measure the widest key
            uint c1_width = 0;
            for (int i=0; i<MetaData.KeyInfos.Count; i++) {
                MetaData.KeyInfo key = MetaData.KeyInfos[i];
                uint key_width = Ap4Utils.StringLength(key.name);
                if (key_width > c1_width) c1_width = key_width;
            }
            c1_width += 2; // account for padding
            uint c3_width = 4;
            uint c2_width = LINE_WIDTH-c1_width-c3_width-1-8;
            TableCell[] cells = new TableCell[4] {
                new TableCell(" Key",         c1_width),
                new TableCell(" Description", c2_width),
                new TableCell(" 4CC",         4       ),
                new TableCell("Type",         4       )
            };
            PrintTableSeparator(cells, 4);
            PrintTableRow(cells, 4);
            PrintTableSeparator(cells, 4);

            string four_cc;
            for (int i=0; i<MetaData.KeyInfos.Count; i++) {
                MetaData.KeyInfo key = MetaData.KeyInfos[i];
                four_cc = Ap4Utils.FormatFourCharsPrintable(key.four_cc);
                cells[0].label = key.name;
                cells[1].label = key.description;
                cells[2].label = four_cc;
                cells[3].label = TypeCode(key.value_type);
                PrintTableRow(cells, 4);
            }
            PrintTableSeparator(cells, 4);

            Printf("\nDCF Special 4CC types:\n");
            Printf("dcfD\n\n");
            Printf("DCF String 4CC types:\n");
            string sep = "";
            for (uint i=0; i<MetaDataAtomTypeHandler.DcfStringTypeList._size; i++) {
                four_cc = Ap4Utils.FormatFourCharsPrintable(MetaDataAtomTypeHandler.DcfStringTypeList._types[i]);
                Printf(sep + four_cc);
                sep = ", ";
            }
            Printf("\n\n3GPP Localized String 4CC types:\n");
            sep = "";
            for (uint i=0; i<MetaDataAtomTypeHandler._3gppLocalizedStringTypeList._size; i++) {
                four_cc = Ap4Utils.FormatFourCharsPrintable(MetaDataAtomTypeHandler._3gppLocalizedStringTypeList._types[i]);
                Printf(sep + four_cc);
                sep = ", ";
            }
            Printf("\n\n3GPP Other 4CC types:\n");
            sep = "";
            for (uint i=0; i<MetaDataAtomTypeHandler._3gppOtherTypeList._size; i++) {
                four_cc = Ap4Utils.FormatFourCharsPrintable(MetaDataAtomTypeHandler._3gppOtherTypeList._types[i]);
                Printf(sep + four_cc);
                sep = ", ";
            }
            Printf("\n");
        }

        /*----------------------------------------------------------------------
        |   ListSymbols
        +---------------------------------------------------------------------*/
        private static void ListSymbols()
        {

        }

        /*----------------------------------------------------------------------
        |   ShowTag
        +---------------------------------------------------------------------*/
        private static void ShowTag(MetaData.Entry entry)
        {
            // look for a match in the key infos
            string key_name = entry._key.GetName();
            bool   show_namespace = true;
            uint   atom_type = Atom.TypeFromString(entry._key.GetName());
            for (int i=0; i<MetaData.KeyInfos.Count; ++i) {
                if (MetaData.KeyInfos[i].four_cc == atom_type) {
                    key_name = MetaData.KeyInfos[i].name;
                    show_namespace = false;
                    break;
                }
            }

            // print the key namespace unless we have a symbolic name
            if (show_namespace) {
                Printf(entry._key.GetNamespace() + "/");
            }

            // print the key name
            Printf(key_name + ":");

            // print the value type
            Printf("[" +
                   TypeCode(entry._value.GetType()) +
                   MeaningCode(entry._value.GetMeaning()) + "]");

            // print the language, unless it is not set
            if (Ap4Utils.StringLength(entry._value.GetLanguage()) != 0) {
                Printf(" (" + entry._value.GetLanguage() + ")");
            }

            // print the value
            Printf(" " + entry._value.ToString());

            Printf("\n");
        }

        /*----------------------------------------------------------------------
        |   ShowTags
        +---------------------------------------------------------------------*/
        private static void ShowTags(File file)
        {
            MetaData metadata = file.GetMetaData();

            // iterator over the entries
            foreach (MetaData.Entry entry in metadata.GetEntries()) {
                ShowTag(entry);
            }
        }

        /*----------------------------------------------------------------------
        |   SplitString
        +---------------------------------------------------------------------*/
        private static Ap4Result SplitString(string       arg,
                                             char         separator,
                                             List<string> components,
                                             uint         count)
        {
            string component;
            int start=0, end;
            for (uint i=0; i<count; i++) {
                end = i==count-1?arg.Length:arg.IndexOf(separator, start);
                if (end == -1) return Ap4Result.FAILURE;
                component = arg.Substring(start, end-start);
                components.Add(component);
                start = end+1;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   ParseKeySpec
        +---------------------------------------------------------------------*/
        private static Ap4Result ParseKeySpec(string      key,
                                              out string  key_namespace,
                                              out string  key_name,
                                              out uint    key_index)
        {
            key_namespace = null;
            key_name = null;
            key_index = 0;

            // deal with the key index specifier
            string processed_key = key;
            for (int i=key.Length-1; i>=0; --i) {
                if (key[i] == '#') {
                    processed_key = key.Substring(0, i);
                    key_index = Strtoul(key.Substring(i+1));
                }
            }

            if (processed_key.IndexOf('/') != -1) {
                // the key is in the form namespace/name
                List<string> key_components = new List<string>();
                SplitString(processed_key, '/', key_components, 2);
                key_namespace = key_components.Count > 0 ? key_components[0] : null;
                key_name      = key_components.Count > 1 ? key_components[1] : null;
            } else {
                // assume the namespace is 'meta'
                key_namespace = "meta";
                key_name      = processed_key;
            }

            // look for the name in the list of keys
            for (int i=0; i<MetaData.KeyInfos.Count; i++) {
                MetaData.KeyInfo key_info = MetaData.KeyInfos[i];
                if (key_name == key_info.name) {
                    byte[] four_cc = new byte[4];
                    four_cc[0] = (byte)(key_info.four_cc>>24);
                    four_cc[1] = (byte)(key_info.four_cc>>16);
                    four_cc[2] = (byte)(key_info.four_cc>> 8);
                    four_cc[3] = (byte)(key_info.four_cc    );
                    key_name = Ap4Utils.StringFromCString(four_cc, 0, 4);
                    break;
                }
            }
            if (key_name == null) {
                Eprintf("ERROR: unknown key\n");
                key_namespace = null;
                return Ap4Result.FAILURE;
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   GetNibble
        +---------------------------------------------------------------------*/
        private static uint GetNibble(byte c)
        {
            switch ((char)c) {
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
                case 'a':
                case 'A': return 0xA;
                case 'b':
                case 'B': return 0xB;
                case 'c':
                case 'C': return 0xC;
                case 'd':
                case 'D': return 0xD;
                case 'e':
                case 'E': return 0xE;
                case 'f':
                case 'F': return 0xF;
                default: return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   RemoveTag
        +---------------------------------------------------------------------*/
        private static Ap4Result RemoveTag(File file, string key, bool silent)
        {
            string key_namespace;
            string  key_name;
            uint    key_index;
            Ap4Result result = ParseKeySpec(key, out key_namespace, out key_name, out key_index);
            if (result.Failed()) {
                Eprintf("ERROR: invalid key name (" + key + ")\n");
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            MetaData.Entry entry = new MetaData.Entry(key_name, key_namespace, null);
            result = entry.RemoveFromFile(file, key_index);
            if (result.Failed() && !silent) {
                Eprintf("ERROR: failed to remove entry (" + D(result) + ")\n");
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AddTag
        +---------------------------------------------------------------------*/
        private static Ap4Result AddTag(File file, string arg, bool remove_first)
        {
            Ap4Result result;

            List<string> arg_components = new List<string>();
            result = SplitString(arg, ':', arg_components, 3);
            if (result.Failed()) {
                Eprintf("ERROR: invalid argument syntax for --add command\n");
                return result;
            }

            // split the components
            string key   = arg_components[0];
            string type  = arg_components[1];
            string value = arg_components[2];

            string key_namespace = null;
            string  key_name = null;
            uint    key_index = 0;
            result = ParseKeySpec(key, out key_namespace, out key_name, out key_index);
            if (result.Failed()) {
                Eprintf("ERROR: invalid key name (" + key + ")\n");
                return result;
            }

            // remove the entry first if necessary
            if (remove_first) {
                RemoveTag(file, key, true);
            }

            // PORT NOTE: upstream indexes the raw bytes of the value
            byte[] value_bytes  = Ap4Utils.StringToCStringBytes(value);
            uint   value_length = (uint)value_bytes.Length;

            // create an entry
            MetaData.Value vobj = null;
            if (type == "S") {
                vobj = new StringMetaDataValue(value);
            } else if (type == "LS") {
                if (value_length < 4 || value_bytes[3] != (byte)':') {
                    Eprintf("ERROR: invalid syntax for LS value\n");
                    result = Ap4Result.ERROR_INVALID_FORMAT;
                    goto end;
                }
                string lang = Ap4Utils.StringFromCString(value_bytes, 0, 3);
                vobj = new StringMetaDataValue(Ap4Utils.StringFromBytes(value_bytes, 4, value_length-4));
                vobj.SetLanguage(lang);
            } else if (type == "I8") {
                long v = Strtol(value);
                vobj = new IntegerMetaDataValue(MetaData.Value.Type.TYPE_INT_08_BE, v);
            } else if (type == "I16") {
                long v = Strtol(value);
                vobj = new IntegerMetaDataValue(MetaData.Value.Type.TYPE_INT_16_BE, v);
            } else if (type == "I32") {
                long v = Strtol(value);
                vobj = new IntegerMetaDataValue(MetaData.Value.Type.TYPE_INT_32_BE, v);
            } else if (type == "JPEG" || type == "GIF") {
                MetaData.Value.Type data_type =
                    (type == "JPEG" ? MetaData.Value.Type.TYPE_JPEG : MetaData.Value.Type.TYPE_GIF);
                ByteStream data_file = null;
                result = FileByteStream.Create(value, FileByteStream.Mode.STREAM_MODE_READ, out data_file);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot open file " + value + "\n");
                    goto end;
                }
                DataBuffer buffer = new DataBuffer();
                ulong      data_size;
                data_file.GetSize(out data_size);
                buffer.SetDataSize((uint)data_size);
                data_file.Read(buffer.UseData(), (uint)data_size);
                vobj = new BinaryMetaDataValue(data_type, buffer.GetData(), buffer.GetDataSize());
                if (data_file is IDisposable) ((IDisposable)data_file).Dispose();
            } else if (type == "B") {
                if (value_length == 0) {
                    Eprintf("ERROR: invalid binary encoding\n");
                    result = Ap4Result.ERROR_INVALID_PARAMETERS;
                    goto end;
                } else if (value_bytes[0] == (byte)'+') {
                    byte[] binary_string = new byte[value_length-1];
                    Array.Copy(value_bytes, 1, binary_string, 0, (int)(value_length-1));
                    vobj = new BinaryMetaDataValue(MetaData.Value.Type.TYPE_BINARY,
                                                   binary_string,
                                                   value_length-1);
                } else if (value_bytes[0] == (byte)'#') {
                    if (((value_length-1)%2) != 0) {
                        Eprintf("ERROR: invalid hex encoding\n");
                        result = Ap4Result.ERROR_INVALID_PARAMETERS;
                        goto end;
                    } else {
                        uint   binary_string_length = (value_length-1)/2;
                        byte[] binary_string = new byte[binary_string_length];
                        for (uint i=0; i<binary_string_length; i++) {
                            uint nibble_0 = GetNibble(value_bytes[1+i*2  ]);
                            uint nibble_1 = GetNibble(value_bytes[1+i*2+1]);
                            binary_string[i] = (byte)((nibble_0<<4)|nibble_1);
                        }
                        vobj = new BinaryMetaDataValue(MetaData.Value.Type.TYPE_BINARY,
                                                       binary_string,
                                                       binary_string_length);
                    }
                } else {
                    Eprintf("ERROR: invalid binary string (does not start with + or #)\n");
                    result = Ap4Result.ERROR_INVALID_PARAMETERS;
                    goto end;
                }
            } else {
                Eprintf("ERROR: unsupported type '" + type + "'\n");
                result = Ap4Result.ERROR_INVALID_PARAMETERS;
                goto end;
            }
            {
                MetaData.Entry entry = new MetaData.Entry(key_name, key_namespace, vobj);

                // add the entry to the file
                result = entry.AddToFile(file, key_index);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot add entry " + key + " (" +
                            D(result) + ":" + Ap4.ResultText(result) + ")\n");
                }
            }

        end:
            return result;
        }

        /*----------------------------------------------------------------------
        |   ExtractTag
        +---------------------------------------------------------------------*/
        private static Ap4Result ExtractTag(File file, string arg)
        {
            List<string> arg_components = new List<string>();
            Ap4Result result = SplitString(arg, ':', arg_components, 2);
            if (result.Failed()) {
                Eprintf("ERROR: invalid argument syntax for --extract command\n");
                return result;
            }

            // split the components
            string key = arg_components[0];
            string output_filename = arg_components[1];

            string key_namespace = null;
            string  key_name = null;
            uint    key_index;
            result = ParseKeySpec(key, out key_namespace, out key_name, out key_index);
            if (result.Failed()) {
                Eprintf("ERROR: invalid key name (" + key + ")\n");
                return result;
            }

            // get the metadata
            MetaData metadata = file.GetMetaData();

            // iterate over the entries and look for a match
            bool found = false;
            foreach (MetaData.Entry entry in metadata.GetEntries()) {
                if (entry._key.GetNamespace() == key_namespace &&
                    entry._key.GetName()      == key_name      &&
                    key_index--               == 0) {
                    // match
                    if (entry._value == null) break;
                    DataBuffer buffer = new DataBuffer();
                    if (entry._value.ToBytes(buffer).Failed()) break;
                    found = true;
                    ByteStream output = null;
                    result = FileByteStream.Create(output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
                    if (result.Failed()) {
                        Eprintf("ERROR: cannot open output/extract file\n");
                        goto end;
                    }
                    output.Write(buffer.GetData(), buffer.GetDataSize());
                    if (output is IDisposable) ((IDisposable)output).Dispose();
                }
            }
            if (!found) {
                Eprintf("ERROR: cannot extract tag, entry not found\n");
                result = Ap4Result.ERROR_NO_SUCH_ITEM;
            }

        end:
            return result;
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            ByteStream input  = null;
            ByteStream output = null;
            int exit_code;
            try {
                exit_code = Run(args, ref input, ref output);
            } catch (ExitException e) {
                exit_code = e._code;
            } finally {
                if (input is IDisposable) ((IDisposable)input).Dispose();
                if (output is IDisposable) ((IDisposable)output).Dispose();
                FlushStdout();
            }
            return exit_code;
        }

        private static int Run(string[] args, ref ByteStream input, ref ByteStream output)
        {
            if (args.Length+1 < 2) {
                PrintUsageAndExit();
            }

            // init options
            Options.input_filename  = null;
            Options.output_filename = null;
            Options.need_input      = false;
            Options.need_output     = false;

            // parse command line
            ParseCommandLine(args);

            // check options
            if (Options.need_input) {
                if (Options.input_filename == null) {
                    Eprintf("ERROR: input file name missing\n");
                    PrintUsageAndExit();
                }
            } else {
                if (Options.input_filename != null) {
                    Eprintf("ERROR: unexpected input file name\n");
                    PrintUsageAndExit();
                }
            }
            if (Options.need_output) {
                if (Options.output_filename == null) {
                    Eprintf("ERROR: output file name missing\n");
                    PrintUsageAndExit();
                }
            } else {
                if (Options.output_filename != null) {
                    Eprintf("ERROR: unexpected output file name\n");
                    PrintUsageAndExit();
                }
            }

            File      file      = null;
            Movie     movie     = null;
            MoovAtom  moov      = null;
            ulong     moov_size = 0;
            Ap4Result result    = Ap4Result.SUCCESS;
            if (Options.need_input) {
                result = FileByteStream.Create(Options.input_filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot open input file\n");
                    return 1;
                }
                file = new File(input);

                // remember the size of the moov atom
                movie = file.GetMovie();
                if (movie != null) {
                    moov = movie.GetMoovAtom();
                    if (moov != null) {
                        moov_size = moov.GetSize();
                    }
                }
            }

            if (Options.need_output) {
                result = FileByteStream.Create(Options.output_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
                if (result.Failed()) {
                    Eprintf("ERROR: cannot open output file for writing\n");
                    return 1;
                }
            }

            foreach (Command item in Options.commands) {
                result = Ap4Result.SUCCESS;
                switch (item._type) {
                    case Command.Type.TYPE_LIST_KEYS:
                        ListKeys();
                        break;

                    case Command.Type.TYPE_LIST_SYMBOLS:
                        ListSymbols();
                        break;

                    case Command.Type.TYPE_SHOW_TAGS:
                        ShowTags(file);
                        break;

                    case Command.Type.TYPE_SET:
                        result = AddTag(file, item._arg1, true);
                        break;

                    case Command.Type.TYPE_ADD:
                        result = AddTag(file, item._arg1, false);
                        break;

                    case Command.Type.TYPE_REMOVE:
                        result = RemoveTag(file, item._arg1, false);
                        break;

                    case Command.Type.TYPE_EXTRACT:
                        result = ExtractTag(file, item._arg1);
                        break;

                    default:
                        break;
                }

                if (result.Failed()) goto end;
            }

            if (output != null) {
                // adjust the chunk offsets if the moov is before the mdat
                if (moov != null && file.IsMoovBeforeMdat()) {
                    ulong new_moov_size = moov.GetSize();
                    long size_diff = unchecked((long)(new_moov_size-moov_size));
                    if (size_diff != 0) {
                        moov.AdjustChunkOffsets(size_diff);
                    }
                }

                // write the modified file
                FileCopier.Write(file, output);
            }

        end:
            // PORT NOTE: `delete file; delete input; delete output` is done by Main
            Options.commands.Clear();

            return (int)result;
        }
    }
}
