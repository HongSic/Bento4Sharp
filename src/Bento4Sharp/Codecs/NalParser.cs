// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Codecs/Ap4NalParser.h, Ap4NalParser.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   NalParser
    +---------------------------------------------------------------------*/
    public class NalParser
    {
        // class methods
        /// <summary>
        /// Remove emulation prevention bytes from a buffer.
        /// </summary>
        public static void Unescape(DataBuffer data)
        {
            uint zero_count = 0;
            uint bytes_removed = 0;
            byte[] @out   = data.UseData();
            byte[] @in    = data.GetData();
            uint  in_size = data.GetDataSize();

            for (uint i=0; i<in_size; i++) {
                if (zero_count == 2 && @in[i] == 3 && i+1 < in_size && @in[i+1] <= 3) {
                    ++bytes_removed;
                    zero_count = 0;
                } else {
                    @out[i-bytes_removed] = @in[i];
                    if (@in[i] == 0) {
                        ++zero_count;
                    } else {
                        zero_count = 0;
                    }
                }
            }
            data.SetDataSize(in_size-bytes_removed);
        }

        /// <summary>
        /// Count how many emualation prevention bytes are encountered until
        /// a certain number of bytes can be produced from an escaped buffer
        /// </summary>
        public static uint CountEmulationPreventionBytes(byte[] data,
                                                         uint   data_size,
                                                         uint   unescaped_size)
        {
            return CountEmulationPreventionBytes(data, 0, data_size, unescaped_size);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer
        public static uint CountEmulationPreventionBytes(byte[] data,
                                                         int    offset,
                                                         uint   data_size,
                                                         uint   unescaped_size)
        {
            uint zero_count = 0;
            uint bytes_produced = 0;
            uint emulation_prevention_bytes = 0;

            // shortcut
            if (data_size <= 2) {
                // no escaping possible in just 2 bytes
                return 0;
            }

            for (uint i=0; i<data_size; i++) {
                if (zero_count == 2 && data[offset+i] == 3 && i+1 < data_size && data[offset+i+1] <= 3) {
                    ++emulation_prevention_bytes;
                    zero_count = 0;
                } else {
                    if (++bytes_produced >= unescaped_size) {
                        break;
                    }
                    if (data[offset+i] == 0) {
                        ++zero_count;
                    } else {
                        zero_count = 0;
                    }
                }
            }

            return emulation_prevention_bytes;
        }

        // constructor
        public NalParser()
        {
            _state     = State.STATE_RESET;
            _zeroTrail = 0;
            _buffer    = new DataBuffer();
        }

        /// <summary>
        /// Feed some data to the parser and look for the next NAL Unit.
        /// </summary>
        /// <param name="data">Pointer to the memory buffer with the data to feed.</param>
        /// <param name="data_size">Size in bytes of the buffer pointed to by the
        /// data pointer.</param>
        /// <param name="bytes_consumed">Number of bytes from the data buffer that were
        /// consumed and stored by the parser.</param>
        /// <param name="nalu">Reference to a pointer to a buffer object that contains
        /// a NAL unit found in the previously fed data, or a NULL pointer if no
        /// NAL unit can be found so far.</param>
        /// <param name="eos">Boolean flag that indicates if this buffer is the last
        /// buffer in the stream/file (End Of Stream).</param>
        /// <returns><see cref="Ap4Result.SUCCESS"/> is the call succeeds, or an error code if it
        /// fails.</returns>
        /// <remarks>
        /// The caller must not feed the same data twice. When this method
        /// returns, the caller should inspect the value of bytes_consumed and
        /// advance the input stream source accordingly, such that the next
        /// buffer passed to this method will be exactly bytes_consumed bytes
        /// after what was passed in this call.
        /// </remarks>
        public Ap4Result Feed(byte[]          data,
                              uint            data_size,
                              out uint        bytes_consumed,
                              out DataBuffer  nalu,
                              bool            eos = false)
        {
            return Feed(data, 0, data_size, out bytes_consumed, out nalu, eos);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer.
        // `data` may be null when data_size is 0 (upstream call sites pass NULL, 0 to flush).
        public Ap4Result Feed(byte[]          data,
                              int             offset,
                              uint            data_size,
                              out uint        bytes_consumed,
                              out DataBuffer  nalu,
                              bool            is_eos = false)
        {
            // default return values
            nalu = null;
            bytes_consumed = 0;

            // iterate the state machine
            uint data_offset;
            uint payload_start = 0;
            uint payload_end   = 0;
            bool found_nalu = false;

            for (data_offset=0; data_offset<data_size && !found_nalu; data_offset++) {
                byte @byte = data[offset+data_offset];
                switch (_state) {
                    case State.STATE_RESET:
                        if (@byte == 0) {
                            _state = State.STATE_START_CODE_1;
                        }
                        break;

                    case State.STATE_START_CODE_1:
                        if (@byte == 0) {
                            _state = State.STATE_START_CODE_2;
                        } else {
                            _state = State.STATE_RESET;
                        }
                        break;

                    case State.STATE_START_CODE_2:
                        if (@byte == 0) break;
                        if (@byte == 1) {
                            _state = State.STATE_START_NALU;
                        } else {
                            _state = State.STATE_RESET;
                        }
                        break;

                    case State.STATE_START_NALU:
                        _buffer.SetDataSize(0);
                        _zeroTrail = 0;
                        payload_start = payload_end = data_offset;
                        _state = State.STATE_IN_NALU;
                        // FALLTHROUGH
                        goto case State.STATE_IN_NALU;

                    case State.STATE_IN_NALU:
                        if (@byte == 0) {
                            ++_zeroTrail;
                            ++payload_end;
                            break;
                        }
                        if (_zeroTrail >= 2) {
                            if (@byte == 1) {
                                found_nalu = true;
                                _state = State.STATE_START_NALU;
                                break;
                            } else {
                                ++payload_end;
                            }
                        } else {
                            ++payload_end;
                        }
                        _zeroTrail = 0;
                        break;
                }
            }
            if (is_eos && _state == State.STATE_IN_NALU && data_offset == data_size) {
                found_nalu = true;
                _zeroTrail = 0;
                _state = State.STATE_RESET;
            }
            if (payload_end > payload_start) {
                uint current_payload_size = _buffer.GetDataSize();
                _buffer.SetDataSize(_buffer.GetDataSize()+(payload_end-payload_start));
                Array.Copy(data, offset+(int)payload_start,
                           _buffer.UseData(), (int)current_payload_size,
                           (int)(payload_end-payload_start));
            }

            // compute how many bytes we have consumed
            bytes_consumed = data_offset;

            // return the NALU if we found one
            if (found_nalu) {
                // trim zero bytes that are part of the next start code
                if (_zeroTrail >= 3 && _buffer.GetDataSize() >= 3) {
                    // 4 byte start code
                    _buffer.SetDataSize(_buffer.GetDataSize()-3);
                } else if (_zeroTrail >= 2 && _buffer.GetDataSize() >= 2) {
                    // 3 byte start code
                    _buffer.SetDataSize(_buffer.GetDataSize()-2);
                }
                _zeroTrail = 0;
                nalu = _buffer;
            }

            return Ap4Result.SUCCESS;
        }

        /// <summary>
        /// Reset the state of the parser (for example, to parse a new stream).
        /// </summary>
        public Ap4Result Reset()
        {
            _state     = State.STATE_RESET;
            _zeroTrail = 0;
            _buffer.SetDataSize(0);

            return Ap4Result.SUCCESS;
        }

        protected enum State {
            STATE_RESET,
            STATE_START_CODE_1,
            STATE_START_CODE_2,
            STATE_START_NALU,
            STATE_IN_NALU
        }
        protected State      _state;
        protected uint       _zeroTrail;
        protected DataBuffer _buffer;
    }
}
