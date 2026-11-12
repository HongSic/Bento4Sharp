// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Dump/Mp4Dump.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream so that
//   the bytes written are exactly those of upstream ('\n' line endings, no
//   encoding translation) and stay in order with the inspector output.
// - the __EMSCRIPTEN__ blocks are not ported.

using System;
using System.Collections.Generic;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Dump
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 File Dumper - Version 1.2\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2011 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
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

        private static void Eprintf(string text)
        {
            Console.Error.Write(text);
        }

        private static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(uint value) { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value

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
                    "\n\nusage: mp4dump [options] <input>\n" +
                    "options are:\n" +
                    "  --verbosity <n>\n" +
                    "      sets the verbosity (details) level to <n> (between 0 and 3)\n" +
                    "  --track <track_id>[:<key>]\n" +
                    "      writes the track data into a file\n" +
                    "      (<mp4filename>.<track_id>) and optionally\n" +
                    "      tries to decrypt it with the key (128-bit in hex)\n" +
                    "      (several --track options can be used, one for each track)\n" +
                    "      Each sample is written preceded by its size encoded as a 32-bit\n" +
                    "      value in big-endian byte order\n" +
                    "  --format <format>\n" +
                    "      format to use for the output, where <format> is either \n" +
                    "      'text' (default) or 'json'\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   CreateTrackDumpByteStream
        +---------------------------------------------------------------------*/
        private static ByteStream CreateTrackDumpByteStream(string mp4_filename,
                                                             uint   track_id)
        {
            // create the output file name
            string dump_filename = mp4_filename + "." + D(track_id); // <filename>.<trackid>

            // create a FileByteStream
            ByteStream  output = null;
            Ap4Result result = FileByteStream.Create(dump_filename, FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: " + D((int)result) + " cannot open file for dumping track " + D(track_id));
            }

            return output;
        }

        /*----------------------------------------------------------------------
        |   DumpSamples
        +---------------------------------------------------------------------*/
        private static void DumpSamples(Track track, ByteStream dump)
        {
            // write the data
            Sample sample = new Sample();
            DataBuffer sample_data = new DataBuffer();
            uint index = 0;

            while (track.ReadSample(index, sample, sample_data).Succeeded()) {
                // write the sample size
                dump.WriteUI32(sample_data.GetDataSize());

                // write the sample
                dump.Write(sample_data.GetData(), sample_data.GetDataSize());
                index++;

                // print progress info
                if (index%10 == 0) Printf(".");
            }
            Printf("\n");
        }

        /*----------------------------------------------------------------------
        |   DecryptAndDumpSamples
        +---------------------------------------------------------------------*/
        private static void DecryptAndDumpSamples(Track              track,
                                                  SampleDescription  sample_desc,
                                                  byte[]             key,
                                                  uint               key_size,
                                                  ByteStream         dump)
        {
            ProtectedSampleDescription pdesc = sample_desc as ProtectedSampleDescription;
            if (pdesc == null) {
                Eprintf("ERROR: unable to obtain cipher info\n");
                return;
            }

            // create the decrypter
            SampleDecrypter  decrypter = SampleDecrypter.Create(pdesc, key, key_size);
            if (decrypter == null) {
                Eprintf("ERROR: unable to create decrypter\n");
                return;
            }

            Sample     sample = new Sample();
            DataBuffer encrypted_data = new DataBuffer();
            DataBuffer decrypted_data = new DataBuffer();
            uint       index = 0;
            while (track.ReadSample(index, sample, encrypted_data).Succeeded()) {
                if (decrypter.DecryptSampleData(encrypted_data, decrypted_data).Failed()) {
                    Eprintf("ERROR: failed to decrypt sample\n");
                    return;
                }

                // write the sample size
                dump.WriteUI32(decrypted_data.GetDataSize());

                // write the sample
                dump.Write(decrypted_data.GetData(), decrypted_data.GetDataSize());
                index++;
                if (index%10 == 0) Printf(".");
            }
            Printf("\n");
        }

        /*----------------------------------------------------------------------
        |   DumpTrackData
        +---------------------------------------------------------------------*/
        private static void DumpTrackData(string           mp4_filename,
                                          File             mp4_file,
                                          List<uint>       tracks_to_dump,
                                          ProtectionKeyMap key_map)
        {
            // dump all the tracks that need to be dumped
            int tracks_to_dump_count = tracks_to_dump.Count;
            for (int i=0; i<tracks_to_dump_count; i++) {
                // get the track
                uint track_id = tracks_to_dump[i];
                // PORT NOTE: upstream dereferences GetMovie() without a NULL check
                Movie tmp1;
                Track track = (tmp1 = mp4_file.GetMovie()) != null ? tmp1.GetTrack(track_id) : null;
                if (track == null) {
                    Eprintf("track not found (id = " + D(track_id) + ")");
                    return;
                }

                // get the sample description
                SampleDescription sample_description = track.GetSampleDescription(0);
                if (sample_description == null) {
                    Eprintf("WARNING: unable to parse sample description\n");
                }

                // get the dump data byte stream
                ByteStream  dump = CreateTrackDumpByteStream(mp4_filename, track_id);
                if (dump == null) return;

                Printf("\nDumping data for track " + D(track_id) + ":\n");
                switch (sample_description != null ?
                        sample_description.GetType() :
                        SampleDescription.Type.TYPE_UNKNOWN) {
                    case SampleDescription.Type.TYPE_PROTECTED:
                        {
                            DataBuffer key = key_map.GetKey(track_id);
                            if (key == null) {
                                Eprintf("WARNING: No key found for encrypted track " + D(track_id) + "... " +
                                        "dumping encrypted samples\n");
                                DumpSamples(track, dump);
                            } else {
                                DecryptAndDumpSamples(track, sample_description, key.GetData(), key.GetDataSize(), dump);
                            }
                        }
                        break;
                    default:
                        DumpSamples(track, dump);
                        break;
                }
                if (dump is IDisposable) ((IDisposable)dump).Dispose();
            }
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            if (args.Length < 1) {
                return PrintUsageAndExit();
            }

            // init the variables
            ByteStream   input       = null;
            string       filename    = null;
            ProtectionKeyMap key_map = new ProtectionKeyMap();
            List<uint>   tracks_to_dump = new List<uint>();
            uint         verbosity   = 0;
            bool         json_format = false;

            // parse the command line
            int argi = 0;
            while (argi < args.Length) {
                string arg = args[argi++];
                if (arg == "--track") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --track option\n");
                        return 1;
                    }
                    string  track_ascii = arg;
                    string  key_ascii = null;
                    int delimiter = arg.IndexOf(':');
                    if (delimiter >= 0) {
                        track_ascii = arg.Substring(0, delimiter);
                        key_ascii = arg.Substring(delimiter+1);
                    }

                    // this track will be dumped
                    uint track_id = unchecked((uint)Strtoul(track_ascii));
                    tracks_to_dump.Add(track_id);

                    // see if we have a key for this track
                    if (key_ascii != null) {
                        byte[] key = new byte[16];
                        if (Ap4Utils.ParseHex(key_ascii, key, 16) != Ap4Result.SUCCESS) {
                            Eprintf("ERROR: invalid hex format for key\n");
                            return 1;
                        }
                        // set the key in the map
                        key_map.SetKey(track_id, key, 16);
                    }
                } else if (arg == "--verbosity") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --verbosity option\n");
                        return 1;
                    }
                    verbosity = unchecked((uint)Strtoul(arg));
                } else if (arg == "--format") {
                    arg = argi < args.Length ? args[argi++] : null;
                    if (arg == null) {
                        Eprintf("ERROR: missing argument after --format option\n");
                        return 1;
                    }
                    if (arg == "json") {
                        json_format = true;
                    } else if (arg != "text") {
                        Eprintf("ERROR: unknown output format\n");
                        return 1;
                    }
                } else {
                    filename = arg;
                    Ap4Result result = FileByteStream.Create(filename, FileByteStream.Mode.STREAM_MODE_READ, out input);
                    if (result.Failed()) {
                        Eprintf("ERROR: cannot open input (" + D((int)result) + ")\n");
                        return 1;
                    }
                }
            }

            if (input == null) {
                Eprintf("ERROR: no input specified\n");
                return 1;
            }

            // open the output
            // PORT NOTE: upstream opens its own "-stdout" stream here, which shares
            // the C runtime's stdout with printf(); the port shares one stream object.
            Printf("");
            ByteStream output = s_Stdout;

            // create an inspector
            AtomInspector inspector;
            if (json_format) {
                inspector = new JsonInspector(output);
            } else {
                inspector = new PrintInspector(output);
            }
            inspector.SetVerbosity(verbosity);

            // inspect the atoms one by one
            Atom atom;
            DefaultAtomFactory atom_factory = new DefaultAtomFactory();
            while (atom_factory.CreateAtomFromStream(input, out atom) == Ap4Result.SUCCESS) {
                // remember the current stream position because the Inspect method
                // may read from the stream (there may be stream references in some
                // of the atoms
                ulong position;
                input.Tell(out position);

                // inspect the atom
                atom.Inspect(inspector);

                // restore the previous stream position
                input.Seek(position);
            }

            // inspect the track data if needed
            if (tracks_to_dump.Count != 0) {
                // rewind
                input.Seek(0);

                // dump the track data
                File file = new File(input);
                DumpTrackData(filename, file, tracks_to_dump, key_map);
            }

            if (input is IDisposable) ((IDisposable)input).Dispose();

            // PORT NOTE: `delete inspector` is the last statement upstream: the
            // JSON inspector writes its closing bracket there, after the track dump.
            if (inspector is IDisposable) ((IDisposable)inspector).Dispose();
            FlushStdout();

            return 0;
        }
    }
}
