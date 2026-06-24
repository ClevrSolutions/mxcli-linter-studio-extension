using Clevr.Acr.Normalizer;
using YamlDotNet.RepresentationModel;

namespace Clevr.AcrSpike;

/// <summary>
/// Leest de page/snippet-export (<c>*.Forms$Page.yaml</c> / <c>*.Forms$Snippet.yaml</c>) in één
/// YamlDotNet-pass en zet elk document om naar het PLATTE objectboom-model (<see cref="PageModel"/>):
/// mapping→Dictionary, sequence→List, scalar→string. De patroon-gevoelige regel-logica (de boom-walk)
/// zit puur in <c>Clevr.Acr.Normalizer.PageRules</c>; deze reader is bewust DOM/structuur-only.
///
/// Herbruikbaar: dezelfde geparseerde boom voedt straks ook 004_0002 (alt-text) — die loopt de
/// CustomWidget/WidgetObject/Texts$Translation-takken af op exact dit model. Eerste page-bestandstype
/// dat we lezen. Tolerant: corrupte/lege YAML wordt overgeslagen.
/// </summary>
public static class PageYamlReader
{
    public static List<PageModel> Load(string projectDir)
    {
        var result = new List<PageModel>();
        var ms = System.IO.Path.Combine(projectDir, "modelsource");
        if (!System.IO.Directory.Exists(ms)) return result;

        foreach (var (pattern, docType) in new[] { ("*.Forms$Page.yaml", "Page"), ("*.Forms$Snippet.yaml", "Snippet") })
        {
            foreach (var f in System.IO.Directory.GetFiles(ms, pattern, System.IO.SearchOption.AllDirectories))
            {
                try
                {
                    var rel = System.IO.Path.GetRelativePath(ms, f).Replace(System.IO.Path.DirectorySeparatorChar, '/');
                    var module = rel.Split('/')[0];

                    var ys = new YamlStream();
                    ys.Load(new System.IO.StringReader(System.IO.File.ReadAllText(f)));
                    if (ys.Documents.Count == 0) continue;

                    var root = Convert(ys.Documents[0].RootNode);
                    var name = root is Dictionary<string, object?> map && map.TryGetValue("Name", out var nv) && nv is string ns ? ns : "";
                    result.Add(new PageModel { Module = module, Name = name, DocType = docType, Root = root });
                }
                catch { /* tolerant: sla corrupte/onleesbare page-YAML over */ }
            }
        }
        return result;
    }

    /// <summary>YamlDotNet-DOM → plat objectboom-model (string-sleutels, List, string-scalars).</summary>
    private static object? Convert(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode m:
                var dict = new Dictionary<string, object?>(System.StringComparer.Ordinal);
                foreach (var kv in m.Children)
                    if (kv.Key is YamlScalarNode k && k.Value is { } key)
                        dict[key] = Convert(kv.Value);
                return dict;
            case YamlSequenceNode s:
                var list = new List<object?>(s.Children.Count);
                foreach (var item in s.Children) list.Add(Convert(item));
                return list;
            case YamlScalarNode sc:
                return sc.Value;
            default:
                return null;
        }
    }
}
