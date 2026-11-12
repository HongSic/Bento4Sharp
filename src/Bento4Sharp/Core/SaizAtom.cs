// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SaizAtom.h, Ap4SaizAtom.cpp
// Original work Copyright 2002-2011 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SaizAtom
    +---------------------------------------------------------------------*/
    public class SaizAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_SaizAtom::Create
        +---------------------------------------------------------------------*/
        public static SaizAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 0) return null;
            return new SaizAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_SaizAtom::AP4_SaizAtom
        +---------------------------------------------------------------------*/
        public SaizAtom() :
            base(AtomType.SAIZ, Ap4.FULL_ATOM_HEADER_SIZE+1+4, 0, 0)
        {
            _auxInfoType           = 0;
            _auxInfoTypeParameter  = 0;
            _defaultSampleInfoSize = 0;
            _sampleCount           = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_SaizAtom::AP4_SaizAtom
        +---------------------------------------------------------------------*/
        private SaizAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.SAIZ, size, version, flags)
        {
            _auxInfoType          = 0;
            _auxInfoTypeParameter = 0;

            uint remains = size-GetHeaderSize();
            if ((flags & 1) != 0) {
                stream.ReadUI32(out _auxInfoType);
                stream.ReadUI32(out _auxInfoTypeParameter);
                remains -= 8;
            }
            stream.ReadUI08(out _defaultSampleInfoSize);
            stream.ReadUI32(out _sampleCount);
            remains -= 5;
            if (_defaultSampleInfoSize == 0) {
                // means that the sample info entries have different sizes
                if (_sampleCount > remains) _sampleCount = remains; // sanity check
                uint sample_count = _sampleCount;
                // PORT NOTE: `remains` wraps around (as upstream) when the atom is
                // too small for its fixed fields, so sample_count may be close to
                // 4G here. Upstream then fails to allocate (std::bad_alloc); a .NET
                // array cannot be that large, so instead of throwing on malformed
                // input the port keeps an empty table and a sample count of 0.
#if NET6_0_OR_GREATER
                if (sample_count > (uint)Array.MaxLength) {
#else
                if (sample_count > (uint)Compat.ArrayMaxLength) {
#endif
                    _sampleCount = 0;
                    return;
                }
                byte[] buffer = new byte[sample_count];
                Ap4Result result = stream.Read(buffer, sample_count);
                if (result.Failed()) {
                    // m_Entries.SetItemCount(sample_count) was done before the read:
                    // the entries exist and are all 0
                    SetItemCount(_entries, sample_count);
                    return;
                }
                _entries.AddRange(buffer);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Array<AP4_UI08>::SetItemCount
        +---------------------------------------------------------------------*/
        // PORT NOTE: helper standing in for AP4_Array::SetItemCount (new items are 0)
        private static void SetItemCount(List<byte> entries, uint item_count)
        {
            if (item_count == (uint)entries.Count) return;
            if (item_count < (uint)entries.Count) {
                entries.RemoveRange((int)item_count, entries.Count-(int)item_count);
                return;
            }
            entries.AddRange(new byte[item_count-(uint)entries.Count]);
        }

        /*----------------------------------------------------------------------
        |   AP4_SaizAtom::SetSampleCount
        +---------------------------------------------------------------------*/
        public Ap4Result SetSampleCount(uint sample_count)
        {
            _sampleCount = sample_count;
            uint extra = (_flags&1) != 0?8u:0u;
            if (_defaultSampleInfoSize == 0) {
                if (sample_count != 0) {
                    SetItemCount(_entries, sample_count);
                } else {
                    _entries.Clear();
                }
                SetSize((ulong)(Ap4.FULL_ATOM_HEADER_SIZE+extra+1+4+sample_count));
            } else {
                SetSize((ulong)(Ap4.FULL_ATOM_HEADER_SIZE+extra+1+4));
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SaizAtom::SetDefaultSampleInfoSize
        +---------------------------------------------------------------------*/
        public Ap4Result SetDefaultSampleInfoSize(byte sample_info_size)
        {
            _defaultSampleInfoSize = sample_info_size;
            _entries.Clear();
            uint extra = (_flags&1) != 0?8u:0u;
            SetSize((ulong)(Ap4.FULL_ATOM_HEADER_SIZE+extra+1+4));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SaizAtom::GetSampleInfoSize
        +---------------------------------------------------------------------*/
        public Ap4Result GetSampleInfoSize(uint sample, out byte sample_info_size)
        {
            if (_defaultSampleInfoSize != 0) {
                sample_info_size = _defaultSampleInfoSize;
            } else {
                // check the sample index
                if (sample >= _sampleCount) {
                    // just return a 0 size, not an error, because of the possibility
                    // that we have a degenerate case where sample_count is 0 and
                    // the default_sample_info_size is also 0
                    sample_info_size = 0;
                } else {
                    sample_info_size = _entries[(int)sample];
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SaizAtom::SetSampleInfoSize
        +---------------------------------------------------------------------*/
        public Ap4Result SetSampleInfoSize(uint sample, byte sample_info_size)
        {
            // check the sample index
            if (sample >= _sampleCount) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            } else {
                if (_defaultSampleInfoSize != 0) {
                    if (_defaultSampleInfoSize != sample_info_size) {
                        return Ap4Result.ERROR_INVALID_STATE;
                    }
                } else {
                    _entries[(int)sample] = sample_info_size;
                }
                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SaizAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            if ((_flags&1) != 0) {
                result = stream.WriteUI32(_auxInfoType);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_auxInfoTypeParameter);
                if (result.Failed()) return result;
            }
            result = stream.WriteUI08(_defaultSampleInfoSize);
            if (result.Failed()) return result;
            result = stream.WriteUI32(_sampleCount);
            if (result.Failed()) return result;
            if (_defaultSampleInfoSize == 0) {
                for (uint i=0; i<_sampleCount; i++) {
                    result = stream.WriteUI08(_entries[(int)i]);
                    if (result.Failed()) return result;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SaizAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            if ((_flags&1) != 0) {
                inspector.AddField("aux info type", _auxInfoType, AtomInspector.FormatHint.HINT_HEX);
                inspector.AddField("aux info type parameter", _auxInfoTypeParameter, AtomInspector.FormatHint.HINT_HEX);
            }
            inspector.AddField("default sample info size", _defaultSampleInfoSize);
            inspector.AddField("sample count", _sampleCount);

            if (inspector.GetVerbosity() >= 2) {
                inspector.StartArray("entries", (uint)_entries.Count);
                for (int i=0; i<_entries.Count; i++) {
                    inspector.AddField(null, _entries[i]);
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        public uint GetAuxInfoType() { return _auxInfoType; }
        public uint GetAuxInfoTypeParameter() { return _auxInfoTypeParameter; }
        public uint GetSampleCount() { return _sampleCount; }

        // members
        private uint       _auxInfoType;
        private uint       _auxInfoTypeParameter;
        private byte       _defaultSampleInfoSize;
        private uint       _sampleCount;
        private List<byte> _entries = new List<byte>();
    }
}
