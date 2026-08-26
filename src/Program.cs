using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Phetzy.Spt413Updater;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0].Equals("--replace-updater", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return UpdaterSelfUpdate.ReplaceRunningUpdater(args[1], int.Parse(args[2]));
            }
            catch (Exception ex)
            {
                WriteCliError(ex);
                return 1;
            }
        }

        if (args.Length == 1 && args[0].Equals("--check-updater", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var release = UpdaterSelfUpdate.CheckAsync().GetAwaiter().GetResult();
                Console.WriteLine(release is null ? "CURRENT" : $"AVAILABLE {release.TagName}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                WriteCliError(ex);
                return 1;
            }
        }

        if (args.Length == 1 && args[0].Equals("--self-update-fixture", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (Environment.GetEnvironmentVariable("PHETZY_UPDATER_FIXTURE_MODE") != "1")
                    throw new InvalidOperationException("The self-update fixture command is disabled outside fixture mode.");
                var release = UpdaterSelfUpdate.CheckAsync().GetAwaiter().GetResult()
                    ?? throw new InvalidOperationException("The self-update fixture did not advertise a newer version.");
                UpdaterSelfUpdate.DownloadVerifyAndLaunchAsync(
                    release,
                    new Progress<UpdaterForm.UpdateProgress>(_ => { })).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                WriteCliError(ex);
                return 1;
            }
        }

        if (args.Length == 2 && args[0].Equals("--generate-cloudfront-key", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var outputDirectory = Path.GetFullPath(args[1]);
                Directory.CreateDirectory(outputDirectory);
                using var rsa = RSA.Create(2048);
                File.WriteAllText(Path.Combine(outputDirectory, "cloudfront-private.pem"), rsa.ExportRSAPrivateKeyPem());
                File.WriteAllText(Path.Combine(outputDirectory, "cloudfront-public.pem"), rsa.ExportSubjectPublicKeyInfoPem());
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                WriteCliError(ex);
                return 1;
            }
        }

        if (args.Length == 2 && args[0].Equals("--install", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                UpdaterForm.ApplyUpdate(args[1], new Progress<UpdaterForm.UpdateProgress>(_ => { }));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                WriteCliError(ex);
                return 1;
            }
        }

        if (args.Length == 2 && args[0].Equals("--install-full", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                UpdaterForm.InstallFullPack(args[1], new Progress<UpdaterForm.UpdateProgress>(_ => { }));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                WriteCliError(ex);
                return 1;
            }
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new UpdaterForm());
        return 0;
    }

    private static void WriteCliError(Exception exception)
    {
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "updater-cli-error.log"), exception.ToString());
    }
}

internal sealed class UpdaterForm : Form
{
    private const string ExpectedEftVersion = "0.16.9.40743";
    private const string ExpectedSptVersion = "4.1.3";
    private const string PayloadResource = "Phetzy.Spt413Updater.Payload.JBOBYH_ItemPreviewQoL.dll";
    private const string NewHash = "C20A912CE1A83DBC260D8A843A64AC8A0F03B26FCD9E43A24CE8670C0B8A17E2";
    private const string OldHash = "4D1886B66F3F9B3BE1D28D18B68AB664407659E6CE8DB1DA05329170564E24B6";

    private readonly TextBox _installPath = new() { Dock = DockStyle.Fill };
    private readonly Button _browse = new() { Text = "Browse…", AutoSize = true };
    private readonly Button _installPack = new() { Text = "Fresh install from private pack", AutoSize = true };
    private readonly Button _apply = new() { Text = "Apply inspect-rotation hotfix", AutoSize = true };
    private readonly Button _checkUpdater = new() { Text = "Check for updater updates", AutoSize = true };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };
    private readonly Label _phase = new() { Text = "Select your combined SPT installation folder.", AutoSize = true };
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    public UpdaterForm()
    {
        Text = "SPT 4.1.3 Phetzy Updater";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 390);
        Size = new Size(820, 470);
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "SPT 4.1.3 — Inspect Rotation Hotfix",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        };
        var description = new Label
        {
            Text = "Updates Item Preview QoL for EFT 40743. The updater validates versions and hashes, creates a backup, and refuses active SPT processes.",
            AutoSize = true,
            MaximumSize = new Size(760, 0)
        };

        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(_installPath, 0, 0);
        pathRow.Controls.Add(_browse, 1, 0);

        var actionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        actionRow.Controls.Add(_installPack);
        actionRow.Controls.Add(_apply);
        actionRow.Controls.Add(_checkUpdater);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 8
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(description, 0, 1);
        layout.Controls.Add(new Label { Text = "SPT installation folder", AutoSize = true, Margin = new Padding(0, 14, 0, 3) }, 0, 2);
        layout.Controls.Add(pathRow, 0, 3);
        layout.Controls.Add(_phase, 0, 4);
        layout.Controls.Add(_progress, 0, 5);
        layout.Controls.Add(_log, 0, 6);
        layout.Controls.Add(actionRow, 0, 7);
        Controls.Add(layout);

        _browse.Click += BrowseClicked;
        _installPack.Click += InstallPackClicked;
        _apply.Click += ApplyClicked;
        _checkUpdater.Click += CheckUpdaterClicked;
        Shown += async (_, _) => await CheckForUpdaterUpdateAsync(showWhenCurrent: false);
    }

    private void BrowseClicked(object? sender, EventArgs e)
    {
        using var picker = new FolderBrowserDialog
        {
            Description = "Select the folder containing EscapeFromTarkov.exe and SPT_Runtime",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = Directory.Exists(_installPath.Text) ? _installPath.Text : string.Empty
        };
        if (picker.ShowDialog(this) == DialogResult.OK)
            _installPath.Text = picker.SelectedPath;
    }

    private async void ApplyClicked(object? sender, EventArgs e)
    {
        SetControlsEnabled(false);
        _log.Clear();
        SetProgress(0, "Starting validation…");

        var reporter = new Progress<UpdateProgress>(p =>
        {
            SetProgress(p.Percent, p.Phase);
            if (!string.IsNullOrWhiteSpace(p.Detail))
                _log.AppendText(p.Detail + Environment.NewLine);
        });

        try
        {
            var result = await Task.Run(() => ApplyUpdate(_installPath.Text, reporter));
            SetProgress(100, "Update complete.");
            MessageBox.Show(this, result, "Update complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _phase.Text = "Update refused. No unverified payload was installed.";
            _log.AppendText("ERROR: " + ex.Message + Environment.NewLine);
            MessageBox.Show(this, ex.Message, "Update refused", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void InstallPackClicked(object? sender, EventArgs e)
    {
        SetControlsEnabled(false);
        _log.Clear();
        SetProgress(0, "Loading private-pack source…");

        var reporter = new Progress<UpdateProgress>(p =>
        {
            SetProgress(p.Percent, p.Phase);
            if (!string.IsNullOrWhiteSpace(p.Detail))
                _log.AppendText(p.Detail + Environment.NewLine);
        });

        try
        {
            var result = await Task.Run(() => InstallFullPack(_installPath.Text, reporter));
            SetProgress(100, "Full mod-pack installation complete.");
            MessageBox.Show(this, result, "Installation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _phase.Text = "Installation stopped. Review the error below.";
            _log.AppendText("ERROR: " + ex.Message + Environment.NewLine);
            MessageBox.Show(this, ex.Message, "Installation stopped", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void CheckUpdaterClicked(object? sender, EventArgs e)
    {
        await CheckForUpdaterUpdateAsync(showWhenCurrent: true);
    }

    private async Task CheckForUpdaterUpdateAsync(bool showWhenCurrent)
    {
        try
        {
            _checkUpdater.Enabled = false;
            var release = await UpdaterSelfUpdate.CheckAsync();
            if (release is null)
            {
                if (showWhenCurrent)
                    MessageBox.Show(this, "This updater is current.", "No update available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var answer = MessageBox.Show(
                this,
                $"Updater {release.TagName} is available. Download, verify, install, and restart it now?",
                "Updater update available",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;

            SetControlsEnabled(false);
            _log.Clear();
            SetProgress(0, "Downloading updater updateâ€¦");
            var reporter = new Progress<UpdateProgress>(p =>
            {
                SetProgress(p.Percent, p.Phase);
                if (!string.IsNullOrWhiteSpace(p.Detail))
                    _log.AppendText(p.Detail + Environment.NewLine);
            });
            await UpdaterSelfUpdate.DownloadVerifyAndLaunchAsync(release, reporter);
            SetProgress(100, "Verified updater staged. Restartingâ€¦");
            Close();
        }
        catch (Exception ex)
        {
            _log.AppendText("Updater check failed: " + ex.Message + Environment.NewLine);
            if (showWhenCurrent)
                MessageBox.Show(this, ex.Message, "Updater check failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed) SetControlsEnabled(true);
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        _installPack.Enabled = enabled;
        _apply.Enabled = enabled;
        _checkUpdater.Enabled = enabled;
        _browse.Enabled = enabled;
        _installPath.Enabled = enabled;
    }

    private void SetProgress(int percent, string phase)
    {
        _progress.Value = Math.Clamp(percent, _progress.Minimum, _progress.Maximum);
        _phase.Text = phase;
    }

    internal static string ApplyUpdate(string selectedPath, IProgress<UpdateProgress> progress)
    {
        progress.Report(new(5, "Validating selected folder…", selectedPath));
        if (string.IsNullOrWhiteSpace(selectedPath))
            throw new InvalidOperationException("Select an SPT installation folder first.");

        var root = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var driveRoot = Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(root, driveRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A drive root cannot be used as the SPT installation folder.");
        if (root.Equals(@"C:\SPT_NEW", StringComparison.OrdinalIgnoreCase) ||
            root.StartsWith(@"C:\SPT_NEW\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(@"C:\SPT_NEW is protected and will not be modified.");

        var eft = Path.Combine(root, "EscapeFromTarkov.exe");
        var server = Path.Combine(root, "SPT_Runtime", "SPT.Server.exe");
        var target = Path.Combine(root, "BepInEx", "plugins", "JBOBYH", "JBOBYH_ItemPreviewQoL.dll");
        foreach (var path in new[] { eft, server, target })
            if (!File.Exists(path)) throw new FileNotFoundException("Required SPT file is missing.", path);

        progress.Report(new(18, "Checking SPT and EFT versions…", null));
        var eftVersion = FileVersionInfo.GetVersionInfo(eft).FileVersion;
        var sptVersion = FileVersionInfo.GetVersionInfo(server).FileVersion;
        if (!string.Equals(eftVersion, ExpectedEftVersion, StringComparison.Ordinal))
            throw new InvalidOperationException($"Wrong EFT build: found {eftVersion ?? "unknown"}; required {ExpectedEftVersion}.");
        if (!string.Equals(sptVersion, ExpectedSptVersion, StringComparison.Ordinal))
            throw new InvalidOperationException($"Wrong SPT version: found {sptVersion ?? "unknown"}; required {ExpectedSptVersion}.");
        progress.Report(new(28, "Versions accepted.", $"SPT {sptVersion}; EFT {eftVersion}"));

        var running = FindProcessesWithin(root);
        if (running.Count > 0)
            throw new InvalidOperationException("Close programs running from this SPT folder: " + string.Join(", ", running));

        progress.Report(new(38, "Verifying embedded update payload…", null));
        using var payload = OpenPayload();
        var payloadHash = HashStream(payload);
        if (!string.Equals(payloadHash, NewHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Embedded update payload failed SHA-256 verification.");

        progress.Report(new(50, "Inspecting installed mod…", null));
        var installedHash = HashFile(target);
        if (installedHash == NewHash)
        {
            progress.Report(new(100, "Already up to date.", "The installed Item Preview QoL DLL already matches the verified hotfix."));
            return "Item Preview QoL is already up to date. No files were changed.";
        }
        if (installedHash != OldHash)
            throw new InvalidOperationException($"The installed Item Preview QoL DLL is not a recognized build. SHA-256: {installedHash}");

        progress.Report(new(62, "Creating recoverable backup…", null));
        var backup = target + $".pre-inspect-rotation-hotfix-{installedHash[..8]}.bak";
        if (!File.Exists(backup)) File.Copy(target, backup, overwrite: false);
        if (HashFile(backup) != installedHash)
            throw new InvalidOperationException("Backup verification failed; the installed DLL was not replaced.");

        progress.Report(new(74, "Staging verified replacement…", null));
        var temporary = target + ".phetzy-update.tmp";
        try
        {
            payload.Position = 0;
            using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                payload.CopyTo(output);
            if (HashFile(temporary) != NewHash)
                throw new InvalidOperationException("Staged replacement failed SHA-256 verification.");

            progress.Report(new(88, "Installing replacement…", null));
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }

        progress.Report(new(96, "Running final audit…", null));
        if (HashFile(target) != NewHash)
            throw new InvalidOperationException("Final installed DLL failed SHA-256 verification. Restore the adjacent backup.");
        progress.Report(new(100, "Update complete.", $"Backup: {backup}"));
        return "The inspect-rotation hotfix was installed and verified. Restart SPT before testing item rotation.";
    }

    internal static string InstallFullPack(string selectedPath, IProgress<UpdateProgress> progress)
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "updater-source.json");
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Private-pack source configuration is missing.", sourcePath);
        var source = JsonSerializer.Deserialize<UpdaterSource>(File.ReadAllText(sourcePath), JsonOptions)
            ?? throw new InvalidOperationException("Private-pack source configuration is invalid.");
        if (!TryHttpsUri(source.ManifestUrl, out var manifestUri))
            throw new InvalidOperationException("The private manifest URL is not a valid HTTPS URL.");

        progress.Report(new(3, "Validating selected SPT folder…", selectedPath));
        var root = ValidateSptRoot(selectedPath, requireItemPreview: false);
        var running = FindProcessesWithin(root);
        if (running.Count > 0)
            throw new InvalidOperationException("Close programs running from this SPT folder: " + string.Join(", ", running));

        progress.Report(new(6, "Checking that the SPT installation is unmodded…", null));
        AssertFreshInstallation(root);

        progress.Report(new(8, "Downloading signed update manifest…", manifestUri.Host));
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var manifestJson = client.GetStringAsync(manifestUri).GetAwaiter().GetResult();
        var manifest = JsonSerializer.Deserialize<PackManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidOperationException("The private pack manifest is invalid.");
        if (manifest.SchemaVersion != 1 || manifest.Bundle is null)
            throw new InvalidOperationException("The private pack manifest schema is not supported.");
        if (manifest.SptVersion != ExpectedSptVersion || manifest.EftVersion != ExpectedEftVersion)
            throw new InvalidOperationException("The private pack manifest targets a different SPT or EFT version.");
        if (!TryHttpsUri(manifest.Bundle.Url, out var bundleUri))
            throw new InvalidOperationException("The private bundle URL is not a valid HTTPS URL.");
        if (manifest.Bundle.Bytes <= 0 || manifest.Bundle.Sha256.Length != 64 || string.IsNullOrWhiteSpace(manifest.Bundle.FileName))
            throw new InvalidOperationException("The private bundle manifest fields are incomplete.");

        var drive = new DriveInfo(Path.GetPathRoot(root)!);
        var minimumFree = checked(manifest.Bundle.Bytes * 3L);
        if (drive.AvailableFreeSpace < minimumFree)
            throw new InvalidOperationException($"Not enough free space. Available: {FormatBytes(drive.AvailableFreeSpace)}; required safety minimum: {FormatBytes(minimumFree)}.");

        var fixtureCache = Environment.GetEnvironmentVariable("PHETZY_UPDATER_FIXTURE_CACHE");
        var cacheRoot = Environment.GetEnvironmentVariable("PHETZY_UPDATER_FIXTURE_MODE") == "1" && !string.IsNullOrWhiteSpace(fixtureCache)
            ? Path.GetFullPath(fixtureCache)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhetzySptUpdater", "downloads");
        Directory.CreateDirectory(cacheRoot);
        var bundlePath = Path.Combine(cacheRoot, Path.GetFileName(manifest.Bundle.FileName));
        DownloadBundle(client, bundleUri, bundlePath, manifest.Bundle.Bytes, progress);

        progress.Report(new(76, "Verifying full bundle SHA-256…", null));
        var downloadedHash = HashFileWithProgress(bundlePath, 76, 84, progress);
        if (!downloadedHash.Equals(manifest.Bundle.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(bundlePath);
            throw new InvalidOperationException($"Downloaded bundle failed SHA-256 verification: {downloadedHash}");
        }

        var extractRoot = Path.Combine(cacheRoot, "extracted-" + Guid.NewGuid().ToString("N"));
        try
        {
            progress.Report(new(85, "Extracting verified installer…", null));
            ZipFile.ExtractToDirectory(bundlePath, extractRoot);
            var installer = Path.Combine(extractRoot, "Install-SPT413-ModPack.ps1");
            if (!File.Exists(installer))
                throw new InvalidOperationException("The verified bundle does not contain its full-pack installer.");

            progress.Report(new(88, "Installing 60 verified mod archives…", null));
            RunPackInstaller(installer, root, progress);

            var receipt = Path.Combine(root, "SPT_Runtime", "user", "SPT413-ModPack-Receipt.json");
            if (!File.Exists(receipt))
                throw new InvalidOperationException("The full-pack installer did not create its installation receipt.");
            using var receiptDocument = JsonDocument.Parse(File.ReadAllText(receipt));
            if (!receiptDocument.RootElement.TryGetProperty("ArchiveCount", out var count) || count.GetInt32() != 60)
                throw new InvalidOperationException("The installed full-pack receipt does not contain 60 archives.");
        }
        finally
        {
            if (Directory.Exists(extractRoot)) Directory.Delete(extractRoot, recursive: true);
        }

        File.Delete(bundlePath);
        progress.Report(new(100, "Full mod-pack installation complete.", "The verified download cache was removed."));
        return "The complete SPT 4.1.3 mod pack was downloaded, verified, and installed. Configure machine-specific Fika networking before launch.";
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string ValidateSptRoot(string selectedPath, bool requireItemPreview)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
            throw new InvalidOperationException("Select an SPT installation folder first.");
        var root = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var driveRoot = Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(root, driveRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A drive root cannot be used as the SPT installation folder.");
        if (root.Equals(@"C:\SPT_NEW", StringComparison.OrdinalIgnoreCase) || root.StartsWith(@"C:\SPT_NEW\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(@"C:\SPT_NEW is protected and will not be modified.");

        var required = new List<string>
        {
            Path.Combine(root, "EscapeFromTarkov.exe"),
            Path.Combine(root, "SPT_Runtime", "SPT.Server.exe"),
            Path.Combine(root, "SPT_Runtime", "SPT.Launcher.exe")
        };
        if (requireItemPreview)
            required.Add(Path.Combine(root, "BepInEx", "plugins", "JBOBYH", "JBOBYH_ItemPreviewQoL.dll"));
        foreach (var path in required)
            if (!File.Exists(path)) throw new FileNotFoundException("Required SPT file is missing.", path);

        var eftVersion = FileVersionInfo.GetVersionInfo(required[0]).FileVersion;
        var sptVersion = FileVersionInfo.GetVersionInfo(required[1]).FileVersion;
        if (eftVersion != ExpectedEftVersion)
            throw new InvalidOperationException($"Wrong EFT build: found {eftVersion ?? "unknown"}; required {ExpectedEftVersion}.");
        if (sptVersion != ExpectedSptVersion)
            throw new InvalidOperationException($"Wrong SPT version: found {sptVersion ?? "unknown"}; required {ExpectedSptVersion}.");
        return root;
    }

    private static void AssertFreshInstallation(string root)
    {
        var serverMods = Path.Combine(root, "SPT_Runtime", "user", "mods");
        if (Directory.Exists(serverMods) && Directory.EnumerateFileSystemEntries(serverMods).Any())
            throw new InvalidOperationException("Existing server mods were found. Use a fresh SPT 4.1.3 installation.");

        var allowedPluginRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "spt" };
        var plugins = Path.Combine(root, "BepInEx", "plugins");
        if (Directory.Exists(plugins))
        {
            var thirdParty = Directory.EnumerateFileSystemEntries(plugins)
                .Where(path => !allowedPluginRoots.Contains(Path.GetFileName(path))).ToArray();
            if (thirdParty.Length > 0)
                throw new InvalidOperationException("Existing third-party BepInEx plugins were found. Use a fresh SPT 4.1.3 installation.");
        }

        var patchers = Path.Combine(root, "BepInEx", "patchers");
        if (Directory.Exists(patchers) && Directory.EnumerateFileSystemEntries(patchers).Any())
            throw new InvalidOperationException("Existing third-party BepInEx patchers were found. Use a fresh SPT 4.1.3 installation.");
    }

    private static void DownloadBundle(HttpClient client, Uri uri, string path, long expectedBytes, IProgress<UpdateProgress> progress)
    {
        var existing = File.Exists(path) ? new FileInfo(path).Length : 0L;
        if (existing > expectedBytes) { File.Delete(path); existing = 0; }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
        using var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
        if (existing > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            File.Delete(path);
            DownloadBundle(client, uri, path, expectedBytes, progress);
            return;
        }
        response.EnsureSuccessStatusCode();

        using var input = response.Content.ReadAsStream();
        using var output = new FileStream(path, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024);
        var buffer = new byte[4 * 1024 * 1024];
        var downloaded = existing;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
            downloaded += read;
            var percent = 10 + (int)Math.Min(65, downloaded * 65L / expectedBytes);
            progress.Report(new(percent, $"Downloading private pack — {FormatBytes(downloaded)} / {FormatBytes(expectedBytes)}", null));
        }
        if (downloaded != expectedBytes)
            throw new InvalidOperationException($"Download size mismatch: received {downloaded}; expected {expectedBytes} bytes.");
    }

    private static string HashFileWithProgress(string path, int start, int end, IProgress<UpdateProgress> progress)
    {
        using var input = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[4 * 1024 * 1024];
        long processed = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            processed += read;
            var percent = start + (int)((end - start) * processed / input.Length);
            progress.Report(new(percent, $"Verifying download — {FormatBytes(processed)} / {FormatBytes(input.Length)}", null));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void RunPackInstaller(string installer, string root, IProgress<UpdateProgress> progress)
    {
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(installer);
        start.ArgumentList.Add("-InstallPath");
        start.ArgumentList.Add(root);

        using var process = new Process { StartInfo = start };
        var installed = 0;
        var errors = new List<string>();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            if (e.Data.StartsWith("Installing ", StringComparison.Ordinal)) installed++;
            var percent = 88 + Math.Min(10, installed * 10 / 60);
            progress.Report(new(percent, $"Installing verified archives — {installed} / 60", e.Data));
        };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (errors) errors.Add(e.Data); };
        if (!process.Start()) throw new InvalidOperationException("Could not start the verified full-pack installer.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Full-pack installer failed: " + string.Join(Environment.NewLine, errors.TakeLast(12)));
    }

    private static bool TryHttpsUri(string? value, out Uri uri)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out uri!)) return false;
        if (uri.Scheme == Uri.UriSchemeHttps) return true;
        return Environment.GetEnvironmentVariable("PHETZY_UPDATER_FIXTURE_MODE") == "1" &&
               uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.0} {units[unit]}";
    }

    private static List<string> FindProcessesWithin(string root)
    {
        var matches = new List<string>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (path is not null && path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    matches.Add($"{process.ProcessName} ({process.Id})");
            }
            catch
            {
                // Protected system processes are outside the selected SPT folder.
            }
            finally
            {
                process.Dispose();
            }
        }
        return matches;
    }

    private static MemoryStream OpenPayload()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidOperationException("Embedded update payload is missing.");
        var copy = new MemoryStream();
        resource.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return HashStream(stream);
    }

    private static string HashStream(Stream stream)
    {
        var originalPosition = stream.CanSeek ? stream.Position : 0;
        if (stream.CanSeek) stream.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        if (stream.CanSeek) stream.Position = originalPosition;
        return hash;
    }

    internal sealed record UpdateProgress(int Percent, string Phase, string? Detail);

    private sealed record UpdaterSource(string ManifestUrl);
    private sealed record PackManifest(int SchemaVersion, string SptVersion, string EftVersion, BundleEntry Bundle);
    private sealed record BundleEntry(string FileName, string Url, long Bytes, string Sha256);
}

internal static class UpdaterSelfUpdate
{
    private const string LatestReleaseApi = "https://api.github.com/repos/phetzy/SPT413-Phetzy-Updater/releases/latest";
    private const string ReleaseAssetPrefix = "SPT413-Phetzy-Updater-GitHub-";
    private const string UpdaterFileName = "SPT413-Phetzy-Updater.exe";

    internal sealed record Release(string TagName, Version Version, string AssetName, Uri AssetUrl, Uri ChecksumUrl);

    internal static async Task<Release?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var configuredApi = Environment.GetEnvironmentVariable("PHETZY_UPDATER_RELEASE_API") ?? LatestReleaseApi;
        var apiUri = RequireUri(configuredApi, requireGitHubApi: true);
        using var client = CreateClient();
        var json = await client.GetStringAsync(apiUri, cancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tagName = root.GetProperty("tag_name").GetString()
            ?? throw new InvalidOperationException("The GitHub release does not have a tag name.");
        var versionText = tagName.StartsWith('v') ? tagName[1..] : tagName;
        if (!Version.TryParse(versionText, out var releaseVersion))
            throw new InvalidOperationException($"The GitHub release tag is not a version: {tagName}");

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        if (releaseVersion <= currentVersion) return null;

        string? assetName = null;
        string? assetUrl = null;
        string? checksumUrl = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("browser_download_url").GetString();
            if (name is null || url is null) continue;
            if (name.StartsWith(ReleaseAssetPrefix, StringComparison.Ordinal) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                assetName = name;
                assetUrl = url;
            }
        }

        if (assetName is null || assetUrl is null)
            throw new InvalidOperationException("The GitHub release does not contain the public updater ZIP.");
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (!string.Equals(name, assetName + ".sha256", StringComparison.Ordinal)) continue;
            checksumUrl = asset.GetProperty("browser_download_url").GetString();
            break;
        }
        if (checksumUrl is null)
            throw new InvalidOperationException("The GitHub release does not contain the updater SHA-256 sidecar.");

        return new Release(
            tagName,
            releaseVersion,
            assetName,
            RequireUri(assetUrl, requireGitHubApi: false),
            RequireUri(checksumUrl, requireGitHubApi: false));
    }

    internal static async Task DownloadVerifyAndLaunchAsync(Release release, IProgress<UpdaterForm.UpdateProgress> progress)
    {
        var originalUpdater = Environment.ProcessPath
            ?? throw new InvalidOperationException("The running updater path is unavailable.");
        var fixtureCache = Environment.GetEnvironmentVariable("PHETZY_UPDATER_SELF_UPDATE_CACHE");
        var cacheRoot = Environment.GetEnvironmentVariable("PHETZY_UPDATER_FIXTURE_MODE") == "1" && !string.IsNullOrWhiteSpace(fixtureCache)
            ? Path.Combine(Path.GetFullPath(fixtureCache), SanitizePathComponent(release.TagName))
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhetzySptUpdater",
                "self-update",
                SanitizePathComponent(release.TagName));
        if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, recursive: true);
        Directory.CreateDirectory(cacheRoot);

        var archivePath = Path.Combine(cacheRoot, release.AssetName);
        var checksumPath = archivePath + ".sha256";
        using var client = CreateClient();
        await DownloadAsync(client, release.ChecksumUrl, checksumPath, 5, 10, progress);
        await DownloadAsync(client, release.AssetUrl, archivePath, 10, 72, progress);

        progress.Report(new(74, "Verifying updater SHA-256â€¦", null));
        var checksumText = await File.ReadAllTextAsync(checksumPath);
        var fields = checksumText.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0 || fields[0].Length != 64 || !fields[0].All(Uri.IsHexDigit))
            throw new InvalidOperationException("The updater checksum sidecar is invalid.");
        var expectedHash = fields[0].ToUpperInvariant();
        var actualHash = HashFile(archivePath);
        if (!actualHash.Equals(expectedHash, StringComparison.Ordinal))
            throw new InvalidOperationException($"The updater download failed SHA-256 verification: {actualHash}");

        progress.Report(new(82, "Extracting verified updaterâ€¦", null));
        var extractRoot = Path.Combine(cacheRoot, "extracted");
        ZipFile.ExtractToDirectory(archivePath, extractRoot);
        var replacement = Path.Combine(extractRoot, UpdaterFileName);
        if (!File.Exists(replacement))
            throw new InvalidOperationException("The verified updater archive does not contain its EXE.");

        progress.Report(new(92, "Starting verified updater replacementâ€¦", null));
        var start = new ProcessStartInfo
        {
            FileName = replacement,
            UseShellExecute = false,
            WorkingDirectory = extractRoot
        };
        start.ArgumentList.Add("--replace-updater");
        start.ArgumentList.Add(originalUpdater);
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        if (Process.Start(start) is null)
            throw new InvalidOperationException("Could not start the verified updater replacement.");
        progress.Report(new(100, "Verified updater is ready to replace this version.", release.TagName));
    }

    internal static int ReplaceRunningUpdater(string targetPath, int previousProcessId)
    {
        var target = Path.GetFullPath(targetPath);
        if (!Path.GetFileName(target).Equals(UpdaterFileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Updater replacement target has an unexpected filename.");
        if (!File.Exists(target))
            throw new FileNotFoundException("Updater replacement target is missing.", target);

        try
        {
            using var previous = Process.GetProcessById(previousProcessId);
            previous.WaitForExit(30_000);
            if (!previous.HasExited)
                throw new InvalidOperationException("The previous updater did not exit within 30 seconds.");
        }
        catch (ArgumentException)
        {
            // The previous process exited before the replacement helper opened it.
        }

        var replacement = Environment.ProcessPath
            ?? throw new InvalidOperationException("The replacement updater path is unavailable.");
        File.Copy(replacement, target, overwrite: true);
        if (!HashFile(replacement).Equals(HashFile(target), StringComparison.Ordinal))
            throw new InvalidOperationException("The replaced updater failed SHA-256 verification.");

        if (Environment.GetEnvironmentVariable("PHETZY_UPDATER_FIXTURE_MODE") == "1") return 0;
        var restart = new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(target)!
        };
        if (Process.Start(restart) is null)
            throw new InvalidOperationException("The updated updater could not be restarted.");
        return 0;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SPT413-Phetzy-Updater/1.0");
        return client;
    }

    private static async Task DownloadAsync(
        HttpClient client,
        Uri uri,
        string destination,
        int startPercent,
        int endPercent,
        IProgress<UpdaterForm.UpdateProgress> progress)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var expectedBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);
        var buffer = new byte[1024 * 1024];
        long received = 0;
        int read;
        while ((read = await input.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read));
            received += read;
            var percent = expectedBytes is > 0
                ? startPercent + (int)((endPercent - startPercent) * received / expectedBytes.Value)
                : startPercent;
            progress.Report(new(Math.Clamp(percent, startPercent, endPercent), "Downloading updater updateâ€¦", $"{received:N0} bytes"));
        }
        if (expectedBytes is > 0 && received != expectedBytes.Value)
            throw new InvalidOperationException($"Updater download size mismatch: received {received}; expected {expectedBytes.Value} bytes.");
    }

    private static Uri RequireUri(string value, bool requireGitHubApi)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Updater release URL is invalid.");
        var fixture = Environment.GetEnvironmentVariable("PHETZY_UPDATER_FIXTURE_MODE") == "1";
        if (fixture && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback) return uri;
        if (uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Updater release URL must use HTTPS.");
        var expectedHost = requireGitHubApi ? "api.github.com" : "github.com";
        if (!uri.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Updater release URL must use {expectedHost}.");
        return uri;
    }

    private static string SanitizePathComponent(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
