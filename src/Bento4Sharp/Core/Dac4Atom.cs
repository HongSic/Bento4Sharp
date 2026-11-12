// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Dac4Atom.h, Ap4Dac4Atom.cpp
// Original work Copyright 2002-2018 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Dac4Atom
    +---------------------------------------------------------------------*/
    public class Dac4Atom : Atom
    {
        // inner classes
        //
        // PORT NOTE (applies to the whole Ac4Dsi tree):
        // - the upstream structs are C# classes (they hold arrays and are
        //   accessed through references/pointers upstream). Where upstream copies
        //   one by value (`a = b`), use Clone(), which reproduces the C++ copy:
        //   scalars and inline arrays are copied, pointer members (substreams,
        //   substream_groups, substream_group_indexs, presentations) are shared.
        // - `new T[n]` followed by AP4_SetMemory(.., 0, ..) becomes an array
        //   filled with fresh (all-zero) objects.
        // - the anonymous unions `d` { v0; v1; } become an object holding two
        //   separate objects, so `x.d.v1.field` reads as upstream, but v0 and v1
        //   do NOT share storage. Upstream only reads the member it wrote, except
        //   in GetCodecString (see the note there).
        // - methods declared as returning AP4_Result but actually returning a
        //   value (a ch_mode, a channel mask, -1...) return int.
        public class Ac4Dsi
        {
            public struct Ac4BitrateDsi
            {
                public byte bit_rate_mode;
                public uint bit_rate;
                public uint bit_rate_precision;

                // methods
                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::Ac4BitrateDsi::WriteBitrateDsi
                +---------------------------------------------------------------------*/
                public Ap4Result WriteBitrateDsi(BitWriter bits)
                {
                    bits.Write(bit_rate_mode, 2);
                    bits.Write(bit_rate, 32);
                    bits.Write(bit_rate_precision, 32);
                    return Ap4Result.SUCCESS;
                }
            }

            public class Ac4AlternativeInfo
            {
                public ushort name_len;
                public byte[] presentation_name = new byte[256]; // restrict to 256 char
                public byte   n_targets;
                public byte[] target_md_compat = new byte[32];
                public byte[] target_device_category = new byte[32];

                // PORT NOTE: C++ struct copy
                public Ac4AlternativeInfo Clone()
                {
                    Ac4AlternativeInfo copy = (Ac4AlternativeInfo)MemberwiseClone();
                    copy.presentation_name      = (byte[])presentation_name.Clone();
                    copy.target_md_compat       = (byte[])target_md_compat.Clone();
                    copy.target_device_category = (byte[])target_device_category.Clone();
                    return copy;
                }

                // methods
                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::Ac4AlternativeInfo::WriteAlternativeInfo
                +---------------------------------------------------------------------*/
                public Ap4Result WriteAlternativeInfo(BitWriter bits)
                {
                    bits.Write(name_len, 16);
                    for (uint l = 0; l < name_len; l ++) {
                        // PORT NOTE: upstream reads past presentation_name[256] when name_len > 256
                        bits.Write(l < presentation_name.Length ? presentation_name[l] : (uint)0, 8);
                    }
                    bits.Write(n_targets, 5);
                    for (uint t = 0; t < n_targets; t ++) {
                        bits.Write(target_md_compat[t], 3);
                        bits.Write(target_device_category[t], 8);
                    }
                    return Ap4Result.SUCCESS;
                }
            }

            public class SubStream
            {
                public byte b_4_back_channels_present;
                public byte b_centre_present;
                public byte top_channels_present;
                public byte b_lfe;
                public byte dsi_sf_multiplier;
                public byte b_substream_bitrate_indicator;
                public byte substream_bitrate_indicator;
                public byte ch_mode; // auxiliary information, used to calcuate pres_ch_mode
                public uint dsi_substream_channel_mask;
                public byte b_ajoc;
                public byte b_static_dmx;
                public byte n_dmx_objects_minus1;
                public byte n_umx_objects_minus1;
                public byte b_substream_contains_bed_objects;
                public byte b_substream_contains_dynamic_objects;
                public byte b_substream_contains_ISF_objects;
                public byte ss_idx;

                // PORT NOTE: C++ struct copy
                public SubStream Clone()
                {
                    return (SubStream)MemberwiseClone();
                }

                // methods
                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::ParseSubstreamInfoChan
                +---------------------------------------------------------------------*/
                public Ap4Result ParseSubstreamInfoChan(BitReader bits,
                                                        uint      presentation_version,
                                                        byte      defalut_presentation_flag,
                                                        uint      fs_idx,
                                                        ref uint  speaker_index_mask,
                                                        uint      frame_rate_factor,
                                                        uint      b_substreams_present,
                                                        ref byte  dolby_atmos_indicator)
                {
                    ch_mode = (byte)ParseChMode(bits, (int)presentation_version, ref dolby_atmos_indicator);
                    int substreamSpeakerGroupIndexMask = Ac4Utils.AC4_SPEAKER_GROUP_INDEX_MASK_BY_CH_MODE[ch_mode];
                    if ((ch_mode >= Ac4Utils.CH_MODE_7_0_4) && (ch_mode <= Ac4Utils.CH_MODE_9_1_4)) {
                        if ((b_4_back_channels_present = (byte)bits.ReadBit()) == 0) {  // b_4_back_channels_present false
                            substreamSpeakerGroupIndexMask &= ~0x8;                     // Remove back channels (Lb,Rb) from mask
                        }
                        if ((b_centre_present = (byte)bits.ReadBit()) == 0) {           // b_centre_present false
                            substreamSpeakerGroupIndexMask &= ~0x2;                     // Remove centre channel (C) from mask
                        }
                        switch (top_channels_present = (byte)bits.ReadBits(2)) {        // top_channels_present
                            case 0:
                                substreamSpeakerGroupIndexMask &= ~0x30;        // Remove top channels (Tfl,Tfr,Tbl,Tbr) from mask
                                break;
                            case 1:
                            case 2:
                                substreamSpeakerGroupIndexMask &= ~0x30;        // Remove top channels (Tfl,Tfr,Tbl,Tbr) from mask
                                substreamSpeakerGroupIndexMask |=  0x80;        // Add top channels (Tl, Tr) from mask;
                                break;
                        }
                    }
                    dsi_substream_channel_mask = (uint)substreamSpeakerGroupIndexMask;
                    // Only combine channel masks of substream groups that are part of the first/default presentation
                    if (defalut_presentation_flag != 0) {
                        speaker_index_mask |= (uint)substreamSpeakerGroupIndexMask;
                    }

                    ParseDsiSfMutiplier(bits, fs_idx);

                    b_substream_bitrate_indicator = (byte)bits.ReadBit();
                    if (b_substream_bitrate_indicator != 0) {    // b_bitrate_info
                        // bitrate_indicator()
                        ParseBitrateIndicator(bits);
                    }

                    if (ch_mode >= Ac4Utils.CH_MODE_70_52 && ch_mode <= Ac4Utils.CH_MODE_71_322) {
                        bits.ReadBit();                     // add_ch_base
                    }
                    for (uint i = 0; i < frame_rate_factor; i++) {
                        bits.ReadBit();                     // b_audio_ndot
                    }

                    ParseSubstreamIdxInfo(bits, b_substreams_present);

                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::ParseSubStreamInfoAjoc
                +---------------------------------------------------------------------*/
                public Ap4Result ParseSubStreamInfoAjoc(BitReader bits,
                                                        ref uint  channel_count,
                                                        byte      defalut_presentation_flag,
                                                        uint      fs_idx,
                                                        uint      frame_rate_factor,
                                                        uint      b_substreams_present)
                {
                    b_lfe = (byte)bits.ReadBit();     // b_lfe
                    b_static_dmx = (byte)bits.ReadBit();
                    if (b_static_dmx != 0) {          // b_static_dmx
                        if (defalut_presentation_flag != 0) {
                            channel_count += 5;
                        }
                    } else {
                        n_dmx_objects_minus1 = (byte)bits.ReadBits(4);
                        uint nFullbandDmxSignals = (uint)(n_dmx_objects_minus1 + 1);
                        BedDynObjAssignment(bits, nFullbandDmxSignals, false);
                        if (defalut_presentation_flag != 0) {
                            channel_count += nFullbandDmxSignals;
                        }
                    }
                    if (bits.ReadBit() != 0) {       // b_oamd_common_data_present
                        // oamd_common_data()
                        ParseOamdCommonData(bits);
                    }
                    int nFullbandUpmixSignalsMinus = (int)bits.ReadBits(4);
                    int nFullbandUpmixSignals = nFullbandUpmixSignalsMinus + 1;
                    if (nFullbandUpmixSignals == 16) {
                        nFullbandUpmixSignals = (int)((uint)nFullbandUpmixSignals + Ac4Utils.Ac4VariableBits(bits, 3));
                    }
                    n_umx_objects_minus1 = (byte)(nFullbandUpmixSignals - 1);

                    BedDynObjAssignment(bits, (uint)nFullbandUpmixSignals, true);
                    ParseDsiSfMutiplier(bits, fs_idx);

                    b_substream_bitrate_indicator = (byte)bits.ReadBit();
                    if (b_substream_bitrate_indicator != 0) {    // b_bitrate_info
                        ParseBitrateIndicator(bits);
                    }
                    for (uint i = 0; i< frame_rate_factor; i++) {
                        bits.ReadBit();                     // b_audio_ndot
                    }
                    ParseSubstreamIdxInfo(bits, b_substreams_present);
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::ParseSubstreamInfoObj
                +---------------------------------------------------------------------*/
                public Ap4Result ParseSubstreamInfoObj(BitReader bits,
                                                       ref uint  channel_count,
                                                       byte      defalut_presentation_flag,
                                                       uint      fs_idx,
                                                       uint      frame_rate_factor,
                                                       uint      b_substreams_present)
                {
                    int nObjectsCode = (int)bits.ReadBits(3);
                    if (defalut_presentation_flag != 0) {
                        switch (nObjectsCode) {
                            case 0:
                            case 1:
                            case 2:
                            case 3:
                                channel_count += (uint)nObjectsCode;
                                break;
                            case 4:
                                channel_count += 5;
                                break;
                            default:
                                break;
                                //TODO: Error
                        }
                    }
                    if (bits.ReadBit() != 0) {                  // b_dynamic_objects
                        b_substream_contains_dynamic_objects = 1;
                        // PORT NOTE: named b_lfe upstream (a local that shadows the member)
                        uint b_lfe_local = (uint)bits.ReadBit();    // b_lfe
                        if (defalut_presentation_flag != 0 && b_lfe_local != 0) { channel_count += 1; }
                    }else {
                        if (bits.ReadBit() != 0) {              // b_bed_objects
                            b_substream_contains_bed_objects = 1;
                            if (bits.ReadBit() != 0) {          // b_bed_start
                                if (bits.ReadBit() != 0) {      // b_ch_assign_code
                                    bits.ReadBits(3);           // bed_chan_assign_code
                                }
                                else {
                                    if (bits.ReadBit() != 0) {  // b_nonstd_bed_channel_assignment
                                        bits.ReadBits(17);      // nonstd_bed_channel_assignment_mask
                                    }
                                    else {
                                        bits.ReadBits(10);      // std_bed_channel_assignment_mask
                                    }
                                }
                            }
                        }else {
                            if (bits.ReadBit() != 0) {          // b_isf
                                b_substream_contains_ISF_objects = 1;
                                if (bits.ReadBit() != 0) {      // b_isf_start
                                    bits.ReadBits(3);           // isf_config
                                }
                            }else {
                                int resBytes = (int)bits.ReadBits(4);
                                bits.ReadBits((uint)(resBytes * 8));
                            }
                        }
                    }

                    ParseDsiSfMutiplier(bits, fs_idx);

                    b_substream_bitrate_indicator = (byte)bits.ReadBit();
                    if (b_substream_bitrate_indicator != 0) {   // b_bitrate_info
                        ParseBitrateIndicator(bits);
                    }
                    for (uint i = 0; i< frame_rate_factor; i++) {
                        bits.ReadBit();                         // b_audio_ndot
                    }
                    ParseSubstreamIdxInfo(bits, b_substreams_present);
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::WriteSubstreamDsi
                +---------------------------------------------------------------------*/
                public Ap4Result WriteSubstreamDsi(BitWriter bits, byte b_channel_coded)
                {
                    bits.Write(dsi_sf_multiplier, 2);
                    bits.Write(b_substream_bitrate_indicator, 1);
                    if (b_substream_bitrate_indicator == 1) {
                        bits.Write(substream_bitrate_indicator, 5);
                    }
                    if (b_channel_coded == 1) {
                        bits.Write(dsi_substream_channel_mask, 24);
                    }else {
                        bits.Write(b_ajoc, 1);
                        if (b_ajoc == 1) {
                            bits.Write(b_static_dmx, 1);
                            if (b_static_dmx == 0) {
                                bits.Write(n_dmx_objects_minus1, 4);
                            }
                            bits.Write(n_umx_objects_minus1, 6);
                        }
                        // substream composition information
                        bits.Write(b_substream_contains_bed_objects, 1);
                        bits.Write(b_substream_contains_dynamic_objects, 1);
                        bits.Write(b_substream_contains_ISF_objects, 1);
                        bits.Write(0, 1); //reserved bit
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::ParseChMode
                +---------------------------------------------------------------------*/
                private int ParseChMode(BitReader bits, int presentationVersion, ref byte dolby_atmos_indicator)
                {
                    int channel_mode_code = 0;

                    channel_mode_code = bits.ReadBit();
                    if (channel_mode_code == 0) {   // Mono 0b0
                        return Ac4Utils.CH_MODE_MONO;
                    }
                    channel_mode_code = (channel_mode_code << 1) | bits.ReadBit();
                    if (channel_mode_code == 2) {   // Stereo  0b10
                        return Ac4Utils.CH_MODE_STEREO;
                    }
                    channel_mode_code = (int)((uint)(channel_mode_code << 2) | bits.ReadBits(2));
                    switch (channel_mode_code) {
                        case 12:                    // 3.0 0b1100
                            return Ac4Utils.CH_MODE_3_0;
                        case 13:                    // 5.0 0b1101
                            return Ac4Utils.CH_MODE_5_0;
                        case 14:                    // 5.1 0b1110
                            return Ac4Utils.CH_MODE_5_1;
                    }
                    channel_mode_code = (int)((uint)(channel_mode_code << 3) | bits.ReadBits(3));
                    switch (channel_mode_code) {
                        case 120:                   // 0b1111000
                            if (presentationVersion == 2) { // IMS (all content)
                                return Ac4Utils.CH_MODE_STEREO;
                            }
                            else {                  // 7.0: 3/4/0
                                return Ac4Utils.CH_MODE_70_34;
                            }
                        case 121:                   // 0b1111001
                            if (presentationVersion == 2) { // IMS (Atmos content)
                                dolby_atmos_indicator |= 1;
                                return Ac4Utils.CH_MODE_STEREO;
                            }
                            else {                  // 7.1: 3/4/0.1
                                return Ac4Utils.CH_MODE_71_34;
                            }
                        case 122:                   // 7.0: 5/2/0   0b1111010
                            return Ac4Utils.CH_MODE_70_52;
                        case 123:                   // 7.1: 5/2/0.1 0b1111011
                            return Ac4Utils.CH_MODE_71_52;
                        case 124:                   // 7.0: 3/2/2   0b1111100
                            return Ac4Utils.CH_MODE_70_322;
                        case 125:                   // 7.1: 3/2/2.1 0b1111101
                            return Ac4Utils.CH_MODE_71_322;
                    }
                    channel_mode_code = (channel_mode_code << 1) | bits.ReadBit();
                    switch (channel_mode_code) {
                        case 252:                   // 7.0.4 0b11111100
                            return Ac4Utils.CH_MODE_7_0_4;
                        case 253:                   // 7.1.4 0b11111101
                            return Ac4Utils.CH_MODE_7_1_4;
                    }
                    channel_mode_code = (channel_mode_code << 1) | bits.ReadBit();
                    switch (channel_mode_code) {
                        case 508:                   // 9.0.4 0b111111100
                            return Ac4Utils.CH_MODE_9_0_4;
                        case 509:                   // 9.1.4 0b111111101
                            return Ac4Utils.CH_MODE_9_1_4;
                        case 510:                   // 22.2 0b111111110
                            return Ac4Utils.CH_MODE_22_2;
                        case 511:                   // Reserved, escape value 0b111111111
                        default:
                            Ac4Utils.Ac4VariableBits(bits, 2);
                            return Ac4Utils.CH_MODE_RESERVED;
                    }
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::ParseDsiSfMutiplier
                +---------------------------------------------------------------------*/
                private Ap4Result ParseDsiSfMutiplier(BitReader bits,
                                                      uint      fs_idx)
                {
                    if (fs_idx == 1) {
                        if (bits.ReadBit() != 0) {                          // b_sf_miultiplier
                            // 96 kHz or 192 kHz
                            dsi_sf_multiplier = (byte)(bits.ReadBit() + 1); // sf_multiplier
                        }else{
                            // 48 kHz
                            dsi_sf_multiplier = 0;
                        }
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::BedDynObjAssignment
                +---------------------------------------------------------------------*/
                private Ap4Result BedDynObjAssignment(BitReader bits,
                                                      uint      nSignals,
                                                      bool      is_upmix)
                {
                    if (bits.ReadBit() == 0) {      // b_dyn_objects_only
                        if (bits.ReadBit() != 0) {  // b_isf
                            byte isf_config = (byte)bits.ReadBits(3);
                            if (is_upmix) {
                                b_substream_contains_ISF_objects |= 1;
                                if (nSignals > ObjNumFromIsfConfig(isf_config)) {
                                    b_substream_contains_dynamic_objects |= 1;
                                }
                            }
                        }else {
                            if (bits.ReadBit() != 0) {          // b_ch_assign_code
                                byte bed_chan_assign_code = (byte)bits.ReadBits(3);
                                if (is_upmix) {
                                    b_substream_contains_bed_objects |= 1;
                                    if (nSignals > BedNumFromAssignCode(bed_chan_assign_code)) {
                                        b_substream_contains_dynamic_objects |= 1;
                                    }
                                }
                            }else {
                                if (bits.ReadBit() != 0) {      // b_chan_assign_mask
                                    if (bits.ReadBit() != 0) {  // b_nonstd_bed_channel_assignment
                                        uint nonstd_bed_channel_assignment_mask = bits.ReadBits(17);
                                        if (is_upmix) {
                                            uint bed_num = BedNumFromNonStdMask(nonstd_bed_channel_assignment_mask);
                                            if (bed_num > 0) { b_substream_contains_bed_objects |= 1; }
                                            if (nSignals > bed_num) {
                                                b_substream_contains_dynamic_objects |= 1;
                                            }
                                        }
                                    }
                                    else {
                                        uint std_bed_channel_assignment_mask = bits.ReadBits(10);
                                        if (is_upmix) {
                                            uint bed_num = BedNumFromStdMask(std_bed_channel_assignment_mask);
                                            if (bed_num > 0) { b_substream_contains_bed_objects |= 1; }
                                            if (nSignals > bed_num) {
                                                b_substream_contains_dynamic_objects |= 1;
                                            }
                                        }
                                    }
                                }else {
                                    uint nBedSignals = 0;
                                    if (nSignals > 1) {
                                        // PORT NOTE: upstream is `(int) ceil(log((float)nSignals)/log((float)2))`,
                                        // evaluated in float or double precision depending on which
                                        // log() overload the C++ library exposes; double is used here
                                        // (both give the same result for the small values involved).
                                        int bedChBits = (int) Math.Ceiling(Math.Log((float)nSignals)/Math.Log((float)2));
                                        nBedSignals = bits.ReadBits((uint)bedChBits) + 1;
                                    }
                                    else {
                                        nBedSignals = 1;
                                    }
                                    for (uint b = 0; b < nBedSignals; b++) {
                                        bits.ReadBits(4);   // nonstd_bed_channel_assignment
                                    }
                                    if (is_upmix) {
                                        b_substream_contains_bed_objects |= 1;
                                        if (nSignals > nBedSignals){ b_substream_contains_dynamic_objects |= 1; }
                                    }
                                }
                            }
                        }
                    }else {
                        if (is_upmix) {
                            b_substream_contains_dynamic_objects |= 1;
                            b_substream_contains_bed_objects     |= 0;
                            b_substream_contains_ISF_objects     |= 0;
                        }
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::ParseSubstreamIdxInfo
                +---------------------------------------------------------------------*/
                private Ap4Result ParseSubstreamIdxInfo(BitReader bits, uint b_substreams_present)
                {
                    if (b_substreams_present == 1) {
                        if (bits.ReadBits(2) == 3) {    // substream_index
                            ss_idx = (byte)Ac4Utils.Ac4VariableBits(bits, 2);
                        }
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::ParseBitrateIndicator
                +---------------------------------------------------------------------*/
                private Ap4Result ParseBitrateIndicator(BitReader bits)
                {
                    substream_bitrate_indicator = (byte)bits.ReadBits(3);
                    if ((substream_bitrate_indicator & 0x1) == 1) { // bitrate_indicator
                        substream_bitrate_indicator = (byte)((uint)(substream_bitrate_indicator << 2) + bits.ReadBits(2));
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::ParseOamdCommonData
                +---------------------------------------------------------------------*/
                private Ap4Result ParseOamdCommonData(BitReader bits)
                {
                    if (bits.ReadBit() == 0) {  // b_default_screen_size_ratio
                        bits.ReadBits(5);       // master_screen_size_ratio_code
                    }
                    bits.ReadBit();             // b_bed_object_chan_distribute
                    if (bits.ReadBit() != 0) {  // b_additional_data
                        int addDataBytes = bits.ReadBit() + 1;
                        if (addDataBytes == 2) {
                            addDataBytes = (int)((uint)addDataBytes + Ac4Utils.Ac4VariableBits(bits, 2));
                        }
                        uint bits_used = (uint)(int)Trim(bits);
                        bits_used += (uint)(int)BedRendeInfo(bits);
                        bits.ReadBits((uint)(addDataBytes * 8) - bits_used);
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::Trim
                +---------------------------------------------------------------------*/
                private Ap4Result Trim(BitReader bits)
                {
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::BedRendeInfo
                +---------------------------------------------------------------------*/
                private Ap4Result BedRendeInfo(BitReader bits)
                {
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::ObjNumFromIsfConfig
                +---------------------------------------------------------------------*/
                private uint ObjNumFromIsfConfig(byte isf_config)
                {
                    uint obj_num = 0;
                    switch (isf_config){
                        case 0: obj_num = 4 ; break;
                        case 1: obj_num = 8 ; break;
                        case 2: obj_num = 10; break;
                        case 3: obj_num = 14; break;
                        case 4: obj_num = 15; break;
                        case 5: obj_num = 30; break;
                        default: obj_num = 0; break;
                    }
                    return obj_num;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::BedNumFromAssignCode
                +---------------------------------------------------------------------*/
                private uint BedNumFromAssignCode(byte assign_code)
                {
                    uint bed_num = 0;
                    switch (assign_code){
                        case 0: bed_num = 2 ; break;
                        case 1: bed_num = 3 ; break;
                        case 2: bed_num = 6 ; break;
                        case 3: bed_num = 8 ; break;
                        case 4: bed_num = 10; break;
                        case 5: bed_num = 8 ; break;
                        case 6: bed_num = 10; break;
                        case 7: bed_num = 12; break;
                        default: bed_num = 0; break;
                    }
                    return bed_num;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::BedNumFromNonStdMask
                +---------------------------------------------------------------------*/
                private uint BedNumFromNonStdMask(uint non_std_mask)
                {
                    uint bed_num = 0;
                    // Table 85: nonstd_bed_channel_assignment AC-4 part-2 v1.2.1
                    for (int idx = 0; idx < 17; idx ++) {
                        if (((non_std_mask >> idx) & 0x1) != 0){
                            bed_num ++;
                        }
                    }
                    return bed_num;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::BedNumFromStdMask
                +---------------------------------------------------------------------*/
                private uint BedNumFromStdMask(uint std_mask)
                {
                    uint bed_num = 0;
                    // Table 86 std_bed_channel_assignment_flag[] AC-4 part-2 v1.2.1
                    for (int idx = 0; idx < 10; idx ++) {
                        if (((std_mask >> idx) & 0x1) != 0){
                            if ((idx == 1) || (idx == 2) || (idx == 9)) { bed_num ++;}
                            else { bed_num += 2; }
                        }
                    }
                    return bed_num;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStream::GetChModeCore
                +---------------------------------------------------------------------*/
                public int GetChModeCore(byte b_channel_coded)
                {
                    int ch_mode_core = -1;
                    if (b_channel_coded == 0 &&
                        b_ajoc          == 1 &&
                        b_static_dmx    == 1 &&
                        b_lfe           == 0){
                        ch_mode_core = 3 ;
                    }else if (b_channel_coded == 0 &&
                              b_ajoc          == 1 &&
                              b_static_dmx    == 1 &&
                              b_lfe           == 1){
                              ch_mode_core = 4 ;
                    }else if ((b_channel_coded == 1) &&
                              (ch_mode == 11 || ch_mode == 13)){
                              ch_mode_core = 5 ;
                    }else if ((b_channel_coded == 1) &&
                              (ch_mode == 12 || ch_mode == 14)){
                              ch_mode_core = 6 ;
                    }
                    return ch_mode_core;
                }
            }

            public class SubStreamGroupV1
            {
                public class V0
                {
                    public byte   channel_mode;
                    public byte   dsi_sf_multiplier;
                    public byte   b_substream_bitrate_indicator;
                    public byte   substream_bitrate_indicator;
                    public byte   add_ch_base;
                    public byte   b_content_type;
                    public byte   content_classifier;
                    public byte   b_language_indicator;
                    public byte   n_language_tag_bytes;
                    public byte[] language_tag_bytes = new byte[64];

                    public V0 Clone()
                    {
                        V0 copy = (V0)MemberwiseClone();
                        copy.language_tag_bytes = (byte[])language_tag_bytes.Clone();
                        return copy;
                    }
                }

                public class V1
                {
                    public byte         b_substreams_present;
                    public byte         b_hsf_ext;
                    public byte         b_channel_coded;
                    public byte         n_substreams;
                    public SubStream[]  substreams;
                    public byte         b_content_type;
                    public byte         content_classifier;
                    public byte         b_language_indicator;
                    public byte         n_language_tag_bytes;
                    public byte[]       language_tag_bytes = new byte[64]; // n_language_tag_bytes is 6 bits
                    public byte         dolby_atmos_indicator;

                    public V1 Clone()
                    {
                        V1 copy = (V1)MemberwiseClone();   // `substreams` stays shared, like the upstream pointer
                        copy.language_tag_bytes = (byte[])language_tag_bytes.Clone();
                        return copy;
                    }
                }

                // PORT NOTE: union upstream; v0 and v1 do not share storage in the port
                public class D
                {
                    public V0 v0 = new V0();
                    public V1 v1 = new V1();
                }

                public D d = new D();

                // PORT NOTE: C++ struct copy
                public SubStreamGroupV1 Clone()
                {
                    SubStreamGroupV1 copy = new SubStreamGroupV1();
                    copy.d.v0 = d.v0.Clone();
                    copy.d.v1 = d.v1.Clone();
                    return copy;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStreamGroupV1::ParseSubstreamGroupInfo
                +---------------------------------------------------------------------*/
                public Ap4Result ParseSubstreamGroupInfo(BitReader bits,
                                                         uint      bitstream_version,
                                                         uint      presentation_version,
                                                         byte      defalut_presentation_flag,
                                                         uint      frame_rate_factor,
                                                         uint      fs_idx,
                                                         ref uint  channel_count,
                                                         ref uint  speaker_index_mask,
                                                         ref uint  b_obj_or_Ajoc)
                {
                    d.v1.b_substreams_present = (byte)bits.ReadBit();
                    d.v1.b_hsf_ext = (byte)bits.ReadBit();
                    if (bits.ReadBit() != 0) {
                        d.v1.n_substreams = 1;
                    } else{
                        d.v1.n_substreams = (byte)(bits.ReadBits(2) + 2);
                        if (d.v1.n_substreams == 5) {
                            d.v1.n_substreams = (byte)(d.v1.n_substreams + Ac4Utils.Ac4VariableBits(bits, 2));
                        }
                    }
                    d.v1.substreams = new Dac4Atom.Ac4Dsi.SubStream[d.v1.n_substreams];
                    for (int i = 0; i < d.v1.n_substreams; i++) {
                        d.v1.substreams[i] = new Dac4Atom.Ac4Dsi.SubStream();
                    }
                    d.v1.b_channel_coded = (byte)bits.ReadBit();
                    if (d.v1.b_channel_coded != 0) {
                        for (uint sus = 0; sus < d.v1.n_substreams; sus++) {
                            Dac4Atom.Ac4Dsi.SubStream substream = d.v1.substreams[sus];
                            if (bitstream_version == 1) {
                                bits.ReadBit(); // sus_ver
                            }

                            // ac4_substream_info_chan()
                            substream.ParseSubstreamInfoChan(bits,
                                                             presentation_version,
                                                             defalut_presentation_flag,
                                                             fs_idx,
                                                             ref speaker_index_mask,
                                                             frame_rate_factor,
                                                             d.v1.b_substreams_present,
                                                             ref d.v1.dolby_atmos_indicator);

                            if (d.v1.b_hsf_ext != 0) {
                                // ac4_hsf_ext_substream_info()
                                ParseHsfExtSubstreamInfo(bits);
                            }
                        }
                    } else {     // b_channel_coded == 0
                        b_obj_or_Ajoc = 1;
                        if (bits.ReadBit() != 0) {  // b_oamd_substream
                                //oamd_substream_info()
                            ParseOamdSubstreamInfo(bits);
                        }
                        for (int sus = 0; sus < d.v1.n_substreams; sus++) {
                            Dac4Atom.Ac4Dsi.SubStream substream = d.v1.substreams[sus];
                            substream.b_ajoc = (byte)bits.ReadBit();
                            uint local_channel_count = 0;
                            if (substream.b_ajoc != 0) { // b_ajoc
                                // ac4_substream_info_ajoc()
                                substream.ParseSubStreamInfoAjoc(bits,
                                                                 ref local_channel_count,
                                                                 defalut_presentation_flag,
                                                                 fs_idx,
                                                                 frame_rate_factor,
                                                                 d.v1.b_substreams_present);

                                if (d.v1.b_hsf_ext != 0) {
                                    // ac4_hsf_ext_substream_info()
                                    ParseHsfExtSubstreamInfo(bits);
                                }
                            }else {
                                // ac4_substream_info_obj()
                                substream.ParseSubstreamInfoObj(bits,
                                                                ref local_channel_count,
                                                                defalut_presentation_flag,
                                                                fs_idx,
                                                                frame_rate_factor,
                                                                d.v1.b_substreams_present);

                                if (d.v1.b_hsf_ext != 0) {
                                    // ac4_hsf_ext_substream_info()
                                    ParseHsfExtSubstreamInfo(bits);
                                }
                            }
                            if (channel_count < local_channel_count) { channel_count = local_channel_count;}
                        }
                    }

                    d.v1.b_content_type = (byte)bits.ReadBit();
                    if (d.v1.b_content_type != 0) {
                        // content_type()
                        ParseContentType(bits);
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStreamGroupV1::WriteSubstreamGroupDsi
                +---------------------------------------------------------------------*/
                public Ap4Result WriteSubstreamGroupDsi(BitWriter bits)
                {
                    bits.Write(d.v1.b_substreams_present, 1);
                    bits.Write(d.v1.b_hsf_ext, 1);
                    bits.Write(d.v1.b_channel_coded, 1);
                    bits.Write(d.v1.n_substreams, 8);
                    for (uint sg = 0; sg < d.v1.n_substreams; sg++ ){
                        d.v1.substreams[sg].WriteSubstreamDsi(bits, d.v1.b_channel_coded);
                    }
                    WriteContentType(bits);
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStreamGroupV1::ParseOamdSubstreamInfo
                +---------------------------------------------------------------------*/
                private Ap4Result ParseOamdSubstreamInfo(BitReader bits)
                {
                    bits.ReadBit();                     // b_oamd_ndot
                    if (d.v1.b_substreams_present == 1) {
                        if (bits.ReadBits(2) == 3) {    // substream_index
                            Ac4Utils.Ac4VariableBits(bits, 2);
                        }
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStreamGroupV1::ParseHsfExtSubstreamInfo
                +---------------------------------------------------------------------*/
                private Ap4Result ParseHsfExtSubstreamInfo(BitReader bits)
                {
                    if (d.v1.b_substreams_present == 1) {
                        if (bits.ReadBits(2) == 3) {    // substream_index
                            Ac4Utils.Ac4VariableBits(bits, 2);
                        }
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStreamGroupV1::ParseContentType
                +---------------------------------------------------------------------*/
                private Ap4Result ParseContentType(BitReader bits)
                {
                    d.v1.content_classifier = (byte)bits.ReadBits(3); // content_classifier
                    d.v1.b_language_indicator = (byte)bits.ReadBit();
                    if (d.v1.b_language_indicator == 1) {       // b_language_indicator
                        if (bits.ReadBit() != 0) {              // b_serialized_language_tag
                            bits.ReadBits(17);                  // b_start_tag, language_tag_chunk
                        }
                        else {
                            d.v1.n_language_tag_bytes= (byte)bits.ReadBits(6);
                            for (uint l = 0; l < d.v1.n_language_tag_bytes; l++){
                                d.v1.language_tag_bytes[l] = (byte)bits.ReadBits(8);
                            }
                        }
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::SubStreamGroupV1::WriteContentType
                +---------------------------------------------------------------------*/
                private Ap4Result WriteContentType(BitWriter bits)
                {
                    bits.Write(d.v1.b_content_type, 1);
                    if (d.v1.b_content_type == 1){
                        bits.Write(d.v1.content_classifier, 3);
                        bits.Write(d.v1.b_language_indicator, 1);
                        if (d.v1.b_language_indicator == 1){
                            bits.Write(d.v1.n_language_tag_bytes, 6);
                            for (uint idx = 0; idx < d.v1.n_language_tag_bytes; idx++ ){
                                bits.Write(d.v1.language_tag_bytes[idx], 8);
                            }
                        }
                    }
                    return Ap4Result.SUCCESS;
                }
            }

            public class PresentationV1
            {
                public byte presentation_version;

                public class V0
                {
                    public byte   presentation_config;
                    public byte   mdcompat;
                    public byte   presentation_id;
                    public byte   dsi_frame_rate_multiply_info;
                    public byte   presentation_emdf_version;
                    public ushort presentation_key_id;
                    public uint   presentation_channel_mask;
                    // more fields omitted, v0 is deprecated.

                    public V0 Clone()
                    {
                        return (V0)MemberwiseClone();
                    }
                }

                public class V1
                {
                    public byte                presentation_config_v1;
                    public byte                mdcompat;
                    public byte                b_presentation_id;
                    public byte                presentation_id;
                    public byte                dsi_frame_rate_multiply_info;
                    public byte                dsi_frame_rate_fraction_info;
                    public byte                presentation_emdf_version;
                    public ushort              presentation_key_id;
                    public byte                b_presentation_channel_coded;
                    public byte                dsi_presentation_ch_mode;
                    public byte                pres_b_4_back_channels_present;
                    public byte                pres_top_channel_pairs;
                    public uint                presentation_channel_mask_v1;
                    public byte                b_presentation_core_differs;
                    public byte                b_presentation_core_channel_coded;
                    public byte                dsi_presentation_channel_mode_core;
                    public byte                b_presentation_filter;
                    public byte                b_enable_presentation;
                    public byte                n_filter_bytes;
                    public byte                b_multi_pid;
                    public byte                n_substream_groups;
                    public SubStreamGroupV1[]  substream_groups;
                    public uint[]              substream_group_indexs; // auxiliary information, not exist in DSI
                    public byte                n_skip_bytes;
                    public byte                b_pre_virtualized;
                    public byte                b_add_emdf_substreams;
                    public byte                n_add_emdf_substreams;
                    public byte[]              substream_emdf_version = new byte[128]; // n_add_emdf_substreams - 7 bits
                    public ushort[]            substream_key_id = new ushort[128];
                    public byte                b_presentation_bitrate_info;
                    public Ac4BitrateDsi       ac4_bitrate_dsi;
                    public byte                b_alternative;
                    public Ac4AlternativeInfo  alternative_info = new Ac4AlternativeInfo();
                    public byte                de_indicator;
                    public byte                dolby_atmos_indicator;
                    public byte                b_extended_presentation_id;
                    public ushort              extended_presentation_id;

                    public V1 Clone()
                    {
                        // `substream_groups` and `substream_group_indexs` stay shared, like the upstream pointers
                        V1 copy = (V1)MemberwiseClone();
                        copy.substream_emdf_version = (byte[])substream_emdf_version.Clone();
                        copy.substream_key_id       = (ushort[])substream_key_id.Clone();
                        copy.alternative_info       = alternative_info.Clone();
                        return copy;
                    }
                }

                // PORT NOTE: union upstream; v0 and v1 do not share storage in the port
                public class D
                {
                    public V0 v0 = new V0();
                    public V1 v1 = new V1();
                }

                public D d = new D();

                // PORT NOTE: C++ struct copy
                public PresentationV1 Clone()
                {
                    PresentationV1 copy = new PresentationV1();
                    copy.presentation_version = presentation_version;
                    copy.d.v0 = d.v0.Clone();
                    copy.d.v1 = d.v1.Clone();
                    return copy;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::Parse
                +---------------------------------------------------------------------*/
                // PORT NOTE: `unsigned int **first_pres_sg_index` is `ref uint[]?`
                public Ap4Result ParsePresentationV1Info(BitReader   bits,
                                                         uint        bitstream_version,
                                                         uint        frame_rate_idx,
                                                         uint        pres_idx,
                                                         ref uint    max_group_index,
                                                         ref uint[]  first_pres_sg_index,
                                                         ref uint    first_pres_sg_num)
                {
                    uint[] substreamGroupIndexes = new uint[Ap4.AC4_SUBSTREAM_GROUP_NUM];
                    uint b_single_substream_group = (uint)bits.ReadBit();
                    if (b_single_substream_group != 1){
                        d.v1.presentation_config_v1 = (byte)bits.ReadBits(3);
                        if (d.v1.presentation_config_v1 == 7) {
                            d.v1.presentation_config_v1 = (byte)(d.v1.presentation_config_v1 + Ac4Utils.Ac4VariableBits(bits, 2));
                        }
                    }else {
                        d.v1.presentation_config_v1 = 0x1f;
                    }

                    // presentation_version()
                    ParsePresentationVersion(bits, bitstream_version);

                    Ac4EmdfInfo tmp_emdf_info = new Ac4EmdfInfo();
                    if (b_single_substream_group != 1 && d.v1.presentation_config_v1 == 6){
                        d.v1.b_add_emdf_substreams = 1;
                    }else{
                        if (bitstream_version != 1){
                            d.v1.mdcompat = (byte)bits.ReadBits(3);
                        }
                        d.v1.b_presentation_id = (byte)bits.ReadBit();
                        if (d.v1.b_presentation_id != 0) {
                            d.v1.presentation_id = (byte)Ac4Utils.Ac4VariableBits(bits, 2);
                        }
                        // frame_rate_multiply_info()
                        ParseDSIFrameRateMultiplyInfo(bits, frame_rate_idx);

                        // frame_rate_fractions_info()
                        ParseDSIFrameRateFractionsInfo(bits, frame_rate_idx);

                        // emdf_info()
                        ParseEmdInfo(bits, tmp_emdf_info);
                        d.v1.presentation_emdf_version = tmp_emdf_info.emdf_version;
                        d.v1.presentation_key_id = tmp_emdf_info.key_id;

                        // b_presentation_filter
                        d.v1.b_presentation_filter = (byte)bits.ReadBit();
                        if (d.v1.b_presentation_filter == 1){
                            d.v1.b_enable_presentation = (byte)bits.ReadBit();
                        }
                        if (b_single_substream_group == 1){
                            substreamGroupIndexes[0] = ParseAc4SgiSpecifier(bits, bitstream_version);
                            max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[0], max_group_index);
                            d.v1.n_substream_groups = 1;
                            d.v1.substream_group_indexs = substreamGroupIndexes;
                        }else {
                            d.v1.b_multi_pid = (byte)bits.ReadBit();
                            switch (d.v1.presentation_config_v1){
                                case 0: // M&E + D
                                case 2: // Main + Assoc
                                    substreamGroupIndexes[0] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    substreamGroupIndexes[1] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[0], max_group_index);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[1], max_group_index);
                                    d.v1.n_substream_groups = 2;
                                    d.v1.substream_group_indexs = substreamGroupIndexes;
                                    break;
                                case 1: // Main + DE
                                    // shall return same substream group index, need stream to verify
                                    substreamGroupIndexes[0] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    substreamGroupIndexes[1] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[0], max_group_index);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[1], max_group_index);
                                    d.v1.n_substream_groups = 2;
                                    d.v1.substream_group_indexs = substreamGroupIndexes;
                                    break;
                                case 3: // M&E + D + Assoc
                                    substreamGroupIndexes[0] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    substreamGroupIndexes[1] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    substreamGroupIndexes[2] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[0], max_group_index);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[1], max_group_index);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[2], max_group_index);
                                    d.v1.n_substream_groups = 3;
                                    d.v1.substream_group_indexs = substreamGroupIndexes;
                                    break;
                                case 4: // Main + DE + Assoc
                                    // shall return only two substream group index, need stream to verify
                                    substreamGroupIndexes[0] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    substreamGroupIndexes[1] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    substreamGroupIndexes[2] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[0], max_group_index);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[1], max_group_index);
                                    max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[2], max_group_index);
                                    d.v1.n_substream_groups = 3;
                                    d.v1.substream_group_indexs = substreamGroupIndexes;
                                    break;
                                case 5:
                                    d.v1.n_substream_groups = (byte)(bits.ReadBits(2) + 2);
                                    if (d.v1.n_substream_groups == 5) {
                                        d.v1.n_substream_groups = (byte)(d.v1.n_substream_groups + Ac4Utils.Ac4VariableBits(bits, 2));
                                    }
                                    substreamGroupIndexes = new uint[d.v1.n_substream_groups];
                                    for (uint sg = 0; sg < d.v1.n_substream_groups; sg ++){
                                        substreamGroupIndexes[sg] = ParseAc4SgiSpecifier(bits, bitstream_version);
                                        max_group_index = Ac4Utils.SetMaxGroupIndex(substreamGroupIndexes[sg], max_group_index);
                                    }
                                    d.v1.substream_group_indexs = substreamGroupIndexes;
                                    break;
                                default:
                                    // presentation_config_ext_info()
                                    ParsePresentationConfigExtInfo(bits, bitstream_version);
                                    break;
                            }
                        }
                        // b_pre_virtualized
                        d.v1.b_pre_virtualized     = (byte)bits.ReadBit();
                        d.v1.b_add_emdf_substreams = (byte)bits.ReadBit();
                        // ac4_presentation_substream_info()
                        ParsePresentationSubstreamInfo(bits);
                    }

                    if (d.v1.b_add_emdf_substreams != 0){
                        d.v1.n_add_emdf_substreams = (byte)bits.ReadBits(2);
                        if (d.v1.n_add_emdf_substreams == 0){
                            d.v1.n_add_emdf_substreams = (byte)(Ac4Utils.Ac4VariableBits(bits, 2) + 4);
                        }
                        for (uint cnt = 0; cnt < d.v1.n_add_emdf_substreams; cnt ++){
                            ParseEmdInfo(bits, tmp_emdf_info);
                            // PORT NOTE: upstream writes past the 128-entry arrays when
                            // n_add_emdf_substreams > 128 (it is a UI08 here)
                            if (cnt < d.v1.substream_emdf_version.Length) {
                                d.v1.substream_emdf_version[cnt] = tmp_emdf_info.emdf_version;
                                d.v1.substream_key_id[cnt]       = tmp_emdf_info.key_id;
                            }
                        }
                    }
                    if (pres_idx == 0){
                        first_pres_sg_index = substreamGroupIndexes;
                        first_pres_sg_num = d.v1.n_substream_groups;
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::WritePresentationV1Dsi
                +---------------------------------------------------------------------*/
                public Ap4Result WritePresentationV1Dsi(BitWriter bits)
                {
                    bits.Write(d.v1.presentation_config_v1, 5);
                    if (d.v1.presentation_config_v1 == 0x06) {
                        d.v1.b_add_emdf_substreams = 1;
                    }else {
                        bits.Write(d.v1.mdcompat, 3);
                        bits.Write(d.v1.b_presentation_id, 1);
                        if (d.v1.b_presentation_id == 1){
                            bits.Write(d.v1.presentation_id, 5);
                        }
                        bits.Write(d.v1.dsi_frame_rate_multiply_info, 2);
                        bits.Write(d.v1.dsi_frame_rate_fraction_info, 2);
                        bits.Write(d.v1.presentation_emdf_version, 5);
                        bits.Write(d.v1.presentation_key_id, 10);
                        d.v1.b_presentation_channel_coded = (byte)((GetPresentationChMode() == -1) ? 0: 1);
                        bits.Write(d.v1.b_presentation_channel_coded, 1);
                        if (d.v1.b_presentation_channel_coded == 1) {
                            d.v1.dsi_presentation_ch_mode = (byte)GetPresentationChMode();
                            bits.Write(d.v1.dsi_presentation_ch_mode, 5);
                            if (d.v1.dsi_presentation_ch_mode >= 11 && d.v1.dsi_presentation_ch_mode <= 14){
                                GetPresB4BackChannelsPresent();
                                GetPresTopChannelPairs();
                                bits.Write(d.v1.pres_b_4_back_channels_present, 1);
                                bits.Write(d.v1.pres_top_channel_pairs, 2);
                                if (d.v1.pres_top_channel_pairs != 0) {
                                    d.v1.dolby_atmos_indicator = 1;
                                }
                            }
                            d.v1.presentation_channel_mask_v1 = (uint)GetPresentationChannelMask();
                            bits.Write(d.v1.presentation_channel_mask_v1, 24);
                        }
                        int pres_ch_mode_core = GetBPresentationCoreDiffers();
                        bits.Write(d.v1.b_presentation_core_differs = (byte)((pres_ch_mode_core == -1) ? 0: 1), 1);
                        if (d.v1.b_presentation_core_differs == 1) {
                            bits.Write(d.v1.b_presentation_core_channel_coded = (byte)((pres_ch_mode_core == -1) ? 0: 1), 1);
                            if (d.v1.b_presentation_core_channel_coded == 1) {
                                bits.Write(d.v1.dsi_presentation_channel_mode_core = (byte)(pres_ch_mode_core - 3), 2);
                            }
                        }
                        bits.Write(d.v1.b_presentation_filter, 1);
                        if (d.v1.b_presentation_filter == 1) {
                            bits.Write(d.v1.b_enable_presentation, 1);
                            // Encoders implemented according to the present document shall write the value 0
                            d.v1.n_filter_bytes = 0;
                            bits.Write(d.v1.n_filter_bytes, 8);
                            //TODO: filter_data if n_filter_bytes > 0
                        }

                        if (d.v1.presentation_config_v1 == 0x1f){
                            d.v1.substream_groups[0].WriteSubstreamGroupDsi(bits);
                        }else {
                            bits.Write(d.v1.b_multi_pid, 1);
                            if (d.v1.presentation_config_v1 <= 2){
                                d.v1.substream_groups[0].WriteSubstreamGroupDsi(bits);
                                d.v1.substream_groups[1].WriteSubstreamGroupDsi(bits);
                            }
                            if (d.v1.presentation_config_v1 >= 3 && d.v1.presentation_config_v1 <= 4){
                                d.v1.substream_groups[0].WriteSubstreamGroupDsi(bits);
                                d.v1.substream_groups[1].WriteSubstreamGroupDsi(bits);
                                d.v1.substream_groups[2].WriteSubstreamGroupDsi(bits);
                            }
                            if (d.v1.presentation_config_v1 == 5) {
                                bits.Write((uint)(d.v1.n_substream_groups - 2), 3);
                                for (uint sg = 0; sg < d.v1.n_substream_groups; sg ++) {
                                    d.v1.substream_groups[sg].WriteSubstreamGroupDsi(bits);
                                }
                            }
                            if (d.v1.presentation_config_v1 > 5) {
                                d.v1.n_skip_bytes = 0;
                                bits.Write(d.v1.n_skip_bytes, 7);
                                //TODO: skip_data if n_skip_bytes > 0
                            }
                        }

                        // IMS shall set the b_pre_virtualized = 1
                        if (presentation_version == 2) {
                            d.v1.b_pre_virtualized = 1;
                        }
                        bits.Write(d.v1.b_pre_virtualized, 1);
                        bits.Write(d.v1.b_add_emdf_substreams, 1);
                    }
                    if (d.v1.b_add_emdf_substreams == 1){
                        bits.Write(d.v1.n_add_emdf_substreams, 7);
                        for (uint j = 0; j < d.v1.n_add_emdf_substreams; j++){
                            // PORT NOTE: upstream reads past the 128-entry arrays when
                            // n_add_emdf_substreams > 128
                            bits.Write(j < d.v1.substream_emdf_version.Length ? d.v1.substream_emdf_version[j] : (uint)0, 5);
                            bits.Write(j < d.v1.substream_key_id.Length ? d.v1.substream_key_id[j] : (uint)0, 10);
                        }
                    }
                    // TODO: Not implement, can't get the informaiton from TOC, need calcuate by the muxer
                    bits.Write(d.v1.b_presentation_bitrate_info, 1);
                    if (d.v1.b_presentation_bitrate_info == 1) {
                        d.v1.ac4_bitrate_dsi.WriteBitrateDsi(bits);
                    }

                    bits.Write(d.v1.b_alternative, 1);
                    if (d.v1.b_alternative == 1) {
                        Ac4Utils.ByteAlign(bits);
                        // TODO: Not implement, need the information from ac4_presentation_substream
                        d.v1.alternative_info.WriteAlternativeInfo(bits);
                    }
                    Ac4Utils.ByteAlign(bits);

                    /*
                     * TODO: Not implement, need the information from ac4_substream.
                     * Currently just set the value to 1 according to Dolby's internal discussion.
                     */
                    d.v1.de_indicator = 1;
                    bits.Write(d.v1.de_indicator, 1);

                    bits.Write(d.v1.dolby_atmos_indicator, 1);
                    bits.Write(0, 4);       //reserved bits

                    if (d.v1.presentation_id > 31) {
                        d.v1.b_extended_presentation_id = 1;
                        d.v1.extended_presentation_id = d.v1.presentation_id;
                    }
                    bits.Write(d.v1.b_extended_presentation_id, 1);
                    if (d.v1.b_extended_presentation_id == 1) {
                        bits.Write(d.v1.extended_presentation_id, 9);
                    }else {
                        bits.Write(0, 1);   //reserved bit
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::ParsePresentationVersion
                +---------------------------------------------------------------------*/
                private Ap4Result ParsePresentationVersion(BitReader bits, uint bitstream_version)
                {
                    presentation_version = 0;
                    if (bitstream_version != 1){
                        while(bits.ReadBit() == 1){
                            presentation_version ++;
                        }
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::ParsePresentationConfigExtInfo
                +---------------------------------------------------------------------*/
                private Ap4Result ParsePresentationConfigExtInfo(BitReader bits, uint bitstream_version)
                {
                    uint nSkipBytes = bits.ReadBits(5);
                    if (bits.ReadBit() != 0) {  // b_more_skip_bytes
                        nSkipBytes += (Ac4Utils.Ac4VariableBits(bits, 2) << 5);
                    }
                    if (bitstream_version == 1 && d.v1.presentation_config_v1 == 7) {
                        // TODO: refer to chapte 6.2.1.5 - TS 103 190-2
                    }
                    bits.ReadBits(nSkipBytes * 8);
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::ParseAc4SgiSpecifier
                +---------------------------------------------------------------------*/
                private uint ParseAc4SgiSpecifier(BitReader bits, uint bitstream_version)
                {
                    if (bitstream_version  == 1) {
                        // Error
                        return 0;
                    } else{
                        uint groupIndex = bits.ReadBits(3);
                        if (groupIndex == 7) {
                            groupIndex += Ac4Utils.Ac4VariableBits(bits, 2);
                        }
                        return groupIndex;
                    }
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::ParseDSIFrameRateMultiplyInfo
                +---------------------------------------------------------------------*/
                private Ap4Result ParseDSIFrameRateMultiplyInfo(BitReader bits, uint frame_rate_idx)
                {
                    switch (frame_rate_idx) {
                        case 2:
                        case 3:
                        case 4:
                            if (bits.ReadBit() != 0) {                      // b_multiplier
                                uint multiplier_bit = (uint)bits.ReadBit(); //multiplier_bit
                                d.v1.dsi_frame_rate_multiply_info = (byte)((multiplier_bit == 0)? 1: 2);
                            }
                            else {
                                d.v1.dsi_frame_rate_multiply_info = 0;
                            }
                            break;
                        case 0:
                        case 1:
                        case 7:
                        case 8:
                        case 9:
                            if (bits.ReadBit() != 0) {
                                d.v1.dsi_frame_rate_multiply_info = 1;
                            }else {
                                d.v1.dsi_frame_rate_multiply_info = 0;
                            }
                            break;
                        default:
                            d.v1.dsi_frame_rate_multiply_info = 0;
                            break;
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::ParseDSIFrameRateFractionsInfo
                +---------------------------------------------------------------------*/
                private Ap4Result ParseDSIFrameRateFractionsInfo(BitReader bits, uint frame_rate_idx)
                {
                     if (frame_rate_idx >= 5 && frame_rate_idx <= 9) {
                        if (bits.ReadBit() == 1) {      // b_frame_rate_fraction
                            d.v1.dsi_frame_rate_fraction_info = 1;
                        }else{
                            d.v1.dsi_frame_rate_fraction_info = 0;
                        }
                    }else if (frame_rate_idx >= 10 && frame_rate_idx <= 12){
                        if (bits.ReadBit() == 1) {      // b_frame_rate_fraction
                            if (bits.ReadBit() == 1) {  // b_frame_rate_fraction_is_4
                                d.v1.dsi_frame_rate_fraction_info = 2;
                            } else {
                                d.v1.dsi_frame_rate_fraction_info = 1;
                            }

                        }else{
                            d.v1.dsi_frame_rate_fraction_info = 0;
                        }
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::ParseEmdInfo
                +---------------------------------------------------------------------*/
                private Ap4Result ParseEmdInfo(BitReader bits, Ac4EmdfInfo emdf_info)
                {
                    emdf_info.emdf_version = (byte)bits.ReadBits(2);
                    if (emdf_info.emdf_version == 3) {
                        emdf_info.emdf_version = (byte)(emdf_info.emdf_version + Ac4Utils.Ac4VariableBits(bits, 2));
                    }
                    emdf_info.key_id = (ushort)bits.ReadBits(3);
                    if (emdf_info.key_id == 7) {
                        emdf_info.key_id = (ushort)(emdf_info.key_id + Ac4Utils.Ac4VariableBits(bits, 3));
                    }
                    emdf_info.b_emdf_payloads_substream_info = (byte)bits.ReadBit();
                    if (emdf_info.b_emdf_payloads_substream_info == 1) {
                        // emdf_payloads_substream_info ()
                        if (bits.ReadBits(2) == 3) {    // substream_index
                            Ac4Utils.Ac4VariableBits(bits, 2);
                        }
                    }
                    emdf_info.protectionLengthPrimary   = (byte)bits.ReadBits(2);
                    emdf_info.protectionLengthSecondary = (byte)bits.ReadBits(2);
                    // protection_bits_primary
                    switch (emdf_info.protectionLengthPrimary) {
                        case 1:
                            emdf_info.protection_bits_primary[0] = (byte)bits.ReadBits(8);
                            break;
                        case 2:
                            for (uint idx = 0; idx < 4; idx ++)  { emdf_info.protection_bits_primary[idx] = (byte)bits.ReadBits(8); }
                            break;
                        case 3:
                            for (uint idx = 0; idx < 16; idx ++) { emdf_info.protection_bits_primary[idx] = (byte)bits.ReadBits(8); }
                            break;
                        default:
                            // Error
                            break;
                    }
                    // protection_bits_secondary
                    switch (emdf_info.protectionLengthSecondary) {
                        case 0:
                            break;
                        case 1:
                            emdf_info.protection_bits_Secondary[0] = (byte)bits.ReadBits(8);
                            break;
                        case 2:
                            for (uint idx = 0; idx < 4; idx ++)  { emdf_info.protection_bits_Secondary[idx] = (byte)bits.ReadBits(8); }
                            break;
                        case 3:
                            for (uint idx = 0; idx < 16; idx ++) { emdf_info.protection_bits_Secondary[idx] = (byte)bits.ReadBits(8); }
                            break;
                        default:
                            // TODO: Error
                            break;
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::ParsePresentationSubstreamInfo
                +---------------------------------------------------------------------*/
                private Ap4Result ParsePresentationSubstreamInfo(BitReader bits)
                {
                    d.v1.b_alternative = (byte)bits.ReadBit();
                    /* unsigned int b_pres_ndot = */ bits.ReadBit();          // b_pres_ndot;
                    uint substream_index = bits.ReadBits(2);    //substream_index
                    if (substream_index == 3){
                        Ac4Utils.Ac4VariableBits(bits, 2);
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::GetPresentationChMode
                +---------------------------------------------------------------------*/
                private int GetPresentationChMode()
                {
                    int pres_ch_mode = -1;
                    sbyte b_obj_or_ajoc = 0;
                    // TODO: n_substream_groups
                    for (uint sg = 0; sg < d.v1.n_substream_groups; sg++){
                        Dac4Atom.Ac4Dsi.SubStreamGroupV1 substream_group = d.v1.substream_groups[sg];
                        uint n_substreams = d.v1.substream_groups[sg].d.v1.n_substreams;
                        for (uint sus = 0; sus < n_substreams; sus++){
                            Dac4Atom.Ac4Dsi.SubStream substream = substream_group.d.v1.substreams[sus];
                            if (substream_group.d.v1.b_channel_coded != 0){
                                pres_ch_mode = Ac4Utils.Ac4SuperSet(pres_ch_mode, substream.ch_mode);
                            }else {
                                b_obj_or_ajoc = 1;
                            }
                        }
                    }
                    if (b_obj_or_ajoc == 1) { pres_ch_mode = -1; }
                    return pres_ch_mode;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::GetPresentationChannelMask
                +---------------------------------------------------------------------*/
                private int GetPresentationChannelMask()
                {
                    uint channel_mask = 0;
                    sbyte b_obj_or_ajoc = 0;
                    // TODO: n_substream_groups
                    for (uint sg = 0; sg < d.v1.n_substream_groups; sg ++){
                        Dac4Atom.Ac4Dsi.SubStreamGroupV1 substream_group = d.v1.substream_groups[sg];
                        uint n_substreams = d.v1.substream_groups[sg].d.v1.n_substreams;
                        for (uint sus = 0; sus < n_substreams; sus ++){
                            Dac4Atom.Ac4Dsi.SubStream substream = substream_group.d.v1.substreams[sus];
                            if (substream_group.d.v1.b_channel_coded != 0){
                                channel_mask |= substream.dsi_substream_channel_mask;
                            }else {
                                b_obj_or_ajoc = 1;
                            }
                        }
                    }
                    //Modify the mask for headphone presentation with b_pre_virtualized = 1
                    /*
                    if (presentation_version == 2 || d.v1.b_pre_virtualized == 1){
                        if (channel_mask == 0x03) { channel_mask = 0x01;}
                    }
                    */
                    // TODO: temporary solution according to Dolby's internal discussion
                    if (channel_mask == 0x03) { channel_mask = 0x01;}

                    // If one substream contains Tfl, Tfr, Tbl, Tbr, Tl and Tr shall be removed.
                    if ((channel_mask & 0x30) != 0 && (channel_mask & 0x80) != 0)  { channel_mask &= ~0x80u;}

                    // objective channel mask
                    if (b_obj_or_ajoc == 1) { channel_mask = 0x800000; }
                    return (int)channel_mask;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::GetPresB4BackChannelsPresent
                +---------------------------------------------------------------------*/
                private Ap4Result GetPresB4BackChannelsPresent()
                {
                    for (uint sg = 0; sg < d.v1.n_substream_groups; sg ++){
                        Dac4Atom.Ac4Dsi.SubStreamGroupV1 substream_group = d.v1.substream_groups[sg];
                        for (uint sus = 0; sus < substream_group.d.v1.n_substreams; sus ++){
                            d.v1.pres_b_4_back_channels_present |=  substream_group.d.v1.substreams[sus].b_4_back_channels_present;
                        }
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::GetPresTopChannelPairs
                +---------------------------------------------------------------------*/
                private Ap4Result GetPresTopChannelPairs()
                {
                    byte tmp_pres_top_channel_pairs = 0;
                    for (uint sg = 0; sg < d.v1.n_substream_groups; sg ++){
                        Dac4Atom.Ac4Dsi.SubStreamGroupV1 substream_group = d.v1.substream_groups[sg];
                        for (uint sus = 0; sus < substream_group.d.v1.n_substreams; sus ++){
                            if (tmp_pres_top_channel_pairs < substream_group.d.v1.substreams[sus].top_channels_present) {
                                tmp_pres_top_channel_pairs = substream_group.d.v1.substreams[sus].top_channels_present;
                            }
                        }
                    }
                    switch (tmp_pres_top_channel_pairs){
                        case 0:
                            d.v1.pres_top_channel_pairs = 0; break;
                        case 1:
                        case 2:
                            d.v1.pres_top_channel_pairs = 1; break;
                        case 3:
                            d.v1.pres_top_channel_pairs = 2; break;
                        default:
                            d.v1.pres_top_channel_pairs = 0; break;
                    }
                    return Ap4Result.SUCCESS;
                }

                /*----------------------------------------------------------------------
                |   AP4_Dac4Atom::Ac4Dsi::PresentationV1::GetBPresentationCoreDiffers
                +---------------------------------------------------------------------*/
                private int GetBPresentationCoreDiffers()
                {
                    int pres_ch_mode_core = -1;
                    sbyte b_obj_or_ajoc_adaptive = 0;
                    for (uint sg = 0; sg < d.v1.n_substream_groups; sg ++){
                        Dac4Atom.Ac4Dsi.SubStreamGroupV1 substream_group = d.v1.substream_groups[sg];
                        uint n_substreams = d.v1.substream_groups[sg].d.v1.n_substreams;
                        for (uint sus = 0; sus < n_substreams; sus ++){
                            Dac4Atom.Ac4Dsi.SubStream substream = substream_group.d.v1.substreams[sus];
                            if (substream_group.d.v1.b_channel_coded != 0){
                                pres_ch_mode_core = Ac4Utils.Ac4SuperSet(pres_ch_mode_core, substream.GetChModeCore(substream_group.d.v1.b_channel_coded));
                            }else {
                                if (substream.b_ajoc != 0){
                                    if (substream.b_static_dmx != 0){
                                        pres_ch_mode_core = Ac4Utils.Ac4SuperSet(pres_ch_mode_core, substream.GetChModeCore(substream_group.d.v1.b_channel_coded));
                                    }else{
                                        b_obj_or_ajoc_adaptive = 1;
                                    }
                                }else{
                                    b_obj_or_ajoc_adaptive = 1;
                                }
                            }
                        }
                    }
                    if (b_obj_or_ajoc_adaptive != 0) {
                        pres_ch_mode_core = -1;
                    }
                    if (pres_ch_mode_core == GetPresentationChMode()) {
                        pres_ch_mode_core = -1;
                    }
                    return pres_ch_mode_core;
                }
            }

            public byte ac4_dsi_version;

            public class V0
            {
                public byte   bitstream_version;
                public byte   fs_index;
                public uint   fs;
                public byte   frame_rate_index;
                public ushort n_presentations;
                // more fields from `ac4_dsi` are not included here, v0 is deprecated.

                public V0 Clone()
                {
                    return (V0)MemberwiseClone();
                }
            }

            public class V1
            {
                public byte              bitstream_version;
                public byte              fs_index;
                public uint              fs;
                public byte              frame_rate_index;
                public byte              b_program_id;
                public ushort            short_program_id;
                public byte              b_uuid;
                public byte[]            program_uuid = new byte[16];
                public Ac4BitrateDsi     ac4_bitrate_dsi;
                public ushort            n_presentations;
                public PresentationV1[]  presentations;

                public V1 Clone()
                {
                    V1 copy = (V1)MemberwiseClone();   // `presentations` stays shared, like the upstream pointer
                    copy.program_uuid = (byte[])program_uuid.Clone();
                    return copy;
                }
            }

            // PORT NOTE: union upstream; v0 and v1 do not share storage in the port
            public class D
            {
                public V0 v0 = new V0();
                public V1 v1 = new V1();
            }

            public D d = new D();

            // PORT NOTE: C++ struct copy
            public Ac4Dsi Clone()
            {
                Ac4Dsi copy = new Ac4Dsi();
                copy.ac4_dsi_version = ac4_dsi_version;
                copy.d.v0 = d.v0.Clone();
                copy.d.v1 = d.v1.Clone();
                return copy;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac4Atom::Create
        +---------------------------------------------------------------------*/
        public static Dac4Atom Create(uint size, ByteStream stream)
        {
            // read the raw bytes in a buffer
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            DataBuffer payload_data = new DataBuffer(payload_size);
            Ap4Result result = stream.Read(payload_data.UseData(), payload_size);
            if (result.Failed()) return null;

            // check the version
            byte[] payload = payload_data.GetData();
            return new Dac4Atom(size, payload);
        }

        // PORT NOTE: the copy constructor AP4_Dac4Atom(const AP4_Dac4Atom& other) is
        // declared but never defined upstream; it is not ported.

        /*----------------------------------------------------------------------
        |   AP4_Dac4Atom::AP4_Dac4Atom
        +---------------------------------------------------------------------*/
        // DSI vaiable initialize m_RawBytes (m_Dsi -> m_RawBytes)
        public Dac4Atom(uint size, Ac4Dsi ac4Dsi) :
            base(AtomType.DAC4, Ap4.ATOM_HEADER_SIZE)
        {
            BitWriter bits = new BitWriter(size);
            bits.Write(ac4Dsi.ac4_dsi_version, 3);
            bits.Write(ac4Dsi.d.v1.bitstream_version, 7);
            bits.Write(ac4Dsi.d.v1.fs_index, 1);
            bits.Write(ac4Dsi.d.v1.frame_rate_index, 4);

            // IMS presentation shall has the legacy presentation
            uint add_n_presentations = 0;
            for (uint idx = 0; idx < ac4Dsi.d.v1.n_presentations; idx++){
                if (ac4Dsi.d.v1.presentations[idx].presentation_version == 2) {
                    add_n_presentations++;
                }
            }

            // Assume the total presentation numbers is less than 512 after adding legacy presentation
            bits.Write(ac4Dsi.d.v1.n_presentations + add_n_presentations, 9);

            if (ac4Dsi.d.v1.bitstream_version > 1){
                bits.Write(ac4Dsi.d.v1.b_program_id, 1);
                if (ac4Dsi.d.v1.b_program_id == 1){
                    bits.Write(ac4Dsi.d.v1.short_program_id, 16);
                    bits.Write(ac4Dsi.d.v1.b_uuid, 1);
                    if (ac4Dsi.d.v1.b_uuid == 1) {
                        for (uint idx = 0; idx < 16; idx ++) {
                            bits.Write(ac4Dsi.d.v1.program_uuid[idx], 8);
                        }
                    }
                }
            }
            // ac4_bitrate_dsi()
            Dac4Atom.Ac4Dsi.Ac4BitrateDsi ac4_bitrate_dsi = ac4Dsi.d.v1.ac4_bitrate_dsi;
            ac4_bitrate_dsi.WriteBitrateDsi(bits);

            Ac4Utils.ByteAlign(bits);
            for (uint idx = 0; idx < ac4Dsi.d.v1.n_presentations; idx ++) {
                uint default_pres_bytes = 36;   // random value
                Dac4Atom.Ac4Dsi.PresentationV1 presentation = ac4Dsi.d.v1.presentations[idx];
                bits.Write(presentation.presentation_version, 8);
                bits.Write(default_pres_bytes, 8);      // pres_bytes, need to be updated later
                //TODO: if (pres_bytes == 255), shall not happen now. Need the memory move function for bits
                uint pres_bytes_idx = bits.GetBitCount() / 8 - 1;
                if (ac4Dsi.d.v1.n_presentations != 1 && presentation.d.v1.b_presentation_id == 0 && presentation.d.v1.b_extended_presentation_id == 0) {
                    Console.Error.Write("WARN: Need presentation_id for multiple presnetaion signal. The presentation of Presentation Index (PI) is " +
                                        ((int)(idx + 1)).ToString(CultureInfo.InvariantCulture) + " miss presentation_id.\n");
                }
                if (presentation.presentation_version == 0){
                    // TODO: support presentation version = 0
                }else if (presentation.presentation_version == 1 || presentation.presentation_version == 2){
                    presentation.WritePresentationV1Dsi(bits);
                    Ac4Utils.Ac4UpdatePresBytes(bits.GetData(), pres_bytes_idx, (byte)(bits.GetBitCount()/8 - pres_bytes_idx - 1));
                } else {
                    Ac4Utils.Ac4UpdatePresBytes(bits.GetData(), pres_bytes_idx, 0);
                }

                //legacy presentation for IMS
                if (presentation.presentation_version == 2) {
                    Dac4Atom.Ac4Dsi.PresentationV1 legacy_presentation = presentation.Clone();
                    if (legacy_presentation.d.v1.b_presentation_id == 0 && legacy_presentation.d.v1.b_extended_presentation_id == 0) {
                        Console.Error.Write("WARN: Need presentation_id for IMS signal.\n");
                    }
                    legacy_presentation.presentation_version = 1;
                    legacy_presentation.d.v1.b_pre_virtualized = 0;
                    legacy_presentation.d.v1.dolby_atmos_indicator = 0;

                    bits.Write(legacy_presentation.presentation_version, 8);
                    bits.Write(default_pres_bytes, 8);    // pres_bytes, need to be updated later
                    //TODO: if (pres_bytes == 255), shall not happen now
                    // PORT NOTE: named pres_bytes_idx upstream (shadows the outer variable)
                    uint legacy_pres_bytes_idx = bits.GetBitCount() / 8 - 1;
                    legacy_presentation.WritePresentationV1Dsi(bits);
                    Ac4Utils.Ac4UpdatePresBytes(bits.GetData(), legacy_pres_bytes_idx, (byte)(bits.GetBitCount()/8 - legacy_pres_bytes_idx - 1));
                }
            }
            _rawBytes.SetData(bits.GetData(), bits.GetBitCount() / 8);
            _size32 += _rawBytes.GetDataSize();
            // clear the DSI
            _dsi = new Ac4Dsi();
            _dsi.ac4_dsi_version = 0xFF; // = -1 upstream
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac4Atom::AP4_Dac4Atom
        +---------------------------------------------------------------------*/
        // box data initialize m_Dsi (m_RawBytes -> m_Dsi)
        private Dac4Atom(uint size, byte[] payload) :
            base(AtomType.DAC4, size)
        {
            // clear the DSI
            _dsi = new Ac4Dsi();

            // make a copy of our configuration bytes
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            _rawBytes.SetData(payload, payload_size);

            // sanity check
            if (payload_size < 11) return;

            // parse the `ac4_dsi` or `ac4_dsi_v1` bits
            BitReader bits = new BitReader(payload, payload_size);
            _dsi.ac4_dsi_version = (byte)bits.ReadBits(3);
            if (_dsi.ac4_dsi_version == 0) {
                _dsi.d.v0.bitstream_version = (byte)bits.ReadBits(7);
                _dsi.d.v0.fs_index          = (byte)bits.ReadBits(1);
                _dsi.d.v0.frame_rate_index  = (byte)bits.ReadBits(4);
                _dsi.d.v0.n_presentations   = (ushort)bits.ReadBits(9);

                // fill in computed fields
                _dsi.d.v0.fs = (uint)(_dsi.d.v0.fs_index == 0 ? 44100 : 48000);
            } else if (_dsi.ac4_dsi_version == 1) {
                _dsi.d.v1.bitstream_version = (byte)bits.ReadBits(7);
                _dsi.d.v1.fs_index          = (byte)bits.ReadBits(1);
                _dsi.d.v1.frame_rate_index  = (byte)bits.ReadBits(4);
                _dsi.d.v1.n_presentations   = (ushort)bits.ReadBits(9);
                if (_dsi.d.v1.bitstream_version > 1) {
                    if (bits.ReadBit() != 0) {
                        _dsi.d.v1.short_program_id = (ushort)bits.ReadBits(16);
                        if (bits.ReadBit() != 0) {
                            for (uint i = 0; i < 16; i ++) {
                                _dsi.d.v1.program_uuid[i] = (byte)bits.ReadBits(8);
                            }
                        }
                    }
                }
                _dsi.d.v1.ac4_bitrate_dsi.bit_rate_mode      = (byte)bits.ReadBits(2);
                _dsi.d.v1.ac4_bitrate_dsi.bit_rate           = bits.ReadBits(32);
                _dsi.d.v1.ac4_bitrate_dsi.bit_rate_precision = bits.ReadBits(32);

                // byte align
                if ((bits.GetBitsRead() % 8) != 0) {
                    bits.SkipBits(8 - (bits.GetBitsRead() % 8));
                }

                // parse the presentations
                _dsi.d.v1.presentations = new Ac4Dsi.PresentationV1[_dsi.d.v1.n_presentations];
                for (int i = 0; i < _dsi.d.v1.n_presentations; i++) {
                    _dsi.d.v1.presentations[i] = new Ac4Dsi.PresentationV1();
                }
                for (uint i = 0; i < _dsi.d.v1.n_presentations; i++) {
                    Ac4Dsi.PresentationV1 presentation = _dsi.d.v1.presentations[i];

                    presentation.presentation_version = (byte)bits.ReadBits(8);
                    uint pres_bytes                   = bits.ReadBits(8);
                    if (pres_bytes == 255) {
                        pres_bytes += bits.ReadBits(16);
                    }
                    uint bits_read_before_presentation = bits.GetBitsRead();
                    if (presentation.presentation_version == 0) {
                        presentation.d.v0.presentation_config = (byte)bits.ReadBits(5);
                        //bool b_add_emdf_substreams = false;
                        if (presentation.d.v0.presentation_config == 0x06) {
                            //b_add_emdf_substreams = true;
                        } else {
                            presentation.d.v0.mdcompat = (byte)bits.ReadBits(3);
                            if (bits.ReadBit() != 0) { // b_presentation_id
                                presentation.d.v0.presentation_id = (byte)bits.ReadBits(5);
                            }
                            presentation.d.v0.dsi_frame_rate_multiply_info = (byte)bits.ReadBits(2);
                            presentation.d.v0.presentation_emdf_version    = (byte)bits.ReadBits(5);
                            presentation.d.v0.presentation_key_id          = (ushort)bits.ReadBits(10);
                            presentation.d.v0.presentation_channel_mask    = bits.ReadBits(24);
                        }
                        // some fields skipped
                        if ((bits.GetBitsRead() % 8) != 0) {
                            bits.SkipBits(8 - (bits.GetBitsRead() % 8));
                        }
                    } else if (presentation.presentation_version == 1 || presentation.presentation_version == 2) {
                        presentation.d.v1.presentation_config_v1 = (byte)bits.ReadBits(5);
                        if (presentation.d.v1.presentation_config_v1 == 0x06) {
                            presentation.d.v1.b_add_emdf_substreams = 1;
                        } else {
                            presentation.d.v1.mdcompat = (byte)bits.ReadBits(3);
                            presentation.d.v1.b_presentation_id = (byte)bits.ReadBit();
                            if (presentation.d.v1.b_presentation_id != 0) {
                                presentation.d.v1.presentation_id = (byte)bits.ReadBits(5);
                            }
                            presentation.d.v1.dsi_frame_rate_multiply_info = (byte)bits.ReadBits(2);
                            presentation.d.v1.dsi_frame_rate_fraction_info = (byte)bits.ReadBits(2);
                            presentation.d.v1.presentation_emdf_version    = (byte)bits.ReadBits(5);
                            presentation.d.v1.presentation_key_id          = (ushort)bits.ReadBits(10);
                            presentation.d.v1.b_presentation_channel_coded = (byte)bits.ReadBit();
                            if (presentation.d.v1.b_presentation_channel_coded != 0) {
                                presentation.d.v1.dsi_presentation_ch_mode = (byte)bits.ReadBits(5);
                                if (presentation.d.v1.dsi_presentation_ch_mode == 11 ||
                                    presentation.d.v1.dsi_presentation_ch_mode == 12 ||
                                    presentation.d.v1.dsi_presentation_ch_mode == 13 ||
                                    presentation.d.v1.dsi_presentation_ch_mode == 14) {
                                    presentation.d.v1.pres_b_4_back_channels_present = (byte)bits.ReadBit();
                                    presentation.d.v1.pres_top_channel_pairs         = (byte)bits.ReadBits(2);
                                }
                                presentation.d.v1.presentation_channel_mask_v1 = bits.ReadBits(24);
                            }else {
                                presentation.d.v1.presentation_channel_mask_v1 = 0x800000;
                            }
                            presentation.d.v1.b_presentation_core_differs = (byte)bits.ReadBit();
                            if (presentation.d.v1.b_presentation_core_differs != 0) {
                                presentation.d.v1.b_presentation_core_channel_coded = (byte)bits.ReadBit();
                                if (presentation.d.v1.b_presentation_core_channel_coded != 0) {
                                    presentation.d.v1.dsi_presentation_channel_mode_core = (byte)bits.ReadBits(2);
                                }
                            }
                            presentation.d.v1.b_presentation_filter = (byte)bits.ReadBit();
                            if (presentation.d.v1.b_presentation_filter != 0) {
                                presentation.d.v1.b_enable_presentation = (byte)bits.ReadBit();
                                presentation.d.v1.n_filter_bytes = (byte)bits.ReadBits(8);
                                for (int idx = 0; idx < presentation.d.v1.n_filter_bytes; idx ++) { bits.SkipBits(8); }
                            }
                            bool parse_substream_groups = true;
                            if (presentation.d.v1.presentation_config_v1 == 0x1f){
                                presentation.d.v1.n_substream_groups = 1;
                            } else {
                                presentation.d.v1.b_multi_pid = (byte)bits.ReadBit();
                                switch (presentation.d.v1.presentation_config_v1){
                                    case 0:
                                    case 1:
                                    case 2:
                                        presentation.d.v1.n_substream_groups = 2;
                                        break;
                                    case 3:
                                    case 4:
                                        presentation.d.v1.n_substream_groups = 3;
                                        break;
                                    case 5:
                                        presentation.d.v1.n_substream_groups =  (byte)(bits.ReadBits(3) + 2);
                                        break;
                                    default:
                                        parse_substream_groups = false;
                                        presentation.d.v1.n_skip_bytes = (byte)bits.ReadBits(7);
                                        for (int idx = 0; idx < presentation.d.v1.n_skip_bytes; idx ++) { bits.SkipBits(8); }
                                        break;
                                }

                            }
                            // Start to parse substream_groups
                            if (parse_substream_groups) {
                                presentation.d.v1.substream_groups = new Ac4Dsi.SubStreamGroupV1[presentation.d.v1.n_substream_groups];
                                for (int cnt = 0; cnt < presentation.d.v1.n_substream_groups; cnt ++) {
                                    presentation.d.v1.substream_groups[cnt] = new Ac4Dsi.SubStreamGroupV1();
                                }
                                for (int cnt = 0; cnt < presentation.d.v1.n_substream_groups; cnt ++){
                                     Ac4Dsi.SubStreamGroupV1 substreamgroup = presentation.d.v1.substream_groups[cnt];
                                     substreamgroup.d.v1.b_substreams_present = (byte)bits.ReadBit();
                                     substreamgroup.d.v1.b_hsf_ext            = (byte)bits.ReadBit();
                                     substreamgroup.d.v1.b_channel_coded      = (byte)bits.ReadBit();
                                     substreamgroup.d.v1.n_substreams         = (byte)bits.ReadBits(8);

                                     substreamgroup.d.v1.substreams = new Ac4Dsi.SubStream[substreamgroup.d.v1.n_substreams];
                                     for (int sus = 0; sus < substreamgroup.d.v1.n_substreams; sus ++) {
                                         substreamgroup.d.v1.substreams[sus] = new Ac4Dsi.SubStream();
                                     }
                                     for (int sus = 0; sus < substreamgroup.d.v1.n_substreams; sus ++){
                                         Ac4Dsi.SubStream substream = substreamgroup.d.v1.substreams[sus];
                                         substream.dsi_sf_multiplier = (byte)bits.ReadBits(2);
                                         substream.b_substream_bitrate_indicator = (byte)bits.ReadBit();
                                         if (substream.b_substream_bitrate_indicator != 0) {
                                            substream.substream_bitrate_indicator = (byte)bits.ReadBits(5);
                                         }
                                         if (substreamgroup.d.v1.b_channel_coded != 0){
                                            substream.dsi_substream_channel_mask = bits.ReadBits(24);
                                         } else {
                                            substream.b_ajoc = (byte)bits.ReadBit();
                                            if (substream.b_ajoc != 0) {
                                                substream.b_static_dmx = (byte)bits.ReadBit();
                                                if (substream.b_static_dmx == 0) {
                                                    substream.n_dmx_objects_minus1 = (byte)bits.ReadBits(4);
                                                }
                                                substream.n_umx_objects_minus1 = (byte)bits.ReadBits(6);
                                            }
                                            substream.b_substream_contains_bed_objects = (byte)bits.ReadBit();
                                            substream.b_substream_contains_dynamic_objects = (byte)bits.ReadBit();
                                            substream.b_substream_contains_ISF_objects = (byte)bits.ReadBit();
                                            bits.SkipBit(); // reserved
                                        }
                                     }
                                     substreamgroup.d.v1.b_content_type = (byte)bits.ReadBit();
                                     if (substreamgroup.d.v1.b_content_type != 0) {
                                         substreamgroup.d.v1.content_classifier = (byte)bits.ReadBits(3);
                                         substreamgroup.d.v1.b_language_indicator = (byte)bits.ReadBit();
                                         if (substreamgroup.d.v1.b_language_indicator != 0) {
                                             substreamgroup.d.v1.n_language_tag_bytes = (byte)bits.ReadBits(6);
                                             for (int l = 0; l < substreamgroup.d.v1.n_language_tag_bytes; l++) {
                                                 substreamgroup.d.v1.language_tag_bytes[l] = (byte)bits.ReadBits(8);
                                             }
                                         }
                                     }
                                }
                            }
                            presentation.d.v1.b_pre_virtualized = (byte)bits.ReadBit();
                            presentation.d.v1.b_add_emdf_substreams = (byte)bits.ReadBit();
                        }
                        if (presentation.d.v1.b_add_emdf_substreams != 0) {
                            presentation.d.v1.n_add_emdf_substreams = (byte)bits.ReadBits(7);
                            for (int j = 0; j < presentation.d.v1.n_add_emdf_substreams; j ++) {
                                presentation.d.v1.substream_emdf_version[j] = (byte)bits.ReadBits(5);
                                presentation.d.v1.substream_key_id[j] = (ushort)bits.ReadBits(10);
                            }
                        }
                        presentation.d.v1.b_presentation_bitrate_info = (byte)bits.ReadBit();
                        if (presentation.d.v1.b_presentation_bitrate_info != 0) {
                            presentation.d.v1.ac4_bitrate_dsi.bit_rate_mode      = (byte)bits.ReadBits(2);
                            presentation.d.v1.ac4_bitrate_dsi.bit_rate           = bits.ReadBits(32);
                            presentation.d.v1.ac4_bitrate_dsi.bit_rate_precision = bits.ReadBits(32);
                        }
                        presentation.d.v1.b_alternative = (byte)bits.ReadBit();
                        if (presentation.d.v1.b_alternative != 0) {
                            // byte align
                            if ((bits.GetBitsRead() % 8) != 0) {
                                bits.SkipBits(8 - (bits.GetBitsRead() % 8));
                            }

                            presentation.d.v1.alternative_info.name_len = (ushort)bits.ReadBits(16);
                            for (int n = 0; n < presentation.d.v1.alternative_info.name_len; n++) {
                                // PORT NOTE: upstream writes past presentation_name[256] when
                                // name_len > 256 (buffer overflow); the extra bytes are read and
                                // dropped here.
                                byte presentation_name_byte = (byte)bits.ReadBits(8);
                                if (n < presentation.d.v1.alternative_info.presentation_name.Length) {
                                    presentation.d.v1.alternative_info.presentation_name[n] = presentation_name_byte;
                                }
                            }
                            presentation.d.v1.alternative_info.n_targets = (byte)bits.ReadBits(5);
                            for (int t = 0; t < presentation.d.v1.alternative_info.n_targets; t++ ){
                                presentation.d.v1.alternative_info.target_md_compat[t] = (byte)bits.ReadBits(3);
                                presentation.d.v1.alternative_info.target_device_category[t] = (byte)bits.ReadBits(8);
                            }
                        }
                        // byte align
                        if ((bits.GetBitsRead() % 8) != 0) {
                            bits.SkipBits(8 - (bits.GetBitsRead() % 8));
                        }
                        presentation.d.v1.de_indicator = (byte)bits.ReadBit();
                        presentation.d.v1.dolby_atmos_indicator = (byte)bits.ReadBit();
                        bits.SkipBits(4);
                        presentation.d.v1.b_extended_presentation_id = (byte)bits.ReadBit();
                        if (presentation.d.v1.b_extended_presentation_id != 0){
                            presentation.d.v1.extended_presentation_id = (ushort)bits.ReadBits(9);
                        } else {
                            bits.SkipBit();
                        }
                    }
                    uint bits_read_after_presentation = bits.GetBitsRead();
                    uint presentation_bytes = (bits_read_after_presentation - bits_read_before_presentation) / 8;
                    if (pres_bytes < presentation_bytes) {
                        break;
                    }
                    uint skip_bytes = pres_bytes - presentation_bytes;
                    for (uint skip = 0; skip < skip_bytes; skip++) {
                        bits.SkipBits(8);
                    }
                }

                // fill in computed fields
                _dsi.d.v1.fs = (uint)(_dsi.d.v1.fs_index == 0 ? 44100 : 48000);
            } else {
                // not supported
                return;
            }
        }

        // PORT NOTE: the destructor only frees memory and is not ported.

        /*----------------------------------------------------------------------
        |   AP4_Dac4Atom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            return stream.Write(_rawBytes.GetData(), _rawBytes.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac4Atom::GetCodecString
        +---------------------------------------------------------------------*/
        public void GetCodecString(out string codec)
        {
            byte bitstream_version    = 0;
            byte presentation_version = 0;
            byte mdcompat             = 0;
            if (_dsi.ac4_dsi_version == 0) {
                bitstream_version = _dsi.d.v0.bitstream_version;
            } else if (_dsi.ac4_dsi_version == 1) {
                bitstream_version = _dsi.d.v1.bitstream_version;
                if (_dsi.d.v1.n_presentations != 0) {
                    presentation_version = _dsi.d.v1.presentations[0].presentation_version;
                    if (presentation_version == 0) {
                        mdcompat = _dsi.d.v1.presentations[0].d.v0.mdcompat;
                    } else if (presentation_version == 1 || presentation_version == 2) {
                        mdcompat = _dsi.d.v1.presentations[0].d.v1.mdcompat;
                        for (int idx = 0; idx < _dsi.d.v1.n_presentations; idx ++){
                            // PORT NOTE: upstream reads d.v1.mdcompat of every presentation,
                            // whatever its version. For a version 0 presentation that
                            // union member aliases d.v0.mdcompat (same offset), which the
                            // port has to read explicitly since v0/v1 do not share storage.
                            Ac4Dsi.PresentationV1 p = _dsi.d.v1.presentations[idx];
                            byte p_mdcompat = (p.presentation_version == 0) ? p.d.v0.mdcompat : p.d.v1.mdcompat;
                            if (mdcompat > p_mdcompat) {
                                mdcompat = p_mdcompat;
                            }
                        }
                    }
                }
            }
            // "ac-4.%02x.%02x.%02x"
            string @string = "ac-4." +
                             bitstream_version.ToString("x2", CultureInfo.InvariantCulture) + "." +
                             presentation_version.ToString("x2", CultureInfo.InvariantCulture) + "." +
                             mdcompat.ToString("x2", CultureInfo.InvariantCulture);
            codec = @string;
        }

        /*----------------------------------------------------------------------
        |   AP4_Dac4Atom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("ac4_dsi_version",    _dsi.ac4_dsi_version);
            if (_dsi.ac4_dsi_version == 0) {
                inspector.AddField("bitstream_version",  _dsi.d.v0.bitstream_version);
                inspector.AddField("fs_index",           _dsi.d.v0.fs_index);
                inspector.AddField("fs",                 _dsi.d.v0.fs);
                inspector.AddField("frame_rate_index",   _dsi.d.v0.frame_rate_index);
            } else if (_dsi.ac4_dsi_version == 1) {
                inspector.AddField("bitstream_version",  _dsi.d.v1.bitstream_version);
                inspector.AddField("fs_index",           _dsi.d.v1.fs_index);
                inspector.AddField("fs",                 _dsi.d.v1.fs);
                inspector.AddField("frame_rate_index",   _dsi.d.v1.frame_rate_index);
                if (_dsi.d.v1.bitstream_version > 1) {
                    inspector.AddField("short_program_id", _dsi.d.v1.short_program_id);
                    inspector.AddField("program_uuid",     _dsi.d.v1.program_uuid, 16, AtomInspector.FormatHint.HINT_HEX);
                }
                inspector.AddField("bit_rate_mode",      _dsi.d.v1.ac4_bitrate_dsi.bit_rate_mode);
                inspector.AddField("bit_rate",           _dsi.d.v1.ac4_bitrate_dsi.bit_rate);
                inspector.AddField("bit_rate_precision", _dsi.d.v1.ac4_bitrate_dsi.bit_rate_precision);

                for (uint i = 0; i < _dsi.d.v1.n_presentations; i++) {
                    Ac4Dsi.PresentationV1 presentation = _dsi.d.v1.presentations[i];
                    // "[%02d]"
                    string prefix = "[" + ((int)i).ToString("D2", CultureInfo.InvariantCulture) + "]";
                    string field_name;
                    field_name = prefix + ".presentation_version";
                    inspector.AddField(field_name, presentation.presentation_version);
                    if (presentation.presentation_version == 0) {
                        field_name = prefix + ".presentation_config";
                        inspector.AddField(field_name, presentation.d.v0.presentation_config);
                        field_name = prefix + ".mdcompat";
                        inspector.AddField(field_name, presentation.d.v0.mdcompat);
                        field_name = prefix + ".presentation_id";
                        inspector.AddField(field_name, presentation.d.v0.presentation_id);
                        field_name = prefix + ".dsi_frame_rate_multiply_info";
                        inspector.AddField(field_name, presentation.d.v0.dsi_frame_rate_multiply_info);
                        field_name = prefix + ".presentation_emdf_version";
                        inspector.AddField(field_name, presentation.d.v0.presentation_emdf_version);
                        field_name = prefix + ".presentation_key_id";
                        inspector.AddField(field_name, presentation.d.v0.presentation_key_id);
                        field_name = prefix + ".presentation_channel_mask";
                        inspector.AddField(field_name, presentation.d.v0.presentation_channel_mask, AtomInspector.FormatHint.HINT_HEX);
                    } else if (presentation.presentation_version == 1 || presentation.presentation_version == 2) {
                        field_name = prefix + ".presentation_config_v1";
                        inspector.AddField(field_name, presentation.d.v1.presentation_config_v1);
                        field_name = prefix + ".mdcompat";
                        inspector.AddField(field_name, presentation.d.v1.mdcompat);
                        field_name = prefix + ".presentation_id";
                        inspector.AddField(field_name, presentation.d.v1.presentation_id);
                        field_name = prefix + ".dsi_frame_rate_multiply_info";
                        inspector.AddField(field_name, presentation.d.v1.dsi_frame_rate_multiply_info);
                        field_name = prefix + ".dsi_frame_rate_fraction_info";
                        inspector.AddField(field_name, presentation.d.v1.dsi_frame_rate_fraction_info);
                        field_name = prefix + ".presentation_emdf_version";
                        inspector.AddField(field_name, presentation.d.v1.presentation_emdf_version);
                        field_name = prefix + ".presentation_key_id";
                        inspector.AddField(field_name, presentation.d.v1.presentation_key_id);
                        field_name = prefix + ".b_presentation_channel_coded";
                        inspector.AddField(field_name, presentation.d.v1.b_presentation_channel_coded);
                        field_name = prefix + ".dsi_presentation_ch_mode";
                        inspector.AddField(field_name, presentation.d.v1.dsi_presentation_ch_mode);
                        field_name = prefix + ".pres_b_4_back_channels_present";
                        inspector.AddField(field_name, presentation.d.v1.pres_b_4_back_channels_present);
                        field_name = prefix + ".pres_top_channel_pairs";
                        inspector.AddField(field_name, presentation.d.v1.pres_top_channel_pairs);
                        field_name = prefix + ".presentation_channel_mask_v1";
                        inspector.AddField(field_name, presentation.d.v1.presentation_channel_mask_v1, AtomInspector.FormatHint.HINT_HEX);
                    }
                }
            }
            return Ap4Result.SUCCESS;
        }

        public override Atom Clone() { return new Dac4Atom(_size32, _rawBytes.GetData()); }
        public virtual Dac4Atom CloneConst() { return new Dac4Atom(_size32, _rawBytes.GetData()); }

        // accessors
        public DataBuffer GetRawBytes() { return _rawBytes;   }
        public Ac4Dsi     GetDsi()      { return _dsi;        }

        // members
        private DataBuffer _rawBytes = new DataBuffer();
        private Ac4Dsi     _dsi;
    }
}
