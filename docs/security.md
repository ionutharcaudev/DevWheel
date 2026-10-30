# Security & Compliance

DevWheel's security case rests on four claims a reviewer can verify: no network code, no persistence of data, only built-in .NET cryptography, and a signed, reproducible build.

## Requirements

| ID | Requirement | How it is met | Where it is enforced |
| --- | --- | --- | --- |
| SEC-01 | No network access | No HttpClient, sockets, WebRequest, or update checks. A build-time test fails if any assembly references `System.Net.Http`, `System.Net.Sockets`, `System.Net.Requests` or `System.Net.WebClient` (and other networking assemblies). Optional extra layer: a Windows Firewall outbound block rule for DevWheel.exe | `SecurityTests.No_network_assembly_is_referenced`, `Source_follows_the_hard_rules` |
| SEC-02 | No logging of inputs or outputs | There is no logging at all. Error toasts show only fixed messages or exception type names, never text content | Source scan bans `Console/Debug/Trace.Write` |
| SEC-03 | No persistence of secrets | Settings file holds preferences only; key prompts are never remembered | `SettingsStoreTests.Settings_have_no_place_for_secrets` (reviewed allowlist of persisted properties) |
| SEC-04 | Secrets excluded from clipboard history and cloud clipboard | Exclusion formats on every sensitive write | `ClipboardService.SetText(text, sensitive: true)` |
| SEC-05 | Sensitive clipboard auto-clears | Default 30 s, range 5–300 s, cannot be disabled; also cleared on exit | `AppSettings.Normalize`, `WheelController.ScheduleClear` |
| SEC-06 | Approved crypto only | System.Security.Cryptography; CSPRNG for all randomness; AES-GCM, SHA-256, PBKDF2-SHA256 | Source scan bans `new Random(` and `Random.Shared` |
| SEC-07 | Memory hygiene | Key and plaintext byte arrays zeroed after use (best effort; .NET strings cannot be zeroed) | `CryptographicOperations.ZeroMemory` in Core tools |
| SEC-08 | Minimal privileges | Runs as standard user; never requests elevation | `app.manifest` `asInvoker`, `SecurityTests.App_never_requests_elevation` |
| SEC-09 | Signed binary | Not required for personal testing; sign with a company certificate before sharing with the team | `scripts/publish.ps1 -CertThumbprint ... -TimestampUrl ...` |
| SEC-10 | Dependency control | No third-party NuGet packages in App, Core, Interop; tests may use xUnit | `Directory.Build.targets` fails the build; `Only_framework_and_own_assemblies_are_referenced` |
| SEC-11 | Weak algorithms clearly labeled | MD5, SHA-1, and unverified JWT decode carry a warning label in the UI | Tool names/hints, JWT viewer title |

## Behaviors security reviewers will ask about

- **Simulated keystrokes (SendInput):** only Ctrl+C and Ctrl+V, only right after the user presses the hotkey, only to the window that was in front. No background input. Before them, modifier keys the user still holds get key-up events (with an unassigned key tapped first, so Alt or Win do not open a menu).
- **Clipboard reading:** only during a hotkey-triggered capture; the previous content is restored.
- **No keyboard hook:** hold mode polls key state instead of a low-level hook, to avoid looking like a keylogger to EDR tools.
- **Global hotkey:** registered with the standard RegisterHotKey API.
- **Foreground switching:** to paste back, DevWheel brings the remembered window to the front with SetForegroundWindow, falling back to AttachThreadInput for that one call when Windows refuses.
- **Registry:** only HKCU `...\CurrentVersion\Run\DevWheel`, and only when "Start with Windows" is on.

## Known limitations

- .NET strings (selected text, results) stay in memory until garbage-collected; acceptable for a local user tool, but state it in the review.
- Other clipboard managers installed on the machine may still capture sensitive content; the exclusion format is a convention, not enforcement.
- Auto-clear cannot remove a value already pasted into another app.
- The clipboard snapshot taken during capture briefly holds the user's previous clipboard content in memory (wiped after restore).

## Approval checklist

Personal testing (now):

- [ ] Build from source locally; no prebuilt binaries from the internet
- [ ] Verify with a network monitor (Resource Monitor or Wireshark) that DevWheel opens no connections
- [ ] Check the .exe runs under your normal user without admin rights
- [ ] Confirm EDR on your machine does not flag it

Before sharing with the team (later):

- [ ] Share the repo and a SHA-256 checksum of the zip, so colleagues can verify the file they received
- [ ] Internal code review of Interop and crypto code
- [ ] Team review of the code and this spec before sharing; for a later company-wide rollout, decide with security whether a formal review and code signing are needed
