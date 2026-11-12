// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.h, Ap4AtomFactory.cpp
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_AtomFactory::CreateAtomFromStream cases of group C1
    |   (codec configuration atoms)
    +---------------------------------------------------------------------*/
    public partial class AtomFactory
    {
        static partial void RegisterGroupC1(AtomRegistry r)
        {
            r.Atom(AtomType.AVCC, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                return c.Ok(AvccAtom.Create(c.Size32, c.Stream));
            });

            r.Atom(AtomType.HVCC, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                return c.Ok(HvccAtom.Create(c.Size32, c.Stream));
            });

            r.Atoms(new uint[] { AtomType.DVCC, AtomType.DVVC }, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                return c.Ok(DvccAtom.Create(c.Size32, c.Stream));
            });

            r.Atom(AtomType.VPCC, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                return c.Ok(VpccAtom.Create(c.Size32, c.Stream));
            });

            r.Atom(AtomType.AV1C, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                return c.Ok(Av1cAtom.Create(c.Size32, c.Stream));
            });

            r.Atom(AtomType.HVCE, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                Atom atom = HvccAtom.Create(c.Size32, c.Stream);
                if (atom != null) {
                    atom.SetType(AtomType.HVCE);
                }
                return c.Ok(atom);
            });

            r.Atom(AtomType.AVCE, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                Atom atom = AvccAtom.Create(c.Size32, c.Stream);
                if (atom != null) {
                    atom.SetType(AtomType.AVCE);
                }
                return c.Ok(atom);
            });

            r.Atom(AtomType.DAC3, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                if (c.GetContext() == AtomType.AC_3 || c.GetContext() == AtomType.ENCA) {
                    return c.Ok(Dac3Atom.Create(c.Size32, c.Stream));
                }
                return AtomRegistry.Fallback;
            });

            r.Atom(AtomType.DEC3, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                if (c.GetContext() == AtomType.EC_3 || c.GetContext() == AtomType.ENCA) {
                    return c.Ok(Dec3Atom.Create(c.Size32, c.Stream));
                }
                return AtomRegistry.Fallback;
            });

            r.Atom(AtomType.DAC4, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                if (c.GetContext() == AtomType.AC_4 || c.GetContext() == AtomType.ENCA) {
                    return c.Ok(Dac4Atom.Create(c.Size32, c.Stream));
                }
                return AtomRegistry.Fallback;
            });

            r.Atom(AtomType.DMLP, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                if (c.GetContext() == AtomType.MLPA || c.GetContext() == AtomType.ENCA) {
                    return c.Ok(DmlpAtom.Create(c.Size32, c.Stream));
                }
                return AtomRegistry.Fallback;
            });
        }
    }
}
