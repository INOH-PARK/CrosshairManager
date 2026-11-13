# CrosshairManager 계약 문서

이 문서는 `.omo/plans/crosshair-overlay.md`의 Must have에서 옮겨 적은 고정 계약이다. 코드와 테스트는 이 문서를 기준으로 한다.

## 툴체인과 패키지

- .NET SDK 10.0.403 (`global.json`, rollForward latestPatch)
- 테스트 스택: VSTest 위의 xunit v2 (xunit.v3 / Microsoft.Testing.Platform 사용 안 함)
- 모든 프로젝트는 Directory.Build.props 의 Nullable, ImplicitUsings, TreatWarningsAsErrors, LangVersion latest 를 따른다.

### 해석된 패키지 버전 (2026-11-12 기준 NuGet 최신)

| 패키지 | 버전 | 사용 프로젝트 |
| --- | --- | --- |
| Microsoft.Windows.CsWin32 (PrivateAssets all) | 0.3.371 | Platform, TestSupport, SendHotkey |
| Microsoft.Win32.SystemEvents | 10.0.15 | Platform |
| CommunityToolkit.Mvvm | 8.4.2 | App |
| H.NotifyIcon.Wpf | 2.4.1 | App |
| xunit | 2.9.3 | 모든 테스트 프로젝트 |
| xunit.runner.visualstudio | 3.1.5 | 모든 테스트 프로젝트 |
| Microsoft.NET.Test.Sdk | 17.14.2 | 모든 테스트 프로젝트 |
| FlaUI.UIA3 | 5.0.0 | Ui.Tests |

### todo 1에서 내린 결정

- **TFM**: Windows 프로젝트(Platform, Cli, TestSupport, Desktop.Tests, Ui.Tests, SendHotkey)는 `net10.0-windows10.0.19041.0`을 쓴다. 계획의 `net10.0-windows`(플랫폼 버전 7.0)로는 CsWin32가 만든 `SetProcessDpiAwarenessContext` 등이 CA1416(오류로 취급)을 내고, `SupportedOSPlatformVersion`은 TargetPlatformVersion보다 높을 수 없어(NETSDK1135) 해결되지 않는다. Core, TestSupport.Core, Core.Tests는 `net10.0`이다.
- **AllowUnsafeBlocks**: CsWin32를 쓰는 세 프로젝트(Platform, TestSupport, SendHotkey)의 csproj에 `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`를 둔다.
- **창 스타일 API**: `GetWindowLongPtr/SetWindowLongPtr` 대신 `GetWindowLong/SetWindowLong`을 NativeMethods.txt에 적는다(GWL_EXSTYLE만 사용). PlatformTarget 변경은 하지 않는다(AnyCPU 유지).
- **DPI 컨텍스트**: PER_MONITOR_AWARE_V2는 `new DPI_AWARENESS_CONTEXT((nint)(-4))`로 만든다. 앱은 `src/CrosshairManager.Platform/Dpi/DpiAwareness.cs`를 쓰고, 테스트용 사본은 `tests/CrosshairManager.TestSupport/DpiAwareness.cs`에 있다.
- **트레이**: H.NotifyIcon.Wpf 2.4.1이 복원되고, `TaskbarIcon`이 있는 WPF 앱이 `dotnet publish src/CrosshairManager.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`로 빌드와 단일 파일 게시까지 성공했다(exit 0, 단일 exe 157,814,590 바이트). 따라서 H.NotifyIcon.Wpf를 그대로 쓰고 `Shell_NotifyIcon` 대체 구현은 필요 없다.
- **테스트 0개 규칙**: `scripts/verify.ps1`은 테스트 메서드(`[Fact]`/`[Theory]`)가 아직 없는 프로젝트가 trx를 만들지 않는 것을 허용한다(뼈대 단계). 테스트 메서드가 하나라도 있으면 trx 누락은 실패다.
- **Ui.Tests**는 App 프로젝트를 참조하지 않는다(TFM 불일치). 실행 파일 경로로 앱을 띄우며, 빌드는 솔루션 빌드가 보장한다.

## 솔루션 구조와 의존 규칙

```
src/CrosshairManager.Core, Platform, App
tools/CrosshairManager.Cli
tests/CrosshairManager.TestSupport.Core, TestSupport, Core.Tests, Desktop.Tests, Ui.Tests
tests/tools/SendHotkey
```

Core <- Platform <- App; Cli -> Core + Platform; TestSupport -> Core + Platform; 각 테스트 프로젝트는 시험 대상 계층과 TestSupport를 참조한다. Core는 Platform을 참조하지 않고 Platform은 WPF를 참조하지 않는다. App의 AssemblyName은 `CrosshairManager`, Cli는 `crosshair`.

## 크로스헤어 모델

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

## 처리 순서

- 처리 순서(모든 곳에서 고정): `Normalize` -> `ApplyScale`(Size/Length/Thickness/Gap에 ScalePercent/100을 곱하고 0에서 먼 쪽으로 반올림; 유일한 하한은 Size/Thickness >= 1; 표의 최댓값으로 다시 자르지 않음) -> `CrosshairLayout.Compute` -> `CrosshairRasterizer.Render` -> `OverlayPlacement.Compute`.

## 도형 규칙

- 도형 규칙(Core): 중심 `P = (mon.Left + W/2 + OffsetX, mon.Top + H/2 + OffsetY)`; `snap(x) = floor(x + 0.5)`; `half(n,c) = snap(c - n/2)`; Dot `[half(s,Px),+s) x [half(s,Py),+s)`; 십자 중심 띠: 가로 기준 두께 `Ty` = 켜진 Left/Right 선 두께의 최댓값(없으면 켜진 점의 크기, 그것도 없으면 0), 세로 기준 두께 `Tx` = 켜진 Top/Bottom 선 두께의 최댓값(없으면 점 크기, 없으면 0); 띠 중심 `cy = Ty>0 ? half(Ty,Py) + Ty/2 : Py`, `cx = Tx>0 ? half(Tx,Px) + Tx/2 : Px`(항상 정수나 .5); Top `x:[half(T,Px),+T) y:[topEnd-L, topEnd)`, `topEnd = snap(cy-G)`; Bottom `y:[bottomStart, +L)`, `bottomStart = 2*cy - topEnd`; Left `x:[leftEnd-L, leftEnd) y:[half(T,Py),+T)`, `leftEnd = snap(cx-G)`; Right `x:[rightStart,+L)`, `rightStart = 2*cx - leftEnd`(맞은편 간격이 띠 중심 기준으로 정확히 대칭이 되어 홀수 두께에서도 네 방향 빈칸 수가 같고, 짝수 두께에서는 `snap(P±G)`와 같은 결과); Outline = 채움 영역 합집합으로부터 체비쇼프 거리 1..o인 픽셀; 캔버스 반폭 `e = ceil(P에서 채움 사각형 꼭짓점까지 체비쇼프 거리의 최댓값) + o + (회전이 90의 배수가 아니면 ceil(e0*0.4143)+1, 아니면 0)`(e0는 회전 전 반폭; 45도 대각선으로 늘어나는 만큼을 덮음); Compute에는 화면의 홀짝 플래그 두 개(W 홀수? H 홀수?)만 들어간다.

## 래스터라이저

- 래스터라이저(Core): premultiplied BGRA, 위에서 아래 순서. 회전이 90의 배수면 픽셀 중앙에서 한 번만 샘플링한다(정확, 안티에일리어싱 없음). 45/135/225/315도는 4x4 균일 슈퍼샘플링. 분류 순서: 샘플점을 P 기준으로 -RotationDeg만큼 돌린 뒤 점 -> 선 -> 외곽선 -> 투명. `alpha = round(255*coverage*partAlpha/255)`, `c_pm = round(c*alpha/255)`. 부분별 알파 바이트 = `round(255 * partOpacity/100 * Opacity/100)`.

## 프리셋 파일

- 프리셋 파일 `presets/{guid}.json`: camelCase, System.Text.Json 소스 생성, `schemaVersion: 1` 필수. 없는 선택 필드는 기본값. `> 1`이면 NewerVersion(사용 불가로 표시, 절대 다시 쓰지 않음). 형식이 깨졌거나 `crosshair`가 없으면 Broken(목록에 표시, 절대 다시 쓰지 않음). 범위를 벗어난 값은 잘라 내고 경고. 정렬: 내장 프리셋(고정 순서) 다음 사용자 프리셋(CreatedUtc, 같으면 Id 서수 비교).

## 설정 스키마

- 설정 `settings.json` v1: `schemaVersion, activePresetId, overlayVisible, monitor {mode, deviceName}, hotkeys {toggleOverlay, nextPreset, previousPreset, openSettings}, language ("system"|"ko"|"en"), startWithWindows, disclaimerAccepted, settingsWindowPlacement {left, top, width, height} | null`. 손상 시 `settings.json.corrupt-<yyyyMMddHHmmss>`로 이름을 바꾸고 기본값을 불러온 뒤 InfoBar로 알림. 더 새로운 버전이면 메모리에서만 기본값을 쓰고 파일은 건드리지 않으며 InfoBar로 알림.

## 단축키 기본값

- 단축키: GlobalHotkeys 전용 메시지 전용 창과 스레드에서 `RegisterHotKey` + `MOD_NOREPEAT`. 기본값: ToggleOverlay `Ctrl+Alt+Shift+X`, NextPreset `Ctrl+Alt+Shift+Right`, PreviousPreset `Ctrl+Alt+Shift+Left`, OpenSettings 미지정. 테스트 전용 조합은 기본값이 쓰지 않는 F9~F12 조합을 쓴다. 다음/이전은 목록 순서대로 불러올 수 있는 프리셋을 순환한다.

## 견본색

- 견본색(순서대로): #00FF00, #FFFF00, #00FFFF, #FF00FF, #FF0000, #FFFFFF, #FF8800, #0080FF.

## AutomationId

- 고정 AutomationId(docs/contracts.md): `PresetList, NewPreset, DuplicatePreset, RenamePreset, RenameText, RenameOk, DeletePreset, ConfirmDeleteOk, ConfirmDeleteCancel, ImportPresetFile, ExportPresetFile, CopyShareCode, ShareCodeInput, PasteShareCode, ShareCodeError, TabSimple, TabAdvanced, Swatch0..Swatch7, SimpleScale, SimpleOpacity, SimpleOutline, ColorHex, ColorR, ColorG, ColorB, DotEnabled, DotSize, DotOpacity, LinkLines, TStyle, Top.Enabled, Top.Length, Top.Thickness, Top.Gap, Bottom.*, Left.*, Right.* (같은 4개 접미사), OutlineEnabled, OutlineThickness, OutlineColorHex, OutlineOpacity, Opacity, Scale, Rotation, OffsetX, OffsetY, MonitorSelect, MonitorWarning, HotkeyToggle, HotkeyNext, HotkeyPrev, HotkeyOpenSettings, HotkeyError, Language, StartWithWindows, OverlayVisible, Preview, InfoBar, DisclaimerDialog, DisclaimerOk, TrayOpenSettings, TrayToggleOverlay, TrayPresets, TrayExit`. 숫자 입력은 Slider + TextBox 쌍이고 AutomationId는 TextBox에 붙인다. Rotation은 8개 값을 가진 ComboBox. 트레이 아이콘 툴팁은 `CrosshairManager`. 항상 보이는 컨트롤이 아닌 것: DisclaimerDialog, DisclaimerOk, RenameText, RenameOk, ConfirmDeleteOk, ConfirmDeleteCancel, ShareCodeError, MonitorWarning, HotkeyError, Tray*.

## CLI `crosshair` 문법 (todo 14)

  - `render <presetIdOrJsonPath> <out.png> [--zoom 1..32 (8)] [--bg #RRGGBB] [--odd] [--data-dir <dir>]`
  - `validate <jsonPath>` -> `OK`/경고 출력 후 exit 0, `BROKEN: <reason>` exit 2, `NEWER: <v>` exit 3
  - `capture <out.png> [--monitor <device>] [--size N (64)]`(중앙 영역)
  - `compare <expected.png> <actual.png> [--tolerance N (0)]` -> 허용 오차 안이면 exit 0, 다르면 1(개수와 첫 차이 출력)
  - `verify-overlay [--data-dir <dir>] [--preset <id>] [--monitor <device>] [--expect-hidden] [--timeout <s> (5)] [--out <dir>]` -> 데이터 폴더의 설정(또는 플래그)에서 활성 프리셋과 모니터를 읽고 예상 창 위치를 계산, todo 11에서 만든 Platform `Capture/Backdrop`으로 #FF00FF 배경창을 위치 + 8 px로 띄우고 Must have 포그라운드 가져오기 규칙으로 앞에 놓은 뒤 `WindowAt(P)`가 배경창인지 확인, Eventually 방식으로 캡처해 배경 위 Core 렌더 합성과 비교(정확 일치; 45도 회전은 허용 오차 2), `--expect-hidden`이면 배경색만인지 비교; 예상/실제 PNG를 `--out`에 저장; 일치 exit 0, 불일치 1, 사용법 오류 2
  - `ico <out.ico>`(builtin.default를 16/32/48 투명 캔버스에 맞춰 그리고 32bpp BMP/DIB 항목으로 ICO 작성)
  사용법 오류는 사용법 문구와 exit 2(validate의 BROKEN도 exit 2를 쓰되 `BROKEN:`을 출력).

## todo 16에서 내린 결정 (설정 창)

- **InfoBar 키**: 손상된 settings.json 복구 메시지 키는 `InfoBar.SettingsRecovered`다(todo 15의 `Settings.RecoveredFromCorrupt`를 이 이름으로 바꿈). `InfoBar` 요소(UIA StatusBar)의 Name은 현재 메시지의 Loc 문구이고, 메시지가 없으면 `InfoBar.Fullscreen` 안내 문구다. 메시지 줄은 AutomationId `InfoBarMessage`, 세부 줄은 `InfoBarDetail`, 전체화면 안내 줄은 `InfoBarNotice`, 닫기 버튼은 `InfoBarDismiss`.
- **추가 AutomationId**: 설정 창 `SettingsWindow`; 프리셋 목록 행 `Preset.<presetId>`(Name = 표시 이름); 간단 탭 내장 프리셋 타일 `Tile.<presetId>`; 이름 변경 대화상자 `RenameDialog`(+ `RenameCancel`); 삭제 확인 대화상자 `ConfirmDeleteDialog`.
- **ColorHex / OutlineColorHex**: 잘못된 값이면 바인딩 검증 오류(INotifyDataErrorInfo)가 표시되고 UIA `ItemStatus`가 `invalid`(정상이면 `valid`), `HelpText`가 `Color.Invalid`의 Loc 문구다. 잘못된 값은 오버레이에 적용되지 않고 저장되지도 않는다.
- **숫자 입력**: `NumberField`(UIA Group) 안의 TextBox가 AutomationId를 가진다. 입력값은 그대로 두고 미리보기/오버레이/저장 시 `Normalize`로 잘라 내며, 잘린 값은 저장 뒤 필드에 다시 반영된다(미리보기 아래 `Editor.Clamped` 경고).
- **LinkLines**는 Length/Thickness/Gap만 네 선에 복사한다(Enabled는 선별 유지, TStyle과 공존). TStyle 체크 = `Top.Enabled == false`.
- **자동 저장**: 사용자 프리셋 편집은 400 ms(`SettingsViewModel.AutosaveDelay`) 뒤 저장, 프리셋 전환/창 숨김/종료 시 즉시 저장. 내장 프리셋의 첫 편집은 편집된 값으로 `Loc.Format("Preset.CopyName", 내장 이름)` 사본을 만들고 그것을 활성화한다.
- **창 위치**: `settingsWindowPlacement`는 물리 픽셀이다. 숨길 때 저장하고, 다시 열 때 중심점이 속한 모니터의 작업 영역 안으로 보정한 뒤 그 모니터의 DPI로 DIP에 변환한다.
- **공유 코드 복사**는 내장 프리셋을 현지화된 이름으로 인코딩한다. 붙여넣기 오류는 `ShareCode.Error.*`의 Loc 값이 `ShareCodeError`에 표시된다.
- **UIA 트리**: Owner가 있는 대화상자(DisclaimerDialog, RenameDialog, ConfirmDeleteDialog)는 바탕 화면이 아니라 소유 창(SettingsWindow)의 자식으로 나타난다. Ui.Tests의 `AppFixture.FindTopLevel`은 두 곳을 모두 찾는다.
