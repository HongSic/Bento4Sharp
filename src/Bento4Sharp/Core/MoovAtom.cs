// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4MoovAtom.h, Ap4MoovAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_TrakAtomCollector
    +---------------------------------------------------------------------*/
    internal class TrakAtomCollector : IListOperator<Atom>
    {
        public TrakAtomCollector(List<TrakAtom> track_atoms) {
            _trakAtoms = track_atoms;
        }

        public Ap4Result Action(Atom atom) {
            if (atom.GetType() == AtomType.TRAK) {
                TrakAtom trak = atom as TrakAtom;
                if (trak != null) {
                    _trakAtoms.Add(trak);
                }
            }
            return Ap4Result.SUCCESS;
        }

        private List<TrakAtom> _trakAtoms;
    }

    /*----------------------------------------------------------------------
    |   AP4_MoovAtom
    +---------------------------------------------------------------------*/
    public class MoovAtom : ContainerAtom
    {
        // class methods
        public static MoovAtom Create(uint        size,
                                       ByteStream  stream,
                                       AtomFactory atom_factory) {
            return new MoovAtom(size, stream, atom_factory);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MoovAtom::AP4_MoovAtom
        +---------------------------------------------------------------------*/
        public MoovAtom() :
            base(AtomType.MOOV)
        {
            _timeScale = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_MoovAtom::AP4_MoovAtom
        +---------------------------------------------------------------------*/
        private MoovAtom(uint        size,
                         ByteStream  stream,
                         AtomFactory atom_factory) :
            base(AtomType.MOOV, size, false, stream, atom_factory)
        {
            _timeScale = 0;

            // collect all trak atoms
            _children.Apply(new TrakAtomCollector(_trakAtoms));
        }

        public List<TrakAtom> GetTrakAtoms() {
            return _trakAtoms;
        }
        public uint GetTimeScale() {
            return _timeScale;
        }

        /*----------------------------------------------------------------------
        |   AP4_MoovAtom::AdjustChunkOffsets
        +---------------------------------------------------------------------*/
        public Ap4Result AdjustChunkOffsets(long offset)
        {
            foreach (TrakAtom trak in _trakAtoms) {
                trak.AdjustChunkOffsets(offset);
            }

            return Ap4Result.SUCCESS;
        }

        // AP4_AtomParent methods
        /*----------------------------------------------------------------------
        |   AP4_MoovAtom::OnChildAdded
        +---------------------------------------------------------------------*/
        public override void OnChildAdded(Atom atom)
        {
            // keep the atom in the list of trak atoms
            if (atom.GetType() == AtomType.TRAK) {
                TrakAtom trak = atom as TrakAtom;
                if (trak != null) {
                    _trakAtoms.Add(trak);
                }
            }

            // call the base class implementation
            base.OnChildAdded(atom);
        }

        /*----------------------------------------------------------------------
        |   AP4_MoovAtom::OnChildRemoved
        +---------------------------------------------------------------------*/
        public override void OnChildRemoved(Atom atom)
        {
            // remove the atom from the list of trak atoms
            if (atom.GetType() == AtomType.TRAK) {
                TrakAtom trak = atom as TrakAtom;
                if (trak != null) {
                    _trakAtoms.Remove(trak);
                }
            }

            // call the base class implementation
            base.OnChildRemoved(atom);
        }

        // members
        // PORT NOTE: the field initializer runs before the base class constructor,
        // so the list exists by the time the children are read.
        private List<TrakAtom> _trakAtoms = new List<TrakAtom>();
        private uint           _timeScale;
    }
}
