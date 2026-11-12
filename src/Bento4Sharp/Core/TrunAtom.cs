// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TrunAtom.h, Ap4TrunAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint TRUN_FLAG_DATA_OFFSET_PRESENT                    = 0x0001;
        public const uint TRUN_FLAG_FIRST_SAMPLE_FLAGS_PRESENT             = 0x0004;
        public const uint TRUN_FLAG_SAMPLE_DURATION_PRESENT                = 0x0100;
        public const uint TRUN_FLAG_SAMPLE_SIZE_PRESENT                    = 0x0200;
        public const uint TRUN_FLAG_SAMPLE_FLAGS_PRESENT                   = 0x0400;
        public const uint TRUN_FLAG_SAMPLE_COMPOSITION_TIME_OFFSET_PRESENT = 0x0800;
    }

    /*----------------------------------------------------------------------
    |   AP4_TrunAtom
    +---------------------------------------------------------------------*/
    public class TrunAtom : Atom
    {
        // constants
        private const uint AP4_TRUN_MAX_SAMPLE_COUNT = 0xFFFFFF;

        // types
        // PORT NOTE: a class (not a struct) so that entries held in a List<T> can
        // be modified in place, as upstream does through UseEntries()[i].
        // SetEntries() copies the fields to keep upstream's value semantics.
        public class Entry {
            public Entry()
            {
                sample_duration = 0; sample_size = 0; sample_flags = 0; sample_composition_time_offset = 0;
            }
            public uint sample_duration;
            public uint sample_size;
            public uint sample_flags;
            public uint sample_composition_time_offset;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrunAtom::Create
        +---------------------------------------------------------------------*/
        public static TrunAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new TrunAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_TrunAtom::ComputeRecordFieldsCount
        +---------------------------------------------------------------------*/
        public static uint ComputeRecordFieldsCount(uint flags)
        {
            // count the number of bits set to 1 in the second byte of the flags
            uint count = 0;
            for (int i=0; i<8; i++) {
                if ((flags & (1u<<(i+8))) != 0) ++count;
            }
            return count;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrunAtom::ComputeOptionalFieldsCount
        +---------------------------------------------------------------------*/
        public static uint ComputeOptionalFieldsCount(uint flags)
        {
            // count the number of bits set to 1 in the LSB of the flags
            uint count = 0;
            for (int i=0; i<8; i++) {
                if ((flags & (1u<<i)) != 0) ++count;
            }
            return count;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrunAtom::AP4_TrunAtom
        +---------------------------------------------------------------------*/
        public TrunAtom(uint flags,
                        int  data_offset,
                        uint first_sample_flags) :
            base(AtomType.TRUN, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, flags)
        {
            _dataOffset = data_offset;
            _firstSampleFlags = first_sample_flags;
            _size32 += 4 * ComputeOptionalFieldsCount(flags);
        }

        /*----------------------------------------------------------------------
        |   AP4_TrunAtom::AP4_TrunAtom
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream leaves m_DataOffset and m_FirstSampleFlags
        // uninitialized when they are not present; they are 0 here.
        private TrunAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.TRUN, size, version, flags)
        {
            if (size < Ap4.FULL_ATOM_HEADER_SIZE + 4) {
                return;
            }
            uint sample_count = 0;
            stream.ReadUI32(out sample_count);
            uint bytes_left = size - Ap4.FULL_ATOM_HEADER_SIZE - 4;

            // read optional fields
            int optional_fields_count = (int)ComputeOptionalFieldsCount(flags);
            if ((flags & Ap4.TRUN_FLAG_DATA_OFFSET_PRESENT) != 0) {
                uint offset = 0;
                if (bytes_left < 4 || stream.ReadUI32(out offset).Failed()) {
                    return;
                }
                _dataOffset = (int)offset;
                if (optional_fields_count == 0) {
                    return;
                }
                --optional_fields_count;
                bytes_left -= 4;
            }
            if ((flags & Ap4.TRUN_FLAG_FIRST_SAMPLE_FLAGS_PRESENT) != 0) {
                if (bytes_left < 4 || stream.ReadUI32(out _firstSampleFlags).Failed()) {
                    return;
                }
                if (optional_fields_count == 0) {
                    return;
                }
                --optional_fields_count;
                bytes_left -= 4;
            }

            // discard unknown optional fields
            for (int i=0; i<optional_fields_count; i++) {
                uint discard;
                if (bytes_left < 4 || stream.ReadUI32(out discard).Failed()) {
                    return;
                }
                bytes_left -= 4;
            }

            int record_fields_count = (int)ComputeRecordFieldsCount(flags);
            if (record_fields_count != 0) {
                if ((bytes_left / (uint)(record_fields_count*4)) < sample_count) {
                    // not enough data for all samples, the format is invalid
                    return;
                }
            } else {
                if (sample_count > AP4_TRUN_MAX_SAMPLE_COUNT) {
                    return;
                }
            }

            // m_Entries.SetItemCount(sample_count)
            _entries.Capacity = (int)sample_count;
            for (uint i=0; i<sample_count; i++) {
                _entries.Add(new Entry());
            }

            // PORT NOTE: as upstream, record_fields_count is not reset for each
            // sample: it keeps being decremented, so unknown record fields are
            // only skipped for the first sample.
            for (int i=0; (uint)i<sample_count; i++) {
                if ((flags & Ap4.TRUN_FLAG_SAMPLE_DURATION_PRESENT) != 0) {
                    if (bytes_left < 4 || stream.ReadUI32(out _entries[i].sample_duration).Failed()) {
                        return;
                    }
                    --record_fields_count;
                    bytes_left -= 4;
                }
                if ((flags & Ap4.TRUN_FLAG_SAMPLE_SIZE_PRESENT) != 0) {
                    if (bytes_left < 4 || stream.ReadUI32(out _entries[i].sample_size).Failed()) {
                        return;
                    }
                    --record_fields_count;
                    bytes_left -= 4;
                }
                if ((flags & Ap4.TRUN_FLAG_SAMPLE_FLAGS_PRESENT) != 0) {
                    if (bytes_left < 4 || stream.ReadUI32(out _entries[i].sample_flags).Failed()) {
                        return;
                    }
                    --record_fields_count;
                    bytes_left -= 4;
                }
                if ((flags & Ap4.TRUN_FLAG_SAMPLE_COMPOSITION_TIME_OFFSET_PRESENT) != 0) {
                    if (bytes_left < 4 || stream.ReadUI32(out _entries[i].sample_composition_time_offset).Failed()) {
                        return;
                    }
                    --record_fields_count;
                    bytes_left -= 4;
                }

                // skip unknown fields
                for (int j=0;j<record_fields_count; j++) {
                    uint discard;
                    if (bytes_left < 4 || stream.ReadUI32(out discard).Failed()) {
                        return;
                    }
                    bytes_left -= 4;
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrunAtom::UpdateFlags
        +---------------------------------------------------------------------*/
        public void UpdateFlags(uint flags)
        {
            _flags  = flags;
            _size32 = Ap4.FULL_ATOM_HEADER_SIZE + 4 + 4 * ComputeOptionalFieldsCount(flags);
        }

        /*----------------------------------------------------------------------
        |   AP4_TrunAtom::SetEntries
        +---------------------------------------------------------------------*/
        public Ap4Result SetEntries(List<Entry> entries)
        {
            // resize the entry array
            // m_Entries.SetItemCount(entries.ItemCount())
            if (entries.Count < _entries.Count) {
                _entries.RemoveRange(entries.Count, _entries.Count-entries.Count);
            } else {
                while (_entries.Count < entries.Count) {
                    _entries.Add(new Entry());
                }
            }

            // copy entries
            int entry_count = entries.Count;
            for (int i=0; i<entry_count; i++) {
                Entry copy = new Entry();
                copy.sample_duration                = entries[i].sample_duration;
                copy.sample_size                    = entries[i].sample_size;
                copy.sample_flags                   = entries[i].sample_flags;
                copy.sample_composition_time_offset = entries[i].sample_composition_time_offset;
                _entries[i] = copy;
            }

            // update the atom size
            uint record_fields_count = ComputeRecordFieldsCount(_flags);
            _size32 += (uint)entries.Count*record_fields_count*4;

            // notify the parent
            if (_parent != null) _parent.OnChildChanged(this);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrunAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.WriteUI32((uint)_entries.Count);
            if (result.Failed()) return result;
            if ((_flags & Ap4.TRUN_FLAG_DATA_OFFSET_PRESENT) != 0) {
                result = stream.WriteUI32((uint)_dataOffset);
                if (result.Failed()) return result;
            }
            if ((_flags & Ap4.TRUN_FLAG_FIRST_SAMPLE_FLAGS_PRESENT) != 0) {
                result = stream.WriteUI32(_firstSampleFlags);
                if (result.Failed()) return result;
            }
            int sample_count = _entries.Count;
            for (int i=0; i<sample_count; i++) {
                if ((_flags & Ap4.TRUN_FLAG_SAMPLE_DURATION_PRESENT) != 0) {
                    result = stream.WriteUI32(_entries[i].sample_duration);
                    if (result.Failed()) return result;
                }
                if ((_flags & Ap4.TRUN_FLAG_SAMPLE_SIZE_PRESENT) != 0) {
                    result = stream.WriteUI32(_entries[i].sample_size);
                    if (result.Failed()) return result;
                }
                if ((_flags & Ap4.TRUN_FLAG_SAMPLE_FLAGS_PRESENT) != 0) {
                    result = stream.WriteUI32(_entries[i].sample_flags);
                    if (result.Failed()) return result;
                }
                if ((_flags & Ap4.TRUN_FLAG_SAMPLE_COMPOSITION_TIME_OFFSET_PRESENT) != 0) {
                    // PORT NOTE: as upstream, the result of this write is not
                    // assigned to `result`.
                    stream.WriteUI32(_entries[i].sample_composition_time_offset);
                    if (result.Failed()) return result;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrunAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("sample count", (uint)_entries.Count);
            if ((_flags & Ap4.TRUN_FLAG_DATA_OFFSET_PRESENT) != 0) {
                // AP4_SI32 converted to AP4_UI64 (sign extended), as upstream
                inspector.AddField("data offset", unchecked((ulong)(long)_dataOffset));
            }
            if ((_flags & Ap4.TRUN_FLAG_FIRST_SAMPLE_FLAGS_PRESENT) != 0) {
                inspector.AddField("first sample flags", _firstSampleFlags, AtomInspector.FormatHint.HINT_HEX);
            }
            if (inspector.GetVerbosity() > 0) {
                inspector.StartArray("entries");
                int sample_count = _entries.Count;
                for (int i = 0; i < sample_count; i++) {
                    inspector.StartObject(null, 0, true);
                    if ((_flags & Ap4.TRUN_FLAG_SAMPLE_DURATION_PRESENT) != 0) {
                        inspector.AddField(inspector.GetVerbosity() >= 2 ? "sample_duration" : "d",
                                           _entries[i].sample_duration);
                    }
                    if ((_flags & Ap4.TRUN_FLAG_SAMPLE_SIZE_PRESENT) != 0) {
                        inspector.AddField(inspector.GetVerbosity() >= 2 ? "sample_size" : "s",
                                           _entries[i].sample_size);
                    }
                    if ((_flags & Ap4.TRUN_FLAG_SAMPLE_FLAGS_PRESENT) != 0) {
                        inspector.AddField(inspector.GetVerbosity() >= 2 ? "sample_flags" : "f",
                                           _entries[i].sample_flags);
                    }
                    if ((_flags & Ap4.TRUN_FLAG_SAMPLE_COMPOSITION_TIME_OFFSET_PRESENT) != 0) {
                        inspector.AddField(inspector.GetVerbosity() >= 2 ? "sample_composition_time_offset" : "c",
                                           _version == 0 ?
                                           (ulong)_entries[i].sample_composition_time_offset :
                                           unchecked((ulong)(long)(int)_entries[i].sample_composition_time_offset));
                    }
                    inspector.EndObject();
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public int         GetDataOffset()                 { return _dataOffset;        }
        public void        SetDataOffset(int offset)       { _dataOffset = offset;      }
        public uint        GetFirstSampleFlags()           { return _firstSampleFlags;  }
        public void        SetFirstSampleFlags(uint flags) { _firstSampleFlags = flags; }
        public List<Entry> GetEntries()                    { return _entries;           }
        public List<Entry> UseEntries()                    { return _entries;           }

        // members
        private int         _dataOffset;
        private uint        _firstSampleFlags;
        private List<Entry> _entries = new List<Entry>();
    }
}
