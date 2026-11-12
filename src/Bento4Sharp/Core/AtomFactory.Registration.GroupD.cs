// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.cpp (the cases of
// AP4_AtomFactory::CreateAtomFromStream for the classes of Ap4SampleEntry,
// Ap4FrmaAtom and Ap4SchmAtom)
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public partial class AtomFactory
    {
        static partial void RegisterGroupD(AtomRegistry r)
        {
            /*----------------------------------------------------------------------
            |   sample entries (GetContext() == AP4_ATOM_TYPE_STSD)
            +---------------------------------------------------------------------*/
            r.SampleEntry(AtomType.MP4A, (c) => c.Ok(new Mp4aSampleEntry(c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.MP4V, (c) => c.Ok(new Mp4vSampleEntry(c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.MP4S, (c) => c.Ok(new Mp4sSampleEntry(c.Size32, c.Stream, c.Factory)));

            // PORT NOTE: the ENCA, ENCV, DRMS and DRMI cases create classes of
            // Ap4Protection; they are registered in AtomFactory.Registration.GroupE.cs.

            r.SampleEntries(new uint[] {
                AtomType.AVC1,
                AtomType.AVC2,
                AtomType.AVC3,
                AtomType.AVC4,
                AtomType.DVAV,
                AtomType.DVA1
            }, (c) => c.Ok(new AvcSampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            r.SampleEntries(new uint[] {
                AtomType.HEV1,
                AtomType.HVC1,
                AtomType.DVHE,
                AtomType.DVH1
            }, (c) => c.Ok(new HevcSampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.AV01, (c) => c.Ok(new Av1SampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.AC_3, (c) => c.Ok(new Ac3SampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.EC_3, (c) => c.Ok(new Eac3SampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.AC_4, (c) => c.Ok(new Ac4SampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.MLPA, (c) => c.Ok(new MlpSampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            r.SampleEntries(new uint[] {
                AtomType.ALAC,
                AtomType.DTSC,
                AtomType.DTSH,
                AtomType.DTSL,
                AtomType.DTSE,
                AtomType.FLAC,
                AtomType.OPUS
            }, (c) => c.Ok(new AudioSampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            r.SampleEntries(new uint[] {
                AtomType.VP08,
                AtomType.VP09,
                AtomType.VP10
            }, (c) => c.Ok(new VisualSampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.RTP_, (c) => c.Ok(new RtpHintSampleEntry(c.Size32, c.Stream, c.Factory)));

            r.SampleEntry(AtomType.STPP, (c) => c.Ok(new SubtitleSampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));

            // default: no custom handler, create a generic entry
            // (`atom = new AP4_UnknownSampleEntry(type, (AP4_UI32)size_64, stream);`)
            AtomFactory.UnknownSampleEntryCreator = (type, size, stream) => new UnknownSampleEntry(type, size, stream);

            /*----------------------------------------------------------------------
            |   regular atoms
            +---------------------------------------------------------------------*/
            r.Atom(AtomType.FRMA, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(FrmaAtom.Create(c.Size32, c.Stream)));

            r.Atom(AtomType.SCHM, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SchmAtom.Create(c.Size32, c.ContextStack, c.Stream)));
        }
    }
}
