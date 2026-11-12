// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Stz2Atom.h, Ap4Stz2Atom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_Stz2Atom
    +---------------------------------------------------------------------*/
    public class Stz2Atom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_Stz2Atom::Create
        +---------------------------------------------------------------------*/
        public static Stz2Atom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new Stz2Atom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_Stz2Atom::AP4_Stz2Atom
        +---------------------------------------------------------------------*/
        public Stz2Atom(byte field_size) :
            base(AtomType.STZ2, Ap4.FULL_ATOM_HEADER_SIZE+8, 0, 0)
        {
            _fieldSize = field_size;
            _sampleCount = 0;
            if (_fieldSize != 4 && _fieldSize != 8 && _fieldSize != 16) {
                _fieldSize = 16;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Stz2Atom::AP4_Stz2Atom
        +---------------------------------------------------------------------*/
        private Stz2Atom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.STZ2, size, version, flags)
        {
            _fieldSize = 0;
            _sampleCount = 0;

            if (size < Ap4.FULL_ATOM_HEADER_SIZE + 8) {
                return;
            }

            byte reserved;
            stream.ReadUI08(out reserved);
            stream.ReadUI08(out reserved);
            stream.ReadUI08(out reserved);
            byte field_size;
            stream.ReadUI08(out field_size);
            if (field_size != 4 && field_size != 8 && field_size != 16) {
                // illegal field size
                return;
            }
            uint sample_count;
            if (stream.ReadUI32(out sample_count).Failed()) {
                return;
            }

            ulong table_size = ((ulong)sample_count * (ulong)field_size + 7) / 8;
            if (table_size > size - Ap4.FULL_ATOM_HEADER_SIZE - 8) {
                return;
            }

            byte[] buffer = new byte[table_size];
            Ap4Result result = stream.Read(buffer, (uint)table_size);
            if (result.Failed()) {
                return;
            }
            _fieldSize = field_size;
            _sampleCount = sample_count;
            // m_Entries.SetItemCount((AP4_Cardinal)sample_count)
            _entries.Capacity = (int)sample_count;
            switch (_fieldSize) {
                case 4:
                    for (uint i=0; i<sample_count; i++) {
                        if ((i%2) == 0) {
                            _entries.Add((uint)((buffer[i/2]>>4)&0x0F));
                        } else {
                            _entries.Add((uint)(buffer[i/2]&0x0F));
                        }
                    }
                    break;

                case 8:
                    for (uint i=0; i<sample_count; i++) {
                        _entries.Add(buffer[i]);
                    }
                    break;

                case 16:
                    for (uint i=0; i<sample_count; i++) {
                        _entries.Add(Ap4Utils.BytesToUInt16BE(buffer, (int)(i*2)));
                    }
                    break;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Stz2Atom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // sample size
            result = stream.WriteUI08(0);
            if (result.Failed()) return result;
            result = stream.WriteUI08(0);
            if (result.Failed()) return result;
            result = stream.WriteUI08(0);
            if (result.Failed()) return result;
            result = stream.WriteUI08(_fieldSize);
            if (result.Failed()) return result;

            // sample count
            result = stream.WriteUI32(_sampleCount);
            if (result.Failed()) return result;

            // write the sample sizes
            switch (_fieldSize) {
                case 4:
                    for (uint i=0; i<_sampleCount; i+=2) {
                        if (i+1 < _sampleCount) {
                            result = stream.WriteUI08((byte)(((_entries[(int)i]&0x0F)<<4) | (_entries[(int)(i+1)]&0x0F)));
                        } else {
                            result = stream.WriteUI08((byte)((_entries[(int)i]&0x0F)<<4));
                        }
                        if (result.Failed()) return result;
                    }
                    break;

                case 8:
                    for (uint i=0; i<_sampleCount; i++) {
                        result = stream.WriteUI08((byte)_entries[(int)i]);
                        if (result.Failed()) return result;
                    }
                    break;

                case 16:
                    for (uint i=0; i<_sampleCount; i++) {
                        result = stream.WriteUI16((ushort)_entries[(int)i]);
                        if (result.Failed()) return result;
                    }
                    break;
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_Stz2Atom::GetSampleCount
        +---------------------------------------------------------------------*/
        public virtual uint GetSampleCount()
        {
            return _sampleCount;
        }

        /*----------------------------------------------------------------------
        |   AP4_Stz2Atom::GetSampleSize
        +---------------------------------------------------------------------*/
        public virtual Ap4Result GetSampleSize(uint sample, out uint sample_size)
        {
            // check the sample index
            if (sample > _sampleCount || sample == 0) {
                sample_size = 0;
                return Ap4Result.ERROR_OUT_OF_RANGE;
            } else {
                sample_size = _entries[(int)(sample - 1)];
                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Stz2Atom::SetSampleSize
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Set the sample size.
        /// </summary>
        /// <param name="sample">1-based index of a sample.</param>
        /// <param name="sample_size">Size of the sample.</param>
        public virtual Ap4Result SetSampleSize(uint sample, uint sample_size)
        {
            // check the sample index
            if (sample > _sampleCount || sample == 0) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            } else {
                _entries[(int)(sample - 1)] = sample_size;
                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_Stz2Atom::AddEntry
        +---------------------------------------------------------------------*/
        public virtual Ap4Result AddEntry(uint size)
        {
            _entries.Add(size);
            _sampleCount++;
            if (_fieldSize == 4) {
                if ((_sampleCount%2) == 1) {
                    _size32++;
                }
            } else {
                _size32 += (uint)(_fieldSize/8);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_Stz2Atom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("field_size", _fieldSize);
            inspector.AddField("sample_count", (uint)_entries.Count);

            if (inspector.GetVerbosity() >= 2) {
                inspector.StartArray("entries");
                for (int i=0; i<_entries.Count; i++) {
                    inspector.AddField(null, _entries[i]);
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private byte       _fieldSize;
        private uint       _sampleCount;
        private List<uint> _entries = new List<uint>();
    }
}
