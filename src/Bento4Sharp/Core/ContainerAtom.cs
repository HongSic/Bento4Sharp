// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4ContainerAtom.h, Ap4ContainerAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_ContainerAtom
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream derives from AP4_Atom and AP4_AtomParent; in the port
    // Atom itself derives from AtomParent (see Core/Atom.cs), so the children
    // list (_children) and the child management methods are inherited from it.
    public class ContainerAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::Create
        +---------------------------------------------------------------------*/
        public static ContainerAtom Create(uint        type,
                                            ulong       size,
                                            bool        is_full,
                                            bool        force_64,
                                            ByteStream  stream,
                                            AtomFactory atom_factory)
        {
            if (is_full) {
                byte version;
                uint flags;
                if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
                if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;

                // special case for 'meta' atoms, because Apple sometimes creates them as
                // regular (non-full) atoms. This is bogus, but we can try to detect it
                if (type == AtomType.META) {
                    uint phantom_size = ((uint)version<<24)|flags;
                    if (phantom_size >= 8 && size >= 16) {
                        // version+flags looks like a size. read the next 4 bytes just
                        // to be sure it is a hdlr atom
                        uint peek;
                        if (stream.ReadUI32(out peek).Failed()) return null;
                        if (peek == AtomType.HDLR) {
                            // rewind the stream by 8 bytes
                            ulong position;
                            stream.Tell(out position);
                            stream.Seek(position-8);

                            // create a non-full container
                            return new ContainerAtom(type, size, force_64, stream, atom_factory);
                        } else {
                            // rewind the stream by 4 bytes
                            ulong position;
                            stream.Tell(out position);
                            stream.Seek(position-4);
                        }
                    }
                }

                return new ContainerAtom(type, size, force_64, version, flags, stream, atom_factory);
            } else {
                return new ContainerAtom(type, size, force_64, stream, atom_factory);
            }
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::AP4_ContainerAtom
        +---------------------------------------------------------------------*/
        public ContainerAtom(uint type) :
            base(type, Ap4.ATOM_HEADER_SIZE)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::AP4_ContainerAtom
        +---------------------------------------------------------------------*/
        public ContainerAtom(uint type, byte version, uint flags) :
            base(type, Ap4.FULL_ATOM_HEADER_SIZE, version, flags)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::AP4_ContainerAtom
        +---------------------------------------------------------------------*/
        public ContainerAtom(uint type, ulong size, bool force_64) :
            base(type, size, force_64)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::AP4_ContainerAtom
        +---------------------------------------------------------------------*/
        public ContainerAtom(uint  type,
                             ulong size,
                             bool  force_64,
                             byte  version,
                             uint  flags) :
            base(type, size, force_64, version, flags)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::AP4_ContainerAtom
        +---------------------------------------------------------------------*/
        protected ContainerAtom(uint        type,
                                ulong       size,
                                bool        force_64,
                                ByteStream  stream,
                                AtomFactory atom_factory) :
            base(type, size, force_64)
        {
            if (size < GetHeaderSize()) return;
            ReadChildren(atom_factory, stream, size-GetHeaderSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::AP4_ContainerAtom
        +---------------------------------------------------------------------*/
        protected ContainerAtom(uint        type,
                                ulong       size,
                                bool        force_64,
                                byte        version,
                                uint        flags,
                                ByteStream  stream,
                                AtomFactory atom_factory) :
            base(type, size, force_64, version, flags)
        {
            if (size < GetHeaderSize()) return;
            ReadChildren(atom_factory, stream, size-GetHeaderSize());
        }

        public new List<Atom> GetChildren() { return _children; }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::Clone
        +---------------------------------------------------------------------*/
        public override Atom Clone()
        {
            ContainerAtom clone;
            if (_isFull) {
                clone = new ContainerAtom(_type, _version, _flags);
            } else {
                clone = new ContainerAtom(_type);
            }

            for (int i = 0; i < _children.Count; i++) {
                Atom child_clone = _children[i].Clone();
                if (child_clone != null) clone.AddChild(child_clone);
            }

            return clone;
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::ReadChildren
        +---------------------------------------------------------------------*/
        protected void ReadChildren(AtomFactory atom_factory,
                                    ByteStream  stream,
                                    ulong       size)
        {
            Atom  atom;
            ulong bytes_available = size;

            // save and switch the factory's context
            atom_factory.PushContext(_type);

            while (atom_factory.CreateAtomFromStream(stream, ref bytes_available, out atom).Succeeded()) {
                atom.SetParent(this);
                _children.Add(atom);
            }

            // restore the saved context
            atom_factory.PopContext();
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            return InspectChildren(inspector);
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::InspectChildren
        +---------------------------------------------------------------------*/
        public virtual Ap4Result InspectChildren(AtomInspector inspector)
        {
            // inspect children
            _children.Apply(new AtomListInspector(inspector));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write all children
            return _children.Apply(new AtomListWriter(stream));
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::OnChildChanged
        +---------------------------------------------------------------------*/
        public override void OnChildChanged(Atom child)
        {
            // remcompute our size
            ulong size = GetHeaderSize();
            AtomSizeAdder adder = new AtomSizeAdder(size);
            _children.Apply(adder);
            size = adder.GetSize();
            SetSize(size);

            // update our parent
            if (_parent != null) _parent.OnChildChanged(this);
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::OnChildAdded
        +---------------------------------------------------------------------*/
        public override void OnChildAdded(Atom child)
        {
            // update our size
            SetSize(GetSize()+child.GetSize());

            // update our parent
            if (_parent != null) _parent.OnChildChanged(this);
        }

        /*----------------------------------------------------------------------
        |   AP4_ContainerAtom::OnChildRemoved
        +---------------------------------------------------------------------*/
        public override void OnChildRemoved(Atom child)
        {
            // update our size
            SetSize(GetSize()-child.GetSize());

            // update our parent
            if (_parent != null) _parent.OnChildChanged(this);
        }
    }
}
