using System.Security.Cryptography;

namespace DevWheel.Core.Tools;

public sealed class AesEncryptTool : ToolBase
{
    public override string Id => "aes.encrypt";
    public override string Group => ToolGroups.Crypto;
    public override string Name => "AES-256-GCM encrypt";
    public override string Label => "AES\nenc";
    public override string Hint => "Asks for a key or passphrase";
    public override bool IsSensitive => true;

    protected override async Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        var key = await ctx.Prompts.AskKeyAsync("AES-256-GCM encrypt", AesGcmFormat.KeyHint, ct).ConfigureAwait(true);
        if (string.IsNullOrEmpty(key)) return new ToolResult.Cancelled();
        var output = AesGcmFormat.Encrypt(ctx.SelectedText!, key);
        return new ToolResult.Success(output, $"AES encrypted, {output.Length} chars");
    }
}

public sealed class AesDecryptTool : ToolBase
{
    public override string Id => "aes.decrypt";
    public override string Group => ToolGroups.Crypto;
    public override string Name => "AES-256-GCM decrypt";
    public override string Label => "AES\ndec";
    public override string Hint => "DevWheel ciphertext; asks for the key";
    public override bool IsSensitive => true;

    protected override async Task<ToolResult> RunCoreAsync(ToolContext ctx, CancellationToken ct)
    {
        // Parse first so a non-ciphertext selection fails before asking for a key.
        AesGcmFormat.Parse(ctx.SelectedText!);
        var key = await ctx.Prompts.AskKeyAsync("AES-256-GCM decrypt", AesGcmFormat.KeyHint, ct).ConfigureAwait(true);
        if (string.IsNullOrEmpty(key)) return new ToolResult.Cancelled();
        var output = AesGcmFormat.Decrypt(ctx.SelectedText!, key);
        return new ToolResult.Success(output, $"AES decrypted, {output.Length} chars");
    }
}

/// <summary>
/// Ciphertext = Base64( version(1) | salt(16, zeros if raw key) | nonce(12) | ciphertext | tag(16) ), version 0x01.
/// Key input: a Base64 string that decodes to exactly 32 bytes is a raw key; anything else is a passphrase,
/// stretched with PBKDF2-SHA256, 600,000 iterations and the random salt. See docs/ciphertext-format.md.
/// </summary>
public static class AesGcmFormat
{
    public const byte Version = 0x01;
    public const int SaltSize = 16;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int KeySize = 32;
    public const int Pbkdf2Iterations = 600_000;
    public const int HeaderSize = 1 + SaltSize + NonceSize;

    public const string KeyHint = "32-byte Base64 key, or a passphrase";

    public static string Encrypt(string plaintext, string keyInput)
    {
        var salt = new byte[SaltSize];
        byte[] key;
        if (!TryParseRawKey(keyInput, out key))
        {
            RandomNumberGenerator.Fill(salt);
            key = DeriveKey(keyInput, salt);
        }

        var plain = StrictUtf8Bytes(plaintext);
        var blob = new byte[HeaderSize + plain.Length + TagSize];
        try
        {
            blob[0] = Version;
            salt.CopyTo(blob, 1);
            var nonce = blob.AsSpan(1 + SaltSize, NonceSize);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plain, blob.AsSpan(HeaderSize, plain.Length), blob.AsSpan(HeaderSize + plain.Length, TagSize));
            return Convert.ToBase64String(blob);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public static string Decrypt(string ciphertext, string keyInput)
    {
        var blob = Parse(ciphertext);
        var salt = blob.AsSpan(1, SaltSize);
        byte[] key;
        if (!salt.ContainsAnyExcept((byte)0))
        {
            if (!TryParseRawKey(keyInput, out key))
            {
                throw new ToolInputException("This text was encrypted with a raw key; enter the 32-byte Base64 key");
            }
        }
        else
        {
            key = DeriveKey(keyInput, salt.ToArray());
        }

        var cipherLength = blob.Length - HeaderSize - TagSize;
        var plain = new byte[cipherLength];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                blob.AsSpan(1 + SaltSize, NonceSize),
                blob.AsSpan(HeaderSize, cipherLength),
                blob.AsSpan(HeaderSize + cipherLength, TagSize),
                plain);
            return DecodeUtf8(plain);
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new ToolInputException("Wrong key, or the text was changed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>Base64-decodes and checks the version and minimum length.</summary>
    public static byte[] Parse(string ciphertext)
    {
        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(ciphertext.Trim());
        }
        catch (FormatException)
        {
            throw new ToolInputException("Not DevWheel AES ciphertext (not Base64)");
        }
        if (blob.Length < HeaderSize + TagSize) throw new ToolInputException("Not DevWheel AES ciphertext (too short)");
        if (blob[0] != Version) throw new ToolInputException($"Unsupported ciphertext version {blob[0]}");
        return blob;
    }

    public static bool TryParseRawKey(string input, out byte[] key)
    {
        key = [];
        var buffer = new byte[KeySize + 3];
        if (Convert.TryFromBase64String(input.Trim(), buffer, out var written) && written == KeySize)
        {
            key = buffer[..KeySize];
            CryptographicOperations.ZeroMemory(buffer);
            return true;
        }
        CryptographicOperations.ZeroMemory(buffer);
        return false;
    }

    public static byte[] DeriveKey(string passphrase, byte[] salt)
    {
        var password = StrictUtf8Bytes(passphrase);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }
    }

    private static byte[] StrictUtf8Bytes(string text)
    {
        try
        {
            return new System.Text.UTF8Encoding(false, true).GetBytes(text);
        }
        catch (System.Text.EncoderFallbackException)
        {
            throw new ToolInputException("Text contains invalid characters");
        }
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        try
        {
            return new System.Text.UTF8Encoding(false, true).GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException)
        {
            throw new ToolInputException("Decrypted bytes are not UTF-8 text");
        }
    }
}
