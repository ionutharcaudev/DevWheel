using DevWheel.Core.Tools;

namespace DevWheel.Core;

public static class ToolGroups
{
    public const string Encode = "Encode";
    public const string Inspect = "Inspect";
    public const string Format = "Format";
    public const string Hash = "Hash";
    public const string Generate = "Generate";
    public const string Crypto = "Crypto";

    /// <summary>Fixed clockwise order from the top of the inner ring.</summary>
    public static IReadOnlyList<string> DefaultOrder { get; } = [Encode, Inspect, Format, Hash, Generate, Crypto];
}

public sealed record ToolGroup(string Name, IReadOnlyList<IDevTool> Tools);

public sealed class ToolRegistry
{
    private readonly List<IDevTool> _tools;

    public ToolRegistry(IEnumerable<IDevTool> tools)
    {
        _tools = [.. tools];
        var duplicate = _tools.GroupBy(t => t.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"Duplicate tool id '{duplicate.Key}'", nameof(tools));
        var unknown = _tools.FirstOrDefault(t => !ToolGroups.DefaultOrder.Contains(t.Group));
        if (unknown is not null) throw new ArgumentException($"Tool '{unknown.Id}' has unknown group '{unknown.Group}'", nameof(tools));
    }

    public IReadOnlyList<IDevTool> All => _tools;

    public IDevTool? Find(string id) => _tools.Find(t => t.Id == id);

    public static ToolRegistry CreateDefault(TimeProvider time) => new(
    [
        new Base64EncodeTool(),
        new Base64DecodeTool(),
        new Base64UrlEncodeTool(),
        new Base64UrlDecodeTool(),
        new UrlEncodeTool(),
        new UrlDecodeTool(),
        new HtmlEncodeTool(),
        new HtmlDecodeTool(),
        new JwtDecodeTool(time),
        new TimestampTool(time),
        new JsonPrettyTool(),
        new JsonMinifyTool(),
        new Sha256Tool(),
        new Sha1Tool(),
        new Md5Tool(),
        new HmacSha256Tool(),
        new GuidTool(),
        new RandomSecretTool(),
        new LaravelKeyTool(),
        new PasswordTool(),
        new AesEncryptTool(),
        new AesDecryptTool(),
    ]);

    /// <summary>The wheel content: groups and tools filtered and ordered by settings. Empty groups are dropped.</summary>
    public IReadOnlyList<ToolGroup> BuildWheel(AppSettings settings)
    {
        var groupOrder = settings.GroupOrder.Where(ToolGroups.DefaultOrder.Contains).Distinct()
            .Concat(ToolGroups.DefaultOrder.Where(g => !settings.GroupOrder.Contains(g)));

        int ToolRank(IDevTool t)
        {
            var i = settings.ToolOrder.IndexOf(t.Id);
            return i >= 0 ? i : settings.ToolOrder.Count + _tools.IndexOf(t);
        }

        var result = new List<ToolGroup>();
        foreach (var group in groupOrder)
        {
            if (settings.HiddenGroups.Contains(group)) continue;
            var tools = _tools.Where(t => t.Group == group && !settings.HiddenTools.Contains(t.Id)).OrderBy(ToolRank).ToList();
            if (tools.Count > 0) result.Add(new ToolGroup(group, tools));
        }
        return result;
    }
}
