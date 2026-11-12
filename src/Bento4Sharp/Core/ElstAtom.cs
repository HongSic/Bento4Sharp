// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4ElstAtom.h, Ap4ElstAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_ElstEntry
    +---------------------------------------------------------------------*/
    // PORT NOTE: a class (reference type) so that entries returned by
    // GetEntries() can be modified in place like the elements of the upstream
    // AP4_Array; AddEntry() copies its argument to keep the value semantics.
    public class ElstEntry {
        public ElstEntry(ulong segment_duration = 0, long media_time = 0, ushort media_rate = 1) {
            _segmentDuration = segment_duration;
            _mediaTime = media_time;
            _mediaRate = media_rate;
        }

        public ulong  _segmentDuration;
        public long   _mediaTime;
        public ushort _mediaRate;
    }

    /*----------------------------------------------------------------------
    |   AP4_ElstAtom
    +---------------------------------------------------------------------*/
    public class ElstAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_ElstAtom::Create
        +---------------------------------------------------------------------*/
        public static ElstAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new ElstAtom(size, version, flags, stream);
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_ElstAtom::AP4_ElstAtom
        +---------------------------------------------------------------------*/
        public ElstAtom() :
            base(AtomType.ELST, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_ElstAtom::AP4_ElstAtom
        +---------------------------------------------------------------------*/
        private ElstAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.ELST, size, version, flags)
        {
            // read the number of entries
            uint entry_count;
            stream.ReadUI32(out entry_count);

            // compute bounds
            uint max_entries;
            if (version == 0) {
                max_entries = (size - (Ap4.FULL_ATOM_HEADER_SIZE + 4)) / 12;
            } else {
                max_entries = (size - (Ap4.FULL_ATOM_HEADER_SIZE + 4)) / 20;
            }
            if (entry_count > max_entries) {
                entry_count = max_entries;
            }

            // read the entries
            // PORT NOTE: m_Entries.EnsureCapacity(entry_count) is not ported (pure
            // preallocation; the list grows as the entries are appended).
            for (uint i=0; i < entry_count; i++) {
                ushort media_rate;
                ushort zero;
                if (version == 0) {
                    uint segment_duration;
                    uint media_time;
                    stream.ReadUI32(out segment_duration);
                    stream.ReadUI32(out media_time);
                    stream.ReadUI16(out media_rate);
                    stream.ReadUI16(out zero);
                    _entries.Add(new ElstEntry(segment_duration, (int)media_time, media_rate));
                } else {
                    ulong segment_duration;
                    ulong media_time;
                    stream.ReadUI64(out segment_duration);
                    stream.ReadUI64(out media_time);
                    stream.ReadUI16(out media_rate);
                    stream.ReadUI16(out zero);
                    _entries.Add(new ElstEntry(segment_duration, (long)media_time, media_rate));
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_ElstAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.WriteUI32((uint)_entries.Count);
            if (result.Failed()) return result;
            for (int i=0; i<_entries.Count; i++) {
                if (_version == 0) {
                    result = stream.WriteUI32((uint)_entries[i]._segmentDuration);
                    if (result.Failed()) return result;
                    result = stream.WriteUI32((uint)_entries[i]._mediaTime);
                    if (result.Failed()) return result;
                } else {
                    result = stream.WriteUI64(_entries[i]._segmentDuration);
                    if (result.Failed()) return result;
                    result = stream.WriteUI64((ulong)_entries[i]._mediaTime);
                    if (result.Failed()) return result;
                }
                result = stream.WriteUI16(_entries[i]._mediaRate);
                if (result.Failed()) return result;
                result = stream.WriteUI16(0);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ElstAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("entry_count", (uint)_entries.Count);
            for (int i=0; i<_entries.Count; i++) {
                if (1 == _version) {
                    inspector.AddField("entry/segment duration", (ulong)_entries[i]._segmentDuration);
                    inspector.AddField("entry/media time", (long)_entries[i]._mediaTime);
                } else {
                    inspector.AddField("entry/segment duration", (uint)_entries[i]._segmentDuration);
                    inspector.AddField("entry/media time", (int)_entries[i]._mediaTime);
                }
                inspector.AddField("entry/media rate", (ushort)_entries[i]._mediaRate);
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public List<ElstEntry> GetEntries() {
            return _entries;
        }

        /*----------------------------------------------------------------------
        |   AP4_ElstAtom::AddEntry
        +---------------------------------------------------------------------*/
        public Ap4Result AddEntry(ElstEntry entry)
        {
            // check if this requires 64-bit fields
            if (entry._segmentDuration > 0xFFFFFFFFUL) {
                _version = 1;
            }
            if (entry._mediaTime > 0 && (ulong)entry._mediaTime > 0xFFFFFFFFUL) {
                _version = 1;
            }

            // add the entry
            _entries.Add(new ElstEntry(entry._segmentDuration, entry._mediaTime, entry._mediaRate));

            // recompute the atom size
            SetSize(Ap4.FULL_ATOM_HEADER_SIZE+4+(uint)_entries.Count*(_version==0?12u:20u));

            return Ap4Result.SUCCESS;
        }

        // members
        private List<ElstEntry> _entries = new List<ElstEntry>();
    }
}
