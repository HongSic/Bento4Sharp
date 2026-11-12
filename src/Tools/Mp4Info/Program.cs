// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Apps/Mp4Info/Mp4Info.cpp
// Original work Copyright 2002-2015 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.
//
// PORT NOTES:
// - printf() output goes through one shared "-stdout" FileByteStream so that
//   the bytes written are exactly those of upstream ('\n' line endings, no
//   encoding translation).
// - printf("%s", NULL) prints "(null)" with the C runtimes upstream is built
//   with; the port does the same where a string may be null.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Bento4;

namespace Bento4Sharp.Tools.Mp4Info
{

    public static class Program
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const string BANNER = "MP4 File Info - Version 1.3.4\n" +
                                      "(Bento4 Version " + Ap4.VERSION_STRING + ")\n" +
                                      "(c) 2002-2017 Axiomatic Systems, LLC";


        /*----------------------------------------------------------------------
        |   globals
        +---------------------------------------------------------------------*/
        private struct MediaInfo {
            public ulong  sample_count;
            public ulong  duration;
            public double bitrate;
        }

        private enum OutputFormat {
            TEXT_FORMAT,
            JSON_FORMAT
        }

        private static class Options {
            public static OutputFormat format;
        }

        /*----------------------------------------------------------------------
        |   stdio helpers (PORT NOTE: printf / fprintf(stderr) replacements)
        +---------------------------------------------------------------------*/
        private static ByteStream s_Stdout;

        private static ByteStream Stdout()
        {
            if (s_Stdout == null) {
                FileByteStream.Create("-stdout", FileByteStream.Mode.STREAM_MODE_WRITE, out s_Stdout);
            }
            return s_Stdout;
        }

        private static void Printf(string text)
        {
            Stdout().WriteString(text);
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

        private static string S(string value) { return value ?? "(null)"; }                                           // %s
        private static string D(int value)     { return value.ToString(CultureInfo.InvariantCulture); }                // %d
        private static string D(uint value)    { return unchecked((int)value).ToString(CultureInfo.InvariantCulture); } // %d of an unsigned value
        private static string LLD(ulong value) { return unchecked((long)value).ToString(CultureInfo.InvariantCulture); } // %lld of an unsigned value
        private static string LLD(long value)  { return value.ToString(CultureInfo.InvariantCulture); }                // %lld
        private static string X(uint value)    { return value.ToString("x", CultureInfo.InvariantCulture); }           // %x
        private static string F(double value)  { return Ap4Utils.FormatFloat(value); }                                 // %f

        // %.3f
        private static string F3(double value)
        {
            if (double.IsNaN(value)) return "nan";
            if (double.IsPositiveInfinity(value)) return "inf";
            if (double.IsNegativeInfinity(value)) return "-inf";
#if NET6_0_OR_GREATER
            return value.ToString("F3", CultureInfo.InvariantCulture);
#else
            return Compat.FormatFixed(value, 3);
#endif
        }

        // %<width>d / %<width>lld
        private static string Pad(string digits, int width)
        {
            return digits.PadLeft(width, ' ');
        }

        // %0<width>d / %0<width>lld
        private static string Pad0(string digits, int width)
        {
            if (digits.Length > 0 && digits[0] == '-') {
                return "-" + digits.Substring(1).PadLeft(width-1, '0');
            }
            return digits.PadLeft(width, '0');
        }

        /*----------------------------------------------------------------------
        |   PrintUsageAndExit
        +---------------------------------------------------------------------*/
        private static int PrintUsageAndExit()
        {
            Eprintf(BANNER +
                    "\n\nusage: mp4info [options] <input>\n" +
                    "Options:\n" +
                    "  --verbose:          show extended information when available\n" +
                    "  --format <format>:  display the information in this format.\n" +
                    "                      <format> is: text (default) or json\n" +
                    "  --show-layout:      show sample layout\n" +
                    "  --show-samples:     show sample details\n" +
                    "  --show-sample-data: show sample data\n" +
                    "  --fast:             skip some details that are slow to compute\n");
            return 1; // PORT NOTE: exit(1) upstream; the caller returns this value from Main
        }

        /*----------------------------------------------------------------------
        |   ShowPayload
        +---------------------------------------------------------------------*/
        private static void ShowPayload(Atom atom, bool ascii = false)
        {
            ulong payload_size = atom.GetSize()-8;
            if (payload_size <= 1024) {
                MemoryByteStream payload = new MemoryByteStream();
                atom.Write(payload);
                if (ascii) {
                    // ascii
                    payload.WriteUI08(0); // terminate with a NULL character
                    uint header_size = atom.GetHeaderSize();
                    if (header_size <= payload.GetDataSize()) {
                        Printf(Ap4Utils.StringFromCString(payload.GetData(), (int)header_size, payload.GetDataSize()-header_size));
                    }
                } else {
                    // hex
                    StringBuilder hex = new StringBuilder();
                    for (uint i=0; i<payload_size; i++) {
                        // PORT NOTE: upstream reads past the end of the buffer when the
                        // atom serializes to fewer bytes than its size; the port stops there.
                        if (atom.GetHeaderSize()+i >= payload.GetDataSize()) break;
                        hex.Append(payload.GetData()[atom.GetHeaderSize()+i].ToString("x2", CultureInfo.InvariantCulture));
                    }
                    Printf(hex.ToString());
                }
            }
        }

        /*----------------------------------------------------------------------
        |   ShowData
        +---------------------------------------------------------------------*/
        private static void ShowData(DataBuffer data)
        {
            StringBuilder hex = new StringBuilder();
            for (uint i=0; i<data.GetDataSize(); i++) {
                hex.Append(data.GetData()[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            Printf(hex.ToString());
        }

        /*----------------------------------------------------------------------
        |   FourCC (PORT NOTE: printf("%c%c%c%c") of the four bytes of a 32-bit value)
        +---------------------------------------------------------------------*/
        private static void PrintFourCC(uint value)
        {
            byte[] chars = new byte[4] {
                (byte)((value>>24) & 0xFF),
                (byte)((value>>16) & 0xFF),
                (byte)((value>> 8) & 0xFF),
                (byte)((value    ) & 0xFF)
            };
            Stdout().Write(chars, 4);
        }

        /*----------------------------------------------------------------------
        |   ShowProtectionSchemeInfo_Text
        +---------------------------------------------------------------------*/
        private static void ShowProtectionSchemeInfo_Text(uint scheme_type, ContainerAtom schi, bool verbose)
        {
            if (scheme_type == Ap4.PROTECTION_SCHEME_TYPE_IAEC) {
                Printf("      iAEC Scheme Info:\n");
                IkmsAtom ikms = schi.FindChild("iKMS") as IkmsAtom;
                if (ikms != null) {
                    Printf("        KMS URI:              " + S(ikms.GetKmsUri()) + "\n");
                }
                IsfmAtom isfm = schi.FindChild("iSFM") as IsfmAtom;
                if (isfm != null) {
                    Printf("        Selective Encryption: " + (isfm.GetSelectiveEncryption()?"yes":"no") + "\n");
                    Printf("        Key Indicator Length: " + D(isfm.GetKeyIndicatorLength()) + "\n");
                    Printf("        IV Length:            " + D(isfm.GetIvLength()) + "\n");
                }
                IsltAtom islt = schi.FindChild("iSLT") as IsltAtom;
                if (islt != null) {
                    Printf("        Salt:                 ");
                    for (uint i=0; i<8; i++) {
                        Printf(islt.GetSalt()[i].ToString("x2", CultureInfo.InvariantCulture));
                    }
                    Printf("\n");
                }
            } else if (scheme_type == Ap4.PROTECTION_SCHEME_TYPE_OMA) {
                Printf("      odkm Scheme Info:\n");
                OdafAtom odaf = schi.FindChild("odkm/odaf") as OdafAtom;
                if (odaf != null) {
                    Printf("        Selective Encryption: " + (odaf.GetSelectiveEncryption()?"yes":"no") + "\n");
                    Printf("        Key Indicator Length: " + D(odaf.GetKeyIndicatorLength()) + "\n");
                    Printf("        IV Length:            " + D(odaf.GetIvLength()) + "\n");
                }
                OhdrAtom ohdr = schi.FindChild("odkm/ohdr") as OhdrAtom;
                if (ohdr != null) {
                    string encryption_method = "";
                    switch (ohdr.GetEncryptionMethod()) {
                        case Ap4.OMA_DCF_ENCRYPTION_METHOD_NULL:    encryption_method = "NULL";    break;
                        case Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CTR: encryption_method = "AES-CTR"; break;
                        case Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CBC: encryption_method = "AES-CBC"; break;
                        default:                                    encryption_method = "UNKNOWN"; break;
                    }
                    Printf("        Encryption Method: " + encryption_method + "\n");
                    Printf("        Content ID:        " + S(ohdr.GetContentId()) + "\n");
                    Printf("        Rights Issuer URL: " + S(ohdr.GetRightsIssuerUrl()) + "\n");

                    DataBuffer headers  = ohdr.GetTextualHeaders();
                    uint       data_len = headers.GetDataSize();
                    if (data_len != 0) {
                        DataBuffer output_buffer = new DataBuffer();
                        output_buffer.SetDataSize(data_len+1);
                        Array.Copy(headers.GetData(), 0, output_buffer.UseData(), 0, (int)data_len);
                        byte[] textual_headers_string = output_buffer.UseData();
                        textual_headers_string[data_len] = 0;
                        for (uint curr = 0; curr < data_len; curr++) {
                            if (textual_headers_string[curr] == 0) {
                                textual_headers_string[curr] = (byte)'\n';
                            }
                        }
                        Printf("        Textual Headers: \n" + Ap4Utils.StringFromCString(textual_headers_string, 0, data_len+1) + "\n");
                    }
                }
            } else if (scheme_type == Ap4.PROTECTION_SCHEME_TYPE_ITUNES) {
                Printf("      itun Scheme Info:\n");
                Atom name = schi.FindChild("name");
                if (name != null) {
                    Printf("        Name:    ");
                    ShowPayload(name, true);
                    Printf("\n");
                }
                Atom user = schi.FindChild("user");
                if (user != null) {
                    Printf("        User ID: ");
                    ShowPayload(user);
                    Printf("\n");
                }
                Atom key = schi.FindChild("key ");
                if (key != null) {
                    Printf("        Key ID:  ");
                    ShowPayload(key);
                    Printf("\n");
                }
                Atom iviv = schi.FindChild("iviv");
                if (iviv != null) {
                    Printf("        IV:      ");
                    ShowPayload(iviv);
                    Printf("\n");
                }
            } else if (scheme_type == Ap4.PROTECTION_SCHEME_TYPE_MARLIN_ACBC ||
                       scheme_type == Ap4.PROTECTION_SCHEME_TYPE_MARLIN_ACGK) {
                Printf("      Marlin IPMP ACBC/ACGK Scheme Info:\n");
                NullTerminatedStringAtom  octopus_id = schi.FindChild("8id ") as NullTerminatedStringAtom;
                if (octopus_id != null) {
                    Printf("        Content ID: " + S(octopus_id.GetValue()) + "\n");
                }
            }

            if (verbose) {
                Printf("    Protection System Details:\n");
                // PORT NOTE: upstream opens another "-stdout" stream, which shares the
                // C runtime's stdout with printf(); the port shares one stream object.
                PrintInspector inspector = new PrintInspector(Stdout(), 4);
                schi.Inspect(inspector);
            }
        }

        /*----------------------------------------------------------------------
        |   ShowProtectionSchemeInfo_Json
        +---------------------------------------------------------------------*/
        private static void ShowProtectionSchemeInfo_Json(uint scheme_type, ContainerAtom schi, bool verbose)
        {
            // not implemented yet
        }

        /*----------------------------------------------------------------------
        |   ShowProtectedSampleDescription_Text
        +---------------------------------------------------------------------*/
        private static void ShowProtectedSampleDescription_Text(ProtectedSampleDescription desc, bool verbose)
        {
            Printf("    [ENCRYPTED]\n");
            string coding = Ap4Utils.FormatFourChars(desc.GetFormat());
            Printf("      Coding:         " + coding + "\n");
            uint st = desc.GetSchemeType();
            Printf("      Scheme Type:    ");
            PrintFourCC(st);
            Printf("\n");
            Printf("      Scheme Version: " + D(desc.GetSchemeVersion()) + "\n");
            Printf("      Scheme URI:     " + S(desc.GetSchemeUri()) + "\n");
            ProtectionSchemeInfo scheme_info = desc.GetSchemeInfo();
            if (scheme_info == null) return;
            ContainerAtom schi = scheme_info.GetSchiAtom();
            if (schi == null) return;
            ShowProtectionSchemeInfo_Text(desc.GetSchemeType(), schi, verbose);
        }

        /*----------------------------------------------------------------------
        |   ShowProtectedSampleDescription_Json
        +---------------------------------------------------------------------*/
        private static void ShowProtectedSampleDescription_Json(ProtectedSampleDescription desc, bool verbose)
        {
            Printf("\"protection\":{\n");
            string coding = Ap4Utils.FormatFourChars(desc.GetFormat());
            Printf("\"coding\":\"" + coding + "\",\n");
            uint st = desc.GetSchemeType();
            Printf("\"scheme_type\":\"");
            PrintFourCC(st);
            Printf("\",\n");
            Printf("\"scheme_version\":" + D(desc.GetSchemeVersion()) + ",\n");
            Printf("\"scheme_uri\":\"" + S(desc.GetSchemeUri()) + "\"");
            ProtectionSchemeInfo scheme_info = desc.GetSchemeInfo();
            if (scheme_info != null) {
                ContainerAtom  schi = scheme_info.GetSchiAtom();
                if (schi != null) {
                    ShowProtectionSchemeInfo_Json(desc.GetSchemeType(), schi, verbose);
                }
            }
            Printf("}");
        }

        /*----------------------------------------------------------------------
        |   ShowMpegAudioSampleDescription
        +---------------------------------------------------------------------*/
        private static void ShowMpegAudioSampleDescription(MpegAudioSampleDescription mpeg_audio_desc)
        {
            byte object_type = mpeg_audio_desc.GetMpeg4AudioObjectType();
            string object_type_string = MpegAudioSampleDescription.GetMpeg4AudioObjectTypeString(object_type);

            switch (Options.format) {
                case OutputFormat.TEXT_FORMAT:
                    Printf("    MPEG-4 Audio Object Type: " + D((int)object_type) + " (" + S(object_type_string) + ")\n");
                    break;

                case OutputFormat.JSON_FORMAT:
                    Printf("\"mpeg_4_audio_object_type\":" + D((int)object_type) + ",\n");
                    Printf("\"mpeg_4_audio_object_type_name\":\"" + S(object_type_string) + "\"");
                    break;
            }

            // Decoder Specific Info
            DataBuffer dsi = mpeg_audio_desc.GetDecoderInfo();
            if (dsi.GetDataSize() != 0) {
                Mp4AudioDecoderConfig dec_config = new Mp4AudioDecoderConfig();
                Ap4Result result = dec_config.Parse(dsi.GetData(), dsi.GetDataSize());
                if (result.Succeeded()) {
                    switch (Options.format) {
                        case OutputFormat.TEXT_FORMAT:
                            Printf("    MPEG-4 Audio Decoder Config:\n");
                            Printf("      Sampling Frequency: " + D(dec_config._samplingFrequency) + "\n");
                            Printf("      Channels: " + D(dec_config._channelCount) + "\n");
                            if (dec_config._extension._objectType != 0) {
                                object_type_string = MpegAudioSampleDescription.GetMpeg4AudioObjectTypeString(
                                    dec_config._extension._objectType);

                                Printf("      Extension:\n");
                                Printf("        Object Type: " + S(object_type_string) + "\n");
                                Printf("        SBR Present: " + (dec_config._extension._sbrPresent?"yes":"no") + "\n");
                                Printf("        PS Present:  " + (dec_config._extension._psPresent?"yes":"no") + "\n");
                                Printf("        Sampling Frequency: " + D(dec_config._extension._samplingFrequency) + "\n");
                            }
                            break;

                        case OutputFormat.JSON_FORMAT:
                            Printf(",\n");
                            Printf("\"mpeg_4_audio_decoder_config\":{\n");
                            Printf("  \"sampling_frequency\":" + D(dec_config._samplingFrequency) + ",\n");
                            Printf("  \"channels\":" + D(dec_config._channelCount) + "\n");
                            if (dec_config._extension._objectType != 0) {
                                object_type_string = MpegAudioSampleDescription.GetMpeg4AudioObjectTypeString(
                                    dec_config._extension._objectType);

                                Printf(",\n");
                                Printf("  \"extension\":{\n");
                                Printf("    \"object_type\":" + D((int)dec_config._extension._objectType) + ",\n");
                                Printf("    \"object_type_name\":\"" + S(object_type_string) + "\",\n");
                                Printf("    \"sbr_present\":" + (dec_config._extension._sbrPresent?"true":"false") + ",\n");
                                Printf("    \"ps_present\":" + (dec_config._extension._psPresent?"true":"false") + ",\n");
                                Printf("    \"sampling_frequency\":" + D(dec_config._extension._samplingFrequency) + "\n");
                                Printf("  }\n");
                            }
                            Printf("}");
                            break;
                    }
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AC-4 presentation helpers
        |   PORT NOTE: the block that computes presentation_type / presentation_codec /
        |   presentation_lang is duplicated in the text and JSON functions upstream;
        |   the port shares it.
        +---------------------------------------------------------------------*/
        private static void GetAc4PresentationInfo(Dac4Atom.Ac4Dsi                dsi,
                                                   Dac4Atom.Ac4Dsi.PresentationV1 presentation,
                                                   ref ushort                     self_contained,
                                                   out string                     presentation_type,
                                                   out string                     presentation_codec,
                                                   out string                     presentation_lang)
        {
            presentation_lang = "un";
            presentation_codec = "ac-4." +
                                 dsi.d.v1.bitstream_version.ToString("x2", CultureInfo.InvariantCulture) + "." +
                                 presentation.presentation_version.ToString("x2", CultureInfo.InvariantCulture) + "." +
                                 presentation.d.v1.mdcompat.ToString("x2", CultureInfo.InvariantCulture);
            for (int sg = 0; sg < presentation.d.v1.n_substream_groups; sg++) {
                Dac4Atom.Ac4Dsi.SubStreamGroupV1 group = presentation.d.v1.substream_groups[sg];
                if ( group.d.v1.b_content_type != 0 &&
                    (group.d.v1.content_classifier == 0 || group.d.v1.content_classifier == 4) &&
                     group.d.v1.b_language_indicator != 0) {
                    presentation_lang = Ap4Utils.StringFromCString(group.d.v1.language_tag_bytes, 0, group.d.v1.n_language_tag_bytes);
                }
            }
            if (presentation.d.v1.b_multi_pid == 0) { self_contained ++; }
            if (presentation.d.v1.b_presentation_channel_coded == 1) {
                presentation_type = "Channel based";
                if (presentation.d.v1.dsi_presentation_ch_mode >= 11 && presentation.d.v1.dsi_presentation_ch_mode <= 15) {
                    presentation_type = "Channel based immsersive";
                }
            } else {
                presentation_type = "Object based";
            }
        }

        /*----------------------------------------------------------------------
        |   ShowSampleDescription_Text
        +---------------------------------------------------------------------*/
        private static void ShowSampleDescription_Text(SampleDescription description, bool verbose)
        {
            SampleDescription desc = description;
            if (desc.GetType() == SampleDescription.Type.TYPE_PROTECTED) {
                ProtectedSampleDescription prot_desc = desc as ProtectedSampleDescription;
                if (prot_desc != null) {
                    ShowProtectedSampleDescription_Text(prot_desc, verbose);
                    desc = prot_desc.GetOriginalSampleDescription();
                }
            }

            if (verbose) {
                Printf("    Bytes: ");
                Atom details = desc.ToAtom();
                // PORT NOTE: upstream dereferences the atom without a NULL check
                if (details != null) ShowPayload(details, false);
                Printf("\n");
            }

            string coding = Ap4Utils.FormatFourChars(desc.GetFormat());
            Printf(    "    Coding:       " + coding);
            string format_name = Ap4.GetFormatName(desc.GetFormat());
            if (format_name != null) {
                Printf(" (" + format_name + ")\n");
            } else {
                Printf("\n");
            }
            string codec;
            desc.GetCodecString(out codec);
            Printf(    "    Codec String: " + codec + "\n");

            switch (desc.GetType()) {
              case SampleDescription.Type.TYPE_MPEG: {
                // MPEG sample description
                MpegSampleDescription mpeg_desc = (desc as MpegSampleDescription);

                Printf("    Stream Type: " + S(MpegSampleDescription.GetStreamTypeString(mpeg_desc.GetStreamType())) + "\n");
                Printf("    Object Type: " + S(MpegSampleDescription.GetObjectTypeString(mpeg_desc.GetObjectTypeId())) + "\n");
                Printf("    Max Bitrate: " + D(mpeg_desc.GetMaxBitrate()) + "\n");
                Printf("    Avg Bitrate: " + D(mpeg_desc.GetAvgBitrate()) + "\n");
                Printf("    Buffer Size: " + D(mpeg_desc.GetBufferSize()) + "\n");

                if (mpeg_desc.GetObjectTypeId() == Ap4.OTI_MPEG4_AUDIO          ||
                    mpeg_desc.GetObjectTypeId() == Ap4.OTI_MPEG2_AAC_AUDIO_LC   ||
                    mpeg_desc.GetObjectTypeId() == Ap4.OTI_MPEG2_AAC_AUDIO_MAIN) {
                    MpegAudioSampleDescription mpeg_audio_desc = mpeg_desc as MpegAudioSampleDescription;
                    if (mpeg_audio_desc != null) ShowMpegAudioSampleDescription(mpeg_audio_desc);
                }
                break;
              }

              case SampleDescription.Type.TYPE_AVC: {
                // AVC specifics
                AvcSampleDescription avc_desc = (desc as AvcSampleDescription);
                string profile_name = AvccAtom.GetProfileName(avc_desc.GetProfile());
                Printf("    AVC Profile:          " + D((int)avc_desc.GetProfile()));
                if (profile_name != null) {
                    Printf(" (" + profile_name + ")\n");
                } else {
                    Printf("\n");
                }
                Printf("    AVC Profile Compat:   " + X(avc_desc.GetProfileCompatibility()) + "\n");
                Printf("    AVC Level:            " + D((int)avc_desc.GetLevel()) + "\n");
                Printf("    AVC NALU Length Size: " + D((int)avc_desc.GetNaluLengthSize()) + "\n");
                Printf("    AVC SPS: [");
                string sep = "";
                for (int i=0; i<avc_desc.GetSequenceParameters().Count; i++) {
                    Printf(sep);
                    ShowData(avc_desc.GetSequenceParameters()[i]);
                    sep = ", ";
                }
                Printf("]\n");
                Printf("    AVC PPS: [");
                sep = "";
                for (int i=0; i<avc_desc.GetPictureParameters().Count; i++) {
                    Printf(sep);
                    ShowData(avc_desc.GetPictureParameters()[i]);
                    sep = ", ";
                }
                Printf("]\n");
                break;
              }

              case SampleDescription.Type.TYPE_HEVC: {
                // HEVC specifics
                HevcSampleDescription hevc_desc = (desc as HevcSampleDescription);
                string profile_name = HvccAtom.GetProfileName(hevc_desc.GetGeneralProfileSpace(), hevc_desc.GetGeneralProfile());
                Printf("    HEVC Profile Space:       " + D((int)hevc_desc.GetGeneralProfileSpace()) + "\n");
                Printf("    HEVC Profile:             " + D((int)hevc_desc.GetGeneralProfile()));
                if (profile_name != null) Printf(" (" + profile_name + ")");
                Printf("\n");
                Printf("    HEVC Profile Compat:      " + X(hevc_desc.GetGeneralProfileCompatibilityFlags()) + "\n");
                Printf("    HEVC Level:               " + D(hevc_desc.GetGeneralLevel()/30) + "." + D((hevc_desc.GetGeneralLevel()%30)/3) + "\n");
                Printf("    HEVC Tier:                " + D((int)hevc_desc.GetGeneralTierFlag()) + "\n");
                Printf("    HEVC Chroma Format:       " + D((int)hevc_desc.GetChromaFormat()));
                string chroma_format_name = HvccAtom.GetChromaFormatName(hevc_desc.GetChromaFormat());
                if (chroma_format_name != null) Printf(" (" + chroma_format_name + ")");
                Printf("\n");
                Printf("    HEVC Chroma Bit Depth:    " + D((int)hevc_desc.GetChromaBitDepth()) + "\n");
                Printf("    HEVC Luma Bit Depth:      " + D((int)hevc_desc.GetLumaBitDepth()) + "\n");
                Printf("    HEVC Average Frame Rate:  " + D((int)hevc_desc.GetAverageFrameRate()) + "\n");
                Printf("    HEVC Constant Frame Rate: " + D((int)hevc_desc.GetConstantFrameRate()) + "\n");
                Printf("    HEVC NALU Length Size:    " + D((int)hevc_desc.GetNaluLengthSize()) + "\n");
                Printf("    HEVC Sequences:\n");
                for (int i=0; i<hevc_desc.GetSequences().Count; i++) {
                    HvccAtom.Sequence seq = hevc_desc.GetSequences()[i];
                    Printf("      {\n");
                    Printf("        Array Completeness=" + D((int)seq._arrayCompleteness) + "\n");
                    Printf("        Type=" + D((int)seq._naluType));
                    string nalu_type_name = HevcNalParser.NaluTypeName(seq._naluType);
                    if (nalu_type_name != null) {
                        Printf(" (" + nalu_type_name + ")");
                    }
                    Printf("\n");
                    string sep = "";
                    for (int j=0; j<seq._nalus.Count; j++) {
                        Printf(sep + "        ");
                        ShowData(seq._nalus[j]);
                        sep = "\n";
                    }
                    Printf("\n      }\n");
                }
                break;
              }

              case SampleDescription.Type.TYPE_AV1: {
                // AV1 specifics
                Av1SampleDescription av1_desc = (desc as Av1SampleDescription);
                string profile_name = Av1cAtom.GetProfileName(av1_desc.GetSeqProfile());
                Printf("    AV1 Profile: " + D((int)av1_desc.GetSeqProfile()));
                if (profile_name != null) {
                    Printf(" (" + profile_name + ")\n");
                } else {
                    Printf("\n");
                }
                Printf("    AV1 Level:   " + D(2 + (av1_desc.GetSeqLevelIdx0() / 4)) + "." +
                                             D(av1_desc.GetSeqLevelIdx0() % 4) + "\n");
                Printf("    AV1 Tier:    " + D((int)av1_desc.GetSeqTier0()) + "\n");
                break;
              }

              case SampleDescription.Type.TYPE_SUBTITLES: {
                // Subtitles
                SubtitleSampleDescription subt_desc = (desc as SubtitleSampleDescription);
                Printf("    Subtitles:\n");
                Printf("       Namespace:       " + subt_desc.GetNamespace() + "\n");
                Printf("       Schema Location: " + subt_desc.GetSchemaLocation() + "\n");
                Printf("       Image Mime Type: " + subt_desc.GetImageMimeType() + "\n");
                break;
              }

              default:
                break;
            }

            AudioSampleDescription audio_desc = desc as AudioSampleDescription;
            if (audio_desc != null) {
                // Audio sample description
                Printf("    Sample Rate: " + D(audio_desc.GetSampleRate()) + "\n");
                Printf("    Sample Size: " + D((int)audio_desc.GetSampleSize()) + "\n");
                Printf("    Channels:    " + D((int)audio_desc.GetChannelCount()) + "\n");
            }
            VideoSampleDescription video_desc = desc as VideoSampleDescription;
            if (video_desc != null) {
                // Video sample description
                Printf("    Width:       " + D((int)video_desc.GetWidth()) + "\n");
                Printf("    Height:      " + D((int)video_desc.GetHeight()) + "\n");
                Printf("    Depth:       " + D((int)video_desc.GetDepth()) + "\n");
            }

            switch (desc.GetFormat()) {
              case Ap4.SAMPLE_FORMAT_AC_3: {
                // Dolby Digital specifics
                Dac3Atom dac3 = desc.GetDetails().GetChild(AtomType.DAC3) as Dac3Atom;
                if (dac3 != null) {
                    Printf("    AC-3 Data Rate: " + D(dac3.GetDataRate()) + "\n");
                    Printf("    AC-3 Stream:\n");
                    Printf("        fscod       = " + D(dac3.GetStreamInfo().fscod) + "\n");
                    Printf("        bsid        = " + D(dac3.GetStreamInfo().bsid) + "\n");
                    Printf("        bsmod       = " + D(dac3.GetStreamInfo().bsmod) + "\n");
                    Printf("        acmod       = " + D(dac3.GetStreamInfo().acmod) + "\n");
                    Printf("        lfeon       = " + D(dac3.GetStreamInfo().lfeon) + "\n");
                    Printf("    AC-3 dac3 payload: [");
                    ShowData(dac3.GetRawBytes());
                    Printf("]\n");
                }
                break;
              }

              case Ap4.SAMPLE_FORMAT_EC_3: {
                // Dolby Digital Plus specifics
                Dec3Atom dec3 = desc.GetDetails().GetChild(AtomType.DEC3) as Dec3Atom;
                if (dec3 != null) {
                    Printf("    E-AC-3 Data Rate: " + D(dec3.GetDataRate()) + "\n");
                    for (int i=0; i<dec3.GetSubStreams().Count; i++) {
                        Printf("    E-AC-3 Substream " + D(i) + ":\n");
                        Printf("        fscod       = " + D(dec3.GetSubStreams()[i].fscod) + "\n");
                        Printf("        bsid        = " + D(dec3.GetSubStreams()[i].bsid) + "\n");
                        Printf("        bsmod       = " + D(dec3.GetSubStreams()[i].bsmod) + "\n");
                        Printf("        acmod       = " + D(dec3.GetSubStreams()[i].acmod) + "\n");
                        Printf("        lfeon       = " + D(dec3.GetSubStreams()[i].lfeon) + "\n");
                        Printf("        num_dep_sub = " + D(dec3.GetSubStreams()[i].num_dep_sub) + "\n");
                        Printf("        chan_loc    = " + D(dec3.GetSubStreams()[i].chan_loc) + "\n");
                    }
                    if (dec3.GetFlagEC3ExtensionTypeA() != 0) {
                        Printf("    Dolby Digital Plus with Dolby Atmos: Yes\n");
                        Printf("    Dolby Atmos Complexity Index: " + D(dec3.GetComplexityIndexTypeA()) + "\n");
                    } else {
                        Printf("    Dolby Digital Plus with Dolby Atmos: No\n");
                    }
                    Printf("    E-AC-3 dec3 payload: [");
                    ShowData(dec3.GetRawBytes());
                    Printf("]\n");
                }
                break;
              }

              case Ap4.SAMPLE_FORMAT_AC_4: {
                // Dolby AC-4 specifics
                Dac4Atom dac4 = desc.GetDetails().GetChild(AtomType.DAC4) as Dac4Atom;
                if (dac4 != null) {
                    Printf("    Codecs String: ");
                    string dac4_codec;
                    dac4.GetCodecString(out dac4_codec);
                    Printf(dac4_codec);
                    Printf("\n");

                    Dac4Atom.Ac4Dsi dsi = dac4.GetDsi();
                    Printf("    AC-4 dsi version: " + D((int)dsi.ac4_dsi_version) + "\n");
                    ushort self_contained = 0;
                    if (dsi.ac4_dsi_version == 1) {
                        Printf("    AC-4 bitstream version: " + D((int)dsi.d.v1.bitstream_version) + "\n");
                        for (uint i = 0; i < dsi.d.v1.n_presentations; i++) {
                            Dac4Atom.Ac4Dsi.PresentationV1 presentation = dsi.d.v1.presentations[i];
                            if (presentation.presentation_version == 1 || presentation.presentation_version == 2) {
                                Printf("    AC-4 Presentation " + D(i) + ":\n");

                                string presentation_type;
                                string presentation_codec;
                                string presentation_lang;
                                GetAc4PresentationInfo(dsi, presentation, ref self_contained,
                                                       out presentation_type, out presentation_codec, out presentation_lang);
                                if (presentation.presentation_version == 2) {
                                    Printf("        Stream Type = Immersive stereo\n");
                                } else {
                                    Printf("        Stream Type = " + presentation_type + "\n");
                                }
                                Printf("        presentation_id = " + D(presentation.d.v1.b_presentation_id != 0 ? (int)presentation.d.v1.presentation_id : -1) + "\n");
                                Printf("        Codec String = " + presentation_codec + "\n");
                                Printf("        presentation_channel_mask_v1 = 0x" + X(presentation.d.v1.presentation_channel_mask_v1) + "\n");
                                Printf("        Dolby Atmos source = " + (presentation.d.v1.dolby_atmos_indicator != 0 ? "Yes": "No") + "\n");
                                Printf("        Language = " + presentation_lang + "\n");
                                Printf("        Self Contained = " + (presentation.d.v1.b_multi_pid != 0 ? "No": "Yes") + "\n");
                            }
                        }
                    }

                    // PORT NOTE: upstream reads d.v1.n_presentations through the v0/v1 union
                    // whatever the DSI version; the port reads the v1 member.
                    Printf("    Self Contained: " + ((self_contained == dsi.d.v1.n_presentations) ? "Yes": ((self_contained == 0)? "No": "Part")) + "\n");
                    Printf("    AC-4 dac4 payload: [");
                    ShowData(dac4.GetRawBytes());
                    Printf("]\n");
                }
                break;
              }

              case Ap4.SAMPLE_FORMAT_MLPA: {
                // Dolby TrueHD specifics
                DmlpAtom dmlp = desc.GetDetails().GetChild(AtomType.DMLP) as DmlpAtom;
                if (dmlp != null) {
                    Printf("    Format Info:    " + D(dmlp._formatInfo) + "\n");
                    Printf("    Peak Data Rate: " + D((int)dmlp._peakDataRate) + "\n");
                }
                break;
              }

              // VPx Specifics
              case Ap4.SAMPLE_FORMAT_VP8:
              case Ap4.SAMPLE_FORMAT_VP9:
              case Ap4.SAMPLE_FORMAT_VP10: {
                VpccAtom vpcc = desc.GetDetails().GetChild(AtomType.VPCC) as VpccAtom;
                if (vpcc != null) {
                    Printf("    Profile:                  " + D((int)vpcc.GetProfile()) + "\n");
                    Printf("    Level:                    " + D((int)vpcc.GetLevel()) + "\n");
                    Printf("    Bit Depth:                " + D((int)vpcc.GetBitDepth()) + "\n");
                    Printf("    Chroma Subsampling:       " + D((int)vpcc.GetChromaSubsampling()) + "\n");
                    Printf("    Colour Primaries:         " + D((int)vpcc.GetColourPrimaries()) + "\n");
                    Printf("    Transfer Characteristics: " + D((int)vpcc.GetTransferCharacteristics()) + "\n");
                    Printf("    Matrix Coefficients:      " + D((int)vpcc.GetMatrixCoefficients()) + "\n");
                    Printf("    Video Full Range Flag:    " + (vpcc.GetVideoFullRangeFlag() ? "true" : "false") + "\n");
                }
                break;
              }
            }

            // Dolby Vision specifics
            DvccAtom dvcc = desc.GetDetails().GetChild(AtomType.DVCC) as DvccAtom;
            if (dvcc != null) {
                /* Dolby Vision */
                Printf("    Dolby Vision:\n");
                Printf("      Version:     " + D((int)dvcc.GetDvVersionMajor()) + "." + D((int)dvcc.GetDvVersionMinor()) + "\n");
                string profile_name = DvccAtom.GetProfileName(dvcc.GetDvProfile());
                if (profile_name != null) {
                    Printf("      Profile:     " + profile_name + "\n");
                } else {
                    Printf("      Profile:     " + D((int)dvcc.GetDvProfile()) + "\n");
                }
                Printf("      Level:       " + D((int)dvcc.GetDvLevel()) + "\n");
                Printf("      RPU Present: " + (dvcc.GetRpuPresentFlag()?"true":"false") + "\n");
                Printf("      EL Present:  " + (dvcc.GetElPresentFlag()?"true":"false") + "\n");
                Printf("      BL Present:  " + (dvcc.GetBlPresentFlag()?"true":"false") + "\n");
                Printf("      BL Signal Compatibility ID:  " + D((int)dvcc.GetDvBlSignalCompatibilityID()) + "\n");
            }
        }

        /*----------------------------------------------------------------------
        |   ShowSampleDescription_Json
        +---------------------------------------------------------------------*/
        private static void ShowSampleDescription_Json(SampleDescription description, bool verbose)
        {
            Printf("{\n");
            SampleDescription desc = description;
            if (desc.GetType() == SampleDescription.Type.TYPE_PROTECTED) {
                ProtectedSampleDescription prot_desc = desc as ProtectedSampleDescription;
                if (prot_desc != null) {
                    ShowProtectedSampleDescription_Json(prot_desc, verbose);
                    Printf(",\n");
                    desc = prot_desc.GetOriginalSampleDescription();
                }
            }
            string coding = Ap4Utils.FormatFourChars(desc.GetFormat());
            string format_name = Ap4.GetFormatName(desc.GetFormat());
            Printf("\"coding\":\"" + coding + "\",\n");
            Printf("\"coding_name\":\"" + (format_name != null ? format_name : "") + "\",\n");
            string codec;
            desc.GetCodecString(out codec);
            Printf("\"codecs_string\":\"" + codec + "\"");

            switch (desc.GetType()) {
              case SampleDescription.Type.TYPE_MPEG: {
                // MPEG sample description
                MpegSampleDescription mpeg_desc = (desc as MpegSampleDescription);

                Printf(",\n");
                Printf("\"stream_type\":" + D((int)mpeg_desc.GetStreamType()) + ",\n");
                Printf("\"stream_type_name\":\"" + S(MpegSampleDescription.GetStreamTypeString(mpeg_desc.GetStreamType())) + "\",\n");
                Printf("\"object_type\":" + D((int)mpeg_desc.GetObjectTypeId()) + ",\n");
                Printf("\"object_type_name\":\"" + S(MpegSampleDescription.GetObjectTypeString(mpeg_desc.GetObjectTypeId())) + "\",\n");
                Printf("\"max_bitrate\":" + D(mpeg_desc.GetMaxBitrate()) + ",\n");
                Printf("\"average_bitrate\":" + D(mpeg_desc.GetAvgBitrate()) + ",\n");
                Printf("\"buffer_size\":" + D(mpeg_desc.GetBufferSize()) + ",\n");
                Printf("\"decoder_info\": \"");
                ShowData(mpeg_desc.GetDecoderInfo());
                Printf("\"");
                if (mpeg_desc.GetObjectTypeId() == Ap4.OTI_MPEG4_AUDIO          ||
                    mpeg_desc.GetObjectTypeId() == Ap4.OTI_MPEG2_AAC_AUDIO_LC   ||
                    mpeg_desc.GetObjectTypeId() == Ap4.OTI_MPEG2_AAC_AUDIO_MAIN) {
                    MpegAudioSampleDescription mpeg_audio_desc = mpeg_desc as MpegAudioSampleDescription;
                    if (mpeg_audio_desc != null) {
                        Printf(",\n");
                        ShowMpegAudioSampleDescription(mpeg_audio_desc);
                    }
                }
                break;
              }

              case SampleDescription.Type.TYPE_AVC: {
                // AVC specifics
                AvcSampleDescription avc_desc = (desc as AvcSampleDescription);
                Printf(",\n");
                Printf("\"avc_profile\":" + D((int)avc_desc.GetProfile()) + ",\n");
                string profile_name = AvccAtom.GetProfileName(avc_desc.GetProfile());
                if (profile_name != null) Printf("\"avc_profile_name\":\"" + profile_name + "\",\n");
                Printf("\"avc_profile_compat\":" + D((int)avc_desc.GetProfileCompatibility()) + ",\n");
                Printf("\"avc_level\":" + D((int)avc_desc.GetLevel()) + ",\n");
                Printf("\"avc_nalu_length_size\":" + D((int)avc_desc.GetNaluLengthSize()) + ",\n");
                Printf("\"avc_sps\": [");
                string sep = "";
                for (int i=0; i<avc_desc.GetSequenceParameters().Count; i++) {
                    Printf(sep);
                    Printf("\"");
                    ShowData(avc_desc.GetSequenceParameters()[i]);
                    Printf("\"");
                    sep = ", ";
                }
                Printf("],\n");
                Printf("\"avc_pps\": [");
                sep = "";
                for (int i=0; i<avc_desc.GetPictureParameters().Count; i++) {
                    Printf(sep);
                    Printf("\"");
                    ShowData(avc_desc.GetPictureParameters()[i]);
                    Printf("\"");
                    sep = ", ";
                }
                Printf("]");
                break;
              }

              case SampleDescription.Type.TYPE_HEVC: {
                // HEVC Specifics
                HevcSampleDescription hevc_desc = (desc as HevcSampleDescription);
                Printf(",\n");
                Printf("\"hevc_profile_space\":" + D((int)hevc_desc.GetGeneralProfileSpace()) + ",\n");
                Printf("\"hevc_profile\":" + D((int)hevc_desc.GetGeneralProfile()) + ",\n");
                string profile_name = HvccAtom.GetProfileName(hevc_desc.GetGeneralProfileSpace(), hevc_desc.GetGeneralProfile());
                if (profile_name != null) Printf("\"hevc_profile_name\":\"" + profile_name + "\",\n");
                Printf("\"hevc_profile_compat\":" + D(hevc_desc.GetGeneralProfileCompatibilityFlags()) + ",\n");
                Printf("\"hevc_constraints\":" + hevc_desc.GetGeneralConstraintIndicatorFlags().ToString(CultureInfo.InvariantCulture) + ",\n");
                Printf("\"hevc_level\":" + D((int)hevc_desc.GetGeneralLevel()) + ",\n");
                Printf("\"hevc_level_name\":\"" + D(hevc_desc.GetGeneralLevel()/30) + "." + D((hevc_desc.GetGeneralLevel()%30)/3) + "\",\n");
                Printf("\"hevc_tier\":" + D((int)hevc_desc.GetGeneralTierFlag()) + ",\n");
                Printf("\"hevc_chroma_format\":" + D((int)hevc_desc.GetChromaFormat()) + ",\n");
                string chroma_format_name = HvccAtom.GetChromaFormatName(hevc_desc.GetChromaFormat());
                if (chroma_format_name != null) Printf("\"hevc_chroma_format_name\":\"" + chroma_format_name + "\",\n");
                Printf("\"hevc_chroma_bit_depth\":" + D((int)hevc_desc.GetChromaBitDepth()) + ",\n");
                Printf("\"hevc_lunma_bit_depth\":" + D((int)hevc_desc.GetLumaBitDepth()) + ",\n");
                Printf("\"hevc_average_frame_rate\":" + D((int)hevc_desc.GetAverageFrameRate()) + ",\n");
                Printf("\"hevc_constant_frame_rate\":" + D((int)hevc_desc.GetConstantFrameRate()) + ",\n");
                Printf("\"hevc_nalu_length_size\":" + D((int)hevc_desc.GetNaluLengthSize()) + ",\n");
                Printf("\"hevc_sequences\": [\n");
                string seq_sep = "";
                for (int i=0; i<hevc_desc.GetSequences().Count; i++) {
                    HvccAtom.Sequence seq = hevc_desc.GetSequences()[i];
                    Printf(seq_sep + "      {\n");
                    Printf("        \"array_completeness\":" + D((int)seq._arrayCompleteness) + ",\n");
                    Printf("        \"type\":" + D((int)seq._naluType) + ",\n");
                    string nalu_type_name = HevcNalParser.NaluTypeName(seq._naluType);
                    if (nalu_type_name != null) {
                        Printf("        \"type_name\":\"" + nalu_type_name + "\",\n");
                    }
                    Printf("        \"data\":[");
                    string sep = "";
                    for (int j=0; j<seq._nalus.Count; j++) {
                        Printf(sep);
                        Printf("\"");
                        ShowData(seq._nalus[j]);
                        Printf("\"");
                        sep = ", ";
                    }
                    Printf("]\n      }");
                    seq_sep = ",\n";
                }
                Printf("]");
                break;
              }

              case SampleDescription.Type.TYPE_AV1: {
                // AV1 specifics
                Av1SampleDescription av1_desc = (desc as Av1SampleDescription);
                Printf(",\n");
                Printf("\"av1_seq_profile\":" + D((int)av1_desc.GetSeqProfile()) + ",\n");
                string profile_name = Av1cAtom.GetProfileName(av1_desc.GetSeqProfile());
                if (profile_name != null) Printf("\"av1_seq_profile_name\":\"" + profile_name + "\",\n");
                Printf("\"av1_seq_level_idx_0\":" + D((int)av1_desc.GetSeqLevelIdx0()) + ",\n");
                Printf("\"av1_seq_tier_0\":" + D((int)av1_desc.GetSeqTier0()));
                break;
              }

              case SampleDescription.Type.TYPE_SUBTITLES: {
                // Subtitles
                Printf(",\n");
                Printf("\"subtitles\": {\n");
                SubtitleSampleDescription subt_desc = (desc as SubtitleSampleDescription);
                Printf("  \"namespace\": \"" + subt_desc.GetNamespace() + "\",\n");
                Printf("  \"schema_location\": \"" + subt_desc.GetSchemaLocation() + "\",\n");
                Printf("  \"image_mime_type\": \"" + subt_desc.GetImageMimeType() + "\"\n");
                Printf("}");
                break;
              }

              default:
                break;
            }

            AudioSampleDescription audio_desc = desc as AudioSampleDescription;
            if (audio_desc != null) {
                // Audio sample description
                Printf(",\n");
                Printf("\"sample_rate\":" + D(audio_desc.GetSampleRate()) + ",\n");
                Printf("\"sample_size\":" + D((int)audio_desc.GetSampleSize()) + ",\n");
                Printf("\"channels\":" + D((int)audio_desc.GetChannelCount()));
            }

            VideoSampleDescription video_desc = desc as VideoSampleDescription;
            if (video_desc != null) {
                // Video sample description
                Printf(",\n");
                Printf("\"width\":" + D((int)video_desc.GetWidth()) + ",\n");
                Printf("\"height\":" + D((int)video_desc.GetHeight()) + ",\n");
                Printf("\"depth\":" + D((int)video_desc.GetDepth()));
            }

            switch (desc.GetFormat()) {
              case Ap4.SAMPLE_FORMAT_AC_3: {
                // Dolby Digital specifics
                Dac3Atom dac3 = desc.GetDetails().GetChild(AtomType.DAC3) as Dac3Atom;
                if (dac3 != null) {
                    Printf(",\n");
                    Printf("\"dolby_digital_info\": {\n");
                    Printf("  \"dac3_payload\": \"");
                    ShowData(dac3.GetRawBytes());
                    Printf("\",\n");
                    Printf("  \"data_rate\": " + D(dac3.GetDataRate()) + ",\n");
                    Printf("  \"stream_info\": {\n");
                    Printf("    \"fscod\": " + D(dac3.GetStreamInfo().fscod) + ",\n");
                    Printf("    \"bsid\": " + D(dac3.GetStreamInfo().bsid) + ",\n");
                    Printf("    \"bsmod\": " + D(dac3.GetStreamInfo().bsmod) + ",\n");
                    Printf("    \"acmod\": " + D(dac3.GetStreamInfo().acmod) + ",\n");
                    Printf("    \"lfeon\": " + D(dac3.GetStreamInfo().lfeon) + "\n");
                    Printf("  }\n");
                    Printf("}");
                }
                break;
              }

              case Ap4.SAMPLE_FORMAT_EC_3: {
                // Dolby Digital Plus specifics
                Dec3Atom dec3 = desc.GetDetails().GetChild(AtomType.DEC3) as Dec3Atom;
                if (dec3 != null) {
                    Printf(",\n");
                    Printf("\"dolby_digital_plus_info\": {\n");
                    Printf("  \"dec3_payload\": \"");
                    ShowData(dec3.GetRawBytes());
                    Printf("\",\n");
                    Printf("  \"data_rate\": " + D(dec3.GetDataRate()) + ",\n");
                    if (dec3.GetFlagEC3ExtensionTypeA() != 0) {
                        Printf("  \"Dolby_Atmos\": \"Yes\",\n");
                        Printf("  \"complexity_index\": " + D(dec3.GetComplexityIndexTypeA()) + ",\n");
                    } else {
                        Printf("  \"Dolby_Atmos\": \"No\",\n");
                    }
                    Printf("  \"substreams\": [\n");
                    string sep = "";
                    for (int i=0; i<dec3.GetSubStreams().Count; i++) {
                        Printf(sep + "    {\n");
                        Printf("      \"fscod\": " + D(dec3.GetSubStreams()[i].fscod) + ",\n");
                        Printf("      \"bsid\": " + D(dec3.GetSubStreams()[i].bsid) + ",\n");
                        Printf("      \"bsmod\": " + D(dec3.GetSubStreams()[i].bsmod) + ",\n");
                        Printf("      \"acmod\": " + D(dec3.GetSubStreams()[i].acmod) + ",\n");
                        Printf("      \"lfeon\": " + D(dec3.GetSubStreams()[i].lfeon) + ",\n");
                        Printf("      \"num_dep_sub\": " + D(dec3.GetSubStreams()[i].num_dep_sub) + ",\n");
                        Printf("      \"chan_loc\": " + D(dec3.GetSubStreams()[i].chan_loc) + "\n");
                        Printf("    }");
                        sep = ",\n";
                    }
                    Printf("\n  ]\n}");
                }
                break;
              }

              case Ap4.SAMPLE_FORMAT_AC_4: {
                // Dolby AC-4 specifics
                Dac4Atom dac4 = desc.GetDetails().GetChild(AtomType.DAC4) as Dac4Atom;
                if (dac4 != null) {
                    Printf(",\n");
                    Printf("\"dolby_ac4_info\": {\n");
                    Dac4Atom.Ac4Dsi dsi = dac4.GetDsi();
                    Printf("  \"dsi version\": " + D((int)dsi.ac4_dsi_version) + ",\n");
                    ushort self_contained = 0;
                    if (dsi.ac4_dsi_version == 1) {
                        Printf("  \"bitstream version\": " + D((int)dsi.d.v1.bitstream_version) + ",\n");
                        Printf("  \"presentations\": [\n");
                        string separator = "";
                        for (uint i = 0; i < dsi.d.v1.n_presentations; i++) {
                            Dac4Atom.Ac4Dsi.PresentationV1 presentation = dsi.d.v1.presentations[i];
                            if (presentation.presentation_version == 1 || presentation.presentation_version == 2) {
                                Printf(separator + "    { ");

                                string presentation_type;
                                string presentation_codec;
                                string presentation_lang;
                                GetAc4PresentationInfo(dsi, presentation, ref self_contained,
                                                       out presentation_type, out presentation_codec, out presentation_lang);
                                if (presentation.presentation_version == 2) {
                                    Printf("\"Stream Type\": \"Immersive stereo\", ");
                                } else {
                                    Printf("\"Stream Type\": \"" + presentation_type + "\", ");
                                }
                                Printf("\"presentation_id\": " + D(presentation.d.v1.b_presentation_id != 0 ? (int)presentation.d.v1.presentation_id : -1) + ", ");
                                Printf("\"presentation_channel_mask_v1\": " + presentation.d.v1.presentation_channel_mask_v1.ToString(CultureInfo.InvariantCulture) + ", ");
                                Printf("\"Dolby Atmos source\": \"" + (presentation.d.v1.dolby_atmos_indicator != 0 ? "Yes": "No") + "\", ");
                                Printf("\"Language\": \"" + presentation_lang + "\", ");
                                Printf("\"Self Contained\": \"" + (presentation.d.v1.b_multi_pid != 0 ? "No": "Yes") + "\"} ");
                                separator = ",\n";
                            }
                        }
                    }
                    Printf("\n  ],\n");

                    // PORT NOTE: see the note about d.v1.n_presentations in ShowSampleDescription_Text
                    Printf("  \"Self Contained\": \"" + ((self_contained == dsi.d.v1.n_presentations) ? "Yes": ((self_contained == 0)? "No": "Part")) + "\",\n");
                    Printf("  \"dac4_payload\": \"");
                    ShowData(dac4.GetRawBytes());
                    Printf("\"\n}");
                }
                break;
              }

              case Ap4.SAMPLE_FORMAT_VP8:
              case Ap4.SAMPLE_FORMAT_VP9:
              case Ap4.SAMPLE_FORMAT_VP10: {
                // VPx Specifics
                VpccAtom vpcc = desc.GetDetails().GetChild(AtomType.VPCC) as VpccAtom;
                if (vpcc != null) {
                    Printf(",\n");
                    Printf("\"vpx_info\": {\n");
                    Printf("    \"profile\":" + D((int)vpcc.GetProfile()) + ",\n");
                    Printf("    \"level\":" + D((int)vpcc.GetLevel()) + ",\n");
                    Printf("    \"bit_depth\":" + D((int)vpcc.GetBitDepth()) + ",\n");
                    Printf("    \"chroma_subsampling\":" + D((int)vpcc.GetChromaSubsampling()) + ",\n");
                    Printf("    \"colour_primaries\":" + D((int)vpcc.GetColourPrimaries()) + ",\n");
                    Printf("    \"transfer_characteristics\":" + D((int)vpcc.GetTransferCharacteristics()) + ",\n");
                    Printf("    \"matrix_coefficients\":" + D((int)vpcc.GetMatrixCoefficients()) + ",\n");
                    Printf("    \"video_full_range_flag\":" + (vpcc.GetVideoFullRangeFlag() ? "true" : "false") + "\n");
                    Printf("}");
                }
                break;
              }
            }

            // Dolby Vision specifics
            DvccAtom dvcc = desc.GetDetails().GetChild(AtomType.DVCC) as DvccAtom;
            if (dvcc == null) {
                dvcc = desc.GetDetails().GetChild(AtomType.DVVC) as DvccAtom;
            }
            if (dvcc != null) {
                /* Codec String */
                string workspace;
                // strncpy(coding, codec.GetChars(), 4)
                byte[] codec_chars = Ap4Utils.StringToCStringBytes(codec);
                coding = Ap4Utils.StringFromBytes(codec_chars, 0, (uint)Math.Min(4, codec_chars.Length));
                string dv_profile = ((int)dvcc.GetDvProfile()).ToString("D2", CultureInfo.InvariantCulture);
                string dv_level   = ((int)dvcc.GetDvLevel()).ToString("D2", CultureInfo.InvariantCulture);
                /* Non back-compatible */
                if (coding == "dvav" || coding == "dva1" ||
                    coding == "dvhe" || coding == "dvh1") {
                    workspace = coding + "." + dv_profile + "." + dv_level;
                    codec = workspace;
                } else {
                    if (coding == "avc1") {
                        coding = "dva1";
                    } else if (coding == "avc3") {
                        coding = "dvav";
                    } else if (coding == "hev1") {
                        coding = "dvhe";
                    } else if (coding == "hvc1") {
                        coding = "dvh1";
                    }
                    workspace = codec + "," + coding + "." + dv_profile + "." + dv_level;
                    // AP4_FormatString(workspace, sizeof(workspace), ...) truncates to 63 chars
                    if (workspace.Length > 63) workspace = workspace.Substring(0, 63);
                    codec = workspace;
                }
                Printf(",\n");
                Printf("\"dv_codecs_string\":\"");
                Printf(codec);
                Printf("\"");
                /* Dolby Vision */
                Printf(",\n");
                Printf("\"dolby_vision\": {\n");
                Printf("   \"version_major\": " + D((int)dvcc.GetDvVersionMajor()) + ",\n");
                Printf("   \"version_minor\": " + D((int)dvcc.GetDvVersionMinor()) + ",\n");
                Printf("   \"profile\": " + D((int)dvcc.GetDvProfile()) + ",\n");
                string profile_name = DvccAtom.GetProfileName(dvcc.GetDvProfile());
                Printf("   \"profile_name\": \"" + (profile_name != null ? profile_name : "") + "\",\n");
                Printf("   \"level\": " + D((int)dvcc.GetDvLevel()) + ",\n");
                Printf("   \"rpu_present\": " + (dvcc.GetRpuPresentFlag()?"true":"false") + ",\n");
                Printf("   \"el_present\": " + (dvcc.GetElPresentFlag()?"true":"false") + ",\n");
                Printf("   \"bl_present\": " + (dvcc.GetBlPresentFlag()?"true":"false") + ",\n");
                Printf("   \"dv_bl_signal_compatibility_id\": " + D((int)dvcc.GetDvBlSignalCompatibilityID()) + "\n");
                Printf("}");
            }

            // Color information from colr atom for non-VPx tracks
            if (desc.GetFormat() != Ap4.SAMPLE_FORMAT_VP8 &&
                desc.GetFormat() != Ap4.SAMPLE_FORMAT_VP9 &&
                desc.GetFormat() != Ap4.SAMPLE_FORMAT_VP10) {
                ColrAtom colr = desc.GetDetails().GetChild(AtomType.COLR) as ColrAtom;
                if (colr != null && colr.GetColourParameterType() == Ap4.SAMPLE_COLOR_TYPE_NCLX) {
                    Printf(",\n");
                    Printf("\"color_info\": {\n");
                    Printf("    \"colour_primaries\":" + D((int)colr.GetPrimariesIndex()) + ",\n");
                    Printf("    \"transfer_characteristics\":" + D((int)colr.GetTransferFunctionIndex()) + ",\n");
                    Printf("    \"matrix_coefficients\":" + D((int)colr.GetMatrixIndex()) + ",\n");
                    Printf("    \"video_full_range_flag\":" + (colr.GetFullRangeFlag() != 0 ? "true" : "false") + "\n");
                    Printf("}");
                }
            }

            Printf("\n}");
        }

        /*----------------------------------------------------------------------
        |   ShowSampleDescription
        +---------------------------------------------------------------------*/
        private static void ShowSampleDescription(SampleDescription description, bool verbose)
        {
            switch (Options.format) {
                case OutputFormat.TEXT_FORMAT: ShowSampleDescription_Text(description, verbose); break;
                case OutputFormat.JSON_FORMAT: ShowSampleDescription_Json(description, verbose); break;
            }
        }

        /*----------------------------------------------------------------------
        |   ShowDcfInfo
        +---------------------------------------------------------------------*/
        private static void ShowDcfInfo(File file)
        {
            FtypAtom ftyp = file.GetFileType();
            if (ftyp == null) return;
            Printf("OMA DCF File, version=" + D(ftyp.GetMinorVersion()) + "\n");
            if (ftyp.GetMinorVersion() != 2) return;

            OdheAtom odhe = file.FindChild("odrm/odhe") as OdheAtom;
            if (odhe != null) {
                Printf("Content Type:      " + S(odhe.GetContentType()) + "\n");
            }
            OhdrAtom ohdr = file.FindChild("odrm/odhe/ohdr") as OhdrAtom;
            if (ohdr != null) {
                Printf("Encryption Method: ");
                switch (ohdr.GetEncryptionMethod()) {
                    case Ap4.OMA_DCF_ENCRYPTION_METHOD_NULL:    Printf("NULL\n");        break;
                    case Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CBC: Printf("AES-128-CBC\n"); break;
                    case Ap4.OMA_DCF_ENCRYPTION_METHOD_AES_CTR: Printf("AES-128-CTR\n"); break;
                    default:                                    Printf(D((int)ohdr.GetEncryptionMethod()) + "\n"); break;
                }
                Printf("Padding Scheme:    ");
                switch (ohdr.GetPaddingScheme()) {
                    case Ap4.OMA_DCF_PADDING_SCHEME_NONE:     Printf("NONE\n"); break;
                    case Ap4.OMA_DCF_PADDING_SCHEME_RFC_2630: Printf("RFC 2630\n"); break;
                    default:                                  Printf(D((int)ohdr.GetPaddingScheme()) + "\n"); break;
                }
                Printf("Content ID:        " + S(ohdr.GetContentId()) + "\n");
                Printf("Rights Issuer URL: " + S(ohdr.GetRightsIssuerUrl()) + "\n");
                Printf("Textual Headers:\n");

                uint   headers_size = ohdr.GetTextualHeaders().GetDataSize();
                byte[] headers = ohdr.GetTextualHeaders().GetData();
                int    headers_pos = 0;
                while (headers_size != 0) {
                    // PORT NOTE: strlen() upstream may run past the end of the buffer when
                    // the last header is not null-terminated; the port stops at the end.
                    string header = Ap4Utils.StringFromCString(headers, headers_pos, headers_size);
                    Printf("  " + header + "\n");
                    uint header_len = Ap4Utils.StringLength(header);
                    if (header_len+1 >= headers_size) break;
                    headers_size -= header_len+1;
                    headers_pos  += (int)(header_len+1);
                }
                GrpiAtom grpi = ohdr.GetChild(AtomType.GRPI) as GrpiAtom;
                if (grpi != null) {
                    Printf("Group ID:          " + S(grpi.GetGroupId()) + "\n");
                }
            }
        }

        /*----------------------------------------------------------------------
        |   ReadGolomb
        +---------------------------------------------------------------------*/
        private static uint ReadGolomb(BitStream bits)
        {
            uint leading_zeros = 0;
            while (bits.ReadBit() == 0) {
                leading_zeros++;
                // PORT NOTE: the caller only writes 8 bytes to the bit stream. Past
                // those, upstream reads the uninitialized part of the AP4_BitStream
                // buffer (undefined result); the port's buffer is zero-filled, so
                // the loop would never end: stop there.
                if (leading_zeros >= 64) return 0;
            }
            if (leading_zeros != 0) {
                return unchecked((uint)((1<<(int)leading_zeros)-1)+bits.ReadBits(leading_zeros));
            } else {
                return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   ShowAvcInfo
        +---------------------------------------------------------------------*/
        private static void ShowAvcInfo(DataBuffer sample_data, AvcSampleDescription avc_desc)
        {
            byte[] data     = sample_data.GetData();
            int    data_pos = 0;
            uint   size     = sample_data.GetDataSize();
            // PORT NOTE: upstream walks the buffer with a raw pointer and may read
            // past the end of the sample data when a sample is truncated or malformed.
            // The port can follow it as far as the end of the buffer (which, as
            // upstream, still holds the bytes of previous, larger samples), not further.
            uint   data_end = (uint)data.Length;

            while (size >= avc_desc.GetNaluLengthSize()) {
                uint nalu_length = 0;
                if (avc_desc.GetNaluLengthSize() == 1) {
                    nalu_length = data[data_pos++];
                    --size;
                } else if (avc_desc.GetNaluLengthSize() == 2) {
                    nalu_length = Ap4Utils.BytesToUInt16BE(data, data_pos);
                    data_pos += 2;
                    size -= 2;
                } else if (avc_desc.GetNaluLengthSize() == 4) {
                    nalu_length = Ap4Utils.BytesToUInt32BE(data, data_pos);
                    data_pos += 4;
                    size -= 4;
                } else {
                    return;
                }
                if (nalu_length <= size) {
                    size -= nalu_length;
                } else {
                    size = 0;
                }

                if ((uint)data_pos >= data_end) return; // PORT NOTE: see above

                switch (data[data_pos] & 0x1F) {
                    case 1: {
                        // PORT NOTE: `bits.WriteBytes(data+1, 8)` upstream; bytes past the end
                        // of the buffer (undefined upstream) are zeros in the port.
                        byte[] window = new byte[8];
                        for (int i=0; i<8; i++) {
                            if ((uint)(data_pos+1+i) < data_end) window[i] = data[data_pos+1+i];
                        }
                        BitStream bits = new BitStream();
                        bits.WriteBytes(window, 8);
                        ReadGolomb(bits);
                        uint slice_type = ReadGolomb(bits);
                        switch (slice_type) {
                            case 0: Printf("<P>");  break;
                            case 1: Printf("<B>");  break;
                            case 2: Printf("<I>");  break;
                            case 3: Printf("<SP>"); break;
                            case 4: Printf("<SI>"); break;
                            case 5: Printf("<P>");  break;
                            case 6: Printf("<B>");  break;
                            case 7: Printf("<I>");  break;
                            case 8: Printf("<SP>"); break;
                            case 9: Printf("<SI>"); break;
                            default: Printf("<S/" + D(slice_type) + ">"); break;
                        }
                        return; // only show first slice type
                    }

                    case 5:
                        Printf("<I>");
                        return;
                }

                // PORT NOTE: guards the pointer arithmetic against overflow (see above)
                if (nalu_length > data_end-(uint)data_pos) return;
                data_pos += (int)nalu_length;
            }
        }

        /*----------------------------------------------------------------------
        |   ShowSample_Text
        +---------------------------------------------------------------------*/
        private static void ShowSample_Text(Track                 track,
                                            Sample                sample,
                                            DataBuffer            sample_data,
                                            uint                  index,
                                            bool                  verbose,
                                            bool                  show_sample_data,
                                            AvcSampleDescription  avc_desc)
        {
            Printf("[" + Pad0(D(track.GetId()), 2) + "." + Pad0(D(index+1), 6) + "] size=" +
                   Pad(D((int)sample.GetSize()), 6) + " duration=" +
                   Pad(D((int)sample.GetDuration()), 6));
            if (verbose) {
                Printf(" (" + Pad(D((int)Ap4Utils.ConvertTime(sample.GetDuration(), track.GetMediaTimeScale(), 1000)), 6) + " ms)" +
                       " offset=" + Pad(LLD(sample.GetOffset()), 10) +
                       " dts=" + Pad(LLD(sample.GetDts()), 10) +
                       " (" + Pad(LLD(Ap4Utils.ConvertTime(sample.GetDts(), track.GetMediaTimeScale(), 1000)), 10) + " ms)" +
                       " cts=" + Pad(LLD(sample.GetCts()), 10) +
                       " (" + Pad(LLD(Ap4Utils.ConvertTime(sample.GetCts(), track.GetMediaTimeScale(), 1000)), 10) + " ms)" +
                       " [" + D(sample.GetDescriptionIndex()) + "]");
            }
            if (sample.IsSync()) {
                Printf(" [S] ");
            } else {
                Printf("     ");
            }
            if (avc_desc != null || show_sample_data) {
                sample.ReadData(sample_data);
            }
            if (avc_desc != null) {
                ShowAvcInfo(sample_data, avc_desc);
            }
            if (show_sample_data) {
                uint show = sample_data.GetDataSize();
                if (!verbose) {
                    if (show > 12) show = 12; // max first 12 chars
                }

                StringBuilder text = new StringBuilder();
                for (uint i=0; i<show; i++) {
                    if (verbose) {
                        if (i%16 == 0) {
                            text.Append("\n" + Pad0(D(i), 6) + ": ");
                        }
                    }
                    text.Append(sample_data.GetData()[i].ToString("x2", CultureInfo.InvariantCulture));
                    if (verbose) text.Append(' ');
                }
                Printf(text.ToString());
                if (show != sample_data.GetDataSize()) {
                    Printf("...");
                }
            }
        }

        /*----------------------------------------------------------------------
        |   ScanMedia
        +---------------------------------------------------------------------*/
        private static void ScanMedia(Movie movie, Track track, ByteStream stream, ref MediaInfo info)
        {
            ulong total_size = 0;
            ulong total_duration = 0;

            ulong position;
            stream.Tell(out position);
            stream.Seek(0);
            LinearReader reader = new LinearReader(movie, stream);
            reader.EnableTrack(track.GetId());

            info.sample_count = 0;

            Sample sample = new Sample();
            if (movie.HasFragments()) {
                DataBuffer sample_data = new DataBuffer();
                for (uint i=0; ; i++) {
                    uint track_id = 0;
                    Ap4Result result = reader.ReadNextSample(sample, sample_data, out track_id);
                    if (result.Succeeded()) {
                        total_size += sample.GetSize();
                        total_duration += sample.GetDuration();
                        ++info.sample_count;
                    } else {
                        break;
                    }
                }
            } else {
                info.sample_count = track.GetSampleCount();
                for (uint i=0; i<track.GetSampleCount(); i++) {
                    if (track.GetSample(i, sample).Succeeded()) {
                        total_size += sample.GetSize();
                    }
                }
                total_duration = track.GetMediaDuration();
            }
            info.duration = total_duration;

            double duration_ms = (double)Ap4Utils.ConvertTime(total_duration, track.GetMediaTimeScale(), 1000);
            if (duration_ms != 0) {
                info.bitrate = 8.0*1000.0*(double)total_size/duration_ms;
            } else {
                info.bitrate = 0.0;
            }
        }

        /*----------------------------------------------------------------------
        |   ShowTrackInfo_Text
        +---------------------------------------------------------------------*/
        private static void ShowTrackInfo_Text(Movie movie, Track track, ByteStream stream, bool show_samples, bool show_sample_data, bool verbose, bool fast)
        {
            Printf("  flags:        " + D(track.GetFlags()));
            if ((track.GetFlags() & Ap4.TRACK_FLAG_ENABLED) != 0) {
                Printf(" ENABLED");
            }
            if ((track.GetFlags() & Ap4.TRACK_FLAG_IN_MOVIE) != 0) {
                Printf(" IN-MOVIE");
            }
            if ((track.GetFlags() & Ap4.TRACK_FLAG_IN_PREVIEW) != 0) {
                Printf(" IN-PREVIEW");
            }
            Printf("\n");
            Printf("  id:           " + D(track.GetId()) + "\n");
            Printf("  type:         ");
            switch (track.GetType()) {
                case Track.Type.TYPE_AUDIO:     Printf("Audio\n");     break;
                case Track.Type.TYPE_VIDEO:     Printf("Video\n");     break;
                case Track.Type.TYPE_HINT:      Printf("Hint\n");      break;
                case Track.Type.TYPE_SYSTEM:    Printf("System\n");    break;
                case Track.Type.TYPE_TEXT:      Printf("Text\n");      break;
                case Track.Type.TYPE_JPEG:      Printf("JPEG\n");      break;
                case Track.Type.TYPE_SUBTITLES: Printf("Subtitles\n"); break;
                default: {
                    string hdlr = Ap4Utils.FormatFourChars(track.GetHandlerType());
                    Printf("Unknown [");
                    Printf(hdlr);
                    Printf("]\n");
                    break;
                }
            }
            Printf("  duration: " + D(track.GetDurationMs()) + " ms\n");
            Printf("  language: " + S(track.GetTrackLanguage()) + "\n");
            Printf("  media:\n");
            Printf("    sample count: " + D(track.GetSampleCount()) + "\n");
            Printf("    timescale:    " + D(track.GetMediaTimeScale()) + "\n");
            Printf("    duration:     " + LLD(track.GetMediaDuration()) + " (media timescale units)\n");
            Printf("    duration:     " + D((uint)Ap4Utils.ConvertTime(track.GetMediaDuration(), track.GetMediaTimeScale(), 1000)) + " (ms)\n");
            if (!fast) {
                MediaInfo media_info = new MediaInfo();
                ScanMedia(movie, track, stream, ref media_info);
                Printf("    bitrate (computed): " + F3(media_info.bitrate/1000.0) + " Kbps\n");
                if (movie.HasFragments()) {
                    Printf("    sample count with fragments: " + LLD(media_info.sample_count) + "\n");
                    Printf("    duration with fragments:     " + LLD(media_info.duration) + "\n");
                    Printf("    duration with fragments:     " + D((uint)Ap4Utils.ConvertTime(media_info.duration, track.GetMediaTimeScale(), 1000)) + " (ms)\n");
                }
            }
            if (track.GetWidth() != 0 || track.GetHeight() != 0) {
                Printf("  display width:  " + F((double)(float)track.GetWidth()/65536.0) + "\n");
                Printf("  display height: " + F((double)(float)track.GetHeight()/65536.0) + "\n");
            }
            if (track.GetType() == Track.Type.TYPE_VIDEO && track.GetSampleCount() != 0) {
                float media_duration_s = (float)track.GetMediaDuration()/(float)track.GetMediaTimeScale();
                float frame_rate = (float)track.GetSampleCount()/media_duration_s;
                Printf("  frame rate (computed): " + F3((double)frame_rate) + "\n");
            }

            // show all sample descriptions
            AvcSampleDescription avc_desc = null;
            for (uint desc_index=0; ; desc_index++) {
                SampleDescription  sample_desc = track.GetSampleDescription(desc_index);
                if (sample_desc == null) break;
                Printf("  Sample Description " + D(desc_index) + "\n");
                ShowSampleDescription(sample_desc, verbose);
                avc_desc = sample_desc as AvcSampleDescription;
            }

            // show samples if requested
            if (show_samples) {
                Sample     sample = new Sample();
                DataBuffer sample_data = new DataBuffer();
                uint       index = 0;
                while (track.GetSample(index, sample).Succeeded()) {
                    if (avc_desc != null || show_sample_data) {
                        sample.ReadData(sample_data);
                    }

                    ShowSample_Text(track, sample, sample_data, index, verbose, show_sample_data, avc_desc);
                    Printf("\n");
                    index++;
                }
            }
        }

        /*----------------------------------------------------------------------
        |   ShowTrackInfo_Json
        +---------------------------------------------------------------------*/
        private static void ShowTrackInfo_Json(Movie movie, Track track, ByteStream stream, bool show_samples, bool show_sample_data, bool verbose, bool fast)
        {
            Printf("{\n");
            Printf("  \"flags\":" + D(track.GetFlags()) + ",\n");
            Printf("  \"flag_names\":[");
            string sep = "";
            if ((track.GetFlags() & Ap4.TRACK_FLAG_ENABLED) != 0) {
                Printf("\"ENABLED\"");
                sep = " ,";
            }
            if ((track.GetFlags() & Ap4.TRACK_FLAG_IN_MOVIE) != 0) {
                Printf(sep + "\"IN-MOVIE\"");
                sep = " ,";
            }
            if ((track.GetFlags() & Ap4.TRACK_FLAG_IN_PREVIEW) != 0) {
                Printf(sep + "\"IN-PREVIEW\"");
            }
            Printf("],\n");
            Printf("  \"id\":" + D(track.GetId()) + ",\n");
            Printf("  \"type\":");
            switch (track.GetType()) {
                case Track.Type.TYPE_AUDIO:     Printf("\"Audio\"");     break;
                case Track.Type.TYPE_VIDEO:     Printf("\"Video\"");     break;
                case Track.Type.TYPE_HINT:      Printf("\"Hint\"");      break;
                case Track.Type.TYPE_SYSTEM:    Printf("\"System\"");    break;
                case Track.Type.TYPE_TEXT:      Printf("\"Text\"");      break;
                case Track.Type.TYPE_JPEG:      Printf("\"JPEG\"");      break;
                case Track.Type.TYPE_SUBTITLES: Printf("\"Subtitles\""); break;
                default: {
                    string hdlr = Ap4Utils.FormatFourChars(track.GetHandlerType());
                    Printf("\"Unknown [");
                    Printf(hdlr);
                    Printf("]\"");
                    break;
                }
            }
            Printf(",\n");
            Printf("  \"duration_ms\":" + D(track.GetDurationMs()) + ",\n");
            Printf("  \"language\":\"" + S(track.GetTrackLanguage()) + "\",\n");
            Printf("  \"media\":{\n");
            Printf("    \"sample_count\":" + D(track.GetSampleCount()) + ",\n");
            Printf("    \"timescale\":" + D(track.GetMediaTimeScale()) + ",\n");
            Printf("    \"duration\":" + LLD(track.GetMediaDuration()) + ",\n");
            Printf("    \"duration_ms\":" + D((uint)Ap4Utils.ConvertTime(track.GetMediaDuration(), track.GetMediaTimeScale(), 1000)));
            if (!fast) {
                MediaInfo media_info = new MediaInfo();
                ScanMedia(movie, track, stream, ref media_info);
                Printf(",\n");
                Printf("    \"bitrate\":" + F3(media_info.bitrate/1000.0));
                if (movie.HasFragments()) {
                    Printf(",\n");
                    Printf("    \"sample_count_with_fragments\":" + LLD(media_info.sample_count) + ",\n");
                    Printf("    \"duration_with_fragments\":" + LLD(media_info.duration) + ",\n");
                    Printf("    \"duration_with_fragments_ms\":" + D((uint)Ap4Utils.ConvertTime(media_info.duration, track.GetMediaTimeScale(), 1000)));
                }
                Printf("\n");
            } else {
                Printf("\n");
            }
            Printf("  },\n");
            if (track.GetWidth() != 0 || track.GetHeight() != 0) {
                Printf("  \"display_width\":" + F((double)(float)track.GetWidth()/65536.0) + ",\n");
                Printf("  \"display_height\":" + F((double)(float)track.GetHeight()/65536.0) + ",\n");
            }
            if (track.GetType() == Track.Type.TYPE_VIDEO && track.GetSampleCount() != 0) {
                float scaled_sample_count = (float)1000*track.GetSampleCount();
                float frame_rate = scaled_sample_count/(float)track.GetDurationMs();
                Printf("  \"frame_rate\":" + F3((double)frame_rate) + ",\n");
            }

            // show all sample descriptions
            Printf("  \"sample_descriptions\":[\n");
            sep = "";
            for (uint desc_index=0; ; desc_index++) {
                SampleDescription  sample_desc = track.GetSampleDescription(desc_index);
                if (sample_desc == null) break;
                Printf(sep);
                ShowSampleDescription(sample_desc, verbose);
                sep = ",\n";
            }
            Printf("  ]\n");
            Printf("}");
        }

        /*----------------------------------------------------------------------
        |   ShowTrackInfo
        +---------------------------------------------------------------------*/
        private static void ShowTrackInfo(Movie movie, Track track, ByteStream stream, bool show_samples, bool show_sample_data, bool verbose, bool fast)
        {
            switch (Options.format) {
                case OutputFormat.TEXT_FORMAT:
                    ShowTrackInfo_Text(movie, track, stream, show_samples, show_sample_data, verbose, fast);
                    break;

                case OutputFormat.JSON_FORMAT:
                    ShowTrackInfo_Json(movie, track, stream, show_samples, show_sample_data, verbose, fast);
                    break;
            }
        }

        /*----------------------------------------------------------------------
        |   ShowMovieInfo
        +---------------------------------------------------------------------*/
        private static void ShowMovieInfo(Movie movie)
        {
            switch (Options.format) {
                case OutputFormat.TEXT_FORMAT:
                    Printf("Movie:\n");
                    Printf("  duration:   " + LLD(movie.GetDuration()) + " (movie timescale units)\n");
                    Printf("  duration:   " + D(movie.GetDurationMs()) + " (ms)\n");
                    Printf("  time scale: " + D(movie.GetTimeScale()) + "\n");
                    Printf("  fragments:  " + (movie.HasFragments()?"yes":"no") + "\n");
                    Printf("\n");
                    break;

                case OutputFormat.JSON_FORMAT:
                    Printf("\"movie\":{\n");
                    Printf("  \"duration_ms\":" + D(movie.GetDurationMs()) + ",\n");
                    Printf("  \"duration\":" + LLD(movie.GetDuration()) + ",\n");
                    Printf("  \"time_scale\":" + D(movie.GetTimeScale()) + ",\n");
                    Printf("  \"fragments\":" + (movie.HasFragments()?"true":"false") + "\n");
                    Printf("},\n");
                    break;
            }
        }

        /*----------------------------------------------------------------------
        |   ShowFileInfo
        +---------------------------------------------------------------------*/
        private static void ShowFileInfo(File file)
        {
            FtypAtom file_type = file.GetFileType();
            if (file_type == null) return;
            string four_cc;

            four_cc = Ap4Utils.FormatFourChars(file_type.GetMajorBrand());
            switch (Options.format) {
                case OutputFormat.TEXT_FORMAT:
                    Printf("File:\n");
                    Printf("  major brand:      " + four_cc + "\n");
                    Printf("  minor version:    " + X(file_type.GetMinorVersion()) + "\n");
                    break;

                case OutputFormat.JSON_FORMAT:
                    Printf("\"file\":{\n");
                    Printf("  \"major_brand\":\"" + four_cc + "\",\n");
                    Printf("  \"minor_version\":" + D(file_type.GetMinorVersion()) + ",\n");
                    break;
            }

            // compatible brands
            if (Options.format == OutputFormat.JSON_FORMAT) Printf("  \"compatible_brands\":[");
            string sep = "";
            for (int i=0; i<file_type.GetCompatibleBrands().Count; i++) {
                uint cb = file_type.GetCompatibleBrands()[i];
                if (cb == 0) continue;
                four_cc = Ap4Utils.FormatFourChars(cb);
                switch (Options.format) {
                    case OutputFormat.TEXT_FORMAT:
                        Printf("  compatible brand: " + four_cc + "\n");
                        break;

                    case OutputFormat.JSON_FORMAT:
                        Printf(sep + "\"" + four_cc + "\"");
                        sep = ", ";
                        break;
                }
            }

            if (Options.format == OutputFormat.JSON_FORMAT) {
                Printf("],\n");
            }

            // fast-start
            switch (Options.format) {
                case OutputFormat.TEXT_FORMAT:
                    Printf("  fast start:       " + (file.IsMoovBeforeMdat() ? "yes" : "no") + "\n\n");
                    break;

                case OutputFormat.JSON_FORMAT:
                    Printf("  \"fast_start\":" + (file.IsMoovBeforeMdat() ? "true" : "false") + "\n},\n");
                    break;
            }
        }

        /*----------------------------------------------------------------------
        |   ShowTracks
        +---------------------------------------------------------------------*/
        private static void ShowTracks(Movie movie, List<Track> tracks, ByteStream stream, bool show_samples, bool show_sample_data, bool verbose, bool fast)
        {
            if (Options.format == OutputFormat.JSON_FORMAT) Printf("\"tracks\":[\n");
            int index=1;
            foreach (Track track in tracks) {
                if (Options.format == OutputFormat.TEXT_FORMAT) {
                    Printf("Track " + D(index) + ":\n");
                }
                if (Options.format == OutputFormat.JSON_FORMAT && index > 1) Printf(",\n");
                ShowTrackInfo(movie, track, stream, show_samples, show_sample_data, verbose, fast);
                ++index;
            }
            if (Options.format == OutputFormat.JSON_FORMAT) Printf("]\n");
        }

        /*----------------------------------------------------------------------
        |   ShowMarlinTracks
        +---------------------------------------------------------------------*/
        private static void ShowMarlinTracks(File file, ByteStream stream, List<Track> tracks, bool show_samples, bool show_sample_data, bool verbose, bool fast)
        {
            if (Options.format != OutputFormat.TEXT_FORMAT) return;

            List<MarlinIpmpParser.SinfEntry> sinf_entries = new List<MarlinIpmpParser.SinfEntry>();
            Ap4Result result = MarlinIpmpParser.Parse(file, stream, sinf_entries);
            if (result.Failed()) {
                Printf("WARNING: cannot parse Marlin IPMP info\n");
                ShowTracks(file.GetMovie(), tracks, stream, show_samples, show_sample_data, verbose, fast);
                return;
            }
            int index=1;
            foreach (Track track in tracks) {
                Printf("Track " + D(index) + ":\n");
                ShowTrackInfo(file.GetMovie(), track, stream, show_samples, show_sample_data, verbose, fast);

                foreach (MarlinIpmpParser.SinfEntry sinf_entry in sinf_entries) {
                    if (sinf_entry._trackId == track.GetId()) {
                        Printf("    [ENCRYPTED]\n");
                        if (sinf_entry._sinf == null) {
                            Printf("WARNING: NULL sinf entry for track ID " + D(track.GetId()) + "\n");
                        } else {
                            ContainerAtom  schi = sinf_entry._sinf.GetChild(AtomType.SCHI) as ContainerAtom;
                            SchmAtom       schm = sinf_entry._sinf.GetChild(AtomType.SCHM) as SchmAtom;
                            if (schm != null && schi != null) {
                                uint scheme_type = schm.GetSchemeType();
                                Printf("      Scheme Type:    ");
                                PrintFourCC(scheme_type);
                                Printf("\n");
                                Printf("      Scheme Version: " + D(schm.GetSchemeVersion()) + "\n");
                                ShowProtectionSchemeInfo_Text(scheme_type, schi, verbose);
                            } else {
                                if (schm == null) Printf("WARNING: schm atom is NULL\n");
                                if (schi == null) Printf("WARNING: schi atom is NULL\n");
                            }
                        }
                    }
                }
                ++index;
            }
            sinf_entries.Clear();
        }

        /*----------------------------------------------------------------------
        |   ShowSampleLayout
        +---------------------------------------------------------------------*/
        private static void ShowSampleLayout(List<Track> tracks, bool verbose)
        {
            uint[] cursors = new uint[tracks.Count];
            for (int i=0; i<tracks.Count; i++) {
                cursors[i] = 0;
            }

            Sample sample         = new Sample();
            ulong  sample_offset  = 0;
            uint   sample_size    = 0;
            ulong  sample_dts     = 0;
            bool   sample_is_sync = false;
            bool   indicator      = true;
            Track  previous_track = null;
            for (uint i=0;;i++) {
                ulong  min_offset = unchecked((ulong)(-1));
                int    chosen_index = -1;
                Track  chosen_track = null;
                int    index = 0;
                foreach (Track track in tracks) {
                     Ap4Result result = track.GetSample(cursors[index], sample);
                     if (result.Succeeded()) {
                        if (sample.GetOffset() < min_offset) {
                            chosen_index   = index;
                            chosen_track   = track;
                            sample_offset  = sample.GetOffset();
                            sample_size    = sample.GetSize();
                            sample_dts     = sample.GetDts();
                            sample_is_sync = sample.IsSync();
                            min_offset     = sample_offset;
                        }
                     }
                     index++;
                }

                // stop if we've exhausted all samples
                if (chosen_index == -1) break;

                // update the cursor for the chosen track
                cursors[chosen_index] = cursors[chosen_index]+1;

                // see if we've changed tracks
                if (previous_track != chosen_track) {
                    previous_track = chosen_track;
                    indicator = !indicator;
                }

                // show the chosen track/sample
                char track_type = ' ';
                switch (chosen_track.GetType()) {
                    case Track.Type.TYPE_AUDIO:     track_type = 'A'; break;
                    case Track.Type.TYPE_VIDEO:     track_type = 'V'; break;
                    case Track.Type.TYPE_HINT:      track_type = 'H'; break;
                    case Track.Type.TYPE_TEXT:      track_type = 'T'; break;
                    case Track.Type.TYPE_SYSTEM:    track_type = 'S'; break;
                    case Track.Type.TYPE_SUBTITLES: track_type = 'U'; break;
                    default:                        track_type = ' '; break;
                }
                ulong sample_dts_ms = Ap4Utils.ConvertTime(sample_dts, chosen_track.GetMediaTimeScale(), 1000);
                Printf((indicator?"|":" ") + " " +
                       Pad0(D(i), 8) + " [" +
                       track_type + "] (" +
                       D(chosen_track.GetId()) + ")" +
                       (sample_is_sync?"*":" ") + " size=" +
                       Pad(D(sample_size), 6) + ", offset=" +
                       Pad(LLD(sample_offset), 8) + ", dts=" +
                       LLD(sample_dts) + " (" +
                       LLD(sample_dts_ms) + " ms)\n");
            }
        }

        /*----------------------------------------------------------------------
        |   ShowFragments_Text
        +---------------------------------------------------------------------*/
        private static void ShowFragments_Text(Movie movie, bool verbose, bool show_sample_data, ByteStream stream)
        {
            stream.Seek(0);
            LinearReader reader = new LinearReader(movie, stream);
            List<uint> counters = new List<uint>();
            foreach (Track track_item in movie.GetTracks()) {
                reader.EnableTrack(track_item.GetId());
                counters.Add(0);
            }

            Sample     sample = new Sample();
            DataBuffer sample_data = new DataBuffer();
            uint       prev_track_id = 0;
            int        counter = 0; // PORT NOTE: `unsigned int* counter = &counters[0]` upstream; an index in the port
            for (;;) {
                uint track_id = 0;
                Ap4Result result = reader.ReadNextSample(sample, sample_data, out track_id);
                if (result.Succeeded()) {
                    // PORT NOTE: upstream dereferences the track without a NULL check
                    Track track = movie.GetTrack(track_id);
                    if (track == null || counters.Count == 0) break;

                    SampleDescription sample_desc = track.GetSampleDescription(sample.GetDescriptionIndex());
                    AvcSampleDescription avc_desc = sample_desc as AvcSampleDescription;

                    if (track_id != prev_track_id) {
                        Printf("Track " + D(track_id) + ":\n");
                        prev_track_id = track_id;

                        // find the right counter for this track
                        int counter_index = 0;
                        foreach (Track track_item in movie.GetTracks()) {
                            if (track_item.GetId() == track_id) {
                                if (counter_index < counters.Count) {
                                    counter = counter_index;
                                }
                                break;
                            }
                            ++counter_index;
                        }
                    }

                    ShowSample_Text(track, sample, sample_data, counters[counter], verbose, show_sample_data, avc_desc);
                    Printf("\n");

                    ++counters[counter];
                } else {
                    break;
                }
            }
        }

        /*----------------------------------------------------------------------
        |   main
        +---------------------------------------------------------------------*/
        public static int Main(string[] args)
        {
            if (args.Length < 1) {
                return PrintUsageAndExit();
            }
            Options.format = OutputFormat.TEXT_FORMAT;
            string  filename         = null;
            bool    verbose          = false;
            bool    show_samples     = false;
            bool    show_sample_data = false;
            bool    show_layout      = false;
            bool    fast             = false;

            int argi = 0;
            while (argi < args.Length) {
                string arg = args[argi++];
                if (arg == "--verbose") {
                    verbose = true;
                } else if (arg == "--format") {
                    // PORT NOTE: upstream tests `if (argv)`, which is always true, and
                    // crashes on a NULL argument; the port reports the missing argument.
                    if (argi < args.Length) {
                        arg = args[argi++];
                        if (arg == "text") {
                            Options.format = OutputFormat.TEXT_FORMAT;
                        } else if (arg == "json") {
                            Options.format = OutputFormat.JSON_FORMAT;
                        } else {
                            Eprintf("ERROR: unsupported format\n");
                            return 1;
                        }
                    } else {
                        Eprintf("ERROR: missing argument after '--format' option\n");
                        return 1;
                    }

                } else if (arg == "--fast") {
                    fast = true;
                } else if (arg == "--show-samples") {
                    show_samples = true;
                } else if (arg == "--show-sample-data") {
                    show_samples     = true;
                    show_sample_data = true;
                } else if (arg == "--show-layout") {
                    show_layout = true;
                } else {
                    if (filename == null) {
                        filename = arg;
                    } else {
                        Eprintf("ERROR: unexpected argument '" + arg + "'\n");
                        return 1;
                    }
                }
            }
            if (filename == null) {
                Eprintf("ERROR: filename missing\n");
                return 1;
            }

            ByteStream input = null;
            Ap4Result result = FileByteStream.Create(filename,
                                                     FileByteStream.Mode.STREAM_MODE_READ,
                                                     out input);
            if (result.Failed() || input == null) {
                Eprintf("ERROR: cannot open input file " + filename + " (" + D((int)result) + ")\n");
                return 1;
            }

            if (Options.format == OutputFormat.JSON_FORMAT) Printf("{\n");

            File file = new File(input, true);
            ShowFileInfo(file);

            Movie movie = file.GetMovie();
            FtypAtom ftyp = file.GetFileType();
            if (movie != null) {
                ShowMovieInfo(movie);

                List<Track> tracks = movie.GetTracks();
                if (Options.format == OutputFormat.TEXT_FORMAT) {
                    Printf("Found " + D(tracks.Count) + " Tracks\n");
                }

                if (ftyp != null && ftyp.GetMajorBrand() == Ap4.MARLIN_BRAND_MGSV) {
                    ShowMarlinTracks(file, input, tracks, show_samples, show_sample_data, verbose, fast);
                } else {
                    ShowTracks(movie, tracks, input, show_samples, show_sample_data, verbose, fast);
                }

                if (show_layout) {
                    ShowSampleLayout(tracks, verbose);
                }

                if (movie.HasFragments() && show_samples) {
                    if (Options.format == OutputFormat.TEXT_FORMAT) {
                        ShowFragments_Text(movie, verbose, show_sample_data, input);
                    }
                }
            } else {
                // check if this is a DCF file
                if (ftyp != null && ftyp.GetMajorBrand() == Ap4.OMA_DCF_BRAND_ODCF) {
                    ShowDcfInfo(file);
                } else {
                    if (Options.format == OutputFormat.TEXT_FORMAT) {
                        Printf("No movie found in the file\n");
                    } else {
                        Printf("\"movie\":{}\n");
                    }
                }
            }

            if (Options.format == OutputFormat.JSON_FORMAT) Printf("}\n");

            if (input is IDisposable) ((IDisposable)input).Dispose();
            FlushStdout();

            return 0;
        }
    }
}
