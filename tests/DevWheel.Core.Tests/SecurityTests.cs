using System.Reflection;
using System.Text.RegularExpressions;
using DevWheel.Interop;

namespace DevWheel.Core.Tests;

/// <summary>Build-time checks for docs/security.md. A failure here means a hard rule was broken.</summary>
public class SecurityTests
{
    private static readonly Assembly[] ProductAssemblies =
    [
        typeof(ToolRegistry).Assembly,          // DevWheel.Core
        typeof(HotkeyService).Assembly,         // DevWheel.Interop
        typeof(DevWheel.App.App).Assembly,      // DevWheel (App)
    ];

    // SEC-01: no networking assemblies may be referenced.
    private static readonly string[] NetworkAssemblies =
    [
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.Requests",
        "System.Net.WebClient",
        "System.Net.HttpListener",
        "System.Net.Mail",
        "System.Net.NetworkInformation",
        "System.Net.Ping",
        "System.Net.Security",
        "System.Net.WebSockets",
        "System.Net.WebSockets.Client",
        "System.Net.Quic",
        "System.Net.NameResolution",
    ];

    public static TheoryData<string> Assemblies() => new(ProductAssemblies.Select(a => a.GetName().Name!));

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void No_network_assembly_is_referenced(string name)
    {
        var assembly = ProductAssemblies.Single(a => a.GetName().Name == name);
        var referenced = assembly.GetReferencedAssemblies().Select(r => r.Name!).ToList();
        Assert.Empty(referenced.Intersect(NetworkAssemblies));
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void Only_framework_and_own_assemblies_are_referenced(string name)
    {
        // SEC-10: no third-party packages.
        var assembly = ProductAssemblies.Single(a => a.GetName().Name == name);
        var foreign = assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(n => !(n.StartsWith("System", StringComparison.Ordinal) || n.StartsWith("Microsoft", StringComparison.Ordinal)
                || n.StartsWith("DevWheel", StringComparison.Ordinal) || n is "mscorlib" or "netstandard"
                || n is "PresentationCore" or "PresentationFramework" or "WindowsBase"));
        Assert.Empty(foreign);
    }

    // Source-level rules from CLAUDE.md: no network types, no keyboard hooks, no System.Random, no DllImport.
    private static readonly (string Pattern, string Rule)[] BannedSource =
    [
        (@"\bHttpClient\b", "SEC-01 no HttpClient"),
        (@"\bWebRequest\b|\bWebClient\b", "SEC-01 no WebRequest/WebClient"),
        (@"\bSystem\.Net\.Sockets\b|\bTcpClient\b|\bUdpClient\b|\bSocket\b", "SEC-01 no sockets"),
        (@"\bSetWindowsHookEx", "no low-level keyboard hooks"),
        (@"new\s+Random\s*\(|\bRandom\.Shared\b", "randomness only via RandomNumberGenerator"),
        (@"\[DllImport", "use LibraryImport, not DllImport"),
        (@"\b(Console|Debug|Trace)\.Write", "SEC-02 no logging of content"),
    ];

    [Fact]
    public void Source_follows_the_hard_rules()
    {
        var src = Path.Combine(RepoRoot(), "src");
        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = lines[i].Split("//")[0];
                foreach (var (pattern, rule) in BannedSource)
                {
                    if (Regex.IsMatch(code, pattern)) violations.Add($"{Path.GetRelativePath(src, file)}:{i + 1} {rule}");
                }
            }
        }
        Assert.Empty(violations);
    }

    [Fact]
    public void App_never_requests_elevation()
    {
        var manifest = File.ReadAllText(Path.Combine(RepoRoot(), "src", "DevWheel.App", "app.manifest"));
        Assert.Contains("level=\"asInvoker\"", manifest);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DevWheel.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("DevWheel.sln not found above the test output");
    }
}
