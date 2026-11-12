// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SampleTable.h, Ap4SampleTable.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SampleTable
    +---------------------------------------------------------------------*/
    public abstract class SampleTable
    {
        // methods
        public abstract uint      GetSampleCount();
        // PORT NOTE: `AP4_Sample& sample` -> the passed Sample object is filled in place (see Sample.cs)
        public abstract Ap4Result GetSample(uint sample_index, Sample sample);
        public abstract Ap4Result GetSampleChunkPosition(uint     sample_index,
                                                         out uint chunk_index,
                                                         out uint position_in_chunk);
        public abstract uint      GetSampleDescriptionCount();
        public abstract SampleDescription GetSampleDescription(uint index);
        public abstract Ap4Result GetSampleIndexForTimeStamp(ulong    ts,
                                                             out uint index);
        public abstract uint      GetNearestSyncSampleIndex(uint index, bool before=true);

        /*----------------------------------------------------------------------
        |   AP4_SampleTable::GenerateStblAtom
        +---------------------------------------------------------------------*/
        public virtual Ap4Result GenerateStblAtom(out ContainerAtom stbl)
        {
            // create the stbl container
            stbl = new ContainerAtom(AtomType.STBL);

            // create the stsd atom
            StsdAtom stsd = new StsdAtom(this);

            // create the stts atom
            SttsAtom stts = new SttsAtom();

            // create the stsc atom
            StscAtom stsc = new StscAtom();

            // create the stsz atom
            StszAtom stsz = new StszAtom();

            // create the stss atom
            StssAtom stss = new StssAtom();

            // declare the ctts atom (may be created later)
            CttsAtom ctts = null;

            // start chunk table
            uint        current_chunk_index              = 0;
            uint        current_chunk_size               = 0;
            ulong       current_chunk_offset             = 0;
            uint        current_samples_in_chunk         = 0;
            uint        current_sample_description_index = 0;
            uint        current_duration                 = 0;
            uint        current_duration_run             = 0;
            uint        current_cts_delta                = 0;
            uint        current_cts_delta_run            = 0;
            List<ulong> chunk_offsets = new List<ulong>();

            // process all the samples
            bool all_samples_are_sync = false;
            uint sample_count = GetSampleCount();
            for (uint i=0; i<sample_count; i++) {
                Sample sample = new Sample();
                GetSample(i, sample);

                // update DTS table
                uint new_duration = sample.GetDuration();
                if (new_duration != current_duration && current_duration_run != 0) {
                    // emit a new stts entry
                    stts.AddEntry(current_duration_run, current_duration);

                    // reset the run count
                    current_duration_run = 0;
                }
                ++current_duration_run;
                current_duration = new_duration;

                // update CTS table
                uint new_cts_delta = sample.GetCtsDelta();
                if (new_cts_delta != current_cts_delta && current_cts_delta_run != 0) {
                    // create a ctts atom if we don't have one
                    if (ctts == null) ctts = new CttsAtom();

                    //emit a new ctts entry
                    ctts.AddEntry(current_cts_delta_run, current_cts_delta);

                    // reset the run count
                    current_cts_delta_run = 0;
                }
                ++current_cts_delta_run;
                current_cts_delta = new_cts_delta;

                // add an entry into the stsz atom
                stsz.AddEntry(sample.GetSize());

                // update the sync sample table
                if (sample.IsSync()) {
                    stss.AddEntry(i+1);
                    if (i==0) all_samples_are_sync = true;
                } else {
                    all_samples_are_sync = false;
                }

                // see in which chunk this sample is
                uint      chunk_index = 0;
                uint      position_in_chunk = 0;
                Ap4Result result = GetSampleChunkPosition(i, out chunk_index, out position_in_chunk);
                if (result.Succeeded()) {
                    if (chunk_index != current_chunk_index && current_samples_in_chunk != 0) {
                        // new chunk
                        chunk_offsets.Add(current_chunk_offset);
                        current_chunk_offset += current_chunk_size;

                        stsc.AddEntry(1,
                                      current_samples_in_chunk,
                                      current_sample_description_index+1);

                        current_samples_in_chunk = 0;
                        current_chunk_size       = 0;
                    }
                    current_chunk_index = chunk_index;
                }

                // store the sample description index
                current_sample_description_index = sample.GetDescriptionIndex();

                // adjust the current chunk info
                current_chunk_size += sample.GetSize();
                ++current_samples_in_chunk;
            }

            // finish the stts table
            if (sample_count != 0) stts.AddEntry(current_duration_run, current_duration);

            // finish the ctts table if we have one
            if (ctts != null) {
                Ap4.ASSERT(current_cts_delta_run != 0);

                // add a ctts entry
                ctts.AddEntry(current_cts_delta_run, current_cts_delta);
            }

            // process any unfinished chunk
            if (current_samples_in_chunk != 0) {
                // new chunk
                chunk_offsets.Add(current_chunk_offset);
                stsc.AddEntry(1,
                              current_samples_in_chunk,
                              current_sample_description_index+1);
            }

            // attach the children of stbl
            stbl.AddChild(stsd);
            stbl.AddChild(stts);
            if (ctts != null) stbl.AddChild(ctts);
            stbl.AddChild(stsc);
            stbl.AddChild(stsz);
            if (!all_samples_are_sync && stss.GetEntries().Count != 0) {
                stbl.AddChild(stss);
            }

            // see if we need a co64 or an stco atom
            uint chunk_count = (uint)chunk_offsets.Count;
            if (current_chunk_offset <= 0xFFFFFFFF) {
                // make an array of 32-bit entries
                uint[] chunk_offsets_32 = new uint[chunk_count];
                for (uint i=0; i<chunk_count; i++) {
                    chunk_offsets_32[i] = (uint)chunk_offsets[(int)i];
                }
                // create the stco atom
                StcoAtom stco = new StcoAtom(chunk_offsets_32, chunk_count);
                stbl.AddChild(stco);
            } else {
                // create the co64 atom
                Co64Atom co64 = new Co64Atom(chunk_offsets.ToArray(), chunk_count);
                stbl.AddChild(co64);
            }

            return Ap4Result.SUCCESS;
        }
    }
}
