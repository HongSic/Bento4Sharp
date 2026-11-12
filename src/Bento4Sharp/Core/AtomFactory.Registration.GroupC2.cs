// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.h, Ap4AtomFactory.cpp
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_AtomFactory::CreateAtomFromStream (cases of group C2)
    +---------------------------------------------------------------------*/
    public partial class AtomFactory
    {
        static partial void RegisterGroupC2(AtomRegistry r)
        {
            r.Atom(AtomType.IODS, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(IodsAtom.Create(c.Size32, c.Stream)));

            r.Atom(AtomType.ESDS, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(EsdsAtom.Create(c.Size32, c.Stream)));

            r.Atom(AtomType.SAIZ, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SaizAtom.Create(c.Size32, c.Stream)));

            r.Atom(AtomType.SAIO, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SaioAtom.Create(c.Size32, c.Stream)));

            r.Atom(AtomType.PSSH, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(PsshAtom.Create(c.Size32, c.Stream)));
        }
    }
}
