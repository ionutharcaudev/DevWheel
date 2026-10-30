using System.Diagnostics;

namespace DevWheel.Interop;

public enum CaptureStatus
{
    Captured,
    NoSelection,
    BlockedByElevation,
    ClipboardBusy,
}

public sealed record CaptureResult(CaptureStatus Status, string? Text);

/// <summary>
/// Reads the selected text of another app by simulating Ctrl+C, and replaces it by simulating Ctrl+V,
/// leaving the user's clipboard as it was (docs/implementation.md, Selection capture / Replacing).
/// </summary>
public sealed class SelectionService(ClipboardService clipboard)
{
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ModifierWait = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PasteSettle = TimeSpan.FromMilliseconds(300);

    public async Task<CaptureResult> CaptureAsync(nint target)
    {
        if (!WindowHelper.IsWindow(target)) return new CaptureResult(CaptureStatus.NoSelection, null);

        ClipboardSnapshot snapshot;
        try
        {
            snapshot = clipboard.Snapshot();
        }
        catch (ClipboardBusyException)
        {
            return new CaptureResult(CaptureStatus.ClipboardBusy, null);
        }

        try
        {
            // Give a quick tap a moment to lift the modifiers; anything still held is released by SendCopy.
            var wait = Stopwatch.StartNew();
            while (InputSimulator.AnyModifierDown() && wait.Elapsed < ModifierWait) await Task.Delay(10);

            var before = ClipboardService.SequenceNumber;
            InputSimulator.SendCopy();

            var timer = Stopwatch.StartNew();
            while (ClipboardService.SequenceNumber == before && timer.Elapsed < CaptureTimeout) await Task.Delay(10);

            if (ClipboardService.SequenceNumber == before)
            {
                var status = WindowHelper.IsBlockedByUipi(target) ? CaptureStatus.BlockedByElevation : CaptureStatus.NoSelection;
                return new CaptureResult(status, null);
            }

            // The sequence number changes on EmptyClipboard; give the app a moment to finish writing.
            await Task.Delay(30);
            var text = clipboard.GetText();
            clipboard.Restore(snapshot);
            return string.IsNullOrEmpty(text)
                ? new CaptureResult(CaptureStatus.NoSelection, null)
                : new CaptureResult(CaptureStatus.Captured, text);
        }
        catch (ClipboardBusyException)
        {
            return new CaptureResult(CaptureStatus.ClipboardBusy, null);
        }
        finally
        {
            snapshot.Wipe();
        }
    }

    /// <summary>
    /// Pastes <paramref name="text"/> over the selection in <paramref name="target"/>. Non-sensitive text is
    /// followed by restoring the previous clipboard; sensitive text stays (marked private) for auto-clear.
    /// Returns the clipboard sequence number after our write.
    /// </summary>
    public async Task<uint> ReplaceAsync(nint target, string text, bool sensitive)
    {
        var snapshot = sensitive ? null : clipboard.Snapshot();
        try
        {
            var written = clipboard.SetText(text, sensitive);
            if (!WindowHelper.Activate(target)) throw new InvalidOperationException("Target window is gone");
            await Task.Delay(50);
            InputSimulator.SendPaste();
            await Task.Delay(PasteSettle);
            if (snapshot is not null && ClipboardService.SequenceNumber == written) clipboard.Restore(snapshot);
            return written;
        }
        finally
        {
            snapshot?.Wipe();
        }
    }
}
