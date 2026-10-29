using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace QuickTranslator;

internal static class AppInfo
{
    /// <summary>GitHub repository ("owner/name") whose Releases carry the installers.</summary>
    public const string GitHubRepo = "hovannam1612/QuickTranslatorTool";

    public static Version Version { get; } = Normalize(typeof(AppInfo).Assembly.GetName().Version);

    public static string VersionText => Version.ToString(3);

    /// <summary>True when running from the Setup.exe installation (an uninstaller sits next to the exe).</summary>
    public static bool IsInstalled =>
        File.Exists(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", "unins000.exe"));

    /// <summary>Reduces to major.minor.build so "1.0.0" and "1.0.0.0" compare as equal.</summary>
    public static Version Normalize(Version? v) => new(v?.Major ?? 0, v?.Minor ?? 0, Math.Max(0, v?.Build ?? 0));
}

internal sealed record UpdateInfo(Version Version, string AssetName, string DownloadUrl, string PageUrl, string Notes);

/// <summary>Looks for a newer release on GitHub and downloads its installer.</summary>
internal sealed class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QuickTranslator/" + AppInfo.VersionText);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public bool IsConfigured => AppInfo.GitHubRepo.Contains('/') && !AppInfo.GitHubRepo.StartsWith("OWNER/", StringComparison.Ordinal);

    /// <summary>Returns the newest release if it is newer than this build, otherwise null.</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct)
    {
        using var response = await Http.GetAsync($"https://api.github.com/repos/{AppInfo.GitHubRepo}/releases/latest", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("No release has been published yet.");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed))
            throw new InvalidOperationException($"Release tag '{tag}' is not a version number.");

        var latest = AppInfo.Normalize(parsed);
        if (latest <= AppInfo.Version) return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? "";
            string url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                && url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                string page = root.GetProperty("html_url").GetString() ?? "";
                string notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
                return new UpdateInfo(latest, name, url, page, notes.Trim());
            }
        }
        throw new InvalidOperationException("The latest release has no installer (…Setup….exe) attached.");
    }

    /// <summary>Downloads the installer to a temp folder and returns its path.</summary>
    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<int>? progress, CancellationToken ct)
    {
        string dir = Path.Combine(Path.GetTempPath(), "QuickTranslator-Update");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, update.AssetName);

        using var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? -1;

        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var target = File.Create(path))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress?.Report((int)(done * 100 / total));
            }
        }
        return path;
    }

    /// <summary>Runs the installer silently; it closes this app, upgrades in place and starts the new version.</summary>
    public static void RunInstaller(string path) =>
        Process.Start(new ProcessStartInfo(path, "/SILENT /NORESTART /RELAUNCH=1") { UseShellExecute = true });
}
