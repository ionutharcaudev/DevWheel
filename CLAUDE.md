# DevWheel

Windows tray app: global hotkey opens a radial tool wheel at the cursor; tools run offline
and write to the clipboard or replace the selected text. Full spec in docs/.

## Stack
- .NET 8, C# 12, WPF. Tray via System.Windows.Forms.NotifyIcon.
- Projects: DevWheel.App (WPF), DevWheel.Core (tools, no UI/Win32), DevWheel.Interop (P/Invoke).
- Tests: xUnit in tests/DevWheel.Core.Tests.

## Hard rules (security, do not break)
- NO network code of any kind. Never reference System.Net.Http or System.Net.Sockets.
- NO third-party NuGet packages in App, Core or Interop. Ask before adding any package.
- NEVER log selected text, tool output, keys or passphrases.
- Randomness only via RandomNumberGenerator. Crypto only via System.Security.Cryptography.
- No low-level keyboard hooks (SetWindowsHookEx). Use RegisterHotKey + GetAsyncKeyState.
- Sensitive outputs: clipboard exclusion formats + auto-clear (see docs/implementation.md).

These rules are enforced by tests/DevWheel.Core.Tests/SecurityTests.cs (assembly references and a
source scan) and by Directory.Build.targets (fails the build on any PackageReference outside tests).

## Conventions
- Every tool implements IDevTool and gets its own test class with known test vectors.
- Use LibraryImport (source-generated P/Invoke), not DllImport. All P/Invoke lives in DevWheel.Interop.
- Nullable enabled, warnings as errors.

## Commands
- The SDK may be user-local: `%USERPROFILE%\.dotnet\dotnet.exe` when `dotnet` is not on PATH.
- Build: dotnet build
- Test: dotnet test
- Publish: ./scripts/publish.ps1 (see docs/implementation.md, Build and publish)
