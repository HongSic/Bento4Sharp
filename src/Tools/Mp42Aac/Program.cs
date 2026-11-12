// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp42Aac/Mp42Aac.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp42Aac
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 To AAC File Converter - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2008 Axiomatic Systems, LLC";

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: fprintf(stderr) replacement)
        +---------------------------------------------------------------------*/
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

        private static string D(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp42aac [options] <input> <output>\n" +
                    "  Options:\n" +
                    "  --key <hex>: 128-bit decryption key (in hex: 32 chars)\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   GetSamplingFrequencyIndex
        +---------------------------------------------------------------------*/
        private static uint GetSamplingFrequencyIndex(uint sampling_frequency)
        {
            switch (sampling_frequency) {
                case 96000: return 0;
                case 88200: return 1;
                case 64000: return 2;
                case 48000: return 3;
                case 44100: return 4;
                case 32000: return 5;
                case 24000: return 6;
                case 22050: return 7;
                case 16000: return 8;
                case 12000: return 9;
                case 11025: return 10;
                case 8000:  return 11;
                case 7350:  return 12;
                default:    return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   WriteAdtsHeader
        +---------------------------------------------------------------------*/
        private static Ap4Result WriteAdtsHeader(ByteStream output,
                                                 uint       frame_size,
                                                 uint       sampling_frequency_index,
                                                 uint       channel_configuration)
        {
            byte[] bits = new byte[7];

            bits[0] = 0xFF;
            bits[1] = 0xF1; // 0xF9 (MPEG2)
            bits[2] = unchecked((byte)(0x40 | (sampling_frequency_index << 2) | (channel_configuration >> 2)));
            bits[3] = unchecked((byte)(((channel_configuration&0x3)<<6) | ((frame_size+7) >> 11)));
            bits[4] = unchecked((byte)(((frame_size+7) >> 3)&0xFF));
            bits[5] = unchecked((byte)((((frame_size+7) << 5)&0xFF) | 0x1F));
            bits[6] = 0xFC;

            return output.Write(bits, 7);

            /*
                0:  syncword 12 always: '111111111111'
                12: ID 1 0: MPEG-4, 1: MPEG-2
                13: layer 2 always: '00'
                15: protection_absent 1
                16: profile 2
                18: sampling_frequency_index 4
                22: private_bit 1
                23: channel_configuration 3
                26: original/copy 1
                27: home 1
                28: emphasis 2 only if ID == 0

                ADTS Variable header: these can change from frame to frame
                28: copyright_identification_bit 1
                29: copyright_identification_start 1
                30: aac_frame_length 13 length of the frame including header (in bytes)
                43: adts_buffer_fullness 11 0x7FF indicates VBR
                54: no_raw_data_blocks_in_frame 2
                ADTS Error check
                crc_check 16 only if protection_absent == 0
           */
        }

        /*----------------------------------------------------------------------
        |   DecryptAndWriteSamples
        +---------------------------------------------------------------------*/
        private static void DecryptAndWriteSamples(Track              track,
                                                   SampleDescription  sdesc,
                                                   byte[]             key,
                                                   ByteStream         output)
        {
            ProtectedSampleDescription pdesc = sdesc as ProtectedSampleDescription;
            if (pdesc == null) {
                Eprintf("ERROR: unable to obtain cipher info\n");
                return;
            }

            AudioSampleDescription audio_desc = pdesc.GetOriginalSampleDescription() as AudioSampleDescription;
            if (audio_desc == null) {
                Eprintf("ERROR: sample description is not audio\n");
                return;
            }
            uint sampling_frequency_index = GetSamplingFrequencyIndex(audio_desc.GetSampleRate());
            uint channel_configuration    = audio_desc.GetChannelCount();

            // create the decrypter
            SampleDecrypter  decrypter = SampleDecrypter.Create(pdesc, key, 16);
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

                WriteAdtsHeader(output, decrypted_data.GetDataSize(), sampling_frequency_index, channel_configuration);
                output.Write(decrypted_data.GetData(), decrypted_data.GetDataSize());
                index++;
            }
        }

        /*----------------------------------------------------------------------
        |   WriteSamples
        +---------------------------------------------------------------------*/
        private static void WriteSamples(Track              track,
                                         SampleDescription  sdesc,
                                         ByteStream         output)
        {
            AudioSampleDescription audio_desc = sdesc as AudioSampleDescription;
            if (audio_desc == null) {
                Eprintf("ERROR: sample description is not audio\n");
                return;
            }
            uint sampling_frequency_index = GetSamplingFrequencyIndex(audio_desc.GetSampleRate());
            uint channel_configuration    = audio_desc.GetChannelCount();

            Sample     sample = new Sample();
            DataBuffer data = new DataBuffer();
            uint       index = 0;
            while (track.ReadSample(index, sample, data).Succeeded()) {
                WriteAdtsHeader(output, sample.GetSize(), sampling_frequency_index, channel_configuration);
                output.Write(data.GetData(), data.GetDataSize());
                index++;
            }
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] argv)
        {
            int return_value = 1;
            int argc = argv.Length+1;

            if (argc < 3) {
                return PrintUsageAndExit();
            }

            // parse command line
            Ap4Result result;
            int    args = 0;
            byte[] key = new byte[16];
            bool   key_option = false;
            if (argv[args] == "--key") {
                if (argc != 5) {
                    Eprintf("ERROR: invalid command line\n");
                    return 1;
                }
                ++args;
                if (Ap4Utils.ParseHex(argv[args++], key, 16) != Ap4Result.SUCCESS) {
                    Eprintf("ERROR: invalid hex format for key\n");
                    return 1;
                }
                key_option = true;
            }

            ByteStream  input  = null;
            File        input_file = null;
            ByteStream  output = null;
            Movie       movie  = null;
            Track       audio_track = null;
            SampleDescription sample_description;

            // create the input stream
            result = FileByteStream.Create(argv[args++], FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input (" + D((int)result) + ")\n");
                goto end;
            }

            // create the output stream
            result = FileByteStream.Create(argv[args++], FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output (" + D((int)result) + ")\n");
                goto end;
            }

            // open the file
            input_file = new File(input);

            // get the movie
            movie = input_file.GetMovie();
            if (movie == null) {
                Eprintf("ERROR: no movie in file\n");
                goto end;
            }

            // get the audio track
            audio_track = movie.GetTrack(Track.Type.TYPE_AUDIO);
            if (audio_track == null) {
                Eprintf("ERROR: no audio track found\n");
                goto end;
            }

            // check that the track is of the right type
            sample_description = audio_track.GetSampleDescription(0);
            if (sample_description == null) {
                Eprintf("ERROR: unable to parse sample description\n");
                goto end;
            }

            // show info
            Ap4.Debug("Audio Track:\n");
            Ap4.Debug("  duration: " + audio_track.GetDurationMs().ToString(CultureInfo.InvariantCulture) + " ms\n");
            Ap4.Debug("  sample count: " + audio_track.GetSampleCount().ToString(CultureInfo.InvariantCulture) + "\n");

            switch (sample_description.GetType()) {
                case SampleDescription.Type.TYPE_MPEG: {
                    WriteSamples(audio_track, sample_description, output);
                    return_value = 0;
                    break;
                }

                case SampleDescription.Type.TYPE_PROTECTED:
                    if (!key_option) {
                        Eprintf("ERROR: encrypted tracks require a key\n");
                        return_value = 1;
                        break;
                    }
                    DecryptAndWriteSamples(audio_track, sample_description, key, output);
                    result = 0; // PORT NOTE: as upstream, `result` (not return_value) is set here: the exit code stays 1
                    break;

                default:
                    Eprintf("ERROR: unsupported sample type\n");
                    return_value = 1;
                    break;
            }

        end:
            if (input is IDisposable) ((IDisposable)input).Dispose();
            if (output is IDisposable) ((IDisposable)output).Dispose();

            return return_value;
        }
    }
}
