// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4ByteStream.h, Ap4ByteStream.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_ByteStream
    +---------------------------------------------------------------------*/
    // PORT NOTE: `void* buffer` becomes `byte[] buffer, int offset`. The abstract
    // ReadPartial/WritePartial take the offset; overloads without offset forward
    // to them with offset 0. AP4_Referenceable is dropped.
    public abstract class ByteStream
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const int AP4_BYTE_STREAM_COPY_BUFFER_SIZE = 65536;

        // PORT NOTE: stand-ins for the small stack buffers of ReadUIxx/WriteUIxx,
        // so that those calls do not allocate. Streams are not thread safe (they
        // are not upstream either).
        private byte[] _readScratch;
        private byte[] _writeScratch;

        // methods
        public abstract Ap4Result ReadPartial(byte[]   buffer,
                                              int      offset,
                                              uint     bytes_to_read,
                                              out uint bytes_read);

        public Ap4Result ReadPartial(byte[] buffer, uint bytes_to_read, out uint bytes_read)
        {
            return ReadPartial(buffer, 0, bytes_to_read, out bytes_read);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::Read
        +---------------------------------------------------------------------*/
        public Ap4Result Read(byte[] buffer, uint bytes_to_read)
        {
            return Read(buffer, 0, bytes_to_read);
        }

        public Ap4Result Read(byte[] buffer, int offset, uint bytes_to_read)
        {
            // shortcut
            if (bytes_to_read == 0) return Ap4Result.SUCCESS;

            // read until failure
            uint bytes_read;
            while (bytes_to_read != 0) {
                Ap4Result result = ReadPartial(buffer, offset, bytes_to_read, out bytes_read);
                if (result.Failed()) return result;
                if (bytes_read == 0) return Ap4Result.ERROR_INTERNAL;
                Ap4.ASSERT(bytes_read <= bytes_to_read);
                bytes_to_read -= bytes_read;
                offset += (int)bytes_read;
            }

            return Ap4Result.SUCCESS;
        }

        public abstract Ap4Result WritePartial(byte[]   buffer,
                                               int      offset,
                                               uint     bytes_to_write,
                                               out uint bytes_written);

        public Ap4Result WritePartial(byte[] buffer, uint bytes_to_write, out uint bytes_written)
        {
            return WritePartial(buffer, 0, bytes_to_write, out bytes_written);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::Write
        +---------------------------------------------------------------------*/
        public Ap4Result Write(byte[] buffer, uint bytes_to_write)
        {
            return Write(buffer, 0, bytes_to_write);
        }

        public Ap4Result Write(byte[] buffer, int offset, uint bytes_to_write)
        {
            // shortcut
            if (bytes_to_write == 0) return Ap4Result.SUCCESS;

            // write until failure
            uint bytes_written;
            while (bytes_to_write != 0) {
                Ap4Result result = WritePartial(buffer, offset, bytes_to_write, out bytes_written);
                if (result.Failed()) return result;
                if (bytes_written == 0) return Ap4Result.ERROR_INTERNAL;
                Ap4.ASSERT(bytes_written <= bytes_to_write);
                bytes_to_write -= bytes_written;
                offset += (int)bytes_written;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::WriteString
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Writes the bytes of a C string, WITHOUT terminator: the string is
        /// encoded with <see cref="Ap4Utils.StringToCStringBytes"/> (lossless
        /// UTF-8, stops before the first U+0000).
        /// </summary>
        public Ap4Result WriteString(string buffer)
        {
            if (buffer == null) return Ap4Result.SUCCESS;
            byte[] bytes = Ap4Utils.StringToCStringBytes(buffer);
            uint string_length = (uint)bytes.Length;
            if (string_length == 0) return Ap4Result.SUCCESS;

            // write the string
            return Write(bytes, string_length);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::WriteDouble
        +---------------------------------------------------------------------*/
        public Ap4Result WriteDouble(double value)
        {
            byte[] buffer = _writeScratch ?? (_writeScratch = new byte[8]);

            // convert value to bytes
            Ap4Utils.BytesFromDoubleBE(buffer, value);

            // write bytes to the stream
            return Write(buffer, 8);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::WriteUI64
        +---------------------------------------------------------------------*/
        public Ap4Result WriteUI64(ulong value)
        {
            byte[] buffer = _writeScratch ?? (_writeScratch = new byte[8]);

            // convert value to bytes
            Ap4Utils.BytesFromUInt64BE(buffer, value);

            // write bytes to the stream
            return Write(buffer, 8);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::WriteUI32
        +---------------------------------------------------------------------*/
        public Ap4Result WriteUI32(uint value)
        {
            byte[] buffer = _writeScratch ?? (_writeScratch = new byte[8]);

            // convert value to bytes
            Ap4Utils.BytesFromUInt32BE(buffer, value);

            // write bytes to the stream
            return Write(buffer, 4);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::WriteUI24
        +---------------------------------------------------------------------*/
        public Ap4Result WriteUI24(uint value)
        {
            byte[] buffer = _writeScratch ?? (_writeScratch = new byte[8]);

            // convert value to bytes
            Ap4Utils.BytesFromUInt24BE(buffer, value);

            // write bytes to the stream
            return Write(buffer, 3);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::WriteUI16
        +---------------------------------------------------------------------*/
        public Ap4Result WriteUI16(ushort value)
        {
            byte[] buffer = _writeScratch ?? (_writeScratch = new byte[8]);

            // convert value to bytes
            Ap4Utils.BytesFromUInt16BE(buffer, value);

            // write bytes to the stream
            return Write(buffer, 2);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::WriteUI08
        +---------------------------------------------------------------------*/
        public Ap4Result WriteUI08(byte value)
        {
            byte[] buffer = _writeScratch ?? (_writeScratch = new byte[8]);
            buffer[0] = value;
            return Write(buffer, 1);
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::ReadUI64
        +---------------------------------------------------------------------*/
        public Ap4Result ReadUI64(out ulong value)
        {
            byte[] buffer = _readScratch ?? (_readScratch = new byte[8]);

            // read bytes from the stream
            Ap4Result result;
            result = Read(buffer, 8);
            if (result.Failed()) {
                value = 0;
                return result;
            }

            // convert bytes to value
            value = Ap4Utils.BytesToUInt64BE(buffer);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::ReadDouble
        +---------------------------------------------------------------------*/
        public Ap4Result ReadDouble(out double value)
        {
            byte[] buffer = _readScratch ?? (_readScratch = new byte[8]);

            // read bytes from the stream
            Ap4Result result;
            result = Read(buffer, 8);
            if (result.Failed()) {
                value = 0;
                return result;
            }

            // convert bytes to value
            value = Ap4Utils.BytesToDoubleBE(buffer);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::ReadUI32
        +---------------------------------------------------------------------*/
        public Ap4Result ReadUI32(out uint value)
        {
            byte[] buffer = _readScratch ?? (_readScratch = new byte[8]);

            // read bytes from the stream
            Ap4Result result;
            result = Read(buffer, 4);
            if (result.Failed()) {
                value = 0;
                return result;
            }

            // convert bytes to value
            value = Ap4Utils.BytesToUInt32BE(buffer);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::ReadUI24
        +---------------------------------------------------------------------*/
        public Ap4Result ReadUI24(out uint value)
        {
            byte[] buffer = _readScratch ?? (_readScratch = new byte[8]);

            // read bytes from the stream
            Ap4Result result;
            result = Read(buffer, 3);
            if (result.Failed()) {
                value = 0;
                return result;
            }

            // convert bytes to value
            value = Ap4Utils.BytesToUInt24BE(buffer);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::ReadUI16
        +---------------------------------------------------------------------*/
        public Ap4Result ReadUI16(out ushort value)
        {
            byte[] buffer = _readScratch ?? (_readScratch = new byte[8]);

            // read bytes from the stream
            Ap4Result result;
            result = Read(buffer, 2);
            if (result.Failed()) {
                value = 0;
                return result;
            }

            // convert bytes to value
            value = Ap4Utils.BytesToUInt16BE(buffer);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::ReadUI08
        +---------------------------------------------------------------------*/
        public Ap4Result ReadUI08(out byte value)
        {
            byte[] buffer = _readScratch ?? (_readScratch = new byte[8]);

            // read bytes from the stream
            Ap4Result result;
            result = Read(buffer, 1);
            if (result.Failed()) {
                value = 0;
                return result;
            }

            // convert bytes to value
            value = buffer[0];

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::ReadString
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Reads a zero-terminated string into a (virtual) buffer of
        /// <paramref name="size"/> bytes: reads byte by byte until a zero byte has
        /// been consumed or size-1 bytes have been read. On a read error the
        /// bytes read so far are returned in <paramref name="value"/> together
        /// with the error. Decoded with <see cref="Ap4Utils.StringFromBytes(byte[], int, uint)"/>.
        /// </summary>
        // PORT NOTE: upstream signature is ReadString(char* buffer, AP4_Size size).
        public Ap4Result ReadString(out string value, uint size)
        {
            if (size == 0) {
                value = "";
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            byte[] buffer = new byte[size];
            uint bytes_read = 0;
            while (bytes_read < size-1) {
                Ap4Result result;
                result = Read(buffer, (int)bytes_read, 1);
                if (result.Failed()) {
                    buffer[bytes_read] = 0;
                    value = Ap4Utils.StringFromBytes(buffer, 0, bytes_read);
                    return result;
                }
                if (buffer[bytes_read] == 0) {
                    // end of string
                    value = Ap4Utils.StringFromBytes(buffer, 0, bytes_read);
                    return Ap4Result.SUCCESS;
                }
                bytes_read++;
            }

            // the string was not null terminated, terminate it
            buffer[size-1] = 0;
            value = Ap4Utils.StringFromBytes(buffer, 0, size-1);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::ReadNullTerminatedString
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Reads bytes up to and including a zero byte. On failure
        /// <paramref name="value"/> is "" (left untouched upstream).
        /// </summary>
        public Ap4Result ReadNullTerminatedString(out string value)
        {
            DataBuffer buffer = new DataBuffer();
            uint       size = 0;
            byte       c = 0;
            do {
                Ap4Result result = ReadUI08(out c);
                if (result.Failed()) {
                    value = "";
                    return result;
                }
                // PORT NOTE: Reserve() added to avoid a reallocation per byte
                buffer.Reserve(size+1);
                buffer.SetDataSize(size+1);
                buffer.UseData()[size] = c;
                ++size;
            } while (c != 0);

            Ap4.ASSERT(size != 0);
            value = Ap4Utils.StringFromBytes(buffer.GetData(), 0, size - 1);

            return Ap4Result.SUCCESS;
        }

        public abstract Ap4Result Seek(ulong position);
        public abstract Ap4Result Tell(out ulong position);
        public abstract Ap4Result GetSize(out ulong size);

        /*----------------------------------------------------------------------
        |   AP4_ByteStream::CopyTo
        +---------------------------------------------------------------------*/
        public virtual Ap4Result CopyTo(ByteStream stream, ulong size)
        {
            byte[] buffer = new byte[(int)Math.Min((ulong)AP4_BYTE_STREAM_COPY_BUFFER_SIZE, size)];
            while (size != 0) {
                uint bytes_read;
                uint bytes_to_read;
                Ap4Result result;

                // decide how much to read
                if (size >= (ulong)buffer.Length) {
                    bytes_to_read = (uint)buffer.Length;
                } else {
                    bytes_to_read = (uint)size;
                }

                // read up to one buffer full
                result = ReadPartial(buffer, bytes_to_read, out bytes_read);
                if (result.Failed()) return result;

                // copy to destination
                if (bytes_read != 0) {
                    result = stream.Write(buffer, bytes_read);
                    if (result.Failed()) return result;
                }

                // update the size
                size -= bytes_read;
            }

            return Ap4Result.SUCCESS;
        }

        public virtual Ap4Result Flush() { return Ap4Result.SUCCESS; }
    }

    /*----------------------------------------------------------------------
    |   AP4_SubStream
    +---------------------------------------------------------------------*/
    public class SubStream : ByteStream
    {
        /*----------------------------------------------------------------------
        |   AP4_SubStream::AP4_SubStream
        +---------------------------------------------------------------------*/
        public SubStream(ByteStream container,
                         ulong      position,
                         ulong      size)
        {
            _container = container;
            _offset = position;
            _size = size;
            _position = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_SubStream::ReadPartial
        +---------------------------------------------------------------------*/
        public override Ap4Result ReadPartial(byte[]   buffer,
                                              int      offset,
                                              uint     bytes_to_read,
                                              out uint bytes_read)
        {
            // default values
            bytes_read = 0;

            // shortcut
            if (bytes_to_read == 0) {
                return Ap4Result.SUCCESS;
            }

            // clamp to range
            if (_position+bytes_to_read > _size) {
                bytes_to_read = (uint)(_size - _position);
            }

            // check for end of substream
            if (bytes_to_read == 0) {
                return Ap4Result.ERROR_EOS;
            }

            // seek inside container
            Ap4Result result;
            result = _container.Seek(_offset+_position);
            if (result.Failed()) {
                return result;
            }

            // read from the container
            result = _container.ReadPartial(buffer, offset, bytes_to_read, out bytes_read);
            if (result.Succeeded()) {
                _position += bytes_read;
            }
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_SubStream::WritePartial
        +---------------------------------------------------------------------*/
        public override Ap4Result WritePartial(byte[]   buffer,
                                               int      offset,
                                               uint     bytes_to_write,
                                               out uint bytes_written)
        {
            // default values
            bytes_written = 0;

            // shortcut
            if (bytes_to_write == 0) {
                return Ap4Result.SUCCESS;
            }

            // clamp to range
            if (_position+bytes_to_write > _size) {
                bytes_to_write = (uint)(_size - _position);
            }

            // check for end of substream
            if (bytes_to_write == 0) {
                return Ap4Result.ERROR_EOS;
            }

            // seek inside container
            Ap4Result result;
            result = _container.Seek(_offset+_position);
            if (result.Failed()) return result;

            // write to container
            result = _container.WritePartial(buffer, offset, bytes_to_write, out bytes_written);
            if (result.Succeeded()) {
                _position += bytes_written;
            }
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_SubStream::Seek
        +---------------------------------------------------------------------*/
        public override Ap4Result Seek(ulong position)
        {
            if (position == _position) return Ap4Result.SUCCESS;
            if (position > _size) return Ap4Result.FAILURE;
            _position = position;
            return Ap4Result.SUCCESS;
        }

        public override Ap4Result Tell(out ulong position)
        {
            position = _position;
            return Ap4Result.SUCCESS;
        }

        public override Ap4Result GetSize(out ulong size)
        {
            size = _size;
            return Ap4Result.SUCCESS;
        }

        private ByteStream _container;
        private ulong      _offset;
        private ulong      _size;
        private ulong      _position;
    }

    /*----------------------------------------------------------------------
    |   AP4_DupStream
    +---------------------------------------------------------------------*/
    public class DupStream : ByteStream
    {
        /*----------------------------------------------------------------------
        |   AP4_DupStream::AP4_DupStream
        +---------------------------------------------------------------------*/
        public DupStream(ByteStream original_stream)
        {
            _originalStream = original_stream;
            _position = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_DupStream::ReadPartial
        +---------------------------------------------------------------------*/
        public override Ap4Result ReadPartial(byte[]   buffer,
                                              int      offset,
                                              uint     bytes_to_read,
                                              out uint bytes_read)
        {
            // default values
            bytes_read = 0;

            // shortcut
            if (bytes_to_read == 0) {
                return Ap4Result.SUCCESS;
            }

            // seek to the right position in the original stream
            _originalStream.Seek(_position);

            // read
            Ap4Result result = _originalStream.ReadPartial(buffer, offset, bytes_to_read, out bytes_read);

            // adjust our position
            if (result.Succeeded()) {
                _position += bytes_read;
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_DupStream::WritePartial
        +---------------------------------------------------------------------*/
        public override Ap4Result WritePartial(byte[]   buffer,
                                               int      offset,
                                               uint     bytes_to_write,
                                               out uint bytes_written)
        {
            // default values
            bytes_written = 0;

            // shortcut
            if (bytes_to_write == 0) {
                return Ap4Result.SUCCESS;
            }

            // seek to the right position in the original stream
            _originalStream.Seek(_position);

            // read
            Ap4Result result = _originalStream.WritePartial(buffer, offset, bytes_to_write, out bytes_written);

            // adjust our position
            if (result.Succeeded()) {
                _position += bytes_written;
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_DupStream::Seek
        +---------------------------------------------------------------------*/
        public override Ap4Result Seek(ulong position)
        {
            if (position == _position) return Ap4Result.SUCCESS;
            Ap4Result result = _originalStream.Seek(position);
            if (result.Succeeded()) {
                _position = position;
            }
            return result;
        }

        public override Ap4Result Tell(out ulong position)
        {
            position = _position;
            return Ap4Result.SUCCESS;
        }

        public override Ap4Result GetSize(out ulong size)
        {
            return _originalStream.GetSize(out size);
        }

        private ByteStream _originalStream;
        private ulong      _position;
    }

    /*----------------------------------------------------------------------
    |   AP4_MemoryByteStream
    +---------------------------------------------------------------------*/
    public class MemoryByteStream : ByteStream
    {
        /*----------------------------------------------------------------------
        |   AP4_MemoryByteStream::AP4_MemoryByteStream
        +---------------------------------------------------------------------*/
        /// <summary>Creates a stream of <paramref name="size"/> zero bytes (it grows when written past its end).</summary>
        public MemoryByteStream(uint size = 0)
        {
            _bufferIsLocal = true;
            _position = 0;
            _buffer = new DataBuffer(size);
            // (a new array is already filled with zeros)
            _buffer.SetDataSize(size);
        }

        /*----------------------------------------------------------------------
        |   AP4_MemoryByteStream::AP4_MemoryByteStream
        +---------------------------------------------------------------------*/
        /// <summary>Creates a stream over a COPY of the first <paramref name="size"/> bytes of <paramref name="buffer"/>.</summary>
        public MemoryByteStream(byte[] buffer, uint size)
        {
            _bufferIsLocal = true;
            _position = 0;
            _buffer = new DataBuffer(buffer, size);
        }

        /// <summary>PORT NOTE: overload for upstream call sites that pass a pointer into a buffer (copies the bytes).</summary>
        public MemoryByteStream(byte[] buffer, int offset, uint size)
        {
            _bufferIsLocal = true;
            _position = 0;
            _buffer = new DataBuffer(buffer, offset, size);
        }

        /*----------------------------------------------------------------------
        |   AP4_MemoryByteStream::AP4_MemoryByteStream
        +---------------------------------------------------------------------*/
        /// <summary>Data is read/written from/to the supplied buffer (no copy).</summary>
        // PORT NOTE: upstream has two constructors (AP4_DataBuffer& without
        // ownership transfer, AP4_DataBuffer* with); ownership is moot here.
        public MemoryByteStream(DataBuffer data_buffer)
        {
            _buffer = data_buffer;
            _bufferIsLocal = false;
            _position = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_MemoryByteStream::ReadPartial
        +---------------------------------------------------------------------*/
        public override Ap4Result ReadPartial(byte[]   buffer,
                                              int      offset,
                                              uint     bytes_to_read,
                                              out uint bytes_read)
        {
            // default values
            bytes_read = 0;

            // shortcut
            if (bytes_to_read == 0) {
                return Ap4Result.SUCCESS;
            }

            // clamp to range
            if (_position+bytes_to_read > _buffer.GetDataSize()) {
                bytes_to_read = (uint)(_buffer.GetDataSize() - _position);
            }

            // check for end of stream
            if (bytes_to_read == 0) {
                return Ap4Result.ERROR_EOS;
            }

            // read from the memory
#if !NETSTANDARD1_3
            Array.Copy(_buffer.GetData(), (long)_position, buffer, offset, bytes_to_read);
#else
            Compat.ArrayCopy(_buffer.GetData(), (long)_position, buffer, offset, bytes_to_read);
#endif
            _position += bytes_to_read;

            bytes_read = bytes_to_read;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MemoryByteStream::WritePartial
        +---------------------------------------------------------------------*/
        public override Ap4Result WritePartial(byte[]   buffer,
                                               int      offset,
                                               uint     bytes_to_write,
                                               out uint bytes_written)
        {
            // default values
            bytes_written = 0;

            // shortcut
            if (bytes_to_write == 0) {
                return Ap4Result.SUCCESS;
            }

            // reserve space in the buffer
            uint space_needed = (uint)(_position+bytes_to_write);
            Ap4Result result = _buffer.Reserve(space_needed);
            if (result.Succeeded()) {
                if (space_needed > _buffer.GetDataSize()) {
                    // the buffer must grow
                    _buffer.SetDataSize(space_needed);
                }
            } else {
                // failed to reserve, most likely caused by a buffer that has
                // external storage
                if (_position+bytes_to_write > _buffer.GetDataSize()) {
                    bytes_to_write = (uint)(_buffer.GetDataSize() - _position);
                }
            }

            // check for end of stream
            if (bytes_to_write == 0) {
                return Ap4Result.ERROR_EOS;
            }

            // write to memory
#if !NETSTANDARD1_3
            Array.Copy(buffer, offset, _buffer.UseData(), (long)_position, bytes_to_write);
#else
            Compat.ArrayCopy(buffer, offset, _buffer.UseData(), (long)_position, bytes_to_write);
#endif
            _position += bytes_to_write;

            bytes_written = bytes_to_write;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MemoryByteStream::Seek
        +---------------------------------------------------------------------*/
        public override Ap4Result Seek(ulong position)
        {
            if (position > _buffer.GetDataSize()) return Ap4Result.FAILURE;
            _position = position;
            return Ap4Result.SUCCESS;
        }

        public override Ap4Result Tell(out ulong position)
        {
            position = _position;
            return Ap4Result.SUCCESS;
        }

        public override Ap4Result GetSize(out ulong size)
        {
            size = _buffer.GetDataSize();
            return Ap4Result.SUCCESS;
        }

        // methods
        /// <summary>The backing array: only the first GetDataSize() bytes are valid (the array is usually longer).</summary>
        public byte[] GetData()     { return _buffer.GetData(); }
        /// <summary>The backing array: only the first GetDataSize() bytes are valid (the array is usually longer).</summary>
        public byte[] UseData()     { return _buffer.UseData(); }
        public uint   GetDataSize() { return _buffer.GetDataSize(); }

        /// <summary>PORT NOTE: not in upstream. Returns a copy of the stream contents (exactly GetDataSize() bytes).</summary>
        public byte[] ToArray()     { return _buffer.ToArray(); }

        private DataBuffer _buffer;
        private bool       _bufferIsLocal;
        private ulong      _position;
    }

    /*----------------------------------------------------------------------
    |   AP4_BufferedInputStream
    +---------------------------------------------------------------------*/
    public class BufferedInputStream : ByteStream
    {
        /*----------------------------------------------------------------------
        |   AP4_BufferedInputStream::AP4_BufferedInputStream
        +---------------------------------------------------------------------*/
        public BufferedInputStream(ByteStream source,
                                   uint       buffer_size = 4096,
                                   uint       seek_as_read_threshold = 1024*128)
        {
            _buffer = new DataBuffer(buffer_size);
            _bufferPosition = 0;
            _source = source;
            _sourcePosition = 0;
            _seekAsReadThreshold = seek_as_read_threshold;
        }

        /*----------------------------------------------------------------------
        |   AP4_BufferedInputStream::Refill
        +---------------------------------------------------------------------*/
        protected Ap4Result Refill()
        {
            _bufferPosition = 0;
            uint bytes_read = 0;
            Ap4Result result = _source.ReadPartial(_buffer.UseData(),
                                                   _buffer.GetBufferSize(),
                                                   out bytes_read);
            if (result.Failed()) {
                _buffer.SetDataSize(0);
                return result;
            }
            Ap4.ASSERT(bytes_read != 0);
            _buffer.SetDataSize(bytes_read);
            _sourcePosition += bytes_read;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_BufferedInputStream::ReadPartial
        +---------------------------------------------------------------------*/
        public override Ap4Result ReadPartial(byte[]   buffer,
                                              int      offset,
                                              uint     bytes_to_read,
                                              out uint bytes_read)
        {
            // check for shortcut
            if (bytes_to_read == 0) {
                bytes_read = 0;
                return Ap4Result.SUCCESS;
            }

            // compute how much data is available in the buffer
            Ap4.ASSERT(_bufferPosition <= _buffer.GetDataSize());
            uint available = _buffer.GetDataSize()-_bufferPosition;

            // refill the buffer if it is empty
            if (available == 0) {
                Ap4Result result = Refill();
                if (result.Failed()) {
                    bytes_read = 0;
                    return result;
                }
                Ap4.ASSERT(_bufferPosition == 0);
                Ap4.ASSERT(_buffer.GetDataSize() != 0);
                available = _buffer.GetDataSize()-_bufferPosition;
            }

            // clamp the number of bytes to read to what's available
            if (bytes_to_read > available) bytes_to_read = available;
            bytes_read = bytes_to_read;

            // copy the buffered data
            Array.Copy(_buffer.GetData(), (int)_bufferPosition, buffer, offset, (int)bytes_to_read);
            _bufferPosition += bytes_to_read;
            Ap4.ASSERT(_bufferPosition <= _buffer.GetDataSize());

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_BufferedInputStream::WritePartial
        +---------------------------------------------------------------------*/
        public override Ap4Result WritePartial(byte[]   buffer,
                                               int      offset,
                                               uint     bytes_to_write,
                                               out uint bytes_written)
        {
            bytes_written = 0;
            return Ap4Result.ERROR_NOT_SUPPORTED;
        }

        /*----------------------------------------------------------------------
        |   AP4_BufferedInputStream::Seek
        +---------------------------------------------------------------------*/
        public override Ap4Result Seek(ulong position)
        {
            Ap4.ASSERT(_sourcePosition >= _buffer.GetDataSize());
            Ap4.ASSERT(_bufferPosition <= _buffer.GetDataSize());
            if (position < _sourcePosition-_buffer.GetDataSize() ||
                position > _sourcePosition) {
                // out of buffer
                _bufferPosition = 0;
                _buffer.SetDataSize(0);

                // seek in the source
                if (position > _sourcePosition && (position-_sourcePosition <= _seekAsReadThreshold)) {
                    byte[] discard = new byte[4096];
                    uint to_skip = (uint)(position-_sourcePosition);
                    while (to_skip != 0) {
                        uint chunk = 4096;
                        if (chunk > to_skip) chunk = to_skip;
                        Ap4Result result = _source.Read(discard, chunk);
                        if (result.Failed()) {
                            return result;
                        }
                        _sourcePosition += chunk;
                        to_skip -= chunk;
                    }
                    return Ap4Result.SUCCESS;
                } else {
                    _sourcePosition = position;
                    return _source.Seek(position);
                }
            }

            // compute the buffer position
            _bufferPosition = (uint)(position-(_sourcePosition-_buffer.GetDataSize()));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_BufferedInputStream::Tell
        +---------------------------------------------------------------------*/
        public override Ap4Result Tell(out ulong position)
        {
            Ap4.ASSERT(_sourcePosition >= _buffer.GetDataSize());
            Ap4.ASSERT(_bufferPosition <= _buffer.GetDataSize());
            position = _sourcePosition-_buffer.GetDataSize()+_bufferPosition;
            return Ap4Result.SUCCESS;
        }

        public override Ap4Result GetSize(out ulong size) { return _source.GetSize(out size); }

        private DataBuffer _buffer;
        private uint       _bufferPosition;
        private ByteStream _source;
        private ulong      _sourcePosition;
        private uint       _seekAsReadThreshold;
    }
}
