// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.cpp (AP4_AtomFactory::CreateAtomFromStream, group A cases)
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public partial class AtomFactory
    {
        static partial void RegisterGroupA(AtomRegistry r)
        {
            r.Atom(AtomType.MOOV, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(MoovAtom.Create(c.Size32, c.Stream, c.Factory)));
            r.Atom(AtomType.MVHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(MvhdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.TRAK, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TrakAtom.Create(c.Size32, c.Stream, c.Factory)));
            r.Atom(AtomType.HDLR, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(HdlrAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.TKHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TkhdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.MDHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(MdhdAtom.Create(c.Size32, c.Stream)));

            // case AP4_ATOM_TYPE_UUID (no atom_is_large check upstream)
            r.Atom(AtomType.UUID, (c) => {
                byte[] uuid = new byte[16];
                Ap4Result result = c.Stream.Read(uuid, 16);
                if (result.Failed()) return c.Fail(result);

#if NET6_0_OR_GREATER
                if (System.MemoryExtensions.SequenceEqual<byte>(uuid, Ap4.UUID_PIFF_TRACK_ENCRYPTION_ATOM)) {
#else
                if (Compat.SequenceEqual(uuid, Ap4.UUID_PIFF_TRACK_ENCRYPTION_ATOM)) {
#endif
                    return c.Ok(PiffTrackEncryptionAtom.Create((uint)c.Size64, c.Stream));
#if NET6_0_OR_GREATER
                } else if (System.MemoryExtensions.SequenceEqual<byte>(uuid, Ap4.UUID_PIFF_SAMPLE_ENCRYPTION_ATOM)) {
#else
                } else if (Compat.SequenceEqual(uuid, Ap4.UUID_PIFF_SAMPLE_ENCRYPTION_ATOM)) {
#endif
                    return c.Ok(PiffSampleEncryptionAtom.Create((uint)c.Size64, c.Stream));
                } else {
                    return c.Ok(new UnknownUuidAtom(c.Size64, uuid, c.Stream));
                }
            });

            r.Atom(AtomType._8BDL, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(_8bdlAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.DREF, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(DrefAtom.Create(c.Size32, c.Stream, c.Factory)));
            r.Atom(AtomType.URL,  (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(UrlAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.ELST, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(ElstAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.VMHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(VmhdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.SMHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SmhdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.NMHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(NmhdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.STHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SthdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.HMHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(HmhdAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.FTYP, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(FtypAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.TIMS, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TimsAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.SDP_, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SdpAtom.Create(c.Size32, c.Stream)));
            // NOTE: in the 'stsd' context 'rtp ' is a sample entry (AP4_RtpHintSampleEntry),
            // registered by the group that ports it.
            r.Atom(AtomType.RTP_, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(RtpAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.PDIN, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(PdinAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.BLOC, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(BlocAtom.Create(c.Size32, c.Stream)));
            r.Atom(AtomType.AINF, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(AinfAtom.Create(c.Size32, c.Stream)));

            // track ref types
            r.Atoms(new uint[] { AtomType.HINT, AtomType.CDSC, AtomType.SYNC, AtomType.MPOD,
                                 AtomType.DPND, AtomType.IPIR, AtomType.ALIS, AtomType.CHAP }, (c) => {
                if (c.GetContext() == AtomType.TREF) {
                    if (c.IsLarge) return AtomRegistry.InvalidFormat;
                    return c.Ok(TrefTypeAtom.Create(c.Type, c.Size32, c.Stream));
                }
                return AtomRegistry.Fallback;
            });

            r.Atom(AtomType.COLR, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(ColrAtom.Create(c.Size32, c.Stream)));
        }
    }
}
