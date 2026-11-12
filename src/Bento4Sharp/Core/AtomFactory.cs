// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.h, Ap4AtomFactory.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Collections.Generic;

namespace Bento4
{

    // PORT NOTE: the two big `switch (type)` statements of
    // AP4_AtomFactory::CreateAtomFromStream are replaced by two lookup tables
    // (regular atoms, and sample entries used when the context is 'stsd') held
    // by an AtomRegistry. The tables are filled once, from the static
    // constructor of AtomFactory, by `static partial void RegisterGroupXxx`
    // hooks that the other ported files implement (see docs/PORTING.md and
    // docs/FOUNDATION_API.md). Everything else (context stack, type handlers,
    // size handling, fallback to UnknownAtom) is a line by line port.

    /*----------------------------------------------------------------------
    |   AtomCreationContext
    +---------------------------------------------------------------------*/
    /// <summary>
    /// What a `case AP4_ATOM_TYPE_XXXX:` of upstream has at hand: the arguments
    /// and locals of AP4_AtomFactory::CreateAtomFromStream(stream, type, size_32,
    /// size_64, atom), and `*this`.
    /// </summary>
    public struct AtomCreationContext
    {
        public AtomCreationContext(uint type, uint size_32, ulong size_64, ByteStream stream, AtomFactory factory)
        {
            Type    = type;
            Size32  = size_32;
            Size64  = size_64;
            IsLarge = (size_32 == 1);
            Force64 = (size_32 == 1 && ((size_64 >> 32) == 0));
            Stream  = stream;
            Factory = factory;
        }

        /// <summary>`type`: the atom type read from the header.</summary>
        public readonly uint        Type;
        /// <summary>`size_32`: the 32-bit size field (1 for a 64-bit size).</summary>
        public readonly uint        Size32;
        /// <summary>`size_64`: the actual atom size, header included.</summary>
        public readonly ulong       Size64;
        /// <summary>`atom_is_large`: size_32 == 1.</summary>
        public readonly bool        IsLarge;
        /// <summary>`force_64`: 64-bit size field whose value fits in 32 bits.</summary>
        public readonly bool        Force64;
        /// <summary>`stream`: positioned just after the atom header (8 or 16 bytes).</summary>
        public readonly ByteStream  Stream;
        /// <summary>`*this`</summary>
        public readonly AtomFactory Factory;

        /// <summary>`GetContext(depth)`: type of the enclosing container(s); 0 at the top level.</summary>
        public uint GetContext(uint depth = 0) { return Factory.GetContext(depth); }

        /// <summary>`&amp;m_ContextStack` (for AP4_SchmAtom::Create); innermost context last.</summary>
        public List<uint> ContextStack { get { return Factory.GetContextStack(); } }

        /// <summary>`atom = x; break;` (x may be null: fall back to UnknownAtom).</summary>
        public AtomCreationResult Ok(Atom  atom) { return new AtomCreationResult(Ap4Result.SUCCESS, atom); }

        /// <summary>`return result;`</summary>
        public AtomCreationResult Fail(Ap4Result result) { return new AtomCreationResult(result, null); }
    }

    /*----------------------------------------------------------------------
    |   AtomCreationResult
    +---------------------------------------------------------------------*/
    public struct AtomCreationResult
    {
        public AtomCreationResult(Ap4Result result, Atom atom)
        {
            Result = result;
            Atom   = atom;
        }

        /// <summary>When not SUCCESS, CreateAtomFromStream returns it (and no atom).</summary>
        public readonly Ap4Result Result;
        /// <summary>The atom, or null to let the factory create an UnknownAtom.</summary>
        public readonly Atom      Atom;
    }

    /// <summary>The body of one `case` of the upstream switch.</summary>
    public delegate AtomCreationResult AtomCreator(AtomCreationContext c);

    /// <summary>Creates the sample entry for a type that is not in the sample entry table (AP4_UnknownSampleEntry).</summary>
    public delegate Atom UnknownSampleEntryCreator(uint type, uint size, ByteStream stream);

    /*----------------------------------------------------------------------
    |   AtomRegistry
    +---------------------------------------------------------------------*/
    public sealed class AtomRegistry
    {
        /// <summary>`if (atom_is_large) return AP4_ERROR_INVALID_FORMAT;`</summary>
        public static readonly AtomCreationResult InvalidFormat = new AtomCreationResult(Ap4Result.ERROR_INVALID_FORMAT, null);

        /// <summary>`break;` without creating an atom: the factory creates an UnknownAtom.</summary>
        public static readonly AtomCreationResult Fallback = new AtomCreationResult(Ap4Result.SUCCESS, null);

        /// <summary>
        /// Registers the creator of a regular atom (used when the context is not
        /// 'stsd'). Throws InvalidOperationException if the type is already
        /// registered.
        /// </summary>
        public void Atom(uint type, AtomCreator creator)
        {
            Add(_atoms, type, creator, "atom");
        }

        /// <summary>Same creator for several types (`case A: case B: ...`); the creator gets the actual type in c.Type.</summary>
        public void Atoms(uint[] types, AtomCreator creator)
        {
            foreach (uint type in types) Atom(type, creator);
        }

        /// <summary>
        /// Registers the creator of a sample entry (used when the context is
        /// 'stsd'). The `atom_is_large` check is done by the factory before the
        /// creator is called. Throws InvalidOperationException if the type is
        /// already registered.
        /// </summary>
        public void SampleEntry(uint type, AtomCreator creator)
        {
            Add(_sampleEntries, type, creator, "sample entry");
        }

        /// <summary>Same creator for several sample entry types.</summary>
        public void SampleEntries(uint[] types, AtomCreator creator)
        {
            foreach (uint type in types) SampleEntry(type, creator);
        }

        public bool TryGetAtom(uint type, out AtomCreator creator)
        {
            return _atoms.TryGetValue(type, out creator);
        }

        public bool TryGetSampleEntry(uint type, out AtomCreator creator)
        {
            return _sampleEntries.TryGetValue(type, out creator);
        }

        private static void Add(Dictionary<uint, AtomCreator> table, uint type, AtomCreator creator, string kind)
        {
            if (creator == null) throw new ArgumentNullException("creator");
            if (!table.TryAdd(type, creator)) {
                throw new InvalidOperationException(
                    "AtomRegistry: " + kind + " type '" + Ap4Utils.FormatFourCharsPrintable(type) + "' is registered twice");
            }
        }

        private readonly Dictionary<uint, AtomCreator> _atoms         = new Dictionary<uint, AtomCreator>();
        private readonly Dictionary<uint, AtomCreator> _sampleEntries = new Dictionary<uint, AtomCreator>();
    }

    /*----------------------------------------------------------------------
    |   AP4_AtomFactory
    +---------------------------------------------------------------------*/
    public partial class AtomFactory
    {
        // types
        public abstract class TypeHandler
        {
            /// <summary>
            /// Returns SUCCESS and an atom when the handler recognizes the type;
            /// otherwise a failure code and a null atom.
            /// </summary>
            public abstract Ap4Result CreateAtom(uint       type,
                                                 uint       size,
                                                 ByteStream stream,
                                                 uint       context,
                                                 out Atom   atom);
        }

        /*----------------------------------------------------------------------
        |   registration (PORT NOTE: replaces the switch statements)
        +---------------------------------------------------------------------*/
        private static readonly AtomRegistry s_registry;

        /// <summary>The tables shared by all the factories.</summary>
        public static AtomRegistry Registry { get { return s_registry; } }

        /// <summary>
        /// Hook set by the SampleEntry port (from its Register hook): creates the
        /// AP4_UnknownSampleEntry for a type that has no registered sample entry
        /// and that no type handler recognizes. When null, such entries become
        /// UnknownAtom objects.
        /// </summary>
        public static UnknownSampleEntryCreator UnknownSampleEntryCreator;

        // implemented (or not) by the files that port the atom classes
        static partial void RegisterGroupA(AtomRegistry r);
        static partial void RegisterGroupB(AtomRegistry r);
        static partial void RegisterGroupC1(AtomRegistry r);
        static partial void RegisterGroupC2(AtomRegistry r);
        static partial void RegisterGroupD(AtomRegistry r);
        static partial void RegisterGroupE(AtomRegistry r);
        static partial void RegisterGroupF(AtomRegistry r);
        static partial void RegisterGroupG(AtomRegistry r);
        static partial void RegisterGroupH(AtomRegistry r);
        static partial void RegisterGroupE6(AtomRegistry r);
        static partial void RegisterGroupMetaData(AtomRegistry r);

        // The static constructor runs before the first AtomFactory (or subclass)
        // instance is created and before any static member is accessed, so the
        // tables are always complete before their first use.
        static AtomFactory()
        {
            AtomRegistry r = new AtomRegistry();
            RegisterCore(r);
            RegisterGroupA(r);
            RegisterGroupB(r);
            RegisterGroupC1(r);
            RegisterGroupC2(r);
            RegisterGroupD(r);
            RegisterGroupE(r);
            RegisterGroupF(r);
            RegisterGroupG(r);
            RegisterGroupH(r);
            RegisterGroupE6(r);
            RegisterGroupMetaData(r);
            s_registry = r;
        }

        // the cases of the upstream switch that only need the classes of the
        // foundation layer
        private static void RegisterCore(AtomRegistry r)
        {
            r.Atom(AtomType._8ID_, (c) => c.Ok(new NullTerminatedStringAtom(c.Type, c.Size64, c.Stream)));

            // container atoms
            r.Atoms(new uint[] {
                AtomType.MOOF,
                AtomType.MVEX,
                AtomType.TRAF,
                AtomType.TREF,
                AtomType.MFRA,
                AtomType.HNTI,
                AtomType.STBL,
                AtomType.MDIA,
                AtomType.DINF,
                AtomType.MINF,
                AtomType.SCHI,
                AtomType.SINF,
                AtomType.UDTA,
                AtomType.ILST,
                AtomType.EDTS,
                AtomType.MDRI,
                AtomType.WAVE
            }, (c) => c.IsLarge ? AtomRegistry.InvalidFormat
                                : c.Ok(ContainerAtom.Create(c.Type, c.Size64, false, c.Force64, c.Stream, c.Factory)));

            // containers, only at the top
            r.Atom(AtomType.MARL, (c) => {
                if (c.GetContext() == 0) {
                    return c.Ok(ContainerAtom.Create(c.Type, c.Size64, false, c.Force64, c.Stream, c.Factory));
                }
                return AtomRegistry.Fallback;
            });

            // full container atoms
            r.Atoms(new uint[] {
                AtomType.META,
                AtomType.ODRM,
                AtomType.ODKM
            }, (c) => c.Ok(ContainerAtom.Create(c.Type, c.Size64, true, c.Force64, c.Stream, c.Factory)));

            // generic atoms
            r.Atoms(new uint[] {
                AtomType.FREE,
                AtomType.WIDE,
                AtomType.MDAT
            }, (c) => AtomRegistry.Fallback);
        }

        // constructor
        public AtomFactory() {}

        // methods
        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::AddTypeHandler
        +---------------------------------------------------------------------*/
        public Ap4Result AddTypeHandler(TypeHandler handler)
        {
            _typeHandlers.Add(handler);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::RemoveTypeHandler
        +---------------------------------------------------------------------*/
        public Ap4Result RemoveTypeHandler(TypeHandler handler)
        {
            for (int i = 0; i < _typeHandlers.Count; i++) {
                if (ReferenceEquals(_typeHandlers[i], handler)) {
                    _typeHandlers.RemoveAt(i);
                    return Ap4Result.SUCCESS;
                }
            }
            return Ap4Result.ERROR_NO_SUCH_ITEM;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::CreateAtomFromStream
        +---------------------------------------------------------------------*/
        public Ap4Result CreateAtomFromStream(ByteStream stream,
                                              out Atom   atom)
        {
            ulong stream_size     = 0;
            ulong stream_position = 0;
            ulong bytes_available = unchecked((ulong)(-1L));
            if (stream.GetSize(out stream_size).Succeeded() &&
                stream_size != 0 &&
                stream.Tell(out stream_position).Succeeded() &&
                stream_position <= stream_size) {
                bytes_available = stream_size-stream_position;
            }
            return CreateAtomFromStream(stream, ref bytes_available, out atom);
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::CreateAtomFromStream
        +---------------------------------------------------------------------*/
        public Ap4Result CreateAtomFromStream(ByteStream stream,
                                              ref ulong  bytes_available,
                                              out Atom   atom)
        {
            Ap4Result result;

            // NULL by default
            atom = null;

            // check that there are enough bytes for at least a header
            if (bytes_available < 8) return Ap4Result.ERROR_EOS;

            // remember current stream offset
            ulong start;
            stream.Tell(out start);

            // read atom size
            uint size_32;
            result = stream.ReadUI32(out size_32);
            if (result.Failed()) {
                stream.Seek(start);
                return result;
            }
            ulong size = size_32;

            // read atom type
            uint type;
            result = stream.ReadUI32(out type);
            if (result.Failed()) {
                stream.Seek(start);
                return result;
            }

            // handle special size values
            bool atom_is_large = false;
            bool force_64      = false;
            if (size == 0) {
                // atom extends to end of file
                ulong stream_size = 0;
                stream.GetSize(out stream_size);
                if (stream_size >= start) {
                    size = stream_size - start;
                    if (size <= 0xFFFFFFFF) {
                        size_32 = (uint)size;
                    } else {
                        size_32 = 1; // signal a large atom
                    }
                }
            } else if (size == 1) {
                // 64-bit size
                atom_is_large = true;
                if (bytes_available < 16) {
                    stream.Seek(start);
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                stream.ReadUI64(out size);
                if (size < 16) {
                    stream.Seek(start);
                    return Ap4Result.ERROR_INVALID_FORMAT;
                }
                if (size <= 0xFFFFFFFF) {
                    force_64 = true;
                }
            }

            // check the size
            if ((size > 0 && size < 8) || size > bytes_available) {
                stream.Seek(start);
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // create the atom
            result = CreateAtomFromStream(stream, type, size_32, size, out atom);
            if (result.Failed()) {
                // PORT NOTE: `atom` is still NULL upstream in this case
                atom = null;
                return result;
            }

            // if we failed to create an atom, use a generic version
            if (atom == null) {
                uint payload_offset = 8;
                if (atom_is_large) payload_offset += 8;
                stream.Seek(start+payload_offset);
                atom = new UnknownAtom(type, size, stream);
            }

            // special case: if the atom is poorly encoded and has a 64-bit
            // size header but an actual size that fits on 32-bit, adjust the
            // object to reflect that.
            if (force_64) {
                atom.SetSize32(1);
                atom.SetSize64(size);
            }

            // adjust the available size
            bytes_available -= size;

            // skip to the end of the atom
            result = stream.Seek(start+size);
            if (result.Failed()) {
                atom = null;
                return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::CreateAtomFromStream
        +---------------------------------------------------------------------*/
        public virtual Ap4Result CreateAtomFromStream(ByteStream stream,
                                                      uint       type,
                                                      uint       size_32,
                                                      ulong      size_64,
                                                      out Atom   atom)
        {
            atom = null;

            bool atom_is_large = (size_32 == 1);
            // (force_64 is computed by AtomCreationContext)
            AtomCreationContext c = new AtomCreationContext(type, size_32, size_64, stream, this);
            AtomCreator creator;

            // create the atom
            if (GetContext() == AtomType.STSD) {
                // sample entry
                if (atom_is_large) return Ap4Result.ERROR_INVALID_FORMAT;
                if (s_registry.TryGetSampleEntry(type, out creator)) {
                    AtomCreationResult created = creator(c);
                    if (created.Result.Failed()) return created.Result;
                    atom = created.Atom;
                } else {
                    // try all the external type handlers
                    for (int i = 0; i < _typeHandlers.Count; i++) {
                        TypeHandler handler = _typeHandlers[i];
                        if (handler.CreateAtom(type, size_32, stream, GetContext(), out atom).Succeeded()) {
                            break;
                        }
                    }

                    // no custom handler, create a generic entry
                    if (atom == null) {
                        // PORT NOTE: `new AP4_UnknownSampleEntry(type, (AP4_UI32)size_64, stream)`
                        // upstream; that class belongs to the SampleEntry port,
                        // which installs this hook.
                        UnknownSampleEntryCreator unknown_sample_entry_creator = UnknownSampleEntryCreator;
                        if (unknown_sample_entry_creator != null) {
                            atom = unknown_sample_entry_creator(type, (uint)size_64, stream);
                        }
                    }
                }
            } else {
                // regular atom
                if (s_registry.TryGetAtom(type, out creator)) {
                    AtomCreationResult created = creator(c);
                    if (created.Result.Failed()) return created.Result;
                    atom = created.Atom;
                } else {
                    // try all the external type handlers
                    for (int i = 0; i < _typeHandlers.Count; i++) {
                        TypeHandler handler = _typeHandlers[i];
                        if (handler.CreateAtom(type, size_32, stream, GetContext(), out atom).Succeeded()) {
                            break;
                        }
                    }
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::CreateAtomsFromStream
        +---------------------------------------------------------------------*/
        public Ap4Result CreateAtomsFromStream(ByteStream stream,
                                               AtomParent atoms)
        {
            ulong stream_size     = 0;
            ulong stream_position = 0;
            ulong bytes_available = unchecked((ulong)(-1L));
            if (stream.GetSize(out stream_size).Succeeded() &&
                stream_size != 0 &&
                stream.Tell(out stream_position).Succeeded() &&
                stream_position <= stream_size) {
                bytes_available = stream_size-stream_position;
            }
            return CreateAtomsFromStream(stream, bytes_available, atoms);
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::CreateAtomsFromStream
        +---------------------------------------------------------------------*/
        public Ap4Result CreateAtomsFromStream(ByteStream stream,
                                               ulong      bytes_available,
                                               AtomParent atoms)
        {
            Ap4Result result;
            do {
                Atom atom = null;
                result = CreateAtomFromStream(stream, ref bytes_available, out atom);
                if (result.Succeeded() && atom != null) {
                    atoms.AddChild(atom);
                }
            } while (result.Succeeded());

            return Ap4Result.SUCCESS;
        }

        // context
        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::PushContext
        +---------------------------------------------------------------------*/
        public void PushContext(uint context)
        {
            _contextStack.Add(context);
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::PopContext
        +---------------------------------------------------------------------*/
        public void PopContext()
        {
            // PORT NOTE: AP4_Array::RemoveLast is a no-op on an empty array
            if (_contextStack.Count != 0) _contextStack.RemoveAt(_contextStack.Count-1);
        }

        /*----------------------------------------------------------------------
        |   AP4_AtomFactory::GetContext
        +---------------------------------------------------------------------*/
        public uint GetContext(uint depth = 0)
        {
            uint available = (uint)_contextStack.Count;
            if (depth >= available) return 0;
            return _contextStack[(int)(available-depth-1)];
        }

        /// <summary>
        /// PORT NOTE: not in upstream, where the switch passes `&amp;m_ContextStack`
        /// to AP4_SchmAtom::Create. Innermost context last. Do not modify.
        /// </summary>
        public List<uint> GetContextStack() { return _contextStack; }

        // members
        private List<uint>        _contextStack = new List<uint>();
        private List<TypeHandler> _typeHandlers = new List<TypeHandler>();
    }

    /*----------------------------------------------------------------------
    |   AP4_DefaultAtomFactory
    +---------------------------------------------------------------------*/
    public class DefaultAtomFactory : AtomFactory
    {
        // class members
        /// <summary>
        /// Hook set by the MetaData port (from AtomFactory.RegisterGroupMetaData):
        /// creates the AP4_MetaDataAtomTypeHandler that Initialize() registers
        /// upstream. When null no built-in type handler is registered.
        /// </summary>
#if !NET20
        public static Func<AtomFactory, AtomFactory.TypeHandler> MetaDataTypeHandlerFactory;
#else
        // PORT NOTE: Func<T, TResult> is in System.Core (.NET Framework 3.5);
        // .NET Framework 2.0 gets a delegate type of the same shape.
        public static TypeHandlerFactory MetaDataTypeHandlerFactory;

        /// <summary>The type of <see cref="MetaDataTypeHandlerFactory"/> (Func&lt;AtomFactory, AtomFactory.TypeHandler&gt; on the other targets).</summary>
        public delegate AtomFactory.TypeHandler TypeHandlerFactory(AtomFactory factory);
#endif

        /*----------------------------------------------------------------------
        |   AP4_DefaultAtomFactory::Instance
        +---------------------------------------------------------------------*/
        // PORT NOTE: created on first use rather than by a static initializer, so
        // that it is always constructed AFTER the static constructor of
        // AtomFactory has run the Register hooks (which set the hooks above).
        private static DefaultAtomFactory  s_instance;
        private static readonly object     s_instanceLock = new object();

        /// <summary>The shared factory (AP4_DefaultAtomFactory::Instance_). Like every AtomFactory it is not thread safe.</summary>
        public static DefaultAtomFactory Instance
        {
            get {
                DefaultAtomFactory instance = s_instance;
                if (instance == null) {
                    lock (s_instanceLock) {
                        instance = s_instance;
                        if (instance == null) {
                            instance = new DefaultAtomFactory();
                            s_instance = instance;
                        }
                    }
                }
                return instance;
            }
        }

        /// <summary>Upstream spelling of <see cref="Instance"/>.</summary>
        public static DefaultAtomFactory Instance_ { get { return Instance; } }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_DefaultAtomFactory::AP4_DefaultAtomFactory
        +---------------------------------------------------------------------*/
        public DefaultAtomFactory()
        {
            Initialize();
        }

        // this member is used to detect the situation where the
        // platform's code loader does not construct static C++ objects
        public bool _initialized;

        /*----------------------------------------------------------------------
        |   AP4_DefaultAtomFactory::Initialize
        +---------------------------------------------------------------------*/
        public Ap4Result Initialize()
        {
            // register built-in type handlers
            Ap4Result result = Ap4Result.SUCCESS;
#if !NET20
            Func<AtomFactory, AtomFactory.TypeHandler> metadata_type_handler_factory = MetaDataTypeHandlerFactory;
#else
            TypeHandlerFactory metadata_type_handler_factory = MetaDataTypeHandlerFactory;
#endif
            if (metadata_type_handler_factory != null) {
                result = AddTypeHandler(metadata_type_handler_factory(this));
            }
            if (result.Succeeded()) _initialized = true;
            return result;
        }
    }
}
