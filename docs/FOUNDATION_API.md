# Bento4Sharp foundation API

Reference for the files in `src/Bento4Sharp/Core/` that every other port
builds on. Read `docs/PORTING.md` first; this file lists what exists and the
decisions PORTING.md left open. Everything is in `namespace Bento4 { ... }`.
The source is C# 4.0 (see PORTING.md, "Language version"): there are no nullable
annotations, so a reference in the signatures below may be `null` wherever the
upstream pointer may be `NULL`.
`ImplicitUsings` is off: add `using System;` etc. yourself.

| File | Upstream | Contents |
|------|----------|----------|
| `Ap4Result.cs` | Ap4Results | `Ap4Result`, `Failed()/Succeeded()`, `Ap4.ResultText` |
| `Ap4.cs` | Ap4Version, Ap4Config | `Ap4.VERSION`, byte order constants |
| `Debug.cs` | Ap4Debug | `Ap4.Debug`, `Ap4.ASSERT` |
| `Interfaces.cs` | Ap4Interfaces | `Ap4Exception` |
| `Utils.cs` | Ap4Utils | `Ap4Utils`, `GlobalOptions`, `BitWriter`, `BitReader` |
| `DataBuffer.cs` | Ap4DataBuffer | `DataBuffer` |
| `ByteStream.cs` | Ap4ByteStream | `ByteStream`, `SubStream`, `DupStream`, `MemoryByteStream`, `BufferedInputStream` |
| `FileByteStream.cs` | Ap4FileByteStream, StdC impl | `FileByteStream`, `StdcFileByteStream` |
| `List.cs` | Ap4List (visitors only) | `IListOperator<T>`, `IListFinder<T>`, `List<T>` extensions |
| `Atom.cs` | Ap4Atom | `AtomType`, `AtomInspector`, `PrintInspector`, `JsonInspector`, `AtomParent`, `Atom`, `IUuidAtom`, `UnknownAtom`, `NullTerminatedStringAtom`, `AtomListInspector`, `AtomListWriter`, `AtomFinder`, `AtomSizeAdder` |
| `ContainerAtom.cs` | Ap4ContainerAtom | `ContainerAtom` |
| `AtomFactory.cs` | Ap4AtomFactory | `AtomFactory`, `DefaultAtomFactory`, `AtomRegistry`, `AtomCreationContext`, `AtomCreationResult` |

Parameter names are the upstream snake_case names. `PORT NOTE:` comments in
the sources mark every deviation.

---------------------------------------------------------------------------

## 1. Things you must know (non-obvious decisions)

1. **`Atom` derives from `AtomParent`** (C# has no multiple inheritance and
   upstream `AP4_ContainerAtom` is both). Hierarchy:
   `AtomParent` <- `Atom` (abstract) <- `ContainerAtom` <- your containers.
   - `AP4_AtomParent*` -> `AtomParent` everywhere; a `ContainerAtom`, a
     `new AtomParent()` and an `AP4_File`-like class (`class File : AtomParent`) all fit.
   - `AP4_DYNAMIC_CAST(AP4_ContainerAtom, x)` -> `x as ContainerAtom`; `AP4_DYNAMIC_CAST(AP4_Atom, parent)` -> `parent as Atom`. Both behave as upstream.
   - **Trap:** `atom as AtomParent` succeeds for *every* atom. Where upstream
     does `AP4_DYNAMIC_CAST(AP4_AtomParent, some_atom)`, write `some_atom as ContainerAtom`.
   - `m_Children` -> `_children` (`protected List<Atom>`, never null), inherited by every atom; leaf atoms just ignore it.
2. **`Atom.GetType()` returns the atom type (`uint`)** and hides `object.GetType()`. Use `is`/`as`/`typeof` for .NET type tests.
3. **Strings are `string`, sizes are bytes.** Never use `string.Length` for an
   atom size: use `Ap4Utils.StringLength(s)` (= `strlen`). See section 3.
4. **`AP4_Debug` prints to stdout in all builds** (`Ap4.Debug(string)`), as upstream. `Ap4.ASSERT` is compiled out unless `DEBUG` is defined.
5. **`JsonInspector` must be disposed**: the closing `\n]\n` that the C++ destructor writes is written by `Dispose()`.
6. **`FileByteStream.Create` returns a `FileByteStream`** (IDisposable) in an `out ByteStream`. Dispose it to close the file: `if (stream is IDisposable) ((IDisposable)stream).Dispose();`.
7. `GetData()/UseData()` of `DataBuffer`/`MemoryByteStream` return the **backing array**, usually longer than `GetDataSize()`. Always pass the size along; use `ToArray()` for an exact copy.
8. `UuidAtom` (not in the foundation) **must implement `IUuidAtom`** (`byte[] GetUuid()`), or `AtomParent.GetChild(byte[] uuid, ...)` will not find it.
9. Every `void* buffer, AP4_Size size` API takes `byte[] buffer, uint size` and has a twin `byte[] buffer, int offset, uint size` for upstream call sites that pass `ptr + n`.
10. `AP4_List<T>` -> `List<T>`; the `Apply(Operator)` / `Find(Finder)` idiom is available as extension methods (section 9).

---------------------------------------------------------------------------

## 2. Results, constants, debug

```csharp
public enum Ap4Result : int { SUCCESS = 0, FAILURE = -1, ERROR_OUT_OF_MEMORY = -2, ERROR_INVALID_PARAMETERS = -3,
    ERROR_NO_SUCH_FILE = -4, ERROR_PERMISSION_DENIED = -5, ERROR_CANNOT_OPEN_FILE = -6, ERROR_EOS = -7,
    ERROR_WRITE_FAILED = -8, ERROR_READ_FAILED = -9, ERROR_INVALID_FORMAT = -10, ERROR_NO_SUCH_ITEM = -11,
    ERROR_OUT_OF_RANGE = -12, ERROR_INTERNAL = -13, ERROR_INVALID_STATE = -14, ERROR_LIST_EMPTY = -15,
    ERROR_LIST_OPERATION_ABORTED = -16, ERROR_INVALID_RTP_CONSTRUCTOR_TYPE = -17, ERROR_NOT_SUPPORTED = -18,
    ERROR_INVALID_TRACK_TYPE = -19, ERROR_INVALID_RTP_PACKET_EXTRA_DATA = -20, ERROR_BUFFER_TOO_SMALL = -21,
    ERROR_NOT_ENOUGH_DATA = -22 }

bool result.Failed();      // AP4_FAILED
bool result.Succeeded();   // AP4_SUCCEEDED
// AP4_CHECK(x):  { Ap4Result _result = x; if (_result.Failed()) return _result; }

public static partial class Ap4 {
    static string ResultText(Ap4Result result);   // "AP4_ERROR_EOS", ... "UNKNOWN"
    static string ResultText(int result);
    const uint   VERSION = 0x01060000;  const string VERSION_STRING = "1.6.0.0";
    const int    PLATFORM_BYTE_ORDER_BIG_ENDIAN = 0, PLATFORM_BYTE_ORDER_LITTLE_ENDIAN = 1;
    static readonly int PLATFORM_BYTE_ORDER;
    const uint   ATOM_HEADER_SIZE = 8, ATOM_HEADER_SIZE_64 = 16, FULL_ATOM_HEADER_SIZE = 12,
                 FULL_ATOM_HEADER_SIZE_64 = 20, ATOM_MAX_NAME_SIZE = 256, ATOM_MAX_URI_SIZE = 512;
    static uint  ATOM_TYPE(char c1, char c2, char c3, char c4);   // not usable in a const initializer
    static void  Debug(string message);     // AP4_Debug: caller formats; printed on stdout, truncated to 1023 chars
    static void  ASSERT(bool condition);    // [Conditional("DEBUG")]
}

public class Ap4Exception : System.Exception { Ap4Exception(Ap4Result error); public Ap4Result _error; }  // AP4_Exception
```

Add your own `AP4_XXX` constants with `public static partial class Ap4 { public const ... }`.

---------------------------------------------------------------------------

## 3. Strings (encoding policy)

Upstream strings are raw bytes. The port decodes/encodes **lossless UTF-8**:

- valid UTF-8 <-> the matching chars (surrogate pairs above U+FFFF);
- a byte that is not part of a valid UTF-8 sequence (stray 0x80-0xFF, overlong,
  encoded surrogate, truncated sequence) decodes to the lone low surrogate
  `U+DC00 + byte` (U+DC80..U+DCFF), and such a char encodes back to that single
  byte. Any other unpaired surrogate (cannot come from decoding) encodes as U+FFFD.

So `bytes -> string -> bytes` is the identity for **any** byte sequence (files
with Latin-1 or garbage strings round-trip exactly, and the inspectors print
the original bytes), and `string -> bytes -> string` is the identity for any
well-formed string. Do not call `System.Text.Encoding` yourself.

```csharp
public static class Ap4Utils {
    static string StringFromBytes(byte[] bytes, int offset, uint size);        // AP4_String(chars, size): keeps embedded 0 bytes
    static string StringFromBytes(byte[] bytes);
    static string StringFromCString(byte[] bytes, int offset, uint max_size);  // AP4_String(chars): stops at first 0 byte (or max_size)
    static byte[] StringToBytes(string str);                                  // all chars, no terminator
    static byte[] StringToCStringBytes(string str);                           // stops before first U+0000, no terminator
    static uint   StringLength(string str);                                   // strlen / AP4_String::GetLength(): BYTE count up to first U+0000
    static string FormatFloat(double value);                                   // printf("%f"): "1.500000", "inf", "-inf", "nan"
}
```

Typical translations:

| C++ | C# |
|-----|----|
| `m_Size32 += m_Name.GetLength()+1` | `_size32 += Ap4Utils.StringLength(_name)+1` |
| `stream.Write(m_Name.GetChars(), m_Name.GetLength()+1)` | `stream.WriteString(_name); stream.WriteUI08(0);` |
| `char name[N]; stream.Read(name, n); name[n]=0; m_Name = name;` | `byte[] name = new byte[n]; stream.Read(name, n); _name = Ap4Utils.StringFromCString(name, 0, n);` |
| `stream.ReadString(buf, sizeof(buf))` | `stream.ReadString(out string s, N)` |
| `AP4_FormatString(s, n, "%d", x)` | `x.ToString(CultureInfo.InvariantCulture)` |
| `"%02x"`, `"%08x"`, `"%llx"` | `x.ToString("x2", CultureInfo.InvariantCulture)`, `"x8"`, `"x"` |
| `"%f"` | `Ap4Utils.FormatFloat(x)` |
| `char s[5]; AP4_FormatFourChars(s, v)` | `string s = Ap4Utils.FormatFourChars(v)` |

`%d` of an unsigned value prints it as signed: cast (`((int)u).ToString(...)`,
`((long)u64).ToString(...)` for `%lld`) to get identical text.

---------------------------------------------------------------------------

## 4. Ap4Utils, GlobalOptions, BitWriter, BitReader

```csharp
public static class Ap4Utils {
    // big-endian conversions; `offset` defaults to 0 on the readers
    static double BytesToDoubleBE(byte[] bytes, int offset = 0);
    static ulong  BytesToUInt64BE(byte[] bytes, int offset = 0);
    static uint   BytesToUInt32BE(byte[] bytes, int offset = 0);
    static int    BytesToInt32BE (byte[] bytes, int offset = 0);
    static uint   BytesToUInt24BE(byte[] bytes, int offset = 0);
    static ushort BytesToUInt16BE(byte[] bytes, int offset = 0);
    static short  BytesToInt16BE (byte[] bytes, int offset = 0);
    static void   BytesFromDoubleBE(byte[] bytes, double value);   static void BytesFromDoubleBE(byte[] bytes, int offset, double value);
    static void   BytesFromUInt64BE(byte[] bytes, ulong value);    static void BytesFromUInt64BE(byte[] bytes, int offset, ulong value);
    static void   BytesFromUInt32BE(byte[] bytes, uint value);     static void BytesFromUInt32BE(byte[] bytes, int offset, uint value);
    static void   BytesFromUInt24BE(byte[] bytes, uint value);     static void BytesFromUInt24BE(byte[] bytes, int offset, uint value);
    static void   BytesFromUInt16BE(byte[] bytes, ushort value);   static void BytesFromUInt16BE(byte[] bytes, int offset, ushort value);
    static void   ByteSwap16(byte[] bytes, uint count);            static void ByteSwap16(byte[] bytes, int offset, uint count);

    static uint   DurationMsFromUnits(ulong units, uint units_per_second);
    static ulong  ConvertTime(ulong time_value, uint from_time_scale, uint to_time_scale);
    static Ap4Result System_GenerateRandomBytes(byte[] buffer, uint buffer_size);   // AP4_System_GenerateRandomBytes

    static byte   HexNibble(char c);                 // 0 for a non-hex char
    static char   NibbleHex(uint nibble);            // UPPER case; ' ' if > 15
    static string FormatFourChars(uint value);          // C string semantics: stops at the first 0 byte (may be < 4 chars)
    static string FormatFourCharsPrintable(uint value); // always 4 chars, bytes outside 0x20..0x7E -> '.'
    static Ap4Result ParseHex(string hex, byte[] bytes, uint count);                 // INVALID_PARAMETERS if hex is shorter than 2*count
    static Ap4Result ParseHex(string hex, int hex_offset, byte[] bytes, uint count);
    static Ap4Result FormatHex(byte[] data, uint data_size, out string hex);         // UPPER case, 2*data_size chars
    static Ap4Result FormatHex(byte[] data, int offset, uint data_size, out string hex);
    static Ap4Result SplitArgs(string arg, out string arg0, out string arg1);        // split at first ':'; FAILURE (arg0 = arg, arg1 = "") if none
    static Ap4Result SplitArgs(string arg, out string arg0, out string arg1, out string arg2);
    static uint   ParseIntegerU(string str);        // 0 if null or any non-digit
    // + the string helpers of section 3
}

public static class GlobalOptions {                  // AP4_GlobalOptions
    static bool GetBool(string name);  static void SetBool(string name, bool value);
    static string GetString(string name);  static void SetString(string name, string value);
}

public class BitWriter {                             // AP4_BitWriter
    BitWriter(uint size);                            // size in bytes, zero filled
    void Write(uint bits, uint bit_count);           // silently ignored if it does not fit
    uint GetBitCount();
    byte[] GetData();                                // Length == size
}

public class BitReader {                             // AP4_BitReader (copies the data)
    BitReader(byte[] data, uint data_size);
    BitReader(byte[] data, int offset, uint data_size);
    Ap4Result Reset();
    int  ReadBit();   uint ReadBits(uint bit_count);     // reads past the end return zeros
    int  PeekBit();   uint PeekBits(uint bit_count);
    void SkipBit();   void SkipBits(uint bit_count);
    uint GetBitsPosition();   uint GetBitsRead();
    // SkipBytes: declared but not implemented upstream; not ported
}
```

---------------------------------------------------------------------------

## 5. DataBuffer

```csharp
public class DataBuffer {
    DataBuffer();
    DataBuffer(uint buffer_size);                    // buffer allocated, data size 0
    DataBuffer(byte[] data, uint data_size);        // copies
    DataBuffer(byte[] data, int offset, uint data_size);
    DataBuffer(DataBuffer other);                    // copy constructor (copies the data)
    Ap4Result SetBuffer(byte[] buffer, uint buffer_size);   // external buffer: no more resizing
    Ap4Result SetBufferSize(uint buffer_size);
    uint      GetBufferSize();
    byte[]    GetData();   byte[] UseData();         // backing array (never null); valid bytes = GetDataSize()
    uint      GetDataSize();
    Ap4Result SetDataSize(uint size);                // grows the buffer if needed (new bytes are zero)
    Ap4Result SetData(byte[] data, uint size);      Ap4Result SetData(byte[] data, int offset, uint size);
    Ap4Result AppendData(byte[] data, uint data_size);  Ap4Result AppendData(byte[] data, int offset, uint data_size);
    Ap4Result Reserve(uint size);
    byte[]    ToArray();                             // PORT addition: exact-size copy
    protected bool _bufferIsLocal; protected byte[] _buffer; protected uint _bufferSize; protected uint _dataSize;
    protected Ap4Result ReallocateBuffer(uint size);
}
```

`uint8* p = buf.UseData() + n` has no equivalent: keep `byte[] data = buf.UseData()` and an `int` index.

---------------------------------------------------------------------------

## 6. ByteStream and friends

```csharp
public abstract class ByteStream {
    // to implement in a subclass:
    abstract Ap4Result ReadPartial (byte[] buffer, int offset, uint bytes_to_read,  out uint bytes_read);
    abstract Ap4Result WritePartial(byte[] buffer, int offset, uint bytes_to_write, out uint bytes_written);
    abstract Ap4Result Seek(ulong position);
    abstract Ap4Result Tell(out ulong position);
    abstract Ap4Result GetSize(out ulong size);
    virtual  Ap4Result CopyTo(ByteStream stream, ulong size);     // copies `size` bytes from the current position
    virtual  Ap4Result Flush();

    Ap4Result ReadPartial (byte[] buffer, uint bytes_to_read,  out uint bytes_read);     // offset 0
    Ap4Result WritePartial(byte[] buffer, uint bytes_to_write, out uint bytes_written);
    Ap4Result Read (byte[] buffer, uint bytes_to_read);    Ap4Result Read (byte[] buffer, int offset, uint bytes_to_read);   // all or error
    Ap4Result Write(byte[] buffer, uint bytes_to_write);   Ap4Result Write(byte[] buffer, int offset, uint bytes_to_write);

    Ap4Result ReadUI08(out byte value);    Ap4Result ReadUI16(out ushort value);  Ap4Result ReadUI24(out uint value);
    Ap4Result ReadUI32(out uint value);    Ap4Result ReadUI64(out ulong value);   Ap4Result ReadDouble(out double value);
        // big-endian; on failure the out value is 0 and the error is returned
    Ap4Result ReadString(out string value, uint size);
        // AP4_ByteStream::ReadString(char* buffer, AP4_Size size): reads byte by byte until a 0 byte has been
        // consumed or size-1 bytes were read; size == 0 -> ERROR_INVALID_PARAMETERS
    Ap4Result ReadNullTerminatedString(out string value);   // "" on failure

    Ap4Result WriteUI08(byte value);  Ap4Result WriteUI16(ushort value);  Ap4Result WriteUI24(uint value);
    Ap4Result WriteUI32(uint value);  Ap4Result WriteUI64(ulong value);   Ap4Result WriteDouble(double value);
    Ap4Result WriteString(string buffer);   // bytes of the C string, NO terminator; null/"" writes nothing
}

public class SubStream : ByteStream           { SubStream(ByteStream container, ulong position, ulong size); }
public class DupStream : ByteStream           { DupStream(ByteStream original_stream); }
public class BufferedInputStream : ByteStream { BufferedInputStream(ByteStream source, uint buffer_size = 4096, uint seek_as_read_threshold = 1024*128);
                                                protected Ap4Result Refill(); }   // WritePartial -> ERROR_NOT_SUPPORTED

public class MemoryByteStream : ByteStream {
    MemoryByteStream(uint size = 0);                        // `size` zero bytes; grows on write
    MemoryByteStream(byte[] buffer, uint size);             // copies
    MemoryByteStream(byte[] buffer, int offset, uint size); // copies
    MemoryByteStream(DataBuffer data_buffer);               // reads/writes the given buffer (both upstream DataBuffer ctors)
    byte[] GetData();  byte[] UseData();                    // backing array, valid bytes = GetDataSize()
    uint   GetDataSize();
    byte[] ToArray();                                       // PORT addition: exact-size copy
}

public class FileByteStream : ByteStream, IDisposable {
    enum Mode { STREAM_MODE_READ = 0, STREAM_MODE_WRITE = 1, STREAM_MODE_READ_WRITE = 2 }   // FileByteStream.Mode.STREAM_MODE_READ
    static Ap4Result Create(string name, Mode mode, out ByteStream stream);
        // name: path, or "-stdin", "-stdout", "-stderr" (also "-stdin#", "-stdout#")
        // errors: ERROR_INVALID_PARAMETERS (null name), ERROR_NO_SUCH_FILE, ERROR_PERMISSION_DENIED, ERROR_CANNOT_OPEN_FILE
        // WRITE = create/truncate ("wb+"), READ_WRITE = existing file ("r+b")
    FileByteStream(ByteStream @delegate);
    FileByteStream(string name, Mode mode);    // deprecated upstream; throws Ap4Exception
    void Dispose();                            // closes the file (std streams are only flushed)
    protected ByteStream _delegate;
}
public class StdcFileByteStream : ByteStream, IDisposable { ... }   // implementation detail (over System.IO.Stream)
```

No `AddReference()/Release()`: drop those calls. Streams are not thread safe.
Typical use:

```csharp
ByteStream input;
Ap4Result result = FileByteStream.Create(path, FileByteStream.Mode.STREAM_MODE_READ, out input);
if (result.Failed()) { ... }
try { ... } finally { if (input is IDisposable) ((IDisposable)input).Dispose(); }
```

---------------------------------------------------------------------------

## 7. Atoms

### AtomType

`public static partial class AtomType` holds every `AP4_ATOM_TYPE_*` of
Ap4Atom.h as `public const uint` (`AtomType.MOOV`, `AtomType.URL`,
`AtomType.RTP_`, `AtomType.SDP_`, `AtomType.AC_3`, `AtomType._8ID_`,
`AtomType._8BDL`, ...). Types declared in *other* upstream headers are added
by the file that ports that header:

```csharp
public static partial class AtomType { public const uint XXXX = 0x78787878; /* 'xxxx' */ }
```

### Atom

```csharp
public abstract class Atom : AtomParent {
    static uint TypeFromString(string four_cc);
    static Ap4Result ReadFullHeader(ByteStream stream, out byte version, out uint flags);   // reads the 4 version+flags bytes

    protected Atom(uint type, uint size = Ap4.ATOM_HEADER_SIZE);                 // simple atom, 32-bit size
    protected Atom(uint type, ulong size, bool force_64 = false);                // simple atom, 64-bit size
    protected Atom(uint type, uint size, byte version, uint flags);              // full atom, 32-bit size
    protected Atom(uint type, ulong size, bool force_64, byte version, uint flags);   // full atom, 64-bit size

    uint GetFlags();     void SetFlags(uint flags);
    byte GetVersion();   void SetVersion(byte version);
    new uint GetType();  void SetType(uint type);             // hides object.GetType()
    virtual uint GetHeaderSize();                             // 8 / 12, +8 when the size is 64-bit
    ulong GetSize();     void SetSize(ulong size, bool force_64 = false);
    uint  GetSize32();   void SetSize32(uint size);
    ulong GetSize64();   void SetSize64(ulong size);

    virtual  Ap4Result Write(ByteStream stream);              // WriteHeader + WriteFields
    virtual  Ap4Result WriteHeader(ByteStream stream);
    abstract Ap4Result WriteFields(ByteStream stream);
    virtual  Ap4Result Inspect(AtomInspector inspector);      // InspectHeader + InspectFields + inspector.EndAtom()
    virtual  Ap4Result InspectHeader(AtomInspector inspector);
    virtual  Ap4Result InspectFields(AtomInspector inspector);   // default: nothing

    virtual Ap4Result   SetParent(AtomParent parent);
    virtual AtomParent GetParent();
    virtual Ap4Result   Detach();
    virtual Atom       Clone();

    protected uint _type; protected uint _size32; protected ulong _size64; protected bool _isFull;
    protected byte _version; protected uint _flags; protected AtomParent _parent;
}
```

Constructor overloads resolve like the C++ ones as long as the size argument
has the upstream type: `uint` picks the 32-bit constructors, `ulong` the
64-bit ones. `AP4_Atom(type, AP4_ATOM_HEADER_SIZE+x, false)` (64-bit ctor with
a 32-bit value) works unchanged.

**Clone.** `Atom.Clone()` is the generic implementation: refuses atoms larger
than 1 MiB (returns null), serializes the atom with `Write` to a
`MemoryByteStream`, and parses it back with a fresh `new DefaultAtomFactory()`
(so the clone is built by whatever is registered for that type, with an
*empty* context stack). Override it only where upstream does
(`UnknownAtom`, `ContainerAtom`, ...). A null clone is skipped by
`ContainerAtom.Clone()` and `AtomParent.CopyChildren()`.

### AtomParent

```csharp
public class AtomParent {
    List<Atom> GetChildren();
    Ap4Result CopyChildren(AtomParent destination);                 // clones every child into destination
    virtual Ap4Result AddChild(Atom child, int position = -1);      // -1: append; ERROR_INVALID_PARAMETERS if child has a parent; ERROR_OUT_OF_RANGE
    virtual Ap4Result RemoveChild(Atom child);
    virtual Ap4Result DeleteChild(uint type, uint index = 0);
    virtual Atom GetChild(uint type, uint index = 0);
    virtual Atom GetChild(byte[] uuid, uint index = 0);            // 'uuid' children implementing IUuidAtom
    virtual Atom FindChild(string path, bool auto_create = false, bool auto_create_full = false);   // "moov/trak[1]/mdia"
    virtual void OnChildChanged(Atom child);  virtual void OnChildAdded(Atom child);  virtual void OnChildRemoved(Atom child);
    protected List<Atom> _children;
}
public interface IUuidAtom { byte[] GetUuid(); }
```

Iterating children: `foreach (Atom child in _children)` or an indexed loop
(replaces `FirstItem()/GetNext()`).

### ContainerAtom

```csharp
public class ContainerAtom : Atom {
    static ContainerAtom Create(uint type, ulong size, bool is_full, bool force_64, ByteStream stream, AtomFactory atom_factory);
    ContainerAtom(uint type);                                              // empty, size 8
    ContainerAtom(uint type, byte version, uint flags);                    // empty full container, size 12
    ContainerAtom(uint type, ulong size, bool force_64);                   // no children read
    ContainerAtom(uint type, ulong size, bool force_64, byte version, uint flags);
    protected ContainerAtom(uint type, ulong size, bool force_64, ByteStream stream, AtomFactory atom_factory);                           // reads children
    protected ContainerAtom(uint type, ulong size, bool force_64, byte version, uint flags, ByteStream stream, AtomFactory atom_factory); // reads children
    protected void ReadChildren(AtomFactory atom_factory, ByteStream stream, ulong size);   // pushes _type as factory context while reading
    override Ap4Result InspectFields(AtomInspector inspector);    // = InspectChildren
    virtual  Ap4Result InspectChildren(AtomInspector inspector);
    override Ap4Result WriteFields(ByteStream stream);            // writes all children (zero-pads a short child up to 1024 bytes)
    override Atom Clone();
    override void OnChildChanged(Atom child);  override void OnChildAdded(Atom child);  override void OnChildRemoved(Atom child);   // keep sizes up to date
}
```

`ReadChildren` adds to `_children` directly (no size update): sizes come from
the file. `AddChild/RemoveChild` update the sizes of all ancestors.

### UnknownAtom, NullTerminatedStringAtom

```csharp
public class UnknownAtom : Atom {
    UnknownAtom(uint type, ulong size, ByteStream stream);     // stream positioned after the header; payload <= 4096-8 bytes (and not 'mdat') is
                                                               // loaded, otherwise the stream is referenced and copied on Write
    UnknownAtom(uint type, byte[] payload, uint payload_size);
    UnknownAtom(UnknownAtom other);                            // copy constructor
}
public class NullTerminatedStringAtom : Atom {
    NullTerminatedStringAtom(uint type, ulong size, ByteStream stream);
    NullTerminatedStringAtom(uint type, string value);
    string GetValue();
}
```

Because an `UnknownAtom` may reference its source stream, keep the input
stream open until the atoms have been written.

---------------------------------------------------------------------------

## 8. Inspectors

```csharp
public class AtomInspector {
    enum FormatHint { HINT_NONE = 0, HINT_HEX = 1, HINT_BOOLEAN = 2 }      // AtomInspector.FormatHint.HINT_HEX
    void SetVerbosity(uint verbosity);  uint GetVerbosity();
    virtual void StartAtom(string name, byte version, uint flags, uint header_size, ulong size);
    virtual void EndAtom();
    virtual void StartDescriptor(string name, uint header_size, ulong size);
    virtual void EndDescriptor();
    virtual void StartArray(string name, uint element_count = 0);
    virtual void EndArray();
    virtual void StartObject(string name, uint field_count = 0, bool compact = false);
    virtual void EndObject();
    virtual void AddField (string name, ulong value,  FormatHint hint = FormatHint.HINT_NONE);
    virtual void AddFieldF(string name, float value,  FormatHint hint = FormatHint.HINT_NONE);
    virtual void AddField (string name, string value, FormatHint hint = FormatHint.HINT_NONE);
    virtual void AddField (string name, byte[] bytes, int offset, uint byte_count, FormatHint hint = FormatHint.HINT_NONE);
    // non-virtual conveniences:
    void AddField(string name, byte[] bytes, uint byte_count, FormatHint hint = FormatHint.HINT_NONE);   // upstream signature
    void AddField(string name, long value, FormatHint hint = FormatHint.HINT_NONE);                      // signed -> (ulong) like C++
    protected uint _verbosity;
}
public class PrintInspector : AtomInspector { PrintInspector(ByteStream stream, uint indent = 0); }       // indent is unused, as upstream
public class JsonInspector  : AtomInspector, IDisposable { JsonInspector(ByteStream stream); void Dispose(); }
```

How they are called (same as upstream):

```csharp
PrintInspector inspector = new PrintInspector(output);
inspector.SetVerbosity(verbosity);
atom.Inspect(inspector);                     // per top-level atom

using (JsonInspector json = new JsonInspector(output)) {   // ctor writes "[\n", Dispose writes "\n]\n"
    foreach (Atom atom in top.GetChildren()) atom.Inspect(json);
}
```

In `InspectFields`, pass exactly the upstream field names (`null` where
upstream passes `NULL`), in the same order:

```csharp
inspector.AddField("track_id", _trackId);                                  // any integer type (signed goes through the long overload)
inspector.AddField("flags", _flags, AtomInspector.FormatHint.HINT_HEX);    // text: "%llx" / json: decimal
inspector.AddFieldF("volume", (float)_volume / 256.0f);                    // "%f"
inspector.AddField("name", _name);                                         // string
inspector.AddField("data", _data.GetData(), _data.GetDataSize());          // bytes -> [00 01 ab]
inspector.AddField("kid", buffer, 4, 16);                                  // bytes at an offset (upstream: buffer+4, 16)
inspector.StartArray("entries", count);
for (...) { inspector.StartObject(null, 2, true); inspector.AddField("a", x); inspector.AddField("b", y); inspector.EndObject(); }
inspector.EndArray();
```

Overload traps: `bool` and `double` have no overload (write `b ? 1 : 0`,
`(float)d`); an enum value needs a cast to an integer type; always pass hints
as `AtomInspector.FormatHint.X`, never as a literal `0`. The output
of both inspectors is byte-for-byte that of upstream (indentation of 2 per
level, `(%8d) ` array indexes, compact objects on one line, `%f` truncated to
31 chars, JSON escaping of `"`, `\` and control chars as `\u00XX` with the
upstream UTF-8 quirks).

---------------------------------------------------------------------------

## 9. List helpers (AP4_List Operator / Finder idiom)

```csharp
public interface IListOperator<T> { Ap4Result Action(T data); }
public interface IListFinder<T>   { Ap4Result Test(T data); }     // SUCCESS = match

// extension methods on List<T>
Ap4Result list.Apply(op);                 // results of Action IGNORED, always SUCCESS (as upstream)
Ap4Result list.ApplyUntilFailure(op);     // first non-SUCCESS result, else SUCCESS
Ap4Result list.ApplyUntilSuccess(op);     // SUCCESS at first success, else FAILURE
Ap4Result list.ReverseApply(op);          // ERROR_LIST_OPERATION_ABORTED at first failure
Ap4Result list.Find(finder, out T data);         // ERROR_NO_SUCH_ITEM + null if not found (T : class)
Ap4Result list.ReverseFind(finder, out T data);

public class AtomListInspector : IListOperator<Atom> { AtomListInspector(AtomInspector inspector); }
public class AtomListWriter    : IListOperator<Atom> { AtomListWriter(ByteStream stream); }   // pads short atoms with zeros (max 1024), prints warnings
public class AtomFinder        : IListFinder<Atom>   { AtomFinder(uint type, uint index = 0); }   // stateful: create a new one per Find
public class AtomSizeAdder     : IListOperator<Atom> { AtomSizeAdder(ulong size); ulong GetSize(); }
```

```csharp
_children.Apply(new AtomListWriter(stream));                       // m_Children.Apply(AP4_AtomListWriter(stream))
AtomSizeAdder adder = new AtomSizeAdder(size);                     // AP4_UI64 size = ...; m_Children.Apply(AP4_AtomSizeAdder(size));
_children.Apply(adder); size = adder.GetSize();
```

Other mappings: `list.ItemCount()` -> `list.Count`; `list.Add(x)`;
`list.Remove(x)`; `list.DeleteReferences()` -> `list.Clear()`;
`AP4_Array::Append` -> `Add`; `RemoveLast()` -> `RemoveAt(Count-1)` (guard for empty);
`SetItemCount(n)` -> resize the `List<T>` by hand.

---------------------------------------------------------------------------

## 10. AtomFactory and atom registration

```csharp
public partial class AtomFactory {
    public abstract class TypeHandler {
        public abstract Ap4Result CreateAtom(uint type, uint size, ByteStream stream, uint context, out Atom atom);
    }
    AtomFactory();
    Ap4Result AddTypeHandler(TypeHandler handler);
    Ap4Result RemoveTypeHandler(TypeHandler handler);
    Ap4Result CreateAtomFromStream(ByteStream stream, out Atom atom);
    Ap4Result CreateAtomFromStream(ByteStream stream, ref ulong bytes_available, out Atom atom);
    virtual Ap4Result CreateAtomFromStream(ByteStream stream, uint type, uint size_32, ulong size_64, out Atom atom);   // the dispatch
    Ap4Result CreateAtomsFromStream(ByteStream stream, AtomParent atoms);
    Ap4Result CreateAtomsFromStream(ByteStream stream, ulong bytes_available, AtomParent atoms);
    void PushContext(uint context);  void PopContext();  uint GetContext(uint depth = 0);   // 0 when the stack is not that deep
    List<uint> GetContextStack();                       // PORT addition (upstream passes &m_ContextStack to AP4_SchmAtom::Create)

    static AtomRegistry Registry { get; }               // the shared tables
    static UnknownSampleEntryCreator UnknownSampleEntryCreator;   // hook, see below
}
public class DefaultAtomFactory : AtomFactory {
    static DefaultAtomFactory Instance { get; }          // AP4_DefaultAtomFactory::Instance_ (alias: Instance_); created on first use
    static Func<AtomFactory, AtomFactory.TypeHandler> MetaDataTypeHandlerFactory;   // hook, see below
                                                         // (net20: the delegate type DefaultAtomFactory.TypeHandlerFactory, same shape; Func<,> is in System.Core)
    DefaultAtomFactory();                                // calls Initialize()
    bool _initialized;
    Ap4Result Initialize();
}

public struct AtomCreationContext {
    readonly uint Type;            // type
    readonly uint Size32;          // size_32  (1 when the size is 64-bit)
    readonly ulong Size64;         // size_64  (the real size, header included)
    readonly bool IsLarge;         // atom_is_large = (size_32 == 1)
    readonly bool Force64;         // force_64
    readonly ByteStream Stream;    // positioned right after the atom header
    readonly AtomFactory Factory;  // *this
    uint GetContext(uint depth = 0);          // GetContext()
    List<uint> ContextStack { get; }          // &m_ContextStack
    AtomCreationResult Ok(Atom atom);        // `atom = ...; break;`   (null -> UnknownAtom)
    AtomCreationResult Fail(Ap4Result result);// `return result;`
}
public struct AtomCreationResult { readonly Ap4Result Result; readonly Atom Atom; }
public delegate AtomCreationResult AtomCreator(AtomCreationContext c);
public delegate Atom UnknownSampleEntryCreator(uint type, uint size, ByteStream stream);

public sealed class AtomRegistry {
    static readonly AtomCreationResult InvalidFormat;   // `return AP4_ERROR_INVALID_FORMAT;`
    static readonly AtomCreationResult Fallback;        // `break;` with atom == NULL -> UnknownAtom
    void Atom(uint type, AtomCreator creator);          // regular table; throws InvalidOperationException on a duplicate type
    void Atoms(uint[] types, AtomCreator creator);      // `case A: case B:` sharing one body (c.Type is the actual type)
    void SampleEntry(uint type, AtomCreator creator);   // table used when GetContext() == 'stsd'
    void SampleEntries(uint[] types, AtomCreator creator);
    bool TryGetAtom(uint type, out AtomCreator creator);
    bool TryGetSampleEntry(uint type, out AtomCreator creator);
}
```

### Dispatch semantics (identical to upstream)

`CreateAtomFromStream(stream, ref bytes_available, out atom)` reads the
header, validates the size and calls the virtual 5-argument overload, which:

- **context is `stsd`** (`GetContext() == AtomType.STSD`): returns
  `ERROR_INVALID_FORMAT` if `size_32 == 1`; else uses the *sample entry* table;
  if the type is not in it, tries the type handlers in order (first one that
  returns SUCCESS wins), then `AtomFactory.UnknownSampleEntryCreator` if set;
- **otherwise**: uses the *atom* table; if the type is not in it, tries the
  type handlers. A type that IS in the table never reaches the handlers, even
  when its creator returns `Ok(null)`/`Fallback` (as upstream).

Back in the caller: a failed result is returned as is (the stream is left
where the creator left it, as upstream); a null atom becomes an
`UnknownAtom` (the stream is first repositioned to the start of the payload);
`force_64` fixes up the size fields; finally the stream is seeked to the end
of the atom, so a creator may leave the stream anywhere.

The same type may be in both tables (`rtp ` is). Registering a type twice in
one table throws at startup (it would be a `TypeInitializationException` on
`AtomFactory`): each upstream `case` belongs to exactly one group.

### Where registrations go

`AtomFactory` is `partial` and its static constructor calls, in this order:
`RegisterCore` (foundation), `RegisterGroupA`, `RegisterGroupB`,
`RegisterGroupC1`, `RegisterGroupC2`, `RegisterGroupD`, `RegisterGroupE`,
`RegisterGroupF`, `RegisterGroupG`, `RegisterGroupH`, `RegisterGroupMetaData`.
Each is declared `static partial void RegisterGroupX(AtomRegistry r);` — a
group that is not implemented yet compiles to nothing. Implement **your**
hook once, in a file you own (e.g. `Core/AtomFactory.Registration.GroupA.cs`):

```csharp
namespace Bento4
{
    public partial class AtomFactory
    {
        static partial void RegisterGroupA(AtomRegistry r)
        {
            // ... one r.Atom / r.SampleEntry per upstream case ...
        }
    }
}
```

The static constructor runs before any `AtomFactory` is constructed and
before `AtomFactory.Registry` is read, so tables are always complete before
first use. Do not touch `DefaultAtomFactory.Instance` from inside a Register
hook.

Already registered by the foundation (do **not** register these again):
`moof mvex traf tref mfra hnti stbl mdia dinf minf schi sinf udta ilst edts
mdri wave` (plain containers), `marl` (container at top level only),
`meta odrm odkm` (full containers), `free wide mdat` (generic), `8id `.
`moov`, `trak`, `stsd`, `dref`, `uuid` etc. are NOT registered: they belong to
the groups that port their classes.

### Worked examples

Each example shows the upstream `case` and its registration.

**1. Plain atom**

```cpp
case AP4_ATOM_TYPE_TKHD:
    if (atom_is_large) return AP4_ERROR_INVALID_FORMAT;
    atom = AP4_TkhdAtom::Create(size_32, stream);
    break;
```
```csharp
r.Atom(AtomType.TKHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TkhdAtom.Create(c.Size32, c.Stream)));
```

with, in `Core/TkhdAtom.cs`:

```csharp
public class TkhdAtom : Atom
{
    public static TkhdAtom Create(uint size, ByteStream stream)
    {
        byte version;
        uint flags;
        if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
        if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
        if (version > 1) return null;
        return new TkhdAtom(size, version, flags, stream);
    }

    private TkhdAtom(uint size, byte version, uint flags, ByteStream stream) :
        base(AtomType.TKHD, size, version, flags)
    {
        stream.ReadUI32(out _trackId);          // read errors are ignored, as upstream
        // ...
    }

    public override Ap4Result WriteFields(ByteStream stream)
    {
        Ap4Result result = stream.WriteUI32(_trackId);
        if (result.Failed()) return result;
        // ...
        return Ap4Result.SUCCESS;
    }

    public override Ap4Result InspectFields(AtomInspector inspector)
    {
        inspector.AddField("id", _trackId);
        return Ap4Result.SUCCESS;
    }

    private uint _trackId;
}
```

Variants of the plain case:

```csharp
// no atom_is_large check, 64-bit size (ODDA):   atom = AP4_OddaAtom::Create(size_64, stream);
r.Atom(AtomType.ODDA, (c) => c.Ok(OddaAtom.Create(c.Size64, c.Stream)));

// several types, one class (DVCC, DVVC)
r.Atoms(new uint[] { AtomType.DVCC, AtomType.DVVC },
        (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(DvccAtom.Create(c.Size32, c.Stream)));

// SetType fixup (HVCE)
r.Atom(AtomType.HVCE, (c) => {
    if (c.IsLarge) return AtomRegistry.InvalidFormat;
    Atom atom = HvccAtom.Create(c.Size32, c.Stream);
    if (atom != null) {
        atom.SetType(AtomType.HVCE);
    }
    return c.Ok(atom);
});

// a case that can fail with its own result (UUID)
r.Atom(AtomType.UUID, (c) => {
    byte[] uuid = new byte[16];
    Ap4Result result = c.Stream.Read(uuid, 16);
    if (result.Failed()) return c.Fail(result);
    // ... compare uuid, create the matching atom with (uint)c.Size64 / c.Size64 ...
    return c.Ok(new UnknownUuidAtom(c.Size64, uuid, c.Stream));
});

// needs the context stack (SCHM):   atom = AP4_SchmAtom::Create(size_32, &m_ContextStack, stream);
r.Atom(AtomType.SCHM, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(SchmAtom.Create(c.Size32, c.ContextStack, c.Stream)));
```

**2. Container atom that needs the factory**

```cpp
case AP4_ATOM_TYPE_TRAK:
    if (atom_is_large) return AP4_ERROR_INVALID_FORMAT;
    atom = AP4_TrakAtom::Create(size_32, stream, *this);
    break;
```
```csharp
r.Atom(AtomType.TRAK, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TrakAtom.Create(c.Size32, c.Stream, c.Factory)));
```

```csharp
public class TrakAtom : ContainerAtom
{
    public static TrakAtom Create(uint size, ByteStream stream, AtomFactory atom_factory)
    {
        return new TrakAtom(size, stream, atom_factory);
    }

    private TrakAtom(uint size, ByteStream stream, AtomFactory atom_factory) :
        base(AtomType.TRAK, size, false, stream, atom_factory)       // AP4_ContainerAtom(type, size, false, stream, factory): reads the children
    {
        _tkhdAtom = FindChild("tkhd") as TkhdAtom;                   // AP4_DYNAMIC_CAST(AP4_TkhdAtom, FindChild("tkhd"))
    }

    private TkhdAtom _tkhdAtom;
}
```

(The `uint size` converts implicitly to the `ulong size` of the base
constructor.) A generic container needs no class at all:

```csharp
r.Atom(AtomType.XXXX, (c) => c.IsLarge ? AtomRegistry.InvalidFormat
                                       : c.Ok(ContainerAtom.Create(c.Type, c.Size64, false, c.Force64, c.Stream, c.Factory)));
```

A container with fields before its children (like `stsd`, `dref`) reads its
fields in the constructor and then calls `ReadChildren` itself, or loops on
`atom_factory.CreateAtomFromStream(stream, ref bytes_available, out atom)`
between `atom_factory.PushContext(_type)` / `PopContext()` exactly as upstream.

**3. Context-dependent atom**

```cpp
case AP4_ATOM_TYPE_DAC3:
    if (atom_is_large) return AP4_ERROR_INVALID_FORMAT;
    if (GetContext() == AP4_ATOM_TYPE_AC_3 || GetContext() == AP4_ATOM_TYPE_ENCA) {
        atom = AP4_Dac3Atom::Create(size_32, stream);
    }
    break;
```
```csharp
r.Atom(AtomType.DAC3, (c) => {
    if (c.IsLarge) return AtomRegistry.InvalidFormat;
    if (c.GetContext() == AtomType.AC_3 || c.GetContext() == AtomType.ENCA) {
        return c.Ok(Dac3Atom.Create(c.Size32, c.Stream));
    }
    return AtomRegistry.Fallback;            // atom stays NULL -> UnknownAtom
});
```

Mind the order of the checks; the track reference types test the context
*first*:

```csharp
r.Atoms(new uint[] { AtomType.HINT, AtomType.CDSC, AtomType.SYNC, AtomType.MPOD,
                     AtomType.DPND, AtomType.IPIR, AtomType.ALIS, AtomType.CHAP }, (c) => {
    if (c.GetContext() == AtomType.TREF) {
        if (c.IsLarge) return AtomRegistry.InvalidFormat;
        return c.Ok(TrefTypeAtom.Create(c.Type, c.Size32, c.Stream));
    }
    return AtomRegistry.Fallback;
});
```

`c.GetContext(1)` is the grandparent container, etc.

**4. Sample entry** (the `GetContext() == AP4_ATOM_TYPE_STSD` switch)

```cpp
case AP4_ATOM_TYPE_MP4A:
    atom = new AP4_Mp4aSampleEntry(size_32, stream, *this);
    break;
case AP4_ATOM_TYPE_AVC1: case AP4_ATOM_TYPE_AVC2: case AP4_ATOM_TYPE_AVC3:
case AP4_ATOM_TYPE_AVC4: case AP4_ATOM_TYPE_DVAV: case AP4_ATOM_TYPE_DVA1:
    atom = new AP4_AvcSampleEntry(type, size_32, stream, *this);
    break;
```
```csharp
r.SampleEntry(AtomType.MP4A, (c) => c.Ok(new Mp4aSampleEntry(c.Size32, c.Stream, c.Factory)));
r.SampleEntries(new uint[] { AtomType.AVC1, AtomType.AVC2, AtomType.AVC3, AtomType.AVC4, AtomType.DVAV, AtomType.DVA1 },
                (c) => c.Ok(new AvcSampleEntry(c.Type, c.Size32, c.Stream, c.Factory)));
```

No `IsLarge` test here: the factory rejects large atoms before looking at
the sample entry table. The group that ports `AP4_UnknownSampleEntry` also
installs the fallback, in the same Register hook:

```csharp
AtomFactory.UnknownSampleEntryCreator = (type, size, stream) => new UnknownSampleEntry(type, size, stream);
```

Until that hook is set, unknown sample entries are plain `UnknownAtom`s.

### MetaData type handler

Upstream `AP4_DefaultAtomFactory::Initialize()` does
`AddTypeHandler(new AP4_MetaDataAtomTypeHandler(this))`. The MetaData port
provides it through its hook:

```csharp
public partial class AtomFactory
{
    static partial void RegisterGroupMetaData(AtomRegistry r)
    {
        DefaultAtomFactory.MetaDataTypeHandlerFactory = (factory) => new MetaDataAtomTypeHandler(factory);
    }
}
```

`Initialize()` (called by every `DefaultAtomFactory` constructor, including
the one inside `Atom.Clone()`) applies it when non-null. A `TypeHandler`:

```csharp
public class MetaDataAtomTypeHandler : AtomFactory.TypeHandler
{
    public MetaDataAtomTypeHandler(AtomFactory atom_factory) { _atomFactory = atom_factory; }

    public override Ap4Result CreateAtom(uint type, uint size, ByteStream stream, uint context, out Atom atom)
    {
        atom = null;
        // ... return SUCCESS with an atom, or a failure code (the next handler is then tried) ...
        return atom != null ? Ap4Result.SUCCESS : Ap4Result.FAILURE;
    }

    private AtomFactory _atomFactory;
}
```

### Reading a file (what the smoke test does)

```csharp
ByteStream input;
Ap4Result result = FileByteStream.Create(path, FileByteStream.Mode.STREAM_MODE_READ, out input);
AtomParent top = new AtomParent();
DefaultAtomFactory.Instance.CreateAtomsFromStream(input!, top);     // always SUCCESS; stops at the first atom that fails
foreach (Atom atom in top.GetChildren()) atom.Inspect(inspector);
foreach (Atom atom in top.GetChildren()) atom.Write(output);        // byte-identical to the input
if (input is IDisposable) ((IDisposable)input).Dispose();
```

`AtomFactory` instances carry a context stack and are not thread safe; use
one `new DefaultAtomFactory()` per thread rather than the shared `Instance`
when parsing concurrently.
