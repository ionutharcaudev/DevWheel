using DevWheel.Core;

namespace DevWheel.Core.Tests;

/// <summary>Scripted prompt answers; records how often each prompt was shown.</summary>
internal sealed class FakePrompts : IPromptService
{
    public string? Key { get; set; }
    public SecretOptions? SecretAnswer { get; set; }
    public PasswordOptions? PasswordAnswer { get; set; }
    public int KeyPrompts { get; private set; }
    public int OptionPrompts { get; private set; }

    public Task<string?> AskKeyAsync(string title, string hint, CancellationToken ct)
    {
        KeyPrompts++;
        return Task.FromResult(Key);
    }

    public Task<SecretOptions?> AskSecretOptionsAsync(SecretOptions defaults, CancellationToken ct)
    {
        OptionPrompts++;
        return Task.FromResult(SecretAnswer);
    }

    public Task<PasswordOptions?> AskPasswordOptionsAsync(PasswordOptions defaults, CancellationToken ct)
    {
        OptionPrompts++;
        return Task.FromResult(PasswordAnswer);
    }
}

/// <summary>A fixed clock in UTC.</summary>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

internal static class Run
{
    public static ToolResult Tool(IDevTool tool, string? selection, FakePrompts? prompts = null, ToolOptions? options = null) =>
        tool.RunAsync(new ToolContext(selection, prompts ?? new FakePrompts(), options ?? ToolOptions.Default), CancellationToken.None)
            .GetAwaiter().GetResult();

    public static string Output(IDevTool tool, string? selection, FakePrompts? prompts = null, ToolOptions? options = null)
    {
        var result = Tool(tool, selection, prompts, options);
        return Assert.IsType<ToolResult.Success>(result).Output;
    }

    public static string Failure(IDevTool tool, string? selection, FakePrompts? prompts = null)
    {
        var result = Tool(tool, selection, prompts);
        return Assert.IsType<ToolResult.Failure>(result).Message;
    }
}
