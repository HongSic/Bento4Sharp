// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TfraAtom.h, Ap4TfraAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_TfraAtom
    +---------------------------------------------------------------------*/
    public class TfraAtom : Atom
    {
        // types
        // PORT NOTE: a class (not a struct) so that entries held in a List<T> can
        // be modified in place, as upstream does through AP4_Array<T>::operator[].
        public class Entry {
            public Entry()
            {
                _time = 0; _moofOffset = 0; _trafNumber = 0; _trunNumber = 0; _sampleNumber = 0;
            }
            public Entry(ulong time,
                         ulong moof_offset,
                         uint  traf_number,
                         uint  trun_number,
                         uint  sample_number)
            {
                _time = time; _moofOffset = moof_offset; _trafNumber = traf_number; _trunNumber = trun_number; _sampleNumber = sample_number;
            }
            public ulong _time;
            public ulong _moofOffset;
            public uint  _trafNumber;
            public uint  _trunNumber;
            public uint  _sampleNumber;
        }

        /*----------------------------------------------------------------------
        |   AP4_TfraAtom::Create
        +---------------------------------------------------------------------*/
        public static TfraAtom Create(uint size, ByteStream stream)
        {
            byte version = 0;
            uint flags   = 0;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            Ap4Result result = ReadFullHeader(stream, out version, out flags);
            if (result.Failed()) return null;
            if (version > 1) return null;
            return new TfraAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_TfraAtom::AP4_TfraAtom
        +---------------------------------------------------------------------*/
        public TfraAtom(uint track_id,
                        byte length_size_of_traf_number   = 0,
                        byte length_size_of_trun_number   = 0,
                        byte length_size_of_sample_number = 0) :
            base(AtomType.TFRA, Ap4.FULL_ATOM_HEADER_SIZE+4+4+4, 0, 0)
        {
            _trackId = track_id;
            _lengthSizeOfTrafNumber = length_size_of_traf_number;
            _lengthSizeOfTrunNumber = length_size_of_trun_number;
            _lengthSizeOfSampleNumber = length_size_of_sample_number;
        }

        /*----------------------------------------------------------------------
        |   AP4_TfraAtom::AP4_TfraAtom
        +---------------------------------------------------------------------*/
        private TfraAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.TFRA, size, version, flags)
        {
            stream.ReadUI32(out _trackId);
            uint fields = 0;
            stream.ReadUI32(out fields);
            _lengthSizeOfTrafNumber   = (byte)((fields>>4)&3);
            _lengthSizeOfTrunNumber   = (byte)((fields>>2)&3);
            _lengthSizeOfSampleNumber = (byte)((fields   )&3);
            uint entry_count = 0;
            stream.ReadUI32(out entry_count);

            // Compute the size of each entry
            uint entry_size;
            if (version == 1) {
                entry_size = 16;
            } else {
                entry_size = 8;
            }
            switch (_lengthSizeOfTrafNumber) {
                case 0:
                    entry_size += 1;
                    break;
                case 1:
                    entry_size += 2;
                    break;
                case 2:
                    entry_size += 3;
                    break;
                case 3:
                    entry_size += 4;
                    break;
            }
            switch (_lengthSizeOfTrunNumber) {
                case 0:
                    entry_size += 1;
                    break;
                case 1:
                    entry_size += 2;
                    break;
                case 2:
                    entry_size += 3;
                    break;
                case 3:
                    entry_size += 4;
                    break;
            }
            switch (_lengthSizeOfSampleNumber) {
                case 0:
                    entry_size += 1;
                    break;
                case 1:
                    entry_size += 2;
                    break;
                case 2:
                    entry_size += 3;
                    break;
                case 3:
                    entry_size += 4;
                    break;
            }

            // Check that it all fits
            // PORT NOTE: as upstream, `size - 24` is an unsigned subtraction that
            // wraps around for an atom shorter than 24 bytes.
            if (entry_count <= 0 || ((size - (Ap4.FULL_ATOM_HEADER_SIZE+4+4+4)) / entry_count) < entry_size) {
                return;
            }

            // Read the entries
            // m_Entries.SetItemCount(entry_count)
            for (uint i=0; i<entry_count; i++) {
                _entries.Add(new Entry());
            }
            for (int i=0; (uint)i<entry_count; i++) {
                if (version == 1) {
                    stream.ReadUI64(out _entries[i]._time);
                    stream.ReadUI64(out _entries[i]._moofOffset);
                } else {
                    uint time = 0;
                    uint moof_offset = 0;
                    stream.ReadUI32(out time);
                    stream.ReadUI32(out moof_offset);
                    _entries[i]._time       = time;
                    _entries[i]._moofOffset = moof_offset;
                }

                switch (_lengthSizeOfTrafNumber) {
                    case 0: {
                        byte traf_number;
                        stream.ReadUI08(out traf_number);
                        _entries[i]._trafNumber = traf_number;
                        break;
                    }
                    case 1: {
                        ushort traf_number;
                        stream.ReadUI16(out traf_number);
                        _entries[i]._trafNumber = traf_number;
                        break;
                    }
                    case 2: {
                        uint traf_number;
                        stream.ReadUI24(out traf_number);
                        _entries[i]._trafNumber = traf_number;
                        break;
                    }
                    case 3: {
                        uint traf_number;
                        stream.ReadUI32(out traf_number);
                        _entries[i]._trafNumber = traf_number;
                        break;
                    }
                }

                switch (_lengthSizeOfTrunNumber) {
                    case 0: {
                        byte trun_number;
                        stream.ReadUI08(out trun_number);
                        _entries[i]._trunNumber = trun_number;
                        break;
                    }
                    case 1: {
                        ushort trun_number;
                        stream.ReadUI16(out trun_number);
                        _entries[i]._trunNumber = trun_number;
                        break;
                    }
                    case 2: {
                        uint trun_number;
                        stream.ReadUI24(out trun_number);
                        _entries[i]._trunNumber = trun_number;
                        break;
                    }
                    case 3: {
                        uint trun_number;
                        stream.ReadUI32(out trun_number);
                        _entries[i]._trunNumber = trun_number;
                        break;
                    }
                }

                switch (_lengthSizeOfSampleNumber) {
                    case 0: {
                        byte sample_number;
                        stream.ReadUI08(out sample_number);
                        _entries[i]._sampleNumber = sample_number;
                        break;
                    }
                    case 1: {
                        ushort sample_number;
                        stream.ReadUI16(out sample_number);
                        _entries[i]._sampleNumber = sample_number;
                        break;
                    }
                    case 2: {
                        uint sample_number;
                        stream.ReadUI24(out sample_number);
                        _entries[i]._sampleNumber = sample_number;
                        break;
                    }
                    case 3: {
                        uint sample_number;
                        stream.ReadUI32(out sample_number);
                        _entries[i]._sampleNumber = sample_number;
                        break;
                    }
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TfraAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.WriteUI32(_trackId);
            if (result.Failed()) return result;
            uint fields = (uint)((_lengthSizeOfTrafNumber<<4)|
                                 (_lengthSizeOfTrunNumber<<2)|
                                 (_lengthSizeOfSampleNumber ));
            result = stream.WriteUI32(fields);
            if (result.Failed()) return result;
            result = stream.WriteUI32((uint)_entries.Count);
            if (result.Failed()) return result;
            for (int i=0; i<_entries.Count; i++) {
                if (_version == 1) {
                    result = stream.WriteUI64(_entries[i]._time);
                    if (result.Failed()) return result;
                    result = stream.WriteUI64(_entries[i]._moofOffset);
                    if (result.Failed()) return result;
                } else {
                    result = stream.WriteUI32((uint)_entries[i]._time);
                    if (result.Failed()) return result;
                    result = stream.WriteUI32((uint)_entries[i]._moofOffset);
                    if (result.Failed()) return result;
                }

                switch (_lengthSizeOfTrafNumber) {
                    case 0:
                        result = stream.WriteUI08((byte)_entries[i]._trafNumber);
                        if (result.Failed()) return result;
                        break;
                    case 1:
                        result = stream.WriteUI16((ushort)_entries[i]._trafNumber);
                        if (result.Failed()) return result;
                        break;
                    case 2:
                        result = stream.WriteUI24(_entries[i]._trafNumber);
                        if (result.Failed()) return result;
                        break;
                    case 3:
                        result = stream.WriteUI32(_entries[i]._trafNumber);
                        if (result.Failed()) return result;
                        break;
                }

                switch (_lengthSizeOfTrunNumber) {
                    case 0:
                        result = stream.WriteUI08((byte)_entries[i]._trunNumber);
                        if (result.Failed()) return result;
                        break;
                    case 1:
                        result = stream.WriteUI16((ushort)_entries[i]._trunNumber);
                        if (result.Failed()) return result;
                        break;
                    case 2:
                        result = stream.WriteUI24(_entries[i]._trunNumber);
                        if (result.Failed()) return result;
                        break;
                    case 3:
                        result = stream.WriteUI32(_entries[i]._trunNumber);
                        if (result.Failed()) return result;
                        break;
                }

                switch (_lengthSizeOfSampleNumber) {
                    case 0:
                        result = stream.WriteUI08((byte)_entries[i]._sampleNumber);
                        if (result.Failed()) return result;
                        break;
                    case 1:
                        result = stream.WriteUI16((ushort)_entries[i]._sampleNumber);
                        if (result.Failed()) return result;
                        break;
                    case 2:
                        result = stream.WriteUI24(_entries[i]._sampleNumber);
                        if (result.Failed()) return result;
                        break;
                    case 3:
                        result = stream.WriteUI32(_entries[i]._sampleNumber);
                        if (result.Failed()) return result;
                        break;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TfraAtom::AddEntry
        +---------------------------------------------------------------------*/
        public Ap4Result AddEntry(ulong time,
                                  ulong moof_offset,
                                  uint  traf_number = 1,
                                  uint  trun_number = 1,
                                  uint  sample_number = 1)
        {
            if (time > 0xFFFFFFFFUL || moof_offset > 0xFFFFFFFFUL) {
                _version = 1;
            }

            Entry entry = new Entry(time, moof_offset, traf_number, trun_number, sample_number);
            _entries.Add(entry);

            _size32 = Ap4.FULL_ATOM_HEADER_SIZE+4+4+4 +
                      (uint)_entries.Count*(uint)((_version == 0 ? 8 : 16) +
                                                  _lengthSizeOfTrafNumber+1+
                                                  _lengthSizeOfTrunNumber+1+
                                                  _lengthSizeOfSampleNumber+1);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TfraAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("track_ID", _trackId);
            inspector.AddField("length_size_of_traf_num",   _lengthSizeOfTrafNumber);
            inspector.AddField("length_size_of_trun_num",   _lengthSizeOfTrunNumber);
            inspector.AddField("length_size_of_sample_num", _lengthSizeOfSampleNumber);
            if (inspector.GetVerbosity() >= 1) {
                inspector.StartArray("entries", (uint)_entries.Count);
                for (int i=0; i<_entries.Count; i++) {
                    inspector.StartObject(null, 5, true);
                    inspector.AddField("time",          _entries[i]._time);
                    inspector.AddField("moof_offset",   _entries[i]._moofOffset);
                    inspector.AddField("traf_number",   _entries[i]._trafNumber);
                    inspector.AddField("trun_number",   _entries[i]._trunNumber);
                    inspector.AddField("sample_number", _entries[i]._sampleNumber);
                    inspector.EndObject();
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public List<Entry> GetEntries() { return _entries; }
        public void SetTrackId(uint track_id) { _trackId = track_id; }
        public uint GetTrackId( )             { return _trackId;     }

        // members
        private uint        _trackId;
        private byte        _lengthSizeOfTrafNumber;
        private byte        _lengthSizeOfTrunNumber;
        private byte        _lengthSizeOfSampleNumber;
        private List<Entry> _entries = new List<Entry>();
    }
}
