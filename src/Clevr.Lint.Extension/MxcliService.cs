using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.Lint.Extension;

/// <summary>mxcli binary lifecycle: resolve location/version and download from GitHub.</summary>
public static class MxcliService
{
    internal static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "clevr-lint", "mxcli");

    private static readonly string CachedExe   = Path.Combine(CacheDir, "mxcli.exe");
    private const string GhApiLatest = "https://api.github.com/repos/mendixlabs/mxcli/releases/latest";
    private const string AssetName   = "mxcli-windows-amd64.exe";

    // ── Resolve ──────────────────────────────────────────────────────────────

    /// <summary>Returns the current mxcli state without downloading anything.</summary>
    public static MxcliInfo Resolve(string? settingsJson, string? fallbackProjectDir)
    {
        var settings   = LintScanSettings.Load(settingsJson, fallbackProjectDir);
        var configured = settings.MxcliPath?.Trim() ?? "";

        if (string.IsNullOrEmpty(configured) || configured == "mxcli")
        {
            var onPath = TryFindOnPath();
            if (onPath != null) return MakeInfo("path", onPath);

            if (File.Exists(CachedExe)) return MakeInfo("clevrLint", CachedExe);

            return new MxcliInfo { Source = "notFound", Found = false };
        }

        if (!File.Exists(configured))
            return new MxcliInfo { Source = "custom", ResolvedPath = configured, Found = false };

        var src = configured.StartsWith(CacheDir, StringComparison.OrdinalIgnoreCase)
            ? "clevrLint" : "custom";
        return MakeInfo(src, configured);
    }

    private static MxcliInfo MakeInfo(string source, string path)
    {
        var version = GetVersion(path);
        string? downloadedAt = null;
        if (source is "clevrLint" or "custom" && File.Exists(path))
            downloadedAt = File.GetLastWriteTime(path).ToString("yyyy-MM-dd");
        return new MxcliInfo
        {
            Source       = source,
            ResolvedPath = path,
            Version      = version,
            Found        = true,
            DownloadedAt = downloadedAt,
        };
    }

    private static string? TryFindOnPath()
    {
        try
        {
            var where = ProcessRunner.Run("where.exe", "mxcli", null, timeoutMs: 5_000);
            if (where.Error == null && !string.IsNullOrWhiteSpace(where.StdOut))
            {
                var line = where.StdOut.Split('\n')
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.Length > 0 && File.Exists(l));
                if (line != null) return line;
            }
        }
        catch { /* where.exe not found — unusual but possible */ }
        // Last-resort: can mxcli run at all?
        var test = ProcessRunner.Run("mxcli", "--version", null, timeoutMs: 5_000);
        return test.Error == null ? "mxcli" : null;
    }

    private static string? GetVersion(string mxcliPath)
    {
        try
        {
            var r = ProcessRunner.Run(mxcliPath, "--version", null, timeoutMs: 10_000);
            var v = r.StdOut.Trim();
            if (!string.IsNullOrEmpty(v)) return v;
            v = r.StdErr.Trim();
            if (!string.IsNullOrEmpty(v)) return v;
        }
        catch { }
        return null;
    }

    // ── Download ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the latest mxcli Windows release to the CLEVR Lint cache, verifies SHA-256,
    /// then updates lint-scan-settings.json to point at the downloaded binary.
    /// Reports 0-100 progress via <paramref name="onProgress"/>; throws on any failure.
    /// </summary>
    public static async Task<MxcliInfo> DownloadLatestAsync(
        IExtensionFileService fileService,
        Action<int> onProgress,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(CacheDir);
        var tmp = CachedExe + ".download";
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "clevr-lint-extension");
            http.DefaultRequestHeaders.Add("Accept",     "application/vnd.github+json");

            onProgress(0);

            var rel = await http.GetFromJsonAsync<GitHubRelease>(GhApiLatest, ct)
                ?? throw new InvalidOperationException("GitHub API returned null.");

            var asset = rel.Assets?.FirstOrDefault(a => a.Name == AssetName)
                ?? throw new InvalidOperationException(
                    $"Asset '{AssetName}' not found in release {rel.TagName}.");

            var digest = asset.Digest ?? "";
            if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"No sha256 digest published for '{AssetName}' in release {rel.TagName}. Refusing to install an unverifiable binary.");

            var expectedHash = digest[7..].Trim().ToLowerInvariant();
            var expectedSize = asset.Size;

            if (File.Exists(tmp)) File.Delete(tmp);

            using (var response = await http.GetAsync(
                asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? expectedSize;
                await using var dest = File.OpenWrite(tmp);
                await using var src  = await response.Content.ReadAsStreamAsync(ct);
                var buf = new byte[81920];
                long bytesRead = 0;
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dest.WriteAsync(buf.AsMemory(0, n), ct);
                    bytesRead += n;
                    if (total > 0) onProgress((int)(bytesRead * 99 / total));
                }
            }

            // Verify size + SHA-256
            var actualSize = new FileInfo(tmp).Length;
            byte[] hashBytes;
            using (var fs = File.OpenRead(tmp))
                hashBytes = await SHA256.HashDataAsync(fs, ct);
            var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            if (actualSize != expectedSize || actualHash != expectedHash)
            {
                File.Delete(tmp);
                throw new InvalidOperationException(
                    $"Integrity check failed — size got={actualSize} expected={expectedSize}, " +
                    $"sha256 got={actualHash[..16]}… expected={expectedHash[..16]}…");
            }

            if (File.Exists(CachedExe)) File.Delete(CachedExe);
            File.Move(tmp, CachedExe);
            onProgress(100);

            UpdateSettingsPath(fileService, CachedExe);
            return MakeInfo("clevrLint", CachedExe);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }
    }

    private static void UpdateSettingsPath(IExtensionFileService fileService, string mxcliPath)
    {
        try
        {
            var settingsPath = fileService.ResolvePath("lint-scan-settings.json");
            LintScanSettings existing;
            if (File.Exists(settingsPath))
                existing = LintScanSettings.Load(File.ReadAllText(settingsPath), null);
            else
                existing = new LintScanSettings();

            existing.MxcliPath = mxcliPath;
            var json = JsonSerializer.Serialize(
                new { mxcliPath = existing.MxcliPath, projectPath = existing.ProjectPath },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json, System.Text.Encoding.UTF8);
        }
        catch { /* best-effort: settings update failing doesn't undo a successful download */ }
    }
}

// Minimal GitHub Releases API models — file-scoped to avoid polluting the namespace.
file sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string?        TagName { get; init; }
    [JsonPropertyName("assets")]   public GitHubAsset[]? Assets  { get; init; }
}

file sealed class GitHubAsset
{
    [JsonPropertyName("name")]                 public string? Name               { get; init; }
    [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; init; }
    [JsonPropertyName("size")]                 public long    Size               { get; init; }
    [JsonPropertyName("digest")]               public string? Digest             { get; init; }
}
