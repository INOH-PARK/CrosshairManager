# CrosshairManager

[한국어](README.md)

A crosshair overlay for Windows. It draws your crosshair in a small transparent window on top of the screen, and mouse clicks pass straight through to whatever is underneath. It ships as one exe: no installer, no admin rights, no .NET install.

## Features

- Click-through, always-on-top crosshair overlay. The window is only as big as the crosshair and redraws only when a setting changes.
- Line length, thickness, gap, outline, center dot, color, opacity, rotation (45 degree steps) and scale
- Built-in and user presets, with import/export as JSON files or clipboard share codes
- Live preview and auto-save in the settings window
- Multi-monitor selection with per-monitor DPI
- Tray icon menu, global hotkeys, single instance
- Korean and English UI (follows the Windows display language by default, switchable in settings)

## Screenshots

The settings window is still being built, so screenshots will be added later.

## Usage

1. Run `CrosshairManager.exe`. The first run shows a notice dialog. Read it and accept.
2. Click the tray icon to open settings. Pick a preset or change values and the overlay updates right away.
3. Run your game in **borderless** windowed mode (or windowed).
4. To quit, choose Exit from the tray menu.

### Default hotkeys

| Action | Hotkey |
| --- | --- |
| Show/hide overlay | `Ctrl+Alt+Shift+X` |
| Next preset | `Ctrl+Alt+Shift+Right` |
| Previous preset | `Ctrl+Alt+Shift+Left` |
| Open settings | not assigned |

You can change hotkeys in settings. Games that block system hotkeys may stop them from working. When that happens, use the tray icon menu. The tray always works.

### Data folder

Settings, presets and logs live in `%LocalAppData%\CrosshairManager`. Pass `--data-dir` to use a different folder.

```
CrosshairManager.exe --data-dir D:\CrosshairData
```

## Fullscreen games

Exclusive fullscreen isn't supported. In that mode the game owns the display and normal windows can't show on top (https://github.com/lacymorrow/crossover/issues/64). Set the game to **borderless** windowed mode instead. Microsoft describes the Windows 11 windowed game optimizations at https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11 .

## Anti-cheat and terms of service

CrosshairManager is a plain external window. It doesn't inject code into games, doesn't hook anything, doesn't read game memory and doesn't read the game's screen. It also doesn't detect which game is running or change behavior based on it.

Some games still ban external crosshair overlays through their terms or anti-cheat. Known cases as of November 2026 (https://www.chrossx.io/blog/are-crosshair-overlays-cheating):

- The Finals blocks third-party overlays.
- Battlefield 6 prohibits crosshair overlays that can be changed while playing.
- Riot (Vanguard) has no allow list.

Policies differ per game and can change. Check the game's terms yourself before you use this. Any account action or other consequence of using this app is your responsibility.

## Recordings and streams

The crosshair is a real window on your screen, so it shows up in anything you record or stream. There's no option to hide it from capture. Hide the overlay with the hotkey or tray when you record.

## SmartScreen warning

The exe isn't code-signed, so Windows SmartScreen may warn you on first run. Click "More info", then "Run anyway". You can also build it yourself.

## Supported systems

Tested on Windows 11. Windows 10 1903 or later is expected to work but hasn't been tested. x64 only.

## Build and test

You need .NET SDK 10.0.403. Scripts run on Windows PowerShell 5.1.

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\perf-check.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish.ps1
```

- `verify.ps1` builds, then runs the Core, Desktop and Ui tests. It fails if any test fails or is skipped. Close CrosshairManager before running it.
- `perf-check.ps1` measures idle CPU and memory. Use `-Exe` to point it at a specific exe.
- `publish.ps1` produces the single file `artifacts\publish\CrosshairManager.exe` and prints its size.

## License

MIT. See [LICENSE](LICENSE). Open source components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
