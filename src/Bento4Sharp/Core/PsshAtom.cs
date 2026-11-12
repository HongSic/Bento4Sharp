// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4PsshAtom.h, Ap4PsshAtom.cpp
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_PsshAtom
    +---------------------------------------------------------------------*/
    public class PsshAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   constants
        +---------------------------------------------------------------------*/
        private const uint AP4_PSSH_MAX_DATA_SIZE = 16*1024*1024; // 16MB, for sanity

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::Create
        +---------------------------------------------------------------------*/
        public static PsshAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new PsshAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::AP4_PsshAtom
        +---------------------------------------------------------------------*/
        public PsshAtom(byte[] system_id,
                        byte[]  kids = null,
                        uint    kid_count = 0) :
            base(AtomType.PSSH, Ap4.FULL_ATOM_HEADER_SIZE+16+4+((kids != null && kid_count != 0)?4+16*kid_count:0), (byte)((kids != null && kid_count != 0)?1:0), 0)
        {
            _kidCount = kid_count;
            Array.Copy(system_id, 0, _systemId, 0, 16);
            if (kids != null && kid_count != 0) {
                _kids.SetData(kids, kid_count*16);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::AP4_PsshAtom
        +---------------------------------------------------------------------*/
        private PsshAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.PSSH, size, version, flags)
        {
            _kidCount = 0;

            // PORT NOTE: upstream leaves m_SystemId uninitialized when it returns
            // early here; in the port it is all zeros.
            if (size < Ap4.FULL_ATOM_HEADER_SIZE + 20) {
                return;
            }
            stream.Read(_systemId, 16);
            if (_version > 0) {
                stream.ReadUI32(out _kidCount);
                if (_kidCount > (size - (Ap4.FULL_ATOM_HEADER_SIZE + 20))/16) {
                    _kidCount = 0;
                    return;
                }
                _kids.SetDataSize(_kidCount*16);
                stream.Read(_kids.UseData(), _kidCount*16);
            }
            uint data_size = 0;
            stream.ReadUI32(out data_size);
            if (data_size > AP4_PSSH_MAX_DATA_SIZE) return;
            _data.SetDataSize(data_size);
            stream.Read(_data.UseData(), data_size);

            uint computed_size = GetComputedSize();
            if (size > computed_size) {
                uint padding_size = size-computed_size;
                // PORT NOTE: padding_size can exceed the largest .NET array for a
                // malformed atom (upstream would fail to allocate); the read is
                // skipped when the buffer could not be sized.
                if (_padding.SetDataSize(padding_size).Succeeded()) {
                    stream.Read(_padding.UseData(), padding_size);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::GetComputedSize
        +---------------------------------------------------------------------*/
        private uint GetComputedSize() {
            return Ap4.FULL_ATOM_HEADER_SIZE + 16 + (_version==0?0:4+_kids.GetDataSize()) + 4+_data.GetDataSize() + _padding.GetDataSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::RecomputeSize
        +---------------------------------------------------------------------*/
        private void RecomputeSize() {
            SetSize32(GetComputedSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::GetKid
        +---------------------------------------------------------------------*/
        // PORT NOTE: upstream returns a pointer into the KID buffer; the port
        // returns a 16-byte copy (null when the index is out of range).
        public byte[] GetKid(uint index)
        {
            if (index >= _kidCount) {
                return null;
            }
            byte[] kid = new byte[16];
            Array.Copy(_kids.GetData(), (int)(index*16), kid, 0, 16);
            return kid;
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::SetData
        +---------------------------------------------------------------------*/
        public Ap4Result SetData(Atom atom)
        {
            MemoryByteStream memstr = new MemoryByteStream(_data);
            Ap4Result result = atom.Write(memstr);
            RecomputeSize();
            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::SetData
        +---------------------------------------------------------------------*/
        public Ap4Result SetData(byte[] data, uint data_size)
        {
            _data.SetData(data, data_size);
            RecomputeSize();
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::SetPadding
        +---------------------------------------------------------------------*/
        public Ap4Result SetPadding(byte[] data, uint data_size)
        {
            Ap4Result result;
            result = _padding.SetData(data, data_size);
            if (result.Failed()) return result;
            RecomputeSize();
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::SetSystemId
        +---------------------------------------------------------------------*/
        public void SetSystemId(byte[] system_id)
        {
            Array.Copy(system_id, 0, _systemId, 0, 16);
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::SetKids
        +---------------------------------------------------------------------*/
        public void SetKids(byte[] kids, uint kid_count)
        {
            if (_version ==0) {
                _version = 1;
            }
            _kidCount = kid_count;
            _kids.SetData(kids, 16*kid_count);
            RecomputeSize();
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.Write(_systemId, 16);
            if (result.Failed()) return result;
            if (_version > 0) {
                result = stream.WriteUI32(_kidCount);
                if (result.Failed()) return result;
                result = stream.Write(_kids.GetData(), _kidCount*16);
                if (result.Failed()) return result;
            }
            result = stream.WriteUI32(_data.GetDataSize());
            if (result.Failed()) return result;
            if (_data.GetDataSize() != 0) {
                result = stream.Write(_data.GetData(), _data.GetDataSize());
                if (result.Failed()) return result;
            }
            if (_padding.GetDataSize() != 0) {
                result = stream.Write(_padding.GetData(), _padding.GetDataSize());
                if (result.Failed()) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_PsshAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("system_id", _systemId, 16);
            inspector.AddField("data_size", _data.GetDataSize());

            if (_version > 0) {
                for (uint i=0; i<_kidCount; i++) {
                    string kid_name = "kid " + ((int)i).ToString(CultureInfo.InvariantCulture);
                    inspector.AddField(kid_name, _kids.GetData(), (int)(i*16), 16);
                }
            }

            if (inspector.GetVerbosity() >= 1) {
                byte[] marlin_pssh_system_id = Ap4.MARLIN_PSSH_SYSTEM_ID;
#if NET6_0_OR_GREATER
                if (_systemId.AsSpan(0, 16).SequenceEqual(marlin_pssh_system_id.AsSpan(0, 16))) {
#else
                if (Compat.SequenceEqual(_systemId, 0, marlin_pssh_system_id, 0, 16)) {
#endif
                    MemoryByteStream mbs = new MemoryByteStream(_data);
                    Atom atom;
                    DefaultAtomFactory atom_factory = new DefaultAtomFactory();
                    while (atom_factory.CreateAtomFromStream(mbs, out atom) == Ap4Result.SUCCESS) {
                        ulong position;
                        mbs.Tell(out position);
                        atom.Inspect(inspector);
                        mbs.Seek(position);
                    }
                } else {
                    inspector.AddField("data", _data.GetData(), _data.GetDataSize());
                }
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public byte[]     GetSystemId() { return _systemId; }
        public uint       GetKidCount() { return _kidCount; }
        public DataBuffer GetData() { return _data; }


        // members
        private byte[]     _systemId = new byte[16];
        private DataBuffer _data = new DataBuffer();
        private uint       _kidCount;
        private DataBuffer _kids = new DataBuffer();
        private DataBuffer _padding = new DataBuffer();
    }
}
