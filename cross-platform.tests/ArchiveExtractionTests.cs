using System.IO.Compression;
using Phetzy.Spt413Updater.CrossPlatform;
using Xunit;

namespace Phetzy.Spt413Updater.CrossPlatform.Tests;

public sealed class ArchiveExtractionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"phetzy-updater-tests-{Guid.NewGuid():N}");

    public ArchiveExtractionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ExtractsAValidModArchive()
    {
        var archivePath = CreateArchive(("BepInEx/plugins/example.dll", "payload"));
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);

        PackInstallEngine.ExtractModArchive(archivePath, output);

        Assert.Equal("payload", File.ReadAllText(Path.Combine(output, "BepInEx", "plugins", "example.dll")));
    }

    [Fact]
    public void ExtractsWindowsSeparatorEntriesIntoPlatformDirectories()
    {
        var archivePath = CreateArchive((@"BepInEx\plugins\example.dll", "payload"));
        var output = Path.Combine(_root, "windows-separator-output");
        Directory.CreateDirectory(output);

        PackInstallEngine.ExtractModArchive(archivePath, output);

        Assert.Equal("payload", File.ReadAllText(Path.Combine(output, "BepInEx", "plugins", "example.dll")));
    }

    [Fact]
    public void RepairsLiteralWindowsSeparatorArtifactsOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        var output = Path.Combine(_root, "repair-output");
        Directory.CreateDirectory(output);
        var misplaced = Path.Combine(output, @"BepInEx\plugins\example.dll");
        File.WriteAllText(misplaced, "recovered payload");

        var repaired = PackInstallEngine.RepairBackslashArtifacts(output);

        Assert.Equal(1, repaired);
        Assert.False(File.Exists(misplaced));
        Assert.Equal("recovered payload",
            File.ReadAllText(Path.Combine(output, "BepInEx", "plugins", "example.dll")));
    }

    [Fact]
    public void RepairIgnoresBackslashArtifactsOutsideManagedRootsOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        var output = Path.Combine(_root, "repair-with-source-output");
        Directory.CreateDirectory(output);
        var misplacedMod = Path.Combine(output, @"BepInEx\plugins\example.dll");
        var unrelatedSource = Path.Combine(output, @"SOURCE\Plugin.cs");
        File.WriteAllText(misplacedMod, "recovered payload");
        File.WriteAllText(unrelatedSource, "source payload");

        var repaired = PackInstallEngine.RepairBackslashArtifacts(output);

        Assert.Equal(1, repaired);
        Assert.False(File.Exists(misplacedMod));
        Assert.Equal("recovered payload",
            File.ReadAllText(Path.Combine(output, "BepInEx", "plugins", "example.dll")));
        Assert.Equal("source payload", File.ReadAllText(unrelatedSource));
    }

    [Fact]
    public void RejectsTraversalBeforeWritingAnyFiles()
    {
        var archivePath = CreateArchive(
            ("BepInEx/plugins/safe.dll", "must not be written"),
            ("../escaped.dll", "blocked"));
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);

        var error = Assert.Throws<InvalidOperationException>(() =>
            PackInstallEngine.ExtractModArchive(archivePath, output));

        Assert.Contains("escapes", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(_root, "escaped.dll")));
    }

    [Fact]
    public void RejectsCaseCollisionsBeforeWritingAnyFiles()
    {
        var archivePath = CreateArchive(
            ("BepInEx/plugins/example.dll", "one"),
            ("BepInEx/Plugins/EXAMPLE.dll", "two"));
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);

        var error = Assert.Throws<InvalidOperationException>(() =>
            PackInstallEngine.ExtractModArchive(archivePath, output));

        Assert.Contains("case-colliding", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void RejectsSymbolicLinks()
    {
        var archivePath = Path.Combine(_root, $"{Guid.NewGuid():N}.zip");
        using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("BepInEx/plugins/link.dll");
            entry.ExternalAttributes = unchecked((int)((0xA000 | 0x1FF) << 16));
            using var writer = new StreamWriter(entry.Open());
            writer.Write("target.dll");
        }

        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        var error = Assert.Throws<InvalidOperationException>(() =>
            PackInstallEngine.ExtractModArchive(archivePath, output));

        Assert.Contains("symbolic link", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories));
    }

    private string CreateArchive(params (string Path, string Contents)[] entries)
    {
        var archivePath = Path.Combine(_root, $"{Guid.NewGuid():N}.zip");
        using var stream = File.Create(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (path, contents) in entries)
        {
            var entry = archive.CreateEntry(path);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(contents);
        }

        return archivePath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
