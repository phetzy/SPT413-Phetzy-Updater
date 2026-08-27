using Phetzy.Spt413Updater.CrossPlatform;
using Xunit;

namespace Phetzy.Spt413Updater.CrossPlatform.Tests;

public sealed class UpdaterSelfUpdateStrategyTests
{
    [Fact]
    public void LinuxUsesAtomicReplacementWithParentAwareRestart()
    {
        Assert.Equal(
            UpdaterSelfUpdate.HandoffStrategy.AtomicReplaceAndParentAwareRestart,
            UpdaterSelfUpdate.SelectHandoffStrategy(isLinux: true));
    }

    [Fact]
    public void WindowsKeepsPostExitReplacementHelper()
    {
        Assert.Equal(
            UpdaterSelfUpdate.HandoffStrategy.HelperAfterExit,
            UpdaterSelfUpdate.SelectHandoffStrategy(isLinux: false));
    }

    [Fact]
    public void LinuxAtomicReplacementHandlesPathsWithSpacesAndBuildsParentAwareRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), $"phetzy updater test {Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "SPT413-Phetzy-Updater.Linux");
            var replacement = Path.Combine(root, "downloaded replacement");
            File.WriteAllText(target, "old updater");
            File.WriteAllText(replacement, "new updater");

            var restart = UpdaterSelfUpdate.PrepareLinuxAtomicReplacement(target, replacement, 12345);

            Assert.Equal("new updater", File.ReadAllText(target));
            Assert.Equal(target, restart.FileName);
            Assert.Equal(root, restart.WorkingDirectory);
            Assert.False(restart.UseShellExecute);
            Assert.Equal(["--wait-for-parent", "12345"], restart.ArgumentList);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
