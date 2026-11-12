// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.cpp (AP4_AtomFactory::CreateAtomFromStream,
//              the cases of the group B atom classes)
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public partial class AtomFactory
    {
        static partial void RegisterGroupB(AtomRegistry r)
        {
            r.Atom(AtomType.MEHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(MehdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.MFHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(MfhdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.TREX, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TrexAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.TFHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TfhdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.TRUN, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TrunAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.TFRA, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TfraAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.MFRO, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(MfroAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.STSD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(StsdAtom.Create(c.Size32, c.Stream, c.Factory)));
            r.Atom(AtomType.STSC, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(StscAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.STCO, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(StcoAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.CO64, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(Co64Atom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.STSZ, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(StszAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.STZ2, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(Stz2Atom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.STTS, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SttsAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.CTTS, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(CttsAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.STSS, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(StssAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.TFDT, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TfdtAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.SIDX, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SidxAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.SBGP, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SbgpAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.SGPD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SgpdAtom.Create(c.Size32, c.Stream)));
        }
    }
}
