namespace Phetzy.Spt413Updater.CrossPlatform;

internal sealed class ProgressLogReducer
{
    private string? _lastPhase;
    private int _lastPercent = -1;

    internal string? Accept(PackInstallEngine.InstallProgress update)
    {
        var phaseChanged = !string.Equals(update.Phase, _lastPhase, StringComparison.Ordinal);
        var milestoneReached = _lastPercent < 0 || update.Percent >= _lastPercent + 5;
        var completed = update.Percent == 100 && _lastPercent != 100;
        if (!phaseChanged && !milestoneReached && !completed) return null;

        _lastPhase = update.Phase;
        _lastPercent = update.Percent;
        return ProgressPresentation.FormatLogLine(update);
    }
}

internal static class ProgressPresentation
{
    internal static string FormatLogLine(PackInstallEngine.InstallProgress update) =>
        $"[{update.Percent,3}%] {update.Phase}" +
        (update.Detail is null ? "" : $" — {update.Detail}");

    internal static string FormatTransferDetail(long received, long? total, TimeSpan elapsed)
    {
        var speed = elapsed.TotalSeconds > 0 ? received / elapsed.TotalSeconds : 0;
        var receivedText = FormatBytes(received);
        var totalText = total is > 0 ? FormatBytes(total.Value) : "unknown";
        return $"{receivedText} / {totalText} — {FormatBytes(speed)}/s";
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }
}
