// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Movie.h, Ap4Movie.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_TrackFinderById
    +---------------------------------------------------------------------*/
    internal class TrackFinderById : IListFinder<Track>
    {
        public TrackFinderById(uint track_id) { _trackId = track_id; }
        public Ap4Result Test(Track track) {
            return track.GetId() == _trackId ? Ap4Result.SUCCESS : Ap4Result.FAILURE;
        }
        private uint _trackId;
    }

    /*----------------------------------------------------------------------
    |   AP4_TrackFinderByType
    +---------------------------------------------------------------------*/
    internal class TrackFinderByType : IListFinder<Track>
    {
        public TrackFinderByType(Track.Type type, uint index = 0) {
            _type = type;
            _index = index;
        }
        public Ap4Result Test(Track track) {
            if (track.GetType() == _type && _index-- == 0) {
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.FAILURE;
            }
        }
        private Track.Type _type;
        private uint       _index;
    }

    /*----------------------------------------------------------------------
    |   AP4_Movie
    +---------------------------------------------------------------------*/
    public class Movie
    {
        /*----------------------------------------------------------------------
        |   AP4_Movie::AP4_Movie
        +---------------------------------------------------------------------*/
        public Movie(uint  time_scale = 0,
                     ulong duration = 0,
                     ulong creation_time = 0,
                     ulong modification_time = 0)
        {
            _moovAtomIsOwned = true;
            _moovAtom = new MoovAtom();
            _mvhdAtom = new MvhdAtom(creation_time,
                                     modification_time,
                                     time_scale,
                                     duration,
                                     0x00010000,
                                     0x0100);
            _moovAtom.AddChild(_mvhdAtom);
        }

        /*----------------------------------------------------------------------
        |   AP4_Movie::AP4_Movie
        +---------------------------------------------------------------------*/
        public Movie(MoovAtom moov, ByteStream sample_stream, bool transfer_moov_ownership = true)
        {
            _moovAtom = moov;
            _moovAtomIsOwned = transfer_moov_ownership;

            // ignore null atoms
            if (moov == null) return;

            // get the time scale
            uint time_scale;
            _mvhdAtom = moov.GetChild(AtomType.MVHD) as MvhdAtom;
            if (_mvhdAtom != null) {
                time_scale = _mvhdAtom.GetTimeScale();
            } else {
                time_scale = 0;
            }

            // get all tracks
            List<TrakAtom> trak_atoms;
            trak_atoms = moov.GetTrakAtoms();
            for (int item = 0; item < trak_atoms.Count; item++) {
                Track track = new Track(trak_atoms[item],
                                        sample_stream,
                                        time_scale);
                _tracks.Add(track);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Movie::Inspect
        +---------------------------------------------------------------------*/
        public Ap4Result Inspect(AtomInspector inspector)
        {
            // dump the moov atom
            return _moovAtom.Inspect(inspector);
        }

        public MoovAtom GetMoovAtom() { return _moovAtom;}
        public MvhdAtom GetMvhdAtom() { return _mvhdAtom;}
        public List<Track> GetTracks() { return _tracks; }

        /*----------------------------------------------------------------------
        |   AP4_Movie::GetTrack
        +---------------------------------------------------------------------*/
        public Track GetTrack(uint track_id)
        {
            Track track = null;
            if (_tracks.Find(new TrackFinderById(track_id), out track).Succeeded()) {
                return track;
            } else {
                return null;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Movie::GetTrack
        +---------------------------------------------------------------------*/
        public Track GetTrack(Track.Type track_type, uint index = 0)
        {
            Track track = null;
            if (_tracks.Find(new TrackFinderByType(track_type, index), out track).Succeeded()) {
                return track;
            } else {
                return null;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Movie::AddTrack
        +---------------------------------------------------------------------*/
        public Ap4Result AddTrack(Track track)
        {
            // assign an ID to the track unless it already has one
            if (track.GetId() == 0) {
                track.SetId((uint)_tracks.Count+1);
            }

            // if we don't have a time scale, use the one from the track
            if (_mvhdAtom.GetTimeScale() == 0) {
                _mvhdAtom.SetTimeScale(track.GetMediaTimeScale());
            }

            // adjust the parent time scale of the track
            track.SetMovieTimeScale(_mvhdAtom.GetTimeScale());

            // update the movie duration
            if (_mvhdAtom.GetDuration() < track.GetDuration()) {
                _mvhdAtom.SetDuration(track.GetDuration());
            }

            // attach the track as a child
            track.Attach(_moovAtom);
            _tracks.Add(track);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Movie::GetTimeScale
        +---------------------------------------------------------------------*/
        public uint GetTimeScale()
        {
            if (_mvhdAtom != null) {
                return _mvhdAtom.GetTimeScale();
            } else {
                return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Movie::GetDuration
        +---------------------------------------------------------------------*/
        public ulong GetDuration()
        {
            if (_mvhdAtom != null) {
                return _mvhdAtom.GetDuration();
            } else {
                return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Movie::GetDurationMs
        +---------------------------------------------------------------------*/
        public uint GetDurationMs()
        {
            if (_mvhdAtom != null) {
                return _mvhdAtom.GetDurationMs();
            } else {
                return 0;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Movie::HasFragments
        +---------------------------------------------------------------------*/
        public bool HasFragments()
        {
            if (_moovAtom == null) return false;
            if (_moovAtom.GetChild(AtomType.MVEX) != null) {
                return true;
            } else {
                return false;
            }
        }

        // members
        private MoovAtom    _moovAtom;
        private bool        _moovAtomIsOwned;
        // PORT NOTE: upstream leaves m_MvhdAtom uninitialized when the movie is
        // constructed from a NULL moov atom; here it is null.
        private MvhdAtom    _mvhdAtom;
        private List<Track> _tracks = new List<Track>();
    }
}
