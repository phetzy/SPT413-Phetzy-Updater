using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Phetzy.Spt413Updater.CrossPlatform;

internal sealed class PackInstallEngine
{
    internal const string ExpectedEftVersion = "0.16.9.40743";
    internal const string ExpectedSptVersion = "4.1.3";
    internal const string ExpectedEftSha256 =
        "679486C18B89092F9C692FEA34AB4841923F768418C5E8302FD93B14DB4A41E5";
    internal const int ExpectedArchiveCount = 60;
    internal const string SourceResource = "Phetzy.Spt413Updater.PrivatePack.updater-source.json";
    internal const string HotfixResource = "Phetzy.Spt413Updater.Payload.JBOBYH_ItemPreviewQoL.dll";
    internal const string NewHotfixHash = "C20A912CE1A83DBC260D8A843A64AC8A0F03B26FCD9E43A24CE8670C0B8A17E2";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly Dictionary<string, string> HotfixHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BepInEx/plugins/QuestsExtended/QuestsExtended.dll"] = "9AE453412D1EBBBE8ECC87AE03569E58EB83BAA32EC84104337732FD9A1B3EA1",
        ["BepInEx/plugins/HealingAutoCancel.dll"] = "9F16929F68B0E362F781DC4E8922EA394B1523EA1C8BDAD80728A1A6DE9CF716",
        ["BepInEx/plugins/WTT-ClientCommonLib/WTT-ClientCommonLib.dll"] = "C53E96FAFADA25B81D8F324B3400479BDFEC9C739EA8A2252F457718569FE94E",
        ["BepInEx/plugins/TTC/TTC.dll"] = "C3E73F0920C87B99DB53698B69676A70891EFBC8F965E9456262DB9ECEA14762",
        ["SPT_Runtime/user/mods/TTC/TTC.Mod.dll"] = "8C6D34D188186B55C5A83C27F6DF8CBC86ACA93BE73C7E2F923F147B1CDE702E",
        ["BepInEx/plugins/WTT-ArmoryClient/WTT-ArmoryClient.dll"] = "BB4B597D39AB77180A3A343719A4EA2A8953B958253C0D795707D555CF292180",
        ["SPT_Runtime/user/mods/WTT-Armory/WTT-Armory.dll"] = "494FB829C46AB9D69A11FE5B52DBF6FF4562846AF4A0CA9848046C6832A0D34C",
        ["BepInEx/plugins/MoreCheckmarks/MoreCheckmarks.dll"] = "1B0DF7D94930192657A7EADEFD74CB1C993E533A2AB3B3A27D815E017683C8F4",
        ["SPT_Runtime/user/mods/MoreCheckmarksBackend/MoreCheckmarksBackend.dll"] = "0712F92B1C7F9E163A358C4145937927C9B48364B459563ECD72467ED063B952",
        ["BepInEx/plugins/JBOBYH/JBOBYH_ItemPreviewQoL.dll"] = NewHotfixHash
    };

    private static readonly string[] Sentinels =
    [
        "BepInEx/plugins/DrakiaXYZ-BigBrain.dll",
        "BepInEx/plugins/DrakiaXYZ-Waypoints/DrakiaXYZ-Waypoints.dll",
        "BepInEx/plugins/RaiRai.ColorConverterAPI.dll",
        "SPT_Runtime/user/mods/WTT-ServerCommonLib/WTT-ServerCommonLib.dll",
        "BepInEx/plugins/Fika/Fika.Core.dll",
        "SPT_Runtime/user/mods/fika-server/FikaServer.dll",
        "BepInEx/plugins/SAIN",
        "BepInEx/plugins/Tyfon.UIFixes.dll",
        "SPT_Runtime/user/mods/Tyfon.HideoutInProgress.Server/Tyfon.HideoutInProgress.Server.dll",
        "SPT_Runtime/user/mods/[SVM] Server Value Modifier/ServerValueModifier.dll",
        "SPT_Runtime/user/mods/acidphantasm-botplacementsystem",
        "SPT_Runtime/user/mods/acidphantasm-enablelabyrinth",
        "BepInEx/plugins/acidphantasm-moretagcolours/acidphantasm-moretagcolours.dll",
        "SPT_Runtime/user/mods/acidphantasm-progressivebotsystem",
        "SPT_Runtime/user/mods/acidphantasm-reffriendlyquests",
        "SPT_Runtime/user/mods/acidphantasm-bosseshavelegamedals/acidphantasm-bosseshavelegamedals.dll",
        "BepInEx/plugins/Kat.BetterAmmoLoadingList.dll",
        "SPT_Runtime/user/mods/bushtail-CantedAiming",
        "BepInEx/plugins/ContinuousHealing",
        "BepInEx/plugins/DrakiaXYZ-EquipFromWeaponRack.dll",
        "BepInEx/plugins/DrakiaXYZ-QuickMoveToContainer.dll",
        "BepInEx/plugins/DrakiaXYZ-SearchOpenContainers.dll",
        "BepInEx/plugins/DrakiaXYZ-TaskListFixes.dll",
        "SPT_Runtime/user/mods/DrakiaXYZ-LiveFleaPrices/DrakiaXYZ-LiveFleaPrices.dll",
        "BepInEx/plugins/mpstark-dynamicmaps",
        "BepInEx/plugins/HandsAreNotBusy.dll",
        "BepInEx/plugins/Kaeno-TraderScrolling.dll",
        "SPT_Runtime/user/mods/LacyPvETweaks",
        "SPT_Runtime/user/mods/MergeConsumablesServer",
        "BepInEx/plugins/ozen.ContinuousLoadAmmo",
        "SPT_Runtime/user/mods/ozen.Foldables",
        "BepInEx/plugins/QuickSellFlea",
        "SPT_Runtime/user/mods/WTT-HeadVoiceSelector",
        "BepInEx/plugins/inory-agonysfx.dll",
        "BepInEx/plugins/IcyClawz/IcyClawz.CustomInteractions.dll",
        "BepInEx/plugins/CWX/CWX_MegaMod.dll",
        "SPT_Runtime/user/mods/DiscipleBallisticCasePlus/DiscipleBallisticCasePlus.dll",
        "SPT_Runtime/user/mods/EternalInsurance/EternalInsurance.dll",
        "SPT_Runtime/user/mods/FleaAdjustment/FleaAdjustment.dll",
        "BepInEx/plugins/HealingAutoCancel.dll",
        "BepInEx/plugins/IcyClawz/IcyClawz.ItemAttributeFix.dll",
        "BepInEx/plugins/IcyClawz/IcyClawz.ItemContextMenuExt.dll",
        "BepInEx/plugins/JBOBYH/JBOBYH_ItemPreviewQoL.dll",
        "BepInEx/plugins/IcyClawz/IcyClawz.ItemSellPrice.dll",
        "BepInEx/plugins/OrionsMods/LevelEnduranceAndStrength.dll",
        "SPT_Runtime/user/mods/WTT-TheLongLostHeadsOfYojenkz/WTT-TheLongLostHeadsOfYojenkz.dll",
        "BepInEx/plugins/IcyClawz/IcyClawz.MagazineInspector.dll",
        "SPT_Runtime/user/mods/MoreBotsServer/MoreBotsServer.dll",
        "BepInEx/plugins/MoreCheckmarks/MoreCheckmarks.dll",
        "SPT_Runtime/user/mods/MoreCheckmarksBackend/MoreCheckmarksBackend.dll",
        "SPT_Runtime/user/mods/TEP300Headset/TEP300Headset.dll",
        "SPT_Runtime/user/mods/QEServerPart/QEServerPart.dll",
        "SPT_Runtime/user/mods/RUAFComeHomeServer/RUAFComeHomeServer.dll",
        "SPT_Runtime/user/mods/yellowdoge-tarkovrarecollectibles",
        "SPT_Runtime/user/mods/TTC/TTC.Mod.dll",
        "BepInEx/plugins/UnityToolkit/UnityToolkit.dll",
        "SPT_Runtime/user/mods/untargh-server",
        "BepInEx/plugins/SPTVRAMCleaner.dll",
        "SPT_Runtime/user/mods/WTT-Armory/WTT-Armory.dll",
        "SPT_Runtime/user/mods/WTT-Artem/WTT-Artem.dll",
        "SPT_Runtime/user/mods/WTT-ContentBackport/WTT-ContentBackport.dll"
    ];

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromHours(8) };

    internal static void ValidateEmbeddedSource()
    {
        _ = LoadSource(required: true);
    }

    internal async Task<string> CheckPrivateChannelAsync(CancellationToken cancellationToken = default)
    {
        var source = LoadSource(required: true);
        if (!Uri.TryCreate(source.ManifestUrl, UriKind.Absolute, out var manifestUri) ||
            manifestUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The embedded manifest URL is not valid HTTPS.");

        var manifestJson = await _httpClient.GetStringAsync(manifestUri, cancellationToken);
        var manifest = JsonSerializer.Deserialize<PackManifest>(manifestJson, JsonOptions)
                       ?? throw new InvalidOperationException("The private pack manifest is invalid.");
        ValidateManifest(manifest);

        if (!Uri.TryCreate(manifest.Bundle.Url, UriKind.Absolute, out var bundleUri) ||
            bundleUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The private bundle URL is not valid HTTPS.");

        using var request = new HttpRequestMessage(HttpMethod.Get, bundleUri);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var remoteBytes = response.Content.Headers.ContentRange?.Length ??
                          response.Content.Headers.ContentLength;
        if (remoteBytes is not null && remoteBytes.Value != manifest.Bundle.Bytes)
            throw new InvalidOperationException(
                $"The private bundle size does not match its manifest: {remoteBytes.Value} != {manifest.Bundle.Bytes}.");

        return "PRIVATE CHANNEL VALID";
    }

    internal async Task<string> InstallFromChannelAsync(string selectedPath, IProgress<InstallProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateTarget(selectedPath, requireFresh: true);
        var source = LoadSource(required: true);
        if (!Uri.TryCreate(source.ManifestUrl, UriKind.Absolute, out var manifestUri) ||
            manifestUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The embedded manifest URL is not valid HTTPS.");

        progress.Report(new(3, "Downloading signed manifest", manifestUri.Host));
        var manifestJson = await _httpClient.GetStringAsync(manifestUri, cancellationToken);
        var manifest = JsonSerializer.Deserialize<PackManifest>(manifestJson, JsonOptions)
                       ?? throw new InvalidOperationException("The pack manifest is invalid.");
        ValidateManifest(manifest);

        var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhetzySptUpdater", "cache");
        Directory.CreateDirectory(cacheRoot);
        var bundlePath = Path.Combine(cacheRoot, Path.GetFileName(manifest.Bundle.FileName));

        try
        {
            await DownloadBundleAsync(manifest.Bundle, bundlePath, progress, cancellationToken);
            return await InstallFromBundleAsync(root, bundlePath, progress, cancellationToken);
        }
        finally
        {
            TryDelete(bundlePath);
        }
    }

    internal async Task<string> InstallFromBundleAsync(string selectedPath, string bundlePath,
        IProgress<InstallProgress> progress, CancellationToken cancellationToken = default)
    {
        var root = ValidateTarget(selectedPath, requireFresh: true);
        if (!File.Exists(bundlePath)) throw new FileNotFoundException("The mod-pack bundle is missing.", bundlePath);

        progress.Report(new(5, "Opening verified mod-pack bundle", Path.GetFileName(bundlePath)));
        using var outer = ZipFile.OpenRead(bundlePath);
        var entries = outer.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .ToDictionary(entry => NormalizeArchivePath(entry.FullName), StringComparer.OrdinalIgnoreCase);

        var order = ReadLines(entries, "INSTALL_ORDER.txt");
        var expected = ReadHashManifest(entries, "ARCHIVES_SHA256SUMS.txt");
        ValidateBundleIndex(order, expected, entries);

        for (var index = 0; index < order.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = NormalizeArchivePath(order[index]);
            var actual = await HashEntryAsync(entries[relative], cancellationToken);
            if (!actual.Equals(expected[relative], StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Archive checksum mismatch: {relative}");
            progress.Report(new(5 + (index + 1) * 25 / order.Count,
                $"Verifying bundled archives — {index + 1} / {order.Count}", relative));
        }

        var scratch = Path.Combine(Path.GetTempPath(), $"phetzy-spt-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scratch);
        try
        {
            for (var index = 0; index < order.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = NormalizeArchivePath(order[index]);
                var tempArchive = Path.Combine(scratch, $"{index:D2}-{Path.GetFileName(relative)}");
                await CopyEntryAsync(entries[relative], tempArchive, cancellationToken);
                ExtractModArchive(tempArchive, root);
                TryDelete(tempArchive);
                progress.Report(new(30 + (index + 1) * 58 / order.Count,
                    $"Installing verified archives — {index + 1} / {order.Count}", relative));
            }

            CopyBundledSettings(entries, root);
            progress.Report(new(91, "Auditing installed files", null));
            AuditInstall(root);
            WriteReceipt(root);
            progress.Report(new(100, "Installation complete", root));
            return "The complete SPT 4.1.3 mod pack was verified and installed.";
        }
        finally
        {
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
        }
    }

    internal string ApplyHotfix(string selectedPath, IProgress<InstallProgress> progress)
    {
        var root = ValidateTarget(selectedPath, requireFresh: false);
        var assembly = Assembly.GetExecutingAssembly();
        using var payload = assembly.GetManifestResourceStream(HotfixResource)
                            ?? throw new InvalidOperationException("This build does not contain the hotfix payload.");
        using var memory = new MemoryStream();
        payload.CopyTo(memory);
        var bytes = memory.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!hash.Equals(NewHotfixHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The embedded hotfix checksum is invalid.");

        var destination = CombineRoot(root, "BepInEx/plugins/JBOBYH/JBOBYH_ItemPreviewQoL.dll");
        if (!File.Exists(destination))
            throw new InvalidOperationException("Item Preview QoL is not installed in the selected SPT folder.");
        var backup = $"{destination}.bak-{DateTime.UtcNow:yyyyMMddHHmmss}";
        File.Copy(destination, backup, false);
        File.WriteAllBytes(destination, bytes);
        progress.Report(new(100, "Hotfix installed", destination));
        return $"Item Preview QoL was updated. Backup: {backup}";
    }

    internal static string ValidateTarget(string selectedPath, bool requireFresh)
    {
        if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
            throw new InvalidOperationException("Select an existing SPT installation folder.");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedPath));
        var fileSystemRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root) ?? "");
        if (root.Equals(fileSystemRoot, PathComparison()))
            throw new InvalidOperationException("A filesystem root cannot be used as the SPT folder.");
        if (OperatingSystem.IsWindows() && root.Equals(@"C:\SPT_NEW", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(@"C:\SPT_NEW is protected and cannot be modified.");

        var eft = CombineRoot(root, "EscapeFromTarkov.exe");
        var bepinex = CombineRoot(root, "BepInEx");
        var serverAssembly = OperatingSystem.IsLinux()
            ? CombineRoot(root, "SPT_Runtime/SPT.Server.Linux.dll")
            : CombineRoot(root, "SPT_Runtime/SPT.Server.exe");
        var launcher = OperatingSystem.IsLinux()
            ? CombineRoot(root, "SPT_Runtime/SPT.Launcher.Linux")
            : CombineRoot(root, "SPT_Runtime/SPT.Launcher.exe");
        foreach (var required in new[] { eft, bepinex, serverAssembly, launcher })
        {
            if (!File.Exists(required) && !Directory.Exists(required))
                throw new InvalidOperationException($"The selected folder is not a combined SPT install; missing {required}");
        }

        var eftVersion = FileVersionInfo.GetVersionInfo(eft).FileVersion;
        var serverVersion = FileVersionInfo.GetVersionInfo(serverAssembly).FileVersion;
        var eftSha256 = string.IsNullOrWhiteSpace(eftVersion)
            ? Sha256File(eft)
            : null;
        ValidateBuildVersions(eftVersion, serverVersion, eftSha256);

        AssertNoRunningProcesses(root);
        if (requireFresh) AssertFresh(root);
        return root;
    }

    internal static void ValidateBuildVersions(string? eftVersion, string? serverVersion, string? eftSha256 = null)
    {
        var exactEftVersion = string.Equals(eftVersion, ExpectedEftVersion, StringComparison.Ordinal);
        var exactEftHashWhenVersionUnavailable = string.IsNullOrWhiteSpace(eftVersion) &&
            string.Equals(eftSha256, ExpectedEftSha256, StringComparison.OrdinalIgnoreCase);
        if ((!exactEftVersion && !exactEftHashWhenVersionUnavailable) ||
            !string.Equals(serverVersion, ExpectedSptVersion, StringComparison.Ordinal))
        {
            var eftDescription = string.IsNullOrWhiteSpace(eftVersion)
                ? $"unknown (SHA-256 {eftSha256 ?? "unavailable"})"
                : eftVersion;
            throw new InvalidOperationException(
                $"Wrong build. Required SPT {ExpectedSptVersion} / EFT {ExpectedEftVersion}; " +
                $"found SPT {serverVersion ?? "unknown"} / EFT {eftDescription}.");
        }
    }

    private static void AssertFresh(string root)
    {
        var mods = CombineRoot(root, "SPT_Runtime/user/mods");
        if (Directory.Exists(mods) && Directory.EnumerateFileSystemEntries(mods).Any())
            throw new InvalidOperationException("Existing server mods were found. Use a fresh SPT 4.1.3 install.");

        var plugins = CombineRoot(root, "BepInEx/plugins");
        if (Directory.Exists(plugins) && Directory.EnumerateFileSystemEntries(plugins)
                .Any(path => !Path.GetFileName(path).Equals("spt", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Existing third-party BepInEx plugins were found.");

        var patchers = CombineRoot(root, "BepInEx/patchers");
        var allowed = new HashSet<string>(["spt-prepatch.dll", "FixPluginTypesSerialization.dll"],
            StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(patchers) && Directory.EnumerateFileSystemEntries(patchers)
                .Any(path => !allowed.Contains(Path.GetFileName(path))))
            throw new InvalidOperationException("Existing third-party BepInEx patchers were found.");
    }

    private static void AssertNoRunningProcesses(string root)
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                string? path;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch
                {
                    // The process exited, or belongs to a user whose executable path is inaccessible.
                    continue;
                }

                if (path is not null && IsWithin(path, root))
                    throw new InvalidOperationException($"Close {process.ProcessName} before modifying this SPT folder.");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private async Task DownloadBundleAsync(BundleEntry bundle, string outputPath, IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(bundle.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The bundle URL is not valid HTTPS.");
        var tempPath = $"{outputPath}.{Guid.NewGuid():N}.part";
        TryDelete(tempPath);
        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                1024 * 1024, true);
            var buffer = new byte[1024 * 1024];
            long written = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                written += read;
                var percent = bundle.Bytes == 0 ? 0 : (int)Math.Min(100, written * 100 / bundle.Bytes);
                progress.Report(new(4 + percent * 20 / 100, $"Downloading mod pack — {percent}%",
                    $"{written:N0} / {bundle.Bytes:N0} bytes"));
            }

            await output.FlushAsync(cancellationToken);
            if (written != bundle.Bytes)
                throw new InvalidOperationException($"Downloaded size mismatch: {written} != {bundle.Bytes}");
            var hash = await Sha256FileAsync(tempPath, cancellationToken);
            if (!hash.Equals(bundle.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Downloaded mod-pack checksum mismatch.");
            File.Move(tempPath, outputPath, true);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void ValidateManifest(PackManifest manifest)
    {
        if (manifest.SchemaVersion != 1 || manifest.Bundle is null)
            throw new InvalidOperationException("Unsupported pack-manifest schema.");
        if (manifest.SptVersion != ExpectedSptVersion || manifest.EftVersion != ExpectedEftVersion)
            throw new InvalidOperationException("The pack manifest targets a different SPT/EFT version.");
        if (manifest.Bundle.Bytes <= 0 || manifest.Bundle.Sha256.Length != 64 ||
            string.IsNullOrWhiteSpace(manifest.Bundle.FileName))
            throw new InvalidOperationException("The pack-manifest bundle fields are incomplete.");
    }

    private static void ValidateBundleIndex(List<string> order, Dictionary<string, string> expected,
        Dictionary<string, ZipArchiveEntry> entries)
    {
        if (order.Count != ExpectedArchiveCount || order.Distinct(StringComparer.OrdinalIgnoreCase).Count() != order.Count)
            throw new InvalidOperationException($"Expected {ExpectedArchiveCount} unique install-order entries.");
        if (expected.Count != ExpectedArchiveCount)
            throw new InvalidOperationException($"Expected {ExpectedArchiveCount} archive checksums.");
        foreach (var item in order.Select(NormalizeArchivePath))
        {
            if (!item.StartsWith("archives/", StringComparison.OrdinalIgnoreCase) || item.Contains("../"))
                throw new InvalidOperationException($"Unsafe archive path in install order: {item}");
            if (!expected.ContainsKey(item)) throw new InvalidOperationException($"Missing checksum for {item}");
            if (!entries.ContainsKey(item)) throw new InvalidOperationException($"Bundle archive is missing: {item}");
        }
    }

    internal static void ExtractModArchive(string archivePath, string root)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            var key = NormalizeArchivePath(entry.Key ?? "");
            if (string.IsNullOrWhiteSpace(key)) continue;
            var destination = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(rootWithSeparator, PathComparison()))
                throw new InvalidOperationException($"Mod archive entry escapes the SPT folder: {entry.Key}");
            if (!destinations.Add(destination))
                throw new InvalidOperationException($"Mod archive contains a case-colliding path: {entry.Key}");
            if (IsSymbolicLink(entry))
                throw new InvalidOperationException($"Mod archive contains a symbolic link: {entry.Key}");
        }

        archive.WriteToDirectory(root, new ExtractionOptions
        {
            ExtractFullPath = true,
            Overwrite = true,
            CheckCrc = true,
            SymbolicLinkHandler = (_, _) => throw new InvalidOperationException("Symbolic links are not allowed.")
        });
    }

    private static void CopyBundledSettings(Dictionary<string, ZipArchiveEntry> entries, string root)
    {
        foreach (var relative in new[]
                 {
                     "SPT_Runtime/user/mods/[SVM] Server Value Modifier/Presets/LadsGOAGANE.json",
                     "SPT_Runtime/user/mods/[SVM] Server Value Modifier/Loader/loader.json"
                 })
        {
            var sourceKey = NormalizeArchivePath($"settings/{relative}");
            if (!entries.TryGetValue(sourceKey, out var entry))
                throw new InvalidOperationException($"Bundled setting is missing: {relative}");
            var destination = CombineRoot(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static void AuditInstall(string root)
    {
        var missing = Sentinels.Where(path => !File.Exists(CombineRoot(root, path)) &&
                                              !Directory.Exists(CombineRoot(root, path))).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Installed mod outputs are missing: {string.Join(", ", missing)}");

        foreach (var (relative, expected) in HotfixHashes)
        {
            var path = CombineRoot(root, relative);
            if (!File.Exists(path)) throw new InvalidOperationException($"Hotfix output is missing: {relative}");
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Hotfix checksum mismatch: {relative}");
        }

        var presetPath = CombineRoot(root,
            "SPT_Runtime/user/mods/[SVM] Server Value Modifier/Presets/LadsGOAGANE.json");
        var loaderPath = CombineRoot(root,
            "SPT_Runtime/user/mods/[SVM] Server Value Modifier/Loader/loader.json");
        using var preset = JsonDocument.Parse(File.ReadAllText(presetPath));
        using var loader = JsonDocument.Parse(File.ReadAllText(loaderPath));
        if (!preset.RootElement.GetProperty("Hideout").GetProperty("EnableHideout").GetBoolean() ||
            !preset.RootElement.GetProperty("Hideout").GetProperty("RemoveConstructionsFIRRequirements").GetBoolean())
            throw new InvalidOperationException("LadsGOAGANE does not remove hideout FIR requirements.");
        if (loader.RootElement.GetProperty("CurrentlySelectedPreset").GetString() != "LadsGOAGANE")
            throw new InvalidOperationException("Greed does not select LadsGOAGANE.");
    }

    private static void WriteReceipt(string root)
    {
        var receipt = new
        {
            Bundle = "SPT413-Full-Mod-Pack-spt413-phetzy.1",
            InstalledUtc = DateTime.UtcNow.ToString("O"),
            Target = root,
            SptServerVersion = ExpectedSptVersion,
            EftVersion = ExpectedEftVersion,
            ArchiveCount = ExpectedArchiveCount,
            InstallerPlatform = OperatingSystem.IsLinux() ? "linux-x64" : "win-x64"
        };
        var path = CombineRoot(root, "SPT_Runtime/user/SPT413-ModPack-Receipt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(receipt, JsonOptions));
    }

    private static UpdaterSource LoadSource(bool required)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var embedded = assembly.GetManifestResourceStream(SourceResource);
        if (embedded is not null)
        {
            var source = JsonSerializer.Deserialize<UpdaterSource>(embedded, JsonOptions);
            if (source is not null) return source;
        }

        var adjacent = Path.Combine(AppContext.BaseDirectory, "updater-source.json");
        if (File.Exists(adjacent))
        {
            return JsonSerializer.Deserialize<UpdaterSource>(File.ReadAllText(adjacent), JsonOptions)
                   ?? throw new InvalidOperationException("updater-source.json is invalid.");
        }

        if (required) throw new InvalidOperationException("This build does not contain private pack access.");
        return new UpdaterSource("");
    }

    private static Dictionary<string, string> ReadHashManifest(Dictionary<string, ZipArchiveEntry> entries, string key)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in ReadLines(entries, key))
        {
            var match = Regex.Match(line, "^([A-Fa-f0-9]{64})  (.+)$");
            if (!match.Success) throw new InvalidOperationException($"Malformed archive checksum line: {line}");
            result[NormalizeArchivePath(match.Groups[2].Value)] = match.Groups[1].Value.ToUpperInvariant();
        }
        return result;
    }

    private static List<string> ReadLines(Dictionary<string, ZipArchiveEntry> entries, string key)
    {
        if (!entries.TryGetValue(NormalizeArchivePath(key), out var entry))
            throw new InvalidOperationException($"Bundle file is missing: {key}");
        using var reader = new StreamReader(entry.Open());
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line.Trim());
        return lines;
    }

    private static async Task<string> HashEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var input = entry.Open();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            hash.AppendData(buffer, 0, read);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task CopyEntryAsync(ZipArchiveEntry entry, string path, CancellationToken cancellationToken)
    {
        await using var input = entry.Open();
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            1024 * 1024, true);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static async Task<string> Sha256FileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string Sha256File(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string CombineRoot(string root, string relative) =>
        Path.Combine(root, NormalizeArchivePath(relative).Replace('/', Path.DirectorySeparatorChar));

    private static bool IsSymbolicLink(SharpCompress.Common.IEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.LinkTarget)) return true;
        if (entry.Attrib is not { } attributes) return false;

        var raw = unchecked((uint)attributes);
        var unixFileType = (raw >> 16) & 0xF000;
        const uint unixSymbolicLink = 0xA000;
        const uint windowsReparsePoint = 0x400;
        return unixFileType == unixSymbolicLink || (raw & windowsReparsePoint) != 0;
    }

    private static string NormalizeArchivePath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static bool IsWithin(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(root + Path.DirectorySeparatorChar, PathComparison());
    }

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }

    internal sealed record InstallProgress(int Percent, string Phase, string? Detail);
    private sealed record UpdaterSource(string ManifestUrl);
    private sealed record PackManifest(int SchemaVersion, string SptVersion, string EftVersion, BundleEntry Bundle);
    private sealed record BundleEntry(string FileName, string Url, long Bytes, string Sha256);
}
