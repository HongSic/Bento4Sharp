// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4FileWriter.h, Ap4FileWriter.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_FileWriter
    +---------------------------------------------------------------------*/
    public class FileWriter
    {
        // types
        public enum Interleaving {
            INTERLEAVING_SEQUENTIAL
        }

        /*----------------------------------------------------------------------
        |   AP4_FileWriter::Write
        +---------------------------------------------------------------------*/
        public static Ap4Result Write(File         file,
                                      ByteStream   stream,
                                      Interleaving interleaving = Interleaving.INTERLEAVING_SEQUENTIAL)
        {
            // get the file type
            FtypAtom file_type = file.GetFileType();

            // write the ftyp atom (always first)
            if (file_type != null) file_type.Write(stream);

            // write the top-level atoms, except for ftyp, moov and mdat
            List<Atom> children = file.GetChildren();
            for (int atom_item = 0; atom_item < children.Count; atom_item++) {
                Atom atom = children[atom_item];
                if (atom.GetType() != AtomType.MDAT &&
                    atom.GetType() != AtomType.FTYP &&
                    atom.GetType() != AtomType.MOOV) {
                    atom.Write(stream);
                }
            }

            // get the movie object (and stop if there isn't any)
            Movie  movie = file.GetMovie();
            if (movie == null) return Ap4Result.SUCCESS;

            // see how much we've written so far
            ulong position;
            stream.Tell(out position);

            // compute the total size of all the samples
            ulong total_sample_size = 0;
            List<Track> tracks = movie.GetTracks();
            for (int track_item = 0; track_item < tracks.Count; track_item++) {
                Track  track = tracks[track_item];
                uint   sample_count = track.GetSampleCount();
                Sample sample = new Sample();
                for (uint i=0; i<sample_count; i++) {
                    track.GetSample(i, sample);
                    total_sample_size += sample.GetSize();
                }
            }

            // decide if we need a 32-bit or 64-bit mdat header
            ulong mdat_size;
            bool mdat_is_large;
            if (total_sample_size <= 0xFFFFFFFF - Ap4.ATOM_HEADER_SIZE) {
                mdat_size = Ap4.ATOM_HEADER_SIZE;
                mdat_is_large = false;
            } else {
                mdat_size = 16;
                mdat_is_large = true;
            }

            // backup and recompute all the chunk offsets
            int t=0;
            Ap4Result result = Ap4Result.SUCCESS;
            ulong     mdat_position = position+movie.GetMoovAtom().GetSize();
            List<List<ulong>> trak_chunk_offsets_backup = new List<List<ulong>>();
            List<ulong>       chunk_offsets = new List<ulong>();
            for (int track_item = 0; track_item < tracks.Count; track_item++) {
                Track    track = tracks[track_item];
                TrakAtom trak  = track.UseTrakAtom();

                // backup the chunk offsets
                List<ulong> chunk_offsets_backup = new List<ulong>();
                trak_chunk_offsets_backup.Add(chunk_offsets_backup);
                result = trak.GetChunkOffsets(chunk_offsets_backup);
                if (result.Failed()) goto end;

                // allocate space for the new chunk offsets
                // (AP4_Array::SetItemCount: existing items are kept, new items are zero)
                if (chunk_offsets.Count > chunk_offsets_backup.Count) {
                    chunk_offsets.RemoveRange(chunk_offsets_backup.Count, chunk_offsets.Count-chunk_offsets_backup.Count);
                }
                while (chunk_offsets.Count < chunk_offsets_backup.Count) {
                    chunk_offsets.Add(0);
                }

                // compute the new chunk offsets
                uint         sample_count = track.GetSampleCount();
                SampleTable  sample_table = track.GetSampleTable();
                Sample       sample = new Sample();
                for (uint i=0; i<sample_count; i++) {
                    uint chunk_index = 0;
                    uint position_in_chunk = 0;
                    sample_table.GetSampleChunkPosition(i, out chunk_index, out position_in_chunk);
                    sample_table.GetSample(i, sample);
                    if (position_in_chunk == 0) {
                        // this sample is the first sample in a chunk, so this is the start of a chunk
                        if (chunk_index >= (uint)chunk_offsets.Count) return Ap4Result.ERROR_INTERNAL;
                        chunk_offsets[(int)chunk_index] = mdat_position+mdat_size;
                    }
                    mdat_size += sample.GetSize();
                }
                result = trak.SetChunkOffsets(chunk_offsets);
            }

            // write the moov atom
            movie.GetMoovAtom().Write(stream);

            // create and write the media data (mdat)
            if (mdat_is_large) {
                stream.WriteUI32(1);
                stream.WriteUI32(AtomType.MDAT);
                stream.WriteUI64(mdat_size);
            } else {
                stream.WriteUI32((uint)mdat_size);
                stream.WriteUI32(AtomType.MDAT);
            }

            // write all tracks and restore the chunk offsets to their backed-up values
            for (int track_item = 0; track_item < tracks.Count; track_item++, ++t) {
                Track    track = tracks[track_item];
                TrakAtom trak  = track.UseTrakAtom();

                // restore the backed-up chunk offsets
                result = trak.SetChunkOffsets(trak_chunk_offsets_backup[t]);

                // write all the track's samples
                uint       sample_count = track.GetSampleCount();
                Sample     sample = new Sample();
                DataBuffer sample_data = new DataBuffer();
                for (uint i=0; i<sample_count; i++) {
                    track.ReadSample(i, sample, sample_data);
                    stream.Write(sample_data.GetData(), sample_data.GetDataSize());
                }
            }

        end:
            return result;
        }

        // don't instantiate this class
        private FileWriter() {}
    }
}
