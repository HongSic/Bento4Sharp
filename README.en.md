# Bento4Sharp

[한국어](README.md) | **English**

A **pure C# implementation** of [Bento4](https://github.com/axiomatic-systems/Bento4), the C++ MP4/ISO-BMFF library and tool suite.

**This is not a wrapper.** It does not P/Invoke a native Bento4 DLL and does not launch executables such as `mp4dump`:
the whole library and all 26 tools are rewritten in managed code (C#). No native binaries are needed: referencing
a single DLL is enough, and being managed code only it is not tied to an operating system or CPU
(verification, however, was done on Windows only).

The class structure and logic of the original are carried over almost line by line, and the goal is output that is
byte-for-byte identical to the original tools.

- Target frameworks: the library targets `net8.0`, `netstandard2.0`, `netstandard1.3` and `net20` (.NET Framework 2.0 and later); the tools target `net8.0` and `net20`
- Runtime dependencies: none on `net8.0`, `netstandard2.0` and `net20`. Only `netstandard1.3` depends on the `NETStandard.Library` metapackage (the base class library of that framework itself)
- The source uses C# 4.0 syntax only: the same files are built by both the dotnet SDK and Visual Studio 2010
- No `unsafe` code
- Upstream baseline: Bento4 1.6.0.0 (commit `b8c50a078356a1c3444ce0a8744634ed488424a4`)
- License: GPL v2 or later (see [LICENSE](LICENSE))
- Author: HongSic (HSKernel) (C# implementation)

## Layout

```
Bento4Sharp.sln         solution for the dotnet SDK
Directory.Build.props   settings shared by every project (enforces C# 4.0, etc.)
src/Bento4Sharp/        the library (namespace Bento4)
  Core/                 atoms, streams, factory, tracks and samples, fragments, processor, protection (CENC/PIFF/OMA/Marlin/ISMA), MPEG-2 TS
  Codecs/               AVC/HEVC/AAC (ADTS)/AC-3/E-AC-3/AC-4 parsers
  Crypto/               AES block and stream ciphers, HMAC, key wrap
  MetaData/             iTunes/3GPP/DCF metadata
src/Tools/              26 command-line tools (one for each of the original Apps)
tests/                  scripts that compare output with the original tools
vs2010/                 Visual Studio 2010 solution and projects (.NET Framework 2.0), and the script that generates them
tools/CSharp4Downgrader/  the rewriter used to convert the source to C# 4.0 syntax (not part of the solution)
docs/                   porting conventions (PORTING.md), foundation API summary (FOUNDATION_API.md), verification results (VERIFICATION.md, in Korean)
```

Tools: `mp4dump` `mp4info` `mp4fragment` `mp4split` `mp4extract` `mp4compact` `mp4edit`
`mp4encrypt` `mp4decrypt` `mp4pssh` `mp4dcfpackager` `mp4tag` `mp4mux` `aac2mp4`
`fixaacsampledescription` `mp42hls` `mp42ts` `mp4iframeindex` `mp42aac` `mp42avc` `mp42hevc`
`avcinfo` `hevcinfo` `mp4audioclip` `mp4diff` `mp4rtphintinfo`

## Building

```bash
dotnet build Bento4Sharp.sln -c Release
```

To run a single tool (the projects are multi-targeted, so `-f` is required):

```bash
dotnet run --project src/Tools/Mp4Dump -c Release -f net8.0 -- input.mp4
```

### Target frameworks

| Framework | Used by | Dependencies | Notes |
|---|---|---|---|
| `net8.0` | library, tools | none | the reference build |
| `netstandard2.0` | library | none | for .NET Framework 4.6.1+, .NET Core 2.0+ and so on |
| `netstandard1.3` | library | `NETStandard.Library` 1.6.1 | On .NET Standard 1.x the base class library is itself delivered as packages, so this metapackage cannot be avoided. There are no other packages. 1.0–1.2 cannot be targeted because they have no cryptography or file APIs |
| `net20` | library, tools | none | .NET Framework 2.0 and later (CLR 2.0). Also runs unchanged on 3.5 and 4.x. References `mscorlib` only; `System.Core` is not used |

- The dotnet SDK does not ship reference assemblies for .NET Framework 2.0, so the
  `Microsoft.NETFramework.ReferenceAssemblies` package is restored when building `net20` only
  (see [Directory.Build.props](Directory.Build.props)). It is a build-time package (`PrivateAssets="all"`):
  it adds nothing to the output and is not a runtime dependency. Neither Visual Studio nor a
  .NET Framework developer pack is needed.
- The `net20` tools are ordinary executables. .NET Framework 2.0, 3.0 or 3.5 (CLR 2.0) must be installed to run them.

  ```bash
  dotnet build src/Tools/Mp4Dump/Mp4Dump.csproj -c Release -f net20
  src/Tools/Mp4Dump/bin/Release/net20/mp4dump.exe input.mp4
  ```

- **The minimum requirement of `net20` is .NET Framework 2.0 without any service pack.** Every `mscorlib` member
  referenced by the `net20` assemblies was listed and checked: nothing added in 2.0 SP1 or later (such as
  `DateTimeOffset`) and nothing from the `System.Core` of 3.5 (`Aes`, `Func<>`, LINQ, `HashSet<T>`, …) is used.
  However, the only runtime it was actually run on is CLR 2.0 SP2 (2.0.50727), the one that comes with
  .NET Framework 3.5 SP1; it was not run on a 2.0 runtime without service pack or with SP1. What differs on `net20`:
  - The AES block transform uses `RijndaelManaged` (128-bit block, ECB, no padding) instead of `Aes`. It is the
    same cipher, so results are bit-identical. `RijndaelManaged` is not a FIPS-validated implementation: on a
    Windows configured for FIPS-only mode, encryption and decryption throw.
  - The field `DefaultAtomFactory.MetaDataTypeHandlerFactory` has the type `DefaultAtomFactory.TypeHandlerFactory`,
    a delegate of the same shape, instead of `Func<AtomFactory, AtomFactory.TypeHandler>` (`Func<,>` is in
    `System.Core`).
  - The `ExtensionAttribute` that extension methods need is included in the library as an `internal` type. It does
    not clash when the library is referenced from a .NET Framework 3.5 or later project. The compilers of
    Visual Studio 2010 and earlier may still report warning CS1685 when such a project also references
    `System.Core` and declares extension methods of its own (the build and the behaviour are not affected).

- Add `-f <framework>` to build a single framework.
- The public API is the same on all four frameworks (the only exception is the type of the
  `MetaDataTypeHandlerFactory` field on `net20`, described above). No public member uses types that exist only on newer
  runtimes (such as `Span`); every per-framework difference lives in `#if` blocks inside method bodies and in the
  internal class `Compat` ([src/Bento4Sharp/Core/Compat.cs](src/Bento4Sharp/Core/Compat.cs),
  [Compat.Extensions.cs](src/Bento4Sharp/Core/Compat.Extensions.cs)).
  `Compat` supplies members that older base class libraries lack (`List<T>.EnsureCapacity`, `Dictionary.TryAdd`, …)
  and reimplements floating-point formatting and parsing (`ToString("F6")`, `double.Parse`), whose results differ on
  .NET Framework, so that they match .NET 8. It is not compiled into the `net8.0` build.
- One difference on `netstandard1.3` only: the 64-bit overloads of `Array.Copy` do not exist there, so the 32-bit
  overloads are used for ranges below 2 GB (within the .NET array size limit, so behaviour is the same).

### Building with Visual Studio 2010

Open `vs2010/Bento4Sharp.VS2010.sln` in Visual Studio 2010 and build. It contains the library, the 26 tools and
`tests/RoundTrip`, all targeting .NET Framework 2.0 (the only references are `mscorlib` and `System`). Configurations are `Debug` and `Release`,
the platform is `Any CPU`. From the command line:

```bat
"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\devenv.com" vs2010\Bento4Sharp.VS2010.sln /Rebuild "Release|Any CPU"
```

- These projects have no sources of their own. They reference the files under `src/` and `tests/RoundTrip` as linked
  items by relative path, so the SDK build and the Visual Studio 2010 build compile the same source. Assembly names
  (`Bento4Sharp.dll`, `mp4dump.exe`, …) and preprocessor symbols (`NET20`, …) are also the same as in the SDK's
  `net20` build.
- Output is collected in one folder, `vs2010/bin/<configuration>/` (the 26 tools and `Bento4Sharp.dll`).
  Intermediate files go to `vs2010/obj/`; nothing is written outside `vs2010/`.
- After adding, removing or renaming a `.cs` file or a tool, regenerate the projects. The project files are
  generated: edit the script, not the projects.

  ```bash
  python vs2010/generate-projects.py
  ```

- On a machine with only Visual Studio 2010, the projects compile against the assemblies in the .NET Framework 2.0
  runtime folder (`%WINDIR%\Microsoft.NET\Framework\v2.0.50727`). On a machine that also has reference assemblies
  for 2.0 in `Reference Assemblies\Microsoft\Framework\.NETFramework\v2.0`, MSBuild would copy `mscorlib.dll` to
  the output folder and report warning MSB3270; the projects therefore contain a conditional
  `FrameworkPathOverride` that takes `mscorlib.dll` from that folder too when it exists.
- The executables are `Any CPU`, so they run as 64-bit processes on 64-bit Windows. Because of a defect in the
  64-bit JIT of CLR 2.0 (.NET Framework 2.0 to 3.5), one method of `mp4mux` (`AddH265Track`) is marked to be compiled
  without optimization under `NET20`. Should the same defect show up elsewhere, set
  `RELEASE_TOOLS_PLATFORM_TARGET` to `"x86"` in `vs2010/generate-projects.py` to build the tools as 32-bit.
  Details are in the "Visual Studio 2010 빌드" section of [docs/VERIFICATION.md](docs/VERIFICATION.md).

### C# 4.0 source policy

Everything under `src/` and `tests/RoundTrip` uses C# 4.0 syntax only. The Visual Studio 2010 compiler supports
nothing newer, and keeping one source tree avoids maintaining separate sources for the SDK and for Visual Studio 2010.

- [Directory.Build.props](Directory.Build.props) sets `<LangVersion>4</LangVersion>` and
  `<Nullable>disable</Nullable>` for every project, so `dotnet build` also rejects syntax newer than C# 4
  (do not override these values in a project file).
- Not available: nullable reference annotations (`Atom?`, `x!`), declarations in `out` argument position
  (`out uint value`, `out var`, `out _`), file-scoped namespaces (`namespace Bento4;`), string interpolation
  (`$"..."`), `?.`, `??=`, `nameof`, pattern matching (`x is Foo f`), expression-bodied members (`=>`),
  `async`/`await`, tuples, local functions, exception filters (`catch ... when`), `readonly struct`, and so on.
  The forms to use instead are tabulated in the "Language version" section of [docs/PORTING.md](docs/PORTING.md).
- The existing source was converted in one pass with [tools/CSharp4Downgrader](tools/CSharp4Downgrader)
  (a Roslyn-based rewriter). Its `--check` option tells whether syntax newer than C# 4.0 has crept back in.

## Compiling the source inside another project

Normally you reference `Bento4Sharp.dll` or the NuGet package. If you need to compile the source as part of your own
project instead, copy the `src/Bento4Sharp` folder **as a whole**. The [.editorconfig](src/Bento4Sharp/.editorconfig)
inside it turns off the nullable reference type warnings for the `.cs` files of that folder only. This source is C# 4.0
and cannot carry nullable annotations, so without that file a project with `<Nullable>enable</Nullable>` reports more
than 1,000 nullable warnings for it.

Only the nullable warnings are turned off. Every other warning and all code analysis behave as your project sets them,
and the rest of your project is unaffected. (With the default settings of a .NET 9 project, five CS0414 warnings remain:
"field is assigned but its value is never used", for members the original declares as well.)

## Using the library

The API has the same shape as the original: the `AP4_` prefix is dropped, methods return an `Ap4Result`, and
output values are returned through `out` parameters.

The example below is written in the same C# 4.0 syntax as the source in this repository, so it also compiles
as is in Visual Studio 2010.

```csharp
using Bento4;

class Example
{
    static void Main()
    {
        ByteStream input;
        Ap4Result result = FileByteStream.Create("input.mp4", FileByteStream.Mode.STREAM_MODE_READ, out input);
        if (result.Failed() || input == null) return;

        File file = new File(input);
        Movie movie = file.GetMovie();
        if (movie != null)
        {
            foreach (Track track in movie.GetTracks())
            {
                System.Console.WriteLine("track " + track.GetId() + ": " + track.GetSampleCount() + " samples");

                Sample sample = new Sample();
                DataBuffer data = new DataBuffer();
                if (track.ReadSample(0, sample, data).Succeeded())
                {
                    System.Console.WriteLine("  first sample: " + data.GetDataSize() + " bytes");
                }
            }
        }
        if (input is System.IDisposable) ((System.IDisposable)input).Dispose();
    }
}
```

A project built with a newer compiler may of course use `out ByteStream input` declarations, `$"..."` or `?.`
when calling the library. The library carries no nullable annotations, however, so values that can be `NULL`
in the original (the return value of `GetMovie()`, for example) must be checked for `null` by the caller.

The original documentation and examples apply almost unchanged. What differs from C++ (how multiple inheritance
is handled, string encoding, value types that became classes, …) is described in
[docs/PORTING.md](docs/PORTING.md) and in section 1 of [docs/FOUNDATION_API.md](docs/FOUNDATION_API.md),
and is marked in the source with `// PORT NOTE:` comments.

The Doxygen documentation comments of the original are carried over as C# XML documentation comments
(`/// <summary>` and so on), so IntelliSense shows the original's descriptions. If you need the XML documentation
file (`Bento4Sharp.xml`), build with `-p:GenerateDocumentationFile=true` (the default build does not produce it).

## Verification against the original

The scripts in `tests/` run the original Bento4 tools and the port with the same arguments and compare output
files, standard output and exit codes byte for byte. They need the original tools (for example built with
Emscripten and run with `node`) and the `Test/Data` folder of the original sources.

```bash
tests/compare-with-reference.sh <reference-dir> <Bento4/Test/Data>
```

By default the `net8.0` build is compared. With `BENTO4SHARP_FRAMEWORK=net20` the .NET Framework 2.0 executables
are built and compared in the same way.

```bash
BENTO4SHARP_FRAMEWORK=net20 tests/compare-with-reference.sh <reference-dir> <Bento4/Test/Data>
```

The scope and results of the verification are in [docs/VERIFICATION.md](docs/VERIFICATION.md) (in Korean).
In short: all six comparison suites (8,526 cases in total) pass with no failures on the `net8.0` build, the `net20`
build and the Visual Studio 2010 build (.NET Framework 2.0), all three run again after the move to `net20`. The
`netstandard2.0` and `netstandard1.3` library builds passed the same suites before that move; after it they were
built and their public API was compared (unchanged), but the suites were not run again with them.
Formats for which no real media was available (HEVC, Dolby Vision, AC-3, E-AC-3, AC-4, RTP hint tracks) were
exercised with synthetic input only.

## License

Bento4Sharp is a derivative work of Bento4|GPL and is distributed under the GNU GPL, version 2 or (at your option)
any later version.
Original work: Copyright 2002-2026 Axiomatic Systems, LLC.
C# implementation (Bento4Sharp) author: HongSic (HSKernel).
Using it in a product that is not compatible with the GPL requires a commercial license from the original author
(Axiomatic Systems); this project cannot grant that license.
