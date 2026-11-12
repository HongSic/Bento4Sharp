// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TfhdAtom.h, Ap4TfhdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint TFHD_FLAG_BASE_DATA_OFFSET_PRESENT         = 0x00001;
        public const uint TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT = 0x00002;
        public const uint TFHD_FLAG_DEFAULT_SAMPLE_DURATION_PRESENT  = 0x00008;
        public const uint TFHD_FLAG_DEFAULT_SAMPLE_SIZE_PRESENT      = 0x00010;
        public const uint TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT     = 0x00020;
        public const uint TFHD_FLAG_DURATION_IS_EMPTY                = 0x10000;
        public const uint TFHD_FLAG_DEFAULT_BASE_IS_MOOF             = 0x20000;
    }

    /*----------------------------------------------------------------------
    |   AP4_TfhdAtom
    +---------------------------------------------------------------------*/
    public class TfhdAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_TfhdAtom::Create
        +---------------------------------------------------------------------*/
        public static TfhdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 0) return null;
            if (size < ComputeSize(flags)) return null;
            return new TfhdAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_TfhdAtom::ComputeSize
        +---------------------------------------------------------------------*/
        public static uint ComputeSize(uint flags)
        {
            uint size = Ap4.FULL_ATOM_HEADER_SIZE+4;
            if ((flags & Ap4.TFHD_FLAG_BASE_DATA_OFFSET_PRESENT) != 0)         size += 8;
            if ((flags & Ap4.TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT) != 0) size += 4;
            if ((flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_DURATION_PRESENT) != 0)  size += 4;
            if ((flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_SIZE_PRESENT) != 0)      size += 4;
            if ((flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT) != 0)     size += 4;
            return size;
        }

        /*----------------------------------------------------------------------
        |   AP4_TfhdAtom::AP4_TfhdAtom
        +---------------------------------------------------------------------*/
        public TfhdAtom(uint  flags,
                        uint  track_id,
                        ulong base_data_offset,
                        uint  sample_description_index,
                        uint  default_sample_duration,
                        uint  default_sample_size,
                        uint  default_sample_flags) :
            base(AtomType.TFHD, ComputeSize(flags), 0, flags)
        {
            _trackId = track_id;
            _baseDataOffset = base_data_offset;
            _sampleDescriptionIndex = sample_description_index;
            _defaultSampleDuration = default_sample_duration;
            _defaultSampleSize = default_sample_size;
            _defaultSampleFlags = default_sample_flags;
        }

        /*----------------------------------------------------------------------
        |   AP4_TfhdAtom::AP4_TfhdAtom
        +---------------------------------------------------------------------*/
        private TfhdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.TFHD, size, version, flags)
        {
            stream.ReadUI32(out _trackId);
            if ((flags & Ap4.TFHD_FLAG_BASE_DATA_OFFSET_PRESENT) != 0) {
                stream.ReadUI64(out _baseDataOffset);
            } else {
                _baseDataOffset = 0;
            }
            if ((flags & Ap4.TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT) != 0) {
                stream.ReadUI32(out _sampleDescriptionIndex);
            } else {
                _sampleDescriptionIndex = 1;
            }
            if ((flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_DURATION_PRESENT) != 0) {
                stream.ReadUI32(out _defaultSampleDuration);
            } else {
                _defaultSampleDuration = 0;
            }
            if ((flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_SIZE_PRESENT) != 0) {
                stream.ReadUI32(out _defaultSampleSize);
            } else {
                _defaultSampleSize = 0;
            }
            if ((flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT) != 0) {
                stream.ReadUI32(out _defaultSampleFlags);
            } else {
                _defaultSampleFlags = 0;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TfhdAtom::UpdateFlags
        +---------------------------------------------------------------------*/
        public void UpdateFlags(uint flags)
        {
            _flags  = flags;
            _size32 = ComputeSize(flags);
        }

        /*----------------------------------------------------------------------
        |   AP4_TfhdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.WriteUI32(_trackId);
            if (result.Failed()) return result;
            if ((_flags & Ap4.TFHD_FLAG_BASE_DATA_OFFSET_PRESENT) != 0) {
                result = stream.WriteUI64(_baseDataOffset);
                if (result.Failed()) return result;
            }
            if ((_flags & Ap4.TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT) != 0) {
                result = stream.WriteUI32(_sampleDescriptionIndex);
                if (result.Failed()) return result;
            }
            // PORT NOTE: as upstream, the results of the next three writes are
            // not assigned to `result`.
            if ((_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_DURATION_PRESENT) != 0) {
                stream.WriteUI32(_defaultSampleDuration);
                if (result.Failed()) return result;
            }
            if ((_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_SIZE_PRESENT) != 0) {
                stream.WriteUI32(_defaultSampleSize);
                if (result.Failed()) return result;
            }
            if ((_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT) != 0) {
                stream.WriteUI32(_defaultSampleFlags);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TfhdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("track ID", _trackId);
            if ((_flags & Ap4.TFHD_FLAG_BASE_DATA_OFFSET_PRESENT) != 0) {
                inspector.AddField("base data offset", _baseDataOffset);
            }
            if ((_flags & Ap4.TFHD_FLAG_SAMPLE_DESCRIPTION_INDEX_PRESENT) != 0) {
                inspector.AddField("sample description index", _sampleDescriptionIndex);
            }
            if ((_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_DURATION_PRESENT) != 0) {
                inspector.AddField("default sample duration", _defaultSampleDuration);
            }
            if ((_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_SIZE_PRESENT) != 0) {
                inspector.AddField("default sample size", _defaultSampleSize);
            }
            if ((_flags & Ap4.TFHD_FLAG_DEFAULT_SAMPLE_FLAGS_PRESENT) != 0) {
                inspector.AddField("default sample flags", _defaultSampleFlags, AtomInspector.FormatHint.HINT_HEX);
            }

            return Ap4Result.SUCCESS;
        }

        public uint  GetTrackId()                            { return _trackId;                   }
        public void  SetTrackId(uint track_id)               { _trackId = track_id;               }
        public ulong GetBaseDataOffset()                     { return _baseDataOffset;            }
        public void  SetBaseDataOffset(ulong offset)         { _baseDataOffset = offset;          }
        public uint  GetSampleDescriptionIndex()             { return _sampleDescriptionIndex;    }
        public void  SetSampleDescriptionIndex(uint indx)    { _sampleDescriptionIndex = indx;    }
        public uint  GetDefaultSampleDuration()              { return _defaultSampleDuration;     }
        public void  SetDefaultSampleDuration(uint duration) { _defaultSampleDuration = duration; }
        public uint  GetDefaultSampleSize()                  { return _defaultSampleSize;         }
        public void  SetDefaultSampleSize(uint size)         { _defaultSampleSize = size;         }
        public uint  GetDefaultSampleFlags()                 { return _defaultSampleFlags;        }
        public void  SetDefaultSampleFlags(uint flags)       { _defaultSampleFlags = flags;       }

        // members
        private uint  _trackId;
        private ulong _baseDataOffset;
        private uint  _sampleDescriptionIndex;
        private uint  _defaultSampleDuration;
        private uint  _defaultSampleSize;
        private uint  _defaultSampleFlags;
    }
}
