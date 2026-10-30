using System.Net;
using System.Text;

namespace DevWheel.Core.Tools;

public sealed class Base64EncodeTool : TextTransformTool
{
    public override string Id => "base64.encode";
    public override string Group => ToolGroups.Encode;
    public override string Name => "Base64 encode";
    public override string Label => "B64\nenc";
    public override string Hint => "UTF-8 text to Base64";
    protected override string DoneMessage => "Base64 encoded";

    protected override string Transform(string input) => Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
}

public sealed class Base64DecodeTool : TextTransformTool
{
    public override string Id => "base64.decode";
    public override string Group => ToolGroups.Encode;
    public override string Name => "Base64 decode";
    public override string Label => "B64\ndec";
    public override string Hint => "Base64 to UTF-8 text";
    protected override string DoneMessage => "Base64 decoded";

    protected override string Transform(string input)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(input.Trim());
        }
        catch (FormatException)
        {
            throw new ToolInputException("Not valid Base64");
        }
        return DecodeUtf8(bytes, "Decoded bytes are not UTF-8 text");
    }
}

public sealed class Base64UrlEncodeTool : TextTransformTool
{
    public override string Id => "base64url.encode";
    public override string Group => ToolGroups.Encode;
    public override string Name => "Base64URL encode";
    public override string Label => "B64U\nenc";
    public override string Hint => "URL-safe Base64, no padding";
    protected override string DoneMessage => "Base64URL encoded";

    protected override string Transform(string input) => Base64Url.Encode(Encoding.UTF8.GetBytes(input));
}

public sealed class Base64UrlDecodeTool : TextTransformTool
{
    public override string Id => "base64url.decode";
    public override string Group => ToolGroups.Encode;
    public override string Name => "Base64URL decode";
    public override string Label => "B64U\ndec";
    public override string Hint => "URL-safe Base64 to UTF-8 text";
    protected override string DoneMessage => "Base64URL decoded";

    protected override string Transform(string input) =>
        DecodeUtf8(Base64Url.Decode(input.Trim()), "Decoded bytes are not UTF-8 text");
}

public sealed class UrlEncodeTool : TextTransformTool
{
    public override string Id => "url.encode";
    public override string Group => ToolGroups.Encode;
    public override string Name => "URL encode";
    public override string Label => "URL\nenc";
    public override string Hint => "Percent-encode (RFC 3986)";
    protected override string DoneMessage => "URL encoded";

    protected override string Transform(string input) => Uri.EscapeDataString(input);
}

public sealed class UrlDecodeTool : TextTransformTool
{
    public override string Id => "url.decode";
    public override string Group => ToolGroups.Encode;
    public override string Name => "URL decode";
    public override string Label => "URL\ndec";
    public override string Hint => "Percent-decode; + becomes a space";
    protected override string DoneMessage => "URL decoded";

    protected override string Transform(string input) => WebUtility.UrlDecode(input);
}

public sealed class HtmlEncodeTool : TextTransformTool
{
    public override string Id => "html.encode";
    public override string Group => ToolGroups.Encode;
    public override string Name => "HTML entity encode";
    public override string Label => "HTML\nenc";
    public override string Hint => "Escape < > & \" '";
    protected override string DoneMessage => "HTML encoded";

    protected override string Transform(string input) => WebUtility.HtmlEncode(input);
}

public sealed class HtmlDecodeTool : TextTransformTool
{
    public override string Id => "html.decode";
    public override string Group => ToolGroups.Encode;
    public override string Name => "HTML entity decode";
    public override string Label => "HTML\ndec";
    public override string Hint => "Unescape HTML entities";
    protected override string DoneMessage => "HTML decoded";

    protected override string Transform(string input) => WebUtility.HtmlDecode(input);
}

/// <summary>Base64URL (RFC 4648 section 5) without padding.</summary>
public static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string input)
    {
        var s = input.TrimEnd('=');
        if (s.Length % 4 == 1 || s.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')))
        {
            throw new ToolInputException("Not valid Base64URL");
        }
        s = s.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        try
        {
            return Convert.FromBase64String(s);
        }
        catch (FormatException)
        {
            throw new ToolInputException("Not valid Base64URL");
        }
    }
}
