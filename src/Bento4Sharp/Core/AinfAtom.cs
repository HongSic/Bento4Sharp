// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AinfAtom.h, Ap4AinfAtom.cpp
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_AinfAtom
    +---------------------------------------------------------------------*/
    public class AinfAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const uint AP4_AINF_MAX_OTHER_SIZE = 16*1024*1024; // 16MB, for sanity

        // class methods
        /*----------------------------------------------------------------------
        |   AP4_AinfAtom::Create
        +---------------------------------------------------------------------*/
        public static AinfAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new AinfAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_AinfAtom::AP4_AinfAtom
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream leaves m_ProfileVersion uninitialized here; it is 0 in the port.
        public AinfAtom() :
            base(AtomType.AINF, Ap4.FULL_ATOM_HEADER_SIZE+4+1, 0, 0)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_AinfAtom::AP4_AinfAtom
        +---------------------------------------------------------------------*/
        private AinfAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.AINF, size, version, flags)
        {
            stream.ReadUI32(out _profileVersion);
            if (size > Ap4.FULL_ATOM_HEADER_SIZE+4 && size < AP4_AINF_MAX_OTHER_SIZE) {
                DataBuffer payload = new DataBuffer();
                uint payload_size = size-Ap4.FULL_ATOM_HEADER_SIZE-4;
                payload.SetDataSize(payload_size+1);
                payload.UseData()[payload_size] = 0;
                stream.Read(payload.UseData(), payload_size);
                _APID = Ap4Utils.StringFromCString(payload.GetData(), 0, payload_size+1);
                if (payload_size > Ap4Utils.StringLength(_APID)+1) {
                    uint other_boxes_size = payload_size-Ap4Utils.StringLength(_APID)-1;
                    _otherBoxes.SetDataSize(other_boxes_size);
                    // PORT NOTE: as upstream, the bytes are copied from the START of the
                    // payload (not from after the APID string).
                    Array.Copy(payload.GetData(), 0, _otherBoxes.UseData(), 0, (int)other_boxes_size);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_AinfAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.WriteUI32(_profileVersion);
            if (result.Failed()) return result;

            // stop early if we have an atom with no APID and a declared size
            // that doesn't leave enough room for the NULL terminator
            if (GetSize() <= Ap4.FULL_ATOM_HEADER_SIZE+4) return Ap4Result.SUCCESS;

            // stream.Write(m_APID.GetChars(), m_APID.GetLength()+1)
            result = stream.WriteString(_APID);
            if (result.Failed()) return result;
            result = stream.WriteUI08(0);
            if (result.Failed()) return result;
            if (_otherBoxes.GetDataSize() != 0) {
                stream.Write(_otherBoxes.GetData(), _otherBoxes.GetDataSize());
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AinfAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string v = Ap4Utils.FormatFourChars(_profileVersion);
            inspector.AddField("profile_version", v);
            inspector.AddField("APID", _APID);
            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint   GetProfileVersion()       { return _profileVersion; }
        public void   SetProfileVersion(uint v) { _profileVersion = v;    }
        public string GetAPID()                 { return _APID;           }
        public void   SetAPID(string apid)     { _APID = apid ?? "";     }

        // members
        private uint       _profileVersion;
        private string     _APID = "";
        private DataBuffer _otherBoxes = new DataBuffer();
    }
}
