# CSharp4Downgrader

C# 소스 트리를 **C# 4.0 문법만 쓰도록** 제자리에서 고쳐 쓰는 Roslyn 기반 변환 도구입니다.

Bento4Sharp의 소스(`src/`, `tests/RoundTrip`)는 원래 최신 C# 문법으로 작성되었고, Visual Studio 2010(C# 4.0 컴파일러)에서도
같은 소스를 빌드할 수 있도록 이 도구로 **한 번** 일괄 변환했습니다. 변환이 재현 가능하도록 소스를 그대로 남겨 둔 것이며,
`Bento4Sharp.sln`에는 들어 있지 않고 라이브러리나 도구의 빌드에도 쓰이지 않습니다. 이 도구 자체의 소스는 최신 C#으로 되어 있습니다
(저장소의 `LangVersion 4` 설정을 프로젝트 파일에서 `latest`로 되돌려 놓았습니다).

## 실행

```bash
# 빌드 (출력을 저장소 밖에 두려면 --artifacts-path를 씁니다)
dotnet build tools/CSharp4Downgrader -c Release --artifacts-path <임시 폴더>

# 새 문법이 다시 들어왔는지 확인만 (파일을 바꾸지 않음, 바뀔 파일이 있으면 종료 코드 1)
dotnet <임시 폴더>/bin/CSharp4Downgrader/release/CSharp4Downgrader.dll src tests/RoundTrip --check

# 실제로 고쳐 쓰기
dotnet <임시 폴더>/bin/CSharp4Downgrader/release/CSharp4Downgrader.dll src tests/RoundTrip
```

```
CSharp4Downgrader <src-root> [<src-root>...] [--check] [--no-verify] [--tfm <tfm>[;<tfm>...]] [--ref <tfm>=<참조 어셈블리 폴더>]
```

- 루트 아래의 모든 `.cs`(`bin/`, `obj/` 제외)를 고칩니다. 루트를 여러 개 주면 함께 분석합니다.
  `tests/RoundTrip`은 `src`의 라이브러리 프로젝트를 참조하므로 반드시 `src`와 함께 줘야 형식이 해석됩니다.
  저장소 루트를 통째로 주면 이 도구의 소스(`tools/`)까지 고치게 되므로 그렇게 하지 마세요.
- 대상 프레임워크, `DefineConstants`, 프로젝트 참조는 `*.csproj`에서, 여러 프로젝트에 링크되는 파일(`Compat.cs`)은
  가장 가까운 `Directory.Build.props`에서 읽습니다. `#if` 분기는 대상 프레임워크와 기호 조합별로 각각 분석해 모든 분기를 고칩니다.
- 여러 번 실행해도 결과가 같습니다(이미 변환된 트리에서는 바뀌는 파일이 0개).
- 끝에 적용한 변환의 종류별 건수, 자동으로 바꾸지 못해 **손으로 고쳐야 하는 곳**의 목록, 그리고 대상 프레임워크별로
  `LangVersion 4`·nullable 끔으로 프로세스 안에서 컴파일해 본 결과를 출력합니다(`--no-verify`로 생략).
- 참조 어셈블리는 설치된 dotnet SDK의 팩과 NuGet 캐시(`Microsoft.NETFramework.ReferenceAssemblies.net20` 등)에서 찾습니다.
  찾지 못하면 `--ref <tfm>=<폴더>`로 지정합니다. 따라서 먼저 `dotnet restore Bento4Sharp.sln`이 되어 있어야 합니다.

## 하는 변환

| 원래 문법 | 바뀐 형태 |
|---|---|
| nullable 참조 형식 `T?`, null 허용 연산자 `x!`, `class?` 제약 | 표기 제거 (nullable 값 형식 `int?`는 그대로) |
| 파일 범위 네임스페이스 `namespace N;` | 블록 네임스페이스(본문 들여쓰기) |
| `out` 인자 자리의 선언 `M(out T v)`, `out _` | 문장 앞에 선언, 더미 변수 |
| `a?.M();`, `(x as T)?.M();` | `if (a != null) a.M();`, `if (x is T) ((T)x).M();` |
| 값으로 쓰인 `a?.b` | `a != null ? a.b : null` (수신자가 단순하지 않으면 임시 변수) |
| `a ??= b` | 문장이면 `if (a == null) a = b;`, 값이면 `a ?? (a = b)` |
| `x is T v` | `T v;`를 문장 앞에 선언하고 `(v = x as T) != null`, 값 형식이면 `if (x is T) { v = (T)x; ... }` |
| 그 밖의 `is` 패턴 | 불리언 식 |
| `switch` 식 | if/else |
| throw 식 (`a ?? throw`, `c ? x : throw`) | 문장 앞의 `if (...) throw ...;` |
| 예외 필터 `catch (E) when (c) {..}` | `catch (E) { if (c) {..} else <뒤의 catch 절이 했을 일> }` |
| `using var x = ...;` | `using (var x = ...) { 블록의 나머지 }` |
| `nameof(x)` | 문자열 리터럴 |
| 문자열 보간 `$"..."`, 원시 문자열 리터럴 | `string.Format`·리터럴·연결, 일반 문자열 리터럴 |
| 식 본문 멤버 `=>` | 블록 본문 |
| 자동 속성 초기화, `{ get; }` 전용 자동 속성, `init` 접근자 | 명시적 필드, `{ get; private set; }`, `set` |
| `default` 리터럴, `new(...)`, `c ? x : null`(대상 형식 추론) | `default(T)`, `new T(...)`, 한쪽 분기에 캐스트 |
| 캡처가 없는 지역 함수 | private 메서드 |
| 튜플 형식·리터럴·요소 이름 | `System.ValueTuple<...>`, `new System.ValueTuple<...>(...)`, `ItemN` |
| 버림 `_ = e;`, 람다의 버림 매개변수 `(_, _)` | `e;` 또는 더미 변수, 이름 있는 매개변수 |
| 람다에 캡처되는 `foreach` 변수 | 반복마다 복사본(C# 4의 클로저 의미에 맞춤) |
| `readonly struct`, `static` 람다, `private protected` | `struct`, 일반 람다, `internal` |
| 이진 리터럴 | 16진 리터럴 |
| `#pragma warning disable CSnnnn` | 숫자만 |

뜻을 보존하면서 자동으로 바꿀 수 없는 경우(`async`/`await`, 레코드, 바깥 변수를 캡처하는 지역 함수, 람다에 캡처되는 루프 안의
변수를 끌어올려야 하는 경우, 부작용이 있는 식을 두 번 평가하게 되는 경우 등)는 바꾸지 않고 "needs a manual edit" 목록에 파일과 줄 번호로 보고합니다.

## Bento4Sharp에 적용한 결과

`CSharp4Downgrader src tests/RoundTrip` 한 번으로 175개 파일(프로젝트 28개)이 모두 변환되었고, 손으로 고쳐야 하는 곳은 0개,
네 대상 프레임워크의 `LangVersion 4` 검증 오류도 0개였습니다. 적용된 변환은 다음과 같습니다.

| 건수 | 변환 |
|---:|---|
| 1770 | nullable 참조 표기 `T?` 제거 |
| 408 | null 허용 연산자 `!` 제거 |
| 175 | 파일 범위 네임스페이스 → 블록 네임스페이스 |
| 102 | `(x as T)?.M();` → `if (x is T) ((T)x).M();` |
| 14 | `a?.M();` → `if (a != null) a.M();` |
| 12 | `a ??= b` → `a ?? (a = b)` |
| 11 | `nameof(x)` → 문자열 리터럴 |
| 6 | `out` 인자 자리의 변수 선언 → 문장 앞 선언 |
| 5 + 2 | `x is T v` 패턴 (참조 형식 5, 값 형식 2) |
| 4 | 값으로 쓰인 `a?.b` |
| 4 | `out _` → 더미 변수 |
| 2 | `readonly struct` → `struct` |
| 2 | 예외 필터 `catch ... when` |
| 2 + 1 | 튜플 요소 이름 → `ItemN`, 튜플 리터럴 → `new System.ValueTuple<...>(...)` |
| 2 | `#pragma warning disable CSnnnn` → 숫자 |
| 1 | `static` 람다 → 일반 람다 |
| 1 | 버림 대입 `_ = e;` |

변환 전후의 `net8.0` 라이브러리에서 공개·`protected` 멤버를 리플렉션으로 뽑아 비교한 결과와, 변환 뒤 원본 도구와의 비교 결과는
[docs/VERIFICATION.md](../../docs/VERIFICATION.md)에 있습니다.

## 소스 구성

| 파일 | 내용 |
|---|---|
| `Program.cs` | 명령줄, 프로젝트·대상 프레임워크·`#if` 기호 조합 탐색, 참조 어셈블리 찾기, 단계별 실행, `LangVersion 4` 검증 |
| `Rewriter.Core.cs` | 변환 공통 도우미(연산자 우선순위, 이름 짓기, 선언 끌어올리기) |
| `Rewriter.Simple.cs` | 문장 구조를 바꾸지 않는 변환(nullable 표기, 리터럴, `nameof`, 한정자, 식 본문, `default`, `new()`, 문자열 보간, 튜플) |
| `Rewriter.Lowering.cs` | 선언이나 문장이 필요한 변환(`out` 변수, 패턴, `?.`, `??=`, 예외 필터, `using` 선언, throw 식, `switch` 식, 자동 속성, 버림) |
| `Rewriter.LocalFunctions.cs` | 캡처가 없는 지역 함수 → private 메서드 |
| `Edits.cs` | 텍스트 편집 묶음 관리 |
