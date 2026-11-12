// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4HdlrAtom.h, Ap4HdlrAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint HANDLER_TYPE_SOUN = 0x736f756e; // 'soun'
        public const uint HANDLER_TYPE_VIDE = 0x76696465; // 'vide'
        public const uint HANDLER_TYPE_HINT = 0x68696e74; // 'hint'
        public const uint HANDLER_TYPE_MDIR = 0x6d646972; // 'mdir'
        public const uint HANDLER_TYPE_TEXT = 0x74657874; // 'text'
        public const uint HANDLER_TYPE_TX3G = 0x74783367; // 'tx3g'
        public const uint HANDLER_TYPE_JPEG = 0x6a706567; // 'jpeg'
        public const uint HANDLER_TYPE_ODSM = 0x6f64736d; // 'odsm'
        public const uint HANDLER_TYPE_SDSM = 0x7364736d; // 'sdsm'
        public const uint HANDLER_TYPE_SUBT = 0x73756274; // 'subt'
        public const uint HANDLER_TYPE_SBTL = 0x7362746c; // 'sbtl'
    }

    /*----------------------------------------------------------------------
    |   AP4_HdlrAtom
    +---------------------------------------------------------------------*/
    public class HdlrAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_HdlrAtom::Create
        +---------------------------------------------------------------------*/
        public static HdlrAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new HdlrAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_HdlrAtom::AP4_HdlrAtom
        +---------------------------------------------------------------------*/
        public HdlrAtom(uint hdlr_type, string hdlr_name) :
            base(AtomType.HDLR, Ap4.FULL_ATOM_HEADER_SIZE, 0, 0)
        {
            _predefined = 0;
            _handlerType = hdlr_type;
            _handlerName = hdlr_name ?? "";
            _quickTimeMode = false;

            _size32 += 20+Ap4Utils.StringLength(_handlerName)+1;
            _reserved[0] = _reserved[1] = _reserved[2] = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_HdlrAtom::AP4_HdlrAtom
        +---------------------------------------------------------------------*/
        private HdlrAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.HDLR, size, version, flags)
        {
            _quickTimeMode = false;
            _handlerName = "";

            stream.ReadUI32(out _predefined);
            stream.ReadUI32(out _handlerType);
            stream.ReadUI32(out _reserved[0]);
            stream.ReadUI32(out _reserved[1]);
            stream.ReadUI32(out _reserved[2]);

            // read the name unless it is empty
            if (size < Ap4.FULL_ATOM_HEADER_SIZE+20) return;
            uint name_size = size-(Ap4.FULL_ATOM_HEADER_SIZE+20);
            byte[] name = new byte[name_size+1];
            stream.Read(name, name_size);
            name[name_size] = 0; // force a null termination
            // handle a special case: the Quicktime files have a pascal
            // string here, but ISO MP4 files have a C string.
            // we try to detect a pascal encoding and correct it.
            if (name[0] == (byte)(name_size-1)) {
                _handlerName = Ap4Utils.StringFromCString(name, 1, name_size);
                _quickTimeMode = true;
            } else {
                _handlerName = Ap4Utils.StringFromCString(name, 0, name_size+1);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_HdlrAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // write the data
            result = stream.WriteUI32(_predefined);
            if (result.Failed()) return result;
            result = stream.WriteUI32(_handlerType);
            if (result.Failed()) return result;
            result = stream.WriteUI32(_reserved[0]);
            if (result.Failed()) return result;
            result = stream.WriteUI32(_reserved[1]);
            if (result.Failed()) return result;
            result = stream.WriteUI32(_reserved[2]);
            if (result.Failed()) return result;

            // sanity check
            if (_size32 < Ap4.FULL_ATOM_HEADER_SIZE + 20) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // PORT NOTE: the 8-bit name size (and its wrap-around for names of
            // 255 bytes or more) is upstream behaviour.
            byte[] handler_name = Ap4Utils.StringToCStringBytes(_handlerName);
            byte name_size = (byte)Ap4Utils.StringLength(_handlerName);
            if (_quickTimeMode) {
                name_size += 1;
                if (Ap4.FULL_ATOM_HEADER_SIZE + 20 + name_size > _size32) {
                    name_size = (byte)(_size32 - (Ap4.FULL_ATOM_HEADER_SIZE + 20));
                }
                if (name_size != 0) {
                    result = stream.WriteUI08((byte)(name_size - 1));
                    if (result.Failed()) return result;
                    result = stream.Write(handler_name, (uint)(name_size - 1));
                    if (result.Failed()) return result;
                }
            } else {
                if (Ap4.FULL_ATOM_HEADER_SIZE + 20 + name_size > _size32) {
                    name_size = (byte)(_size32 - (Ap4.FULL_ATOM_HEADER_SIZE + 20));
                }
                if (name_size != 0) {
                    result = stream.Write(handler_name, name_size);
                    if (result.Failed()) return result;
                }
            }

            // pad with zeros if necessary
            if (_size32 > Ap4.FULL_ATOM_HEADER_SIZE + 20 + name_size) {
                uint padding = _size32 - (Ap4.FULL_ATOM_HEADER_SIZE + 20 + name_size);
                while (padding-- != 0) stream.WriteUI08(0);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_HdlrAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string type = Ap4Utils.FormatFourChars(_handlerType);
            inspector.AddField("handler_type", type);
            inspector.AddField("handler_name", _handlerName);

            return Ap4Result.SUCCESS;
        }

        public uint   GetHandlerType() { return _handlerType; }
        public string GetHandlerName() { return _handlerName; }

        // members
        private uint   _predefined; // even if this is supposed to be predefined == 0,
                                    // we allow keeping a non-zero value for compatitibilty
                                    // with QuickTime
        private uint   _handlerType;
        private uint[] _reserved = new uint[3];
        private string _handlerName;
        private bool   _quickTimeMode; // set to true when in QuickTime-compatible mode
    }
}
