// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Codecs/Ap4AvcParser.h, Ap4AvcParser.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint AVC_NAL_UNIT_TYPE_UNSPECIFIED                       = 0;
        public const uint AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_NON_IDR_PICTURE    = 1;
        public const uint AVC_NAL_UNIT_TYPE_CODED_SLICE_DATA_PARTITION_A      = 2;
        public const uint AVC_NAL_UNIT_TYPE_CODED_SLICE_DATA_PARTITION_B      = 3;
        public const uint AVC_NAL_UNIT_TYPE_CODED_SLICE_DATA_PARTITION_C      = 4;
        public const uint AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE        = 5;
        public const uint AVC_NAL_UNIT_TYPE_SEI                               = 6;
        public const uint AVC_NAL_UNIT_TYPE_SPS                               = 7;
        public const uint AVC_NAL_UNIT_TYPE_PPS                               = 8;
        public const uint AVC_NAL_UNIT_TYPE_ACCESS_UNIT_DELIMITER             = 9;
        public const uint AVC_NAL_UNIT_TYPE_END_OF_SEQUENCE                   = 10;
        public const uint AVC_NAL_UNIT_TYPE_END_OF_STREAM                     = 11;
        public const uint AVC_NAL_UNIT_TYPE_FILLER_DATA                       = 12;
        public const uint AVC_NAL_UNIT_TYPE_SPS_EXTENSION                     = 13;
        public const uint AVC_NAL_UNIT_TYPE_PREFIX                            = 14;
        public const uint AVC_NAL_UNIT_TYPE_SUBSET_SPS                        = 15;
        public const uint AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_AUXILIARY_PICTURE  = 19;
        public const uint AVC_NAL_UNIT_TYPE_CODED_SLICE_IN_SCALABLE_EXTENSION = 20;
        public const uint AVC_NAL_UNIT_TYPE_UNSPECIFIED28                     = 28;
        public const uint AVC_NAL_UNIT_TYPE_UNSPECIFIED30                     = 30;

        public const uint AVC_SLICE_TYPE_P                                    = 0;
        public const uint AVC_SLICE_TYPE_B                                    = 1;
        public const uint AVC_SLICE_TYPE_I                                    = 2;
        public const uint AVC_SLICE_TYPE_SP                                   = 3;
        public const uint AVC_SLICE_TYPE_SI                                   = 4;

        public const uint AVC_SPS_MAX_ID                                      = 255;
        public const uint AVC_SPS_MAX_NUM_REF_FRAMES_IN_PIC_ORDER_CNT_CYCLE   = 256;
        public const uint AVC_SPS_MAX_SCALING_LIST_COUNT                      = 12;

        public const uint AVC_PPS_MAX_ID                                      = 255;
        public const uint AVC_PPS_MAX_SLICE_GROUPS                            = 256;
        public const uint AVC_PPS_MAX_PIC_SIZE_IN_MAP_UNITS                   = 65536;
    }

    /*----------------------------------------------------------------------
    |   types
    +---------------------------------------------------------------------*/
    public class AvcScalingList4x4 {
        public int[] scale = new int[16];
    }

    public class AvcScalingList8x8 {
        public int[] scale = new int[64];
    }

    public class AvcSequenceParameterSet {
        public AvcSequenceParameterSet()
        {
            profile_idc = 0;
            constraint_set0_flag = 0;
            constraint_set1_flag = 0;
            constraint_set2_flag = 0;
            constraint_set3_flag = 0;
            level_idc = 0;
            seq_parameter_set_id = 0;
            chroma_format_idc = 1;
            separate_colour_plane_flag = 0;
            bit_depth_luma_minus8 = 0;
            bit_depth_chroma_minus8 = 0;
            qpprime_y_zero_transform_bypass_flag = 0;
            seq_scaling_matrix_present_flag = 0;
            log2_max_frame_num_minus4 = 0;
            pic_order_cnt_type = 0;
            log2_max_pic_order_cnt_lsb_minus4 = 0;
            delta_pic_order_always_zero_flags = 0;
            offset_for_non_ref_pic = 0;
            offset_for_top_to_bottom_field = 0;
            num_ref_frames_in_pic_order_cnt_cycle = 0;
            num_ref_frames = 0;
            gaps_in_frame_num_value_allowed_flag = 0;
            pic_width_in_mbs_minus1 = 0;
            pic_height_in_map_units_minus1 = 0;
            frame_mbs_only_flag = 0;
            mb_adaptive_frame_field_flag = 0;
            direct_8x8_inference_flag = 0;
            frame_cropping_flag = 0;
            frame_crop_left_offset = 0;
            frame_crop_right_offset = 0;
            frame_crop_top_offset = 0;
            frame_crop_bottom_offset = 0;

            for (int i=0; i<6; i++) {
                scaling_list_4x4[i] = new AvcScalingList4x4();
                scaling_list_8x8[i] = new AvcScalingList8x8();
            }
        }

        public void GetInfo(out uint width, out uint height)
        {
            width = (pic_width_in_mbs_minus1+1) * 16;
            height = (2-frame_mbs_only_flag) * (pic_height_in_map_units_minus1+1) * 16;

            if (frame_cropping_flag != 0) {
                uint crop_h = 2*(frame_crop_left_offset+frame_crop_right_offset);
                uint crop_v = 2*(frame_crop_top_offset+frame_crop_bottom_offset)*(2-frame_mbs_only_flag);
                if (crop_h < width) width   -= crop_h;
                if (crop_v < height) height -= crop_v;
            }
        }

        public DataBuffer raw_bytes = new DataBuffer();

        public uint profile_idc;
        public uint constraint_set0_flag;
        public uint constraint_set1_flag;
        public uint constraint_set2_flag;
        public uint constraint_set3_flag;
        public uint level_idc;
        public uint seq_parameter_set_id;
        public uint chroma_format_idc;
        public uint separate_colour_plane_flag;
        public uint bit_depth_luma_minus8;
        public uint bit_depth_chroma_minus8;
        public uint qpprime_y_zero_transform_bypass_flag;
        public uint seq_scaling_matrix_present_flag;
        public AvcScalingList4x4[] scaling_list_4x4 = new AvcScalingList4x4[6];
        public bool[]              use_default_scaling_matrix_4x4 = new bool[Ap4.AVC_SPS_MAX_SCALING_LIST_COUNT];
        public AvcScalingList8x8[] scaling_list_8x8 = new AvcScalingList8x8[6];
        public byte[]              use_default_scaling_matrix_8x8 = new byte[Ap4.AVC_SPS_MAX_SCALING_LIST_COUNT];
        public uint log2_max_frame_num_minus4;
        public uint pic_order_cnt_type;
        public uint log2_max_pic_order_cnt_lsb_minus4;
        public uint delta_pic_order_always_zero_flags;
        public int  offset_for_non_ref_pic;
        public int  offset_for_top_to_bottom_field;
        public uint num_ref_frames_in_pic_order_cnt_cycle;
        public uint[] offset_for_ref_frame = new uint[Ap4.AVC_SPS_MAX_NUM_REF_FRAMES_IN_PIC_ORDER_CNT_CYCLE];
        public uint num_ref_frames;
        public uint gaps_in_frame_num_value_allowed_flag;
        public uint pic_width_in_mbs_minus1;
        public uint pic_height_in_map_units_minus1;
        public uint frame_mbs_only_flag;
        public uint mb_adaptive_frame_field_flag;
        public uint direct_8x8_inference_flag;
        public uint frame_cropping_flag;
        public uint frame_crop_left_offset;
        public uint frame_crop_right_offset;
        public uint frame_crop_top_offset;
        public uint frame_crop_bottom_offset;
    }

    public class AvcPictureParameterSet {
        public AvcPictureParameterSet()
        {
            pic_parameter_set_id = 0;
            seq_parameter_set_id = 0;
            entropy_coding_mode_flag = 0;
            pic_order_present_flag = 0;
            num_slice_groups_minus1 = 0;
            slice_group_map_type = 0;
            slice_group_change_direction_flag = 0;
            slice_group_change_rate_minus1 = 0;
            pic_size_in_map_units_minus1 = 0;
            num_ref_idx_10_active_minus1 = 0;
            num_ref_idx_11_active_minus1 = 0;
            weighted_pred_flag = 0;
            weighted_bipred_idc = 0;
            pic_init_qp_minus26 = 0;
            pic_init_qs_minus26 = 0;
            chroma_qp_index_offset = 0;
            deblocking_filter_control_present_flag = 0;
            constrained_intra_pred_flag = 0;
            redundant_pic_cnt_present_flag = 0;
        }

        public DataBuffer raw_bytes = new DataBuffer();

        public uint pic_parameter_set_id;
        public uint seq_parameter_set_id;
        public uint entropy_coding_mode_flag;
        public uint pic_order_present_flag;
        public uint num_slice_groups_minus1;
        public uint slice_group_map_type;
        public uint[] run_length_minus1 = new uint[Ap4.AVC_PPS_MAX_SLICE_GROUPS];
        public uint[] top_left = new uint[Ap4.AVC_PPS_MAX_SLICE_GROUPS];
        public uint[] bottom_right = new uint[Ap4.AVC_PPS_MAX_SLICE_GROUPS];
        public uint slice_group_change_direction_flag;
        public uint slice_group_change_rate_minus1;
        public uint pic_size_in_map_units_minus1;
        public uint num_ref_idx_10_active_minus1;
        public uint num_ref_idx_11_active_minus1;
        public uint weighted_pred_flag;
        public uint weighted_bipred_idc;
        public int  pic_init_qp_minus26;
        public int  pic_init_qs_minus26;
        public int  chroma_qp_index_offset;
        public uint deblocking_filter_control_present_flag;
        public uint constrained_intra_pred_flag;
        public uint redundant_pic_cnt_present_flag;
    }

    public class AvcSliceHeader {
        public AvcSliceHeader()
        {
            size = 0;
            first_mb_in_slice = 0;
            slice_type = 0;
            pic_parameter_set_id = 0;
            colour_plane_id = 0;
            frame_num = 0;
            field_pic_flag = 0;
            bottom_field_flag = 0;
            idr_pic_id = 0;
            pic_order_cnt_lsb = 0;
            redundant_pic_cnt = 0;
            direct_spatial_mv_pred_flag = 0;
            num_ref_idx_active_override_flag = 0;
            num_ref_idx_l0_active_minus1 = 0;
            num_ref_idx_l1_active_minus1 = 0;
            ref_pic_list_reordering_flag_l0 = 0;
            reordering_of_pic_nums_idc = 0;
            abs_diff_pic_num_minus1 = 0;
            long_term_pic_num = 0;
            ref_pic_list_reordering_flag_l1 = 0;
            luma_log2_weight_denom = 0;
            chroma_log2_weight_denom = 0;
            cabac_init_idc = 0;
            slice_qp_delta = 0;
            sp_for_switch_flag = 0;
            slice_qs_delta = 0;
            disable_deblocking_filter_idc = 0;
            slice_alpha_c0_offset_div2 = 0;
            slice_beta_offset_div2 = 0;
            slice_group_change_cycle = 0;
            no_output_of_prior_pics_flag = 0;
            long_term_reference_flag = 0;
            difference_of_pic_nums_minus1 = 0;
            long_term_frame_idx = 0;
            max_long_term_frame_idx_plus1 = 0;

            delta_pic_order_cnt[0] = delta_pic_order_cnt[1] = 0;
        }

        public uint size; // not from the bitstream, this is computed after parsing

        public uint first_mb_in_slice;
        public uint slice_type;
        public uint pic_parameter_set_id;
        public uint colour_plane_id;
        public uint frame_num;
        public uint field_pic_flag;
        public uint bottom_field_flag;
        public uint idr_pic_id;
        public uint pic_order_cnt_lsb;
        public int[] delta_pic_order_cnt = new int[2];
        public uint redundant_pic_cnt;
        public uint direct_spatial_mv_pred_flag;
        public uint num_ref_idx_active_override_flag;
        public uint num_ref_idx_l0_active_minus1;
        public uint num_ref_idx_l1_active_minus1;
        public uint ref_pic_list_reordering_flag_l0;
        public uint reordering_of_pic_nums_idc;
        public uint abs_diff_pic_num_minus1;
        public uint long_term_pic_num;
        public uint ref_pic_list_reordering_flag_l1;
        public uint luma_log2_weight_denom;
        public uint chroma_log2_weight_denom;
        public uint cabac_init_idc;
        public uint slice_qp_delta;
        public uint sp_for_switch_flag;
        public int  slice_qs_delta;
        public uint disable_deblocking_filter_idc;
        public int  slice_alpha_c0_offset_div2;
        public int  slice_beta_offset_div2;
        public uint slice_group_change_cycle;
        public uint no_output_of_prior_pics_flag;
        public uint long_term_reference_flag;
        public uint difference_of_pic_nums_minus1;
        public uint long_term_frame_idx;
        public uint max_long_term_frame_idx_plus1;
    }

    /*----------------------------------------------------------------------
    |   AvcNalParser
    +---------------------------------------------------------------------*/
    public class AvcNalParser : NalParser {
        public static string NaluTypeName(uint nalu_type)
        {
            switch (nalu_type) {
                case  0: return "Unspecified";
                case  1: return "Coded slice of a non-IDR picture";
                case  2: return "Coded slice data partition A";
                case  3: return "Coded slice data partition B";
                case  4: return "Coded slice data partition C";
                case  5: return "Coded slice of an IDR picture";
                case  6: return "Supplemental enhancement information (SEI)";
                case  7: return "Sequence parameter set";
                case  8: return "Picture parameter set";
                case  9: return "Access unit delimiter";
                case 10: return "End of sequence";
                case 11: return "End of stream";
                case 12: return "Filler data";
                case 13: return "Sequence parameter set extension";
                case 14: return "Prefix NAL unit in scalable extension";
                case 15: return "Subset sequence parameter set";
                case 19: return "Coded slice of an auxiliary coded picture without partitioning";
                case 20: return "Coded slice in scalable extension";
                case 28: return "Dolby Vision RPU NAL units";
                case 30: return "Dolby Vision EL NAL units";
                default: return null;
            }
        }

        public static string PrimaryPicTypeName(uint primary_pic_type)
        {
            switch (primary_pic_type) {
                case 0: return "I";
                case 1: return "I, P";
                case 2: return "I, P, B";
                case 3: return "SI";
                case 4: return "SI, SP";
                case 5: return "I, SI";
                case 6: return "I, SI, P, SP";
                case 7: return "I, SI, P, SP, B";
                default: return null;
            }
        }

        public static string SliceTypeName(uint slice_type)
        {
            switch (slice_type) {
                case 0: return "P";
                case 1: return "B";
                case 2: return "I";
                case 3: return "SP";
                case 4: return "SI";
                case 5: return "P";
                case 6: return "B";
                case 7: return "I";
                case 8: return "SP";
                case 9: return "SI";
                default: return null;
            }
        }

        public AvcNalParser() :
            base()
        {
        }
    }

    /*----------------------------------------------------------------------
    |   AvcFrameParser
    +---------------------------------------------------------------------*/
    public class AvcFrameParser {
        // types
        public class AccessUnitInfo {
            public List<DataBuffer> nal_units = new List<DataBuffer>();
            public bool             is_idr;
            public uint             decode_order;
            public uint             display_order;

            public void Reset()
            {
                nal_units.Clear();
                is_idr = false;
                decode_order = 0;
                display_order = 0;
            }
        }

        // methods
        public AvcFrameParser()
        {
            _nalUnitType = 0;
            _nalRefIdc = 0;
            _sliceHeader = null;
            _accessUnitVclNalUnitCount = 0;
            _totalNalUnitCount = 0;
            _totalAccessUnitCount = 0;
            _prevFrameNum = 0;
            _prevFrameNumOffset = 0;
            _prevPicOrderCntMsb = 0;
            _prevPicOrderCntLsb = 0;
            _keepParameterSets = true;

            for (uint i=0; i<256; i++) {
                _PPS[i] = null;
                _SPS[i] = null;
            }
        }

        /*----------------------------------------------------------------------
        |   ReadGolomb
        +---------------------------------------------------------------------*/
        private static uint ReadGolomb(BitReader bits)
        {
            uint leading_zeros = 0;
            while (bits.ReadBit() == 0) {
                leading_zeros++;
                if (leading_zeros > 32) return 0; // safeguard
            }
            if (leading_zeros != 0) {
                // PORT NOTE: (1<<leading_zeros) is an int shift, as upstream (count masked to 5 bits
                // like x86 for leading_zeros == 32), then converted to unsigned for the addition.
                return unchecked((uint)((1<<(int)leading_zeros)-1)+bits.ReadBits(leading_zeros));
            } else {
                return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   SignedGolomb
        +---------------------------------------------------------------------*/
        private static int SignedGolomb(uint code_num)
        {
            if ((code_num % 2) != 0) {
                return unchecked((int)((code_num+1)/2));
            } else {
                return unchecked(-((int)code_num/2));
            }
        }

        /// <summary>
        /// Feed some data to the parser and look for the next NAL Unit.
        /// </summary>
        /// <param name="data">Pointer to the memory buffer with the data to feed.</param>
        /// <param name="data_size">Size in bytes of the buffer pointed to by the
        /// data pointer.</param>
        /// <param name="bytes_consumed">Number of bytes from the data buffer that were
        /// consumed and stored by the parser.</param>
        /// <param name="access_unit_info">Reference to a <see cref="AccessUnitInfo"/> structure that will
        /// contain information about any access unit found in the data. If no
        /// access unit was found, the nal_units field of this structure will be an
        /// empty array.</param>
        /// <param name="eos">Boolean flag that indicates if this buffer is the last
        /// buffer in the stream/file (End Of Stream).</param>
        /// <returns><see cref="Ap4Result.SUCCESS"/> is the call succeeds, or an error code if it
        /// fails.</returns>
        /// <remarks>
        /// <para>The caller must not feed the same data twice. When this method
        /// returns, the caller should inspect the value of bytes_consumed and
        /// advance the input stream source accordingly, such that the next
        /// buffer passed to this method will be exactly bytes_consumed bytes
        /// after what was passed in this call. After all the input data has
        /// been supplied to the parser (eos=true), the caller also should
        /// this method with an empty buffer (data=NULL and/or data_size=0) until
        /// no more data is returned, because there may be buffered data still
        /// available.</para>
        /// <para>When data is returned in the access_unit_info structure, the caller is
        /// responsible for freeing this data by calling <see cref="AccessUnitInfo.Reset"/></para>
        /// </remarks>
        public Ap4Result Feed(byte[]         data,
                              uint           data_size,
                              out uint       bytes_consumed,
                              AccessUnitInfo access_unit_info,
                              bool           eos = false)
        {
            return Feed(data, 0, data_size, out bytes_consumed, access_unit_info, eos);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public Ap4Result Feed(byte[]         data,
                              int            offset,
                              uint           data_size,
                              out uint       bytes_consumed,
                              AccessUnitInfo access_unit_info,
                              bool           eos = false)
        {
            DataBuffer nal_unit = null;

            // feed the NAL unit parser
            Ap4Result result = _nalParser.Feed(data, offset, data_size, out bytes_consumed, out nal_unit, eos);
            if (result.Failed()) {
                return result;
            }

            if (bytes_consumed < data_size) {
                // there will be more to parse
                eos = false;
            }

            return Feed(nal_unit != null ? nal_unit.GetData() : null,
                        nal_unit != null ? nal_unit.GetDataSize() : 0,
                        access_unit_info,
                        eos);
        }

        public Ap4Result Feed(byte[]         nal_unit,
                              uint           nal_unit_size,
                              AccessUnitInfo access_unit_info,
                              bool           last_unit = false)
        {
            return Feed(nal_unit, 0, nal_unit_size, access_unit_info, last_unit);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public Ap4Result Feed(byte[]         nal_unit,
                              int            nal_unit_offset,
                              uint           nal_unit_size,
                              AccessUnitInfo access_unit_info,
                              bool           last_unit = false)
        {
            Ap4Result result;

            // default return values
            access_unit_info.Reset();

            if (nal_unit != null && nal_unit_size != 0) {
                uint    nal_unit_type = (uint)(nal_unit[nal_unit_offset]&0x1F);
                string  nal_unit_type_name = AvcNalParser.NaluTypeName(nal_unit_type);
                uint    nal_ref_idc = (uint)((nal_unit[nal_unit_offset]>>5)&3);
                if (nal_unit_type_name == null) nal_unit_type_name = "UNKNOWN";
                if (nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_ACCESS_UNIT_DELIMITER) {
                    // PORT NOTE: upstream reads nal_unit[1] here only to feed a debug printf that is
                    // compiled out (primary_pic_type); the read is dropped so that a 1-byte AUD
                    // cannot index past the end of the array.

                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else if (nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_NON_IDR_PICTURE ||
                           nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE     ||
                           nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_DATA_PARTITION_A) {
                    AvcSliceHeader slice_header = new AvcSliceHeader();
                    result = ParseSliceHeader(nal_unit, nal_unit_offset+1,
                                              nal_unit_size-1,
                                              nal_unit_type,
                                              nal_ref_idc,
                                              slice_header);
                    if (result.Failed()) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }

                    string slice_type_name = AvcNalParser.SliceTypeName(slice_header.slice_type);
                    if (slice_type_name == null) slice_type_name = "?";
                    if (slice_header != null) {
                        if (_sliceHeader != null &&
                            !SameFrame(_nalUnitType, _nalRefIdc, _sliceHeader,
                                       nal_unit_type, nal_ref_idc, slice_header)) {
                            CheckIfAccessUnitIsCompleted(access_unit_info);
                            _accessUnitVclNalUnitCount = 1;
                        } else {
                            // continuation of an access unit
                            ++_accessUnitVclNalUnitCount;
                        }
                    }

                    // buffer this NAL unit
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                    _sliceHeader = slice_header;
                    _nalUnitType = nal_unit_type;
                    _nalRefIdc   = nal_ref_idc;
                } else if (nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_PPS) {
                    AvcPictureParameterSet pps = new AvcPictureParameterSet();
                    result = ParsePPS(nal_unit, nal_unit_offset, nal_unit_size, pps);
                    if (result.Failed()) {
                        // PPS ERROR!!!
                    } else {
                        _PPS[pps.pic_parameter_set_id] = pps;

                        // keep the PPS with the NAL unit (this is optional)
                        AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                        CheckIfAccessUnitIsCompleted(access_unit_info);
                    }
                } else if (nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_SPS) {
                    AvcSequenceParameterSet sps = new AvcSequenceParameterSet();
                    result = ParseSPS(nal_unit, nal_unit_offset, nal_unit_size, sps);
                    if (result.Failed()) {
                        // SPS ERROR!!!
                    } else {
                        _SPS[sps.seq_parameter_set_id] = sps;
                        CheckIfAccessUnitIsCompleted(access_unit_info);
                    }
                } else if (nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_SEI) {
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else if (nal_unit_type >= 14 && nal_unit_type <= 18) {
                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else if (nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_UNSPECIFIED28) {
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else if (nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_UNSPECIFIED30) {
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else {
                }
                _totalNalUnitCount++;
            }

            // flush if needed
            if (last_unit && access_unit_info.nal_units.Count == 0) {
                CheckIfAccessUnitIsCompleted(access_unit_info);
            }

            return Ap4Result.SUCCESS;
        }

        // PORT NOTE: upstream returns pointers to the first element of the 256-entry arrays
        public AvcSequenceParameterSet[] GetSequenceParameterSets() { return _SPS;         }
        public AvcPictureParameterSet[]  GetPictureParameterSets()  { return _PPS;         }
        public AvcSliceHeader             GetSliceHeader()           { return _sliceHeader; }

        public Ap4Result ParseSPS(byte[]                  data,
                                  uint                    data_size,
                                  AvcSequenceParameterSet sps)
        {
            return ParseSPS(data, 0, data_size, sps);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public Ap4Result ParseSPS(byte[]                  data,
                                  int                     data_offset,
                                  uint                    data_size,
                                  AvcSequenceParameterSet sps)
        {
            sps.raw_bytes.SetData(data, data_offset, data_size);
            DataBuffer unescaped = new DataBuffer(data, data_offset, data_size);
            NalParser.Unescape(unescaped);
            BitReader bits = new BitReader(unescaped.GetData(), unescaped.GetDataSize());

            bits.SkipBits(8); // NAL Unit Type

            sps.profile_idc = bits.ReadBits(8);
            sps.constraint_set0_flag = (uint)bits.ReadBit();
            sps.constraint_set1_flag = (uint)bits.ReadBit();
            sps.constraint_set2_flag = (uint)bits.ReadBit();
            sps.constraint_set3_flag = (uint)bits.ReadBit();
            bits.SkipBits(4);
            sps.level_idc = bits.ReadBits(8);
            sps.seq_parameter_set_id = ReadGolomb(bits);
            if (sps.seq_parameter_set_id > Ap4.AVC_SPS_MAX_ID) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            if (sps.profile_idc  ==  100  ||
                sps.profile_idc  ==  110  ||
                sps.profile_idc  ==  122  ||
                sps.profile_idc  ==  244  ||
                sps.profile_idc  ==  44   ||
                sps.profile_idc  ==  83   ||
                sps.profile_idc  ==  86) {
                sps.chroma_format_idc = ReadGolomb(bits);
                sps.separate_colour_plane_flag = 0;
                if (sps.chroma_format_idc == 3) {
                    sps.separate_colour_plane_flag = (uint)bits.ReadBit();
                }
                sps.bit_depth_luma_minus8 = ReadGolomb(bits);
                sps.bit_depth_chroma_minus8 = ReadGolomb(bits);
                sps.qpprime_y_zero_transform_bypass_flag = (uint)bits.ReadBit();
                sps.seq_scaling_matrix_present_flag = (uint)bits.ReadBit();
                if (sps.seq_scaling_matrix_present_flag != 0) {
                    for (int i=0; i<(sps.chroma_format_idc != 3 ? 8 : 12); i++) {
                        uint seq_scaling_list_present_flag = (uint)bits.ReadBit();
                        if (seq_scaling_list_present_flag != 0) {
                            if (i<6) {
                                int last_scale = 8;
                                int next_scale = 8;
                                for (uint j=0; j<16; j++) {
                                    if (next_scale != 0) {
                                        int delta_scale = SignedGolomb(ReadGolomb(bits));
                                        next_scale = unchecked(last_scale + delta_scale + 256) % 256;
                                        sps.use_default_scaling_matrix_4x4[i] = (j == 0 && next_scale == 0);
                                    }
                                    sps.scaling_list_4x4[i].scale[j] = (next_scale == 0 ? last_scale : next_scale);
                                    last_scale = sps.scaling_list_4x4[i].scale[j];
                                }
                            } else {
                                int last_scale = 8;
                                int next_scale = 8;
                                for (uint j=0; j<64; j++) {
                                    if (next_scale != 0) {
                                        int delta_scale = SignedGolomb(ReadGolomb(bits));
                                        next_scale = unchecked(last_scale + delta_scale + 256) % 256;
                                        sps.use_default_scaling_matrix_8x8[i-6] = (byte)((j == 0 && next_scale == 0) ? 1 : 0);
                                    }
                                    sps.scaling_list_8x8[i-6].scale[j] = (next_scale == 0 ? last_scale : next_scale);
                                    last_scale = sps.scaling_list_8x8[i-6].scale[j];
                                }
                            }
                        }
                    }
                }
            }
            sps.log2_max_frame_num_minus4 = ReadGolomb(bits);
            sps.pic_order_cnt_type = ReadGolomb(bits);
            if (sps.pic_order_cnt_type > 2) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            if (sps.pic_order_cnt_type == 0) {
                sps.log2_max_pic_order_cnt_lsb_minus4 = ReadGolomb(bits);
            } else if (sps.pic_order_cnt_type == 1) {
                sps.delta_pic_order_always_zero_flags = (uint)bits.ReadBit();
                sps.offset_for_non_ref_pic = SignedGolomb(ReadGolomb(bits));
                sps.offset_for_top_to_bottom_field = SignedGolomb(ReadGolomb(bits));
                sps.num_ref_frames_in_pic_order_cnt_cycle = ReadGolomb(bits);
                if (sps.num_ref_frames_in_pic_order_cnt_cycle > Ap4.AVC_SPS_MAX_NUM_REF_FRAMES_IN_PIC_ORDER_CNT_CYCLE) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                for (uint i=0; i<sps.num_ref_frames_in_pic_order_cnt_cycle; i++) {
                    sps.offset_for_ref_frame[i] = unchecked((uint)SignedGolomb(ReadGolomb(bits)));
                }
            }
            sps.num_ref_frames                       = ReadGolomb(bits);
            sps.gaps_in_frame_num_value_allowed_flag = (uint)bits.ReadBit();
            sps.pic_width_in_mbs_minus1              = ReadGolomb(bits);
            sps.pic_height_in_map_units_minus1       = ReadGolomb(bits);
            sps.frame_mbs_only_flag                  = (uint)bits.ReadBit();
            if (sps.frame_mbs_only_flag == 0) {
                sps.mb_adaptive_frame_field_flag = (uint)bits.ReadBit();
            }
            sps.direct_8x8_inference_flag = (uint)bits.ReadBit();
            sps.frame_cropping_flag       = (uint)bits.ReadBit();
            if (sps.frame_cropping_flag != 0) {
                sps.frame_crop_left_offset   = ReadGolomb(bits);
                sps.frame_crop_right_offset  = ReadGolomb(bits);
                sps.frame_crop_top_offset    = ReadGolomb(bits);
                sps.frame_crop_bottom_offset = ReadGolomb(bits);
            }

            return Ap4Result.SUCCESS;
        }

        public Ap4Result ParsePPS(byte[]                 data,
                                  uint                   data_size,
                                  AvcPictureParameterSet pps)
        {
            return ParsePPS(data, 0, data_size, pps);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public Ap4Result ParsePPS(byte[]                 data,
                                  int                    data_offset,
                                  uint                   data_size,
                                  AvcPictureParameterSet pps)
        {
            pps.raw_bytes.SetData(data, data_offset, data_size);
            DataBuffer unescaped = new DataBuffer(data, data_offset, data_size);
            NalParser.Unescape(unescaped);
            BitReader bits = new BitReader(unescaped.GetData(), unescaped.GetDataSize());

            bits.SkipBits(8); // NAL Unit Type

            pps.pic_parameter_set_id     = ReadGolomb(bits);
            if (pps.pic_parameter_set_id > Ap4.AVC_PPS_MAX_ID) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            pps.seq_parameter_set_id     = ReadGolomb(bits);
            if (pps.seq_parameter_set_id > Ap4.AVC_SPS_MAX_ID) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            pps.entropy_coding_mode_flag = (uint)bits.ReadBit();
            pps.pic_order_present_flag   = (uint)bits.ReadBit();
            pps.num_slice_groups_minus1  = ReadGolomb(bits);
            if (pps.num_slice_groups_minus1 >= Ap4.AVC_PPS_MAX_SLICE_GROUPS) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            if (pps.num_slice_groups_minus1 > 0) {
                pps.slice_group_map_type = ReadGolomb(bits);
                if (pps.slice_group_map_type == 0) {
                    for (uint i=0; i<=pps.num_slice_groups_minus1; i++) {
                        pps.run_length_minus1[i] = ReadGolomb(bits);
                    }
                } else if (pps.slice_group_map_type == 2) {
                    for (uint i=0; i<pps.num_slice_groups_minus1; i++) {
                        pps.top_left[i] = ReadGolomb(bits);
                        pps.bottom_right[i] = ReadGolomb(bits);
                    }
                } else if (pps.slice_group_map_type == 3 ||
                           pps.slice_group_map_type == 4 ||
                           pps.slice_group_map_type == 5) {
                    pps.slice_group_change_direction_flag = (uint)bits.ReadBit();
                    pps.slice_group_change_rate_minus1 = ReadGolomb(bits);
                } else if (pps.slice_group_map_type == 6) {
                    pps.pic_size_in_map_units_minus1 = ReadGolomb(bits);
                    if (pps.pic_size_in_map_units_minus1 >= Ap4.AVC_PPS_MAX_PIC_SIZE_IN_MAP_UNITS) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                    uint num_bits_per_slice_group_id;
                    if (pps.num_slice_groups_minus1 + 1 > 4) {
                        num_bits_per_slice_group_id = 3;
                    } else if (pps.num_slice_groups_minus1 + 1 > 2) {
                        num_bits_per_slice_group_id = 2;
                    } else {
                        num_bits_per_slice_group_id = 1;
                    }
                    for (uint i=0; i<=pps.pic_size_in_map_units_minus1; i++) {
                        /*pps.slice_group_id[i] =*/ bits.ReadBits(num_bits_per_slice_group_id);
                    }
                }
            }
            pps.num_ref_idx_10_active_minus1 = ReadGolomb(bits);
            pps.num_ref_idx_11_active_minus1 = ReadGolomb(bits);
            pps.weighted_pred_flag           = (uint)bits.ReadBit();
            pps.weighted_bipred_idc          = bits.ReadBits(2);
            pps.pic_init_qp_minus26          = SignedGolomb(ReadGolomb(bits));
            pps.pic_init_qs_minus26          = SignedGolomb(ReadGolomb(bits));
            pps.chroma_qp_index_offset       = SignedGolomb(ReadGolomb(bits));
            pps.deblocking_filter_control_present_flag = (uint)bits.ReadBit();
            pps.constrained_intra_pred_flag            = (uint)bits.ReadBit();
            pps.redundant_pic_cnt_present_flag         = (uint)bits.ReadBit();

            return Ap4Result.SUCCESS;
        }

        public Ap4Result ParseSliceHeader(byte[]         data,
                                          uint           data_size,
                                          uint           nal_unit_type,
                                          uint           nal_ref_idc,
                                          AvcSliceHeader slice_header)
        {
            return ParseSliceHeader(data, 0, data_size, nal_unit_type, nal_ref_idc, slice_header);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public Ap4Result ParseSliceHeader(byte[]         data,
                                          int            data_offset,
                                          uint           data_size,
                                          uint           nal_unit_type,
                                          uint           nal_ref_idc,
                                          AvcSliceHeader slice_header)
        {
            DataBuffer unescaped = new DataBuffer(data, data_offset, data_size);
            NalParser.Unescape(unescaped);
            BitReader bits = new BitReader(unescaped.GetData(), unescaped.GetDataSize());

            // init the computer fields
            slice_header.size = 0;

            slice_header.first_mb_in_slice    = ReadGolomb(bits);
            slice_header.slice_type           = ReadGolomb(bits);
            slice_header.pic_parameter_set_id = ReadGolomb(bits);
            if (slice_header.pic_parameter_set_id > Ap4.AVC_PPS_MAX_ID) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            AvcPictureParameterSet pps = _PPS[slice_header.pic_parameter_set_id];
            if (pps == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            AvcSequenceParameterSet sps = _SPS[pps.seq_parameter_set_id];
            if (sps == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            if (sps.separate_colour_plane_flag != 0) {
                slice_header.colour_plane_id = bits.ReadBits(2);
            }
            slice_header.frame_num = bits.ReadBits(sps.log2_max_frame_num_minus4 + 4);
            if (sps.frame_mbs_only_flag == 0) {
                slice_header.field_pic_flag = (uint)bits.ReadBit();
                if (slice_header.field_pic_flag != 0) {
                    slice_header.bottom_field_flag = (uint)bits.ReadBit();
                }
            }
            if (nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE) {
                slice_header.idr_pic_id = ReadGolomb(bits);
            }
            if (sps.pic_order_cnt_type == 0) {
                slice_header.pic_order_cnt_lsb = bits.ReadBits(sps.log2_max_pic_order_cnt_lsb_minus4 + 4);
                if (pps.pic_order_present_flag != 0 && slice_header.field_pic_flag == 0) {
                    slice_header.delta_pic_order_cnt[0] = SignedGolomb(ReadGolomb(bits));
                }
            }
            if (sps.pic_order_cnt_type == 1 && sps.delta_pic_order_always_zero_flags == 0) {
                slice_header.delta_pic_order_cnt[0] = SignedGolomb(ReadGolomb(bits));
                if (pps.pic_order_present_flag != 0 && slice_header.field_pic_flag == 0) {
                    slice_header.delta_pic_order_cnt[1] = SignedGolomb(ReadGolomb(bits));
                }
            }
            if (pps.redundant_pic_cnt_present_flag != 0) {
                slice_header.redundant_pic_cnt = ReadGolomb(bits);
            }

            uint slice_type = slice_header.slice_type % 5; // this seems to be implicit in the spec

            if (slice_type == Ap4.AVC_SLICE_TYPE_B) {
                slice_header.direct_spatial_mv_pred_flag = (uint)bits.ReadBit();
            }

            if (slice_type == Ap4.AVC_SLICE_TYPE_P  ||
                slice_type == Ap4.AVC_SLICE_TYPE_SP ||
                slice_type == Ap4.AVC_SLICE_TYPE_B) {
                slice_header.num_ref_idx_active_override_flag = (uint)bits.ReadBit();

                if (slice_header.num_ref_idx_active_override_flag != 0) {
                    slice_header.num_ref_idx_l0_active_minus1 = ReadGolomb(bits);
                    if ((slice_header.slice_type % 5) == Ap4.AVC_SLICE_TYPE_B) {
                        slice_header.num_ref_idx_l1_active_minus1 = ReadGolomb(bits);
                    }
                } else {
                    slice_header.num_ref_idx_l0_active_minus1 = pps.num_ref_idx_10_active_minus1;
                    slice_header.num_ref_idx_l1_active_minus1 = pps.num_ref_idx_11_active_minus1;
                }
            }

            // ref_pic_list_reordering
            if ((slice_header.slice_type % 5) != 2 && (slice_header.slice_type % 5) != 4) {
                slice_header.ref_pic_list_reordering_flag_l0 = (uint)bits.ReadBit();
                if (slice_header.ref_pic_list_reordering_flag_l0 != 0) {
                    do {
                        slice_header.reordering_of_pic_nums_idc = ReadGolomb(bits);
                        if (slice_header.reordering_of_pic_nums_idc == 0 ||
                            slice_header.reordering_of_pic_nums_idc == 1) {
                            slice_header.abs_diff_pic_num_minus1 = ReadGolomb(bits);
                        } else if (slice_header.reordering_of_pic_nums_idc == 2) {
                            slice_header.long_term_pic_num = ReadGolomb(bits);
                        }
                    } while (slice_header.reordering_of_pic_nums_idc != 3);
                }
            }
            if ((slice_header.slice_type % 5) == 1) {
                slice_header.ref_pic_list_reordering_flag_l1 = (uint)bits.ReadBit();
                if (slice_header.ref_pic_list_reordering_flag_l1 != 0) {
                    do {
                        slice_header.reordering_of_pic_nums_idc = ReadGolomb(bits);
                        if (slice_header.reordering_of_pic_nums_idc == 0 ||
                            slice_header.reordering_of_pic_nums_idc == 1) {
                            slice_header.abs_diff_pic_num_minus1 = ReadGolomb(bits);
                        } else if (slice_header.reordering_of_pic_nums_idc == 2) {
                            slice_header.long_term_pic_num = ReadGolomb(bits);
                        }
                    } while (slice_header.reordering_of_pic_nums_idc != 3);
                }
            }

            if ((pps.weighted_pred_flag != 0 &&
                (slice_type == Ap4.AVC_SLICE_TYPE_P || slice_type == Ap4.AVC_SLICE_TYPE_SP)) ||
                (pps.weighted_bipred_idc == 1 && slice_type == Ap4.AVC_SLICE_TYPE_B)) {
                // pred_weight_table
                slice_header.luma_log2_weight_denom = ReadGolomb(bits);

                if (sps.chroma_format_idc != 0) {
                    slice_header.chroma_log2_weight_denom = ReadGolomb(bits);
                }

                for (uint i=0; i<=slice_header.num_ref_idx_l0_active_minus1; i++) {
                    uint luma_weight_l0_flag = (uint)bits.ReadBit();
                    if (luma_weight_l0_flag != 0) {
                        /* slice_header.luma_weight_l0[i] = SignedGolomb( */ ReadGolomb(bits);
                        /* slice_header.luma_offset_l0[i] = SignedGolomb( */ ReadGolomb(bits);
                    }
                    if (sps.chroma_format_idc != 0) {
                        uint chroma_weight_l0_flag = (uint)bits.ReadBit();
                        if (chroma_weight_l0_flag != 0) {
                            for (uint j=0; j<2; j++) {
                                /* slice_header.chroma_weight_l0[i][j] = SignedGolomb( */ ReadGolomb(bits);
                                /* slice_header.chroma_offset_l0[i][j] = SignedGolomb( */ ReadGolomb(bits);
                            }
                        }
                    }
                }
                if ((slice_header.slice_type % 5) == 1) {
                    for (uint i=0; i<=slice_header.num_ref_idx_l1_active_minus1; i++) {
                        uint luma_weight_l1_flag = (uint)bits.ReadBit();
                        if (luma_weight_l1_flag != 0) {
                            /* slice_header.luma_weight_l1[i] = SignedGolomb( */ ReadGolomb(bits);
                            /* slice_header.luma_offset_l1[i] = SignedGolomb( */ ReadGolomb(bits);
                        }
                        if (sps.chroma_format_idc != 0) {
                            uint chroma_weight_l1_flag = (uint)bits.ReadBit();
                            if (chroma_weight_l1_flag != 0) {
                                for (uint j=0; j<2; j++) {
                                    /* slice_header.chroma_weight_l1[i][j] = SignedGolomb( */ ReadGolomb(bits);
                                    /* slice_header.chroma_offset_l1[i][j] = SignedGolomb( */ ReadGolomb(bits);
                                }
                            }
                        }
                    }
                }
            }

            if (nal_ref_idc != 0) {
                // dec_ref_pic_marking
                if (nal_unit_type == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE) {
                    slice_header.no_output_of_prior_pics_flag = (uint)bits.ReadBit();
                    slice_header.long_term_reference_flag     = (uint)bits.ReadBit();
                } else {
                    uint adaptive_ref_pic_marking_mode_flag = (uint)bits.ReadBit();
                    if (adaptive_ref_pic_marking_mode_flag != 0) {
                        uint memory_management_control_operation = 0;
                        do {
                            memory_management_control_operation = ReadGolomb(bits);
                            if (memory_management_control_operation == 1 || memory_management_control_operation == 3) {
                                slice_header.difference_of_pic_nums_minus1 = ReadGolomb(bits);
                            }
                            if (memory_management_control_operation == 2) {
                                slice_header.long_term_pic_num = ReadGolomb(bits);
                            }
                            if (memory_management_control_operation == 3 || memory_management_control_operation == 6) {
                                slice_header.long_term_frame_idx = ReadGolomb(bits);
                            }
                            if (memory_management_control_operation == 4) {
                                slice_header.max_long_term_frame_idx_plus1 = ReadGolomb(bits);
                            }
                        } while (memory_management_control_operation != 0);
                    }
                }
            }
            if (pps.entropy_coding_mode_flag != 0 && slice_type != Ap4.AVC_SLICE_TYPE_I && slice_type != Ap4.AVC_SLICE_TYPE_SI) {
                slice_header.cabac_init_idc = ReadGolomb(bits);
            }
            slice_header.slice_qp_delta = ReadGolomb(bits);
            if (slice_type == Ap4.AVC_SLICE_TYPE_SP || slice_type == Ap4.AVC_SLICE_TYPE_SI) {
                if (slice_type == Ap4.AVC_SLICE_TYPE_SP) {
                    slice_header.sp_for_switch_flag = (uint)bits.ReadBit();
                }
                slice_header.slice_qs_delta = SignedGolomb(ReadGolomb(bits));
            }
            if (pps.deblocking_filter_control_present_flag != 0) {
                slice_header.disable_deblocking_filter_idc = ReadGolomb(bits);
                if (slice_header.disable_deblocking_filter_idc != 1) {
                    slice_header.slice_alpha_c0_offset_div2 = SignedGolomb(ReadGolomb(bits));
                    slice_header.slice_beta_offset_div2     = SignedGolomb(ReadGolomb(bits));
                }
            }
            if (pps.num_slice_groups_minus1 > 0 &&
                pps.slice_group_map_type >= 3   &&
                pps.slice_group_map_type <= 5) {
                slice_header.slice_group_change_cycle = ReadGolomb(bits);
            }

            /* compute the size */
            slice_header.size = bits.GetBitsRead();

            return Ap4Result.SUCCESS;
        }

        public void SetParameterControl(bool isKeep) { _keepParameterSets = isKeep; }

        // methods
        private AvcSequenceParameterSet GetSliceSPS(AvcSliceHeader sh)
        {
            // lookup the SPS
            AvcPictureParameterSet  pps = _PPS[sh.pic_parameter_set_id];
            if (pps == null) return null;
            return _SPS[pps.seq_parameter_set_id];
        }

        /*----------------------------------------------------------------------
        |   AvcFrameParser::SameFrame
        |   14496-10 7.4.1.2.4 Detection of the first VCL NAL unit of a primary coded picture
        +---------------------------------------------------------------------*/
        private bool SameFrame(uint nal_unit_type_1, uint nal_ref_idc_1, AvcSliceHeader sh1,
                               uint nal_unit_type_2, uint nal_ref_idc_2, AvcSliceHeader sh2)
        {
            if (sh1.frame_num != sh2.frame_num) {
                return false;
            }

            if (sh1.pic_parameter_set_id != sh2.pic_parameter_set_id) {
                return false;
            }

            if (sh1.field_pic_flag != sh2.field_pic_flag) {
                return false;
            }

            if (sh1.field_pic_flag != 0) {
                if (sh1.bottom_field_flag != sh2.bottom_field_flag) {
                    return false;
                }
            }

            if ((nal_ref_idc_1 == 0 || nal_ref_idc_2 == 0) && (nal_ref_idc_1 != nal_ref_idc_2)) {
                return false;
            }

            // lookup the SPS
            AvcSequenceParameterSet sps = GetSliceSPS(sh1);
            if (sps == null) return false;

            if (sps.pic_order_cnt_type == 0) {
                if (sh1.pic_order_cnt_lsb      != sh2.pic_order_cnt_lsb ||
                    sh1.delta_pic_order_cnt[0] != sh2.delta_pic_order_cnt[0]) {
                    return false;
                }
            }

            if (sps.pic_order_cnt_type == 1) {
                if (sh1.delta_pic_order_cnt[0] != sh2.delta_pic_order_cnt[0] ||
                    sh1.delta_pic_order_cnt[1] != sh2.delta_pic_order_cnt[1]) {
                    return false;
                }
            }

            if (nal_unit_type_1 == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE ||
                nal_unit_type_2 == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE) {
                if (nal_unit_type_1 != nal_unit_type_2) {
                    return false;
                }
            }

            if (nal_unit_type_1 == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE &&
                nal_unit_type_2 == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE) {
                if (sh1.idr_pic_id != sh2.idr_pic_id) {
                    return false;
                }
            }

            return true;
        }

        private enum AvcPicType {
            AP4_AVC_PIC_TYPE_FRAME,
            AP4_AVC_PIC_TYPE_TOP_FIELD,
            AP4_AVC_PIC_TYPE_BOTTOM_FIELD
        }

        private void CheckIfAccessUnitIsCompleted(AccessUnitInfo access_unit_info)
        {
            if (_sliceHeader == null) {
                return;
            }
            if (_accessUnitVclNalUnitCount == 0) {
                return;
            }
            _accessUnitVclNalUnitCount = 0;

            AvcSequenceParameterSet sps = GetSliceSPS(_sliceHeader);
            if (sps == null) return;
            int max_frame_num = 1 << (int)(sps.log2_max_frame_num_minus4 + 4);

            // compute the picture type
            AvcPicType pic_type;
            if (sps.frame_mbs_only_flag != 0 || _sliceHeader.field_pic_flag == 0) {
                pic_type = AvcPicType.AP4_AVC_PIC_TYPE_FRAME;
            } else if (_sliceHeader.bottom_field_flag != 0) {
                pic_type = AvcPicType.AP4_AVC_PIC_TYPE_BOTTOM_FIELD;
            } else {
                pic_type = AvcPicType.AP4_AVC_PIC_TYPE_TOP_FIELD;
            }

            // prepare to compute the picture order count
            // PORT NOTE: the casts below reproduce the C++ usual arithmetic conversions
            // (int op unsigned -> unsigned, then converted back to int on assignment).
            int top_field_pic_order_cnt = 0;
            int bottom_field_pic_order_cnt = 0;
            uint frame_num_offset = 0;
            uint frame_num = _sliceHeader.frame_num;
            if (_nalUnitType == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE) {
                _prevPicOrderCntMsb = 0;
                _prevPicOrderCntLsb = 0;
            } else {
                if (frame_num < _prevFrameNum) {
                    // wrap around
                    frame_num_offset = unchecked(_prevFrameNumOffset + (uint)max_frame_num);
                } else {
                    frame_num_offset = _prevFrameNumOffset;
                }
            }

            // compute the picture order count
            int pic_order_cnt_msb = 0;
            if (sps.pic_order_cnt_type == 0) {
                uint max_pic_order_cnt_lsb = unchecked((uint)(1 << (int)(sps.log2_max_pic_order_cnt_lsb_minus4 + 4)));
                if (_sliceHeader.pic_order_cnt_lsb < _prevPicOrderCntLsb &&
                    _prevPicOrderCntLsb - _sliceHeader.pic_order_cnt_lsb >= max_pic_order_cnt_lsb/2) {
                    pic_order_cnt_msb = unchecked((int)((uint)_prevPicOrderCntMsb + max_pic_order_cnt_lsb));
                } else if (_sliceHeader.pic_order_cnt_lsb > _prevPicOrderCntLsb &&
                           _sliceHeader.pic_order_cnt_lsb - _prevPicOrderCntLsb > max_pic_order_cnt_lsb/2) {
                    pic_order_cnt_msb = unchecked((int)((uint)_prevPicOrderCntMsb - max_pic_order_cnt_lsb));
                } else {
                    pic_order_cnt_msb = _prevPicOrderCntMsb;
                }

                if (pic_type != AvcPicType.AP4_AVC_PIC_TYPE_BOTTOM_FIELD) {
                    top_field_pic_order_cnt = unchecked((int)((uint)pic_order_cnt_msb+_sliceHeader.pic_order_cnt_lsb));
                }
                if (pic_type != AvcPicType.AP4_AVC_PIC_TYPE_TOP_FIELD) {
                    if (_sliceHeader.field_pic_flag == 0) {
                        bottom_field_pic_order_cnt = unchecked(top_field_pic_order_cnt + _sliceHeader.delta_pic_order_cnt[0]);
                    } else {
                        bottom_field_pic_order_cnt = unchecked((int)((uint)pic_order_cnt_msb + _sliceHeader.pic_order_cnt_lsb));
                    }
                }
            } else if (sps.pic_order_cnt_type == 1) {
                uint abs_frame_num = 0;
                if (sps.num_ref_frames_in_pic_order_cnt_cycle != 0) {
                    abs_frame_num = unchecked(frame_num_offset + frame_num);
                }
                if (_nalRefIdc == 0 && abs_frame_num > 0) {
                    --abs_frame_num;
                }

                int expected_pic_order_cnt = 0;
                if (abs_frame_num >0) {
                    uint pic_order_cnt_cycle_cnt = (abs_frame_num-1)/sps.num_ref_frames_in_pic_order_cnt_cycle;
                    uint frame_num_in_pic_order_cnt_cycle = (abs_frame_num-1)%sps.num_ref_frames_in_pic_order_cnt_cycle;

                    int expected_delta_per_pic_order_cnt_cycle = 0;
                    for (uint i=0; i<sps.num_ref_frames_in_pic_order_cnt_cycle; i++) {
                        expected_delta_per_pic_order_cnt_cycle = unchecked((int)((uint)expected_delta_per_pic_order_cnt_cycle + sps.offset_for_ref_frame[i]));
                    }
                    expected_pic_order_cnt = unchecked((int)(pic_order_cnt_cycle_cnt * (uint)expected_delta_per_pic_order_cnt_cycle));
                    for (uint i=0; i<frame_num_in_pic_order_cnt_cycle; i++) {
                        expected_pic_order_cnt = unchecked((int)((uint)expected_pic_order_cnt + sps.offset_for_ref_frame[i]));
                    }
                }
                if (_nalRefIdc == 0) {
                    expected_pic_order_cnt = unchecked(expected_pic_order_cnt + sps.offset_for_non_ref_pic);
                }

                if (_sliceHeader.field_pic_flag == 0) {
                    top_field_pic_order_cnt    = unchecked(expected_pic_order_cnt + _sliceHeader.delta_pic_order_cnt[0]);
                    bottom_field_pic_order_cnt = unchecked(top_field_pic_order_cnt + sps.offset_for_top_to_bottom_field + _sliceHeader.delta_pic_order_cnt[1]);
                } else if (_sliceHeader.bottom_field_flag == 0) {
                    top_field_pic_order_cnt = unchecked(expected_pic_order_cnt + _sliceHeader.delta_pic_order_cnt[0]);
                } else {
                    bottom_field_pic_order_cnt = unchecked(expected_pic_order_cnt + sps.offset_for_top_to_bottom_field + _sliceHeader.delta_pic_order_cnt[0]);
                }
            } else if (sps.pic_order_cnt_type == 2) {
                int pic_order_cnt_2;
                if (_nalUnitType == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE) {
                    pic_order_cnt_2 = 0;
                } else if (_nalRefIdc == 0) {
                    pic_order_cnt_2 = unchecked((int)(2*(frame_num_offset+frame_num)-1));
                } else {
                    pic_order_cnt_2 = unchecked((int)(2*(frame_num_offset+frame_num)));
                }

                if (_sliceHeader.field_pic_flag == 0) {
                    top_field_pic_order_cnt    = pic_order_cnt_2;
                    bottom_field_pic_order_cnt = pic_order_cnt_2;
                } else if (_sliceHeader.bottom_field_flag != 0) {
                    bottom_field_pic_order_cnt = pic_order_cnt_2;
                } else {
                    top_field_pic_order_cnt = pic_order_cnt_2;
                }
            }

            uint pic_order_cnt;
            if (pic_type == AvcPicType.AP4_AVC_PIC_TYPE_FRAME) {
                pic_order_cnt = unchecked((uint)(top_field_pic_order_cnt < bottom_field_pic_order_cnt ? top_field_pic_order_cnt : bottom_field_pic_order_cnt));
            } else if (pic_type == AvcPicType.AP4_AVC_PIC_TYPE_TOP_FIELD) {
                pic_order_cnt = unchecked((uint)top_field_pic_order_cnt);
            } else {
                pic_order_cnt = unchecked((uint)bottom_field_pic_order_cnt);
            }

            // emit the access unit (transfer ownership)
            // PORT NOTE: AP4_Array::operator= replaces the destination's items with a copy of the source's
            access_unit_info.nal_units.Clear();
            access_unit_info.nal_units.AddRange(_accessUnitData);
            access_unit_info.is_idr        = (_nalUnitType == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE);
            access_unit_info.decode_order  = _totalAccessUnitCount;
            access_unit_info.display_order = pic_order_cnt;
            _accessUnitData.Clear();
            ++_totalAccessUnitCount;

            // update state
            _prevFrameNum       = frame_num;
            _prevFrameNumOffset = frame_num_offset;
            if (_nalRefIdc != 0) {
                _prevPicOrderCntMsb = pic_order_cnt_msb;
                _prevPicOrderCntLsb = _sliceHeader.pic_order_cnt_lsb;
            }
        }

        private void AppendNalUnitData(byte[] data, int data_offset, uint data_size)
        {
            _accessUnitData.Add(new DataBuffer(data, data_offset, data_size));
        }

        // members
        private AvcNalParser               _nalParser = new AvcNalParser();
        private AvcSequenceParameterSet[] _SPS = new AvcSequenceParameterSet[Ap4.AVC_SPS_MAX_ID+1];
        private AvcPictureParameterSet[]  _PPS = new AvcPictureParameterSet[Ap4.AVC_PPS_MAX_ID+1];

        // only updated on new VLC NAL Units
        private uint                       _nalUnitType;
        private uint                       _nalRefIdc;
        private AvcSliceHeader             _sliceHeader;
        private uint                       _accessUnitVclNalUnitCount;

        // accumulator for NAL unit data
        private uint                       _totalNalUnitCount;
        private uint                       _totalAccessUnitCount;
        private List<DataBuffer>           _accessUnitData = new List<DataBuffer>();

        // used to keep track of picture order count
        private uint                       _prevFrameNum;
        private uint                       _prevFrameNumOffset;
        private int                        _prevPicOrderCntMsb;
        private uint                       _prevPicOrderCntLsb;

        // control if the parameter sets(SPS, PPS) need to be stored in stream('mdat')
        private bool                       _keepParameterSets;
    }
}
