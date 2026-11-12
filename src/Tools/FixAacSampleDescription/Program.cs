// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/FixAacSampleDescription/FixAacSampleDescription.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream so that
//   the bytes written are exactly those of upstream ('\n' line endings, no
//   encoding translation).

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.FixAacSampleDescription
{

    /*----------------------------------------------------------------------
    |   AP4_AacSampleDescritionFixer
    +---------------------------------------------------------------------*/
    public class AacSampleDescriptionFixer : Processor
    {
        public override TrackHandler CreateTrackHandler(TrakAtom trak) {
            // find the stsd atom
            StsdAtom stsd = trak.FindChild("mdia/minf/stbl/stsd") as StsdAtom;

            // avoid tracks with no stsd atom (should not happen)
            if (stsd == null) return null;

            // we only look at the first sample description
            SampleDescription  desc = stsd.GetSampleDescription(0);
            MpegAudioSampleDescription audio_desc =
                desc as MpegAudioSampleDescription;
            if (audio_desc != null && audio_desc.GetObjectTypeId()==Ap4.OTI_MPEG2_AAC_AUDIO_LC) {
                // patch the stsd
                MpegAudioSampleDescription new_audio_desc = new MpegAudioSampleDescription(Ap4.OTI_MPEG4_AUDIO,
                                                                                           audio_desc.GetSampleRate(),
                                                                                           audio_desc.GetSampleSize(),
                                                                                           audio_desc.GetChannelCount(),
                                                                                           audio_desc.GetDecoderInfo(),
                                                                                           audio_desc.GetBufferSize(),
                                                                                           audio_desc.GetMaxBitrate(),
                                                                                           audio_desc.GetAvgBitrate());
                stsd.RemoveChild(stsd.GetChild(AtomType.MP4A));
                stsd.AddChild(new_audio_desc.ToAtom());
                Program.Printf("audio sample description patched\n");
            }
            return null;
        }
    }

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 Aac Sample Description Fixer - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2008 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static ByteStream s_Stdout;

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
            Console.Error.Write(text);
        }

        private static string D(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: fixaacsampledescription <input> <output>\n");
            return 1;
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
            if (args.Length != 2) {
                return PrintUsageAndExit();
            }

            // create the input stream
            Ap4Result result;
            ByteStream input = null;
            result = FileByteStream.Create(args[0], FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input file (" + args[0] + ") " + D((int)result) + "\n");
                return 1;
            }

            // create the output stream
            ByteStream output = null;
            result = FileByteStream.Create(args[1], FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output file (" + args[1] + ") " + D((int)result) + "\n");
                return 1;
            }

            // create the processor
            Processor processor = new AacSampleDescriptionFixer();
            result = processor.Process(input, output, (Processor.ProgressListener)null);
            if (result.Failed()) {
                Eprintf("ERROR: failed to process the file (" + D((int)result) + ")\n");
            }

            // cleanup
            if (input is IDisposable) ((IDisposable)input).Dispose();
            if (output is IDisposable) ((IDisposable)output).Dispose();

            return 0;
        }
    }
}
