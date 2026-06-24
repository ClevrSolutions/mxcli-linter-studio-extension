using YamlDotNet.RepresentationModel;

namespace Clevr.AcrSpike;

/// <summary>
/// Leest de constant-export (<c>*.Constants$Constant.yaml</c>) in één YamlDotNet-pass, voor de
/// constant-route (CLEVR-SEC-011 = mxlint 006_0001 ExposedConstants). Eerste constant-bestandstype
/// dat we lezen. Plat: top-level <c>Name</c> (string) + <c>ExposedToClient</c> (bool). Module = de
/// eerste padsegment-map onder modelsource (zoals de domein-/microflow-readers).
///
/// Dezelfde infra-stijl als <see cref="MicroflowYamlExpressions"/>: YamlDotNet → primitieven, de pure
/// regel-logica (gevoelig-naam-patroon) zit in <c>Clevr.Acr.Normalizer.ConstantRules</c>. Tolerant:
/// corrupte/lege YAML wordt overgeslagen.
/// </summary>
public static class ConstantYamlReader
{
    /// <summary>Leest alle constants onder modelsource → (Module, Name, ExposedToClient).</summary>
    public static List<(string Module, string Name, bool Exposed)> Load(string projectDir)
    {
        var result = new List<(string, string, bool)>();
        var ms = System.IO.Path.Combine(projectDir, "modelsource");
        if (!System.IO.Directory.Exists(ms)) return result;

        foreach (var f in System.IO.Directory.GetFiles(ms, "*.Constants$Constant.yaml", System.IO.SearchOption.AllDirectories))
        {
            try
            {
                var rel = System.IO.Path.GetRelativePath(ms, f).Replace(System.IO.Path.DirectorySeparatorChar, '/');
                var module = rel.Split('/')[0];

                var ys = new YamlStream();
                ys.Load(new System.IO.StringReader(System.IO.File.ReadAllText(f)));
                if (ys.Documents.Count == 0 || ys.Documents[0].RootNode is not YamlMappingNode root) continue;

                var name = Scalar(root, "Name") ?? "";
                var exposed = string.Equals(Scalar(root, "ExposedToClient"), "true", System.StringComparison.OrdinalIgnoreCase);
                if (name.Length > 0) result.Add((module, name, exposed));
            }
            catch { /* tolerant: sla corrupte/onleesbare constant-YAML over */ }
        }
        return result;
    }

    private static string? Scalar(YamlMappingNode m, string key)
        => m.Children.TryGetValue(new YamlScalarNode(key), out var v) && v is YamlScalarNode s ? s.Value : null;
}
