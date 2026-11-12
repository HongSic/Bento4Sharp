// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Ac4Utils.h, Ap4Ac4Utils.cpp
// Original work Copyright 2002-2020 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint CH_MODE_LENGTH           = 16;   /* AC-4 ch_mode length  */
        public const uint AC4_FRAME_RATE_NUM       = 14;   /* AC-4 frame rate number  */
        public const uint AC4_SUBSTREAM_GROUP_NUM  = 3;    /* AC-4 Maximum substream group if presentation_config < 5 */
    }

    /*----------------------------------------------------------------------
    |   AP4_Ac4EmdfInfo
    +---------------------------------------------------------------------*/
    // PORT NOTE: a class (it holds arrays); upstream leaves a freshly declared
    // AP4_Ac4EmdfInfo uninitialized, the port zero-initializes it.
    public class Ac4EmdfInfo
    {
        public byte   emdf_version;
        public ushort key_id;
        public byte   b_emdf_payloads_substream_info;
        public ushort substream_index;
        public byte   protectionLengthPrimary;
        public byte   protectionLengthSecondary;
        public byte[] protection_bits_primary = new byte[16];
        public byte[] protection_bits_Secondary = new byte[16];
    }

    /*----------------------------------------------------------------------
    |   Ac4Utils
    +---------------------------------------------------------------------*/
    // PORT NOTE: holds the tables, the constants that have no AP4_ prefix
    // upstream, and the free functions of Ap4Ac4Utils.h/.cpp (function names
    // minus their AP4_/Ap4_ prefix).
    public static class Ac4Utils
    {
        public static readonly byte[,] SUPER_SET_CH_MODE = new byte[(int)Ap4.CH_MODE_LENGTH, (int)Ap4.CH_MODE_LENGTH]
        {
            {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,11,12,13,14,15},
            {1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,11,12,13,14,15},
            {2, 2, 2, 3, 4, 5, 6, 7, 8, 9, 10,11,12,13,14,15},
            {3, 3, 3, 3, 4, 5, 6, 7, 8, 9, 10,11,12,13,14,15},
            {4, 4, 4, 4, 4, 6, 6, 8, 8, 10,10,12,12,14,14,15},
            {5, 5, 5, 5, 6, 5, 6, 7, 8, 9, 10,11,12,13,14,15},
            {6, 6, 6, 6, 6, 6, 6, 6, 8, 6, 10,12,12,14,14,15},
            {7, 7, 7, 7, 8, 7, 6, 7, 8, 9, 10,12,12,13,14,15},
            {8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 10,11,12,14,14,15},
            {9, 9, 9, 9, 10,9, 10,9, 9, 9, 10,11,12,13,14,15},
            {10,10,10,10,10,10,10,10,10,10,10,10,12,13,14,15},
            {11,11,11,11,12,11,12,11,12,11,12,11,13,13,14,15},
            {12,12,12,12,12,12,12,12,12,12,12,12,12,13,14,15},
            {13,13,13,13,14,13,14,13,14,13,14,13,14,13,14,15},
            {14,14,14,14,14,14,14,14,14,14,14,14,14,14,14,15},
            {15,15,15,15,15,15,15,15,15,15,15,15,15,15,15,15}
        };

        public const int CH_MODE_MONO      = 0;
        public const int CH_MODE_STEREO    = 1;
        public const int CH_MODE_3_0       = 2;
        public const int CH_MODE_5_0       = 3;
        public const int CH_MODE_5_1       = 4;
        public const int CH_MODE_70_34     = 5;
        public const int CH_MODE_71_34     = 6;
        public const int CH_MODE_70_52     = 7;
        public const int CH_MODE_71_52     = 8;
        public const int CH_MODE_70_322    = 9;
        public const int CH_MODE_71_322    = 10;
        public const int CH_MODE_7_0_4     = 11;
        public const int CH_MODE_7_1_4     = 12;
        public const int CH_MODE_9_0_4     = 13;
        public const int CH_MODE_9_1_4     = 14;
        public const int CH_MODE_22_2      = 15;
        public const int CH_MODE_RESERVED  = 16;

        public static readonly int[] AC4_SPEAKER_GROUP_INDEX_MASK_BY_CH_MODE =
        {
            2,        // 0b10 - 1.0
            1,        // 0b01 - 2.0
            3,        // 0b11 - 3.0
            7,        // 0b0000111 - 5.0
            71,       // 0b1000111 - 5.1
            15,       // 0b0001111 - 7.0: 3/4/0
            79,       // 0b1001111 - 7.1: 3/4/0.1
            131079,   // 0b100000000000000111 - 7.0: 5/2/0
            131143,   // 0b100000000001000111 - 7.1: 5/2/0.1
            262151,   // 0b1000000000000000111 - 7.0: 3/2/2
            262215,   // 0b1000000000001000111 - 7.1: 3/2/2.1
            63,       // 0b0111111 - 7.0.4
            127,      // 0b1111111 - 7.1.4
            65599,    // 0b10000000000111111 - 9.0.4
            65663,    // 0b10000000001111111 - 9.1.4
            196479,   // 0b101111111101111111 - 22.2
            0         // reserved
        };

        public static readonly uint[] Ac4SamplingFrequencyTable =
        {
            44100,  // 44.1 kHz
            48000   // 48 kHz
        };

        public static readonly uint[] Ac4SampleDeltaTable = new uint[(int)Ap4.AC4_FRAME_RATE_NUM]
        {
            2002,
            2000,
            1920,
            8008,  // 29.97 fps, using 240 000 media time scale
            1600,
            1001,
            1000,
            960,
            4004,  // 59.97 fps
            800,
            480,
            2002,  // 119.88 fps
            400,
            2048  // 23.44 fps, AC-4 native frame rate
        };

        public static readonly uint[] Ac4MediaTimeScaleTable = new uint[(int)Ap4.AC4_FRAME_RATE_NUM]
        {
            48000,
            48000,
            48000,
            240000,  //29.97 fps
            48000,
            48000,
            48000,
            48000,
            240000,  // 59.97 fps
            48000,
            48000,
            240000,  // 119.88 fps
            48000,
            48000,
        };

        /*----------------------------------------------------------------------
        |   AP4_Ac4VariableBits
        +---------------------------------------------------------------------*/
        public static uint Ac4VariableBits(BitReader data, int nBits)
        {
            uint value = 0;
            uint b_moreBits;
            do{
                value += data.ReadBits((uint)nBits);
                b_moreBits = (uint)data.ReadBit();
                if (b_moreBits == 1) {
                    value <<= nBits;
                    value += (uint)(1<<nBits);
              }
            } while (b_moreBits == 1);
            return value;
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac4ChannelCountFromSpeakerGroupIndexMask
        +---------------------------------------------------------------------*/
        // PORT NOTE: declared as returning AP4_Result (an int) upstream; the value
        // is a channel count.
        public static int Ac4ChannelCountFromSpeakerGroupIndexMask(uint speakerGroupIndexMask)
        {
            uint channelCount= 0;
            if ((speakerGroupIndexMask & 1) != 0) { // 0: L,R 0b1
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 2) != 0) { // 1: C 0b10
                channelCount += 1;
            }
            if ((speakerGroupIndexMask & 4) != 0) { // 2: Ls,Rs 0b100
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 8) != 0) { // 3: Lb,Rb 0b1000
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 16) != 0) { // 4: Tfl,Tfr 0b10000
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 32) != 0) { // 5: Tbl,Tbr 0b100000
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 64) != 0) { // 6: LFE 0b1000000
                channelCount += 1;
            }
            if ((speakerGroupIndexMask & 128) != 0) { // 7: TL,TR 0b10000000
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 256) != 0) { // 8: Tsl,Tsr 0b100000000
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 512) != 0) { // 9: Tfc
                channelCount += 1;
            }
            if ((speakerGroupIndexMask & 1024) != 0) { // 10: Tbc
                channelCount += 1;
            }
            if ((speakerGroupIndexMask & 2048) != 0) { // 11: Tc
                channelCount += 1;
            }
            if ((speakerGroupIndexMask & 4096) != 0) { // 12: LFE2
                channelCount += 1;
            }
            if ((speakerGroupIndexMask & 8192) != 0) { // 13: Bfl,Bfr
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 16384) != 0) { // 14: Bfc
                channelCount += 1;
            }
            if ((speakerGroupIndexMask & 32768) != 0) { // 15: Cb
                channelCount += 1;
            }
            if ((speakerGroupIndexMask & 65536) != 0) { // 16: Lscr,Rscr
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 131072) != 0) { // 17: Lw,Rw
                channelCount += 2;
            }
            if ((speakerGroupIndexMask & 262144) != 0) { // 18: Vhl,Vhr
                channelCount += 2;
            }
            return (int)channelCount;
        }

        /*----------------------------------------------------------------------
        |   AP4_Ac4SuperSet
        +---------------------------------------------------------------------*/
        // PORT NOTE: declared as returning AP4_Result (an int) upstream; the value
        // is a ch_mode (or -1).
        public static int Ac4SuperSet(int lvalue, int rvalue)
        {
            if ((lvalue == -1) || (lvalue > 15)) return rvalue;
            if ((rvalue == -1) || (rvalue > 15)) return lvalue;
            return SUPER_SET_CH_MODE[lvalue, rvalue];
        }

        /*----------------------------------------------------------------------
        |   Ap4_Ac4SubstreamGroupPartOfDefaultPresentation
        +---------------------------------------------------------------------*/
        // PORT NOTE: declared as returning AP4_Result (an int) upstream; the value
        // is 0 or 1.
        public static int Ac4SubstreamGroupPartOfDefaultPresentation(uint substreamGroupIndex, uint[] presSubstreamGroupIndexes, uint presNSubstreamGroups)
        {
            int partOf = 0;
            for (uint idx = 0; idx < presNSubstreamGroups; idx ++) {
                if (presSubstreamGroupIndexes[idx] == substreamGroupIndex) { partOf = 1; }
            }
            return partOf;
        }

        /*----------------------------------------------------------------------
        |   AP4_SetMaxGroupIndex
        +---------------------------------------------------------------------*/
        public static uint SetMaxGroupIndex(uint groupIndex, uint maxGroupIndex)
        {
            if (groupIndex > maxGroupIndex) { maxGroupIndex = groupIndex; }
            return maxGroupIndex;
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteAlign
        +---------------------------------------------------------------------*/
        public static void ByteAlign(BitWriter bits)
        {
            uint byte_align_nums = 8 - (bits.GetBitCount() % 8 == 0 ? 8: bits.GetBitCount() % 8);
            if (byte_align_nums != 0){ bits.Write(0, byte_align_nums); }
        }

        /*----------------------------------------------------------------------
        |   Ap4_Ac4UpdatePresBytes
        +---------------------------------------------------------------------*/
        public static void Ac4UpdatePresBytes(byte[] buffer, uint idx, byte value)
        {
            buffer[idx] = value;
        }
    }
}
