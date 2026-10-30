using System.Text;

namespace DevWheel.Core.Tools;

/// <summary>Common shape for tools: metadata plus a run method that turns <see cref="ToolInputException"/> into a failure.</summary>
public abstract class ToolBase : IDevTool
{
    public abstract string Id { get; }
    public abstract string Group { get; }
    public abstract string Name { get; }
    public abstract string Label { get; }
    public abstract string Hint { get; }
    public virtual bool RequiresSelection => true;
    public virtual bool IsSensitive => false;
    public virtual OutputMode DefaultOutput => OutputMode.ReplaceSelection;

    public async Task<ToolResult> RunAsync(ToolContext ctx, CancellationToken ct)
    {
        if (RequiresSelection && string.IsNullOrEmpty(ctx.SelectedText)) return new ToolResult.Failure("Select some text first");
        try
        {
            return await RunCoreAsync(ctx, ct).ConfigureAwait(true);
        }
        catch (ToolInputException e)
        {
            return new ToolResult.Failure(e.Message);
        }
    }

    protected abstract Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct);

    /// <summary>UTF-8 that throws on invalid bytes instead of inserting replacement characters.</summary>
    protected static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    protected static string DecodeUtf8(byte[] bytes, string error)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new ToolInputException(error);
        }
    }
}

/// <summary>A selection-in, text-out tool with no prompts.</summary>
public abstract class TextTransformTool : ToolBase
{
    /// <summary>Toast prefix, e.g. "Base64 encoded".</summary>
    protected abstract string DoneMessage { get; }

    protected abstract string Transform(string input);

    protected override Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        var output = Transform(ctx.SelectedText!);
        return Task.FromResult<ToolResult>(new ToolResult.Success(output, $"{DoneMessage}, {output.Length} chars"));
    }
}
