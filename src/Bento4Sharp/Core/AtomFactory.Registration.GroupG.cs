// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.cpp (cases of the Processor / ISMA atom classes)
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public partial class AtomFactory
    {
        static partial void RegisterGroupG(AtomRegistry r)
        {
            r.Atom(AtomType.IKMS, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(IkmsAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.ISFM, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(IsfmAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.ISLT, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(IsltAtom.Create(c.Size32, c.Stream)));
        }
    }
}
