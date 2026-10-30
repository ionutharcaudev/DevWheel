namespace DevWheel.Core;

public enum OutputMode
{
    ReplaceSelection,
    Clipboard,
    Viewer,
}

public interface IDevTool
{
    string Id { get; }                // "base64.encode"
    string Group { get; }             // "Encode"
    string Name { get; }              // "Base64 encode"
    string Label { get; }             // short text drawn on the wheel segment, e.g. "B64\nenc"
    string Hint { get; }              // shown in the hub
    bool RequiresSelection { get; }
    bool IsSensitive { get; }         // triggers clipboard exclusion + auto-clear
    OutputMode DefaultOutput { get; } // ReplaceSelection | Clipboard | Viewer
    Task<ToolResult> RunAsync(ToolContext ctx, CancellationToken ct);
}

public sealed record ToolContext(string? SelectedText, IPromptService Prompts, ToolOptions Options);

/// <summary>Per-run options: generator defaults from settings, and whether to ask instead (Shift+click).</summary>
public sealed record ToolOptions(GeneratorSettings Generators, bool AskForOptions)
{
    public static ToolOptions Default { get; } = new(new GeneratorSettings(), false);
}

public abstract record ToolResult
{
    /// <summary>Text to paste over the selection or copy, plus the toast message.</summary>
    public sealed record Success(string Output, string Message) : ToolResult;

    /// <summary>Read-only viewer content. When <paramref name="CopyText"/> is set it is also copied.</summary>
    public sealed record View(string Title, string Content, string? CopyText, string Message) : ToolResult;

    public sealed record Failure(string Message) : ToolResult;

    public sealed record Cancelled : ToolResult;
}

/// <summary>UI prompts a tool may need. Implemented by the App; faked in tests.</summary>
public interface IPromptService
{
    /// <summary>Masked key prompt. Returns null when cancelled. The value is never remembered.</summary>
    Task<string?> AskKeyAsync(string title, string hint, CancellationToken ct);

    Task<SecretOptions?> AskSecretOptionsAsync(SecretOptions defaults, CancellationToken ct);

    Task<PasswordOptions?> AskPasswordOptionsAsync(PasswordOptions defaults, CancellationToken ct);
}

/// <summary>Thrown by tools for invalid input; the message is shown to the user as is.</summary>
public sealed class ToolInputException(string message) : Exception(message);
