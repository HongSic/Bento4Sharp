// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4UuidAtom.h, Ap4UuidAtom.cpp
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
        public const uint UUID_ATOM_HEADER_SIZE      = ATOM_HEADER_SIZE+16;
        public const uint FULL_UUID_ATOM_HEADER_SIZE = FULL_ATOM_HEADER_SIZE+16;
    }

    /*----------------------------------------------------------------------
    |   AP4_UuidAtom
    +---------------------------------------------------------------------*/
    /// <summary>
    /// Base class for uuid atoms.
    /// </summary>
    // PORT NOTE: implements IUuidAtom so that AtomParent.GetChild(byte[] uuid, ...)
    // can find uuid atoms (see docs/FOUNDATION_API.md).
    public abstract class UuidAtom : Atom, IUuidAtom {
        // methods
        /*----------------------------------------------------------------------
        |   AP4_UuidAtom::AP4_UuidAtom
        +---------------------------------------------------------------------*/
        protected UuidAtom(ulong size, byte[] uuid) :
            base(AtomType.UUID, size)
        {
            Array.Copy(uuid, 0, _uuid, 0, 16);
        }

        /*----------------------------------------------------------------------
        |   AP4_UuidAtom::AP4_UuidAtom
        +---------------------------------------------------------------------*/
        protected UuidAtom(ulong size, byte[] uuid, byte version, uint flags) :
            base(AtomType.UUID, size, false, version, flags)
        {
            Array.Copy(uuid, 0, _uuid, 0, 16);
        }

        /*----------------------------------------------------------------------
        |   AP4_UuidAtom::AP4_UuidAtom
        +---------------------------------------------------------------------*/
        // PORT NOTE: as upstream, this constructor does not set the uuid (upstream
        // leaves m_Uuid uninitialized; here it is all zeros).
        protected UuidAtom(ulong size, bool is_full, ByteStream stream) :
            base(AtomType.UUID, size)
        {
            if (is_full) {
                _isFull = true;
                ReadFullHeader(stream, out _version, out _flags);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_UuidAtom::GetHeaderSize
        +---------------------------------------------------------------------*/
        public override uint GetHeaderSize()
        {
            return (_isFull ? Ap4.FULL_UUID_ATOM_HEADER_SIZE : Ap4.UUID_ATOM_HEADER_SIZE)+(_size32==1?8u:0u);
        }

        /*----------------------------------------------------------------------
        |   AP4_UuidAtom::WriteHeader
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteHeader(ByteStream stream)
        {
            Ap4Result result;

            // write the size
            result = stream.WriteUI32(_size32);
            if (result.Failed()) return result;

            // write the type
            result = stream.WriteUI32(_type);
            if (result.Failed()) return result;

            // handle 64-bit sizes
            if (_size32 == 1) {
                result = stream.WriteUI64(_size64);
                if (result.Failed()) return result;
            }

            // write the extended type
            result = stream.Write(_uuid, 16);
            if (result.Failed()) return result;

            // for full atoms, write version and flags
            if (_isFull) {
                result = stream.WriteUI08((byte)_version);
                if (result.Failed()) return result;
                result = stream.WriteUI24(_flags);
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_UuidAtom::InspectHeader
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectHeader(AtomInspector inspector)
        {
            char[] uuid = new char[36];
            int dst = 0;
            for (uint i=0; i<16; i++) {
                uuid[dst++] = Ap4Utils.NibbleHex((uint)(_uuid[i]>>4));
                uuid[dst++] = Ap4Utils.NibbleHex((uint)(_uuid[i]&0x0F));
                if (i == 5 || i == 7 || i == 9 || i == 11) uuid[dst++] = '-';
            }

            inspector.StartAtom(new string(uuid),
                                _version,
                                _flags,
                                GetHeaderSize(),
                                GetSize());

            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte[] GetUuid() { return _uuid; }

        // members
        protected byte[] _uuid = new byte[16];
    }

    /*----------------------------------------------------------------------
    |   AP4_UnknownUuidAtom
    +---------------------------------------------------------------------*/
    /// <summary>
    /// Unknown uuid atoms.
    /// </summary>
    public class UnknownUuidAtom : UuidAtom {
        // constructors
        /*----------------------------------------------------------------------
        |   AP4_UnknownUuidAtom::AP4_UnknownUuidAtom
        +---------------------------------------------------------------------*/
        public UnknownUuidAtom(ulong size, ByteStream stream) :
            base(size, false, stream)
        {
            // store the data
            uint header_size = GetHeaderSize();
            if (size > header_size) {
                _data.SetDataSize((uint)size-GetHeaderSize());
                stream.Read(_data.UseData(), _data.GetDataSize());
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownUuidAtom::AP4_UnknownUuidAtom
        +---------------------------------------------------------------------*/
        public UnknownUuidAtom(ulong size, byte[] uuid, ByteStream stream) :
            base(size, uuid)
        {
            // store the data
            uint header_size = GetHeaderSize();
            if (size > header_size) {
                _data.SetDataSize((uint)size-GetHeaderSize());
                stream.Read(_data.UseData(), _data.GetDataSize());
            }
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_UnknownUuidAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the data
            return stream.Write(_data.GetData(), _data.GetDataSize());
        }

        // members
        protected DataBuffer _data = new DataBuffer();
    }
}
