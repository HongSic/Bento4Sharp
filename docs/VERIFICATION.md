# 검증 범위와 결과

Bento4Sharp는 원본 Bento4 도구와 같은 인자로 실행했을 때 출력 파일, 표준 출력, 표준 오류, 종료 코드가
바이트 단위로 같은지를 기준으로 검증했습니다.

- 기준 원본: Bento4 1.6.0.0 (커밋 `b8c50a078356a1c3444ce0a8744634ed488424a4`)
- 원본 도구: 네이티브 컴파일러가 없는 환경이라 Emscripten으로 빌드해 Node.js로 실행 (wasm32)
- 입력: 원본 `Test/Data`의 MP4 5개(AAC 3개, H.264+AAC 2개), 그것을 원본 도구로 프래그먼트·분할·암호화한 파생 파일,
  그리고 `tests/make-synthetic-inputs*.py`가 만드는 합성 파일

## 비교 스크립트

모든 스크립트는 `<reference-dir> <Bento4/Test/Data>` 두 인자를 받습니다(환경 변수 설명은 각 스크립트 머리말 참고).

| 스크립트 | 대상 | 통과 | 실패 | 제외 |
|---|---|---|---|---|
| `tests/compare-with-reference.sh` | mp4dump, mp4info, 읽고 다시 쓰기 | 814 | 0 | 2 |
| `tests/compare-tools-T1.sh` | mp4fragment, mp4split, mp4extract, mp4compact, mp4edit | 2279 | 0 | 17 |
| `tests/compare-tools-T2.sh` | mp4encrypt, mp4decrypt, mp4pssh, mp4dcfpackager, mp4tag | 2093 | 0 | 0 |
| `tests/compare-tools-T3.sh` | mp4mux, aac2mp4, fixaacsampledescription | 231 | 0 | 3 |
| `tests/compare-tools-T4.sh` | mp42hls, mp42ts, mp4iframeindex | 1768 | 0 | 0 |
| `tests/compare-tools-T5.sh` | mp42aac, mp42avc, mp42hevc, avcinfo, hevcinfo, mp4audioclip, mp4diff, mp4rtphintinfo | 1341 | 0 | 0 |

"제외"는 원본 도구가 그 입력에서 스스로 중단되어(디버그 빌드의 assert, 정의되지 않은 동작) 비교할 기준이 없는 경우입니다.

## 최신 .NET 대상을 `net8.0`에서 `net6.0`으로 낮춘 뒤 결과

라이브러리의 최신 .NET 대상과 도구의 대상을 `net8.0`에서 `net6.0`으로 낮췄습니다. 소스는 바뀌지 않았습니다
(`#if NET6_0_OR_GREATER` 분기가 그대로 적용됩니다). .NET 6 이상의 프로젝트는 모두 이 빌드를 사용합니다.

- 빌드: `dotnet build Bento4Sharp.sln -c Release` 경고 0, 오류 0 (`NETSDK1138` 지원 종료 안내는 `Directory.Build.props`에서 끔)
- `net6.0` 실행 파일로 위 여섯 스크립트를 다시 실행: 814 / 2279 / 2093 / 231 / 1768 / 1341 통과, 실패 0 (제외 2 / 17 / 0 / 3 / 0 / 0)
- `net20`, `netstandard2.0`, `netstandard1.3` 빌드는 이 변경으로 코드가 달라지지 않아 다시 실행하지 않았습니다.
- 아래 절들에 나오는 `net8.0` 결과는 이 변경 전에 측정한 것입니다.

## .NET Framework 2.0(`net20`) 전환 뒤 결과

.NET Framework용 대상을 `net35`에서 `net20`으로 낮춘 뒤(현재 소스) 같은 스크립트를 다시 돌린 결과(통과/실패)입니다.
제외 건수는 모든 행에서 위 표와 같습니다.

| 라이브러리 빌드 | 도구 빌드 | 실행한 런타임 | reference | T1 | T2 | T3 | T4 | T5 |
|---|---|---|---|---|---|---|---|---|
| `net8.0` (SDK) | `net8.0` (SDK) | .NET 8 | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| `net20` (SDK) | `net20` (SDK) | CLR 2.0.50727.9179 (.NET Framework 3.5 SP1에 든 2.0 SP2, 64비트) | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| Visual Studio 2010 `Release` (.NET Framework 2.0) | Visual Studio 2010 `Release` (Any CPU) | CLR 2.0.50727.9179 (위와 같음, 64비트) | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |

- 빌드: `dotnet build Bento4Sharp.sln -c Release`는 모든 프로젝트의 모든 대상에서 오류 0, 경고 0입니다.
  `devenv.com vs2010\Bento4Sharp.VS2010.sln /Rebuild`는 `Release|Any CPU`, `Debug|Any CPU` 모두 프로젝트 28개 성공, 경고 0입니다.
- 참조 어셈블리: `net20` 출력물(SDK 빌드 28개, Visual Studio 2010 `Release` 28개와 `Debug` 28개)의 어셈블리 참조를 메타데이터에서 읽어 확인했습니다.
  모두 `mscorlib 2.0.0.0`(도구는 여기에 `Bento4Sharp`)만 참조하고, Visual Studio 2010 `Debug` 빌드의 한 어셈블리만 `System 2.0.0.0`을 더 참조합니다.
  `System.Core`를 참조하는 것은 없습니다.
- Visual Studio 2010 행은 `vs2010/bin/Release`를 스크립트가 기대하는 폴더 구조(`bin/<프로젝트>/release_net20/`)로 복사해
  `BENTO4SHARP_FRAMEWORK=net20`, `BENTO4SHARP_SKIP_BUILD=1`로 실행한 것입니다. `Debug` 출력물로는 비교 스크립트를 돌리지 않았습니다.
- **최소 런타임.** `net20` 어셈블리(위 84개)가 바깥 어셈블리에서 참조하는 형식 88개와 멤버를 모두 메타데이터에서 뽑아 확인했습니다.
  .NET Framework 2.0 SP1 이후에 추가된 것(`DateTimeOffset`, `WaitHandle.WaitOne(int)`, `GCCollectionMode` 등)과 3.5의 `System.Core`에 있는 것
  (`Func<>`, LINQ, `HashSet<T>`, `Aes`, `ExtensionAttribute` 등)은 없습니다. 따라서 서비스 팩이 없는 .NET Framework 2.0에서 동작하도록 만들어져 있습니다.
  다만 이것은 참조 목록을 알려진 추가 항목과 대조한 것이고, 실제로 실행한 런타임은 위 표의 2.0 SP2(2.0.50727.9179)뿐입니다.
  서비스 팩이 없는 2.0이나 2.0 SP1 런타임에서는 실행해 보지 못했습니다(이 환경에 그 런타임이나 그 `mscorlib`이 없습니다).
- `net20`에서 달라진 코드: `Func<,>` 대신 대리자 형식 `DefaultAtomFactory.TypeHandlerFactory`, `DateTimeOffset` 대신 `DateTime` 계산(`Compat.UtcNowUnixTimeSeconds`),
  `Aes` 대신 `RijndaelManaged`(블록 128비트, ECB, 패딩 없음), 라이브러리 안의 `internal` `ExtensionAttribute`. 그 밖의 `#if`는 기호 이름만 `NET35`에서 `NET20`으로 바뀌었습니다.
- 공개 API: 전환 전후의 `Bento4Sharp.dll`에서 공개·`protected` 형식과 멤버를 뽑아 비교했습니다. `net8.0`, `netstandard2.0`, `netstandard1.3`은 4,381줄이 전환 전과 완전히 같습니다.
  `net20`은 전환 전 `net35`와 견주어 `DefaultAtomFactory.MetaDataTypeHandlerFactory` 필드의 형식(위 대리자)과 그 대리자 형식 자체(5줄)만 다릅니다.
  SDK와 Visual Studio 2010이 만든 `net20` 라이브러리의 목록은 서로 같습니다.
- `netstandard2.0`, `netstandard1.3` 빌드는 이번 전환 뒤 빌드(오류 0, 경고 0)와 위 공개 API 비교만 했고, 여섯 스크립트는 다시 돌리지 않았습니다. 아래 표의 해당 행은 전환 전 결과입니다.
- 64비트 JIT 결함 회피(아래 "Visual Studio 2010 빌드")는 기호가 `NET20`으로 바뀐 뒤에도 유효합니다. Visual Studio 2010 `Release`의 `mp4mux.exe`로
  `h265:` 세 경우를 25번씩 반복해 한 번도 죽지 않았습니다. 같은 실행 파일에서 `AddH265Track`의 `nooptimization`만 뺀 것(ildasm/ilasm)은
  세 경우 중 두 경우에서 25번 중 15번씩 `AccessViolationException`으로 죽었습니다.
- NuGet 패키지: `dotnet pack src/Bento4Sharp -c Release`로 만든 패키지를 로컬 피드에서 받아 `net8.0;net48;net35;net20` 콘솔 프로젝트에서 참조해
  네 실행 파일 모두 `Test/Data/video-h264-001.mp4`의 트랙 샘플 수(54, 78)와 FIPS-197 AES 벡터를 올바르게 출력했습니다(`net48`, `net35`, `net20`은 패키지의 `lib/net20`을 씁니다).
- `CSharp4Downgrader src tests/RoundTrip --check`는 바뀔 파일 0개(176개 중), `LangVersion 4` 검증 오류 0개를 보고합니다.

## C# 4.0 전환 뒤 결과 (당시 .NET Framework 대상은 `net35`)

소스 전체를 C# 4.0 문법으로 바꾼 뒤(`tools/CSharp4Downgrader`, [README](../README.md)의 "C# 4.0 소스 정책" 참고) 같은 스크립트를
다시 돌린 결과(통과/실패)입니다. 제외 건수는 모든 행에서 위 표와 같습니다. 이 절의 `net35` 행과 설명은 `net20`으로 낮추기 전에
`net35` 대상으로 측정한 기록입니다.

| 라이브러리 빌드 | 도구 빌드 | 실행한 런타임 | reference | T1 | T2 | T3 | T4 | T5 |
|---|---|---|---|---|---|---|---|---|
| `net8.0` (SDK) | `net8.0` (SDK) | .NET 8 | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| `net35` (SDK) | `net35` (SDK) | .NET Framework 3.5 (CLR 2.0, 64비트) | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| Visual Studio 2010 `Release` | Visual Studio 2010 `Release` (Any CPU) | .NET Framework 3.5 (CLR 2.0, 64비트) | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| `netstandard2.0` (SDK) | `net48` (임시 프로젝트) | .NET Framework 4.8 (64비트) | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| `netstandard1.3` (SDK) | `net48` (임시 프로젝트) | .NET Framework 4.8 (64비트) | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |

- SDK 행은 `dotnet build Bento4Sharp.sln -c Release`(모든 프로젝트의 모든 대상, `LangVersion 4`, 오류 0, 경고 0)의 출력물을
  `BENTO4SHARP_SKIP_BUILD=1`로 실행한 것입니다.
- Visual Studio 2010 행은 `devenv.com vs2010\Bento4Sharp.VS2010.sln /Rebuild "Release|Any CPU"`의 출력물(`vs2010/bin/Release`)을
  스크립트가 기대하는 폴더 구조(`bin/<프로젝트>/release_net35/`)로 복사해 `BENTO4SHARP_FRAMEWORK=net35`, `BENTO4SHARP_SKIP_BUILD=1`로 실행한 것입니다.
  `Release`와 `Debug` 구성 모두 프로젝트 28개(라이브러리, 도구 26개, RoundTrip)가 오류 0, 경고 0으로 빌드되었습니다. `Debug` 출력물로는 비교 스크립트를 돌리지 않았습니다.
- .NET Standard 행은 전환 전과 같은 방식입니다. 도구 26개와 RoundTrip의 소스를 그대로 `net48` 실행 파일로 컴파일하되(`LangVersion 4`)
  SDK가 빌드한 `netstandard2.0`/`netstandard1.3`용 `Bento4Sharp.dll`을 `HintPath`로 참조하게 한 저장소 밖의 임시 프로젝트입니다.
  전환 뒤에는 `net48` 조합으로 여섯 스크립트를 모두 돌렸고, 임시 `net8.0` 프로젝트 조합은 다시 돌리지 않았습니다.
- 공개 API: 전환 전후의 `net8.0`용 `Bento4Sharp.dll`에서 공개·`protected` 형식과 멤버(시그니처, 상수 값, nullable 관련 특성을 뺀 사용자 지정 특성 포함 4,382줄)를
  리플렉션으로 뽑아 비교했습니다. 차이는 한 가지입니다. `AtomCreationContext`와 `AtomCreationResult`가 `readonly struct`에서 `struct`로 바뀌어
  두 형식에서 `IsReadOnlyAttribute`가 빠졌습니다(C# 4.0에는 `readonly struct`가 없습니다. 필드는 그대로 `readonly`입니다). 그 밖의 형식·멤버·시그니처는 같습니다.
- 전환 뒤 `CSharp4Downgrader src tests/RoundTrip --check`는 바뀔 파일 0개, 네 대상 프레임워크의 `LangVersion 4` 검증 오류 0개를 보고합니다.

### Visual Studio 2010 빌드

- Visual Studio 2010은 자체 C# 4.0 컴파일러로 빌드합니다. 같은 소스라도 SDK(Roslyn)가 만든 IL과 완전히 같지는 않습니다.
- **64비트 JIT 결함과 그 회피.** Visual Studio 2010으로 최적화 빌드한 `mp4mux.exe`를 64비트 프로세스로 실행하면, `h265:` 트랙에 매개변수가 하나라도
  붙은 경우(`--track h265:video.h265#frame_rate=30` 등) `Program.AddH265Track`에서 `AccessViolationException`으로 죽는 일이 있었습니다
  (같은 명령을 반복했을 때 10번 중 8번 안팎. 입력 파일이 없는 경우에도, 메서드의 첫 문장이 실행되기 전에 발생).
  - 이 메서드의 IL은 유효하고(PEVerify 통과), Roslyn이나 .NET Framework 4.8의 `csc`가 만든 것과 `.maxstack` 값 하나(Visual Studio 2010은 28, 나머지는 27)만 다릅니다.
  - Visual Studio 2010이 만든 실행 파일을 ildasm/ilasm으로 풀었다 다시 조립하면서 이 메서드의 `.maxstack`만 바꿔 보면 28, 29, 30에서는 죽고 27과 31 이상에서는 죽지 않았습니다.
    반대로 `csc`가 만든 실행 파일에서 27을 28로 바꾸면 죽습니다.
  - 32비트 프로세스에서는 발생하지 않았고, 메서드에 `NoOptimization`을 붙이면 `.maxstack`이 28~30이어도 발생하지 않았습니다(`NoInlining`만으로는 그대로 발생).
  - 따라서 소스의 잘못이 아니라 CLR 2.0의 64비트 최적화 JIT이 이 모양의 메서드에서 잘못된 코드를 만드는 것으로 판단했습니다. 디버거가 없는 환경이라 기계어 수준의 원인까지는 확인하지 못했습니다.
  - 조치: `src/Tools/Mp4Mux/Program.cs`의 `AddH265Track`에 `#if NET35`(지금은 `#if NET20`)로 `[MethodImpl(MethodImplOptions.NoOptimization)]`을 붙였습니다(동작은 그대로이고 이 메서드만 최적화되지 않습니다.
    `net8.0` 빌드에는 들어가지 않습니다). 그 뒤 Visual Studio 2010 `Release` 빌드에서 문제의 세 경우를 30번씩 반복해 한 번도 죽지 않았고, 위 표의 여섯 스크립트가 모두 통과했습니다.
  - SDK(Roslyn)로 빌드한 `net35` 실행 파일은 이 조치 전후 모두 같은 반복 실행에서 죽지 않았습니다.
  - 같은 결함이 다른 메서드에서 나타날 가능성을 배제할 수는 없습니다(비교 스크립트의 경우들에서는 나타나지 않았습니다). 그럴 때는 `vs2010/generate-projects.py`의
    `RELEASE_TOOLS_PLATFORM_TARGET`을 `"x86"`으로 바꿔 도구를 32비트로 빌드하면 64비트 JIT을 쓰지 않게 됩니다. 이 32비트 구성으로는 비교 스크립트를 돌리지 않았습니다.
- Visual Studio 2010 IDE에서 솔루션을 직접 열어 보는 확인(솔루션 탐색기 표시 등)은 하지 못했습니다. 빌드는 모두 `devenv.com` 명령줄로 했습니다.

## 대상 프레임워크별 결과 (C# 4.0 전환 전, 당시 .NET Framework 대상은 `net35`)

아래는 C# 4.0으로 바꾸기 전의 소스에서 여섯 스크립트를 빌드별로 각각 끝까지 돌린 결과(통과/실패)입니다.
제외 건수는 모든 행에서 위 표와 같습니다. 이 절과 하위 절에서 `net35`라고 적은 것은 당시 `net35` 대상으로 측정한 기록입니다
(지금의 `net20` 결과는 위 ".NET Framework 2.0(`net20`) 전환 뒤 결과" 참고).

| 라이브러리 빌드 | 도구 빌드 | 실행한 런타임 | reference | T1 | T2 | T3 | T4 | T5 |
|---|---|---|---|---|---|---|---|---|
| `net8.0` | `net8.0` | .NET 8 | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| `net35` | `net35` | .NET Framework 3.5 (CLR 2.0, 64비트) | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| `netstandard2.0` | `net48` (임시 프로젝트) | .NET Framework 4.8 (64비트) | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| `netstandard1.3` | `net48` (임시 프로젝트) | .NET Framework 4.8 (64비트) | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| `netstandard2.0` | `net8.0` (임시 프로젝트) | .NET 8 | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |
| `netstandard1.3` | `net8.0` (임시 프로젝트) | .NET 8 | 814/0 | 2279/0 | 2093/0 | 231/0 | 1768/0 | 1341/0 |

- `net8.0`, `net35` 행은 저장소의 프로젝트를 그대로 빌드해 실행한 것입니다(`BENTO4SHARP_FRAMEWORK=net8.0` 또는 `net35`).
  `net35`에서는 `dotnet <tool>.dll` 대신 `<tool>.exe`를 직접 실행합니다.
- .NET Standard 빌드는 그 자체로 실행할 수 없어, 저장소 밖의 임시 프로젝트로 확인했습니다. 도구 26개와 `tests/RoundTrip`의
  `Program.cs`를 고치지 않고 그대로 가져와 `net48` 또는 `net8.0` 실행 파일로 컴파일하되, 프로젝트 참조 대신 미리 빌드한
  `netstandard2.0`/`netstandard1.3`용 `Bento4Sharp.dll`을 `HintPath`로 직접 참조하게 하고, 같은 여섯 스크립트를
  `BENTO4SHARP_SKIP_BUILD=1`로 돌렸습니다. 이 임시 프로젝트는 저장소에 들어 있지 않습니다.
- 네 라이브러리 빌드의 공개·`protected` 멤버(형식, 메서드, 생성자, 필드와 상수 값 4,377줄)를 리플렉션으로 뽑아 비교했고 서로 같았습니다.
- 모든 대상을 한 번에 빌드하는 `dotnet build Bento4Sharp.sln -c Release`는 오류 0, 경고 0입니다
  (`netstandard1.3` 때문에 나오는 NETSDK1215 안내 하나만 라이브러리 프로젝트에서 껐습니다).

### 런타임 차이 때문에 따로 구현한 것

.NET Framework의 기본 클래스 라이브러리는 같은 코드라도 .NET 8과 결과가 다른 곳이 있습니다. 출력이 바이트 단위로 같아야 하므로
`net8.0`이 아닌 대상에서는 아래를 `Compat`(`src/Bento4Sharp/Core/Compat.cs`)의 자체 구현으로 대신합니다. `net8.0` 빌드의 코드는 바뀌지 않았습니다.

- **실수 서식** (`%f`에 해당하는 `ToString("F6")`, `"F3"` 등): .NET Framework는 유효 숫자 15자리로 먼저 반올림한 뒤 0을 채웁니다.
  `Compat.FormatFixed`는 값의 정확한 십진 전개를 구해 .NET 8과 같은 문자열을 만듭니다(정확히 절반인 경우 짝수 쪽, 음의 0은 `-0.000000`).
- **실수 파싱** (`mp4fragment`, `mp4mux`의 `strtod`/`atof` 대응): .NET Framework의 `double.TryParse`는 자릿수가 많은 입력에서 마지막 비트가
  어긋날 수 있고, 범위를 넘으면 무한대를 돌려주는 대신 실패합니다. `Compat.TryParseDouble`은 가장 가까운 값(절반은 짝수 쪽)을 돌려주고 넘침은 무한대로 처리합니다.
- **`Math.ScaleB`, `double.IsNegative`**: 예전 대상에 없어 같은 결과를 내도록 구현했습니다.
- 위 네 가지는 .NET 8의 결과를 기준으로 446,020개 경우(임의 비트 패턴, 반올림 경계, 정확히 절반인 값, 두 double의 중간값과 그 이웃, 넘침·밑넘침, NaN·무한대,
  비정규 수)를 만들어 `net8.0`, `net35`, `net48`에서 각각 대조했고 불일치는 없었습니다. 같은 실행에서 `ulong`→`double` 변환과
  `mp4info`의 `float` 연산 식도 세 런타임이 같은 비트를 내는지 확인했습니다.
- **AES**: `Aes.EncryptEcb`/`DecryptEcb`가 없는 대상에서는 ECB·패딩 없음으로 만든 `ICryptoTransform`(방향별로 한 번 생성)에 한 블록씩 넣습니다.
  `net20`에는 `Aes` 클래스가 없어(`System.Core`) 같은 방식으로 `RijndaelManaged`(블록 128비트)를 씁니다. 같은 암호라 결과는 같습니다.
- **난수**: `RandomNumberGenerator.Fill` 대신 `RandomNumberGenerator.Create().GetBytes`.
- 문자열의 UTF-8 변환, SHA-256, HMAC, 키 랩은 원래부터 직접 구현한 코드라 런타임에 따라 달라지지 않습니다.

### 이전 프레임워크에서 확인하지 않은 것

- 32비트 프로세스. `net35`·`net48` 실행은 모두 64비트였습니다. 32비트 .NET Framework는 실수 연산에 x87을 쓰므로 `mp4info`의 프레임률처럼
  `float`로 계산하는 값이 달라질 수 있는지는 확인하지 못했습니다.
- 서비스 팩이 없는 .NET Framework 2.0과 2.0 SP1 런타임. `net20` 빌드를 실행한 CLR 2.0은 3.5 SP1에 든 2.0 SP2(2.0.50727.9179)뿐입니다.
- .NET Framework 3.5/4.8과 .NET 8 이외의 런타임(.NET Framework 4.0~4.7.2, .NET Core 1.x~3.1, Mono, Unity, Xamarin 등)에서의 실행.
  `netstandard1.3`/`netstandard2.0` 빌드는 위 두 런타임에서만 실행해 보았습니다.
- Windows 이외의 운영체제, FIPS 전용 모드로 설정된 Windows.

### 이전 프레임워크에서 달라지는 것으로 확인된 것

- **장치 이름을 파일 이름으로 준 경우** (`NUL`, `CON`, `COM1` 등): .NET 8은 원본의 `fopen`처럼 장치를 엽니다.
  .NET Framework(3.5, 4.8 모두)의 `FileStream`은 장치를 열지 않고 `NotSupportedException`을 던지므로 `ERROR_CANNOT_OPEN_FILE`이 됩니다.
  예: `mp4dump NUL`은 `net8.0`에서 아무것도 출력하지 않고 종료 코드 0, `net35`와 `net48`(netstandard 빌드)에서는
  `ERROR: cannot open input (-6)`과 종료 코드 1입니다. 관리 코드만으로는 고칠 수 없어(`CreateFile` P/Invoke가 필요) 그대로 두었습니다.
  비교 스크립트에서는 이 차이가 드러나지 않았습니다.
- 300자짜리 파일 이름, `<` `|` `?` `:`가 든 파일 이름은 `net8.0`, `net35`, `net48`에서 같은 오류 코드가 나오는 것을 `mp4dump`로 확인했습니다.

## 라이브러리 단위 확인

- AES: FIPS-197, NIST SP 800-38A(CBC, CTR) 벡터와 임의 입력 대조
- HMAC-SHA256: RFC 4231 벡터 / AES 키 랩: RFC 3394 벡터
- `Processor` 기본 재작성: 63개 입력에서 원본 `mp4edit`(옵션 없음)과 동일
- AVC 프레임 파서, ADTS 파서: 테스트 영상의 샘플과 대조

## 비교에서 따로 처리한 것

- **현재 시각**: `mp4mux`, `mp4fragment`는 `mvhd`의 생성·수정 시각에 현재 시각을 씁니다. 그 8바이트만 비교에서 제외합니다.
- **무작위 값**: 원본 Emscripten 빌드는 난수를 얻지 못해 무작위 키·IV 경로가 실패합니다. 포팅본은 네이티브 빌드처럼 난수를 생성합니다.
  이 경로는 포팅본이 고른 값을 원본에 고정값으로 넣어 결과가 같은지로 확인했습니다. `mp4rtphintinfo`의 시작 시퀀스·타임스탬프도
  원본부터 무작위라서 첫 패킷 기준 상대값으로 비교합니다.
- **`mp4pssh --insert/--replace`**: 원본이 출력 파일을 쓴 뒤 이중 해제로 비정상 종료합니다. 출력 파일만 비교합니다.

## 실제 미디어로 실행하지 못한 영역

이 환경에 해당 형식의 실제 파일이 없어, 아래는 합성 입력(유효한 헤더 + 채움 데이터)이나 코드 대조로만 확인했습니다.
실제 인코더가 만든 파일로는 돌려 보지 않았습니다.

- HEVC (hvcC, HEVC 파서, `mp42hevc`, `hevcinfo`, `mp4mux`의 H.265 입력, CBCS의 HEVC 경로)
- Dolby Vision
- AC-3, E-AC-3, AC-4 (최소 헤더만 가진 합성 스트림으로 일부 경로만 실행)
- RTP 힌트 트랙 (라이브러리로 합성한 힌트 트랙으로만 확인)
- `SegmentBuilder` 클래스: 원본 도구 중 이를 쓰는 것이 없어 컴파일과 코드 대조만 했습니다
- 4GB를 넘는 파일

## 원본과 의도적으로 다른 점

- 원본이 정의되지 않은 동작(널 포인터 역참조, 배열 범위 밖 읽기, 해제 후 사용)에 들어가는 입력에서는 포팅본이
  오류 코드를 돌려주거나 0으로 채운 값을 씁니다. 해당 위치마다 `// PORT NOTE:` 주석이 있습니다.
- C의 `long`/`unsigned long` 폭에 의존하는 인자 파싱은 도구마다 원본 기준 빌드(32비트)를 따르거나 64비트로 둔 곳이 있습니다.
  42억을 넘는 비정상적인 값에서만 차이가 납니다.
- AES 블록 변환은 원본의 룩업 테이블 대신 `System.Security.Cryptography.Aes`를 씁니다(모드 로직은 원본 그대로).
- 디렉터리를 입력 파일로 준 경우처럼 플랫폼의 `fopen` 동작에 좌우되는 오류 코드는 Windows 기준입니다.
