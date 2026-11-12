// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Sample.h, Ap4Sample.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Sample
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream AP4_Sample is a value type that is passed by
    // reference (`AP4_Sample& sample`) to be filled in, and copied with the copy
    // constructor / operator=. The port makes it a (sealed) class: methods that
    // take `AP4_Sample&` take a `Sample` instance and fill it in place, the copy
    // constructor is `new Sample(other)` and `a = b` is `a.Assign(b)`. Never
    // port `a = b` between samples as a C# reference assignment.
    public sealed class Sample
    {
        /*----------------------------------------------------------------------
        |   AP4_Sample::AP4_Sample
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Default constructor
        /// </summary>
        public Sample()
        {
            _dataStream = null;
            _offset = 0;
            _size = 0;
            _duration = 0;
            _descriptionIndex = 0;
            _dts = 0;
            _ctsDelta = 0;
            _isSync = true;
        }

        /*----------------------------------------------------------------------
        |   AP4_Sample::AP4_Sample
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Construct an <see cref="Sample"/> referencing a data stream
        /// </summary>
        /// <param name="data_stream">The byte stream that contains the sample data.
        /// The sample object added to the track will keep a reference to that byte
        /// stream</param>
        /// <param name="offset">Position of the first byte of sample data within the stream</param>
        /// <param name="size">Size in bytes of the sample data</param>
        /// <param name="duration">Duration of the sample</param>
        /// <param name="description_index">Index of the sample description that applies to
        /// this sample</param>
        /// <param name="dts">Decoding timestamp of the sample</param>
        /// <param name="cts_delta">Difference between the CTS (composition/display timestamp) and the
        /// DTS (decoding timestamp), in the timescale of the media.</param>
        /// <param name="sync_flag">Boolean flag indicating whether this is a sync sample
        /// or not</param>
        public Sample(ByteStream data_stream,
                      ulong      offset,
                      uint       size,
                      uint       duration,
                      uint       description_index,
                      ulong      dts,
                      uint       cts_delta,
                      bool       sync_flag)
        {
            _offset = offset;
            _size = size;
            _duration = duration;
            _descriptionIndex = description_index;
            _dts = dts;
            _ctsDelta = (int)cts_delta;
            _isSync = sync_flag;
            _dataStream = data_stream;
        }

        /*----------------------------------------------------------------------
        |   AP4_Sample::AP4_Sample
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Copy constructor
        /// </summary>
        public Sample(Sample other)
        {
            _dataStream = other._dataStream;
            _offset = other._offset;
            _size = other._size;
            _duration = other._duration;
            _descriptionIndex = other._descriptionIndex;
            _dts = other._dts;
            _ctsDelta = other._ctsDelta;
            _isSync = other._isSync;
        }

        /*----------------------------------------------------------------------
        |   AP4_Sample::operator=
        +---------------------------------------------------------------------*/
        // PORT NOTE: operator= cannot be overloaded in C#; see the class note.
        public Sample Assign(Sample other)
        {
            _dataStream = other._dataStream;

            _offset           = other._offset;
            _size             = other._size;
            _duration         = other._duration;
            _descriptionIndex = other._descriptionIndex;
            _dts              = other._dts;
            _ctsDelta         = other._ctsDelta;
            _isSync           = other._isSync;

            return this;
        }

        /*----------------------------------------------------------------------
        |   AP4_Sample::ReadData
        +---------------------------------------------------------------------*/
        public Ap4Result ReadData(DataBuffer data)
        {
            return ReadData(data, _size);
        }

        /*----------------------------------------------------------------------
        |   AP4_Sample::ReadData
        +---------------------------------------------------------------------*/
        public Ap4Result ReadData(DataBuffer data, uint size, uint offset = 0)
        {
            // check that we have a stream
            if (_dataStream == null) return Ap4Result.FAILURE;

            // shortcut
            if (size == 0) return Ap4Result.SUCCESS;

            // check the size
            if (_size < size+offset) return Ap4Result.FAILURE;

            // check if there's enough data in the stream
            ulong stream_size = 0;
            Ap4Result result = _dataStream.GetSize(out stream_size);
            if (result.Succeeded()) {
                if (size + offset > stream_size) {
                    return Ap4Result.ERROR_OUT_OF_RANGE;
                }
            }

            // set the buffer size
            result = data.SetDataSize(size);
            if (result.Failed()) return result;

            // get the data from the stream
            result = _dataStream.Seek(_offset+offset);
            if (result.Failed()) return result;
            return _dataStream.Read(data.UseData(), size);
        }

        /*----------------------------------------------------------------------
        |   AP4_Sample::GetDataStream
        +---------------------------------------------------------------------*/
        public ByteStream GetDataStream()
        {
            return _dataStream;
        }

        /*----------------------------------------------------------------------
        |   AP4_Sample::SetDataStream
        +---------------------------------------------------------------------*/
        public void SetDataStream(ByteStream stream)
        {
            _dataStream = stream;
        }

        /*----------------------------------------------------------------------
        |   AP4_Sample::Detach
        +---------------------------------------------------------------------*/
        public void Detach()
        {
            _dataStream = null;
        }

        // sample properties accessors
        public ulong GetOffset() { return _offset; }
        public void  SetOffset(ulong offset) { _offset = offset; }
        public uint  GetSize() { return _size; }
        public void  SetSize(uint size) { _size = size; }
        public uint  GetDescriptionIndex() { return _descriptionIndex; }
        public void  SetDescriptionIndex(uint index) { _descriptionIndex = index; }

        /// <summary>
        /// Get the DTS (Decoding Time Stamp) of the sample in the timescale of the media
        /// </summary>
        public ulong GetDts() { return _dts; }

        /// <summary>
        /// Set the DTS (Decoding Time Stamp) of the sample in the timescale of the media
        /// </summary>
        public void SetDts(ulong dts) { _dts = dts; }

        /// <summary>
        /// Get the CTS (Composition Time Stamp) of the sample in the timescale of the media
        /// </summary>
        public ulong GetCts() { return unchecked(_dts+(ulong)(long)_ctsDelta); } // SI32 -> UI64 conversion sign-extends, as in C++

        /// <summary>
        /// Set the CTS (Composition Time Stamp) of the sample in the timescale of the media
        /// </summary>
        public void SetCts(ulong cts) { _ctsDelta = (cts > _dts) ? unchecked((int)(uint)(cts-_dts)) : 0;  }

        /// <summary>
        /// Get the CTS Delta (difference between the CTS (Composition Time Stamp) and DTS (Decoding Time Stamp)
        /// of the sample in the timescale of the media.
        /// </summary>
        public uint GetCtsDelta() { return unchecked((uint)_ctsDelta); }

        /// <summary>
        /// Set the CTS Delta (difference between the CTS (Composition Time Stamp) and DTS (Decoding Time Stamp)
        /// of the sample in the timescale of the media.
        /// </summary>
        public void SetCtsDelta(uint delta) { _ctsDelta = unchecked((int)delta);  }

        /// <summary>
        /// Get the duration of the sample in the timescale of the media
        /// </summary>
        public uint GetDuration() { return _duration; }

        /// <summary>
        /// Set the duration of the sample in the timescale of the media
        /// </summary>
        public void SetDuration(uint duration) { _duration = duration; }

        /// <summary>
        /// Return whether the sample is a sync (random-access point) sample or not.
        /// </summary>
        public bool IsSync() { return _isSync; }

        /// <summary>
        /// Set whether the sample is a sync (random-access point) sample or not.
        /// </summary>
        public void SetSync(bool is_sync) { _isSync = is_sync; }

        /*----------------------------------------------------------------------
        |   AP4_Sample::Reset
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Resets the sample: will also release any data stream reference
        /// </summary>
        public void Reset()
        {
            _dataStream = null; // AP4_RELEASE(m_DataStream) also sets the pointer to NULL

            _offset           = 0;
            _size             = 0;
            _duration         = 0;
            _descriptionIndex = 0;
            _dts              = 0;
            _ctsDelta         = 0;
            _isSync           = false;
        }

        private ByteStream  _dataStream;
        private ulong       _offset;
        private uint        _size;
        private uint        _duration;
        private uint        _descriptionIndex;
        private ulong       _dts;
        private int         _ctsDelta; // make this a signed value, because quicktime can use negative offsets
        private bool        _isSync;
    }
}
