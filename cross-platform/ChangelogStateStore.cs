using System.Text.Json;

namespace Phetzy.Spt413Updater.CrossPlatform;

internal sealed class ChangelogStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _statePath;

    internal ChangelogStateStore(string? statePath = null)
    {
        _statePath = statePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhetzySptUpdater", "changelog-state.json");
    }

    internal bool ShouldShow(string releaseId, string updaterVersion)
    {
        var state = Read();
        return state is null ||
               !string.Equals(state.ReleaseId, releaseId, StringComparison.Ordinal) ||
               !string.Equals(state.UpdaterVersion, updaterVersion, StringComparison.Ordinal);
    }

    internal void MarkShown(string releaseId, string updaterVersion)
    {
        var directory = Path.GetDirectoryName(_statePath)
                        ?? throw new InvalidOperationException("Changelog state has no parent directory.");
        Directory.CreateDirectory(directory);
        var temp = $"{_statePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(
                new ChangelogDisplayState(releaseId, updaterVersion), JsonOptions));
            File.Move(temp, _statePath, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    internal static string Format(PackInstallEngine.PackRelease release)
    {
        var lines = new List<string>
        {
            release.Label,
            $"Release: {release.ReleaseId}"
        };
        if (!string.IsNullOrWhiteSpace(release.Notes))
        {
            lines.Add("");
            lines.Add(release.Notes.Trim());
        }

        lines.Add("");
        lines.Add("Mod changes");
        var changes = release.Changes ?? [];
        if (changes.Count == 0)
        {
            lines.Add("No individual mod updates are recorded for this release.");
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var change in changes)
        {
            lines.Add("");
            var version = FormatVersion(change.PreviousVersion, change.Version);
            lines.Add(string.IsNullOrWhiteSpace(version) ? change.Mod : $"{change.Mod} — {version}");
            foreach (var detail in change.Details) lines.Add($"  • {detail}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private ChangelogDisplayState? Read()
    {
        if (!File.Exists(_statePath)) return null;
        try
        {
            return JsonSerializer.Deserialize<ChangelogDisplayState>(File.ReadAllText(_statePath));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatVersion(string? previous, string? current)
    {
        if (!string.IsNullOrWhiteSpace(previous) && !string.IsNullOrWhiteSpace(current))
            return $"{previous} → {current}";
        if (!string.IsNullOrWhiteSpace(current)) return current;
        return previous ?? "";
    }

    private sealed record ChangelogDisplayState(string ReleaseId, string UpdaterVersion);
}
