using Phetzy.Spt413Updater.CrossPlatform;
using Xunit;

namespace Phetzy.Spt413Updater.CrossPlatform.Tests;

public sealed class BuildVersionValidationTests
{
    [Fact]
    public void LinuxUnknownEftMetadataAcceptsExact40743ExecutableHash()
    {
        PackInstallEngine.ValidateBuildVersions(
            eftVersion: null,
            serverVersion: PackInstallEngine.ExpectedSptVersion,
            eftSha256: PackInstallEngine.ExpectedEftSha256);
    }

    [Fact]
    public void LinuxUnknownEftMetadataRejectsDifferentExecutableHash()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            PackInstallEngine.ValidateBuildVersions(
                eftVersion: null,
                serverVersion: PackInstallEngine.ExpectedSptVersion,
                eftSha256: new string('0', 64)));

        Assert.Contains("EFT unknown", error.Message);
        Assert.Contains(new string('0', 64), error.Message);
    }

    [Fact]
    public void ExactEftVersionStillRejectsWrongSptVersion()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            PackInstallEngine.ValidateBuildVersions(
                eftVersion: PackInstallEngine.ExpectedEftVersion,
                serverVersion: "4.1.2"));

        Assert.Contains("SPT 4.1.2", error.Message);
    }
}
