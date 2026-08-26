using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace Phetzy.Spt413Updater.CrossPlatform;

internal sealed class MainWindow : Window
{
    private readonly PackInstallEngine _engine = new();
    private readonly TextBox _installPath = new() { PlaceholderText = "Select the combined SPT 4.1.3 folder" };
    private readonly Button _browse = new() { Content = "Browse…" };
    private readonly Button _install = new() { Content = "Fresh install from private pack" };
    private readonly Button _hotfix = new() { Content = "Apply Hotfix" };
    private readonly Button _checkUpdater = new() { Content = "Check for updater updates" };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Height = 22 };
    private readonly TextBlock _phase = new() { Text = "Select your SPT installation folder." };
    private readonly TextBlock _detail = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox _log = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        MinHeight = 160
    };

    internal MainWindow()
    {
        Title = "SPT 4.1.3 Phetzy Updater";
        Width = 860;
        Height = 560;
        MinWidth = 700;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var title = new TextBlock
        {
            Text = "SPT 4.1.3 Mod Pack",
            FontSize = 24,
            FontWeight = Avalonia.Media.FontWeight.Bold
        };
        var explanation = new TextBlock
        {
            Text = "Validates SPT 4.1.3 / EFT 40743, downloads the signed private pack, verifies every archive, " +
                   "and installs it with native Windows or Linux extraction.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        var pathGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        pathGrid.Children.Add(_installPath);
        Grid.SetColumn(_browse, 1);
        pathGrid.Children.Add(_browse);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { _install, _hotfix, _checkUpdater }
        };

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 14,
                Children = { title, explanation, pathGrid, buttons, _progress, _phase, _detail, _log }
            }
        };

        _browse.Click += BrowseClicked;
        _install.Click += InstallClicked;
        _hotfix.Click += HotfixClicked;
        _checkUpdater.Click += CheckUpdaterClicked;
        Opened += async (_, _) => await CheckForUpdaterUpdateAsync(showCurrentMessage: false);
    }

    private async void BrowseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var start = Directory.Exists(_installPath.Text) ? _installPath.Text : Environment.CurrentDirectory;
        var suggested = await StorageProvider.TryGetFolderFromPathAsync(start);
        var selections = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = suggested,
            Title = "Select combined SPT 4.1.3 installation"
        });
        if (selections.Count > 0) _installPath.Text = selections[0].Path.LocalPath;
    }

    private async void InstallClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunAsync(async reporter => await _engine.InstallFromChannelAsync(_installPath.Text ?? "", reporter));
    }

    private async void HotfixClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunAsync(reporter => Task.FromResult(_engine.ApplyHotfix(_installPath.Text ?? "", reporter)));
    }

    private async void CheckUpdaterClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await CheckForUpdaterUpdateAsync(showCurrentMessage: true);

    private async Task CheckForUpdaterUpdateAsync(bool showCurrentMessage)
    {
        _checkUpdater.IsEnabled = false;
        try
        {
            var release = await UpdaterSelfUpdate.CheckAsync();
            if (release is null)
            {
                _checkUpdater.Content = "Updater is current";
                if (showCurrentMessage) await ShowMessageAsync("No update available", "This updater is current.");
                return;
            }

            _checkUpdater.Content = $"Install updater {release.TagName}";
            var answer = await ShowQuestionAsync(
                "Updater update available",
                $"Updater {release.TagName} is available. Download, verify, install, and restart it now?");
            if (!answer) return;

            await RunAsync(async reporter =>
            {
                await UpdaterSelfUpdate.DownloadVerifyAndLaunchAsync(release, reporter);
                return "The verified updater is staged. This process will now close.";
            });
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _log.Text += $"UPDATER CHECK ERROR: {ex}\n";
            if (showCurrentMessage) await ShowMessageAsync("Updater check failed", ex.Message);
        }
        finally
        {
            _checkUpdater.IsEnabled = true;
        }
    }

    private async Task RunAsync(Func<IProgress<PackInstallEngine.InstallProgress>, Task<string>> operation)
    {
        SetEnabled(false);
        _log.Text = "";
        var reporter = new Progress<PackInstallEngine.InstallProgress>(update =>
        {
            _progress.Value = update.Percent;
            _phase.Text = update.Phase;
            _detail.Text = update.Detail ?? "";
            _log.Text += $"[{update.Percent,3}%] {update.Phase}{(update.Detail is null ? "" : $" — {update.Detail}")}\n";
            _log.CaretIndex = _log.Text.Length;
        });

        try
        {
            var result = await Task.Run(async () => await operation(reporter));
            _phase.Text = "Complete";
            _detail.Text = result;
            await ShowMessageAsync("Installation complete", result);
        }
        catch (Exception ex)
        {
            _phase.Text = "Stopped";
            _detail.Text = ex.Message;
            _log.Text += $"ERROR: {ex}\n";
            await ShowMessageAsync("Installation stopped", ex.Message);
        }
        finally
        {
            SetEnabled(true);
        }
    }

    private void SetEnabled(bool enabled)
    {
        _browse.IsEnabled = enabled;
        _install.IsEnabled = enabled;
        _hotfix.IsEnabled = enabled;
        _checkUpdater.IsEnabled = enabled;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var close = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80 };
        var dialog = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, close
                }
            }
        };
        close.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private async Task<bool> ShowQuestionAsync(string title, string message)
    {
        var yes = new Button { Content = "Update", MinWidth = 90 };
        var no = new Button { Content = "Not now", MinWidth = 90 };
        var result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 540,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children = { no, yes }
                    }
                }
            }
        };
        no.Click += (_, _) => dialog.Close();
        yes.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };
        await dialog.ShowDialog(this);
        return result;
    }
}
