using DevWheel.Core.Tools;

namespace DevWheel.Core.Tests;

public class Base64EncodeToolTests
{
    // RFC 4648 section 10 test vectors.
    [Theory]
    [InlineData("f", "Zg==")]
    [InlineData("fo", "Zm8=")]
    [InlineData("foo", "Zm9v")]
    [InlineData("foob", "Zm9vYg==")]
    [InlineData("fooba", "Zm9vYmE=")]
    [InlineData("foobar", "Zm9vYmFy")]
    [InlineData("ăîșț €", "xIPDrsiZyJsg4oKs")]
    public void Encodes_utf8(string input, string expected) => Assert.Equal(expected, Run.Output(new Base64EncodeTool(), input));

    [Fact]
    public void Needs_a_selection() => Assert.Equal("Select some text first", Run.Failure(new Base64EncodeTool(), null));

    [Fact]
    public void Message_reports_length()
    {
        var result = Assert.IsType<ToolResult.Success>(Run.Tool(new Base64EncodeTool(), "foobar"));
        Assert.Equal("Base64 encoded, 8 chars", result.Message);
    }
}

public class Base64DecodeToolTests
{
    [Theory]
    [InlineData("Zm9vYmFy", "foobar")]
    [InlineData("  Zm9v\r\nYmFy  ", "foobar")]
    [InlineData("xIPDrsiZyJsg4oKs", "ăîșț €")]
    public void Decodes(string input, string expected) => Assert.Equal(expected, Run.Output(new Base64DecodeTool(), input));

    [Fact]
    public void Rejects_invalid_base64() => Assert.Equal("Not valid Base64", Run.Failure(new Base64DecodeTool(), "not base64!"));

    [Fact]
    public void Rejects_binary_that_is_not_utf8() =>
        Assert.Equal("Decoded bytes are not UTF-8 text", Run.Failure(new Base64DecodeTool(), "/w=="));
}

public class Base64UrlEncodeToolTests
{
    [Theory]
    [InlineData("foobar", "Zm9vYmFy")]
    [InlineData("f", "Zg")]
    [InlineData("??>", "Pz8-")]
    [InlineData("???", "Pz8_")]
    public void Encodes_without_padding(string input, string expected) =>
        Assert.Equal(expected, Run.Output(new Base64UrlEncodeTool(), input));
}

public class Base64UrlDecodeToolTests
{
    [Theory]
    [InlineData("Zg", "f")]
    [InlineData("Zg==", "f")]
    [InlineData("Pz8-", "??>")]
    [InlineData("Pz8_", "???")]
    public void Decodes_with_or_without_padding(string input, string expected) =>
        Assert.Equal(expected, Run.Output(new Base64UrlDecodeTool(), input));

    [Theory]
    [InlineData("Pz8+")]
    [InlineData("a")]
    [InlineData("ab cd")]
    public void Rejects_invalid(string input) => Assert.Equal("Not valid Base64URL", Run.Failure(new Base64UrlDecodeTool(), input));
}

public class UrlEncodeToolTests
{
    [Theory]
    [InlineData("a b&c=d/é", "a%20b%26c%3Dd%2F%C3%A9")]
    [InlineData("safe-._~", "safe-._~")]
    public void Encodes(string input, string expected) => Assert.Equal(expected, Run.Output(new UrlEncodeTool(), input));
}

public class UrlDecodeToolTests
{
    [Theory]
    [InlineData("a%20b%26c%3Dd%2F%C3%A9", "a b&c=d/é")]
    [InlineData("a+b", "a b")]
    public void Decodes(string input, string expected) => Assert.Equal(expected, Run.Output(new UrlDecodeTool(), input));
}

public class HtmlEncodeToolTests
{
    [Fact]
    public void Encodes_special_characters() =>
        Assert.Equal("&lt;a href=&quot;x&quot;&gt;Tom &amp; Jerry&#39;s&lt;/a&gt;", Run.Output(new HtmlEncodeTool(), "<a href=\"x\">Tom & Jerry's</a>"));
}

public class HtmlDecodeToolTests
{
    [Fact]
    public void Decodes_named_and_numeric_entities() =>
        Assert.Equal("<b>\"x\" & 'y' ©</b>", Run.Output(new HtmlDecodeTool(), "&lt;b&gt;&quot;x&quot; &amp; &#39;y&#x27; &copy;&lt;/b&gt;"));
}
