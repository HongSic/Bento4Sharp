// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Mpeg2Ts.h, Ap4Mpeg2Ts.cpp
// Original work Copyright 2002-2015 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const ushort MPEG2_TS_DEFAULT_PID_PMT            = 0x100;
        public const ushort MPEG2_TS_DEFAULT_PID_AUDIO          = 0x101;
        public const ushort MPEG2_TS_DEFAULT_PID_VIDEO          = 0x102;
        public const ushort MPEG2_TS_DEFAULT_STREAM_ID_AUDIO    = 0xc0;
        public const ushort MPEG2_TS_DEFAULT_STREAM_ID_VIDEO    = 0xe0;
        public const ushort MPEG2_TS_STREAM_ID_PRIVATE_STREAM_1 = 0xbd;

        public const byte MPEG2_STREAM_TYPE_ISO_IEC_13818_1_PES = 0x06;
        public const byte MPEG2_STREAM_TYPE_ISO_IEC_13818_7     = 0x0F;
        public const byte MPEG2_STREAM_TYPE_AVC                 = 0x1B;
        public const byte MPEG2_STREAM_TYPE_HEVC                = 0x24;
        public const byte MPEG2_STREAM_TYPE_ATSC_AC3            = 0x81;
        public const byte MPEG2_STREAM_TYPE_ATSC_EAC3           = 0x87;

        public const ulong MPEG2_TS_DEFAULT_PCR_OFFSET = 10000;
    }

    /*----------------------------------------------------------------------
    |   file-local constants and functions of Ap4Mpeg2Ts.cpp
    +---------------------------------------------------------------------*/
    internal static class Mpeg2TsUtils
    {
        public const uint MPEG2TS_PACKET_SIZE         = 188;
        public const uint MPEG2TS_PACKET_PAYLOAD_SIZE = 184;
        public const uint MPEG2TS_SYNC_BYTE           = 0x47;
        public const uint MPEG2TS_PCR_ADAPTATION_SIZE = 6;

        public static readonly byte[] StuffingBytes = MakeStuffingBytes();

        private static byte[] MakeStuffingBytes()
        {
            // PORT NOTE: the 188 literal 0xFF bytes of the upstream table
            byte[] bytes = new byte[MPEG2TS_PACKET_SIZE];
#if NET6_0_OR_GREATER
            Array.Fill(bytes, (byte)0xFF);
#else
            for (int i = 0; i < bytes.Length; i++) bytes[i] = 0xFF;
#endif
            return bytes;
        }

        // PORT NOTE: Ap4Mpeg2Ts.cpp defines its own copies of these constants
        public const uint HEVC_NALU_TYPE_VPS_NUT        = 32;
        public const uint HEVC_NALU_TYPE_SPS_NUT        = 33;
        public const uint HEVC_NALU_TYPE_PPS_NUT        = 34;

        /*----------------------------------------------------------------------
        |   GetSamplingFrequencyIndex
        +---------------------------------------------------------------------*/
        public static uint
        GetSamplingFrequencyIndex(uint sampling_frequency)
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
        |   MakeAdtsHeader
        +---------------------------------------------------------------------*/
        public static void
        MakeAdtsHeader(byte[] bits,
                       uint   frame_size,
                       uint   sampling_frequency_index,
                       uint   channel_configuration)
        {
            bits[0] = 0xFF;
            bits[1] = 0xF1; // 0xF9 (MPEG2)
            bits[2] = (byte)(0x40 | (sampling_frequency_index << 2) | (channel_configuration >> 2));
            bits[3] = (byte)(((channel_configuration&0x3)<<6) | ((frame_size+7) >> 11));
            bits[4] = (byte)(((frame_size+7) >> 3)&0xFF);
            bits[5] = (byte)((((frame_size+7) << 5)&0xFF) | 0x1F);
            bits[6] = 0xFC;

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
        |   CRC_Table
        +---------------------------------------------------------------------*/
        private static readonly uint[] CRC_Table = new uint[256] {
            0x00000000, 0x04c11db7, 0x09823b6e, 0x0d4326d9, 0x130476dc, 0x17c56b6b,
            0x1a864db2, 0x1e475005, 0x2608edb8, 0x22c9f00f, 0x2f8ad6d6, 0x2b4bcb61,
            0x350c9b64, 0x31cd86d3, 0x3c8ea00a, 0x384fbdbd, 0x4c11db70, 0x48d0c6c7,
            0x4593e01e, 0x4152fda9, 0x5f15adac, 0x5bd4b01b, 0x569796c2, 0x52568b75,
            0x6a1936c8, 0x6ed82b7f, 0x639b0da6, 0x675a1011, 0x791d4014, 0x7ddc5da3,
            0x709f7b7a, 0x745e66cd, 0x9823b6e0, 0x9ce2ab57, 0x91a18d8e, 0x95609039,
            0x8b27c03c, 0x8fe6dd8b, 0x82a5fb52, 0x8664e6e5, 0xbe2b5b58, 0xbaea46ef,
            0xb7a96036, 0xb3687d81, 0xad2f2d84, 0xa9ee3033, 0xa4ad16ea, 0xa06c0b5d,
            0xd4326d90, 0xd0f37027, 0xddb056fe, 0xd9714b49, 0xc7361b4c, 0xc3f706fb,
            0xceb42022, 0xca753d95, 0xf23a8028, 0xf6fb9d9f, 0xfbb8bb46, 0xff79a6f1,
            0xe13ef6f4, 0xe5ffeb43, 0xe8bccd9a, 0xec7dd02d, 0x34867077, 0x30476dc0,
            0x3d044b19, 0x39c556ae, 0x278206ab, 0x23431b1c, 0x2e003dc5, 0x2ac12072,
            0x128e9dcf, 0x164f8078, 0x1b0ca6a1, 0x1fcdbb16, 0x018aeb13, 0x054bf6a4,
            0x0808d07d, 0x0cc9cdca, 0x7897ab07, 0x7c56b6b0, 0x71159069, 0x75d48dde,
            0x6b93dddb, 0x6f52c06c, 0x6211e6b5, 0x66d0fb02, 0x5e9f46bf, 0x5a5e5b08,
            0x571d7dd1, 0x53dc6066, 0x4d9b3063, 0x495a2dd4, 0x44190b0d, 0x40d816ba,
            0xaca5c697, 0xa864db20, 0xa527fdf9, 0xa1e6e04e, 0xbfa1b04b, 0xbb60adfc,
            0xb6238b25, 0xb2e29692, 0x8aad2b2f, 0x8e6c3698, 0x832f1041, 0x87ee0df6,
            0x99a95df3, 0x9d684044, 0x902b669d, 0x94ea7b2a, 0xe0b41de7, 0xe4750050,
            0xe9362689, 0xedf73b3e, 0xf3b06b3b, 0xf771768c, 0xfa325055, 0xfef34de2,
            0xc6bcf05f, 0xc27dede8, 0xcf3ecb31, 0xcbffd686, 0xd5b88683, 0xd1799b34,
            0xdc3abded, 0xd8fba05a, 0x690ce0ee, 0x6dcdfd59, 0x608edb80, 0x644fc637,
            0x7a089632, 0x7ec98b85, 0x738aad5c, 0x774bb0eb, 0x4f040d56, 0x4bc510e1,
            0x46863638, 0x42472b8f, 0x5c007b8a, 0x58c1663d, 0x558240e4, 0x51435d53,
            0x251d3b9e, 0x21dc2629, 0x2c9f00f0, 0x285e1d47, 0x36194d42, 0x32d850f5,
            0x3f9b762c, 0x3b5a6b9b, 0x0315d626, 0x07d4cb91, 0x0a97ed48, 0x0e56f0ff,
            0x1011a0fa, 0x14d0bd4d, 0x19939b94, 0x1d528623, 0xf12f560e, 0xf5ee4bb9,
            0xf8ad6d60, 0xfc6c70d7, 0xe22b20d2, 0xe6ea3d65, 0xeba91bbc, 0xef68060b,
            0xd727bbb6, 0xd3e6a601, 0xdea580d8, 0xda649d6f, 0xc423cd6a, 0xc0e2d0dd,
            0xcda1f604, 0xc960ebb3, 0xbd3e8d7e, 0xb9ff90c9, 0xb4bcb610, 0xb07daba7,
            0xae3afba2, 0xaafbe615, 0xa7b8c0cc, 0xa379dd7b, 0x9b3660c6, 0x9ff77d71,
            0x92b45ba8, 0x9675461f, 0x8832161a, 0x8cf30bad, 0x81b02d74, 0x857130c3,
            0x5d8a9099, 0x594b8d2e, 0x5408abf7, 0x50c9b640, 0x4e8ee645, 0x4a4ffbf2,
            0x470cdd2b, 0x43cdc09c, 0x7b827d21, 0x7f436096, 0x7200464f, 0x76c15bf8,
            0x68860bfd, 0x6c47164a, 0x61043093, 0x65c52d24, 0x119b4be9, 0x155a565e,
            0x18197087, 0x1cd86d30, 0x029f3d35, 0x065e2082, 0x0b1d065b, 0x0fdc1bec,
            0x3793a651, 0x3352bbe6, 0x3e119d3f, 0x3ad08088, 0x2497d08d, 0x2056cd3a,
            0x2d15ebe3, 0x29d4f654, 0xc5a92679, 0xc1683bce, 0xcc2b1d17, 0xc8ea00a0,
            0xd6ad50a5, 0xd26c4d12, 0xdf2f6bcb, 0xdbee767c, 0xe3a1cbc1, 0xe760d676,
            0xea23f0af, 0xeee2ed18, 0xf0a5bd1d, 0xf464a0aa, 0xf9278673, 0xfde69bc4,
            0x89b8fd09, 0x8d79e0be, 0x803ac667, 0x84fbdbd0, 0x9abc8bd5, 0x9e7d9662,
            0x933eb0bb, 0x97ffad0c, 0xafb010b1, 0xab710d06, 0xa6322bdf, 0xa2f33668,
            0xbcb4666d, 0xb8757bda, 0xb5365d03, 0xb1f740b4
        };

        /*----------------------------------------------------------------------
        |   ComputeCRC
        +---------------------------------------------------------------------*/
        public static uint
        ComputeCRC(byte[] data, int data_offset, uint data_size)
        {
            uint crc = 0xFFFFFFFF;

            for (uint i=0; i<data_size; i++) {
                crc = (crc << 8) ^ CRC_Table[((crc >> 24) ^ data[data_offset++]) & 0xFF];
            }

            return crc;
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_Mpeg2TsWriter
    +---------------------------------------------------------------------*/
    /// <summary>
    /// This class is a simple implementation of a converter that can
    /// convert MP4 audio and video access units into an MPEG2 transport
    /// stream.
    /// It currently only supports one audio tracks with MPEG4 AAC LC, and one
    /// video track with MPEG4 AVC.
    /// </summary>
    public class Mpeg2TsWriter
    {
        // classes
        public class Stream {
            public Stream(ushort pid) { _PID = pid; _continuityCounter = 0; }

            public ushort GetPID() { return _PID; }

            /*----------------------------------------------------------------------
            |   AP4_Mpeg2TsWriter::Stream::WritePacketHeader
            +---------------------------------------------------------------------*/
            public void WritePacketHeader(bool       payload_start,
                                          ref uint   payload_size,
                                          bool       with_pcr,
                                          ulong      pcr,
                                          ByteStream output)
            {
                byte[] header = new byte[4];
                header[0] = (byte)Mpeg2TsUtils.MPEG2TS_SYNC_BYTE;
                header[1] = (byte)(((payload_start?1:0)<<6) | (_PID >> 8));
                header[2] = (byte)(_PID & 0xFF);

                uint adaptation_field_size = 0;
                if (with_pcr) adaptation_field_size += 2+Mpeg2TsUtils.MPEG2TS_PCR_ADAPTATION_SIZE;

                // clamp the payload size
                if (payload_size+adaptation_field_size > Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE) {
                    payload_size = Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE-adaptation_field_size;
                }

                // adjust the adaptation field to include stuffing if necessary
                if (adaptation_field_size+payload_size < Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE) {
                    adaptation_field_size = Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE-payload_size;
                }

                if (adaptation_field_size == 0) {
                    // no adaptation field
                    header[3] = (byte)((1<<4) | ((_continuityCounter++)&0x0F));
                    output.Write(header, 4);
                } else {
                    // adaptation field present
                    header[3] = (byte)((3<<4) | ((_continuityCounter++)&0x0F));
                    output.Write(header, 4);

                    if (adaptation_field_size == 1) {
                        // just one byte (stuffing)
                        output.WriteUI08(0);
                    } else {
                        // two or more bytes (stuffing and/or PCR)
                        output.WriteUI08((byte)(adaptation_field_size-1));
                        output.WriteUI08((byte)(with_pcr?(1<<4):0));
                        uint pcr_size = 0;
                        if (with_pcr) {
                            pcr_size = Mpeg2TsUtils.MPEG2TS_PCR_ADAPTATION_SIZE;
                            ulong pcr_base = pcr/300;
                            uint  pcr_ext  = (uint)(pcr%300);
                            BitWriter writer = new BitWriter(pcr_size);
                            writer.Write((uint)(pcr_base>>32), 1);
                            writer.Write((uint)pcr_base, 32);
                            writer.Write(0x3F, 6);
                            writer.Write(pcr_ext, 9);
                            output.Write(writer.GetData(), pcr_size);
                        }
                        if (adaptation_field_size > 2) {
                            output.Write(Mpeg2TsUtils.StuffingBytes, adaptation_field_size-pcr_size-2);
                        }
                    }
                }
            }

            private ushort _PID;
            private uint   _continuityCounter;
        }

        public abstract class SampleStream : Stream {
            public SampleStream(ushort  pid,
                                byte    stream_type,
                                ushort  stream_id,
                                uint    timescale,
                                byte[]  descriptor,
                                uint    descriptor_length,
                                ulong   pcr_offset = Ap4.MPEG2_TS_DEFAULT_PCR_OFFSET) :
                base(pid)
            {
                _streamType = stream_type;
                _streamId = stream_id;
                _timeScale = timescale;
                _pcrOffset = pcr_offset;
                if (descriptor != null && descriptor_length != 0) {
                    _descriptor.SetData(descriptor, descriptor_length);
                }
            }

            /*----------------------------------------------------------------------
            |   AP4_Mpeg2TsWriter::SampleStream::WritePES
            +---------------------------------------------------------------------*/
            public virtual Ap4Result WritePES(byte[]     data,
                                              uint       data_size,
                                              ulong      dts,
                                              bool       with_dts,
                                              ulong      pts,
                                              bool       with_pcr,
                                              ByteStream output)
            {
                // PORT NOTE: `data` is advanced as a pointer upstream
                int data_pos = 0;

                // ISO/IEC 13818-1 section 2.7.5 says a DTS shall appear only if the
                // decoding time differs from the presentation time.
                if (with_dts && (dts == pts)) {
                   with_dts = false;
                }

                uint pes_header_size = 14+(with_dts?5u:0u);
                BitWriter pes_header = new BitWriter(pes_header_size);

                // adjust the base timestamp, offset from the PCR
                dts += _pcrOffset;
                pts += _pcrOffset;

                pes_header.Write(0x000001, 24);    // packet_start_code_prefix
                pes_header.Write(_streamId, 8);    // stream_id
                pes_header.Write(_streamId == Ap4.MPEG2_TS_DEFAULT_STREAM_ID_VIDEO?0:(data_size+pes_header_size-6), 16); // PES_packet_length
                pes_header.Write(2, 2);            // '01'
                pes_header.Write(0, 2);            // PES_scrambling_control
                pes_header.Write(0, 1);            // PES_priority
                pes_header.Write(1, 1);            // data_alignment_indicator
                pes_header.Write(0, 1);            // copyright
                pes_header.Write(0, 1);            // original_or_copy
                pes_header.Write(with_dts?3u:2u, 2); // PTS_DTS_flags
                pes_header.Write(0, 1);            // ESCR_flag
                pes_header.Write(0, 1);            // ES_rate_flag
                pes_header.Write(0, 1);            // DSM_trick_mode_flag
                pes_header.Write(0, 1);            // additional_copy_info_flag
                pes_header.Write(0, 1);            // PES_CRC_flag
                pes_header.Write(0, 1);            // PES_extension_flag
                pes_header.Write(pes_header_size-9, 8);// PES_header_data_length

                pes_header.Write(with_dts?3u:2u, 4);       // '0010' or '0011'
                pes_header.Write((uint)(pts>>30), 3);      // PTS[32..30]
                pes_header.Write(1, 1);                    // marker_bit
                pes_header.Write((uint)(pts>>15), 15);     // PTS[29..15]
                pes_header.Write(1, 1);                    // marker_bit
                pes_header.Write((uint)pts, 15);           // PTS[14..0]
                pes_header.Write(1, 1);                    // market_bit

                if (with_dts) {
                    pes_header.Write(1, 4);                    // '0001'
                    pes_header.Write((uint)(dts>>30), 3);      // DTS[32..30]
                    pes_header.Write(1, 1);                    // marker_bit
                    pes_header.Write((uint)(dts>>15), 15);     // DTS[29..15]
                    pes_header.Write(1, 1);                    // marker_bit
                    pes_header.Write((uint)dts, 15);           // DTS[14..0]
                    pes_header.Write(1, 1);                    // market_bit
                }

                bool first_packet = true;
                data_size += pes_header_size; // add size of PES header
                while (data_size != 0) {
                    uint payload_size = data_size;
                    if (payload_size > Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE) payload_size = Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE;

                    if (first_packet)  {
                        WritePacketHeader(first_packet, ref payload_size, with_pcr, ((with_dts?dts:pts)-_pcrOffset)*300, output);
                        first_packet = false;
                        output.Write(pes_header.GetData(), pes_header_size);
                        output.Write(data, data_pos, payload_size-pes_header_size);
                        data_pos += (int)(payload_size-pes_header_size);
                    } else {
                        WritePacketHeader(first_packet, ref payload_size, false, 0, output);
                        output.Write(data, data_pos, payload_size);
                        data_pos += (int)payload_size;
                    }
                    data_size -= payload_size;
                }

                return Ap4Result.SUCCESS;
            }

            public abstract Ap4Result WriteSample(Sample             sample,
                                                  DataBuffer         sample_data,
                                                  SampleDescription  sample_description,
                                                  bool               with_pcr,
                                                  ByteStream         output);

            /*----------------------------------------------------------------------
            |   AP4_Mpeg2TsWriter::SampleStream::WriteSample
            +---------------------------------------------------------------------*/
            public Ap4Result WriteSample(Sample             sample,
                                         SampleDescription  sample_description,
                                         bool               with_pcr,
                                         ByteStream         output)
            {
                DataBuffer sample_data = new DataBuffer();
                Ap4Result result = sample.ReadData(sample_data);
                if (result.Failed()) return result;
                return WriteSample(sample,
                                   sample_data,
                                   sample_description,
                                   with_pcr,
                                   output);
            }

            public void SetType(byte type) {_streamType = type;}

            public void SetDescriptor(byte[] descriptor, uint descriptor_length) {
                if (descriptor != null && descriptor_length != 0) {
                    _descriptor.SetData(descriptor, descriptor_length);
                }
            }

            public byte       _streamType;
            public ushort     _streamId;
            public uint       _timeScale;
            public DataBuffer _descriptor = new DataBuffer();
            public ulong      _pcrOffset;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mpeg2TsWriter::AP4_Mpeg2TsWriter
        +---------------------------------------------------------------------*/
        // constructor
        public Mpeg2TsWriter(ushort pmt_pid = Ap4.MPEG2_TS_DEFAULT_PID_PMT)
        {
            _audio = null;
            _video = null;
            _PAT = new Stream(0);
            _PMT = new Stream(pmt_pid);
        }

        public Stream GetPAT() { return _PAT; }
        public Stream GetPMT() { return _PMT; }

        /*----------------------------------------------------------------------
        |   AP4_Mpeg2TsWriter::WritePAT
        +---------------------------------------------------------------------*/
        public Ap4Result
        WritePAT(ByteStream output)
        {
            uint payload_size = Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE;
            _PAT.WritePacketHeader(true, ref payload_size, false, 0, output);

            BitWriter writer = new BitWriter(1024);

            writer.Write(0, 8);  // pointer
            writer.Write(0, 8);  // table_id
            writer.Write(1, 1);  // section_syntax_indicator
            writer.Write(0, 1);  // '0'
            writer.Write(3, 2);  // reserved
            writer.Write(13, 12);// section_length
            writer.Write(1, 16); // transport_stream_id
            writer.Write(3, 2);  // reserved
            writer.Write(0, 5);  // version_number
            writer.Write(1, 1);  // current_next_indicator
            writer.Write(0, 8);  // section_number
            writer.Write(0, 8);  // last_section_number
            writer.Write(1, 16); // program number
            writer.Write(7, 3);  // reserved
            writer.Write(_PMT.GetPID(), 13); // program_map_PID
            writer.Write(Mpeg2TsUtils.ComputeCRC(writer.GetData(), 1, 17-1-4), 32);

            output.Write(writer.GetData(), 17);

            output.Write(Mpeg2TsUtils.StuffingBytes, Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE-17);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mpeg2TsWriter::WritePMT
        +---------------------------------------------------------------------*/
        public Ap4Result
        WritePMT(ByteStream output)
        {
            // check that we have at least one media stream
            if (_audio == null && _video == null) {
                return Ap4Result.ERROR_INVALID_STATE;
            }

            uint payload_size = Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE;
            _PMT.WritePacketHeader(true, ref payload_size, false, 0, output);

            BitWriter writer = new BitWriter(1024);

            uint section_length = 13;
            uint pcr_pid = 0;
            if (_audio != null) {
                section_length += 5+_audio._descriptor.GetDataSize();
                pcr_pid = _audio.GetPID();
            }
            if (_video != null) {
                section_length += 5+_video._descriptor.GetDataSize();
                pcr_pid = _video.GetPID();
            }

            writer.Write(0, 8);        // pointer
            writer.Write(2, 8);        // table_id
            writer.Write(1, 1);        // section_syntax_indicator
            writer.Write(0, 1);        // '0'
            writer.Write(3, 2);        // reserved
            writer.Write(section_length, 12); // section_length
            writer.Write(1, 16);       // program_number
            writer.Write(3, 2);        // reserved
            writer.Write(0, 5);        // version_number
            writer.Write(1, 1);        // current_next_indicator
            writer.Write(0, 8);        // section_number
            writer.Write(0, 8);        // last_section_number
            writer.Write(7, 3);        // reserved
            writer.Write(pcr_pid, 13); // PCD_PID
            writer.Write(0xF, 4);      // reserved
            writer.Write(0, 12);       // program_info_length

            if (_audio != null) {
                writer.Write(_audio._streamType, 8);                // stream_type
                writer.Write(0x7, 3);                               // reserved
                writer.Write(_audio.GetPID(), 13);                  // elementary_PID
                writer.Write(0xF, 4);                               // reserved
                writer.Write(_audio._descriptor.GetDataSize(), 12); // ES_info_length
                for (uint i=0; i<_audio._descriptor.GetDataSize(); i++) {
                    writer.Write(_audio._descriptor.GetData()[i], 8);
                }
            }

            if (_video != null) {
                writer.Write(_video._streamType, 8);                // stream_type
                writer.Write(0x7, 3);                               // reserved
                writer.Write(_video.GetPID(), 13);                  // elementary_PID
                writer.Write(0xF, 4);                               // reserved
                writer.Write(_video._descriptor.GetDataSize(), 12); // ES_info_length
                for (uint i=0; i<_video._descriptor.GetDataSize(); i++) {
                    writer.Write(_video._descriptor.GetData()[i], 8);
                }
            }

            writer.Write(Mpeg2TsUtils.ComputeCRC(writer.GetData(), 1, section_length-1), 32); // CRC

            output.Write(writer.GetData(), section_length+4);
            output.Write(Mpeg2TsUtils.StuffingBytes, Mpeg2TsUtils.MPEG2TS_PACKET_PAYLOAD_SIZE-(section_length+4));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mpeg2TsWriter::SetAudioStream
        +---------------------------------------------------------------------*/
        public Ap4Result
        SetAudioStream(uint              timescale,
                       byte              stream_type,
                       ushort            stream_id,
                       out SampleStream  stream,
                       ushort            pid = Ap4.MPEG2_TS_DEFAULT_PID_AUDIO,
                       byte[]            descriptor = null,
                       uint              descriptor_length = 0,
                       ulong             pcr_offset = Ap4.MPEG2_TS_DEFAULT_PCR_OFFSET)
        {
            // default
            stream = null;

            Ap4Result result = Mpeg2TsAudioSampleStream.Create(pid,
                                                               timescale,
                                                               stream_type,
                                                               stream_id,
                                                               out _audio,
                                                               descriptor,
                                                               descriptor_length,
                                                               pcr_offset);
            if (result.Failed()) return result;
            stream = _audio;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mpeg2TsWriter::SetVideoStream
        +---------------------------------------------------------------------*/
        public Ap4Result
        SetVideoStream(uint              timescale,
                       byte              stream_type,
                       ushort            stream_id,
                       out SampleStream  stream,
                       ushort            pid = Ap4.MPEG2_TS_DEFAULT_PID_VIDEO,
                       byte[]            descriptor = null,
                       uint              descriptor_length = 0,
                       ulong             pcr_offset = Ap4.MPEG2_TS_DEFAULT_PCR_OFFSET)
        {
            // default
            stream = null;

            Ap4Result result = Mpeg2TsVideoSampleStream.Create(pid,
                                                               timescale,
                                                               stream_type,
                                                               stream_id,
                                                               out _video,
                                                               descriptor,
                                                               descriptor_length,
                                                               pcr_offset);
            if (result.Failed()) return result;
            stream = _video;
            return Ap4Result.SUCCESS;
        }

        private Stream        _PAT;
        private Stream        _PMT;
        private SampleStream  _audio;
        private SampleStream _video;
    }

    /*----------------------------------------------------------------------
    |   AP4_Mpeg2TsAudioSampleStream
    +---------------------------------------------------------------------*/
    internal class Mpeg2TsAudioSampleStream : Mpeg2TsWriter.SampleStream
    {
        /*----------------------------------------------------------------------
        |   AP4_Mpeg2TsAudioSampleStream::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result
        Create(ushort                          pid,
               uint                            timescale,
               byte                            stream_type,
               ushort                          stream_id,
               out Mpeg2TsWriter.SampleStream  stream,
               byte[]                          descriptor,
               uint                            descriptor_length,
               ulong                           pcr_offset)
        {
            stream = new Mpeg2TsAudioSampleStream(pid, timescale, stream_type, stream_id, descriptor, descriptor_length, pcr_offset);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Mpeg2TsAudioSampleStream::WriteSample
        +---------------------------------------------------------------------*/
        public override Ap4Result
        WriteSample(Sample             sample,
                    DataBuffer         sample_data,
                    SampleDescription  sample_description,
                    bool               with_pcr,
                    ByteStream         output)
        {
            if (sample_description == null) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            // check the sample description
            if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_MP4A) {
                MpegAudioSampleDescription audio_desc = sample_description as MpegAudioSampleDescription;

                if (audio_desc == null) return Ap4Result.ERROR_NOT_SUPPORTED;
                if (audio_desc.GetMpeg4AudioObjectType() != Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_LC   &&
                    audio_desc.GetMpeg4AudioObjectType() != Ap4.MPEG4_AUDIO_OBJECT_TYPE_AAC_MAIN &&
                    audio_desc.GetMpeg4AudioObjectType() != Ap4.MPEG4_AUDIO_OBJECT_TYPE_SBR      &&
                    audio_desc.GetMpeg4AudioObjectType() != Ap4.MPEG4_AUDIO_OBJECT_TYPE_PS) {
                    return Ap4Result.ERROR_NOT_SUPPORTED;
                }

                uint sample_rate   = audio_desc.GetSampleRate();
                uint channel_count = audio_desc.GetChannelCount();
                DataBuffer dsi     = audio_desc.GetDecoderInfo();
                if (dsi.GetDataSize() != 0) {
                    Mp4AudioDecoderConfig dec_config = new Mp4AudioDecoderConfig();
                    Ap4Result result = dec_config.Parse(dsi.GetData(), dsi.GetDataSize());
                    if (result.Succeeded()) {
                        sample_rate = dec_config._samplingFrequency;
                        channel_count = dec_config._channelCount;
                    }
                }
                uint sampling_frequency_index = Mpeg2TsUtils.GetSamplingFrequencyIndex(sample_rate);
                uint channel_configuration    = channel_count;

                // PORT NOTE: upstream allocates 7+sample_data.GetDataSize() bytes but
                // passes 7+sample.GetSize() to WritePES (it would read past the end of
                // the buffer if the two sizes differed); the buffer is sized for the
                // larger of the two so that case writes zeros instead of throwing.
                byte[] buffer = new byte[7+Math.Max(sample_data.GetDataSize(), sample.GetSize())];
                Mpeg2TsUtils.MakeAdtsHeader(buffer, sample_data.GetDataSize(), sampling_frequency_index, channel_configuration);
#if !NETSTANDARD1_3
                Array.Copy(sample_data.GetData(), 0, buffer, 7, sample_data.GetDataSize());
#else
                Compat.ArrayCopy(sample_data.GetData(), 0, buffer, 7, sample_data.GetDataSize());
#endif
                ulong ts = Ap4Utils.ConvertTime(sample.GetDts(), _timeScale, 90000);
                WritePES(buffer, 7+sample.GetSize(), ts, false, ts, with_pcr, output);
            } else if (sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AC_3 ||
                       sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_EC_3 ||
                       sample_description.GetFormat() == Ap4.SAMPLE_FORMAT_AC_4) {
                ulong ts = Ap4Utils.ConvertTime(sample.GetDts(), _timeScale, 90000);
                WritePES(sample_data.GetData(), sample_data.GetDataSize(), ts, false, ts, with_pcr, output);
            } else {
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }


            return Ap4Result.SUCCESS;
        }

        private Mpeg2TsAudioSampleStream(ushort  pid,
                                         uint    timescale,
                                         byte    stream_type,
                                         ushort  stream_id,
                                         byte[]  descriptor,
                                         uint    descriptor_length,
                                         ulong   pcr_offset) :
            base(pid,
                 stream_type,
                 stream_id,
                 timescale,
                 descriptor,
                 descriptor_length,
                 pcr_offset) {}
    }

    /*----------------------------------------------------------------------
    |   AP4_Mpeg2TsVideoSampleStream
    +---------------------------------------------------------------------*/
    internal class Mpeg2TsVideoSampleStream : Mpeg2TsWriter.SampleStream
    {
        /*----------------------------------------------------------------------
        |   AP4_Mpeg2TsVideoSampleStream::AP4_Mpeg2TsVideoSampleStream
        +---------------------------------------------------------------------*/
        public static Ap4Result
        Create(ushort                          pid,
               uint                            timescale,
               byte                            stream_type,
               ushort                          stream_id,
               out Mpeg2TsWriter.SampleStream  stream,
               byte[]                          descriptor,
               uint                            descriptor_length,
               ulong                           pcr_offset = Ap4.MPEG2_TS_DEFAULT_PCR_OFFSET)
        {
            // create the stream object
            stream = new Mpeg2TsVideoSampleStream(pid, timescale, stream_type, stream_id, descriptor, descriptor_length, pcr_offset);
            return Ap4Result.SUCCESS;
        }

        // PORT NOTE: helper for the block that upstream repeats for every
        // parameter set: grow the prefix by a 00 00 00 01 start code + the NALU.
        private void AppendToPrefix(DataBuffer buffer)
        {
            uint prefix_size = _prefix.GetDataSize();
            _prefix.SetDataSize(prefix_size+4+buffer.GetDataSize());
            byte[] p = _prefix.UseData();
            int    p_pos = (int)prefix_size;
            p[p_pos++] = 0;
            p[p_pos++] = 0;
            p[p_pos++] = 0;
            p[p_pos++] = 1;
#if !NETSTANDARD1_3
            Array.Copy(buffer.GetData(), 0, p, p_pos, buffer.GetDataSize());
#else
            Compat.ArrayCopy(buffer.GetData(), 0, p, p_pos, buffer.GetDataSize());
#endif
        }

        /*----------------------------------------------------------------------
        |   AP4_Mpeg2TsVideoSampleStream::WriteSample
        +---------------------------------------------------------------------*/
        public override Ap4Result
        WriteSample(Sample             sample,
                    DataBuffer         sample_data,
                    SampleDescription  sample_description,
                    bool               with_pcr,
                    ByteStream         output)
        {
            if (sample_description == null) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            if (sample_description.GetType() == SampleDescription.Type.TYPE_AVC) {
                // check the sample description
                AvcSampleDescription  avc_desc = sample_description as AvcSampleDescription;
                if (avc_desc == null) return Ap4Result.ERROR_NOT_SUPPORTED;

                if ((int)sample.GetDescriptionIndex() != _sampleDescriptionIndex) {
                    _sampleDescriptionIndex = (int)sample.GetDescriptionIndex();
                    _naluLengthSize = avc_desc.GetNaluLengthSize();

                    // make the SPS/PPS prefix
                    _prefix.SetDataSize(0);
                    for (int i=0; i<avc_desc.GetSequenceParameters().Count; i++) {
                        DataBuffer buffer = avc_desc.GetSequenceParameters()[i];
                        AppendToPrefix(buffer);
                    }
                    for (int i=0; i<avc_desc.GetPictureParameters().Count; i++) {
                        DataBuffer buffer = avc_desc.GetPictureParameters()[i];
                        AppendToPrefix(buffer);
                    }
                }
            } else if (sample_description.GetType() == SampleDescription.Type.TYPE_HEVC) {
                // check the sample description
                HevcSampleDescription  hevc_desc = sample_description as HevcSampleDescription;
                if (hevc_desc == null) return Ap4Result.ERROR_NOT_SUPPORTED;

                if ((int)sample.GetDescriptionIndex() != _sampleDescriptionIndex) {
                    _sampleDescriptionIndex = (int)sample.GetDescriptionIndex();
                    _naluLengthSize = hevc_desc.GetNaluLengthSize();

                    // make the VPS/SPS/PPS prefix
                    _prefix.SetDataSize(0);
                    for (int i=0; i<hevc_desc.GetSequences().Count; i++) {
                        HvccAtom.Sequence seq = hevc_desc.GetSequences()[i];
                        if (seq._naluType == Mpeg2TsUtils.HEVC_NALU_TYPE_VPS_NUT) {
                            for (int j=0; j<seq._nalus.Count; j++) {
                                DataBuffer buffer = seq._nalus[j];
                                AppendToPrefix(buffer);
                            }
                        }
                    }

                    for (int i=0; i<hevc_desc.GetSequences().Count; i++) {
                        HvccAtom.Sequence seq = hevc_desc.GetSequences()[i];
                        if (seq._naluType == Mpeg2TsUtils.HEVC_NALU_TYPE_SPS_NUT) {
                            for (int j=0; j<seq._nalus.Count; j++) {
                                DataBuffer buffer = seq._nalus[j];
                                AppendToPrefix(buffer);
                            }
                        }
                    }

                    for (int i=0; i<hevc_desc.GetSequences().Count; i++) {
                        HvccAtom.Sequence seq = hevc_desc.GetSequences()[i];
                        if (seq._naluType == Mpeg2TsUtils.HEVC_NALU_TYPE_PPS_NUT) {
                            for (int j=0; j<seq._nalus.Count; j++) {
                                DataBuffer buffer = seq._nalus[j];
                                AppendToPrefix(buffer);
                            }
                        }
                    }

                }
            } else {
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            // decide if we need to emit the prefix
            bool emit_prefix = false;
            if (sample.IsSync() || _samplesWritten == 0) {
                emit_prefix = true;
            }

            // write the NAL units
            byte[] data      = sample_data.GetData();
            int    data_pos  = 0;
            uint   data_size = sample_data.GetDataSize();

            // allocate a buffer for the PES packet
            DataBuffer pes_data = new DataBuffer();

            // output all NALUs
            for (uint nalu_count = 0; data_size != 0; nalu_count++) {
                // sanity check
                if (data_size < _naluLengthSize) break;

                // get the next NAL unit
                uint nalu_size;
                if (_naluLengthSize == 1) {
                    nalu_size = data[data_pos++];
                    data_size--;
                } else if (_naluLengthSize == 2) {
                    // PORT NOTE: AP4_BytesToInt16BE: the signed result is sign-extended, as upstream
                    nalu_size = unchecked((uint)Ap4Utils.BytesToInt16BE(data, data_pos));
                    data_pos  += 2;
                    data_size -= 2;
                } else if (_naluLengthSize == 4) {
                    nalu_size = unchecked((uint)Ap4Utils.BytesToInt32BE(data, data_pos));
                    data_pos  += 4;
                    data_size -= 4;
                } else {
                    break;
                }
                if (nalu_size > data_size) break;

                // check if we need to add a delimiter before the NALU
                if (nalu_count == 0 && sample_description.GetType() == SampleDescription.Type.TYPE_AVC) {
                    if (data_size < 1) break;
                    if (/* nalu_size != 2 || */ (uint)(data[data_pos] & 0x1F) != Ap4.AVC_NAL_UNIT_TYPE_ACCESS_UNIT_DELIMITER) {
                        // the first NAL unit is not an Access Unit Delimiter, we need to add one
                        byte[] delimiter = new byte[6];
                        delimiter[0] = 0;
                        delimiter[1] = 0;
                        delimiter[2] = 0;
                        delimiter[3] = 1;
                        delimiter[4] = 9;    // NAL type = Access Unit Delimiter;
                        delimiter[5] = 0xF0; // Slice types = ANY
                        pes_data.AppendData(delimiter, 6);

                        if (emit_prefix) {
                            pes_data.AppendData(_prefix.GetData(), _prefix.GetDataSize());
                            emit_prefix = false;
                        }
                    }
                } else {
                    if (emit_prefix) {
                        pes_data.AppendData(_prefix.GetData(), _prefix.GetDataSize());
                        emit_prefix = false;
                    }
                }

                // add a start code before the NAL unit
                byte[] start_code = new byte[3];
                start_code[0] = 0;
                start_code[1] = 0;
                start_code[2] = 1;
                pes_data.AppendData(start_code, 3);

                // add the NALU
                pes_data.AppendData(data, data_pos, nalu_size);

                // for AVC streams that do start with a NAL unit delimiter, we need to add the prefix now
                if (emit_prefix) {
                    pes_data.AppendData(_prefix.GetData(), _prefix.GetDataSize());
                    emit_prefix = false;
                }

                // move to the next NAL unit
                data_pos  += (int)nalu_size;
                data_size -= nalu_size;
            }

            // compute the timestamp
            ulong dts = Ap4Utils.ConvertTime(sample.GetDts(), _timeScale, 90000);
            ulong pts = Ap4Utils.ConvertTime(sample.GetCts(), _timeScale, 90000);

            // update counters
            ++_samplesWritten;

            // write the packet
            return WritePES(pes_data.GetData(), pes_data.GetDataSize(), dts, true, pts, with_pcr, output);
        }

        private Mpeg2TsVideoSampleStream(ushort  pid,
                                         uint    timescale,
                                         byte    stream_type,
                                         ushort  stream_id,
                                         byte[]  descriptor,
                                         uint    descriptor_length,
                                         ulong   pcr_offset) :
            base(pid,
                 stream_type,
                 stream_id,
                 timescale,
                 descriptor,
                 descriptor_length,
                 pcr_offset)
        {
            _sampleDescriptionIndex = -1;
            _naluLengthSize = 0;
            _samplesWritten = 0;
        }

        private int        _sampleDescriptionIndex;
        private DataBuffer _prefix = new DataBuffer();
        private uint       _naluLengthSize;
        private ulong      _samplesWritten;
    }
}
