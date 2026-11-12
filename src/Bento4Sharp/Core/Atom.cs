// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Atom.h, Ap4Atom.cpp
// Original work Copyright 2002-2011 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bento4
{

    public static partial class Ap4
    {
        /*----------------------------------------------------------------------
        |   macros
        +---------------------------------------------------------------------*/
        /// <summary>
        /// AP4_ATOM_TYPE(c1,c2,c3,c4). Not usable in a `const` initializer: for
        /// constants write the hex value, or
        /// ((uint)'m'&lt;&lt;24)|((uint)'o'&lt;&lt;16)|((uint)'o'&lt;&lt;8)|(uint)'v'.
        /// </summary>
        public static uint ATOM_TYPE(char c1, char c2, char c3, char c4)
        {
            return ((((uint)(byte)c1)<<24) |
                    (((uint)(byte)c2)<<16) |
                    (((uint)(byte)c3)<< 8) |
                    (((uint)(byte)c4)    ));
        }

        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        public const uint ATOM_HEADER_SIZE         = 8;
        public const uint ATOM_HEADER_SIZE_64      = 16;
        public const uint FULL_ATOM_HEADER_SIZE    = 12;
        public const uint FULL_ATOM_HEADER_SIZE_64 = 20;
        public const uint ATOM_MAX_NAME_SIZE       = 256;
        public const uint ATOM_MAX_URI_SIZE        = 512;

        // Ap4Atom.cpp
        internal const uint ATOM_MAX_CLONE_SIZE = 1048576; // 1 meg
        internal const uint UNKNOWN_ATOM_MAX_LOCAL_PAYLOAD_SIZE = 4096;
        internal const uint ATOM_LIST_WRITER_MAX_PADDING = 1024;
    }

    /*----------------------------------------------------------------------
    |   atom types
    +---------------------------------------------------------------------*/
    public static partial class AtomType
    {
        public const uint UDTA  = 0x75647461; // 'udta'
        public const uint URL   = 0x75726c20; // 'url '
        public const uint TRAK  = 0x7472616b; // 'trak'
        public const uint TRAF  = 0x74726166; // 'traf'
        public const uint TKHD  = 0x746b6864; // 'tkhd'
        public const uint TFHD  = 0x74666864; // 'tfhd'
        public const uint TRUN  = 0x7472756e; // 'trun'
        public const uint STTS  = 0x73747473; // 'stts'
        public const uint STSZ  = 0x7374737a; // 'stsz'
        public const uint STZ2  = 0x73747a32; // 'stz2'
        public const uint STSS  = 0x73747373; // 'stss'
        public const uint STSD  = 0x73747364; // 'stsd'
        public const uint STSC  = 0x73747363; // 'stsc'
        public const uint STCO  = 0x7374636f; // 'stco'
        public const uint CO64  = 0x636f3634; // 'co64'
        public const uint STBL  = 0x7374626c; // 'stbl'
        public const uint SINF  = 0x73696e66; // 'sinf'
        public const uint SCHM  = 0x7363686d; // 'schm'
        public const uint SCHI  = 0x73636869; // 'schi'
        public const uint MVHD  = 0x6d766864; // 'mvhd'
        public const uint MEHD  = 0x6d656864; // 'mehd'
        public const uint MP4S  = 0x6d703473; // 'mp4s'
        public const uint MP4A  = 0x6d703461; // 'mp4a'
        public const uint MP4V  = 0x6d703476; // 'mp4v'
        public const uint AVC1  = 0x61766331; // 'avc1'
        public const uint AVC2  = 0x61766332; // 'avc2'
        public const uint AVC3  = 0x61766333; // 'avc3'
        public const uint AVC4  = 0x61766334; // 'avc4'
        public const uint DVAV  = 0x64766176; // 'dvav'
        public const uint DVA1  = 0x64766131; // 'dva1'
        public const uint HEV1  = 0x68657631; // 'hev1'
        public const uint HVC1  = 0x68766331; // 'hvc1'
        public const uint DVHE  = 0x64766865; // 'dvhe'
        public const uint DVH1  = 0x64766831; // 'dvh1'
        public const uint VP08  = 0x76703038; // 'vp08'
        public const uint VP09  = 0x76703039; // 'vp09'
        public const uint VP10  = 0x76703130; // 'vp10'
        public const uint AV01  = 0x61763031; // 'av01'
        public const uint ALAC  = 0x616c6163; // 'alac'
        public const uint ENCA  = 0x656e6361; // 'enca'
        public const uint ENCV  = 0x656e6376; // 'encv'
        public const uint MOOV  = 0x6d6f6f76; // 'moov'
        public const uint MOOF  = 0x6d6f6f66; // 'moof'
        public const uint MVEX  = 0x6d766578; // 'mvex'
        public const uint TREX  = 0x74726578; // 'trex'
        public const uint MINF  = 0x6d696e66; // 'minf'
        public const uint META  = 0x6d657461; // 'meta'
        public const uint MDHD  = 0x6d646864; // 'mdhd'
        public const uint MFHD  = 0x6d666864; // 'mfhd'
        public const uint ILST  = 0x696c7374; // 'ilst'
        public const uint HDLR  = 0x68646c72; // 'hdlr'
        public const uint FTYP  = 0x66747970; // 'ftyp'
        public const uint IODS  = 0x696f6473; // 'iods'
        public const uint ESDS  = 0x65736473; // 'esds'
        public const uint EDTS  = 0x65647473; // 'edts'
        public const uint DRMS  = 0x64726d73; // 'drms'
        public const uint DRMI  = 0x64726d69; // 'drmi'
        public const uint DREF  = 0x64726566; // 'dref'
        public const uint DINF  = 0x64696e66; // 'dinf'
        public const uint CTTS  = 0x63747473; // 'ctts'
        public const uint MDIA  = 0x6d646961; // 'mdia'
        public const uint ELST  = 0x656c7374; // 'elst'
        public const uint VMHD  = 0x766d6864; // 'vmhd'
        public const uint SMHD  = 0x736d6864; // 'smhd'
        public const uint NMHD  = 0x6e6d6864; // 'nmhd'
        public const uint STHD  = 0x73746864; // 'sthd'
        public const uint HMHD  = 0x686d6864; // 'hmhd'
        public const uint FRMA  = 0x66726d61; // 'frma'
        public const uint MDAT  = 0x6d646174; // 'mdat'
        public const uint FREE  = 0x66726565; // 'free'
        public const uint TIMS  = 0x74696d73; // 'tims'
        public const uint RTP_  = 0x72747020; // 'rtp '
        public const uint HNTI  = 0x686e7469; // 'hnti'
        public const uint SDP_  = 0x73647020; // 'sdp '
        public const uint IKMS  = 0x694b4d53; // 'iKMS'
        public const uint ISFM  = 0x6953464d; // 'iSFM'
        public const uint ISLT  = 0x69534c54; // 'iSLT'
        public const uint TREF  = 0x74726566; // 'tref'
        public const uint HINT  = 0x68696e74; // 'hint'
        public const uint CDSC  = 0x63647363; // 'cdsc'
        public const uint MPOD  = 0x6d706f64; // 'mpod'
        public const uint IPIR  = 0x69706972; // 'ipir'
        public const uint CHAP  = 0x63686170; // 'chap'
        public const uint ALIS  = 0x616c6973; // 'alis'
        public const uint SYNC  = 0x73796e63; // 'sync'
        public const uint DPND  = 0x64706e64; // 'dpnd'
        public const uint ODRM  = 0x6f64726d; // 'odrm'
        public const uint ODKM  = 0x6f646b6d; // 'odkm'
        public const uint OHDR  = 0x6f686472; // 'ohdr'
        public const uint ODDA  = 0x6f646461; // 'odda'
        public const uint ODHE  = 0x6f646865; // 'odhe'
        public const uint ODAF  = 0x6f646166; // 'odaf'
        public const uint GRPI  = 0x67727069; // 'grpi'
        public const uint IPRO  = 0x6970726f; // 'ipro'
        public const uint MDRI  = 0x6d647269; // 'mdri'
        public const uint AVCC  = 0x61766343; // 'avcC'
        public const uint HVCC  = 0x68766343; // 'hvcC'
        public const uint DVCC  = 0x64766343; // 'dvcC'
        public const uint VPCC  = 0x76706343; // 'vpcC'
        public const uint DVVC  = 0x64767643; // 'dvvC'
        public const uint HVCE  = 0x68766345; // 'hvcE'
        public const uint AVCE  = 0x61766345; // 'avcE'
        public const uint AV1C  = 0x61763143; // 'av1C'
        public const uint WAVE  = 0x77617665; // 'wave'
        public const uint WIDE  = 0x77696465; // 'wide'
        public const uint UUID  = 0x75756964; // 'uuid'
        public const uint _8ID_ = 0x38696420; // '8id '
        public const uint _8BDL = 0x3862646c; // '8bdl'
        public const uint AC_3  = 0x61632d33; // 'ac-3'
        public const uint EC_3  = 0x65632d33; // 'ec-3'
        public const uint AC_4  = 0x61632d34; // 'ac-4'
        public const uint MLPA  = 0x6d6c7061; // 'mlpa'
        public const uint DTSC  = 0x64747363; // 'dtsc'
        public const uint DTSH  = 0x64747368; // 'dtsh'
        public const uint DTSL  = 0x6474736c; // 'dtsl'
        public const uint DTSE  = 0x64747365; // 'dtse'
        public const uint FLAC  = 0x664c6143; // 'fLaC'
        public const uint OPUS  = 0x4f707573; // 'Opus'
        public const uint MFRA  = 0x6d667261; // 'mfra'
        public const uint TFRA  = 0x74667261; // 'tfra'
        public const uint MFRO  = 0x6d66726f; // 'mfro'
        public const uint TFDT  = 0x74666474; // 'tfdt'
        public const uint TENC  = 0x74656e63; // 'tenc'
        public const uint SENC  = 0x73656e63; // 'senc'
        public const uint SAIO  = 0x7361696f; // 'saio'
        public const uint SAIZ  = 0x7361697a; // 'saiz'
        public const uint PDIN  = 0x7064696e; // 'pdin'
        public const uint BLOC  = 0x626c6f63; // 'bloc'
        public const uint AINF  = 0x61696e66; // 'ainf'
        public const uint PSSH  = 0x70737368; // 'pssh'
        public const uint MARL  = 0x6d61726c; // 'marl'
        public const uint MKID  = 0x6d6b6964; // 'mkid'
        public const uint PRFT  = 0x70726674; // 'prft'
        public const uint STPP  = 0x73747070; // 'stpp'
        public const uint DAC3  = 0x64616333; // 'dac3'
        public const uint DEC3  = 0x64656333; // 'dec3'
        public const uint DAC4  = 0x64616334; // 'dac4'
        public const uint DMLP  = 0x646d6c70; // 'dmlp'
        public const uint SIDX  = 0x73696478; // 'sidx'
        public const uint SSIX  = 0x73736978; // 'ssix'
        public const uint SBGP  = 0x73626770; // 'sbgp'
        public const uint SGPD  = 0x73677064; // 'sgpd'
        public const uint COLR  = 0x636f6c72; // 'colr'
    }

    /*----------------------------------------------------------------------
    |   AP4_AtomInspector
    +---------------------------------------------------------------------*/
    /// <summary>
    /// Class used in a visitor pattern to walk all the atoms in a tree of
    /// <see cref="Atom"/> objects.
    /// </summary>
    public class AtomInspector
    {
        // types
        public enum FormatHint {
            HINT_NONE    = 0,
            HINT_HEX     = 1,
            HINT_BOOLEAN = 2
        }

        // constructor and destructor
        public AtomInspector() { _verbosity = 0; }

        // methods
        public void SetVerbosity(uint verbosity) { _verbosity = verbosity; }
        public uint GetVerbosity()               { return _verbosity;      }

        // virtual methods
        public virtual void StartAtom(string name,
                                      byte   version,
                                      uint   flags,
                                      uint   header_size,
                                      ulong  size) {}
        public virtual void EndAtom() {}
        public virtual void StartDescriptor(string name,
                                            uint   header_size,
                                            ulong  size) {}
        public virtual void EndDescriptor() {}
        public virtual void StartArray(string name, uint element_count = 0) {}
        public virtual void EndArray() {}
        public virtual void StartObject(string name, uint field_count = 0, bool compact = false) {}
        public virtual void EndObject() {}
        public virtual void AddField(string     name,
                                     ulong      value,
                                     FormatHint hint = FormatHint.HINT_NONE) {
        }
        public virtual void AddFieldF(string     name,
                                      float      value,
                                      FormatHint hint = FormatHint.HINT_NONE) {
        }
        public virtual void AddField(string     name,
                                     string     value,
                                     FormatHint hint = FormatHint.HINT_NONE) {
        }
        /// <summary>Bytes field. This is the overload to override (the one without offset forwards to it).</summary>
        public virtual void AddField(string     name,
                                     byte[]     bytes,
                                     int        offset,
                                     uint       byte_count,
                                     FormatHint hint = FormatHint.HINT_NONE) {
        }

        // PORT NOTE: convenience overloads, not virtual.
        /// <summary>AddField(name, bytes, byte_count, hint) of upstream.</summary>
        public void AddField(string     name,
                             byte[]     bytes,
                             uint       byte_count,
                             FormatHint hint = FormatHint.HINT_NONE) {
            AddField(name, bytes, 0, byte_count, hint);
        }
        /// <summary>
        /// Signed values: converted to AP4_UI64 like the implicit conversion of
        /// C++ does (so that an `int` variable can be passed without a cast).
        /// </summary>
        public void AddField(string     name,
                             long       value,
                             FormatHint hint = FormatHint.HINT_NONE) {
            AddField(name, unchecked((ulong)value), hint);
        }

        protected uint _verbosity;
    }

    /*----------------------------------------------------------------------
    |   AP4_PrintInspector
    +---------------------------------------------------------------------*/
    public class PrintInspector : AtomInspector
    {
        // types
        private class Context {
            public enum Type {
                TOP_LEVEL,
                ATOM,
                ARRAY,
                OBJECT,
                COMPACT_OBJECT
            }

            public Context(Type type) { _type = type; _arrayIndex = 0; }

            public Type _type;
            public uint _arrayIndex;
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::AP4_PrintInspector
        +---------------------------------------------------------------------*/
        public PrintInspector(ByteStream stream, uint indent = 0)
        {
            _stream = stream;
            PushContext(Context.Type.TOP_LEVEL);
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::PushContext
        +---------------------------------------------------------------------*/
        private void PushContext(Context.Type type)
        {
            _contexts.Add(new Context(type));
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::PopContext
        +---------------------------------------------------------------------*/
        private void PopContext()
        {
            // PORT NOTE: AP4_Array::RemoveLast is a no-op on an empty array
            if (_contexts.Count != 0) _contexts.RemoveAt(_contexts.Count - 1);
        }

        private Context LastContext() { return _contexts[_contexts.Count - 1]; }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::PrintPrefix
        +---------------------------------------------------------------------*/
        private void PrintPrefix()
        {
            if (LastContext()._type == Context.Type.COMPACT_OBJECT) {
                if (LastContext()._arrayIndex++ != 0) {
                    _stream.WriteString(", ");
                }
                return;
            }
            if (_contexts.Count >= 1) {
                string prefix = InspectorUtils.MakePrefixString((uint)(_contexts.Count - 1) * 2, 256);
                _stream.WriteString(prefix);
                if (LastContext()._type == Context.Type.ARRAY) {
                    // "(%8d) "
                    string index = "(" + ((int)LastContext()._arrayIndex).ToString(CultureInfo.InvariantCulture).PadLeft(8) + ") ";
                    _stream.WriteString(index);
                    ++LastContext()._arrayIndex;
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::PrintSuffix
        +---------------------------------------------------------------------*/
        private void PrintSuffix()
        {
            if (LastContext()._type != Context.Type.COMPACT_OBJECT) {
                _stream.WriteString("\n");
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::StartAtom
        +---------------------------------------------------------------------*/
        public override void StartAtom(string name,
                                       byte   version,
                                       uint   flags,
                                       uint   header_size,
                                       ulong  size)
        {
            PrintPrefix();
            PushContext(Context.Type.ATOM);

            // write atom name
            string info;
            string extra = "";
            if (header_size == 28 || header_size == 12 || header_size == 20) {
                if (version != 0 && flags != 0) {
                    // ", version=%d, flags=%x"
                    extra = ", version=" + version.ToString(CultureInfo.InvariantCulture) +
                            ", flags=" + flags.ToString("x", CultureInfo.InvariantCulture);
                } else if (version != 0) {
                    // ", version=%d"
                    extra = ", version=" + version.ToString(CultureInfo.InvariantCulture);
                } else if (flags != 0) {
                    // ", flags=%x"
                    extra = ", flags=" + flags.ToString("x", CultureInfo.InvariantCulture);
                }
            }
            // "size=%d+%lld%s"
            info = "size=" + ((int)header_size).ToString(CultureInfo.InvariantCulture) +
                   "+" + ((long)(size-header_size)).ToString(CultureInfo.InvariantCulture) +
                   extra;
            info = InspectorUtils.Truncate(info, 128);
            _stream.WriteString("[");
            _stream.WriteString(name);
            _stream.WriteString("] ");
            _stream.WriteString(info);

            PrintSuffix();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::EndAtom
        +---------------------------------------------------------------------*/
        public override void EndAtom()
        {
            PopContext();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::StartDescriptor
        +---------------------------------------------------------------------*/
        public override void StartDescriptor(string name,
                                             uint   header_size,
                                             ulong  size)
        {
            PrintPrefix();
            PushContext(Context.Type.ATOM);

            // write atom name
            // "size=%d+%lld"
            string info = "size=" + ((int)header_size).ToString(CultureInfo.InvariantCulture) +
                          "+" + ((long)(size-header_size)).ToString(CultureInfo.InvariantCulture);
            info = InspectorUtils.Truncate(info, 128);
            _stream.WriteString("[");
            _stream.WriteString(name);
            _stream.WriteString("] ");
            _stream.WriteString(info);

            PrintSuffix();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::EndDescriptor
        +---------------------------------------------------------------------*/
        public override void EndDescriptor()
        {
            EndAtom();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::StartArray
        +---------------------------------------------------------------------*/
        public override void StartArray(string name, uint element_count = 0)
        {
            PrintPrefix();
            PushContext(Context.Type.ARRAY);

            if (name != null) {
                _stream.WriteString(name);
                _stream.WriteString(":");
            }

            PrintSuffix();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::EndArray
        +---------------------------------------------------------------------*/
        public override void EndArray()
        {
            PopContext();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::StartObject
        +---------------------------------------------------------------------*/
        public override void StartObject(string name, uint field_count = 0, bool compact = false)
        {
            PrintPrefix();
            PushContext(compact ? Context.Type.COMPACT_OBJECT : Context.Type.OBJECT);

            if (name != null) {
                _stream.WriteString(name);
                _stream.WriteString(": ");
            }

            PrintSuffix();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::EndObject
        +---------------------------------------------------------------------*/
        public override void EndObject()
        {
            if (LastContext()._type == Context.Type.COMPACT_OBJECT) {
                _stream.WriteString("\n");
            }
            PopContext();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::AddField
        +---------------------------------------------------------------------*/
        public override void AddField(string name, string value, FormatHint hint = FormatHint.HINT_NONE)
        {
            PrintPrefix();
            if (name != null) {
                _stream.WriteString(name);
                _stream.WriteString(" = ");
            }
            _stream.WriteString(value);

            PrintSuffix();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::AddField
        +---------------------------------------------------------------------*/
        public override void AddField(string name, ulong value, FormatHint hint = FormatHint.HINT_NONE)
        {
            PrintPrefix();
            if (name != null) {
                _stream.WriteString(name);
                _stream.WriteString(" = ");
            }

            // hint == HINT_HEX ? "%llx":"%lld"
            string str = hint == FormatHint.HINT_HEX ?
                value.ToString("x", CultureInfo.InvariantCulture) :
                ((long)value).ToString(CultureInfo.InvariantCulture);
            _stream.WriteString(str);

            PrintSuffix();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::AddFieldF
        +---------------------------------------------------------------------*/
        public override void AddFieldF(string name, float value, FormatHint hint = FormatHint.HINT_NONE)
        {
            PrintPrefix();
            if (name != null) {
                _stream.WriteString(name);
                _stream.WriteString(" = ");
            }

            // "%f" in a char[32]
            string str = InspectorUtils.Truncate(Ap4Utils.FormatFloat(value), 32);
            _stream.WriteString(str);

            PrintSuffix();
        }

        /*----------------------------------------------------------------------
        |   AP4_PrintInspector::AddField
        +---------------------------------------------------------------------*/
        public override void AddField(string     name,
                                      byte[]     bytes,
                                      int        offset,
                                      uint       byte_count,
                                      FormatHint hint = FormatHint.HINT_NONE)
        {
            PrintPrefix();
            if (name != null) {
                _stream.WriteString(name);
                _stream.WriteString(" = ");
            }
            _stream.WriteString("[");
            InspectorUtils.WriteHexBytes(_stream, bytes, offset, byte_count);
            _stream.WriteString("]");

            PrintSuffix();
        }

        // members
        private ByteStream    _stream;
        private List<Context> _contexts = new List<Context>();
    }

    /*----------------------------------------------------------------------
    |   AP4_JsonInspector
    +---------------------------------------------------------------------*/
    // PORT NOTE: the C++ destructor writes the closing "\n]\n" of the JSON
    // document. The port does it in Dispose(): callers MUST dispose the inspector
    // (or wrap it in `using`) to get a complete document.
    public class JsonInspector : AtomInspector, IDisposable
    {
        // types
        private class Context {
            public enum Type {
                TOP_LEVEL,
                ATOM,
                ARRAY,
                OBJECT
            }

            public Context(Type type) { _type = type; _fieldCount = 0; _childrenCount = 0; }

            public Type _type;
            public uint _fieldCount;
            public uint _childrenCount; // to count atoms within atoms
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::AP4_JsonInspector
        +---------------------------------------------------------------------*/
        public JsonInspector(ByteStream stream)
        {
            _stream = stream;
            _stream.WriteString("[\n");
            PushContext(Context.Type.TOP_LEVEL);
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::~AP4_JsonInspector
        +---------------------------------------------------------------------*/
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stream.WriteString("\n]\n");
            GC.SuppressFinalize(this);
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::PushContext
        +---------------------------------------------------------------------*/
        private void PushContext(Context.Type type)
        {
            _contexts.Add(new Context(type));
            _prefix = InspectorUtils.MakePrefixString((uint)_contexts.Count * 2, 256);
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::PopContext
        +---------------------------------------------------------------------*/
        private void PopContext()
        {
            // PORT NOTE: AP4_Array::RemoveLast is a no-op on an empty array
            if (_contexts.Count != 0) _contexts.RemoveAt(_contexts.Count - 1);
            _prefix = InspectorUtils.MakePrefixString((uint)_contexts.Count * 2, 256);
        }

        private Context LastContext() { return _contexts[_contexts.Count - 1]; }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::OnFieldAdded
        +---------------------------------------------------------------------*/
        private void OnFieldAdded()
        {
            if (LastContext()._fieldCount != 0) {
                _stream.WriteString(",\n");
            }
            ++LastContext()._fieldCount;
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::PrintFieldName
        +---------------------------------------------------------------------*/
        private void PrintFieldName(string name)
        {
            if (name == null) return;
            _stream.WriteString("\"");
            _stream.WriteString(EscapeString(name));
            _stream.WriteString("\": ");
        }

        /*----------------------------------------------------------------------
        |   ReadUTF8
        +---------------------------------------------------------------------*/
        private static Ap4Result ReadUTF8(byte[] utf, int pos, ref uint length, out uint code_point) {
            code_point = 0;
            if (length < 1) {
                return Ap4Result.ERROR_NOT_ENOUGH_DATA;
            }
            uint c = utf[pos+0];
            if ((c & 0x80) == 0) {
                length = 1;
                code_point = c;
                return Ap4Result.SUCCESS;
            }
            if (length < 2) {
                return Ap4Result.ERROR_NOT_ENOUGH_DATA;
            }
            code_point = 0;
            if ((utf[pos+1] & 0xc0) != 0x80) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            if ((c & 0xe0) == 0xe0) {
                if (length < 3) {
                    return Ap4Result.ERROR_NOT_ENOUGH_DATA;
                }
                if ((utf[pos+2] & 0xc0) != 0x80) {
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                if ((c & 0xf0) == 0xf0) {
                    if (length < 4) {
                        return Ap4Result.ERROR_NOT_ENOUGH_DATA;
                    }
                    if ((c & 0xf8) != 0xf0 || (utf[pos+3] & 0xc0) != 0x80) {
                        return Ap4Result.ERROR_INVALID_FORMAT;
                    }
                    length = 4;
                    c  = (uint)(utf[pos+0] & 0x07) << 18;
                    c |= (uint)(utf[pos+1] & 0x3f) << 12;
                    c |= (uint)(utf[pos+2] & 0x3f) <<  6;
                    c |= (uint)(utf[pos+3] & 0x3f);
                } else {
                    length = 3;
                    c  = (uint)(utf[pos+0] & 0x0f) << 12;
                    c |= (uint)(utf[pos+1] & 0x3f) << 6;
                    c |= (uint)(utf[pos+2] & 0x3f);
                }
            } else {
              /* 2-byte code */
                length = 2;
                c  = (uint)(utf[pos+0] & 0x1f) << 6;
                c |= (uint)(utf[pos+1] & 0x3f);
            }
            code_point = c;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::EscapeString
        +---------------------------------------------------------------------*/
        // PORT NOTE: works on the bytes of the string, like upstream, so that the
        // output is byte-for-byte identical (including the upstream behaviour of
        // silently cutting the string at the first malformed UTF-8 sequence).
        private static string EscapeString(string @string)
        {
            byte[] result = Ap4Utils.StringToCStringBytes(@string);

            // Shortcut
            if (result.Length == 0) {
                return "";
            }

            // Compute the output size
            uint string_length = (uint)result.Length;
            byte[] input = result;
            int input_pos = 0;
            uint input_length = string_length;
            uint output_size = 0;
            while (input_length != 0) {
                uint chars_available = input_length;
                uint code_point = 0;
                Ap4Result r = ReadUTF8(input, input_pos, ref chars_available, out code_point);
                if (r.Failed()) {
                    // stop, but don't fail
                    break;
                }
                if (code_point == '"' || code_point == '\\') {
                    output_size += 2;
                } else if (code_point <= 0x1F) {
                    output_size += 6;
                } else {
                    output_size += chars_available;
                }
                input_length -= chars_available;
                input_pos    += (int)chars_available;
            }

            // Shortcut
            if (output_size == (uint)result.Length) {
                return Ap4Utils.StringFromBytes(result);
            }

            // Compute the escaped string in a temporary buffer
            byte[] buffer = new byte[output_size];
            int escaped = 0;
            input_pos = 0;
            input_length = string_length;
            while (input_length != 0) {
                uint chars_available = input_length;
                uint code_point = 0;
                Ap4Result r = ReadUTF8(input, input_pos, ref chars_available, out code_point);
                if (r.Failed()) {
                    // stop, but don't fail
                    break;
                }
                if (code_point == '"' || code_point == '\\') {
                    buffer[escaped++] = (byte)'\\';
                    buffer[escaped++] = (byte)code_point;
                } else if (code_point <= 0x1F) {
                    buffer[escaped++] = (byte)'\\';
                    buffer[escaped++] = (byte)'u';
                    buffer[escaped++] = (byte)'0';
                    buffer[escaped++] = (byte)'0';
                    buffer[escaped++] = (byte)Ap4Utils.NibbleHex(code_point >> 4);
                    buffer[escaped++] = (byte)Ap4Utils.NibbleHex(code_point & 0x0F);
                } else {
                    for (uint i = 0; i < chars_available; i++) {
                        buffer[escaped++] = input[input_pos+i];
                    }
                }
                input_length -= chars_available;
                input_pos    += (int)chars_available;
            }

            // Copy the buffer to a final string
            return Ap4Utils.StringFromBytes(buffer, 0, output_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::StartAtom
        +---------------------------------------------------------------------*/
        public override void StartAtom(string name,
                                       byte   version,
                                       uint   flags,
                                       uint   header_size,
                                       ulong  size)
        {
            OnFieldAdded();
            ++LastContext()._childrenCount;

            // Starting the first atom within an atom means staring a childen array
            if (LastContext()._type == Context.Type.ATOM && LastContext()._childrenCount == 1) {
                _stream.WriteString(_prefix);
                _stream.WriteString("\"children\":[ \n");
            }

            _stream.WriteString(_prefix);
            _stream.WriteString("{\n");
            PushContext(Context.Type.ATOM);
            OnFieldAdded();
            _stream.WriteString(_prefix);
            PrintFieldName("name");
            _stream.WriteString("\"");
            _stream.WriteString(EscapeString(name));
            _stream.WriteString("\"");
            OnFieldAdded();
            _stream.WriteString(_prefix);
            PrintFieldName("header_size");
            string val;
            val = ((int)header_size).ToString(CultureInfo.InvariantCulture); // "%d"
            _stream.WriteString(val);
            OnFieldAdded();
            _stream.WriteString(_prefix);
            PrintFieldName("size");
            val = ((long)size).ToString(CultureInfo.InvariantCulture); // "%lld"
            _stream.WriteString(val);

            if (version != 0) {
                OnFieldAdded();
                _stream.WriteString(_prefix);
                PrintFieldName("version");
                val = version.ToString(CultureInfo.InvariantCulture); // "%d"
                _stream.WriteString(val);
            }
            if (flags != 0) {
                OnFieldAdded();
                _stream.WriteString(_prefix);
                PrintFieldName("flags");
                val = ((int)flags).ToString(CultureInfo.InvariantCulture); // "%d"
                _stream.WriteString(val);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::EndAtom
        +---------------------------------------------------------------------*/
        public override void EndAtom()
        {
            // Ending an atom with children means we need to close the children array
            if (LastContext()._childrenCount != 0) {
                _stream.WriteString("]");
            }
            PopContext();
            _stream.WriteString("\n");
            _stream.WriteString(_prefix);
            _stream.WriteString("}");
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::StartDescriptor
        +---------------------------------------------------------------------*/
        public override void StartDescriptor(string name,
                                             uint   header_size,
                                             ulong  size)
        {
            StartAtom(name, 0, 0, header_size, size);
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::EndDescriptor
        +---------------------------------------------------------------------*/
        public override void EndDescriptor()
        {
            EndAtom();
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::StartArray
        +---------------------------------------------------------------------*/
        public override void StartArray(string name, uint element_count = 0)
        {
            OnFieldAdded();
            _stream.WriteString(_prefix);
            if (name != null) {
                PrintFieldName(name);
            }
            _stream.WriteString("[\n");
            PushContext(Context.Type.ARRAY);
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::EndArray
        +---------------------------------------------------------------------*/
        public override void EndArray()
        {
            PopContext();
            _stream.WriteString("\n");
            _stream.WriteString(_prefix);
            _stream.WriteString("]");
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::StartObject
        +---------------------------------------------------------------------*/
        public override void StartObject(string name, uint field_count = 0, bool compact = false)
        {
            OnFieldAdded();
            _stream.WriteString(_prefix);
            if (name != null) {
                PrintFieldName(name);
            }
            _stream.WriteString("{\n");
            PushContext(Context.Type.ARRAY);
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::EndObject
        +---------------------------------------------------------------------*/
        public override void EndObject()
        {
            PopContext();
            _stream.WriteString("\n");
            _stream.WriteString(_prefix);
            _stream.WriteString("}");
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::AddField
        +---------------------------------------------------------------------*/
        public override void AddField(string name, string value, FormatHint hint = FormatHint.HINT_NONE)
        {
            OnFieldAdded();
            _stream.WriteString(_prefix);
            PrintFieldName(name);
            _stream.WriteString("\"");
            _stream.WriteString(EscapeString(value));
            _stream.WriteString("\"");
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::AddField
        +---------------------------------------------------------------------*/
        public override void AddField(string name, ulong value, FormatHint hint = FormatHint.HINT_NONE)
        {
            OnFieldAdded();
            _stream.WriteString(_prefix);
            PrintFieldName(name);
            // "%lld"
            string str = ((long)value).ToString(CultureInfo.InvariantCulture);
            _stream.WriteString(str);
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::AddFieldF
        +---------------------------------------------------------------------*/
        public override void AddFieldF(string name, float value, FormatHint hint = FormatHint.HINT_NONE)
        {
            OnFieldAdded();
            _stream.WriteString(_prefix);
            PrintFieldName(name);
            // "%f" in a char[32]
            string str = InspectorUtils.Truncate(Ap4Utils.FormatFloat(value), 32);
            _stream.WriteString(str);
        }

        /*----------------------------------------------------------------------
        |   AP4_JsonInspector::AddField
        +---------------------------------------------------------------------*/
        public override void AddField(string     name,
                                      byte[]     bytes,
                                      int        offset,
                                      uint       byte_count,
                                      FormatHint hint = FormatHint.HINT_NONE)
        {
            OnFieldAdded();
            _stream.WriteString(_prefix);
            PrintFieldName(name);
            _stream.WriteString("\"[");
            InspectorUtils.WriteHexBytes(_stream, bytes, offset, byte_count);
            _stream.WriteString("]\"");
        }

        // members
        private ByteStream    _stream;
        private List<Context> _contexts = new List<Context>();
        private string        _prefix = "";
        private bool          _disposed;
    }

    /// <summary>PORT NOTE: static helpers of Ap4Atom.cpp shared by the two inspectors.</summary>
    internal static class InspectorUtils
    {
        /*----------------------------------------------------------------------
        |   AP4_MakePrefixString
        +---------------------------------------------------------------------*/
        // `size` is the size of the char buffer of upstream (terminator included)
        public static string MakePrefixString(uint indent, uint size)
        {
            if (size == 0) return "";
            if (indent >= size-1) indent = size-1;
            if (indent < (uint)s_prefixes.Length) return s_prefixes[indent];
            return new string(' ', (int)indent);
        }

        private static readonly string[] s_prefixes = MakePrefixes();

        private static string[] MakePrefixes()
        {
            string[] prefixes = new string[65];
            for (int i = 0; i < prefixes.Length; i++) prefixes[i] = new string(' ', i);
            return prefixes;
        }

        /// <summary>snprintf truncation: at most size-1 chars are kept.</summary>
        public static string Truncate(string str, int size)
        {
            return str.Length > size-1 ? str.Substring(0, size-1) : str;
        }

        /// <summary>The " %02x" loop of AddField(bytes): "00 01 02" (no leading space, lower case).</summary>
        public static void WriteHexBytes(ByteStream stream, byte[] bytes, int offset, uint byte_count)
        {
            const string hex = "0123456789abcdef";
            const int chunk_bytes = 1024;
            byte[] chunk = new byte[(int)Math.Min(byte_count, (uint)chunk_bytes) * 3];
            uint done = 0;
            while (done < byte_count) {
                int n = 0;
                uint count = Math.Min(byte_count-done, (uint)chunk_bytes);
                for (uint i = 0; i < count; i++) {
                    byte b = bytes[offset+(int)(done+i)];
                    if (done+i != 0) chunk[n++] = (byte)' ';
                    chunk[n++] = (byte)hex[b >> 4];
                    chunk[n++] = (byte)hex[b & 0x0F];
                }
                stream.Write(chunk, (uint)n);
                done += count;
            }
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_AtomParent
    +---------------------------------------------------------------------*/
    /// <summary>
    /// Base class for containers of atoms.
    /// This class also implements the logic for finding descendents by name.
    /// </summary>
    // PORT NOTE: upstream, AP4_ContainerAtom inherits from both AP4_Atom and
    // AP4_AtomParent. C# has no multiple inheritance, so in the port Atom derives
    // from AtomParent: `ContainerAtom : Atom : AtomParent`. Consequences:
    //   - an AtomParent variable can hold a ContainerAtom, an AP4_File-like
    //     standalone parent, or a plain `new AtomParent()`, as upstream;
    //   - `parent as ContainerAtom` and `parent as Atom` behave as upstream;
    //   - BUT `atom as AtomParent` succeeds for EVERY atom. Where upstream does
    //     AP4_DYNAMIC_CAST(AP4_AtomParent, atom) on an AP4_Atom*, write
    //     `atom as ContainerAtom` (the only atoms that are parents upstream are
    //     the AP4_ContainerAtom subclasses);
    //   - leaf atoms inherit (and should ignore) the child management methods.
    public class AtomParent
    {
        // base methods
        public List<Atom> GetChildren() { return _children; }

        /*----------------------------------------------------------------------
        |   AP4_AtomParent::CopyChildren
        +---------------------------------------------------------------------*/
        public Ap4Result CopyChildren(AtomParent destination)
        {
            for (int i = 0; i < _children.Count; i++) {
                Atom child_clone = _children[i].Clone();
                // PORT NOTE: upstream would crash on a NULL clone
                if (child_clone != null) destination.AddChild(child_clone);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomParent::AddChild
        +---------------------------------------------------------------------*/
        public virtual Ap4Result AddChild(Atom child, int position = -1)
        {
            // check that the child does not already have a parent
            if (child.GetParent() != null) return Ap4Result.ERROR_INVALID_PARAMETERS;

            // attach the child
            Ap4Result result;
            if (position == -1) {
                // insert at the tail
                _children.Add(child);
                result = Ap4Result.SUCCESS;
            } else if (position == 0) {
                // insert at the head
                _children.Insert(0, child);
                result = Ap4Result.SUCCESS;
            } else {
                // insert after <n-1>
                if (position > 0 && position <= _children.Count) {
                    _children.Insert(position, child);
                    result = Ap4Result.SUCCESS;
                } else {
                    result = Ap4Result.ERROR_OUT_OF_RANGE;
                }
            }
            if (result.Failed()) return result;

            // notify the child of its parent
            child.SetParent(this);

            // get a chance to update
            OnChildAdded(child);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomParent::RemoveChild
        +---------------------------------------------------------------------*/
        public virtual Ap4Result RemoveChild(Atom child)
        {
            // check that this is our child
            if (child.GetParent() != this) return Ap4Result.ERROR_INVALID_PARAMETERS;

            // remove the child
            int index = IndexOfChild(child);
            if (index < 0) return Ap4Result.ERROR_NO_SUCH_ITEM;
            _children.RemoveAt(index);

            // notify that child that it is orphaned
            child.SetParent(null);

            // get a chance to update
            OnChildRemoved(child);

            return Ap4Result.SUCCESS;
        }

        private int IndexOfChild(Atom child)
        {
            for (int i = 0; i < _children.Count; i++) {
                if (ReferenceEquals(_children[i], child)) return i;
            }
            return -1;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomParent::DeleteChild
        +---------------------------------------------------------------------*/
        public virtual Ap4Result DeleteChild(uint type, uint index = 0)
        {
            // find the child
            Atom child = GetChild(type, index);
            if (child == null) return Ap4Result.FAILURE;

            // remove the child
            Ap4Result result = RemoveChild(child);
            if (result.Failed()) return result;

            // delete the child (PORT NOTE: nothing to do)

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomParent::GetChild
        +---------------------------------------------------------------------*/
        public virtual Atom GetChild(uint type, uint index = 0)
        {
            Atom atom;
            Ap4Result result = _children.Find(new AtomFinder(type, index), out atom);
            if (result.Succeeded()) {
                return atom;
            } else {
                return null;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomParent::GetChild
        +---------------------------------------------------------------------*/
        /// <summary>Finds a 'uuid' child by its 16-byte UUID.</summary>
        public virtual Atom GetChild(byte[] uuid, uint index = 0)
        {
            for (int i = 0; i < _children.Count; i++) {
                Atom atom = _children[i];
                if (atom.GetType() == AtomType.UUID) {
                    // PORT NOTE: upstream casts to AP4_UuidAtom (which lives in
                    // another file); the port goes through the IUuidAtom
                    // interface that UuidAtom implements. Atoms of type 'uuid'
                    // that do not implement it are skipped (upstream would
                    // dereference a NULL pointer).
                    IUuidAtom uuid_atom = atom as IUuidAtom;
                    if (uuid_atom == null) continue;
                    byte[] atom_uuid = uuid_atom.GetUuid();
                    bool equal = true;
                    for (int x = 0; x < 16; x++) {
                        if (atom_uuid[x] != uuid[x]) { equal = false; break; }
                    }
                    if (equal) {
                        if (index == 0) return atom;
                        --index;
                    }
                }
            }
            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomParent::FindChild
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Finds a descendant by path: 4-character codes (or 32 hex digit UUIDs)
        /// separated by '/', each optionally followed by [index]; ex:
        /// "moov/trak[1]/mdia/minf".
        /// </summary>
        public virtual Atom FindChild(string path,
                                       bool   auto_create = false,
                                       bool   auto_create_full = false)
        {
            // start from here
            AtomParent parent = this;

            // PORT NOTE: the path is walked as a zero-terminated byte string
            byte[] bytes = Ap4Utils.StringToCStringBytes(path);
            byte[] p = new byte[bytes.Length+1];
            Array.Copy(bytes, p, bytes.Length);
            int pos = 0;

            // walk the path
            while (p[pos] != 0 && p[pos+1] != 0 && p[pos+2] != 0 && p[pos+3] != 0) {
                // we have 4 valid chars
                int end = pos+4;

                // look for the end or a separator
                while (p[end] != 0 && p[end] != '/' && p[end] != '[') {
                    ++end;
                }

                // decide if this is a 4-character code or a UUID
                byte[] uuid = new byte[16];
                uint type = 0;
                bool is_uuid = false;
                if (end == pos+4) {
                    // 4-character code
                    type = AtomTypeFromSignedChars(p[pos], p[pos+1], p[pos+2], p[pos+3]);
                } else if (end == pos+32) {
                    // UUID
                    is_uuid = true;
                    // AP4_ParseHex(path, uuid, sizeof(uuid))
                    for (int i = 0; i < 16; i++) {
                        uuid[i] = (byte)((Ap4Utils.HexNibble((char)p[pos+2*i]) << 4) | Ap4Utils.HexNibble((char)p[pos+2*i+1]));
                    }
                } else {
                    // malformed path
                    return null;
                }

                // parse the array index, if any
                int index = 0;
                if (p[end] == '[') {
                    int x = end+1;
                    while (p[x] >= '0' && p[x] <= '9') {
                        index = 10*index+(p[x++] - '0');
                    }
                    if (p[x] != ']') {
                        // malformed path
                        return null;
                    }
                    end = x+1;
                }

                // check what's at the end now
                if (p[end] == '/') {
                    ++end;
                } else if (p[end] != 0) {
                    // malformed path
                    return null;
                }

                // look for this atom in the current list
                Atom atom = null;
                if (is_uuid) {
                    atom = parent.GetChild(uuid, (uint)index);
                } else {
                    atom = parent.GetChild(type, (uint)index);
                }
                if (atom == null) {
                    // not found
                    if (auto_create && (index == 0)) {
                        if (auto_create_full) {
                            atom = new ContainerAtom(type, (byte)0, (uint)0);
                        } else {
                            atom = new ContainerAtom(type);
                        }
                        parent.AddChild(atom);
                    } else {
                        return null;
                    }
                }

                if (p[end] != 0) {
                    pos = end;
                    // if this atom is an atom parent, recurse
                    parent = atom as ContainerAtom;
                    if (parent == null) return null;
                } else {
                    return atom;
                }
            }

            // not found
            return null;
        }

        // PORT NOTE: AP4_ATOM_TYPE() / TypeFromString applied to (signed) `char`
        // values, as upstream does: a char >= 0x80 is sign-extended before the
        // shift, which sets all the bits above it. Kept for bit-exactness.
#pragma warning disable 0675 // sign extension is intended
        internal static uint AtomTypeFromSignedChars(byte c1, byte c2, byte c3, byte c4)
        {
            return unchecked(
                (((uint)(sbyte)c1)<<24) |
                (((uint)(sbyte)c2)<<16) |
                (((uint)(sbyte)c3)<< 8) |
                (((uint)(sbyte)c4)    ));
        }
#pragma warning restore 0675

        // methods designed to be overridden
        public virtual void OnChildChanged(Atom child) {}
        public virtual void OnChildAdded(Atom child)   {}
        public virtual void OnChildRemoved(Atom child) {}

        // members
        protected List<Atom> _children = new List<Atom>();
    }

    /*----------------------------------------------------------------------
    |   AP4_Atom
    +---------------------------------------------------------------------*/
    /// <summary>
    /// Abstract base class for all atom types.
    /// </summary>
    // PORT NOTE: derives from AtomParent, see the note on AtomParent.
    public abstract class Atom : AtomParent
    {
        // types
        // typedef AP4_UI32 Type;  -> uint

        // class methods
        /*----------------------------------------------------------------------
        |   AP4_Atom::TypeFromString
        +---------------------------------------------------------------------*/
        public static uint TypeFromString(string s)
        {
            // convert the name into an atom type
            // PORT NOTE: a string shorter than 4 bytes is padded with zeros
            // (upstream reads past the terminator).
            byte[] bytes = Ap4Utils.StringToCStringBytes(s);
            byte c0 = bytes.Length > 0 ? bytes[0] : (byte)0;
            byte c1 = bytes.Length > 1 ? bytes[1] : (byte)0;
            byte c2 = bytes.Length > 2 ? bytes[2] : (byte)0;
            byte c3 = bytes.Length > 3 ? bytes[3] : (byte)0;
            return AtomTypeFromSignedChars(c0, c1, c2, c3);
        }

        /*----------------------------------------------------------------------
        |   AP4_Atom::ReadFullHeader
        +---------------------------------------------------------------------*/
        public static Ap4Result ReadFullHeader(ByteStream stream,
                                               out byte   version,
                                               out uint   flags)
        {
            uint header;
            Ap4Result _result = stream.ReadUI32(out header);
            if (_result.Failed()) {
                // PORT NOTE: left unassigned upstream
                version = 0;
                flags = 0;
                return _result;
            }
            version = (byte)((header>>24)&0x000000FF);
            flags   = (header    )&0x00FFFFFF;

            return Ap4Result.SUCCESS;
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_Atom::AP4_Atom
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a simple atom with a specified type and 32-bit size.
        /// </summary>
        protected Atom(uint type, uint size = Ap4.ATOM_HEADER_SIZE)
        {
            _type = type;
            _size32 = size;
            _size64 = 0;
            _isFull = false;
            _version = 0;
            _flags = 0;
            _parent = null;
        }

        /*----------------------------------------------------------------------
        |   AP4_Atom::AP4_Atom
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a simple atom with a specified type and 64-bit size.
        /// </summary>
        protected Atom(uint type, ulong size, bool force_64 = false)
        {
            _type = type;
            _size32 = 0;
            _size64 = 0;
            _isFull = false;
            _version = 0;
            _flags = 0;
            _parent = null;
            SetSize(size, force_64);
        }

        /*----------------------------------------------------------------------
        |   AP4_Atom::AP4_Atom
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a full atom with a specified type, 32-bit size, version and flags.
        /// </summary>
        protected Atom(uint type,
                       uint size,
                       byte version,
                       uint flags)
        {
            _type = type;
            _size32 = size;
            _size64 = 0;
            _isFull = true;
            _version = version;
            _flags = flags;
            _parent = null;
        }

        /*----------------------------------------------------------------------
        |   AP4_Atom::AP4_Atom
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a full atom with a specified type, 64-bit size, version and flags.
        /// </summary>
        protected Atom(uint  type,
                       ulong size,
                       bool  force_64,
                       byte  version,
                       uint  flags)
        {
            _type = type;
            _size32 = 0;
            _size64 = 0;
            _isFull = true;
            _version = version;
            _flags = flags;
            _parent = null;
            SetSize(size, force_64);
        }

        // methods
        public uint GetFlags() { return _flags; }
        public void SetFlags(uint flags) { _flags = flags; }
        public byte GetVersion() { return _version; }
        public void SetVersion(byte version) { _version = version; }
        /// <summary>
        /// The atom type (AP4_Atom::GetType). PORT NOTE: this hides
        /// System.Object.GetType() on atoms, on purpose (methods keep their
        /// upstream names); use `typeof`/`is`/`as` for .NET type tests.
        /// </summary>
        public new uint GetType() { return _type; }
        public void SetType(uint type) { _type = type; }

        /*----------------------------------------------------------------------
        |   AP4_Atom::GetHeaderSize
        +---------------------------------------------------------------------*/
        public virtual uint GetHeaderSize()
        {
            return (_isFull ? Ap4.FULL_ATOM_HEADER_SIZE : Ap4.ATOM_HEADER_SIZE)+(_size32==1?8u:0u);
        }

        public ulong GetSize() { return _size32 == 1?_size64:_size32; }

        /*----------------------------------------------------------------------
        |   AP4_Atom::SetSize
        +---------------------------------------------------------------------*/
        public void SetSize(ulong size, bool force_64 = false)
        {
            if (!force_64) {
                // see if we need to implicitely force 64-bit encoding
                if (_size32 == 1 && _size64 <= 0xFFFFFFFF) {
                    // we have a forced 64-bit encoding
                    force_64 = true;
                }
            }
            if ((size >> 32) == 0 && !force_64) {
                _size32 = (uint)size;
                _size64 = 0;
            } else {
                _size32 = 1;
                _size64 = size;
            }
        }

        public uint  GetSize32() { return _size32; }
        public void  SetSize32(uint size) { _size32 = size; }
        public ulong GetSize64() { return _size64; }
        public void  SetSize64(ulong size) { _size64 = size; }

        /*----------------------------------------------------------------------
        |   AP4_Atom::Write
        +---------------------------------------------------------------------*/
        public virtual Ap4Result Write(ByteStream stream)
        {
            Ap4Result result;

#if AP4_DEBUG
            ulong before;
            stream.Tell(out before);
#endif

            // write the header
            result = WriteHeader(stream);
            if (result.Failed()) return result;

            // write the fields
            result = WriteFields(stream);
            if (result.Failed()) return result;

#if AP4_DEBUG
            ulong after;
            stream.Tell(out after);
            ulong atom_size = GetSize();
            if (after-before != atom_size) {
                Ap4.Debug("ERROR: atom size mismatch (declared size=" + ((int)(uint)atom_size).ToString(CultureInfo.InvariantCulture) +
                          ", actual size=" + ((int)(uint)(after-before)).ToString(CultureInfo.InvariantCulture) + ")\n");
                Atom atom = this;
                while (atom != null) {
                    string name = "[" + Ap4Utils.FormatFourCharsPrintable(atom.GetType()) + "]";
                    Ap4.Debug("       while writing " + name + "\n");
                    atom = atom.GetParent() as Atom;
                }
                Ap4.ASSERT(after-before == atom_size);
            }
#endif

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Atom::WriteHeader
        +---------------------------------------------------------------------*/
        public virtual Ap4Result WriteHeader(ByteStream stream)
        {
            Ap4Result result;

            // write the size
            result = stream.WriteUI32(_size32);
            if (result.Failed()) return result;

            // write the type
            result = stream.WriteUI32(_type);
            if (result.Failed()) return result;

            // handle 64-bit sizes
            if (_size32 == 1) {
                result = stream.WriteUI64(_size64);
                if (result.Failed()) return result;
            }

            // for full atoms, write version and flags
            if (_isFull) {
                result = stream.WriteUI08(_version);
                if (result.Failed()) return result;
                result = stream.WriteUI24(_flags);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        public abstract Ap4Result WriteFields(ByteStream stream);

        /*----------------------------------------------------------------------
        |   AP4_Atom::Inspect
        +---------------------------------------------------------------------*/
        public virtual Ap4Result Inspect(AtomInspector inspector)
        {
            InspectHeader(inspector);
            InspectFields(inspector);
            inspector.EndAtom();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Atom::InspectHeader
        +---------------------------------------------------------------------*/
        public virtual Ap4Result InspectHeader(AtomInspector inspector)
        {
            string name = Ap4Utils.FormatFourCharsPrintable(_type);
            inspector.StartAtom(name,
                                _version,
                                _flags,
                                GetHeaderSize(),
                                GetSize());

            return Ap4Result.SUCCESS;
        }

        public virtual Ap4Result InspectFields(AtomInspector inspector) {
            return Ap4Result.SUCCESS;
        }

        // parent/child relationship methods
        public virtual Ap4Result SetParent(AtomParent parent) {
            _parent = parent;
            return Ap4Result.SUCCESS;
        }
        public virtual AtomParent GetParent() { return _parent; }

        /*----------------------------------------------------------------------
        |   AP4_Atom::Detach
        +---------------------------------------------------------------------*/
        public virtual Ap4Result Detach()
        {
            if (_parent != null) {
                return _parent.RemoveChild(this);
            } else {
                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Atom::Clone
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a clone of the object.
        /// This method returns a clone of the atom, or NULL if
        /// the atom cannot be cloned.
        /// Override this if your want to make an atom cloneable in a more
        /// efficient way than the default implementation.
        /// </summary>
        public virtual Atom Clone()
        {
            Atom clone = null;

            // check the size (refuse to clone atoms that are too large)
            ulong size = GetSize();
            if (size > Ap4.ATOM_MAX_CLONE_SIZE) return null;

            // create a memory byte stream to which we can serialize
            MemoryByteStream mbs = new MemoryByteStream((uint)GetSize());

            // serialize to memory
            if (Write(mbs).Failed()) {
                return null;
            }

            // create the clone from the serialized form
            mbs.Seek(0);
            DefaultAtomFactory atom_factory = new DefaultAtomFactory();
            atom_factory.CreateAtomFromStream(mbs, out clone);

            return clone;
        }

        // members
        protected uint        _type;
        protected uint        _size32;
        protected ulong       _size64; // this is 0 if m_Size is not 1 (encoded in 32-bits)
                                       // and non-zero only if m_Size is 1 (encoded in 64-bits)
        protected bool        _isFull;
        protected byte        _version;
        protected uint        _flags;
        protected AtomParent  _parent;
    }

    /// <summary>
    /// PORT NOTE: not in upstream. Implemented by UuidAtom (Core/UuidAtom.cs) so
    /// that AtomParent.GetChild(byte[] uuid, index) can be compiled without it.
    /// </summary>
    public interface IUuidAtom
    {
        /// <summary>The 16 bytes of the UUID (AP4_UuidAtom::GetUuid).</summary>
        byte[] GetUuid();
    }

    /*----------------------------------------------------------------------
    |   AP4_UnknownAtom
    +---------------------------------------------------------------------*/
    /// <summary>
    /// Class that represents atoms for which there is no specific support.
    /// Instances of this class keep a reference to the stream from which
    /// the atom is parsed, so that it can read the atom's payload when it
    /// is serialized.
    /// If the atom is small, its payload is actually read and stored in
    /// a data buffer, so no reference to the source stream is kept
    /// </summary>
    public class UnknownAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_UnknownAtom::AP4_UnknownAtom
        +---------------------------------------------------------------------*/
        public UnknownAtom(uint       type,
                           ulong      size,
                           ByteStream stream) :
            base(type, size)
        {
            _sourceStream = stream;
            _payload = new DataBuffer();

            if (size <= Ap4.UNKNOWN_ATOM_MAX_LOCAL_PAYLOAD_SIZE &&
                type != AtomType.MDAT) {
                _sourcePosition = 0;
                _sourceStream   = null;
                uint payload_size = (uint)size-GetHeaderSize();
                // PORT NOTE: the result check is not in upstream (it guards
                // against a wrapped-around size that cannot be allocated)
                if (_payload.SetDataSize(payload_size).Succeeded()) {
                    stream.Read(_payload.UseData(), payload_size);
                }
                return;
            }

            // store source stream position
            stream.Tell(out _sourcePosition);

            // clamp to the file size
            ulong file_size;
            if (stream.GetSize(out file_size).Succeeded()) {
                if (_sourcePosition-GetHeaderSize()+size > file_size) {
                    if (_size32 == 1) {
                        // size is encoded as a large size
                        _size64 = file_size-_sourcePosition;
                    } else {
                        Ap4.ASSERT(size <= 0xFFFFFFFF);
                        _size32 = (uint)(file_size-_sourcePosition);
                    }
                }
            }

            // keep a reference to the source stream
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownAtom::AP4_UnknownAtom
        +---------------------------------------------------------------------*/
        public UnknownAtom(uint   type,
                           byte[] payload,
                           uint   payload_size) :
            base(type, (ulong)(Ap4.ATOM_HEADER_SIZE+payload_size), false)
        {
            _sourceStream = null;
            _sourcePosition = 0;
            _payload = new DataBuffer();
            _payload.SetData(payload, payload_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownAtom::AP4_UnknownAtom
        +---------------------------------------------------------------------*/
        public UnknownAtom(UnknownAtom other) :
            base(other._type, (uint)0)
        {
            _sourceStream = other._sourceStream;
            _sourcePosition = other._sourcePosition;
            _payload = new DataBuffer(other._payload);

            _size32 = other._size32;
            _size64 = other._size64;
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // if we don't have a source, write from the buffered payload
            if (_sourceStream == null) {
                return stream.Write(_payload.GetData(), _payload.GetDataSize());
            }

            // remember the source position
            ulong position;
            _sourceStream.Tell(out position);

            // seek into the source at the stored offset
            result = _sourceStream.Seek(_sourcePosition);
            if (result.Failed()) return result;

            // copy the source stream to the output
            ulong payload_size = GetSize()-GetHeaderSize();
            result = _sourceStream.CopyTo(stream, payload_size);
            if (result.Failed()) return result;

            // restore the original stream position
            _sourceStream.Seek(position);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownAtom::Clone
        +---------------------------------------------------------------------*/
        public override Atom Clone()
        {
            return new UnknownAtom(this);
        }

        // members
        private ByteStream  _sourceStream;
        private ulong       _sourcePosition;
        private DataBuffer  _payload;
    }

    /*----------------------------------------------------------------------
    |   AP4_NullTerminatedStringAtom
    +---------------------------------------------------------------------*/
    /// <summary>
    /// Generic Class usd for all atoms that contain a single null-terminated
    /// string.
    /// </summary>
    public class NullTerminatedStringAtom : Atom
    {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_NullTerminatedStringAtom::AP4_NullTerminatedStringAtom
        +---------------------------------------------------------------------*/
        public NullTerminatedStringAtom(uint type, string value) :
            base(type, Ap4.ATOM_HEADER_SIZE)
        {
            _value = value ?? "";
            _size32 += Ap4Utils.StringLength(_value)+1;
        }

        /*----------------------------------------------------------------------
        |   AP4_NullTerminatedStringAtom::AP4_NullTerminatedStringAtom
        +---------------------------------------------------------------------*/
        public NullTerminatedStringAtom(uint       type,
                                        ulong      size,
                                        ByteStream stream) :
            base(type, size)
        {
            _value = "";
            uint str_size = (uint)size - Ap4.ATOM_HEADER_SIZE;
            // PORT NOTE: the upper bound is not in upstream (a .NET array cannot
            // be larger; upstream would attempt a multi-gigabyte allocation)
#if NET6_0_OR_GREATER
            if (str_size != 0 && str_size <= (uint)Array.MaxLength) {
#else
            if (str_size != 0 && str_size <= (uint)Compat.ArrayMaxLength) {
#endif
                byte[] str = new byte[str_size];
                stream.Read(str, str_size);
                str[str_size - 1] = 0; // force null-termination
                _value = Ap4Utils.StringFromCString(str, 0, str_size);
            }
        }

        // accessors
        public string GetValue() { return _value; }

        /*----------------------------------------------------------------------
        |   AP4_NullTerminatedStringAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            if (_size32 > Ap4.ATOM_HEADER_SIZE) {
                byte[] chars = Ap4Utils.StringToCStringBytes(_value);
                uint length = (uint)chars.Length;
                Ap4Result result = stream.Write(chars, length);
                if (result.Failed()) return result;
                result = stream.WriteUI08(0);
                if (result.Failed()) return result;

                // pad with zeros if necessary
                uint padding = _size32-(Ap4.ATOM_HEADER_SIZE+length+1);
                while (padding-- != 0) stream.WriteUI08(0);
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_NullTerminatedStringAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("string value", _value);
            return Ap4Result.SUCCESS;
        }

        // members
        private string _value;
    }

    /*----------------------------------------------------------------------
    |   AP4_AtomListInspector
    +---------------------------------------------------------------------*/
    public class AtomListInspector : IListOperator<Atom>
    {
        public AtomListInspector(AtomInspector inspector) {
            _inspector = inspector;
        }
        public Ap4Result Action(Atom atom) {
            atom.Inspect(_inspector);
            return Ap4Result.SUCCESS;
        }

        private AtomInspector _inspector;
    }

    /*----------------------------------------------------------------------
    |   AP4_AtomListWriter
    +---------------------------------------------------------------------*/
    public class AtomListWriter : IListOperator<Atom>
    {
        public AtomListWriter(ByteStream stream) {
            _stream = stream;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomListWriter::Action
        +---------------------------------------------------------------------*/
        public Ap4Result Action(Atom atom)
        {
            ulong before;
            _stream.Tell(out before);

            atom.Write(_stream);

            ulong after;
            _stream.Tell(out after);

            ulong bytes_written = after-before;
            Ap4.ASSERT(bytes_written <= atom.GetSize());
            if (bytes_written < atom.GetSize()) {
                Ap4.Debug("WARNING: atom serialized to fewer bytes than declared size\n");
                ulong padding = atom.GetSize()-bytes_written;
                if (padding > Ap4.ATOM_LIST_WRITER_MAX_PADDING) {
                    Ap4.Debug("WARNING: padding would be too large\n");
                    return Ap4Result.FAILURE;
                } else {
                    for (uint i=0; i<padding; i++) {
                        _stream.WriteUI08(0);
                    }
                }
            }

            return Ap4Result.SUCCESS;
        }

        private ByteStream _stream;
    }

    /*----------------------------------------------------------------------
    |   AP4_AtomFinder
    +---------------------------------------------------------------------*/
    public class AtomFinder : IListFinder<Atom>
    {
        public AtomFinder(uint type, uint index = 0) {
            _type = type;
            _index = index;
        }
        public Ap4Result Test(Atom atom) {
            if (atom.GetType() == _type) {
                if (_index-- == 0) {
                    return Ap4Result.SUCCESS;
                } else {
                    return Ap4Result.FAILURE;
                }
            } else {
                return Ap4Result.FAILURE;
            }
        }

        private uint _type;
        private uint _index;
    }

    /*----------------------------------------------------------------------
    |   AP4_AtomSizeAdder
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream accumulates into an `AP4_UI64&` given to the
    // constructor. Here the running total is held by the object:
    //     AtomSizeAdder adder = new AtomSizeAdder(size);
    //     _children.Apply(adder);
    //     size = adder.GetSize();
    public class AtomSizeAdder : IListOperator<Atom>
    {
        public AtomSizeAdder(ulong size) { _size = size; }

        public Ap4Result Action(Atom atom) {
            _size += atom.GetSize();
            return Ap4Result.SUCCESS;
        }

        public ulong GetSize() { return _size; }

        private ulong _size;
    }
}
