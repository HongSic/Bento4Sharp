// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4StsdAtom.h, Ap4StsdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_StsdAtom
    +---------------------------------------------------------------------*/
    public class StsdAtom : ContainerAtom
    {
        /*----------------------------------------------------------------------
        |   AP4_StsdAtom::Create
        +---------------------------------------------------------------------*/
        public static StsdAtom Create(uint        size,
                                       ByteStream  stream,
                                       AtomFactory atom_factory)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new StsdAtom(size, version, flags, stream, atom_factory);
        }

        /*----------------------------------------------------------------------
        |   AP4_StsdAtom::AP4_StsdAtom
        +---------------------------------------------------------------------*/
        public StsdAtom(SampleTable sample_table) :
            base(AtomType.STSD, (byte)0, (uint)0)
        {
            _size32 += 4;
            uint sample_description_count = sample_table.GetSampleDescriptionCount();
            _sampleDescriptions.Capacity = (int)sample_description_count;
            for (uint i=0; i<sample_description_count; i++) {
                // clear the cache entry
                _sampleDescriptions.Add(null);

                // create an entry for the description
                SampleDescription  sample_description = sample_table.GetSampleDescription(i);
                if (sample_description == null)
                    continue;
                Atom entry = sample_description.ToAtom();
                AddChild(entry);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_StsdAtom::AP4_StsdAtom
        +---------------------------------------------------------------------*/
        private StsdAtom(uint        size,
                         byte        version,
                         uint        flags,
                         ByteStream  stream,
                         AtomFactory atom_factory) :
            base(AtomType.STSD, size, false, version, flags)
        {
            if (size < Ap4.FULL_ATOM_HEADER_SIZE + 4) return;

            // read the number of entries
            uint entry_count;
            stream.ReadUI32(out entry_count);

            // save and switch the factory's context
            atom_factory.PushContext(_type);

            // read all entries
            ulong bytes_available = size-Ap4.FULL_ATOM_HEADER_SIZE-4;
            for (uint i=0; i<entry_count; i++) {
                Atom  atom;
                if (atom_factory.CreateAtomFromStream(stream,
                                                      ref bytes_available,
                                                      out atom).Succeeded()) {
                    atom.SetParent(this);
                    _children.Add(atom);
                } else {
                    break;
                }
            }

            // restore the saved context
            atom_factory.PopContext();

            // initialize the sample description cache
            _sampleDescriptions.Capacity = _children.Count;
            for (uint i=0; i<(uint)_children.Count; i++) {
                _sampleDescriptions.Add(null);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_StsdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // entry count
            result = stream.WriteUI32((uint)_children.Count);
            if (result.Failed()) return result;

            // entries
            return _children.Apply(new AtomListWriter(stream));
        }

        /*----------------------------------------------------------------------
        |   AP4_StsdAtom::OnChildChanged
        +---------------------------------------------------------------------*/
        public override void OnChildChanged(Atom child)
        {
            // remcompute our size
            ulong size = GetHeaderSize()+4;
            AtomSizeAdder adder = new AtomSizeAdder(size);
            _children.Apply(adder);
            size = adder.GetSize();
            _size32 = (uint)size;

            // update our parent
            if (_parent != null) _parent.OnChildChanged(this);
        }

        /*----------------------------------------------------------------------
        |   AP4_StsdAtom::GetSampleDescription
        +---------------------------------------------------------------------*/
        public virtual SampleDescription GetSampleDescription(uint index)
        {
            // check index
            if (index >= (uint)_children.Count) return null;

            // PORT NOTE: upstream indexes m_SampleDescriptions without a bounds
            // check; the cache can be shorter than the child list when children
            // were added after construction (out-of-bounds read upstream). The
            // port grows the cache instead of throwing.
            while ((uint)_sampleDescriptions.Count <= index) {
                _sampleDescriptions.Add(null);
            }

            // return the description if we already have it in the internal table
            if (_sampleDescriptions[(int)index] != null) return _sampleDescriptions[(int)index];

            // create and cache a sample description for this entry
            Atom entry = _children[(int)index];
            SampleEntry  sample_entry = entry as SampleEntry;
            if (sample_entry == null) {
                _sampleDescriptions[(int)index] = new UnknownSampleDescription(entry);
            } else {
                _sampleDescriptions[(int)index] = sample_entry.ToSampleDescription();
            }
            return _sampleDescriptions[(int)index];
        }

        /*----------------------------------------------------------------------
        |   AP4_StsdAtom::GetSampleEntry
        +---------------------------------------------------------------------*/
        public virtual SampleEntry GetSampleEntry(uint index)
        {
            // check index
            if (index >= (uint)_children.Count) return null;

            // return the sample entry
            Atom entry = _children[(int)index];
            return entry as SampleEntry;
        }

        /*----------------------------------------------------------------------
        |   AP4_StsdAtom::GetSampleDescriptionCount
        +---------------------------------------------------------------------*/
        public virtual uint GetSampleDescriptionCount()
        {
            return (uint)_children.Count;
        }

        /*----------------------------------------------------------------------
        |   AP4_StsdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("entry_count", (uint)_children.Count);

            // inspect children
            _children.Apply(new AtomListInspector(inspector));

            return Ap4Result.SUCCESS;
        }

        // members
        private List<SampleDescription> _sampleDescriptions = new List<SampleDescription>();
    }
}
