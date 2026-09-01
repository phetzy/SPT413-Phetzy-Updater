using Phetzy.Spt413Updater.CrossPlatform;
using Xunit;

namespace SPT413_Phetzy_Updater.CrossPlatform.Tests;

public sealed class ChangelogStateStoreTests
{
    [Fact]
    public void State_ShowsFirstRunAndSkipsSameUpdaterAndReleaseAfterViewing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"phetzy-changelog-{Guid.NewGuid():N}");
        var statePath = Path.Combine(root, "state.json");
        var store = new ChangelogStateStore(statePath);
        try
        {
            Assert.True(store.ShouldShow("release-1", "1.2.1"));
            store.MarkShown("release-1", "1.2.1");
            Assert.False(store.ShouldShow("release-1", "1.2.1"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("release-2", "1.2.1")]
    [InlineData("release-1", "1.2.2")]
    public void State_ShowsForNewPackOrNewUpdater(string releaseId, string updaterVersion)
    {
        var root = Path.Combine(Path.GetTempPath(), $"phetzy-changelog-{Guid.NewGuid():N}");
        var statePath = Path.Combine(root, "state.json");
        var store = new ChangelogStateStore(statePath);
        try
        {
            store.MarkShown("release-1", "1.2.1");
            Assert.True(store.ShouldShow(releaseId, updaterVersion));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Format_ShowsModVersionsAndDetails()
    {
        var release = new PackInstallEngine.PackRelease(
            "release-2", "Pack release 2", "2026-09-01T03:00:00Z", "https://example.test/manifest.json",
            new string('A', 64), "available", false, "Combat integration fixes.",
            [new PackInstallEngine.ModChange("MoreBotsAPI", "2.0.1", "2.0.1-phetzy.2",
                ["Restored SAIN mappings."])]);

        var text = ChangelogStateStore.Format(release);

        Assert.Contains("MoreBotsAPI — 2.0.1 → 2.0.1-phetzy.2", text, StringComparison.Ordinal);
        Assert.Contains("• Restored SAIN mappings.", text, StringComparison.Ordinal);
    }
}
