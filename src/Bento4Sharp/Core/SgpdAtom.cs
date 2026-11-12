// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SgpdAtom.h, Ap4SgpdAtom.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SgpdAtom
    +---------------------------------------------------------------------*/
    public class SgpdAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_SgpdAtom::Create
        +---------------------------------------------------------------------*/
        public static SgpdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new SgpdAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_SgpdAtom::AP4_SgpdAtom
        +---------------------------------------------------------------------*/
        private SgpdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.SGPD, size, version, flags)
        {
            _groupingType = 0;
            _defaultLength = 0;

            uint bytes_available = size-Ap4.FULL_ATOM_HEADER_SIZE;
            stream.ReadUI32(out _groupingType);
            bytes_available -= 4;

            if (version >= 1) {
                stream.ReadUI32(out _defaultLength);
                bytes_available -= 4;
            }

            // read the number of entries
            uint entry_count = 0;
            Ap4Result result = stream.ReadUI32(out entry_count);
            if (result.Failed()) return;
            bytes_available -= 4;

            // read all entries
            for (uint i=0; i<entry_count; i++) {
                uint description_length = _defaultLength;
                if (_version == 0) {
                    // entry size unknown, read the whole thing
                    description_length = bytes_available;
                } else {
                    if (_defaultLength == 0) {
                        // PORT NOTE: upstream bug kept on purpose:
                        //   description_length = stream.ReadUI32(description_length);
                        // assigns the AP4_Result of the read (0 on success) to the
                        // length, so the entry is always read as empty.
                        Ap4Result read_result = stream.ReadUI32(out description_length);
                        description_length = unchecked((uint)(int)read_result);
                    }
                }
                if (description_length <= bytes_available) {
                    DataBuffer payload = new DataBuffer();
                    if (description_length != 0) {
                        // PORT NOTE: upstream ignores the result of SetDataSize;
                        // the port skips the read when the buffer could not be
                        // allocated (bytes_available wrapped around on a truncated
                        // atom), instead of writing past the buffer.
                        if (payload.SetDataSize(description_length).Succeeded()) {
                            stream.Read(payload.UseData(), description_length);
                        }
                    }
                    _entries.Add(payload);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SgpdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.WriteUI32(_groupingType);
            if (result.Failed()) return result;

            if (_version >= 1) {
                result = stream.WriteUI32(_defaultLength);
                if (result.Failed()) return result;
            }

            // write the children
            result = stream.WriteUI32((uint)_entries.Count);
            if (result.Failed()) return result;

            foreach (DataBuffer entry in _entries) {
                if (_version >= 1) {
                    if (_defaultLength == 0) {
                        stream.WriteUI32((uint)entry.GetDataSize());
                    }
                }
                result = stream.Write(entry.GetData(), entry.GetDataSize());
                if (result.Failed()) {
                    return result;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SgpdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string fourcc = Ap4Utils.FormatFourChars(_groupingType);
            inspector.AddField("grouping_type", fourcc);
            if (_version >= 1) {
                inspector.AddField("default_length", _defaultLength);
            }
            inspector.AddField("entry_count", (uint)_entries.Count);

            // inspect entries
            inspector.StartArray("entries");
            foreach (DataBuffer entry in _entries) {
                inspector.AddField(null, entry.GetData(), entry.GetDataSize());
            }
            inspector.EndArray();

            return Ap4Result.SUCCESS;
        }

        // members
        private uint             _groupingType;
        private uint             _defaultLength;
        private List<DataBuffer> _entries = new List<DataBuffer>();
    }
}
