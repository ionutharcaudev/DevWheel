using System.Security.Cryptography;

namespace DevWheel.Core.Tools;

public sealed class GuidTool : ToolBase
{
    public override string Id => "generate.guid";
    public override string Group => ToolGroups.Generate;
    public override string Name => "GUID / UUID v4";
    public override string Label => "GUID";
    public override string Hint => "Random UUID v4";
    public override bool RequiresSelection => false;
    public override OutputMode DefaultOutput => OutputMode.Clipboard;

    protected override Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct) =>
        Task.FromResult<ToolResult>(new ToolResult.Success(Guid.NewGuid().ToString("D"), "GUID copied"));
}

public sealed class RandomSecretTool : ToolBase
{
    public override string Id => "generate.secret";
    public override string Group => ToolGroups.Generate;
    public override string Name => "Random secret";
    public override string Label => "Secret";
    public override string Hint => "Shift+click for length and format";
    public override bool RequiresSelection => false;
    public override bool IsSensitive => true;
    public override OutputMode DefaultOutput => OutputMode.Clipboard;

    protected override async Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        var options = ctx.Options.Generators.Secret;
        if (ctx.Options.AskForOptions)
        {
            var asked = await ctx.Prompts.AskSecretOptionsAsync(options, ct).ConfigureAwait(true);
            if (asked is null) return new ToolResult.Cancelled();
            options = asked;
        }
        if (options.Validate() is { } error) throw new ToolInputException(error);

        var bytes = RandomNumberGenerator.GetBytes(options.Bytes);
        try
        {
            var text = options.Format == SecretFormat.Hex
                ? Convert.ToHexString(bytes).ToLowerInvariant()
                : Convert.ToBase64String(bytes);
            return new ToolResult.Success(text, $"{options.Bytes}-byte secret ({options.Format})");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}

public sealed class LaravelKeyTool : ToolBase
{
    public override string Id => "generate.laravel";
    public override string Group => ToolGroups.Generate;
    public override string Name => "Laravel APP_KEY";
    public override string Label => "APP\nKEY";
    public override string Hint => "base64: + 32 random bytes";
    public override bool RequiresSelection => false;
    public override bool IsSensitive => true;
    public override OutputMode DefaultOutput => OutputMode.Clipboard;

    protected override Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        try
        {
            return Task.FromResult<ToolResult>(new ToolResult.Success("base64:" + Convert.ToBase64String(bytes), "Laravel APP_KEY"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}

public sealed class PasswordTool : ToolBase
{
    public const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    public const string DigitChars = "0123456789";
    public const string SymbolChars = "!#$%&()*+,-./:;<=>?@[]^_{|}~";

    public override string Id => "generate.password";
    public override string Group => ToolGroups.Generate;
    public override string Name => "Password";
    public override string Label => "Pass\nword";
    public override string Hint => "Shift+click for length and charset";
    public override bool RequiresSelection => false;
    public override bool IsSensitive => true;
    public override OutputMode DefaultOutput => OutputMode.Clipboard;

    protected override async Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        var options = ctx.Options.Generators.Password;
        if (ctx.Options.AskForOptions)
        {
            var asked = await ctx.Prompts.AskPasswordOptionsAsync(options, ct).ConfigureAwait(true);
            if (asked is null) return new ToolResult.Cancelled();
            options = asked;
        }
        return new ToolResult.Success(Generate(options), $"{options.Length}-char password");
    }

    /// <summary>
    /// Each character is drawn with RandomNumberGenerator.GetInt32 (no modulo bias). A result missing one of the
    /// chosen character sets is discarded and redrawn, which keeps every accepted password equally likely.
    /// </summary>
    public static string Generate(PasswordOptions options)
    {
        if (options.Validate() is { } error) throw new ToolInputException(error);

        var sets = new List<string>(4);
        if (options.Upper) sets.Add(UpperChars);
        if (options.Lower) sets.Add(LowerChars);
        if (options.Digits) sets.Add(DigitChars);
        if (options.Symbols) sets.Add(SymbolChars);
        var charset = string.Concat(sets);

        var chars = new char[options.Length];
        while (true)
        {
            for (var i = 0; i < chars.Length; i++) chars[i] = charset[RandomNumberGenerator.GetInt32(charset.Length)];
            if (sets.TrueForAll(set => chars.AsSpan().IndexOfAny(set) >= 0)) break;
        }
        var result = new string(chars);
        Array.Clear(chars);
        return result;
    }
}
