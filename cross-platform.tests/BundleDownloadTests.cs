using System.Net;
using System.Security.Cryptography;
using Phetzy.Spt413Updater.CrossPlatform;
using Xunit;

namespace SPT413_Phetzy_Updater.CrossPlatform.Tests;

public sealed class BundleDownloadTests
{
    [Fact]
    public async Task DownloadBundle_ReleasesWriterBeforeHashingAndPromotion()
    {
        var payload = Enumerable.Range(0, 2 * 1024 * 1024 + 17)
            .Select(index => (byte)(index % 251))
            .ToArray();
        using var client = new HttpClient(new StaticContentHandler(payload));
        var engine = new PackInstallEngine(client);
        var root = Path.Combine(Path.GetTempPath(), $"phetzy-download-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var outputPath = Path.Combine(root, "bundle.zip");
        var bundle = new PackInstallEngine.BundleEntry(
            "bundle.zip",
            "https://example.test/bundle.zip",
            payload.LongLength,
            Convert.ToHexString(SHA256.HashData(payload)));

        try
        {
            await engine.DownloadBundleAsync(bundle, outputPath, new SinkProgress(),
                CancellationToken.None);

            Assert.Equal(payload, await File.ReadAllBytesAsync(outputPath));
            Assert.Empty(Directory.EnumerateFiles(root, "*.part"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task DownloadBundle_ReusesVerifiedCompletedPartWithoutDownloadingAgain()
    {
        var payload = Enumerable.Range(0, 1024 * 1024 + 31)
            .Select(index => (byte)(index % 239))
            .ToArray();
        using var client = new HttpClient(new FailIfRequestedHandler());
        var engine = new PackInstallEngine(client);
        var root = Path.Combine(Path.GetTempPath(), $"phetzy-download-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var outputPath = Path.Combine(root, "bundle.zip");
        var completedPart = $"{outputPath}.{Guid.NewGuid():N}.part";
        await File.WriteAllBytesAsync(completedPart, payload);
        var bundle = new PackInstallEngine.BundleEntry(
            "bundle.zip",
            "https://example.test/bundle.zip",
            payload.LongLength,
            Convert.ToHexString(SHA256.HashData(payload)));

        try
        {
            await engine.DownloadBundleAsync(bundle, outputPath, new SinkProgress(), CancellationToken.None);

            Assert.Equal(payload, await File.ReadAllBytesAsync(outputPath));
            Assert.False(File.Exists(completedPart));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class StaticContentHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            });
    }

    private sealed class FailIfRequestedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new Xunit.Sdk.XunitException("A completed cached download must not trigger an HTTP request.");
    }

    private sealed class SinkProgress : IProgress<PackInstallEngine.InstallProgress>
    {
        public void Report(PackInstallEngine.InstallProgress value)
        {
        }
    }
}
