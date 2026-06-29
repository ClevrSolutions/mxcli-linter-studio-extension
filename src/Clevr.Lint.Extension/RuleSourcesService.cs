using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Clevr.Lint.Extension;

public sealed record RuleSourceFetchResult(int Copied, int Skipped, int Failed, string[] Errors);
public sealed record RuleSourceDeleteResult(int Deleted, int NotFound);

internal sealed class RuleSourcesService
{
    // Matches: https://github.com/{owner}/{repo}/tree/{branch}/{path}
    // The path segment is optional (root of repo).
    private static readonly Regex GitHubTreeUrl = new(
        @"^https://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/tree/(?<branch>[^/]+)(?:/(?<path>.+))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string BuildApiUrl(string githubUrl)
    {
        var m = GitHubTreeUrl.Match(githubUrl.Trim());
        if (!m.Success)
            throw new ArgumentException(
                $"The URL does not look like a GitHub tree URL (expected https://github.com/owner/repo/tree/branch/path). Got: {githubUrl}");

        var owner  = m.Groups["owner"].Value;
        var repo   = m.Groups["repo"].Value;
        var branch = m.Groups["branch"].Value;
        var path   = m.Groups["path"].Success ? m.Groups["path"].Value : "";

        return string.IsNullOrEmpty(path)
            ? $"https://api.github.com/repos/{owner}/{repo}/contents?ref={branch}"
            : $"https://api.github.com/repos/{owner}/{repo}/contents/{path}?ref={branch}";
    }

    private static async Task<GitHubContentItem[]> ListFilesAsync(HttpClient http, string githubUrl, CancellationToken ct)
    {
        var apiUrl = BuildApiUrl(githubUrl);
        var response = await http.GetAsync(apiUrl, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException(
                $"GitHub returned 404. Check that the URL points to an existing directory and the branch/path are correct.\nURL tried: {apiUrl}");

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            (int)response.StatusCode == 429)
            throw new InvalidOperationException(
                "GitHub API rate limit exceeded (unauthenticated limit: 60 requests/hour). Wait a while and try again.");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);

        try
        {
            var parsed = JsonSerializer.Deserialize<GitHubContentItem[]>(json);
            if (parsed == null) throw new InvalidOperationException("GitHub API returned an empty response.");
            return parsed;
        }
        catch (JsonException)
        {
            var single = JsonSerializer.Deserialize<GitHubContentItem>(json);
            if (single?.Type == "file")
                throw new ArgumentException(
                    "The URL points to a file, not a directory. Provide the URL of a folder in the GitHub tree view.");
            throw;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "clevr-lint-extension");
        http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return http;
    }

    private static void RequireProjectDir(string projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException(
                "No project path configured. Open a project in Studio Pro or set the Mendix project path in Settings → Configuration before fetching rule sources.");
    }

    public async Task<RuleSourceFetchResult> FetchRuleSourceAsync(
        string githubUrl,
        string projectDir,
        bool replaceExisting,
        Action<string> onProgress,
        CancellationToken ct = default)
    {
        RequireProjectDir(projectDir);

        var m = GitHubTreeUrl.Match(githubUrl.Trim());
        var owner = m.Groups["owner"].Value;
        var repo  = m.Groups["repo"].Value;

        onProgress($"Listing files from {owner}/{repo}…");

        using var http = CreateHttpClient();
        var items = await ListFilesAsync(http, githubUrl, ct);
        var fileItems = items.Where(i => i.Type == "file").ToArray();

        if (fileItems.Length == 0)
        {
            onProgress("No files found in directory.");
            return new RuleSourceFetchResult(0, 0, 0, []);
        }

        var destDir = Path.Combine(projectDir, ".claude", "lint-rules");
        Directory.CreateDirectory(destDir);

        var copied = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<string>();

        for (var i = 0; i < fileItems.Length; i++)
        {
            var item = fileItems[i];
            var fileName = item.Name ?? "";
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(item.DownloadUrl))
                continue;

            onProgress($"[{i + 1}/{fileItems.Length}] {fileName}");

            var destPath = Path.Combine(destDir, fileName);
            if (!replaceExisting && File.Exists(destPath))
            {
                skipped++;
                continue;
            }

            try
            {
                var content = await http.GetByteArrayAsync(item.DownloadUrl, ct);
                var tmpPath = destPath + ".tmp";
                await File.WriteAllBytesAsync(tmpPath, content, ct);
                File.Move(tmpPath, destPath, overwrite: true);
                copied++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{fileName}: {ex.Message}");
            }
        }

        return new RuleSourceFetchResult(copied, skipped, failed, [.. errors]);
    }

    public async Task<RuleSourceDeleteResult> DeleteRuleSourceFilesAsync(
        string githubUrl,
        string projectDir,
        Action<string> onProgress,
        CancellationToken ct = default)
    {
        RequireProjectDir(projectDir);

        var m = GitHubTreeUrl.Match(githubUrl.Trim());
        var owner = m.Groups["owner"].Value;
        var repo  = m.Groups["repo"].Value;

        onProgress($"Listing files from {owner}/{repo}…");

        using var http = CreateHttpClient();
        var items = await ListFilesAsync(http, githubUrl, ct);
        var fileItems = items.Where(i => i.Type == "file").ToArray();

        if (fileItems.Length == 0)
        {
            onProgress("No files found in directory.");
            return new RuleSourceDeleteResult(0, 0);
        }

        var rulesDir = Path.Combine(projectDir, ".claude", "lint-rules");
        var deleted = 0;
        var notFound = 0;

        for (var i = 0; i < fileItems.Length; i++)
        {
            var fileName = fileItems[i].Name ?? "";
            if (string.IsNullOrEmpty(fileName)) continue;

            onProgress($"[{i + 1}/{fileItems.Length}] {fileName}");

            var filePath = Path.Combine(rulesDir, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                deleted++;
            }
            else
            {
                notFound++;
            }
        }

        return new RuleSourceDeleteResult(deleted, notFound);
    }
}

internal sealed class GitHubContentItem
{
    [JsonPropertyName("name")]         public string? Name        { get; init; }
    [JsonPropertyName("type")]         public string? Type        { get; init; }
    [JsonPropertyName("download_url")] public string? DownloadUrl { get; init; }
}
