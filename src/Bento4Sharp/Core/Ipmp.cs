// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Ipmp.h, Ap4Ipmp.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte DESCRIPTOR_TAG_IPMP_DESCRIPTOR_POINTER = 0x0A;
        public const byte DESCRIPTOR_TAG_IPMP_DESCRIPTOR         = 0x0B;
    }

    /*----------------------------------------------------------------------
    |   AP4_IpmpDescriptorPointer
    +---------------------------------------------------------------------*/
    public class IpmpDescriptorPointer : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_IpmpDescriptorPointer::AP4_IpmpDescriptorPointer
        +---------------------------------------------------------------------*/
        public IpmpDescriptorPointer(byte descriptor_id) :
            base(Ap4.DESCRIPTOR_TAG_IPMP_DESCRIPTOR_POINTER, 2, 1)
        {
            _descriptorId   = descriptor_id;
            _descriptorIdEx = 0;
            _esId           = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_IpmpDescriptorPointer::AP4_IpmpDescriptorPointer
        +---------------------------------------------------------------------*/
        public IpmpDescriptorPointer(ByteStream stream,
                                     uint       header_size,
                                     uint       payload_size) :
            base(Ap4.DESCRIPTOR_TAG_IPMP_DESCRIPTOR_POINTER, header_size, payload_size)
        {
            // PORT NOTE: upstream leaves m_DescriptorIdEx and m_EsId uninitialized
            // when they are not read; in the port they are zero.
            stream.ReadUI08(out _descriptorId);
            if (_descriptorId == 0xFF && payload_size >= 5) {
                stream.ReadUI16(out _descriptorIdEx);
                stream.ReadUI16(out _esId);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_IpmpDescriptorPointer::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            stream.WriteUI08(_descriptorId);
            if (_descriptorId == 0xFF) {
                stream.WriteUI16(_descriptorIdEx);
                stream.WriteUI16(_esId);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_IpmpDescriptorPointer::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            inspector.StartDescriptor("IPMP_DescriptorPointer", GetHeaderSize(), GetSize());
            inspector.AddField("IPMP_DescriptorID", _descriptorId);
            if (_descriptorId == 0xFF) {
                inspector.AddField("IPMP_DescriptorIDEx", _descriptorIdEx);
                inspector.AddField("IPMP_ES_ID",          _esId);
            }
            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte   GetDescriptorId()   { return _descriptorId;   }
        public ushort GetDescriptorIdEx() { return _descriptorIdEx; }
        public ushort GetEsId()           { return _esId;           }

        // members
        private byte   _descriptorId;
        private ushort _descriptorIdEx;
        private ushort _esId;
    }

    /*----------------------------------------------------------------------
    |   AP4_IpmpDescriptor
    +---------------------------------------------------------------------*/
    public class IpmpDescriptor : Descriptor
    {
        /*----------------------------------------------------------------------
        |   AP4_IpmpDescriptor::AP4_IpmpDescriptor
        +---------------------------------------------------------------------*/
        public IpmpDescriptor(byte descriptor_id, ushort ipmps_type) :
            base(Ap4.DESCRIPTOR_TAG_IPMP_DESCRIPTOR, 2, 3)
        {
            _descriptorId     = descriptor_id;
            _ipmpsType        = ipmps_type;
            _descriptorIdEx   = 0;
            _controlPointCode = 0;
            _sequenceCode     = 0;
            System.Array.Clear(_toolId, 0, _toolId.Length);
        }

        /*----------------------------------------------------------------------
        |   AP4_IpmpDescriptor::AP4_IpmpDescriptor
        +---------------------------------------------------------------------*/
        public IpmpDescriptor(ByteStream stream,
                              uint       header_size,
                              uint       payload_size) :
            base(Ap4.DESCRIPTOR_TAG_IPMP_DESCRIPTOR, header_size, payload_size)
        {
            _descriptorIdEx   = 0;
            _controlPointCode = 0;
            _sequenceCode     = 0;

            stream.ReadUI08(out _descriptorId);
            stream.ReadUI16(out _ipmpsType);
            System.Array.Clear(_toolId, 0, _toolId.Length);
            if (_descriptorId == 0xFF && _ipmpsType == 0xFFFF) {
                uint fields_size = 3+3;
                stream.ReadUI16(out _descriptorIdEx);
                stream.Read(_toolId, 16);
                stream.ReadUI08(out _controlPointCode);
                if (_controlPointCode > 0) {
                    stream.ReadUI08(out _sequenceCode);
                    ++fields_size;
                }
                if (fields_size < payload_size) {
                    _data.SetDataSize(payload_size-fields_size);
                    stream.Read(_data.UseData(), payload_size-fields_size);
                }
            } else if (_ipmpsType == 0) {
                if (payload_size > 3) {
                    byte[] buffer = new byte[1+payload_size-3];
                    buffer[payload_size-3] = 0;
                    stream.Read(buffer, payload_size-3);
                    _url = Ap4Utils.StringFromBytes(buffer, 0, payload_size-3);
                }
            } else {
                if (payload_size > 3) {
                    _data.SetDataSize(payload_size-3);
                    stream.Read(_data.UseData(), payload_size-3);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_IpmpDescriptor::SetData
        +---------------------------------------------------------------------*/
        public void SetData(byte[] data, uint data_size)
        {
            _data.SetData(data, data_size);
            _payloadSize += data_size;
            _headerSize = MinHeaderSize(_payloadSize);
        }

        /*----------------------------------------------------------------------
        |   AP4_IpmpDescriptor::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            stream.WriteUI08(_descriptorId);
            stream.WriteUI16(_ipmpsType);
            if (_descriptorId == 0xFF && _ipmpsType == 0xFFFF) {
                stream.WriteUI16(_descriptorIdEx);
                stream.Write(_toolId, 16);
                stream.WriteUI08(_controlPointCode);
                if (_controlPointCode > 0) {
                    stream.WriteUI08(_sequenceCode);
                }
                if (_data.GetDataSize() != 0) {
                    stream.Write(_data.GetData(), _data.GetDataSize());
                }
            } else if (_ipmpsType == 0) {
                // PORT NOTE: upstream writes m_Url.GetChars() for GetLength()+1
                // bytes, where the AP4_String was built with Assign(buffer, size)
                // and so may hold embedded 0 bytes: all the bytes of the string are
                // written (not only the C string part), then the terminator.
                byte[] url = Ap4Utils.StringToBytes(_url);
                stream.Write(url, (uint)url.Length);
                stream.WriteUI08(0);
            } else {
                stream.Write(_data.GetData(), _data.GetDataSize());
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_IpmpDescriptor::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            inspector.StartDescriptor("IPMP_Descriptor", GetHeaderSize(), GetSize());
            inspector.AddField("IPMP_DescriptorID", _descriptorId);
            inspector.AddField("IPMPS_Type", _ipmpsType, AtomInspector.FormatHint.HINT_HEX);
            if (_descriptorId == 0xFF && _ipmpsType == 0xFFFF) {
                inspector.AddField("IPMP_DescriptorIDEx", _descriptorIdEx);
                inspector.AddField("IPMP_ToolID", _toolId, 16, AtomInspector.FormatHint.HINT_HEX);
                inspector.AddField("controlPointCode", _controlPointCode);
                if (_controlPointCode > 0) {
                    inspector.AddField("sequenceCode", _sequenceCode);
                }
            } else if (_ipmpsType == 0) {
                inspector.AddField("URL", _url);
            } else {
                inspector.AddField("data size", _data.GetDataSize());
            }
            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte       GetDescriptorId()     { return _descriptorId;     }
        public ushort     GetIpmpsType()        { return _ipmpsType;        }
        public ushort     GetDescriptorIdEx()   { return _descriptorIdEx;   }
        public byte[]     GetToolId()           { return _toolId;           }
        public byte       GetControlPointCode() { return _controlPointCode; }
        public byte       GetSequenceCode()     { return _sequenceCode;     }
        public string     GetUrl()              { return _url;              }
        public DataBuffer GetData()             { return _data;             }

        // members
        private byte       _descriptorId;
        private ushort     _ipmpsType;
        private ushort     _descriptorIdEx;
        private byte[]     _toolId = new byte[16];
        private byte       _controlPointCode;
        private byte       _sequenceCode;
        private string     _url = "";
        private DataBuffer _data = new DataBuffer();
    }
}
