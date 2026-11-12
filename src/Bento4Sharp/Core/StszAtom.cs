// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4StszAtom.h, Ap4StszAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_StszAtom
    +---------------------------------------------------------------------*/
    public class StszAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_StszAtom::Create
        +---------------------------------------------------------------------*/
        public static StszAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new StszAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_StszAtom::AP4_StszAtom
        +---------------------------------------------------------------------*/
        public StszAtom() :
            base(AtomType.STSZ, Ap4.FULL_ATOM_HEADER_SIZE+8, 0, 0)
        {
            _sampleSize = 0;
            _sampleCount = 0;
        }

        /*----------------------------------------------------------------------
        |   AP4_StszAtom::AP4_StszAtom
        +---------------------------------------------------------------------*/
        private StszAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.STSZ, size, version, flags)
        {
            _sampleSize = 0;
            _sampleCount = 0;

            if (size < Ap4.FULL_ATOM_HEADER_SIZE + 8) {
                return;
            }

            stream.ReadUI32(out _sampleSize);
            uint sample_count;
            stream.ReadUI32(out sample_count);
            if (_sampleSize == 0) { // means that the samples have different sizes
                // check for overflow
                if (sample_count > (size - Ap4.FULL_ATOM_HEADER_SIZE - 8) / 4) {
                    return;
                }

                // read the entries
                byte[] buffer = new byte[sample_count * 4];
                Ap4Result result = stream.Read(buffer, sample_count * 4);
                if (result.Failed()) {
                    return;
                }
                // m_Entries.SetItemCount((AP4_Cardinal)sample_count)
                _entries.Capacity = (int)sample_count;
                for (uint i = 0; i < sample_count; i++) {
                    _entries.Add(Ap4Utils.BytesToUInt32BE(buffer, (int)(i * 4)));
                }
            }
            _sampleCount = sample_count;
        }

        /*----------------------------------------------------------------------
        |   AP4_StszAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // sample size
            result = stream.WriteUI32(_sampleSize);
            if (result.Failed()) return result;

            // sample count
            result = stream.WriteUI32(_sampleCount);
            if (result.Failed()) return result;

            // entries if needed (the samples have different sizes)
            if (_sampleSize == 0) {
                for (uint i=0; i<_sampleCount; i++) {
                    result = stream.WriteUI32(_entries[(int)i]);
                    if (result.Failed()) return result;
                }
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_StszAtom::GetSampleCount
        +---------------------------------------------------------------------*/
        public virtual uint GetSampleCount()
        {
            return _sampleCount;
        }

        /*----------------------------------------------------------------------
        |   AP4_StszAtom::GetSampleSize
        +---------------------------------------------------------------------*/
        public virtual Ap4Result GetSampleSize(uint sample, out uint sample_size)
        {
            // check the sample index
            if (sample > _sampleCount || sample == 0) {
                sample_size = 0;
                return Ap4Result.ERROR_OUT_OF_RANGE;
            } else {
                // find the size
                if (_sampleSize != 0) { // constant size
                    sample_size = _sampleSize;
                } else {
                    sample_size = _entries[(int)(sample - 1)];
                }
                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_StszAtom::SetSampleSize
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Set the sample size.
        /// </summary>
        /// <param name="sample">1-based index of a sample.</param>
        /// <param name="sample_size">Size of the sample.</param>
        /// <remarks>
        /// If this table represents a global-size table (one where there are
        /// no individual entries for each sample, but all sample have the same
        /// size = m_SampleSize), then calling this method with sample=1 will update
        /// the global size, and calling with sample&gt;1 will check that
        /// the value of sample_size is the same as the global size m_SampleSize.
        /// </remarks>
        public virtual Ap4Result SetSampleSize(uint sample, uint sample_size)
        {
            // check the sample index
            if (sample > _sampleCount || sample == 0) {
                return Ap4Result.ERROR_OUT_OF_RANGE;
            } else {
                if (_entries.Count == 0) {
                    // all samples must have the same size
                    if (sample_size != _sampleSize) {
                        // not the same
                        if (sample == 1 && sample_size != 0) {
                            // if this is the first sample, update the global size
                            _sampleSize = sample_size;
                            return Ap4Result.SUCCESS;
                        } else {
                            // can't have different sizes
                            return Ap4Result.ERROR_INVALID_PARAMETERS;
                        }
                    }
                } else {
                    // each sample has a different size
                    if (sample > (uint)_entries.Count) {
                        return Ap4Result.ERROR_OUT_OF_RANGE;
                    }
                    _entries[(int)(sample - 1)] = sample_size;
                }

                return Ap4Result.SUCCESS;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_StszAtom::AddEntry
        +---------------------------------------------------------------------*/
        public virtual Ap4Result AddEntry(uint size)
        {
            _entries.Add(size);
            _sampleCount++;
            _size32 += 4;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StszAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("sample_size", _sampleSize);
            inspector.AddField("sample_count", _sampleCount);

            if (inspector.GetVerbosity() >= 2) {
                inspector.StartArray("entries", (uint)_entries.Count);
                for (int i=0; i<_entries.Count; i++) {
                    inspector.AddField(null, _entries[i]);
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // members
        private uint       _sampleSize;
        private uint       _sampleCount;
        private List<uint> _entries = new List<uint>();
    }
}
