// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4CommonEncryption.h, Ap4CommonEncryption.cpp
// Original work Copyright 2002-2017 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

// PORT NOTE: multiple inheritance scheme.
//
// Upstream mixes two helper base classes into atoms:
//     class AP4_TencAtom                 : public AP4_Atom,     public AP4_CencTrackEncryption
//     class AP4_PiffTrackEncryptionAtom  : public AP4_UuidAtom, public AP4_CencTrackEncryption
//     class AP4_SencAtom                 : public AP4_Atom,     public AP4_CencSampleEncryption
//     class AP4_PiffSampleEncryptionAtom : public AP4_UuidAtom, public AP4_CencSampleEncryption
//
// C# has no multiple inheritance, so in the port:
//   * `ICencTrackEncryption` / `ICencSampleEncryption` are interfaces that
//     declare every public method of the upstream mix-in classes;
//   * `CencTrackEncryption` / `CencSampleEncryption` are concrete classes that
//     hold the state and the logic of the upstream mix-ins and implement those
//     interfaces (their constructors are public here; upstream they are
//     protected because only the derived atoms call them);
//   * each of the four atoms derives from `Atom`/`UuidAtom`, HOLDS one such
//     object (`_trackEncryption` / `_sampleEncryption`, exposed with
//     `GetTrackEncryption()` / `GetSampleEncryption()`), and implements the
//     interface by forwarding every method to it.
//
// Mechanical translation rules that follow from this:
//   AP4_CencSampleEncryption* x                    -> ICencSampleEncryption? x
//   AP4_CencTrackEncryption*  x                    -> ICencTrackEncryption?  x
//   AP4_DYNAMIC_CAST(AP4_CencSampleEncryption, a)  -> a as ICencSampleEncryption
//   AP4_DYNAMIC_CAST(AP4_CencTrackEncryption, a)   -> a as ICencTrackEncryption
//   AP4_DYNAMIC_CAST(AP4_SencAtom, a)              -> a as SencAtom   (unchanged)
//   x->GetOuter()                                  -> x.GetOuter()    (the atom itself)
//   tenc->GetDefaultKid(), senc->AddSampleInfo(..) -> same calls, on the atom or on the interface
//
// Other PORT NOTEs that apply to the whole file:
//   * pointers into buffers (`const AP4_UI08* in`, `in += n`) are ported as
//     the backing `byte[]` plus an integer position;
//   * `const AP4_UI08* iv` / `kid` parameters are `byte[]` (16 bytes unless
//     stated otherwise);
//   * `delete x` and destructors that only free memory are dropped;
//   * the classes that upstream declares in the .cpp file only
//     (AP4_CencTrackEncrypter, AP4_CencFragmentEncrypter, AP4_CencTrackDecrypter,
//     AP4_CencFragmentDecrypter) are `internal` classes here.

using System;
using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint PROTECTION_SCHEME_TYPE_CENC = 0x63656e63; // 'cenc'
        public const uint PROTECTION_SCHEME_TYPE_CENS = 0x63656e73; // 'cens'
        public const uint PROTECTION_SCHEME_TYPE_CBC1 = 0x63626331; // 'cbc1'
        public const uint PROTECTION_SCHEME_TYPE_CBCS = 0x63626373; // 'cbcs'
        public const uint PROTECTION_SCHEME_VERSION_CENC_10 = 0x00010000;

        public const uint CENC_CIPHER_NONE         = 0;
        public const uint CENC_CIPHER_AES_128_CTR  = 1;
        public const uint CENC_CIPHER_AES_128_CBC  = 2;

        public const uint CENC_SAMPLE_ENCRYPTION_FLAG_OVERRIDE_TRACK_ENCRYPTION_DEFAULTS = 1;
        public const uint CENC_SAMPLE_ENCRYPTION_FLAG_USE_SUB_SAMPLE_ENCRYPTION          = 2;

        // Ap4CommonEncryption.cpp
        public static readonly byte[] EME_COMMON_SYSTEM_ID = new byte[16] {
            0x10, 0x77, 0xEF, 0xEC, 0xC0, 0xB2, 0x4D, 0x02, 0xAC, 0xE3, 0x3C, 0x1E, 0x52, 0xE2, 0xFB, 0x4B
        };

        public const uint CENC_NAL_UNIT_ENCRYPTION_MIN_SIZE = 112;
    }

    public enum CencVariant {
        CENC_VARIANT_PIFF_CTR,
        CENC_VARIANT_PIFF_CBC,
        CENC_VARIANT_MPEG_CENC,
        CENC_VARIANT_MPEG_CBC1,
        CENC_VARIANT_MPEG_CENS,
        CENC_VARIANT_MPEG_CBCS
    }

    /*----------------------------------------------------------------------
    |   ICencTrackEncryption
    +---------------------------------------------------------------------*/
    // PORT NOTE: see the note at the top of this file. This interface plays the
    // role of the upstream AP4_CencTrackEncryption base class in dynamic casts.
    public interface ICencTrackEncryption
    {
        // methods
        Ap4Result Parse(ByteStream stream);
        Ap4Result DoInspectFields(AtomInspector inspector);
        Ap4Result DoWriteFields(ByteStream stream);

        // accessors
        uint   GetDefaultIsProtected();
        byte   GetDefaultPerSampleIvSize();
        byte   GetDefaultConstantIvSize();
        byte[] GetDefaultConstantIv();
        byte[] GetDefaultKid();
        byte   GetDefaultCryptByteBlock();
        byte   GetDefaultSkipByteBlock();
    }

    /*----------------------------------------------------------------------
    |   ICencSampleEncryption
    +---------------------------------------------------------------------*/
    // PORT NOTE: see the note at the top of this file. This interface plays the
    // role of the upstream AP4_CencSampleEncryption base class in dynamic casts.
    public interface ICencSampleEncryption
    {
        // methods
        Ap4Result DoInspectFields(AtomInspector inspector);
        Ap4Result DoWriteFields(ByteStream stream);

        // accessors
        Atom      GetOuter();
        uint      GetAlgorithmId();
        byte      GetPerSampleIvSize();
        Ap4Result SetPerSampleIvSize(byte iv_size);
        byte      GetConstantIvSize();
        Ap4Result SetConstantIvSize(byte iv_size);
        byte      GetCryptByteBlock();
        byte      GetSkipByteBlock();
        byte[]    GetConstantIv();
        byte[]    GetKid();
        uint      GetSampleInfoCount();
        Ap4Result AddSampleInfo(byte[] iv, DataBuffer subsample_info);
        Ap4Result SetSampleInfosSize(uint size);
        Ap4Result CreateSampleInfoTable(byte                     flags,
                                        byte                     default_crypt_byte_block,
                                        byte                     default_skip_byte_block,
                                        byte                     default_per_sample_iv_size,
                                        byte                     default_constant_iv_size,
                                        byte[]                   default_constant_iv,
                                        out CencSampleInfoTable  table);
    }

    /*----------------------------------------------------------------------
    |   AP4_CencTrackEncryption
    +---------------------------------------------------------------------*/
    public class CencTrackEncryption : ICencTrackEncryption
    {
        // accessors
        public uint   GetDefaultIsProtected()     { return _defaultIsProtected;     }
        public byte   GetDefaultPerSampleIvSize() { return _defaultPerSampleIvSize; }
        public byte   GetDefaultConstantIvSize()  { return _defaultConstantIvSize;  }
        public byte[] GetDefaultConstantIv()      { return _defaultConstantIv;      }
        public byte[] GetDefaultKid()             { return _defaultKid;             }
        public byte   GetDefaultCryptByteBlock()  { return _defaultCryptByteBlock;  }
        public byte   GetDefaultSkipByteBlock()   { return _defaultSkipByteBlock;   }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_CencTrackEncryption::AP4_CencTrackEncryption
        +---------------------------------------------------------------------*/
        public CencTrackEncryption(byte version)
        {
            _version_               = version;
            _defaultIsProtected     = 0;
            _defaultPerSampleIvSize = 0;
            _defaultConstantIvSize  = 0;
            _defaultCryptByteBlock  = 0;
            _defaultSkipByteBlock   = 0;
            Array.Clear(_defaultKid, 0, 16);
            Array.Clear(_defaultConstantIv, 0, 16);
        }

        /*----------------------------------------------------------------------
        |   AP4_CencTrackEncryption::AP4_CencTrackEncryption
        +---------------------------------------------------------------------*/
        public CencTrackEncryption(byte    version,
                                   byte    default_is_protected,
                                   byte    default_per_sample_iv_size,
                                   byte[]  default_kid,
                                   byte    default_constant_iv_size = 0,
                                   byte[]  default_constant_iv = null,
                                   byte    default_crypt_byte_block = 0,
                                   byte    default_skip_byte_block = 0)
        {
            _version_               = version;
            _defaultIsProtected     = default_is_protected;
            _defaultPerSampleIvSize = default_per_sample_iv_size;
            _defaultConstantIvSize  = default_constant_iv_size;
            _defaultCryptByteBlock  = default_crypt_byte_block;
            _defaultSkipByteBlock   = default_skip_byte_block;

            Array.Copy(default_kid, 0, _defaultKid, 0, 16);
            Array.Clear(_defaultConstantIv, 0, 16);
            if (default_per_sample_iv_size == 0 && default_constant_iv_size != 0 && default_constant_iv != null) {
                if (default_constant_iv_size > 16) {
                    // too large, truncate
                    default_constant_iv_size = 16;
                }
                Array.Copy(default_constant_iv, 0, _defaultConstantIv, 16-default_constant_iv_size, default_constant_iv_size);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_CencTrackEncryption::Parse
        +---------------------------------------------------------------------*/
        public Ap4Result Parse(ByteStream stream)
        {
            byte reserved;
            Ap4Result result = stream.ReadUI08(out reserved);
            if (result.Failed()) return result;
            if (_version_ == 0) {
                result = stream.ReadUI08(out reserved);
                if (result.Failed()) return result;
            } else {
                byte blocks;
                result = stream.ReadUI08(out blocks);
                if (result.Failed()) return result;
                _defaultCryptByteBlock = (byte)((blocks >> 4) & 0xF);
                _defaultSkipByteBlock  = (byte)((blocks     ) & 0xF);
            }
            result = stream.ReadUI08(out _defaultIsProtected);
            if (result.Failed()) return result;
            result = stream.ReadUI08(out _defaultPerSampleIvSize);
            if (result.Failed()) return result;
            Array.Clear(_defaultKid, 0, 16);
            result = stream.Read(_defaultKid, 16);
            if (result.Failed()) return result;
            if (_defaultPerSampleIvSize == 0) {
                result = stream.ReadUI08(out _defaultConstantIvSize);
                if (result.Failed()) return result;
                if (_defaultConstantIvSize > 16) {
                    _defaultConstantIvSize = 0;
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                Array.Clear(_defaultConstantIv, 0, 16);
                result = stream.Read(_defaultConstantIv, _defaultConstantIvSize);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencTrackEncryption::DoInspectFields
        +---------------------------------------------------------------------*/
        public Ap4Result DoInspectFields(AtomInspector inspector)
        {
            // the spelling of these fields is inconsistent, but that's how it appears in the spec!
            inspector.AddField("default_isProtected",        _defaultIsProtected);
            inspector.AddField("default_Per_Sample_IV_Size", _defaultPerSampleIvSize);
            inspector.AddField("default_KID",                _defaultKid, 16);
            if (_version_ >= 1) {
                inspector.AddField("default_crypt_byte_block", _defaultCryptByteBlock);
                inspector.AddField("default_skip_byte_block", _defaultSkipByteBlock);
            }
            if (_defaultPerSampleIvSize == 0) {
                inspector.AddField("default_constant_IV_size", _defaultConstantIvSize);
                if (_defaultConstantIvSize <= 16) {
                    inspector.AddField("default_constant_IV", _defaultConstantIv, (uint)_defaultConstantIvSize);
                }
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencTrackEncryption::DoWriteFields
        +---------------------------------------------------------------------*/
        public Ap4Result DoWriteFields(ByteStream stream)
        {
            Ap4Result result;

            // write the fields
            result = stream.WriteUI08(0); // reserved
            if (result.Failed()) return result;
            if (_version_ == 0) {
                result = stream.WriteUI08(0); // reserved
                if (result.Failed()) return result;
            } else {
                result = stream.WriteUI08((byte)(_defaultCryptByteBlock<<4 | _defaultSkipByteBlock));
                if (result.Failed()) return result;
            }
            result = stream.WriteUI08(_defaultIsProtected);
            if (result.Failed()) return result;
            result = stream.WriteUI08(_defaultPerSampleIvSize);
            if (result.Failed()) return result;
            result = stream.Write(_defaultKid, 16);
            if (result.Failed()) return result;
            if (_defaultPerSampleIvSize == 0) {
                result = stream.WriteUI08(_defaultConstantIvSize);
                if (result.Failed()) return result;
                result = stream.Write(_defaultConstantIv, _defaultConstantIvSize <= 16 ? (uint)_defaultConstantIvSize : 16u);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private byte   _version_; // cannot be called m_Version because it would conflict with AP4_Atom::m_Version
        private byte   _defaultIsProtected;
        private byte   _defaultPerSampleIvSize;
        private byte   _defaultConstantIvSize;
        private byte[] _defaultConstantIv = new byte[16];
        private byte[] _defaultKid = new byte[16];
        private byte   _defaultCryptByteBlock;
        private byte   _defaultSkipByteBlock;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencSampleEncryption
    +---------------------------------------------------------------------*/
    public class CencSampleEncryption : ICencSampleEncryption
    {
        // accessors
        public Atom   GetOuter()              { return _outer;           }
        public uint   GetAlgorithmId()        { return _algorithmId;     }
        public byte   GetPerSampleIvSize()    { return _perSampleIvSize; }
        // PORT NOTE: SetPerSampleIvSize and SetConstantIvSize are declared but not
        // implemented upstream (nothing calls them); they are simple setters here.
        public Ap4Result SetPerSampleIvSize(byte iv_size) { _perSampleIvSize = iv_size; return Ap4Result.SUCCESS; }
        public byte   GetConstantIvSize()     { return _constantIvSize;  }
        public Ap4Result SetConstantIvSize(byte iv_size) { _constantIvSize = iv_size; return Ap4Result.SUCCESS; }
        public byte   GetCryptByteBlock()     { return _cryptByteBlock;  }
        public byte   GetSkipByteBlock()      { return _skipByteBlock;   }
        public byte[] GetConstantIv()         { return _constantIv;      }
        public byte[] GetKid()                { return _kid;             }
        public uint   GetSampleInfoCount()    { return _sampleInfoCount; }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_CencSampleEncryption::AP4_CencSampleEncryption
        +---------------------------------------------------------------------*/
        public CencSampleEncryption(Atom       outer,
                                    uint       size,
                                    ByteStream stream)
        {
            _outer            = outer;
            _constantIvSize   = 0;
            _cryptByteBlock   = 0;
            _skipByteBlock    = 0;
            _sampleInfoCursor = 0;

            Array.Clear(_constantIv, 0, 16);

            if ((outer.GetFlags() & Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_OVERRIDE_TRACK_ENCRYPTION_DEFAULTS) != 0) {
                stream.ReadUI24(out _algorithmId);
                stream.ReadUI08(out _perSampleIvSize);
                stream.Read    (_kid, 16);
            } else {
                _algorithmId     = 0;
                _perSampleIvSize = 0;
                Array.Clear(_kid, 0, 16);
            }

            stream.ReadUI32(out _sampleInfoCount);

            /* Prevent the payload_size from becoming negative to avoid overflow */
            uint payload_size = 0;
            if (size > (_outer.GetHeaderSize() + 4))
                payload_size = size - _outer.GetHeaderSize() - 4;

            _sampleInfos.SetDataSize(payload_size);
            stream.Read(_sampleInfos.UseData(), payload_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleEncryption::AP4_CencSampleEncryption
        +---------------------------------------------------------------------*/
        public CencSampleEncryption(Atom    outer,
                                    byte    per_sample_iv_size,
                                    byte    constant_iv_size = 0,
                                    byte[]  constant_iv = null,
                                    byte    crypt_byte_block = 0,
                                    byte    skip_byte_block = 0)
        {
            _outer            = outer;
            _algorithmId      = 0;
            _perSampleIvSize  = per_sample_iv_size;
            _constantIvSize   = constant_iv_size;
            _cryptByteBlock   = crypt_byte_block;
            _skipByteBlock    = skip_byte_block;
            _sampleInfoCount  = 0;
            _sampleInfoCursor = 0;

            Array.Clear(_constantIv, 0, 16);
            if (constant_iv_size <= 16 && constant_iv != null) {
                Array.Copy(constant_iv, 0, _constantIv, 0, _constantIvSize);
            }
            Array.Clear(_kid, 0, 16);
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleEncryption::AP4_CencSampleEncryption
        +---------------------------------------------------------------------*/
        public CencSampleEncryption(Atom   outer,
                                    uint   algorithm_id,
                                    byte   per_sample_iv_size,
                                    byte[] kid)
        {
            _outer            = outer;
            _algorithmId      = algorithm_id;
            _perSampleIvSize  = per_sample_iv_size;
            _constantIvSize   = 0;
            _cryptByteBlock   = 0;
            _skipByteBlock    = 0;
            _sampleInfoCount  = 0;
            _sampleInfoCursor = 0;

            Array.Clear(_constantIv, 0, 16);
            Array.Copy(kid, 0, _kid, 0, 16);
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleEncryption::AddSampleInfo
        +---------------------------------------------------------------------*/
        public Ap4Result AddSampleInfo(byte[]     iv,
                                       DataBuffer subsample_info)
        {
            uint added_size = _perSampleIvSize+subsample_info.GetDataSize();

            if (_sampleInfoCursor+added_size > _sampleInfos.GetDataSize()) {
                // too much data!
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }
            byte[] info = _sampleInfos.UseData();
            int    info_pos = (int)_sampleInfoCursor;
            if (_perSampleIvSize != 0) {
                Array.Copy(iv, 0, info, info_pos, _perSampleIvSize);
            }
            if (subsample_info.GetDataSize() != 0) {
                Array.Copy(subsample_info.GetData(), 0, info, info_pos+_perSampleIvSize, (int)subsample_info.GetDataSize());
            }
            _sampleInfoCursor += added_size;
            ++_sampleInfoCount;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleEncryption::SetSampleInfosSize
        +---------------------------------------------------------------------*/
        public Ap4Result SetSampleInfosSize(uint size)
        {
            _sampleInfos.SetDataSize(size);
            Array.Clear(_sampleInfos.UseData(), 0, (int)size);
            if ((_outer.GetFlags() & Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_OVERRIDE_TRACK_ENCRYPTION_DEFAULTS) != 0) {
                _outer.SetSize(_outer.GetHeaderSize()+20+4+size);
            } else {
                _outer.SetSize(_outer.GetHeaderSize()+4+size);
            }
            if (_outer.GetParent() != null) {
                // PORT NOTE: upstream is AP4_DYNAMIC_CAST(AP4_AtomParent, m_Outer.GetParent());
                // every atom is an AtomParent in this port, so the cast is to ContainerAtom
                // (see docs/FOUNDATION_API.md section 1).
                ContainerAtom parent = _outer.GetParent() as ContainerAtom;
                if (parent != null) {
                    parent.OnChildChanged(_outer);
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleEncryption::CreateSampleInfoTable
        +---------------------------------------------------------------------*/
        public Ap4Result CreateSampleInfoTable(byte                     flags,
                                               byte                     default_crypt_byte_block,
                                               byte                     default_skip_byte_block,
                                               byte                     default_per_sample_iv_size,
                                               byte                     default_constant_iv_size,
                                               byte[]                   default_constant_iv,
                                               out CencSampleInfoTable  table)
        {
            // default return value
            table = null;

            // check if some values are overridden
            uint per_sample_iv_size = default_per_sample_iv_size;
            if ((_outer.GetFlags() & Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_OVERRIDE_TRACK_ENCRYPTION_DEFAULTS) != 0) {
                per_sample_iv_size = _perSampleIvSize;
            }
            bool has_subsamples = false;
            if ((_outer.GetFlags() & Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_USE_SUB_SAMPLE_ENCRYPTION) != 0) {
                has_subsamples = true;
            }

            // check some of the parameters
            if (per_sample_iv_size == 0 || _sampleInfoCount == 0) {
                if (default_constant_iv_size == 0 || default_constant_iv == null) {
                    return Ap4Result.ERROR_INVALID_PARAMETERS;
                }
            }

            // create the table
            Ap4Result result = Ap4Result.ERROR_INVALID_FORMAT;
            table = new CencSampleInfoTable(flags,
                                            default_crypt_byte_block,
                                            default_skip_byte_block,
                                            _sampleInfoCount,
                                            (byte)(per_sample_iv_size != 0 ? per_sample_iv_size : default_constant_iv_size));

            // fill the table
            if (_sampleInfoCount == 0) {
                // special case, this means all samples are encrypted with the same IV
                table.SetIv(0, default_constant_iv);
            } else {
                byte[] data      = _sampleInfos.GetData();
                int    data_pos  = 0;
                uint   data_size = _sampleInfos.GetDataSize();
                for (uint i=0; i<_sampleInfoCount; i++) {
                    if (per_sample_iv_size != 0) {
                        if (data_size < per_sample_iv_size) goto end;
                        table.SetIv(i, data, data_pos);
                        data_pos  += (int)per_sample_iv_size;
                        data_size -= per_sample_iv_size;
                    } else {
                        table.SetIv(i, default_constant_iv);
                    }
                    if (has_subsamples) {
                        if (data_size < 2) goto end;
                        ushort subsample_count = Ap4Utils.BytesToUInt16BE(data, data_pos);
                        data_pos  += 2;
                        data_size -= 2;

                        if (data_size < subsample_count*(uint)6) goto end;

                        result = table.AddSubSampleData(subsample_count, data, data_pos);
                        if (result.Failed()) goto end;

                        data_pos  += 6*subsample_count;
                        data_size -= (uint)(6*subsample_count);
                    }
                }
            }

            // done
            result = Ap4Result.SUCCESS;

        end:
            if (result.Failed()) {
                table = null;
            }
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleEncryption::DoInspectFields
        +---------------------------------------------------------------------*/
        public Ap4Result DoInspectFields(AtomInspector inspector)
        {
            if ((_outer.GetFlags() & Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_OVERRIDE_TRACK_ENCRYPTION_DEFAULTS) != 0) {
                inspector.AddField("AlgorithmID", _algorithmId);
                inspector.AddField("IV_size",     _perSampleIvSize);
                inspector.AddField("KID",         _kid, 16);
            }

            inspector.AddField("sample info count", _sampleInfoCount);

            if (inspector.GetVerbosity() < 2) {
                return Ap4Result.SUCCESS;
            }

            // since we don't know the IV size necessarily (we don't have the context), we
            // will try to guess the IV size (we'll try 16 and 8)
            uint iv_size = _perSampleIvSize;
            if (iv_size == 0) {
                if ((_outer.GetFlags() & Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_USE_SUB_SAMPLE_ENCRYPTION) != 0) {
                    bool data_ok = false;
                    for (uint k=0; k<=2 && !data_ok; k++) {
                        data_ok = true;
                        iv_size = 8*k;
                        byte[] info_data = _sampleInfos.GetData();
                        uint   info_pos  = 0;
                        uint   info_size = _sampleInfos.GetDataSize();
                        for (uint i=0; i<_sampleInfoCount; i++) {
                            if (info_size < iv_size+2) {
                                data_ok = false;
                                break;
                            }
                            info_pos  += iv_size;
                            info_size -= iv_size;
                            uint num_entries = unchecked((uint)(int)Ap4Utils.BytesToInt16BE(info_data, (int)info_pos));
                            info_pos  += 2;
                            info_size -= 2;
                            if (info_size < num_entries*6) {
                                data_ok = false;
                                break;
                            }
                            info_pos  += num_entries*6;
                            info_size -= num_entries*6;
                        }
                    }
                    if (data_ok == false) return Ap4Result.SUCCESS;
                } else {
                    iv_size = _sampleInfoCount != 0 ? (_sampleInfos.GetDataSize()/_sampleInfoCount) : 0;
                    if (iv_size*_sampleInfoCount != _sampleInfos.GetDataSize()) {
                        // not a multiple...
                        return Ap4Result.SUCCESS;
                    }
                }
            }
            inspector.AddField("IV Size (inferred)", iv_size);

            inspector.StartArray("sample info entries", _sampleInfoCount);
            {
                // PORT NOTE: upstream walks the sample infos with a raw pointer and no
                // bounds checks (when the IV size comes from the atom itself, or when
                // sample_info_count does not match the payload, it reads past the end
                // of the buffer). The port stops listing entries where the data ends.
                byte[] info      = _sampleInfos.GetData();
                uint   info_pos  = 0;
                uint   info_size = _sampleInfos.GetDataSize();
                for (uint i=0; i<_sampleInfoCount; i++) {
                    if ((ulong)info_pos+iv_size > info_size) break;
                    inspector.StartObject(null);
                    inspector.AddField("info", info, (int)info_pos, iv_size);
                    info_pos += iv_size;
                    if ((_outer.GetFlags() & Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_USE_SUB_SAMPLE_ENCRYPTION) != 0) {
                        if ((ulong)info_pos+2 > info_size) {
                            inspector.EndObject();
                            break;
                        }
                        uint num_entries = unchecked((uint)(int)Ap4Utils.BytesToInt16BE(info, (int)info_pos));
                        info_pos += 2;
                        inspector.StartArray("sub entries", num_entries);
                        for (uint j=0; j<num_entries; j++) {
                            if ((ulong)info_pos+6 > info_size) break;
                            inspector.StartObject(null, 2, true);
                            uint bocd = Ap4Utils.BytesToUInt16BE(info, (int)info_pos);
                            inspector.AddField("bytes_of_clear_data", bocd);
                            uint boed = Ap4Utils.BytesToUInt32BE(info, (int)info_pos+2);
                            inspector.AddField("bytes_of_encrypted_data", boed);
                            info_pos += 6;
                            inspector.EndObject();
                        }
                        inspector.EndArray();
                    }
                    inspector.EndObject();
                }
            }
            inspector.EndArray();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleEncryption::DoWriteFields
        +---------------------------------------------------------------------*/
        public Ap4Result DoWriteFields(ByteStream stream)
        {
            Ap4Result result;

            // optional fields
            if ((_outer.GetFlags() & Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_OVERRIDE_TRACK_ENCRYPTION_DEFAULTS) != 0) {
                result = stream.WriteUI24(_algorithmId);
                if (result.Failed()) return result;
                result = stream.WriteUI08(_perSampleIvSize);
                if (result.Failed()) return result;
                result = stream.Write(_kid, 16);
                if (result.Failed()) return result;
            }

            // IVs
            result = stream.WriteUI32(_sampleInfoCount);
            if (result.Failed()) return result;
            if (_sampleInfos.GetDataSize() != 0) {
                // (as upstream, the result of this write is not checked)
                stream.Write(_sampleInfos.GetData(), _sampleInfos.GetDataSize());
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        // members
        protected Atom       _outer;
        protected uint       _algorithmId;
        protected byte       _perSampleIvSize;
        protected byte       _constantIvSize;
        protected byte[]     _constantIv = new byte[16];
        protected byte       _cryptByteBlock;
        protected byte       _skipByteBlock;
        protected byte[]     _kid = new byte[16];
        protected uint       _sampleInfoCount;
        protected DataBuffer _sampleInfos = new DataBuffer();
        protected uint       _sampleInfoCursor;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencSampleInfoTable
    +---------------------------------------------------------------------*/
    public class CencSampleInfoTable
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(ProtectedSampleDescription sample_description,
                                       ContainerAtom              traf,
                                       out uint                   cipher_type,
                                       out bool                   reset_iv_at_each_subsample,
                                       ByteStream                 aux_info_data,
                                       ulong                      aux_info_data_offset,
                                       out CencSampleInfoTable    sample_info_table)
        {
            SaioAtom saio;
            SaizAtom saiz;
            ICencSampleEncryption sample_encryption_atom;
            return Create(sample_description,
                          traf,
                          out saio,
                          out saiz,
                          out sample_encryption_atom,
                          out cipher_type,
                          out reset_iv_at_each_subsample,
                          aux_info_data,
                          aux_info_data_offset,
                          out sample_info_table);
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(ProtectedSampleDescription sample_description,
                                       ContainerAtom              traf,
                                       out SaioAtom               saio,
                                       out SaizAtom               saiz,
                                       out ICencSampleEncryption  sample_encryption_atom,
                                       out uint                   cipher_type,
                                       out bool                   reset_iv_at_each_subsample,
                                       ByteStream                 aux_info_data,
                                       ulong                      aux_info_data_offset,
                                       out CencSampleInfoTable    sample_info_table)
        {
            // default return values
            saio                       = null;
            saiz                       = null;
            sample_encryption_atom     = null;
            sample_info_table          = null;
            cipher_type                = Ap4.CENC_CIPHER_NONE;
            reset_iv_at_each_subsample = false;

            // get the scheme info atom
            ContainerAtom schi = sample_description.GetSchemeInfo().GetSchiAtom();
            if (schi == null) return Ap4Result.ERROR_INVALID_FORMAT;

            // look for a track encryption atom
            ICencTrackEncryption track_encryption_atom =
                schi.GetChild(AtomType.TENC) as ICencTrackEncryption;
            if (track_encryption_atom == null) {
                track_encryption_atom = schi.GetChild(Ap4.UUID_PIFF_TRACK_ENCRYPTION_ATOM) as ICencTrackEncryption;
            }
            if (track_encryption_atom == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // look for a sample encryption atom
            if (traf != null) {
                sample_encryption_atom = traf.GetChild(AtomType.SENC) as SencAtom;
                if (sample_encryption_atom == null) {
                    sample_encryption_atom = traf.GetChild(Ap4.UUID_PIFF_SAMPLE_ENCRYPTION_ATOM) as PiffSampleEncryptionAtom;
                }
            }

            // check the scheme and compute the cipher
            switch (sample_description.GetSchemeType()) {
              case Ap4.PROTECTION_SCHEME_TYPE_PIFF:
                switch (track_encryption_atom.GetDefaultIsProtected()) {
                  case 0:
                    cipher_type = Ap4.CENC_CIPHER_NONE;
                    break;

                  case 1:
                    cipher_type = Ap4.CENC_CIPHER_AES_128_CTR;
                    break;
                  case 2:
                    cipher_type = Ap4.CENC_CIPHER_AES_128_CBC;
                    break;

                  default:
                    return Ap4Result.ERROR_NOT_SUPPORTED;
                }
                break;

              case Ap4.PROTECTION_SCHEME_TYPE_CENC:
              case Ap4.PROTECTION_SCHEME_TYPE_CENS:
                cipher_type = Ap4.CENC_CIPHER_AES_128_CTR;
                break;

              case Ap4.PROTECTION_SCHEME_TYPE_CBC1:
                cipher_type = Ap4.CENC_CIPHER_AES_128_CBC;
                break;

              case Ap4.PROTECTION_SCHEME_TYPE_CBCS:
                cipher_type = Ap4.CENC_CIPHER_AES_128_CBC;
                reset_iv_at_each_subsample = true;
                break;

              default:
                // not supported
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }
            if (track_encryption_atom.GetDefaultIsProtected() == 0) {
                cipher_type = Ap4.CENC_CIPHER_NONE;
            }

            // parse the crypto params
            byte    per_sample_iv_size = 0;
            byte    constant_iv_size   = 0;
            byte[]  constant_iv        = null;
            byte    crypt_byte_block   = 0;
            byte    skip_byte_block    = 0;
            if (sample_encryption_atom != null &&
                (sample_encryption_atom.GetOuter().GetFlags() & Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_OVERRIDE_TRACK_ENCRYPTION_DEFAULTS) != 0) {

                switch (sample_encryption_atom.GetAlgorithmId()) {
                  case 0:
                    cipher_type = Ap4.CENC_CIPHER_NONE;
                    break;

                  case 1:
                    cipher_type = Ap4.CENC_CIPHER_AES_128_CTR;
                    break;

                  case 2:
                    cipher_type = Ap4.CENC_CIPHER_AES_128_CBC;
                    break;
                }
                per_sample_iv_size = sample_encryption_atom.GetPerSampleIvSize();
            } else {
                per_sample_iv_size = track_encryption_atom.GetDefaultPerSampleIvSize();
                constant_iv_size   = track_encryption_atom.GetDefaultConstantIvSize();
                crypt_byte_block   = track_encryption_atom.GetDefaultCryptByteBlock();
                skip_byte_block    = track_encryption_atom.GetDefaultSkipByteBlock();
                if (constant_iv_size != 0) {
                    constant_iv = track_encryption_atom.GetDefaultConstantIv();
                }
            }

            // try to create a sample info table from saio/saiz
            if (sample_info_table == null && traf != null) {
                foreach (Atom child in traf.GetChildren()) {
                    // PORT NOTE: the null checks on the casts are additions (upstream
                    // dereferences the result of the dynamic cast directly).
                    if (child.GetType() == AtomType.SAIO) {
                        saio = child as SaioAtom;
                        if (saio != null && saio.GetAuxInfoType() != 0 && saio.GetAuxInfoType() != Ap4.PROTECTION_SCHEME_TYPE_CENC) {
                            saio = null;
                        }
                    } else if (child.GetType() == AtomType.SAIZ) {
                        saiz = child as SaizAtom;
                        if (saiz != null && saiz.GetAuxInfoType() != 0 && saiz.GetAuxInfoType() != Ap4.PROTECTION_SCHEME_TYPE_CENC) {
                            saiz = null;
                        }
                    }
                }
                if (saio != null && saiz != null) {
                    Ap4Result result = Create(0,
                                              crypt_byte_block,
                                              skip_byte_block,
                                              per_sample_iv_size,
                                              constant_iv_size,
                                              constant_iv,
                                              traf,
                                              saio,
                                              saiz,
                                              aux_info_data,
                                              aux_info_data_offset,
                                              out sample_info_table);
                    // only abort the processing if the error is not due to an invalid
                    // format: some files have an invalid saio/saiz based construction,
                    // but may still have a valid `senc`, which we can try to parse
                    // below
                    if (!result.Succeeded() && result != Ap4Result.ERROR_INVALID_FORMAT) {
                        return result;
                    }
                }
            }

            // try to create a sample info table from senc
            if (sample_info_table == null && sample_encryption_atom != null) {
                Ap4Result result = sample_encryption_atom.CreateSampleInfoTable(0,
                                                                                crypt_byte_block,
                                                                                skip_byte_block,
                                                                                per_sample_iv_size,
                                                                                constant_iv_size,
                                                                                constant_iv,
                                                                                out sample_info_table);
                if (result.Failed()) return result;
            }

            if (sample_info_table == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(byte                     flags,
                                       byte                     crypt_byte_block,
                                       byte                     skip_byte_block,
                                       byte                     per_sample_iv_size,
                                       byte                     constant_iv_size,
                                       byte[]                   constant_iv,
                                       ContainerAtom            traf,
                                       SaioAtom                 saio,
                                       SaizAtom                 saiz,
                                       ByteStream               aux_info_data,
                                       ulong                    aux_info_data_offset,
                                       out CencSampleInfoTable  sample_info_table)
        {
            Ap4Result result = Ap4Result.SUCCESS;

            // PORT NOTE: upstream leaves the output argument untouched on the early
            // error return below (all callers initialize it to NULL).
            sample_info_table = null;

            // remember where we are in the data stream so that we can return to that point
            // before returning
            ulong position_before = 0;
            aux_info_data.Tell(out position_before);

            // count the samples
            uint sample_info_count = 0;
            foreach (Atom atom in traf.GetChildren()) {
                if (atom.GetType() == AtomType.TRUN) {
                    // PORT NOTE: null check added (upstream dereferences the cast result directly)
                    TrunAtom trun = atom as TrunAtom;
                    if (trun == null) continue;
                    sample_info_count += (uint)trun.GetEntries().Count;
                }
            }

            // create the table
            byte iv_size = per_sample_iv_size;
            if (iv_size == 0) {
                iv_size = constant_iv_size;
                if (iv_size == 0 || constant_iv == null) {
                    return Ap4Result.ERROR_INVALID_PARAMETERS;
                }
            }
            CencSampleInfoTable table = new CencSampleInfoTable(flags,
                                                                crypt_byte_block,
                                                                skip_byte_block,
                                                                sample_info_count,
                                                                iv_size);

            // process each sample's auxiliary info
            uint       saio_index  = 0;
            uint       saiz_index  = 0;
            DataBuffer info = new DataBuffer();
            foreach (Atom atom in traf.GetChildren()) {
                if (atom.GetType() == AtomType.TRUN) {
                    // PORT NOTE: null check added (upstream dereferences the cast result directly)
                    TrunAtom trun = atom as TrunAtom;
                    if (trun == null) continue;

                    if (saio_index == 0) {
                        // PORT NOTE: upstream reads entry 0 without checking that it exists
                        if (saio.GetEntries().Count == 0) {
                            result = Ap4Result.ERROR_INVALID_FORMAT;
                            goto end;
                        }
                        aux_info_data.Seek(aux_info_data_offset+saio.GetEntries()[0]);
                    } else {
                        if (saio.GetEntries().Count > 1) {
                            if (saio_index >= (uint)saio.GetEntries().Count) {
                                result = Ap4Result.ERROR_INVALID_FORMAT;
                                goto end;
                            }
                            aux_info_data.Seek(aux_info_data_offset+saio.GetEntries()[(int)saio_index]);
                        }
                    }
                    ++saio_index;

                    for (int i=0; i<trun.GetEntries().Count; i++) {
                        byte info_size = 0;
                        result = saiz.GetSampleInfoSize(saiz_index, out info_size);
                        if (result.Failed()) goto end;

                        info.SetDataSize(info_size);
                        result = aux_info_data.Read(info.UseData(), info_size);
                        if (result.Failed()) goto end;

                        byte[] info_data = info.GetData();
                        if (per_sample_iv_size != 0) {
                            if (per_sample_iv_size > info_size) {
                                result = Ap4Result.ERROR_INVALID_FORMAT;
                                goto end;
                            }
                            table.SetIv(saiz_index, info_data);
                        } else {
                            table.SetIv(saiz_index, constant_iv);
                        }
                        // Aux info layouts we must accept:
                        //   1) [IV][subsample_count:2][subsample_map:subsample_count*6]
                        //      (subsample encryption, e.g. video with cleartext slice
                        //      headers mixed with encrypted payload)
                        //   2) [IV]                                  (no subsample info)
                        //      (whole-sample encryption, common for audio where every
                        //      byte of the sample is encrypted and there is nothing
                        //      to describe — valid per ISO/IEC 23001-7)
                        ushort subsample_count = 0;
                        if (info_size >= per_sample_iv_size+2) {
                            subsample_count = Ap4Utils.BytesToUInt16BE(info_data, per_sample_iv_size);
                            if (info_size < per_sample_iv_size+2+subsample_count*6) {
                                // not enough data
                                result = Ap4Result.ERROR_INVALID_FORMAT;
                                goto end;
                            }
                            table.AddSubSampleData(subsample_count, info_data, per_sample_iv_size+2);
                        } else if (info_size == per_sample_iv_size) {
                            // IV-only aux info: whole sample is encrypted, no subsamples.
                            table.AddSubSampleData(0, null);
                        } else {
                            result = Ap4Result.ERROR_INVALID_FORMAT;
                            goto end;
                        }
                        saiz_index++;
                    }
                }
            }
            result = Ap4Result.SUCCESS;

        end:
            if (result.Succeeded()) {
                sample_info_table = table;
            } else {
                sample_info_table = null;
            }
            aux_info_data.Seek(position_before);
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::Create
        +---------------------------------------------------------------------*/
        // see note below regarding the serialization format
        public static Ap4Result Create(byte[]                   serialized,
                                       uint                     serialized_size,
                                       out CencSampleInfoTable  sample_info_table)
        {
            sample_info_table = null;

            // basic size check
            if (serialized_size < 4+4) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            int  pos = 0; // PORT NOTE: position in `serialized` (upstream advances the pointer)
            uint sample_count     = Ap4Utils.BytesToUInt32BE(serialized, pos); pos += 4; serialized_size -= 4;
            byte flags            = serialized[pos]; pos += 1; serialized_size -= 1;
            byte crypt_byte_block = serialized[pos]; pos += 1; serialized_size -= 1;
            byte skip_byte_block  = serialized[pos]; pos += 1; serialized_size -= 1;
            byte iv_size          = serialized[pos]; pos += 1; serialized_size -= 1;

            if (serialized_size < sample_count*iv_size) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            // PORT NOTE: upstream does not check that the single IV stored when
            // sample_count is 0 is actually present (it reads past the buffer).
            if (sample_count == 0 && serialized_size < iv_size) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            CencSampleInfoTable table = new CencSampleInfoTable(flags, crypt_byte_block, skip_byte_block, sample_count, (byte)iv_size);
            table._ivData.SetData(serialized, pos, sample_count != 0 ? sample_count*iv_size : iv_size);
            pos             += (int)(sample_count != 0 ? sample_count*iv_size : iv_size);
            serialized_size -= sample_count != 0 ? sample_count*iv_size : iv_size;

            if (serialized_size < 4) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            uint item_count = Ap4Utils.BytesToUInt32BE(serialized, pos); pos += 4; serialized_size -= 4;
            if (serialized_size < item_count*(2+4)) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            SetItemCount(table._bytesOfCleartextData, item_count);
            SetItemCount(table._bytesOfEncryptedData, item_count);
            for (uint i=0; i<item_count; i++) {
                table._bytesOfCleartextData[(int)i] = Ap4Utils.BytesToUInt16BE(serialized, pos);
                pos             += 2;
                serialized_size -= 2;
            }
            for (uint i=0; i<item_count; i++) {
                table._bytesOfEncryptedData[(int)i] = Ap4Utils.BytesToUInt32BE(serialized, pos);
                pos             += 4;
                serialized_size -= 4;
            }

            if (serialized_size < 4) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            bool use_subsamples = Ap4Utils.BytesToUInt32BE(serialized, pos) != 0; pos += 4; serialized_size -= 4;
            if (!use_subsamples) {
                sample_info_table = table;
                return Ap4Result.SUCCESS;
            }

            if (serialized_size < sample_count*(4+4)) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            SetItemCount(table._subSampleMapStarts, sample_count);
            SetItemCount(table._subSampleMapLengths, sample_count);
            for (uint i=0; i<sample_count; i++) {
                table._subSampleMapStarts[(int)i] = Ap4Utils.BytesToUInt32BE(serialized, pos);
                pos             += 4;
                serialized_size -= 4;
            }
            for (uint i=0; i<sample_count; i++) {
                table._subSampleMapLengths[(int)i] = Ap4Utils.BytesToUInt32BE(serialized, pos);
                pos             += 4;
                serialized_size -= 4;
            }

            sample_info_table = table;
            return Ap4Result.SUCCESS;
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::AP4_CencSampleInfoTable
        +---------------------------------------------------------------------*/
        public CencSampleInfoTable(byte flags,
                                   byte crypt_byte_block,
                                   byte skip_byte_block,
                                   uint sample_count,
                                   byte iv_size)
        {
            _sampleCount    = sample_count;
            _flags          = flags;
            _cryptByteBlock = crypt_byte_block;
            _skipByteBlock  = skip_byte_block;
            _ivSize         = iv_size;

            if (sample_count == 0) {
                // All samples encrypted with a constant IV, reserve some space to
                // store the constant IV
                sample_count = 1;
            }
            _ivData.SetDataSize(_ivSize*sample_count);
            Array.Clear(_ivData.UseData(), 0, (int)(_ivSize*sample_count));
        }

        // methods
        public byte GetFlags()          { return _flags;          }
        public byte GetCryptByteBlock() { return _cryptByteBlock; }
        public byte GetSkipByteBlock()  { return _skipByteBlock;  }
        public uint GetSampleCount()    { return _sampleCount;    }
        public byte GetIvSize()         { return _ivSize;         }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::SetIv
        +---------------------------------------------------------------------*/
        public Ap4Result SetIv(uint sample_index, byte[] iv)
        {
            return SetIv(sample_index, iv, 0);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer.
        // GetIvSize() bytes are copied from `iv`; if fewer bytes are available
        // (upstream would read past the end of the source), only those are copied.
        public Ap4Result SetIv(uint sample_index, byte[] iv, int offset)
        {
            if (_sampleCount == 0) {
                if (sample_index != 0) return Ap4Result.ERROR_OUT_OF_RANGE;
            } else {
                if (sample_index >= _sampleCount) return Ap4Result.ERROR_OUT_OF_RANGE;
            }
            Ap4.ASSERT(_ivData.GetDataSize() >= _ivSize*(sample_index+1));
            byte[] dst = _ivData.UseData();
            int    dst_pos = (int)(_ivSize*sample_index);
            int    count = Math.Min((int)_ivSize, iv.Length-offset);
            if (count > 0) {
                Array.Copy(iv, offset, dst, dst_pos, count);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::GetIv
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream returns a pointer into the IV table; the port returns
        // a copy of the GetIvSize() bytes of the IV (null where upstream returns NULL).
        public byte[] GetIv(uint sample_index)
        {
            uint position;
            if (_sampleCount == 0) {
                position = 0;
            } else {
                if (sample_index >= _sampleCount) return null;
                position = _ivSize*sample_index;
            }
            byte[] iv = new byte[_ivSize];
            if ((ulong)position+_ivSize <= _ivData.GetDataSize()) {
                Array.Copy(_ivData.GetData(), (int)position, iv, 0, _ivSize);
            }
            return iv;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::AddSubSampleData
        +---------------------------------------------------------------------*/
        public Ap4Result AddSubSampleData(uint    subsample_count,
                                          byte[]  subsample_data,
                                          int     offset = 0)
        {
            uint current = (uint)_subSampleMapStarts.Count;
            if (current == 0) {
                _subSampleMapStarts.Add(0);
            } else {
                _subSampleMapStarts.Add(_subSampleMapStarts[(int)current-1]+
                                        _subSampleMapLengths[(int)current-1]);
            }
            _subSampleMapLengths.Add(subsample_count);
            for (uint i=0; i<subsample_count; i++) {
                _bytesOfCleartextData.Add(Ap4Utils.BytesToUInt16BE(subsample_data, offset));
                _bytesOfEncryptedData.Add(Ap4Utils.BytesToUInt32BE(subsample_data, offset+2));
                offset += 6;
            }

            return Ap4Result.SUCCESS;
        }

        public bool HasSubSampleInfo() {
            return _subSampleMapStarts.Count != 0;
        }

        public uint GetSubsampleCount(uint sample_index) {
            if (sample_index < _sampleCount) {
                return _subSampleMapLengths[(int)sample_index];
            } else {
                return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::GetSampleInfo
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream returns pointers into the internal arrays; the port
        // returns copies of the `subsample_count` entries of the sample (null where
        // upstream returns NULL). ERROR_OUT_OF_RANGE is returned where upstream
        // would return pointers past the end of its arrays (inconsistent table).
        public Ap4Result GetSampleInfo(uint          sample_index,
                                       out uint      subsample_count,
                                       out ushort[]  bytes_of_cleartext_data,
                                       out uint[]    bytes_of_encrypted_data)
        {
            subsample_count         = 0;
            bytes_of_cleartext_data = null;
            bytes_of_encrypted_data = null;

            if (_sampleCount == 0) {
                // all samples encrypted entirely, no subsamples
                return Ap4Result.SUCCESS;
            }

            if (sample_index >= _sampleCount) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }

            if (_subSampleMapStarts.Count == 0) {
                // no subsamples
                return Ap4Result.SUCCESS;
            }

            if (sample_index >= (uint)_subSampleMapLengths.Count || sample_index >= (uint)_subSampleMapStarts.Count) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }
            uint count  = _subSampleMapLengths[(int)sample_index];
            uint target = _subSampleMapStarts[(int)sample_index];
            if ((ulong)target+count > (ulong)_bytesOfCleartextData.Count ||
                (ulong)target+count > (ulong)_bytesOfEncryptedData.Count) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }
            subsample_count         = count;
            bytes_of_cleartext_data = _bytesOfCleartextData.GetRange((int)target, (int)count).ToArray();
            bytes_of_encrypted_data = _bytesOfEncryptedData.GetRange((int)target, (int)count).ToArray();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::GetSubsampleInfo
        +---------------------------------------------------------------------*/
        public Ap4Result GetSubsampleInfo(uint       sample_index,
                                          uint       subsample_index,
                                          out ushort bytes_of_cleartext_data,
                                          out uint   bytes_of_encrypted_data)
        {
            bytes_of_cleartext_data = 0;
            bytes_of_encrypted_data = 0;

            // PORT NOTE: the checks against the map sizes are additions (upstream indexes
            // m_SubSampleMapLengths without checking that there is subsample info)
            if (sample_index    >= _sampleCount ||
                sample_index    >= (uint)_subSampleMapLengths.Count ||
                subsample_index >= _subSampleMapLengths[(int)sample_index]) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }
            uint target = _subSampleMapStarts[(int)sample_index]+subsample_index;
            if (target >= (uint)_bytesOfCleartextData.Count || target >= (uint)_bytesOfEncryptedData.Count) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }
            bytes_of_cleartext_data = _bytesOfCleartextData[(int)target];
            bytes_of_encrypted_data = _bytesOfEncryptedData[(int)target];

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleInfoTable::Serialize
        +---------------------------------------------------------------------*/
        // see note below regarding the serialization format
        public Ap4Result Serialize(DataBuffer buffer)
        {
            uint size = 4 +
                        4 +
                        (_sampleCount != 0 ? _sampleCount*_ivSize : _ivSize) +
                        4 +
                        (uint)_bytesOfCleartextData.Count*2 +
                        (uint)_bytesOfEncryptedData.Count*4 +
                        4;
            bool use_subsamples = _subSampleMapStarts.Count != 0;
            if (use_subsamples) {
                size += _sampleCount*(4+4);
            }

            // sanity check
            if (_ivData.GetDataSize()       != _sampleCount*_ivSize ||
                _bytesOfCleartextData.Count != _bytesOfEncryptedData.Count ||
                _subSampleMapStarts.Count   != _subSampleMapLengths.Count) {
                return Ap4Result.ERROR_INTERNAL;
            }
            if (use_subsamples && (uint)_subSampleMapStarts.Count != _sampleCount) {
                return Ap4Result.ERROR_INTERNAL;
            }

            buffer.SetDataSize(size);
            byte[] data = buffer.UseData();
            int    pos  = 0;

            Ap4Utils.BytesFromUInt32BE(data, pos, _sampleCount); pos += 4;
            data[pos++] = _flags;
            data[pos++] = _cryptByteBlock;
            data[pos++] = _skipByteBlock;
            data[pos++] = _ivSize;
            if (_sampleCount != 0) {
                Array.Copy(_ivData.GetData(), 0, data, pos, (int)(_sampleCount*_ivSize)); pos += (int)(_sampleCount*_ivSize);
            } else {
                // All samples encrypted, no per-sample IV, create one entry for the default IV
                Array.Copy(_ivData.GetData(), 0, data, pos, _ivSize); pos += _ivSize;
            }
            Ap4Utils.BytesFromUInt32BE(data, pos, (uint)_bytesOfCleartextData.Count);  pos += 4;
            for (int i=0; i<_bytesOfCleartextData.Count; i++) {
                Ap4Utils.BytesFromUInt16BE(data, pos, _bytesOfCleartextData[i]); pos += 2;
            }
            for (int i=0; i<_bytesOfEncryptedData.Count; i++) {
                Ap4Utils.BytesFromUInt32BE(data, pos, _bytesOfEncryptedData[i]); pos += 4;
            }
            Ap4Utils.BytesFromUInt32BE(data, pos, use_subsamples?1u:0u); pos += 4;
            if (use_subsamples) {
                for (uint i=0; i<_sampleCount; i++) {
                    Ap4Utils.BytesFromUInt32BE(data, pos, _subSampleMapStarts[(int)i]); pos += 4;
                }
                for (uint i=0; i<_sampleCount; i++) {
                    Ap4Utils.BytesFromUInt32BE(data, pos, _subSampleMapLengths[(int)i]); pos += 4;
                }
            }
            return Ap4Result.SUCCESS;
        }

        // PORT NOTE: AP4_Array<T>::SetItemCount
        private static void SetItemCount<T>(List<T> list, uint item_count) where T : struct
        {
            if ((uint)list.Count > item_count) {
                list.RemoveRange((int)item_count, list.Count-(int)item_count);
            } else {
                while ((uint)list.Count < item_count) list.Add(default(T));
            }
        }

        private uint         _sampleCount; // If 0, all samples are fully encrypted, and there's a single constant IV in m_IvData
        private byte         _flags;
        private byte         _cryptByteBlock;
        private byte         _skipByteBlock;
        private byte         _ivSize;
        private DataBuffer   _ivData = new DataBuffer();
        private List<ushort> _bytesOfCleartextData = new List<ushort>();
        private List<uint>   _bytesOfEncryptedData = new List<uint>();
        private List<uint>   _subSampleMapStarts = new List<uint>();
        private List<uint>   _subSampleMapLengths = new List<uint>();
    }

    /*----------------------------------------------------------------------
    |   AP4_CencSampleInfoTable serialization format
    |
    |   (All integers are stored in big-endian byte order)
    |
    |   +---------------+----------------+------------------------------------+
    |   | Size          |  Type          |  Description                       |
    |   +---------------+----------------+------------------------------------+
    |
    |   +---------------+----------------+------------------------------------+
    |   | 4 bytes       | 32-bit integer | sample_count                       |
    |   +---------------+----------------+------------------------------------+
    |   | 1 byte        | 8-bit integer  | flags                              |
    |   +---------------+----------------+------------------------------------+
    |   | 1 byte        | 8-bit integer  | crypt_byte_block                   |
    |   +---------------+----------------+------------------------------------+
    |   | 1 byte        | 8-bit integer  | skip_byte_block                    |
    |   +---------------+----------------+------------------------------------+
    |   | 1 byte        | 8-bit integer  | iv_size                            |
    |   +---------------+----------------+------------------------------------+
    |
    |   repeat sample_count times, or once if sample_count is 0:
    |   +---------------+----------------+------------------------------------+
    |   | iv_size bytes | byte array     | IV[i]                              |
    |   +---------------+----------------+------------------------------------+
    |
    |   +---------------+----------------+------------------------------------+
    |   | 4 bytes       | 32-bit integer | entry_count                        |
    |   +---------------+----------------+------------------------------------+
    |
    |   repeat entry_count times:
    |   +---------------+----------------+------------------------------------+
    |   | 2 bytes       | 16-bit integer | bytes_of_cleartext_data[i]         |
    |   +---------------+----------------+------------------------------------+
    |
    |   repeat entry_count times:
    |   +---------------+----------------+------------------------------------+
    |   | 4 bytes       | 32-bit integer | bytes_of_encrypted_data[i]         |
    |   +---------------+----------------+------------------------------------+
    |
    |   +---------------+----------------+------------------------------------+
    |   | 4 bytes       | 32-bit flags   | 1 if subsamples are used, 0 if not |
    |   +---------------+----------------+------------------------------------+
    |
    |   if subsamples are used, repeat sample_count times:
    |   +---------------+----------------+------------------------------------+
    |   | 4 bytes       | 32-bit integer | subsample_map_start[i]             |
    |   +---------------+----------------+------------------------------------+
    |
    |   if subsamples are used, repeat sample_count times:
    |   +---------------+----------------+------------------------------------+
    |   | 4 bytes       | 32-bit integer | subsample_map_length[i]            |
    |   +---------------+----------------+------------------------------------+
    |
    |   NOTES: subsample_map_start[i] ans subsample_map_length[i] are, respectively,
    |   the index and the length the i'th subsample map sequence in the
    |   bytes_of_cleartext_data anb bytes_of_encrypted_data arrays.
    |   For example, if we have:
    |   bytes_of_cleartext_data[] = { 10,   15, 13, 17, 12 }
    |   bytes_of_encrypted_data[] = { 100, 200, 50, 80, 32 }
    |   subsample_map_start  = { 0, 3 }
    |   subsample_map_length = { 3, 2 }
    |   It means that the (bytes_of_cleartext_data, bytes_of_encrypted_data)
    |   sequences for the two subsamples are:
    |   subsample[0] --> [(10,100), (15, 200), (13, 50)]
    |   subsample[1] --> [(17, 80), (12,  32)]
    |
    +---------------------------------------------------------------------*/

    /*----------------------------------------------------------------------
    |   AP4_CencSampleEncrypter
    +---------------------------------------------------------------------*/
    public abstract class CencSampleEncrypter
    {
        // constructor and destructor
        public CencSampleEncrypter(StreamCipher  cipher,
                                   bool          constant_iv)
        {
            _cipher     = cipher;
            _constantIv = constant_iv;
            Array.Clear(_iv, 0, 16);
        }

        // methods
        public abstract Ap4Result EncryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    DataBuffer sample_infos);

        public void           SetIv(byte[] iv) { Array.Copy(iv, 0, _iv, 0, 16); }
        public byte[]         GetIv()          { return _iv;                    }
        public virtual bool   UseSubSamples()  { return false;                  }
        public virtual Ap4Result GetSubSampleMap(DataBuffer   sample_data,
                                                 List<ushort> bytes_of_cleartext_data,
                                                 List<uint>   bytes_of_encrypted_data) {
            return Ap4Result.SUCCESS;
        }

        protected byte[]        _iv = new byte[16];
        protected StreamCipher  _cipher;
        protected bool          _constantIv;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencCtrSampleEncrypter
    +---------------------------------------------------------------------*/
    public class CencCtrSampleEncrypter : CencSampleEncrypter
    {
        // constructor and destructor
        public CencCtrSampleEncrypter(StreamCipher  cipher,
                                      bool          constant_iv,
                                      uint          iv_size) :
            base(cipher, constant_iv)
        {
            _ivSize = iv_size;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencCtrSampleEncrypter::EncryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result EncryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    DataBuffer sample_infos)
        {
            // the output has the same size as the input
            data_out.SetDataSize(data_in.GetDataSize());

            // setup direct pointers to the buffers
            byte[] @in = data_in.GetData();
            byte[] @out = data_out.UseData();

            // setup the IV
            _cipher.SetIV(_iv);

            // process the sample data
            if (data_in.GetDataSize() != 0) {
                uint out_size = data_out.GetDataSize();
                Ap4Result result = _cipher.ProcessBuffer(@in, 0, data_in.GetDataSize(), @out, 0, ref out_size, false);
                if (result.Failed()) return result;
            }

            // update the IV
            if (_ivSize == 16) {
                uint block_count = (data_in.GetDataSize()+15)/16;
                ulong counter = Ap4Utils.BytesToUInt64BE(_iv, 8);
                Ap4Utils.BytesFromUInt64BE(_iv, 8, counter+block_count);
            } else if (_ivSize == 8){
                ulong counter = Ap4Utils.BytesToUInt64BE(_iv, 0);
                Ap4Utils.BytesFromUInt64BE(_iv, 0, counter+1);
            } else {
                return Ap4Result.ERROR_INTERNAL;
            }

            return Ap4Result.SUCCESS;
        }

        protected uint _ivSize;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencCbcSampleEncrypter
    +---------------------------------------------------------------------*/
    public class CencCbcSampleEncrypter : CencSampleEncrypter
    {
        // constructor and destructor
        public CencCbcSampleEncrypter(StreamCipher  cipher,
                                      bool          constant_iv) :
            base(cipher, constant_iv) {}

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencCbcSampleEncrypter::EncryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result EncryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    DataBuffer sample_infos)
        {
            // the output has the same size as the input
            data_out.SetDataSize(data_in.GetDataSize());

            // setup direct pointers to the buffers
            byte[] @in = data_in.GetData();
            byte[] @out = data_out.UseData();
            int    in_pos = 0;
            int    out_pos = 0;

            // setup the IV
            _cipher.SetIV(_iv);

            // process the sample data
            uint block_count = data_in.GetDataSize()/16;
            if (block_count != 0) {
                uint out_size = data_out.GetDataSize();
                Ap4Result result = _cipher.ProcessBuffer(@in, in_pos, block_count*16, @out, out_pos, ref out_size, false);
                if (result.Failed()) return result;
                in_pos  += (int)(block_count*16);
                out_pos += (int)(block_count*16);

                if (!_constantIv) {
                    // update the IV (last cipherblock emitted)
                    Array.Copy(@out, out_pos-16, _iv, 0, 16);
                }
            }

            // any partial block at the end remains in the clear
            uint partial = data_in.GetDataSize()%16;
            if (partial != 0) {
                Array.Copy(@in, in_pos, @out, out_pos, (int)partial);
            }

            return Ap4Result.SUCCESS;
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_CencSubSampleMapper
    +---------------------------------------------------------------------*/
    public abstract class CencSubSampleMapper
    {
        // constructor and destructor
        public CencSubSampleMapper(uint nalu_length_size, uint format)
        {
            _naluLengthSize = nalu_length_size;
            _format         = format;
        }

        // methods
        public abstract Ap4Result GetSubSampleMap(DataBuffer   sample_data,
                                                  List<ushort> bytes_of_cleartext_data,
                                                  List<uint>   bytes_of_encrypted_data);

        /*----------------------------------------------------------------------
        |   AP4_CencSubSampleMapAppend
        +---------------------------------------------------------------------*/
        // PORT NOTE: file-static function AP4_CencSubSampleMapAppendEntry upstream
        protected static void
        CencSubSampleMapAppendEntry(List<ushort> bytes_of_cleartext_data,
                                    List<uint>   bytes_of_encrypted_data,
                                    uint         cleartext_size,
                                    uint         encrypted_size)
        {
            // if there's already an entry, attempt to extend the last entry
            if (bytes_of_cleartext_data.Count > 0) {
                int last_index = bytes_of_cleartext_data.Count - 1;
                if (bytes_of_encrypted_data[last_index] == 0) {
                    cleartext_size += bytes_of_cleartext_data[last_index];
                    bytes_of_cleartext_data.RemoveAt(bytes_of_cleartext_data.Count-1);
                    bytes_of_encrypted_data.RemoveAt(bytes_of_encrypted_data.Count-1);
                }
            }

            // append chunks of cleartext size taking into account that the cleartext_size field is only 16 bits
            while (cleartext_size > 0xFFFF) {
                bytes_of_cleartext_data.Add(0xFFFF);
                bytes_of_encrypted_data.Add(0);
                cleartext_size -= 0xFFFF;
            }

            // append whatever is left
            bytes_of_cleartext_data.Add((ushort)cleartext_size);
            bytes_of_encrypted_data.Add(encrypted_size);
        }

        // members
        protected uint _naluLengthSize;
        protected uint _format;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencBasicSubSampleMapper
    +---------------------------------------------------------------------*/
    public class CencBasicSubSampleMapper : CencSubSampleMapper
    {
        // constructor and destructor
        public CencBasicSubSampleMapper(uint nalu_length_size, uint format) :
            base(nalu_length_size, format) {}

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencBasicSubSampleMapper::GetSubSampleMap
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSubSampleMap(DataBuffer   sample_data,
                                                  List<ushort> bytes_of_cleartext_data,
                                                  List<uint>   bytes_of_encrypted_data)
        {
            // setup direct pointers to the buffers
            byte[] @in = sample_data.GetData();
            uint   in_pos = 0;

            // process the sample data, one NALU at a time
            uint in_end = sample_data.GetDataSize();
            if (_format == Ap4.SAMPLE_FORMAT_AC_4) {
                Ac4Header ac4_header = new Ac4Header(@in, (uint)(in_end - in_pos), false);
                // BytesOfProtectedData shall be adjusted to a multiple of 16 bytes for 'cbc1'
                uint encrypted_size = ((uint)(in_end - in_pos) - ac4_header._tocSize)/16*16;
                uint cleartext_size = (uint)(in_end - in_pos) - encrypted_size;
                bytes_of_cleartext_data.Add((ushort)cleartext_size);
                bytes_of_encrypted_data.Add(encrypted_size);
                return Ap4Result.SUCCESS;
            }
            while ((uint)(in_end-in_pos) > _naluLengthSize) {
                uint nalu_length;
                switch (_naluLengthSize) {
                    case 1:
                        nalu_length = @in[in_pos];
                        break;

                    case 2:
                        nalu_length = Ap4Utils.BytesToUInt16BE(@in, (int)in_pos);
                        break;

                    case 4:
                        nalu_length = Ap4Utils.BytesToUInt32BE(@in, (int)in_pos);
                        break;

                    default:
                        return Ap4Result.ERROR_INVALID_FORMAT;
                }

                uint chunk_size     = _naluLengthSize+nalu_length;
                uint cleartext_size = chunk_size%16;
                uint block_count    = chunk_size/16;
                if (cleartext_size < _naluLengthSize+1) {
                    Ap4.ASSERT(block_count != 0);
                    --block_count;
                    cleartext_size += 16;
                }

                // PORT NOTE: upstream does not check that the NAL unit fits in the
                // sample (it would then read, and later copy/encrypt, memory past the
                // end of the sample buffer). The port reports such samples as invalid.
                if ((ulong)in_pos+chunk_size > in_end) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }

                // move the pointers
                in_pos += chunk_size;

                // store the info
                bytes_of_cleartext_data.Add((ushort)cleartext_size);
                bytes_of_encrypted_data.Add((uint)(block_count*16));
            }

            return Ap4Result.SUCCESS;
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_CencAdvancedSubSampleMapper
    +---------------------------------------------------------------------*/
    public class CencAdvancedSubSampleMapper : CencSubSampleMapper
    {
        // constructor and destructor
        public CencAdvancedSubSampleMapper(uint nalu_length_size, uint format) :
            base(nalu_length_size, format) {}

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencAdvancedSubSampleEncrypter::GetSubSampleMap
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSubSampleMap(DataBuffer   sample_data,
                                                  List<ushort> bytes_of_cleartext_data,
                                                  List<uint>   bytes_of_encrypted_data)
        {
            // setup direct pointers to the buffers
            byte[] @in = sample_data.GetData();
            uint   in_pos = 0;

            // process the sample data, one NALU at a time
            uint in_end = sample_data.GetDataSize();
            if (_format == Ap4.SAMPLE_FORMAT_AC_4) {
                // According to ISO/IEC 23001-7 2023-8, BytesOfProtectedData shall be adjusted to a multiple of 16 bytes for 'cens' and 'cenc' mode.
                Ac4Header ac4_header = new Ac4Header(@in, (uint)(in_end - in_pos), false);
                uint total = (uint)(in_end - in_pos);
                uint header_size = ac4_header._tocSize;
                uint encrypt_size = total - header_size;

                uint tail = encrypt_size & 0xF;
                bytes_of_cleartext_data.Add((ushort)(header_size + tail));
                bytes_of_encrypted_data.Add(encrypt_size - tail);
                return Ap4Result.SUCCESS;
            }
            while ((uint)(in_end-in_pos) > 1+_naluLengthSize) {
                uint nalu_length;
                switch (_naluLengthSize) {
                    case 1:
                        nalu_length = @in[in_pos];
                        break;

                    case 2:
                        nalu_length = Ap4Utils.BytesToUInt16BE(@in, (int)in_pos);
                        break;

                    case 4:
                        nalu_length = Ap4Utils.BytesToUInt32BE(@in, (int)in_pos);
                        break;

                    default:
                        return Ap4Result.ERROR_INVALID_FORMAT;
                }

                uint nalu_size = _naluLengthSize+nalu_length;
                if ((ulong)in_pos+nalu_size > in_end) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }

                // skip encryption if the NAL unit is smaller than the threshold (DECE CFF spec)
                // or should be left unencrypted for this specific format/type
                bool skip = false;
                if (nalu_size < Ap4.CENC_NAL_UNIT_ENCRYPTION_MIN_SIZE) {
                    skip = true;
                } else if (_format == Ap4.SAMPLE_FORMAT_AVC1 ||
                           _format == Ap4.SAMPLE_FORMAT_AVC2 ||
                           _format == Ap4.SAMPLE_FORMAT_AVC3 ||
                           _format == Ap4.SAMPLE_FORMAT_AVC4 ||
                           _format == Ap4.SAMPLE_FORMAT_DVAV ||
                           _format == Ap4.SAMPLE_FORMAT_DVA1) {
                    uint nalu_type = (uint)(@in[in_pos+_naluLengthSize] & 0x1F);
                    if (nalu_type != Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_NON_IDR_PICTURE &&
                        nalu_type != Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_DATA_PARTITION_A   &&
                        nalu_type != Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_DATA_PARTITION_B   &&
                        nalu_type != Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_DATA_PARTITION_C   &&
                        nalu_type != Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE) {
                        // this NAL unit is not a VCL NAL unit
                        skip = true;
                    }
                } else if (_format == Ap4.SAMPLE_FORMAT_HEV1 ||
                           _format == Ap4.SAMPLE_FORMAT_HVC1 ||
                           _format == Ap4.SAMPLE_FORMAT_DVHE ||
                           _format == Ap4.SAMPLE_FORMAT_DVH1) {
                    uint nalu_type = (uint)((@in[in_pos+_naluLengthSize] >> 1) & 0x3F);
                    if (nalu_type >= 32) {
                        // this NAL unit is not a VCL NAL unit
                        skip = true;
                    }
                }

                string cenc_layout = GlobalOptions.GetString("mpeg-cenc.encryption-layout");
                if (cenc_layout != null && string.CompareOrdinal(cenc_layout, "nalu-length-and-type-only") == 0) {
                    uint cleartext_size = _naluLengthSize+1;
                    uint encrypted_size = nalu_size > cleartext_size ? nalu_size-cleartext_size : 0;
                    CencSubSampleMapAppendEntry(bytes_of_cleartext_data, bytes_of_encrypted_data, cleartext_size, encrypted_size);
                } else if (skip) {
                    // use cleartext regions to cover the entire NAL unit
                    CencSubSampleMapAppendEntry(bytes_of_cleartext_data, bytes_of_encrypted_data, nalu_size, 0);
                } else {
                    // leave some cleartext bytes at the start and encrypt the rest (multiple of blocks)
                    uint encrypted_size = nalu_size-(Ap4.CENC_NAL_UNIT_ENCRYPTION_MIN_SIZE-16);
                    encrypted_size -= (encrypted_size % 16);
                    uint cleartext_size = nalu_size-encrypted_size;
                    Ap4.ASSERT(encrypted_size >= 16);
                    Ap4.ASSERT(cleartext_size >= _naluLengthSize);
                    CencSubSampleMapAppendEntry(bytes_of_cleartext_data, bytes_of_encrypted_data, cleartext_size, encrypted_size);
                }

                // move the pointers
                in_pos += nalu_size;
            }

            return Ap4Result.SUCCESS;
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_CencCbcsSubSampleMapper
    +---------------------------------------------------------------------*/
    public class CencCbcsSubSampleMapper : CencSubSampleMapper
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_CencCbcsSubSampleEncrypter::AP4_CencCbcsSubSampleEncrypter
        +---------------------------------------------------------------------*/
        public CencCbcsSubSampleMapper(uint nalu_length_size, uint format, TrakAtom trak) :
            base(nalu_length_size, format)
        {
            _avcParser  = null;
            _hevcParser = null;

            if (trak == null) return;

            // get the sample description atom
            StsdAtom stsd = trak.FindChild("mdia/minf/stbl/stsd") as StsdAtom;
            if (stsd == null) return;

            if (format == Ap4.SAMPLE_FORMAT_AVC1 ||
                format == Ap4.SAMPLE_FORMAT_AVC2 ||
                format == Ap4.SAMPLE_FORMAT_AVC3 ||
                format == Ap4.SAMPLE_FORMAT_AVC4 ||
                format == Ap4.SAMPLE_FORMAT_DVAV ||
                format == Ap4.SAMPLE_FORMAT_DVA1) {
                // create the parser
                _avcParser = new AvcFrameParser();

                // look for an avc sample description
                AvccAtom avcc =       stsd.FindChild("avc1/avcC") as AvccAtom;
                if (avcc == null) avcc = stsd.FindChild("avc2/avcC") as AvccAtom;
                if (avcc == null) avcc = stsd.FindChild("avc3/avcC") as AvccAtom;
                if (avcc == null) avcc = stsd.FindChild("avc4/avcC") as AvccAtom;
                if (avcc == null) return;

                // parse the sps and pps if we have them
                List<DataBuffer> sps_list = avcc.GetSequenceParameters();
                for (int i=0; i<sps_list.Count; i++) {
                    DataBuffer sps = sps_list[i];
                    ParseAvcData(sps.GetData(), 0, sps.GetDataSize());
                }
                List<DataBuffer> pps_list = avcc.GetPictureParameters();
                for (int i=0; i<pps_list.Count; i++) {
                    DataBuffer pps = pps_list[i];
                    ParseAvcData(pps.GetData(), 0, pps.GetDataSize());
                }
            } else if (format == Ap4.SAMPLE_FORMAT_HEV1 ||
                       format == Ap4.SAMPLE_FORMAT_HVC1 ||
                       format == Ap4.SAMPLE_FORMAT_DVHE ||
                       format == Ap4.SAMPLE_FORMAT_DVH1) {
                // create the parser
                _hevcParser = new HevcFrameParser();

                // look for an hevc sample description
                HvccAtom hvcc =       stsd.FindChild("hvc1/hvcC") as HvccAtom;
                if (hvcc == null) hvcc = stsd.FindChild("hev1/hvcC") as HvccAtom;
                if (hvcc == null) hvcc = stsd.FindChild("dvh1/hvcC") as HvccAtom;
                if (hvcc == null) hvcc = stsd.FindChild("dvhe/hvcC") as HvccAtom;
                if (hvcc == null) return;

                // parse the vps, sps and pps if we have them
                List<HvccAtom.Sequence> sequence_list = hvcc.GetSequences();
                for (int i=0; i<sequence_list.Count; i++) {
                    List<DataBuffer> nalus = sequence_list[i]._nalus;
                    for (int j=0; j<nalus.Count; j++) {
                        ParseHevcData(nalus[j].GetData(), 0, nalus[j].GetDataSize());
                    }
                }
            }
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencCbcsSubSampleMapper::GetSubSampleMap
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSubSampleMap(DataBuffer   sample_data,
                                                  List<ushort> bytes_of_cleartext_data,
                                                  List<uint>   bytes_of_encrypted_data)
        {
            // setup direct pointers to the buffers
            byte[] @in = sample_data.GetData();
            uint   in_pos = 0;

            // process the sample data, one NALU at a time
            uint in_end = sample_data.GetDataSize();
            if (_format == Ap4.SAMPLE_FORMAT_AC_4) {
                Ac4Header ac4_header = new Ac4Header(@in, (uint)(in_end - in_pos), false);
                // BytesOfProtectedData is not adjusted to a multiple of 16 bytes for 'cbcs' according
                // to ISO/IEC 23001-7 2023-8, however decryption model doesn't support in Bento4, need
                // to revisit this code in future.
                // bytes_of_cleartext_data.Append(ac4_header.m_TocSize);
                // bytes_of_encrypted_data.Append((AP4_Size)(in_end - in) - ac4_header.m_TocSize);
                // BytesOfProtectedData shall be adjusted to a multiple of 16 bytes for 'cbc1'
                uint encrypted_size = ((uint)(in_end - in_pos) - ac4_header._tocSize) / 16 * 16;
                uint cleartext_size = (uint)(in_end - in_pos) - encrypted_size;
                bytes_of_cleartext_data.Add((ushort)cleartext_size);
                bytes_of_encrypted_data.Add(encrypted_size);
                return Ap4Result.SUCCESS;
            }

            while ((uint)(in_end-in_pos) > 1+_naluLengthSize) {
                uint nalu_length;
                switch (_naluLengthSize) {
                    case 1:
                        nalu_length = @in[in_pos];
                        break;

                    case 2:
                        nalu_length = Ap4Utils.BytesToUInt16BE(@in, (int)in_pos);
                        break;

                    case 4:
                        nalu_length = Ap4Utils.BytesToUInt32BE(@in, (int)in_pos);
                        break;

                    default:
                        return Ap4Result.ERROR_INVALID_FORMAT;
                }

                uint nalu_size = _naluLengthSize+nalu_length;
                if ((ulong)in_pos+nalu_size > in_end) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }

                // skip encryption if the NAL unit should be left unencrypted for this specific format/type
                bool skip = false;
                if (_format == Ap4.SAMPLE_FORMAT_AVC1 ||
                    _format == Ap4.SAMPLE_FORMAT_AVC2 ||
                    _format == Ap4.SAMPLE_FORMAT_AVC3 ||
                    _format == Ap4.SAMPLE_FORMAT_AVC4 ||
                    _format == Ap4.SAMPLE_FORMAT_DVAV ||
                    _format == Ap4.SAMPLE_FORMAT_DVA1) {
                    int  nalu_data = (int)(in_pos+_naluLengthSize); // PORT NOTE: position of the NAL unit in `in`
                    uint nalu_type = (uint)(@in[nalu_data] & 0x1F);

                    if (nalu_type == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_NON_IDR_PICTURE ||
                        nalu_type == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_DATA_PARTITION_A   ||
                        nalu_type == Ap4.AVC_NAL_UNIT_TYPE_CODED_SLICE_OF_IDR_PICTURE) {
                        // parse the NAL unit to get the slice header size
                        if (_avcParser == null) return Ap4Result.ERROR_INTERNAL;
                        AvcSliceHeader slice_header = new AvcSliceHeader();
                        uint nalu_ref_idc = (uint)((@in[nalu_data]>>5)&3);
                        Ap4Result result = _avcParser.ParseSliceHeader(@in, nalu_data+1,
                                                                       nalu_length-1,
                                                                       nalu_type,
                                                                       nalu_ref_idc,
                                                                       slice_header);
                        if (result.Failed()) {
                            return result;
                        }

                        // round up the slide header size to a multiple of bytes
                        uint header_size = (slice_header.size+7)/8;

                        // account for emulation prevention bytes
                        uint emulation_prevention_bytes = NalParser.CountEmulationPreventionBytes(@in, nalu_data+1, nalu_length-1, header_size);
                        header_size += emulation_prevention_bytes;

                        // leave the slice header in the clear, including the NAL type
                        uint cleartext_size = _naluLengthSize+1+header_size;
                        uint encrypted_size = nalu_size-cleartext_size;
                        CencSubSampleMapAppendEntry(bytes_of_cleartext_data, bytes_of_encrypted_data, cleartext_size, encrypted_size);
                    } else {
                        // this NAL unit does not have a slice header
                        skip = true;

                        // parse SPS and PPS NAL units
                        if (nalu_type == Ap4.AVC_NAL_UNIT_TYPE_SPS ||
                            nalu_type == Ap4.AVC_NAL_UNIT_TYPE_PPS) {
                            Ap4Result result = ParseAvcData(@in, nalu_data, nalu_length);
                            if (result.Failed()) {
                                return result;
                            }
                        }
                    }
                } else if (_format == Ap4.SAMPLE_FORMAT_HEV1 ||
                           _format == Ap4.SAMPLE_FORMAT_HVC1 ||
                           _format == Ap4.SAMPLE_FORMAT_DVHE ||
                           _format == Ap4.SAMPLE_FORMAT_DVH1) {
                    int  nalu_data = (int)(in_pos+_naluLengthSize); // PORT NOTE: position of the NAL unit in `in`
                    uint nalu_type = (uint)((@in[nalu_data] >> 1) & 0x3F);

                    if (nalu_type < Ap4.HEVC_NALU_TYPE_VPS_NUT) {
                        // this is a VCL NAL Unit
                        if (_hevcParser == null) return Ap4Result.ERROR_INTERNAL;
                        HevcSliceSegmentHeader slice_header = new HevcSliceSegmentHeader();
                        Ap4Result result = _hevcParser.ParseSliceSegmentHeader(@in, nalu_data+2, nalu_length-2, nalu_type, slice_header);
                        if (result.Failed()) {
                            return result;
                        }

                        // leave the slice header in the clear, including the NAL type
                        // NOTE: the slice header is always a multiple of 8 bits because of byte_alignment()
                        uint header_size = slice_header.size/8;

                        // account for emulation prevention bytes
                        uint emulation_prevention_bytes = NalParser.CountEmulationPreventionBytes(@in, nalu_data+2, nalu_length-2, header_size);
                        header_size += emulation_prevention_bytes;

                        // set the encrypted range
                        uint cleartext_size = _naluLengthSize+2+header_size;
                        uint encrypted_size = nalu_size-cleartext_size;
                        CencSubSampleMapAppendEntry(bytes_of_cleartext_data, bytes_of_encrypted_data, cleartext_size, encrypted_size);
                    } else {
                        skip = true;

                        // parse VPS, SPS and PPS NAL units
                        if (nalu_type == Ap4.HEVC_NALU_TYPE_VPS_NUT ||
                            nalu_type == Ap4.HEVC_NALU_TYPE_SPS_NUT ||
                            nalu_type == Ap4.HEVC_NALU_TYPE_PPS_NUT) {
                            Ap4Result result = ParseHevcData(@in, nalu_data, nalu_length);
                            if (result.Failed()) {
                                return result;
                            }
                        }
                    }
                } else {
                    // only AVC and HEVC elementary streams are supported.
                    return Ap4Result.ERROR_NOT_SUPPORTED;
                }

                if (skip) {
                    // use cleartext regions to cover the entire NAL unit
                    CencSubSampleMapAppendEntry(bytes_of_cleartext_data, bytes_of_encrypted_data, nalu_size, 0);
                }

                // move the pointers
                in_pos += nalu_size;
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private AvcFrameParser   _avcParser;
        private HevcFrameParser  _hevcParser;

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencCbcsSubSampleMapper::ParseAvcData
        +---------------------------------------------------------------------*/
        // PORT NOTE: (data, data_size) upstream; `offset` is the position of the data in `data`
        private Ap4Result ParseAvcData(byte[] data, int offset, uint data_size)
        {
            if (_avcParser == null) return Ap4Result.ERROR_INVALID_PARAMETERS;

            AvcFrameParser.AccessUnitInfo access_unit_info = new AvcFrameParser.AccessUnitInfo();
            Ap4Result result = _avcParser.Feed(data, offset, data_size, access_unit_info);
            if (result.Failed()) return result;

            // cleanup
            access_unit_info.Reset();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencCbcsSubSampleMapper::ParseHevcData
        +---------------------------------------------------------------------*/
        // PORT NOTE: (data, data_size) upstream; `offset` is the position of the data in `data`
        private Ap4Result ParseHevcData(byte[] data, int offset, uint data_size)
        {
            if (_hevcParser == null) return Ap4Result.ERROR_INVALID_PARAMETERS;

            HevcFrameParser.AccessUnitInfo access_unit_info = new HevcFrameParser.AccessUnitInfo();
            Ap4Result result = _hevcParser.Feed(data, offset, data_size, access_unit_info);
            if (result.Failed()) return result;

            // cleanup
            access_unit_info.Reset();

            return Ap4Result.SUCCESS;
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_CencSubSampleEncrypter
    +---------------------------------------------------------------------*/
    public abstract class CencSubSampleEncrypter : CencSampleEncrypter
    {
        // constructor and destructor
        public CencSubSampleEncrypter(StreamCipher         cipher,
                                      bool                 constant_iv,
                                      bool                 reset_iv_at_each_subsample,
                                      CencSubSampleMapper  subsample_mapper) :
            base(cipher, constant_iv)
        {
            _resetIvForEachSubsample = reset_iv_at_each_subsample;
            _subSampleMapper         = subsample_mapper;
        }

        // methods
        public override bool UseSubSamples() { return true; }
        public override Ap4Result GetSubSampleMap(DataBuffer   sample_data,
                                                  List<ushort> bytes_of_cleartext_data,
                                                  List<uint>   bytes_of_encrypted_data) {
            return _subSampleMapper.GetSubSampleMap(sample_data, bytes_of_cleartext_data, bytes_of_encrypted_data);
        }

        // members
        public bool                 _resetIvForEachSubsample;
        public CencSubSampleMapper  _subSampleMapper;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencCtrSubSampleEncrypter
    +---------------------------------------------------------------------*/
    public class CencCtrSubSampleEncrypter : CencSubSampleEncrypter
    {
        // constructor and destructor
        public CencCtrSubSampleEncrypter(StreamCipher         cipher,
                                         bool                 constant_iv,
                                         bool                 reset_iv_at_each_subsample,
                                         uint                 iv_size,
                                         CencSubSampleMapper  subsample_mapper) :
            base(cipher,
                 constant_iv,
                 reset_iv_at_each_subsample,
                 subsample_mapper)
        {
            _ivSize = iv_size;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencCtrSubSampleEncrypter::EncryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result EncryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    DataBuffer sample_infos)
        {
            // the output has the same size as the input
            data_out.SetDataSize(data_in.GetDataSize());

            // check some basics
            if (data_in.GetDataSize() == 0) return Ap4Result.SUCCESS;

            // setup direct pointers to the buffers
            byte[] @in = data_in.GetData();
            byte[] @out = data_out.UseData();
            int    in_pos = 0;
            int    out_pos = 0;

            // setup the IV
            uint total_encrypted = 0;
            _cipher.SetIV(_iv);

            // get the subsample map
            List<ushort> bytes_of_cleartext_data = new List<ushort>();
            List<uint>   bytes_of_encrypted_data = new List<uint>();
            Ap4Result result = _subSampleMapper.GetSubSampleMap(data_in, bytes_of_cleartext_data, bytes_of_encrypted_data);
            if (result.Failed()) return result;

            // process the data
            for (int i=0; i<bytes_of_cleartext_data.Count; i++) {
                // copy the cleartext portion
                Array.Copy(@in, in_pos, @out, out_pos, bytes_of_cleartext_data[i]);

                // encrypt the rest
                if (bytes_of_encrypted_data[i] != 0) {
                    uint out_size = bytes_of_encrypted_data[i];
                    _cipher.ProcessBuffer(@in, in_pos+bytes_of_cleartext_data[i],
                                          bytes_of_encrypted_data[i],
                                          @out, out_pos+bytes_of_cleartext_data[i],
                                          ref out_size);
                    total_encrypted += bytes_of_encrypted_data[i];
                }

                // move the pointers
                in_pos  += (int)(bytes_of_cleartext_data[i]+bytes_of_encrypted_data[i]);
                out_pos += (int)(bytes_of_cleartext_data[i]+bytes_of_encrypted_data[i]);
            }

            // update the IV
            if (_ivSize == 16) {
                ulong counter = Ap4Utils.BytesToUInt64BE(_iv, 8);
                Ap4Utils.BytesFromUInt64BE(_iv, 8, counter+(total_encrypted+15)/16);
            } else {
                ulong counter = Ap4Utils.BytesToUInt64BE(_iv, 0);
                Ap4Utils.BytesFromUInt64BE(_iv, 0, counter+1);
            }

            // encode the sample infos
            uint sample_info_count = (uint)bytes_of_cleartext_data.Count;
            sample_infos.SetDataSize(2+sample_info_count*6);
            byte[] infos = sample_infos.UseData();
            Ap4Utils.BytesFromUInt16BE(infos, 0, (ushort)sample_info_count);
            for (uint i=0; i<sample_info_count; i++) {
                Ap4Utils.BytesFromUInt16BE(infos, (int)(2+i*6),   bytes_of_cleartext_data[(int)i]);
                Ap4Utils.BytesFromUInt32BE(infos, (int)(2+i*6+2), bytes_of_encrypted_data[(int)i]);
            }

            return Ap4Result.SUCCESS;
        }

        protected uint _ivSize;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencCbcSubSampleEncrypter
    +---------------------------------------------------------------------*/
    public class CencCbcSubSampleEncrypter : CencSubSampleEncrypter
    {
        // constructor and destructor
        public CencCbcSubSampleEncrypter(StreamCipher         cipher,
                                         bool                 constant_iv,
                                         bool                 reset_iv_at_each_subsample,
                                         CencSubSampleMapper  subsample_mapper) :
            base(cipher,
                 constant_iv,
                 reset_iv_at_each_subsample,
                 subsample_mapper) {}

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencCbcSubSampleEncrypter::EncryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result EncryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    DataBuffer sample_infos)
        {
            // the output has the same size as the input
            data_out.SetDataSize(data_in.GetDataSize());

            // check some basics
            if (data_in.GetDataSize() == 0) return Ap4Result.SUCCESS;

            // setup direct pointers to the buffers
            byte[] @in = data_in.GetData();
            byte[] @out = data_out.UseData();
            int    in_pos = 0;
            int    out_pos = 0;

            // setup the IV
            _cipher.SetIV(_iv);

            // get the subsample map
            List<ushort> bytes_of_cleartext_data = new List<ushort>();
            List<uint>   bytes_of_encrypted_data = new List<uint>();
            Ap4Result result = _subSampleMapper.GetSubSampleMap(data_in, bytes_of_cleartext_data, bytes_of_encrypted_data);
            if (result.Failed()) return result;

            for (int i=0; i<bytes_of_cleartext_data.Count; i++) {
                // copy the cleartext portion
                Array.Copy(@in, in_pos, @out, out_pos, bytes_of_cleartext_data[i]);

                // encrypt the rest
                if (_resetIvForEachSubsample) {
                    _cipher.SetIV(_iv);
                }
                if (bytes_of_encrypted_data[i] != 0) {
                    uint out_size = bytes_of_encrypted_data[i];
                    result = _cipher.ProcessBuffer(@in, in_pos+bytes_of_cleartext_data[i],
                                                   bytes_of_encrypted_data[i],
                                                   @out, out_pos+bytes_of_cleartext_data[i],
                                                   ref out_size, false);
                    if (result.Failed()) return result;

                    if (!_constantIv) {
                        // update the IV (last cipherblock emitted)
                        Array.Copy(@out, out_pos+bytes_of_cleartext_data[i]+(int)bytes_of_encrypted_data[i]-16, _iv, 0, 16);
                    }
                }

                // move the pointers
                in_pos  += (int)(bytes_of_cleartext_data[i]+bytes_of_encrypted_data[i]);
                out_pos += (int)(bytes_of_cleartext_data[i]+bytes_of_encrypted_data[i]);
            }

            // encode the sample infos
            uint sample_info_count = (uint)bytes_of_cleartext_data.Count;
            sample_infos.SetDataSize(2+sample_info_count*6);
            byte[] infos = sample_infos.UseData();
            Ap4Utils.BytesFromUInt16BE(infos, 0, (ushort)sample_info_count);
            for (uint i=0; i<sample_info_count; i++) {
                Ap4Utils.BytesFromUInt16BE(infos, (int)(2+i*6),   bytes_of_cleartext_data[(int)i]);
                Ap4Utils.BytesFromUInt32BE(infos, (int)(2+i*6+2), bytes_of_encrypted_data[(int)i]);
            }

            return Ap4Result.SUCCESS;
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_CencTrackEncrypter
    +---------------------------------------------------------------------*/
    internal class CencTrackEncrypter : Processor.TrackHandler {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_CencTrackEncrypter::AP4_CencTrackEncrypter
        +---------------------------------------------------------------------*/
        public CencTrackEncrypter(
            CencVariant       variant,
            uint              default_is_protected,
            byte              default_per_sample_iv_size,
            byte[]            default_kid,
            byte              default_constant_iv_size,
            byte[]            default_constant_iv,
            byte              default_crypt_byte_block,
            byte              default_skip_byte_block,
            List<SampleEntry> sample_entries,
            uint              format)
        {
            _variant                = variant;
            _format                 = format;
            _defaultIsProtected     = default_is_protected;
            _defaultPerSampleIvSize = default_per_sample_iv_size;
            _defaultConstantIvSize  = default_constant_iv_size;
            _defaultCryptByteBlock  = default_crypt_byte_block;
            _defaultSkipByteBlock   = default_skip_byte_block;

            // copy the KID and IV
            Array.Copy(default_kid, 0, _defaultKid, 0, 16);
            if (default_constant_iv != null) {
                Array.Copy(default_constant_iv, 0, _defaultConstantIv, 0, 16);
            }

            // copy the sample entry list
            for (int i=0; i<sample_entries.Count; i++) {
                _sampleEntries.Add(sample_entries[i]);
            }
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencTrackEncrypter::ProcessTrack
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessTrack()
        {
            for (int i=0; i<_sampleEntries.Count; i++) {
                // original format
                FrmaAtom frma = new FrmaAtom(_sampleEntries[i].GetType());

                // scheme info
                SchmAtom  schm = null;
                Atom      tenc = null;
                switch (_variant) {
                    case CencVariant.CENC_VARIANT_PIFF_CBC:
                    case CencVariant.CENC_VARIANT_PIFF_CTR:
                        schm = new SchmAtom(Ap4.PROTECTION_SCHEME_TYPE_PIFF,
                                            Ap4.PROTECTION_SCHEME_VERSION_PIFF_11);
                        tenc = new PiffTrackEncryptionAtom(_defaultIsProtected,
                                                           _defaultPerSampleIvSize,
                                                           _defaultKid);
                        break;

                    case CencVariant.CENC_VARIANT_MPEG_CENC:
                        schm = new SchmAtom(Ap4.PROTECTION_SCHEME_TYPE_CENC,
                                            Ap4.PROTECTION_SCHEME_VERSION_CENC_10);
                        tenc = new TencAtom(_defaultIsProtected,
                                            _defaultPerSampleIvSize,
                                            _defaultKid);
                        break;

                    case CencVariant.CENC_VARIANT_MPEG_CBC1:
                        schm = new SchmAtom(Ap4.PROTECTION_SCHEME_TYPE_CBC1,
                                            Ap4.PROTECTION_SCHEME_VERSION_CENC_10);
                        tenc = new TencAtom(_defaultIsProtected,
                                            _defaultPerSampleIvSize,
                                            _defaultKid);
                        break;

                    case CencVariant.CENC_VARIANT_MPEG_CENS:
                        schm = new SchmAtom(Ap4.PROTECTION_SCHEME_TYPE_CENS,
                                            Ap4.PROTECTION_SCHEME_VERSION_CENC_10);
                        tenc = new TencAtom(_defaultIsProtected,
                                            _defaultPerSampleIvSize,
                                            _defaultKid,
                                            _defaultConstantIvSize,
                                            _defaultConstantIv,
                                            _defaultCryptByteBlock,
                                            _defaultSkipByteBlock);
                        break;

                    case CencVariant.CENC_VARIANT_MPEG_CBCS:
                        schm = new SchmAtom(Ap4.PROTECTION_SCHEME_TYPE_CBCS,
                                            Ap4.PROTECTION_SCHEME_VERSION_CENC_10);
                        tenc = new TencAtom(_defaultIsProtected,
                                            _defaultPerSampleIvSize,
                                            _defaultKid,
                                            _defaultConstantIvSize,
                                            _defaultConstantIv,
                                            _defaultCryptByteBlock,
                                            _defaultSkipByteBlock);
                        break;
                }

                // populate the schi container
                ContainerAtom schi = new ContainerAtom(AtomType.SCHI);
                schi.AddChild(tenc);

                // populate the sinf container
                ContainerAtom sinf = new ContainerAtom(AtomType.SINF);
                sinf.AddChild(frma);
                sinf.AddChild(schm);
                sinf.AddChild(schi);

                // add the sinf atom to the sample description
                _sampleEntries[i].AddChild(sinf);

                // change the atom type of the sample description
                _sampleEntries[i].SetType(_format);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencTrackEncrypter::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessSample(DataBuffer data_in,
                                                DataBuffer data_out)
        {
            return data_out.SetData(data_in.GetData(), data_in.GetDataSize());
        }

        // members
        private CencVariant       _variant;
        private List<SampleEntry> _sampleEntries = new List<SampleEntry>();
        private uint              _format;
        private uint              _defaultIsProtected;
        private byte              _defaultPerSampleIvSize;
        private byte[]            _defaultKid = new byte[16];
        private byte              _defaultConstantIvSize;
        private byte[]            _defaultConstantIv = new byte[16];
        private byte              _defaultCryptByteBlock;
        private byte              _defaultSkipByteBlock;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencFragmentEncrypter
    +---------------------------------------------------------------------*/
    internal class CencFragmentEncrypter : Processor.FragmentHandler {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_CencFragmentEncrypter::AP4_CencFragmentEncrypter
        +---------------------------------------------------------------------*/
        public CencFragmentEncrypter(CencVariant                        variant,
                                     uint                               options,
                                     ContainerAtom                      traf,
                                     CencEncryptingProcessor.Encrypter  encrypter,
                                     uint                               cleartext_sample_description_index)
        {
            _variant                         = variant;
            _options                         = options;
            _traf                            = traf;
            _sampleEncryptionAtom            = null;
            _sampleEncryptionAtomShadow      = null;
            _saiz                            = null;
            _saio                            = null;
            _encrypter                       = encrypter;
            _cleartextSampleDescriptionIndex = cleartext_sample_description_index;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencFragmentEncrypter::ProcessFragment
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessFragment()
        {
            _sampleEncryptionAtom       = null;
            _sampleEncryptionAtomShadow = null;
            _saiz = null;
            _saio = null;

            // set the default-base-is-moof flag
            TfhdAtom tfhd = _traf.GetChild(AtomType.TFHD) as TfhdAtom;
            if (tfhd != null) {
                if (_variant != CencVariant.CENC_VARIANT_PIFF_CBC && _variant != CencVariant.CENC_VARIANT_PIFF_CTR) {
                    tfhd.SetFlags(tfhd.GetFlags() | Ap4.TFHD_FLAG_DEFAULT_BASE_IS_MOOF);
                }
            }

            // if we're still in the cleartext lead, update the tfhd and stop
            if (_encrypter._currentFragment < _encrypter._cleartextFragments && _cleartextSampleDescriptionIndex != 0) {
                if (tfhd != null) {
                    tfhd.SetSampleDescriptionIndex(_cleartextSampleDescriptionIndex);
                    tfhd.SetFlags(tfhd.GetFlags() | Ap4.TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT);
                    tfhd.SetSize(TfhdAtom.ComputeSize(tfhd.GetFlags()));
                    _traf.OnChildChanged(tfhd);
                }
                return Ap4Result.SUCCESS;
            }

            // create a sample encryption atom
            switch (_variant) {
                case CencVariant.CENC_VARIANT_PIFF_CBC:
                    _sampleEncryptionAtom = new PiffSampleEncryptionAtom(16);
                    break;

                case CencVariant.CENC_VARIANT_PIFF_CTR:
                    _sampleEncryptionAtom = new PiffSampleEncryptionAtom(8);
                    break;

                case CencVariant.CENC_VARIANT_MPEG_CENC:
                    if ((_options & CencEncryptingProcessor.OPTION_PIFF_COMPATIBILITY) != 0) {
                        byte iv_size = 8;
                        if ((_options & CencEncryptingProcessor.OPTION_PIFF_IV_SIZE_16) != 0) {
                            iv_size = 16;
                        }
                        _sampleEncryptionAtom       = new SencAtom(iv_size);
                        _sampleEncryptionAtomShadow = new PiffSampleEncryptionAtom(iv_size);
                    } else {
                        byte iv_size = 16; // default
                        if ((_options & CencEncryptingProcessor.OPTION_IV_SIZE_8) != 0) {
                            iv_size = 8;
                        }
                        _sampleEncryptionAtom = new SencAtom(iv_size);
                    }
                    _saiz = new SaizAtom();
                    _saio = new SaioAtom();
                    break;

                case CencVariant.CENC_VARIANT_MPEG_CBC1:
                    _sampleEncryptionAtom = new SencAtom(16);
                    _saiz = new SaizAtom();
                    _saio = new SaioAtom();
                    break;

                case CencVariant.CENC_VARIANT_MPEG_CENS:
                    _sampleEncryptionAtom = new SencAtom(16, 0, null, 0, 0);
                    _saiz = new SaizAtom();
                    _saio = new SaioAtom();
                    break;

                case CencVariant.CENC_VARIANT_MPEG_CBCS:
                    _sampleEncryptionAtom = new SencAtom(0, 16, null, 0, 0);
                    _saiz = new SaizAtom();
                    _saio = new SaioAtom();
                    break;

                default:
                    return Ap4Result.ERROR_INTERNAL;
            }
            if (_encrypter._sampleEncrypter.UseSubSamples()) {
                _sampleEncryptionAtom.GetOuter().SetFlags(
                    _sampleEncryptionAtom.GetOuter().GetFlags() |
                    Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_USE_SUB_SAMPLE_ENCRYPTION);
                if (_sampleEncryptionAtomShadow != null) {
                    _sampleEncryptionAtomShadow.GetOuter().SetFlags(
                        _sampleEncryptionAtomShadow.GetOuter().GetFlags() |
                        Ap4.CENC_SAMPLE_ENCRYPTION_FLAG_USE_SUB_SAMPLE_ENCRYPTION);
                }
            }

            // this is mostly for testing: forces the clients to parse saio/saiz instead
            // on relying on 'senc'
            if ((_options & CencEncryptingProcessor.OPTION_NO_SENC) != 0) {
                _sampleEncryptionAtom.GetOuter().SetType(ATOM_TYPE_SENC_ALT);
            }

            // add the child atoms to the traf container
            if (_saiz != null) _traf.AddChild(_saiz);
            if (_saio != null) _traf.AddChild(_saio);
            _traf.AddChild(_sampleEncryptionAtom.GetOuter());
            if (_sampleEncryptionAtomShadow != null) {
                _traf.AddChild(_sampleEncryptionAtomShadow.GetOuter());
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencFragmentEncrypter::PrepareForSamples
        +---------------------------------------------------------------------*/
        public override Ap4Result PrepareForSamples(FragmentSampleTable sample_table) {
            // do nothing if we're still in the clear lead part
            if (_encrypter._currentFragment < _encrypter._cleartextFragments) {
                return Ap4Result.SUCCESS;
            }

            uint sample_count = sample_table.GetSampleCount();

            // resize the saio atom if we have one
            if (_saio != null) {
                _saio.AddEntry(0); // we'll compute the offset later
            }

            if (!_encrypter._sampleEncrypter.UseSubSamples()) {
                _sampleEncryptionAtom.SetSampleInfosSize(sample_count*_sampleEncryptionAtom.GetPerSampleIvSize());
                if (_sampleEncryptionAtomShadow != null) {
                    _sampleEncryptionAtomShadow.SetSampleInfosSize(sample_count*_sampleEncryptionAtomShadow.GetPerSampleIvSize());
                }
                if (_saiz != null) {
                    if (_sampleEncryptionAtom.GetPerSampleIvSize() != 0) {
                        _saiz.SetDefaultSampleInfoSize(_sampleEncryptionAtom.GetPerSampleIvSize());
                        _saiz.SetSampleCount(sample_count);
                    } else {
                        _saiz.SetDefaultSampleInfoSize(0);
                        _saiz.SetSampleCount(0);
                    }
                }
                return Ap4Result.SUCCESS;
            }

            // resize the saiz atom if we have one
            if (_saiz != null) {
                _saiz.SetSampleCount(sample_count);
            }

            // prepare for samples
            Sample       sample = new Sample();
            DataBuffer   sample_data = new DataBuffer();
            List<ushort> bytes_of_cleartext_data = new List<ushort>();
            List<uint>   bytes_of_encrypted_data = new List<uint>();
            uint         sample_info_size = sample_count*(_sampleEncryptionAtom.GetPerSampleIvSize());
            for (uint i=0; i<sample_count; i++) {
                Ap4Result result = sample_table.GetSample(i, sample);
                if (result.Failed()) return result;
                result = sample.ReadData(sample_data);
                if (result.Failed()) return result;
                bytes_of_cleartext_data.Clear();
                bytes_of_encrypted_data.Clear();
                result = _encrypter._sampleEncrypter.GetSubSampleMap(sample_data,
                                                                     bytes_of_cleartext_data,
                                                                     bytes_of_encrypted_data);
                if (result.Failed()) return result;
                sample_info_size += 2+(uint)bytes_of_cleartext_data.Count*6;

                if (_saiz != null) {
                    _saiz.SetSampleInfoSize(i, (byte)(_sampleEncryptionAtom.GetPerSampleIvSize()+2+bytes_of_cleartext_data.Count*6));
                }
            }
            _sampleEncryptionAtom.SetSampleInfosSize(sample_info_size);
            if (_sampleEncryptionAtomShadow != null) {
                _sampleEncryptionAtomShadow.SetSampleInfosSize(sample_info_size);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencFragmentEncrypter::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessSample(DataBuffer data_in,
                                                DataBuffer data_out)
        {
            // just copy data if we're still in the clear lead part
            if (_encrypter._currentFragment < _encrypter._cleartextFragments) {
                data_out.SetData(data_in.GetData(), data_in.GetDataSize());
                return Ap4Result.SUCCESS;
            }

            // copy the current IV
            byte[] iv = new byte[16];
            Array.Copy(_encrypter._sampleEncrypter.GetIv(), 0, iv, 0, 16);

            // encrypt the sample
            DataBuffer sample_infos = new DataBuffer();
            Ap4Result result = _encrypter._sampleEncrypter.EncryptSampleData(data_in, data_out, sample_infos);
            if (result.Failed()) return result;

            // update the sample info
            _sampleEncryptionAtom.AddSampleInfo(iv, sample_infos);
            if (_sampleEncryptionAtomShadow != null) {
                _sampleEncryptionAtomShadow.AddSampleInfo(iv, sample_infos);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencFragmentEncrypter::FinishFragment
        +---------------------------------------------------------------------*/
        public override Ap4Result FinishFragment()
        {
            if (_encrypter._currentFragment++ < _encrypter._cleartextFragments) {
                return Ap4Result.SUCCESS;
            }

            if (_saio == null) return Ap4Result.SUCCESS;

            // compute the saio offsets
            ContainerAtom  moof = _traf.GetParent() as ContainerAtom;
            if (moof == null) return Ap4Result.ERROR_INTERNAL;

            ulong traf_offset = moof.GetHeaderSize();
            foreach (Atom child in moof.GetChildren()) {
                if ((child as ContainerAtom) == _traf) {
                    ulong senc_offset = _traf.GetHeaderSize();
                    Atom  senc_atom = null;
                    foreach (Atom atom in _traf.GetChildren()) {
                        if (atom.GetType() == AtomType.SENC ||
                            atom.GetType() == ATOM_TYPE_SENC_ALT) { // sometimes used instead of 'senc' for testing
                            senc_atom = atom;
                        } else if (atom.GetType() == AtomType.UUID) {
                            // PORT NOTE: null check added (upstream dereferences the cast result directly)
                            UuidAtom uuid_atom = atom as UuidAtom;
                            if (uuid_atom != null &&
#if NET6_0_OR_GREATER
                                System.MemoryExtensions.SequenceEqual<byte>(uuid_atom.GetUuid(), Ap4.UUID_PIFF_SAMPLE_ENCRYPTION_ATOM)) {
#else
                                Compat.SequenceEqual(uuid_atom.GetUuid(), Ap4.UUID_PIFF_SAMPLE_ENCRYPTION_ATOM)) {
#endif
                                senc_atom = atom;
                            }
                        }
                        if (senc_atom != null) break;
                        senc_offset += atom.GetSize();
                    }

                    if (senc_atom != null) {
                        ulong saio_offset = traf_offset+senc_offset+senc_atom.GetHeaderSize()+4;
                        _saio.SetEntry(0, saio_offset);
                    }
                } else {
                    traf_offset += child.GetSize();
                }
            }

            return Ap4Result.SUCCESS;
        }

        // AP4_ATOM_TYPE('s', 'e', 'n', 'C')
        private const uint ATOM_TYPE_SENC_ALT = 0x73656e43;

        // members
        private CencVariant                       _variant;
        private uint                              _options;
        private ContainerAtom                     _traf;
        private ICencSampleEncryption             _sampleEncryptionAtom;
        private ICencSampleEncryption             _sampleEncryptionAtomShadow;
        private SaizAtom                          _saiz;
        private SaioAtom                          _saio;
        private CencEncryptingProcessor.Encrypter _encrypter;
        private uint                              _cleartextSampleDescriptionIndex;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencEncryptingProcessor
    +---------------------------------------------------------------------*/
    public class CencEncryptingProcessor : Processor
    {
        // class constants
        /// <summary>Include a 'standard EME' pssh atom in the output</summary>
        public const uint OPTION_EME_PSSH           = 0x01;
        /// <summary>Attempt to create an output that is compatible with the PIFF format</summary>
        public const uint OPTION_PIFF_COMPATIBILITY = 0x02;
        /// <summary>With the PIFF-compatibiity option, use an IV of size 16 when possible (instead of 8)</summary>
        public const uint OPTION_PIFF_IV_SIZE_16    = 0x04;
        /// <summary>Use an IV of size 8 when possible (instead of 16 by default).</summary>
        public const uint OPTION_IV_SIZE_8          = 0x08;
        /// <summary>Don't output an 'senc' atom</summary>
        public const uint OPTION_NO_SENC            = 0x10;

        // types
        public class Encrypter {
            public Encrypter(uint track_id, uint cleartext_fragments, CencSampleEncrypter sample_encrypter)
            {
                _trackId            = track_id;
                _currentFragment    = 0;
                _cleartextFragments = cleartext_fragments;
                _sampleEncrypter    = sample_encrypter;
            }
            public uint                _trackId;
            public uint                _currentFragment;
            public uint                _cleartextFragments;
            public CencSampleEncrypter _sampleEncrypter;
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_CencEncryptingProcessor:AP4_CencEncryptingProcessor
        +---------------------------------------------------------------------*/
        public CencEncryptingProcessor(CencVariant         variant,
                                       uint                options = 0,
                                       BlockCipherFactory  block_cipher_factory = null)
        {
            _variant = variant;
            _options = options;

            // create a block cipher factory if none is given
            if (block_cipher_factory == null) {
                _blockCipherFactory = DefaultBlockCipherFactory.Instance;
            } else {
                _blockCipherFactory = block_cipher_factory;
            }
        }

        // accessors
        public ProtectionKeyMap GetKeyMap()      { return _keyMap;      }
        public TrackPropertyMap GetPropertyMap() { return _propertyMap; }
        public List<PsshAtom>  GetPsshAtoms()   { return _psshAtoms;   }

        // AP4_Processor methods
        /*----------------------------------------------------------------------
        |   AP4_CencEncryptingProcessor::Initialize
        +---------------------------------------------------------------------*/
        public override Ap4Result Initialize(AtomParent                  top_level,
                                             ByteStream                  stream,
                                             Processor.ProgressListener  listener = null)
        {
            FtypAtom ftyp = top_level.GetChild(AtomType.FTYP) as FtypAtom;
            if (ftyp != null) {
                // remove the atom, it will be replaced with a new one
                top_level.RemoveChild(ftyp);

                // keep the existing brand and compatible brands
                List<uint> compatible_brands = new List<uint>();
                compatible_brands.Capacity = ftyp.GetCompatibleBrands().Count+1;
                for (int i=0; i<ftyp.GetCompatibleBrands().Count; i++) {
                    compatible_brands.Add(ftyp.GetCompatibleBrands()[i]);
                }

                // add the compatible brand if it is not already there
                if (_variant == CencVariant.CENC_VARIANT_PIFF_CBC || _variant == CencVariant.CENC_VARIANT_PIFF_CTR) {
                    if (!ftyp.HasCompatibleBrand(Ap4.PIFF_BRAND)) {
                        compatible_brands.Add(Ap4.PIFF_BRAND);
                    }
                } else if (_variant == CencVariant.CENC_VARIANT_MPEG_CENC ||
                           _variant == CencVariant.CENC_VARIANT_MPEG_CBC1 ||
                           _variant == CencVariant.CENC_VARIANT_MPEG_CENS ||
                           _variant == CencVariant.CENC_VARIANT_MPEG_CBCS) {
                    if (!ftyp.HasCompatibleBrand(Ap4.FILE_BRAND_ISO6)) {
                        compatible_brands.Add(Ap4.FILE_BRAND_ISO6);
                    }
                }

                // create a replacement
                FtypAtom new_ftyp = new FtypAtom(ftyp.GetMajorBrand(),
                                                 ftyp.GetMinorVersion(),
                                                 compatible_brands.ToArray(),
                                                 (uint)compatible_brands.Count);
                ftyp = new_ftyp;
            } else {
                List<uint> compatible_brands = new List<uint>();
                compatible_brands.Add(Ap4.FILE_BRAND_ISO6);
                if (_variant == CencVariant.CENC_VARIANT_PIFF_CBC || _variant == CencVariant.CENC_VARIANT_PIFF_CTR) {
                    compatible_brands.Add(Ap4.PIFF_BRAND);
                    compatible_brands.Add(Ap4.FTYP_BRAND_MSDH);
                }
                ftyp = new FtypAtom(Ap4.FTYP_BRAND_MP42, 0, compatible_brands.ToArray(), (uint)compatible_brands.Count);
            }

            // insert the ftyp atom as the first child
            Ap4Result result = top_level.AddChild(ftyp, 0);
            if (result.Failed()) return result;

            // add pssh atoms if needed
            ContainerAtom moov = top_level.GetChild(AtomType.MOOV) as ContainerAtom;
            if (moov != null) {
                // create a 'standard EME' pssh atom
                PsshAtom  eme_pssh = null;
                if ((_variant == CencVariant.CENC_VARIANT_MPEG_CENC ||
                     _variant == CencVariant.CENC_VARIANT_MPEG_CBC1 ||
                     _variant == CencVariant.CENC_VARIANT_MPEG_CENS ||
                     _variant == CencVariant.CENC_VARIANT_MPEG_CBCS) &&
                    (_options & OPTION_EME_PSSH) != 0) {
                    DataBuffer kids = new DataBuffer();
                    uint       kid_count = 0;
                    List<TrackPropertyMap.Entry> prop_entries = _propertyMap.GetEntries();

                    for (int i=0; i<prop_entries.Count; i++) {
                        TrackPropertyMap.Entry entry = prop_entries[i];
                        string kid_hex = _propertyMap.GetProperty(entry._trackId, "KID");

                        if (kid_hex != null && Ap4Utils.StringLength(kid_hex) == 32) {
                            byte[] kid = new byte[16];
                            Ap4Utils.ParseHex(kid_hex, kid, 16);
                            // only add this entry if there isn't already an entry for this KID
                            bool found = false;
                            for (uint j=0; j<kid_count && !found; j++) {
                                if (CompareMemory(kid, 0, kids.GetData(), (int)(j*16), 16) == 0) {
                                    found = true;
                                }
                            }
                            if (!found) {
                                kids.SetDataSize((kid_count+1)*16);
                                Array.Copy(kid, 0, kids.UseData(), (int)(kid_count*16), 16);
                                ++kid_count;
                            }
                        }
                    }
                    if (kid_count != 0) {
                        eme_pssh = new PsshAtom(Ap4.EME_COMMON_SYSTEM_ID);
                        eme_pssh.SetKids(kids.GetData(), kid_count);
                    }
                }

                // check if we need to create a Marlin 'mkid' table
                PsshAtom marlin_pssh = null;
                if (_variant == CencVariant.CENC_VARIANT_MPEG_CENC) {
                    List<TrackPropertyMap.Entry> prop_entries = _propertyMap.GetEntries();
                    MkidAtom mkid = null;
                    for (int i=0; i<prop_entries.Count; i++) {
                        TrackPropertyMap.Entry entry = prop_entries[i];
                        if (entry != null && entry._name == "ContentId") {
                            if (mkid == null) mkid = new MkidAtom();
                            string kid_hex = _propertyMap.GetProperty(entry._trackId, "KID");

                            if (kid_hex != null && Ap4Utils.StringLength(kid_hex) == 32) {
                                byte[] kid = new byte[16];
                                Ap4Utils.ParseHex(kid_hex, kid, 16);
                                // only add this entry if there isn't already an entry for this KID
                                List<MkidAtom.Entry> entries = mkid.GetEntries();
                                bool found = false;
                                for (int j=0; j<entries.Count && !found; j++) {
                                    if (CompareMemory(entries[j]._kid, 0, kid, 0, 16) == 0) {
                                        found = true;
                                    }
                                }
                                if (!found) {
                                    mkid.AddEntry(kid, entry._value);
                                }
                            }
                        }
                    }

                    if (mkid != null) {
                        // create a 'marl' container
                        ContainerAtom marl = new ContainerAtom(AtomType.MARL);
                        marl.AddChild(mkid);

                        // see if we need to pad the 'pssh' container
                        string  padding_str = _propertyMap.GetProperty(0, "PsshPadding");
                        uint    padding = 0;
                        if (padding_str != null) {
                            padding = Ap4Utils.ParseIntegerU(padding_str);
                        }

                        // create a 'pssh' atom to contain the 'marl' atom
                        marlin_pssh = new PsshAtom(Ap4.MARLIN_PSSH_SYSTEM_ID);
                        marlin_pssh.SetData(marl);
                        if (padding > marl.GetSize()+32 && padding < 1024*1024) {
                            padding -= (uint)marl.GetSize()+32;
                            byte[] data = new byte[padding];
                            marlin_pssh.SetPadding(data, padding);
                        }
                    }
                }

                // add the 'pssh' atoms at the end of the 'moov' container
                // but before any 'free' atom, if any
                int pssh_position = -1;
                int pssh_current = 0;
                foreach (Atom child in moov.GetChildren()) {
                    if (child.GetType() == AtomType.FREE) {
                        pssh_position = pssh_current;
                    }
                    ++pssh_current;
                }
                if (marlin_pssh != null) {
                    moov.AddChild(marlin_pssh, pssh_position);
                    if (pssh_position >= 0) {
                        ++pssh_position;
                    }
                }
                if (eme_pssh != null) {
                    moov.AddChild(eme_pssh, pssh_position);
                    if (pssh_position >= 0) {
                        ++pssh_position;
                    }
                }
                for (int i=0; i<_psshAtoms.Count; i++) {
                    if (_psshAtoms[i] != null) {
                        // PORT NOTE: upstream is `new AP4_PsshAtom(*m_PsshAtoms[i])` (implicit copy
                        // constructor). PsshAtom has no copy constructor in the port, so the copy
                        // is made with Clone() (serialize + parse), which yields the same atom.
                        Atom  pssh_copy = _psshAtoms[i].Clone();
                        if (pssh_copy != null) {
                            moov.AddChild(pssh_copy, pssh_position);
                        }
                    }
                    if (pssh_position >= 0) {
                        ++pssh_position;
                    }
                }
            }


            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencEncryptingProcessor:CreateTrackHandler
        +---------------------------------------------------------------------*/
        public override Processor.TrackHandler CreateTrackHandler(TrakAtom trak)
        {
            // find the stsd atom
            StsdAtom stsd = trak.FindChild("mdia/minf/stbl/stsd") as StsdAtom;

            // avoid tracks with no stsd atom (should not happen)
            if (stsd == null) return null;

            // get the list of sample entries
            List<SampleEntry> entries = new List<SampleEntry>();
            for (uint i=0; i<stsd.GetSampleDescriptionCount(); i++) {
                SampleEntry  entry = stsd.GetSampleEntry(i);
                if (entry == null) return null;
                entries.Add(entry);
            }

            // create a handler for this track if we have a key for it and we know
            // how to map the type
            DataBuffer key;
            DataBuffer iv;
            if (_keyMap.GetKeyAndIv(trak.GetId(), out key, out iv).Failed()) {
                return null;
            }
            if (iv == null || iv.GetDataSize() != 16) {
                return null;
            }

            uint format = entries[0].GetType(); // only look at the type of the first entry
            uint enc_format = 0;
            switch (format) {
                case AtomType.MP4A:
                    enc_format = AtomType.ENCA;
                    break;

                case AtomType.MP4V:
                case AtomType.AVC1:
                case AtomType.AVC2:
                case AtomType.AVC3:
                case AtomType.AVC4:
                case AtomType.DVAV:
                case AtomType.DVA1:
                case AtomType.HEV1:
                case AtomType.HVC1:
                case AtomType.DVHE:
                case AtomType.DVH1:
                    enc_format = AtomType.ENCV;
                    break;

                default: {
                    // try to find if this is audio or video
                    HdlrAtom  hdlr = trak.FindChild("mdia/hdlr") as HdlrAtom;
                    if (hdlr != null) {
                        switch (hdlr.GetHandlerType()) {
                            case Ap4.HANDLER_TYPE_SOUN:
                                enc_format = AtomType.ENCA;
                                break;

                            case Ap4.HANDLER_TYPE_VIDE:
                                enc_format = AtomType.ENCV;
                                break;
                        }
                    }
                    break;
                }
            }
            if (enc_format == 0) return null;

            // get the track properties
            byte[] kid = new byte[16] {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
            string kid_hex = _propertyMap.GetProperty(trak.GetId(), "KID");
            if (kid_hex != null && Ap4Utils.StringLength(kid_hex) == 32) {
                Ap4Utils.ParseHex(kid_hex, kid, 16);
            }

            // create the encrypter
            Processor.TrackHandler track_encrypter;
            byte                   cipher_iv_size = 16;
            BlockCipher.CipherMode cipher_mode;
            BlockCipher.CtrParams  cipher_ctr_params = new BlockCipher.CtrParams();
            object                 cipher_mode_params = null;
            byte                   crypt_byte_block = 0;
            byte                   skip_byte_block = 0;
            bool                   constant_iv = false;
            bool                   reset_iv_at_each_subsample = false;
            switch (_variant) {
                case CencVariant.CENC_VARIANT_PIFF_CTR:
                    cipher_mode = BlockCipher.CipherMode.CTR;
                    cipher_ctr_params.counter_size = 8;
                    cipher_mode_params = cipher_ctr_params;
                    cipher_iv_size = 8;
                    track_encrypter = new CencTrackEncrypter(_variant,
                                                             1,
                                                             cipher_iv_size,
                                                             kid,
                                                             0,
                                                             null,
                                                             0,
                                                             0,
                                                             entries,
                                                             enc_format);
                    break;

                case CencVariant.CENC_VARIANT_PIFF_CBC:
                    cipher_mode = BlockCipher.CipherMode.CBC;
                    track_encrypter = new CencTrackEncrypter(_variant,
                                                             2,
                                                             cipher_iv_size,
                                                             kid,
                                                             0,
                                                             null,
                                                             0,
                                                             0,
                                                             entries,
                                                             enc_format);
                    break;

                case CencVariant.CENC_VARIANT_MPEG_CENC:
                    cipher_mode = BlockCipher.CipherMode.CTR;
                    cipher_ctr_params.counter_size = 8;
                    cipher_mode_params = cipher_ctr_params;
                    if ((_options & OPTION_IV_SIZE_8) != 0 ||
                        ((_options & OPTION_PIFF_COMPATIBILITY) != 0 && (_options & OPTION_PIFF_IV_SIZE_16) == 0)) {
                        cipher_iv_size = 8;
                    }

                    track_encrypter = new CencTrackEncrypter(_variant,
                                                             1,
                                                             cipher_iv_size,
                                                             kid,
                                                             0,
                                                             null,
                                                             0,
                                                             0,
                                                             entries,
                                                             enc_format);
                    break;

                case CencVariant.CENC_VARIANT_MPEG_CENS:
                    cipher_mode = BlockCipher.CipherMode.CTR;
                    cipher_ctr_params.counter_size = 8;
                    cipher_mode_params = cipher_ctr_params;
                    if ((_options & OPTION_IV_SIZE_8) != 0) {
                        cipher_iv_size = 8;
                    }
                    if (enc_format == AtomType.ENCV || (enc_format == AtomType.ENCA && format == AtomType.AC_4)) {
                        crypt_byte_block = 1;
                        skip_byte_block  = 9;
                    }
                    track_encrypter = new CencTrackEncrypter(_variant,
                                                             1,
                                                             cipher_iv_size,
                                                             kid,
                                                             0,
                                                             null,
                                                             crypt_byte_block,
                                                             skip_byte_block,
                                                             entries,
                                                             enc_format);
                    break;

                case CencVariant.CENC_VARIANT_MPEG_CBC1:
                    cipher_mode = BlockCipher.CipherMode.CBC;
                    track_encrypter = new CencTrackEncrypter(_variant,
                                                             1,
                                                             cipher_iv_size,
                                                             kid,
                                                             0,
                                                             null,
                                                             0,
                                                             0,
                                                             entries,
                                                             enc_format);
                    break;

                case CencVariant.CENC_VARIANT_MPEG_CBCS:
                    cipher_mode = BlockCipher.CipherMode.CBC;
                    if (enc_format == AtomType.ENCV || (enc_format == AtomType.ENCA && format == AtomType.AC_4)) {
                        crypt_byte_block = 1;
                        skip_byte_block  = 9;
                    }
                    constant_iv = true;
                    reset_iv_at_each_subsample = true;
                    track_encrypter = new CencTrackEncrypter(_variant,
                                                             1,
                                                             0,
                                                             kid,
                                                             cipher_iv_size,
                                                             iv.GetData(),
                                                             crypt_byte_block,
                                                             skip_byte_block,
                                                             entries,
                                                             enc_format);
                    break;

                default:
                    return null;
            }

            // create a block cipher
            BlockCipher block_cipher = null;
            Ap4Result result = _blockCipherFactory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                BlockCipher.CipherDirection.ENCRYPT,
                                                                cipher_mode,
                                                                cipher_mode_params,
                                                                key.GetData(),
                                                                key.GetDataSize(),
                                                                out block_cipher);
            if (result.Failed()) {
                return null;
            }

            // compute the size of NAL units
            uint nalu_length_size = 0;
            bool use_subsample_encryption = false;
            switch (format) {
                case AtomType.AVC1:
                case AtomType.AVC2:
                case AtomType.AVC3:
                case AtomType.AVC4:
                case AtomType.DVAV:
                case AtomType.DVA1: {
                    AvccAtom avcc = entries[0].GetChild(AtomType.AVCC) as AvccAtom;
                    if (avcc != null) {
                        nalu_length_size = avcc.GetNaluLengthSize();
                        use_subsample_encryption = (nalu_length_size > 0);
                    }
                    break;
                }
                case AtomType.HEV1:
                case AtomType.HVC1:
                case AtomType.DVHE:
                case AtomType.DVH1: {
                    HvccAtom hvcc = entries[0].GetChild(AtomType.HVCC) as HvccAtom;
                    if (hvcc != null) {
                        nalu_length_size = hvcc.GetNaluLengthSize();
                        use_subsample_encryption = (nalu_length_size > 0);
                    }
                    break;
                }
                case AtomType.AC_4:
                    use_subsample_encryption = true;
                    break;
                default:
                    use_subsample_encryption = false;
                    break;
            }

            // add a new cipher state for this track
            CencSampleEncrypter  sample_encrypter = null;
            StreamCipher         stream_cipher = null;
            switch (cipher_mode) {
                case BlockCipher.CipherMode.CBC:
                    stream_cipher = new CbcStreamCipher(block_cipher);
                    if (crypt_byte_block != 0 && skip_byte_block != 0) {
                        stream_cipher = new PatternStreamCipher(stream_cipher, crypt_byte_block, skip_byte_block);
                    }

                    if (use_subsample_encryption) {
                        CencSubSampleMapper subsample_mapper = null;
                        if (_variant == CencVariant.CENC_VARIANT_MPEG_CBCS) {
                            subsample_mapper = new CencCbcsSubSampleMapper(nalu_length_size, format, trak);
                        } else {
                            subsample_mapper = new CencBasicSubSampleMapper(nalu_length_size, format);
                        }
                        sample_encrypter = new CencCbcSubSampleEncrypter(stream_cipher,
                                                                         constant_iv,
                                                                         reset_iv_at_each_subsample,
                                                                         subsample_mapper);
                    } else {
                        sample_encrypter = new CencCbcSampleEncrypter(stream_cipher, constant_iv);
                    }

                    break;

                case BlockCipher.CipherMode.CTR:
                    stream_cipher = new CtrStreamCipher(block_cipher, 16);
                    if (crypt_byte_block != 0 && skip_byte_block != 0) {
                        stream_cipher = new PatternStreamCipher(stream_cipher, crypt_byte_block, skip_byte_block);
                    }
                    if (use_subsample_encryption) {
                        CencSubSampleMapper subsample_mapper = new CencAdvancedSubSampleMapper(nalu_length_size, format);
                        sample_encrypter = new CencCtrSubSampleEncrypter(stream_cipher,
                                                                         constant_iv,
                                                                         reset_iv_at_each_subsample,
                                                                         cipher_iv_size,
                                                                         subsample_mapper);
                    } else {
                        sample_encrypter = new CencCtrSampleEncrypter(stream_cipher, constant_iv, cipher_iv_size);
                    }
                    break;
            }
            if (sample_encrypter == null) {
                return null;
            }
            sample_encrypter.SetIv(iv.GetData());

            // if we need to leave some samples unencrypted, create clones of the sample descriptions
            string clear_lead = _propertyMap.GetProperty(trak.GetId(), "ClearLeadFragments");
            uint clear_fragments = 0;
            if (clear_lead != null) {
                clear_fragments = Ap4Utils.ParseIntegerU(clear_lead);
                if (clear_fragments != 0) {
                    uint sample_description_count = stsd.GetSampleDescriptionCount();
                    for (uint i=0; i<sample_description_count; i++) {
                        stsd.AddChild(stsd.GetSampleEntry(i).Clone());
                    }
                }
            }

            _encrypters.Add(new Encrypter(trak.GetId(), clear_fragments, sample_encrypter));
            return track_encrypter;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencEncryptingProcessor:CreateFragmentHandler
        +---------------------------------------------------------------------*/
        public override Processor.FragmentHandler CreateFragmentHandler(TrakAtom       trak,
                                                                         TrexAtom       trex,
                                                                         ContainerAtom  traf,
                                                                         ByteStream     moof_data,
                                                                         ulong          moof_offset)
        {
            // get the traf header (tfhd) for this track fragment so we can get the track ID
            TfhdAtom tfhd = traf.GetChild(AtomType.TFHD) as TfhdAtom;
            if (tfhd == null) return null;

            // lookup the encrypter for this track
            Encrypter encrypter = null;
            foreach (Encrypter item in _encrypters) {
                if (item._trackId == tfhd.GetTrackId()) {
                    encrypter = item;
                    break;
                }
            }
            if (encrypter == null) return null;

            uint clear_sample_description_index = 0;
            string clear_lead = _propertyMap.GetProperty(trak.GetId(), "ClearLeadFragments");
            if (clear_lead != null) {
                if (encrypter._currentFragment < encrypter._cleartextFragments) {
                    // find the stsd atom
                    StsdAtom stsd = trak.FindChild("mdia/minf/stbl/stsd") as StsdAtom;
                    if (stsd != null) {
                        uint tfhd_flags = tfhd.GetFlags();
                        uint sample_description_index = 0;
                        if ((tfhd_flags & Ap4.TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT) != 0) {
                            sample_description_index = tfhd.GetSampleDescriptionIndex();
                        } else {
                            sample_description_index = trex.GetDefaultSampleDescriptionIndex();
                        }
                        if (sample_description_index > 0) {
                            clear_sample_description_index = sample_description_index+stsd.GetSampleDescriptionCount()/2;
                        }
                    }
                }
            }
            return new CencFragmentEncrypter(_variant, _options, traf, encrypter, clear_sample_description_index);
        }

        // PORT NOTE: AP4_CompareMemory on two buffers at given positions
        private static int CompareMemory(byte[] a, int a_offset, byte[] b, int b_offset, int size)
        {
            for (int i=0; i<size; i++) {
                if (a[a_offset+i] != b[b_offset+i]) {
                    return a[a_offset+i] < b[b_offset+i] ? -1 : 1;
                }
            }
            return 0;
        }

        // members
        protected CencVariant        _variant;
        protected uint               _options;
        protected BlockCipherFactory _blockCipherFactory;
        protected ProtectionKeyMap   _keyMap = new ProtectionKeyMap();
        protected TrackPropertyMap   _propertyMap = new TrackPropertyMap();
        protected List<PsshAtom>    _psshAtoms = new List<PsshAtom>();
        protected List<Encrypter>    _encrypters = new List<Encrypter>();
    }

    /*----------------------------------------------------------------------
    |   AP4_CencDecryptingProcessor
    +---------------------------------------------------------------------*/
    public class CencDecryptingProcessor : Processor
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_CencDecryptingProcessor::AP4_CencDecryptingProcessor
        +---------------------------------------------------------------------*/
        public CencDecryptingProcessor(ProtectionKeyMap    key_map,
                                       BlockCipherFactory  block_cipher_factory = null)
        {
            _keyMap = key_map;

            if (block_cipher_factory != null) {
                _blockCipherFactory = block_cipher_factory;
            } else {
                _blockCipherFactory = DefaultBlockCipherFactory.Instance;
            }
        }

        // AP4_Processor methods
        /*----------------------------------------------------------------------
        |   AP4_CencDecryptingProcessor:CreateTrackHandler
        +---------------------------------------------------------------------*/
        public override Processor.TrackHandler CreateTrackHandler(TrakAtom trak)
        {
            // find the stsd atom
            StsdAtom stsd = trak.FindChild("mdia/minf/stbl/stsd") as StsdAtom;

            // avoid tracks with no stsd atom (should not happen)
            if (stsd == null) return null;

            // if we don't have keys, don't bother
            if (_keyMap == null) return null;

            // build a list of sample descriptions
            List<ProtectedSampleDescription> sample_descs = new List<ProtectedSampleDescription>();
            List<SampleEntry>                sample_entries = new List<SampleEntry>();
            for (uint i=0; i<stsd.GetSampleDescriptionCount(); i++) {
                SampleDescription  sample_desc  = stsd.GetSampleDescription(i);
                SampleEntry        sample_entry = stsd.GetSampleEntry(i);
                if (sample_desc == null || sample_entry == null) continue;
                if (sample_desc.GetType() == SampleDescription.Type.TYPE_PROTECTED) {
                    ProtectedSampleDescription protected_desc =
                        (ProtectedSampleDescription)sample_desc;
                    if (protected_desc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_PIFF ||
                        protected_desc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_CENC ||
                        protected_desc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_CBC1 ||
                        protected_desc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_CENS ||
                        protected_desc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_CBCS) {
                        sample_descs.Add(protected_desc);
                        sample_entries.Add(sample_entry);
                    }
                }
            }
            if (sample_entries.Count == 0) return null;

            // get the key for this track
            DataBuffer key = GetKeyForTrak(trak.GetId(), sample_descs.Count != 0 ? sample_descs[0] : null);

            // create a decrypter with this key
            if (key != null) {
                CencTrackDecrypter handler = null;
                Ap4Result result = CencTrackDecrypter.Create(key.GetData(),
                                                             key.GetDataSize(),
                                                             sample_descs,
                                                             sample_entries,
                                                             out handler);
                if (result.Failed()) return null;
                return handler;
            }

            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencDecryptingProcessor::CreateFragmentHandler
        +---------------------------------------------------------------------*/
        public override Processor.FragmentHandler CreateFragmentHandler(TrakAtom       trak,
                                                                         TrexAtom       trex,
                                                                         ContainerAtom  traf,
                                                                         ByteStream     moof_data,
                                                                         ulong          moof_offset)
        {
            // find the matching track handler to get to the track sample description
            DataBuffer  key = null;
            ProtectedSampleDescription sample_description = null;
            for (int i=0; i<_trackIds.Count; i++) {
                TfhdAtom  tfhd = traf.GetChild(AtomType.TFHD) as TfhdAtom;
                if (tfhd != null && _trackIds[i] == tfhd.GetTrackId()) {
                    // look for the Track Encryption Box
                    CencTrackDecrypter track_decrypter =
                        _trackHandlers[i] as CencTrackDecrypter;
                    if (track_decrypter != null) {
                        uint index = trex.GetDefaultSampleDescriptionIndex();
                        uint tfhd_flags = tfhd.GetFlags();
                        if ((tfhd_flags & Ap4.TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT) != 0) {
                            index = tfhd.GetSampleDescriptionIndex();
                        }
                        if (index >= 1) {
                            sample_description = track_decrypter.GetSampleDescription(index-1);
                        }
                        if (sample_description == null) return null;

                        // get the key for this track
                        key = GetKeyForTrak(tfhd.GetTrackId(), sample_description);
                    }

                    break;
                }
            }
            if (sample_description == null || key == null) return null;

            // create the sample decrypter for the fragment
            CencSampleDecrypter  sample_decrypter = null;
            SaioAtom saio = null;
            SaizAtom saiz = null;
            ICencSampleEncryption sample_encryption_atom = null;
            Ap4Result result = CencSampleDecrypter.Create(
                sample_description,
                traf,
                moof_data,
                moof_offset,
                key.GetData(),
                key.GetDataSize(),
                _blockCipherFactory,
                out saio,
                out saiz,
                out sample_encryption_atom,
                out sample_decrypter);
            if (result.Failed()) return null;

            return new CencFragmentDecrypter(sample_decrypter, saio, saiz, sample_encryption_atom);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencDecryptingProcessor:GetKeyForTrak
        +---------------------------------------------------------------------*/
        protected DataBuffer GetKeyForTrak(uint track_id, ProtectedSampleDescription sample_description)
        {
            // look for the key by track ID
            DataBuffer  key = _keyMap.GetKey(track_id);
            if (key == null) {
                // no key found by track ID, look for a key by KID
                if (sample_description != null) {
                    ProtectionSchemeInfo  scheme_info = sample_description.GetSchemeInfo();
                    if (scheme_info != null) {
                        ContainerAtom  schi = scheme_info.GetSchiAtom();
                        if (schi != null) {
                            TencAtom tenc = schi.FindChild("tenc") as TencAtom;
                            if (tenc != null) {
                                byte[] kid = tenc.GetDefaultKid();
                                if (kid != null) {
                                    key = _keyMap.GetKeyByKid(kid);
                                }
                            }
                        }
                    }
                }
            }

            return key;
        }

        // members
        protected BlockCipherFactory _blockCipherFactory;
        protected ProtectionKeyMap   _keyMap;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencSingleSampleDecrypter
    +---------------------------------------------------------------------*/
    public class CencSingleSampleDecrypter
    {
        /*----------------------------------------------------------------------
        |   AP4_CencSingleSampleDecrypter::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(uint                           cipher_type,
                                       byte[]                         key,
                                       uint                           key_size,
                                       byte                           crypt_byte_block,
                                       byte                           skip_byte_block,
                                       BlockCipherFactory             block_cipher_factory,
                                       bool                           reset_iv_at_each_subsample,
                                       out CencSingleSampleDecrypter  decrypter)
        {
            // PORT NOTE: upstream leaves the output argument untouched on error
            decrypter = null;

            // check the parameters
            if (key == null) return Ap4Result.ERROR_INVALID_PARAMETERS;

            // use the default cipher factory if  none was passed
            if (block_cipher_factory == null) {
                block_cipher_factory = DefaultBlockCipherFactory.Instance;
            }

            // create the cipher
            StreamCipher  stream_cipher    = null;
            bool          full_blocks_only = false;
            switch (cipher_type) {
                case Ap4.CENC_CIPHER_NONE:
                    break;

                case Ap4.CENC_CIPHER_AES_128_CTR: {
                    // create the block cipher
                    BlockCipher block_cipher = null;
                    BlockCipher.CtrParams ctr_params = new BlockCipher.CtrParams();
                    ctr_params.counter_size = 8;
                    Ap4Result result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                         BlockCipher.CipherDirection.DECRYPT,
                                                                         BlockCipher.CipherMode.CTR,
                                                                         ctr_params,
                                                                         key,
                                                                         key_size,
                                                                         out block_cipher);
                    if (result.Failed()) return result;

                    // create the stream cipher
                    stream_cipher = new CtrStreamCipher(block_cipher, 8);
                    break;
                }

                case Ap4.CENC_CIPHER_AES_128_CBC: {
                    // create the block cipher
                    BlockCipher block_cipher = null;
                    Ap4Result result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                         BlockCipher.CipherDirection.DECRYPT,
                                                                         BlockCipher.CipherMode.CBC,
                                                                         null,
                                                                         key,
                                                                         key_size,
                                                                         out block_cipher);
                    if (result.Failed()) return result;

                    // create the stream cipher
                    stream_cipher = new CbcStreamCipher(block_cipher);

                    // with CBC, we only use full blocks
                    full_blocks_only = true;
                    break;
                }

                default:
                    return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            // wrap with a pattern processor if needed
            if (crypt_byte_block != 0 && skip_byte_block != 0) {
                stream_cipher = new PatternStreamCipher(stream_cipher, crypt_byte_block, skip_byte_block);
            }

            // create the decrypter
            decrypter = new CencSingleSampleDecrypter(stream_cipher, full_blocks_only, reset_iv_at_each_subsample);

            return Ap4Result.SUCCESS;
        }

        // methods
        public CencSingleSampleDecrypter(StreamCipher cipher)
        {
            _cipher         = cipher;
            _fullBlocksOnly = false;
            // (m_ResetIvAtEachSubsample is left uninitialized upstream; false here)
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSingleSampleDecrypter::DecryptSampleData
        +---------------------------------------------------------------------*/
        public virtual Ap4Result DecryptSampleData(DataBuffer data_in,
                                                   DataBuffer data_out,

                                                   // always 16 bytes
                                                   byte[]     iv,

                                                   // pass 0 for full decryption
                                                   uint       subsample_count,

                                                   // array of <subsample_count> integers. NULL if subsample_count is 0
                                                   ushort[]   bytes_of_cleartext_data,

                                                   // array of <subsample_count> integers. NULL if subsample_count is 0
                                                   uint[]     bytes_of_encrypted_data)
        {
            // the output has the same size as the input
            data_out.SetDataSize(data_in.GetDataSize());

            // check input parameters
            if (iv == null) return Ap4Result.ERROR_INVALID_PARAMETERS;
            if (subsample_count != 0) {
                if (bytes_of_cleartext_data == null || bytes_of_encrypted_data == null) {
                    return Ap4Result.ERROR_INVALID_PARAMETERS;
                }
            }

            // shortcut for NULL ciphers
            if (_cipher == null) {
                Array.Copy(data_in.GetData(), 0, data_out.UseData(), 0, (int)data_in.GetDataSize());
                return Ap4Result.SUCCESS;
            }

            // setup direct pointers to the buffers
            byte[] @in = data_in.GetData();
            byte[] @out = data_out.UseData();
            uint   in_pos = 0;
            uint   out_pos = 0;

            // setup the IV
            _cipher.SetIV(iv);

            if (subsample_count != 0) {
                // process the sample data, one sub-sample at a time
                uint in_end = data_in.GetDataSize();
                for (uint i=0; i<subsample_count; i++) {
                    ushort cleartext_size = bytes_of_cleartext_data[i];
                    uint   encrypted_size = bytes_of_encrypted_data[i];

                    // check bounds
                    // PORT NOTE: computed in 64 bits (upstream's 32-bit sum can wrap around
                    // and let an oversized subsample through, which then overruns the buffers)
                    if (in_pos > in_end || (ulong)(in_end-in_pos) < (ulong)cleartext_size+encrypted_size) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }

                    // copy the cleartext portion
                    if (cleartext_size != 0) {
                        Array.Copy(@in, (int)in_pos, @out, (int)out_pos, cleartext_size);
                    }

                    // decrypt the rest
                    if (encrypted_size != 0) {
                        if (_resetIvAtEachSubsample) {
                            _cipher.SetIV(iv);
                        }

                        Ap4Result result = _cipher.ProcessBuffer(@in, (int)(in_pos+cleartext_size), encrypted_size, @out, (int)(out_pos+cleartext_size), ref encrypted_size, false);
                        if (result.Failed()) return result;
                    }

                    // move the pointers and udate counters
                    in_pos  += cleartext_size+encrypted_size;
                    out_pos += cleartext_size+encrypted_size;
                }

                // copy any leftover partial block
                // PORT NOTE: `in_pos <= in_end` check added (the cipher may report an output
                // size that is larger than its input, in which case upstream computes a huge
                // "partial" size and overruns the buffers)
                if (in_pos <= in_end) {
                    uint partial = (uint)(in_end-in_pos);
                    if (partial != 0) {
                        Array.Copy(@in, (int)in_pos, @out, (int)out_pos, (int)partial);
                    }
                }
            } else {
                if (_fullBlocksOnly) {
                    uint block_count = data_in.GetDataSize()/16;
                    if (block_count != 0) {
                        uint out_size = data_out.GetDataSize();
                        Ap4Result result = _cipher.ProcessBuffer(@in, (int)in_pos, block_count*16, @out, (int)out_pos, ref out_size, false);
                        if (result.Failed()) return result;
                        Ap4.ASSERT(out_size == block_count*16);
                        in_pos  += block_count*16;
                        out_pos += block_count*16;
                    }

                    // any partial block at the end remains in the clear
                    uint partial = data_in.GetDataSize()%16;
                    if (partial != 0) {
                        Array.Copy(@in, (int)in_pos, @out, (int)out_pos, (int)partial);
                    }
                } else {
                    // process the entire sample data at once
                    uint encrypted_size = data_in.GetDataSize();
                    Ap4Result result = _cipher.ProcessBuffer(@in, (int)in_pos, encrypted_size, @out, (int)out_pos, ref encrypted_size, true);
                    if (result.Failed()) return result;
                }
            }

            return Ap4Result.SUCCESS;
        }

        // constructor
        private CencSingleSampleDecrypter(StreamCipher  cipher,
                                          bool          full_blocks_only,
                                          bool          reset_iv_at_each_subsample)
        {
            _cipher                 = cipher;
            _fullBlocksOnly         = full_blocks_only;
            _resetIvAtEachSubsample = reset_iv_at_each_subsample;
        }

        // members
        private StreamCipher  _cipher;
        private bool          _fullBlocksOnly;
        private bool          _resetIvAtEachSubsample;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencSampleDecrypter
    +---------------------------------------------------------------------*/
    public class CencSampleDecrypter : SampleDecrypter
    {
        /*----------------------------------------------------------------------
        |   AP4_CencSampleDecrypter::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(ProtectedSampleDescription sample_description,
                                       ContainerAtom              traf,
                                       ByteStream                 aux_info_data,
                                       ulong                      aux_info_data_offset,
                                       byte[]                     key,
                                       uint                       key_size,
                                       BlockCipherFactory         block_cipher_factory,
                                       out CencSampleDecrypter    decrypter)
        {
            SaioAtom saio = null;
            SaizAtom saiz = null;
            ICencSampleEncryption sample_encryption_atom = null;
            return Create(sample_description,
                          traf,
                          aux_info_data,
                          aux_info_data_offset,
                          key,
                          key_size,
                          block_cipher_factory,
                          out saio,
                          out saiz,
                          out sample_encryption_atom,
                          out decrypter);
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleDecrypter::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(ProtectedSampleDescription sample_description,
                                       ContainerAtom              traf,
                                       ByteStream                 aux_info_data,
                                       ulong                      aux_info_data_offset,
                                       byte[]                     key,
                                       uint                       key_size,
                                       BlockCipherFactory         block_cipher_factory,
                                       out SaioAtom               saio,                   // [out]
                                       out SaizAtom               saiz,                   // [out]
                                       out ICencSampleEncryption  sample_encryption_atom, // [out]
                                       out CencSampleDecrypter    decrypter)
        {
            // default return values
            saio                   = null;
            saiz                   = null;
            sample_encryption_atom = null;
            decrypter              = null;

            // create the sample info table
            CencSampleInfoTable  sample_info_table = null;
            uint                 protection_sheme  = 0;
            bool                 reset_iv_at_each_subsample = false;
            Ap4Result result = CencSampleInfoTable.Create(sample_description,
                                                          traf,
                                                          out saio,
                                                          out saiz,
                                                          out sample_encryption_atom,
                                                          out protection_sheme,
                                                          out reset_iv_at_each_subsample,
                                                          aux_info_data,
                                                          aux_info_data_offset,
                                                          out sample_info_table);
            if (result.Failed()) {
                return result;
            }

            // we now have all the info we need to create the decrypter
            return Create(sample_info_table,
                          protection_sheme,
                          key,
                          key_size,
                          block_cipher_factory,
                          reset_iv_at_each_subsample,
                          out decrypter);
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleDecrypter::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(CencSampleInfoTable      sample_info_table,
                                       uint                     cipher_type,
                                       byte[]                   key,
                                       uint                     key_size,
                                       BlockCipherFactory       block_cipher_factory,
                                       bool                     reset_iv_at_each_subsample,
                                       out CencSampleDecrypter  decrypter)
        {
            // default return value
            decrypter = null;

            // check some basic paramaters
            uint iv_size = sample_info_table.GetIvSize();
            switch (cipher_type) {
                case Ap4.CENC_CIPHER_NONE:
                    break;

                case Ap4.CENC_CIPHER_AES_128_CTR:
                    if (iv_size != 8 && iv_size != 16) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                    break;

                case Ap4.CENC_CIPHER_AES_128_CBC:
                    if (iv_size != 16) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                    break;

                default:
                    return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            // create a single-sample decrypter
            CencSingleSampleDecrypter single_sample_decrypter = null;
            Ap4Result result = CencSingleSampleDecrypter.Create(cipher_type,
                                                                key,
                                                                key_size,
                                                                sample_info_table.GetCryptByteBlock(),
                                                                sample_info_table.GetSkipByteBlock(),
                                                                block_cipher_factory,
                                                                reset_iv_at_each_subsample,
                                                                out single_sample_decrypter);
            if (result.Failed()) return result;

            // create the decrypter
            decrypter = new CencSampleDecrypter(single_sample_decrypter, sample_info_table);

            return Ap4Result.SUCCESS;
        }

        // methods
        public CencSampleDecrypter(CencSingleSampleDecrypter single_sample_decrypter,
                                   CencSampleInfoTable       sample_info_table)
        {
            _singleSampleDecrypter = single_sample_decrypter;
            _sampleInfoTable       = sample_info_table;
            _sampleCursor          = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleDecrypter::SetSampleIndex
        +---------------------------------------------------------------------*/
        public override Ap4Result SetSampleIndex(uint sample_index)
        {
            _sampleCursor = sample_index;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencSampleDecrypter::DecryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result DecryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    byte[]     iv = null)
        {
            // increment the sample cursor
            uint sample_cursor = _sampleCursor++;

            // setup the IV
            byte[] iv_block = new byte[16];
            if (iv == null) {
                iv = _sampleInfoTable.GetIv(sample_cursor);
            }
            if (iv == null) return Ap4Result.ERROR_INVALID_FORMAT;
            uint iv_size = _sampleInfoTable.GetIvSize();
            // PORT NOTE: the copy is clamped to the 16 bytes of iv_block and to the
            // bytes available in `iv` (upstream copies iv_size bytes unconditionally)
            Array.Copy(iv, 0, iv_block, 0, (int)Math.Min(Math.Min(iv_size, 16u), (uint)iv.Length));
            // (iv_block is zero-initialized: AP4_SetMemory(&iv_block[iv_size], 0, 16-iv_size))

            // get the subsample info for this sample if needed
            uint      subsample_count = 0;
            ushort[]  bytes_of_cleartext_data = null;
            uint[]    bytes_of_encrypted_data = null;
            if (_sampleInfoTable != null) {
                Ap4Result result = _sampleInfoTable.GetSampleInfo(sample_cursor, out subsample_count, out bytes_of_cleartext_data, out bytes_of_encrypted_data);
                if (result.Failed()) return result;
            }

            // decrypt the sample
            return _singleSampleDecrypter.DecryptSampleData(data_in, data_out, iv_block, subsample_count, bytes_of_cleartext_data, bytes_of_encrypted_data);
        }

        protected CencSingleSampleDecrypter _singleSampleDecrypter;
        protected CencSampleInfoTable       _sampleInfoTable;
        protected uint                      _sampleCursor;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencTrackDecrypter
    +---------------------------------------------------------------------*/
    internal class CencTrackDecrypter : Processor.TrackHandler {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_CencTrackDecrypter::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(byte[]                           key,
                                       uint                             key_size,
                                       List<ProtectedSampleDescription> sample_descriptions,
                                       List<SampleEntry>                sample_entries,
                                       out CencTrackDecrypter           decrypter)
        {
            decrypter = null;

            // check and set defaults
            if (key == null) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            // instantiate the object
            decrypter = new CencTrackDecrypter(sample_descriptions,
                                               sample_entries,
                                               sample_descriptions[0].GetOriginalFormat());
            return Ap4Result.SUCCESS;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencTrackDecrypter::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessSample(DataBuffer data_in,
                                                DataBuffer data_out)
        {
            data_out.SetData(data_in.GetData(), data_in.GetDataSize());
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencTrackDecrypter::ProcessTrack
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessTrack()
        {
            for (int i=0; i<_sampleEntries.Count; i++) {
                _sampleEntries[i].SetType(_originalFormat);
                _sampleEntries[i].DeleteChild(AtomType.SINF);
            }
            return Ap4Result.SUCCESS;
        }

        // accessors
        public ProtectedSampleDescription GetSampleDescription(uint i) {
            if (i < (uint)_sampleDescriptions.Count) {
                return _sampleDescriptions[(int)i];
            } else {
                return null;
            }
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_CencTrackDecrypter::AP4_CencTrackDecrypter
        +---------------------------------------------------------------------*/
        private CencTrackDecrypter(List<ProtectedSampleDescription> sample_descriptions,
                                   List<SampleEntry>                sample_entries,
                                   uint                             original_format)
        {
            _originalFormat = original_format;

            for (int i=0; i<sample_descriptions.Count; i++) {
                _sampleDescriptions.Add(sample_descriptions[i]);
            }
            for (int i=0; i<sample_entries.Count; i++) {
                _sampleEntries.Add(sample_entries[i]);
            }
        }

        // members
        private List<ProtectedSampleDescription> _sampleDescriptions = new List<ProtectedSampleDescription>();
        private List<SampleEntry>                _sampleEntries = new List<SampleEntry>();
        private uint                             _originalFormat;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencFragmentDecrypter
    +---------------------------------------------------------------------*/
    internal class CencFragmentDecrypter : Processor.FragmentHandler {
        // constructor
        public CencFragmentDecrypter(CencSampleDecrypter    sample_decrypter, // ownership is transfered
                                     SaioAtom               saio_atom,
                                     SaizAtom               saiz_atom,
                                     ICencSampleEncryption  sample_encryption_atom)
        {
            _sampleDecrypter      = sample_decrypter;
            _saioAtom             = saio_atom;
            _saizAtom             = saiz_atom;
            _sampleEncryptionAtom = sample_encryption_atom;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_CencFragmentDecrypter::ProcessFragment
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessFragment()
        {
            // detach the sample encryption atom
            if (_sampleDecrypter != null) {
                if (_saioAtom != null) _saioAtom.Detach();
                if (_saizAtom != null) _saizAtom.Detach();
                if (_sampleEncryptionAtom != null) _sampleEncryptionAtom.GetOuter().Detach();
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencFragmentDecrypter::FinishFragment
        +---------------------------------------------------------------------*/
        public override Ap4Result FinishFragment()
        {
            if (_sampleDecrypter != null) {
                _saioAtom = null;
                _saizAtom = null;
                _sampleEncryptionAtom = null;
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_CencFragmentDecrypter::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessSample(DataBuffer data_in,
                                                DataBuffer data_out)
        {
            // decrypt the sample
            return _sampleDecrypter.DecryptSampleData(data_in, data_out, null);
        }

        // members
        private CencSampleDecrypter    _sampleDecrypter;
        private SaioAtom               _saioAtom;
        private SaizAtom               _saizAtom;
        private ICencSampleEncryption  _sampleEncryptionAtom;
    }

    /*----------------------------------------------------------------------
    |   AP4_CencSampleEncryptionInformationGroupEntry
    +---------------------------------------------------------------------*/
    public class CencSampleEncryptionInformationGroupEntry {
        /*----------------------------------------------------------------------
        |   AP4_CencSampleEncryptionInformationGroupEntry::AP4_CencSampleEncryptionInformationGroupEntry
        +---------------------------------------------------------------------*/
        public CencSampleEncryptionInformationGroupEntry(byte[] data)
        {
            _isEncrypted = ((data[2] & 1) != 0);
            _ivSize      = data[3];
            Array.Copy(data, 4, _kid, 0, 16);
        }

        // accessors
        public bool   IsEncrypted() { return _isEncrypted; }
        public byte   GetIvSize()   { return _ivSize;      }
        // PORT NOTE: upstream returns `&m_KID[9]` (a pointer to the 10th byte of the
        // KID, which looks like an upstream typo). The port returns a copy of what
        // that pointer validly addresses: the last 7 bytes of the KID.
        public byte[] GetKid()      { byte[] kid = new byte[7]; Array.Copy(_kid, 9, kid, 0, 7); return kid; }

        // members
        private bool   _isEncrypted;
        private byte   _ivSize;
        private byte[] _kid = new byte[16];
    }
}
