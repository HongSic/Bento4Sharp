// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4IodsAtom.h, Ap4IodsAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_IodsAtom
    +---------------------------------------------------------------------*/
    public class IodsAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_IodsAtom::Create
        +---------------------------------------------------------------------*/
        public static IodsAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new IodsAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_IodsAtom::AP4_IodsAtom
        +---------------------------------------------------------------------*/
        public IodsAtom(ObjectDescriptor descriptor) : // ownership of 'descriptor' is transfered
            base(AtomType.IODS, Ap4.FULL_ATOM_HEADER_SIZE, 0, 0)
        {
            _objectDescriptor = descriptor;
            if (_objectDescriptor != null) _size32 += _objectDescriptor.GetSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_IodsAtom::AP4_IodsAtom
        +---------------------------------------------------------------------*/
        private IodsAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.IODS, size, version, flags)
        {
            // read the descriptor
            Descriptor  descriptor = null;
            if (DescriptorFactory.CreateDescriptorFromStream(stream, out descriptor) == Ap4Result.SUCCESS) {
                _objectDescriptor = descriptor as ObjectDescriptor;
            } else {
                _objectDescriptor = null;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_IodsAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the es descriptor
            if (_objectDescriptor != null) return _objectDescriptor.Write(stream);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_IodsAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            // inspect descriptor
            if (_objectDescriptor != null) {
                _objectDescriptor.Inspect(inspector);
            }

            return Ap4Result.SUCCESS;
        }

        public ObjectDescriptor GetObjectDescriptor() { return _objectDescriptor; }

        // members
        private ObjectDescriptor _objectDescriptor;
    }
}
