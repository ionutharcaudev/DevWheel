using DevWheel.Core.Tools;

namespace DevWheel.Core.Tests;

public class Sha256ToolTests
{
    // FIPS 180 / NIST example vectors.
    [Theory]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1")]
    public void Hashes(string input, string hex) => Assert.Equal(hex, Run.Output(new Sha256Tool(), input));

    [Fact]
    public void Copies_to_clipboard() => Assert.Equal(OutputMode.Clipboard, new Sha256Tool().DefaultOutput);
}

public class Sha1ToolTests
{
    [Fact]
    public void Hashes() => Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", Run.Output(new Sha1Tool(), "abc"));

    [Fact]
    public void Is_labelled_legacy() => Assert.Contains("not for security", new Sha1Tool().Hint);
}

public class Md5ToolTests
{
    // RFC 1321 test suite.
    [Theory]
    [InlineData("abc", "900150983cd24fb0d6963f7d28e17f72")]
    [InlineData("message digest", "f96b697d7cb7938d525a2f31aaf161d0")]
    public void Hashes(string input, string hex) => Assert.Equal(hex, Run.Output(new Md5Tool(), input));

    [Fact]
    public void Is_labelled_legacy() => Assert.Contains("not for security", new Md5Tool().Hint);
}

public class HmacSha256ToolTests
{
    // RFC 4231 test case 2.
    [Fact]
    public void Matches_rfc4231()
    {
        var prompts = new FakePrompts { Key = "Jefe" };
        Assert.Equal("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843",
            Run.Output(new HmacSha256Tool(), "what do ya want for nothing?", prompts));
        Assert.Equal(1, prompts.KeyPrompts);
    }

    [Fact]
    public void Cancelled_prompt_cancels() =>
        Assert.IsType<ToolResult.Cancelled>(Run.Tool(new HmacSha256Tool(), "x", new FakePrompts { Key = null }));

    [Fact]
    public void Is_sensitive() => Assert.True(new HmacSha256Tool().IsSensitive);
}
