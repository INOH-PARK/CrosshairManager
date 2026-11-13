# 서드파티 고지 (Third-Party Notices)

CrosshairManager는 아래 오픈소스 패키지를 사용합니다. 각 패키지의 라이선스 원문은 해당 프로젝트 저장소나 NuGet 패키지에 들어 있습니다.

## 배포되는 exe에 포함

| 패키지 | 버전 | 라이선스 | 용도 |
| --- | --- | --- | --- |
| Microsoft.Windows.CsWin32 | 0.3.371 | MIT License | Win32 API 호출 코드를 빌드할 때 생성합니다(빌드 전용 생성기). |
| Microsoft.Win32.SystemEvents | 10.0.15 | MIT License | 디스플레이 설정 변경 같은 시스템 이벤트를 받습니다. |
| CommunityToolkit.Mvvm | 8.4.2 | MIT License | 설정 창의 MVVM 바인딩에 씁니다. |
| H.NotifyIcon.Wpf | 2.4.1 | MIT License | 트레이 아이콘과 메뉴에 씁니다. |

## 테스트 전용 (배포 exe에 포함되지 않음)

| 패키지 | 버전 | 라이선스 | 용도 |
| --- | --- | --- | --- |
| FlaUI.UIA3 | 5.0.0 | MIT License | UI 자동화 테스트에 씁니다. |
| xunit | 2.9.3 | Apache License 2.0 | 테스트 프레임워크입니다. |
| xunit.runner.visualstudio | 3.1.5 | Apache License 2.0 | 테스트 실행기입니다. |

배포되는 exe에는 self-contained로 .NET 런타임(MIT License)도 함께 들어 있습니다.
