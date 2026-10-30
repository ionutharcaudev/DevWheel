using System.Security.Cryptography;
using System.Text;

namespace DevWheel.Core.Tools;

public abstract class HashToolBase : ToolBase
{
    public override string Group => ToolGroups.Hash;
    public override OutputMode DefaultOutput => OutputMode.Clipboard;

    protected abstract byte[] Hash(byte[] data);

    protected override Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        var hex = Convert.ToHexString(Hash(Encoding.UTF8.GetBytes(ctx.SelectedText!))).ToLowerInvariant();
        return Task.FromResult<ToolResult>(new ToolResult.Success(hex, $"{Name} copied"));
    }
}

public sealed class Sha256Tool : HashToolBase
{
    public override string Id => "hash.sha256";
    public override string Name => "SHA-256";
    public override string Label => "SHA\n256";
    public override string Hint => "Hex digest of the UTF-8 text";

    protected override byte[] Hash(byte[] data) => SHA256.HashData(data);
}

public sealed class Sha1Tool : HashToolBase
{
    public override string Id => "hash.sha1";
    public override string Name => "SHA-1 (legacy)";
    public override string Label => "SHA1";
    public override string Hint => "Legacy, not for security";

    protected override byte[] Hash(byte[] data) => SHA1.HashData(data);
}

public sealed class Md5Tool : HashToolBase
{
    public override string Id => "hash.md5";
    public override string Name => "MD5 (legacy)";
    public override string Label => "MD5";
    public override string Hint => "Legacy, not for security";

    protected override byte[] Hash(byte[] data) => MD5.HashData(data);
}

public sealed class HmacSha256Tool : ToolBase
{
    public override string Id => "hash.hmac256";
    public override string Group => ToolGroups.Hash;
    public override string Name => "HMAC-SHA256";
    public override string Label => "HMAC";
    public override string Hint => "Asks for a key; hex output";
    public override bool IsSensitive => true;
    public override OutputMode DefaultOutput => OutputMode.Clipboard;

    protected override async Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        var keyText = await ctx.Prompts.AskKeyAsync("HMAC-SHA256 key", "Text key, used as UTF-8 bytes", ct).ConfigureAwait(true);
        if (string.IsNullOrEmpty(keyText)) return new ToolResult.Cancelled();

        var key = Encoding.UTF8.GetBytes(keyText);
        try
        {
            return new ToolResult.Success(Compute(key, ctx.SelectedText!), "HMAC-SHA256 copied");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static string Compute(byte[] key, string message) =>
        Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
}
