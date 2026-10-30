# DevWheel — Local Developer Multi-Tool (Spec)

DevWheel is a Windows tray app that opens a radial tool wheel at the mouse cursor on a global hotkey, runs common dev utilities (encode, decode, hash, generate keys, encrypt) fully offline, and returns the result to the clipboard or replaces the selected text in place.

## Problem

Developers need small utilities many times a day: Base64, JWT decoding, key generation, hashing. In a bank, pasting secrets or tokens into external websites is a data-leak risk and is often blocked. DevWheel keeps all of this on the local machine, with no network access, using only the .NET built-in cryptography.

## Goals

- One keystroke to any tool, no window switching
- Works on selected text in any app (IDE, browser, terminal, Notepad)
- Fully local: zero network access, zero logging of inputs or outputs
- Standalone portable .exe with no host app, installer, or runtime to install
- Easy to extend with new tools through one interface

## Scope

In v1: global hotkey, radial wheel with one level of submenus, selection capture and replace, clipboard output with auto-clear for secrets, tray icon with settings, the tool catalogue below.

Out of v1, and out of scope for good: any network feature (cloud sync, telemetry, update checks, online help). Also out of v1: plugins loaded from external DLLs, macOS or Linux, an installer.

## Tool catalogue (v1)

| Group | Tool | Input | Output | Sensitive |
| --- | --- | --- | --- | --- |
| Encode | Base64 encode / decode | Selection | Replace selection | No |
| Encode | Base64URL encode / decode | Selection | Replace selection | No |
| Encode | URL encode / decode | Selection | Replace selection | No |
| Encode | HTML entity encode / decode | Selection | Replace selection | No |
| Inspect | JWT decode (header + payload, no verification) | Selection | Popup viewer | Yes |
| Inspect | Unix timestamp to ISO date and back | Selection | Popup + clipboard | No |
| Format | JSON pretty-print / minify | Selection | Replace selection | No |
| Hash | SHA-256, SHA-1, MD5 (hex) | Selection | Clipboard | No |
| Hash | HMAC-SHA256 (asks for key) | Selection + key prompt | Clipboard | Yes |
| Generate | GUID / UUID v4 | None | Clipboard | No |
| Generate | Random secret (16/32/64 bytes; hex or Base64) | None | Clipboard, auto-clear | Yes |
| Generate | Laravel APP_KEY (base64: + 32 bytes) | None | Clipboard, auto-clear | Yes |
| Generate | Password (length, charset options) | None | Clipboard, auto-clear | Yes |
| Crypto | AES-256-GCM encrypt / decrypt (key prompt) | Selection + key prompt | Replace selection | Yes |

## Key decisions

- Standalone app: no PowerToys or any other host; everything runs locally with no network.
- Stack: C# on .NET 8, WPF for the overlay, no third-party packages.
- Selection capture: simulated Ctrl+C with clipboard save and restore (details in Implementation).
- Secrets never touch Windows clipboard history: content is flagged to be excluded and cleared after 30 seconds.
- Distribution: shared by hand as a zip of the .exe, or built from the repo. Rollout in three steps: Ionut alone, then the team after a team review, then the company if there is interest.
- Wheel layout: variant C, groups on an inner ring and tools in an outer arc (see Design). Working name DevWheel; rename freely.

## Settled questions

- Default tool when the wheel opens: none. Nothing is preselected; the hub stays neutral until the user hovers.
- Hotkey: Ctrl+Alt+Space by default; the user can record any other combination in Settings.
- Rollout: Ionut uses it alone first; then a team review before sharing with the team; company-wide only later, if there is interest.
