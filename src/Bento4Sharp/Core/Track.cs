// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Track.h, Ap4Track.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint TRACK_DEFAULT_MOVIE_TIMESCALE = 1000;

        public const uint TRACK_FLAG_ENABLED    = 0x0001;
        public const uint TRACK_FLAG_IN_MOVIE   = 0x0002;
        public const uint TRACK_FLAG_IN_PREVIEW = 0x0004;
    }

    /*----------------------------------------------------------------------
    |   AP4_Track
    +---------------------------------------------------------------------*/
    public class Track
    {
        // types
        public enum Type {
            TYPE_UNKNOWN   = 0,
            TYPE_AUDIO     = 1,
            TYPE_VIDEO     = 2,
            TYPE_SYSTEM    = 3,
            TYPE_HINT      = 4,
            TYPE_TEXT      = 5,
            TYPE_JPEG      = 6,
            TYPE_RTP       = 7,
            TYPE_SUBTITLES = 8
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::AP4_Track
        +---------------------------------------------------------------------*/
        public Track(Type         type,
                     SampleTable  sample_table,     // ownership is transfered to the AP4_Track object
                     uint         track_id,
                     uint         movie_time_scale, // 0 = use default
                     ulong        track_duration,   // in the movie timescale
                     uint         media_time_scale,
                     ulong        media_duration,   // in the media timescale
                     string       language,
                     uint         width,            // in 16.16 fixed point
                     uint         height,           // in 16.16 fixed point
                     ulong        creation_time = 0,
                     ulong        modification_time = 0)
        {
            _trakAtomIsOwned = true;
            _type = type;
            _sampleTable = sample_table;
            _sampleTableIsOwned = true;
            _movieTimeScale = movie_time_scale != 0 ?
                              movie_time_scale :
                              Ap4.TRACK_DEFAULT_MOVIE_TIMESCALE;

            // compute the default volume value
            uint volume = 0;
            if (type == Type.TYPE_AUDIO) volume = 0x100;

            // compute the handler type and name
            uint    hdlr_type;
            string  hdlr_name;
            switch (type) {
                case Type.TYPE_AUDIO:
                    hdlr_type = Ap4.HANDLER_TYPE_SOUN;
                    hdlr_name = "Bento4 Sound Handler";
                    break;

                case Type.TYPE_VIDEO:
                    hdlr_type = Ap4.HANDLER_TYPE_VIDE;
                    hdlr_name = "Bento4 Video Handler";
                    break;

                case Type.TYPE_HINT:
                    hdlr_type = Ap4.HANDLER_TYPE_HINT;
                    hdlr_name = "Bento4 Hint Handler";
                    break;

                case Type.TYPE_TEXT:
                    hdlr_type = Ap4.HANDLER_TYPE_TEXT;
                    hdlr_name = "Bento4 Text Handler";
                    break;

                case Type.TYPE_SUBTITLES:
                    hdlr_type = Ap4.HANDLER_TYPE_SUBT;
                    hdlr_name = "Bento4 Subtitle Handler";
                    break;

                default:
                    hdlr_type = 0;
                    hdlr_name = null;
                    break;
            }

            // create a trak atom
            _trakAtom = new TrakAtom(sample_table,
                                     hdlr_type,
                                     hdlr_name,
                                     track_id,
                                     creation_time,
                                     modification_time,
                                     track_duration,
                                     media_time_scale,
                                     media_duration,
                                     (ushort)volume,
                                     language,
                                     width,
                                     height);
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::AP4_Track
        +---------------------------------------------------------------------*/
        public Track(SampleTable  sample_table,     // ownership is transferred to the AP4_Track object
                     uint         track_id,
                     uint         movie_time_scale, // 0 = use default
                     ulong        track_duration,   // in the movie timescale
                     uint         media_time_scale,
                     ulong        media_duration,   // in the media timescale
                     Track        track_prototype)  // prototype for the type and other track fields
        {
            _trakAtomIsOwned = true;
            _type = track_prototype._type;
            _sampleTable = sample_table;
            _sampleTableIsOwned = true;
            _movieTimeScale = movie_time_scale != 0 ?
                              movie_time_scale :
                              Ap4.TRACK_DEFAULT_MOVIE_TIMESCALE;

            // compute the handler type and name
            uint    hdlr_type;
            string  hdlr_name;
            switch (track_prototype.GetType()) {
                case Type.TYPE_AUDIO:
                    hdlr_type = Ap4.HANDLER_TYPE_SOUN;
                    hdlr_name = "Bento4 Sound Handler";
                    break;

                case Type.TYPE_VIDEO:
                    hdlr_type = Ap4.HANDLER_TYPE_VIDE;
                    hdlr_name = "Bento4 Video Handler";
                    break;

                case Type.TYPE_HINT:
                    hdlr_type = Ap4.HANDLER_TYPE_HINT;
                    hdlr_name = "Bento4 Hint Handler";
                    break;

                case Type.TYPE_TEXT:
                    hdlr_type = Ap4.HANDLER_TYPE_TEXT;
                    hdlr_name = "Bento4 Text Handler";
                    break;

                case Type.TYPE_SUBTITLES:
                    hdlr_type = Ap4.HANDLER_TYPE_SUBT;
                    hdlr_name = "Bento4 Subtitle Handler";
                    break;

                default:
                    hdlr_type = track_prototype.GetHandlerType();
                    hdlr_name = track_prototype.GetTrackLanguage(); // (sic) as upstream
                    break;
            }

            // create a trak atom
            TkhdAtom  tkhd = null;
            if (track_prototype.GetTrakAtom() != null) {
                tkhd = track_prototype.GetTrakAtom().GetTkhdAtom();
            }

            _trakAtom = new TrakAtom(sample_table,
                                     hdlr_type,
                                     hdlr_name,
                                     track_id,
                                     tkhd != null ? tkhd.GetCreationTime() : 0,
                                     tkhd != null ? tkhd.GetModificationTime() : 0,
                                     track_duration,
                                     media_time_scale,
                                     media_duration,
                                     tkhd != null ? tkhd.GetVolume() : (ushort)(track_prototype.GetType() == Type.TYPE_AUDIO ? 0x100 : 0),
                                     track_prototype.GetTrackLanguage(),
                                     track_prototype.GetWidth(),
                                     track_prototype.GetHeight(),
                                     tkhd != null ? tkhd.GetLayer() : (ushort)0,
                                     tkhd != null ? tkhd.GetAlternateGroup() : (ushort)0,
                                     tkhd != null ? tkhd.GetMatrix() : null);
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::AP4_Track
        +---------------------------------------------------------------------*/
        public Track(TrakAtom   atom,
                     ByteStream sample_stream,
                     uint       movie_time_scale)
        {
            _trakAtom = atom;
            _trakAtomIsOwned = false;
            _type = Type.TYPE_UNKNOWN;
            _sampleTable = null;
            _sampleTableIsOwned = true;
            _movieTimeScale = movie_time_scale;

            // find the handler type
            Atom sub = atom.FindChild("mdia/hdlr");
            if (sub != null) {
                HdlrAtom hdlr = sub as HdlrAtom;
                if (hdlr != null) {
                    uint type = hdlr.GetHandlerType();
                    if (type == Ap4.HANDLER_TYPE_SOUN) {
                        _type = Type.TYPE_AUDIO;
                    } else if (type == Ap4.HANDLER_TYPE_VIDE) {
                        _type = Type.TYPE_VIDEO;
                    } else if (type == Ap4.HANDLER_TYPE_HINT) {
                        _type = Type.TYPE_HINT;
                    } else if (type == Ap4.HANDLER_TYPE_ODSM ||
                               type == Ap4.HANDLER_TYPE_SDSM) {
                        _type = Type.TYPE_SYSTEM;
                    } else if (type == Ap4.HANDLER_TYPE_TEXT ||
                               type == Ap4.HANDLER_TYPE_TX3G) {
                        _type = Type.TYPE_TEXT;
                    } else if (type == Ap4.HANDLER_TYPE_JPEG) {
                        _type = Type.TYPE_JPEG;
                    } else if (type == Ap4.HANDLER_TYPE_SUBT ||
                               type == Ap4.HANDLER_TYPE_SBTL) {
                        _type = Type.TYPE_SUBTITLES;
                    }
                }
            }

            // create a facade for the stbl atom
            ContainerAtom stbl = atom.FindChild("mdia/minf/stbl") as ContainerAtom;
            if (stbl != null) {
                _sampleTable = new AtomSampleTable(stbl, sample_stream);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::Clone
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Clone a track. This is useful if you want to create a track from
        /// a non-synthetic track (parsed from a file for example) and
        /// write it out
        /// </summary>
        // PORT NOTE: `AP4_Result* result = NULL` -> two overloads.
        public Track Clone()
        {
            Ap4Result unused1;
            return Clone(out unused1);
        }

        public Track Clone(out Ap4Result result)
        {
            SyntheticSampleTable sample_table = new SyntheticSampleTable();

            // default return value
            result = Ap4Result.SUCCESS;

            // add clones of the sample descriptions to the new sample table
            for (uint i=0; ;i++) {
                SampleDescription sample_description = GetSampleDescription(i);
                if (sample_description == null) break;
                sample_table.AddSampleDescription(sample_description.Clone());
            }

            Sample sample = new Sample();
            uint   index = 0;
            while (GetSample(index, sample).Succeeded()) {
                ByteStream data_stream;
                data_stream = sample.GetDataStream();
                sample_table.AddSample(data_stream,
                                       sample.GetOffset(),
                                       sample.GetSize(),
                                       sample.GetDuration(),
                                       sample.GetDescriptionIndex(),
                                       sample.GetDts(),
                                       sample.GetCtsDelta(),
                                       sample.IsSync());
                index++;
            }

            // create the cloned track
            Track clone = new Track(sample_table,
                                    GetId(),
                                    GetMovieTimeScale(),
                                    GetDuration(),
                                    GetMediaTimeScale(),
                                    GetMediaDuration(),
                                    this);

            return clone;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::Attach
        +---------------------------------------------------------------------*/
        public Ap4Result Attach(MoovAtom moov)
        {
            if (!_trakAtomIsOwned) return Ap4Result.ERROR_INTERNAL;
            moov.AddChild(_trakAtom);
            _trakAtomIsOwned = false;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetFlags
        +---------------------------------------------------------------------*/
        public uint GetFlags()
        {
            if (_trakAtom != null) {
                TkhdAtom tkhd = _trakAtom.FindChild("tkhd") as TkhdAtom;
                if (tkhd != null) {
                    return tkhd.GetFlags();
                }
            }
            return 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::SetFlags
        +---------------------------------------------------------------------*/
        public Ap4Result SetFlags(uint flags)
        {
            if (_trakAtom != null) {
                TkhdAtom tkhd = _trakAtom.FindChild("tkhd") as TkhdAtom;
                if (tkhd != null) {
                    tkhd.SetFlags(flags);
                    return Ap4Result.SUCCESS;
                }
            }
            return Ap4Result.ERROR_INVALID_STATE;
        }

        // PORT NOTE: hides object.GetType(), like Atom.GetType(); use `is`/`as`/typeof for .NET type tests.
        public new Track.Type GetType() { return _type; }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetHandlerType
        +---------------------------------------------------------------------*/
        public uint GetHandlerType()
        {
            if (_trakAtom != null) {
                HdlrAtom hdlr = _trakAtom.FindChild("mdia/hdlr") as HdlrAtom;
                if (hdlr != null) {
                    return hdlr.GetHandlerType();
                }
            }
            return 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetId
        +---------------------------------------------------------------------*/
        public uint GetId()
        {
            return _trakAtom.GetId();
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetWidth
        +---------------------------------------------------------------------*/
        public uint GetWidth() // in 16.16 fixed point
        {
            return _trakAtom.GetWidth();
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetHeight
        +---------------------------------------------------------------------*/
        public uint GetHeight() // in 16.16 fixed point
        {
            return _trakAtom.GetHeight();
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::SetId
        +---------------------------------------------------------------------*/
        public Ap4Result SetId(uint id)
        {
            _trakAtom.SetId(id);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetDuration
        +---------------------------------------------------------------------*/
        public ulong GetDuration() // in the timescale of the movie
        {
            return _trakAtom.GetDuration();
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetDurationMs
        +---------------------------------------------------------------------*/
        public uint GetDurationMs() // in milliseconds
        {
            ulong duration = _trakAtom.GetDuration();
            return Ap4Utils.DurationMsFromUnits(duration, _movieTimeScale);
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetSampleCount
        +---------------------------------------------------------------------*/
        public uint GetSampleCount()
        {
            // delegate to the sample table
            return _sampleTable != null ? _sampleTable.GetSampleCount() : 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetSample
        +---------------------------------------------------------------------*/
        public Ap4Result GetSample(uint index, Sample sample)
        {
            // delegate to the sample table
            return _sampleTable != null ? _sampleTable.GetSample(index, sample) : Ap4Result.FAILURE;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetSampleDescription
        +---------------------------------------------------------------------*/
        public SampleDescription GetSampleDescription(uint index)
        {
            // delegate to the sample table
            return _sampleTable != null ? _sampleTable.GetSampleDescription(index) : null;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetSampleDescriptionCount
        +---------------------------------------------------------------------*/
        public uint GetSampleDescriptionCount()
        {
            // delegate to the sample table
            return _sampleTable != null ? _sampleTable.GetSampleDescriptionCount() : 0;
        }

        public SampleTable GetSampleTable() { return _sampleTable; }

        /*----------------------------------------------------------------------
        |   AP4_Track::ReadSample
        +---------------------------------------------------------------------*/
        public Ap4Result ReadSample(uint       index,
                                    Sample     sample,
                                    DataBuffer data)
        {
            Ap4Result result;

            // get the sample
            result = GetSample(index, sample);
            if (result.Failed()) return result;

            // read the data
            return sample.ReadData(data);
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetSampleIndexForTimeStampMs
        +---------------------------------------------------------------------*/
        public Ap4Result GetSampleIndexForTimeStampMs(uint ts_ms, out uint index)
        {
            // PORT NOTE: upstream leaves `index` untouched here; an `out` must be assigned.
            index = 0;
            if (_sampleTable == null) return Ap4Result.ERROR_INVALID_STATE;

            // convert the ts in the timescale of the track's media
            ulong ts = Ap4Utils.ConvertTime(ts_ms, 1000, GetMediaTimeScale());

            return _sampleTable.GetSampleIndexForTimeStamp(ts, out index);
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetNearestSyncSampleIndex
        +---------------------------------------------------------------------*/
        public uint GetNearestSyncSampleIndex(uint index, bool before = true)
        {
            if (_sampleTable == null) return index;
            return _sampleTable.GetNearestSyncSampleIndex(index, before);
        }

        public TrakAtom GetTrakAtom() { return _trakAtom; }
        public TrakAtom UseTrakAtom() { return _trakAtom; }

        /*----------------------------------------------------------------------
        |   AP4_Track::SetMovieTimeScale
        +---------------------------------------------------------------------*/
        public Ap4Result SetMovieTimeScale(uint time_scale)
        {
            // check that we can convert
            if (_movieTimeScale == 0) return Ap4Result.FAILURE;

            // convert from one time scale to the other
            _trakAtom.SetDuration(Ap4Utils.ConvertTime(_trakAtom.GetDuration(),
                                                        _movieTimeScale,
                                                        time_scale));

            // keep the new movie timescale
            _movieTimeScale = time_scale;

            return Ap4Result.SUCCESS;
        }

        public uint GetMovieTimeScale() { return _movieTimeScale; }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetMediaTimeScale
        +---------------------------------------------------------------------*/
        public uint GetMediaTimeScale()
        {
            return _trakAtom != null ? _trakAtom.GetMediaTimeScale() : 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetMediaDuration
        +---------------------------------------------------------------------*/
        public ulong GetMediaDuration() // in the timescale of the media
        {
            return _trakAtom != null ? _trakAtom.GetMediaDuration() : 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetTrackName
        +---------------------------------------------------------------------*/
        public string GetTrackName()
        {
            HdlrAtom hdlr;
            if ((hdlr = _trakAtom.FindChild("mdia/hdlr") as HdlrAtom) != null) {
                return hdlr.GetHandlerName();
            }
            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::GetTrackLanguage
        +---------------------------------------------------------------------*/
        public string GetTrackLanguage()
        {
            MdhdAtom mdhd;
            if ((mdhd = _trakAtom.FindChild("mdia/mdhd") as MdhdAtom) != null) {
                return mdhd.GetLanguage();
            }
            return null;
        }

        /*----------------------------------------------------------------------
        |   AP4_Track::SetTrackLanguage
        +---------------------------------------------------------------------*/
        public Ap4Result SetTrackLanguage(string language)
        {
            if (Ap4Utils.StringLength(language) != 3) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            MdhdAtom mdhd;
            if ((mdhd = _trakAtom.FindChild("mdia/mdhd") as MdhdAtom) != null) {
                return mdhd.SetLanguage(language);
            }
            return Ap4Result.ERROR_INVALID_STATE;
        }

        // members
        protected TrakAtom     _trakAtom;
        protected bool         _trakAtomIsOwned;
        protected Type         _type;
        protected SampleTable  _sampleTable;
        protected bool         _sampleTableIsOwned;
        protected uint         _movieTimeScale;
    }
}
