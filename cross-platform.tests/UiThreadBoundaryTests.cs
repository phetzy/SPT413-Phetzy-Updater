using Phetzy.Spt413Updater.CrossPlatform;
using Xunit;

namespace Phetzy.Spt413Updater.CrossPlatform.Tests;

public sealed class UiThreadBoundaryTests
{
    [Fact]
    public async Task PathOperationCapturesUiValueBeforeWorkerStarts()
    {
        var ownerThread = Environment.CurrentManagedThreadId;
        string ReadThreadAffinePath()
        {
            if (Environment.CurrentManagedThreadId != ownerThread)
            {
                throw new InvalidOperationException(
                    "The calling thread cannot access this object because a different thread owns it.");
            }

            return "/home/anthony/Games/SPT";
        }

        var operation = MainWindow.BindPathOperation(
            ReadThreadAffinePath,
            (path, _) => Task.FromResult(path));
        var reporter = new Progress<PackInstallEngine.InstallProgress>(_ => { });

        var result = await Task.Run(() => operation(reporter));

        Assert.Equal("/home/anthony/Games/SPT", result);
    }

    [Fact]
    public void ExpectedValidationErrorsAreShownWithoutAStackTrace()
    {
        var error = new InvalidOperationException(
            "Existing server mods were found. Use Verify Mod Pack Install for an existing pack.");

        var formatted = MainWindow.FormatOperationError(error);

        Assert.Equal(
            "ERROR: Existing server mods were found. Use Verify Mod Pack Install for an existing pack.",
            formatted);
        Assert.DoesNotContain(" at ", formatted, StringComparison.Ordinal);
    }
}
