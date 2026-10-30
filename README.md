# DevWheel

A Windows tray app for everyday developer utilities. Press **Ctrl+Alt+Space** anywhere and a radial
wheel opens at the mouse cursor: encode, decode, inspect, format, hash, generate keys and encrypt,
working on the text you selected in any app. Everything runs locally, offline, with built-in .NET
cryptography only.

## Using it

1. Optionally select text in any app (IDE, browser, terminal, Notepad).
2. Press the hotkey. The inner ring shows the groups (Encode, Inspect, Format, Hash, Generate, Crypto);
   hover one and its tools appear in the outer arc.
3. Click a tool. The result replaces the selection, goes to the clipboard, or opens in a viewer,
   and a toast confirms.

| Action | Mouse | Keyboard |
| --- | --- | --- |
| Open a group | hover it | 1 to 6, or arrows + Enter |
| Run a tool | click it | 1 to n, or arrows + Enter |
| Options for one run (secret, password) | Shift+click | Shift+Enter |
| Back to groups | move back to the inner ring | Backspace |
| Close | Esc, right-click, click outside, move away | Esc |

**Hold mode** (Settings): hold the hotkey, move to a tool, release to run it.
**Direct hotkeys** (Settings, Wheel tab): give any tool its own hotkey to skip the wheel.

Tools with a lock badge are sensitive: their output is kept out of Windows clipboard history and
cloud clipboard, and the clipboard is cleared after 30 seconds (configurable, cannot be turned off).

Tray menu: Open settings, Pause hotkey, About, Exit.

## Tools (v1)

| Group | Tools |
| --- | --- |
| Encode | Base64, Base64URL, URL, HTML entity (encode and decode) |
| Inspect | JWT decode (signature NOT verified), Unix timestamp ⇄ ISO date |
| Format | JSON pretty-print, JSON minify |
| Hash | SHA-256, SHA-1 (legacy), MD5 (legacy), HMAC-SHA256 |
| Generate | GUID v4, random secret, Laravel APP_KEY, password |
| Crypto | AES-256-GCM encrypt / decrypt ([format](docs/ciphertext-format.md)) |

## Building

Requires the .NET 8 SDK on Windows.

```powershell
dotnet build
dotnet test
./scripts/publish.ps1        # tests, single-file self-contained exe, zip and SHA-256 checksums in artifacts/dist
```

## Installing

The zip from `publish.ps1` contains the .exe and a small install kit (`scripts/install`). Unzip it and
double-click `install.cmd`: it copies DevWheel to `%LOCALAPPDATA%\Programs\DevWheel`, turns on
"Start with Windows" (HKCU Run key, no admin rights), adds a Start Menu shortcut and starts the app.
Run it again to upgrade. `uninstall.cmd` removes all of that and keeps your settings
(`uninstall.ps1 -RemoveSettings` deletes them too). `install.ps1 -NoAutostart` installs without autostart.

To sign before sharing with the team: `./scripts/publish.ps1 -CertThumbprint <thumbprint> -TimestampUrl <company timestamp url>`.

## For security reviewers

Start with [docs/security.md](docs/security.md): every requirement (SEC-01 to SEC-11) is listed with
how it is met and where it is enforced. In short:

- **No network:** no networking assembly is referenced; `SecurityTests` fails the build otherwise, and a
  source scan bans HttpClient, WebRequest, WebClient and sockets.
- **No logging:** there is no log at all; error messages never contain the text being processed.
- **No persistence of secrets:** `%APPDATA%\DevWheel\settings.json` holds preferences only; a test pins
  the list of persisted properties.
- **No third-party code:** `Directory.Build.targets` fails the build on any package reference outside the
  test project.
- **No keyboard hook:** the hotkey uses RegisterHotKey; hold mode polls GetAsyncKeyState.
- **Simulated input** is limited to Ctrl+C / Ctrl+V towards the window that was in front when the user
  pressed the hotkey. All Win32 calls are in `src/DevWheel.Interop`.
- **Runs as the standard user** (`asInvoker` manifest).

Known limitations are listed at the end of docs/security.md. One practical note: editors such as VS Code
copy the whole current line on Ctrl+C when nothing is selected, so DevWheel sees that line as the selection.

## Layout

```text
src/DevWheel.App       WPF app: tray, wheel, dialogs, settings, pipeline (WheelController)
src/DevWheel.Core      IDevTool, ToolRegistry, all tools, settings model; no UI, no Win32
src/DevWheel.Interop   Hotkey, SendInput, clipboard, selection capture/replace, DPI (LibraryImport)
tests/                 xUnit: tool test vectors, settings, security checks
docs/                  Spec: overview, design, implementation, security, ciphertext format
```

Adding a tool: one class implementing `IDevTool` (usually deriving from `ToolBase` or `TextTransformTool`),
one line in `ToolRegistry.CreateDefault`, one test class.
