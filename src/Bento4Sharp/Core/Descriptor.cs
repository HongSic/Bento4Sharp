// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Descriptor.h, Ap4Descriptor.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Descriptor
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream AP4_Descriptor is implicitly abstract (it inherits the
    // pure virtual WriteFields from AP4_Expandable); declared abstract here.
    public abstract class Descriptor : Expandable
    {
        // constructor
        public Descriptor(byte tag, uint header_size, uint payload_size) :
            base(tag, ClassIdSize.CLASS_ID_SIZE_08, header_size, payload_size) {}

        /*----------------------------------------------------------------------
        |   AP4_Descriptor::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            string name = "Descriptor:" + _classId.ToString("x2", CultureInfo.InvariantCulture);
            inspector.StartDescriptor(name, GetHeaderSize(), GetSize());
            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        // methods
        public byte GetTag() { return (byte)_classId; }
    }

    /*----------------------------------------------------------------------
    |   AP4_UnknownDescriptor
    +---------------------------------------------------------------------*/
    public class UnknownDescriptor : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_UnknownDescriptor::AP4_UnknownDescriptor
        +---------------------------------------------------------------------*/
        public UnknownDescriptor(ByteStream stream,
                                 byte       tag,
                                 uint       header_size,
                                 uint       payload_size) :
            base(tag, header_size, payload_size)
        {
            _data.SetDataSize(payload_size);
            stream.Read(_data.UseData(), payload_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownDescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the payload
            stream.Write(_data.GetData(), _data.GetDataSize());

            return Ap4Result.SUCCESS;
        }

        // members
        private DataBuffer _data = new DataBuffer();
    }

    /*----------------------------------------------------------------------
    |   AP4_DescriptorFinder
    +---------------------------------------------------------------------*/
    public class DescriptorFinder : IListFinder<Descriptor>
    {
        public DescriptorFinder(byte tag) { _tag = tag; }
        public Ap4Result Test(Descriptor descriptor) {
            return (descriptor != null && descriptor.GetTag() == _tag) ? Ap4Result.SUCCESS : Ap4Result.FAILURE;
        }

        private byte _tag;
    }

    /*----------------------------------------------------------------------
    |   AP4_DescriptorListWriter
    +---------------------------------------------------------------------*/
    public class DescriptorListWriter : IListOperator<Descriptor>
    {
        public DescriptorListWriter(ByteStream stream) { _stream = stream; }
        public Ap4Result Action(Descriptor descriptor) {
            return descriptor.Write(_stream);
        }

        private ByteStream _stream;
    }

    /*----------------------------------------------------------------------
    |   AP4_DescriptorListInspector
    +---------------------------------------------------------------------*/
    public class DescriptorListInspector : IListOperator<Descriptor>
    {
        public DescriptorListInspector(AtomInspector inspector) { _inspector = inspector; }
        public Ap4Result Action(Descriptor descriptor) {
            descriptor.Inspect(_inspector);
            return Ap4Result.SUCCESS;
        }

        private AtomInspector _inspector;
    }
}
