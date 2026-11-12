// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TkhdAtom.h, Ap4TkhdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const int TKHD_FLAG_TRACK_ENABLED    = 1;
        public const int TKHD_FLAG_TRACK_IN_MOVIE   = 2;
        public const int TKHD_FLAG_TRACK_IN_PREVIEW = 4;
        public const int TKHD_FLAG_DEFAULTS         = 7;
    }

    /*----------------------------------------------------------------------
    |   AP4_TkhdAtom
    +---------------------------------------------------------------------*/
    public class TkhdAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_TkhdAtom::Create
        +---------------------------------------------------------------------*/
        public static TkhdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new TkhdAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_TkhdAtom::AP4_TkhdAtom
        +---------------------------------------------------------------------*/
        public TkhdAtom(ulong  creation_time,
                        ulong  modification_time,
                        uint   track_id,
                        ulong  duration,
                        ushort volume,
                        uint   width,
                        uint   height,
                        ushort layer = 0,
                        ushort alternate_group = 0,
                        int[]  matrix = null) :
            base(AtomType.TKHD, Ap4.FULL_ATOM_HEADER_SIZE+80, 0, 0)
        {
            _creationTime = creation_time;
            _modificationTime = modification_time;
            _trackId = track_id;
            _reserved1 = 0;
            _duration = duration;
            _layer = layer;
            _alternateGroup = alternate_group;
            _volume = volume;
            _reserved3 = 0;
            _width = width;
            _height = height;

            _flags = Ap4.TKHD_FLAG_DEFAULTS;

            if (matrix != null) {
                for (uint i=0; i<9; i++) {
                    _matrix[i] = matrix[i];
                }
            } else {
                // default matrix
                _matrix[0] = 0x00010000;
                _matrix[1] = 0;
                _matrix[2] = 0;
                _matrix[3] = 0;
                _matrix[4] = 0x00010000;
                _matrix[5] = 0;
                _matrix[6] = 0;
                _matrix[7] = 0;
                _matrix[8] = 0x40000000;
            }

            // m_Reserved2[0] = 0; m_Reserved2[1] = 0;
            Array.Clear(_reserved2, 0, _reserved2.Length);

            if (duration          > 0xFFFFFFFFUL ||
                creation_time     > 0xFFFFFFFFUL ||
                modification_time > 0xFFFFFFFFUL) {
                _version = 1;
                _size32 += 12;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TkhdAtom::AP4_TkhdAtom
        +---------------------------------------------------------------------*/
        private TkhdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.TKHD, size, version, flags)
        {
            if (_version == 0) {
                // we only deal with version 0 for now
                uint creation_time;
                stream.ReadUI32(out creation_time);
                _creationTime = creation_time;
                uint modification_time;
                stream.ReadUI32(out modification_time);
                _modificationTime = modification_time;
                stream.ReadUI32(out _trackId);
                stream.ReadUI32(out _reserved1);
                uint duration;
                stream.ReadUI32(out duration);
                _duration = duration;
            } else {
                stream.ReadUI64(out _creationTime);
                stream.ReadUI64(out _modificationTime);
                stream.ReadUI32(out _trackId);
                stream.ReadUI32(out _reserved1);
                stream.ReadUI64(out _duration);
            }

            stream.Read(_reserved2, 8);
            stream.ReadUI16(out _layer);
            stream.ReadUI16(out _alternateGroup);
            stream.ReadUI16(out _volume);
            stream.ReadUI16(out _reserved3);
            for (int i=0; i<9; i++) {
                uint coefficient = 0;
                stream.ReadUI32(out coefficient);
                _matrix[i] = (int)coefficient;
            }
            stream.ReadUI32(out _width);
            stream.ReadUI32(out _height);
        }

        /*----------------------------------------------------------------------
        |   AP4_TkhdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // creation/modification time, track id, reserved1 & duration
            if (_version == 0) {
                result = stream.WriteUI32((uint)_creationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI32((uint)_modificationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_trackId);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_reserved1);
                if (result.Failed()) return result;
                result = stream.WriteUI32((uint)_duration);
                if (result.Failed()) return result;
            } else {
                result = stream.WriteUI64(_creationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI64(_modificationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_trackId);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_reserved1);
                if (result.Failed()) return result;
                result = stream.WriteUI64(_duration);
                if (result.Failed()) return result;
            }

            // reserved2
            result = stream.Write(_reserved2, (uint)_reserved2.Length);
            if (result.Failed()) return result;

            // layer, alternate group & volume
            result = stream.WriteUI16(_layer);
            if (result.Failed()) return result;
            result = stream.WriteUI16(_alternateGroup);
            if (result.Failed()) return result;
            result = stream.WriteUI16(_volume);
            if (result.Failed()) return result;

            // reserved3
            result = stream.WriteUI16(_reserved3);
            if (result.Failed()) return result;

            // matrix
            for (int i=0; i<9; i++) {
                result = stream.WriteUI32((uint)_matrix[i]);
                if (result.Failed()) return result;
            }

            // width & height
            result = stream.WriteUI32(_width);
            if (result.Failed()) return result;
            result = stream.WriteUI32(_height);
            if (result.Failed()) return result;

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_TkhdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("enabled", (((_flags & Ap4.TKHD_FLAG_TRACK_ENABLED) != 0) ? 1 : 0), AtomInspector.FormatHint.HINT_BOOLEAN);
            inspector.AddField("id", _trackId);
            inspector.AddField("duration", _duration);
            if (inspector.GetVerbosity() > 0) {
                inspector.AddField("volume", _volume);
                inspector.AddField("layer", _layer);
                inspector.AddField("alternate_group", _alternateGroup);
                inspector.AddFieldF("matrix_0", (float)_matrix[0]/65536.0f);
                inspector.AddFieldF("matrix_1", (float)_matrix[1]/65536.0f);
                inspector.AddFieldF("matrix_2", (float)_matrix[2]/65536.0f);
                inspector.AddFieldF("matrix_3", (float)_matrix[3]/65536.0f);
                inspector.AddFieldF("matrix_4", (float)_matrix[4]/65536.0f);
                inspector.AddFieldF("matrix_5", (float)_matrix[5]/65536.0f);
                inspector.AddFieldF("matrix_6", (float)_matrix[6]/65536.0f);
                inspector.AddFieldF("matrix_7", (float)_matrix[7]/65536.0f);
                inspector.AddFieldF("matrix_8", (float)_matrix[8]/65536.0f);
            }
            inspector.AddFieldF("width",    (float)_width/65536.0f);
            inspector.AddFieldF("height",   (float)_height/65536.0f);

            return Ap4Result.SUCCESS;
        }

        public ulong  GetCreationTime()                         { return _creationTime;                  }
        public void   SetCreationTime(ulong creation_time)      { _creationTime = creation_time;         }
        public ulong  GetModificationTime()                     { return _modificationTime;              }
        public void   SetModificationTime(ulong modification_time) { _modificationTime = modification_time; }
        public uint   GetTrackId()                              { return _trackId;                       }
        public void   SetTrackId(uint track_id)                 { _trackId = track_id;                   }
        public ulong  GetDuration()                             { return _duration;                      }
        public void   SetDuration(ulong duration)               { _duration = duration;                  }
        public ushort GetLayer()                                { return _layer;                         }
        public void   SetLayer(ushort layer)                    { _layer = layer;                        }
        public ushort GetAlternateGroup()                       { return _alternateGroup;                }
        public void   SetAlternateGroup(ushort group)           { _alternateGroup = group;               }
        public ushort GetVolume()                               { return _volume;                        }
        public void   SetVolume(ushort volume)                  { _volume = volume;                      }
        public uint   GetWidth()                                { return _width;                         }
        public void   SetWidth(uint width)                      { _width = width;                        }
        public uint   GetHeight()                               { return _height;                        }
        public void   SetHeight(uint height)                    { _height = height;                      }
        public int[]  GetMatrix()                               { return _matrix;                        }
        public void   SetMatrix(int[] matrix)                   { Array.Copy(matrix, 0, _matrix, 0, 9);  }
        public void GetTranslation(out float x, out float y) {
            x = (float)_matrix[6] / 65536;
            y = (float)_matrix[7] / 65536;
        }

        // members
        private ulong  _creationTime;
        private ulong  _modificationTime;
        private uint   _trackId;
        private uint   _reserved1;
        private ulong  _duration;
        // PORT NOTE: upstream declares AP4_UI32 m_Reserved2[2] but only ever reads
        // and writes it as 8 raw bytes; kept as a byte array so that the bytes
        // round-trip regardless of the platform byte order.
        private byte[] _reserved2 = new byte[8];
        private ushort _layer;
        private ushort _alternateGroup;
        private ushort _volume;
        private ushort _reserved3;
        private int[]  _matrix = new int[9];
        private uint   _width;
        private uint   _height;
    }
}
