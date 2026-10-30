namespace DevWheel.Core;

public enum SecretFormat
{
    Hex,
    Base64,
}

public sealed record SecretOptions(int Bytes = 32, SecretFormat Format = SecretFormat.Hex)
{
    public static IReadOnlyList<int> AllowedBytes { get; } = [16, 32, 64];

    public string? Validate() =>
        AllowedBytes.Contains(Bytes) ? null : "Secret length must be 16, 32 or 64 bytes";
}

public sealed record PasswordOptions(int Length = 20, bool Upper = true, bool Lower = true, bool Digits = true, bool Symbols = true)
{
    public const int MinLength = 4;
    public const int MaxLength = 256;

    public int ClassCount => (Upper ? 1 : 0) + (Lower ? 1 : 0) + (Digits ? 1 : 0) + (Symbols ? 1 : 0);

    public string? Validate()
    {
        if (ClassCount == 0) return "Choose at least one character set";
        if (Length is < MinLength or > MaxLength) return $"Password length must be {MinLength} to {MaxLength}";
        return null;
    }
}

public sealed record GeneratorSettings
{
    public SecretOptions Secret { get; init; } = new();
    public PasswordOptions Password { get; init; } = new();
}
