using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DevWheel.Core.Tools;

/// <summary>Decodes a JWT header and payload. The signature is never verified.</summary>
public sealed class JwtDecodeTool(TimeProvider time) : ToolBase
{
    public override string Id => "jwt.decode";
    public override string Group => ToolGroups.Inspect;
    public override string Name => "JWT decode";
    public override string Label => "JWT";
    public override string Hint => "Header + payload; signature NOT verified";
    public override bool IsSensitive => true;
    public override OutputMode DefaultOutput => OutputMode.Viewer;

    private static readonly string[] TimeClaims = ["exp", "nbf", "iat", "auth_time"];

    protected override Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        var token = ctx.SelectedText!.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) token = token[7..].Trim();

        var parts = token.Split('.');
        if (parts.Length == 5) throw new ToolInputException("Encrypted JWT (JWE) can't be decoded");
        if (parts.Length != 3) throw new ToolInputException("Not a JWT (expected header.payload.signature)");

        using var header = ParsePart(parts[0], "header");
        using var payload = ParsePart(parts[1], "payload");

        var sb = new StringBuilder();
        sb.AppendLine("⚠ Signature NOT verified").AppendLine();
        sb.AppendLine("Header").AppendLine(JsonText.Write(header.RootElement, indented: true)).AppendLine();
        sb.AppendLine("Payload").AppendLine(JsonText.Write(payload.RootElement, indented: true));

        var claims = DescribeTimeClaims(payload.RootElement);
        if (claims.Count > 0)
        {
            sb.AppendLine().AppendLine("Times");
            foreach (var line in claims) sb.AppendLine(line);
        }

        return Task.FromResult<ToolResult>(new ToolResult.View("JWT (signature NOT verified)", sb.ToString().TrimEnd(), null, "JWT decoded"));
    }

    private static JsonDocument ParsePart(string part, string name)
    {
        var bytes = Base64Url.Decode(part);
        try
        {
            return JsonDocument.Parse(bytes);
        }
        catch (JsonException)
        {
            throw new ToolInputException($"JWT {name} is not JSON");
        }
    }

    private List<string> DescribeTimeClaims(JsonElement payload)
    {
        var lines = new List<string>();
        if (payload.ValueKind != JsonValueKind.Object) return lines;
        var now = time.GetUtcNow();
        foreach (var claim in TimeClaims)
        {
            if (!payload.TryGetProperty(claim, out var value) || !value.TryGetInt64(out var seconds)) continue;
            DateTimeOffset at;
            try
            {
                at = DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }
            var local = TimeZoneInfo.ConvertTime(at, time.LocalTimeZone);
            var relative = claim == "exp"
                ? (at > now ? $"expires in {Humanize(at - now)}" : $"expired {Humanize(now - at)} ago")
                : (at > now ? $"in {Humanize(at - now)}" : $"{Humanize(now - at)} ago");
            lines.Add($"{claim,-9} {local.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)} · {relative}");
        }
        return lines;
    }

    public static string Humanize(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = span.Negate();
        if (span.TotalSeconds < 60) return $"{(int)span.TotalSeconds} s";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min";
        if (span.TotalHours < 48) return $"{(int)span.TotalHours} h";
        return $"{(int)span.TotalDays} days";
    }
}

/// <summary>Unix timestamp (seconds or milliseconds) to ISO 8601 UTC, or a date back to Unix seconds.</summary>
public sealed class TimestampTool(TimeProvider time) : ToolBase
{
    public override string Id => "timestamp.convert";
    public override string Group => ToolGroups.Inspect;
    public override string Name => "Unix timestamp ⇄ ISO date";
    public override string Label => "Unix\ntime";
    public override string Hint => "Number to date, or date to Unix seconds";
    public override OutputMode DefaultOutput => OutputMode.Viewer;

    // 12+ digits is read as milliseconds: 12-digit seconds would be past year 5000.
    private const int MillisecondDigits = 12;

    protected override Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        var input = ctx.SelectedText!.Trim();
        return Task.FromResult(long.TryParse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? FromUnix(number, input.TrimStart('-').Length >= MillisecondDigits)
            : ToUnix(input));
    }

    private ToolResult FromUnix(long number, bool milliseconds)
    {
        DateTimeOffset at;
        try
        {
            at = milliseconds ? DateTimeOffset.FromUnixTimeMilliseconds(number) : DateTimeOffset.FromUnixTimeSeconds(number);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ToolInputException("Timestamp out of range");
        }

        var iso = FormatIso(at);
        var content = new StringBuilder()
            .AppendLine($"Unix      {number} ({(milliseconds ? "milliseconds" : "seconds")})")
            .AppendLine($"UTC       {iso}")
            .AppendLine($"Local     {FormatLocal(at)}")
            .Append($"Relative  {Relative(at)}")
            .ToString();
        return new ToolResult.View("Unix timestamp", content, iso, "Copied ISO date");
    }

    private ToolResult ToUnix(string input)
    {
        if (!DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
        {
            throw new ToolInputException("Not a Unix timestamp or a date");
        }

        var seconds = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var content = new StringBuilder()
            .AppendLine($"Unix      {seconds} (seconds)")
            .AppendLine($"Unix ms   {at.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}")
            .AppendLine($"UTC       {FormatIso(at)}")
            .AppendLine($"Local     {FormatLocal(at)}")
            .Append($"Relative  {Relative(at)}")
            .ToString();
        return new ToolResult.View("Unix timestamp", content, seconds, "Copied Unix seconds");
    }

    public static string FormatIso(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        var format = utc.Millisecond != 0 ? "yyyy-MM-dd'T'HH:mm:ss.fff'Z'" : "yyyy-MM-dd'T'HH:mm:ss'Z'";
        return utc.ToString(format, CultureInfo.InvariantCulture);
    }

    private string FormatLocal(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, time.LocalTimeZone).ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

    private string Relative(DateTimeOffset at)
    {
        var now = time.GetUtcNow();
        return at > now ? $"in {JwtDecodeTool.Humanize(at - now)}" : $"{JwtDecodeTool.Humanize(now - at)} ago";
    }
}
