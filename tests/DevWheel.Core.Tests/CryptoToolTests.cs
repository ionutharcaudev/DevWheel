using System.Security.Cryptography;
using System.Text;
using DevWheel.Core.Tools;

namespace DevWheel.Core.Tests;

public class AesEncryptToolTests
{
    private static readonly string RawKey = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());

    [Fact]
    public void Round_trips_with_raw_key()
    {
        var cipher = Run.Output(new AesEncryptTool(), "secret ăî €", new FakePrompts { Key = RawKey });
        Assert.Equal("secret ăî €", Run.Output(new AesDecryptTool(), cipher, new FakePrompts { Key = RawKey }));
    }

    [Fact]
    public void Round_trips_with_passphrase()
    {
        var cipher = Run.Output(new AesEncryptTool(), "hello", new FakePrompts { Key = "correct horse battery staple" });
        Assert.Equal("hello", Run.Output(new AesDecryptTool(), cipher, new FakePrompts { Key = "correct horse battery staple" }));
    }

    [Fact]
    public void Raw_key_uses_zero_salt_and_documented_layout()
    {
        var blob = Convert.FromBase64String(AesGcmFormat.Encrypt("abc", RawKey));
        Assert.Equal(1 + 16 + 12 + 3 + 16, blob.Length);
        Assert.Equal(0x01, blob[0]);
        Assert.All(blob[1..17], b => Assert.Equal(0, b));
    }

    [Fact]
    public void Passphrase_uses_random_salt()
    {
        var blob = Convert.FromBase64String(AesGcmFormat.Encrypt("abc", "pass phrase"));
        Assert.Contains(blob[1..17], b => b != 0);
    }

    [Fact]
    public void Same_input_gives_different_ciphertext() =>
        Assert.NotEqual(AesGcmFormat.Encrypt("abc", RawKey), AesGcmFormat.Encrypt("abc", RawKey));

    [Fact]
    public void Decrypts_independently_built_ciphertext()
    {
        // Built with AesGcm directly from the documented layout, as another team would.
        var key = Convert.FromBase64String(RawKey);
        var nonce = Enumerable.Repeat((byte)7, 12).ToArray();
        var plain = Encoding.UTF8.GetBytes("interop");
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plain, cipher, tag);
        var blob = new byte[] { 0x01 }.Concat(new byte[16]).Concat(nonce).Concat(cipher).Concat(tag).ToArray();

        Assert.Equal("interop", AesGcmFormat.Decrypt(Convert.ToBase64String(blob), RawKey));
    }

    [Fact]
    public void Pbkdf2_matches_known_vector()
    {
        // PBKDF2-HMAC-SHA256("password", "salt", 1 iteration, 32 bytes), from RFC 7914 style vectors.
        var derived = Rfc2898DeriveBytes.Pbkdf2("password"u8, "salt"u8, 1, HashAlgorithmName.SHA256, 32);
        Assert.Equal("120fb6cffcf8b32c43e7225256c4f837a86548c92ccc35480805987cb70be17b", Convert.ToHexString(derived).ToLowerInvariant());
        Assert.Equal(600_000, AesGcmFormat.Pbkdf2Iterations);
    }

    [Fact]
    public void Cancelled_prompt_cancels() =>
        Assert.IsType<ToolResult.Cancelled>(Run.Tool(new AesEncryptTool(), "x", new FakePrompts { Key = null }));
}

public class AesDecryptToolTests
{
    private static readonly string RawKey = Convert.ToBase64String(new byte[32]);

    [Fact]
    public void Wrong_key_fails()
    {
        var cipher = AesGcmFormat.Encrypt("hello", "right passphrase");
        Assert.Equal("Wrong key, or the text was changed", Run.Failure(new AesDecryptTool(), cipher, new FakePrompts { Key = "wrong passphrase" }));
    }

    [Fact]
    public void Tampered_ciphertext_fails()
    {
        var blob = Convert.FromBase64String(AesGcmFormat.Encrypt("hello", RawKey));
        blob[^20] ^= 1;
        Assert.Equal("Wrong key, or the text was changed", Run.Failure(new AesDecryptTool(), Convert.ToBase64String(blob), new FakePrompts { Key = RawKey }));
    }

    [Fact]
    public void Raw_key_ciphertext_needs_raw_key()
    {
        var cipher = AesGcmFormat.Encrypt("hello", RawKey);
        Assert.StartsWith("This text was encrypted with a raw key", Run.Failure(new AesDecryptTool(), cipher, new FakePrompts { Key = "a passphrase" }));
    }

    [Theory]
    [InlineData("not base64 !!", "Not DevWheel AES ciphertext (not Base64)")]
    [InlineData("AAAA", "Not DevWheel AES ciphertext (too short)")]
    public void Rejects_other_text_before_asking_for_key(string input, string error)
    {
        var prompts = new FakePrompts { Key = RawKey };
        Assert.Equal(error, Run.Failure(new AesDecryptTool(), input, prompts));
        Assert.Equal(0, prompts.KeyPrompts);
    }

    [Fact]
    public void Rejects_unknown_version()
    {
        var blob = Convert.FromBase64String(AesGcmFormat.Encrypt("hello", RawKey));
        blob[0] = 2;
        Assert.Equal("Unsupported ciphertext version 2", Run.Failure(new AesDecryptTool(), Convert.ToBase64String(blob), new FakePrompts { Key = RawKey }));
    }
}
