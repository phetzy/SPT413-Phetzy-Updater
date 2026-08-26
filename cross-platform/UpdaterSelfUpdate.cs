using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Phetzy.Spt413Updater.CrossPlatform;

internal static class UpdaterSelfUpdate
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/phetzy/SPT413-Phetzy-Updater/releases/latest";

    internal sealed record Release(
        string TagName,
        Version Version,
        string AssetName,
        Uri AssetUrl,
        Uri ChecksumUrl);

    internal static async Task<Release?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var json = await client.GetStringAsync(RequireUri(LatestReleaseApi, true), cancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tagName = root.GetProperty("tag_name").GetString()
                      ?? throw new InvalidOperationException("The GitHub release has no tag name.");
        var versionText = tagName.StartsWith('v') ? tagName[1..] : tagName;
        if (!Version.TryParse(versionText, out var releaseVersion))
            throw new InvalidOperationException($"The GitHub release tag is not a version: {tagName}");

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
        if (releaseVersion <= currentVersion) return null;

        var platformPrefix = OperatingSystem.IsLinux()
            ? "SPT413-Phetzy-Updater-Linux-"
            : "SPT413-Phetzy-Updater-Windows-";
        var assets = root.GetProperty("assets").EnumerateArray()
            .Select(asset => new
            {
                Name = asset.GetProperty("name").GetString(),
                Url = asset.GetProperty("browser_download_url").GetString()
            })
            .Where(asset => asset.Name is not null && asset.Url is not null)
            .ToArray();
        var binary = assets.SingleOrDefault(asset =>
            asset.Name!.StartsWith(platformPrefix, StringComparison.Ordinal) &&
            asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (binary is null)
            throw new InvalidOperationException("The release does not contain an updater for this platform.");
        var checksum = assets.SingleOrDefault(asset => asset.Name == binary.Name + ".sha256")
                       ?? throw new InvalidOperationException("The updater SHA-256 sidecar is missing.");

        return new Release(
            tagName,
            releaseVersion,
            binary.Name!,
            RequireUri(binary.Url!, false),
            RequireUri(checksum.Url!, false));
    }

    internal static async Task DownloadVerifyAndLaunchAsync(
        Release release,
        IProgress<PackInstallEngine.InstallProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var originalUpdater = Environment.ProcessPath
                              ?? throw new InvalidOperationException("The running updater path is unavailable.");
        var cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhetzySptUpdater",
            "self-update",
            SanitizePathComponent(release.TagName));
        if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
        Directory.CreateDirectory(cacheRoot);

        var archivePath = Path.Combine(cacheRoot, release.AssetName);
        var checksumPath = archivePath + ".sha256";
        using var client = CreateClient();
        await DownloadAsync(client, release.ChecksumUrl, checksumPath, 5, 10, progress, cancellationToken);
        await DownloadAsync(client, release.AssetUrl, archivePath, 10, 72, progress, cancellationToken);

        progress.Report(new(74, "Verifying updater SHA-256", null));
        var fields = (await File.ReadAllTextAsync(checksumPath, cancellationToken))
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0 || fields[0].Length != 64 || !fields[0].All(Uri.IsHexDigit))
            throw new InvalidOperationException("The updater checksum sidecar is invalid.");
        var expectedHash = fields[0].ToUpperInvariant();
        var actualHash = HashFile(archivePath);
        if (!actualHash.Equals(expectedHash, StringComparison.Ordinal))
            throw new InvalidOperationException("The updater download failed SHA-256 verification.");

        progress.Report(new(82, "Extracting verified updater", null));
        var extractRoot = Path.Combine(cacheRoot, "extracted");
        Directory.CreateDirectory(extractRoot);
        var replacementName = UpdaterFileName();
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            var replacementEntry = archive.Entries.SingleOrDefault(entry =>
                entry.FullName.Equals(replacementName, StringComparison.Ordinal));
            if (replacementEntry is null)
                throw new InvalidOperationException($"The updater archive does not contain {replacementName}.");
            replacementEntry.ExtractToFile(Path.Combine(extractRoot, replacementName), true);
        }

        var replacement = Path.Combine(extractRoot, replacementName);
        EnsureExecutable(replacement);
        progress.Report(new(92, "Starting verified updater replacement", null));
        var start = new ProcessStartInfo
        {
            FileName = replacement,
            UseShellExecute = false,
            WorkingDirectory = extractRoot
        };
        start.ArgumentList.Add("--replace-updater");
        start.ArgumentList.Add(originalUpdater);
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        if (Process.Start(start) is null)
            throw new InvalidOperationException("Could not start the verified updater replacement.");
        progress.Report(new(100, "Verified updater is ready to replace this version", release.TagName));
    }

    internal static int ReplaceRunningUpdater(string targetPath, int previousProcessId)
    {
        var target = Path.GetFullPath(targetPath);
        if (!Path.GetFileName(target).Equals(UpdaterFileName(), PathComparison()))
            throw new InvalidOperationException("Updater replacement target has an unexpected filename.");
        if (!File.Exists(target)) throw new FileNotFoundException("Updater replacement target is missing.", target);

        try
        {
            using var previous = Process.GetProcessById(previousProcessId);
            previous.WaitForExit(30_000);
            if (!previous.HasExited)
                throw new InvalidOperationException("The previous updater did not exit within 30 seconds.");
        }
        catch (ArgumentException)
        {
            // The previous process already exited.
        }

        var replacement = Environment.ProcessPath
                          ?? throw new InvalidOperationException("The replacement updater path is unavailable.");
        File.Copy(replacement, target, true);
        if (!HashFile(replacement).Equals(HashFile(target), StringComparison.Ordinal))
            throw new InvalidOperationException("The replaced updater failed SHA-256 verification.");
        EnsureExecutable(target);

        var restart = new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(target)!
        };
        if (Process.Start(restart) is null)
            throw new InvalidOperationException("The updated updater could not be restarted.");
        return 0;
    }

    private static async Task DownloadAsync(
        HttpClient client,
        Uri uri,
        string destination,
        int startPercent,
        int endPercent,
        IProgress<PackInstallEngine.InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var expectedBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None,
            1024 * 1024, true);
        var buffer = new byte[1024 * 1024];
        long received = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            var percent = expectedBytes is > 0
                ? startPercent + (int)((endPercent - startPercent) * received / expectedBytes.Value)
                : startPercent;
            progress.Report(new(Math.Clamp(percent, startPercent, endPercent), "Downloading updater update",
                $"{received:N0} bytes"));
        }

        if (expectedBytes is > 0 && received != expectedBytes.Value)
            throw new InvalidOperationException(
                $"Updater download size mismatch: received {received}; expected {expectedBytes.Value} bytes.");
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SPT413-Phetzy-Updater/1.1");
        return client;
    }

    private static Uri RequireUri(string value, bool requireGitHubApi)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Updater release URLs must use HTTPS.");
        var expectedHost = requireGitHubApi ? "api.github.com" : "github.com";
        if (!uri.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Updater release URL must use {expectedHost}.");
        return uri;
    }

    private static string UpdaterFileName() =>
        OperatingSystem.IsLinux() ? "SPT413-Phetzy-Updater.Linux" : "SPT413-Phetzy-Updater.exe";

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsLinux()) return;
        File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute);
    }

    private static string SanitizePathComponent(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
