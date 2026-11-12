// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4DvccAtom.h, Ap4DvccAtom.cpp
// Original work Copyright 2002-2016 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte DV_PROFILE_DVAV_PER = 0;
        public const byte DV_PROFILE_DVAV_PEN = 1;
        public const byte DV_PROFILE_DVHE_DER = 2;
        public const byte DV_PROFILE_DVHE_DEN = 3;
        public const byte DV_PROFILE_DVHE_DTR = 4;
        public const byte DV_PROFILE_DVHE_STN = 5;
        public const byte DV_PROFILE_DVHE_DTH = 6;
        public const byte DV_PROFILE_DVHE_DTB = 7;
        public const byte DV_PROFILE_DVHE_ST  = 8;
        public const byte DV_PROFILE_DVHE_SE  = 9;
    }

    /*----------------------------------------------------------------------
    |   AP4_DvccAtom
    +---------------------------------------------------------------------*/
    public class DvccAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_DvccAtom::GetProfileName
        +---------------------------------------------------------------------*/
        public static string GetProfileName(byte profile)
        {
            switch (profile) {
                case Ap4.DV_PROFILE_DVAV_PER: return "dvav.per";
                case Ap4.DV_PROFILE_DVAV_PEN: return "dvav.pen";
                case Ap4.DV_PROFILE_DVHE_DER: return "dvhe.der";
                case Ap4.DV_PROFILE_DVHE_DEN: return "dvhe.den";
                case Ap4.DV_PROFILE_DVHE_DTR: return "dvhe.dtr";
                case Ap4.DV_PROFILE_DVHE_STN: return "dvhe.stn";
                case Ap4.DV_PROFILE_DVHE_DTH: return "dvhe.dth";
                case Ap4.DV_PROFILE_DVHE_DTB: return "dvhr.dtb";
                case Ap4.DV_PROFILE_DVHE_ST : return "dvhe.st";
                case Ap4.DV_PROFILE_DVHE_SE : return "dvav.se";
            }

            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_DvccAtom::Create
        +---------------------------------------------------------------------*/
        public static DvccAtom Create(uint size, ByteStream stream)
        {
            if (size < Ap4.ATOM_HEADER_SIZE+24) return null;

            byte[] payload = new byte[24];
            Ap4Result result = stream.Read(payload, 24);
            if (result.Failed()) return null;

            return new DvccAtom(payload[0],
                                payload[1],
                                (byte)(payload[2]>>1),
                                (byte)(((payload[2]&1)<<5) | (payload[3]>>3)),
                                (payload[3]&4) != 0,
                                (payload[3]&2) != 0,
                                (payload[3]&1) != 0,
                                (byte)(payload[4]>>4));
        }

        /*----------------------------------------------------------------------
        |   AP4_DvccAtom::AP4_DvccAtom
        +---------------------------------------------------------------------*/
        public DvccAtom() :
            base(AtomType.DVCC, Ap4.ATOM_HEADER_SIZE+24)
        {
            _dvVersionMajor = 0;
            _dvVersionMinor = 0;
            _dvProfile = 0;
            _dvLevel = 0;
            _rpuPresentFlag = 0;
            _elPresentFlag = 0;
            _blPresentFlag = 0;
            _dvBlSignalCompatibilityID = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_DvccAtom::AP4_DvccAtom
        +---------------------------------------------------------------------*/
        public DvccAtom(byte dv_version_major,
                        byte dv_version_minor,
                        byte dv_profile,
                        byte dv_level,
                        bool rpu_present_flag,
                        bool el_present_flag,
                        bool bl_present_flag,
                        byte dv_bl_signal_compatibility_id) :
            base((dv_profile>7) ? AtomType.DVVC : AtomType.DVCC, Ap4.ATOM_HEADER_SIZE+24)
        {
            _dvVersionMajor = dv_version_major;
            _dvVersionMinor = dv_version_minor;
            _dvProfile = dv_profile;
            _dvLevel = dv_level;
            _rpuPresentFlag = (byte)(rpu_present_flag ? 1 : 0);
            _elPresentFlag = (byte)(el_present_flag ? 1 : 0);
            _blPresentFlag = (byte)(bl_present_flag ? 1 : 0);
            _dvBlSignalCompatibilityID = dv_bl_signal_compatibility_id;
        }

        /*----------------------------------------------------------------------
        |   AP4_DvccAtom::GetCodecString
        +---------------------------------------------------------------------*/
        public Ap4Result GetCodecString(string  parent_codec_string,
                                        uint    parent_format,
                                        out string codec)
        {
            string workspace;

            if (parent_format == AtomType.DVAV ||
                parent_format == AtomType.DVA1 ||
                parent_format == AtomType.DVHE ||
                parent_format == AtomType.DVH1) {
                /* Non backward-compatible */
                string coding = Ap4Utils.FormatFourChars(parent_format);
                // "%s.%02d.%02d"
                workspace = coding + "." +
                            GetDvProfile().ToString("D2", CultureInfo.InvariantCulture) + "." +
                            GetDvLevel().ToString("D2", CultureInfo.InvariantCulture);
                codec = Truncate(workspace);
            } else {
                /* Backward-compatible */
                uint format = parent_format;
                switch (parent_format) {
                  case AtomType.AVC1:
                    format = AtomType.DVA1;
                    break;

                  case AtomType.AVC3:
                    format = AtomType.DVAV;
                    break;

                  case AtomType.HEV1:
                    format = AtomType.DVHE;
                    break;

                  case AtomType.HVC1:
                    format = AtomType.DVH1;
                    break;
                }
                string coding = Ap4Utils.FormatFourChars(format);
                // "%s,%s.%02d.%02d"
                workspace = parent_codec_string + "," + coding + "." +
                            GetDvProfile().ToString("D2", CultureInfo.InvariantCulture) + "." +
                            GetDvLevel().ToString("D2", CultureInfo.InvariantCulture);
                codec = Truncate(workspace);
            }

            return Ap4Result.SUCCESS;
        }

        // PORT NOTE: upstream formats into a 64-byte buffer with AP4_FormatString
        // (vsnprintf), which truncates the result to 63 bytes.
        private static string Truncate(string workspace)
        {
            byte[] bytes = Ap4Utils.StringToBytes(workspace);
            if (bytes.Length <= 63) return workspace;
            return Ap4Utils.StringFromBytes(bytes, 0, 63);
        }

        /*----------------------------------------------------------------------
        |   AP4_DvccAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            byte[] payload = new byte[24];
            payload[0] = _dvVersionMajor;
            payload[1] = _dvVersionMinor;
            payload[2] = (byte)((_dvProfile<<1) | ((_dvLevel&0x20)>>5));
            payload[3] = (byte)((_dvLevel<<3) | (_rpuPresentFlag != 0?4:0) | (_elPresentFlag != 0?2:0) | (_blPresentFlag != 0?1:0));
            payload[4] = (byte)(_dvBlSignalCompatibilityID<<4);

            return stream.Write(payload, 24);
        }

        /*----------------------------------------------------------------------
        |   AP4_DvccAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("dv_version_major", _dvVersionMajor);
            inspector.AddField("dv_version_minor", _dvVersionMinor);
            inspector.AddField("dv_profile", _dvProfile);
            string profile_name = GetProfileName(_dvProfile);
            if (profile_name != null) {
                inspector.AddField("dv_profile_name", profile_name);
            } else {
                inspector.AddField("dv_profile_name", "unknown");
            }
            inspector.AddField("dv_level", _dvLevel);
            inspector.AddField("rpu_present_flag", _rpuPresentFlag);
            inspector.AddField("el_present_flag",  _elPresentFlag);
            inspector.AddField("bl_present_flag",  _blPresentFlag);
            inspector.AddField("dv_bl_signal_compatibility_id", _dvBlSignalCompatibilityID);
            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte GetDvVersionMajor() { return _dvVersionMajor;      }
        public byte GetDvVersionMinor() { return _dvVersionMinor;      }
        public byte GetDvProfile()      { return _dvProfile;           }
        public byte GetDvLevel()        { return _dvLevel;             }
        public bool GetRpuPresentFlag() { return _rpuPresentFlag != 0; }
        public bool GetElPresentFlag()  { return _elPresentFlag  != 0; }
        public bool GetBlPresentFlag()  { return _blPresentFlag  != 0; }
        public byte GetDvBlSignalCompatibilityID() { return _dvBlSignalCompatibilityID; }

        // members
        private byte _dvVersionMajor;
        private byte _dvVersionMinor;
        private byte _dvProfile;
        private byte _dvLevel;
        private byte _rpuPresentFlag;
        private byte _elPresentFlag;
        private byte _blPresentFlag;
        private byte _dvBlSignalCompatibilityID;
    }
}
