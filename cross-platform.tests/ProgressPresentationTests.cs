using Phetzy.Spt413Updater.CrossPlatform;
using Xunit;

namespace Phetzy.Spt413Updater.CrossPlatform.Tests;

public sealed class ProgressPresentationTests
{
    [Fact]
    public void RepeatedNetworkReadsDoNotFloodTheVisibleLog()
    {
        var reducer = new ProgressLogReducer();
        var lines = Enumerable.Range(1, 10_000)
            .Select(bytes => reducer.Accept(new PackInstallEngine.InstallProgress(
                4,
                "Downloading mod pack",
                $"{bytes:N0} / 13,281,886,914 bytes")))
            .Count(line => line is not null);

        Assert.Equal(1, lines);
    }

    [Fact]
    public void OverallProgressMilestonesRemainVisible()
    {
        var reducer = new ProgressLogReducer();
        var lines = Enumerable.Range(0, 101)
            .Select(percent => reducer.Accept(new PackInstallEngine.InstallProgress(
                percent,
                "Installing verified archives",
                $"Archive {percent}")))
            .Where(line => line is not null)
            .ToArray();

        Assert.InRange(lines.Length, 20, 22);
        Assert.Contains(lines, line => line!.StartsWith("[100%]"));
    }

    [Fact]
    public void DownloadedBundleInstallationProgressNeverMovesBehindDownloadCompletion()
    {
        var mapped = new[] { 0, 5, 30, 88, 91, 100 }
            .Select(percent => PackInstallEngine.MapDownloadedBundleProgress(
                new PackInstallEngine.InstallProgress(percent, "phase", null)).Percent)
            .ToArray();

        Assert.Equal([25, 28, 47, 91, 93, 100], mapped);
        Assert.True(mapped.SequenceEqual(mapped.Order()));
    }

    [Fact]
    public void TransferDetailContainsReadableAmountTotalAndSpeed()
    {
        var detail = ProgressPresentation.FormatTransferDetail(
            1024 * 1024,
            4 * 1024 * 1024,
            TimeSpan.FromSeconds(2));

        Assert.Equal("1.0 MiB / 4.0 MiB — 512.0 KiB/s", detail);
    }
}
