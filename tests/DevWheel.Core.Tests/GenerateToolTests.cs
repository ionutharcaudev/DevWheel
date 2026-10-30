using DevWheel.Core.Tools;

namespace DevWheel.Core.Tests;

public class GuidToolTests
{
    [Fact]
    public void Generates_uuid_v4()
    {
        var output = Run.Output(new GuidTool(), null);
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", output);
        Assert.NotEqual(output, Run.Output(new GuidTool(), null));
    }
}

public class RandomSecretToolTests
{
    [Theory]
    [InlineData(16, SecretFormat.Hex, 32)]
    [InlineData(32, SecretFormat.Hex, 64)]
    [InlineData(64, SecretFormat.Hex, 128)]
    [InlineData(32, SecretFormat.Base64, 44)]
    public void Uses_default_options(int bytes, SecretFormat format, int length)
    {
        var options = new ToolOptions(new GeneratorSettings { Secret = new SecretOptions(bytes, format) }, false);
        var output = Run.Output(new RandomSecretTool(), null, options: options);
        Assert.Equal(length, output.Length);
        if (format == SecretFormat.Hex) Assert.Matches("^[0-9a-f]+$", output);
        else Assert.Equal(bytes, Convert.FromBase64String(output).Length);
    }

    [Fact]
    public void Shift_click_asks_for_options()
    {
        var prompts = new FakePrompts { SecretAnswer = new SecretOptions(16, SecretFormat.Hex) };
        var output = Run.Output(new RandomSecretTool(), null, prompts, ToolOptions.Default with { AskForOptions = true });
        Assert.Equal(32, output.Length);
        Assert.Equal(1, prompts.OptionPrompts);
    }

    [Fact]
    public void Cancelled_options_cancel() =>
        Assert.IsType<ToolResult.Cancelled>(Run.Tool(new RandomSecretTool(), null, new FakePrompts(), ToolOptions.Default with { AskForOptions = true }));

    [Fact]
    public void Does_not_repeat() => Assert.NotEqual(Run.Output(new RandomSecretTool(), null), Run.Output(new RandomSecretTool(), null));

    [Fact]
    public void Is_sensitive() => Assert.True(new RandomSecretTool().IsSensitive);
}

public class LaravelKeyToolTests
{
    [Fact]
    public void Has_laravel_format()
    {
        var output = Run.Output(new LaravelKeyTool(), null);
        Assert.StartsWith("base64:", output);
        Assert.Equal(32, Convert.FromBase64String(output["base64:".Length..]).Length);
    }

    [Fact]
    public void Is_sensitive() => Assert.True(new LaravelKeyTool().IsSensitive);
}

public class PasswordToolTests
{
    [Fact]
    public void Contains_every_chosen_set()
    {
        for (var i = 0; i < 200; i++)
        {
            var password = PasswordTool.Generate(new PasswordOptions(8));
            Assert.Equal(8, password.Length);
            Assert.Contains(password, char.IsAsciiLetterUpper);
            Assert.Contains(password, char.IsAsciiLetterLower);
            Assert.Contains(password, char.IsAsciiDigit);
            Assert.Contains(password, c => PasswordTool.SymbolChars.Contains(c));
        }
    }

    [Fact]
    public void Uses_only_chosen_sets()
    {
        var password = PasswordTool.Generate(new PasswordOptions(64, Upper: false, Lower: false, Digits: true, Symbols: false));
        Assert.Matches("^[0-9]{64}$", password);
    }

    [Fact]
    public void Characters_are_spread_evenly()
    {
        // 26 * 4000 draws: every lowercase letter should appear roughly 4000 times.
        var counts = new int[26];
        foreach (var c in PasswordTool.Generate(new PasswordOptions(256, false, true, false, false)) +
                          string.Concat(Enumerable.Range(0, 405).Select(_ => PasswordTool.Generate(new PasswordOptions(256, false, true, false, false)))))
        {
            counts[c - 'a']++;
        }
        Assert.All(counts, n => Assert.InRange(n, 3400, 4600));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(257, true)]
    [InlineData(20, false)]
    public void Validates_options(int length, bool anySet)
    {
        var options = anySet ? new PasswordOptions(length) : new PasswordOptions(length, false, false, false, false);
        Assert.IsType<ToolResult.Failure>(Run.Tool(new PasswordTool(), null, options: new ToolOptions(new GeneratorSettings { Password = options }, false)));
    }
}
