// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Codecs/Ap4HevcParser.h, Ap4HevcParser.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
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
        public const uint HEVC_NALU_TYPE_TRAIL_N        = 0;
        public const uint HEVC_NALU_TYPE_TRAIL_R        = 1;
        public const uint HEVC_NALU_TYPE_TSA_N          = 2;
        public const uint HEVC_NALU_TYPE_TSA_R          = 3;
        public const uint HEVC_NALU_TYPE_STSA_N         = 4;
        public const uint HEVC_NALU_TYPE_STSA_R         = 5;
        public const uint HEVC_NALU_TYPE_RADL_N         = 6;
        public const uint HEVC_NALU_TYPE_RADL_R         = 7;
        public const uint HEVC_NALU_TYPE_RASL_N         = 8;
        public const uint HEVC_NALU_TYPE_RASL_R         = 9;
        public const uint HEVC_NALU_TYPE_RSV_VCL_N10    = 10;
        public const uint HEVC_NALU_TYPE_RSV_VCL_R11    = 11;
        public const uint HEVC_NALU_TYPE_RSV_VCL_N12    = 12;
        public const uint HEVC_NALU_TYPE_RSV_VCL_R13    = 13;
        public const uint HEVC_NALU_TYPE_RSV_VCL_N14    = 14;
        public const uint HEVC_NALU_TYPE_RSV_VCL_R15    = 15;
        public const uint HEVC_NALU_TYPE_BLA_W_LP       = 16;
        public const uint HEVC_NALU_TYPE_BLA_W_RADL     = 17;
        public const uint HEVC_NALU_TYPE_BLA_N_LP       = 18;
        public const uint HEVC_NALU_TYPE_IDR_W_RADL     = 19;
        public const uint HEVC_NALU_TYPE_IDR_N_LP       = 20;
        public const uint HEVC_NALU_TYPE_CRA_NUT        = 21;
        public const uint HEVC_NALU_TYPE_RSV_IRAP_VCL22 = 22;
        public const uint HEVC_NALU_TYPE_RSV_IRAP_VCL23 = 23;
        public const uint HEVC_NALU_TYPE_RSV_VCL24      = 24;
        public const uint HEVC_NALU_TYPE_RSV_VCL25      = 25;
        public const uint HEVC_NALU_TYPE_RSV_VCL26      = 26;
        public const uint HEVC_NALU_TYPE_RSV_VCL27      = 27;
        public const uint HEVC_NALU_TYPE_RSV_VCL28      = 28;
        public const uint HEVC_NALU_TYPE_RSV_VCL29      = 29;
        public const uint HEVC_NALU_TYPE_RSV_VCL30      = 30;
        public const uint HEVC_NALU_TYPE_RSV_VCL31      = 31;
        public const uint HEVC_NALU_TYPE_VPS_NUT        = 32;
        public const uint HEVC_NALU_TYPE_SPS_NUT        = 33;
        public const uint HEVC_NALU_TYPE_PPS_NUT        = 34;
        public const uint HEVC_NALU_TYPE_AUD_NUT        = 35;
        public const uint HEVC_NALU_TYPE_EOS_NUT        = 36;
        public const uint HEVC_NALU_TYPE_EOB_NUT        = 37;
        public const uint HEVC_NALU_TYPE_FD_NUT         = 38;
        public const uint HEVC_NALU_TYPE_PREFIX_SEI_NUT = 39;
        public const uint HEVC_NALU_TYPE_SUFFIX_SEI_NUT = 40;
        public const uint HEVC_NALU_TYPE_RSV_NVCL41     = 41;
        public const uint HEVC_NALU_TYPE_RSV_NVCL42     = 42;
        public const uint HEVC_NALU_TYPE_RSV_NVCL43     = 43;
        public const uint HEVC_NALU_TYPE_RSV_NVCL44     = 44;
        public const uint HEVC_NALU_TYPE_RSV_NVCL45     = 45;
        public const uint HEVC_NALU_TYPE_RSV_NVCL46     = 46;
        public const uint HEVC_NALU_TYPE_RSV_NVCL47     = 47;
        public const uint HEVC_NALU_TYPE_UNSPEC62       = 62;
        public const uint HEVC_NALU_TYPE_UNSPEC63       = 63;

        public const uint HEVC_PPS_MAX_ID               = 63;
        public const uint HEVC_SPS_MAX_ID               = 15;
        public const uint HEVC_VPS_MAX_ID               = 15;
        public const uint HEVC_SPS_MAX_RPS              = 64;
        public const uint HEVC_MAX_LT_REFS              = 32;

        public const uint HEVC_ACCESS_UNIT_FLAG_IS_IDR              = 0x01;
        public const uint HEVC_ACCESS_UNIT_FLAG_IS_IRAP             = 0x02;
        public const uint HEVC_ACCESS_UNIT_FLAG_IS_BLA              = 0x04;
        public const uint HEVC_ACCESS_UNIT_FLAG_IS_RADL             = 0x08;
        public const uint HEVC_ACCESS_UNIT_FLAG_IS_RASL             = 0x10;
        public const uint HEVC_ACCESS_UNIT_FLAG_IS_SUBLAYER_NON_REF = 0x20;

        public const uint HEVC_SLICE_TYPE_B = 0;
        public const uint HEVC_SLICE_TYPE_P = 1;
        public const uint HEVC_SLICE_TYPE_I = 2;
    }

    /*----------------------------------------------------------------------
    |   static helpers of Ap4HevcParser.cpp
    +---------------------------------------------------------------------*/
    internal static class HevcParserUtils
    {
        /*----------------------------------------------------------------------
        |   ReadGolomb
        +---------------------------------------------------------------------*/
        internal static uint ReadGolomb(BitReader bits)
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
        internal static int SignedGolomb(uint code_num)
        {
            if ((code_num % 2) != 0) {
                return unchecked((int)((code_num+1)/2));
            } else {
                return unchecked(-((int)code_num/2));
            }
        }

        /*----------------------------------------------------------------------
        |   BitsNeeded
        +---------------------------------------------------------------------*/
        internal static uint BitsNeeded(uint num_values)
        {
            uint bits_needed = 1;
            while (num_values > unchecked((uint)(1 << (int)bits_needed))) {
                ++bits_needed;
                // PORT NOTE: for num_values > 2^31 upstream ends up shifting an int by 32 or more
                // (undefined behaviour, an endless loop with x86 shift semantics). The port stops
                // at 32, the mathematically correct answer, instead of hanging on malformed input.
                if (bits_needed == 32) break;
            }

            return bits_needed;
        }

        /*----------------------------------------------------------------------
        |   scaling_list_data
        +---------------------------------------------------------------------*/
        internal static void scaling_list_data(BitReader bits)
        {
            for (uint sizeId = 0; sizeId < 4; sizeId++) {
                for (uint matrixId = 0; matrixId < (uint)((sizeId == 3)?2:6); matrixId++) {
                    uint flag = (uint)bits.ReadBit(); // scaling_list_pred_mode_flag[ sizeId ][ matrixId ]
                    if (flag == 0) {
                        ReadGolomb(bits); // scaling_list_pred_matrix_id_delta[ sizeId ][ matrixId ]
                    } else {
                        // nextCoef = 8;
                        uint coefNum = (uint)(1 << (int)(4+(sizeId << 1)));
                        if (coefNum > 64) coefNum = 64;
                        if (sizeId > 1) {
                            ReadGolomb(bits); // scaling_list_dc_coef_minus8[ sizeId − 2 ][ matrixId ]
                            // nextCoef = scaling_list_dc_coef_minus8[ sizeId − 2 ][ matrixId ] + 8
                        }
                        for (uint i = 0; i < coefNum; i++) {
                            ReadGolomb(bits); // scaling_list_delta_coef
                            // nextCoef = ( nextCoef + scaling_list_delta_coef + 256 ) % 256
                            // ScalingList[ sizeId ][ matrixId ][ i ] = nextCoef
                        }
                    }
                }
            }
        }

        /*----------------------------------------------------------------------
        |   st_ref_pic_set
        +---------------------------------------------------------------------*/
        internal static Ap4Result parse_st_ref_pic_set(HevcShortTermRefPicSet   rps,
                                                       HevcSequenceParameterSet sps,
                                                       uint                     stRpsIdx,
                                                       uint                     num_short_term_ref_pic_sets,
                                                       BitReader                bits)
        {
            rps.Clear(); // AP4_SetMemory(rps, 0, sizeof(*rps));

            uint inter_ref_pic_set_prediction_flag = 0;
            if (stRpsIdx != 0) {
                inter_ref_pic_set_prediction_flag = (uint)bits.ReadBit();
            }
            if (inter_ref_pic_set_prediction_flag != 0) {
                uint delta_idx_minus1 = 0;
                if (stRpsIdx == num_short_term_ref_pic_sets) {
                    delta_idx_minus1 = ReadGolomb(bits);
                }
                /* delta_rps_sign = */ bits.ReadBit();
                /* abs_delta_rps_minus1 = */ ReadGolomb(bits);
                if (delta_idx_minus1+1 > stRpsIdx) return Ap4Result.ERROR_INVALID_FORMAT; // should not happen
                uint RefRpsIdx = stRpsIdx - (delta_idx_minus1 + 1);
                // PORT NOTE: upstream indexes without a bounds check. delta_idx_minus1 == 0xFFFFFFFF
                // wraps `delta_idx_minus1+1` to 0, passes the test above and yields RefRpsIdx ==
                // stRpsIdx, which is one past the end of the array when stRpsIdx == 64 (C++ reads
                // whatever follows the array). Treated as invalid here instead of throwing.
                if (RefRpsIdx >= Ap4.HEVC_SPS_MAX_RPS) return Ap4Result.ERROR_INVALID_FORMAT;
                uint NumDeltaPocs = sps.short_term_ref_pic_sets[RefRpsIdx].num_delta_pocs;
                for (uint j=0; j<=NumDeltaPocs; j++) {
                    uint used_by_curr_pic_flag /*[j]*/ = (uint)bits.ReadBit();
                    uint use_delta_flag /*[j]*/ = 1;
                    if (used_by_curr_pic_flag == 0 /*[j]*/) {
                        use_delta_flag /*[j]*/ = (uint)bits.ReadBit();
                    }
                    if (used_by_curr_pic_flag != 0 /*[j]*/ || use_delta_flag != 0 /*[j]*/) {
                        rps.num_delta_pocs++;
                    }
                }
            } else {
                rps.num_negative_pics = ReadGolomb(bits);
                rps.num_positive_pics = ReadGolomb(bits);
                if (rps.num_negative_pics > 16 || rps.num_positive_pics > 16) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                rps.num_delta_pocs = rps.num_negative_pics + rps.num_positive_pics;
                for (uint i=0; i<rps.num_negative_pics; i++) {
                    rps.delta_poc_s0_minus1[i] = ReadGolomb(bits);
                    rps.used_by_curr_pic_s0_flag[i] = (uint)bits.ReadBit();
                }
                for (uint i=0; i<rps.num_positive_pics; i++) {
                    rps.delta_poc_s1_minus1[i] = ReadGolomb(bits);
                    rps.used_by_curr_pic_s1_flag[i] = (uint)bits.ReadBit();
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   NumPicTotalCurr
        +---------------------------------------------------------------------*/
        internal static uint NumPicTotalCurr(HevcShortTermRefPicSet  rps,
                                             HevcSliceSegmentHeader  slice_segment_header)
        {
            // compute NumPicTotalCurr
            uint nptc = 0;
            if (rps != null) {
                for (uint i=0; i<rps.num_negative_pics; i++) {
                    if (rps.used_by_curr_pic_s0_flag[i] != 0) {
                        ++nptc;
                    }
                }
                for (uint i=0; i<rps.num_positive_pics; i++) {
                    if (rps.used_by_curr_pic_s1_flag[i] != 0) {
                        ++nptc;
                    }
                }
            }
            for (uint i=0; i<slice_segment_header.num_long_term_sps + slice_segment_header.num_long_term_pics; i++) {
                if (slice_segment_header.used_by_curr_pic_lt_flag[i] != 0) {
                    ++nptc;
                }
            }
            // TODO: for now we assume pps_curr_pic_ref_enabled is 0
            //if (pps_curr_pic_ref_enabled) {
            //    ++nptc;
            //}

            return nptc;
        }
    }

    /*----------------------------------------------------------------------
    |   HevcProfileTierLevel
    +---------------------------------------------------------------------*/
    public class HevcProfileTierLevel {
        public HevcProfileTierLevel()
        {
            general_profile_space = 0;
            general_tier_flag = 0;
            general_profile_idc = 0;
            general_profile_compatibility_flags = 0;
            general_constraint_indicator_flags = 0;
            general_level_idc = 0;
        }

        // methods
        public Ap4Result Parse(BitReader bits, uint max_num_sub_layers_minus_1)
        {
            // profile_tier_level
            general_profile_space               = bits.ReadBits(2);
            general_tier_flag                   = (uint)bits.ReadBit();
            general_profile_idc                 = bits.ReadBits(5);
            general_profile_compatibility_flags = bits.ReadBits(32);

            general_constraint_indicator_flags  = ((ulong)bits.ReadBits(16)) << 32;
            general_constraint_indicator_flags |= bits.ReadBits(32);

            general_level_idc                   = bits.ReadBits(8);
            for (uint i = 0; i < max_num_sub_layers_minus_1; i++) {
                sub_layer_info[i].sub_layer_profile_present_flag = (byte)bits.ReadBit();
                sub_layer_info[i].sub_layer_level_present_flag   = (byte)bits.ReadBit();
            }
            if (max_num_sub_layers_minus_1 != 0) {
                for (uint i = max_num_sub_layers_minus_1; i < 8; i++) {
                    bits.ReadBits(2); // reserved_zero_2bits[i]
                }
            }
            for (uint i = 0; i < max_num_sub_layers_minus_1; i++) {
                if (sub_layer_info[i].sub_layer_profile_present_flag != 0) {
                    sub_layer_info[i].sub_layer_profile_space               = (byte)bits.ReadBits(2);
                    sub_layer_info[i].sub_layer_tier_flag                   = (byte)bits.ReadBit();
                    sub_layer_info[i].sub_layer_profile_idc                 = (byte)bits.ReadBits(5);
                    sub_layer_info[i].sub_layer_profile_compatibility_flags = bits.ReadBits(32);
                    sub_layer_info[i].sub_layer_progressive_source_flag     = (byte)bits.ReadBit();
                    sub_layer_info[i].sub_layer_interlaced_source_flag      = (byte)bits.ReadBit();
                    sub_layer_info[i].sub_layer_non_packed_constraint_flag  = (byte)bits.ReadBit();
                    sub_layer_info[i].sub_layer_frame_only_constraint_flag  = (byte)bits.ReadBit();
                    bits.ReadBits(32); bits.ReadBits(12); // sub_layer_reserved_zero_44bits
                }
                if (sub_layer_info[i].sub_layer_level_present_flag != 0) {
                    sub_layer_info[i].sub_layer_level_idc = (byte)bits.ReadBits(8);
                }
            }

            return Ap4Result.SUCCESS;
        }

        public uint general_profile_space;
        public uint general_tier_flag;
        public uint general_profile_idc;
        public uint general_profile_compatibility_flags;

        // this is a synthetic field that includes 48 bits starting with:
        // general_progressive_source_flag    (1 bit)
        // general_interlaced_source_flag     (1 bit)
        // general_non_packed_constraint_flag (1 bit)
        // general_frame_only_constraint_flag (1 bit)
        // general_reserved_zero_44bits       (44 bits)
        public ulong general_constraint_indicator_flags;

        public uint general_level_idc;
        // PORT NOTE: anonymous struct upstream
        public struct SubLayerInfo {
            public byte sub_layer_profile_present_flag;
            public byte sub_layer_level_present_flag;
            public byte sub_layer_profile_space;
            public byte sub_layer_tier_flag;
            public byte sub_layer_profile_idc;
            public uint sub_layer_profile_compatibility_flags;
            public byte sub_layer_progressive_source_flag;
            public byte sub_layer_interlaced_source_flag;
            public byte sub_layer_non_packed_constraint_flag;
            public byte sub_layer_frame_only_constraint_flag;
            public byte sub_layer_level_idc;
        }
        public SubLayerInfo[] sub_layer_info = new SubLayerInfo[8];
    }

    /*----------------------------------------------------------------------
    |   HevcVuiParameters
    +---------------------------------------------------------------------*/
    public class HevcVuiParameters {
        public HevcVuiParameters()
        {
            aspect_ratio_info_present_flag = 0;
            aspect_ratio_idc = 0;
            sar_width = 0;
            sar_height = 0;
            overscan_info_present_flag = 0;
            overscan_appropriate_flag = 0;
            video_signal_type_present_flag = 0;
            video_format = 0;
            video_full_range_flag = 0;
            colour_description_present_flag = 0;
            colour_primaries = 0;
            transfer_characteristics = 0;
            matrix_coeffs = 0;
        }

        // methods
        // PORT NOTE: as upstream, the `transfer_characteristics` parameter hides the member of the
        // same name: the parsed value is stored through the reference only (the one upstream caller
        // passes the member itself).
        public Ap4Result Parse(BitReader bits, ref uint transfer_characteristics)
        {
            // vui_parameters
            aspect_ratio_info_present_flag = (uint)bits.ReadBit();
            if (aspect_ratio_info_present_flag != 0) {
                aspect_ratio_idc = bits.ReadBits(8);
                if (aspect_ratio_idc == 255) {
                    sar_width  = bits.ReadBits(16);
                    sar_height = bits.ReadBits(16);
                }
            }
            overscan_info_present_flag = (uint)bits.ReadBit();
            if (overscan_info_present_flag != 0) overscan_appropriate_flag = (uint)bits.ReadBit();
            video_signal_type_present_flag = (uint)bits.ReadBit();
            if (video_signal_type_present_flag != 0) {
                video_format                    = bits.ReadBits(3);
                video_full_range_flag           = (uint)bits.ReadBit();
                colour_description_present_flag = (uint)bits.ReadBit();
                if (colour_description_present_flag != 0) {
                    colour_primaries         = bits.ReadBits(8);
                    transfer_characteristics = bits.ReadBits(8);
                    matrix_coeffs            = bits.ReadBits(8);
                }
            }
            return Ap4Result.SUCCESS;
        }

        public uint aspect_ratio_info_present_flag;
        public uint aspect_ratio_idc;
        public uint sar_width;
        public uint sar_height;
        public uint overscan_info_present_flag;
        public uint overscan_appropriate_flag;
        public uint video_signal_type_present_flag;
        public uint video_format;
        public uint video_full_range_flag;
        public uint colour_description_present_flag;
        public uint colour_primaries;
        public uint transfer_characteristics;
        public uint matrix_coeffs;
        //unsigned int chroma_loc_info_present_flag;
        //unsigned int chroma_sample_loc_type_top_field;
        //unsigned int chroma_sample_loc_type_bottom_field;
        //unsigned int neutral_chroma_indication_flag;
        //unsigned int field_seq_flag;
        //unsigned int frame_field_info_present_flag;
        //unsigned int default_display_window_flag;
        //unsigned int def_disp_win_left_offset;
        //unsigned int def_disp_win_right_offset;
        //unsigned int def_disp_win_top_offset;
        //unsigned int def_disp_win_bottom_offset;
        //unsigned int vui_timing_info_present_flag;
        //unsigned int vui_num_units_in_tick;
        //unsigned int vui_time_scale;
        //unsigned int vui_poc_proportional_to_timing_flag;
        //unsigned int vui_num_ticks_poc_diff_one_minus1;
        //unsigned int vui_hrd_parameters_present_flag;

        //// skip hrd_parameters
        //unsigned int bitstream_restriction_flag;
        //unsigned int tiles_fixed_structure_flag;
        //unsigned int motion_vectors_over_pic_boundaries_flag;
        //unsigned int restricted_ref_pic_lists_flag;
        //unsigned int min_spatial_segmentation_idc;
        //unsigned int max_bytes_per_pic_denom;
        //unsigned int max_bits_per_min_cu_denom;
        //unsigned int log2_max_mv_length_horizontal;
        //unsigned int log2_max_mv_length_vertical;
    }

    /*----------------------------------------------------------------------
    |   HevcShortTermRefPicSet
    +---------------------------------------------------------------------*/
    public class HevcShortTermRefPicSet {
        public uint[] delta_poc_s0_minus1 = new uint[16];
        public uint[] delta_poc_s1_minus1 = new uint[16];
        public uint[] used_by_curr_pic_s0_flag = new uint[16];
        public uint[] used_by_curr_pic_s1_flag = new uint[16];
        public uint num_negative_pics;
        public uint num_positive_pics;
        public uint num_delta_pocs;

        // PORT NOTE: replaces AP4_SetMemory(rps, 0, sizeof(*rps))
        public void Clear()
        {
            Array.Clear(delta_poc_s0_minus1, 0, 16);
            Array.Clear(delta_poc_s1_minus1, 0, 16);
            Array.Clear(used_by_curr_pic_s0_flag, 0, 16);
            Array.Clear(used_by_curr_pic_s1_flag, 0, 16);
            num_negative_pics = 0;
            num_positive_pics = 0;
            num_delta_pocs = 0;
        }
    }

    /*----------------------------------------------------------------------
    |   HevcPictureParameterSet
    +---------------------------------------------------------------------*/
    public class HevcPictureParameterSet {
        public HevcPictureParameterSet()
        {
            pps_pic_parameter_set_id = 0;
            pps_seq_parameter_set_id = 0;
            dependent_slice_segments_enabled_flag = 0;
            output_flag_present_flag = 0;
            num_extra_slice_header_bits = 0;
            sign_data_hiding_enabled_flag = 0;
            cabac_init_present_flag = 0;
            num_ref_idx_l0_default_active_minus1 = 0;
            num_ref_idx_l1_default_active_minus1 = 0;
            init_qp_minus26 = 0;
            constrained_intra_pred_flag = 0;
            transform_skip_enabled_flag = 0;
            cu_qp_delta_enabled_flag = 0;
            diff_cu_qp_delta_depth = 0;
            pps_cb_qp_offset = 0;
            pps_cr_qp_offset = 0;
            pps_slice_chroma_qp_offsets_present_flag = 0;
            weighted_pred_flag = 0;
            weighted_bipred_flag = 0;
            transquant_bypass_enabled_flag = 0;
            tiles_enabled_flag = 0;
            entropy_coding_sync_enabled_flag = 0;
            num_tile_columns_minus1 = 0;
            num_tile_rows_minus1 = 0;
            uniform_spacing_flag = 1;
            loop_filter_across_tiles_enabled_flag = 1;
            pps_loop_filter_across_slices_enabled_flag = 0;
            deblocking_filter_control_present_flag = 0;
            deblocking_filter_override_enabled_flag = 0;
            pps_deblocking_filter_disabled_flag = 0;
            pps_beta_offset_div2 = 0;
            pps_tc_offset_div2 = 0;
            pps_scaling_list_data_present_flag = 0;
            lists_modification_present_flag = 0;
            log2_parallel_merge_level_minus2 = 0;
            slice_segment_header_extension_present_flag = 0;
        }

        // methods
        public Ap4Result Parse(byte[] data, uint data_size)
        {
            return Parse(data, 0, data_size);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public Ap4Result Parse(byte[] data, int data_offset, uint data_size)
        {
            raw_bytes.SetData(data, data_offset, data_size);

            DataBuffer unescaped = new DataBuffer(data, data_offset, data_size);
            NalParser.Unescape(unescaped);
            BitReader bits = new BitReader(unescaped.GetData(), unescaped.GetDataSize());

            bits.SkipBits(16); // NAL Unit Header

            pps_pic_parameter_set_id = HevcParserUtils.ReadGolomb(bits);
            if (pps_pic_parameter_set_id > Ap4.HEVC_PPS_MAX_ID) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            pps_seq_parameter_set_id = HevcParserUtils.ReadGolomb(bits);
            if (pps_seq_parameter_set_id > Ap4.HEVC_SPS_MAX_ID) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            dependent_slice_segments_enabled_flag    = (uint)bits.ReadBit();
            output_flag_present_flag                 = (uint)bits.ReadBit();
            num_extra_slice_header_bits              = bits.ReadBits(3);
            sign_data_hiding_enabled_flag            = (uint)bits.ReadBit();
            cabac_init_present_flag                  = (uint)bits.ReadBit();
            num_ref_idx_l0_default_active_minus1     = HevcParserUtils.ReadGolomb(bits);
            num_ref_idx_l1_default_active_minus1     = HevcParserUtils.ReadGolomb(bits);
            init_qp_minus26                          = HevcParserUtils.SignedGolomb(HevcParserUtils.ReadGolomb(bits));
            constrained_intra_pred_flag              = (uint)bits.ReadBit();
            transform_skip_enabled_flag              = (uint)bits.ReadBit();
            cu_qp_delta_enabled_flag                 = (uint)bits.ReadBit();
            if (cu_qp_delta_enabled_flag != 0) {
                diff_cu_qp_delta_depth = HevcParserUtils.ReadGolomb(bits);
            }
            pps_cb_qp_offset                         = HevcParserUtils.SignedGolomb(HevcParserUtils.ReadGolomb(bits));
            pps_cr_qp_offset                         = HevcParserUtils.SignedGolomb(HevcParserUtils.ReadGolomb(bits));
            pps_slice_chroma_qp_offsets_present_flag = (uint)bits.ReadBit();
            weighted_pred_flag                       = (uint)bits.ReadBit();
            weighted_bipred_flag                     = (uint)bits.ReadBit();
            transquant_bypass_enabled_flag           = (uint)bits.ReadBit();
            tiles_enabled_flag                       = (uint)bits.ReadBit();
            entropy_coding_sync_enabled_flag         = (uint)bits.ReadBit();
            if (tiles_enabled_flag != 0) {
                num_tile_columns_minus1 = HevcParserUtils.ReadGolomb(bits);
                num_tile_rows_minus1    = HevcParserUtils.ReadGolomb(bits);
                uniform_spacing_flag    = (uint)bits.ReadBit();
                if (uniform_spacing_flag == 0) {
                    for (uint i=0; i<num_tile_columns_minus1; i++) {
                        HevcParserUtils.ReadGolomb(bits); // column_width_minus1[i]
                    }
                    for (uint i = 0; i < num_tile_rows_minus1; i++) {
                        HevcParserUtils.ReadGolomb(bits); // row_height_minus1[i]
                    }
                }
                loop_filter_across_tiles_enabled_flag = (uint)bits.ReadBit();
            }
            pps_loop_filter_across_slices_enabled_flag = (uint)bits.ReadBit();
            deblocking_filter_control_present_flag     = (uint)bits.ReadBit();
            if (deblocking_filter_control_present_flag != 0) {
                deblocking_filter_override_enabled_flag = (uint)bits.ReadBit();
                pps_deblocking_filter_disabled_flag     = (uint)bits.ReadBit();
                if (pps_deblocking_filter_disabled_flag == 0) {
                    pps_beta_offset_div2 = HevcParserUtils.SignedGolomb(HevcParserUtils.ReadGolomb(bits));
                    pps_tc_offset_div2   = HevcParserUtils.SignedGolomb(HevcParserUtils.ReadGolomb(bits));
                }
            }
            pps_scaling_list_data_present_flag = (uint)bits.ReadBit();
            if (pps_scaling_list_data_present_flag != 0) {
                HevcParserUtils.scaling_list_data(bits);
            }
            lists_modification_present_flag = (uint)bits.ReadBit();
            log2_parallel_merge_level_minus2 = HevcParserUtils.ReadGolomb(bits);
            slice_segment_header_extension_present_flag = (uint)bits.ReadBit();

            return Ap4Result.SUCCESS;
        }

        public DataBuffer raw_bytes = new DataBuffer();
        public uint pps_pic_parameter_set_id;
        public uint pps_seq_parameter_set_id;
        public uint dependent_slice_segments_enabled_flag;
        public uint output_flag_present_flag;
        public uint num_extra_slice_header_bits;
        public uint sign_data_hiding_enabled_flag;
        public uint cabac_init_present_flag;
        public uint num_ref_idx_l0_default_active_minus1;
        public uint num_ref_idx_l1_default_active_minus1;
        public int  init_qp_minus26;
        public uint constrained_intra_pred_flag;
        public uint transform_skip_enabled_flag;
        public uint cu_qp_delta_enabled_flag;
        public uint diff_cu_qp_delta_depth;
        public int  pps_cb_qp_offset;
        public int  pps_cr_qp_offset;
        public uint pps_slice_chroma_qp_offsets_present_flag;
        public uint weighted_pred_flag;
        public uint weighted_bipred_flag;
        public uint transquant_bypass_enabled_flag;
        public uint tiles_enabled_flag;
        public uint entropy_coding_sync_enabled_flag;
        public uint num_tile_columns_minus1;
        public uint num_tile_rows_minus1;
        public uint uniform_spacing_flag;
        public uint loop_filter_across_tiles_enabled_flag;
        public uint pps_loop_filter_across_slices_enabled_flag;
        public uint deblocking_filter_control_present_flag;
        public uint deblocking_filter_override_enabled_flag;
        public uint pps_deblocking_filter_disabled_flag;
        public int  pps_beta_offset_div2;
        public int  pps_tc_offset_div2;
        public uint pps_scaling_list_data_present_flag;
        public uint lists_modification_present_flag;
        public uint log2_parallel_merge_level_minus2;
        public uint slice_segment_header_extension_present_flag;
    }

    /*----------------------------------------------------------------------
    |   HevcSequenceParameterSet
    +---------------------------------------------------------------------*/
    public class HevcSequenceParameterSet {
        public HevcSequenceParameterSet()
        {
            sps_video_parameter_set_id = 0;
            sps_max_sub_layers_minus1 = 0;
            sps_temporal_id_nesting_flag = 0;
            sps_seq_parameter_set_id = 0;
            chroma_format_idc = 0;
            separate_colour_plane_flag = 0;
            pic_width_in_luma_samples = 0;
            pic_height_in_luma_samples = 0;
            conformance_window_flag = 0;
            conf_win_left_offset = 0;
            conf_win_right_offset = 0;
            conf_win_top_offset = 0;
            conf_win_bottom_offset = 0;
            bit_depth_luma_minus8 = 0;
            bit_depth_chroma_minus8 = 0;
            log2_max_pic_order_cnt_lsb_minus4 = 0;
            sps_sub_layer_ordering_info_present_flag = 0;
            log2_min_luma_coding_block_size_minus3 = 0;
            log2_diff_max_min_luma_coding_block_size = 0;
            log2_min_transform_block_size_minus2 = 0;
            log2_diff_max_min_transform_block_size = 0;
            max_transform_hierarchy_depth_inter = 0;
            max_transform_hierarchy_depth_intra = 0;
            scaling_list_enabled_flag = 0;
            sps_scaling_list_data_present_flag = 0;
            amp_enabled_flag = 0;
            sample_adaptive_offset_enabled_flag = 0;
            pcm_enabled_flag = 0;
            pcm_sample_bit_depth_luma_minus1 = 0;
            pcm_sample_bit_depth_chroma_minus1 = 0;
            log2_min_pcm_luma_coding_block_size_minus3 = 0;
            log2_diff_max_min_pcm_luma_coding_block_size = 0;
            pcm_loop_filter_disabled_flag = 0;
            num_short_term_ref_pic_sets = 0;
            long_term_ref_pics_present_flag = 0;
            num_long_term_ref_pics_sps = 0;
            sps_temporal_mvp_enabled_flag = 0;
            strong_intra_smoothing_enabled_flag = 0;
            vui_parameters_present_flag = 0;

            // profile_tier_level, vui_parameters: zero filled by their constructors
            for (uint i=0; i<8; i++) {
                sps_max_dec_pic_buffering_minus1[i] = 0;
                sps_max_num_reorder_pics[i]         = 0;
                sps_max_latency_increase_plus1[i]   = 0;
            }
            for (uint i=0; i<Ap4.HEVC_SPS_MAX_RPS; i++) {
                short_term_ref_pic_sets[i] = new HevcShortTermRefPicSet();
            }
        }

        // methods
        public Ap4Result Parse(byte[] data, uint data_size)
        {
            return Parse(data, 0, data_size);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public Ap4Result Parse(byte[] data, int data_offset, uint data_size)
        {
            raw_bytes.SetData(data, data_offset, data_size);

            DataBuffer unescaped = new DataBuffer(data, data_offset, data_size);
            NalParser.Unescape(unescaped);
            BitReader bits = new BitReader(unescaped.GetData(), unescaped.GetDataSize());

            bits.SkipBits(16); // NAL Unit Header

            sps_video_parameter_set_id   = bits.ReadBits(4);
            sps_max_sub_layers_minus1    = bits.ReadBits(3);
            sps_temporal_id_nesting_flag = (uint)bits.ReadBit();

            Ap4Result result = profile_tier_level.Parse(bits, sps_max_sub_layers_minus1);
            if (result.Failed()) {
                return result;
            }

            sps_seq_parameter_set_id = HevcParserUtils.ReadGolomb(bits);
            if (sps_seq_parameter_set_id > Ap4.HEVC_SPS_MAX_ID) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            chroma_format_idc = HevcParserUtils.ReadGolomb(bits);
            if (chroma_format_idc == 3) {
                separate_colour_plane_flag = (uint)bits.ReadBit();
            }
            pic_width_in_luma_samples  = HevcParserUtils.ReadGolomb(bits);
            pic_height_in_luma_samples = HevcParserUtils.ReadGolomb(bits);
            conformance_window_flag    = (uint)bits.ReadBit();

            if (conformance_window_flag != 0) {
                conf_win_left_offset    = HevcParserUtils.ReadGolomb(bits);
                conf_win_right_offset   = HevcParserUtils.ReadGolomb(bits);
                conf_win_top_offset     = HevcParserUtils.ReadGolomb(bits);
                conf_win_bottom_offset  = HevcParserUtils.ReadGolomb(bits);
            }
            bit_depth_luma_minus8                    = HevcParserUtils.ReadGolomb(bits);
            bit_depth_chroma_minus8                  = HevcParserUtils.ReadGolomb(bits);
            log2_max_pic_order_cnt_lsb_minus4        = HevcParserUtils.ReadGolomb(bits);
            if (log2_max_pic_order_cnt_lsb_minus4 > 16) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            sps_sub_layer_ordering_info_present_flag = (uint)bits.ReadBit();
            for (uint i = (sps_sub_layer_ordering_info_present_flag != 0 ? 0 : sps_max_sub_layers_minus1);
                      i <= sps_max_sub_layers_minus1;
                      i++) {
                sps_max_dec_pic_buffering_minus1[i] = HevcParserUtils.ReadGolomb(bits);
                sps_max_num_reorder_pics[i]         = HevcParserUtils.ReadGolomb(bits);
                sps_max_latency_increase_plus1[i]   = HevcParserUtils.ReadGolomb(bits);
            }
            log2_min_luma_coding_block_size_minus3   = HevcParserUtils.ReadGolomb(bits);
            log2_diff_max_min_luma_coding_block_size = HevcParserUtils.ReadGolomb(bits);
            log2_min_transform_block_size_minus2     = HevcParserUtils.ReadGolomb(bits);
            log2_diff_max_min_transform_block_size   = HevcParserUtils.ReadGolomb(bits);
            max_transform_hierarchy_depth_inter      = HevcParserUtils.ReadGolomb(bits);
            max_transform_hierarchy_depth_intra      = HevcParserUtils.ReadGolomb(bits);
            scaling_list_enabled_flag                = (uint)bits.ReadBit();
            if (scaling_list_enabled_flag != 0) {
                sps_scaling_list_data_present_flag = (uint)bits.ReadBit();
                if (sps_scaling_list_data_present_flag != 0) {
                    HevcParserUtils.scaling_list_data(bits);
                }
            }
            amp_enabled_flag = (uint)bits.ReadBit();
            sample_adaptive_offset_enabled_flag = (uint)bits.ReadBit();
            pcm_enabled_flag = (uint)bits.ReadBit();
            if (pcm_enabled_flag != 0) {
                pcm_sample_bit_depth_luma_minus1 = bits.ReadBits(4);
                pcm_sample_bit_depth_chroma_minus1 = bits.ReadBits(4);
                log2_min_pcm_luma_coding_block_size_minus3 = HevcParserUtils.ReadGolomb(bits);
                log2_diff_max_min_pcm_luma_coding_block_size = HevcParserUtils.ReadGolomb(bits);
                pcm_loop_filter_disabled_flag = (uint)bits.ReadBit();
            }
            num_short_term_ref_pic_sets = HevcParserUtils.ReadGolomb(bits);
            if (num_short_term_ref_pic_sets > Ap4.HEVC_SPS_MAX_RPS) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            for (uint i=0; i<num_short_term_ref_pic_sets; i++) {
                result = HevcParserUtils.parse_st_ref_pic_set(short_term_ref_pic_sets[i], this, i, num_short_term_ref_pic_sets, bits);
                if (result.Failed()) return result;
            }
            long_term_ref_pics_present_flag = (uint)bits.ReadBit();
            if (long_term_ref_pics_present_flag != 0) {
                num_long_term_ref_pics_sps = HevcParserUtils.ReadGolomb(bits);
                for (uint i=0; i<num_long_term_ref_pics_sps; i++) {
                    /* lt_ref_pic_poc_lsb_sps[i] = */ bits.ReadBits(log2_max_pic_order_cnt_lsb_minus4 + 4);
                    /* used_by_curr_pic_lt_sps_flag[i] = */ bits.ReadBit();
                }
            }
            sps_temporal_mvp_enabled_flag  = (uint)bits.ReadBit();
            strong_intra_smoothing_enabled_flag = (uint)bits.ReadBit();
            vui_parameters_present_flag = (uint)bits.ReadBit();
            if (vui_parameters_present_flag != 0) {
                Ap4Result result2 = vui_parameters.Parse(bits, ref vui_parameters.transfer_characteristics);
                if (result2.Failed()) {
                    return result2;
                }
            }

            return Ap4Result.SUCCESS;
        }

        public void GetInfo(out uint width, out uint height)
        {
            width  = pic_width_in_luma_samples;
            height = pic_height_in_luma_samples;
        }

        public DataBuffer           raw_bytes = new DataBuffer();
        public uint                 sps_video_parameter_set_id;
        public uint                 sps_max_sub_layers_minus1;
        public uint                 sps_temporal_id_nesting_flag;
        public HevcProfileTierLevel profile_tier_level = new HevcProfileTierLevel();
        public uint                 sps_seq_parameter_set_id;
        public uint                 chroma_format_idc;
        public uint                 separate_colour_plane_flag;
        public uint                 pic_width_in_luma_samples;
        public uint                 pic_height_in_luma_samples;
        public uint                 conformance_window_flag;
        public uint                 conf_win_left_offset;
        public uint                 conf_win_right_offset;
        public uint                 conf_win_top_offset;
        public uint                 conf_win_bottom_offset;
        public uint                 bit_depth_luma_minus8;
        public uint                 bit_depth_chroma_minus8;
        public uint[]               sps_max_dec_pic_buffering_minus1 = new uint[8];
        public uint[]               sps_max_num_reorder_pics = new uint[8];
        public uint[]               sps_max_latency_increase_plus1 = new uint[8];
        public uint                 log2_max_pic_order_cnt_lsb_minus4;
        public uint                 sps_sub_layer_ordering_info_present_flag;
        public uint                 log2_min_luma_coding_block_size_minus3;
        public uint                 log2_diff_max_min_luma_coding_block_size;
        public uint                 log2_min_transform_block_size_minus2;
        public uint                 log2_diff_max_min_transform_block_size;
        public uint                 max_transform_hierarchy_depth_inter;
        public uint                 max_transform_hierarchy_depth_intra;
        public uint                 scaling_list_enabled_flag;
        public uint                 sps_scaling_list_data_present_flag;
                                    // skipped scaling list data
        public uint                 amp_enabled_flag;
        public uint                 sample_adaptive_offset_enabled_flag;
        public uint                 pcm_enabled_flag;
        public uint                 pcm_sample_bit_depth_luma_minus1;
        public uint                 pcm_sample_bit_depth_chroma_minus1;
        public uint                 log2_min_pcm_luma_coding_block_size_minus3;
        public uint                 log2_diff_max_min_pcm_luma_coding_block_size;
        public uint                 pcm_loop_filter_disabled_flag;
        public uint                 num_short_term_ref_pic_sets;
        public uint                 long_term_ref_pics_present_flag;
        public uint                 num_long_term_ref_pics_sps;
        public uint                 sps_temporal_mvp_enabled_flag;
        public uint                 strong_intra_smoothing_enabled_flag;
        public uint                 vui_parameters_present_flag;
        public HevcVuiParameters    vui_parameters = new HevcVuiParameters();
        public HevcShortTermRefPicSet[] short_term_ref_pic_sets = new HevcShortTermRefPicSet[Ap4.HEVC_SPS_MAX_RPS];
    }

    /*----------------------------------------------------------------------
    |   HevcVideoParameterSet
    +---------------------------------------------------------------------*/
    public class HevcVideoParameterSet {
        public HevcVideoParameterSet()
        {
            vps_video_parameter_set_id = 0;
            vps_max_layers_minus1 = 0;
            vps_max_sub_layers_minus1 = 0;
            vps_temporal_id_nesting_flag = 0;
            vps_sub_layer_ordering_info_present_flag = 0;
            vps_max_layer_id = 0;
            vps_num_layer_sets_minus1 = 0;
            vps_timing_info_present_flag = 0;
            vps_num_units_in_tick = 0;
            vps_time_scale = 0;
            vps_poc_proportional_to_timing_flag = 0;
            vps_num_ticks_poc_diff_one_minus1 = 0;

            // profile_tier_level: zero filled by its constructor
            for (uint i=0; i<8; i++) {
                vps_max_dec_pic_buffering_minus1[i] = 0;
                vps_max_num_reorder_pics[i]         = 0;
                vps_max_latency_increase_plus1[i]   = 0;
            }
        }

        // methods
        public Ap4Result Parse(byte[] data, uint data_size)
        {
            return Parse(data, 0, data_size);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public Ap4Result Parse(byte[] data, int data_offset, uint data_size)
        {
            raw_bytes.SetData(data, data_offset, data_size);

            DataBuffer unescaped = new DataBuffer(data, data_offset, data_size);
            NalParser.Unescape(unescaped);
            BitReader bits = new BitReader(unescaped.GetData(), unescaped.GetDataSize());

            bits.SkipBits(16); // NAL Unit Header

            vps_video_parameter_set_id     = bits.ReadBits(4);
            /* vps_reserved_three_2bits */   bits.ReadBits(2);
            vps_max_layers_minus1          = bits.ReadBits(6);
            vps_max_sub_layers_minus1      = bits.ReadBits(3);
            vps_temporal_id_nesting_flag   = (uint)bits.ReadBit();
            /* vps_reserved_0xffff_16bits */ bits.ReadBits(16);
            profile_tier_level.Parse(bits, vps_max_sub_layers_minus1);
            vps_sub_layer_ordering_info_present_flag = (uint)bits.ReadBit();
            for (uint i = (vps_sub_layer_ordering_info_present_flag != 0 ? 0 : vps_max_sub_layers_minus1);
                      i <= vps_max_sub_layers_minus1;
                      i++) {
                vps_max_dec_pic_buffering_minus1[i] = HevcParserUtils.ReadGolomb(bits);
                vps_max_num_reorder_pics[i]         = HevcParserUtils.ReadGolomb(bits);
                vps_max_latency_increase_plus1[i]   = HevcParserUtils.ReadGolomb(bits);
            }
            vps_max_layer_id          = bits.ReadBits(6);
            vps_num_layer_sets_minus1 = HevcParserUtils.ReadGolomb(bits);
            for (uint i = 1; i <= vps_num_layer_sets_minus1; i++) {
                for (uint j = 0; j <= vps_max_layer_id; j++) {
                    bits.ReadBit();
                }
            }
            vps_timing_info_present_flag = (uint)bits.ReadBit();
            if (vps_timing_info_present_flag != 0) {
                vps_num_units_in_tick               = bits.ReadBits(32);
                vps_time_scale                      = bits.ReadBits(32);
                vps_poc_proportional_to_timing_flag = (uint)bits.ReadBit();
                if (vps_poc_proportional_to_timing_flag != 0) {
                    vps_num_ticks_poc_diff_one_minus1 = HevcParserUtils.ReadGolomb(bits);
                }
            }

            return Ap4Result.SUCCESS;
        }

        public void GetInfo(out uint time_scale, out uint num_units)
        {
            time_scale = vps_time_scale;
            num_units  = vps_num_units_in_tick;
        }

        public DataBuffer           raw_bytes = new DataBuffer();
        public uint                 vps_video_parameter_set_id;
        public uint                 vps_max_layers_minus1;
        public uint                 vps_max_sub_layers_minus1;
        public uint                 vps_temporal_id_nesting_flag;
        public HevcProfileTierLevel profile_tier_level = new HevcProfileTierLevel();
        public uint                 vps_sub_layer_ordering_info_present_flag;
        public uint[]               vps_max_dec_pic_buffering_minus1 = new uint[8];
        public uint[]               vps_max_num_reorder_pics = new uint[8];
        public uint[]               vps_max_latency_increase_plus1 = new uint[8];
        public uint                 vps_max_layer_id;
        public uint                 vps_num_layer_sets_minus1;
        public uint                 vps_timing_info_present_flag;
        public uint                 vps_num_units_in_tick;
        public uint                 vps_time_scale;
        public uint                 vps_poc_proportional_to_timing_flag;
        public uint                 vps_num_ticks_poc_diff_one_minus1;
    }

    /*----------------------------------------------------------------------
    |   HevcSliceSegmentHeader
    +---------------------------------------------------------------------*/
    public class HevcSliceSegmentHeader {
        // PORT NOTE: upstream leaves the members uninitialized on purpose; they are zero here
        public HevcSliceSegmentHeader() {}

        public Ap4Result Parse(byte[]                      data,
                               uint                        data_size,
                               uint                        nal_unit_type,
                               HevcPictureParameterSet[]  picture_parameter_sets,
                               HevcSequenceParameterSet[] sequence_parameter_sets)
        {
            return Parse(data, 0, data_size, nal_unit_type, picture_parameter_sets, sequence_parameter_sets);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer.
        // The parameter set arrays are the frame parser's (PPS_MAX_ID+1 and SPS_MAX_ID+1 entries).
        public Ap4Result Parse(byte[]                      data,
                               int                         data_offset,
                               uint                        data_size,
                               uint                        nal_unit_type,
                               HevcPictureParameterSet[]  picture_parameter_sets,
                               HevcSequenceParameterSet[] sequence_parameter_sets)
        {
            // initialize all members to 0
            size = 0;
            first_slice_segment_in_pic_flag = 0;
            no_output_of_prior_pics_flag = 0;
            slice_pic_parameter_set_id = 0;
            dependent_slice_segment_flag = 0;
            slice_segment_address = 0;
            slice_type = 0;
            pic_output_flag = 0;
            colour_plane_id = 0;
            slice_pic_order_cnt_lsb = 0;
            short_term_ref_pic_set_sps_flag = 0;
            short_term_ref_pic_set_idx = 0;
            num_entry_point_offsets = 0;
            offset_len_minus1 = 0;
            num_long_term_sps = 0;
            num_long_term_pics = 0;
            short_term_ref_pic_set.Clear();
            Array.Clear(used_by_curr_pic_lt_flag, 0, used_by_curr_pic_lt_flag.Length);

            // some fields default to 1
            pic_output_flag = 1;

            // start the parser
            DataBuffer unescaped = new DataBuffer(data, data_offset, data_size);
            NalParser.Unescape(unescaped);
            BitReader bits = new BitReader(unescaped.GetData(), unescaped.GetDataSize());

            first_slice_segment_in_pic_flag = (uint)bits.ReadBit();
            if (nal_unit_type >= Ap4.HEVC_NALU_TYPE_BLA_W_LP && nal_unit_type <= Ap4.HEVC_NALU_TYPE_RSV_IRAP_VCL23) {
                no_output_of_prior_pics_flag = (uint)bits.ReadBit();
            }
            slice_pic_parameter_set_id = HevcParserUtils.ReadGolomb(bits);
            if (slice_pic_parameter_set_id > Ap4.HEVC_PPS_MAX_ID) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            HevcPictureParameterSet pps = picture_parameter_sets[slice_pic_parameter_set_id];
            if (pps == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            HevcSequenceParameterSet sps = sequence_parameter_sets[pps.pps_seq_parameter_set_id];
            if (sps == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // PicSizeInCtbsY = PicWidthInCtbsY * PicHeightInCtbsY
            // PicWidthInCtbsY = Ceil( pic_width_in_luma_samples / CtbSizeY )
            // PicHeightInCtbsY = Ceil( pic_height_in_luma_samples / CtbSizeY )
            // CtbSizeY = 1 << CtbLog2SizeY
            // CtbLog2SizeY = MinCbLog2SizeY + log2_diff_max_min_luma_coding_block_size
            // MinCbLog2SizeY = log2_min_luma_coding_block_size_minus3 + 3
            uint MinCbLog2SizeY   = sps.log2_min_luma_coding_block_size_minus3 + 3;
            uint CtbLog2SizeY     = MinCbLog2SizeY + sps.log2_diff_max_min_luma_coding_block_size;
            uint CtbSizeY         = unchecked((uint)(1 << (int)CtbLog2SizeY));
            uint PicWidthInCtbsY  = (sps.pic_width_in_luma_samples + CtbSizeY - 1) / CtbSizeY;
            uint PicHeightInCtbsY = (sps.pic_height_in_luma_samples + CtbSizeY - 1) / CtbSizeY;
            uint PicSizeInCtbsY   = PicWidthInCtbsY * PicHeightInCtbsY;

            if (first_slice_segment_in_pic_flag == 0) {
                if (pps.dependent_slice_segments_enabled_flag != 0) {
                    dependent_slice_segment_flag = (uint)bits.ReadBit();
                }

                uint bits_needed = HevcParserUtils.BitsNeeded(PicSizeInCtbsY);
                if (bits_needed != 0) {
                    slice_segment_address = bits.ReadBits(bits_needed);
                }
            }

            if (dependent_slice_segment_flag == 0) {
                if (pps.num_extra_slice_header_bits != 0) {
                    bits.ReadBits(pps.num_extra_slice_header_bits); // slice_reserved_flag[...]
                }

                slice_type = HevcParserUtils.ReadGolomb(bits);
                if (slice_type != Ap4.HEVC_SLICE_TYPE_B && slice_type != Ap4.HEVC_SLICE_TYPE_P && slice_type != Ap4.HEVC_SLICE_TYPE_I) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                if (pps.output_flag_present_flag != 0) {
                    pic_output_flag = (uint)bits.ReadBit();
                }
                if (sps.separate_colour_plane_flag != 0) {
                    colour_plane_id = bits.ReadBits(2);
                }
                uint slice_sao_luma_flag = 0;
                uint slice_sao_chroma_flag = 0;
                uint slice_deblocking_filter_disabled_flag = 0;
                uint slice_temporal_mvp_enabled_flag = 0;
                HevcShortTermRefPicSet rps = null;
                if (nal_unit_type != Ap4.HEVC_NALU_TYPE_IDR_W_RADL && nal_unit_type != Ap4.HEVC_NALU_TYPE_IDR_N_LP) {
                    slice_pic_order_cnt_lsb = bits.ReadBits(sps.log2_max_pic_order_cnt_lsb_minus4+4);
                    short_term_ref_pic_set_sps_flag = (uint)bits.ReadBit();
                    if (short_term_ref_pic_set_sps_flag == 0) {
                        Ap4Result result = HevcParserUtils.parse_st_ref_pic_set(short_term_ref_pic_set,
                                                                                sps,
                                                                                sps.num_short_term_ref_pic_sets,
                                                                                sps.num_short_term_ref_pic_sets,
                                                                                bits);
                        if (result.Failed()) return result;
                        rps = short_term_ref_pic_set;
                    } else if (sps.num_short_term_ref_pic_sets > 1) {
                        short_term_ref_pic_set_idx = bits.ReadBits(HevcParserUtils.BitsNeeded(sps.num_short_term_ref_pic_sets));
                        // PORT NOTE: upstream indexes without a bounds check; the index is at most
                        // 6 bits wide here (num_short_term_ref_pic_sets <= 64), so it is always in range.
                        rps = sps.short_term_ref_pic_sets[short_term_ref_pic_set_idx];
                    }

                    if (sps.long_term_ref_pics_present_flag != 0) {
                        if (sps.num_long_term_ref_pics_sps > 0) {
                            num_long_term_sps = HevcParserUtils.ReadGolomb(bits);
                        }
                        num_long_term_pics = HevcParserUtils.ReadGolomb(bits);

                        if (num_long_term_sps > sps.num_long_term_ref_pics_sps) {
                            return Ap4Result.ERROR_INVALID_FORMAT;
                        }
                        if (num_long_term_sps + num_long_term_pics > Ap4.HEVC_MAX_LT_REFS) {
                            return Ap4Result.ERROR_INVALID_FORMAT;
                        }
                        for (uint i=0; i<num_long_term_sps + num_long_term_pics; i++) {
                            if (i < num_long_term_sps) {
                                if (sps.num_long_term_ref_pics_sps > 1) {
                                    /* lt_idx_sps[i] = */ bits.ReadBits(HevcParserUtils.BitsNeeded(sps.num_long_term_ref_pics_sps));
                                }
                            } else {
                                /* poc_lsb_lt[i] = */ bits.ReadBits(sps.log2_max_pic_order_cnt_lsb_minus4+4);
                                used_by_curr_pic_lt_flag[i] = (uint)bits.ReadBit();
                            }
                            uint delta_poc_msb_present_flag /*[i]*/ = (uint)bits.ReadBit();
                            if (delta_poc_msb_present_flag != 0 /*[i]*/) {
                                /* delta_poc_msb_cycle_lt[i] = */ HevcParserUtils.ReadGolomb(bits);
                            }
                        }
                    }
                    if (sps.sps_temporal_mvp_enabled_flag != 0) {
                        slice_temporal_mvp_enabled_flag = (uint)bits.ReadBit();
                    }
                }
                if (sps.sample_adaptive_offset_enabled_flag != 0) {
                    slice_sao_luma_flag   = (uint)bits.ReadBit();
                    uint ChromaArrayType = sps.separate_colour_plane_flag != 0 ? 0 : sps.chroma_format_idc;
                    if (ChromaArrayType != 0) {
                        slice_sao_chroma_flag = (uint)bits.ReadBit();
                    }
                }
                if (slice_type == Ap4.HEVC_SLICE_TYPE_P || slice_type == Ap4.HEVC_SLICE_TYPE_B) {
                    uint num_ref_idx_l0_active_minus1 = pps.num_ref_idx_l0_default_active_minus1;
                    uint num_ref_idx_l1_active_minus1 = pps.num_ref_idx_l1_default_active_minus1;
                    uint num_ref_idx_active_override_flag = (uint)bits.ReadBit();
                    if (num_ref_idx_active_override_flag != 0) {
                        num_ref_idx_l0_active_minus1 = HevcParserUtils.ReadGolomb(bits);
                        if (slice_type == Ap4.HEVC_SLICE_TYPE_B) {
                            num_ref_idx_l1_active_minus1 = HevcParserUtils.ReadGolomb(bits);
                        }
                    }
                    if (num_ref_idx_l0_active_minus1 > 14 || num_ref_idx_l1_active_minus1 > 14) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                    uint nptc = HevcParserUtils.NumPicTotalCurr(rps, this);
                    if (pps.lists_modification_present_flag != 0 && nptc > 1) {
                        // ref_pic_lists_modification
                        uint ref_pic_list_modification_flag_l0 = (uint)bits.ReadBit();
                        if (ref_pic_list_modification_flag_l0 != 0) {
                            for (uint i=0; i<=num_ref_idx_l0_active_minus1; i++) {
                                /* list_entry_l0[i]; */ bits.ReadBits(HevcParserUtils.BitsNeeded(nptc));
                            }
                        }
                        if (slice_type == Ap4.HEVC_SLICE_TYPE_B) {
                            uint ref_pic_list_modification_flag_l1 = (uint)bits.ReadBit();
                            if (ref_pic_list_modification_flag_l1 != 0) {
                                for (uint i=0; i<=num_ref_idx_l1_active_minus1; i++) {
                                    /* list_entry_l1[i]; */ bits.ReadBits(HevcParserUtils.BitsNeeded(nptc));
                                }
                            }
                        }
                    }
                    if (slice_type == Ap4.HEVC_SLICE_TYPE_B) {
                        /* mvd_l1_zero_flag = */ bits.ReadBit();
                    }
                    if (pps.cabac_init_present_flag != 0) {
                        /* cabac_init_flag = */ bits.ReadBit();
                    }
                    if (slice_temporal_mvp_enabled_flag != 0) {
                        uint collocated_from_l0_flag = 1;
                        if (slice_type == Ap4.HEVC_SLICE_TYPE_B) {
                            collocated_from_l0_flag = (uint)bits.ReadBit();
                        }
                        if ((collocated_from_l0_flag != 0 && num_ref_idx_l0_active_minus1 > 0) ||
                            (collocated_from_l0_flag == 0 && num_ref_idx_l1_active_minus1 > 0)) {
                            /* collocated_ref_idx = */ HevcParserUtils.ReadGolomb(bits);
                        }
                    }
                    if ((pps.weighted_pred_flag   != 0 && slice_type == Ap4.HEVC_SLICE_TYPE_P) ||
                        (pps.weighted_bipred_flag != 0 && slice_type == Ap4.HEVC_SLICE_TYPE_B)) {
                        // +++ pred_weight_table()
                        /* luma_log2_weight_denom = */ HevcParserUtils.ReadGolomb(bits);
                        if (sps.chroma_format_idc != 0) {
                            /* delta_chroma_log2_weight_denom = */ /* SignedGolomb( */ HevcParserUtils.ReadGolomb(bits) /*)*/;
                        }
                        uint[] luma_weight_l0_flag = new uint[16];
                        for (uint i=0; i<=num_ref_idx_l0_active_minus1; i++) {
                            luma_weight_l0_flag[i] = (uint)bits.ReadBit();
                        }
                        uint[] chroma_weight_l0_flag = new uint[16];
                        if (sps.chroma_format_idc != 0) {
                            for (uint i=0; i<=num_ref_idx_l0_active_minus1; i++) {
                                chroma_weight_l0_flag[i] = (uint)bits.ReadBit();
                            }
                        }
                        for (uint i=0; i<=num_ref_idx_l0_active_minus1; i++) {
                            if (luma_weight_l0_flag[i] != 0) {
                                /* delta_luma_weight_l0[i] = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                                /* luma_offset_l0[i] = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                            }
                            if (chroma_weight_l0_flag[i] != 0) {
                                for (uint j=0; j<2; j++) {
                                    /* delta_chroma_weight_l0[i][j] = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                                    /* delta_chroma_offset_l0[i][j] = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                                }
                            }
                        }
                        if (slice_type == Ap4.HEVC_SLICE_TYPE_B) {
                            uint[] luma_weight_l1_flag = new uint[16];
                            for (uint i=0; i<=num_ref_idx_l1_active_minus1; i++) {
                                luma_weight_l1_flag[i] = (uint)bits.ReadBit();
                            }
                            uint[] chroma_weight_l1_flag = new uint[16];
                            if (sps.chroma_format_idc != 0) {
                                for (uint i=0; i<=num_ref_idx_l1_active_minus1; i++) {
                                    chroma_weight_l1_flag[i] = (uint)bits.ReadBit();
                                }
                            }
                            for (uint i=0; i<=num_ref_idx_l1_active_minus1; i++) {
                                if (luma_weight_l1_flag[i] != 0) {
                                    /* delta_luma_weight_l1[i] = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                                    /* luma_offset_l1[i] = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                                }
                                if (chroma_weight_l1_flag[i] != 0) {
                                    for (uint j=0; j<2; j++) {
                                        /* delta_chroma_weight_l1[i][j] = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                                        /* delta_chroma_offset_l1[i][j] = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                                    }
                                }
                            }
                        }
                        // --- pred_weight_table()
                    }
                    /* five_minus_max_num_merge_cand = */ HevcParserUtils.ReadGolomb(bits);
                }
                /* slice_qp_delta = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                if (pps.pps_slice_chroma_qp_offsets_present_flag != 0) {
                    /* slice_cb_qp_offset = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                    /* slice_cr_qp_offset = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                }
                uint deblocking_filter_override_flag = 0;
                if (pps.deblocking_filter_override_enabled_flag != 0) {
                    deblocking_filter_override_flag = (uint)bits.ReadBit();
                }
                if (deblocking_filter_override_flag != 0) {
                    slice_deblocking_filter_disabled_flag = (uint)bits.ReadBit();
                    if (slice_deblocking_filter_disabled_flag == 0) {
                        /* slice_beta_offset_div2 = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                        /* slice_tc_offset_div2   = */ /*SignedGolomb(*/ HevcParserUtils.ReadGolomb(bits) /*)*/;
                    }
                }
                if (pps.pps_loop_filter_across_slices_enabled_flag != 0 &&
                    (slice_sao_luma_flag != 0 || slice_sao_chroma_flag != 0 || slice_deblocking_filter_disabled_flag == 0)) {
                    /* slice_loop_filter_across_slices_enabled_flag = */ bits.ReadBit();
                }
            }

            if (pps.tiles_enabled_flag != 0 || pps.entropy_coding_sync_enabled_flag != 0) {
                num_entry_point_offsets = HevcParserUtils.ReadGolomb(bits);
                if (num_entry_point_offsets > 0 ) {
                    offset_len_minus1 = HevcParserUtils.ReadGolomb(bits);
                    if (offset_len_minus1 > 31) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                    for (uint i=0; i<num_entry_point_offsets; i++) {
                        bits.ReadBits(offset_len_minus1+1);
                    }
                }
            }

            if (pps.slice_segment_header_extension_present_flag != 0) {
                uint slice_segment_header_extension_length = HevcParserUtils.ReadGolomb(bits);
                for (uint i=0; i<slice_segment_header_extension_length; i++) {
                    bits.ReadBits(8); // slice_segment_header_extension_data_byte[i]
                }
            }

            // byte_alignment()
            bits.ReadBit(); // alignment_bit_equal_to_one
            uint bits_read = bits.GetBitsRead();
            if ((bits_read % 8) != 0) {
                bits.ReadBits(8-(bits_read%8));
            }

            /* compute the size */
            size = bits.GetBitsRead();

            return Ap4Result.SUCCESS;
        }

        public uint size; // size of the parsed data

        public uint first_slice_segment_in_pic_flag;
        public uint no_output_of_prior_pics_flag;
        public uint slice_pic_parameter_set_id;
        public uint dependent_slice_segment_flag;
        public uint slice_segment_address;
        public uint slice_type;
        public uint pic_output_flag;
        public uint colour_plane_id;
        public uint slice_pic_order_cnt_lsb;
        public uint short_term_ref_pic_set_sps_flag;
        public uint short_term_ref_pic_set_idx;
        public uint num_entry_point_offsets;
        public uint offset_len_minus1;
        public uint num_long_term_sps;
        public uint num_long_term_pics;

        public HevcShortTermRefPicSet short_term_ref_pic_set = new HevcShortTermRefPicSet();
        public uint[]                 used_by_curr_pic_lt_flag = new uint[Ap4.HEVC_MAX_LT_REFS];
    }

    /*----------------------------------------------------------------------
    |   HevcNalParser
    +---------------------------------------------------------------------*/
    public class HevcNalParser : NalParser {
        public static string NaluTypeName(uint nalu_type)
        {
            switch (nalu_type) {
                case  0: return "TRAIL_N - Coded slice segment of a non-TSA, non-STSA trailing picture";
                case  1: return "TRAIL_R - Coded slice segment of a non-TSA, non-STSA trailing picture";
                case  2: return "TSA_N - Coded slice segment of a TSA picture";
                case  3: return "TSA_R - Coded slice segment of a TSA picture";
                case  4: return "STSA_N - Coded slice segment of an STSA picture";
                case  5: return "STSA_R - Coded slice segment of an STSA picture";
                case  6: return "RADL_N - Coded slice segment of a RADL picture";
                case  7: return "RADL_R - Coded slice segment of a RADL picture";
                case  8: return "RASL_N - Coded slice segment of a RASL picture";
                case  9: return "RASL_R - Coded slice segment of a RASL picture";
                case 10: return "RSV_VCL_N10 - Reserved non-IRAP sub-layer non-reference";
                case 12: return "RSV_VCL_N12 - Reserved non-IRAP sub-layer non-reference";
                case 14: return "RSV_VCL_N14 - Reserved non-IRAP sub-layer non-reference";
                case 11: return "RSV_VCL_R11 - Reserved non-IRAP sub-layer reference";
                case 13: return "RSV_VCL_R13 - Reserved non-IRAP sub-layer reference";
                case 15: return "RSV_VCL_R15 - Reserved non-IRAP sub-layer reference";
                case 16: return "BLA_W_LP - Coded slice segment of a BLA picture";
                case 17: return "BLA_W_RADL - Coded slice segment of a BLA picture";
                case 18: return "BLA_N_LP - Coded slice segment of a BLA picture";
                case 19: return "IDR_W_RADL - Coded slice segment of an IDR picture";
                case 20: return "IDR_N_LP - Coded slice segment of an IDR picture";
                case 21: return "CRA_NUT - Coded slice segment of a CRA picture";
                case 22: return "RSV_IRAP_VCL22 - Reserved IRAP";
                case 23: return "RSV_IRAP_VCL23 - Reserved IRAP";
                case 24: return "RSV_VCL24 - Reserved non-IRAP";
                case 25: return "RSV_VCL25 - Reserved non-IRAP";
                case 26: return "RSV_VCL26 - Reserved non-IRAP";
                case 27: return "RSV_VCL27 - Reserved non-IRAP";
                case 28: return "RSV_VCL28 - Reserved non-IRAP";
                case 29: return "RSV_VCL29 - Reserved non-IRAP";
                case 30: return "RSV_VCL30 - Reserved non-IRAP";
                case 31: return "RSV_VCL31 - Reserved non-IRAP";
                case 32: return "VPS_NUT - Video parameter set";
                case 33: return "SPS_NUT - Sequence parameter set";
                case 34: return "PPS_NUT - Picture parameter set";
                case 35: return "AUD_NUT - Access unit delimiter";
                case 36: return "EOS_NUT - End of sequence";
                case 37: return "EOB_NUT - End of bitstream";
                case 38: return "FD_NUT - Filler data";
                case 39: return "PREFIX_SEI_NUT - Supplemental enhancement information";
                case 40: return "SUFFIX_SEI_NUT - Supplemental enhancement information";
                case 62: return "Dolby Vision RPU NAL units";
                case 63: return "Dolby Vision EL NAL units";
                default: return null;
            }
        }

        public static string PicTypeName(uint primary_pic_type)
        {
            switch (primary_pic_type) {
                case 0: return "I";
                case 1: return "I, P";
                case 2: return "I, P, B";
                default: return null;
            }
        }

        public static string SliceTypeName(uint slice_type)
        {
            switch (slice_type) {
                case Ap4.HEVC_SLICE_TYPE_B: return "B";
                case Ap4.HEVC_SLICE_TYPE_P: return "P";
                case Ap4.HEVC_SLICE_TYPE_I: return "I";
                default: return null;
            }
        }

        public HevcNalParser() :
            base()
        {
        }
    }

    /*----------------------------------------------------------------------
    |   HevcFrameParser
    +---------------------------------------------------------------------*/
    public class HevcFrameParser {
        // types
        public class AccessUnitInfo {
            public List<DataBuffer> nal_units = new List<DataBuffer>();
            public bool             is_random_access;
            public uint             decode_order;
            public uint             display_order;

            public void Reset()
            {
                nal_units.Clear();
                is_random_access = false;
                decode_order = 0;
                display_order = 0;
            }
        }

        // methods
        public HevcFrameParser()
        {
            _currentSlice = null;
            _currentNalUnitType = 0;
            _currentTemporalId = 0;
            _totalNalUnitCount = 0;
            _totalAccessUnitCount = 0;
            _accessUnitFlags = 0;
            _vclNalUnitsInAccessUnit = 0;
            _prevTid0Pic_PicOrderCntMsb = 0;
            _prevTid0Pic_PicOrderCntLsb = 0;
            _keepParameterSets = true;

            for (uint i=0; i<=Ap4.HEVC_PPS_MAX_ID; i++) {
                _PPS[i] = null;
            }
            for (uint i=0; i<=Ap4.HEVC_SPS_MAX_ID; i++) {
                _SPS[i] = null;
            }
            for (uint i=0; i<=Ap4.HEVC_VPS_MAX_ID; i++) {
                _VPS[i] = null;
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

            if (nal_unit != null && nal_unit_size >= 2) {
                uint nal_unit_type    = (uint)((nal_unit[nal_unit_offset] >> 1) & 0x3F);
                uint nuh_layer_id     = (uint)(((nal_unit[nal_unit_offset] & 1) << 5) | (nal_unit[nal_unit_offset+1] >> 3));
                uint nuh_temporal_id  = (uint)(nal_unit[nal_unit_offset+1] & 0x7);

                if (nuh_temporal_id-- == 0) {
                    // illegal value, ignore this NAL unit
                    return Ap4Result.SUCCESS;
                }

                _currentNalUnitType = nal_unit_type;
                _currentTemporalId  = nuh_temporal_id;
                string nal_unit_type_name = HevcNalParser.NaluTypeName(nal_unit_type);
                if (nal_unit_type_name == null) nal_unit_type_name = "UNKNOWN";

                // parse the NAL unit details and react accordingly
                if (nal_unit_type < Ap4.HEVC_NALU_TYPE_VPS_NUT) {
                    // this is a VCL NAL Unit
                    HevcSliceSegmentHeader slice_header = new HevcSliceSegmentHeader();
                    result = slice_header.Parse(nal_unit, nal_unit_offset+2, nal_unit_size-2, nal_unit_type, _PPS, _SPS);
                    if (result.Failed()) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }

                    if (slice_header.first_slice_segment_in_pic_flag != 0) {
                        CheckIfAccessUnitIsCompleted(access_unit_info);
                    }

                    // compute access unit flags
                    _accessUnitFlags = 0;
                    if (nal_unit_type >= Ap4.HEVC_NALU_TYPE_BLA_W_LP && nal_unit_type <= Ap4.HEVC_NALU_TYPE_RSV_IRAP_VCL23) {
                        _accessUnitFlags |= Ap4.HEVC_ACCESS_UNIT_FLAG_IS_IRAP;
                    }
                    if (nal_unit_type == Ap4.HEVC_NALU_TYPE_IDR_W_RADL || nal_unit_type == Ap4.HEVC_NALU_TYPE_IDR_N_LP) {
                        _accessUnitFlags |= Ap4.HEVC_ACCESS_UNIT_FLAG_IS_IDR;
                    }
                    if (nal_unit_type >= Ap4.HEVC_NALU_TYPE_BLA_W_LP && nal_unit_type <= Ap4.HEVC_NALU_TYPE_BLA_N_LP) {
                        _accessUnitFlags |= Ap4.HEVC_ACCESS_UNIT_FLAG_IS_BLA;
                    }
                    if (nal_unit_type == Ap4.HEVC_NALU_TYPE_RADL_N || nal_unit_type == Ap4.HEVC_NALU_TYPE_RADL_R) {
                        _accessUnitFlags |= Ap4.HEVC_ACCESS_UNIT_FLAG_IS_RADL;
                    }
                    if (nal_unit_type == Ap4.HEVC_NALU_TYPE_RASL_N || nal_unit_type == Ap4.HEVC_NALU_TYPE_RASL_R) {
                        _accessUnitFlags |= Ap4.HEVC_ACCESS_UNIT_FLAG_IS_RASL;
                    }
                    if (nal_unit_type <= Ap4.HEVC_NALU_TYPE_RSV_VCL_R15 && ((nal_unit_type & 1) == 0)) {
                        _accessUnitFlags |= Ap4.HEVC_ACCESS_UNIT_FLAG_IS_SUBLAYER_NON_REF;
                    }

                    // make this the current slice if this is the first slice in the access unit
                    if (_currentSlice == null) {
                        _currentSlice = slice_header;
                    }

                    // buffer this NAL unit
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                    ++_vclNalUnitsInAccessUnit;
                } else if (nal_unit_type == Ap4.HEVC_NALU_TYPE_AUD_NUT) {
                    uint    pic_type = (uint)(nal_unit[nal_unit_offset+1]>>5);
                    string  pic_type_name = HevcNalParser.PicTypeName(pic_type);
                    if (pic_type_name == null) pic_type_name = "UNKNOWN";

                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else if (nal_unit_type == Ap4.HEVC_NALU_TYPE_PPS_NUT) {
                    HevcPictureParameterSet pps = new HevcPictureParameterSet();
                    result = pps.Parse(nal_unit, nal_unit_offset, nal_unit_size);
                    if (result.Failed()) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                    _PPS[pps.pps_pic_parameter_set_id] = pps;

                    // keep the PPS with the NAL unit (this is optional)
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else if (nal_unit_type == Ap4.HEVC_NALU_TYPE_SPS_NUT) {
                    HevcSequenceParameterSet sps = new HevcSequenceParameterSet();
                    result = sps.Parse(nal_unit, nal_unit_offset, nal_unit_size);
                    if (result.Failed()) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                    _SPS[sps.sps_seq_parameter_set_id] = sps;

                    // keep the SPS with the NAL unit (this is optional)
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else if (nal_unit_type == Ap4.HEVC_NALU_TYPE_VPS_NUT) {
                    HevcVideoParameterSet vps = new HevcVideoParameterSet();
                    result = vps.Parse(nal_unit, nal_unit_offset, nal_unit_size);
                    if (result.Failed()) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                    _VPS[vps.vps_video_parameter_set_id] = vps;

                    // keep the VPS with the NAL unit (this is optional)
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else if (nal_unit_type == Ap4.HEVC_NALU_TYPE_EOS_NUT ||
                           nal_unit_type == Ap4.HEVC_NALU_TYPE_EOB_NUT) {
                    CheckIfAccessUnitIsCompleted(access_unit_info);
                } else if (nal_unit_type == Ap4.HEVC_NALU_TYPE_PREFIX_SEI_NUT) {
                    CheckIfAccessUnitIsCompleted(access_unit_info);
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                } else if (nal_unit_type == Ap4.HEVC_NALU_TYPE_SUFFIX_SEI_NUT){
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                } else if (nal_unit_type == Ap4.HEVC_NALU_TYPE_UNSPEC62) {
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                } else if (nal_unit_type == Ap4.HEVC_NALU_TYPE_UNSPEC63) {
                    AppendNalUnitData(nal_unit, nal_unit_offset, nal_unit_size);
                }
                _totalNalUnitCount++;
            }

            // flush if needed
            if (last_unit && access_unit_info.nal_units.Count == 0) {
                CheckIfAccessUnitIsCompleted(access_unit_info);
            }

            return Ap4Result.SUCCESS;
        }

        public Ap4Result ParseSliceSegmentHeader(byte[]                 data,
                                                 uint                   data_size,
                                                 uint                   nal_unit_type,
                                                 HevcSliceSegmentHeader slice_header)
        {
            return slice_header.Parse(data, data_size, nal_unit_type, _PPS, _SPS);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public Ap4Result ParseSliceSegmentHeader(byte[]                 data,
                                                 int                    data_offset,
                                                 uint                   data_size,
                                                 uint                   nal_unit_type,
                                                 HevcSliceSegmentHeader slice_header)
        {
            return slice_header.Parse(data, data_offset, data_size, nal_unit_type, _PPS, _SPS);
        }

        // PORT NOTE: upstream returns pointers to the first element of the arrays
        public HevcVideoParameterSet[]    GetVideoParameterSets()    { return _VPS; }
        public HevcSequenceParameterSet[] GetSequenceParameterSets() { return _SPS; }
        public HevcPictureParameterSet[]  GetPictureParameterSets()  { return _PPS; }

        public void SetParameterControl(bool isKeep) { _keepParameterSets = isKeep; }

        // methods
        private void CheckIfAccessUnitIsCompleted(AccessUnitInfo access_unit_info)
        {
            if (_vclNalUnitsInAccessUnit == 0 || _currentSlice == null) {
                return;
            }

            // PORT NOTE: upstream indexes m_SPS (16 entries) with the slice's *PPS* id (0..63), not
            // with the SPS id of that PPS. This is kept as is; for a PPS id above 15 the C++ code
            // reads past the end of m_SPS (undefined behaviour), here that case is treated like a
            // missing SPS.
            if (_currentSlice.slice_pic_parameter_set_id > Ap4.HEVC_SPS_MAX_ID) return;
            HevcSequenceParameterSet sps = _SPS[_currentSlice.slice_pic_parameter_set_id];
            if (sps == null) return;

            uint MaxPicOrderCntLsb = unchecked((uint)(1 << (int)(sps.log2_max_pic_order_cnt_lsb_minus4+4)));
            bool NoRaslOutputFlag = false;
            if ((_accessUnitFlags & Ap4.HEVC_ACCESS_UNIT_FLAG_IS_IRAP) != 0) {
                if ((_accessUnitFlags & Ap4.HEVC_ACCESS_UNIT_FLAG_IS_IDR) != 0 ||
                    (_accessUnitFlags & Ap4.HEVC_ACCESS_UNIT_FLAG_IS_BLA) != 0
                    /* TODO: check for end-of-sequence */) {
                    NoRaslOutputFlag = true;
                }
            }
            uint PrevPicOrderCntLsb = 0;
            uint PrevPicOrderCntMsb = 0;
            if (!((_accessUnitFlags & Ap4.HEVC_ACCESS_UNIT_FLAG_IS_IRAP) != 0 && NoRaslOutputFlag)) {
                PrevPicOrderCntLsb = _prevTid0Pic_PicOrderCntLsb;
                PrevPicOrderCntMsb = _prevTid0Pic_PicOrderCntMsb;
            }
            uint PicOrderCntMsb = 0;
            if (_currentSlice.slice_pic_order_cnt_lsb < PrevPicOrderCntLsb &&
                (PrevPicOrderCntLsb - _currentSlice.slice_pic_order_cnt_lsb) >= (MaxPicOrderCntLsb / 2)) {
                PicOrderCntMsb = PrevPicOrderCntMsb + MaxPicOrderCntLsb;
            } else if (_currentSlice.slice_pic_order_cnt_lsb > PrevPicOrderCntLsb &&
                       (_currentSlice.slice_pic_order_cnt_lsb - PrevPicOrderCntLsb) > (MaxPicOrderCntLsb / 2)) {
                PicOrderCntMsb = PrevPicOrderCntMsb - MaxPicOrderCntLsb;
            } else {
                PicOrderCntMsb = PrevPicOrderCntMsb;
            }

            if (_currentNalUnitType == Ap4.HEVC_NALU_TYPE_BLA_N_LP ||
                _currentNalUnitType == Ap4.HEVC_NALU_TYPE_BLA_W_LP ||
                _currentNalUnitType == Ap4.HEVC_NALU_TYPE_BLA_W_RADL) {
                PicOrderCntMsb = 0;
            }
            uint PicOrderCntVal = PicOrderCntMsb + _currentSlice.slice_pic_order_cnt_lsb;

            if (_currentTemporalId == 0 && (
                (_accessUnitFlags & Ap4.HEVC_ACCESS_UNIT_FLAG_IS_RADL) == 0 ||
                (_accessUnitFlags & Ap4.HEVC_ACCESS_UNIT_FLAG_IS_RASL) == 0 ||
                (_accessUnitFlags & Ap4.HEVC_ACCESS_UNIT_FLAG_IS_SUBLAYER_NON_REF) == 0)) {
                _prevTid0Pic_PicOrderCntLsb = _currentSlice.slice_pic_order_cnt_lsb;
                _prevTid0Pic_PicOrderCntMsb = PicOrderCntMsb;
            }

            // emit the access unit (transfer ownership)
            // PORT NOTE: AP4_Array::operator= replaces the destination's items with a copy of the source's
            access_unit_info.nal_units.Clear();
            access_unit_info.nal_units.AddRange(_accessUnitData);
            access_unit_info.decode_order     = _totalAccessUnitCount;
            access_unit_info.is_random_access = (_accessUnitFlags & Ap4.HEVC_ACCESS_UNIT_FLAG_IS_IRAP) != 0 ? true : false;
            access_unit_info.display_order    = PicOrderCntVal;
            _accessUnitData.Clear();
            _vclNalUnitsInAccessUnit  = 0;
            _accessUnitFlags          = 0;
            _currentSlice = null;
            ++_totalAccessUnitCount;
        }

        private void AppendNalUnitData(byte[] data, int data_offset, uint data_size)
        {
            _accessUnitData.Add(new DataBuffer(data, data_offset, data_size));
        }

        // members
        private HevcNalParser               _nalParser = new HevcNalParser();
        private HevcSliceSegmentHeader      _currentSlice;
        private uint                        _currentNalUnitType;
        private uint                        _currentTemporalId;
        private HevcPictureParameterSet[]  _PPS = new HevcPictureParameterSet[Ap4.HEVC_PPS_MAX_ID+1];
        private HevcSequenceParameterSet[] _SPS = new HevcSequenceParameterSet[Ap4.HEVC_SPS_MAX_ID+1];
        private HevcVideoParameterSet[]    _VPS = new HevcVideoParameterSet[Ap4.HEVC_VPS_MAX_ID+1];

        // accumulator for NAL unit data
        private uint             _totalNalUnitCount;
        private uint             _totalAccessUnitCount;
        private List<DataBuffer> _accessUnitData = new List<DataBuffer>();
        private uint             _accessUnitFlags;
        private uint             _vclNalUnitsInAccessUnit;

        // picture order counting
        private uint             _prevTid0Pic_PicOrderCntMsb;
        private uint             _prevTid0Pic_PicOrderCntLsb;

        // control if the parameter sets(VPS, SPS, PPS) need to be stored in stream('mdat')
        private bool             _keepParameterSets;
    }
}
