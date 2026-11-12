// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4FragmentSampleTable.h, Ap4FragmentSampleTable.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_FragmentSampleTable
    +---------------------------------------------------------------------*/
    public class FragmentSampleTable : SampleTable
    {
        /*----------------------------------------------------------------------
        |   AP4_FragmentSampleTable::AP4_FragmentSampleTable
        +---------------------------------------------------------------------*/
        public FragmentSampleTable(ContainerAtom traf,
                                   TrexAtom      trex,
                                   ByteStream    sample_stream,
                                   ulong         moof_offset,
                                   ulong         mdat_payload_offset, // hack because MS doesn't implement the spec correctly
                                   ulong         dts_origin=0)
        {
            _duration = 0;

            TfhdAtom tfhd = traf.GetChild(AtomType.TFHD) as TfhdAtom;
            if (tfhd == null) return;

            // count all the samples and reserve space for them
            uint sample_count = 0;
            List<Atom> children = traf.GetChildren();
            for (int item = 0; item < children.Count; item++) {
                Atom atom = children[item];
                if (atom.GetType() == AtomType.TRUN) {
                    TrunAtom trun = atom as TrunAtom;
                    if (trun != null) sample_count += (uint)trun.GetEntries().Count;
                }
            }
            _samples.EnsureCapacity((int)sample_count);

            // check if we have a timecode base
            TfdtAtom tfdt = traf.GetChild(AtomType.TFDT) as TfdtAtom;
            if (tfdt != null) {
                dts_origin = tfdt.GetBaseMediaDecodeTime();
            }

            // process all the trun atoms
            for (int item = 0; item < children.Count; item++) {
                Atom atom = children[item];
                if (atom.GetType() == AtomType.TRUN) {
                    TrunAtom trun = atom as TrunAtom;
                    if (trun != null) {
                        Ap4Result result = AddTrun(trun,
                                                   tfhd,
                                                   trex,
                                                   sample_stream,
                                                   moof_offset,
                                                   ref mdat_payload_offset,
                                                   ref dts_origin);
                        if (result.Failed()) return;
                    }
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_FragmentSampleTable::AddTrun
        +---------------------------------------------------------------------*/
        private Ap4Result AddTrun(TrunAtom    trun,
                                  TfhdAtom    tfhd,
                                  TrexAtom    trex,
                                  ByteStream  sample_stream,
                                  ulong       moof_offset,
                                  ref ulong   payload_offset,
                                  ref ulong   dts_origin)
        {
            uint tfhd_flags = tfhd.GetFlags();
            uint trun_flags = trun.GetFlags();

            // update the number of samples
            int start = _samples.Count;
            // m_Samples.SetItemCount(start + trun->GetEntries().ItemCount())
            for (int i=0; i<trun.GetEntries().Count; i++) {
                _samples.Add(new Sample());
            }

            // base data offset
            ulong data_offset = 0;
            if ((tfhd_flags & Ap4.TFHD_FLAG_BASE_DATA_OFFSET_PRESENT) != 0) {
                data_offset = tfhd.GetBaseDataOffset();
            } else {
                data_offset = moof_offset;
            }
            if ((trun_flags & Ap4.TRUN_FLAG_DATA_OFFSET_PRESENT) != 0) {
                data_offset = unchecked(data_offset + (ulong)(long)trun.GetDataOffset()); // SI32 sign-extended, as in C++
            }
            // MS hack
            if (data_offset == moof_offset) {
                data_offset = payload_offset;
            } else {
                payload_offset = data_offset;
            }

            // sample description index
            uint sample_description_index = 0;
            if ((tfhd_flags & Ap4.TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT) != 0) {
                sample_description_index = tfhd.GetSampleDescriptionIndex();
            } else if (trex != null) {
                sample_description_index = trex.GetDefaultSampleDescriptionIndex();
            }

            // default sample size
            uint default_sample_size = 0;
            if ((tfhd_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_SIZE_PRESENT) != 0) {
                default_sample_size = tfhd.GetDefaultSampleSize();
            } else if (trex != null) {
                default_sample_size = trex.GetDefaultSampleSize();
            }

            // default sample duration
            uint default_sample_duration = 0;
            if ((tfhd_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_DURATION_PRESENT) != 0) {
                default_sample_duration = tfhd.GetDefaultSampleDuration();
            } else if (trex != null) {
                default_sample_duration = trex.GetDefaultSampleDuration();
            }

            // default sample flags
            uint default_sample_flags = 0;
            if ((tfhd_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT) != 0) {
                default_sample_flags = tfhd.GetDefaultSampleFlags();
            } else if (trex != null) {
                default_sample_flags = trex.GetDefaultSampleFlags();
            }

            // parse all trun entries to setup the samples
            ulong dts = dts_origin;
            for (int i=0; i<trun.GetEntries().Count; i++) {
                TrunAtom.Entry entry  = trun.GetEntries()[i];
                Sample         sample = _samples[start+i];

                // sample size
                if ((trun_flags & Ap4.TRUN_FLAG_SAMPLE_SIZE_PRESENT) != 0) {
                    sample.SetSize(entry.sample_size);
                } else {
                    sample.SetSize(default_sample_size);
                }
                payload_offset += sample.GetSize(); // update the payload offset

                // sample duration
                if ((trun_flags & Ap4.TRUN_FLAG_SAMPLE_DURATION_PRESENT) != 0) {
                    sample.SetDuration(entry.sample_duration);
                } else {
                    sample.SetDuration(default_sample_duration);
                }

                // sample flags
                uint sample_flags = default_sample_flags;
                if (i==0 && (trun_flags & Ap4.TRUN_FLAG_FIRST_SAMPLE_FLAGS_PRESENT) != 0) {
                    sample_flags = trun.GetFirstSampleFlags();
                } else if ((trun_flags & Ap4.TRUN_FLAG_SAMPLE_FLAGS_PRESENT) != 0) {
                    sample_flags = entry.sample_flags;
                }
                if ((sample_flags & Ap4.FRAG_FLAG_SAMPLE_IS_DIFFERENCE) == 0) {
                    sample.SetSync(true);
                } else {
                    sample.SetSync(false);
                }

                // sample description index
                if (sample_description_index >= 1) {
                    sample.SetDescriptionIndex(sample_description_index-1);
                }

                // data stream
                if (sample_stream != null) sample.SetDataStream(sample_stream);

                // data offset
                sample.SetOffset(data_offset);
                data_offset += sample.GetSize();

                // dts and cts
                sample.SetDts(dts);
                if ((trun_flags & Ap4.TRUN_FLAG_SAMPLE_COMPOSITION_TIME_OFFSET_PRESENT) != 0) {
                    sample.SetCtsDelta(entry.sample_composition_time_offset);
                }

                // update the counters
                dts       += sample.GetDuration();
                _duration += sample.GetDuration();
            }

            // update the dts
            dts_origin = dts;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_FragmentSampleTable::GetSample
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSample(uint   index,
                                            Sample sample)
        {
            if (index >= (uint)_samples.Count) return Ap4Result.ERROR_OUT_OF_RANGE;

            // copy the sample
            sample.Assign(_samples[(int)index]);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_FragmentSampleTable::GetSampleCount
        +---------------------------------------------------------------------*/
        public override uint GetSampleCount()
        {
            return (uint)_samples.Count;
        }

        /*----------------------------------------------------------------------
        |   AP4_FragmentSampleTable::GetSampleDescription
        +---------------------------------------------------------------------*/
        public override SampleDescription GetSampleDescription(uint index)
        {
            return null; // TODO
        }

        /*----------------------------------------------------------------------
        |   AP4_FragmentSampleTable::GetSampleDescriptionCount
        +---------------------------------------------------------------------*/
        public override uint GetSampleDescriptionCount()
        {
            return 1; // TODO
        }

        /*----------------------------------------------------------------------
        |   AP4_FragmentSampleTable::GetSampleChunkPosition
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSampleChunkPosition(uint     sample_index,
                                                         out uint chunk_index,
                                                         out uint position_in_chunk)
        {
            chunk_index       = 0;
            position_in_chunk = sample_index;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_FragmentSampleTable::GetSampleIndexForTimeStamp
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSampleIndexForTimeStamp(ulong    ts,
                                                             out uint sample_index)
        {
            sample_index = 0; // TODO
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_FragmentSampleTable::GetNearestSyncSampleIndex
        +---------------------------------------------------------------------*/
        public override uint GetNearestSyncSampleIndex(uint sample_index, bool before = true)
        {
            return 0; // TODO
        }

        // methods
        public ulong GetDuration() { return _duration; }

        // members
        private List<Sample> _samples = new List<Sample>();
        private ulong        _duration;
    }
}
