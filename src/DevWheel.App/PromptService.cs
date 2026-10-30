using System.Threading;
using System.Threading.Tasks;
using DevWheel.Core;

namespace DevWheel.App;

/// <summary>Modal prompts near the cursor. Nothing typed here is kept after the dialog closes (SEC-03).</summary>
public sealed class PromptService : IPromptService
{
    public Task<string?> AskKeyAsync(string title, string hint, CancellationToken ct) =>
        Task.FromResult(KeyPromptWindow.Ask(title, hint));

    public Task<SecretOptions?> AskSecretOptionsAsync(SecretOptions defaults, CancellationToken ct) =>
        Task.FromResult(SecretOptionsWindow.Ask(defaults));

    public Task<PasswordOptions?> AskPasswordOptionsAsync(PasswordOptions defaults, CancellationToken ct) =>
        Task.FromResult(PasswordOptionsWindow.Ask(defaults));
}
