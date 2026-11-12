// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.cpp (cases owned by group E6)
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public partial class AtomFactory
    {
        static partial void RegisterGroupE6(AtomRegistry r)
        {
            // PORT NOTE: nothing to register here.
            //
            // The only factory case related to this group is, in the 'stsd' context:
            //
            //     case AP4_ATOM_TYPE_RTP_:
            //         atom = new AP4_RtpHintSampleEntry(size_32, stream, *this);
            //         break;
            //
            // AP4_RtpHintSampleEntry is declared in Ap4SampleEntry.h (not in
            // Ap4RtpHint.h), so it was ported in Core/SampleEntry.cs and that case is
            // already registered by group D (AtomFactory.Registration.GroupD.cs):
            //
            //     r.SampleEntry(AtomType.RTP_, (c) => c.Ok(new RtpHintSampleEntry(c.Size32, c.Stream, c.Factory)));
            //
            // Registering it again would throw (duplicate type) in the static
            // constructor of AtomFactory. Ap4RtpHint and Ap4HintTrackReader define
            // no atoms of their own.
        }
    }
}
