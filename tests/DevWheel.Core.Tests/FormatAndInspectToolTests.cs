using DevWheel.Core.Tools;

namespace DevWheel.Core.Tests;

public class JsonPrettyToolTests
{
    [Fact]
    public void Indents_and_keeps_unicode()
    {
        var output = Run.Output(new JsonPrettyTool(), "{\"a\":1,\"b\":[true,null],\"name\":\"Ștefan\"}");
        var expected = "{\n  \"a\": 1,\n  \"b\": [\n    true,\n    null\n  ],\n  \"name\": \"Ștefan\"\n}";
        Assert.Equal(expected, output.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Accepts_comments_and_trailing_commas() =>
        Assert.Equal("{\"a\":1}", Run.Output(new JsonMinifyTool(), "{ // note\n \"a\": 1, }"));

    [Fact]
    public void Reports_position_of_invalid_json() =>
        Assert.StartsWith("Not valid JSON (line 2", Run.Failure(new JsonPrettyTool(), "{\n  \"a\": }"));
}

public class JsonMinifyToolTests
{
    [Fact]
    public void Removes_whitespace() =>
        Assert.Equal("{\"a\":[1,2],\"b\":{\"c\":\"d e\"}}", Run.Output(new JsonMinifyTool(), "{\n  \"a\": [ 1, 2 ],\n  \"b\": { \"c\": \"d e\" }\n}"));
}

public class JwtDecodeToolTests
{
    // jwt.io sample token: {"alg":"HS256","typ":"JWT"} . {"sub":"1234567890","name":"John Doe","iat":1516239022}
    private const string Token =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    private static readonly FixedTime Now = new(DateTimeOffset.FromUnixTimeSeconds(1516239022 + 3 * 3600));

    [Fact]
    public void Decodes_header_payload_and_times()
    {
        var view = Assert.IsType<ToolResult.View>(Run.Tool(new JwtDecodeTool(Now), "Bearer " + Token));
        Assert.Contains("Signature NOT verified", view.Content);
        Assert.Contains("\"alg\": \"HS256\"", view.Content);
        Assert.Contains("\"name\": \"John Doe\"", view.Content);
        Assert.Contains("iat       2018-01-18 01:30:22 +00:00 · 3 h ago", view.Content);
        Assert.Null(view.CopyText);
    }

    [Fact]
    public void Describes_expiry()
    {
        var payload = Base64Url.Encode("{\"exp\":1516246222}"u8);
        var view = Assert.IsType<ToolResult.View>(Run.Tool(new JwtDecodeTool(new FixedTime(DateTimeOffset.FromUnixTimeSeconds(1516239022))), $"eyJhbGciOiJub25lIn0.{payload}."));
        Assert.Contains("expires in 2 h", view.Content);
    }

    [Theory]
    [InlineData("abc", "Not a JWT (expected header.payload.signature)")]
    [InlineData("a.b.c.d.e", "Encrypted JWT (JWE) can't be decoded")]
    [InlineData("Zm9v.Zm9v.x", "JWT header is not JSON")]
    public void Rejects_non_jwt(string input, string error) => Assert.Equal(error, Run.Failure(new JwtDecodeTool(Now), input));

    [Fact]
    public void Is_sensitive_viewer() => Assert.True(new JwtDecodeTool(Now) is { IsSensitive: true, DefaultOutput: OutputMode.Viewer });
}

public class TimestampToolTests
{
    private static readonly FixedTime Now = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData("1700000000", "2023-11-14T22:13:20Z")]
    [InlineData("1700000000123", "2023-11-14T22:13:20.123Z")]
    [InlineData("0", "1970-01-01T00:00:00Z")]
    public void Unix_to_iso(string input, string iso)
    {
        var view = Assert.IsType<ToolResult.View>(Run.Tool(new TimestampTool(Now), input));
        Assert.Equal(iso, view.CopyText);
        Assert.Contains($"UTC       {iso}", view.Content);
    }

    [Theory]
    [InlineData("2023-11-14T22:13:20Z", "1700000000")]
    [InlineData("2023-11-15T00:13:20+02:00", "1700000000")]
    [InlineData("2023-11-14 22:13:20", "1700000000")]
    public void Date_to_unix(string input, string seconds)
    {
        var view = Assert.IsType<ToolResult.View>(Run.Tool(new TimestampTool(Now), input));
        Assert.Equal(seconds, view.CopyText);
    }

    [Fact]
    public void Shows_relative_time()
    {
        var view = Assert.IsType<ToolResult.View>(Run.Tool(new TimestampTool(Now), "1761825600")); // 2025-10-30T12:00Z
        Assert.Contains("365 days ago", view.Content);
    }

    [Fact]
    public void Rejects_other_text() => Assert.Equal("Not a Unix timestamp or a date", Run.Failure(new TimestampTool(Now), "hello"));
}
