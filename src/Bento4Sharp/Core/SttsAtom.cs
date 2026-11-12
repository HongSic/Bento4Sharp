// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SttsAtom.h, Ap4SttsAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SttsTableEntry
    +---------------------------------------------------------------------*/
    // PORT NOTE: a class (not a struct), see StscTableEntry.
    public class SttsTableEntry
    {
        public SttsTableEntry()
        {
            _sampleCount = 0;
            _sampleDuration = 0;
        }
        public SttsTableEntry(uint sample_count,
                              uint sample_duration)
        {
            _sampleCount = sample_count;
            _sampleDuration = sample_duration;
        }

        public uint _sampleCount;
        public uint _sampleDuration;
    }

    /*----------------------------------------------------------------------
    |   AP4_SttsAtom
    +---------------------------------------------------------------------*/
    public class SttsAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_SttsAtom::Create
        +---------------------------------------------------------------------*/
        public static SttsAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new SttsAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_SttsAtom::AP4_SttsAtom
        +---------------------------------------------------------------------*/
        public SttsAtom() :
            base(AtomType.STTS, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
            _lookupCache.entry_index = 0;
            _lookupCache.sample      = 0;
            _lookupCache.dts         = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_SttsAtom::AP4_SttsAtom
        +---------------------------------------------------------------------*/
        private SttsAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.STTS, size, version, flags)
        {
            _lookupCache.entry_index = 0;
            _lookupCache.sample      = 0;
            _lookupCache.dts         = 0;

            uint entry_count;
            stream.ReadUI32(out entry_count);
            while (entry_count-- != 0) {
                uint sample_count;
                uint sample_duration;
                if (stream.ReadUI32(out sample_count)    == Ap4Result.SUCCESS &&
                    stream.ReadUI32(out sample_duration) == Ap4Result.SUCCESS) {
                    _entries.Add(new SttsTableEntry(sample_count,
                                                    sample_duration));
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SttsAtom::GetDts
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream has one method with an optional `AP4_UI32* duration
        // = NULL`; the port has two overloads (the 3-argument one is the virtual).
        public Ap4Result GetDts(uint sample, out ulong dts)
        {
            uint unused1;
            return GetDts(sample, out dts, out unused1);
        }

        public virtual Ap4Result GetDts(uint sample, out ulong dts, out uint duration)
        {
            // default value
            dts = 0;
            duration = 0;

            // sample indexes start at 1
            if (sample == 0) return Ap4Result.ERROR_OUT_OF_RANGE;
            --sample;

            // check the lookup cache
            uint  lookup_start = 0;
            uint  sample_start = 0;
            ulong dts_start    = 0;
            if (sample >= _lookupCache.sample) {
                // start from the cached entry
                lookup_start = _lookupCache.entry_index;
                sample_start = _lookupCache.sample;
                dts_start    = _lookupCache.dts;
            }

            // look from the last known point
            for (uint i = lookup_start; i < (uint)_entries.Count; i++) {
                SttsTableEntry entry = _entries[(int)i];

                // check if we have reached the sample
                if (sample < sample_start+entry._sampleCount) {
                    // we are within the sample range for the current entry
                    dts = dts_start + (ulong)(sample - sample_start) * (ulong)entry._sampleDuration;
                    duration = entry._sampleDuration;

                    // update the lookup cache
                    _lookupCache.entry_index = i;
                    _lookupCache.sample      = sample_start;
                    _lookupCache.dts         = dts_start;

                    return Ap4Result.SUCCESS;
                }

                // update the sample and dts bases
                sample_start += entry._sampleCount;
                dts_start    += (ulong)entry._sampleCount * (ulong)entry._sampleDuration;
            }

            // sample is greater than the number of samples
            return Ap4Result.ERROR_OUT_OF_RANGE;
        }

        /*----------------------------------------------------------------------
        |   AP4_SttsAtom::AddEntry
        +---------------------------------------------------------------------*/
        public virtual Ap4Result AddEntry(uint sample_count, uint sample_duration)
        {
            _entries.Add(new SttsTableEntry(sample_count, sample_duration));
            _size32 += 8;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SttsAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // write the entry count
            uint entry_count = (uint)_entries.Count;
            result = stream.WriteUI32(entry_count);
            if (result.Failed()) return result;

            // write the entries
            for (uint i=0; i<entry_count; i++) {
                // sample count
                result = stream.WriteUI32(_entries[(int)i]._sampleCount);
                if (result.Failed()) return result;

                // time offset
                result = stream.WriteUI32(_entries[(int)i]._sampleDuration);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SttsAtom::GetSampleIndexForTimeStamp
        +---------------------------------------------------------------------*/
        public virtual Ap4Result GetSampleIndexForTimeStamp(ulong    ts,
                                                            out uint sample_index)
        {
            // init
            uint entry_count = (uint)_entries.Count;
            ulong accumulated = 0;
            sample_index = 0;

            for (uint i=0; i<entry_count; i++) {
                ulong next_accumulated =
                    accumulated +
                    (ulong)_entries[(int)i]._sampleCount *
                    (ulong)_entries[(int)i]._sampleDuration;

                // check if the ts is in the range of this entry
                if (ts < next_accumulated) {
                    sample_index += (uint)((ts - accumulated) / _entries[(int)i]._sampleDuration);
                    return Ap4Result.SUCCESS;
                }

                // update accumulated and sample
                accumulated = next_accumulated;
                sample_index += _entries[(int)i]._sampleCount;
            }

            // ts not in range of the table
            return Ap4Result.FAILURE;
        }

        /*----------------------------------------------------------------------
        |   AP4_SttsAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("entry_count", (uint)_entries.Count);

            if (inspector.GetVerbosity() >= 1) {
                inspector.StartArray("entries", (uint)_entries.Count);
                for (int i=0; i<_entries.Count; i++) {
                    inspector.StartObject(null, 2, true);
                    inspector.AddField("sample_count",    _entries[i]._sampleCount);
                    inspector.AddField("sample_duration", _entries[i]._sampleDuration);
                    inspector.EndObject();
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private List<SttsTableEntry> _entries = new List<SttsTableEntry>();
        private struct LookupCache {
            public uint  entry_index;
            public uint  sample;
            public ulong dts;
        }
        private LookupCache _lookupCache;
    }
}
