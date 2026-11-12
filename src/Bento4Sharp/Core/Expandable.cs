// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Expandable.h, Ap4Expandable.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Expandable
    +---------------------------------------------------------------------*/
    public abstract class Expandable
    {
        // types
        public enum ClassIdSize {
            CLASS_ID_SIZE_08
        }

        /*----------------------------------------------------------------------
        |   AP4_Expandable::MinHeaderSize
        +---------------------------------------------------------------------*/
        public static uint MinHeaderSize(uint payload_size)
        {
            // compute how many bytes are needed to encode the payload size plus tag
            uint min_header_size = 2;
            while (payload_size > 128) {
                ++min_header_size;
                payload_size /= 128;
            }

            return min_header_size;
        }

        /*----------------------------------------------------------------------
        |   AP4_Expandable::AP4_Expandable
        +---------------------------------------------------------------------*/
        public Expandable(uint        class_id,
                          ClassIdSize class_id_size,
                          uint        header_size,
                          uint        payload_size)
        {
            _classId     = class_id;
            _classIdSize = class_id_size;
            _headerSize  = header_size;
            _payloadSize = payload_size;
            Ap4.ASSERT(header_size >= 1+1);
            Ap4.ASSERT(header_size <= 1+4);
        }

        public uint GetClassId()    { return _classId;                 }
        public uint GetSize()       { return _payloadSize+_headerSize; }
        public uint GetHeaderSize() { return _headerSize;              }

        /*----------------------------------------------------------------------
        |   AP4_Expandable::Write
        +---------------------------------------------------------------------*/
        public virtual Ap4Result Write(ByteStream stream)
        {
            Ap4Result result;

            // write the class id
            switch (_classIdSize) {
                case ClassIdSize.CLASS_ID_SIZE_08:
                    result = stream.WriteUI08((byte)_classId);
                    if (result.Failed()) return result;
                    break;

                default:
                    return Ap4Result.ERROR_INTERNAL;
            }

            // write the size
            Ap4.ASSERT(_headerSize-1 <= 8);
            Ap4.ASSERT(_headerSize >= 2);
            uint size = _payloadSize;
            byte[] bytes = new byte[8];

            // last bytes of the encoded size
            bytes[_headerSize-2] = (byte)(size&0x7F);

            // leading bytes of the encoded size
            for (int i=unchecked((int)(_headerSize-3)); i>=0; i--) {
                // move to the next 7 bits
                size >>= 7;

                // output a byte with a top bit marker
                bytes[i] = (byte)((size&0x7F) | 0x80);
            }

            result = stream.Write(bytes, _headerSize-1);
            if (result.Failed()) return result;

            // write the fields
            WriteFields(stream);

            return result;
        }

        public abstract Ap4Result WriteFields(ByteStream stream);

        /*----------------------------------------------------------------------
        |   AP4_Expandable::Inspect
        +---------------------------------------------------------------------*/
        public virtual Ap4Result Inspect(AtomInspector inspector)
        {
            string name = "#:" + _classId.ToString("x2", CultureInfo.InvariantCulture);
            inspector.StartDescriptor(name, GetHeaderSize(), GetSize());
            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        // members
        protected uint        _classId;
        protected ClassIdSize _classIdSize;
        protected uint        _headerSize;
        protected uint        _payloadSize;
    }

    // PORT NOTE: AP4_UnknownExpandable is declared in Ap4Expandable.h but upstream
    // has no implementation for it anywhere (its constructor and WriteFields are
    // never defined, and nothing references the class), so it is not ported.
}
