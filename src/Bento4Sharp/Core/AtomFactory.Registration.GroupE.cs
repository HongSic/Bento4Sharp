// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.cpp (the cases of
// AP4_AtomFactory::CreateAtomFromStream for the classes of Ap4Protection)
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public partial class AtomFactory
    {
        static partial void RegisterGroupE(AtomRegistry r)
        {
            /*----------------------------------------------------------------------
            |   sample entries (GetContext() == AP4_ATOM_TYPE_STSD)
            +---------------------------------------------------------------------*/
            r.SampleEntry(AtomType.ENCA, (c) => c.Ok(new EncaSampleEntry(c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.ENCV, (c) => c.Ok(new EncvSampleEntry(c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.DRMS, (c) => c.Ok(new DrmsSampleEntry(c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.DRMI, (c) => c.Ok(new DrmiSampleEntry(c.Size32, c.Stream, c.Factory)));
        }
    }
}
