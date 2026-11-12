// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Protection.h, Ap4Protection.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
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
        // this is fixed for now
        public const uint PROTECTION_KEY_LENGTH = 16;

        public const uint PROTECTION_SCHEME_TYPE_ITUNES = 0x6974756e; // 'itun'
    }

    /*----------------------------------------------------------------------
    |   AP4_EncaSampleEntry
    +---------------------------------------------------------------------*/
    public class EncaSampleEntry : AudioSampleEntry
    {
        // methods
        /*----------------------------------------------------------------------
        |   AP4_EncaSampleEntry::AP4_EncaSampleEntry
        +---------------------------------------------------------------------*/
        public EncaSampleEntry(uint        type,
                               uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(type, size, stream, atom_factory)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_EncaSampleEntry::AP4_EncaSampleEntry
        +---------------------------------------------------------------------*/
        public EncaSampleEntry(uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(AtomType.ENCA, size, stream, atom_factory)
        {
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_EncaSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // get the original sample format
            FrmaAtom frma = FindChild("sinf/frma") as FrmaAtom;

            // get the schi atom
            ContainerAtom schi;
            schi = FindChild("sinf/schi") as ContainerAtom;

            // get the scheme info
            SchmAtom schm = FindChild("sinf/schm") as SchmAtom;
            uint original_format = frma != null?frma.GetOriginalFormat():AtomType.MP4A;
            if (schm != null) {
                // create the original sample description
                return new ProtectedSampleDescription(
                    _type,
                    ToTargetSampleDescription(original_format),
                    original_format,
                    schm.GetSchemeType(),
                    schm.GetSchemeVersion(),
                    schm.GetSchemeUri(),
                    schi);
            } else if (schi != null) {
                // try to see if we can guess the protection scheme from the 'schi' contents
                Atom odkm = schi.GetChild(AtomType.ODKM);
                if (odkm != null) {
                    // create the original sample description
                    return new ProtectedSampleDescription(
                        _type,
                        ToTargetSampleDescription(original_format),
                        original_format,
                        Ap4.PROTECTION_SCHEME_TYPE_OMA,
                        Ap4.PROTECTION_SCHEME_VERSION_OMA_20,
                        null,
                        schi);
                }
            }

            // unknown scheme
            return null;
        }

        // this method is used as a factory by the ISMACryp classes
        // NOTE: this should be named ToSampleDescription, but C++ has a
        // problem with that because the base class does not have this
        // overloaded method, but has another other one by that name
        /*----------------------------------------------------------------------
        |   AP4_EncaSampleEntry::ToTargetSampleDescription
        +---------------------------------------------------------------------*/
        public virtual SampleDescription ToTargetSampleDescription(uint format)
        {
            switch (format) {
                case AtomType.MP4A: {
                    EsdsAtom esds = GetChild(AtomType.ESDS) as EsdsAtom;
                    if (esds == null) {
                        // check if this is a quicktime style sample description
                        if (_qtVersion > 0) {
                            esds = FindChild("wave/esds") as EsdsAtom;
                        }
                    }
                    return new MpegAudioSampleDescription(
                        GetSampleRate(),
                        GetSampleSize(),
                        GetChannelCount(),
                        esds);
                }

                default:
                    return new GenericAudioSampleDescription(
                        format,
                        GetSampleRate(),
                        GetSampleSize(),
                        GetChannelCount(),
                        this);
            }
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_EncvSampleEntry
    +---------------------------------------------------------------------*/
    public class EncvSampleEntry : VisualSampleEntry
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_EncvSampleEntry::AP4_EncvSampleEntry
        +---------------------------------------------------------------------*/
        public EncvSampleEntry(uint        type,
                               uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(type, size, stream, atom_factory)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_EncvSampleEntry::AP4_EncvSampleEntry
        +---------------------------------------------------------------------*/
        public EncvSampleEntry(uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(AtomType.ENCV, size, stream, atom_factory)
        {
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_EncvSampleEntry::ToSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription ToSampleDescription()
        {
            // get the original sample format
            FrmaAtom frma = FindChild("sinf/frma") as FrmaAtom;

            // get the schi atom
            ContainerAtom schi;
            schi = FindChild("sinf/schi") as ContainerAtom;

            // get the scheme info
            SchmAtom schm = FindChild("sinf/schm") as SchmAtom;
            uint original_format = frma != null?frma.GetOriginalFormat():AtomType.MP4V;
            if (schm != null) {
                // create the sample description
                return new ProtectedSampleDescription(
                    _type,
                    ToTargetSampleDescription(original_format),
                    original_format,
                    schm.GetSchemeType(),
                    schm.GetSchemeVersion(),
                    schm.GetSchemeUri(),
                    schi);
            } else if (schi != null) {
                // try to see if we can guess the protection scheme from the 'schi' contents
                Atom odkm = schi.GetChild(AtomType.ODKM);
                if (odkm != null) {
                    // create the original sample description
                    return new ProtectedSampleDescription(
                        _type,
                        ToTargetSampleDescription(original_format),
                        original_format,
                        Ap4.PROTECTION_SCHEME_TYPE_OMA,
                        Ap4.PROTECTION_SCHEME_VERSION_OMA_20,
                        null,
                        schi);
                }
            }

            // unknown scheme
            return null;

        }

        // this method is used as a factory by the ISMACryp classes
        // NOTE: this should be named ToSampleDescription, but C++ has a
        // problem with that because the base class does not have this
        // overloaded method, but has another other one by that name
        /*----------------------------------------------------------------------
        |   AP4_EncvSampleEntry::ToTargetSampleDescription
        +---------------------------------------------------------------------*/
        public virtual SampleDescription ToTargetSampleDescription(uint format)
        {
            switch (format) {
                case Ap4.SAMPLE_FORMAT_AVC1:
                case Ap4.SAMPLE_FORMAT_AVC2:
                case Ap4.SAMPLE_FORMAT_AVC3:
                case Ap4.SAMPLE_FORMAT_AVC4:
                case Ap4.SAMPLE_FORMAT_DVAV:
                case Ap4.SAMPLE_FORMAT_DVA1:
                    return new AvcSampleDescription(
                        format,
                        _width,
                        _height,
                        _depth,
                        _compressorName,
                        (AtomParent)this);

                case Ap4.SAMPLE_FORMAT_HVC1:
                case Ap4.SAMPLE_FORMAT_HEV1:
                case Ap4.SAMPLE_FORMAT_DVHE:
                case Ap4.SAMPLE_FORMAT_DVH1:
                    return new HevcSampleDescription(
                        format,
                        _width,
                        _height,
                        _depth,
                        _compressorName,
                        (AtomParent)this);

                case Ap4.SAMPLE_FORMAT_AV01:
                    return new Av1SampleDescription(
                        format,
                        _width,
                        _height,
                        _depth,
                        _compressorName,
                        (AtomParent)this);

                case AtomType.MP4V:
                    return new MpegVideoSampleDescription(
                        _width,
                        _height,
                        _depth,
                        _compressorName,
                        GetChild(AtomType.ESDS) as EsdsAtom);

                default:
                    return new GenericVideoSampleDescription(
                        format,
                        _width,
                        _height,
                        _depth,
                        _compressorName,
                        this);
            }
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_DrmsSampleEntry
    +---------------------------------------------------------------------*/
    public class DrmsSampleEntry : EncaSampleEntry
    {
        // methods
        /*----------------------------------------------------------------------
        |   AP4_DrmsSampleEntry::AP4_DrmsSampleEntry
        +---------------------------------------------------------------------*/
        public DrmsSampleEntry(uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(AtomType.DRMS, size, stream, atom_factory)
        {
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_DrmiSampleEntry
    +---------------------------------------------------------------------*/
    public class DrmiSampleEntry : EncvSampleEntry
    {
        // methods
        /*----------------------------------------------------------------------
        |   AP4_DrmiSampleEntry::AP4_DrmiSampleEntry
        +---------------------------------------------------------------------*/
        public DrmiSampleEntry(uint        size,
                               ByteStream  stream,
                               AtomFactory atom_factory) :
            base(AtomType.DRMI, size, stream, atom_factory)
        {
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_ProtectionKeyMap
    +---------------------------------------------------------------------*/
    // PORT NOTE: keys, IVs and KIDs are `byte[]` read from offset 0 (a KID is 16
    // bytes); a NULL pointer is a null array.
    public class ProtectionKeyMap
    {
        // constructors and destructor
        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::AP4_ProtectionKeyMap
        +---------------------------------------------------------------------*/
        public ProtectionKeyMap()
        {
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::SetKey
        +---------------------------------------------------------------------*/
        public Ap4Result SetKey(uint    track_id,
                                byte[]  key,
                                uint    key_size,
                                byte[]  iv = null,
                                uint    iv_size = 0)
        {
            KeyEntry entry = GetEntry(track_id);
            if (entry == null) {
                _keyEntries.Add(new KeyEntry(track_id, key, key_size, iv, iv_size));
            } else {
                entry.SetKey(key, key_size, iv, iv_size);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::SetKeyForKid
        +---------------------------------------------------------------------*/
        public Ap4Result SetKeyForKid(byte[]  kid,
                                      byte[]  key,
                                      uint    key_size,
                                      byte[]  iv = null,
                                      uint    iv_size = 0)
        {
            KeyEntry entry = GetEntryByKid(kid);
            if (entry == null) {
                _keyEntries.Add(new KeyEntry(kid, key, key_size, iv, iv_size));
            } else {
                entry.SetKey(key, key_size, iv, iv_size);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::SetKey
        +---------------------------------------------------------------------*/
        public Ap4Result SetKeys(ProtectionKeyMap key_map)
        {
            // (as upstream, the entries are copied by track ID: the KIDs are not copied)
            for (int item = 0; item < key_map._keyEntries.Count; item++) {
                KeyEntry entry = key_map._keyEntries[item];
                _keyEntries.Add(new KeyEntry(entry._trackId,
                                             entry._key.GetData(),
                                             entry._key.GetDataSize(),
                                             entry._iV.GetData(),
                                             entry._iV.GetDataSize()));
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::GetKeyAndIv
        +---------------------------------------------------------------------*/
        public Ap4Result GetKeyAndIv(uint            track_id,
                                     out DataBuffer  key,
                                     out DataBuffer iv)
        {
            KeyEntry entry = GetEntry(track_id);
            if (entry != null) {
                key = entry._key;
                iv = entry._iV;
                return Ap4Result.SUCCESS;
            } else {
                key = null;
                iv = null;
                return Ap4Result.ERROR_NO_SUCH_ITEM;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::GetKeyAndIvByKid
        +---------------------------------------------------------------------*/
        public Ap4Result GetKeyAndIvByKid(byte[]          kid,
                                          out DataBuffer  key,
                                          out DataBuffer iv)
        {
            KeyEntry entry = GetEntryByKid(kid);
            if (entry != null) {
                key = entry._key;
                iv = entry._iV;
                return Ap4Result.SUCCESS;
            } else {
                key = null;
                iv = null;
                return Ap4Result.ERROR_NO_SUCH_ITEM;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::GetKey
        +---------------------------------------------------------------------*/
        public DataBuffer GetKey(uint track_id)
        {
            KeyEntry entry = GetEntry(track_id);
            if (entry != null) {
                return entry._key;
            } else {
                return null;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::GetKeyByKid
        +---------------------------------------------------------------------*/
        public DataBuffer GetKeyByKid(byte[] kid)
        {
            KeyEntry entry = GetEntryByKid(kid);
            if (entry != null) {
                return entry._key;
            } else {
                return null;
            }
        }

        // types
        private class KeyEntry {
            /*----------------------------------------------------------------------
            |   AP4_ProtectionKeyMap::KeyEntry::KeyEntry
            +---------------------------------------------------------------------*/
            public KeyEntry(uint    track_id,
                            byte[]  key,
                            uint    key_size,
                            byte[]  iv = null,
                            uint    iv_size = 0)
            {
                _trackId = track_id;
                Array.Clear(_kID, 0, 16);
                SetKey(key, key_size, iv, iv_size);
            }

            /*----------------------------------------------------------------------
            |   AP4_ProtectionKeyMap::KeyEntry::KeyEntry
            +---------------------------------------------------------------------*/
            public KeyEntry(byte[]  kid,
                            byte[]  key,
                            uint    key_size,
                            byte[]  iv = null,
                            uint    iv_size = 0)
            {
                _trackId = 0;
                Array.Copy(kid, 0, _kID, 0, 16);
                SetKey(key, key_size, iv, iv_size);
            }

            /*----------------------------------------------------------------------
            |   AP4_ProtectionKeyMap::KeyEntry::SetKey
            +---------------------------------------------------------------------*/
            public void SetKey(byte[] key, uint key_size,
                               byte[] iv,  uint iv_size)
            {
                if (key != null) {
                    _key.SetData(key, key_size);
                }
                if (iv != null) {
                    _iV.SetData(iv, iv_size);
                } else {
                    _iV.SetDataSize(16);
                    Array.Clear(_iV.UseData(), 0, 16);
                }
            }
            public uint       _trackId;
            public byte[]     _kID = new byte[16];
            public DataBuffer _key = new DataBuffer();
            public DataBuffer _iV = new DataBuffer();
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::GetEntry
        +---------------------------------------------------------------------*/
        private KeyEntry GetEntry(uint track_id)
        {
            for (int item = 0; item < _keyEntries.Count; item++) {
                KeyEntry entry = _keyEntries[item];
                if (entry._trackId == track_id) return entry;
            }

            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_ProtectionKeyMap::GetEntryByKid
        +---------------------------------------------------------------------*/
        private KeyEntry GetEntryByKid(byte[] kid)
        {
            for (int item = 0; item < _keyEntries.Count; item++) {
                KeyEntry entry = _keyEntries[item];
                bool equal = true;
                for (int i = 0; i < 16; i++) {
                    if (entry._kID[i] != kid[i]) {
                        equal = false;
                        break;
                    }
                }
                if (equal) return entry;
            }

            return null;
        }

        // members
        private List<KeyEntry> _keyEntries = new List<KeyEntry>();
    }

    /*----------------------------------------------------------------------
    |   AP4_TrackPropertyMap
    +---------------------------------------------------------------------*/
    public class TrackPropertyMap
    {
        // types
        public class Entry {
            public Entry(uint track_id, string name, string value) {
                _trackId = track_id;
                _name = CString(name);
                _value = CString(value);
            }
            public uint   _trackId;
            public string _name;
            public string _value;

            // PORT NOTE: AP4_String(const char*): empty for NULL, stops at the
            // first NUL character
            private static string CString(string s) {
                if (s == null) return "";
                int nul = s.IndexOf('\0');
                return nul < 0 ? s : s.Substring(0, nul);
            }
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_TrackPropertyMap::SetProperty
        +---------------------------------------------------------------------*/
        public Ap4Result SetProperty(uint    track_id,
                                     string  name,
                                     string value)
        {
            _entries.Add(new Entry(track_id, name, value));
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrackPropertyMap::SetProperties
        +---------------------------------------------------------------------*/
        public Ap4Result SetProperties(TrackPropertyMap properties)
        {
            for (int item = 0; item < properties._entries.Count; item++) {
                Entry entry = properties._entries[item];
                _entries.Add(new Entry(entry._trackId,
                                       entry._name,
                                       entry._value));
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrackPropertyMap::GetProperty
        +---------------------------------------------------------------------*/
        public string GetProperty(uint track_id, string name)
        {
            for (int item = 0; item < _entries.Count; item++) {
                Entry entry = _entries[item];
                if (entry._trackId == track_id &&
                    CompareStrings(entry._name, name) == 0) {
                    return entry._value;
                }
            }

            // not found
            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrackPropertyMap::GetTextualHeaders
        +---------------------------------------------------------------------*/
        public Ap4Result GetTextualHeaders(uint track_id, DataBuffer textual_headers)
        {
            uint      buffer_size = 0;
            Ap4Result result      = Ap4Result.SUCCESS;
            byte[]    data_buffer;
            int       data_buffer_offset = 0;

            // get the size needed for the textual headers
            for (int item = 0; item < _entries.Count; item++) {
                Entry entry = _entries[item];
                if (entry._trackId == track_id) {
                    string name = entry._name;
                    if (CompareStrings(name, "ContentId")       != 0 &&
                        CompareStrings(name, "RightsIssuerUrl") != 0 &&
                        CompareStrings(name, "KID")             != 0) {
                        buffer_size += (Ap4Utils.StringLength(entry._name)  +
                                        Ap4Utils.StringLength(entry._value) +
                                        2); // colon + nul

                    }
                }
            }

            result = textual_headers.SetDataSize(buffer_size);
            if (result.Failed()) return result;

            data_buffer = textual_headers.UseData();

            // set the textual headers
            for (int item = 0; item < _entries.Count; item++) {
                Entry entry = _entries[item];
                if (entry._trackId == track_id) {
                    string  name       = entry._name;
                    string  value      = null;
                    uint    name_len   = 0;
                    uint    value_len  = 0;

                    if (CompareStrings(name, "ContentId")       != 0 &&
                        CompareStrings(name, "RightsIssuerUrl") != 0 &&
                        CompareStrings(name, "KID")             != 0) {
                        name_len  = Ap4Utils.StringLength(entry._name);
                        value     = entry._value;
                        value_len = Ap4Utils.StringLength(entry._value);

                        // format is name:value\0
                        if (name != null && value != null) {
                            Array.Copy(Ap4Utils.StringToCStringBytes(name), 0, data_buffer, data_buffer_offset, (int)name_len);
                            data_buffer[data_buffer_offset+name_len] = (byte)':';
                            data_buffer_offset += (int)(1+name_len);
                            Array.Copy(Ap4Utils.StringToCStringBytes(value), 0, data_buffer, data_buffer_offset, (int)value_len);
                            data_buffer[data_buffer_offset+value_len] = (byte)'\0';
                            data_buffer_offset += (int)(1+value_len);
                        }
                    }
                }
            }

            // success path
            return Ap4Result.SUCCESS;
        }

        public List<Entry> GetEntries() { return _entries; }

        // PORT NOTE: AP4_CompareStrings (strcmp); only tested for equality here
        private static int CompareStrings(string s1, string s2)
        {
            return string.CompareOrdinal(s1, s2 ?? "");
        }

        // members
        private List<Entry> _entries = new List<Entry>();
    }

    /*----------------------------------------------------------------------
    |   AP4_ProtectionSchemeInfo
    +---------------------------------------------------------------------*/
    public class ProtectionSchemeInfo
    {
        // constructors and destructor
        /*----------------------------------------------------------------------
        |   AP4_ProtectionSchemeInfo::AP4_ProtectionSchemeInfo
        +---------------------------------------------------------------------*/
        public ProtectionSchemeInfo(ContainerAtom schi)
        {
            if (schi != null) {
                _schiAtom = schi.Clone() as ContainerAtom;
            } else {
                _schiAtom = null;
            }
        }

        // accessors
        public ContainerAtom GetSchiAtom() { return _schiAtom; }

        protected ContainerAtom _schiAtom;
    }

    /*----------------------------------------------------------------------
    |   AP4_ProtectedSampleDescription
    +---------------------------------------------------------------------*/
    public class ProtectedSampleDescription : SampleDescription
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_ProtectedSampleDescription::AP4_ProtectedSampleDescription
        +---------------------------------------------------------------------*/
        public ProtectedSampleDescription(uint               format,
                                          SampleDescription  original_sample_description,
                                          uint               original_format,
                                          uint               scheme_type,
                                          uint               scheme_version,
                                          string             scheme_uri,
                                          ContainerAtom      schi, // will be cloned
                                          bool               transfer_ownership_of_original = true) :
            base(Type.TYPE_PROTECTED, format, null)
        {
            _originalSampleDescription = original_sample_description;
            _originalSampleDescriptionIsOwned = transfer_ownership_of_original;
            _originalFormat = original_format;
            _schemeType = scheme_type;
            _schemeVersion = scheme_version;
            _schemeUri = scheme_uri ?? "";
            _schemeInfo = new ProtectionSchemeInfo(schi);
        }

        // accessors
        public SampleDescription GetOriginalSampleDescription() {
            return _originalSampleDescription;
        }
        public uint   GetOriginalFormat() { return _originalFormat; }
        public uint   GetSchemeType()     { return _schemeType;     }
        public uint   GetSchemeVersion()  { return _schemeVersion;  }
        public string GetSchemeUri()      { return _schemeUri;      }
        public ProtectionSchemeInfo GetSchemeInfo() {
            return _schemeInfo;
        }

        // implementation of abstract base class methods
        /*----------------------------------------------------------------------
        |   AP4_ProtectedSampleDescription::ToAtom
        +---------------------------------------------------------------------*/
        public override Atom ToAtom()
        {
            // construct the atom for the original sample description
            if (_originalSampleDescription == null) return null;
            Atom atom = _originalSampleDescription.ToAtom();

            // PORT NOTE: upstream dereferences `atom` without a check
            if (atom == null) return null;

            // switch the atom type
            atom.SetType(_format);

            // check that the constructed atom is a container
            ContainerAtom container = atom as ContainerAtom;
            if (container == null) return atom; // not a container ?? return now.

            // create the sinf atom
            ContainerAtom sinf = new ContainerAtom(AtomType.SINF);

            // create and add a frma atom
            FrmaAtom frma = new FrmaAtom(_originalFormat);
            sinf.AddChild(frma);

            // create and add a schm atom
            SchmAtom schm = new SchmAtom(_schemeType, _schemeVersion, _schemeUri);
            sinf.AddChild(schm);

            // add the schi atom
            if (_schemeInfo != null && _schemeInfo.GetSchiAtom() != null) {
                Atom schi_clone = _schemeInfo.GetSchiAtom().Clone();
                if (schi_clone != null) sinf.AddChild(schi_clone);
            }

            // add the sinf to the returned atom
            container.AddChild(sinf);

            return atom;
        }

        // members
        private SampleDescription     _originalSampleDescription;
        private bool                  _originalSampleDescriptionIsOwned;
        private uint                  _originalFormat;
        private uint                  _schemeType;
        private uint                  _schemeVersion;
        private string                _schemeUri;
        private ProtectionSchemeInfo  _schemeInfo;
    }

    /*----------------------------------------------------------------------
    |   AP4_BlockCipher
    +---------------------------------------------------------------------*/
    public abstract class BlockCipher
    {
        // types
        public enum CipherDirection {
            ENCRYPT,
            DECRYPT
        }

        public enum CipherType {
            AES_128
        }

        public enum CipherMode {
            CBC,
            CTR
        }

        public struct CtrParams {
            public uint counter_size;
        }

        // methods
        public abstract CipherDirection GetDirection();

        // PORT NOTE: `input`/`output` pointers are array + offset; `iv` is null
        // or an array whose first 16 bytes are used. The overload without
        // offsets is the upstream signature.
        public abstract Ap4Result Process(byte[]  input,
                                          int     input_offset,
                                          uint    input_size,
                                          byte[]  output,
                                          int     output_offset,
                                          byte[]  iv);

        public Ap4Result Process(byte[]  input,
                                 uint    input_size,
                                 byte[]  output,
                                 byte[]  iv)
        {
            return Process(input, 0, input_size, output, 0, iv);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_BlockCipherFactory
    +---------------------------------------------------------------------*/
    public abstract class BlockCipherFactory
    {
        // types
        public struct CtrParams {
            public uint counter_size;
        }

        // methods
        // PORT NOTE: `const void* params` is an `object?`: null, or (for the CTR
        // mode) a BlockCipher.CtrParams. `params` is a C# keyword: the parameter
        // is named `mode_params`, as in the upstream implementation.
        public abstract Ap4Result CreateCipher(BlockCipher.CipherType      type,
                                               BlockCipher.CipherDirection direction,
                                               BlockCipher.CipherMode      mode,
                                               object                      mode_params,
                                               byte[]                      key,
                                               uint                        key_size,
                                               out BlockCipher             cipher);
    }

    /*----------------------------------------------------------------------
    |   AP4_DefaultBlockCipherFactory
    +---------------------------------------------------------------------*/
    public class DefaultBlockCipherFactory : BlockCipherFactory
    {
        // class variables
        /*----------------------------------------------------------------------
        |   AP4_DefaultBlockCipherFactory::Instance
        +---------------------------------------------------------------------*/
        public static readonly DefaultBlockCipherFactory Instance = new DefaultBlockCipherFactory();

        // this member is used to detect the situation where the
        // platform's code loader does not construct static C++ objects
        /*----------------------------------------------------------------------
        |   AP4_DefaultBlockCipherFactory::AP4_DefaultBlockCipherFactory
        +---------------------------------------------------------------------*/
        public DefaultBlockCipherFactory()
        {
            _initialized = true;
        }
        public bool _initialized;

        /*----------------------------------------------------------------------
        |   AP4_DefaultBlockCipherFactory::Initialize
        +---------------------------------------------------------------------*/
        public Ap4Result Initialize()
        {
            _initialized = true;
            return Ap4Result.SUCCESS;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_DefaultBlockCipherFactory
        +---------------------------------------------------------------------*/
        public override Ap4Result CreateCipher(BlockCipher.CipherType      type,
                                               BlockCipher.CipherDirection direction,
                                               BlockCipher.CipherMode      mode,
                                               object                      mode_params,
                                               byte[]                      key,
                                               uint                        key_size,
                                               out BlockCipher             cipher)
        {
            // setup default return vaule
            cipher = null;

            switch (type) {
                case BlockCipher.CipherType.AES_128:
                    // check cipher parameters
                    if (key == null || key_size != Ap4.AES_BLOCK_SIZE) {
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }

                    // create the cipher
                    {
                        AesBlockCipher aes_cipher = null;
                        Ap4Result result = AesBlockCipher.Create(key, direction, mode, mode_params, out aes_cipher);
                        if (result.Failed()) return result;
                        cipher = aes_cipher;
                        return Ap4Result.SUCCESS;
                    }

                default:
                    // not supported
                    return Ap4Result.ERROR_NOT_SUPPORTED;
            }
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_SampleDecrypter
    +---------------------------------------------------------------------*/
    public abstract class SampleDecrypter
    {
        /*----------------------------------------------------------------------
        |   AP4_SampleDecrypter:Create
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a sample decrypter given a protected sample description
        /// </summary>
        public static SampleDecrypter Create(ProtectedSampleDescription  sample_description,
                                              byte[]                      key,
                                              uint                        key_size,
                                              BlockCipherFactory          block_cipher_factory = null)
        {
            if (sample_description == null || key == null) return null;

            // select the block cipher factory
            if (block_cipher_factory == null) {
                block_cipher_factory = DefaultBlockCipherFactory.Instance;
            }

            switch(sample_description.GetSchemeType()) {
                case Ap4.PROTECTION_SCHEME_TYPE_OMA: {
                    OmaDcfSampleDecrypter decrypter = null;
                    Ap4Result result = OmaDcfSampleDecrypter.Create(sample_description,
                                                                    key,
                                                                    key_size,
                                                                    block_cipher_factory,
                                                                    out decrypter);
                    if (result.Failed()) return null;
                    return decrypter;
                }

                case Ap4.PROTECTION_SCHEME_TYPE_IAEC: {
                    IsmaCipher decrypter = null;
                    Ap4Result result = IsmaCipher.CreateSampleDecrypter(sample_description,
                                                                        key,
                                                                        key_size,
                                                                        block_cipher_factory,
                                                                        out decrypter);
                    if (result.Failed()) return null;
                    return decrypter;
                }

                default:
                    return null;
            }

            // unreachable - return NULL;
        }

        /*----------------------------------------------------------------------
        |   AP4_SampleDecrypter:Create
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a fragment sample decrypter given a protected sample description and a track fragment
        /// </summary>
        public static SampleDecrypter Create(ProtectedSampleDescription  sample_description,
                                              ContainerAtom               traf,
                                              ByteStream                  aux_info_data,
                                              ulong                       aux_info_data_offset,
                                              byte[]                      key,
                                              uint                        key_size,
                                              BlockCipherFactory          block_cipher_factory = null)
        {
            if (sample_description == null || traf == null || key == null) return null;

            // select the block cipher factory
            if (block_cipher_factory == null) {
                block_cipher_factory = DefaultBlockCipherFactory.Instance;
            }

            switch(sample_description.GetSchemeType()) {
                case Ap4.PROTECTION_SCHEME_TYPE_PIFF:
                case Ap4.PROTECTION_SCHEME_TYPE_CENC: {
                    CencSampleDecrypter decrypter = null;
                    Ap4Result result = CencSampleDecrypter.Create(sample_description,
                                                                  traf,
                                                                  aux_info_data,
                                                                  aux_info_data_offset,
                                                                  key,
                                                                  key_size,
                                                                  block_cipher_factory,
                                                                  out decrypter);
                    if (result.Failed()) return null;
                    return decrypter;
                }

                default:
                    return null;
            }

            // unreachable - return NULL;
        }

        // methods
        public virtual uint      GetDecryptedSampleSize(Sample sample) { return sample.GetSize(); }
        public virtual Ap4Result SetSampleIndex(uint index)            { return Ap4Result.SUCCESS; }
        public abstract Ap4Result DecryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    byte[]     iv = null);
    }

    /*----------------------------------------------------------------------
    |   AP4_StandardDecryptingProcessor
    +---------------------------------------------------------------------*/
    public class StandardDecryptingProcessor : Processor
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_StandardDecryptingProcessor:AP4_StandardDecryptingProcessor
        +---------------------------------------------------------------------*/
        public StandardDecryptingProcessor(ProtectionKeyMap    key_map              = null,
                                           BlockCipherFactory  block_cipher_factory = null)
        {
            if (key_map != null) {
                // copy the keys
                _keyMap.SetKeys(key_map);
            }

            if (block_cipher_factory == null) {
                _blockCipherFactory = DefaultBlockCipherFactory.Instance;
            } else {
                _blockCipherFactory = block_cipher_factory;
            }
        }

        // accessors
        public ProtectionKeyMap GetKeyMap() { return _keyMap; }

        // methods
        /*----------------------------------------------------------------------
         |   AP4_StandardDecryptingProcessor:Initialize
         +---------------------------------------------------------------------*/
        public override Ap4Result Initialize(AtomParent        top_level,
                                             ByteStream        stream,
                                             ProgressListener  listener = null)
        {
            FtypAtom ftyp = top_level.GetChild(AtomType.FTYP) as FtypAtom;
            if (ftyp != null) {
                // remove the atom, it will be replaced with a new one
                top_level.RemoveChild(ftyp);

                // keep the existing brand and compatible brands except for the ones we want to remove
                List<uint> compatible_brands = new List<uint>();
                compatible_brands.EnsureCapacity(ftyp.GetCompatibleBrands().Count);
                for (int i=0; i<ftyp.GetCompatibleBrands().Count; i++) {
                    if (ftyp.GetCompatibleBrands()[i] != Ap4.OMA_DCF_BRAND_OPF2) {
                        compatible_brands.Add(ftyp.GetCompatibleBrands()[i]);
                    }
                }

                // create a replacement for the major brand
                top_level.AddChild(new FtypAtom(ftyp.GetMajorBrand(),
                                                ftyp.GetMinorVersion(),
                                                compatible_brands.ToArray(),
                                                (uint)compatible_brands.Count), 0);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StandardDecryptingProcessor:CreateTrackHandler
        +---------------------------------------------------------------------*/
        public override Processor.TrackHandler CreateTrackHandler(TrakAtom trak)
        {
            // find the stsd atom
            StsdAtom stsd = trak.FindChild("mdia/minf/stbl/stsd") as StsdAtom;

            // avoid tracks with no stsd atom (should not happen)
            if (stsd == null) return null;

            // we only look at the first sample description
            SampleDescription  desc = stsd.GetSampleDescription(0);
            SampleEntry entry = stsd.GetSampleEntry(0);
            if (desc == null || entry == null) return null;
            if (desc.GetType() == SampleDescription.Type.TYPE_PROTECTED) {
                // create a handler for this track
                ProtectedSampleDescription protected_desc =
                    (ProtectedSampleDescription)desc;
                if (protected_desc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_OMA) {
                    DataBuffer key = _keyMap.GetKey(trak.GetId());
                    if (key != null) {
                        OmaDcfTrackDecrypter handler = null;
                        Ap4Result result = OmaDcfTrackDecrypter.Create(key.GetData(),
                                                                       key.GetDataSize(),
                                                                       protected_desc,
                                                                       entry,
                                                                       _blockCipherFactory,
                                                                       out handler);
                        if (result.Failed()) return null;
                        return handler;
                    }
                } else if (protected_desc.GetSchemeType() == Ap4.PROTECTION_SCHEME_TYPE_IAEC) {
                    DataBuffer key = _keyMap.GetKey(trak.GetId());
                    if (key != null) {
                        IsmaTrackDecrypter handler = null;
                        Ap4Result result = IsmaTrackDecrypter.Create(key.GetData(),
                                                                     key.GetDataSize(),
                                                                     protected_desc,
                                                                     entry,
                                                                     _blockCipherFactory,
                                                                     out handler);
                        if (result.Failed()) return null;
                        return handler;
                    }
                }
            }

            return null;
        }

        // members
        private BlockCipherFactory _blockCipherFactory;
        private ProtectionKeyMap   _keyMap = new ProtectionKeyMap();
    }

    /*----------------------------------------------------------------------
    |   AP4_DecryptingStream
    +---------------------------------------------------------------------*/
    public class DecryptingStream : ByteStream
    {
        /*----------------------------------------------------------------------
        |   AP4_DecryptingStream::AP4_DecryptingStream
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(BlockCipher.CipherMode mode,
                                       ByteStream             encrypted_stream,
                                       ulong                  cleartext_size,
                                       byte[]                 iv,
                                       uint                   iv_size,
                                       byte[]                 key,
                                       uint                   key_size,
                                       BlockCipherFactory     block_cipher_factory,
                                       out ByteStream         stream)
        {
            // default return value
            stream = null;

            // default cipher settings
            if (block_cipher_factory == null) {
                block_cipher_factory = DefaultBlockCipherFactory.Instance;
            }

            // get the encrypted size (includes padding)
            ulong encrypted_size = 0;
            Ap4Result result = encrypted_stream.GetSize(out encrypted_size);
            if (result.Failed()) return result;

            // check IV
            if (iv == null || iv_size != 16) return Ap4Result.ERROR_INVALID_PARAMETERS;

            // check that the encrypted size is consistent with the cipher mode
            BlockCipher.CtrParams ctr_params = new BlockCipher.CtrParams();
            object                mode_params = null;
            if (mode == BlockCipher.CipherMode.CBC) {
                // we need at least 16 bytes of data+padding
                // we also need a multiple of the block size
                if (encrypted_size < 16 || ((encrypted_size % 16) != 0)) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
            } else if (mode == BlockCipher.CipherMode.CTR) {
                ctr_params.counter_size = 16;
                mode_params = ctr_params;
            } else {
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            // create the stream cipher
            BlockCipher block_cipher;
            result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                       BlockCipher.CipherDirection.DECRYPT,
                                                       mode,
                                                       mode_params,
                                                       key,
                                                       key_size,
                                                       out block_cipher);
            if (result.Failed()) return result;

            // create the cipher according to the mode
            StreamCipher  stream_cipher = null;
            switch (mode) {
                case BlockCipher.CipherMode.CBC:
                    stream_cipher = new CbcStreamCipher(block_cipher);
                    break;
                case BlockCipher.CipherMode.CTR:
                    stream_cipher = new CtrStreamCipher(block_cipher, Ap4.CIPHER_BLOCK_SIZE);
                    break;
                default:
                    // should never occur
                    return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            // set the IV
            stream_cipher.SetIV(iv);

            // create the stream
            stream = new DecryptingStream(cleartext_size,
                                          encrypted_stream,
                                          encrypted_size,
                                          stream_cipher);

            return Ap4Result.SUCCESS;
        }

        // AP4_ByteStream methods
        /*----------------------------------------------------------------------
        |   AP4_DecryptingStream::ReadPartial
        +---------------------------------------------------------------------*/
        public override Ap4Result ReadPartial(byte[]   buffer,
                                              int      offset,
                                              uint     bytes_to_read,
                                              out uint bytes_read)
        {
            bytes_read = 0;

            // never read more than what's available
            ulong available = _cleartextSize-_cleartextPosition;
            if (available < bytes_to_read) {
                if (available == 0) {
                    return Ap4Result.ERROR_EOS;
                }
                bytes_to_read = (uint)available;
            }

            if (_bufferFullness != 0) {
                // we have some leftovers
                uint chunk = bytes_to_read;
                if (chunk > _bufferFullness) chunk = _bufferFullness;
                Array.Copy(_buffer, (int)_bufferOffset, buffer, offset, (int)chunk);
                offset += (int)chunk;
                _cleartextPosition += chunk;
                available          -= chunk;
                bytes_to_read      -= chunk;
                _bufferFullness    -= chunk;
                _bufferOffset      += chunk;
                bytes_read         += chunk;
            }
            if (bytes_to_read == 0) return Ap4Result.SUCCESS;

            // seek to the right place in the input
            _encryptedStream.Seek(_encryptedPosition);

            while (bytes_to_read != 0) {
                // read from the source
                byte[] encrypted = new byte[1024];
                uint encrypted_read = 0;
                Ap4Result result = _encryptedStream.ReadPartial(encrypted, (uint)encrypted.Length, out encrypted_read);
                if (result == Ap4Result.ERROR_EOS) {
                    if (bytes_read == 0) {
                        return Ap4Result.ERROR_EOS;
                    } else {
                        return Ap4Result.SUCCESS;
                    }
                } else if (result != Ap4Result.SUCCESS) {
                    return result;
                } else {
                    _encryptedPosition += encrypted_read;
                }
                bool is_last_buffer = (_encryptedPosition >= _encryptedSize);
                uint buffer_size = (uint)_buffer.Length;
                result = _streamCipher.ProcessBuffer(encrypted,
                                                     encrypted_read,
                                                     _buffer,
                                                     ref buffer_size,
                                                     is_last_buffer);
                if (result.Failed()) return result;
                _bufferOffset   = 0;
                _bufferFullness = buffer_size;

                uint chunk = bytes_to_read;
                if (chunk > _bufferFullness) chunk = _bufferFullness;
                if (chunk != 0) Array.Copy(_buffer, (int)_bufferOffset, buffer, offset, (int)chunk);
                offset += (int)chunk;
                _cleartextPosition += chunk;
                available          -= chunk;
                bytes_to_read      -= chunk;
                _bufferFullness    -= chunk;
                _bufferOffset      += chunk;
                bytes_read         += chunk;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DecryptingStream::WritePartial
        +---------------------------------------------------------------------*/
        public override Ap4Result WritePartial(byte[]   buffer,
                                               int      offset,
                                               uint     bytes_to_write,
                                               out uint bytes_written)
        {
            // PORT NOTE: upstream leaves bytes_written untouched
            bytes_written = 0;
            return Ap4Result.ERROR_NOT_SUPPORTED;
        }

        /*----------------------------------------------------------------------
        |   AP4_DecryptingStream::Seek
        +---------------------------------------------------------------------*/
        public override Ap4Result Seek(ulong position)
        {
            uint preroll = 0;

            // check for no-op seek requests
            if (position == _cleartextPosition) {
                return Ap4Result.SUCCESS;
            }

            // check bounds
            if (position > _cleartextSize) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            // try to put the stream cipher at the right offset
            Ap4Result result = _streamCipher.SetStreamOffset(position, out preroll);
            if (result.Failed()) return result;

            // seek in the source stream
            result = _encryptedStream.Seek(position-preroll);
            if (result.Failed()) return result;

            // if we need to, process the preroll bytes
            if (preroll > 0) {
                uint out_size = 0;
                byte[] buffer = new byte[2*Ap4.CIPHER_BLOCK_SIZE]; // bigger than preroll
                result = _encryptedStream.Read(buffer, preroll);
                if (result.Failed()) return result;
                result = _streamCipher.ProcessBuffer(buffer, preroll, buffer, ref out_size);
                if (result.Failed()) return result;
                Ap4.ASSERT(out_size == 0); // we're just feeding prerolled bytes,
                                           // there can be no output
            }

            // update the counters
            _cleartextPosition = position;
            _encryptedPosition = position;
            _bufferFullness    = 0;
            _bufferOffset      = 0;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DecryptingStream::Tell
        +---------------------------------------------------------------------*/
        public override Ap4Result Tell(out ulong position)
        {
            position = _cleartextPosition;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DecryptingStream::GetSize
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSize(out ulong size)
        {
            size = _cleartextSize;
            return Ap4Result.SUCCESS;
        }

        // private constructor, use the factory instead
        private DecryptingStream(ulong        cleartext_size,
                                 ByteStream   encrypted_stream,
                                 ulong        encrypted_size,
                                 StreamCipher stream_cipher)
        {
            _cleartextSize = cleartext_size;
            _cleartextPosition = 0;
            _encryptedStream = encrypted_stream;
            _encryptedSize = encrypted_size;
            _encryptedPosition = 0;
            _streamCipher = stream_cipher;
            _bufferFullness = 0;
            _bufferOffset = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        // members
        private ulong        _cleartextSize;
        private ulong        _cleartextPosition;
        private ByteStream   _encryptedStream;
        private ulong        _encryptedSize;
        private ulong        _encryptedPosition;
        private StreamCipher _streamCipher;
        private byte[]       _buffer = new byte[1024];
        private uint         _bufferFullness;
        private uint         _bufferOffset;
    }

    /*----------------------------------------------------------------------
    |   AP4_EncryptingStream
    +---------------------------------------------------------------------*/
    public class EncryptingStream : ByteStream
    {
        /*----------------------------------------------------------------------
        |   AP4_EncryptingStream::AP4_EncryptingStream
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(BlockCipher.CipherMode mode,
                                       ByteStream             cleartext_stream,
                                       byte[]                 iv,
                                       uint                   iv_size,
                                       byte[]                 key,
                                       uint                   key_size,
                                       bool                   prepend_iv,
                                       BlockCipherFactory     block_cipher_factory,
                                       out ByteStream         stream)
        {
            // default return value
            stream = null;

            // get the cleartext size
            ulong cleartext_size = 0;
            Ap4Result result = cleartext_stream.GetSize(out cleartext_size);
            if (result.Failed()) return result;

            // check IV
            if (iv == null || iv_size != 16) return Ap4Result.ERROR_INVALID_PARAMETERS;

            // compute the encrypted size and set cipher params
            ulong                 encrypted_size = cleartext_size;
            object                mode_params = null;
            BlockCipher.CtrParams ctr_params = new BlockCipher.CtrParams();
            if (mode == BlockCipher.CipherMode.CBC) {
                encrypted_size += (16-(cleartext_size%16)); // with padding
            } else {
                ctr_params.counter_size = 16;
                mode_params = ctr_params;
            }

            // create the stream cipher
            // (as upstream, block_cipher_factory is not optional here)
            BlockCipher block_cipher;
            result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                       BlockCipher.CipherDirection.ENCRYPT,
                                                       mode,
                                                       mode_params,
                                                       key,
                                                       key_size,
                                                       out block_cipher);
            if (result.Failed()) return result;

            // create the cipher according to the mode
            StreamCipher  stream_cipher = null;
            switch (mode) {
                case BlockCipher.CipherMode.CBC:
                    stream_cipher = new CbcStreamCipher(block_cipher);
                    break;
                case BlockCipher.CipherMode.CTR:
                    stream_cipher = new CtrStreamCipher(block_cipher, Ap4.CIPHER_BLOCK_SIZE);
                    break;
                default:
                    // should never occur
                    // PORT NOTE: upstream only asserts and then dereferences a
                    // NULL stream cipher
                    Ap4.ASSERT(false);
                    return Ap4Result.ERROR_NOT_SUPPORTED;
            }

            // set the IV
            stream_cipher.SetIV(iv);

            // create the stream
            EncryptingStream enc_stream = new EncryptingStream(cleartext_size,
                                                               cleartext_stream,
                                                               encrypted_size,
                                                               stream_cipher);
            stream = enc_stream;

            // deal with the prepended IV if required
            if (prepend_iv) {
                enc_stream._encryptedSize += 16;
                enc_stream._bufferFullness = 16;
                Array.Copy(iv, 0, enc_stream._buffer, 0, 16);
            }

            return Ap4Result.SUCCESS;
        }

        // AP4_ByteStream methods
        /*----------------------------------------------------------------------
        |   AP4_EncryptingStream::ReadPartial
        +---------------------------------------------------------------------*/
        public override Ap4Result ReadPartial(byte[]   buffer,
                                              int      offset,
                                              uint     bytes_to_read,
                                              out uint bytes_read)
        {
            bytes_read = 0;

            // never read more than what's available
            ulong available = _encryptedSize-_encryptedPosition;
            if (available < bytes_to_read) {
                if (available == 0) return Ap4Result.ERROR_EOS;
                bytes_to_read = (uint)available;
            }

            if (_bufferFullness != 0) {
                // we have some leftovers
                uint chunk = bytes_to_read;
                if (chunk > _bufferFullness) chunk = _bufferFullness;
                Array.Copy(_buffer, (int)_bufferOffset, buffer, offset, (int)chunk);
                offset += (int)chunk;
                _encryptedPosition += chunk;
                available          -= chunk;
                bytes_to_read      -= chunk;
                _bufferFullness    -= chunk;
                _bufferOffset      += chunk;
                bytes_read         += chunk;
            }

            // seek to the right place in the input
            _cleartextStream.Seek(_cleartextPosition);

            while (bytes_to_read != 0) {
                // read from the source
                byte[] cleartext = new byte[1024];
                uint cleartext_read = 0;
                Ap4Result result = _cleartextStream.ReadPartial(cleartext, (uint)cleartext.Length, out cleartext_read);
                if (result == Ap4Result.ERROR_EOS) {
                    if (bytes_read == 0) {
                        return Ap4Result.ERROR_EOS;
                    } else {
                        return Ap4Result.SUCCESS;
                    }
                } else if (result != Ap4Result.SUCCESS) {
                    return result;
                } else {
                    _cleartextPosition += cleartext_read;
                }
                bool is_last_buffer = (_cleartextPosition >= _cleartextSize);
                uint buffer_size = (uint)_buffer.Length;
                result = _streamCipher.ProcessBuffer(cleartext,
                                                     cleartext_read,
                                                     _buffer,
                                                     ref buffer_size,
                                                     is_last_buffer);
                if (result.Failed()) return result;
                _bufferOffset   = 0;
                _bufferFullness = buffer_size;

                uint chunk = bytes_to_read;
                if (chunk > _bufferFullness) chunk = _bufferFullness;
                if (chunk != 0) {
                    Array.Copy(_buffer, (int)_bufferOffset, buffer, offset, (int)chunk);
                    offset += (int)chunk;
                    _encryptedPosition += chunk;
                    available          -= chunk;
                    bytes_to_read      -= chunk;
                    _bufferFullness    -= chunk;
                    _bufferOffset      += chunk;
                    bytes_read         += chunk;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EncryptingStream::WritePartial
        +---------------------------------------------------------------------*/
        public override Ap4Result WritePartial(byte[]   buffer,
                                               int      offset,
                                               uint     bytes_to_write,
                                               out uint bytes_written)
        {
            // PORT NOTE: upstream leaves bytes_written untouched
            bytes_written = 0;
            return Ap4Result.ERROR_NOT_SUPPORTED;
        }

        /*----------------------------------------------------------------------
        |   AP4_EncryptingStream::Seek
        +---------------------------------------------------------------------*/
        public override Ap4Result Seek(ulong position)
        {
            if (position == _encryptedPosition) {
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_NOT_SUPPORTED;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_EncryptingStream::Tell
        +---------------------------------------------------------------------*/
        public override Ap4Result Tell(out ulong position)
        {
            position = _encryptedPosition;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EncryptingStream::GetSize
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSize(out ulong size)
        {
            size = _encryptedSize;
            return Ap4Result.SUCCESS;
        }

        // private constructor, use the factory instead
        private EncryptingStream(ulong        cleartext_size,
                                 ByteStream   cleartext_stream,
                                 ulong        encrypted_size,
                                 StreamCipher stream_cipher)
        {
            _cleartextSize = cleartext_size;
            _cleartextPosition = 0;
            _cleartextStream = cleartext_stream;
            _encryptedSize = encrypted_size;
            _encryptedPosition = 0;
            _streamCipher = stream_cipher;
            _bufferFullness = 0;
            _bufferOffset = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        // members
        private ulong        _cleartextSize;
        private ulong        _cleartextPosition;
        private ByteStream   _cleartextStream;
        private ulong        _encryptedSize;
        private ulong        _encryptedPosition;
        private StreamCipher _streamCipher;
        private byte[]       _buffer = new byte[1024+16];
        private uint         _bufferFullness;
        private uint         _bufferOffset;
    }
}
