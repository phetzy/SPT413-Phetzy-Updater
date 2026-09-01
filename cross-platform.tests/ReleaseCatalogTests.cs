using Phetzy.Spt413Updater.CrossPlatform;
using Xunit;

namespace SPT413_Phetzy_Updater.CrossPlatform.Tests;

public sealed class ReleaseCatalogTests
{
    private const string Hash = "8E2373C84043D893235D92D0358BB95304CD46844D9C97D1EE93C6C9C82D67B5";

    [Fact]
    public void ValidateReleaseCatalog_AcceptsCurrentAndRollbackReleases()
    {
        var catalog = Catalog(
            Release("spt413-phetzy.2-aaaaaaaaaaaa", "available"),
            Release("spt413-phetzy.1-8e2373c84043", "available"));

        PackInstallEngine.ValidateReleaseCatalog(catalog);
    }

    [Fact]
    public void ValidateReleaseCatalog_RejectsMissingCurrentRelease()
    {
        var catalog = Catalog(Release("spt413-phetzy.1-8e2373c84043", "available")) with
        {
            CurrentReleaseId = "spt413-phetzy.2-aaaaaaaaaaaa"
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            PackInstallEngine.ValidateReleaseCatalog(catalog));

        Assert.Contains("current release", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateReleaseCatalog_RejectsMutableHttpManifestLocation()
    {
        var release = Release("spt413-phetzy.1-8e2373c84043", "available") with
        {
            ManifestUrl = "http://example.test/releases/spt413-phetzy.1/manifest.json"
        };

        Assert.Throws<InvalidOperationException>(() =>
            PackInstallEngine.ValidateReleaseCatalog(Catalog(release)));
    }

    [Fact]
    public void ValidateReleaseCatalog_RejectsUnknownStatus()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PackInstallEngine.ValidateReleaseCatalog(Catalog(
                Release("spt413-phetzy.1-8e2373c84043", "broken"))));
    }

    [Fact]
    public void ValidateReleaseCatalog_RejectsReleaseIdThatCouldEscapeCache()
    {
        var release = Release("spt413-phetzy.1-8e2373c84043", "available") with
        {
            ReleaseId = "../../outside"
        };
        var catalog = Catalog(release) with { CurrentReleaseId = "../../outside" };

        Assert.Throws<InvalidOperationException>(() => PackInstallEngine.ValidateReleaseCatalog(catalog));
    }

    [Fact]
    public void ValidateReleaseCatalog_RejectsModChangeWithoutDetails()
    {
        var release = Release("spt413-phetzy.1-8e2373c84043", "available") with
        {
            Changes = [new PackInstallEngine.ModChange("MoreBotsAPI", "2.0.1", "2.0.2", [])]
        };

        Assert.Throws<InvalidOperationException>(() =>
            PackInstallEngine.ValidateReleaseCatalog(Catalog(release)));
    }

    private static PackInstallEngine.ReleaseCatalog Catalog(params PackInstallEngine.PackRelease[] releases) =>
        new(1, "spt413", PackInstallEngine.ExpectedSptVersion, PackInstallEngine.ExpectedEftVersion,
            "spt413-phetzy.1-8e2373c84043", releases.ToList());

    private static PackInstallEngine.PackRelease Release(string id, string status) =>
        new(id, id, "2026-08-31T18:00:00Z", $"https://example.test/releases/{id}/manifest.json",
            Hash, status);

    [Fact]
    public void RemoveObsoleteManagedFiles_DeletesOnlyPriorPackOwnedRuntimeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"phetzy-rollback-test-{Guid.NewGuid():N}");
        var obsolete = Path.Combine(root, "BepInEx", "plugins", "obsolete.dll");
        var retained = Path.Combine(root, "BepInEx", "plugins", "retained.dll");
        var config = Path.Combine(root, "BepInEx", "config", "obsolete.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(obsolete)!);
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(obsolete, "old");
        File.WriteAllText(retained, "keep");
        File.WriteAllText(config, "user setting");

        try
        {
            var removed = PackInstallEngine.RemoveObsoleteManagedFiles(root,
                ["BepInEx/plugins/obsolete.dll", "BepInEx/plugins/retained.dll", "BepInEx/config/obsolete.cfg"],
                ["BepInEx/plugins/retained.dll"]);

            Assert.Equal(1, removed);
            Assert.False(File.Exists(obsolete));
            Assert.True(File.Exists(retained));
            Assert.True(File.Exists(config));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
