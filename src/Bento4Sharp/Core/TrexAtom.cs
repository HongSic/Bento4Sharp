// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TrexAtom.h, Ap4TrexAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    // PORT NOTE: upstream declares the macro
    //   #define AP4_TREX_SAMPLE_IS_DIFFERENCE_SAMPLE(_flags) TODO
    // which is an unusable placeholder; it is not ported.

    /*----------------------------------------------------------------------
    |   AP4_TrexAtom
    +---------------------------------------------------------------------*/
    public class TrexAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_TrexAtom::Create
        +---------------------------------------------------------------------*/
        public static TrexAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new TrexAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_TrexAtom::AP4_TrexAtom
        +---------------------------------------------------------------------*/
        public TrexAtom(uint track_id,
                        uint default_sample_description_index,
                        uint default_sample_duration,
                        uint default_sample_size,
                        uint default_sample_flags) :
            base(AtomType.TREX, Ap4.FULL_ATOM_HEADER_SIZE+20, 0, 0)
        {
            _trackId = track_id;
            _defaultSampleDescriptionIndex = default_sample_description_index;
            _defaultSampleDuration = default_sample_duration;
            _defaultSampleSize = default_sample_size;
            _defaultSampleFlags = default_sample_flags;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrexAtom::AP4_TrexAtom
        +---------------------------------------------------------------------*/
        private TrexAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.TREX, size, version, flags)
        {
            stream.ReadUI32(out _trackId);
            stream.ReadUI32(out _defaultSampleDescriptionIndex);
            stream.ReadUI32(out _defaultSampleDuration);
            stream.ReadUI32(out _defaultSampleSize);
            stream.ReadUI32(out _defaultSampleFlags);
        }

        /*----------------------------------------------------------------------
        |   AP4_TrexAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            stream.WriteUI32(_trackId);
            stream.WriteUI32(_defaultSampleDescriptionIndex);
            stream.WriteUI32(_defaultSampleDuration);
            stream.WriteUI32(_defaultSampleSize);
            stream.WriteUI32(_defaultSampleFlags);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrexAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("track id", _trackId);
            inspector.AddField("default sample description index", _defaultSampleDescriptionIndex);
            inspector.AddField("default sample duration", _defaultSampleDuration);
            inspector.AddField("default sample size", _defaultSampleSize);
            inspector.AddField("default sample flags", _defaultSampleFlags, AtomInspector.FormatHint.HINT_HEX);

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint GetTrackId()                       { return _trackId;                       }
        public uint GetDefaultSampleDescriptionIndex() { return _defaultSampleDescriptionIndex; }
        public uint GetDefaultSampleDuration()         { return _defaultSampleDuration;         }
        public uint GetDefaultSampleSize()             { return _defaultSampleSize;             }
        public uint GetDefaultSampleFlags()            { return _defaultSampleFlags;            }

        // members
        private uint _trackId;
        private uint _defaultSampleDescriptionIndex;
        private uint _defaultSampleDuration;
        private uint _defaultSampleSize;
        private uint _defaultSampleFlags;
    }
}
