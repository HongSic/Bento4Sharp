// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4SidxAtom.h, Ap4SidxAtom.cpp
// Original work Copyright 2002-2014 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_SidxAtom
    +---------------------------------------------------------------------*/
    public class SidxAtom : Atom
    {
        // types
        // PORT NOTE: a class (not a struct) so that references held in a List<T>
        // can be modified in place, as upstream does through UseReferences()[i].
        // SetReference() copies the fields to keep upstream's value semantics.
        public class Reference {
            public Reference()
            {
                _referenceType = 0;
                _referencedSize = 0;
                _subsegmentDuration = 0;
                _startsWithSap = false;
                _sapType = 0;
                _sapDeltaTime = 0;
            }
            public byte _referenceType;
            public uint _referencedSize;
            public uint _subsegmentDuration;
            public bool _startsWithSap;
            public byte _sapType;
            public uint _sapDeltaTime;
        }

        /*----------------------------------------------------------------------
        |   AP4_SidxAtom::Create
        +---------------------------------------------------------------------*/
        public static SidxAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new SidxAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_SidxAtom::AP4_SidxAtom
        +---------------------------------------------------------------------*/
        public SidxAtom(uint  reference_id,
                        uint  timescale,
                        ulong earliest_presentation_time,
                        ulong first_offset) :
            base(AtomType.SIDX, Ap4.FULL_ATOM_HEADER_SIZE+20, 0, 0)
        {
            _referenceId = reference_id;
            _timeScale = timescale;
            _earliestPresentationTime = earliest_presentation_time;
            _firstOffset = first_offset;

            if ((earliest_presentation_time >> 32) != 0 || (first_offset >> 32) != 0) {
                _version = 1;
                _size32 += 8;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SidxAtom::AP4_SidxAtom
        +---------------------------------------------------------------------*/
        private SidxAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.SIDX, size, version, flags)
        {
            stream.ReadUI32(out _referenceId);
            stream.ReadUI32(out _timeScale);
            if (version == 0) {
                uint earliest_presentation_time = 0;
                uint first_offset = 0;
                stream.ReadUI32(out earliest_presentation_time);
                stream.ReadUI32(out first_offset);
                _earliestPresentationTime = earliest_presentation_time;
                _firstOffset              = first_offset;
            } else {
                stream.ReadUI64(out _earliestPresentationTime);
                stream.ReadUI64(out _firstOffset);
            }
            ushort reserved;
            stream.ReadUI16(out reserved);
            ushort reference_count = 0;
            stream.ReadUI16(out reference_count);
            if (size < Ap4.FULL_ATOM_HEADER_SIZE+4+4+(version==0?8u:16u)+2+2+(uint)reference_count*12) {
                // not enough space to store all references, something's wrong
                return;
            }
            // m_References.SetItemCount(reference_count)
            for (uint i=0; i<reference_count; i++) {
                _references.Add(new Reference());
            }
            for (int i=0; i<reference_count; i++) {
                uint value = 0;
                stream.ReadUI32(out value);
                _references[i]._referenceType = (byte)((value&(1u<<31)) != 0 ? 1 : 0);
                _references[i]._referencedSize = value & 0x7FFFFFFF;
                stream.ReadUI32(out _references[i]._subsegmentDuration);
                stream.ReadUI32(out value);
                _references[i]._startsWithSap = ((value&(1u<<31)) != 0);
                _references[i]._sapType       = (byte)((value >>28)&0x07);
                _references[i]._sapDeltaTime  = value & 0x0FFFFFFF;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_SidxAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            stream.WriteUI32(_referenceId);
            stream.WriteUI32(_timeScale);
            if (_version == 0) {
                stream.WriteUI32((uint)_earliestPresentationTime);
                stream.WriteUI32((uint)_firstOffset);
            } else {
                stream.WriteUI64(_earliestPresentationTime);
                stream.WriteUI64(_firstOffset);
            }
            stream.WriteUI16(0);
            stream.WriteUI16((ushort)_references.Count);
            for (int i=0; i<_references.Count; i++) {
                stream.WriteUI32(((uint)_references[i]._referenceType<<31) |
                                  _references[i]._referencedSize);
                stream.WriteUI32(_references[i]._subsegmentDuration);
                stream.WriteUI32(((_references[i]._startsWithSap?1u:0u)<<31) |
                                  ((uint)_references[i]._sapType << 28) |
                                   _references[i]._sapDeltaTime);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_SidxAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("reference_ID", _referenceId);
            inspector.AddField("timescale", _timeScale);
            inspector.AddField("earliest_presentation_time", _earliestPresentationTime);
            inspector.AddField("first_offset", _firstOffset);

            if (inspector.GetVerbosity() >= 1) {
                uint reference_count = (uint)_references.Count;
                inspector.StartArray("entries", reference_count);
                for (int i=0; i<(int)reference_count; i++) {
                    inspector.StartObject(null, 6, true);
                    inspector.AddField("reference_type",      _references[i]._referenceType);
                    inspector.AddField("referenced_size",     _references[i]._referencedSize);
                    inspector.AddField("subsegment_duration", _references[i]._subsegmentDuration);
                    inspector.AddField("starts_with_SAP",     _references[i]._startsWithSap ? 1 : 0);
                    inspector.AddField("SAP_type",            _references[i]._sapType);
                    inspector.AddField("SAP_delta_time",      _references[i]._sapDeltaTime);
                    inspector.EndObject();
                }
                inspector.EndArray();
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint  GetReferenceId()              { return _referenceId;              }
        public uint  GetTimeScale()                { return _timeScale;                }
        public ulong GetEarliestPresentationTime() { return _earliestPresentationTime; }
        public ulong GetFirstOffset()              { return _firstOffset;              }
        public void SetReferenceId(uint reference_id)       { _referenceId = reference_id;      }
        public void SetTimeScale(uint timescale)            { _timeScale = timescale;           }
        public void SetEarliestPresentationTime(ulong time) { _earliestPresentationTime = time; }
        public void SetFirstOffset(ulong offset)            { _firstOffset              = offset; }

        // access to references methods
        public List<Reference> GetReferences() {
            return _references;
        }
        public List<Reference> UseReferences() {
            return _references;
        }

        /*----------------------------------------------------------------------
        |   AP4_SidxAtom::SetReferenceCount
        +---------------------------------------------------------------------*/
        public void SetReferenceCount(uint count) {
            _size32 -= (uint)_references.Count*12;
            // m_References.SetItemCount(count)
            if (count < (uint)_references.Count) {
                _references.RemoveRange((int)count, _references.Count-(int)count);
            } else {
                while ((uint)_references.Count < count) {
                    _references.Add(new Reference());
                }
            }
            _size32 += (uint)_references.Count*12;
        }

        public void SetReference(uint      reference_index,
                                 Reference reference) {
            // m_References[reference_index] = reference (copy by value)
            Reference copy = new Reference();
            copy._referenceType      = reference._referenceType;
            copy._referencedSize     = reference._referencedSize;
            copy._subsegmentDuration = reference._subsegmentDuration;
            copy._startsWithSap      = reference._startsWithSap;
            copy._sapType            = reference._sapType;
            copy._sapDeltaTime       = reference._sapDeltaTime;
            _references[(int)reference_index] = copy;
        }

        // members
        private uint            _referenceId;
        private uint            _timeScale;
        private ulong           _earliestPresentationTime;
        private ulong           _firstOffset;
        private List<Reference> _references = new List<Reference>();
    }
}
