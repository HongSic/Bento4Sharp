// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4MovieFragment.h, Ap4MovieFragment.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint FRAG_FLAG_SAMPLE_IS_DIFFERENCE = 0x00010000;
    }

    /*----------------------------------------------------------------------
    |   AP4_MovieFragment
    +---------------------------------------------------------------------*/
    public class MovieFragment
    {
        /*----------------------------------------------------------------------
        |   AP4_MovieFragment::AP4_MovieFragment
        +---------------------------------------------------------------------*/
        // this constructor transfers the ownership of the moof atom to the
        // newly constructed object
        public MovieFragment(ContainerAtom moof)
        {
            _moofAtom = moof;
            _mfhdAtom = null;
            if (moof != null) _mfhdAtom = moof.GetChild(AtomType.MFHD) as MfhdAtom;
        }

        public ContainerAtom  GetMoofAtom() { return _moofAtom;}
        public MfhdAtom       GetMfhdAtom() { return _mfhdAtom;}

        /*----------------------------------------------------------------------
        |   AP4_MovieFragment::GetSequenceNumber
        +---------------------------------------------------------------------*/
        public uint GetSequenceNumber()
        {
            return _mfhdAtom != null ? _mfhdAtom.GetSequenceNumber() : 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_MovieFragment::GetTrackIds
        +---------------------------------------------------------------------*/
        public Ap4Result GetTrackIds(List<uint> ids)
        {
            ids.Clear();
            List<Atom> children = _moofAtom.GetChildren();
            ids.EnsureCapacity(children.Count);

            for (int item = 0; item < children.Count; item++) {
                Atom atom = children[item];
                if (atom.GetType() == AtomType.TRAF) {
                    ContainerAtom traf = atom as ContainerAtom;
                    if (traf != null) {
                        TfhdAtom tfhd = traf.GetChild(AtomType.TFHD) as TfhdAtom;
                        if (tfhd != null) ids.Add(tfhd.GetTrackId());
                    }
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MovieFragment::GetTrafAtom
        +---------------------------------------------------------------------*/
        public Ap4Result GetTrafAtom(uint track_id, out ContainerAtom traf)
        {
            List<Atom> children = _moofAtom.GetChildren();
            for (int item = 0; item < children.Count; item++) {
                Atom atom = children[item];
                if (atom.GetType() == AtomType.TRAF) {
                    traf = atom as ContainerAtom;
                    if (traf != null) {
                        TfhdAtom tfhd = traf.GetChild(AtomType.TFHD) as TfhdAtom;
                        if (tfhd != null && tfhd.GetTrackId() == track_id) {
                            return Ap4Result.SUCCESS;
                        }
                    }
                }
            }

            // not found
            traf = null;
            return Ap4Result.ERROR_NO_SUCH_ITEM;
        }

        /*----------------------------------------------------------------------
        |   AP4_MovieFragment::CreateSampleTable
        +---------------------------------------------------------------------*/
        public Ap4Result CreateSampleTable(MoovAtom                 moov,
                                           uint                     track_id,
                                           ByteStream               sample_stream,
                                           ulong                    moof_offset,
                                           ulong                    mdat_payload_offset, // hack because MS doesn't implement the spec properly
                                           ulong                    dts_origin,
                                           out FragmentSampleTable  sample_table)
        {
            // default value
            sample_table = null;

            // find a trex for this track, if any
            ContainerAtom  mvex = null;
            TrexAtom       trex = null;
            if (moov != null) {
                mvex = moov.GetChild(AtomType.MVEX) as ContainerAtom;
            }
            if (mvex != null) {
                List<Atom> children = mvex.GetChildren();
                for (int item = 0; item < children.Count; item++) {
                    Atom atom = children[item];
                    if (atom.GetType() == AtomType.TREX) {
                        trex = atom as TrexAtom;
                        if (trex != null && trex.GetTrackId() == track_id) break;
                        trex = null;
                    }
                }
            }
            ContainerAtom traf = null;
            if (GetTrafAtom(track_id, out traf).Succeeded()) {
                sample_table = new FragmentSampleTable(traf,
                                                       trex,
                                                       sample_stream,
                                                       moof_offset,
                                                       mdat_payload_offset,
                                                       dts_origin);
                return Ap4Result.SUCCESS;
            }

            return Ap4Result.ERROR_NO_SUCH_ITEM;
        }

        /*----------------------------------------------------------------------
        |   AP4_MovieFragment::CreateSampleTable
        +---------------------------------------------------------------------*/
        public Ap4Result CreateSampleTable(Movie                    movie,
                                           uint                     track_id,
                                           ByteStream               sample_stream,
                                           ulong                    moof_offset,
                                           ulong                    mdat_payload_offset, // hack because MS doesn't implement the spec properly
                                           ulong                    dts_origin,
                                           out FragmentSampleTable  sample_table)
        {
            MoovAtom moov = movie != null ? movie.GetMoovAtom() : null;
            return CreateSampleTable(moov, track_id, sample_stream, moof_offset, mdat_payload_offset, dts_origin, out sample_table);
        }

        // members
        private ContainerAtom  _moofAtom;
        private MfhdAtom       _mfhdAtom;
    }
}
