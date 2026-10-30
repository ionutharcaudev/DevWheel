# Implementation

DevWheel is a .NET 8 WPF app split into three projects (App, Core, Interop) plus tests; Core holds every tool behind one interface and has no UI or Win32 dependency, so it is fully unit-testable.

## Tech stack

| Concern | Choice | Why |
| --- | --- | --- |
| Runtime | .NET 8, C# 12 | Team knows .NET; LTS |
| UI | WPF | Transparent, topmost, animated overlay is easy |
| Tray icon | System.Windows.Forms.NotifyIcon (UseWindowsForms) | Built in, avoids a third-party package |
| Crypto | System.Security.Cryptography only | No external crypto libraries to review |
| Win32 | P/Invoke (LibraryImport source generator) | RegisterHotKey, SendInput, clipboard, DPI |
| Tests | xUnit | Core tools are pure functions |
| Packaging | Single-file, self-contained win-x64 | One .exe, no runtime install |

## Solution structure

```text
DevWheel.sln
  src/
    DevWheel.App/        WPF: App.xaml, TrayIcon, WheelWindow, SettingsWindow, prompts, toasts
    DevWheel.Core/       IDevTool, ToolRegistry, all tools, settings model (no WPF, no Win32)
    DevWheel.Interop/    HotkeyService, InputSimulator, ClipboardService, SelectionService, WindowHelper (foreground, DPI)
  tests/
    DevWheel.Core.Tests/ xUnit tests per tool, incl. known test vectors, plus security checks
```

## Runtime pipeline

1. **Startup:** named Mutex `Global\DevWheel` enforces single instance; load settings; register hotkey; show tray icon.
2. **Hotkey (WM_HOTKEY 0x0312)** arrives on a hidden HwndSource window.
3. **Remember target:** store `GetForegroundWindow()` handle and `GetCursorPos()`.
4. **Capture selection** (next section), with a timeout of about 250 ms.
5. **Show wheel** at the cursor, converted from physical pixels to WPF units using the monitor's DPI.
6. **User picks a tool;** wheel hides immediately.
7. **Run tool** in Core with a ToolContext (selected text, prompt service).
8. **Deliver:** replace selection, clipboard only, or viewer window; then toast.
9. **Cleanup:** restore the user's previous clipboard where applicable; start auto-clear timer for sensitive output.

## Global hotkey

- Use `RegisterHotKey` with `MOD_NOREPEAT`. If registration fails (key taken), show a tray balloon and open settings. Changing the hotkey: the settings recorder captures the combination, calls UnregisterHotKey on the old one, tries RegisterHotKey on the new one, and keeps the old one if the new registration fails (taken by another app). Require at least one of Ctrl, Alt or Win, and reject combinations Windows reserves (e.g. Ctrl+Alt+Del, Win+L). The hotkey is stored in settings.json as modifiers plus virtual-key code.
- Hold mode needs to know when the hotkey is released. Poll `GetAsyncKeyState` every 15 ms while the wheel is open rather than installing a low-level keyboard hook: low-level hooks are a common EDR red flag.

## Selection capture (simulated copy)

1. Snapshot the current clipboard (and note `GetClipboardSequenceNumber()`).
2. **Wait until the user's modifier keys are released** or send key-up events for them; otherwise Ctrl+C arrives as Ctrl+Alt+C. This is the most common bug.
3. Send Ctrl+C with `SendInput` to the remembered foreground window.
4. Wait for the sequence number to change, up to 250 ms. No change = no selection.
5. Read the text; then restore the snapshot so the user's clipboard looks untouched.

Notes: `OpenClipboard` can fail while another app holds it, so wrap all clipboard access in a retry (5 tries, 20 ms apart). Windows blocks input injection into elevated (admin) windows from a non-elevated app (UIPI); show the toast "Can't read selection from an admin window" instead of failing silently.

As built: the snapshot copies every memory-based clipboard format (not only text), so images and file lists survive too; GDI-handle formats are skipped (bitmaps survive through CF_DIB). Some editors (VS Code, Visual Studio) copy the whole current line on Ctrl+C when nothing is selected; DevWheel then sees that line as the selection.

## Replacing the selection

1. Put the result on the clipboard.
2. `SetForegroundWindow` on the remembered window (allowed because our wheel was just foreground).
3. Send Ctrl+V with `SendInput`.
4. After about 300 ms, restore the original clipboard (skip if the tool is sensitive; then auto-clear applies instead).

## Clipboard privacy

When writing sensitive output, add these extra formats so Windows clipboard history and cloud clipboard ignore it:

- `ExcludeClipboardContentFromMonitorProcessing` (any value)
- `CanIncludeInClipboardHistory` = DWORD 0
- `CanUploadToCloudClipboard` = DWORD 0

Auto-clear: after the configured delay, clear the clipboard only if `GetClipboardSequenceNumber()` still equals the value right after our write, so we never wipe something the user copied later. On exit, a pending auto-clear runs immediately.

## Overlay window

- WPF Window: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`.
- Add `WS_EX_TOOLWINDOW` so it never appears in Alt+Tab.
- Draw the inner ring and the outer arc as `Path` segments generated in code from the group and tool counts. Hit-test with polar math instead of per-element: the distance from the wheel center picks the zone (hub, inner ring, outer arc, outside), the angle picks the group or tool. Keep the last hovered group open while the pointer is anywhere in its outer arc.
- Declare PerMonitorV2 DPI awareness in app.manifest; position with the cursor monitor's DPI.
- Close on Deactivated, Esc, or right-click.

## Tool interface (Core)

```csharp
public interface IDevTool
{
    string Id { get; }            // "base64.encode"
    string Group { get; }         // "Encode"
    string Name { get; }          // "Base64 encode"
    string Label { get; }         // short text drawn on the wheel segment, e.g. "B64\nenc"
    string Hint { get; }          // shown in the hub
    bool RequiresSelection { get; }
    bool IsSensitive { get; }     // triggers clipboard exclusion + auto-clear
    OutputMode DefaultOutput { get; } // ReplaceSelection | Clipboard | Viewer
    Task<ToolResult> RunAsync(ToolContext ctx, CancellationToken ct);
}

public sealed record ToolContext(string? SelectedText, IPromptService Prompts, ToolOptions Options);

public abstract record ToolResult
{
    public sealed record Success(string Output, string Message) : ToolResult;
    public sealed record View(string Title, string Content, string? CopyText, string Message) : ToolResult;
    public sealed record Failure(string Message) : ToolResult;
    public sealed record Cancelled : ToolResult;
}
```

Changes from the original spec: `IconGlyph` became `Label` (a short text is clearer than an icon for encode vs decode in a 24-degree segment; groups keep Segoe Fluent icons), and `ToolResult.View` was added for the viewer tools (JWT, timestamp).

Tools are registered in a `ToolRegistry`; the wheel is built from the registry filtered and ordered by settings. Adding a tool = one class + one test class.

## Crypto specifics

- **Random bytes:** `RandomNumberGenerator.GetBytes(n)` only; never `System.Random`.
- **Passwords:** pick each character with `RandomNumberGenerator.GetInt32(charset.Length)` (no modulo bias). A password missing one of the chosen character sets is redrawn whole.
- **Laravel APP_KEY:** `"base64:" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))`.
- **Hashes:** `SHA256.HashData`, output lowercase hex; MD5 and SHA-1 labeled "legacy, not for security".
- **JWT decode:** split on `.`, Base64URL-decode header and payload, pretty-print; clearly labeled "signature NOT verified".
- **AES-256-GCM:** `AesGcm` with a 12-byte random nonce and 16-byte tag. The key prompt accepts either a 32-byte Base64 key or a passphrase.
- **Passphrase keys:** `Rfc2898DeriveBytes.Pbkdf2` with SHA-256, 600,000 iterations, 16-byte random salt.
- **Ciphertext format:** Base64 of `version(1) | salt(16, zeros if raw key) | nonce(12) | ciphertext | tag(16)`, version byte 0x01. See [ciphertext-format.md](ciphertext-format.md).
- Zero out key and plaintext byte arrays with `CryptographicOperations.ZeroMemory` after use.

## Settings

- JSON at `%APPDATA%\DevWheel\settings.json`; never stores keys, passphrases, or outputs.
- Start with Windows via the HKCU Run key (no admin rights needed).

## Build and publish

```text
dotnet publish src/DevWheel.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

`scripts/publish.ps1` runs the tests, publishes, optionally signs (`-CertThumbprint`, `-TimestampUrl`; `signtool sign /fd SHA256 /tr <timestamp url>`), zips the .exe together with the install kit from `scripts/install` and writes SHA-256 checksums to `artifacts/dist`.

The install kit (`install.cmd`/`install.ps1`, `uninstall.cmd`/`uninstall.ps1`) is per user and needs no admin rights: it copies the .exe to `%LOCALAPPDATA%\Programs\DevWheel`, writes the same HKCU Run value as `StartupRegistration`, sets `StartWithWindows` in settings.json so the app keeps the Run value on the next save, and adds a Start Menu shortcut.
