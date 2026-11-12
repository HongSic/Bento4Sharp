// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SchmAtom.h, Ap4SchmAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SchmAtom
    +---------------------------------------------------------------------*/
    public class SchmAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_SchmAtom::Create
        +---------------------------------------------------------------------*/
        public static SchmAtom Create(uint        size,
                                       List<uint>  context,
                                       ByteStream  stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE+6) return null;

            // check the context to see if this is a short form atom or not
            bool short_form = false;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE+8) short_form = true;
            if (context != null) {
                uint context_depth = (uint)context.Count;
                if (context_depth >= 2 &&
                    context[(int)(context_depth-2)] == 0x6d726c6e /* 'mrln' */) {
                    short_form = true;
                }
            }

            return new SchmAtom(size, version, flags, short_form, stream);
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_SchmAtom::AP4_SchmAtom
        +---------------------------------------------------------------------*/
        public SchmAtom(uint    scheme_type,
                        uint    scheme_version,
                        string  scheme_uri = null,
                        bool    short_form = false) :
            base(AtomType.SCHM, Ap4.FULL_ATOM_HEADER_SIZE+4+(short_form?2u:4u), 0, 0)
        {
            _atomHasShortForm = short_form;
            _schemeType       = scheme_type;
            _schemeVersion    = scheme_version;
            if (scheme_uri != null) {
                _schemeUri = scheme_uri;
                _flags = 1;
                _size32 += Ap4Utils.StringLength(_schemeUri)+1;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SchmAtom::AP4_SchmAtom
        +---------------------------------------------------------------------*/
        private SchmAtom(uint       size,
                         byte       version,
                         uint       flags,
                         bool       short_form,
                         ByteStream stream) :
            base(AtomType.SCHM, size, version, flags)
        {
            _atomHasShortForm = short_form;
            stream.ReadUI32(out _schemeType);
            if (short_form) {
                ushort short_version;
                stream.ReadUI16(out short_version);
                _schemeVersion = short_version;
            } else {
                stream.ReadUI32(out _schemeVersion);
            }
            if ((_flags & 1) != 0) {
                int str_size = unchecked((int)(size-(Ap4.FULL_ATOM_HEADER_SIZE+8)));
                if (str_size > 0) {
                    byte[] str = new byte[str_size];
                    stream.Read(str, (uint)str_size);
                    str[str_size-1] = 0; // force null-termination
                    _schemeUri = Ap4Utils.StringFromCString(str, 0, (uint)str_size);
                }
            }
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_SchmAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // scheme type
            result = stream.WriteUI32(_schemeType);
            if (result.Failed()) return result;

            // scheme version
            if (_atomHasShortForm) {
                result = stream.WriteUI16((ushort)_schemeVersion);
                if (result.Failed()) return result;
            } else {
                result = stream.WriteUI32(_schemeVersion);
                if (result.Failed()) return result;
            }

            // uri if needed
            if ((_flags & 1) != 0) {
                // PORT NOTE: upstream writes the chars and the terminator in one Write call
                result = stream.WriteString(_schemeUri);
                if (result.Failed()) return result;
                result = stream.WriteUI08(0);
                if (result.Failed()) return result;

                // pad with zeros if necessary
                uint fields_size = 4+(_atomHasShortForm?2u:4u);
                uint padding = _size32-(Ap4.FULL_ATOM_HEADER_SIZE+fields_size+Ap4Utils.StringLength(_schemeUri)+1);
                while (padding-- != 0) stream.WriteUI08(0);
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_SchmAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string st = Ap4Utils.FormatFourChars(_schemeType);
            inspector.AddField("scheme_type", st);
            if (_atomHasShortForm) {
                inspector.AddField("scheme_version (short)", _schemeVersion);
            } else {
                inspector.AddField("scheme_version", _schemeVersion);
            }
            if ((_flags & 1) != 0) {
                inspector.AddField("scheme_uri", _schemeUri);
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint   GetSchemeType()    { return _schemeType;    }
        public uint   GetSchemeVersion() { return _schemeVersion; }
        public string GetSchemeUri()     { return _schemeUri;     }

        // members
        private bool   _atomHasShortForm; // for versions of this where the version
                                          // field is only 16 bits
        private uint   _schemeType;
        private uint   _schemeVersion;
        private string _schemeUri = "";
    }
}
