// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4DataBuffer.h, Ap4DataBuffer.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_DataBuffer
    +---------------------------------------------------------------------*/
    // PORT NOTE: m_Buffer is never null in the port (an empty array stands for
    // NULL). GetData()/UseData() return the backing array itself: its Length is
    // GetBufferSize() (or more, for an external buffer), NOT GetDataSize().
    public class DataBuffer
    {
        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::AP4_DataBuffer
        +---------------------------------------------------------------------*/
        public DataBuffer()
        {
            _bufferIsLocal = true;
#if !NET20
            _buffer = Array.Empty<byte>();
#else
            _buffer = new byte[0];
#endif
            _bufferSize = 0;
            _dataSize = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::AP4_DataBuffer
        +---------------------------------------------------------------------*/
        public DataBuffer(uint buffer_size)
        {
            _bufferIsLocal = true;
            _bufferSize = buffer_size;
            _dataSize = 0;
            _buffer = new byte[buffer_size];
        }

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::AP4_DataBuffer
        +---------------------------------------------------------------------*/
        public DataBuffer(byte[] data, uint data_size) : this(data, 0, data_size)
        {
        }

        /// <summary>PORT NOTE: overload for upstream call sites that pass a pointer into a buffer.</summary>
        public DataBuffer(byte[] data, int offset, uint data_size)
        {
            _bufferIsLocal = true;
            _bufferSize = data_size;
            _dataSize = data_size;
            // PORT NOTE: upstream leaves m_Buffer NULL when data is NULL (with a
            // non-zero size); the port allocates a zero-filled buffer instead.
#if !NET20
            _buffer = data_size != 0 ? new byte[data_size] : Array.Empty<byte>();
#else
            _buffer = data_size != 0 ? new byte[data_size] : new byte[0];
#endif
            if (data != null && data_size != 0) {
                Array.Copy(data, offset, _buffer, 0, (int)data_size);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::AP4_DataBuffer
        +---------------------------------------------------------------------*/
        /// <summary>Copy constructor: copies the DATA of <paramref name="other"/> into a new local buffer.</summary>
        public DataBuffer(DataBuffer other)
        {
            _bufferIsLocal = true;
            _bufferSize = other._dataSize;
            _dataSize = other._dataSize;
            _buffer = new byte[_bufferSize];
            Array.Copy(other._buffer, 0, _buffer, 0, (int)_bufferSize);
        }

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::Reserve
        +---------------------------------------------------------------------*/
        public Ap4Result Reserve(uint size)
        {
            if (size <= _bufferSize) return Ap4Result.SUCCESS;

            // try doubling the buffer to accomodate for the new size
            uint new_size = _bufferSize*2+1024;
            if (new_size < size) new_size = size;
            return SetBufferSize(new_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::SetBuffer
        +---------------------------------------------------------------------*/
        /// <summary>Use an external buffer (it can no longer be resized by this object).</summary>
        public Ap4Result SetBuffer(byte[] buffer, uint buffer_size)
        {
            // we're now using an external buffer
            _bufferIsLocal = false;
            _buffer = buffer;
            _bufferSize = buffer_size;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::SetBufferSize
        +---------------------------------------------------------------------*/
        public Ap4Result SetBufferSize(uint buffer_size)
        {
            if (_bufferIsLocal) {
                return ReallocateBuffer(buffer_size);
            } else {
                return Ap4Result.FAILURE; // you cannot change the
                                          // buffer management mode
            }
        }

        public uint GetBufferSize() { return _bufferSize; }

        // data handling methods
        /// <summary>The backing array (valid data: the first GetDataSize() bytes).</summary>
        public byte[] GetData() { return _buffer; }
        /// <summary>The backing array (valid data: the first GetDataSize() bytes).</summary>
        public byte[] UseData() { return _buffer; }
        public uint   GetDataSize() { return _dataSize; }

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::SetDataSize
        +---------------------------------------------------------------------*/
        public Ap4Result SetDataSize(uint size)
        {
            if (size > _bufferSize) {
                if (_bufferIsLocal) {
                    Ap4Result result = ReallocateBuffer(size);
                    if (result.Failed()) return result;
                } else {
                    return Ap4Result.FAILURE;
                }
            }
            _dataSize = size;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::SetData
        +---------------------------------------------------------------------*/
        public Ap4Result SetData(byte[] data, uint size)
        {
            return SetData(data, 0, size);
        }

        /// <summary>PORT NOTE: overload for upstream call sites that pass a pointer into a buffer.</summary>
        public Ap4Result SetData(byte[]  data, int offset, uint size)
        {
            if (size > _bufferSize) {
                if (_bufferIsLocal) {
                    Ap4Result result = ReallocateBuffer(size);
                    if (result.Failed()) return result;
                } else {
                    return Ap4Result.FAILURE;
                }
            }
            if (data != null && size != 0) {
                Array.Copy(data, offset, _buffer, 0, (int)size);
            }
            _dataSize = size;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::AppendData
        +---------------------------------------------------------------------*/
        public Ap4Result AppendData(byte[] data, uint data_size)
        {
            return AppendData(data, 0, data_size);
        }

        /// <summary>PORT NOTE: overload for upstream call sites that pass a pointer into a buffer.</summary>
        public Ap4Result AppendData(byte[] data, int offset, uint data_size)
        {
            if (data == null || data_size == 0) {
                return Ap4Result.SUCCESS;
            }

            uint existing_size = _dataSize;
            Ap4Result result = SetDataSize(existing_size+data_size);
            if (result.Failed()) {
                return result;
            }
            Array.Copy(data, offset, _buffer, (int)existing_size, (int)data_size);

            return Ap4Result.SUCCESS;
        }

        /// <summary>
        /// PORT NOTE: not in upstream. Returns a copy of the valid data (exactly
        /// GetDataSize() bytes).
        /// </summary>
        public byte[] ToArray()
        {
            byte[] copy = new byte[_dataSize];
            Array.Copy(_buffer, 0, copy, 0, (int)_dataSize);
            return copy;
        }

        // members
        protected bool   _bufferIsLocal;
        protected byte[] _buffer;
        protected uint   _bufferSize;
        protected uint   _dataSize;

        /*----------------------------------------------------------------------
        |   AP4_DataBuffer::ReallocateBuffer
        +---------------------------------------------------------------------*/
        protected Ap4Result ReallocateBuffer(uint size)
        {
            // check that the existing data fits
            if (_dataSize > size) return Ap4Result.FAILURE;

            // PORT NOTE: upstream would throw std::bad_alloc; a .NET array cannot
            // hold more than Array.MaxLength bytes.
#if NET6_0_OR_GREATER
            if (size > (uint)Array.MaxLength) return Ap4Result.ERROR_OUT_OF_MEMORY;
#else
            if (size > (uint)Compat.ArrayMaxLength) return Ap4Result.ERROR_OUT_OF_MEMORY;
#endif

            // allocate a new buffer
            byte[] new_buffer = new byte[size];

            // copy the contents of the previous buffer ,is any
            if (_dataSize != 0) {
                Array.Copy(_buffer, 0, new_buffer, 0, (int)_dataSize);
            }

            // use the new buffer
            _buffer = new_buffer;
            _bufferSize = size;

            return Ap4Result.SUCCESS;
        }
    }
}
