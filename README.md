# Bento4Sharp

**한국어** | [English](README.en.md)

[Bento4](https://github.com/axiomatic-systems/Bento4)(C++ MP4/ISO-BMFF 라이브러리와 도구 모음)를 C#으로 옮긴 **순수 C# 구현체**입니다.

**래퍼(wrapper)가 아닙니다.** 네이티브 Bento4 DLL을 P/Invoke로 호출하거나 `mp4dump` 같은 실행 파일을 띄우는 방식이 아니라,
라이브러리 전체와 도구 26개를 관리 코드(C#)로 다시 작성했습니다. 네이티브 바이너리가 전혀 필요 없어서 DLL 하나만 참조하면 되고,
관리 코드뿐이라 특정 운영체제나 CPU에 묶이지 않습니다(다만 검증은 Windows에서만 했습니다).

원본의 클래스 구조와 로직을 거의 줄 단위로 그대로 옮겼고, 원본 도구와 출력이 바이트 단위로 같아지는 것을 목표로 합니다.

- 대상 프레임워크: 라이브러리는 `net8.0`, `netstandard2.0`, `netstandard1.3`, `net20`(.NET Framework 2.0 이상), 도구는 `net8.0`, `net20`
- 런타임 의존성: `net8.0`, `netstandard2.0`, `net20`은 없음. `netstandard1.3`만 `NETStandard.Library` 메타패키지(그 프레임워크의 기본 클래스 라이브러리 자체)에 의존
- 소스는 C# 4.0 문법만 사용: 같은 소스를 dotnet SDK와 Visual Studio 2010 양쪽에서 빌드합니다
- `unsafe` 코드 없음
- 기준 원본: Bento4 1.6.0.0 (커밋 `b8c50a078356a1c3444ce0a8744634ed488424a4`)
- 라이선스: GPL v2 이상 ([LICENSE](LICENSE) 참고)
- Author: HongSic (HSKernel) (C# 구현)

## 구성

```
Bento4Sharp.sln         dotnet SDK용 솔루션
Directory.Build.props   모든 프로젝트 공통 설정 (C# 4.0 강제 등)
src/Bento4Sharp/        라이브러리 (namespace Bento4)
  Core/                 Atom, 스트림, 팩토리, 트랙·샘플, 프래그먼트, 프로세서, 보호(CENC/PIFF/OMA/Marlin/ISMA), MPEG-2 TS
  Codecs/               AVC/HEVC/AAC(ADTS)/AC-3/E-AC-3/AC-4 파서
  Crypto/               AES 블록·스트림 암호, HMAC, 키 랩
  MetaData/             iTunes/3GPP/DCF 메타데이터
src/Tools/              명령줄 도구 26개 (원본 Apps와 1:1)
tests/                  원본 도구와 출력을 비교하는 스크립트
vs2010/                 Visual Studio 2010용 솔루션과 프로젝트 (.NET Framework 2.0), 프로젝트 생성 스크립트
tools/CSharp4Downgrader/  소스를 C# 4.0 문법으로 바꾸는 데 쓴 변환 도구 (솔루션에는 들어 있지 않음)
docs/                   포팅 규칙(PORTING.md), 기반 API 요약(FOUNDATION_API.md), 검증 결과(VERIFICATION.md)
```

도구 목록: `mp4dump` `mp4info` `mp4fragment` `mp4split` `mp4extract` `mp4compact` `mp4edit`
`mp4encrypt` `mp4decrypt` `mp4pssh` `mp4dcfpackager` `mp4tag` `mp4mux` `aac2mp4`
`fixaacsampledescription` `mp42hls` `mp42ts` `mp4iframeindex` `mp42aac` `mp42avc` `mp42hevc`
`avcinfo` `hevcinfo` `mp4audioclip` `mp4diff` `mp4rtphintinfo`

## 빌드

```bash
dotnet build Bento4Sharp.sln -c Release
```

도구 하나만 실행하려면(프로젝트가 여러 프레임워크를 대상으로 하므로 `-f`가 필요합니다):

```bash
dotnet run --project src/Tools/Mp4Dump -c Release -f net8.0 -- input.mp4
```

### 대상 프레임워크

| 프레임워크 | 대상 | 의존성 | 비고 |
|---|---|---|---|
| `net8.0` | 라이브러리, 도구 | 없음 | 기준 빌드 |
| `netstandard2.0` | 라이브러리 | 없음 | .NET Framework 4.6.1 이상, .NET Core 2.0 이상 등에서 참조 |
| `netstandard1.3` | 라이브러리 | `NETStandard.Library` 1.6.1 | .NET Standard 1.x는 기본 클래스 라이브러리가 패키지로 제공되므로 이 메타패키지는 피할 수 없습니다. 그 밖의 패키지는 없습니다. 1.0~1.2는 암호화·파일 API가 없어 대상이 될 수 없습니다 |
| `net20` | 라이브러리, 도구 | 없음 | .NET Framework 2.0 이상 (CLR 2.0). 3.5와 4.x에서도 그대로 실행됩니다. `mscorlib`만 참조하고 `System.Core`는 쓰지 않습니다 |

- .NET Framework 2.0용 참조 어셈블리는 dotnet SDK에 들어 있지 않아, `net20`을 빌드할 때만
  `Microsoft.NETFramework.ReferenceAssemblies` 패키지를 받아 씁니다([Directory.Build.props](Directory.Build.props)).
  빌드 시점에만 쓰이는 패키지(`PrivateAssets="all"`)라서 출력물에 들어가지 않고 런타임 의존성도 아닙니다.
  Visual Studio나 .NET Framework 개발자 팩은 필요 없습니다.
- `net20` 도구는 일반 실행 파일입니다. 실행하려면 .NET Framework 2.0, 3.0, 3.5 중 하나(CLR 2.0)가 설치되어 있어야 합니다.

  ```bash
  dotnet build src/Tools/Mp4Dump/Mp4Dump.csproj -c Release -f net20
  src/Tools/Mp4Dump/bin/Release/net20/mp4dump.exe input.mp4
  ```

- **`net20`의 최소 요구 사항은 서비스 팩이 없는 .NET Framework 2.0입니다.** `net20` 어셈블리가 참조하는 `mscorlib` 멤버를 모두 뽑아
  확인했고, 2.0 SP1 이후에 추가된 것(`DateTimeOffset` 등)이나 3.5의 `System.Core`에 있는 것(`Aes`, `Func<>`, LINQ, `HashSet<T>` 등)은
  쓰지 않습니다. 다만 실제로 실행해 본 런타임은 .NET Framework 3.5 SP1에 들어 있는 CLR 2.0 SP2(2.0.50727)뿐이고, SP가 없는 2.0이나
  SP1 런타임에서는 실행해 보지 못했습니다. `net20`에서 달라지는 점은 다음과 같습니다.
  - AES 블록 변환에 `Aes` 대신 `RijndaelManaged`(블록 128비트, ECB, 패딩 없음)를 씁니다. 같은 암호라 결과는 비트 단위로 같습니다.
    `RijndaelManaged`는 FIPS 인증 구현이 아니어서, FIPS 전용 모드로 설정된 Windows에서는 암호화·복호화 기능이 예외를 냅니다.
  - `DefaultAtomFactory.MetaDataTypeHandlerFactory` 필드의 형식이 `Func<AtomFactory, AtomFactory.TypeHandler>` 대신 같은 모양의 대리자
    `DefaultAtomFactory.TypeHandlerFactory`입니다(`Func<,>`가 `System.Core`에 있기 때문입니다).
  - 확장 메서드에 필요한 `ExtensionAttribute`를 라이브러리 안에 `internal`로 넣었습니다. .NET Framework 3.5 이상 프로젝트에서 참조해도
    충돌하지 않습니다. 다만 Visual Studio 2010 이하의 컴파일러는 그런 프로젝트가 `System.Core`도 참조하면서 확장 메서드를 직접 선언할 때
    경고 CS1685를 낼 수 있습니다(빌드와 동작에는 영향이 없습니다).

- 한 프레임워크만 빌드하려면 `-f <프레임워크>`를 붙입니다.
- 공개 API는 네 프레임워크에서 같습니다(위에 적은 `net20`의 `MetaDataTypeHandlerFactory` 필드 형식 하나만 예외).
  `Span` 등 새 런타임 전용 형식을 쓰는 공개 멤버가 없고, 프레임워크별 차이는 모두
  메서드 본문 안의 `#if`와 내부 클래스 `Compat`([src/Bento4Sharp/Core/Compat.cs](src/Bento4Sharp/Core/Compat.cs),
  [Compat.Extensions.cs](src/Bento4Sharp/Core/Compat.Extensions.cs))에 있습니다.
  `Compat`은 예전 기본 클래스 라이브러리에 없는 멤버(`List<T>.EnsureCapacity`, `Dictionary.TryAdd` 등)와,
  .NET Framework에서 결과가 달라지는 실수 서식·파싱(`ToString("F6")`, `double.Parse`)을 .NET 8과 같은 결과가 나오도록 대신 구현한 것입니다.
  `net8.0` 빌드에는 컴파일되지 않습니다.
- `netstandard1.3`에서만 다른 점: `Array.Copy`의 64비트 오버로드가 없어 2GB 미만 범위의 32비트 오버로드를 씁니다(.NET 배열 크기 한도 안이라 동작 차이는 없습니다).

### Visual Studio 2010에서 빌드

`vs2010/Bento4Sharp.VS2010.sln`을 Visual Studio 2010에서 열어 빌드합니다. 라이브러리, 도구 26개, `tests/RoundTrip`
프로젝트가 들어 있고 모두 .NET Framework 2.0을 대상으로 합니다(참조는 `mscorlib`와 `System`뿐). 구성은 `Debug`와 `Release`,
플랫폼은 `Any CPU`입니다. 명령줄에서는 다음과 같이 빌드합니다.

```bat
"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\devenv.com" vs2010\Bento4Sharp.VS2010.sln /Rebuild "Release|Any CPU"
```

- 이 프로젝트들은 자기 소스를 갖지 않습니다. `src/`와 `tests/RoundTrip`의 파일을 상대 경로의 링크 항목으로 참조하므로
  SDK 빌드와 Visual Studio 2010 빌드가 같은 소스를 컴파일합니다. 어셈블리 이름(`Bento4Sharp.dll`, `mp4dump.exe` 등)과
  전처리기 기호(`NET20` 등)도 SDK의 `net20` 빌드와 같습니다.
- 출력은 `vs2010/bin/<구성>/` 한 폴더에 모입니다(도구 26개와 `Bento4Sharp.dll`). 중간 파일은 `vs2010/obj/`에 생기며,
  `vs2010/` 밖에는 아무것도 쓰지 않습니다.
- `.cs` 파일이나 도구를 추가·삭제·이름 변경했을 때는 프로젝트를 다시 생성합니다. 프로젝트 파일은 생성물이므로 직접 고치지 말고
  스크립트를 고칩니다.

  ```bash
  python vs2010/generate-projects.py
  ```

- Visual Studio 2010만 설치된 컴퓨터에서는 .NET Framework 2.0 런타임 폴더(`%WINDIR%\Microsoft.NET\Framework\v2.0.50727`)의 어셈블리를
  참조해 컴파일합니다. `Reference Assemblies\Microsoft\Framework\.NETFramework\v2.0`에 2.0용 참조 어셈블리가 따로 설치된 컴퓨터에서는
  MSBuild가 `mscorlib.dll`을 출력 폴더에 복사하고 경고 MSB3270을 내므로, 그 폴더가 있으면 `mscorlib.dll`도 거기서 가져오도록
  프로젝트에 조건부 `FrameworkPathOverride`를 넣어 두었습니다.
- 실행 파일은 `Any CPU`라서 64비트 Windows에서는 64비트 프로세스로 실행됩니다. CLR 2.0(.NET Framework 2.0~3.5)의 64비트 JIT
  결함 때문에 `mp4mux`의 메서드 하나(`AddH265Track`)는 `NET20`에서 최적화를 끄도록 표시해 두었습니다. 같은 결함이 다른 곳에서
  나타나면 `vs2010/generate-projects.py`의 `RELEASE_TOOLS_PLATFORM_TARGET`을 `"x86"`으로 바꿔 도구를 32비트로 빌드할 수
  있습니다. 자세한 내용은 [docs/VERIFICATION.md](docs/VERIFICATION.md)의 "Visual Studio 2010 빌드" 절에 있습니다.

### C# 4.0 소스 정책

`src/`와 `tests/RoundTrip`의 모든 소스는 C# 4.0 문법만 씁니다. Visual Studio 2010의 컴파일러가 C# 4.0까지만 지원하고,
SDK용과 Visual Studio 2010용 소스를 따로 두지 않기 위해서입니다.

- [Directory.Build.props](Directory.Build.props)가 모든 프로젝트에 `<LangVersion>4</LangVersion>`과
  `<Nullable>disable</Nullable>`을 지정합니다. 그래서 `dotnet build`도 C# 5 이후의 문법을 오류로 처리합니다
  (프로젝트 파일에서 이 값을 바꾸지 마세요).
- 쓸 수 없는 것: nullable 참조 형식 표기(`Atom?`, `x!`), `out` 인자 자리에서의 변수 선언(`out uint value`, `out var`, `out _`),
  파일 범위 네임스페이스(`namespace Bento4;`), 문자열 보간(`$"..."`), `?.`, `??=`, `nameof`, 패턴 매칭(`x is Foo f`),
  식 본문 멤버(`=>`), `async`/`await`, 튜플, 지역 함수, 예외 필터(`catch ... when`), `readonly struct` 등.
  대신 쓰는 형태는 [docs/PORTING.md](docs/PORTING.md)의 "Language version" 절에 표로 정리되어 있습니다.
- 기존 소스는 [tools/CSharp4Downgrader](tools/CSharp4Downgrader)(Roslyn 기반 변환 도구)로 한 번 일괄 변환했습니다.
  이 도구의 `--check` 옵션으로 C# 4.0보다 새로운 문법이 다시 들어왔는지 확인할 수 있습니다.

## 소스를 다른 프로젝트에 직접 넣어 쓰기

보통은 `Bento4Sharp.dll`이나 NuGet 패키지를 참조하면 됩니다. 소스를 자기 프로젝트에 직접 넣어 빌드해야 한다면
`src/Bento4Sharp` 폴더를 **폴더째** 복사하세요. 폴더 안의 [.editorconfig](src/Bento4Sharp/.editorconfig)가 그 폴더의 `.cs` 파일에 대해서만
nullable 참조 형식 경고를 끕니다. 이 소스는 nullable 표기를 쓸 수 없는 C# 4.0이라, 이 설정이 없으면 `<Nullable>enable</Nullable>`인
프로젝트에서 nullable 경고가 1,000건 넘게 납니다.

끄는 것은 nullable 경고뿐입니다. 그 밖의 경고와 코드 분석은 프로젝트 설정대로 동작하고, 프로젝트의 나머지 코드에는 아무 영향이 없습니다.
(.NET 9 기본 설정에서는 원본에도 있는 "값을 넣었지만 읽지 않는 필드" 경고 CS0414가 5건 남습니다.)

## 라이브러리 사용 예

API는 원본과 같은 모양입니다. `AP4_` 접두사만 빠지고, 메서드는 `Ap4Result`를 돌려주며 출력값은 `out` 인자로 받습니다.

아래 예는 이 저장소의 소스와 같은 C# 4.0 문법으로 썼으므로 Visual Studio 2010에서도 그대로 컴파일됩니다.

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

새 컴파일러를 쓰는 프로젝트에서 이 라이브러리를 참조할 때는 `out ByteStream input`처럼 인자 자리에서 선언하거나 `$"..."`,
`?.`를 써도 됩니다. 다만 라이브러리에는 nullable 표기가 없으므로, 원본에서 `NULL`일 수 있는 값(`GetMovie()`의 반환값 등)은
`null`인지 직접 확인해야 합니다.

원본 문서와 예제가 거의 그대로 적용됩니다. C++과 달라진 점(다중 상속 처리, 문자열 인코딩, 값 타입이 클래스로 바뀐 곳 등)은
[docs/PORTING.md](docs/PORTING.md)와 [docs/FOUNDATION_API.md](docs/FOUNDATION_API.md) 1절에 정리되어 있고,
소스에서는 `// PORT NOTE:` 주석으로 표시되어 있습니다.

원본의 Doxygen 문서 주석은 C# XML 문서 주석(`/// <summary>` 등)으로 옮겨져 있어 IntelliSense에 원본의 설명이 그대로 나옵니다.
XML 문서 파일(`Bento4Sharp.xml`)이 필요하면 `-p:GenerateDocumentationFile=true`를 붙여 빌드하세요(기본 빌드는 만들지 않습니다).

## 원본과의 비교 검증

`tests/`의 스크립트는 원본 Bento4 도구와 포팅본을 같은 인자로 실행해 출력 파일, 표준 출력, 종료 코드를 바이트 단위로 비교합니다.
원본 도구(Emscripten 등으로 빌드해 `node`로 실행하는 형태)와 원본의 `Test/Data` 폴더가 필요합니다.

```bash
tests/compare-with-reference.sh <reference-dir> <Bento4/Test/Data>
```

기본은 `net8.0` 빌드를 비교합니다. `BENTO4SHARP_FRAMEWORK=net20`을 주면 .NET Framework 2.0용 실행 파일을 빌드해 같은 비교를 합니다.

```bash
BENTO4SHARP_FRAMEWORK=net20 tests/compare-with-reference.sh <reference-dir> <Bento4/Test/Data>
```

검증 범위와 결과는 [docs/VERIFICATION.md](docs/VERIFICATION.md)에 있습니다.

## 라이선스

Bento4Sharp는 Bento4|GPL의 파생 저작물이며 GNU GPL v2(또는 그 이후 버전)로 배포됩니다.
원저작권: Copyright 2002-2026 Axiomatic Systems, LLC.
C# 구현(Bento4Sharp) Author: HongSic (HSKernel).
GPL과 호환되지 않는 제품에 쓰려면 원저작자(Axiomatic Systems)의 상용 라이선스가 필요하며, 이 프로젝트가 그 라이선스를 대신 줄 수는 없습니다.
