using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Clevr.Lint.Extension;

public sealed record LinterConfigRule
{
    public bool? Enabled { get; init; }
    public string? Severity { get; init; }
}

public sealed record LinterConfig
{
    public Dictionary<string, LinterConfigRule> Rules { get; init; } = new();
    public List<string> ExcludedModules { get; init; } = new();
}

/// <summary>
/// Reads and writes lint-config.yaml in the project directory.
/// Only rules that deviate from defaults (enabled=true, no severity override) are written.
/// </summary>
public sealed class LinterConfigStore
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public LinterConfig Load(string projectDir)
    {
        var path = ConfigPath(projectDir);
        if (!File.Exists(path))
        {
            // mxcli reads this file directly, so the default must exist on disk
            // before the first scan, not just live in memory here.
            var defaults = new LinterConfig { ExcludedModules = ["System"] };
            Save(projectDir, defaults);
            return defaults;
        }
        try
        {
            var yaml = File.ReadAllText(path);
            var raw = Deserializer.Deserialize<RawConfig?>(yaml);
            if (raw is null) return new LinterConfig();
            var rules = (raw.Rules ?? new()).ToDictionary(
                kv => kv.Key,
                kv => new LinterConfigRule { Enabled = kv.Value?.Enabled, Severity = kv.Value?.Severity });
            var excludedModules = raw.ExcludedModules ?? new List<string>();
            return new LinterConfig { Rules = rules, ExcludedModules = excludedModules };
        }
        catch (Exception ex) { DebugLog.Write(projectDir, $"Failed to load lint-config.yaml: {ex.Message}", LogLevel.Error); return new LinterConfig(); }
    }

    public void Save(string projectDir, LinterConfig config)
    {
        var path = ConfigPath(projectDir);
        var rawRules = config.Rules
            .Where(kv => kv.Value.Enabled == false || kv.Value.Severity is not null)
            .ToDictionary(kv => kv.Key, kv => (RawRule?)new RawRule { Enabled = kv.Value.Enabled, Severity = kv.Value.Severity });

        var excludedModules = config.ExcludedModules.Count > 0 ? config.ExcludedModules : null;
        var raw = new RawConfig
        {
            ExcludedModules = excludedModules,
            Rules = rawRules.Count > 0 ? rawRules : null,
        };
        var yaml = Serializer.Serialize(raw);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmpPath = path + ".tmp";
        File.WriteAllText(tmpPath, yaml);
        File.Move(tmpPath, path, overwrite: true);
    }

    private static string ConfigPath(string projectDir) =>
        Path.Combine(projectDir, "lint-config.yaml");

    private sealed class RawConfig
    {
        // mxcli's Go struct tag is "excludeModules" (no 'd') — the key on disk
        // must match exactly, since mxcli reads this file directly.
        [YamlMember(Alias = "excludeModules")]
        public List<string>? ExcludedModules { get; set; }
        public Dictionary<string, RawRule?>? Rules { get; set; }
    }

    private sealed class RawRule
    {
        public bool? Enabled { get; set; }
        public string? Severity { get; set; }
    }
}
