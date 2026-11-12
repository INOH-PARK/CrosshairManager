# crosshair-overlay - Work Plan

> 문서 언어 규칙: 이 계획서와 프로젝트 문서는 한국어로 작성한다. 실행 도구가 파싱하는 섹션 헤더(`## ...`), 작업 행 형식(`- [ ] N.`), 작업 필드 레이블(`Recommended task executor category:`, `What to do:` 등), 표 머리행, 코드 식별자·경로·명령은 원문 그대로 둔다.

## TL;DR (For humans)

**Who this is for and what changes for them:** 테두리 없는 창모드나 창모드로 게임을 하는 Windows 11 FPS 플레이어가 대상입니다. 크로스헤어가 항상 게임 위 화면 정중앙에 떠 있고, 클릭이나 포커스를 가로채지 않으며, 게임을 벗어나지 않고 바꾸거나 숨길 수 있습니다.

**What you'll get:** 설치 없이 실행하는 프로그램 하나입니다. 트레이 아이콘이 있고, 설정 창은 간단 모드(프리셋 원클릭, 견본색, 크기)와 상세 모드(점, 상하좌우 선, 간격, 두께, 외곽선, 투명도, 45도 단위 회전, 위치 오프셋, 모니터 선택)로 나뉩니다. 저장한 프리셋은 파일이나 짧은 코드로 공유할 수 있고, 메뉴는 한국어와 영어를 지원하며, 단축키로 켜기/끄기와 다음/이전 프리셋 전환을 합니다.

**Why this approach:** 크로스헤어를 정확한 픽셀로 계산하는 작은 엔진 하나를 테스트, 미리보기, 화면 표시가 함께 씁니다. 그래서 테스트한 결과가 곧 화면에 보이는 결과입니다. 표시는 크로스헤어 크기만 한 투명 창이 맡아서 선명하고 게임에 부담이 적습니다.

**What it will NOT do:** 진짜 전체화면(Exclusive) 게임 위에는 표시되지 않습니다. 게임에 끼어들거나 게임을 읽지 않습니다. Valorant/CS2 코드 가져오기, 게임별 자동 전환, 녹화 화면에서 숨기기는 하지 않습니다.

**Effort:** XL
**Risk:** Medium - 실제 안티치트 게임과 flip 모델 게임 위에서의 동작은 자동으로 시험할 수 없고, 일부 게임은 규정으로 오버레이를 금지합니다.
**Decisions to sanity-check:** 기본 단축키 Ctrl+Alt+Shift+X / →(다음) / ←(이전), 기본 크로스헤어는 녹색에 검은 외곽선 1px, 설정은 PC별로 Local AppData에 저장, 라이선스는 MIT(저작권자 ButterCoconut).

Your next move: 이 세션이나 새 세션에서 `/ulw-execute crosshair-overlay`로 실행을 시작합니다. 자세한 실행 내용은 아래에 있습니다.

---

> TL;DR (machine): XL, risk medium; .NET 10 WPF + Win32 layered overlay + framework-free Core; 20 todos + F1-F4; deliverables: portable exe, Core/Desktop/Ui test suites, CLI verifier, README ko/en.

## Scope
### Affected user and ideal state
**Affected user:**
- U1 - 테두리 없는 창모드나 창모드로 게임을 하는 Windows 11 FPS 플레이어. 현재: 아무것도 없음(프로젝트 폴더가 비어 있음). 이후: 포터블 exe를 더블클릭해 내장 크로스헤어를 고르거나 세밀하게 조정하고, 게임 위에 크로스헤어를 띄운 채 플레이하며, 트레이와 전역 단축키로 Alt+Tab 없이 조작한다.
- U2 - 프리셋과 설정을 확장할 미래의 유지보수자나 코딩 에이전트. 이후: 프레임워크에 의존하지 않고 테스트로 고정된 정확한 도형 계산을 하는 Core, 마이그레이션이 있는 버전 붙은 JSON 스키마, 테스트 계층마다 명령 하나.
- U3 - 방송하거나 녹화하는 플레이어. 이후: 크로스헤어가 화면 캡처에도 찍힌다는 점을 README에서 안다(캡처 제외 기능은 의도적으로 없음).

| Row | Statement | Reason |
| --- | --- | --- |
| IS-1 | 크로스헤어가 테두리 없는 창모드/창모드 게임을 포함한 모든 일반 창 위, 선택한 모니터의 정확한 중앙(+프리셋 X/Y 오프셋)에 그려진다. 포커스를 가져가지 않고 마우스 클릭을 가로채지 않는다. | 핵심 목적. 포커스나 클릭을 빼앗기면 게임 중 결정적인 순간을 망친다. |
| IS-2 | 픽셀 단위로 정확하다. 정수 물리 픽셀로 그리고, 중앙 기준 대칭이며, Windows 배율과 무관하게 선명하다(크기가 물리 픽셀이므로 구조적으로 보장, 이 PC의 DPI에서 검증). | 흐리거나 1px 어긋난 크로스헤어는 조준에 쓸 수 없다. |
| IS-3 | 상세 설정: 색(hex + R/G/B + 견본색 8개), 중앙 점(켜기/끄기, 크기, 불투명도), 선 4개 각각 켜기/끄기·길이·두께·간격("선 연동"과 T자형 단축 포함), 외곽선(켜기/끄기, 두께, 색, 불투명도), 전체 불투명도, 크기 배율, 45도 단위 회전, X/Y 오프셋, 대상 모니터. | 사용자가 점·선·색·외곽선 등 여러 항목의 상세 설정을 요청했다. |
| IS-4 | 간단 모드: 내장 프리셋을 한 번 클릭으로 적용하고, 견본색·크기 배율·불투명도·외곽선 토글을 제공한다. 사용자 프리셋은 만들기·이름 변경·복제·삭제가 가능하다. 내장 프리셋은 읽기 전용이다(편집하면 사본이 자동으로 생긴다). | 사용자가 간단한 기본 프리셋 모드를 요청했다. |
| IS-5 | 모든 편집이 화면의 오버레이에 즉시 반영되고, 실제 픽셀 격자를 확대한 미리보기에 보인다. 모든 것이 자동 저장된다. 손상된 파일, 더 새로운 버전의 파일, 쓸 수 없는 폴더 때문에 앱이 종료되거나 다른 프리셋을 잃는 일이 없다. | 어디서도 걸리거나 잃어버리는 일이 없어야 한다. |
| IS-6 | 트레이 아이콘(설정 열기, 표시/숨김, 프리셋 목록, 종료)과 전역 단축키(켜기/끄기, 다음, 이전 프리셋)를 제공하고 단축키는 바꿀 수 있다. 등록할 수 없는 단축키는 UI에 알린다. 인스턴스는 하나만 실행되고, 두 번째 실행은 첫 번째 인스턴스의 설정 창을 연다. | 게임 중 Alt+Tab 없이 조작한다. |
| IS-7 | 프리셋을 .json 파일로 내보내고 가져올 수 있고, 짧은 텍스트 코드로 복사/붙여넣기 할 수 있다. | 사용자 결정 D5. |
| IS-8 | UI는 한국어와 영어를 지원한다. 기본값은 OS 언어(둘 다 아니면 영어)이고 실행 중에 바로 바꿀 수 있다. | 사용자 결정 D4. |
| IS-9 | 설치·관리자 권한·.NET 설치가 필요 없는 self-contained exe 하나로 배포한다. 유휴 CPU는 거의 0이다(렌더 루프 없음, 바뀔 때만 다시 그림). | 사용자 결정 D3. 게임 FPS를 깎으면 안 된다. |
| IS-10 | 앱 안(첫 실행 대화상자 + 설정 창 하단)과 README에서 알린다: Exclusive 전체화면은 지원하지 않으니 테두리 없는 창모드를 쓸 것, 일부 게임은 오버레이를 금지하며(안티치트/약관) 책임은 사용자에게 있음, 시스템 단축키를 막는 게임에서는 단축키가 동작하지 않을 수 있음(트레이는 동작), 앱은 게임에 코드 주입·후킹·메모리 읽기·화면 읽기를 하지 않음, (U3) 크로스헤어는 녹화·방송 화면에도 찍힘(README). | 혼란과 계정 제재를 막는다. |
| IS-11 | 오버레이 창은 크로스헤어 경계 상자만큼만 크고, 바뀔 때만 다시 그리며, 무언가가 위에 올라왔을 때만 최상단을 다시 잡는다. | 크로스헤어를 계속 보이게 하면서 게임 출력 경로에 주는 영향을 최소화한다. |
| IS-12 | (U2) Core 로직은 프레임워크에 의존하지 않고 결정적인 테스트로 덮인다. 데스크톱과 UI 동작은 자동 테스트로 덮이고, 실패나 건너뜀이 하나라도 있으면 실패하는 스크립트 하나로 실행한다. | 유지보수성. TDD 결정 D6. |
| GAP-1 | 오버레이 창이 없다. | IS-1, IS-11을 닫는다. |
| GAP-2 | 도형 계산기/래스터라이저가 없다. | IS-2를 닫는다. |
| GAP-3 | 크로스헤어 모델과 설정 UI가 없다. | IS-3을 닫는다. |
| GAP-4 | 프리셋이 없다. | IS-4를 닫는다. |
| GAP-5 | 실시간 적용, 미리보기, 저장이 없다. | IS-5를 닫는다. |
| GAP-6 | 트레이, 단축키, 단일 인스턴스가 없다. | IS-6을 닫는다. |
| GAP-7 | 가져오기/내보내기/공유 코드가 없다. | IS-7을 닫는다. |
| GAP-8 | 다국어 지원이 없다. | IS-8을 닫는다. |
| GAP-9 | 빌드/배포 파이프라인이 없다. | IS-9를 닫는다. |
| GAP-10 | 사용자 안내와 고지가 없다. | IS-10을 닫는다. |
| GAP-11 | z-order 유지와 최소 크기 창 배치가 없다. | IS-11을 닫는다. |
| GAP-12 | 저장소, 솔루션, 테스트 계층이 없다. | IS-12를 닫는다. |

### Must have
- 툴체인(유일한 기준은 todo 1): .NET SDK 10.0.403(`global.json`, rollForward latestPatch). 테스트 스택은 VSTest 위의 xunit v2이며 xunit.v3/Microsoft.Testing.Platform이 아니다. 패키지는 `xunit`(2.9.x 최신), `xunit.runner.visualstudio`(3.x 최신), `Microsoft.NET.Test.Sdk`(17.x 최신)이고, 실제로 해석된 버전은 `docs/contracts.md`에 기록한다. App의 `AssemblyName`은 `CrosshairManager`(exe `CrosshairManager.exe`, 프로세스 이름 `CrosshairManager`).
- 솔루션 구조(정확히 이대로):
  ```
  CrosshairManager.sln   (dotnet new sln --format sln 으로 생성; .slnx 아님)
  global.json, Directory.Build.props (Nullable, ImplicitUsings, TreatWarningsAsErrors, LangVersion latest)
  .gitignore (dotnet 템플릿 + artifacts/, TestResults/, .omo/evidence/), .editorconfig, LICENSE (MIT, "Copyright (c) 2026 ButterCoconut"; 법적 문구라 영어 원문)
  src/CrosshairManager.Core/              net10.0 (Windows TFM 없음, P/Invoke 없음)
  src/CrosshairManager.Platform/          net10.0-windows, Microsoft.Windows.CsWin32 (PrivateAssets all), Microsoft.Win32.SystemEvents (10.x 최신), UseWPF 없음
  src/CrosshairManager.App/               net10.0-windows10.0.19041.0, UseWPF, CommunityToolkit.Mvvm, H.NotifyIcon.Wpf, app.manifest, AssemblyName CrosshairManager
  tools/CrosshairManager.Cli/             net10.0-windows, Core + Platform 참조, AssemblyName crosshair, app.manifest (PerMonitorV2)
  tests/CrosshairManager.TestSupport.Core/  net10.0 (AsciiArt, GoldenComparer)
  tests/CrosshairManager.TestSupport/       net10.0-windows, 자체 NativeMethods.txt를 가진 CsWin32 (BackdropWindow, Composite, Eventually, Input, Win32 테스트 헬퍼)
  tests/CrosshairManager.Core.Tests/        net10.0, xunit, Core + TestSupport.Core 참조
  tests/CrosshairManager.Desktop.Tests/     net10.0-windows, UseWPF, xunit, 모든 테스트에 [Trait("Category","Desktop")]
  tests/CrosshairManager.Ui.Tests/          net10.0-windows, xunit + FlaUI.UIA3, 모든 테스트에 [Trait("Category","Ui")]
  tests/tools/SendHotkey/                   net10.0-windows 콘솔, CsWin32 (SendInput), 테스트 전용
  scripts/build.ps1, scripts/verify.ps1, scripts/perf-check.ps1, scripts/publish.ps1 (모두 Windows PowerShell 5.1 호환; 이 PC에는 pwsh가 없음)
  docs/contracts.md (한국어), README.md (한국어, 기본), README.en.md (영어), THIRD-PARTY-NOTICES.md (한국어 설명 + 라이선스 원문 이름)
  ```
  프로젝트는 모두 10개. 의존 규칙(프로젝트 참조로만): Core <- Platform <- App; Cli -> Core + Platform; TestSupport -> Core + Platform; 각 테스트 프로젝트는 시험 대상 계층과 해당 TestSupport를 참조한다. Core는 Platform을 참조하지 않고, Platform은 WPF를 참조하지 않는다.
- Desktop.Tests와 Ui.Tests에는 `[assembly: CollectionBehavior(DisableTestParallelization = true)]`와 `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)`를 호출하는 `[ModuleInitializer]`가 있다. 각 프로젝트의 첫 테스트는 `AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), PER_MONITOR_AWARE_V2)`를 단언한다.
- 단위: 모든 크기는 정수 물리 픽셀이다. Windows DPI는 크로스헤어 크기를 바꾸지 않고, ScalePercent만 바꾼다. App과 Cli는 `app.manifest`로 Per-Monitor-V2 DPI 인식을 켠다(`<dpiAwareness>PerMonitorV2</dpiAwareness>`, `asInvoker`, Windows 10/11 supportedOS `{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}`).
- 크로스헤어 모델(Core, 정확히 이대로):

  | Field | Unit | Range | Default |
  | --- | --- | --- | --- |
  | Color | #RRGGBB | 제한 없음 | #00FF00 |
  | Opacity | % | 0..100 | 100 |
  | ScalePercent | % | 25..400 | 100 |
  | RotationDeg | 도, 시계 방향 | 0,45,90,135,180,225,270,315 (Normalize가 가장 가까운 45의 배수로 맞춤, 동률은 올림, 경고 포함) | 0 |
  | OffsetX / OffsetY | px (+오른쪽/+아래) | -4096..4096 | 0 |
  | Dot.Enabled / Size / Opacity | bool / px / % | - / 1..64 / 0..100 | true / 2 / 100 |
  | Top,Bottom,Left,Right .Enabled / Length / Thickness / Gap | bool / px / px / px | - / 0..512 / 1..64 / -32..256 | true / 6 / 2 / 3 |
  | Outline.Enabled / Thickness / Color / Opacity | bool / px / #RRGGBB / % | - / 1..16 / 제한 없음 / 0..100 | true / 1 / #000000 / 100 |

  점과 선은 같은 색을 쓴다. 선의 불투명도는 전체 Opacity 하나를 공유한다(선별 불투명도 없음). Gap은 중심점부터 잰다. T자형은 `Top.Enabled=false`이다(UI 단축 기능이지 필드가 아님). 바깥쪽 선 세트는 따로 없다. `Normalize()`는 표의 범위로 잘라 내고, 경고(`"<path>: <value> clamped to <new>"`)를 돌려주며, 여러 번 적용해도 결과가 같다.
- 처리 순서(모든 곳에서 고정): `Normalize` -> `ApplyScale`(Size/Length/Thickness/Gap에 ScalePercent/100을 곱하고 0에서 먼 쪽으로 반올림; 유일한 하한은 Size/Thickness >= 1; 표의 최댓값으로 다시 자르지 않음) -> `CrosshairLayout.Compute` -> `CrosshairRasterizer.Render` -> `OverlayPlacement.Compute`.
- 도형 규칙(Core): 중심 `P = (mon.Left + W/2 + OffsetX, mon.Top + H/2 + OffsetY)`; `snap(x) = floor(x + 0.5)`; `half(n,c) = snap(c - n/2)`; Dot `[half(s,Px),+s) x [half(s,Py),+s)`; 십자 중심 띠: 가로 기준 두께 `Ty` = 켜진 Left/Right 선 두께의 최댓값(없으면 켜진 점의 크기, 그것도 없으면 0), 세로 기준 두께 `Tx` = 켜진 Top/Bottom 선 두께의 최댓값(없으면 점 크기, 없으면 0); 띠 중심 `cy = Ty>0 ? half(Ty,Py) + Ty/2 : Py`, `cx = Tx>0 ? half(Tx,Px) + Tx/2 : Px`(항상 정수나 .5); Top `x:[half(T,Px),+T) y:[topEnd-L, topEnd)`, `topEnd = snap(cy-G)`; Bottom `y:[bottomStart, +L)`, `bottomStart = 2*cy - topEnd`; Left `x:[leftEnd-L, leftEnd) y:[half(T,Py),+T)`, `leftEnd = snap(cx-G)`; Right `x:[rightStart,+L)`, `rightStart = 2*cx - leftEnd`(맞은편 간격이 띠 중심 기준으로 정확히 대칭이 되어 홀수 두께에서도 네 방향 빈칸 수가 같고, 짝수 두께에서는 `snap(P±G)`와 같은 결과); Outline = 채움 영역 합집합으로부터 체비쇼프 거리 1..o인 픽셀; 캔버스 반폭 `e = ceil(P에서 채움 사각형 꼭짓점까지 체비쇼프 거리의 최댓값) + o + (회전이 90의 배수가 아니면 ceil(e0*0.4143)+1, 아니면 0)`(e0는 회전 전 반폭; 45도 대각선으로 늘어나는 만큼을 덮음); Compute에는 화면의 홀짝 플래그 두 개(W 홀수? H 홀수?)만 들어간다.
- 래스터라이저(Core): premultiplied BGRA, 위에서 아래 순서. 회전이 90의 배수면 픽셀 중앙에서 한 번만 샘플링한다(정확, 안티에일리어싱 없음). 45/135/225/315도는 4x4 균일 슈퍼샘플링. 분류 순서: 샘플점을 P 기준으로 -RotationDeg만큼 돌린 뒤 점 -> 선 -> 외곽선 -> 투명. `alpha = round(255*coverage*partAlpha/255)`, `c_pm = round(c*alpha/255)`. 부분별 알파 바이트 = `round(255 * partOpacity/100 * Opacity/100)`.
- 오버레이(Platform `OverlayHost`): Win32 `WS_POPUP` + `WS_EX_LAYERED|WS_EX_TRANSPARENT|WS_EX_TOPMOST|WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE`. 바뀔 때만 `UpdateLayeredWindow(ULW_ALPHA)`로 그린다. 자체 메시지 루프를 가진 전용 백그라운드 스레드 "Overlay"에서 돌고, 창 크기는 캔버스와 같다. `TopmostKeeper`: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT)` + 1초 확인 타이머. `GetWindow(hwnd, GW_HWNDPREV)`가 창을 돌려줄 때(위에 무언가 있을 때)만 `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE|SWP_NOMOVE|SWP_NOSIZE)`로 최상단을 다시 잡고 `Reasserted`를 발생시킨다.
- 모니터 식별자는 `MONITORINFOEX.szDevice`. 선택 모드는 `primary` 또는 `device`. 지정한 장치가 없으면 주 모니터로 대체하고 UI에 경고하되 설정은 다시 쓰지 않는다. `SystemEvents.DisplaySettingsChanged`(해상도, 모니터 연결/해제, 배율 변경 포함) 때 위치를 다시 계산한다.
- 단축키: GlobalHotkeys 전용 메시지 전용 창과 스레드에서 `RegisterHotKey` + `MOD_NOREPEAT`. 기본값: ToggleOverlay `Ctrl+Alt+Shift+X`, NextPreset `Ctrl+Alt+Shift+Right`, PreviousPreset `Ctrl+Alt+Shift+Left`, OpenSettings 미지정. 테스트 전용 조합은 기본값이 쓰지 않는 F9~F12 조합을 쓴다. 다음/이전은 목록 순서대로 불러올 수 있는 프리셋을 순환한다.
- 내장 프리셋(임베디드 JSON, 읽기 전용, 목록 순서): `builtin.default`(기본값), `builtin.dot`(점 4, 선 모두 끔, 외곽선 1), `builtin.cross`(점 끔, 선 6/2/3), `builtin.t`(기본값에서 Top 끔), `builtin.x45`(builtin.cross를 45도 회전), `builtin.classic1px`(점 끔, 선 L4 T1 G2, 외곽선 끔). 이름은 resx 키 `Preset.builtin.<suffix>`로 가져온다.
- 프리셋 파일 `presets/{guid}.json`: camelCase, System.Text.Json 소스 생성, `schemaVersion: 1` 필수. 없는 선택 필드는 기본값. `> 1`이면 NewerVersion(사용 불가로 표시, 절대 다시 쓰지 않음). 형식이 깨졌거나 `crosshair`가 없으면 Broken(목록에 표시, 절대 다시 쓰지 않음). 범위를 벗어난 값은 잘라 내고 경고. 정렬: 내장 프리셋(고정 순서) 다음 사용자 프리셋(CreatedUtc, 같으면 Id 서수 비교).
- 설정 `settings.json` v1: `schemaVersion, activePresetId, overlayVisible, monitor {mode, deviceName}, hotkeys {toggleOverlay, nextPreset, previousPreset, openSettings}, language ("system"|"ko"|"en"), startWithWindows, disclaimerAccepted, settingsWindowPlacement {left, top, width, height} | null`. 손상 시 `settings.json.corrupt-<yyyyMMddHHmmss>`로 이름을 바꾸고 기본값을 불러온 뒤 InfoBar로 알림. 더 새로운 버전이면 메모리에서만 기본값을 쓰고 파일은 건드리지 않으며 InfoBar로 알림.
- 데이터 폴더: `%LocalAppData%\CrosshairManager\`. 우선순위 `--data-dir <path>` > 환경변수 `CROSSHAIRMANAGER_DATA_DIR` > 기본값. 쓰기는 원자적으로 한다(`*.tmp`에 쓴 뒤 `File.Replace(tmp, target, target+".bak")`, 대상이 없으면 `File.Move`). 데이터 폴더에 쓸 수 없으면 메모리에서 계속 동작하고 `%TEMP%\CrosshairManager-fallback.log`에 `data dir read-only` 줄을 기록하며 InfoBar 키 `Error.DataDirReadOnly`로 알린다. 로그는 `logs/app.log`이고 1 MB에서 `app.1.log`로 넘긴다. 오버레이가 보이는 상태로 갱신에 성공할 때마다 표지 줄 `overlay shown <x>,<y>,<w>,<h>`를, 숨길 때 `overlay hidden`을 기록한다.
- 시작 시 표시 우선순위: `--hidden` > 저장된 `overlayVisible` > true. 시작할 때 설정 창은 `--settings`를 주었거나 `disclaimerAccepted`가 false일 때(첫 실행; 그 위에 DisclaimerDialog 표시)만 연다. 고지 대화상자를 확인 없이 닫으면 계속 진행하고 다음 실행 때 다시 보여 준다. 설정 창의 닫기 버튼은 트레이로 숨기고, 종료는 트레이에서만 한다.
- 단일 인스턴스: 정규화한 데이터 폴더 = `Path.GetFullPath(dir).TrimEnd('\\').ToUpperInvariant()`; 뮤텍스 `Local\CrosshairManager-<SHA-256(정규화 경로)의 앞 16자리 hex>`; 같은 접미사에 `-open`을 붙인 EventWaitHandle. 두 번째 실행은 그 이벤트를 신호하고 exit 0으로 끝나며, 첫 번째 인스턴스가 설정 창을 열고 활성화한다.
- Windows 시작 시 실행: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`의 값 `CrosshairManager` = `"<현재 exe 경로>"`. 켜져 있으면 실행할 때마다 경로가 다르면 다시 쓴다. 끄면 값을 지운다.
- 편집: 편집은 즉시 적용된다. 사용자 프리셋 편집은 400 ms 동안 입력이 없으면 자동 저장된다. 내장 프리셋을 처음 편집하면 `Loc["Preset.CopyName"]`에 내장 이름을 넣은 이름으로 사용자 사본을 만들고, 그것을 선택한 뒤 편집을 적용한다.
- 공유 코드: `CHM1:` + base64url(패딩 없음)(raw-deflate(UTF-8 정규 JSON `{name, crosshair}`)). 정규 JSON은 `CrosshairSpec.Default`와 같은 값을 생략하고(디코드할 때 기본값으로 채움) 스키마 순서대로 키를 쓴다. 압축을 푼 크기 상한 16 KB. 가져오면 새 GUID. 오류 키 `ShareCode.Error.Prefix|Base64|Inflate|Json|TooLarge`.
- 다국어: `Resources/Strings.resx`(영어, 중립) + `Strings.ko.resx`. 인덱서 `this[string key]`와 `PropertyChanged("Item[]")`를 발생시키는 `SetLanguage("system"|"ko"|"en")`를 가진 `Loc` 싱글턴. XAML은 `{Binding [Key], Source={x:Static loc:Loc.Instance}}`. 사용자에게 보이는 문자열을 하드코딩하지 않는다.
- 설정 창: 왼쪽에 썸네일이 있는 프리셋 목록, 가운데 간단/상세 탭, 오른쪽 미리보기, 아래쪽 InfoBar. 미리보기 = Core PixelBuffer를 정수 배율 8로, `BitmapScalingMode.NearestNeighbor`, Image 크기를 `VisualTreeHelper.GetDpi(this).DpiScaleX/Y`로 나눠 원본 1픽셀이 장치 픽셀 8x8이 되게 함, 체크무늬 배경. 창 위치는 숨길 때 `settingsWindowPlacement`에 저장하고 다시 열 때 복원(보이는 모니터 안으로 보정). 최소 크기 900x620, 기본 글꼴 Segoe UI Variable, WPF 기본 테마.
- HotkeyBox: keydown에서 키 조합을 받는다. `e.Key == Key.ImeProcessed`(한글 IME 켜짐)이면 `e.ImeProcessedKey`를, `e.Key == Key.System`(Alt/F10 조합)이면 `e.SystemKey`를 쓴다. HotkeyGesture로 검증하고, 등록에 실패하면 HotkeyError를 표시한다.
- 견본색(순서대로): #00FF00, #FFFF00, #00FFFF, #FF00FF, #FF0000, #FFFFFF, #FF8800, #0080FF.
- 고정 AutomationId(docs/contracts.md): `PresetList, NewPreset, DuplicatePreset, RenamePreset, RenameText, RenameOk, DeletePreset, ConfirmDeleteOk, ConfirmDeleteCancel, ImportPresetFile, ExportPresetFile, CopyShareCode, ShareCodeInput, PasteShareCode, ShareCodeError, TabSimple, TabAdvanced, Swatch0..Swatch7, SimpleScale, SimpleOpacity, SimpleOutline, ColorHex, ColorR, ColorG, ColorB, DotEnabled, DotSize, DotOpacity, LinkLines, TStyle, Top.Enabled, Top.Length, Top.Thickness, Top.Gap, Bottom.*, Left.*, Right.* (같은 4개 접미사), OutlineEnabled, OutlineThickness, OutlineColorHex, OutlineOpacity, Opacity, Scale, Rotation, OffsetX, OffsetY, MonitorSelect, MonitorWarning, HotkeyToggle, HotkeyNext, HotkeyPrev, HotkeyOpenSettings, HotkeyError, Language, StartWithWindows, OverlayVisible, Preview, InfoBar, DisclaimerDialog, DisclaimerOk, TrayOpenSettings, TrayToggleOverlay, TrayPresets, TrayExit`. 숫자 입력은 Slider + TextBox 쌍이고 AutomationId는 TextBox에 붙인다. Rotation은 8개 값을 가진 ComboBox. 트레이 아이콘 툴팁은 `CrosshairManager`. 항상 보이는 컨트롤이 아닌 것: DisclaimerDialog, DisclaimerOk, RenameText, RenameOk, ConfirmDeleteOk, ConfirmDeleteCancel, ShareCodeError, MonitorWarning, HotkeyError, Tray*.
- 포그라운드 가져오기(테스트 배경창과 CLI 배경창 공통): `SetForegroundWindow`를 먼저 시도하고, `GetForegroundWindow()`가 배경창이 아니면 현재 포그라운드 창의 스레드에 `AttachThreadInput` -> `SetWindowPos(HWND_TOP)` -> `SetForegroundWindow` -> 분리 순서로 다시 시도한다. 캡처 전에는 `WindowAt(P)`가 배경창인지 단언하고, 아니면 이유를 담아 실패한다.
- 화면 사용 규칙: 화면 중앙에 창을 띄우는 명령(Desktop/Ui 테스트 실행, `crosshair verify-overlay`, 앱 실행 QA, perf-check)은 세션 전체에서 한 번에 하나만 실행한다. 병렬 웨이브라도 코드 작성은 병렬로 하고 화면을 쓰는 실행만 순서대로 한다.
- CLI `crosshair`(todo 14): `render`, `validate`, `capture`, `compare`, `verify-overlay`, `ico`(정확한 문법은 todo 14). `verify-overlay`가 실제 화면 오버레이 QA의 단일 합격/불합격 판정이다.

### Must NOT have (guardrails, anti-slop, scope boundaries)
- Xbox Game Bar 위젯, Exclusive 전체화면 지원 시도 없음(D1).
- Valorant/CS2 코드 가져오기 없음(D5). 게임별 자동 프로필, 포그라운드 프로세스 검사, 게임 상황에 따라 바뀌는 동작 없음.
- DLL 주입, `SetWindowsHookEx`(저수준 키보드/마우스 훅 포함), 다른 프로세스 메모리 읽기 없음. `src/` 안의 앱 코드는 화면 캡처와 `SendInput`을 하지 않는다(ScreenProbe와 Capture/Backdrop은 Platform에 있지만 Cli와 테스트만 호출하고 App은 참조하지 않음).
- 설치 프로그램/MSIX, 코드 서명, 자동 업데이트, 원격 측정, 네트워크 접근 없음.
- 캡처 제외(display affinity) 기능, HSV 색 사각형, 자유 각도 회전, 추가 도형, 선별 불투명도, 바깥쪽 선 세트, 애니메이션/렌더 루프 없음.
- WinUI, Electron, WPF 창을 오버레이로 쓰는 것 없음.
- 테스트 건너뛰기·삭제·`Skip=` 없음. Desktop/Ui 실행은 건너뜀 0이어야 한다.
- 테스트와 스크립트에 sleep이나 고정 지연 없음. 이벤트, `DwmFlush`, FlaUI `Retry.WhileFalse`, 로그 표지 줄을 기다리되 모두 상한 시간이 있다.
- 테스트에 번역된 문자열을 직접 박지 않는다. 해당 키의 resx 값과 비교한다.

## Verification strategy
> Zero human intervention - all verification is agent-executed.
- 테스트 방침: Core는 TDD(실패하는 xUnit 테스트를 먼저 작성). Desktop/Ui 테스트는 해당 기능과 같은 todo에서 작성한다. 프레임워크: xunit v2(VSTest), FlaUI.UIA3.
- 명령(저장소 루트, Windows PowerShell 5.1):
  - Build: `dotnet build CrosshairManager.sln -c Debug` -> exit 0(경고는 오류로 취급).
  - Core: `dotnet test tests/CrosshairManager.Core.Tests -c Debug --logger "trx;LogFileName=core.trx"`
  - Desktop: `dotnet test tests/CrosshairManager.Desktop.Tests -c Debug --filter Category=Desktop --logger "trx;LogFileName=desktop.trx"`
  - Ui: `dotnet test tests/CrosshairManager.Ui.Tests -c Debug --filter Category=Ui --logger "trx;LogFileName=ui.trx"`
  - 전체: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1` -> `CrosshairManager` 프로세스가 실행 중이면 즉시 실패. 그다음 빌드와 위 세 테스트 명령을 실행하고 각 trx를 파싱해서, 실패나 건너뜀이 하나라도 있거나 trx가 없으면 exit 1.
- 캡처 규칙: 모든 화면 캡처 단언은 앞에 `DwmFlush()`를 호출하고 TestSupport `Eventually.True(predicate, TimeSpan.FromSeconds(2))`로 감싼다. 이 함수는 시도 사이에 `DwmFlush()`를 호출한다(sleep 없음). 모든 캡처는 `BackdropWindow`(#FF00FF, 오버레이 캔버스 + 8 px 크기, 포그라운드로 가져옴, 최상단 아님) 위에서 찍어서 에이전트의 다른 창이 픽셀을 오염시키지 않게 한다.
- 자동으로 앱을 실행할 때는 `settings.json`을 미리 만들어 둔다(`disclaimerAccepted: true`, `settingsWindowPlacement` = 주 모니터의 (40,40,900,620)). 첫 실행 전용 테스트만 예외다.
- 환경(architect 조사, 2026-11-12): 모니터 2대, 주 모니터 `\\.\DISPLAY2`가 (0,0), `\\.\DISPLAY1`이 (1920,0), 둘 다 1920x1080. SDK 10.0.403 + WindowsDesktop 런타임 10.0.14. pwsh 없음(Windows PowerShell 5.1만 있음). DPI 배율과 HDR 상태는 첫 Desktop 테스트가 출력하고 증거에 기록한다. 두 번째 모니터가 필요한 테스트는 첫 번째 비주 모니터를 쓰고, 모니터가 하나뿐이면 메시지와 함께 `Assert.Fail` 한다.
- 증거 경로: `.omo/evidence/task-<N>-crosshair-overlay.<ext>`(ulw-loop에서는 `<attemptDir>/task-<N>-crosshair-overlay.<ext>`; attemptDir은 agentToolkit을 await import(`${env("OMO_AGENT_TOOLKIT_SDK_ROOT")}/sdk.js`)로 불러온 뒤 `await agentToolkit.status()`의 result.currentAttemptDir). PNG는 증거일 뿐이고 합격/불합격은 항상 숫자 비교나 종료 코드로 판정한다.

## Execution strategy
### Parallel execution waves
- Wave 0: 1.
- Wave 1: step A(1 이후 병렬) 2, 4, 7; step B 5, 6 [4 필요].
- Wave 2: step A(병렬) 3 [2, 4 필요], 8 [2, 4 필요], 9 [4, 6, 7 필요], 11 [2, 5 필요]; step B 10 [8 필요], 12 [3 필요].
- Wave 3: 13과 14(둘 다 11, 12 필요; 14는 6, 9도 필요). 코드 작성은 병렬, 화면을 쓰는 실행(13의 Desktop 테스트 실행, 14의 verify-overlay QA)은 화면 사용 규칙에 따라 순서대로.
- Wave 4(App 파일을 공유하므로 순차): 15 -> 16 -> 17 -> 18.
- Wave 5: 19, 20. 작성은 병렬, 화면을 쓰는 실행(19의 perf-check, 20의 게시본 검증·verify.ps1·perf-check)은 순서대로.
- Final: F1-F4 병렬(F3의 화면 사용 실행은 다른 화면 실행과 겹치지 않게).

### Dependency matrix
| Todo | Depends on | Blocks | Can parallelize with |
| --- | --- | --- | --- |
| 1 | - | all | - |
| 2 | 1 | 3, 8, 10, 11 | 4, 5, 6, 7 |
| 3 | 2, 4 | 12 | 8, 9, 11 |
| 4 | 1 | 3, 5, 6, 8, 9, 12, 14 | 2, 7 |
| 5 | 1, 4 | 11, 13, 15 | 2, 6, 7 |
| 6 | 1, 4 | 9, 13, 14, 15 | 2, 5, 7 |
| 7 | 1 | 9, 13, 15 | 2, 4, 5, 6 |
| 8 | 2, 4 | 10, 14, 15, 16 | 3, 9, 11 |
| 9 | 4, 6, 7 | 14, 15, 16 | 3, 8, 11 |
| 10 | 2, 8 | 16 | 12 |
| 11 | 2, 5 | 13, 14 | 3, 8, 9 |
| 12 | 3, 4 | 13, 14, 15, 16 | 10 |
| 13 | 5, 6, 7, 11, 12 | 18 | 14 |
| 14 | 4, 6, 8, 9, 11, 12 | 15, 18, 19, 20 | 13 |
| 15 | 5, 6, 7, 8, 9, 12, 14 | 16 | - |
| 16 | 6, 7, 8, 9, 10, 12, 15 | 17 | - |
| 17 | 16 | 18 | - |
| 18 | 13, 14, 15, 16, 17 | 19, 20 | - |
| 19 | 14, 18 | F1-F4 | 20 |
| 20 | 14, 18 | F1-F4 | 19 |

## Todos
> Implementation + Test = ONE todo. Never separate.
<!-- APPEND TASK BATCHES BELOW THIS LINE WITH edit/apply_patch - never rewrite the headers above. -->
- [ ] 1. 저장소, 툴체인, 솔루션, 인터롭 목록, 계약 문서 뼈대 만들기
  Recommended task executor category: unspecified-low - 여러 파일을 만드는 기계적인 뼈대 작업이고 모든 선택은 Must have에 정해져 있다.
  What to do: `E:/MyData/Projects/CrosshairManager`에서 `git init`; `dotnet new gitignore` 후 `artifacts/`, `TestResults/`, `.omo/evidence/` 추가; `.editorconfig`(C# 기본값, 들여쓰기 4칸); `global.json`; `Directory.Build.props`; `LICENSE`(MIT 원문, `Copyright (c) 2026 ButterCoconut`); `dotnet new sln --format sln -n CrosshairManager`; Must have 구조의 프로젝트 10개를 `dotnet new classlib|wpf|console|xunit`로 만들고 TFM/AssemblyName을 정확히 설정, 모두 `CrosshairManager.sln`에 추가, 의존 규칙대로 참조 연결. 패키지: Platform·TestSupport·SendHotkey에 `Microsoft.Windows.CsWin32`(PrivateAssets all); Platform에 `Microsoft.Win32.SystemEvents`(10.x 최신; WPF가 아닌 프로젝트는 WindowsDesktop 참조 팩을 쓸 수 없음); App에 `CommunityToolkit.Mvvm`, `H.NotifyIcon.Wpf`; 모든 테스트 프로젝트에 `xunit` 2.9.x + `xunit.runner.visualstudio` 3.x + `Microsoft.NET.Test.Sdk` 17.x(템플릿이 만든 것은 교체; xunit.v3 금지); Ui.Tests에 `FlaUI.UIA3`. App과 Cli에 Must have의 `app.manifest`. Platform `NativeMethods.txt` - 함수: CreateWindowEx, RegisterClassEx, UnregisterClass, DefWindowProc, GetMessage, TranslateMessage, DispatchMessage, PostMessage, PostQuitMessage, PostThreadMessage, ShowWindow, SetWindowPos, GetWindow, GetWindowRect, IsWindow, IsWindowVisible, DestroyWindow, UpdateLayeredWindow, CreateDIBSection, CreateCompatibleDC, DeleteDC, SelectObject, DeleteObject, GetDC, ReleaseDC, BitBlt, EnumDisplayMonitors, GetMonitorInfo, MonitorFromPoint, GetDpiForMonitor, SetProcessDpiAwarenessContext, GetThreadDpiAwarenessContext, AreDpiAwarenessContextsEqual, RegisterHotKey, UnregisterHotKey, SetWinEventHook, UnhookWinEvent, GetForegroundWindow, SetForegroundWindow, AttachThreadInput, GetWindowThreadProcessId, GetCurrentThreadId, WindowFromPoint, GetWindowLongPtr, SetWindowLongPtr, SetTimer, KillTimer, DwmFlush, FillRect, CreateSolidBrush, BeginPaint, EndPaint; 열거형 타입(멤버 이름이 아니라 타입 이름으로 적음): WINDOW_STYLE, WINDOW_EX_STYLE, SET_WINDOW_POS_FLAGS, SHOW_WINDOW_CMD, UPDATE_LAYERED_WINDOW_FLAGS, HOT_KEY_MODIFIERS, GET_WINDOW_CMD, WINDOW_LONG_PTR_INDEX, ROP_CODE, DIB_USAGE, MONITOR_DPI_TYPE; #define 상수: WM_HOTKEY, WM_TIMER, WM_APP, WM_DESTROY, HWND_TOPMOST, HWND_TOP, HWND_MESSAGE, AC_SRC_OVER, AC_SRC_ALPHA, EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT. 규칙: CsWin32가 어떤 이름을 찾지 못했다고 경고하면(경고는 빌드 오류) 그 이름을 지우고 그것을 담은 열거형 타입 이름으로 바꾼다. TestSupport `NativeMethods.txt`: CreateWindowEx, RegisterClassEx, DefWindowProc, DestroyWindow, IsWindow, IsWindowVisible, GetWindowRect, GetWindowLongPtr, SetForegroundWindow, GetForegroundWindow, AttachThreadInput, GetWindowThreadProcessId, GetCurrentThreadId, SetWindowPos, SendInput, DwmFlush, FillRect, CreateSolidBrush, BeginPaint, EndPaint, PeekMessage, TranslateMessage, DispatchMessage, GetThreadDpiAwarenessContext, AreDpiAwarenessContextsEqual, SetProcessDpiAwarenessContext, INPUT, KEYBDINPUT, HWND_TOP. SendHotkey `NativeMethods.txt`: SendInput, INPUT, KEYBDINPUT, VIRTUAL_KEY. Desktop/Ui 테스트 어셈블리에 병렬 실행 끄기 특성과 ModuleInitializer(Must have). 템플릿의 `UnitTest1.cs` 삭제. `scripts/build.ps1`, `scripts/verify.ps1`(동작은 Verification strategy; Windows PowerShell 5.1 호환). `docs/contracts.md`(한국어): Must have의 모델 표, 처리 순서, 도형 규칙, 설정 스키마, AutomationId, 단축키 기본값, todo 14의 CLI 문법을 옮겨 적고 해석된 패키지 버전을 기록. H.NotifyIcon.Wpf가 복원되고 TaskbarIcon이 있는 WPF 앱이 빌드와 단일 파일 게시까지 되는지 확인(`dotnet publish src/CrosshairManager.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o "$env:TEMP\chm-pubprobe"` 후 그 폴더 삭제). 복원이나 게시가 실패하면 패키지를 빼고, todo 15가 CsWin32의 `Shell_NotifyIcon`으로 `Platform/Tray/NativeTrayIcon.cs`에 트레이를 구현해야 한다고 contracts.md에 기록(Platform NativeMethods.txt에 Shell_NotifyIcon, NOTIFYICONDATAW, TrackPopupMenu, CreatePopupMenu, AppendMenu, DestroyMenu, LoadImage 추가).
  Must NOT do: 기능 코드 금지. 목록에 없는 패키지 금지.
  Closes: GAP-12
  Parallelization: Wave 0 | Blocked by: - | Blocks: all
  References: 초안 `.omo/drafts/crosshair-overlay.md`(D1-D8); CsWin32 https://github.com/microsoft/CsWin32 ; H.NotifyIcon https://github.com/HavenDV/H.NotifyIcon ; 단일 파일 https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
  Acceptance criteria: `dotnet build CrosshairManager.sln -c Debug` exit 0(CsWin32 경고 0); `dotnet sln CrosshairManager.sln list`가 정확히 10개 프로젝트를 나열; `dotnet test CrosshairManager.sln --list-tests` exit 0; `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1` exit 0(테스트 0개, 건너뜀 0); `Select-String -Path src/CrosshairManager.Core/obj/project.assets.json -Pattern 'CsWin32|WindowsDesktop'` 결과 없음; `git rev-parse --is-inside-work-tree`가 `true`.
  QA scenarios: happy - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1; $LASTEXITCODE` -> `0`, 출력을 `.omo/evidence/task-1-crosshair-overlay.txt`에 저장. failure - `C:\Windows\System32\cmd.exe`를 `$env:TEMP\chm-fake\CrosshairManager.exe`로 복사해 `/k`로 실행한 뒤 `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1` -> `already running`을 포함한 메시지와 exit 1; 가짜 프로세스 종료(`Stop-Process -Name CrosshairManager`), `$env:TEMP\chm-fake` 삭제, `Get-Process CrosshairManager -ErrorAction SilentlyContinue`가 비었는지 확인; 같은 증거 파일에 덧붙임.
  Commit: Y | chore: scaffold CrosshairManager solution

- [ ] 2. Core 모델, Rgb 파싱, Normalize, ApplyScale (TDD)
  Recommended task executor category: quick - 고정된 표에 따른 레코드와 범위 자르기.
  What to do: `src/CrosshairManager.Core/Model/`: `Rgb`(readonly record struct; `Parse/TryParse`가 `#RRGGBB`, `RRGGBB`, `#RGB`를 받음; `ToString()`은 대문자 `#RRGGBB`), `DotSpec`, `LineSpec`, `OutlineSpec`, Must have 모델 표 그대로의 `CrosshairSpec`, `CrosshairSpec.Default`, `Normalize()` -> `(CrosshairSpec Spec, IReadOnlyList<string> Warnings)`, 처리 순서에 맞는 `ApplyScale()`. 테스트를 먼저 `tests/CrosshairManager.Core.Tests/Model/`에 작성.
  Must NOT do: JSON 금지(todo 8), 도형 계산 금지.
  Closes: GAP-3 (모델)
  Parallelization: Wave 1 step A | Blocked by: 1 | Blocks: 3, 8, 10
  References: Must have 모델 표와 처리 순서.
  Acceptance criteria: Core 테스트 통과, 다음을 포함: 숫자 필드마다 양쪽 경계에서 잘리고 경고 문구가 정확함; 무작위 사양 200개(seed 12345)에서 Normalize 반복 적용 결과 동일; 회전 30 -> 45, 22 -> 0, 22.5 -> 45, 350 -> 0(360 경유), -45 -> 315; `Rgb.TryParse("#0f0")` == #00FF00; `TryParse("#GG0000")` false; ApplyScale 200이 Size/Length/Thickness/Gap을 두 배로; Length 512를 200%로 하면 1024(다시 자르지 않음); Thickness 1을 25%로 하면 1.
  QA scenarios: happy - `dotnet test tests/CrosshairManager.Core.Tests --filter FullyQualifiedName~Model` -> `.omo/evidence/task-2-crosshair-overlay.txt`(Failed 0). failure - 같은 파일에 `--list-tests`로 `#GG0000` 테스트와 범위 경고 테스트가 있음을 기록(이 테스트들이 거부 경로를 단언).
  Commit: Y | feat(core): crosshair model and normalization

- [ ] 3. Core 도형 계산: CrosshairLayout + OverlayPlacement (TDD)
  Recommended task executor category: deep-low - 홀짝 조합별 픽셀 단위 스냅 계산; 테스트로 판정된다.
  What to do: `src/CrosshairManager.Core/Geometry/`(IntRect는 todo 4에서 이미 만듦): `ShapeRole` {Dot,Top,Bottom,Left,Right}, `GeomRect`, `CrosshairGeometry`(CanvasW, CanvasH, PivotX, PivotY, AnchorX, AnchorY, Fills, OutlineThickness, RotationDeg, Color, OutlineColor, DotAlpha, LineAlpha, OutlineAlpha), `CrosshairLayout.Compute(CrosshairSpec scaled, bool oddWidth, bool oddHeight)`, `OverlayPlacement.Compute(CrosshairGeometry g, IntRect monitorPhysical, int offsetX, int offsetY) -> (IntRect Window, IntRect CanvasRegion)`(모니터 범위로 자름; 완전히 벗어나면 둘 다 비어 있음). Must have 도형 규칙 그대로 구현; 꺼진 부분과 길이 0인 선은 사각형을 만들지 않음; 모든 부분이 꺼지면 CanvasW = CanvasH = 0.
  Must NOT do: 래스터화 금지, Windows API 금지.
  Closes: GAP-2, GAP-11
  Parallelization: Wave 2 step A | Blocked by: 2, 4 | Blocks: 12
  References: Must have 도형 규칙.
  Acceptance criteria: 다음 경우의 GeomRect 목록을 정확히 단언: 기본값의 짝/짝과 홀/홀; 점만 있는 크기 1-4의 양쪽 홀짝; T자형(Top 없음); gap 0; gap -2; scale 200; 모두 끔 -> 0x0 캔버스와 빈 창; 모니터 (1920,0,1920,1080)에 오프셋 (0,0) -> P = (2880,540)이 되도록 중앙 배치; 캔버스 절반을 밀어내는 오프셋 -> 잘린 CanvasRegion; 캔버스 완전히 왼쪽에 있는 모니터 -> 빈 Window; 짝수 홀짝의 짝수 두께 대칭 사양 -> P 기준 좌우상하 대칭; 네 방향 빈칸 동일: builtin.classic1px(T1)와 builtin.cross(T2)를 짝/짝과 홀/홀 모두에서 계산하면 중심 띠에서 Top/Bottom/Left/Right 선까지의 빈 픽셀 수가 네 방향 모두 같음(1920x1080 주 모니터의 classic1px: Top y[535,539), Bottom y[542,546), Left x[955,959), Right x[962,966), 가로선 행 540, 세로선 열 960); 무작위 사양 200개(seed 12345, 각 축의 두 선 두께가 같은 경우)에서도 같은 성질; 45도 회전 캔버스 >= 회전 전 캔버스 * 1.41(올림).
  QA scenarios: happy - `dotnet test tests/CrosshairManager.Core.Tests --filter FullyQualifiedName~Geometry` -> `.omo/evidence/task-3-crosshair-overlay.txt`. failure - 모니터를 완전히 벗어난 경우 빈 Window를 단언하는 테스트(증거에 기록).
  Commit: Y | feat(core): pixel-exact crosshair geometry

- [ ] 4. Core IntRect, PixelBuffer, PNG 인코더, AtomicFile (TDD)
  Recommended task executor category: quick - 작고 독립적인 유틸리티.
  What to do: `Geometry/IntRect`(X,Y,W,H 반열린 구간, `IsEmpty`; todo 3, 5, 6, 11이 사용); `Render/PixelBuffer`(Width, Height, premultiplied BGRA 위에서 아래, stride Width*4, `GetPixel`, 바이트 비교 `Equals`); `Render/Png.Encode(PixelBuffer)`(premultiply 해제한 RGBA8, `ZLibStream`, 256개 항목 표를 쓰는 CRC32 직접 구현)와 이 인코더와 CLI가 만드는 파일을 읽는 `Png.Decode(byte[])`(8비트 RGBA/RGB, 필터 0-4); Must have에 따른 `IO/AtomicFile.WriteAllText/WriteAllBytes`(폴더 생성; 쓸 수 없는 폴더면 `IOException`).
  Must NOT do: Core에서 System.Drawing/WPF 금지.
  Closes: GAP-5 (쓰기 안전성), GAP-12
  Parallelization: Wave 1 step A | Blocked by: 1 | Blocks: 3, 5, 6, 8, 9, 12, 14
  References: PNG 사양 https://www.w3.org/TR/png/ ; File.Replace https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace
  Acceptance criteria: Core: 1x1..33x17 무작위 버퍼의 인코드->디코드 왕복 일치; 시그니처와 IHDR/IEND CRC가 알려진 상수와 일치; 바이트 하나를 뒤집으면 CRC 불일치를 감지; AtomicFile로 v1 다음 v2를 쓰면 대상은 v2, `.bak`은 v1; tmp만 쓰고 Replace를 안 하면 대상은 여전히 v1. Desktop: `PngBitmapDecoder`가 인코더의 3x2 출력을 같은 픽셀로 디코드.
  QA scenarios: happy - Core + Desktop 테스트 실행(필터 `~Png|~AtomicFile`) -> `.omo/evidence/task-4-crosshair-overlay.txt`. failure - 같은 실행의 바이트 뒤집기 CRC 테스트.
  Commit: Y | feat(core): pixel buffer, png codec, atomic file writes

- [ ] 5. Platform OverlayHost: 전용 스레드의 클릭 통과 레이어드 창
  Recommended task executor category: deep-low - 실제 데스크톱에서의 Win32 인터롭과 스레딩.
  What to do: `src/CrosshairManager.Platform/Overlay/OverlayHost.cs`: 메시지 루프를 가진 백그라운드 스레드 "Overlay"; 창 클래스 `CrosshairManager.Overlay`; 오버레이 HWND(Must have 스타일)와 메시지 전용 컨트롤러 HWND; `PostMessage(WM_APP+1)` + 큐로 동작하는 `Invoke(Action)`; `Update(PixelBuffer buffer, IntRect screenRect)`(32bpp 위에서 아래 DIB section, `BLENDFUNCTION{AC_SRC_OVER,0,255,AC_SRC_ALPHA}`로 `UpdateLayeredWindow`; 빈 버퍼나 빈 사각형이면 숨김); `Show()`(SW_SHOWNOACTIVATE), `Hide()`, `IsVisible`, `Handle`, `ControllerHandle`, 이벤트 `Updated`, `VisibilityChanged`; `Dispose()`는 WM_QUIT을 보내고 스레드 종료를 기다림(5초 상한, 넘으면 `TimeoutException`). `Dpi/DpiAwareness.EnsurePerMonitorV2()` 헬퍼.
  Must NOT do: WPF 참조 금지, 전체 화면 창 금지, 타이머로 다시 그리기 금지.
  Closes: GAP-1, GAP-11
  Parallelization: Wave 1 step B | Blocked by: 1, 4 | Blocks: 11, 13, 15
  References: UpdateLayeredWindow https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow ; 클릭 통과 https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features ; 확장 스타일 https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles
  Acceptance criteria: Desktop 테스트: DPI 인식 단언 테스트(Must have); 8x8 불투명 빨간 버퍼를 (100,100)에 -> `Updated` 대기(5초) -> `GetWindowRect` == (100,100,108,108), 보임, GWL_EXSTYLE에 5개 플래그 모두; Show로 포그라운드 HWND가 바뀌지 않음; 빈 버퍼 -> 숨김; Dispose 후 5초 안에 `IsWindow(handle)` false.
  QA scenarios: happy - `dotnet test tests/CrosshairManager.Desktop.Tests --filter "Category=Desktop&FullyQualifiedName~OverlayHost"` -> `.omo/evidence/task-5-crosshair-overlay.txt`, 건너뜀 0. failure - 같은 실행의 빈 버퍼 숨김 테스트.
  Commit: Y | feat(platform): layered click-through overlay host

- [ ] 6. Platform MonitorService (다중 모니터, 물리 좌표)
  Recommended task executor category: quick - EnumDisplayMonitors 래퍼.
  What to do: Core `Settings/MonitorSelection` 레코드(`Mode` Primary|Device, `DeviceName`); Platform `Monitors/MonitorService`: `GetMonitors()` -> `MonitorInfo`(DeviceName, Bounds, WorkArea, IsPrimary, GetDpiForMonitor MDT_EFFECTIVE_DPI로 얻은 DpiX, DpiY), `Resolve(MonitorSelection, out bool fellBack)`, `SystemEvents.DisplaySettingsChanged`에서 발생하는 이벤트 `Changed`.
  Must NOT do: 모니터를 순번으로 식별하지 않는다.
  Closes: GAP-1
  Parallelization: Wave 1 step B | Blocked by: 1, 4 | Blocks: 9, 13, 15
  References: EnumDisplayMonitors https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors
  Acceptance criteria: Desktop 테스트가 모니터(장치, 좌표, DPI)를 출력하고 (0,0)에 있는 주 모니터가 정확히 하나임을 단언; `Resolve(Device "\\.\DISPLAY_NOPE")` -> 주 모니터, fellBack true; 첫 번째 비주 모니터를 지정하면 그 모니터, fellBack false.
  QA scenarios: happy - 모니터 목록 출력이 포함된 실행 -> `.omo/evidence/task-6-crosshair-overlay.txt`. failure - 같은 실행의 대체 테스트.
  Commit: Y | feat(platform): monitor enumeration and selection

- [ ] 7. Core HotkeyGesture + Platform GlobalHotkeys(전용 메시지 창) + TestSupport Input
  Recommended task executor category: unspecified-low - 파서와 RegisterHotKey 래퍼.
  What to do: Core `Settings/HotkeyGesture`: `TryParse`(수정키 Ctrl/Alt/Shift/Win 순서 무관, 대소문자 무관; 키 A-Z, 0-9, F1-F24, Left/Right/Up/Down, Home/End/PageUp/PageDown/Insert/Delete, NumPad0-9; F13-F24를 빼면 수정키 1개 이상 필수), 정규 `ToString()` 순서 Ctrl+Alt+Shift+Win+Key, `Modifiers`(MOD_* 값), `VirtualKey`. Platform `Hotkeys/GlobalHotkeys`: 전용 백그라운드 스레드 + 메시지 전용 창; `TryRegister(string id, HotkeyGesture g, out int win32Error)`(자기 스레드로 넘겨 처리, MOD_NOREPEAT 추가), `Unregister(id)`, 이벤트 `Pressed(string id)`, `Dispose`. TestSupport `Input.SendChord(HotkeyGesture)`(SendInput 키 누름/뗌 순서)도 이 todo에서 만든다.
  Must NOT do: 저수준 훅 금지, OverlayHost 의존 금지.
  Closes: GAP-6
  Parallelization: Wave 1 step A | Blocked by: 1 | Blocks: 9, 13, 15
  References: RegisterHotKey https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey ; 가상 키 https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
  Acceptance criteria: Core: `"shift+ctrl+x"` -> `Ctrl+Shift+X`; `"X"`, `"Ctrl+"`, `"Ctrl+Foo"` 거부; `"F13"` 허용. Desktop: `Ctrl+Alt+Shift+F11` 등록 후 `Input.SendChord`로 입력, `Pressed` 대기(5초); 두 번째 인스턴스가 같은 조합을 등록하면 false와 오류 1409.
  QA scenarios: happy - 테스트 출력 -> `.omo/evidence/task-7-crosshair-overlay.txt`. failure - 중복 등록 테스트.
  Commit: Y | feat: hotkey gesture parsing and global hotkeys

- [ ] 8. Core 프리셋: 스키마 v1, 소스 생성 JSON, PresetStore, 내장 프리셋, 마이그레이션
  Recommended task executor category: unspecified-high - 버전 관리와 실패 경로를 가진 저장 계약.
  What to do: `Presets/Preset`(Id, Name, CreatedUtc, ModifiedUtc, Crosshair, IsBuiltIn), `PresetJson`(JsonSerializerContext, camelCase, Rgb는 "#RRGGBB"), `PresetFile.Load(string json)` -> `Ok(Preset, warnings) | NewerVersion(int) | Broken(reason)`, `Migrations`(int -> Func<JsonNode,JsonNode>; v1에서는 비어 있지만 `< current`면 체인을 실행), `BuiltInPresets`(Must have의 임베디드 JSON 6개; Name에는 resx 키), `PresetStore(string dataDir)`: `LoadAll()`(Must have 정렬), `Create(name, spec)`, `Save`, `Rename`, `Duplicate`, `Delete`(파일 + `.bak`), `ImportFile(path)`(Id 충돌 시 새 GUID), `ExportFile(id, path)`; 내장 프리셋을 바꾸려 하면 `InvalidOperationException`. 픽스처 `tests/fixtures/presets/v1.json`.
  Must NOT do: NewerVersion/Broken 파일은 절대 다시 쓰지 않는다.
  Closes: GAP-4, GAP-5, GAP-7
  Parallelization: Wave 2 step A | Blocked by: 2, 4 | Blocks: 10, 14, 15, 16
  References: Must have 프리셋 스키마.
  Acceptance criteria: 내장 6개와 무작위 사양 50개의 왕복 일치; 픽스처 로드; `schemaVersion: 2` 파일 -> NewerVersion, LoadAll 후 SHA-256 그대로; 깨진 JSON -> Broken; 범위 초과 -> Ok + 경고; 없는 선택 필드 -> 기본값; 내장 Delete는 예외; 충돌하는 가져오기 -> 새 Id; 내보내기/가져오기 왕복 일치; CreatedUtc가 같으면 Id 순.
  QA scenarios: happy - 테스트 출력 -> `.omo/evidence/task-8-crosshair-overlay.txt`. failure - NewerVersion 파일 미변경(해시) 테스트.
  Commit: Y | feat(core): versioned preset storage and built-in presets

- [ ] 9. Core AppSettings, SettingsStore, DataDirectory
  Recommended task executor category: quick - todo 8과 같은 패턴의 스키마 하나.
  What to do: Must have에 따른 `Settings/AppSettings`(기본값: `builtin.default`, overlayVisible true, 주 모니터, 단축키 기본값, language `system`, startWithWindows false, disclaimerAccepted false, placement null); `SettingsStore(dataDir)`: `Load()` -> `(AppSettings, SettingsLoadStatus Ok|CreatedDefault|RecoveredFromCorrupt(backupPath)|NewerVersion)`, `Save`(원자적; IOException을 그대로 전달); 잘못된 단축키 문자열 -> 그 단축키는 null + 경고; Must have에 따른 `DataDirectory.Resolve(string[] args, IDictionary env)`와 `DataDirectory.Normalize(path)`.
  Must NOT do: 레지스트리 금지.
  Closes: GAP-5, GAP-6
  Parallelization: Wave 2 step A | Blocked by: 4, 6, 7 | Blocks: 15, 16
  References: Must have 설정 스키마, 데이터 폴더, 단일 인스턴스 정규화.
  Acceptance criteria: 파일 없음 -> CreatedDefault; 손상 -> RecoveredFromCorrupt, 백업에 원본 바이트; 새 버전 -> 미변경; 우선순위 인자 > 환경변수 > 기본값; Normalize(`C:\a\B\`) == `C:\A\B`.
  QA scenarios: happy/failure - 손상 경우를 포함한 테스트 출력 -> `.omo/evidence/task-9-crosshair-overlay.txt`.
  Commit: Y | feat(core): app settings persistence

- [ ] 10. Core 공유 코드 CHM1
  Recommended task executor category: quick - 검증을 포함한 인코드/디코드.
  What to do: Must have에 따른 `Presets/ShareCode.Encode(string name, CrosshairSpec spec)`와 `TryDecode(string code, out string name, out CrosshairSpec spec, out IReadOnlyList<string> warnings, out string errorKey)`; 정규 JSON은 기본값과 같은 값을 생략; 앞뒤 공백 제거; 디코드한 사양은 Normalize.
  Must NOT do: 네트워크 금지, raw deflate 외 압축 금지.
  Closes: GAP-7
  Parallelization: Wave 2 step B | Blocked by: 2, 8 | Blocks: 16
  References: Must have 공유 코드 규칙.
  Acceptance criteria: 내장 6개 왕복 일치; 기본 프리셋 코드 길이 < 200; 거부 경로마다 서로 다른 오류 키(`CHM2:x` Prefix, `CHM1:!!!` Base64, 쓰레기 데이터의 올바른 base64 Inflate, 압축된 비JSON Json, 16 KB 초과 TooLarge).
  QA scenarios: happy - 인코드한 기본 코드를 포함한 출력 -> `.omo/evidence/task-10-crosshair-overlay.txt`. failure - 거부 테스트 5개.
  Commit: Y | feat(core): preset share codes

- [ ] 11. Platform TopmostKeeper + ScreenProbe; TestSupport BackdropWindow, Eventually, Composite
  Recommended task executor category: deep-low - WinEvent 훅, z-order, 캡처 동작.
  What to do: Must have에 따른 Platform `Overlay/TopmostKeeper(OverlayHost)`(Invoke로 오버레이 스레드에서 훅 + 타이머). Platform `Capture/ScreenProbe.Capture(IntRect)`(DwmFlush, GetDC(NULL) + BitBlt SRCCOPY|CAPTUREBLT로 32bpp DIB에 복사, 알파는 255로 고정)와 `WindowAt(int x,int y)`. Platform `Capture/Backdrop(IntRect rect, Rgb color)`(전용 스레드, 최상단이 아닌 일반 팝업, 단색 칠하기, Must have 포그라운드 가져오기 규칙을 따르는 `BringToFront()`, `Handle`, Dispose; Cli와 테스트만 사용). TestSupport: Platform `Capture/Backdrop`을 감싸는 `BackdropWindow`, `Eventually.True(Func<bool>, TimeSpan)`(Must have 캡처 규칙), `Composite.Over(PixelBuffer premul, Rgb backdrop) -> PixelBuffer`.
  Must NOT do: App이 ScreenProbe를 참조하지 않는다.
  Closes: GAP-11, GAP-12
  Parallelization: Wave 2 step A | Blocked by: 2, 5 | Blocks: 13, 14
  References: SetWinEventHook https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook ; BitBlt https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt ; DwmFlush https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush ; AttachThreadInput https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-attachthreadinput
  Acceptance criteria: Desktop: BackdropWindow 위에 오버레이(8x8 빨강); `BringToFront()` 후 `WindowAt(center)` == 배경창 HWND; 위에 최상단 테스트 창을 띄우면 `Reasserted` 대기(3초) -> Eventually 캡처가 빨간 블록; 위에 아무 창도 없이 타이머 3회가 지나면 `Reasserted` 횟수 0; keeper를 Dispose한 뒤 같은 덮기 시나리오면 캡처 != 빨강.
  QA scenarios: happy - 출력 -> `.omo/evidence/task-11-crosshair-overlay.txt`. failure - keeper Dispose 테스트와 불필요한 재설정 없음 테스트.
  Commit: Y | feat(platform): topmost maintenance and screen probe

- [ ] 12. Core CrosshairRasterizer + ASCII 기준 그림과 성질 테스트
  Recommended task executor category: deep-low - 정확한 래스터 규칙; 기준 그림과 성질로 판정된다.
  What to do: Must have에 따른 `Render/CrosshairRasterizer.Render(CrosshairGeometry)`와 `Render(g, IntRect canvasRegion)`; TestSupport.Core `AsciiArt.From(PixelBuffer)`(채움 `X`, 외곽선 `o`, 빈칸 `.`, 부분 `+`)와 `GoldenComparer`(처음 다른 행/열 보고). 기준 그림 `tests/CrosshairManager.Core.Tests/Goldens/*.txt`: 기본 짝수, 기본 홀수, 점만 1/2/3, classic1px, T자형, classic1px 짝/짝과 홀/홀(교차점부터 네 방향 빈칸이 같음), gap 0, gap -2, 외곽선 3, 1px 틈을 메우는 외곽선(점 2, L2 T2 G2, 외곽선 1: 점과 선 사이 빈 1px을 외곽선이 채움), 잘린 영역, 90도 회전한 T자형(Right 팔이 없음 = 시계 방향), 45도 회전한 십자.
  Must NOT do: 90의 배수 회전에서 슈퍼샘플링 금지.
  Closes: GAP-2
  Parallelization: Wave 2 step B | Blocked by: 3, 4 | Blocks: 13, 14, 15, 16
  References: Must have 래스터라이저 규칙.
  Acceptance criteria: 기준 그림 모두 통과; 성질: 짝수 홀짝의 짝수 두께 대칭 사양은 대칭 출력; 선 두께가 축마다 같은 사양은 홀수 두께여도 네 방향 빈칸 수가 같음; 대칭 사양의 90도 회전 == 0도의 전치; 알파가 round(255*Opacity/100)를 넘지 않음; 투명하지 않은 픽셀은 모두 캔버스 안; 45도 회전 출력이 두 번 실행해도 바이트 단위로 같음; 일부러 바꾼 기준 문자열에 대해 `GoldenComparer`가 차이 위치를 보고.
  QA scenarios: happy - 기본 기준 그림을 붙인 출력 -> `.omo/evidence/task-12-crosshair-overlay.txt`. failure - 비교기 자체 테스트.
  Commit: Y | feat(core): pixel-perfect crosshair rasterizer

- [ ] 13. 데스크톱 종단 테스트: 픽셀 일치, 클릭 통과, z-order, 단축키, 두 번째 모니터
  Recommended task executor category: deep-low - Core와 Platform을 묶은 실제 데스크톱 테스트.
  What to do: Desktop 테스트(모두 BackdropWindow 위, 캡처 규칙 준수): (a) builtin.default, builtin.dot, builtin.classic1px를 100%로 -> 처리 순서 -> OverlayHost.Update -> 캡처 == #FF00FF 위 합성 결과와 정확히 일치; (b) 불투명도 50% 사양은 채널당 +-1 이내; (c) builtin.x45는 +-2 이내; (d) WindowAt(P) == 배경창 HWND; (e) z-order 복구; (f) GlobalHotkeys `Ctrl+Alt+Shift+F12`로 숨김 -> 캡처 == 배경색만; (g) 첫 번째 비주 모니터에 배치하면 그 모니터에서 일치. 각 경우 `expected.png`/`actual.png`를 `TestResults/desktop/<case>/`에 저장.
  Must NOT do: sleep 금지, 건너뛰기 금지.
  Closes: GAP-1, GAP-2, GAP-11
  Parallelization: Wave 3 | Blocked by: 5, 6, 7, 11, 12 | Blocks: 18
  References: Must have(모델, 도형, 래스터라이저, 캡처 규칙).
  Acceptance criteria: `dotnet test tests/CrosshairManager.Desktop.Tests --filter Category=Desktop` 모두 통과, 건너뜀 0; (a)-(g)의 PNG 쌍 존재. 실행은 화면 사용 규칙에 따라 todo 14의 화면 QA와 겹치지 않게 한다.
  QA scenarios: happy - trx 요약 + PNG 경로 -> `.omo/evidence/task-13-crosshair-overlay.txt`. failure - (f)가 숨긴 뒤 배경색만 보이는지 단언.
  Commit: Y | test(desktop): end-to-end overlay pixel and behavior tests

- [ ] 14. CLI `crosshair`: render, validate, capture, compare, verify-overlay, ico
  Recommended task executor category: unspecified-low - 기존 Core/Platform API를 감싸는 명령.
  What to do: `tools/CrosshairManager.Cli`(EnsurePerMonitorV2 호출 + manifest):
  - `render <presetIdOrJsonPath> <out.png> [--zoom 1..32 (8)] [--bg #RRGGBB] [--odd] [--data-dir <dir>]`
  - `validate <jsonPath>` -> `OK`/경고 출력 후 exit 0, `BROKEN: <reason>` exit 2, `NEWER: <v>` exit 3
  - `capture <out.png> [--monitor <device>] [--size N (64)]`(중앙 영역)
  - `compare <expected.png> <actual.png> [--tolerance N (0)]` -> 허용 오차 안이면 exit 0, 다르면 1(개수와 첫 차이 출력)
  - `verify-overlay [--data-dir <dir>] [--preset <id>] [--monitor <device>] [--expect-hidden] [--timeout <s> (5)] [--out <dir>]` -> 데이터 폴더의 설정(또는 플래그)에서 활성 프리셋과 모니터를 읽고 예상 창 위치를 계산, todo 11에서 만든 Platform `Capture/Backdrop`으로 #FF00FF 배경창을 위치 + 8 px로 띄우고 Must have 포그라운드 가져오기 규칙으로 앞에 놓은 뒤 `WindowAt(P)`가 배경창인지 확인, Eventually 방식으로 캡처해 배경 위 Core 렌더 합성과 비교(정확 일치; 45도 회전은 허용 오차 2), `--expect-hidden`이면 배경색만인지 비교; 예상/실제 PNG를 `--out`에 저장; 일치 exit 0, 불일치 1, 사용법 오류 2
  - `ico <out.ico>`(builtin.default를 16/32/48 투명 캔버스에 맞춰 그리고 32bpp BMP/DIB 항목으로 ICO 작성)
  사용법 오류는 사용법 문구와 exit 2(validate의 BROKEN도 exit 2를 쓰되 `BROKEN:`을 출력).
  Must NOT do: 외부 인자 파서 금지, App이 Cli를 참조하지 않음.
  Closes: GAP-12, GAP-1 (QA 표면)
  Parallelization: Wave 3 | Blocked by: 4, 6, 8, 9, 11, 12 | Blocks: 15, 18, 19, 20
  References: Must have 처리 순서, 캡처 규칙, 포그라운드 가져오기 규칙.
  Acceptance criteria: `dotnet run --project tools/CrosshairManager.Cli -- render builtin.default out.png --zoom 8 --bg '#202020'`(PowerShell에서 `#`은 주석이므로 따옴표 필수) exit 0, `Png.Decode` 성공; `validate tests/fixtures/presets/v1.json` -> `OK` exit 0; 깨진 파일 -> exit 2; `compare a.png a.png` exit 0, 1px 바꾼 사본과는 exit 1; `ico x.ico`의 헤더가 이미지 3개를 보고; 앱이 없을 때 `verify-overlay --expect-hidden` exit 0. 화면을 쓰는 실행은 todo 13의 Desktop 테스트 실행과 겹치지 않게 한다.
  QA scenarios: happy - 출력 + out.png -> `.omo/evidence/task-14-crosshair-overlay.{txt,png}`. failure - `render nope.json x.png` exit 2; 앱이 없을 때 `verify-overlay`(숨김 아님) exit 1.
  Commit: Y | feat(cli): render, compare and overlay verification commands

- [ ] 15. App 셸: 구성 루트, Loc 기반, OverlayController, 트레이, 단일 인스턴스, 시작
  Recommended task executor category: unspecified-high - Core, Platform, WPF를 잇는 여러 파일 배선.
  What to do: `App.xaml(.cs)`(ShutdownMode OnExplicitShutdown): 인자 `--data-dir`, `--settings`, `--hidden`; DataDirectory; 단일 인스턴스(Must have); SettingsStore/PresetStore를 상태와 함께 로드 -> 대기 중인 InfoBar 메시지; OverlayHost + TopmostKeeper + MonitorService + GlobalHotkeys; `OverlayController.Apply(CrosshairSpec)`(Dispatcher Background 우선순위로 합침; 처리 순서; 모니터에서 홀짝; MonitorService.Changed 때 다시 실행); 표시 우선순위(Must have); 단축키 동작: 켜기/끄기(overlayVisible 저장), 다음/이전(activePresetId 저장), 설정 열기; todo 16이 쓰는 `IAppShell` 경계: `OpenSettings()`, `Apply(spec)`, `Presets`, `Settings`, `SaveSettings()`, `HotkeyStatus`(id -> 등록됨/오류 코드), `RebindHotkey(id, gesture)`, `SetStartWithWindows(bool)`, `PendingMessages`; `Localization/Loc` + 이 todo가 쓰는 트레이/InfoBar/오류 키를 담은 `Resources/Strings.resx`(영어); `Tray/TrayIcon`(H.NotifyIcon.Wpf, todo 1이 대체안을 기록했다면 NativeTrayIcon)의 메뉴 항목에 TrayOpenSettings/TrayToggleOverlay/TrayPresets(라디오 하위 메뉴)/TrayExit AutomationId, 툴팁 `CrosshairManager`; `crosshair ico`로 만든 `Assets/app.ico`를 커밋하고 `ApplicationIcon`으로도 지정; `Startup/AutoStart`(Must have); 표지 줄을 쓰는 `Logging/FileLog`; 쓸 수 없는 데이터 폴더 처리(Must have); Dispatcher 미처리 예외는 기록하고 메시지 상자를 띄운 뒤 계속 실행. `tests/tools/SendHotkey` 프로그램: `SendHotkey <gesture>`가 SendInput으로 조합을 보내고 exit 0. 자리표시 `SettingsWindow`는 여기서 만들지 않는다. `OpenSettings()`는 todo 16이 구독할 이벤트를 발생시킨다(그 전까지는 `settings requested`를 기록).
  Must NOT do: 설정 컨트롤 금지, ScreenProbe 참조 금지, App에서 SendInput 금지.
  Closes: GAP-1, GAP-5, GAP-6, GAP-9
  Parallelization: Wave 4 | Blocked by: 5, 6, 7, 8, 9, 12, 14 | Blocks: 16
  References: H.NotifyIcon https://github.com/HavenDV/H.NotifyIcon ; Must have 앱/시작/단일 인스턴스/데이터 폴더 규칙.
  Acceptance criteria (Windows PowerShell, `$d = "$env:TEMP\chm-qa15"`, settings.json 미리 생성): `Start-Process src/CrosshairManager.App/bin/Debug/net10.0-windows10.0.19041.0/CrosshairManager.exe -ArgumentList "--data-dir `"$d`""`; 게이트: `Select-String "$d\logs\app.log" 'overlay shown'`을 확인하고 READY를 출력하는 `tool.monitor` 명령 루프, 10초 상한; `crosshair verify-overlay --data-dir $d` exit 0; `SendHotkey Ctrl+Alt+Shift+X` -> 로그 `overlay hidden` -> `verify-overlay --data-dir $d --expect-hidden` exit 0; `SendHotkey Ctrl+Alt+Shift+X`를 다시 보낸 뒤 `SendHotkey Ctrl+Alt+Shift+Right` -> `verify-overlay --data-dir $d --preset builtin.dot` exit 0이고 settings.json의 `activePresetId == builtin.dot`; 같은 데이터 폴더로 두 번째 실행 시 3초 안에 exit 0, 로그에 `settings requested`.
  QA scenarios: happy - 전체 출력 + verify-overlay PNG -> `.omo/evidence/task-15-crosshair-overlay.txt`; 정리: `Stop-Process -Name CrosshairManager`, `Get-Process CrosshairManager -ErrorAction SilentlyContinue`가 비었는지 확인, `$d` 삭제. failure 1 - `settings.json` 손상(`Set-Content $d\settings.json '{'`) -> 앱이 시작되고 `settings.json.corrupt-*`가 생기며 `verify-overlay` exit 0. failure 2 - 시작 전 `icacls $d /deny "$($env:USERNAME):(W)"` -> 대체 로그 `%TEMP%\CrosshairManager-fallback.log`에 `data dir read-only`와 `overlay shown`(게이트는 위와 같음), `Get-Process CrosshairManager` 살아 있음, `verify-overlay --data-dir $d` exit 0; 그 뒤 `icacls $d /remove:d $env:USERNAME`, 프로세스 종료, 대체 로그 삭제.
  Commit: Y | feat(app): overlay controller, tray, hotkeys and single instance

- [ ] 16. 설정 창, 프리셋 영역, 실시간 미리보기, 색 편집기, 단축키 상자, 안내 + Ui 테스트 픽스처
  Recommended task executor category: visual-engineering - MVVM과 고정 AutomationId를 갖춘 WPF UI/UX.
  What to do: `IAppShell` 위의 `ViewModels/`(CommunityToolkit.Mvvm): `SettingsViewModel`(실시간 적용, 400 ms 자동 저장, 내장 프리셋 자동 사본, 경고), `PresetListViewModel`(새로 만들기/복제/이름 변경(RenameText/RenameOk 대화상자)/삭제(ConfirmDeleteOk/Cancel), 필터 `*.json`의 OpenFileDialog/SaveFileDialog로 가져오기/내보내기, CopyShareCode로 클립보드 복사, ShareCodeInput + PasteShareCode와 ShareCodeError), `LineViewModel` 4개 + LinkLines(편집을 모든 선에 반영) + TStyle; Must have에 따른 `Views/SettingsWindow.xaml`(간단: 견본색 8개, SimpleScale, SimpleOpacity, SimpleOutline; 상세 그룹: 색 / 점 / 선 / 외곽선 / 변형(Opacity, Scale, Rotation, OffsetX, OffsetY) / 표시(MonitorSelect, MonitorWarning) / 단축키(HotkeyBox 4개, HotkeyError) / 일반(Language, StartWithWindows, OverlayVisible)); `PreviewControl`; `HotkeyBox`(IME/SystemKey 규칙); `InfoBar`(전체화면 안내는 항상, 대기 메시지, 모니터 대체 경고); `DisclaimerDialog`(전체화면, 안티치트/약관, 단축키 한계 문구; DisclaimerOk가 disclaimerAccepted 저장); 창 위치 저장/복원; 모든 컨트롤에 고정 AutomationId; 새 키는 모두 `Strings.resx`(영어)에 추가. Ui.Tests: `AppFixture`(고유 임시 데이터 폴더 + 미리 만든 설정 + `--settings`로 exe 실행, UIA3 연결, Dispose에서 프로세스 종료 후 HasExited 단언, 폴더 삭제)와 테스트: (u1) Must have에서 "항상 보이는 컨트롤이 아닌 것"을 뺀 모든 고정 AutomationId가 존재하고, `IsOffscreen == false`, BoundingRectangle이 창 안, 형제 말단 컨트롤끼리 겹치지 않음(두 탭 모두); (u2) 첫 실행: 설정 없는 새 폴더 -> DisclaimerDialog 표시 -> DisclaimerOk -> settings.json `disclaimerAccepted == true` -> 다시 실행 -> 창 준비 후 3초 동안 DisclaimerDialog 없음; (u3) ColorHex `#GG0000` -> TextBox가 검증 오류 상태(UIA HelpText/ItemStatus 설정)이고 `crosshair verify-overlay --data-dir <픽스처 폴더>`가 이전 색으로 exit 0.
  Must NOT do: HSV 사각형 금지, 테마 라이브러리 금지, 사용자에게 보이는 문자열 하드코딩 금지.
  Closes: GAP-3, GAP-4, GAP-5, GAP-7, GAP-10
  Parallelization: Wave 4 | Blocked by: 6, 7, 8, 9, 10, 12, 15 | Blocks: 17
  References: Must have 설정 창, HotkeyBox, AutomationId, 견본색, 편집 규칙; CommunityToolkit.Mvvm https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/ ; FlaUI https://github.com/FlaUI/FlaUI
  Acceptance criteria: 빌드 경고 0; `dotnet test tests/CrosshairManager.Ui.Tests --filter "Category=Ui&(FullyQualifiedName~u1|FullyQualifiedName~u2|FullyQualifiedName~u3)"` 통과, 건너뜀 0.
  QA scenarios: happy - 두 탭의 FlaUI `Capture.Element` -> `.omo/evidence/task-16-crosshair-overlay-{simple,advanced}.png` + 테스트 출력 `.txt`. failure - u3(잘못된 hex 입력이 오버레이를 바꾸지 않음).
  Commit: Y | feat(app): settings window with simple and advanced modes

- [ ] 17. 한국어 현지화 + 실행 중 언어 전환
  Recommended task executor category: unspecified-low - resx 번역과 작은 런타임 전환, 테스트.
  What to do: 모든 키를 번역한 `Resources/Strings.ko.resx`(자연스러운 한국어 게임 용어: 크로스헤어, 점, 선, 간격, 두께, 길이, 외곽선, 불투명도, 프리셋, 단축키); Language 콤보 -> `Loc.SetLanguage` 즉시 적용 + 저장; `system`은 `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko"`이면 ko, 아니면 en. Ui.Tests: (l1) Strings.resx와 Strings.ko.resx의 키 집합 일치(둘 다 XML로 읽음), 빈 값 없음; (l2) Language를 en으로 -> `TabSimple`의 Name == Strings.resx의 `Tab.Simple` 값; ko로 -> Strings.ko.resx의 값; 저장된 `language` 갱신.
  Must NOT do: 세 번째 언어 금지, TODO 같은 자리표시 문구 금지.
  Closes: GAP-8
  Parallelization: Wave 4 | Blocked by: 16 | Blocks: 18
  References: Must have 다국어; https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview
  Acceptance criteria: l1, l2 통과(`--filter "Category=Ui&(FullyQualifiedName~l1|FullyQualifiedName~l2)"`).
  QA scenarios: happy - 두 언어 스크린샷 -> `.omo/evidence/task-17-crosshair-overlay-{ko,en}.png` + 출력. failure - 키 하나를 지운 Strings.ko.resx 임시 사본으로 l1 실행(환경변수 `CHM_RESX_DIR`이 있으면 그 경로를 읽음) -> 해당 키 이름과 함께 실패; 기록 후 원복.
  Commit: Y | feat(app): Korean and English localization

- [ ] 18. UI 자동화: 편집 -> 파일 -> 오버레이 픽셀, 트레이, 대화상자, 단축키
  Recommended task executor category: unspecified-high - 계층을 가로지르는 종단 테스트.
  What to do: Ui.Tests(todo 16의 AppFixture; 오버레이 확인은 `crosshair verify-overlay --data-dir <픽스처 폴더>`를 프로세스로 실행해 종료 코드를 단언): (e1) builtin.default 선택, DotSize=5 -> presets/에 새 파일이 생길 때까지 Retry -> JSON `dot.size == 5` -> verify-overlay exit 0; (e2) ColorHex `#FF8800` -> Preview 요소 캡처에서 Core 렌더의 불투명 원본 픽셀마다 그 색의 8x8 장치 픽셀 블록에 대응; verify-overlay exit 0; (e3) 이름 변경(RenameText/RenameOk) -> 파일의 name 필드 갱신; 복제 -> 파일 2개; 삭제(ConfirmDeleteOk) -> 파일 없음; ConfirmDeleteCancel은 유지; (e4) SaveFileDialog로 내보내기(대화상자 파일 이름 입력란에 경로 입력 후 Enter) -> 파일 존재, `crosshair validate` exit 0; 다시 가져오기 -> 목록 항목 추가; (e5) CopyShareCode -> 클립보드가 `CHM1:`로 시작; 새 프리셋에서 ShareCodeInput에 붙여넣고 PasteShareCode -> 크로스헤어 일치; `CHM1:!!!` -> ShareCodeError 문구 == `ShareCode.Error.Base64`의 Loc 값; (e6) HotkeyToggle을 `Ctrl+Alt+Shift+F10`(SystemKey 경로)으로 설정 -> settings.json 갱신 -> `SendHotkey Ctrl+Alt+Shift+F10` -> verify-overlay `--expect-hidden` exit 0; 충돌: 테스트 프로세스가 `Ctrl+Alt+Shift+F9`를 RegisterHotKey로 먼저 등록한 뒤 `hotkeys.nextPreset = "Ctrl+Alt+Shift+F9"`를 미리 넣은 설정으로 앱 실행 -> HotkeyNext에 HotkeyError 표시; (e7) 두 번째 인스턴스 실행 -> 설정 창이 포그라운드가 됨; (e8) 손상된 설정으로 시작 -> InfoBar 문구 == `InfoBar.SettingsRecovered`의 Loc 값; (e9) TStyle -> verify-overlay exit 0, 프리셋 `top.enabled == false`; (e10) MonitorSelect를 첫 번째 비주 모니터로 -> verify-overlay `--monitor <device>` exit 0; (e11) 트레이: `Shell_TrayWnd` 아래에서 이름이 `CrosshairManager`인 버튼을 찾고, 없으면 작업 표시줄의 숨겨진 아이콘 표시 버튼을 눌러 넘침 영역 최상위 창(Windows 11은 클래스 `TopLevelWindowForOverflowXamlIsland`, Windows 10은 `NotifyIconOverflowWindow`)에서 찾음, 오른쪽 클릭 -> TrayToggleOverlay -> verify-overlay `--expect-hidden` exit 0; TrayPresets -> builtin.dot -> verify-overlay `--preset builtin.dot` exit 0; TrayExit -> 5초 안에 프로세스 종료; (e12) StartWithWindows 켜기 -> `HKCU\...\Run\CrosshairManager` == exe 경로; 끄기 -> 값 없음.
  Must NOT do: sleep 금지, 건너뛰기 금지, 번역 문자열 직접 사용 금지.
  Closes: GAP-3, GAP-4, GAP-5, GAP-6, GAP-7, GAP-12
  Parallelization: Wave 4 | Blocked by: 13, 14, 15, 16, 17 | Blocks: 19, 20
  References: FlaUI https://github.com/FlaUI/FlaUI ; Must have AutomationId.
  Acceptance criteria: `dotnet test tests/CrosshairManager.Ui.Tests --filter Category=Ui` 모두 통과, 건너뜀 0; 실행 후 `Get-Process CrosshairManager -ErrorAction SilentlyContinue`가 비어 있고 Run 값 없음.
  QA scenarios: happy - trx 요약 + verify-overlay PNG 폴더 -> `.omo/evidence/task-18-crosshair-overlay.txt`. failure - 같은 실행의 e5 잘못된 코드와 e6 충돌 경로.
  Commit: Y | test(ui): end-to-end settings automation

- [ ] 19. 유휴 성능 확인
  Recommended task executor category: quick - 측정 스크립트.
  What to do: `scripts/perf-check.ps1 [-Exe <path>] [-MaxCpuPercent 0.5] [-MaxWorkingSetMB 150]`(Windows PowerShell 5.1 호환): 미리 만든 임시 데이터 폴더(설정 창 닫힘)로 앱 시작, 로그 `overlay shown` 게이트(10초 상한), `Wait-Process -Timeout 10`으로 대기하는 동안 `[Diagnostics.Stopwatch]`로 잰 10초 구간의 `TotalProcessorTime`과 `WorkingSet64`를 샘플링(프로세스가 끝나지 않으므로 이 대기가 곧 측정 구간이며 sleep이 아님), 코어 하나 기준 CPU%를 계산해 두 값 출력, 프로세스 종료 후 종료됐는지 단언, 폴더 삭제; 임계값을 넘으면 exit 1.
  Must NOT do: 확인이 실패하지 않는 한 앱 코드 수정 금지(실패하면 원인 파일에서 고치고 다시 실행).
  Closes: GAP-9, GAP-11
  Parallelization: Wave 5 | Blocked by: 14, 18 | Blocks: F1-F4
  References: IS-9, IS-11.
  Acceptance criteria: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\perf-check.ps1` exit 0, CPU%와 MB 출력. todo 20의 화면 실행과 겹치지 않게 실행.
  QA scenarios: happy - 출력 -> `.omo/evidence/task-19-crosshair-overlay.txt`. failure - `-MaxCpuPercent -1` -> exit 1.
  Commit: Y | chore: idle performance check script

- [ ] 20. 단일 파일 exe 게시, README(한국어 기본/영어), 고지 문서
  Recommended task executor category: writing - 문서와 게시 프로필.
  What to do: `src/CrosshairManager.App/Properties/PublishProfiles/win-x64.pubxml`(Release, win-x64, SelfContained, PublishSingleFile, IncludeNativeLibrariesForSelfExtract, EnableCompressionInSingleFile true, PublishTrimmed false, DebugType embedded); `scripts/publish.ps1` -> `artifacts/publish/CrosshairManager.exe`; `README.md`(한국어, 기본)와 `README.en.md`(영어): 기능, 스크린샷(todo 16/17 증거 PNG를 `docs/images/`로 복사), 사용법, 기본 단축키, 데이터 폴더, 테두리 없는 창모드 안내, 안티치트/약관 고지(코드 주입·후킹·메모리 읽기·화면 읽기 없음; The Finals는 외부 오버레이를 차단, Battlefield 6는 실행 중 바꿀 수 있는 크로스헤어 오버레이를 금지, Riot은 Vanguard 허용 목록이 없음 - 책임은 사용자; 초안 Findings의 URL 인용), 단축키 한계(시스템 단축키를 막는 게임에서는 트레이 사용), 녹화/방송에 오버레이가 찍힘, SmartScreen 안내(서명 없음), "Windows 11에서 테스트; Windows 10 1903+ 동작 예상", 빌드/테스트 명령; `THIRD-PARTY-NOTICES.md`(한국어 설명; CsWin32 MIT, Microsoft.Win32.SystemEvents MIT, CommunityToolkit.Mvvm MIT, H.NotifyIcon MIT, FlaUI MIT, xunit Apache-2.0; 테스트 전용 표시).
  Must NOT do: 설치 프로그램 금지, 서명 금지.
  Closes: GAP-9, GAP-10
  Parallelization: Wave 5 | Blocked by: 14, 18 | Blocks: F1-F4
  References: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview ; https://www.chrossx.io/blog/are-crosshair-overlays-cheating ; https://github.com/lacymorrow/crossover/issues/64
  Acceptance criteria: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish.ps1` exit 0; `artifacts/publish`에 `.exe` 정확히 하나, `.dll` 없음; 게시한 exe를 미리 만든 `--data-dir $env:TEMP\chm-qa-pub`로 실행 -> 10초 안에 로그 `overlay shown`, `crosshair verify-overlay --data-dir $env:TEMP\chm-qa-pub` exit 0; `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1` exit 0; `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\perf-check.ps1 -Exe artifacts/publish/CrosshairManager.exe` exit 0. 화면 실행은 하나씩 순서대로.
  QA scenarios: happy - 출력 + verify-overlay PNG -> `.omo/evidence/task-20-crosshair-overlay.txt`; 정리: 프로세스 종료, 남은 프로세스 없음 확인, 임시 폴더 삭제. failure - README 링크 점검: 두 README의 모든 `http` URL이 초안 Findings에 있음(스크립트로 `Select-String` 비교, 모르는 URL 0개); README 필수 항목 점검: `Select-String -Path README.md -Pattern '테두리 없는 창모드','녹화','안티치트'`와 `Select-String -Path README.en.md -Pattern 'borderless','record','anti-cheat'`가 각각 패턴마다 1건 이상.
  Commit: Y | build: single-file publish and documentation (footer `Plan: .omo/plans/crosshair-overlay.md`)

## Final verification wave
> Runs in parallel after ALL todos. ALL must APPROVE. Surface results and wait for the user's explicit okay before declaring complete.
- [ ] F1. Plan compliance audit
  Recommended task executor category: unspecified-high - 모든 todo의 수용 기준과 Must NOT have를 점검한다. `src/`에서 SetWindowsHookEx, SendInput, App의 ScreenProbe/Capture.Backdrop 사용, HttpClient/Socket/WebRequest를, 테스트와 스크립트에서 `Skip =`, `Thread.Sleep`/`Task.Delay`/`Start-Sleep`을 `rg`로 검색한다.
- [ ] F2. Code quality review
  Recommended task executor category: unspecified-high - 계층 규칙(Core의 project.assets에 Windows 패키지 없음, Platform에 WPF 없음), 경고=오류 상태 깨끗함, 죽은 코드 없음, contracts.md가 코드 시그니처와 일치하는지 검토한다.
- [ ] F3. Real manual QA
  Recommended task executor category: unspecified-high - 게시한 exe를 미리 만든 데이터 폴더로 이 데스크톱에서 실행한다. 최대화한 메모장(GDI)과 최대화한 Windows 계산기(DirectComposition/flip 모델 대용) 위의 크로스헤어, 클릭 통과(중앙을 클릭하면 아래 앱이 포그라운드가 됨), 트레이 동작, 단축키, 프리셋 전환, 언어 전환, 두 번째 모니터를 확인하고, 각 확인은 `crosshair verify-overlay` 종료 코드 + PNG로 증명한다. DPI 배율을 기록하고, 실제 안티치트 게임은 시험하지 않았음을 적는다.
- [ ] F4. Ideal-state fidelity
  Recommended task executor category: unspecified-high - IS-1..IS-12를 하나씩 증거와 대조한다. 부족한 부분은 새 번호의 todo가 된다.

## Commit strategy
- todo마다 해당 todo에 적힌 메시지로 커밋 하나(Conventional Commits; 저장소에 이전 기록 없음; 커밋 메시지는 git 관례에 따라 영어 유지). 각 커밋은 단독으로 빌드되고 건드린 테스트 프로젝트가 통과해야 한다.
- 마지막 커밋(todo 20)의 꼬리말: `Plan: .omo/plans/crosshair-overlay.md`.
- `artifacts/`, `TestResults/`, `bin/`, `obj/`, `.omo/evidence/`는 커밋하지 않는다. `.omo/plans/`와 `.omo/drafts/`는 todo 1에서 커밋한다.

## Success criteria
> One row per IS row. The plan is complete only when every IS row has a delivering todo and a proving QA scenario; F4 checks the delivered behavior against these rows 1:1, and a shortfall becomes new `- [ ] N.` rows, never a note.
| IS | Delivering todo(s) | Proving QA scenario | Evidence |
| --- | --- | --- | --- |
| IS-1 | 5, 6, 11, 13, 15 | Desktop (a)(d)(g), NOACTIVATE 테스트, todo 15 verify-overlay, F3 클릭 통과 | task-5, task-13, task-15 |
| IS-2 | 3, 12, 13 | 도형 홀짝 테스트, 기준 그림, 캡처 정확 일치 | task-3, task-12, task-13 |
| IS-3 | 2, 16, 18 | u1 배치 테스트; e1, e2, e9, e10 | task-16, task-18 |
| IS-4 | 8, 16, 18 | e1 자동 사본, e3 이름 변경/복제/삭제 | task-8, task-18 |
| IS-5 | 4, 8, 9, 15, 16, 18 | e1 자동 저장 파일, e2 미리보기 블록, e8 손상 복구, todo 15 읽기 전용 폴더 | task-15, task-18 |
| IS-6 | 7, 15, 16, 18 | todo 15 단축키 verify-overlay; e6 충돌; e7 두 번째 인스턴스; e11 트레이 | task-15, task-18 |
| IS-7 | 8, 10, 16, 18 | e4 파일 대화상자, e5 공유 코드 | task-8, task-10, task-18 |
| IS-8 | 15, 17 | l1 키 일치, l2 실행 중 전환 | task-17 |
| IS-9 | 19, 20 | 게시본 perf-check; exe 하나, dll 없음 | task-19, task-20 |
| IS-10 | 16, 20 | u2 첫 실행 고지; InfoBar 존재(u1); README 필수 항목 Select-String(todo 20) | task-16, task-20 |
| IS-11 | 3, 5, 11, 19 | 창 크기 == 캔버스; Reasserted와 불필요한 재설정 없음 테스트; 유휴 CPU | task-5, task-11, task-19 |
| IS-12 | 1-14, 18, 20 | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1` exit 0, 건너뜀 0 | task-20 |
