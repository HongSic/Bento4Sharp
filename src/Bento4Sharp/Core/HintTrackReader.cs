// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4HintTrackReader.h, Ap4HintTrackReader.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_HintTrackReader
    +---------------------------------------------------------------------*/
    public class HintTrackReader
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(Track                hint_track,
                                       Movie                movie,
                                       uint                 ssrc, // if 0, a random value is chosen
                                       out HintTrackReader  reader)
        {
            // default value
            reader = null;

            // check the type
            if (hint_track.GetType() != Track.Type.TYPE_HINT) {
                return Ap4Result.ERROR_INVALID_TRACK_TYPE;
            }

            // create a new object
            reader = new HintTrackReader(hint_track, movie, ssrc);

            return Ap4Result.SUCCESS;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::GetNextPacket
        +---------------------------------------------------------------------*/
        public Ap4Result GetNextPacket(DataBuffer packet_data,
                                       out uint   ts_ms)
        {
            Ap4Result result = Ap4Result.SUCCESS;

            // PORT NOTE: ts_ms is a reference left untouched on failure upstream.
            ts_ms = 0;

            // PORT NOTE: upstream dereferences a NULL m_RtpSampleData here when the
            // first hint sample could not be read (e.g. empty hint track).
            if (_rtpSampleData == null) return Ap4Result.ERROR_INVALID_STATE;

            // get the next rtp sample if needed
            List<RtpPacket> packets = _rtpSampleData.GetPackets();
            while (_packetIndex == (uint)packets.Count) { // while: handle the 0 packet case
                result = GetRtpSample(++_sampleIndex);
                if (result.Failed()) return result;
                packets = _rtpSampleData.GetPackets();
            }

            // get the packet
            RtpPacket packet;
            // result = packets->Get(m_PacketIndex++, packet);
            uint packet_index = _packetIndex++;
            if (packet_index >= (uint)packets.Count) return Ap4Result.ERROR_NO_SUCH_ITEM;
            packet = packets[(int)packet_index];

            // build it
            result = BuildRtpPacket(packet, packet_data);
            if (result.Failed()) return result;

            // set the time stamp
            ts_ms = GetCurrentTimeStampMs();

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::SeekToTimeStampMs
        +---------------------------------------------------------------------*/
        public Ap4Result SeekToTimeStampMs(uint     desired_ts_ms,
                                           out uint actual_ts_ms)
        {
            // PORT NOTE: actual_ts_ms is a reference left untouched on failure upstream.
            actual_ts_ms = 0;

            // get the sample index
            uint index;
            Ap4Result result = _hintTrack.GetSampleIndexForTimeStampMs(desired_ts_ms, out index);
            if (result.Failed()) return result;

            // get the current sample based on the index and renew the sample data
            result = GetRtpSample(index);
            if (result.Failed()) return result;

            // set the actual ts
            actual_ts_ms = GetCurrentTimeStampMs();
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::GetCurrentTimeStampMs
        +---------------------------------------------------------------------*/
        public uint GetCurrentTimeStampMs()
        {
            return unchecked((uint)Ap4Utils.ConvertTime(_currentHintSample.GetCts(),
                                                        _hintTrack.GetMediaTimeScale(),
                                                        1000));
        }

        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::Rewind
        +---------------------------------------------------------------------*/
        public Ap4Result Rewind()
        {
            _sampleIndex = 0;
            return GetRtpSample(_sampleIndex);
        }

        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::GetSdpText
        +---------------------------------------------------------------------*/
        public Ap4Result GetSdpText(out string sdp_text)
        {
            // PORT NOTE: sdp_text is a reference left untouched on failure upstream.
            sdp_text = "";

            TrakAtom tmp1;
            Atom sdp_atom = (tmp1 = _hintTrack.UseTrakAtom()) != null ? tmp1.FindChild("udta/hnti/sdp ") : null;
            if (sdp_atom == null) return Ap4Result.FAILURE;

            // C cast is OK because we know the type of the atom
            // PORT NOTE: upstream dereferences the result of the dynamic cast unchecked.
            SdpAtom sdp = sdp_atom as SdpAtom;
            if (sdp == null) return Ap4Result.FAILURE;
            sdp_text = sdp.GetSdpText();
            return Ap4Result.SUCCESS;
        }

        public Track GetMediaTrack() { return _mediaTrack; }

        // use the factory instead of the constructor
        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::AP4_HintTrackReader
        +---------------------------------------------------------------------*/
        private HintTrackReader(Track hint_track,
                                Movie movie,
                                uint  ssrc)
        {
            _hintTrack = hint_track;
            _mediaTrack = null;
            _mediaTimeScale = 0;
            _rtpSampleData = null;
            _ssrc = ssrc;
            _sampleIndex = 0;
            _packetIndex = 0;
            _rtpSequenceStart = 0;
            _rtpTimeStampStart = 0;
            _rtpTimeScale = 0;

            // get the media track
            // PORT NOTE: upstream dereferences hint_trak_atom, the result of the
            // dynamic casts and the media track without checking them; the port
            // skips the step when one of them is NULL (or the 'hint' reference
            // has no track id) instead of crashing.
            TrakAtom  hint_trak_atom = hint_track.UseTrakAtom();
            Atom atom = hint_trak_atom != null ? hint_trak_atom.FindChild("tref/hint") : null;
            if (atom != null) {
                TrefTypeAtom tref_type = atom as TrefTypeAtom;
                if (tref_type != null && tref_type.GetTrackIds().Count > 0) {
                    uint media_track_id = tref_type.GetTrackIds()[0];
                    _mediaTrack = movie.GetTrack(media_track_id);

                    // get the media time scale
                    if (_mediaTrack != null) {
                        _mediaTimeScale = _mediaTrack.GetMediaTimeScale();
                    }
                }
            }

            // initiate random generator
            // PORT NOTE: srand((int)time(NULL)) / rand() are replaced by System.Random
            // (time-seeded by default). The range of rand() is platform dependent
            // upstream (0..RAND_MAX); Random.Next() yields 0..0x7FFFFFFE.
            System.Random random = new System.Random();

            // rtp sequence start init TODO!!
            _rtpSequenceStart = (ushort)(random.Next()&0xFFFF);

            // rtp timestamp start init TODO!!
            _rtpTimeStampStart = (uint)random.Next();

            // rtp time scale
            // PORT NOTE: path kept as upstream (it has no 'stsd' component, so with
            // a standard file layout nothing is found and the time scale stays 0;
            // the member is not used anywhere else).
            atom = hint_trak_atom != null ? hint_trak_atom.FindChild("mdia/minf/stbl/rtp /tims") : null;
            if (atom != null) {
                TimsAtom tims = atom as TimsAtom;
                if (tims != null) {
                    _rtpTimeScale = tims.GetTimeScale();
                }
            }

            // generate a random ssrc if = 0
            if (_ssrc == 0) {
                _ssrc = (uint)random.Next();
            }

            // get the first sample
            GetRtpSample(0);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::GetRtpSample
        +---------------------------------------------------------------------*/
        private Ap4Result GetRtpSample(uint index)
        {
            // get the sample
            Ap4Result result = _hintTrack.GetSample(index, _currentHintSample);
            if (result.Failed()) return result;

            // renew the sample data
            ByteStream rtp_data_stream = _currentHintSample.GetDataStream();
            // PORT NOTE: upstream dereferences a NULL data stream here.
            if (rtp_data_stream == null) return Ap4Result.FAILURE;
            rtp_data_stream.Seek(_currentHintSample.GetOffset());
            _rtpSampleData = new RtpSampleData(rtp_data_stream,
                                               _currentHintSample.GetSize());

            // reinit the packet index
            _packetIndex = 0;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::BuildRtpPacket
        +---------------------------------------------------------------------*/
        private Ap4Result BuildRtpPacket(RtpPacket  packet,
                                         DataBuffer packet_data)
        {
            // set the data size
            Ap4Result result = packet_data.SetDataSize(packet.GetConstructedDataSize());
            if (result.Failed()) return result;

            // now write
            ByteStream stream = new MemoryByteStream(packet_data);

            // header + ssrc
            stream.WriteUI08((byte)(0x80 | ((packet.GetPBit() ? 1 : 0) << 5) | ((packet.GetXBit() ? 1 : 0) << 4)));
            stream.WriteUI08(unchecked((byte)(((packet.GetMBit() ? 1 : 0) << 7) | packet.GetPayloadType())));
            stream.WriteUI16(unchecked((ushort)(_rtpSequenceStart + packet.GetSequenceSeed())));
            stream.WriteUI32(unchecked(_rtpTimeStampStart + (uint)_currentHintSample.GetCts() + (uint)packet.GetTimeStampOffset()));
            stream.WriteUI32(_ssrc);

            foreach (RtpConstructor  constructor in packet.GetConstructors()) {
                // PORT NOTE: a NULL constructor (unparseable constructor in the hint
                // sample, see RtpPacket) is dereferenced upstream; treated here like
                // an unknown constructor type.
                if (constructor == null) return Ap4Result.FAILURE;

                // add data to the packet according to the constructor
                switch (constructor.GetType()) {
                    case Ap4.RTP_CONSTRUCTOR_TYPE_NOOP:
                        // nothing to do here
                        break;
                    case Ap4.RTP_CONSTRUCTOR_TYPE_IMMEDIATE:
                        result = WriteImmediateRtpData(
                            (ImmediateRtpConstructor)constructor, stream);
                        if (result.Failed()) return result;
                        break;
                    case Ap4.RTP_CONSTRUCTOR_TYPE_SAMPLE:
                        result = WriteSampleRtpData(
                            (SampleRtpConstructor)constructor, stream);
                        if (result.Failed()) return result;
                        break;
                    case Ap4.RTP_CONSTRUCTOR_TYPE_SAMPLE_DESC:
                        return Ap4Result.ERROR_NOT_SUPPORTED;
                    default:
                        // unknown constructor type
                        return Ap4Result.FAILURE;
                }
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::WriteImmediateRtpData
        +---------------------------------------------------------------------*/
        private Ap4Result WriteImmediateRtpData(ImmediateRtpConstructor constructor,
                                                ByteStream              data_stream)
        {
            DataBuffer data_buffer = constructor.GetData();
            return data_stream.Write(data_buffer.GetData(), data_buffer.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_HintTrackReader::WriteSampleRtpData
        +---------------------------------------------------------------------*/
        private Ap4Result WriteSampleRtpData(SampleRtpConstructor constructor,
                                             ByteStream           data_stream)
        {
            Track referenced_track = null;
            if (constructor.GetTrackRefIndex() == 0xFF) {
                // data is in the hint track
                referenced_track = _hintTrack;
            } else {
                // check if we have a media track
                if (_mediaTrack == null) return Ap4Result.FAILURE;
                referenced_track = _mediaTrack;
            }

            // write the sample data
            Sample sample = new Sample();
            Ap4Result result = referenced_track.GetSample(unchecked(constructor.GetSampleNum()-1), // adjust
                                                          sample);
            if (result.Failed()) return result;
            DataBuffer buffer = new DataBuffer(constructor.GetLength());
            result = sample.ReadData(
                buffer, constructor.GetLength(), constructor.GetSampleOffset());
            if (result.Failed()) return result;

            // write the data
            return data_stream.Write(buffer.GetData(), buffer.GetDataSize());
        }

        // members
        private Track          _hintTrack;
        private Track          _mediaTrack;
        private uint           _mediaTimeScale;
        private Sample         _currentHintSample = new Sample();
        private RtpSampleData  _rtpSampleData;
        private uint           _ssrc;
        private uint           _sampleIndex;
        private uint           _packetIndex;
        private ushort         _rtpSequenceStart;
        private uint           _rtpTimeStampStart;
        private uint           _rtpTimeScale;
    }
}
