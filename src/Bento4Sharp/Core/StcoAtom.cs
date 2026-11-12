// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4StcoAtom.h, Ap4StcoAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_StcoAtom
    +---------------------------------------------------------------------*/
    public class StcoAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_StcoAtom::Create
        +---------------------------------------------------------------------*/
        public static StcoAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new StcoAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_StcoAtom::AP4_StcoAtom
        +---------------------------------------------------------------------*/
        public StcoAtom(uint[] entries, uint entry_count) :
            base(AtomType.STCO,
                 Ap4.FULL_ATOM_HEADER_SIZE+4+entry_count*4,
                 0, 0)
        {
            _entries = new uint[entry_count];
            _entryCount = entry_count;
            Array.Copy(entries, 0, _entries, 0, (int)_entryCount);
        }

        /*----------------------------------------------------------------------
        |   AP4_StcoAtom::AP4_StcoAtom
        +---------------------------------------------------------------------*/
        private StcoAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.STCO, size, version, flags)
        {
            // PORT NOTE: upstream leaves m_Entries NULL on the early return; the
            // port uses an empty array.
#if !NET20
            _entries = Array.Empty<uint>();
#else
            _entries = new uint[0];
#endif
            _entryCount = 0;

            if (size < Ap4.FULL_ATOM_HEADER_SIZE + 4) {
                return;
            }
            stream.ReadUI32(out _entryCount);
            if (_entryCount > (size-Ap4.FULL_ATOM_HEADER_SIZE-4)/4) {
                _entryCount = (size-Ap4.FULL_ATOM_HEADER_SIZE-4)/4;
            }
            // PORT NOTE: when the read below fails, upstream keeps the entry count
            // with uninitialized entries; here the entries are zero.
            _entries = new uint[_entryCount];
            byte[] buffer = new byte[_entryCount*4];
            Ap4Result result = stream.Read(buffer, _entryCount*4);
            if (result.Failed()) {
                return;
            }
            for (uint i=0; i<_entryCount; i++) {
                _entries[i] = Ap4Utils.BytesToUInt32BE(buffer, (int)(i*4));
            }
        }

        public uint   GetChunkCount()   { return _entryCount; }
        public uint[] GetChunkOffsets() { return _entries;    }

        /*----------------------------------------------------------------------
        |   AP4_StcoAtom::GetChunkOffset
        +---------------------------------------------------------------------*/
        public Ap4Result GetChunkOffset(uint chunk, out uint chunk_offset)
        {
            // check the bounds
            if (chunk > _entryCount || chunk == 0) {
                chunk_offset = 0; // PORT NOTE: upstream leaves the output untouched
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }

            // get the chunk offset
            chunk_offset = _entries[chunk - 1]; // m_Entries is zero index based

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StcoAtom::SetChunkOffset
        +---------------------------------------------------------------------*/
        public Ap4Result SetChunkOffset(uint chunk, uint chunk_offset)
        {
            // check the bounds
            if (chunk > _entryCount || chunk == 0) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            }

            // get the chunk offset
            _entries[chunk - 1] = chunk_offset; // m_Entries is zero index based

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StcoAtom::AdjustChunkOffsets
        +---------------------------------------------------------------------*/
        public Ap4Result AdjustChunkOffsets(int delta)
        {
            for (uint i=0; i<_entryCount; i++) {
                _entries[i] = unchecked(_entries[i] + (uint)delta);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StcoAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // entry count
            result = stream.WriteUI32(_entryCount);
            if (result.Failed()) return result;

            // entries
            for (uint i=0; i<_entryCount; i++) {
                result = stream.WriteUI32(_entries[i]);
                if (result.Failed()) return result;
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_StcoAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("entry_count", _entryCount);
            if (inspector.GetVerbosity() >= 1) {
                inspector.StartArray("entries", _entryCount);
                for (uint i=0; i<_entryCount; i++) {
                    inspector.AddField(null, _entries[i]);
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private uint[] _entries;
        private uint   _entryCount;
    }
}
