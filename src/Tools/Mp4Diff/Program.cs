// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Diff/Mp4Diff.cpp
// Original work Copyright 2002-2017 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one buffered binary standard output stream
//   so that the bytes written are exactly those of upstream ('\n' line
//   endings, no encoding translation).

using System;
using System.Collections.Generic;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Diff
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Diff - Version 1.0.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2017 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static System.IO.Stream s_Stdout;

        private static void Printf(string text)
        {
            if (s_Stdout == null) {
                s_Stdout = new System.IO.BufferedStream(Console.OpenStandardOutput(), 65536);
            }
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stdout.Write(bytes, 0, bytes.Length);
        }

        private static void FlushStdout()
        {
            if (s_Stdout != null) s_Stdout.Flush();
        }

        // The bytes are written as they are: Console.Error would re-encode the
        // text (file names given on the command line) with the console code page.
        private static System.IO.Stream  s_Stderr;

        private static void Eprintf(string text)
        {
            if (s_Stderr == null) {
                s_Stderr = Console.OpenStandardError();
            }
            byte[] bytes = Ap4Utils.StringToBytes(text);
            s_Stderr.Write(bytes, 0, bytes.Length);
        }

        private static string D(int value)  { return value.ToString(CultureInfo.InvariantCulture); }
        private static string D(uint value) { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp4diff [options] <input1> <input2>\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   DiffSamples
        +---------------------------------------------------------------------*/
        private static void DiffSamples(uint index, DataBuffer sample_data1, DataBuffer sample_data2)
        {
            if (sample_data1.GetDataSize() != sample_data2.GetDataSize()) {
                Printf("!!! sample " + D(index) + ": sizes not equal: " + D(sample_data1.GetDataSize()) + ", " + D(sample_data2.GetDataSize()) + "\n");
                return;
            }

            byte[] data1 = sample_data1.GetData();
            byte[] data2 = sample_data2.GetData();
            bool   in_diff = false;
            uint   diff_start = 0;
            for (uint i=0; i<sample_data1.GetDataSize(); i++) {
                if (in_diff) {
                    if (data1[i] == data2[i]) {
                        // end of diff
                        Printf("!!! sample " + D(index) + ", " + D(i-diff_start) + " differences at " + D(diff_start) + " / " + D(sample_data1.GetDataSize()) + "\n");
                        in_diff = false;
                    }
                } else {
                    if (data1[i] != data2[i]) {
                        diff_start = i;
                        in_diff = true;
                    }
                }
            }
            if (in_diff) {
                Printf("!!! sample " + D(index) + ", " + D(sample_data1.GetDataSize()-diff_start) + " differences at " + D(diff_start) + " / " + D(sample_data1.GetDataSize()) + "\n");
            }
        }

        /*----------------------------------------------------------------------
        |   DiffFragments
        +---------------------------------------------------------------------*/
        private static void DiffFragments(Movie movie1, ByteStream stream1, Movie movie2, ByteStream stream2)
        {
            stream1.Seek(0);
            stream2.Seek(0);
            LinearReader reader1 = new LinearReader(movie1, stream1);
            LinearReader reader2 = new LinearReader(movie2, stream2);
            // PORT NOTE: the two lists are walked with indexes instead of list items
            List<Track> tracks1 = movie1.GetTracks();
            List<Track> tracks2 = movie2.GetTracks();
            int track_item1 = 0;
            int track_item2 = 0;
            while (track_item1 < tracks1.Count && track_item2 < tracks2.Count) {
                reader1.EnableTrack(tracks1[track_item1].GetId());
                reader2.EnableTrack(tracks1[track_item1].GetId());
                track_item1++;
                track_item2++;
            }

            Sample     sample1 = new Sample();
            Sample     sample2 = new Sample();
            DataBuffer sample_data1 = new DataBuffer();
            DataBuffer sample_data2 = new DataBuffer();
            uint       prev_track_id = 0;
            for (uint i=0; ; i++) {
                uint track_id1 = 0;
                uint track_id2 = 0;
                Ap4Result result1 = reader1.ReadNextSample(sample1, sample_data1, out track_id1);
                Ap4Result result2 = reader2.ReadNextSample(sample2, sample_data2, out track_id2);
                if (result1.Succeeded() && result2.Succeeded()) {
                    if (track_id1 != track_id2) {
                        Eprintf("!!! sample " + D(i) + ", track ID 1 = " + D(track_id1) + ", track ID 2 = " + D(track_id2) + "\n");
                        return;
                    }

                    if (track_id1 != prev_track_id) {
                        Printf("Track " + D(track_id1) + ":\n");
                        prev_track_id = track_id1;
                    }

                    DiffSamples(i, sample_data1, sample_data2);
                } else {
                    Printf("### processed " + D(i+1) + " samples\n");
                    break;
                }
            }
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] argv)
        {
            int result = RealMain(argv);
            FlushStdout();
            return result;
        }

        private static int RealMain(string[] argv)
        {
            int argc = argv.Length+1;
            if (argc < 3) {
                return PrintUsageAndExit();
            }
            string filename1 = null;
            string filename2 = null;

            foreach (string arg in argv) {
                if (filename1 == null) {
                    filename1 = arg;
                } else if (filename2 == null) {
                    filename2 = arg;
                } else {
                    Eprintf("ERROR: unexpected argument '" + arg + "'\n");
                    return 1;
                }
            }
            if (filename1 == null || filename2 == null) {
                Eprintf("ERROR: filename missing\n");
                return 1;
            }

            ByteStream input1 = null;
            Ap4Result result = FileByteStream.Create(filename1,
                                                     FileByteStream.Mode.STREAM_MODE_READ,
                                                     out input1);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file " + filename1 + " (" + D((int)result) + ")\n");
                return 1;
            }

            ByteStream input2 = null;
            result = FileByteStream.Create(filename2,
                                           FileByteStream.Mode.STREAM_MODE_READ,
                                           out input2);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file " + filename2 + " (" + D((int)result) + ")\n");
                return 1;
            }

            File file1 = new File(input1, true);
            File file2 = new File(input2, true);

            Movie movie1 = file1.GetMovie();
            Movie movie2 = file2.GetMovie();

            if (movie1 != null && movie2 != null) {
                List<Track> tracks1 = movie1.GetTracks();
                List<Track> tracks2 = movie1.GetTracks(); // (sic) movie1, as upstream

                if (tracks1.Count != tracks2.Count) {
                    Eprintf("### file 1 has " + D(tracks1.Count) + " tracks, file 2 has " + D(tracks2.Count) + " tracks\n");
                } else {
                    for (int i=0; i<tracks1.Count; i++) {
                        Track track1;
                        Track track2;
                        track1 = tracks1[i];
                        track2 = tracks2[i];
                        Printf("--- comparing track ID " + D(track1.GetId()) + "\n");
                    }
                }

                if (movie1.HasFragments() != movie2.HasFragments()) {
                    Eprintf("### file 1 fragmented=" + (movie1.HasFragments() ? "true" : "false") +
                            ", file2 fragmented=" + (movie2.HasFragments() ? "true" : "false") + "\n");
                }
                if (movie1.HasFragments() && movie2.HasFragments()) {
                    DiffFragments(movie1, input1, movie2, input2);
                }
            }

            if (input1 is IDisposable) ((IDisposable)input1).Dispose();
            if (input2 is IDisposable) ((IDisposable)input2).Dispose();

            return 0;
        }
    }
}
