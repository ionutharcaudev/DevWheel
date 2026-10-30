using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DevWheel.Core.Tools;

public sealed class JsonPrettyTool : TextTransformTool
{
    public override string Id => "json.pretty";
    public override string Group => ToolGroups.Format;
    public override string Name => "JSON pretty-print";
    public override string Label => "JSON\npretty";
    public override string Hint => "Indent with 2 spaces";
    protected override string DoneMessage => "JSON formatted";

    protected override string Transform(string input) => JsonText.Rewrite(input, indented: true);
}

public sealed class JsonMinifyTool : TextTransformTool
{
    public override string Id => "json.minify";
    public override string Group => ToolGroups.Format;
    public override string Name => "JSON minify";
    public override string Label => "JSON\nmin";
    public override string Hint => "Remove all whitespace";
    protected override string DoneMessage => "JSON minified";

    protected override string Transform(string input) => JsonText.Rewrite(input, indented: false);
}

public static class JsonText
{
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Parses and rewrites JSON; non-ASCII text is kept as is rather than escaped.</summary>
    public static string Rewrite(string input, bool indented)
    {
        try
        {
            using var doc = JsonDocument.Parse(input, ReadOptions);
            return Write(doc.RootElement, indented);
        }
        catch (JsonException e)
        {
            throw new ToolInputException($"Not valid JSON (line {(e.LineNumber ?? 0) + 1}, position {(e.BytePositionInLine ?? 0) + 1})");
        }
    }

    public static string Write(JsonElement element, bool indented)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
        {
            Indented = indented,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            element.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
