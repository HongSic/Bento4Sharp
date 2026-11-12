// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp42Avc/Mp42Avc.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Globalization;
using Bento4;

namespace Bento4Sharp.Tools.Mp42Avc
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 To AVC File Converter - Version 1.0\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2009 Axiomatic Systems, LLC";

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

        // PORT NOTE: upstream goes on when the output file could not be opened and
        // crashes when it first uses the NULL stream (segmentation fault, or a trap
        // in the WebAssembly reference build), after whatever messages come before
        // that point. The port follows the same path and stops at the same point,
        // with this exception, which Main turns into exit code 1.
        private sealed class NullStreamException : Exception {}

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp42avc [options] <input> <output>\n" +
                    "  Options:\n" +
                    "  --key <hex>: 128-bit decryption key (in hex: 32 chars)\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   WriteSample
        +---------------------------------------------------------------------*/
        private static void WriteSample(DataBuffer sample_data,
                                        DataBuffer prefix,
                                        uint       nalu_length_size,
                                        ByteStream output)
        {
            // PORT NOTE: `data` is a pointer walking through the sample upstream;
            // here it is an offset in the buffer of sample_data.
            byte[] data_bytes = sample_data.GetData();
            int    data       = 0;
            uint   data_size  = sample_data.GetDataSize();

            // allocate a buffer for the PES packet
            DataBuffer frame_data = new DataBuffer();
            byte[]     frame_buffer;
            int        frame_offset;

            // add a delimiter if we don't already have one
            bool have_access_unit_delimiter = (data_size >  nalu_length_size) && ((data_bytes[nalu_length_size] & 0x1F) == Ap4.AVC_NAL_UNIT_TYPE_ACCESS_UNIT_DELIMITER);
            if (!have_access_unit_delimiter) {
                uint frame_data_size = frame_data.GetDataSize();
                frame_data.SetDataSize(frame_data_size+6);
                frame_buffer = frame_data.UseData();
                frame_offset = (int)frame_data_size;

                // start of access unit
                frame_buffer[frame_offset+0] = 0;
                frame_buffer[frame_offset+1] = 0;
                frame_buffer[frame_offset+2] = 0;
                frame_buffer[frame_offset+3] = 1;
                frame_buffer[frame_offset+4] = 9;    // NAL type = Access Unit Delimiter;
                frame_buffer[frame_offset+5] = 0xE0; // Slice types = ANY
            }

            // write the NAL units
            bool prefix_added = false;
            while (data_size != 0) {
                // sanity check
                if (data_size < nalu_length_size) break;

                // get the next NAL unit
                uint nalu_size;
                if (nalu_length_size == 1) {
                    nalu_size = data_bytes[data++];
                    data_size--;
                } else if (nalu_length_size == 2) {
                    nalu_size = unchecked((uint)Ap4Utils.BytesToInt16BE(data_bytes, data)); // SI16 -> UI32 sign-extends, as in C++
                    data      += 2;
                    data_size -= 2;
                } else if (nalu_length_size == 4) {
                    nalu_size = unchecked((uint)Ap4Utils.BytesToInt32BE(data_bytes, data));
                    data      += 4;
                    data_size -= 4;
                } else {
                    break;
                }
                if (nalu_size > data_size) break;

                // add the prefix if needed
                if (prefix.GetDataSize() != 0 && !prefix_added && !have_access_unit_delimiter) {
                    uint frame_data_size_ = frame_data.GetDataSize();
                    frame_data.SetDataSize(frame_data_size_+prefix.GetDataSize());
                    frame_buffer = frame_data.UseData();
                    frame_offset = (int)frame_data_size_;
                    Array.Copy(prefix.GetData(), 0, frame_buffer, frame_offset, (int)prefix.GetDataSize());
                    prefix_added = true;
                }

                // add a start code before the NAL unit
                {
                    uint frame_data_size = frame_data.GetDataSize();
                    frame_data.SetDataSize(frame_data_size+3+nalu_size);
                    frame_buffer = frame_data.UseData();
                    frame_offset = (int)frame_data_size;
                    frame_buffer[frame_offset+0] = 0;
                    frame_buffer[frame_offset+1] = 0;
                    frame_buffer[frame_offset+2] = 1;
                    Array.Copy(data_bytes, data, frame_buffer, frame_offset+3, (int)nalu_size);
                }

                // add the prefix if needed
                if (prefix.GetDataSize() != 0 && !prefix_added) {
                    uint frame_data_size_ = frame_data.GetDataSize();
                    frame_data.SetDataSize(frame_data_size_+prefix.GetDataSize());
                    frame_buffer = frame_data.UseData();
                    frame_offset = (int)frame_data_size_;
                    Array.Copy(prefix.GetData(), 0, frame_buffer, frame_offset, (int)prefix.GetDataSize());
                    prefix_added = true;
                }

                // move to the next NAL unit
                data      += (int)nalu_size;
                data_size -= nalu_size;
            }

            if (output == null) throw new NullStreamException();
            output.Write(frame_data.GetData(), frame_data.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   MakeFramePrefix
        +---------------------------------------------------------------------*/
        private static Ap4Result MakeFramePrefix(SampleDescription sdesc, DataBuffer prefix, ref uint nalu_length_size)
        {
            AvcSampleDescription avc_desc = sdesc as AvcSampleDescription;
            if (avc_desc == null) {
                Eprintf("ERROR: track does not contain an AVC stream\n");
                return Ap4Result.FAILURE;
            }

            if (sdesc.GetFormat() == Ap4.SAMPLE_FORMAT_AVC3 ||
                sdesc.GetFormat() == Ap4.SAMPLE_FORMAT_AVC4 ||
                sdesc.GetFormat() == Ap4.SAMPLE_FORMAT_DVAV) {
                // no need for a prefix, SPS/PPS NALs should be in the elementary stream already
                return Ap4Result.SUCCESS;
            }

            // make the SPS/PPS prefix
            nalu_length_size = avc_desc.GetNaluLengthSize();
            for (int i=0; i<avc_desc.GetSequenceParameters().Count; i++) {
                DataBuffer buffer = avc_desc.GetSequenceParameters()[i];
                uint prefix_size = prefix.GetDataSize();
                prefix.SetDataSize(prefix_size+4+buffer.GetDataSize());
                byte[] p_bytes = prefix.UseData();
                int    p = (int)prefix_size;
                p_bytes[p++] = 0;
                p_bytes[p++] = 0;
                p_bytes[p++] = 0;
                p_bytes[p++] = 1;
                Array.Copy(buffer.GetData(), 0, p_bytes, p, (int)buffer.GetDataSize());
            }
            for (int i=0; i<avc_desc.GetPictureParameters().Count; i++) {
                DataBuffer buffer = avc_desc.GetPictureParameters()[i];
                uint prefix_size = prefix.GetDataSize();
                prefix.SetDataSize(prefix_size+4+buffer.GetDataSize());
                byte[] p_bytes = prefix.UseData();
                int    p = (int)prefix_size;
                p_bytes[p++] = 0;
                p_bytes[p++] = 0;
                p_bytes[p++] = 0;
                p_bytes[p++] = 1;
                Array.Copy(buffer.GetData(), 0, p_bytes, p, (int)buffer.GetDataSize());
            }

            return Ap4Result.SUCCESS;
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

            // get the original sample description and make the prefix
            SampleDescription orig_sdesc = pdesc.GetOriginalSampleDescription();
            uint       nalu_length_size = 0;
            DataBuffer prefix = new DataBuffer();
            if (MakeFramePrefix(orig_sdesc, prefix, ref nalu_length_size).Failed()) {
                return;
            }

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
                WriteSample(decrypted_data, prefix, nalu_length_size, output);
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
            // make the frame prefix
            uint       nalu_length_size = 0;
            DataBuffer prefix = new DataBuffer();
            if (MakeFramePrefix(sdesc, prefix, ref nalu_length_size).Failed()) {
                return;
            }

            Sample     sample = new Sample();
            DataBuffer data = new DataBuffer();
            uint       index = 0;
            while (track.ReadSample(index, sample, data).Succeeded()) {
                WriteSample(data, prefix, nalu_length_size, output);
                index++;
            }
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] argv)
        {
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

            // create the input stream
            ByteStream input = null;
            result = FileByteStream.Create(argv[args++], FileByteStream.Mode.STREAM_MODE_READ, out input);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open input (" + D((int)result) + ")\n");
            }

            // create the output stream
            ByteStream output = null;
            result = FileByteStream.Create(argv[args++], FileByteStream.Mode.STREAM_MODE_WRITE, out output);
            if (result.Failed()) {
                Eprintf("ERROR: cannot open output (" + D((int)result) + ")\n");
            }

            // PORT NOTE: upstream does not stop when a stream could not be opened
            // and then dereferences the NULL pointer (a crash: segmentation fault,
            // or a trap in the WebAssembly reference build). The port stops here
            // with a non-zero exit code instead of crashing.
            if (input == null) {
                if (output is IDisposable) ((IDisposable)output).Dispose();
                return 1;
            }

            // open the file
            File input_file = new File(input);

            // get the movie
            SampleDescription sample_description;
            Track video_track;
            Movie movie = input_file.GetMovie();
            if (movie == null) {
                Eprintf("ERROR: no movie in file\n");
                goto end;
            }

            // get the video track
            video_track = movie.GetTrack(Track.Type.TYPE_VIDEO);
            if (video_track == null) {
                Eprintf("ERROR: no video track found\n");
                goto end;
            }

            // check that the track is of the right type
            sample_description = video_track.GetSampleDescription(0);
            if (sample_description == null) {
                Eprintf("ERROR: unable to parse sample description\n");
                goto end;
            }

            // show info
            Ap4.Debug("Video Track:\n");
            Ap4.Debug("  duration: " + video_track.GetDurationMs().ToString(CultureInfo.InvariantCulture) + " ms\n");
            Ap4.Debug("  sample count: " + video_track.GetSampleCount().ToString(CultureInfo.InvariantCulture) + "\n");

            try {
            switch (sample_description.GetType()) {
                case SampleDescription.Type.TYPE_AVC:
                    WriteSamples(video_track, sample_description, output);
                    break;

                case SampleDescription.Type.TYPE_PROTECTED:
                    if (!key_option) {
                        Eprintf("ERROR: encrypted tracks require a key\n");
                        goto end;
                    }
                    DecryptAndWriteSamples(video_track, sample_description, key, output);
                    break;

                default:
                    Eprintf("ERROR: unsupported sample type\n");
                    break;
            }
            } catch (NullStreamException) {
                // PORT NOTE: the point where upstream crashes on the NULL output (see NullStreamException)
                if (input is IDisposable) ((IDisposable)input).Dispose();
                return 1;
            }

        end:
            if (input is IDisposable) ((IDisposable)input).Dispose();
            if (output is IDisposable) ((IDisposable)output).Dispose();

            // PORT NOTE: output->Release() on a NULL pointer crashes upstream
            if (output == null) return 1;

            return 0;
        }
    }
}
