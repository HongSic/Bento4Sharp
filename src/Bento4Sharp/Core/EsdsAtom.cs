// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4EsdsAtom.h, Ap4EsdsAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_EsdsAtom
    +---------------------------------------------------------------------*/
    public class EsdsAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_EsdsAtom::Create
        +---------------------------------------------------------------------*/
        public static EsdsAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new EsdsAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_EsdsAtom::AP4_EsdsAtom
        +---------------------------------------------------------------------*/
        public EsdsAtom(EsDescriptor descriptor) :
            base(AtomType.ESDS, Ap4.FULL_ATOM_HEADER_SIZE, 0, 0)
        {
            _esDescriptor = descriptor;
            if (_esDescriptor != null) _size32 += _esDescriptor.GetSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_EsdsAtom::AP4_EsdsAtom
        +---------------------------------------------------------------------*/
        private EsdsAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.ESDS, size, version, flags)
        {
            // read descriptor
            Descriptor descriptor = null;
            if (DescriptorFactory.CreateDescriptorFromStream(stream, out descriptor)
                == Ap4Result.SUCCESS) {
                _esDescriptor = descriptor as EsDescriptor;
            } else {
                _esDescriptor = null;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_EsdsAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the es descriptor
            if (_esDescriptor != null) return _esDescriptor.Write(stream);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_EsdsAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            // inspect descriptor
            if (_esDescriptor != null) {
                _esDescriptor.Inspect(inspector);
            }

            return Ap4Result.SUCCESS;
        }

        public EsDescriptor GetEsDescriptor() { return _esDescriptor; }

        // members
        private EsDescriptor _esDescriptor;
    }
}
