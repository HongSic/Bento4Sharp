// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4MvhdAtom.h, Ap4MvhdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_MvhdAtom
    +---------------------------------------------------------------------*/
    public class MvhdAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_MvhdAtom::Create
        +---------------------------------------------------------------------*/
        public static MvhdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new MvhdAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MvhdAtom::AP4_MvhdAtom
        +---------------------------------------------------------------------*/
        public MvhdAtom(ulong  creation_time,
                        ulong  modification_time,
                        uint   time_scale,
                        ulong  duration,
                        uint   rate,
                        ushort volume) :
            base(AtomType.MVHD, Ap4.FULL_ATOM_HEADER_SIZE + 96, 0, 0)
        {
            _creationTime = creation_time;
            _modificationTime = modification_time;
            _timeScale = time_scale;
            _duration = duration;
            _rate = rate;
            _volume = volume;
            _nextTrackId = 0xFFFFFFFF;

            _matrix[0] = 0x00010000;
            _matrix[1] = 0;
            _matrix[2] = 0;
            _matrix[3] = 0;
            _matrix[4] = 0x00010000;
            _matrix[5] = 0;
            _matrix[6] = 0;
            _matrix[7] = 0;
            _matrix[8] = 0x40000000;

            // PORT NOTE: m_Reserved1, m_Reserved2 and m_Predefined are zero-filled
            // by the array allocations (AP4_SetMemory upstream).

            if (duration          > 0xFFFFFFFFUL ||
                creation_time     > 0xFFFFFFFFUL ||
                modification_time > 0xFFFFFFFFUL) {
                _version = 1;
                _size32 += 12;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MvhdAtom::AP4_MvhdAtom
        +---------------------------------------------------------------------*/
        private MvhdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.MVHD, size, version, flags)
        {
            if (_version == 0) {
                uint creation_time;
                stream.ReadUI32(out creation_time);
                _creationTime = creation_time;
                uint modification_time;
                stream.ReadUI32(out modification_time);
                _modificationTime = modification_time;
                stream.ReadUI32(out _timeScale);
                uint duration;
                stream.ReadUI32(out duration);
                _duration = duration;
            } else {
                stream.ReadUI64(out _creationTime);
                stream.ReadUI64(out _modificationTime);
                stream.ReadUI32(out _timeScale);
                stream.ReadUI64(out _duration);
            }

            stream.ReadUI32(out _rate);
            stream.ReadUI16(out _volume);
            stream.Read(_reserved1, (uint)_reserved1.Length);
            stream.Read(_reserved2, (uint)_reserved2.Length);

            for (int i=0; i<9; i++) {
                stream.ReadUI32(out _matrix[i]);
            }

            stream.Read(_predefined, (uint)_predefined.Length);
            stream.ReadUI32(out _nextTrackId);
        }

        /*----------------------------------------------------------------------
        |   AP4_MvhdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            if (_version == 0) {
                result = stream.WriteUI32((uint)_creationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI32((uint)_modificationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_timeScale);
                if (result.Failed()) return result;
                result = stream.WriteUI32((uint)_duration);
                if (result.Failed()) return result;
            } else {
                result = stream.WriteUI64(_creationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI64(_modificationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_timeScale);
                if (result.Failed()) return result;
                result = stream.WriteUI64(_duration);
                if (result.Failed()) return result;
            }

            // rate & volume
            result = stream.WriteUI32(_rate);
            if (result.Failed()) return result;
            result = stream.WriteUI16(_volume);
            if (result.Failed()) return result;

            // reserved
            result = stream.Write(_reserved1, (uint)_reserved1.Length);
            if (result.Failed()) return result;
            result = stream.Write(_reserved2, (uint)_reserved2.Length);
            if (result.Failed()) return result;

            // matrix
            for (int i=0; i<9; i++) {
                result = stream.WriteUI32(_matrix[i]);
                if (result.Failed()) return result;
            }

            // pre-defined
            result = stream.Write(_predefined, (uint)_predefined.Length);
            if (result.Failed()) return result;

            // next track id
            return stream.WriteUI32(_nextTrackId);
        }

        public ulong GetDuration() { return _duration; }
        public void  SetDuration(ulong duration) { _duration = duration;}

        /*----------------------------------------------------------------------
        |   AP4_MvhdAtom::GetDurationMs
        +---------------------------------------------------------------------*/
        public uint GetDurationMs()
        {
            if (_timeScale != 0) {
                return (uint)Ap4Utils.ConvertTime(_duration, _timeScale, 1000);
            } else {
                return 0;
            }
        }

        public uint GetTimeScale() { return _timeScale; }
        public Ap4Result SetTimeScale(uint time_scale) {
            _timeScale = time_scale;
            return Ap4Result.SUCCESS;
        }
        public Ap4Result SetNextTrackId(uint next_track_id){
            _nextTrackId = next_track_id;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MvhdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("timescale", _timeScale);
            inspector.AddField("duration", _duration);
            inspector.AddField("duration(ms)", GetDurationMs());

            return Ap4Result.SUCCESS;
        }

        // members
        private ulong  _creationTime;
        private ulong  _modificationTime;
        private uint   _timeScale;
        private ulong  _duration;
        private uint   _rate;
        private ushort _volume;
        private byte[] _reserved1 = new byte[2];
        private byte[] _reserved2 = new byte[8];
        private uint[] _matrix = new uint[9];
        private byte[] _predefined = new byte[24];
        private uint   _nextTrackId;
    }
}
