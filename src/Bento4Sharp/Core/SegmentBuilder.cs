// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SegmentBuilder.h, Ap4SegmentBuilder.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint SEGMENT_BUILDER_DEFAULT_TIMESCALE = 1000;
        public const uint STREAM_FEEDER_DEFAULT_BUFFER_SIZE = 65536;
    }

    /*----------------------------------------------------------------------
    |   AP4_SegmentBuilder
    +---------------------------------------------------------------------*/
    public abstract class SegmentBuilder
    {
        /*----------------------------------------------------------------------
        |   AP4_SegmentBuilder::AP4_SegmentBuilder
        +---------------------------------------------------------------------*/
        // constructor and destructor
        public SegmentBuilder(Track.Type track_type,
                              uint       track_id,
                              ulong      media_time_origin = 0)
        {
            _trackType = track_type;
            _trackId = track_id;
            _trackLanguage = "und";
            _timescale = Ap4.SEGMENT_BUILDER_DEFAULT_TIMESCALE;
            _sampleStartNumber = 0;
            _mediaTimeOrigin = media_time_origin;
            _mediaStartTime = 0;
            _mediaDuration = 0;
        }

        // accessors
        public uint         GetTrackId()        { return _trackId;        }
        public uint         GetTimescale()      { return _timescale;      }
        public ulong        GetMediaStartTime() { return _mediaStartTime; }
        public ulong        GetMediaDuration()  { return _mediaDuration;  }
        public List<Sample> GetSamples()        { return _samples;        }

        /*----------------------------------------------------------------------
        |   AP4_SegmentBuilder::AddSample
        +---------------------------------------------------------------------*/
        // methods
        public virtual Ap4Result
        AddSample(Sample sample)
        {
            // PORT NOTE: AP4_Array::Append stores a copy of the sample
            _samples.Add(new Sample(sample));
            _mediaDuration += sample.GetDuration();

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SegmentBuilder::WriteMediaSegment
        +---------------------------------------------------------------------*/
        public virtual Ap4Result
        WriteMediaSegment(ByteStream stream, uint sequence_number)
        {
            uint tfhd_flags = Ap4.TFHD_FLAG_DEFAULT_BASE_IS_MOOF;
            if (_trackType == Track.Type.TYPE_VIDEO) {
                tfhd_flags |= Ap4.TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT;
            }

            // setup the moof structure
            ContainerAtom moof = new ContainerAtom(AtomType.MOOF);
            MfhdAtom mfhd = new MfhdAtom(sequence_number);
            moof.AddChild(mfhd);
            ContainerAtom traf = new ContainerAtom(AtomType.TRAF);
            TfhdAtom tfhd = new TfhdAtom(tfhd_flags,
                                         _trackId,
                                         0,
                                         1,
                                         0,
                                         0,
                                         0);
            if ((tfhd_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT) != 0) {
                tfhd.SetDefaultSampleFlags(0x1010000); // sample_is_non_sync_sample=1, sample_depends_on=1 (not I frame)
            }

            traf.AddChild(tfhd);
            TfdtAtom tfdt = new TfdtAtom(1, _mediaTimeOrigin+_mediaStartTime);
            traf.AddChild(tfdt);
            uint trun_flags = Ap4.TRUN_FLAG_DATA_OFFSET_PRESENT     |
                              Ap4.TRUN_FLAG_SAMPLE_DURATION_PRESENT |
                              Ap4.TRUN_FLAG_SAMPLE_SIZE_PRESENT;
            uint first_sample_flags = 0;
            if (_trackType == Track.Type.TYPE_VIDEO) {
                trun_flags |= Ap4.TRUN_FLAG_FIRST_SAMPLE_FLAGS_PRESENT;
                first_sample_flags = 0x2000000; // sample_depends_on=2 (I frame)
            }
            TrunAtom trun = new TrunAtom(trun_flags, 0, first_sample_flags);

            traf.AddChild(trun);
            moof.AddChild(traf);

            // add samples to the fragment
            List<TrunAtom.Entry> trun_entries = new List<TrunAtom.Entry>();
            uint                 mdat_size = Ap4.ATOM_HEADER_SIZE;
            // trun_entries.SetItemCount(m_Samples.ItemCount())
            for (int i=0; i<_samples.Count; i++) {
                trun_entries.Add(new TrunAtom.Entry());
            }
            for (int i=0; i<_samples.Count; i++) {
                // if we have one non-zero CTS delta, we'll need to express it
                if (_samples[i].GetCtsDelta() != 0) {
                    trun.SetFlags(trun.GetFlags() | Ap4.TRUN_FLAG_SAMPLE_COMPOSITION_TIME_OFFSET_PRESENT);
                }

                // add one sample
                TrunAtom.Entry trun_entry = trun_entries[i];
                trun_entry.sample_duration                = _samples[i].GetDuration();
                trun_entry.sample_size                    = _samples[i].GetSize();
                trun_entry.sample_composition_time_offset = _samples[i].GetCtsDelta();

                mdat_size += trun_entry.sample_size;
            }

            // update moof and children
            trun.SetEntries(trun_entries);
            trun.SetDataOffset(unchecked((int)((uint)moof.GetSize()+Ap4.ATOM_HEADER_SIZE)));

            // write moof
            moof.Write(stream);

            // write mdat
            stream.WriteUI32(mdat_size);
            stream.WriteUI32(AtomType.MDAT);
            for (int i=0; i<_samples.Count; i++) {
                Ap4Result result;
                ByteStream data_stream = _samples[i].GetDataStream();
                result = data_stream.Seek(_samples[i].GetOffset());
                if (result.Failed()) {
                    return result;
                }
                result = data_stream.CopyTo(stream, _samples[i].GetSize());
                if (result.Failed()) {
                    return result;
                }
            }

            // update counters
            _sampleStartNumber += (ulong)_samples.Count;
            _mediaStartTime    += _mediaDuration;
            _mediaDuration      = 0;

            // cleanup
            _samples.Clear();

            return Ap4Result.SUCCESS;
        }

        public abstract Ap4Result WriteInitSegment(ByteStream stream);

        // members
        protected Track.Type   _trackType;
        protected uint         _trackId;
        protected string       _trackLanguage;
        protected uint         _timescale;
        protected ulong        _sampleStartNumber;
        protected ulong        _mediaTimeOrigin;
        protected ulong        _mediaStartTime;
        protected ulong        _mediaDuration;
        protected List<Sample> _samples = new List<Sample>();
    }

    /*----------------------------------------------------------------------
    |   AP4_FeedSegmentBuilder
    +---------------------------------------------------------------------*/
    public abstract class FeedSegmentBuilder : SegmentBuilder
    {
        /*----------------------------------------------------------------------
        |   AP4_FeedSegmentBuilder::AP4_FeedSegmentBuilder
        +---------------------------------------------------------------------*/
        // constructor
        public FeedSegmentBuilder(Track.Type track_type,
                                  uint       track_id,
                                  ulong      media_time_origin = 0) :
            base(track_type, track_id, media_time_origin)
        {
        }

        // methods
        // PORT NOTE: `const void* data` is a byte[] plus an offset (AP4_StreamFeeder
        // passes a pointer into its buffer); the offset-less twin has the upstream
        // signature. data == null means "end of stream", as upstream.
        public abstract Ap4Result Feed(byte[]   data,
                                       int      data_offset,
                                       uint     data_size,
                                       out uint bytes_consumed);

        public Ap4Result Feed(byte[]   data,
                              uint     data_size,
                              out uint bytes_consumed)
        {
            return Feed(data, 0, data_size, out bytes_consumed);
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_VideoSegmentBuilder
    +---------------------------------------------------------------------*/
    public abstract class VideoSegmentBuilder : FeedSegmentBuilder
    {
        /*----------------------------------------------------------------------
        |   AP4_VideoSegmentBuilder::AP4_VideoSegmentBuilder
        +---------------------------------------------------------------------*/
        // constructor
        public VideoSegmentBuilder(uint   track_id,
                                   double frames_per_second,
                                   ulong  media_time_origin = 0) :
            base(Track.Type.TYPE_VIDEO, track_id, media_time_origin)
        {
            _framesPerSecond = frames_per_second;
            _timescale = (uint)(frames_per_second*1000.0);
        }

        // types
        // PORT NOTE: a struct upstream; a class here so that elements of the
        // list can be modified in place. Swapping two elements swaps references.
        protected class SampleOrder {
            public SampleOrder(uint decode_order, uint display_order) {
                _decodeOrder = decode_order;
                _displayOrder = display_order;
            }
            public uint _decodeOrder;
            public uint _displayOrder;
        }

        /*----------------------------------------------------------------------
        |   AP4_VideoSegmentBuilder::SortSamples
        +---------------------------------------------------------------------*/
        // PORT NOTE: `SampleOrder* array` is the list plus the index of the first element
        protected void
        SortSamples(List<SampleOrder> array, int array_offset, uint n)
        {
            if (n < 2) {
                return;
            }
            SampleOrder pivot = new SampleOrder(array[array_offset + (int)(n / 2)]._decodeOrder,
                                                array[array_offset + (int)(n / 2)]._displayOrder);
            int left  = array_offset;
            int right = array_offset + (int)n - 1;
            while (left <= right) {
                if (array[left]._displayOrder < pivot._displayOrder) {
                    ++left;
                    continue;
                }
                if (array[right]._displayOrder > pivot._displayOrder) {
                    --right;
                    continue;
                }
                SampleOrder temp = array[left];
                array[left++] = array[right];
                array[right--] = temp;
            }
            SortSamples(array, array_offset, (uint)(right - array_offset + 1));
            SortSamples(array, left, (uint)(array_offset + (int)n - left));
        }

        /*----------------------------------------------------------------------
        |   AP4_VideoSegmentBuilder::WriteVideoInitSegment
        +---------------------------------------------------------------------*/
        protected Ap4Result
        WriteVideoInitSegment(ByteStream         stream,
                              SampleDescription  sample_description,
                              uint               width,
                              uint               height,
                              uint               brand)
        {
            // create the output file object
            Movie output_movie = new Movie(Ap4.SEGMENT_BUILDER_DEFAULT_TIMESCALE);

            // create an mvex container
            ContainerAtom mvex = new ContainerAtom(AtomType.MVEX);
            MehdAtom mehd = new MehdAtom(0);
            mvex.AddChild(mehd);

            // create a sample table (with no samples) to hold the sample description
            SyntheticSampleTable sample_table = new SyntheticSampleTable();
            sample_table.AddSampleDescription(sample_description, true);

            // create the track
            Track output_track = new Track(Track.Type.TYPE_VIDEO,
                                           sample_table,
                                           _trackId,
                                           Ap4.SEGMENT_BUILDER_DEFAULT_TIMESCALE,
                                           0,
                                           _timescale,
                                           0,
                                           _trackLanguage,
                                           width << 16,
                                           height << 16);
            output_movie.AddTrack(output_track);

            // add a trex entry to the mvex container
            TrexAtom trex = new TrexAtom(_trackId,
                                         1,
                                         0,
                                         0,
                                         0);
            mvex.AddChild(trex);

            // update the mehd duration
            // TBD mehd->SetDuration(0);

            // the mvex container to the moov container
            output_movie.GetMoovAtom().AddChild(mvex);

            // write the ftyp atom
            List<uint> brands = new List<uint>();
            brands.Add(Ap4.FILE_BRAND_ISOM);
            brands.Add(Ap4.FILE_BRAND_MP42);
            brands.Add(Ap4.FILE_BRAND_MP41);
            brands.Add(brand);

            FtypAtom ftyp = new FtypAtom(Ap4.FILE_BRAND_MP42, 1, brands.ToArray(), (uint)brands.Count);
            ftyp.Write(stream);

            // write the moov atom
            Ap4Result result = output_movie.GetMoovAtom().Write(stream);
            if (result.Failed()) {
                return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_VideoSegmentBuilder::WriteMediaSegment
        +---------------------------------------------------------------------*/
        // AP4_SegmentBuilder methods
        public override Ap4Result
        WriteMediaSegment(ByteStream stream, uint sequence_number)
        {
            if (_sampleOrders.Count > 1) {
                // rebase the decode order
                uint decode_order_base = _sampleOrders[0]._decodeOrder;
                for (int i=0; i<_sampleOrders.Count; i++) {
                    if (_sampleOrders[i]._decodeOrder >= decode_order_base) {
                        _sampleOrders[i]._decodeOrder -= decode_order_base;
                    }
                }

                // adjust the sample CTS/DTS offsets based on the sample orders
                uint start = 0;
                for (uint i=1; i<=_sampleOrders.Count; i++) {
                    if (i == _sampleOrders.Count || _sampleOrders[(int)i]._displayOrder == 0) {
                        // we got to the end of the GOP, sort it by display order
                        SortSamples(_sampleOrders, (int)start, i-start);
                        start = i;
                    }
                }

                // compute the max CTS delta
                uint max_delta = 0;
                for (uint i=0; i<_sampleOrders.Count; i++) {
                    if (_sampleOrders[(int)i]._decodeOrder > i) {
                        uint delta =_sampleOrders[(int)i]._decodeOrder-i;
                        if (delta > max_delta) {
                            max_delta = delta;
                        }
                    }
                }

                // set the CTS for all samples
                for (uint i=0; i<_sampleOrders.Count; i++) {
                    // PORT NOTE: upstream reads m_Samples[i] without a bounds check;
                    // the value is only used when m_Timescale is 0.
                    ulong dts = i < _samples.Count ? _samples[(int)i].GetDts() : 0;
                    if (_timescale != 0) {
                        dts = (ulong)((double)_timescale/_framesPerSecond*(double)(i+max_delta));
                    }
                    if (_sampleOrders[(int)i]._decodeOrder < _samples.Count) {
                        _samples[(int)_sampleOrders[(int)i]._decodeOrder].SetCts(dts);
                    }
                }

                _sampleOrders.Clear();
            }

            return base.WriteMediaSegment(stream, sequence_number);
        }

        // members
        protected double            _framesPerSecond;
        protected List<SampleOrder> _sampleOrders = new List<SampleOrder>();
    }

    /*----------------------------------------------------------------------
    |   AP4_AvcSegmentBuilder
    +---------------------------------------------------------------------*/
    public class AvcSegmentBuilder : VideoSegmentBuilder
    {
        /*----------------------------------------------------------------------
        |   AP4_AvcSegmentBuilder::AP4_AvcSegmentBuilder
        +---------------------------------------------------------------------*/
        // constructor
        public AvcSegmentBuilder(uint   track_id,
                                 double frames_per_second,
                                 ulong  media_time_origin = 0) :
            base(track_id, frames_per_second, media_time_origin)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_AvcSegmentBuilder::Feed
        +---------------------------------------------------------------------*/
        public override Ap4Result
        Feed(byte[]   data,
             int      data_offset,
             uint     data_size,
             out uint bytes_consumed)
        {
            Ap4Result result;

            AvcFrameParser.AccessUnitInfo access_unit_info = new AvcFrameParser.AccessUnitInfo();
            result = _frameParser.Feed(data, data_offset, data_size, out bytes_consumed, access_unit_info, data == null);
            if (result.Failed()) return result;

            // check if we have an access unit
            if (access_unit_info.nal_units.Count != 0) {
                // compute the total size of the sample data
                uint sample_data_size = 0;
                for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                    sample_data_size += 4+access_unit_info.nal_units[i].GetDataSize();
                }

                // format the sample data
                MemoryByteStream sample_data = new MemoryByteStream(sample_data_size);
                for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                    sample_data.WriteUI32(access_unit_info.nal_units[i].GetDataSize());
                    sample_data.Write(access_unit_info.nal_units[i].GetData(), access_unit_info.nal_units[i].GetDataSize());
                }

                // compute the timestamp in a drift-less manner
                uint  duration = 0;
                ulong dts      = 0;
                if (_timescale !=0 && _framesPerSecond != 0.0) {
                    ulong this_sample_time = _mediaStartTime+_mediaDuration;
                    ulong next_sample_time = (ulong)((double)_timescale*(double)(_sampleStartNumber+(ulong)_samples.Count+1)/_framesPerSecond);
                    duration = (uint)(next_sample_time-this_sample_time);
                    dts      = (ulong)((double)_timescale/_framesPerSecond*(double)_samples.Count);
                }

                // create a new sample and add it to the list
                Sample sample = new Sample(sample_data, 0, sample_data_size, duration, 0, dts, 0, access_unit_info.is_idr);
                AddSample(sample);

                // remember the sample order
                _sampleOrders.Add(new SampleOrder(access_unit_info.decode_order, access_unit_info.display_order));

                // free the memory buffers
                access_unit_info.nal_units.Clear();

                return (Ap4Result)1; // one access unit returned
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AvcSegmentBuilder::WriteInitSegment
        +---------------------------------------------------------------------*/
        // AP4_SegmentBuilder methods
        public override Ap4Result
        WriteInitSegment(ByteStream stream)
        {
            // compute the track parameters
            AvcSequenceParameterSet sps = null;
            for (uint i=0; i<=Ap4.AVC_SPS_MAX_ID; i++) {
                if (_frameParser.GetSequenceParameterSets()[i] != null) {
                    sps = _frameParser.GetSequenceParameterSets()[i];
                    break;
                }
            }
            if (sps == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            uint video_width = 0;
            uint video_height = 0;
            sps.GetInfo(out video_width, out video_height);

            // collect the SPS and PPS into arrays
            List<DataBuffer> sps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.AVC_SPS_MAX_ID; i++) {
                if (_frameParser.GetSequenceParameterSets()[i] != null) {
                    sps_array.Add(new DataBuffer(_frameParser.GetSequenceParameterSets()[i].raw_bytes));
                }
            }
            List<DataBuffer> pps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.AVC_PPS_MAX_ID; i++) {
                if (_frameParser.GetPictureParameterSets()[i] != null) {
                    pps_array.Add(new DataBuffer(_frameParser.GetPictureParameterSets()[i].raw_bytes));
                }
            }

            // setup the video the sample descripton
            AvcSampleDescription sample_description =
                new AvcSampleDescription(Ap4.SAMPLE_FORMAT_AVC1,
                                         (ushort)video_width,
                                         (ushort)video_height,
                                         24,
                                         "h264",
                                         (byte)sps.profile_idc,
                                         (byte)sps.level_idc,
                                         (byte)(sps.constraint_set0_flag<<7 |
                                                sps.constraint_set1_flag<<6 |
                                                sps.constraint_set2_flag<<5 |
                                                sps.constraint_set3_flag<<4),
                                         4,
                                         (byte)sps.chroma_format_idc,
                                         (byte)sps.bit_depth_luma_minus8,
                                         (byte)sps.bit_depth_chroma_minus8,
                                         sps_array,
                                         pps_array);

            // let the base class finish the work
            return WriteVideoInitSegment(stream,
                                         sample_description,
                                         video_width,
                                         video_height,
                                         Ap4.FILE_BRAND_AVC1);
        }

        // members
        protected AvcFrameParser _frameParser = new AvcFrameParser();
    }

    /*----------------------------------------------------------------------
    |   AP4_HevcSegmentBuilder
    +---------------------------------------------------------------------*/
    public class HevcSegmentBuilder : VideoSegmentBuilder
    {
        /*----------------------------------------------------------------------
        |   AP4_HevcSegmentBuilder::AP4_HevcSegmentBuilder
        +---------------------------------------------------------------------*/
        // constructor
        public HevcSegmentBuilder(uint   track_id,
                                  double frames_per_second,
                                  uint   video_format = Ap4.SAMPLE_FORMAT_HEV1,
                                  ulong  media_time_origin = 0) :
            base(track_id, frames_per_second, media_time_origin)
        {
            _videoFormat = video_format;
        }

        /*----------------------------------------------------------------------
        |   AP4_HevcSegmentBuilder::Feed
        +---------------------------------------------------------------------*/
        public override Ap4Result
        Feed(byte[]   data,
             int      data_offset,
             uint     data_size,
             out uint bytes_consumed)
        {
            Ap4Result result;

            HevcFrameParser.AccessUnitInfo access_unit_info = new HevcFrameParser.AccessUnitInfo();
            result = _frameParser.Feed(data, data_offset, data_size, out bytes_consumed, access_unit_info, data == null);
            if (result.Failed()) return result;

            // check if we have an access unit
            if (access_unit_info.nal_units.Count != 0) {
                // compute the total size of the sample data
                uint sample_data_size = 0;
                for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                    sample_data_size += 4+access_unit_info.nal_units[i].GetDataSize();
                }

                // format the sample data
                MemoryByteStream sample_data = new MemoryByteStream(sample_data_size);
                for (int i=0; i<access_unit_info.nal_units.Count; i++) {
                    sample_data.WriteUI32(access_unit_info.nal_units[i].GetDataSize());
                    sample_data.Write(access_unit_info.nal_units[i].GetData(), access_unit_info.nal_units[i].GetDataSize());
                }

                // compute the timestamp in a drift-less manner
                uint  duration = 0;
                ulong dts      = 0;
                if (_timescale !=0 && _framesPerSecond != 0.0) {
                    ulong this_sample_time = _mediaStartTime+_mediaDuration;
                    ulong next_sample_time = (ulong)((double)_timescale*(double)(_sampleStartNumber+(ulong)_samples.Count+1)/_framesPerSecond);
                    duration = (uint)(next_sample_time-this_sample_time);
                    dts      = (ulong)((double)_timescale/_framesPerSecond*(double)_samples.Count);
                }

                // create a new sample and add it to the list
                Sample sample = new Sample(sample_data, 0, sample_data_size, duration, 0, dts, 0, access_unit_info.is_random_access);
                AddSample(sample);

                // remember the sample order
                _sampleOrders.Add(new SampleOrder(access_unit_info.decode_order, access_unit_info.display_order));

                // free the memory buffers
                access_unit_info.nal_units.Clear();

                return (Ap4Result)1; // one access unit returned
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_HevcSegmentBuilder::WriteInitSegment
        +---------------------------------------------------------------------*/
        // AP4_SegmentBuilder methods
        public override Ap4Result
        WriteInitSegment(ByteStream stream)
        {
            // check that we have at least one SPS
            HevcSequenceParameterSet sps = null;
            for (uint i=0; i<=Ap4.HEVC_SPS_MAX_ID; i++) {
                sps = _frameParser.GetSequenceParameterSets()[i];
                if (sps != null) break;
            }
            if (sps == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }
            uint video_width = 0;
            uint video_height = 0;
            sps.GetInfo(out video_width, out video_height);

            // collect parameters from the first SPS entry
            // TODO: synthesize from multiple SPS entries if we have more than one
            byte   general_profile_space =               (byte)sps.profile_tier_level.general_profile_space;
            byte   general_tier_flag =                   (byte)sps.profile_tier_level.general_tier_flag;
            byte   general_profile =                     (byte)sps.profile_tier_level.general_profile_idc;
            uint   general_profile_compatibility_flags = sps.profile_tier_level.general_profile_compatibility_flags;
            ulong  general_constraint_indicator_flags =  sps.profile_tier_level.general_constraint_indicator_flags;
            byte   general_level =                       (byte)sps.profile_tier_level.general_level_idc;
            uint   min_spatial_segmentation =            0; // TBD (should read from VUI if present)
            byte   parallelism_type =                    0; // unknown
            byte   chroma_format =                       (byte)sps.chroma_format_idc;
            byte   luma_bit_depth =                      8; // hardcoded temporarily, should be read from the bitstream
            byte   chroma_bit_depth =                    8; // hardcoded temporarily, should be read from the bitstream
            ushort average_frame_rate =                  0; // unknown
            byte   constant_frame_rate =                 0; // unknown
            byte   num_temporal_layers =                 0; // unknown
            byte   temporal_id_nested =                  0; // unknown
            byte   nalu_length_size =                    4;

            // collect the VPS, SPS and PPS into arrays
            List<DataBuffer> vps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.HEVC_VPS_MAX_ID; i++) {
                if (_frameParser.GetVideoParameterSets()[i] != null) {
                    vps_array.Add(new DataBuffer(_frameParser.GetVideoParameterSets()[i].raw_bytes));
                }
            }
            List<DataBuffer> sps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.HEVC_SPS_MAX_ID; i++) {
                if (_frameParser.GetSequenceParameterSets()[i] != null) {
                    sps_array.Add(new DataBuffer(_frameParser.GetSequenceParameterSets()[i].raw_bytes));
                }
            }
            List<DataBuffer> pps_array = new List<DataBuffer>();
            for (uint i=0; i<=Ap4.HEVC_PPS_MAX_ID; i++) {
                if (_frameParser.GetPictureParameterSets()[i] != null) {
                    pps_array.Add(new DataBuffer(_frameParser.GetPictureParameterSets()[i].raw_bytes));
                }
            }

            // setup the video the sample descripton
            byte parameters_completeness = (byte)(_videoFormat == Ap4.SAMPLE_FORMAT_HVC1 ? 1 : 0);
            HevcSampleDescription sample_description =
                new HevcSampleDescription(_videoFormat,
                                          (ushort)video_width,
                                          (ushort)video_height,
                                          24,
                                          "HEVC Coding",
                                          general_profile_space,
                                          general_tier_flag,
                                          general_profile,
                                          general_profile_compatibility_flags,
                                          general_constraint_indicator_flags,
                                          general_level,
                                          min_spatial_segmentation,
                                          parallelism_type,
                                          chroma_format,
                                          luma_bit_depth,
                                          chroma_bit_depth,
                                          average_frame_rate,
                                          constant_frame_rate,
                                          num_temporal_layers,
                                          temporal_id_nested,
                                          nalu_length_size,
                                          vps_array,
                                          parameters_completeness,
                                          sps_array,
                                          parameters_completeness,
                                          pps_array,
                                          parameters_completeness);

            // let the base class finish the work
            return WriteVideoInitSegment(stream,
                                         sample_description,
                                         video_width,
                                         video_height,
                                         Ap4.FILE_BRAND_HVC1);
        }

        // members
        protected HevcFrameParser _frameParser = new HevcFrameParser();
        protected uint            _videoFormat;
    }

    /*----------------------------------------------------------------------
    |   AP4_AacSegmentBuilder
    +---------------------------------------------------------------------*/
    public class AacSegmentBuilder : FeedSegmentBuilder
    {
        /*----------------------------------------------------------------------
        |   AP4_AacSegmentBuilder::AP4_AacSegmentBuilder
        +---------------------------------------------------------------------*/
        // constructor
        public AacSegmentBuilder(uint track_id, ulong media_time_origin = 0) :
            base(Track.Type.TYPE_AUDIO, track_id, media_time_origin)
        {
            _sampleDescription = null;
            _timescale = 0; // we will compute the real value when we get the first audio frame
        }

        /*----------------------------------------------------------------------
        |   AP4_AacSegmentBuilder::Feed
        +---------------------------------------------------------------------*/
        public override Ap4Result
        Feed(byte[]   data,
             int      data_offset,
             uint     data_size,
             out uint bytes_consumed)
        {
            bytes_consumed = 0;

            // try to get a frame
            AacFrame frame = new AacFrame();
            Ap4Result result = _frameParser.FindFrame(frame);
            if (result.Succeeded()) {
                if (_sampleDescription == null) {
                    // create a sample description for our samples
                    DataBuffer dsi = new DataBuffer();
                    byte[] aac_dsi = new byte[2];

                    uint object_type = 2; // AAC LC by default
                    aac_dsi[0] = (byte)((object_type<<3) | (frame._info._samplingFrequencyIndex>>1));
                    aac_dsi[1] = (byte)(((frame._info._samplingFrequencyIndex&1)<<7) | (frame._info._channelConfiguration<<3));

                    dsi.SetData(aac_dsi, 2);
                    _sampleDescription =
                        new MpegAudioSampleDescription(
                        Ap4.OTI_MPEG4_AUDIO,   // object type
                        (uint)frame._info._samplingFrequency,
                        16,                    // sample size
                        (ushort)frame._info._channelConfiguration,
                        dsi,                   // decoder info
                        6144,                  // buffer size
                        128000,                // max bitrate
                        128000);               // average bitrate

                    _timescale = (uint)frame._info._samplingFrequency;
                }

                // read and store the sample data
                DataBuffer sample_data = new DataBuffer(frame._info._frameLength);
                sample_data.SetDataSize(frame._info._frameLength);
                frame._source.ReadBytes(sample_data.UseData(), frame._info._frameLength);
                MemoryByteStream sample_data_stream = new MemoryByteStream(frame._info._frameLength);
                sample_data_stream.Write(sample_data.GetData(), sample_data.GetDataSize());

                // add the sample to the table
                Sample sample = new Sample(sample_data_stream, 0, frame._info._frameLength, 1024, 0, 0, 0, true);
                AddSample(sample);

                return (Ap4Result)1;
            }

            // not enough data in the parser for a frame, feed some more
            if (data == null) {
                // PORT NOTE: m_FrameParser.Feed(NULL, NULL, AP4_BITSTREAM_FLAG_EOS)
                uint no_size = 0;
                _frameParser.Feed(null, ref no_size, Ap4.BITSTREAM_FLAG_EOS);
            } else {
                uint can_feed = _frameParser.GetBytesFree();
                if (can_feed > data_size) {
                    can_feed = data_size;
                }
                result = _frameParser.Feed(data, data_offset, ref can_feed);
                if (result.Succeeded()) {
                    bytes_consumed += can_feed;
                }
            }
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AacSegmentBuilder::WriteInitSegment
        +---------------------------------------------------------------------*/
        // AP4_SegmentBuilder methods
        public override Ap4Result
        WriteInitSegment(ByteStream stream)
        {
            Ap4Result result;

            // check that we have a sample description
            if (_sampleDescription == null) {
                return Ap4Result.ERROR_INVALID_STATE;
            }

            // create the output file object
            Movie output_movie = new Movie(Ap4.SEGMENT_BUILDER_DEFAULT_TIMESCALE);

            // create an mvex container
            ContainerAtom mvex = new ContainerAtom(AtomType.MVEX);
            MehdAtom mehd = new MehdAtom(0);
            mvex.AddChild(mehd);

            // create a sample table (with no samples) to hold the sample description
            SyntheticSampleTable sample_table = new SyntheticSampleTable();
            sample_table.AddSampleDescription(_sampleDescription, false);

            // create the track
            Track output_track = new Track(Track.Type.TYPE_AUDIO,
                                           sample_table,
                                           _trackId,
                                           Ap4.SEGMENT_BUILDER_DEFAULT_TIMESCALE,
                                           0,
                                           _timescale,
                                           0,
                                           _trackLanguage,
                                           0,
                                           0);
            output_movie.AddTrack(output_track);

            // add a trex entry to the mvex container
            TrexAtom trex = new TrexAtom(_trackId,
                                         1,
                                         0,
                                         0,
                                         0);
            mvex.AddChild(trex);

            // the mvex container to the moov container
            output_movie.GetMoovAtom().AddChild(mvex);

            // write the ftyp atom
            List<uint> brands = new List<uint>();
            brands.Add(Ap4.FILE_BRAND_ISOM);
            brands.Add(Ap4.FILE_BRAND_MP42);
            brands.Add(Ap4.FILE_BRAND_MP41);

            FtypAtom ftyp = new FtypAtom(Ap4.FILE_BRAND_MP42, 1, brands.ToArray(), (uint)brands.Count);
            ftyp.Write(stream);

            // write the moov atom
            result = output_movie.GetMoovAtom().Write(stream);

            return result;
        }

        // members
        protected AdtsParser                  _frameParser = new AdtsParser();
        protected MpegAudioSampleDescription  _sampleDescription;
    }

    /*----------------------------------------------------------------------
    |   AP4_StreamFeeder
    |
    |   Class that can be used to feed an AP4_FeedSegmentBuilder from a stream
    +---------------------------------------------------------------------*/
    public class StreamFeeder
    {
        /*----------------------------------------------------------------------
        |   AP4_StreamFeeder::AP4_StreamFeeder
        +---------------------------------------------------------------------*/
        // constructor and destructor
        public StreamFeeder(ByteStream source, FeedSegmentBuilder builder)
        {
            _source = source;
            _builder = builder;
            _feedBufferSize = 0;
            _feedBytesPending = 0;
            _feedBytesParsed = 0;
            Ap4.ASSERT(source != null);
            _feedBuffer = new byte[Ap4.STREAM_FEEDER_DEFAULT_BUFFER_SIZE];
            _feedBufferSize = Ap4.STREAM_FEEDER_DEFAULT_BUFFER_SIZE;
        }

        /*----------------------------------------------------------------------
        |   AP4_StreamFeeder::Feed
        +---------------------------------------------------------------------*/
        // methods
        public Ap4Result
        Feed() // Read some data from the stream and feed it to the builder
        {
            // read more data if the buffer is empty
            if (_feedBytesPending == 0) {
                _feedBytesParsed = 0;
                if (_feedBufferSize == 0) return Ap4Result.ERROR_INTERNAL;
                Ap4Result result_ = _source.ReadPartial(_feedBuffer, _feedBufferSize, out _feedBytesPending);
                if (result_.Failed()) {
                    return result_;
                }
                if (_feedBytesPending == 0) {
                    return Ap4Result.ERROR_EOS;
                }
            }

            // feed the builder
            uint bytes_consumed = 0;
            Ap4Result result = _builder.Feed(_feedBuffer, (int)_feedBytesParsed, _feedBytesPending, out bytes_consumed);
            if ((int)result < 0) return result;

            // update counters
            _feedBytesParsed  += bytes_consumed;
            _feedBytesPending -= bytes_consumed;

            return Ap4Result.SUCCESS;
        }

        private ByteStream         _source;
        private FeedSegmentBuilder _builder;
        private byte[]             _feedBuffer;
        private uint               _feedBufferSize;
        private uint               _feedBytesPending;  // number of bytes not yet parsed
        private uint               _feedBytesParsed;   // number of bytes already parsed
    }
}
