using System.Runtime.InteropServices;

namespace DevWheel.Interop;

public sealed class ClipboardBusyException() : Exception("The clipboard is in use by another app");

/// <summary>A copy of every memory-based clipboard format, taken so the user's clipboard can be put back.</summary>
public sealed class ClipboardSnapshot
{
    internal ClipboardSnapshot(IReadOnlyList<(uint Format, byte[] Data)> items) => Items = items;

    internal IReadOnlyList<(uint Format, byte[] Data)> Items { get; }

    /// <summary>Zeroes the copied bytes; call once the snapshot is no longer needed.</summary>
    public void Wipe()
    {
        foreach (var (_, data) in Items) Array.Clear(data);
    }
}

/// <summary>
/// Win32 clipboard access with retry, full snapshot/restore, privacy formats for sensitive output and a
/// sequence-number guard so auto-clear never wipes something the user copied later.
/// </summary>
public sealed class ClipboardService(nint ownerWindow)
{
    private const int OpenRetries = 5;
    private const int OpenRetryDelayMs = 20;
    private const long SnapshotByteLimit = 64L * 1024 * 1024;

    // Formats backed by GDI handles rather than global memory: they cannot be copied byte-wise.
    // Bitmaps survive anyway through CF_DIB / CF_DIBV5, which Windows keeps alongside CF_BITMAP.
    private static readonly HashSet<uint> HandleFormats = [2, 3, 9, 14, 0x80, 0x82, 0x83, 0x8E];

    private static readonly Lazy<uint[]> PrivacyFormats = new(() =>
    [
        NativeMethods.RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing"),
        NativeMethods.RegisterClipboardFormatW("CanIncludeInClipboardHistory"),
        NativeMethods.RegisterClipboardFormatW("CanUploadToCloudClipboard"),
    ]);

    public static uint SequenceNumber => NativeMethods.GetClipboardSequenceNumber();

    public ClipboardSnapshot Snapshot()
    {
        var items = new List<(uint, byte[])>();
        using (Open())
        {
            long total = 0;
            uint format = 0;
            while ((format = NativeMethods.EnumClipboardFormats(format)) != 0)
            {
                if (HandleFormats.Contains(format)) continue;
                var data = ReadGlobal(NativeMethods.GetClipboardData(format), SnapshotByteLimit - total);
                if (data is null) continue;
                total += data.Length;
                items.Add((format, data));
            }
        }
        return new ClipboardSnapshot(items);
    }

    public void Restore(ClipboardSnapshot snapshot)
    {
        using (Open())
        {
            NativeMethods.EmptyClipboard();
            foreach (var (format, data) in snapshot.Items) SetGlobal(format, data);
        }
    }

    public string? GetText()
    {
        using (Open())
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (handle == 0) return null;
            var ptr = NativeMethods.GlobalLock(handle);
            if (ptr == 0) return null;
            try
            {
                var maxChars = (int)Math.Min((long)NativeMethods.GlobalSize(handle) / 2, int.MaxValue);
                return ReadNullTerminated(ptr, maxChars);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
    }

    /// <summary>
    /// Replaces the clipboard with <paramref name="text"/>. Sensitive text also gets the formats that keep it out
    /// of clipboard history and cloud clipboard (SEC-04). Returns the sequence number right after the write.
    /// </summary>
    public uint SetText(string text, bool sensitive)
    {
        using (Open())
        {
            NativeMethods.EmptyClipboard();
            var bytes = new byte[(text.Length + 1) * 2];
            try
            {
                MemoryMarshal.AsBytes(text.AsSpan()).CopyTo(bytes);
                SetGlobal(NativeMethods.CF_UNICODETEXT, bytes);
            }
            finally
            {
                Array.Clear(bytes);
            }
            if (sensitive)
            {
                var zeroDword = new byte[4];
                foreach (var format in PrivacyFormats.Value) SetGlobal(format, zeroDword);
            }
        }
        return SequenceNumber;
    }

    /// <summary>Empties the clipboard only if nothing was copied since <paramref name="sequenceNumber"/>.</summary>
    public bool ClearIfUnchanged(uint sequenceNumber)
    {
        if (SequenceNumber != sequenceNumber) return false;
        using (Open())
        {
            if (SequenceNumber != sequenceNumber) return false;
            NativeMethods.EmptyClipboard();
        }
        return true;
    }

    private ClipboardLease Open()
    {
        for (var attempt = 0; attempt < OpenRetries; attempt++)
        {
            if (NativeMethods.OpenClipboard(ownerWindow)) return new ClipboardLease();
            Thread.Sleep(OpenRetryDelayMs);
        }
        throw new ClipboardBusyException();
    }

    private static unsafe string ReadNullTerminated(nint ptr, int maxChars)
    {
        var span = new ReadOnlySpan<char>((void*)ptr, maxChars);
        var end = span.IndexOf('\0');
        return new string(end >= 0 ? span[..end] : span);
    }

    private static unsafe byte[]? ReadGlobal(nint handle, long budget)
    {
        if (handle == 0) return null;
        var size = (long)NativeMethods.GlobalSize(handle);
        if (size <= 0 || size > budget || size > int.MaxValue) return null;
        var ptr = NativeMethods.GlobalLock(handle);
        if (ptr == 0) return null;
        try
        {
            return new ReadOnlySpan<byte>((void*)ptr, (int)size).ToArray();
        }
        finally
        {
            NativeMethods.GlobalUnlock(handle);
        }
    }

    private static unsafe void SetGlobal(uint format, byte[] data)
    {
        var handle = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (nuint)Math.Max(data.Length, 1));
        if (handle == 0) return;
        var ptr = NativeMethods.GlobalLock(handle);
        if (ptr == 0)
        {
            NativeMethods.GlobalFree(handle);
            return;
        }
        data.CopyTo(new Span<byte>((void*)ptr, data.Length));
        NativeMethods.GlobalUnlock(handle);
        // On success the system owns the memory; on failure it is still ours to free.
        if (NativeMethods.SetClipboardData(format, handle) == 0) NativeMethods.GlobalFree(handle);
    }

    private readonly struct ClipboardLease : IDisposable
    {
        public void Dispose() => NativeMethods.CloseClipboard();
    }
}
