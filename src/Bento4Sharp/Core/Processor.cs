// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Processor.h, Ap4Processor.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   types
    +---------------------------------------------------------------------*/
    // PORT NOTE: AP4_SampleLocator is a struct that upstream copies by value
    // (`locators.Append(locator)`); the port uses a class with an explicit copy
    // constructor, called wherever upstream makes a copy.
    internal sealed class SampleLocator
    {
        public SampleLocator()
        {
            _trakIndex = 0;
            _sampleTable = null;
            _sampleIndex = 0;
            _chunkIndex = 0;
            _sample = new Sample();
        }
        public SampleLocator(SampleLocator other)
        {
            _trakIndex = other._trakIndex;
            _sampleTable = other._sampleTable;
            _sampleIndex = other._sampleIndex;
            _chunkIndex = other._chunkIndex;
            _sample = new Sample(other._sample);
        }
        public uint             _trakIndex;
        public AtomSampleTable  _sampleTable;
        public uint             _sampleIndex;
        public uint             _chunkIndex;
        public Sample           _sample;
    }

    internal sealed class SampleCursor
    {
        public SampleCursor() { _endReached = false; }
        public SampleLocator _locator = new SampleLocator();
        public bool          _endReached;
    }

    // PORT NOTE: declared `struct AP4_AtomLocator` at namespace scope upstream
    // (forward-declared in Ap4Processor.h because it appears in the signature of
    // the protected ProcessFragments method), so it has to be public here.
    public sealed class AtomLocator
    {
        public AtomLocator(Atom atom, ulong offset)
        {
            _atom = atom;
            _offset = offset;
        }
        public Atom  _atom;
        public ulong _offset;
    }

    /*----------------------------------------------------------------------
    |   AP4_DefaultFragmentHandler
    +---------------------------------------------------------------------*/
    internal class DefaultFragmentHandler : Processor.FragmentHandler
    {
        public DefaultFragmentHandler(Processor.TrackHandler track_handler)
        {
            _trackHandler = track_handler;
        }

        /*----------------------------------------------------------------------
        |   AP4_DefaultFragmentHandler::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessSample(DataBuffer data_in, DataBuffer data_out)
        {
            if (_trackHandler == null) {
                data_out.SetData(data_in.GetData(), data_in.GetDataSize());
                return Ap4Result.SUCCESS;
            }
            return _trackHandler.ProcessSample(data_in, data_out);
        }

        private Processor.TrackHandler _trackHandler;
    }

    /*----------------------------------------------------------------------
    |   AP4_Processor
    +---------------------------------------------------------------------*/
    public class Processor
    {
        /// <summary>
        /// Abstract class that defines the interface implemented by progress
        /// listeners. A progress listener is called during the <c>AP4_Processor::Process()</c>
        /// method to notify of progres information.
        /// </summary>
        public abstract class ProgressListener
        {
            /// <summary>
            /// This method is called during the call to <c>AP4_Processor::Process()</c> to
            /// notify of the progress of the operation. If this method returns an
            /// error result, processing is aborted.
            /// </summary>
            /// <param name="step">Ordinal of the current progress step.</param>
            /// <param name="total">Total number of steps.</param>
            /// <returns>A result code. If this method returns <see cref="Ap4Result.SUCCESS"/>, the
            /// processing continues. If an error code is returned, the processing
            /// is aborted.</returns>
            public abstract Ap4Result OnProgress(uint step,
                                                 uint total);
        }

        /// <summary>
        /// Abstract class that defines the interface implemented by concrete
        /// track handlers. A track handler is responsible for processing a
        /// track and its media samples.
        /// </summary>
        public abstract class TrackHandler
        {
            /// <summary>
            /// A track handler may override this method if it needs to modify
            /// the track atoms before processing the track samples.
            /// </summary>
            public virtual Ap4Result ProcessTrack() { return Ap4Result.SUCCESS; }

            /// <summary>
            /// Returns the size of a sample after processing.
            /// </summary>
            /// <param name="sample">Sample of which the processed size is requested.</param>
            /// <returns>Size of the sample data after processing.</returns>
            public virtual uint GetProcessedSampleSize(Sample sample) { return sample.GetSize(); }

            /// <summary>
            /// Process the data of one sample.
            /// </summary>
            /// <param name="data_in">Data buffer with the data of the sample to process.</param>
            /// <param name="data_out">Data buffer in which the processed sample data is
            /// returned.</param>
            public abstract Ap4Result ProcessSample(DataBuffer data_in,
                                                    DataBuffer data_out);
        }

        /// <summary>
        /// Abstract class that defines the interface implemented by concrete
        /// fragment handlers. A fragment handler is responsible for processing a
        /// fragment and its media samples.
        /// </summary>
        public abstract class FragmentHandler
        {
            /// <summary>
            /// A fragment handler may override this method if it needs to modify
            /// the fragment atoms before processing the fragment samples.
            /// </summary>
            public virtual Ap4Result ProcessFragment() { return Ap4Result.SUCCESS; }

            /// <summary>
            /// A fragment handler may override this method if it needs to inspect
            /// the samples before they are written out.
            /// </summary>
            public virtual Ap4Result PrepareForSamples(FragmentSampleTable sample_table) {
                return Ap4Result.SUCCESS;
            }

            /// <summary>
            /// A fragment handler may override this method if it needs to modify
            /// the fragment atoms after processing the fragment samples.
            /// NOTE: this method MUST NOT change the size of any of the atoms.
            /// </summary>
            public virtual Ap4Result FinishFragment() { return Ap4Result.SUCCESS; }

            /// <summary>
            /// Process the data of one sample.
            /// </summary>
            /// <param name="data_in">Data buffer with the data of the sample to process.</param>
            /// <param name="data_out">Data buffer in which the processed sample data is
            /// returned.</param>
            public abstract Ap4Result ProcessSample(DataBuffer data_in,
                                                    DataBuffer data_out);
        }

        /*----------------------------------------------------------------------
        |   FragmentMapEntry
        +---------------------------------------------------------------------*/
        private struct FragmentMapEntry {
            public ulong before;
            public ulong after;
        }

        /*----------------------------------------------------------------------
        |   FindFragmentMapEntry
        +---------------------------------------------------------------------*/
        // PORT NOTE: returns the index of the entry (-1 for NULL) instead of a pointer.
        private static int
        FindFragmentMapEntry(List<FragmentMapEntry> fragment_map, ulong fragment_offset) {
            int first = 0;
            int last = fragment_map.Count;
            while (first < last) {
                int middle = (last+first)/2;
                ulong middle_value = fragment_map[middle].before;
                if (fragment_offset < middle_value) {
                    last = middle;
                } else if (fragment_offset > middle_value) {
                    first = middle+1;
                } else {
                    return middle;
                }
            }

            return -1;
        }

        /*----------------------------------------------------------------------
        |   AP4_Processor::ProcessFragments
        +---------------------------------------------------------------------*/
        protected Ap4Result
        ProcessFragments(MoovAtom          moov,
                         List<AtomLocator> atoms,
                         ContainerAtom     mfra,
                         SidxAtom          sidx,
                         ulong             sidx_position,
                         ByteStream        input,
                         ByteStream        output)
        {
            uint fragment_index = 0;
            List<FragmentMapEntry> fragment_map = new List<FragmentMapEntry>();

            for (int item = 0;
                     item < atoms.Count;
                     item++, ++fragment_index) {
                AtomLocator        locator     = atoms[item];
                Atom               atom        = locator._atom;
                ulong              atom_offset = locator._offset;
                ulong              mdat_payload_offset = atom_offset+atom.GetSize()+Ap4.ATOM_HEADER_SIZE;
                Sample             sample = new Sample();
                DataBuffer         sample_data_in = new DataBuffer();
                DataBuffer         sample_data_out = new DataBuffer();
                Ap4Result          result = Ap4Result.SUCCESS;

                // if this is not a moof atom, just write it back and continue
                if (atom.GetType() != AtomType.MOOF) {
                    result = atom.Write(output);
                    if (result.Failed()) return result;
                    continue;
                }

                // parse the moof
                ContainerAtom moof = atom as ContainerAtom;
                MovieFragment fragment = new MovieFragment(moof);

                // process all the traf atoms
                List<Processor.FragmentHandler> handlers = new List<Processor.FragmentHandler>();
                List<FragmentSampleTable> sample_tables = new List<FragmentSampleTable>();
                for (Atom child; (child = moof.GetChild(AtomType.TRAF, (uint)handlers.Count)) != null;) {
                    ContainerAtom traf = child as ContainerAtom;
                    TfhdAtom tfhd = traf.GetChild(AtomType.TFHD) as TfhdAtom;

                    // find the 'trak' for this track
                    TrakAtom trak = null;
                    foreach (Atom child_atom in moov.GetChildren()) {
                        if (child_atom.GetType() == AtomType.TRAK) {
                            trak = child_atom as TrakAtom;
                            if (trak != null) {
                                TkhdAtom tkhd = trak.GetChild(AtomType.TKHD) as TkhdAtom;
                                if (tkhd != null && tkhd.GetTrackId() == tfhd.GetTrackId()) {
                                    break;
                                }
                            }
                            trak = null;
                        }
                    }

                    // find the 'trex' for this track
                    ContainerAtom  mvex = null;
                    TrexAtom       trex = null;
                    mvex = moov.GetChild(AtomType.MVEX) as ContainerAtom;
                    if (mvex != null) {
                        foreach (Atom child_atom in mvex.GetChildren()) {
                            if (child_atom.GetType() == AtomType.TREX) {
                                trex = child_atom as TrexAtom;
                                if (trex != null && trex.GetTrackId() == tfhd.GetTrackId()) {
                                    break;
                                }
                                trex = null;
                            }
                        }
                    }

                    // create the handler for this traf
                    Processor.FragmentHandler handler = CreateFragmentHandler(trak, trex, traf, input, atom_offset);
                    if (handler != null) {
                        result = handler.ProcessFragment();
                        if (result.Failed()) return result;
                    }
                    handlers.Add(handler);

                    // create a sample table object so we can read the sample data
                    FragmentSampleTable sample_table = null;
                    result = fragment.CreateSampleTable(moov, tfhd.GetTrackId(), input, atom_offset, mdat_payload_offset, 0, out sample_table);
                    if (result.Failed()) return result;
                    sample_tables.Add(sample_table);

                    // let the handler look at the samples before we process them
                    if (handler != null) result = handler.PrepareForSamples(sample_table);
                    if (result.Failed()) return result;
                }

                // write the moof
                ulong moof_out_start = 0;
                output.Tell(out moof_out_start);
                moof.Write(output);

                // remember the location of this fragment
                FragmentMapEntry map_entry = new FragmentMapEntry { before = atom_offset, after = moof_out_start };
                fragment_map.Add(map_entry);

                // write an mdat header
                ulong mdat_out_start;
                ulong mdat_size = Ap4.ATOM_HEADER_SIZE;
                output.Tell(out mdat_out_start);
                output.WriteUI32(0);
                output.WriteUI32(AtomType.MDAT);

                // process all track runs
                for (uint i=0; i<handlers.Count; i++) {
                    Processor.FragmentHandler handler = handlers[(int)i];

                    // get the track ID
                    ContainerAtom traf = moof.GetChild(AtomType.TRAF, i) as ContainerAtom;
                    if (traf == null) continue;
                    TfhdAtom tfhd = traf.GetChild(AtomType.TFHD) as TfhdAtom;

                    // compute the base data offset
                    ulong base_data_offset;
                    if ((tfhd.GetFlags() & Ap4.TFHD_FLAG_BASE_DATA_OFFSET_PRESENT) != 0) {
                        base_data_offset = mdat_out_start+Ap4.ATOM_HEADER_SIZE;
                    } else {
                        base_data_offset = moof_out_start;
                    }

                    // build a list of all trun atoms
                    List<TrunAtom> truns = new List<TrunAtom>();
                    foreach (Atom child_atom in traf.GetChildren()) {
                        if (child_atom.GetType() == AtomType.TRUN) {
                            TrunAtom trun_ = child_atom as TrunAtom;
                            if (trun_ != null) {
                                truns.Add(trun_);
                            }
                        }
                    }
                    if (truns.Count == 0) {
                        continue;
                    }
                    uint     trun_index        = 0;
                    uint     trun_sample_index = 0;
                    TrunAtom trun = truns[0];
                    trun.SetDataOffset(unchecked((int)((mdat_out_start+mdat_size)-base_data_offset)));

                    // write the mdat
                    uint default_sample_size = 0;
                    for (uint j=0; j<sample_tables[(int)i].GetSampleCount(); j++, trun_sample_index++) {
                        // advance the trun index if necessary
                        if (trun_sample_index >= trun.GetEntries().Count) {
                            trun = truns[(int)(++trun_index)];
                            trun.SetDataOffset(unchecked((int)((mdat_out_start+mdat_size)-base_data_offset)));
                            trun_sample_index = 0;
                        }

                        // get the next sample
                        result = sample_tables[(int)i].GetSample(j, sample);
                        if (result.Failed()) return result;
                        sample.ReadData(sample_data_in);

                        // process the sample data
                        if (handler != null) {
                            result = handler.ProcessSample(sample_data_in, sample_data_out);
                            if (result.Failed()) return result;

                            // write the sample data
                            result = output.Write(sample_data_out.GetData(), sample_data_out.GetDataSize());
                            if (result.Failed()) return result;

                            // update the mdat size
                            mdat_size += sample_data_out.GetDataSize();

                            // update the trun entry
                            trun.UseEntries()[(int)trun_sample_index].sample_size = sample_data_out.GetDataSize();

                            // if this entry uses the default sample size, adjust the default accordingly
                            // (NOTE: there's only one default, so this assumes, of course, that all sample
                            // sizes change the same way, if they change at all)
                            if (default_sample_size == 0 && (trun.GetFlags() & Ap4.TRUN_FLAG_SAMPLE_SIZE_PRESENT) == 0) {
                                default_sample_size = sample_data_out.GetDataSize();
                            }
                        } else {
                            // write the sample data (unmodified)
                            result = output.Write(sample_data_in.GetData(), sample_data_in.GetDataSize());
                            if (result.Failed()) return result;

                            // update the mdat size
                            mdat_size += sample_data_in.GetDataSize();
                        }
                    }

                    if (handler != null) {
                        // update the tfhd header
                        if ((tfhd.GetFlags() & Ap4.TFHD_FLAG_BASE_DATA_OFFSET_PRESENT) != 0) {
                            tfhd.SetBaseDataOffset(mdat_out_start+Ap4.ATOM_HEADER_SIZE);
                        }
                        if ((tfhd.GetFlags() & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_SIZE_PRESENT) != 0) {
                            if (default_sample_size != 0) {
                                tfhd.SetDefaultSampleSize(default_sample_size);
                            }
                        }

                        // give the handler a chance to update the atoms
                        handler.FinishFragment();
                    }
                }

                // update the mdat header
                ulong mdat_out_end;
                output.Tell(out mdat_out_end);
                Ap4.ASSERT(mdat_out_end-mdat_out_start == mdat_size);
                output.Seek(mdat_out_start);
                output.WriteUI32((uint)mdat_size);
                output.Seek(mdat_out_end);

                // update the moof if needed
                output.Seek(moof_out_start);
                moof.Write(output);
                output.Seek(mdat_out_end);

                // update the sidx if we have one
                if (sidx != null && fragment_index < sidx.GetReferences().Count) {
                    if (fragment_index == 0) {
                        sidx.SetFirstOffset(moof_out_start-(sidx_position+sidx.GetSize()));
                    }
                    ulong fragment_size = mdat_out_end-moof_out_start;
                    SidxAtom.Reference sidx_ref = sidx.UseReferences()[(int)fragment_index];
                    sidx_ref._referencedSize = (uint)fragment_size;
                }

                // cleanup
                // (nothing to delete in the port)
            }

            // update the mfra if we have one
            if (mfra != null) {
                foreach (Atom mfra_item in mfra.GetChildren()) {
                    if (mfra_item.GetType() != AtomType.TFRA) continue;
                    TfraAtom tfra = mfra_item as TfraAtom;
                    if (tfra == null) continue;
                    List<TfraAtom.Entry> entries     = tfra.GetEntries();
                    uint                 entry_count = (uint)entries.Count;
                    for (int i=0; i<entry_count; i++) {
                        int found = FindFragmentMapEntry(fragment_map, entries[i]._moofOffset);
                        if (found >= 0) {
                            entries[i]._moofOffset = fragment_map[found].after;
                        }
                    }
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Processor::CreateFragmentHandler
        +---------------------------------------------------------------------*/
        /// <summary>
        /// This method can be overridden by concrete subclasses.
        /// It is called once for each fragment in the input file.
        /// </summary>
        /// <param name="trak">Pointer to the 'trak' atom that matches the track for this fragment</param>
        /// <param name="trex">Pointer to the 'trex' atom that defines the defaults for this track fragment</param>
        /// <param name="traf">Pointer to the fragment for which a handler should be created.</param>
        /// <param name="moof_data">Stream that contains the 'moof' atom.</param>
        /// <param name="moof_offset">Position of the 'moof' atom in <paramref name="moof_data"/>.</param>
        /// <returns>A pointer to a fragment handler, or NULL if no handler
        /// needs to be created for that fragment.</returns>
        public virtual FragmentHandler
        CreateFragmentHandler(TrakAtom       trak,
                              TrexAtom       trex,
                              ContainerAtom  traf,
                              ByteStream     moof_data,
                              ulong          moof_offset)
        {
            // find the matching track handler
            for (int i=0; i<_trackIds.Count; i++) {
                TfhdAtom  tfhd = traf.GetChild(AtomType.TFHD) as TfhdAtom;
                if (tfhd != null && _trackIds[i] == tfhd.GetTrackId()) {
                    return new DefaultFragmentHandler(_trackHandlers[i]);
                }
            }

            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_Processor::Process
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream names this protected overload `Process` too; it
        // differs from the public (fragments, output, init, ...) overload only by
        // pointer-vs-reference, which C# cannot express, hence the new name.
        // A null atom_factory stands for the upstream default argument
        // AP4_DefaultAtomFactory::Instance_.
        protected Ap4Result
        ProcessInternal(ByteStream        input,
                        ByteStream        output,
                        ByteStream        fragments,
                        ProgressListener  listener,
                        AtomFactory       atom_factory_)
        {
            AtomFactory atom_factory = atom_factory_ ?? DefaultAtomFactory.Instance;

            // read all atoms.
            // keep all atoms except [mdat]
            // keep a ref to [moov]
            // put [moof] atoms in a separate list
            AtomParent        top_level = new AtomParent();
            MoovAtom          moov = null;
            ContainerAtom     mfra = null;
            SidxAtom          sidx = null;
            List<AtomLocator> frags = new List<AtomLocator>();
            ulong             stream_offset = 0;
            bool              in_fragments = false;
            uint              sidx_count = 0;
            for (Atom atom = null;
                atom_factory.CreateAtomFromStream(input, out atom).Succeeded();
                input.Tell(out stream_offset)) {
                if (atom.GetType() == AtomType.MDAT) {
                    continue;
                } else if (atom.GetType() == AtomType.MOOV) {
                    moov = atom as MoovAtom;
                    if (fragments != null) break;
                } else if (atom.GetType() == AtomType.MFRA) {
                    mfra = atom as ContainerAtom;
                    continue;
                } else if (atom.GetType() == AtomType.SIDX) {
                    // don't keep the index, it is likely to be invalidated, we will recompute it later
                    ++sidx_count;
                    if (sidx == null) {
                        sidx = atom as SidxAtom;
                    } else {
                        continue;
                    }
                } else if (atom.GetType() == AtomType.SSIX) {
                    // don't keep the index, it is likely to be invalidated
                    continue;
                } else if (fragments == null && (in_fragments || atom.GetType() == AtomType.MOOF)) {
                    in_fragments = true;
                    frags.Add(new AtomLocator(atom, stream_offset));
                    continue;
                }
                top_level.AddChild(atom);
            }

            // check that we have at most one sidx (we can't deal with multi-sidx streams here
            if (sidx_count > 1) {
                // PORT NOTE: upstream passes a possibly-NULL sidx to RemoveChild
                if (sidx != null) top_level.RemoveChild(sidx);
                sidx = null;
            }

            // if we have a fragments stream, get the fragment locators from there
            if (fragments != null) {
                stream_offset = 0;
                for (Atom atom = null;
                    atom_factory.CreateAtomFromStream(fragments, out atom).Succeeded();
                    fragments.Tell(out stream_offset)) {
                    if (atom.GetType() == AtomType.MDAT) {
                        continue;
                    }
                    frags.Add(new AtomLocator(atom, stream_offset));
                }
            }

            // initialize the processor
            Ap4Result result = Initialize(top_level, input);
            if (result.Failed()) return result;

            // process the tracks if we have a moov atom
            List<SampleLocator> locators          = new List<SampleLocator>();
            uint                track_count       = 0;
            List<TrakAtom>      trak_atoms        = null;
            ulong               mdat_payload_size = 0;
            SampleCursor[]      cursors           = null;
            if (moov != null) {
                // build an array of track sample locators
                trak_atoms = moov.GetTrakAtoms();
                track_count = (uint)trak_atoms.Count;
                cursors = new SampleCursor[track_count];
                for (uint i=0; i<track_count; i++) {
                    cursors[i] = new SampleCursor();
                }
                SetItemCount(_trackHandlers, track_count, null);
                SetItemCount(_trackIds, track_count, 0u);
                for (int i=0; i<track_count; i++) {
                    _trackHandlers[i] = null;
                    _trackIds[i] = 0;
                }

                uint index = 0;
                foreach (TrakAtom trak in trak_atoms) {
                    // find the stsd atom
                    ContainerAtom stbl = trak.FindChild("mdia/minf/stbl") as ContainerAtom;
                    if (stbl == null) continue;

                    // see if there's an external data source for this track
                    ByteStream trak_data_stream = input;
                    foreach (ExternalTrackData tdata in _externalTrackData) {
                        if (tdata._trackId == trak.GetId()) {
                            trak_data_stream = tdata._mediaData;
                            break;
                        }
                    }

                    // create the track handler
                    _trackHandlers[(int)index] = CreateTrackHandler(trak);
                    _trackIds[(int)index]      = trak.GetId();
                    cursors[index]._locator._trakIndex   = index;
                    cursors[index]._locator._sampleTable = new AtomSampleTable(stbl, trak_data_stream);
                    cursors[index]._locator._sampleIndex = 0;
                    cursors[index]._locator._chunkIndex  = 0;
                    if (cursors[index]._locator._sampleTable.GetSampleCount() != 0) {
                        cursors[index]._locator._sampleTable.GetSample(0, cursors[index]._locator._sample);
                    } else {
                        cursors[index]._endReached = true;
                    }

                    index++;
                }

                // figure out the layout of the chunks
                for (;;) {
                    // see which is the next sample to write
                    ulong min_offset = unchecked((ulong)(-1L));
                    int cursor = -1;
                    for (uint i=0; i<track_count; i++) {
                        if (!cursors[i]._endReached &&
                            cursors[i]._locator._sampleTable != null &&
                            cursors[i]._locator._sample.GetOffset() <= min_offset) {
                            min_offset = cursors[i]._locator._sample.GetOffset();
                            cursor = (int)i;
                        }
                    }

                    // stop if all cursors are exhausted
                    if (cursor == -1) break;

                    // append this locator to the layout list
                    SampleLocator locator = cursors[cursor]._locator;
                    locators.Add(new SampleLocator(locator));

                    // move the cursor to the next sample
                    locator._sampleIndex++;
                    if (locator._sampleIndex == locator._sampleTable.GetSampleCount()) {
                        // mark this track as completed
                        cursors[cursor]._endReached = true;
                    } else {
                        // get the next sample info
                        locator._sampleTable.GetSample(locator._sampleIndex, locator._sample);
                        uint skip, sdesc;
                        locator._sampleTable.GetChunkForSample(locator._sampleIndex,
                                                               out locator._chunkIndex,
                                                               out skip, out sdesc);
                    }
                }

                // update the stbl atoms and compute the mdat size
                int current_track = -1;
                int current_chunk = -1;
                ulong current_chunk_offset = 0;
                uint current_chunk_size = 0;
                for (int i=0; i<locators.Count; i++) {
                    SampleLocator locator = locators[i];
                    if ((int)locator._trakIndex  != current_track ||
                        (int)locator._chunkIndex != current_chunk) {
                        // start a new chunk for this track
                        current_chunk_offset += current_chunk_size;
                        current_chunk_size = 0;
                        current_track = (int)locator._trakIndex;
                        current_chunk = (int)locator._chunkIndex;
                        locator._sampleTable.SetChunkOffset(locator._chunkIndex, current_chunk_offset);
                    }
                    uint sample_size;
                    TrackHandler handler = _trackHandlers[(int)locator._trakIndex];
                    if (handler != null) {
                        sample_size = handler.GetProcessedSampleSize(locator._sample);
                        locator._sampleTable.SetSampleSize(locator._sampleIndex, sample_size);
                    } else {
                        sample_size = locator._sample.GetSize();
                    }
                    current_chunk_size += sample_size;
                    mdat_payload_size  += sample_size;
                }

                // process the tracks (ex: sample descriptions processing)
                for (int i=0; i<track_count; i++) {
                    TrackHandler handler = _trackHandlers[i];
                    if (handler != null) handler.ProcessTrack();
                }
            }

            // finalize the processor
            Finalize(top_level);

            if (fragments == null) {
                // calculate the size of all atoms combined
                ulong atoms_size = 0;
                AtomSizeAdder size_adder = new AtomSizeAdder(atoms_size);
                top_level.GetChildren().Apply(size_adder);
                atoms_size = size_adder.GetSize();

                // see if we need a 64-bit or 32-bit mdat
                uint mdat_header_size = Ap4.ATOM_HEADER_SIZE;
                if (mdat_payload_size+mdat_header_size > 0xFFFFFFFF) {
                    // we need a 64-bit size
                    mdat_header_size += 8;
                }

                // adjust the chunk offsets
                for (int i=0; i<track_count; i++) {
                    TrakAtom trak = trak_atoms[i];
                    trak.AdjustChunkOffsets(unchecked((long)(atoms_size+mdat_header_size)));
                }

                // write all atoms
                top_level.GetChildren().Apply(new AtomListWriter(output));

                // write mdat header
                if (mdat_payload_size != 0) {
                    if (mdat_header_size == Ap4.ATOM_HEADER_SIZE) {
                        // 32-bit size
                        output.WriteUI32((uint)(mdat_header_size+mdat_payload_size));
                        output.WriteUI32(AtomType.MDAT);
                    } else {
                        // 64-bit size
                        output.WriteUI32(1);
                        output.WriteUI32(AtomType.MDAT);
                        output.WriteUI64(mdat_header_size+mdat_payload_size);
                    }
                }
            }

            // write the samples
            if (moov != null) {
                if (fragments == null) {
#if DEBUG
                    ulong before;
                    output.Tell(out before);
#endif
                    DataBuffer data_in = new DataBuffer();
                    DataBuffer data_out = new DataBuffer();
                    for (int i=0; i<locators.Count; i++) {
                        SampleLocator locator = locators[i];
                        locator._sample.ReadData(data_in);
                        TrackHandler handler = _trackHandlers[(int)locator._trakIndex];
                        if (handler != null) {
                            result = handler.ProcessSample(data_in, data_out);
                            if (result.Failed()) return result;
                            output.Write(data_out.GetData(), data_out.GetDataSize());
                        } else {
                            output.Write(data_in.GetData(), data_in.GetDataSize());
                        }

                        // notify the progress listener
                        if (listener != null) {
                            listener.OnProgress((uint)i+1, (uint)locators.Count);
                        }
                    }

#if DEBUG
                    ulong after;
                    output.Tell(out after);
                    Ap4.ASSERT(after-before == mdat_payload_size);
#endif
                }

                // find the position of the sidx atom
                ulong sidx_position = 0;
                if (sidx != null) {
                    foreach (Atom atom in top_level.GetChildren()) {
                        if (atom.GetType() == AtomType.SIDX) {
                            break;
                        }
                        sidx_position += atom.GetSize();
                    }
                }

                // process the fragments, if any
                result = ProcessFragments(moov, frags, mfra, sidx, sidx_position, fragments != null ? fragments : input, output);
                if (result.Failed()) return result;

                // update and re-write the sidx if we have one
                if (sidx != null && sidx_position != 0) {
                    ulong where = 0;
                    output.Tell(out where);
                    output.Seek(sidx_position);
                    result = sidx.Write(output);
                    if (result.Failed()) return result;
                    output.Seek(where);
                }

                if (fragments == null) {
                    // write the mfra atom at the end if we have one
                    if (mfra != null) {
                        mfra.Write(output);
                    }
                }

                // cleanup
                _trackHandlers.Clear();
            }

            // cleanup
            frags.Clear();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Processor::Process
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Process the input stream into an output stream.
        /// </summary>
        /// <param name="input">Input stream from which to read the input file.</param>
        /// <param name="output">Output stream to which the processed input
        /// will be written.</param>
        /// <param name="listener">Pointer to a listener, or NULL. The listener
        /// will be called one or more times before this method returns,
        /// with progress information.</param>
        /// <param name="atom_factory">Atom factory (null: DefaultAtomFactory.Instance)</param>
        public Ap4Result
        Process(ByteStream        input,
                ByteStream        output,
                ProgressListener  listener = null,
                AtomFactory       atom_factory = null)
        {
            return ProcessInternal(input, output, null, listener, atom_factory);
        }

        /*----------------------------------------------------------------------
        |   AP4_Processor::Process
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Process a fragment input stream into an output stream.
        /// </summary>
        /// <param name="fragments">Input stream from which to read the fragments.</param>
        /// <param name="output">Output stream to which the processed fragments
        /// will be written.</param>
        /// <param name="init">Input stream from which to read the init data.</param>
        /// <param name="listener">Pointer to a listener, or NULL. The listener
        /// will be called one or more times before this method returns,
        /// with progress information.</param>
        /// <param name="atom_factory">Atom factory (null: DefaultAtomFactory.Instance)</param>
        public Ap4Result
        Process(ByteStream        fragments,
                ByteStream        output,
                ByteStream        init,
                ProgressListener  listener = null,
                AtomFactory       atom_factory = null)
        {
            return ProcessInternal(init, output, fragments, listener, atom_factory);
        }

        /*----------------------------------------------------------------------
        |   AP4_Processor:Initialize
        +---------------------------------------------------------------------*/
        /// <summary>
        /// This method can be overridden by concrete subclasses.
        /// It is called just after the input stream has been parsed into
        /// an atom tree, before the processing of the tracks.
        /// </summary>
        /// <param name="top_level">Container atom containing all the atoms parsed
        /// from the input stream. Note that this atom does not actually
        /// exist in the file; it is a synthetised container created for the
        /// purpose of holding together all the input's top-level atoms.</param>
        /// <param name="stream">Input stream.</param>
        /// <param name="listener">Pointer to a listener, or NULL.</param>
        public virtual Ap4Result
        Initialize(AtomParent        top_level,
                   ByteStream        stream,
                   ProgressListener  listener = null)
        {
            // default implementation: do nothing
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Processor:Finalize
        +---------------------------------------------------------------------*/
        /// <summary>
        /// This method can be overridden by concrete subclasses.
        /// It is called just after the tracks have been processed.
        /// </summary>
        public virtual Ap4Result
        Finalize(AtomParent        top_level,
                 ProgressListener  listener = null)
        {
            // default implementation: do nothing
            return Ap4Result.SUCCESS;
        }

        /// <summary>
        /// This method can be overridden by concrete subclasses.
        /// It is called once for each track in the input file.
        /// </summary>
        /// <param name="trak">Pointer to the track for which a handler should be
        /// created.</param>
        /// <returns>A pointer to a track handler, or NULL if no handler
        /// needs to be created for that track.</returns>
        public virtual TrackHandler CreateTrackHandler(TrakAtom trak) { return null; }

        protected class ExternalTrackData {
            public ExternalTrackData(uint track_id, ByteStream media_data) {
                _trackId = track_id;
                _mediaData = media_data;
            }
            public uint       _trackId;
            public ByteStream _mediaData;
        }

        // PORT NOTE: helper for AP4_Array::SetItemCount
        private static void SetItemCount<T>(List<T> list, uint item_count, T fill)
        {
            if (item_count < list.Count) {
                list.RemoveRange((int)item_count, list.Count-(int)item_count);
            } else {
                while (list.Count < item_count) {
                    list.Add(fill);
                }
            }
        }

        protected List<ExternalTrackData> _externalTrackData = new List<ExternalTrackData>();
        protected List<uint>              _trackIds = new List<uint>();
        protected List<TrackHandler>     _trackHandlers = new List<TrackHandler>();
    }
}
