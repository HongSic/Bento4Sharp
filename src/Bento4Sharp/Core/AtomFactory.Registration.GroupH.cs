// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.cpp (the cases of
// AP4_AtomFactory::CreateAtomFromStream for the classes of Ap4OdheAtom,
// Ap4OhdrAtom, Ap4OddaAtom, Ap4OdafAtom, Ap4GrpiAtom, Ap4IproAtom and Ap4Marlin)
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public partial class AtomFactory
    {
        static partial void RegisterGroupH(AtomRegistry r)
        {
            r.Atom(AtomType.ODHE, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(OdheAtom.Create(c.Size32, c.Stream, c.Factory)));

            r.Atom(AtomType.OHDR, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(OhdrAtom.Create(c.Size32, c.Stream, c.Factory)));

            r.Atom(AtomType.ODDA, (c) => c.Ok(OddaAtom.Create(c.Size64, c.Stream)));

            r.Atom(AtomType.ODAF, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(OdafAtom.Create(c.Size32, c.Stream)));

            r.Atom(AtomType.GRPI, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(GrpiAtom.Create(c.Size32, c.Stream)));

            r.Atom(AtomType.IPRO, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(IproAtom.Create(c.Size32, c.Stream, c.Factory)));

            r.Atom(AtomType.MKID, (c) => {
                if (c.IsLarge) return AtomRegistry.InvalidFormat;
                if (c.GetContext() == AtomType.MARL) {
                    return c.Ok(MkidAtom.Create(c.Size32, c.Stream));
                }
                return AtomRegistry.Fallback;
            });
        }
    }
}
