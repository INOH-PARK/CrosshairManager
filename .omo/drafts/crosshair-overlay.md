---
slug: crosshair-overlay
status: approved
intent: clear
review_required: true
plan_path: .omo/plans/crosshair-overlay.md
plan_sha256: e204e5c91838408d28c6e149a21a55143cfde40ef43edfe49718c709ba832238
review_round_id: r4-1794485870591
review_round_limit: 5
pending-action: none - 계획 승인됨; /ulw-execute crosshair-overlay로 실행
review:
  plan_reviewer:
    status: approved
    workspace_root: E:/MyData/Projects/CrosshairManager
    runtime_home: null
    target: .omo/plans/crosshair-overlay.md
    round_id: r4-1794485870591
    plan_sha256: e204e5c91838408d28c6e149a21a55143cfde40ef43edfe49718c709ba832238
    launch_id: null
    session: st_01a1cfb1
    result: OKAY
approach: "설계 B - 프레임워크에 의존하지 않는 Core(net10.0: 모델/Normalize/도형/래스터라이저 -> premultiplied BGRA/프리셋/설정/공유 코드) + Platform(net10.0-windows, CsWin32: 전용 스레드에서 UpdateLayeredWindow로 그리는 Win32 레이어드 클릭 통과 오버레이 HWND, TopmostKeeper, MonitorService, RegisterHotKey, ScreenProbe) + WPF App(간단/상세 설정, 최근접 확대 미리보기, 트레이, 단일 인스턴스, ko/en resx) + CLI(render/validate/capture/compare/verify-overlay/ico) + xUnit Core/Desktop/Ui(FlaUI) 테스트; TDD; 포터블 단일 파일 게시."
---

# 초안: crosshair-overlay

> 문서 언어 규칙(사용자 지시, 2026-11-12): "앞으로 문서는 한국어로 작성한다." 앞부분 메타데이터 키, 섹션 헤더, 식별자, URL은 도구가 읽으므로 원문 그대로 둔다.

## Affected user and ideal state
대상 사용자:
- (U1) 테두리 없는 창모드/창모드로 게임을 하는 Windows 11 FPS 플레이어 - 현재는 앱이 없음(빈 폴더); 이후 포터블 exe를 실행해 크로스헤어를 고르거나 편집하고 플레이한다.
- (U2) 프리셋/설정을 확장할 미래의 유지보수자나 에이전트 - 테스트 가능한 Core와 버전 붙은 설정 스키마가 필요하다.
- (U3) 방송/녹화하는 플레이어 - 크로스헤어가 캡처에도 찍힌다는 점을 README로 안다.

- IS-1 | 테두리 없는 게임을 포함한 모든 일반 창 위, 모니터 정중앙(+오프셋)에 그려지고 포커스나 클릭을 빼앗지 않음 | 핵심 목적
- IS-2 | 픽셀 단위로 정확하고 선명함: 정수 물리 픽셀, 중앙 대칭, 배율과 무관하게 흐림 없음 | 흐리거나 비대칭인 크로스헤어는 쓸 수 없음
- IS-3 | 상세 설정: 색(hex + RGB + 견본색 8개), 중앙 점(크기/불투명도), 선 4개 각각(켜기/끄기, 길이/두께/간격, 선 연동, T자형), 외곽선(켜기/끄기, 두께, 색, 불투명도), 전체 불투명도, 배율, 45도 단위 회전, X/Y 오프셋, 대상 모니터 | 사용자 요청(채택한 기본값으로 정리: 바깥쪽 선 세트 없음, 선별 불투명도 없음, HSV 없음)
- IS-4 | 간단 모드: 내장 프리셋 원클릭 + 견본색 + 크기; 사용자 프리셋 저장/이름 변경/복제/삭제; 내장 프리셋은 읽기 전용 | 사용자 요청
- IS-5 | 변경이 확대 미리보기와 함께 즉시 적용됨; 재시작해도 설정 유지; 손상된 파일이 앱을 종료시키지 않음 | 걸리는 곳이 없어야 함
- IS-6 | 트레이 아이콘 + 전역 단축키(켜기/끄기, 다음/이전 프리셋), 변경 가능, 충돌은 조용히 넘기지 않고 알림; 단일 인스턴스 | 게임 중 Alt+Tab 없이 조작
- IS-7 | .json 파일과 클립보드 코드로 프리셋 공유 | D5
- IS-8 | 한국어/영어 UI, 전환 가능 | D4
- IS-9 | 포터블 단일 exe, 설치·관리자 권한 불필요; 낮은 CPU(유휴 약 0%, 렌더 루프 없음) | D3, FPS를 깎으면 안 됨
- IS-10 | 명확한 안내: Exclusive 전체화면 미지원 -> 테두리 없는 창모드; 약관/안티치트 주의(주입·메모리 읽기·화면 읽기 없음) | 정직함, 제재 방지
- IS-11 | 오버레이 창을 최소 크기(전체 화면이 아니라 경계 상자 크기)로 유지해 flip 모델/independent flip 영향을 줄임 | 성능 조사 결과
- IS-12 | (U2) Core는 결정적 테스트로, 데스크톱/UI는 자동 테스트로 덮이고 스크립트 하나로 실행 | 유지보수성, D6
- GAP-1..12: 모든 IS 행이 현재는 없음(빈 폴더에서 새로 시작) -> 계획서의 todo들이 각각 닫는다.

## Components (topology ledger)
- C1 | Core(모델·도형·래스터·프리셋·설정·공유 코드) | active | todo 2-4, 8-10, 12
- C2 | Platform(오버레이 창·모니터·단축키·z-order·캡처) | active | todo 5-7, 11
- C3 | App(셸·설정 창·현지화·트레이) | active | todo 15-17
- C4 | 검증 도구(CLI·Desktop/Ui 테스트·스크립트) | active | todo 1, 13, 14, 18, 19
- C5 | 배포·문서 | active | todo 20

## Open assumptions (announced defaults)
- architect st_01a1cf22의 설계를 채택(Findings 참고). 아래 기본값은 모두 되돌리기 쉬운 내부 결정이다:
- 오버레이 = WPF 창이 아닌 Win32 레이어드 HWND | 픽셀 바이트가 테스트의 단일 기준 | 되돌림 가능
- 크기는 물리 px, PMv2 manifest; ScalePercent만 배율; snap floor(x+0.5) | 선명도 | 되돌림 가능
- 모델: 점과 선은 같은 색, 선마다 Length/Thickness/Gap(+ 선 연동 토글), T자형 = Top 끔, 간격은 중심 기준(Valorant 방식), 회전은 45도 단위(90의 배수는 정확, 나머지는 4x4 SSAA), 외곽선은 체비쇼프 팽창. 바깥쪽 선 세트 없음 | 선별 설정이 상세 설정의 형태 | 되돌림 가능
- 기본값: #00FF00, 점 2, 선 6/2/3, 검은 외곽선 1px | 흔한 형태 | 되돌림 가능
- 내장 프리셋: builtin.default, builtin.dot, builtin.cross, builtin.t, builtin.x45, builtin.classic1px(읽기 전용, 편집 시 사본 자동 생성) | IS-4 | 되돌림 가능
- 데이터 폴더 %LocalAppData%\CrosshairManager(presets/{id}.json + settings.json, schemaVersion 1, File.Replace + .bak 원자적 쓰기), --data-dir / CROSSHAIRMANAGER_DATA_DIR로 변경 가능 | 되돌림 가능
- 단축키 기본값: 켜기/끄기 Ctrl+Alt+Shift+X, 다음 Ctrl+Alt+Shift+Right, 이전 Ctrl+Alt+Shift+Left, 설정 열기 미지정; RegisterHotKey + MOD_NOREPEAT만 사용(저수준 훅 없음) | 안티치트, 편집기 단어 선택 단축키와 충돌 방지 | 되돌림 가능
- 공유 코드 CHM1: + base64url(deflate(기본값 생략 JSON)) | D5 | 되돌림 가능
- 캡처 제외(hideFromCapture)는 제외 - README에 녹화에 찍힌다고 안내; Windows 시작 시 실행(HKCU Run)은 기본 꺼짐 | 되돌림 가능
- NuGet 의존성: Microsoft.Windows.CsWin32, Microsoft.Win32.SystemEvents, CommunityToolkit.Mvvm, H.NotifyIcon.Wpf(대체안 Shell_NotifyIcon), 테스트 전용 xunit + FlaUI.UIA3 | 승인 때 공개 | 되돌림 가능
- todo 1에서 git init(폴더가 저장소가 아님) | 커밋 전략 | 되돌림 가능
- 프로젝트 문서는 한국어(README.md 한국어 기본 + README.en.md 영어, docs/contracts.md 한국어); LICENSE는 법적 문구라 영어 원문; 커밋 메시지는 git 관례에 따라 영어 | 사용자 문서 언어 규칙 | 되돌림 가능

## Findings (cited - path:lines)
- 프로젝트 폴더 E:/MyData/Projects/CrosshairManager는 비어 있음(새 프로젝트), git 저장소 아님.
- 툴체인: .NET SDK 10.0.403(C:/Program Files/dotnet/sdk), Node, Git, winget v1.29.380; Rust/cargo 없음, PATH에 cl.exe 없음.
- pwsh 없음(`where pwsh` 결과 없음) -> 스크립트는 Windows PowerShell 5.1로 실행.
- Microsoft.Win32.SystemEvents.dll은 WindowsDesktop 참조 팩(`Microsoft.WindowsDesktop.App.Ref\10.0.14\ref\net10.0\`)에만 있고 NETCore 참조 팩에는 없음 -> WPF가 아닌 Platform에는 NuGet 패키지 필요.
- 분류: 아키텍처급 새 프로젝트(새 앱, 여러 모듈). 질문 방식: 첫 세션, CLEAR -> 하나씩.
- 조사 레인: librarian st_01a1cf21(오버레이/안티치트/스택/도구), architect st_01a1cf22(설계 자문).
- librarian st_01a1cf21 실패(Bash 도구 스키마 불일치, 결과 0) -> 계획자가 web_search/webfetch로 직접 조사.
- 클릭 통과: 레이어드 창 + WS_EX_TRANSPARENT는 마우스를 아래 창으로 넘김(https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features ; 스타일 https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles).
- WPF AllowsTransparency=true는 성능 비용이 있음; 대안은 WindowChrome + WS_EX_TRANSPARENT(https://dwayneneed.github.io/wpf/2008/09/08/transparent-windows-in-wpf.html , https://blog.csdn.net/lindexi_gd/article/details/112240402).
- Exclusive 전체화면: 일반 최상단 오버레이는 보이지 않음(CrossOver: https://github.com/lacymorrow/crossover/issues/64 ; "창 전체화면을 쓰라"). Crosshair X는 별도의 Xbox Game Bar 위젯으로 해결(UWP 전용 SDK, Microsoft.Gaming.XboxGameBar NuGet, Store 배포: https://learn.microsoft.com/en-us/xbox/game-bar/quickstart/introduction , https://github.com/microsoft/XboxGameBarSamples).
- Win11 "창 모드 게임 최적화"는 DX10/11 테두리 없는 게임을 flip 모델로 바꿈(https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11). 게임 위의 HWND_TOPMOST 창은 independent flip/테두리 없는 창 최적화를 깨뜨릴 수 있음(Discord 오버레이 사례: https://erikmcclure.com/blog/discord-overlay-breaks-gsync/), MPO가 완화(https://forums.blurbusters.com/viewtopic.php?t=14040) -> 설계: 오버레이 창은 전체 화면이 아니라 크로스헤어 경계 상자만큼만.
- 안티치트/약관(2026-11-09 기준, https://www.chrossx.io/blog/are-crosshair-overlays-cheating): The Finals는 외부 오버레이 차단(TFAV8011); Battlefield 6는 실행 중 바꿀 수 있는 크로스헤어 오버레이 금지; Riot은 Vanguard 허용 목록 없음; PUBG/CoD는 포괄적 금지. Crosshair X는 알려진 제재가 없다고 주장(https://steamcommunity.com/app/1366800/discussions/0/4292564952449561976/). -> 앱은 평범한 외부 창이어야 함: 주입 없음, 메모리/화면 읽기 없음, 게임 상황에 따른 동작 없음; 사용 위험 안내 포함.
- Valorant 프로필 코드(0;P;...) 가져오기/내보내기는 여러 웹 도구가 구현(https://aimfinder.pro/valorant/crosshair) - 공식 사양 없음; 선택 기능(이번 범위 제외, D5).

## Decisions (with rationale)
- D9 (사용자 지시): 앞으로 문서는 한국어로 작성. 계획서와 초안을 한국어로 재작성하면서 2회차 리뷰 참고 사항 n1-n5를 계획서에 반영함(재작성으로 승인 해시가 무효가 되어 새 리뷰 회차 필요).
- D8 (사용자, 질문함): LICENSE는 MIT, 저작권자는 닉네임 "ButterCoconut"(닉네임으로 해도 되는지 물어봄 - 가능함, 가명도 유효).
- D7 (사용자, plan-consultant 이후 질문함): 회전은 45도 단계(0/45/.../315)로 제한; 90의 배수는 정확, 나머지 45의 배수는 4x4 SSAA.
- plan-consultant st_01a1cf37 반영: TestSupport 분리, Loc을 todo 15로, IAppShell 경계와 함께 15->16->17->18 순차, Ui 픽스처를 16으로, 단축키 전용 창, TestSupport CsWin32로 테스트 인터롭, BackdropWindow를 11로, xunit v2 고정, DwmFlush+Eventually, 테스트 병렬 끄기, verify-overlay CLI, 설정 미리 생성, 읽기 전용 폴더, 로그 표지 줄, 트레이/대화상자 ID, resx 값 비교, 처리 순서 Normalize->ApplyScale, AssemblyName CrosshairManager, 표시 우선순위, 단축키 기본값 Ctrl+Alt+Shift, IME/SystemKey, 덮였을 때만 최상단 재설정, App TFM net10.0-windows10.0.19041.0, 압축, ICO DIB 항목, F3 flip 모델 대용.
- D6 (사용자, 질문함): 테스트 전략 = Core 로직은 TDD(xUnit) + 에이전트가 실제 화면 QA를 항상 실행.
- D5 (사용자, 질문함): 추가 기능 포함 = 다중 모니터 선택(모니터별 DPI), 단축키 다음/이전 프리셋(앱 안 약관 주의 문구), 프리셋 JSON 파일 + 클립보드 코드 가져오기/내보내기. 제외 = Valorant/CS2 코드 가져오기.
- D4 (사용자, 질문함): UI 언어 = resx로 한국어 + 영어, 기본값은 OS UI 언어, 설정에서 전환.
- D3 (사용자, 질문함): 배포 = 포터블 self-contained 단일 exe(win-x64), 설정은 %LocalAppData%\CrosshairManager(%APPDATA%에서 변경: 모니터 이름이 PC마다 다름); 서명 없음(README에 SmartScreen 안내).
- D2 (사용자, 질문함): 스택 = C# .NET 10 + WPF(net10.0-windows).
- D1 (사용자, 질문함): 전체화면 범위 = 테두리 없는 창모드/창모드만; Xbox Game Bar 위젯 없음. 앱이 게임을 테두리 없는 창모드로 바꾸라고 안내.

## Scope IN
IS-1..IS-12; 회전(45도 단계), 배율, 가져오기/내보내기/공유 코드, ko/en 현지화, 단축키 다음/이전, 다중 모니터, 게시, README를 포함한 계획서의 todo 1-20과 F1-F4.

## Scope OUT (Must NOT have)
- Xbox Game Bar 위젯 / Exclusive 전체화면 지원(D1)
- Valorant/CS2 코드 가져오기(D5)
- 게임별 자동 프로필, 포그라운드 프로세스 감지, 게임 상황에 따른 동작
- DLL 주입, SetWindowsHookEx/저수준 키보드 훅, 메모리 읽기, 게임 화면 읽기, 앱의 SendInput
- 설치 프로그램/MSIX, 코드 서명, 자동 업데이트, 원격 측정, 네트워크 접근
- 캡처 제외 기능, HSV 색 사각형, 자유 각도 회전

## Open questions
- 없음.

## Approval gate
status: approved (사용자: 승인)
next action: 한국어 재작성본에 대해 새 plan-reviewer 회차를 실행하고, 승인되면 실행 안내.

## Review rounds
- r1(sha256 51d10af0...): plan-consultant st_01a1cf37 완료 후 반영. plan-reviewer st_01a1cf4b 결과 REJECT. 동결된 원장: B1 웨이브 순서 타입(IntRect/PixelBuffer/SendChord) -> 수정: IntRect를 todo 4로, 5/6이 4에 의존, SendChord를 todo 7로; B2 pwsh 없음(확인: where pwsh -> 없음) -> 수정: Windows PowerShell 5.1 호출; B3 SystemEvents가 NETCore 참조 팩에 없음(확인) -> 수정: Platform에 Microsoft.Win32.SystemEvents 패키지. 참고 사항 반영: e6 충돌을 미리 넣은 설정으로, e11 Win11 넘침 영역 클래스, 기준 그림 G2, architect 참조 제거.
- r2-1794480058869(sha256 43279fb3...): plan-reviewer st_01a1cf58 결과 OKAY(조건 없는 승인, 참고 사항만 있음), 원장 B1-B3 닫힘. 참고 사항: (n1) 13||14, 19||20의 화면 사용 단계 순차 실행; (n2) 공유 코드 정규 JSON에서 기본값 생략; (n3) u1은 항상 보이는 AutomationId만 검사; (n4) `dotnet new sln --format sln`, NativeMethods.txt에는 열거형 멤버가 아닌 타입 이름; (n5) 배경창 포그라운드 대체 방법(AttachThreadInput + SetWindowPos(HWND_TOP)), 캡처 전 WindowAt(P) 단언, Platform에 SetForegroundWindow 추가.
- 한국어 재작성(D9): 계획서를 한국어로 다시 쓰면서 n1-n5를 계획서 본문에 반영함. r2 승인 해시와 달라졌으므로 새 회차 r3로 다시 리뷰.
- 회차 r3-1794484888712 초기화; plan_sha256 223e43d7b87ac678c32fe4e608d7a7525bd64cecabbd87c266c563ad93e22eb5.
- r3 결과: REJECT (st_01a1cfa2). 원장 추가: B4 홀수 두께 선의 간격이 1px 비대칭(IS-2; 직접 계산으로 확인: classic1px 위/왼쪽 2px, 아래/오른쪽 1px) -> 수정: 십자 중심 띠(cx, cy) 기준 맞은편 간격 대칭 규칙, todo 3/12에 네 방향 빈칸 동일 성질과 classic1px 짝/홀 기준 그림 추가(검산: T1/T2 모두 짝·홀에서 빈칸 동일, 짝수 두께는 기존 결과와 같음). 참고 사항 반영: Platform Capture/Backdrop을 todo 11이 만들도록, 행렬 간선 14->6,9와 11->2, PowerShell 5.1 인용('#202020', $env:TEMP), IS-10에 U3 녹화 안내 + todo 20 README Select-String.
- 회차 r4-1794485870591 초기화; plan_sha256 e204e5c91838408d28c6e149a21a55143cfde40ef43edfe49718c709ba832238.
- r4 결과: OKAY (st_01a1cfb1), 조건 없는 승인(참고 사항만). 원장 B4 닫힘(리뷰어가 classic1px 좌표를 직접 검산). 현재 계획서 sha256 재검증 == e204e5c91838408d28c6e149a21a55143cfde40ef43edfe49718c709ba832238.
- 실행 참고 사항(비차단, 계획서는 승인 해시 유지를 위해 고치지 않음; 실행자가 해당 todo에서 반영): (m1) todo 5/11 TopmostKeeper: '위에 있는 창'은 보이고(IsWindowVisible), cloak되지 않았고(DwmGetWindowAttribute DWMWA_CLOAKED == 0), 오버레이 사각형과 겹치는 창만 셈 - 셸 z-band 창 때문에 매 틱 재설정되는 것 방지; NativeMethods.txt에 DwmGetWindowAttribute, DWMWINDOWATTRIBUTE, IntersectRect 추가. (m2) todo 1: Directory.Build.props 또는 CsWin32 쓰는 3개 프로젝트에 AllowUnsafeBlocks true; GetWindowLongPtr/SetWindowLongPtr 대신 GetWindowLong/SetWindowLong(GWL_EXSTYLE만 사용) 또는 PlatformTarget x64. (m3) todo 15/18 e7: 두 번째 인스턴스가 신호 전에 AllowSetForegroundWindow(첫 인스턴스 PID) 호출(PID는 뮤텍스 소유 인스턴스가 data dir의 instance.pid에 기록), 첫 인스턴스는 Must have 포그라운드 가져오기 규칙으로 설정 창 활성화. (m4) todo 15 failure 2: 데이터 폴더가 읽기 전용이면 표지 줄(overlay shown/hidden)도 대체 로그 %TEMP%\CrosshairManager-fallback.log에 기록하고 게이트는 그 파일을 봄. (m5) todo 16 DisclaimerDialog 문구에 '코드 주입·후킹·메모리 읽기·화면 읽기를 하지 않음' 추가(IS-10). (m6) todo 13 Blocked by에 8 추가 의미(웨이브로 보장됨); todo 2/6/9 Blocks 칸은 의존성 행렬 기준; 'BackdropWindow 위 캡처' 규칙은 오버레이 캡처에만 적용(e2 Preview 요소 캡처는 예외).
