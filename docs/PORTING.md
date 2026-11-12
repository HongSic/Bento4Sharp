# Bento4Sharp porting conventions

Bento4Sharp is a pure C# implementation of [Bento4](https://github.com/axiomatic-systems/Bento4)
(C++). The goal is a **faithful, mostly line-by-line port**: same class
structure, same logic, same output bytes. Do not redesign, "modernize" or
fix upstream behaviour. Where a deviation is unavoidable, mark it with a
`// PORT NOTE:` comment explaining why.

## Layout

| Upstream                      | Port                                      |
|-------------------------------|-------------------------------------------|
| `Source/C++/Core/Ap4Foo.{h,cpp}`     | `src/Bento4Sharp/Core/Foo.cs`      |
| `Source/C++/Codecs/Ap4Foo.{h,cpp}`   | `src/Bento4Sharp/Codecs/Foo.cs`    |
| `Source/C++/Crypto/Ap4Foo.{h,cpp}`   | `src/Bento4Sharp/Crypto/Foo.cs`    |
| `Source/C++/MetaData/Ap4MetaData.*`  | `src/Bento4Sharp/MetaData/MetaData.cs` |
| `Source/C++/Apps/Mp4Dump`            | `src/Tools/Mp4Dump/`               |

One C# file per upstream `.h/.cpp` pair, holding every class of that pair.
Targets: `net8.0`, `netstandard2.0`, `netstandard1.3` and `net20` for the
library, `net8.0` and `net20` for the tools; **C# 4.0 syntax only** (see
"Language version" below), no `unsafe`, no NuGet dependencies (except the unavoidable `NETStandard.Library`
metapackage of `netstandard1.3`; the reference assemblies package used to
compile `net20` is build-time only). The code is written against the .NET 8
base class library: what an older target lacks goes in `Core/Compat.cs`
(internal, not compiled for `net8.0`; its extension methods are in
`Core/Compat.Extensions.cs`, which only the library compiles), or in the
narrowest possible `#if` whose `net8.0` branch is the original code. Do not use language features that
need runtime types the old targets do not have (tuples, `Span`, `Index`/`Range`,
`async`, `init`/records, default interface methods), and do not format or parse
floating point values with the BCL directly (see `Compat.FormatFixed`,
`Compat.TryParseDouble`): .NET Framework gives different results.
Everything lives in `namespace Bento4 { ... }`.

## The `net20` target: .NET Framework 2.0, `mscorlib` only

`net20` must run on .NET Framework 2.0 without service pack, so its assemblies
reference `mscorlib` (and at most `System`) and nothing else. In code compiled
for `net20` (symbol `NET20`):

- nothing from `System.Core` (.NET Framework 3.5): no LINQ (`System.Linq`),
  no `Func<>`, no `Action` with zero or several type parameters (`Action<T>`,
  `Predicate<T>`, `Comparison<T>` and `Converter<,>` are in `mscorlib`), no
  `HashSet<T>`, no `TimeZoneInfo`, no `System.Security.Cryptography.Aes`
  (the port uses `RijndaelManaged` with a 128 bit block there). Declare a
  delegate type where a `Func<>` would be used. Lambdas and extension methods
  are fine: they are compiler features (`ExtensionAttribute` is supplied by
  `Core/Compat.Extensions.cs`, as an internal type);
- nothing that was added to `mscorlib`/`System` after 2.0 RTM. The reference
  assemblies the SDK build compiles against describe 2.0 **SP2**, so compiling
  proves nothing here. Known additions: `DateTimeOffset` (2.0 SP1),
  `WaitHandle.WaitOne(int)`/`WaitOne(TimeSpan)`, `GCCollectionMode`,
  `GCSettings.LatencyMode`, and everything introduced in 4.0
  (`string.IsNullOrWhiteSpace`, `Stream.CopyTo`, `StringBuilder.Clear`,
  `Enum.HasFlag`, `Path.Combine` with more than two arguments, ...; these do
  not compile);
- keep the workaround in `src/Tools/Mp4Mux/Program.cs` (`AddH265Track` is
  `NoOptimization` under `NET20`): the 64-bit JIT of CLR 2.0 miscompiles the
  method as Visual Studio 2010 emits it (see `docs/VERIFICATION.md`).

## Language version: C# 4.0

The same source files are compiled by the dotnet SDK and by Visual Studio 2010
(`vs2010/Bento4Sharp.VS2010.sln`, .NET Framework 2.0), whose compiler is
C# 4.0. `Directory.Build.props` sets `<LangVersion>4</LangVersion>` and
`<Nullable>disable</Nullable>` for every project, so the SDK build fails on
newer syntax (error CS8025, "feature ... is not available in C# 4"). Do not override them in a project.

C# 4.0 has: generics, LINQ (do not use it: `net20` has no `System.Core`) and
lambdas, `var`, extension methods, object and
collection initializers, auto-properties (`{ get; set; }`, no initializer),
optional and named arguments, `dynamic` (do not use it: `netstandard1.3` and
`net20` lack the binder). It does **not** have, so write instead:

| Not available (C# 5 and later)                 | Write                                                    |
|------------------------------------------------|----------------------------------------------------------|
| nullable reference types: `Atom?`, `x!`        | `Atom`, `x` (a reference may be `null`; say so in a comment where it matters) |
| `Foo(out uint value)` / `out var` / `out _`    | `uint value; Foo(out value);`                            |
| `namespace Bento4;`                            | `namespace Bento4 { ... }`                               |
| `$"track {id}"`                                | concatenation or `string.Format(CultureInfo.InvariantCulture, ...)` |
| `a?.M()`, `a?.b`                               | `if (a != null) a.M();`, `a != null ? a.b : null`        |
| `a ??= b`                                      | `if (a == null) a = b;`                                  |
| `x is Foo f`, `switch` patterns/expressions    | `Foo f = x as Foo; if (f != null) ...`                   |
| `nameof(x)`                                    | `"x"`                                                    |
| `int M() => 1;`, `int P => 1;`, `int P { get; } = 1;` | block bodies, a `readonly` backing field          |
| `catch (E) when (c)`                           | `catch (E) { if (!c) throw; ... }` only when rethrowing is equivalent; otherwise restructure |
| `async`/`await`, tuples, local functions, `default` literal, `new()`, records, `readonly struct`, `in`/`ref` locals, ranges, `using` declarations, `static` lambdas, digit separators, binary literals | the C# 4 equivalent |

Also unavailable to the Visual Studio 2010 compiler: `[CallerMemberName]`
and friends (the attributes do not exist in .NET 2.0), and `#pragma warning
disable CS0168` (write the number only: `#pragma warning disable 0168`).

When a `.cs` file is added, removed or renamed, regenerate the Visual Studio
2010 projects: `python vs2010/generate-projects.py`.

The tree was converted once, mechanically, with `tools/CSharp4Downgrader`
(see its README); `CSharp4Downgrader src tests/RoundTrip --check` reports
whether anything newer than C# 4 has crept back in.

## File header

Every ported file starts with:

```csharp
// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Foo.h, Ap4Foo.cpp
// Original work Copyright 2002-20xx Axiomatic Systems, LLC   <- copy years from upstream file
// Licensed under the GNU General Public License v2 or later. See LICENSE.
```

## Documentation comments

Upstream's Doxygen comments (`/** ... */`, trailing `/**< ... */` and `///<`)
become C# XML documentation comments on the corresponding declaration, with
upstream's wording: do not reword, translate or add to them.

| Doxygen                                  | XML documentation comment                              |
|------------------------------------------|--------------------------------------------------------|
| description                              | `/// <summary>...</summary>` (one `///` line per upstream line) |
| `@param x text`                          | `/// <param name="x">text</param>` (the C# parameter name) |
| `@return text` (upstream also writes `@result:`) | `/// <returns>text</returns>`                  |
| text after the `@param`/`@return` list, notes | `/// <remarks>...</remarks>` (`<para>` only for real paragraph breaks) |
| `@deprecated`                            | `/// <remarks>Deprecated.</remarks>` (no `[Obsolete]`: that would change the code) |
| `@see X`                                 | `/// <seealso cref="X"/>` when it resolves             |
| `#AP4_Foo`, `AP4_Foo`, `AP4_Foo::Bar()`, `AP4_SUCCESS` | `<see cref="Foo"/>`, `<see cref="Foo.Bar"/>`, `<see cref="Ap4Result.SUCCESS"/>` when the cref resolves to exactly one symbol; otherwise `<c>AP4_Foo::Bar()</c>` with upstream's spelling (an overloaded method is ambiguous: CS0419) |
| member `x; /**< text */`                 | `/// <summary>text</summary>` on the line above the member |
| `<`, `>`, `&` in the text                | `&lt;`, `&gt;`, `&amp;`                                |

- The comment goes directly above the declaration, after the banner comment.
  The banners (`/*---- | AP4_Foo::Bar +----*/`) are section separators, not
  documentation: copy them unchanged.
- When one upstream declaration becomes several C# members (a twin with an
  explicit offset, an overload that replaces a default argument), document the
  primary one. Do not use `<inheritdoc>` (Visual Studio 2010 does not know it).
- If upstream documents only some parameters, give the others a short factual
  `<param>` as well (the compiler warns otherwise, CS1573); this is the only
  place where a port-specific phrase belongs in a documentation comment.
  Everything else the port has to say stays in a `// PORT NOTE:` comment.
- Not ported, because C# has no declaration to attach them to: `@file` and
  `@mainpage` blocks, comments on destructors and on members that do not exist
  in the port.
- Check with `dotnet build src/Bento4Sharp -p:GenerateDocumentationFile=true`:
  it must give no warning (CS1591, "missing XML comment", is suppressed in the
  project because upstream documents only part of its API).

## Naming

- Drop the `AP4_` prefix: `AP4_MoovAtom` -> `MoovAtom`, `AP4_ByteStream` -> `ByteStream`.
  If the bare name collides with a BCL type in common use, keep it anyway
  (we do not `using System.IO;` implicitly: `ImplicitUsings` is disabled;
  refer to BCL types with explicit `using` or fully qualified names).
- Methods keep their upstream names and stay **methods** (`GetTrackId()`,
  `SetDuration(x)`), not properties. This keeps the port mechanical.
- Fields: `m_FooBar` -> `_fooBar` (same visibility as upstream; `protected`
  stays `protected`).
- Constants: `AP4_FOO_BAR` -> a `const` in the most natural static class,
  PascalCase is **not** applied to keep grep-ability:
  - atom types `AP4_ATOM_TYPE_MOOV` -> `AtomType.MOOV` (`public static partial class AtomType`,
    `public const uint`). Names that start with a digit get a leading underscore
    (`AP4_ATOM_TYPE_8ID_` -> `AtomType._8ID_`). Any file may add constants to the
    partial class when upstream declares them in that header.
  - other constants `AP4_TRACK_FLAG_ENABLED` -> `Ap4.TRACK_FLAG_ENABLED`
    (`public static partial class Ap4`), or a `public const` on the owning
    class when upstream scopes it to a class.
  - enums nested in classes stay nested enums with upstream member names
    (`AP4_Track::TYPE_AUDIO` -> `Track.Type.TYPE_AUDIO`... keep the member
    spelling exactly as upstream).

## Types

| C++                         | C#        |
|-----------------------------|-----------|
| `AP4_UI08/UI16/UI32/UI64`   | `byte/ushort/uint/ulong` |
| `AP4_SI08/SI16/SI32/SI64`   | `sbyte/short/int/long`   |
| `AP4_Size`, `AP4_Ordinal`, `AP4_Cardinal`, `AP4_Flags`, `AP4_Mask` | `uint` |
| `AP4_LargeSize`, `AP4_Position`, `AP4_Offset` | `ulong` |
| `AP4_Byte`                  | `byte`    |
| `AP4_String`, `const char*` | `string` (see Strings) |
| `AP4_DataBuffer`            | `DataBuffer` |
| `AP4_Array<T>`, `AP4_List<T>` | `System.Collections.Generic.List<T>` |
| `AP4_Result`                | `Ap4Result` (enum) |
| raw `AP4_UI08*` + size      | `byte[]` (+ explicit offset/size when upstream passes a pointer into a buffer) |

C++ unsigned arithmetic wraps; C# does too in the default `unchecked`
context, but watch for implicit promotion to `int`/`long` (`byte + byte`,
`uint - int`, shifts of `byte`). Add casts so that results match C++.

## Results and errors

Upstream returns `AP4_Result` codes and never throws. The port does the
same: methods return `Ap4Result` and use `out` parameters.

```csharp
uint value;
Ap4Result result = stream.ReadUI32(out value);
if (result.Failed()) return result;          // AP4_FAILED(result)
if (result.Succeeded()) { ... }              // AP4_SUCCEEDED(result)
```

`AP4_SUCCESS` -> `Ap4Result.SUCCESS`, `AP4_ERROR_EOS` -> `Ap4Result.ERROR_EOS`, etc.
Like upstream, constructors that parse from a stream ignore read errors
(`out` values are zero on failure). Do not introduce exceptions for
malformed input; throw only for programming errors (`ArgumentNullException`).

## Memory, ownership, casts

- No reference counting. `AddReference()/Release()` calls are dropped; streams
  are plain objects (`FileByteStream` is `IDisposable`).
- `delete x` is dropped. Destructors that only free memory are dropped.
- `AP4_DYNAMIC_CAST(AP4_Foo, x)` -> `x as Foo`.
- Pointers that may be `NULL` become plain references that may be `null`
  (`Atom`; there are no nullable annotations in C# 4.0).

## Strings

Upstream strings are raw bytes. The port decodes/encodes them as UTF-8
through the helpers in `Ap4Utils`/`ByteStream` provided by the foundation
(`ByteStream.ReadString`, `ByteStream.WriteString`); use those helpers
instead of calling `System.Text.Encoding` directly, so the policy stays in
one place. `AP4_FormatString(buf, size, "fmt", ...)` becomes C# string
formatting that yields **exactly** the same text (`%d`, `%02x`, `%08x`,
`%lld`, `%f` -> use `CultureInfo.InvariantCulture`; `%f` is 6 decimals).

## Atoms

```csharp
public class TkhdAtom : Atom
{
    public static TkhdAtom Create(uint size, ByteStream stream) { ... }    // AP4_TkhdAtom::Create
    public TkhdAtom(...) : base(AtomType.TKHD, size, version, flags) { ... }
    private TkhdAtom(uint size, byte version, uint flags, ByteStream stream) : base(...) { ... }
    public override Ap4Result WriteFields(ByteStream stream) { ... }
    public override Ap4Result InspectFields(AtomInspector inspector) { ... }
}
```

- `Create` returns `null` where upstream returns `NULL`.
- Field names passed to the inspector, and their order, must be identical
  to upstream: `mp4dump` output is compared against the original tool.

## Atom factory registration

`AtomFactory` (Core/AtomFactory.cs) keeps upstream's context stack and
dispatch semantics, but the big `switch` is replaced by two lookup tables
so that files can be ported independently:

```csharp
// in your own file, e.g. Core/AtomFactory.Registration.GroupA.cs
namespace Bento4
{
    public partial class AtomFactory
    {
        static partial void RegisterGroupA(AtomRegistry r)
        {
            r.Atom(AtomType.TKHD, (c) => c.IsLarge ? AtomRegistry.InvalidFormat : c.Ok(TkhdAtom.Create(c.Size32, c.Stream)));
            r.SampleEntry(AtomType.MP4A, (c) => c.Ok(new Mp4aSampleEntry(c.Size32, c.Stream, c.Factory)));
        }
    }
}
```

The exact helper names are whatever Core/AtomFactory.cs defines (read it
first); what matters: reproduce, for each atom type, exactly what the
matching `case` in upstream `AP4_AtomFactory::CreateAtomFromStream` does
(the `atom_is_large` check, the `GetContext()` conditions, `SetType` fixups).
A creator that leaves the atom `null` and reports success means "fall back
to `UnknownAtom`", as upstream.
