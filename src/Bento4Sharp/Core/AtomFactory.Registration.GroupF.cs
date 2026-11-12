// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.cpp (AP4_AtomFactory::CreateAtomFromStream, common encryption atom cases)
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public partial class AtomFactory
    {
        static partial void RegisterGroupF(AtomRegistry r)
        {
            r.Atom(AtomType.TENC, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TencAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.SENC, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SencAtom.Create(c.Size32, c.Stream)));

            // NOTE: the PIFF 'uuid' atoms (AP4_PiffTrackEncryptionAtom, AP4_PiffSampleEncryptionAtom)
            // are dispatched by the AP4_ATOM_TYPE_UUID case, which is registered in group A.
        }
    }
}
