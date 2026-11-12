// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Co64Atom.h, Ap4Co64Atom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Co64Atom
    +---------------------------------------------------------------------*/
    public class Co64Atom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_Co64Atom::Create
        +---------------------------------------------------------------------*/
        public static Co64Atom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new Co64Atom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_Co64Atom::AP4_Co64Atom
        +---------------------------------------------------------------------*/
        public Co64Atom(ulong[] entries, uint entry_count) :
            base(AtomType.CO64,
                 Ap4.FULL_ATOM_HEADER_SIZE+4+entry_count*8,
                 0, 0)
        {
            _entries = new ulong[entry_count];
            _entryCount = entry_count;
            Array.Copy(entries, 0, _entries, 0, (int)_entryCount);
        }

        /*----------------------------------------------------------------------
        |   AP4_Co64Atom::AP4_Co64Atom
        +---------------------------------------------------------------------*/
        private Co64Atom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.CO64, size, version, flags)
        {
            stream.ReadUI32(out _entryCount);
            // PORT NOTE: as upstream, there is no check that size >= header+4: for
            // a 12..15 byte atom the unsigned subtraction wraps and the count is
            // not clamped (upstream then attempts a huge allocation as well).
            if (_entryCount > (size-Ap4.FULL_ATOM_HEADER_SIZE-4)/8) {
                _entryCount = (size-Ap4.FULL_ATOM_HEADER_SIZE-4)/8;
            }
            _entries = new ulong[_entryCount];
            for (uint i=0; i<_entryCount; i++) {
                stream.ReadUI64(out _entries[i]);
            }
        }

        public uint    GetChunkCount()   { return _entryCount; }
        public ulong[] GetChunkOffsets() { return _entries;    }

        /*----------------------------------------------------------------------
        |   AP4_Co64Atom::GetChunkOffset
        +---------------------------------------------------------------------*/
        public Ap4Result GetChunkOffset(uint chunk, out ulong chunk_offset)
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
        |   AP4_Co64Atom::SetChunkOffset
        +---------------------------------------------------------------------*/
        public Ap4Result SetChunkOffset(uint chunk, ulong chunk_offset)
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
        |   AP4_Co64Atom::AdjustChunkOffsets
        +---------------------------------------------------------------------*/
        public Ap4Result AdjustChunkOffsets(long delta)
        {
            for (uint i=0; i<_entryCount; i++) {
                _entries[i] = unchecked(_entries[i] + (ulong)delta);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Co64Atom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // entry count
            result = stream.WriteUI32(_entryCount);
            if (result.Failed()) return result;

            // entries
            for (uint i=0; i<_entryCount; i++) {
                result = stream.WriteUI64(_entries[i]);
                if (result.Failed()) return result;
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_Co64Atom::InspectFields
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
        private ulong[] _entries;
        private uint    _entryCount;
    }
}
