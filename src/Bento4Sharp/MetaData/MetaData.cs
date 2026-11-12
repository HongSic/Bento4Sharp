// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/MetaData/Ap4MetaData.h, Ap4MetaData.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   metadata keys
    +---------------------------------------------------------------------*/
    public static partial class AtomType
    {
        public const uint DATA = 0x64617461; // 'data' data
        public const uint MEAN = 0x6d65616e; // 'mean' namespace
        public const uint NAME = 0x6e616d65; // 'name' name
        public const uint dddd = 0x2d2d2d2d; // '----' free form
        public const uint cNAM = 0xa96e616d; // '\xA9nam' name
        public const uint cART = 0xa9415254; // '\xA9ART' artist
        public const uint cCOM = 0xa9636f6d; // '\xA9com' composer
        public const uint cWRT = 0xa9777274; // '\xA9wrt' writer
        public const uint cALB = 0xa9616c62; // '\xA9alb' album
        public const uint cGEN = 0xa967656e; // '\xA9gen' genre
        public const uint cGRP = 0xa9677270; // '\xA9grp' group
        public const uint cDAY = 0xa9646179; // '\xA9day' date
        public const uint cTOO = 0xa9746f6f; // '\xA9too' tool
        public const uint cCMT = 0xa9636d74; // '\xA9cmt' comment
        public const uint cLYR = 0xa96c7972; // '\xA9lyr' lyrics
        public const uint TRKN = 0x74726b6e; // 'trkn' track#
        public const uint DISK = 0x6469736b; // 'disk' disk#
        public const uint COVR = 0x636f7672; // 'covr' cover art
        public const uint DESC = 0x64657363; // 'desc' description
        public const uint CPIL = 0x6370696c; // 'cpil' compilation?
        public const uint TMPO = 0x746d706f; // 'tmpo' tempo
        public const uint apID = 0x61704944; // 'apID'
        public const uint cnID = 0x636e4944; // 'cnID'
        public const uint cmID = 0x636d4944; // 'cmID'
        public const uint atID = 0x61744944; // 'atID'
        public const uint plID = 0x706c4944; // 'plID'
        public const uint geID = 0x67654944; // 'geID'
        public const uint sfID = 0x73664944; // 'sfID'
        public const uint akID = 0x616b4944; // 'akID'
        public const uint aART = 0x61415254; // 'aART'
        public const uint TVNN = 0x74766e6e; // 'tvnn' TV network
        public const uint TVSH = 0x74767368; // 'tvsh' TV show
        public const uint TVEN = 0x7476656e; // 'tven' TV episode name
        public const uint TVSN = 0x7476736e; // 'tvsn' TV show season #
        public const uint TVES = 0x74766573; // 'tves' TV show episode #
        public const uint STIK = 0x7374696b; // 'stik'
        public const uint PCST = 0x70637374; // 'pcst' Podcast?
        public const uint PURD = 0x70757264; // 'purd'
        public const uint PURL = 0x7075726c; // 'purl' Podcast URL (binary)
        public const uint EGID = 0x65676964; // 'egid'
        public const uint PGAP = 0x70676170; // 'pgap' Gapless Playback
        public const uint CATG = 0x63617467; // 'catg' Category
        public const uint KEYW = 0x6b657977; // 'keyw' Keywords
        public const uint SONM = 0x736f6e6d; // 'sonm' Sort-Order: Name
        public const uint SOAL = 0x736f616c; // 'soal' Sort-Order: Album
        public const uint SOAR = 0x736f6172; // 'soar' Sort-Order: Artist
        public const uint SOAA = 0x736f6161; // 'soaa' Sort-Order: Album Artist
        public const uint SOCO = 0x736f636f; // 'soco' Sort-Order: Composer
        public const uint SOSN = 0x736f736e; // 'sosn' Sort-Order: Show

        public const uint TITL = 0x7469746c; // 'titl' 3GPP: title
        public const uint DSCP = 0x64736370; // 'dscp' 3GPP: description
        public const uint CPRT = 0x63707274; // 'cprt' 3GPP, ISO or ILST: copyright
        public const uint PERF = 0x70657266; // 'perf' 3GPP: performer
        public const uint AUTH = 0x61757468; // 'auth' 3GPP: author
        public const uint GNRE = 0x676e7265; // 'gnre' 3GPP or ILST: genre (in 3GPP -> string, in ILST -> ID3v1 index + 1)
        public const uint RTNG = 0x72746e67; // 'rtng' 3GPP or ILST: rating
        public const uint CLSF = 0x636c7366; // 'clsf' 3GPP: classification
        public const uint KYWD = 0x6b797764; // 'kywd' 3GPP: keywords
        public const uint LOCI = 0x6c6f6369; // 'loci' 3GPP: location information
        public const uint ALBM = 0x616c626d; // 'albm' 3GPP: album title and track number
        public const uint YRRC = 0x79727263; // 'yrrc' 3GPP: recording year
        public const uint TSEL = 0x7473656c; // 'tsel' 3GPP: track selection

        public const uint ICNU = 0x69636e75; // 'icnu' DCF: icon URI      (OMA DCF 2.1)
        public const uint INFU = 0x696e6675; // 'infu' DCF: info URI      (OMA DCF 2.1)
        public const uint CVRU = 0x63767275; // 'cvru' DCF: cover art URI (OMA DCF 2.1)
        public const uint LRCU = 0x6c726375; // 'lrcu' DCF: lyrics URI    (OMA DCF 2.1)
        public const uint DCFD = 0x64636644; // 'dcfD' DCF: duration      (OMarlin)
    }

    /*----------------------------------------------------------------------
    |   AP4_MetaData
    +---------------------------------------------------------------------*/
    public class MetaData
    {
        public class Key
        {
            // constructors
            public Key(string name, string ns)
            {
                _name      = CStr(name);
                _namespace = CStr(ns);
            }

            // methods
            public string GetNamespace() { return _namespace; }
            public string GetName()      { return _name;      }

            // members
            private readonly string _name;
            private readonly string _namespace;
        }

        public abstract class Value
        {
            // types
            public enum Type {
                TYPE_BINARY,
                TYPE_STRING_UTF_8,
                TYPE_STRING_UTF_16,
                TYPE_STRING_PASCAL,
                TYPE_GIF,
                TYPE_JPEG,
                TYPE_INT_08_BE,
                TYPE_INT_16_BE,
                TYPE_INT_32_BE,
                TYPE_FLOAT_32_BE,
                TYPE_FLOAT_64_BE
            }

            public enum TypeCategory {
                TYPE_CATEGORY_STRING,
                TYPE_CATEGORY_BINARY,
                TYPE_CATEGORY_INTEGER,
                TYPE_CATEGORY_FLOAT
            }

            public enum Meaning {
                MEANING_UNKNOWN,
                MEANING_ID3_GENRE,
                MEANING_BOOLEAN,
                MEANING_FILE_KIND,
                MEANING_BINARY_ENCODED_CHARS
            }

            // methods
            // PORT NOTE: GetType() hides object.GetType(), like Atom.GetType().
            public new Type     GetType()                      { return _type;              }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Value::GetTypeCategory
            +---------------------------------------------------------------------*/
            public TypeCategory GetTypeCategory()
            {
                return MapTypeToCategory(_type);
            }

            public Meaning      GetMeaning()                   { return _meaning;           }
            public string       GetLanguage()                  { return _language;          }
            public void         SetLanguage(string language)  { _language = CStr(language); }
            public abstract override string ToString();
            public abstract Ap4Result ToBytes(DataBuffer bytes);
            // PORT NOTE: C++ `long` (32 or 64 bits depending on the platform) is
            // ported as the 64-bit C# `long` everywhere in this file.
            public abstract long      ToInteger();

            // class methods
            /*----------------------------------------------------------------------
            |   AP4_MetaData::Value::MapDataTypeToCategory
            +---------------------------------------------------------------------*/
            protected static TypeCategory MapTypeToCategory(Type type)
            {
                switch (type) {
                    case Type.TYPE_INT_08_BE:
                    case Type.TYPE_INT_16_BE:
                    case Type.TYPE_INT_32_BE:
                        return TypeCategory.TYPE_CATEGORY_INTEGER;

                    case Type.TYPE_STRING_UTF_8:
                    case Type.TYPE_STRING_UTF_16:
                    case Type.TYPE_STRING_PASCAL:
                        return TypeCategory.TYPE_CATEGORY_STRING;

                    case Type.TYPE_FLOAT_32_BE:
                    case Type.TYPE_FLOAT_64_BE:
                        return TypeCategory.TYPE_CATEGORY_FLOAT;

                    default:
                        return TypeCategory.TYPE_CATEGORY_BINARY;
                }
            }

            // constructor
            protected Value(Type    type,
                            Meaning meaning  = Meaning.MEANING_UNKNOWN,
                            string  language = null)
            {
                _type     = type;
                _meaning  = meaning;
                _language = CStr(language);
            }

            // members
            protected Type    _type;
            protected Meaning _meaning;
            protected string  _language;
        }

        public struct KeyInfo
        {
            // PORT NOTE: constructor added to keep the table below readable.
            public KeyInfo(string name, string description, uint four_cc, Value.Type value_type)
            {
                this.name        = name;
                this.description = description;
                this.four_cc     = four_cc;
                this.value_type  = value_type;
            }

            // members
            public string     name;
            public string     description;
            public uint       four_cc;
            public Value.Type value_type;
        }

        public class Entry
        {
            // constructor
            public Entry(string name, string ns, Value value)
            {
                _key   = new Key(name, ns);
                _value = value;
            }

            // methods
            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::ToAtom
            +---------------------------------------------------------------------*/
            public Ap4Result ToAtom(out Atom atom)
            {
                atom = null;

                if (_value == null) {
                    return Ap4Result.ERROR_INVALID_PARAMETERS;
                }

                if (_key.GetNamespace() == "meta") {
                    // convert the name into an atom type
                    if (Ap4Utils.StringLength(_key.GetName()) != 4) {
                        // the name is not in the right format
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                    uint atom_type = Atom.TypeFromString(_key.GetName());

                    // create a container atom for the data
                    ContainerAtom container = new ContainerAtom(atom_type);

                    // add the data atom
                    DataAtom data = new DataAtom(_value);
                    container.AddChild(data);

                    atom = container;
                    return Ap4Result.SUCCESS;
                } else if (_key.GetNamespace() == "dcf") {
                    // convert the name into an atom type
                    if (Ap4Utils.StringLength(_key.GetName()) != 4) {
                        // the name is not in the right format
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                    uint atom_type = Atom.TypeFromString(_key.GetName());

                    if (MetaDataAtomTypeHandler.IsTypeInList(atom_type,
                        MetaDataAtomTypeHandler.DcfStringTypeList)) {
                        string atom_value = _value.ToString();
                        atom = new DcfStringAtom(atom_type, atom_value);
                        return Ap4Result.SUCCESS;
                    } else if (MetaDataAtomTypeHandler.IsTypeInList(atom_type,
                               MetaDataAtomTypeHandler._3gppLocalizedStringTypeList)) {
                        string atom_value = _value.ToString();
                        string language = "eng"; // default
                        if (Ap4Utils.StringLength(_value.GetLanguage()) != 0) {
                            language = _value.GetLanguage();
                        }
                        atom = new _3GppLocalizedStringAtom(atom_type, language, atom_value);
                        return Ap4Result.SUCCESS;
                    } else if (atom_type == AtomType.DCFD) {
                        atom = new DcfdAtom((uint)_value.ToInteger());
                        return Ap4Result.SUCCESS;
                    }

                     // not supported
                     return Ap4Result.ERROR_NOT_SUPPORTED;
                } else if (_key.GetNamespace() == "3gpp") {
                    // convert the name into an atom type
                    if (Ap4Utils.StringLength(_key.GetName()) != 4) {
                        // the name is not in the right format
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                    uint atom_type = Atom.TypeFromString(_key.GetName());

                    if (MetaDataAtomTypeHandler.IsTypeInList(atom_type,
                        MetaDataAtomTypeHandler._3gppLocalizedStringTypeList)) {
                        string atom_value = _value.ToString();
                        string language = "eng"; // default
                        if (Ap4Utils.StringLength(_value.GetLanguage()) != 0) {
                            language = _value.GetLanguage();
                        }
                        atom = new _3GppLocalizedStringAtom(atom_type, language, atom_value);
                        return Ap4Result.SUCCESS;
                    }

                     // not supported
                     return Ap4Result.ERROR_NOT_SUPPORTED;
                } else {
                    // create a '----' atom
                    ContainerAtom container = new ContainerAtom(AtomType.dddd);

                    // add a 'mean' string
                    container.AddChild(new MetaDataStringAtom(AtomType.MEAN, _key.GetNamespace()));

                    // add a 'name' string
                    container.AddChild(new MetaDataStringAtom(AtomType.NAME, _key.GetName()));

                    // add the data atom
                    DataAtom data = new DataAtom(_value);
                    container.AddChild(data);

                    atom = container;
                    return Ap4Result.SUCCESS;
                }

                // unreachable - return AP4_ERROR_NOT_SUPPORTED;
            }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::FindInIlst
            +---------------------------------------------------------------------*/
            public ContainerAtom FindInIlst(ContainerAtom ilst)
            {
                if (_key.GetNamespace() == "meta") {
                    uint atom_type = Atom.TypeFromString(_key.GetName());
                    return ilst.GetChild(atom_type) as ContainerAtom;
                } else {
                    foreach (Atom ilst_item in ilst.GetChildren()) {
                        ContainerAtom entry_atom = ilst_item as ContainerAtom;
                        if (entry_atom != null) {
                            // PORT NOTE: upstream uses unchecked static_casts here;
                            // a child of another class is treated as missing.
                            MetaDataStringAtom mean = entry_atom.GetChild(AtomType.MEAN) as MetaDataStringAtom;
                            MetaDataStringAtom  name = entry_atom.GetChild(AtomType.NAME) as MetaDataStringAtom;
                            if (mean != null && name != null &&
                                mean.GetValue() == _key.GetNamespace() &&
                                name.GetValue() == _key.GetName()) {
                                return entry_atom;
                            }
                        }
                    }
                }

                // not found
                return null;
            }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::AddToFileIlst
            +---------------------------------------------------------------------*/
            public Ap4Result AddToFileIlst(File file, uint index = 0)
            {
                // check that we have a correct entry
                if (_value == null) return Ap4Result.ERROR_INVALID_STATE;

                // convert the entry into an atom
                Atom atom;
                Ap4Result result = ToAtom(out atom);
                if (result.Failed()) return result;

                // look for the 'moov'
                Movie movie = file.GetMovie();
                if (movie == null) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                MoovAtom moov = movie.GetMoovAtom();
                if (moov == null) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }

                // look for 'udta', and create if it does not exist
                ContainerAtom udta = moov.FindChild("udta", true) as ContainerAtom;
                if (udta == null) return Ap4Result.ERROR_INTERNAL;

                // look for 'meta', and create if it does not exist ('meta' is a FULL atom)
                ContainerAtom meta = udta.FindChild("meta", true, true) as ContainerAtom;
                if (meta == null) return Ap4Result.ERROR_INTERNAL;

                // look for a 'hdlr' atom type 'mdir'
                HdlrAtom hdlr = meta.FindChild("hdlr") as HdlrAtom;
                if (hdlr == null) {
                    hdlr = new HdlrAtom(Ap4.HANDLER_TYPE_MDIR, "");
                    meta.AddChild(hdlr);
                } else {
                    if (hdlr.GetHandlerType() != Ap4.HANDLER_TYPE_MDIR) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                }

                // get/create the list of entries
                ContainerAtom ilst = meta.FindChild("ilst", true) as ContainerAtom;
                if (ilst == null) return Ap4Result.ERROR_INTERNAL;

                // look if there is already a container for this entry
                ContainerAtom existing = FindInIlst(ilst);
                if (existing == null) {
                    // look for a simple entry with the same type
                    Atom previous = ilst.GetChild(atom.GetType());
                    if (previous != null) {
                        ilst.RemoveChild(previous);
                    }

                    // just add the one we have
                    ilst.AddChild(atom);
                } else {
                    // add the entry's data to the existing entry
                    ContainerAtom entry_atom = atom as ContainerAtom;
                    if (entry_atom == null) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }

                    DataAtom data_atom = entry_atom.GetChild(AtomType.DATA) as DataAtom;
                    if (data_atom == null) return Ap4Result.ERROR_INTERNAL;
                    entry_atom.RemoveChild(data_atom);
                    existing.AddChild(data_atom, (int)index);
                }

                return Ap4Result.SUCCESS;
            }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::AddToFileDcf
            +---------------------------------------------------------------------*/
            public Ap4Result AddToFileDcf(File file, uint index = 0)
            {
                // check that we have a correct entry
                if (_value == null) return Ap4Result.ERROR_INVALID_STATE;

                // look for 'odrm/odhe'
                ContainerAtom odhe = file.FindChild("odrm/odhe") as ContainerAtom;
                if (odhe == null) return Ap4Result.ERROR_NO_SUCH_ITEM;

                // get/create the list of entries
                ContainerAtom udta = odhe.FindChild("udta", true) as ContainerAtom;
                if (udta == null) return Ap4Result.ERROR_INTERNAL;

                // convert the entry into an atom
                Atom data_atom;
                Ap4Result result = ToAtom(out data_atom);
                if (result.Failed()) return result;

                // add the entry's data to the container
                return udta.AddChild(data_atom, (int)index);
            }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::AddToFileUdta
            +---------------------------------------------------------------------*/
            public Ap4Result AddToFileUdta(File file, uint index = 0)
            {
                // check that we have a correct entry
                if (_value == null) return Ap4Result.ERROR_INVALID_STATE;

                // convert the entry into an atom
                // (the atom is converted twice, and the first one is unused, as upstream)
                Atom atom;
                Ap4Result result = ToAtom(out atom);
                if (result.Failed()) return result;

                // look for the 'moov'
                Movie movie = file.GetMovie();
                if (movie == null) return Ap4Result.ERROR_INVALID_FORMAT;
                MoovAtom moov = movie.GetMoovAtom();
                if (moov == null) return Ap4Result.ERROR_INVALID_FORMAT;

                // look for 'udta', and create if it does not exist
                ContainerAtom udta = moov.FindChild("udta", true) as ContainerAtom;
                if (udta == null) return Ap4Result.ERROR_INTERNAL;

                // convert the entry into an atom
                Atom data_atom;
                result = ToAtom(out data_atom);
                if (result.Failed()) return result;

                // add the entry's data to the container
                return udta.AddChild(data_atom, (int)index);
            }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::AddToFile
            +---------------------------------------------------------------------*/
            public Ap4Result AddToFile(File file, uint index = 0)
            {
                // check that we have a correct entry
                if (_value == null) return Ap4Result.ERROR_INVALID_STATE;

                // check the namespace of the key to know where to add the atom
                if (_key.GetNamespace() == "meta") {
                    return AddToFileIlst(file, index);
                } else if (_key.GetNamespace() == "dcf") {
                    return AddToFileDcf(file, index);
                } else if (_key.GetNamespace() == "3gpp") {
                    return AddToFileUdta(file, index);
                } else {
                    // custom namespace
                    return AddToFileIlst(file, index);
                }
            }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::RemoveFromFileIlst
            +---------------------------------------------------------------------*/
            public Ap4Result RemoveFromFileIlst(File file, uint index)
            {
                // look for the 'moov'
                Movie movie = file.GetMovie();
                if (movie == null) return Ap4Result.ERROR_INVALID_FORMAT;
                MoovAtom moov = movie.GetMoovAtom();
                if (moov == null) return Ap4Result.ERROR_INVALID_FORMAT;

                // look for 'udta/meta/ilst'
                ContainerAtom ilst = moov.FindChild("udta/meta/ilst") as ContainerAtom;
                if (ilst == null) return Ap4Result.ERROR_NO_SUCH_ITEM;

                // look if there is already a container for this entry
                ContainerAtom existing = FindInIlst(ilst);
                if (existing == null) return Ap4Result.ERROR_NO_SUCH_ITEM;

                // remove the data atom in the entry
                Ap4Result result = existing.DeleteChild(AtomType.DATA, index);
                if (result.Failed()) return result;

                // cleanup
                if (existing.GetType() == AtomType.dddd) {
                    // custom entry: if there are no more 'data' children, remove the entry
                    if (existing.GetChild(AtomType.DATA) == null) {
                        ilst.RemoveChild(existing);
                    }
                } else {
                    // normal entry: if the entry is empty, remove it
                    if (existing.GetChildren().Count == 0) {
                        ilst.RemoveChild(existing);
                    }
                }

                return Ap4Result.SUCCESS;
            }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::RemoveFromFileDcf
            +---------------------------------------------------------------------*/
            public Ap4Result RemoveFromFileDcf(File file, uint index)
            {
                // look for 'odrm/odhe/udta'
                ContainerAtom udta = file.FindChild("odrm/odhe/udta") as ContainerAtom;
                if (udta == null) return Ap4Result.ERROR_NO_SUCH_ITEM;

                // remove the data atom in the entry
                uint type = Ap4Utils.BytesToUInt32BE(NameBytes());
                Ap4Result result = udta.DeleteChild(type, index);
                if (result.Failed()) return result;

                return Ap4Result.SUCCESS;
            }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::RemoveFromFileUdta
            +---------------------------------------------------------------------*/
            public Ap4Result RemoveFromFileUdta(File file, uint index)
            {
                // look for the 'moov'
                Movie movie = file.GetMovie();
                if (movie == null) return Ap4Result.ERROR_INVALID_FORMAT;
                MoovAtom moov = movie.GetMoovAtom();
                if (moov == null) return Ap4Result.ERROR_INVALID_FORMAT;

                // look for 'udta'
                ContainerAtom udta = moov.FindChild("udta") as ContainerAtom;
                if (udta == null) return Ap4Result.ERROR_NO_SUCH_ITEM;

                // remove the data atom in the entry
                uint type = Ap4Utils.BytesToUInt32BE(NameBytes());
                Ap4Result result = udta.DeleteChild(type, index);
                if (result.Failed()) return result;

                return Ap4Result.SUCCESS;
            }

            /*----------------------------------------------------------------------
            |   AP4_MetaData::Entry::RemoveFromFile
            +---------------------------------------------------------------------*/
            public Ap4Result RemoveFromFile(File file, uint index)
            {
                // check the namespace of the key to know where to add the atom
                if (_key.GetNamespace() == "meta") {
                    return RemoveFromFileIlst(file, index);
                } else if (_key.GetNamespace() == "dcf") {
                    return RemoveFromFileDcf(file, index);
                } else if (_key.GetNamespace() == "3gpp") {
                    return RemoveFromFileUdta(file, index);
                } else {
                    // custom namespace
                    return RemoveFromFileIlst(file, index);
                }
            }

            // PORT NOTE: helper for `(const unsigned char*)m_Key.GetName().GetChars()`
            // passed to AP4_BytesToUInt32BE: the first 4 bytes of the name. A name
            // shorter than 4 bytes is padded with zeros (upstream reads past the
            // terminator).
            private byte[] NameBytes()
            {
                byte[] chars = Ap4Utils.StringToCStringBytes(_key.GetName());
                byte[] bytes = new byte[4];
                Array.Copy(chars, bytes, Math.Min(chars.Length, 4));
                return bytes;
            }

            // members
            public Key    _key;
            public Value  _value;
        }

        /*----------------------------------------------------------------------
        |   metadata keys
        +---------------------------------------------------------------------*/
        private static readonly KeyInfo[] MetaData_KeyInfos = new KeyInfo[] {
            new KeyInfo("Name",                "Name",                 AtomType.cNAM, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Artist",              "Artist",               AtomType.cART, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("AlbumArtist",         "Album Artist",         AtomType.aART, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Composer",            "Composer",             AtomType.cCOM, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Writer",              "Writer",               AtomType.cWRT, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Album",               "Album",                AtomType.cALB, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("GenreCode",           "Genre",                AtomType.GNRE, Value.Type.TYPE_BINARY),
            new KeyInfo("GenreName",           "Genre",                AtomType.cGEN, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Grouping",            "Grouping",             AtomType.cGRP, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Date",                "Date",                 AtomType.cDAY, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Tool",                "Encoding Tool",        AtomType.cTOO, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Comment",             "Comment",              AtomType.cCMT, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Lyrics",              "Lyrics",               AtomType.cLYR, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Copyright",           "Copyright",            AtomType.CPRT, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Track",               "Track Number",         AtomType.TRKN, Value.Type.TYPE_BINARY),
            new KeyInfo("Disc",                "Disc Number",          AtomType.DISK, Value.Type.TYPE_BINARY),
            new KeyInfo("Cover",               "Cover Art",            AtomType.COVR, Value.Type.TYPE_BINARY),
            new KeyInfo("Description",         "Description",          AtomType.DESC, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Rating",              "Rating",               AtomType.RTNG, Value.Type.TYPE_INT_08_BE),
            new KeyInfo("Tempo",               "Tempo",                AtomType.TMPO, Value.Type.TYPE_INT_16_BE),
            new KeyInfo("Compilation",         "Compilation",          AtomType.CPIL, Value.Type.TYPE_INT_08_BE),
            new KeyInfo("IsGapless",           "Is Gapless",           AtomType.PGAP, Value.Type.TYPE_INT_08_BE),
            new KeyInfo("Title",               "Title",                AtomType.TITL, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Description",         "Description",          AtomType.DSCP, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("StoreFrontID",        "Store Front ID",       AtomType.sfID, Value.Type.TYPE_INT_32_BE),
            new KeyInfo("FileKind",            "File Kind",            AtomType.STIK, Value.Type.TYPE_INT_08_BE),
            new KeyInfo("ShowName",            "Show Name",            AtomType.TVSH, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("ShowSeason",          "Show Season Number",   AtomType.TVSN, Value.Type.TYPE_INT_32_BE),
            new KeyInfo("ShowEpisodeNumber",   "Show Episode Number",  AtomType.TVES, Value.Type.TYPE_INT_32_BE),
            new KeyInfo("ShowEpisodeName",     "Show Episode Name",    AtomType.TVEN, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("TVNetworkName",       "TV Network Name",      AtomType.TVNN, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("IsPodcast",           "Is a Podcast",         AtomType.PCST, Value.Type.TYPE_INT_08_BE),
            new KeyInfo("PodcastUrl",          "Podcast URL",          AtomType.PURL, Value.Type.TYPE_BINARY),
            new KeyInfo("PodcastGuid",         "Podcast GUID",         AtomType.EGID, Value.Type.TYPE_BINARY),
            new KeyInfo("PodcastCategory",     "Podcast Category",     AtomType.CATG, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Keywords",            "Keywords",             AtomType.KEYW, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("PurchaseDate",        "Purchase Date",        AtomType.PURD, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("IconUri",             "Icon URI",             AtomType.ICNU, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("InfoUrl",             "Info URL",             AtomType.INFU, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("CoverUri",            "Cover Art URI",        AtomType.CVRU, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("LyricsUri",           "Lyrics URI",           AtomType.LRCU, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Duration",            "Duration",             AtomType.DCFD, Value.Type.TYPE_INT_32_BE),
            new KeyInfo("Performer",           "Performer",            AtomType.PERF, Value.Type.TYPE_STRING_UTF_8),
            new KeyInfo("Author",              "Author",               AtomType.AUTH, Value.Type.TYPE_STRING_UTF_8),
        };

        // class members
        // (declared after MetaData_KeyInfos: static initializers run in textual order)
        public static List<KeyInfo> KeyInfos = new List<KeyInfo>(MetaData_KeyInfos);

        // PORT NOTE: Initialized/Initialize/UnInitialize are private upstream and
        // called by the friend class AP4 (AP4::Initialize); they are `internal`
        // here so that a port of that class can reach them.
        internal static Ap4Result Initialized() { return (Ap4Result)(KeyInfos.Count != 0 ? 1 : 0); }
        internal static Ap4Result Initialize() // don't call this directly. See AP4::Initialize for details
        {
            int item_count = MetaData_KeyInfos.Length;
            KeyInfos.Clear();
            for (int i=0; i<item_count; i++) {
                KeyInfos.Add(MetaData_KeyInfos[i]);
            }
            return Ap4Result.SUCCESS;
        }
        internal static Ap4Result UnInitialize()
        {
            KeyInfos.Clear();
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   genre IDs
        +---------------------------------------------------------------------*/
        internal static readonly string[] Ap4Id3Genres = new string[]
        {
            "Blues",
            "Classic Rock",
            "Country",
            "Dance",
            "Disco",
            "Funk",
            "Grunge",
            "Hip-Hop",
            "Jazz",
            "Metal",
            "New Age",
            "Oldies",
            "Other",
            "Pop",
            "R&B",
            "Rap",
            "Reggae",
            "Rock",
            "Techno",
            "Industrial",
            "Alternative",
            "Ska",
            "Death Metal",
            "Pranks",
            "Soundtrack",
            "Euro-Techno",
            "Ambient",
            "Trip-Hop",
            "Vocal",
            "Jazz+Funk",
            "Fusion",
            "Trance",
            "Classical",
            "Instrumental",
            "Acid",
            "House",
            "Game",
            "Sound Clip",
            "Gospel",
            "Noise",
            "AlternRock",
            "Bass",
            "Soul",
            "Punk",
            "Space",
            "Meditative",
            "Instrumental Pop",
            "Instrumental Rock",
            "Ethnic",
            "Gothic",
            "Darkwave",
            "Techno-Industrial",
            "Electronic",
            "Pop-Folk",
            "Eurodance",
            "Dream",
            "Southern Rock",
            "Comedy",
            "Cult",
            "Gangsta",
            "Top 40",
            "Christian Rap",
            "Pop/Funk",
            "Jungle",
            "Native American",
            "Cabaret",
            "New Wave",
            "Psychadelic",
            "Rave",
            "Showtunes",
            "Trailer",
            "Lo-Fi",
            "Tribal",
            "Acid Punk",
            "Acid Jazz",
            "Polka",
            "Retro",
            "Musical",
            "Rock & Roll",
            "Hard Rock",
            "Folk",
            "Folk-Rock",
            "National Folk",
            "Swing",
            "Fast Fusion",
            "Bebob",
            "Latin",
            "Revival",
            "Celtic",
            "Bluegrass",
            "Avantgarde",
            "Gothic Rock",
            "Progressive Rock",
            "Psychedelic Rock",
            "Symphonic Rock",
            "Slow Rock",
            "Big Band",
            "Chorus",
            "Easy Listening",
            "Acoustic",
            "Humour",
            "Speech",
            "Chanson",
            "Opera",
            "Chamber Music",
            "Sonata",
            "Symphony",
            "Booty Bass",
            "Primus",
            "Porn Groove",
            "Satire",
            "Slow Jam",
            "Club",
            "Tango",
            "Samba",
            "Folklore",
            "Ballad",
            "Power Ballad",
            "Rhythmic Soul",
            "Freestyle",
            "Duet",
            "Punk Rock",
            "Drum Solo",
            "Acapella",
            "Euro-House",
            "Dance Hall"
        };

        internal static readonly string[] Ap4StikNames = new string[] {
            "Movie",            // 0
            "Normal",           // 1
            "Audiobook",        // 2
            "?",                // 3
            "?",                // 4
            "Whacked Bookmark", // 5
            "Music Video",      // 6
            "?",                // 7
            "?",                // 8
            "Short Film",       // 9
            "TV Show",          // 10
            "Booklet",          // 11
            "?",                // 12
            "?",                // 13
            "Ring Tone"         // 14
        };

        /* sfID Store Front country
            Australia   => 143460,
            Austria     => 143445,
            Belgium     => 143446,
            Canada      => 143455,
            Denmark     => 143458,
            Finland     => 143447,
            France      => 143442,
            Germany     => 143443,
            Greece      => 143448,
            Ireland     => 143449,
            Italy       => 143450,
            Japan       => 143462,
            Luxembourg  => 143451,
            Netherlands => 143452,
            Norway      => 143457,
            Portugal    => 143453,
            Spain       => 143454,
            Sweden      => 143456,
            Switzerland => 143459,
            UK          => 143444,
            USA         => 143441,
        */

        // PORT NOTE: not in upstream. `AP4_String(const char*)` semantics for a
        // string that upstream passes as a C string (GetChars(), char[] buffers):
        // NULL gives an empty string, and the string stops at the first NUL.
        internal static string CStr(string s)
        {
            if (s == null) return "";
            int zero = s.IndexOf('\0');
            return zero >= 0 ? s.Substring(0, zero) : s;
        }

        /*----------------------------------------------------------------------
        |   AP4_MetaData::AP4_MetaData
        +---------------------------------------------------------------------*/
        public MetaData(File file)
        {
            // get the file's movie
            Movie  movie = file.GetMovie();

            // handle the movie's metadata if there is a movie in the file
            if (movie != null) {
                MoovAtom moov = movie.GetMoovAtom();
                if (moov == null) return;
                ParseMoov(moov);
                Atom udta = moov.GetChild(AtomType.UDTA);
                if (udta != null) {
                    ContainerAtom udta_container = udta as ContainerAtom;
                    if (udta_container != null) {
                        ParseUdta(udta_container, "3gpp");
                    }
                }
            } else {
                // if we don't have a movie, try to show metadata from a udta atom
                List<Atom> top_level_atoms = file.GetTopLevelAtoms();

                foreach (Atom atom_item in top_level_atoms) {
                    ContainerAtom container = atom_item as ContainerAtom;
                    if (container != null) {
                        // look for a udta in a DCF layout
                        Atom udta = container.FindChild("odhe/udta");
                        if (udta != null) {
                            ContainerAtom udta_container = udta as ContainerAtom;
                            if (udta_container != null) {
                                ParseUdta(udta_container, "dcf");
                            }
                        }
                    }
                }
            }
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MetaData::ParseMoov
        +---------------------------------------------------------------------*/
        public Ap4Result ParseMoov(MoovAtom moov)
        {
            // look for a 'meta' atom with 'hdlr' type 'mdir'
            HdlrAtom hdlr = moov.FindChild("udta/meta/hdlr") as HdlrAtom;
            if (hdlr == null || hdlr.GetHandlerType() != Ap4.HANDLER_TYPE_MDIR) return Ap4Result.ERROR_NO_SUCH_ITEM;

            // get the list of entries
            ContainerAtom ilst = moov.FindChild("udta/meta/ilst") as ContainerAtom;
            if (ilst == null) return Ap4Result.ERROR_NO_SUCH_ITEM;

            foreach (Atom ilst_item in ilst.GetChildren()) {
                ContainerAtom entry_atom = ilst_item as ContainerAtom;
                if (entry_atom != null) {
                    AddIlstEntries(entry_atom, "meta");
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MetaData::ParseUdta
        +---------------------------------------------------------------------*/
        public Ap4Result ParseUdta(ContainerAtom udta, string namespc)
        {
            // check that the atom is indeed a 'udta' atom
            if (udta.GetType() != AtomType.UDTA) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            foreach (Atom udta_item in udta.GetChildren()) {
                _3GppLocalizedStringAtom _3gpp_atom = udta_item as _3GppLocalizedStringAtom;
                if (_3gpp_atom != null) {
                    Add3GppEntry(_3gpp_atom, namespc);
                    continue;
                }

                DcfStringAtom dcfs_atom = udta_item as DcfStringAtom;
                if (dcfs_atom != null) {
                    AddDcfStringEntry(dcfs_atom, namespc);
                    continue;
                }

                DcfdAtom dcfd_atom = udta_item as DcfdAtom;
                if (dcfd_atom != null) {
                    AddDcfdEntry(dcfd_atom, namespc);
                }
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public List<Entry> GetEntries() { return _entries; }

        /*----------------------------------------------------------------------
        |   AP4_MetaData::ResolveKeyName
        +---------------------------------------------------------------------*/
        public Ap4Result ResolveKeyName(uint atom_type, out string value)
        {
            string  key_name = null;
            string  four_cc;

            // look for a match in the key infos
            for (int i=0;
                 i<MetaData_KeyInfos.Length;
                 i++) {
                if (MetaData_KeyInfos[i].four_cc == atom_type) {
                    key_name = MetaData_KeyInfos[i].name;
                    break;
                }
            }
            if (key_name == null) {
                // this key was not found in the key infos, create a name for it
                four_cc = Ap4Utils.FormatFourChars(atom_type);
                key_name = four_cc;
            }
            value = key_name;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MetaData::AddIlstEntries
        +---------------------------------------------------------------------*/
        public Ap4Result AddIlstEntries(ContainerAtom atom, string namespc)
        {
            MetaData.Value value = null;

            if (atom.GetType() == AtomType.dddd) {
                // PORT NOTE: upstream uses unchecked static_casts for the three
                // children below (undefined behaviour if a child is of another
                // class, e.g. in a cloned or hand-built tree); here a child of
                // another class is treated as missing.

                // look for the namespace
                MetaDataStringAtom mean = atom.GetChild(AtomType.MEAN) as MetaDataStringAtom;
                if (mean == null) return Ap4Result.ERROR_INVALID_FORMAT;

                // look for the name
                MetaDataStringAtom name = atom.GetChild(AtomType.NAME) as MetaDataStringAtom;
                if (name == null) return Ap4Result.ERROR_INVALID_FORMAT;

                // get the value
                DataAtom data_atom = atom.GetChild(AtomType.DATA) as DataAtom;
                if (data_atom == null) return Ap4Result.ERROR_INVALID_FORMAT;
                value = new AtomMetaDataValue(data_atom, atom.GetType());

                _entries.Add(new Entry(name.GetValue(),
                                       mean.GetValue(),
                                       value));
                return Ap4Result.SUCCESS;
            } else {
                string  key_name = null;
                string  four_cc;

                // convert the atom type to a name
                four_cc = Ap4Utils.FormatFourChars(atom.GetType());
                key_name = four_cc;

                // add one entry for each data atom
                foreach (Atom item_atom in atom.GetChildren()) {
                    if (item_atom.GetType() == AtomType.DATA) {
                        // PORT NOTE: unchecked static_cast upstream; a 'data' child
                        // that is not a DataAtom is skipped.
                        DataAtom data_atom = item_atom as DataAtom;
                        if (data_atom == null) continue;
                        value = new AtomMetaDataValue(data_atom, atom.GetType());
                        _entries.Add(new Entry(key_name, namespc, value));
                    }
                }

                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MetaData::Add3GppEntry
        +---------------------------------------------------------------------*/
        public Ap4Result Add3GppEntry(_3GppLocalizedStringAtom atom, string namespc)
        {
            string key_name;
            ResolveKeyName(atom.GetType(), out key_name);

            string language = null;
            if (atom.GetLanguage().Length != 0) {
                language = atom.GetLanguage();
            }
            MetaData.Value value = new StringMetaDataValue(atom.GetValue(),
                                                           language);
            _entries.Add(new Entry(key_name, namespc, value));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MetaData::AddDcfStringEntry
        +---------------------------------------------------------------------*/
        public Ap4Result AddDcfStringEntry(DcfStringAtom atom, string namespc)
        {
            string key_name;
            ResolveKeyName(atom.GetType(), out key_name);

            MetaData.Value value = new StringMetaDataValue(atom.GetValue());
            _entries.Add(new Entry(key_name, namespc, value));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MetaData::AddDcfdEntry
        +---------------------------------------------------------------------*/
        public Ap4Result AddDcfdEntry(DcfdAtom atom, string namespc)
        {
            string key_name;
            ResolveKeyName(atom.GetType(), out key_name);

            MetaData.Value value = new IntegerMetaDataValue(MetaData.Value.Type.TYPE_INT_32_BE,
                                                            atom.GetDuration());
            _entries.Add(new Entry(key_name, namespc, value));

            return Ap4Result.SUCCESS;
        }

        // members
        private List<Entry> _entries = new List<Entry>();
    }

    /*----------------------------------------------------------------------
    |   AP4_MetaDataAtomTypeHandler
    +---------------------------------------------------------------------*/
    public class MetaDataAtomTypeHandler : AtomFactory.TypeHandler
    {
        // constructor
        public MetaDataAtomTypeHandler(AtomFactory atom_factory)
        {
            _atomFactory = atom_factory;
        }

        /*----------------------------------------------------------------------
        |   AP4_MetaDataAtomTypeHandler::CreateAtom
        +---------------------------------------------------------------------*/
        public override Ap4Result CreateAtom(uint       type,
                                             uint       size,
                                             ByteStream stream,
                                             uint       context,
                                             out Atom   atom)
        {
            atom = null;

            if (context == AtomType.ILST) {
                if (IsTypeInList(type, IlstTypeList)) {
                    _atomFactory.PushContext(type);
                    atom = ContainerAtom.Create(type, size, false, false, stream, _atomFactory);
                    _atomFactory.PopContext();
                }
            } else if (type == AtomType.DATA) {
                if (IsTypeInList(context, IlstTypeList)) {
                    atom = new DataAtom(size, stream);
                }
            } else if (context == AtomType.dddd) {
                if (type == AtomType.MEAN || type == AtomType.NAME) {
                    atom = new MetaDataStringAtom(type, size, stream);
                }
            } else if (context == AtomType.UDTA) {
                if (IsTypeInList(type, _3gppLocalizedStringTypeList)) {
                    atom = _3GppLocalizedStringAtom.Create(type, size, stream);
                } else if (IsTypeInList(type, DcfStringTypeList)) {
                    atom = DcfStringAtom.Create(type, size, stream);
                } else if (type == AtomType.DCFD) {
                    atom = DcfdAtom.Create(size, stream);
                }
            }

            return atom != null ? Ap4Result.SUCCESS : Ap4Result.FAILURE;
        }

        // types
        public struct TypeList
        {
            public TypeList(uint[] types, uint size) { _types = types; _size = size; }
            public uint[] _types;
            public uint   _size;
        }

        // class constants
        // PORT NOTE: declared in the order of the definitions in Ap4MetaData.cpp
        // (each list after its array: static initializers run in textual order).

        /*----------------------------------------------------------------------
        |   3GPP localized string atoms
        +---------------------------------------------------------------------*/
        public static readonly uint[] _3gppLocalizedStringTypes = new uint[] {
            AtomType.TITL,
            AtomType.DSCP,
            AtomType.CPRT,
            AtomType.PERF,
            AtomType.AUTH,
            AtomType.GNRE
        };
        public static readonly TypeList _3gppLocalizedStringTypeList = new TypeList(
            _3gppLocalizedStringTypes,
            (uint)_3gppLocalizedStringTypes.Length
        );

        /*----------------------------------------------------------------------
        |   other 3GPP atoms
        +---------------------------------------------------------------------*/
        public static readonly uint[] _3gppOtherTypes = new uint[] {
            AtomType.RTNG,
            AtomType.CLSF,
            AtomType.KYWD,
            AtomType.LOCI,
            AtomType.ALBM,
            AtomType.YRRC,
        };
        public static readonly TypeList _3gppOtherTypeList = new TypeList(
            _3gppOtherTypes,
            (uint)_3gppOtherTypes.Length
        );

        /*----------------------------------------------------------------------
        |   DCF string atoms
        +---------------------------------------------------------------------*/
        public static readonly uint[] DcfStringTypes = new uint[] {
            AtomType.ICNU,
            AtomType.INFU,
            AtomType.CVRU,
            AtomType.LRCU
        };
        public static readonly TypeList DcfStringTypeList = new TypeList(
            DcfStringTypes,
            (uint)DcfStringTypes.Length
        );

        /*----------------------------------------------------------------------
        |   atom type lists
        +---------------------------------------------------------------------*/
        public static readonly uint[] IlstTypes = new uint[]
        {
            AtomType.dddd,
            AtomType.cNAM,
            AtomType.cART,
            AtomType.cCOM,
            AtomType.cWRT,
            AtomType.cALB,
            AtomType.cGEN,
            AtomType.cGRP,
            AtomType.cDAY,
            AtomType.cTOO,
            AtomType.cCMT,
            AtomType.CPRT,
            AtomType.TRKN,
            AtomType.DISK,
            AtomType.COVR,
            AtomType.DESC,
            AtomType.GNRE,
            AtomType.CPIL,
            AtomType.TMPO,
            AtomType.RTNG,
            AtomType.apID,
            AtomType.cnID,
            AtomType.cmID,
            AtomType.atID,
            AtomType.plID,
            AtomType.geID,
            AtomType.sfID,
            AtomType.akID,
            AtomType.aART,
            AtomType.TVNN,
            AtomType.TVSH,
            AtomType.TVEN,
            AtomType.TVSN,
            AtomType.TVES,
            AtomType.STIK,
            AtomType.PGAP,
            AtomType.PCST,
            AtomType.PURD,
            AtomType.PURL,
            AtomType.EGID,
            AtomType.SONM,
            AtomType.SOAL,
            AtomType.SOAR,
            AtomType.SOAA,
            AtomType.SOCO,
            AtomType.SOSN
        };
        public static readonly TypeList IlstTypeList = new TypeList(
            IlstTypes,
            (uint)IlstTypes.Length
        );

        // class methods
        /*----------------------------------------------------------------------
        |   AP4_MetaDataAtomTypeHandler::IsTypeInList
        +---------------------------------------------------------------------*/
        public static bool IsTypeInList(uint type, TypeList list)
        {
            for (uint i=0; i<list._size; i++) {
                if (type == list._types[i]) return true;
            }
            return false;
        }

        // members
        private AtomFactory _atomFactory;
    }

    /*----------------------------------------------------------------------
    |   AP4_MetaDataTag
    +---------------------------------------------------------------------*/
    // PORT NOTE: AP4_MetaDataTag is declared in Ap4MetaData.h but none of its
    // members (constructor, Write, Inspect) is defined anywhere upstream and
    // nothing uses it; it is not ported.

    /*----------------------------------------------------------------------
    |   AP4_3GppLocalizedStringAtom
    +---------------------------------------------------------------------*/
    // PORT NOTE: a C# identifier cannot start with a digit, hence the leading
    // underscore (same rule as AtomType._8ID_).
    public class _3GppLocalizedStringAtom : Atom
    {
        // factory method
        /*----------------------------------------------------------------------
        |   AP4_3GppLocalizedStringAtom::Create
        +---------------------------------------------------------------------*/
        public static _3GppLocalizedStringAtom Create(uint type, uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new _3GppLocalizedStringAtom(type, size, version, flags, stream);
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_3GppLocalizedStringAtom::AP4_3GppLocalizedStringAtom
        +---------------------------------------------------------------------*/
        public _3GppLocalizedStringAtom(uint   type,
                                        string language,
                                        string value) :
            base(type, Ap4.FULL_ATOM_HEADER_SIZE+2, 0, 0)
        {
            _value = MetaData.CStr(value);

            // PORT NOTE: upstream copies language[0..3] blindly; a language
            // shorter than 3 chars is padded with zeros here (upstream reads past
            // the terminator).
            byte[] language_chars = Ap4Utils.StringToCStringBytes(language);
            _language[0] = language_chars.Length > 0 ? language_chars[0] : (byte)0;
            _language[1] = language_chars.Length > 1 ? language_chars[1] : (byte)0;
            _language[2] = language_chars.Length > 2 ? language_chars[2] : (byte)0;
            _language[3] = language_chars.Length > 3 ? language_chars[3] : (byte)0;

            _size32 += Ap4Utils.StringLength(_value)+1;
        }

        /*----------------------------------------------------------------------
        |   AP4_3GppLocalizedStringAtom::AP4_3GppLocalizedStringAtom
        +---------------------------------------------------------------------*/
        public _3GppLocalizedStringAtom(uint       type,
                                        uint       size,
                                        byte       version,
                                        uint       flags,
                                        ByteStream stream) :
            base(type, size, version, flags)
        {
            // read the language code
            ushort packed_language;
            stream.ReadUI16(out packed_language);
            _language[0] = (byte)(0x60+((packed_language>>10)&0x1F));
            _language[1] = (byte)(0x60+((packed_language>> 5)&0x1F));
            _language[2] = (byte)(0x60+((packed_language    )&0x1F));
            _language[3] = 0;

            // read the value (should be a NULL-terminated string, but we'll
            // allow for strings that are not terminated)
            if (size > Ap4.FULL_ATOM_HEADER_SIZE+2) {
                uint value_size = size-(Ap4.FULL_ATOM_HEADER_SIZE+2);
                byte[] value = new byte[value_size];
                stream.Read(value, value_size);
                _value = Ap4Utils.StringFromBytes(value, 0, value_size);
            }
        }

        // AP4_Atom methods
        /*----------------------------------------------------------------------
        |   AP4_3GppLocalizedStringAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // PORT NOTE: m_Language is a `char` array upstream; `char` is taken as
            // signed (as on x86), which only matters for bytes >= 0x80.
            ushort packed_language = (ushort)((((sbyte)_language[0]-0x60)<<10) |
                                              (((sbyte)_language[1]-0x60)<< 5) |
                                              (((sbyte)_language[2]-0x60)));
            stream.WriteUI16(packed_language);
            uint payload_size = (uint)GetSize()-GetHeaderSize();
            if (payload_size < 2) return Ap4Result.ERROR_INVALID_FORMAT;
            // m_Value.GetLength() is the full length (the value may contain NULs,
            // typically the terminator read from the file)
            byte[] value_chars = Ap4Utils.StringToBytes(_value);
            uint value_size = (uint)value_chars.Length+1;
            if (value_size > payload_size-2) {
                value_size = payload_size-2;
            }
            // stream.Write(m_Value.GetChars(), value_size): the chars, then the
            // terminator of the AP4_String when value_size reaches it
            if (value_size > (uint)value_chars.Length) {
                stream.Write(value_chars, (uint)value_chars.Length);
                stream.WriteUI08(0);
            } else {
                stream.Write(value_chars, value_size);
            }
            for (uint i=value_size; i<payload_size-2; i++) {
                stream.WriteUI08(0);
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_3GppLocalizedStringAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("language", GetLanguage());
            inspector.AddField("value", MetaData.CStr(_value));
            return Ap4Result.SUCCESS;
        }

        // methods
        public string GetLanguage() { return Ap4Utils.StringFromCString(_language, 0, 4); }
        public string GetValue()    { return _value; }

        // members
        private byte[] _language = new byte[4];
        private string _value = "";
    }

    /*----------------------------------------------------------------------
    |   AP4_DcfStringAtom
    +---------------------------------------------------------------------*/
    public class DcfStringAtom : Atom
    {
        // factory method
        /*----------------------------------------------------------------------
        |   AP4_DcfStringAtom::Create
        +---------------------------------------------------------------------*/
        public static DcfStringAtom Create(uint type, uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new DcfStringAtom(type, size, version, flags, stream);
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_DcfStringAtom::AP4_DcfStringAtom
        +---------------------------------------------------------------------*/
        public DcfStringAtom(uint type, string value) :
            base(type, Ap4.FULL_ATOM_HEADER_SIZE, 0, 0)
        {
            _value = MetaData.CStr(value);
            _size32 += Ap4Utils.StringLength(_value);
        }

        /*----------------------------------------------------------------------
        |   AP4_DcfStringAtom::AP4_DcfStringAtom
        +---------------------------------------------------------------------*/
        public DcfStringAtom(uint       type,
                             uint       size,
                             byte       version,
                             uint       flags,
                             ByteStream stream) :
            base(type, size, version, flags)
        {
            if (size > Ap4.FULL_ATOM_HEADER_SIZE) {
                uint value_size = size-(Ap4.FULL_ATOM_HEADER_SIZE);
                byte[] value = new byte[value_size];
                stream.Read(value, value_size);
                _value = Ap4Utils.StringFromBytes(value, 0, value_size);
            }
        }

        // AP4_Atom methods
        /*----------------------------------------------------------------------
        |   AP4_DcfStringAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            byte[] value_chars = Ap4Utils.StringToBytes(_value);
            if (value_chars.Length != 0) stream.Write(value_chars, (uint)value_chars.Length);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DcfStringAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("value", MetaData.CStr(_value));
            return Ap4Result.SUCCESS;
        }

        // methods
        public string GetValue() { return _value;  }

        // members
        private string _value = "";
    }

    /*----------------------------------------------------------------------
    |   AP4_DcfdAtom
    +---------------------------------------------------------------------*/
    public class DcfdAtom : Atom
    {
        // factory method
        /*----------------------------------------------------------------------
        |   AP4_DcfdAtom::Create
        +---------------------------------------------------------------------*/
        public static DcfdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            if (size != Ap4.FULL_ATOM_HEADER_SIZE+4) return null;
            return new DcfdAtom(version, flags, stream);
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_DcfdAtom::AP4_DcfdAtom
        +---------------------------------------------------------------------*/
        public DcfdAtom(byte       version,
                        uint       flags,
                        ByteStream stream) :
            base(AtomType.DCFD, Ap4.FULL_ATOM_HEADER_SIZE+4, version, flags)
        {
            _duration = 0;
            stream.ReadUI32(out _duration);
        }

        /*----------------------------------------------------------------------
        |   AP4_DcfdAtom::AP4_DcfdAtom
        +---------------------------------------------------------------------*/
        public DcfdAtom(uint duration) :
            base(AtomType.DCFD, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _duration = duration;
        }

        // AP4_Atom methods
        /*----------------------------------------------------------------------
        |   AP4_DcfdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            stream.WriteUI32(_duration);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DcfdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("duration", _duration);
            return Ap4Result.SUCCESS;
        }

        // methods
        public uint GetDuration() { return _duration;  }

        // members
        private uint _duration;
    }

    /*----------------------------------------------------------------------
    |   AP4_MetaDataStringAtom
    +---------------------------------------------------------------------*/
    public class MetaDataStringAtom : Atom
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_MetaDataStringAtom::AP4_MetaDataStringAtom
        +---------------------------------------------------------------------*/
        public MetaDataStringAtom(uint type, string value) :
            base(type, Ap4.ATOM_HEADER_SIZE)
        {
            _reserved = 0;
            _value = MetaData.CStr(value);
            _size32 += 4+Ap4Utils.StringLength(_value);
        }

        /*----------------------------------------------------------------------
        |   AP4_MetaDataStringAtom::AP4_MetaDataStringAtom
        +---------------------------------------------------------------------*/
        public MetaDataStringAtom(uint type, uint size, ByteStream stream) :
            base(type, size)
        {
            _reserved = 0;
            // PORT NOTE: upstream computes size-AP4_ATOM_HEADER_SIZE-4 without
            // checking for underflow (an atom smaller than 12 bytes makes it
            // overflow the heap); the value is left empty in that case.
            uint value_size = size >= Ap4.ATOM_HEADER_SIZE+4 ? size-Ap4.ATOM_HEADER_SIZE-4 : 0;
            // AP4_String(AP4_Size): `value_size` zero chars, then filled by Read
            byte[] value = new byte[value_size];
            stream.ReadUI32(out _reserved);
            stream.Read(value, value_size);
            _value = Ap4Utils.StringFromBytes(value, 0, value_size);
        }

        // AP4_Atom methods
        /*----------------------------------------------------------------------
        |   AP4_MetaDataStringAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            stream.WriteUI32(_reserved);
            byte[] value_chars = Ap4Utils.StringToBytes(_value);
            return stream.Write(value_chars, (uint)value_chars.Length);
        }

        /*----------------------------------------------------------------------
        |   AP4_MetaDataStringAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("value", MetaData.CStr(_value));
            return Ap4Result.SUCCESS;
        }

        // methods
        public string GetValue() { return _value; }

        // members
        private uint   _reserved;
        private string _value;
    }

    /*----------------------------------------------------------------------
    |   AP4_DataAtom
    +---------------------------------------------------------------------*/
    public class DataAtom : Atom
    {
        // PORT NOTE: `: uint` so that any value read from a file is kept as is.
        public enum DataType : uint {
            DATA_TYPE_BINARY        = 0,
            DATA_TYPE_STRING_UTF_8  = 1,
            DATA_TYPE_STRING_UTF_16 = 2,
            DATA_TYPE_STRING_PASCAL = 3,
            DATA_TYPE_GIF           = 13,
            DATA_TYPE_JPEG          = 14,
            DATA_TYPE_SIGNED_INT_BE = 21, /* the size of the integer is derived from the container size */
            DATA_TYPE_FLOAT_32_BE   = 22,
            DATA_TYPE_FLOAT_64_BE   = 23
        }

        public enum DataLang : uint {
            LANGUAGE_ENGLISH = 0
        }

        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const uint DATA_ATOM_MAX_SIZE = 0x40000000;   // AP4_DATA_ATOM_MAX_SIZE

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_DataAtom::AP4_DataAtom
        +---------------------------------------------------------------------*/
        public DataAtom(MetaData.Value value) :
            base(AtomType.DATA, Ap4.ATOM_HEADER_SIZE)
        {
            _dataType = DataType.DATA_TYPE_BINARY;
            _source   = null;

            MemoryByteStream memory = new MemoryByteStream();
            uint payload_size = 8;
            _source = memory;

            switch (value.GetType()) {
                case MetaData.Value.Type.TYPE_STRING_UTF_8: {
                    _dataType = DataType.DATA_TYPE_STRING_UTF_8;
                    string string_value = value.ToString();
                    // GetLength(): the full length, embedded NULs included
                    byte[] string_chars = Ap4Utils.StringToBytes(string_value);
                    if (string_chars.Length != 0) {
                        memory.Write(string_chars, (uint)string_chars.Length);
                    }
                    payload_size += (uint)string_chars.Length;
                    break;
                }

                // PORT NOTE: for the three integer types upstream writes the
                // in-memory representation of the integer (`memory->Write(&int_value, n)`),
                // i.e. the bytes are in the byte order of the platform, not
                // big-endian. BitConverter.GetBytes does the same.
                case MetaData.Value.Type.TYPE_INT_08_BE: {
                    _dataType = DataType.DATA_TYPE_SIGNED_INT_BE;
                    byte int_value = (byte)value.ToInteger();
                    memory.Write(new byte[] { int_value }, 1);
                    payload_size += 1;
                    break;
                }

                case MetaData.Value.Type.TYPE_INT_16_BE: {
                    _dataType = DataType.DATA_TYPE_SIGNED_INT_BE;
                    ushort int_value = (ushort)value.ToInteger();
                    memory.Write(BitConverter.GetBytes(int_value), 2);
                    payload_size += 2;
                    break;
                }

                case MetaData.Value.Type.TYPE_INT_32_BE: {
                    _dataType = DataType.DATA_TYPE_SIGNED_INT_BE;
                    uint int_value = (uint)value.ToInteger();
                    memory.Write(BitConverter.GetBytes(int_value), 4);
                    payload_size += 4;
                    break;
                }

                // PORT NOTE: upstream is three cases falling through
                // (JPEG -> GIF -> BINARY); C# has no fallthrough.
                case MetaData.Value.Type.TYPE_JPEG:
                case MetaData.Value.Type.TYPE_GIF:
                case MetaData.Value.Type.TYPE_BINARY: {
                    if (value.GetType() == MetaData.Value.Type.TYPE_JPEG) {
                        _dataType = DataType.DATA_TYPE_JPEG;
                    } else if (value.GetType() == MetaData.Value.Type.TYPE_GIF) {
                        if (_dataType == DataType.DATA_TYPE_BINARY) _dataType = DataType.DATA_TYPE_GIF;
                    }
                    DataBuffer buffer = new DataBuffer();
                    value.ToBytes(buffer);
                    if (buffer.GetDataSize() != 0) {
                        memory.Write(buffer.GetData(), buffer.GetDataSize());
                    }
                    payload_size += buffer.GetDataSize();
                    break;
                }

                default:
                    break;
            }

            string language = value.GetLanguage();
            if (language == "en") {
                _dataLang = DataLang.LANGUAGE_ENGLISH;
            } else {
                // default
                _dataLang = DataLang.LANGUAGE_ENGLISH;
            }

            _size32 += payload_size;
        }

        /*----------------------------------------------------------------------
        |   AP4_DataAtom::AP4_DataAtom
        +---------------------------------------------------------------------*/
        public DataAtom(uint size, ByteStream stream) :
            base(AtomType.DATA, size)
        {
            _source = null;

            // PORT NOTE: when returning here upstream leaves m_DataType and
            // m_DataLang uninitialized; they are 0 in the port.
            if (size < Ap4.ATOM_HEADER_SIZE+8) return;

            uint i;
            stream.ReadUI32(out i); _dataType = (DataType)i;
            stream.ReadUI32(out i); _dataLang = (DataLang)i;

            // the stream for the data is a substream of this source
            ulong data_offset;
            stream.Tell(out data_offset);
            uint data_size = size-Ap4.ATOM_HEADER_SIZE-8;
            _source = new SubStream(stream, data_offset, data_size);
        }

        // AP4_Atom methods
        /*----------------------------------------------------------------------
        |   AP4_DataAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            // PORT NOTE: the enums are passed as unsigned 32-bit values (upstream
            // relies on the implementation-defined promotion of the enum).
            inspector.AddField("type", (uint)_dataType);
            inspector.AddField("lang", (uint)_dataLang);
            if (_dataType == DataType.DATA_TYPE_STRING_UTF_8) {
                string str;
                if (LoadString(out str).Succeeded()) {
                    inspector.AddField("value", MetaData.CStr(str));
                }
            } else if (_dataType == DataType.DATA_TYPE_SIGNED_INT_BE) {
                long value;
                if (LoadInteger(out value).Succeeded()) {
                    inspector.AddField("value", value);
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DataAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            stream.WriteUI32((uint)_dataType);
            stream.WriteUI32((uint)_dataLang);
            if (_source != null) {
                ulong size = 0;
                _source.GetSize(out size);
                _source.Seek(0);
                _source.CopyTo(stream, size);
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public DataType GetDataType() { return _dataType; }
        public DataLang GetDataLang() { return _dataLang; }

        /*----------------------------------------------------------------------
        |   AP4_DataAtom::GetValueType
        +---------------------------------------------------------------------*/
        public MetaData.Value.Type GetValueType()
        {
            switch (_dataType) {
                case DataType.DATA_TYPE_BINARY:
                    return MetaData.Value.Type.TYPE_BINARY;

                case DataType.DATA_TYPE_SIGNED_INT_BE:
                    switch (_size32-16) {
                        case 1: return MetaData.Value.Type.TYPE_INT_08_BE;
                        case 2: return MetaData.Value.Type.TYPE_INT_16_BE;
                        case 4: return MetaData.Value.Type.TYPE_INT_32_BE;
                        default: return MetaData.Value.Type.TYPE_BINARY;
                    }

                case DataType.DATA_TYPE_STRING_UTF_8:
                    return MetaData.Value.Type.TYPE_STRING_UTF_8;

                case DataType.DATA_TYPE_STRING_UTF_16:
                    return MetaData.Value.Type.TYPE_STRING_UTF_16;

                case DataType.DATA_TYPE_STRING_PASCAL:
                    return MetaData.Value.Type.TYPE_STRING_PASCAL;

                case DataType.DATA_TYPE_GIF:
                    return MetaData.Value.Type.TYPE_GIF;

                case DataType.DATA_TYPE_JPEG:
                    return MetaData.Value.Type.TYPE_JPEG;

                default:
                    return MetaData.Value.Type.TYPE_BINARY;
            }

            // unreachable - return AP4_MetaData::Value::TYPE_BINARY;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_DataAtom::LoadString
        +---------------------------------------------------------------------*/
        // PORT NOTE: `string` is null when (and only when) an error is returned.
        // The string has the size of the data, embedded NULs included.
        public Ap4Result LoadString(out string  @string)
        {
            if (_source == null) {
                @string = "";
                return Ap4Result.SUCCESS;
            } else {
                // create a string with enough capactiy for the data
                ulong size = 0;
                _source.GetSize(out size);
                if (size > DATA_ATOM_MAX_SIZE) {
                    @string = null;
                    return Ap4Result.ERROR_OUT_OF_RANGE;
                }
                byte[] chars = new byte[(uint)size];

                // read from the start of the stream
                _source.Seek(0);
                Ap4Result result = _source.Read(chars, (uint)size);
                if (result.Failed()) {
                    @string = null;
                } else {
                    @string = Ap4Utils.StringFromBytes(chars, 0, (uint)size);
                }

                return result;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_DataAtom::LoadBytes
        +---------------------------------------------------------------------*/
        public Ap4Result LoadBytes(DataBuffer bytes)
        {
            if (_source == null) {
                bytes.SetDataSize(0);
                return Ap4Result.SUCCESS;
            }
            ulong size = 0;
            _source.GetSize(out size);
            if (size > DATA_ATOM_MAX_SIZE) return Ap4Result.ERROR_OUT_OF_RANGE;
            bytes.SetDataSize((uint)size);
            _source.Seek(0);
            Ap4Result result = _source.Read(bytes.UseData(), (uint)size);
            if (result.Failed()) {
                bytes.SetDataSize(0);
            }
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_DataAtom::LoadInteger
        +---------------------------------------------------------------------*/
        public Ap4Result LoadInteger(out long value)
        {
            Ap4Result result = Ap4Result.FAILURE;
            value = 0;
            if (_source == null) return Ap4Result.SUCCESS;
            ulong size = 0;
            _source.GetSize(out size);
            if (size > 4) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }
            byte[] bytes = new byte[4];
            _source.Seek(0);
            _source.Read(bytes, (uint)size);
            result = Ap4Result.SUCCESS;
            switch (size) {
                case 1: value = bytes[0]; break;
                case 2: value = Ap4Utils.BytesToInt16BE(bytes); break;
                case 4: value = Ap4Utils.BytesToInt32BE(bytes); break;
                default: value = 0; result = Ap4Result.ERROR_INVALID_FORMAT; break;
            }
            return result;
        }

        // members
        private DataType    _dataType;
        private DataLang    _dataLang;
        private ByteStream  _source;
    }

    /*----------------------------------------------------------------------
    |   AP4_StringMetaDataValue
    +---------------------------------------------------------------------*/
    public class StringMetaDataValue : MetaData.Value
    {
        // constructor
        public StringMetaDataValue(string value, string language = null) :
            base(Type.TYPE_STRING_UTF_8, Meaning.MEANING_UNKNOWN, language)
        {
            _value = MetaData.CStr(value);
        }

        // AP4_MetaData::Value methods
        /*----------------------------------------------------------------------
        |   AP4_StringMetaDataValue::ToString
        +---------------------------------------------------------------------*/
        public override string ToString()
        {
            return _value;
        }

        /*----------------------------------------------------------------------
        |   AP4_StringMetaDataValue::ToBytes
        +---------------------------------------------------------------------*/
        public override Ap4Result ToBytes(DataBuffer bytes)
        {
            return Ap4Result.ERROR_NOT_SUPPORTED;
        }

        /*----------------------------------------------------------------------
        |   AP4_StringMetaDataValue::ToInteger
        +---------------------------------------------------------------------*/
        public override long ToInteger()
        {
            return 0;
        }

        // members
        private string _value;
    }

    /*----------------------------------------------------------------------
    |   AP4_IntegerMetaDataValue
    +---------------------------------------------------------------------*/
    public class IntegerMetaDataValue : MetaData.Value
    {
        // constructor
        public IntegerMetaDataValue(Type type, long value) :
            base(type)
        {
            _value = value;
        }

        // AP4_MetaData::Value methods
        /*----------------------------------------------------------------------
        |   AP4_IntegerMetaDataValue::ToString
        +---------------------------------------------------------------------*/
        public override string ToString()
        {
            // char value[16]; AP4_FormatString(value, sizeof(value), "%ld", m_Value);
            // (the output is truncated to 15 chars, as upstream)
            string value = _value.ToString(CultureInfo.InvariantCulture);
            if (value.Length > 15) value = value.Substring(0, 15);
            return value;
        }

        /*----------------------------------------------------------------------
        |   AP4_IntegerMetaDataValue::ToBytes
        +---------------------------------------------------------------------*/
        public override Ap4Result ToBytes(DataBuffer bytes)
        {
            return Ap4Result.ERROR_NOT_SUPPORTED;
        }

        /*----------------------------------------------------------------------
        |   AP4_IntegerMetaDataValue::ToInteger
        +---------------------------------------------------------------------*/
        public override long ToInteger()
        {
            return _value;
        }

        // members
        private long _value;
    }

    /*----------------------------------------------------------------------
    |   AP4_BinaryMetaDataValue
    +---------------------------------------------------------------------*/
    public class BinaryMetaDataValue : MetaData.Value
    {
        // constructor
        public BinaryMetaDataValue(Type type, byte[] data, uint size) :
            base(type)
        {
            _value = new DataBuffer(data, size);
        }

        // AP4_MetaData::Value methods
        /*----------------------------------------------------------------------
        |   AP4_BinaryMetaDataValue::ToString
        +---------------------------------------------------------------------*/
        public override string ToString()
        {
            return ""; // not supported
        }

        /*----------------------------------------------------------------------
        |   AP4_BinaryMetaDataValue::ToBytes
        +---------------------------------------------------------------------*/
        public override Ap4Result ToBytes(DataBuffer bytes)
        {
            bytes.SetDataSize(_value.GetDataSize());
            Array.Copy(_value.GetData(), bytes.UseData(), (int)_value.GetDataSize());
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_BinaryMetaDataValue::ToInteger
        +---------------------------------------------------------------------*/
        public override long ToInteger()
        {
            return 0; // NOT SUPPORTED
        }

        // members
        private DataBuffer _value;
    }

    /*----------------------------------------------------------------------
    |   AP4_AtomMetaDataValue
    +---------------------------------------------------------------------*/
    public class AtomMetaDataValue : MetaData.Value
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_AtomMetaDataValue::AP4_AtomMetaDataValue
        +---------------------------------------------------------------------*/
        public AtomMetaDataValue(DataAtom atom,
                                 uint     parent_type) :
            base(atom.GetValueType())
        {
            _dataAtom = atom;

            switch (parent_type) {
                case AtomType.GNRE:
                    _meaning = Meaning.MEANING_ID3_GENRE;
                    break;

                case AtomType.CPIL:
                    _meaning = Meaning.MEANING_BOOLEAN;
                    break;

                case AtomType.PGAP:
                case AtomType.PCST:
                    _meaning = Meaning.MEANING_BOOLEAN;
                    break;

                case AtomType.STIK:
                    _meaning = Meaning.MEANING_FILE_KIND;
                    break;

                case AtomType.PURL:
                case AtomType.EGID:
                    _meaning = Meaning.MEANING_BINARY_ENCODED_CHARS;
                    break;

                default:
                    break;
            }
        }

        // AP4_MetaData::Value methods
        /*----------------------------------------------------------------------
        |   AP4_AtomMetaDataValue::ToString
        +---------------------------------------------------------------------*/
        public override string ToString()
        {
            string @string = "";

            MetaData.Value.Type value_type = _dataAtom.GetValueType();
            switch (MetaData.Value.MapTypeToCategory(value_type)) {
                case TypeCategory.TYPE_CATEGORY_INTEGER:
                    {
                        long value;
                        if (_dataAtom.LoadInteger(out value).Succeeded()) {
                            if (_meaning == Meaning.MEANING_BOOLEAN) {
                                if (value != 0) {
                                    return "True";
                                } else {
                                    return "False";
                                }
                            } else if (_meaning == Meaning.MEANING_FILE_KIND) {
                                // PORT NOTE: upstream tests `<=` against the size of
                                // Ap4StikNames, and so reads one entry past the end
                                // of the array for the value 15 (undefined
                                // behaviour); the port returns "Unknown" for it.
                                if (value >= 0 && ((uint)value) < MetaData.Ap4StikNames.Length) {
                                    @string = "(" + value.ToString(CultureInfo.InvariantCulture) + ") " + MetaData.Ap4StikNames[value];
                                } else {
                                    return "Unknown";
                                }
                            } else {
                                @string = value.ToString(CultureInfo.InvariantCulture);
                            }
                        }
                        return @string;
                    }

                case TypeCategory.TYPE_CATEGORY_STRING:
                    {
                        string category_string;
                        if (_dataAtom.LoadString(out category_string).Succeeded()) {
                            string result = category_string;
                            return result;
                        }
                        break;
                    }

                case TypeCategory.TYPE_CATEGORY_BINARY:
                    {
                        DataBuffer data = new DataBuffer();
                        if (_dataAtom.LoadBytes(data).Succeeded()) {
                            if (_meaning == Meaning.MEANING_ID3_GENRE && data.GetDataSize() == 2) {
                                uint genre = (uint)((data.GetData()[0])*256+data.GetData()[1]);
                                if (genre >= 1 && genre <= MetaData.Ap4Id3Genres.Length) {
                                    @string = "(" + genre.ToString(CultureInfo.InvariantCulture) + ") " + MetaData.Ap4Id3Genres[genre-1];
                                    return @string;
                                } else {
                                    return "Unknown";
                                }
                            } else if (_meaning == Meaning.MEANING_BINARY_ENCODED_CHARS) {
                                string result;
                                result = Ap4Utils.StringFromBytes(data.GetData(), 0, data.GetDataSize());
                                return result;
                            } else {
                                uint dump_length = data.GetDataSize();
                                bool truncate = false;
                                if (dump_length > 16) {
                                    dump_length = 16;
                                    truncate = true;
                                }
                                System.Text.StringBuilder @out = new System.Text.StringBuilder();
                                for (uint i=0; i<dump_length; i++) {
                                    @out.Append(data.GetData()[i].ToString("x2", CultureInfo.InvariantCulture));
                                    @out.Append(' ');
                                }
                                if (truncate) {
                                    @out.Append("... ");
                                }
                                @out.Append('[');
                                @out.Append(((int)data.GetDataSize()).ToString(CultureInfo.InvariantCulture));
                                @out.Append(" bytes]");
                                @string = @out.ToString();
                            }
                        }
                        return @string;
                    }
                default:
                    return "";
            }

            return "";
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomMetaDataValue::ToBytes
        +---------------------------------------------------------------------*/
        public override Ap4Result ToBytes(DataBuffer bytes)
        {
            return _dataAtom.LoadBytes(bytes);
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomMetaDataValue::ToInteger
        +---------------------------------------------------------------------*/
        public override long ToInteger()
        {
            long value;
            if (_dataAtom.LoadInteger(out value).Succeeded()) {
                return value;
            } else {
                return 0;
            }
        }

        // members
        private DataAtom _dataAtom;
    }
}
