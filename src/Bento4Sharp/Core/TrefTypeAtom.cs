// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TrefTypeAtom.h, Ap4TrefTypeAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_TrefTypeAtom
    +---------------------------------------------------------------------*/
    public class TrefTypeAtom : Atom
    {
        // class methods
        public static TrefTypeAtom Create(uint       type,
                                           uint       size,
                                           ByteStream stream) {
            return new TrefTypeAtom(type, size, stream);
        }

        // contructor
        /*----------------------------------------------------------------------
        |   AP4_TrefTypeAtom::AP4_TrefTypeAtom
        +---------------------------------------------------------------------*/
        public TrefTypeAtom(uint type) :
            base(type, Ap4.ATOM_HEADER_SIZE)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_TrefTypeAtom::AP4_TrefTypeAtom
        +---------------------------------------------------------------------*/
        private TrefTypeAtom(uint       type,
                             uint       size,
                             ByteStream stream) :
            base(type, size)
        {
            uint data_size = size - 8; // size and atom type

            // read the track ids
            while (data_size >= 4) {
                uint track_id;
                stream.ReadUI32(out track_id);
                _trackIds.Add(track_id);
                data_size -= 4;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrefTypeAtom::AddTrackId
        +---------------------------------------------------------------------*/
        public Ap4Result AddTrackId(uint track_id)
        {
            // AP4_Result result = m_TrackIds.Append(track_id);
            _trackIds.Add(track_id);
            Ap4Result result = Ap4Result.SUCCESS;
            if (result.Succeeded()) {
                _size32 += 4;
            }
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrefTypeAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result = Ap4Result.SUCCESS;

            uint track_id_count = (uint)_trackIds.Count;
            for (uint i=0; i<track_id_count; i++) {
                result = stream.WriteUI32(_trackIds[(int)i]);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrefTypeAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("track_id_count", (uint)_trackIds.Count);
            for (int i=0; i<_trackIds.Count; i++) {
                inspector.AddField("track id ", _trackIds[i]);
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public List<uint> GetTrackIds() { return _trackIds; }

        // members
        private List<uint> _trackIds = new List<uint>();
    }
}
