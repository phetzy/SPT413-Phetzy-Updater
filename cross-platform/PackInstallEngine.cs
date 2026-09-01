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

    private readonly HttpClient _httpClient;

    internal PackInstallEngine(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromHours(8) };
    }

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

    internal async Task<IReadOnlyList<PackRelease>> GetAvailableReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        var source = LoadSource(required: true);
        if (string.IsNullOrWhiteSpace(source.ReleaseCatalogUrl)) return [];
        if (!Uri.TryCreate(source.ReleaseCatalogUrl, UriKind.Absolute, out var catalogUri) ||
            catalogUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The embedded release-catalog URL is not valid HTTPS.");

        var catalogJson = await _httpClient.GetStringAsync(catalogUri, cancellationToken);
        var catalog = JsonSerializer.Deserialize<ReleaseCatalog>(catalogJson, JsonOptions)
                      ?? throw new InvalidOperationException("The pack release catalog is invalid.");
        ValidateReleaseCatalog(catalog);
        return catalog.Releases
            .Where(release => !release.Status.Equals("withdrawn", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(release => release.ReleaseId.Equals(catalog.CurrentReleaseId,
                StringComparison.Ordinal))
            .ThenByDescending(release => release.PublishedUtc)
            .Select(release => release with
            {
                IsCurrent = release.ReleaseId.Equals(catalog.CurrentReleaseId, StringComparison.Ordinal)
            })
            .ToArray();
    }

    internal async Task<string> RestoreReleaseAsync(string selectedPath, PackRelease release,
        IProgress<InstallProgress> progress, CancellationToken cancellationToken = default)
    {
        var root = ValidateTarget(selectedPath, requireFresh: false);
        if (ReadReceiptManagedFiles(root) is null)
            throw new InvalidOperationException(
                "This installation predates rollback tracking. Run Verify Mod Pack Install on the current release " +
                "once before selecting an older release.");
        if (!Uri.TryCreate(release.ManifestUrl, UriKind.Absolute, out var manifestUri) ||
            manifestUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The selected release manifest URL is not valid HTTPS.");

        progress.Report(new(3, "Downloading selected release manifest", release.ReleaseId));
        var manifestJson = await _httpClient.GetStringAsync(manifestUri, cancellationToken);
        var manifest = JsonSerializer.Deserialize<PackManifest>(manifestJson, JsonOptions)
                       ?? throw new InvalidOperationException("The selected pack manifest is invalid.");
        ValidateManifest(manifest);
        if (!string.Equals(manifest.ReleaseId, release.ReleaseId, StringComparison.Ordinal) ||
            !string.Equals(manifest.Bundle.Sha256, release.BundleSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected release does not match its immutable catalog entry.");

        var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhetzySptUpdater", "cache");
        Directory.CreateDirectory(cacheRoot);
        var bundlePath = Path.Combine(cacheRoot,
            $"{release.ReleaseId}-{Path.GetFileName(manifest.Bundle.FileName)}");
        await DownloadBundleAsync(manifest.Bundle, bundlePath, progress, cancellationToken);
        var restoreProgress = new InlineProgress<InstallProgress>(update =>
            progress.Report(MapDownloadedBundleProgress(update)));
        var result = await VerifyAndRepairFromBundleAsync(root, bundlePath, restoreProgress, cancellationToken,
            release.ReleaseId, manifest.Bundle.Sha256);
        return $"Restored mod-pack release {release.ReleaseId}. {result}";
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
            var installProgress = new InlineProgress<InstallProgress>(update =>
                progress.Report(MapDownloadedBundleProgress(update)));
            return await InstallFromBundleAsync(root, bundlePath, installProgress, cancellationToken,
                manifest.ReleaseId, manifest.Bundle.Sha256);
        }
        finally
        {
            TryDelete(bundlePath);
        }
    }

    internal async Task<string> VerifyAndRepairFromChannelAsync(string selectedPath,
        IProgress<InstallProgress> progress, CancellationToken cancellationToken = default)
    {
        var root = ValidateTarget(selectedPath, requireFresh: false);
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
        await DownloadBundleAsync(manifest.Bundle, bundlePath, progress, cancellationToken);

        var verifyProgress = new InlineProgress<InstallProgress>(update =>
            progress.Report(MapDownloadedBundleProgress(update)));
        return await VerifyAndRepairFromBundleAsync(root, bundlePath, verifyProgress, cancellationToken,
            manifest.ReleaseId, manifest.Bundle.Sha256);
    }

    internal async Task<string> InstallFromBundleAsync(string selectedPath, string bundlePath,
        IProgress<InstallProgress> progress, CancellationToken cancellationToken = default,
        string? releaseId = null, string? bundleSha256 = null)
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
                "Verifying bundled archives", $"{index + 1} / {order.Count} — {relative}"));
        }

        var scratch = Path.Combine(Path.GetTempPath(), $"phetzy-spt-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scratch);
        var managedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            for (var index = 0; index < order.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = NormalizeArchivePath(order[index]);
                var tempArchive = Path.Combine(scratch, $"{index:D2}-{Path.GetFileName(relative)}");
                await CopyEntryAsync(entries[relative], tempArchive, cancellationToken);
                managedFiles.UnionWith(ReadManagedPathsFromArchive(tempArchive));
                ExtractModArchive(tempArchive, root);
                TryDelete(tempArchive);
                progress.Report(new(30 + (index + 1) * 58 / order.Count,
                    "Installing verified archives", $"{index + 1} / {order.Count} — {relative}"));
            }

            CopyBundledSettings(entries, root);
            managedFiles.UnionWith(BundledSettingPaths);
            progress.Report(new(91, "Auditing installed files", null));
            AuditInstall(root);
            WriteReceipt(root, releaseId, bundleSha256, managedFiles);
            progress.Report(new(100, "Installation complete", root));
            return "The complete SPT 4.1.3 mod pack was verified and installed.";
        }
        finally
        {
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
        }
    }

    internal async Task<string> VerifyAndRepairFromBundleAsync(string selectedPath, string bundlePath,
        IProgress<InstallProgress> progress, CancellationToken cancellationToken = default,
        string? releaseId = null, string? bundleSha256 = null)
    {
        var root = ValidateTarget(selectedPath, requireFresh: false);
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
                "Verifying bundled archives", $"{index + 1} / {order.Count} â€” {relative}"));
        }

        var scratch = Path.Combine(Path.GetTempPath(), $"phetzy-spt-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scratch);
        var total = new ArchiveRepairResult(0, 0, 0, 0);
        var managedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previousManagedFiles = ReadReceiptManagedFiles(root);
        try
        {
            for (var index = 0; index < order.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = NormalizeArchivePath(order[index]);
                var tempArchive = Path.Combine(scratch, $"{index:D2}-{Path.GetFileName(relative)}");
                await CopyEntryAsync(entries[relative], tempArchive, cancellationToken);
                managedFiles.UnionWith(ReadManagedPathsFromArchive(tempArchive));
                var result = RepairManagedFilesFromArchive(tempArchive, root);
                total += result;
                TryDelete(tempArchive);
                progress.Report(new(30 + (index + 1) * 60 / order.Count,
                    "Verifying installed mod files",
                    $"{index + 1} / {order.Count} â€” restored {total.Missing}, replaced {total.Replaced}, preserved {total.Preserved}"));
            }

            CopyBundledSettings(entries, root);
            managedFiles.UnionWith(BundledSettingPaths);
            var removed = previousManagedFiles is null
                ? 0
                : RemoveObsoleteManagedFiles(root, previousManagedFiles, managedFiles);
            progress.Report(new(94, "Auditing verified installation", null));
            AuditInstall(root);
            WriteReceipt(root, releaseId, bundleSha256, managedFiles);
            progress.Report(new(100, "Mod-pack verification complete",
                $"Restored {total.Missing}; replaced {total.Replaced}; removed {removed}; preserved {total.Preserved} configurations"));
            return $"Mod-pack verification completed. Missing files restored: {total.Missing}. " +
                   $"Corrupt files replaced: {total.Replaced}. Obsolete pack files removed: {removed}. " +
                   $"Existing configurations preserved: {total.Preserved}.";
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

    internal string RepairLinuxInstall(string selectedPath, IProgress<InstallProgress> progress)
    {
        if (!OperatingSystem.IsLinux())
            throw new InvalidOperationException("Linux archive-path repair is only available on Linux.");

        var root = ValidateTarget(selectedPath, requireFresh: false);
        progress.Report(new(10, "Locating misplaced Linux archive entries", null));
        var repaired = RepairBackslashArtifacts(root);
        if (repaired == 0)
        {
            progress.Report(new(80, "No misplaced Linux paths found", "Auditing the existing installation"));
            try
            {
                AuditInstall(root);
                WriteReceipt(root);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    "No Linux archive-path repair is needed, but the mod-pack audit found a problem. " +
                    "Use Verify Mod Pack Install to replace missing or corrupt files.", ex);
            }

            progress.Report(new(100, "No Linux repair needed", "The installed mod-pack audit passed"));
            return "No Linux archive-path repair was needed. The installed mod-pack audit passed.";
        }

        progress.Report(new(80, "Auditing repaired installation", $"Repaired {repaired} paths"));
        AuditInstall(root);
        WriteReceipt(root);
        progress.Report(new(100, "Linux installation repaired", $"Repaired {repaired} paths"));
        return $"The Linux mod installation was repaired and audited. Repaired paths: {repaired}.";
    }

    internal static int RepairBackslashArtifacts(string root)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        var plans = new List<(string Source, string Destination, bool IsDirectory)>();
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.TopDirectoryOnly))
        {
            var literalName = Path.GetFileName(source);
            if (!literalName.Contains('\\')) continue;

            var relative = NormalizeArchivePath(literalName).TrimEnd('/');
            if (!relative.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase) &&
                !relative.StartsWith("SPT_Runtime/", StringComparison.OrdinalIgnoreCase))
                continue;
            var destination = Path.GetFullPath(Path.Combine(root,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(rootWithSeparator, PathComparison()))
                throw new InvalidOperationException($"Misplaced archive path escapes the SPT folder: {literalName}");
            if (!destinations.Add(destination))
                throw new InvalidOperationException($"Misplaced archive paths collide: {literalName}");

            var isDirectory = Directory.Exists(source);
            if (isDirectory && Directory.EnumerateFileSystemEntries(source).Any())
                throw new InvalidOperationException($"Misplaced archive directory is not empty: {literalName}");
            plans.Add((source, destination, isDirectory));
        }

        foreach (var plan in plans)
        {
            if (plan.IsDirectory)
            {
                Directory.CreateDirectory(plan.Destination);
                Directory.Delete(plan.Source);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(plan.Destination)!);
            File.Move(plan.Source, plan.Destination, true);
        }

        return plans.Count;
    }

    internal static ArchiveRepairResult RepairManagedFilesFromArchive(string archivePath, string root)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var planned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            if (IsManagedRuntimePath(key)) planned.Add(key, destination);
        }

        var options = new ExtractionOptions
        {
            Overwrite = true,
            CheckCrc = true,
            SymbolicLinkHandler = (_, _) => throw new InvalidOperationException("Symbolic links are not allowed.")
        };
        var missing = 0;
        var replaced = 0;
        var preserved = 0;
        var unchanged = 0;
        foreach (var entry in archive.Entries)
        {
            var key = NormalizeArchivePath(entry.Key ?? "");
            if (!planned.TryGetValue(key, out var destination) || entry.IsDirectory) continue;

            if (File.Exists(destination) && IsUserMutableConfiguration(key))
            {
                preserved++;
                continue;
            }

            var exists = File.Exists(destination);
            if (exists)
            {
                using var expectedStream = entry.OpenEntryStream();
                var expectedHash = SHA256.HashData(expectedStream);
                using var actualStream = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
                var actualHash = SHA256.HashData(actualStream);
                if (expectedHash.AsSpan().SequenceEqual(actualHash))
                {
                    unchanged++;
                    continue;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temp = $"{destination}.phetzy-repair-{Guid.NewGuid():N}.tmp";
            try
            {
                entry.WriteToFile(temp, options);
                File.Move(temp, destination, true);
            }
            finally
            {
                TryDelete(temp);
            }

            if (exists) replaced++;
            else missing++;
        }

        return new ArchiveRepairResult(missing, replaced, preserved, unchanged);
    }

    internal static void ValidateManagedVerificationRuntime()
    {
        var fixture = Path.Combine(Path.GetTempPath(), $"phetzy-verification-smoke-{Guid.NewGuid():N}");
        var root = Path.Combine(fixture, "SPT");
        var archivePath = Path.Combine(fixture, "fixture.zip");
        Directory.CreateDirectory(Path.Combine(root, "BepInEx", "plugins"));
        Directory.CreateDirectory(Path.Combine(root, "BepInEx", "config"));
        try
        {
            using (var stream = File.Create(archivePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteFixtureEntry(archive, @"BepInEx\plugins\broken.dll", "expected payload");
                WriteFixtureEntry(archive, @"SPT_Runtime\user\mods\fixture\missing.dll", "restored payload");
                WriteFixtureEntry(archive, @"BepInEx\config\fixture.cfg", "pack default");
                WriteFixtureEntry(archive, @"SOURCE\Plugin.cs", "ignored source");
            }

            File.WriteAllText(Path.Combine(root, "BepInEx", "plugins", "broken.dll"), "corrupt payload");
            File.WriteAllText(Path.Combine(root, "BepInEx", "config", "fixture.cfg"), "user setting");
            var result = RepairManagedFilesFromArchive(archivePath, root);
            if (result.Missing != 1 || result.Replaced != 1 || result.Preserved != 1 ||
                File.ReadAllText(Path.Combine(root, "BepInEx", "plugins", "broken.dll")) != "expected payload" ||
                File.ReadAllText(Path.Combine(root, "SPT_Runtime", "user", "mods", "fixture", "missing.dll")) !=
                "restored payload" ||
                File.ReadAllText(Path.Combine(root, "BepInEx", "config", "fixture.cfg")) != "user setting" ||
                File.Exists(Path.Combine(root, "SOURCE", "Plugin.cs")))
                throw new InvalidOperationException("Managed verification runtime smoke check failed.");
        }
        finally
        {
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }

    private static void WriteFixtureEntry(ZipArchive archive, string path, string contents)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
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
            throw new InvalidOperationException(
                "Existing server mods were found. Use Verify Mod Pack Install for an existing pack, " +
                "or select a fresh SPT 4.1.3 folder.");

        var plugins = CombineRoot(root, "BepInEx/plugins");
        if (Directory.Exists(plugins) && Directory.EnumerateFileSystemEntries(plugins)
                .Any(path => !Path.GetFileName(path).Equals("spt", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Existing third-party BepInEx plugins were found. Use Verify Mod Pack Install for an existing pack, " +
                "or select a fresh SPT 4.1.3 folder.");

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

    internal async Task DownloadBundleAsync(BundleEntry bundle, string outputPath,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(bundle.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The bundle URL is not valid HTTPS.");
        if (await TryUseCachedBundleAsync(bundle, outputPath, progress, cancellationToken))
            return;
        if (await TryPromoteCompletedPartAsync(bundle, outputPath, progress, cancellationToken))
            return;

        var tempPath = $"{outputPath}.{Guid.NewGuid():N}.part";
        TryDelete(tempPath);
        long written = 0;
        try
        {
            {
                using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    1024 * 1024, true);
                var buffer = new byte[1024 * 1024];
                var started = Stopwatch.GetTimestamp();
                var lastReport = started;
                var lastTransferPercent = -1;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    written += read;
                    var percent = bundle.Bytes == 0 ? 0 : (int)Math.Min(100, written * 100 / bundle.Bytes);
                    var now = Stopwatch.GetTimestamp();
                    var reportDue = percent != lastTransferPercent ||
                                    Stopwatch.GetElapsedTime(lastReport, now) >= TimeSpan.FromMilliseconds(250) ||
                                    written == bundle.Bytes;
                    if (reportDue)
                    {
                        progress.Report(new(4 + percent * 20 / 100, "Downloading mod pack",
                            $"{percent}% — {ProgressPresentation.FormatTransferDetail(written, bundle.Bytes, Stopwatch.GetElapsedTime(started, now))}"));
                        lastTransferPercent = percent;
                        lastReport = now;
                    }
                }

                await output.FlushAsync(cancellationToken);
            }
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

    private static async Task<bool> TryUseCachedBundleAsync(BundleEntry bundle, string outputPath,
        IProgress<InstallProgress> progress, CancellationToken cancellationToken)
    {
        if (!File.Exists(outputPath)) return false;
        if (new FileInfo(outputPath).Length != bundle.Bytes)
        {
            TryDelete(outputPath);
            return false;
        }

        progress.Report(new(24, "Verifying cached mod pack", Path.GetFileName(outputPath)));
        var hash = await Sha256FileAsync(outputPath, cancellationToken);
        if (hash.Equals(bundle.Sha256, StringComparison.OrdinalIgnoreCase)) return true;
        TryDelete(outputPath);
        return false;
    }

    private static async Task<bool> TryPromoteCompletedPartAsync(BundleEntry bundle, string outputPath,
        IProgress<InstallProgress> progress, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return false;

        var pattern = $"{Path.GetFileName(outputPath)}.*.part";
        foreach (var candidate in Directory.EnumerateFiles(directory, pattern))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (new FileInfo(candidate).Length != bundle.Bytes)
            {
                TryDelete(candidate);
                continue;
            }

            progress.Report(new(24, "Verifying completed cached download", Path.GetFileName(candidate)));
            var hash = await Sha256FileAsync(candidate, cancellationToken);
            if (!hash.Equals(bundle.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(candidate);
                continue;
            }

            File.Move(candidate, outputPath, true);
            return true;
        }

        return false;
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

    internal static void ValidateReleaseCatalog(ReleaseCatalog catalog)
    {
        if (catalog.SchemaVersion != 1 ||
            catalog.SptVersion != ExpectedSptVersion ||
            catalog.EftVersion != ExpectedEftVersion ||
            string.IsNullOrWhiteSpace(catalog.Channel) ||
            string.IsNullOrWhiteSpace(catalog.CurrentReleaseId) ||
            catalog.Releases is null || catalog.Releases.Count == 0)
            throw new InvalidOperationException("The release catalog is incomplete or targets another build.");

        if (catalog.Releases.Select(release => release.ReleaseId)
            .Distinct(StringComparer.Ordinal).Count() != catalog.Releases.Count)
            throw new InvalidOperationException("The release catalog contains duplicate release identifiers.");
        if (!catalog.Releases.Any(release =>
                release.ReleaseId.Equals(catalog.CurrentReleaseId, StringComparison.Ordinal)))
            throw new InvalidOperationException("The current release is absent from the release catalog.");

        foreach (var release in catalog.Releases)
        {
            if (!Regex.IsMatch(release.ReleaseId, "^[a-z0-9]+(?:[.-][a-z0-9]+)*$") ||
                string.IsNullOrWhiteSpace(release.Label) ||
                !Regex.IsMatch(release.BundleSha256, "^[A-Fa-f0-9]{64}$") ||
                !DateTimeOffset.TryParse(release.PublishedUtc, out _) ||
                !Uri.TryCreate(release.ManifestUrl, UriKind.Absolute, out var manifestUri) ||
                manifestUri.Scheme != Uri.UriSchemeHttps ||
                release.Status is not ("available" or "withdrawn"))
                throw new InvalidOperationException($"The release catalog entry is invalid: {release.ReleaseId}");
            foreach (var change in release.Changes ?? [])
            {
                if (string.IsNullOrWhiteSpace(change.Mod) || change.Details is null ||
                    change.Details.Count == 0 || change.Details.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidOperationException(
                        $"The release changelog is invalid: {release.ReleaseId}");
            }
        }
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
        var planned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            planned.Add(key, destination);
        }

        var options = new ExtractionOptions
        {
            Overwrite = true,
            CheckCrc = true,
            SymbolicLinkHandler = (_, _) => throw new InvalidOperationException("Symbolic links are not allowed.")
        };
        foreach (var entry in archive.Entries)
        {
            var key = NormalizeArchivePath(entry.Key ?? "");
            if (!planned.TryGetValue(key, out var destination)) continue;
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.WriteToFile(destination, options);
        }
    }

    private static void CopyBundledSettings(Dictionary<string, ZipArchiveEntry> entries, string root)
    {
        foreach (var relative in BundledSettingPaths)
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

    private static readonly string[] BundledSettingPaths =
    [
        "SPT_Runtime/user/mods/[SVM] Server Value Modifier/Presets/LadsGOAGANE.json",
        "SPT_Runtime/user/mods/[SVM] Server Value Modifier/Loader/loader.json"
    ];

    private static IReadOnlyList<string> ReadManagedPathsFromArchive(string archivePath)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        return archive.Entries
            .Where(entry => !entry.IsDirectory)
            .Select(entry => NormalizeArchivePath(entry.Key ?? ""))
            .Where(path => !string.IsNullOrWhiteSpace(path) && IsManagedRuntimePath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static int RemoveObsoleteManagedFiles(string root, IEnumerable<string> previous,
        IEnumerable<string> desired)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var desiredSet = desired.Select(NormalizeArchivePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = 0;
        foreach (var relative in previous.Select(NormalizeArchivePath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (desiredSet.Contains(relative) || !IsManagedRuntimePath(relative) ||
                IsUserMutableConfiguration(relative)) continue;
            var path = CombineRoot(root, relative);
            if (!IsWithin(path, root)) throw new InvalidOperationException($"Receipt path escapes the SPT folder: {relative}");
            if (!File.Exists(path)) continue;
            File.Delete(path);
            removed++;
        }
        return removed;
    }

    private static List<string>? ReadReceiptManagedFiles(string root)
    {
        var path = CombineRoot(root, "SPT_Runtime/user/SPT413-ModPack-Receipt.json");
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("ManagedFiles", out var files) ||
            files.ValueKind != JsonValueKind.Array) return null;
        return files.EnumerateArray().Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToList();
    }

    private static string? ReadReceiptString(string root, string propertyName)
    {
        var path = CombineRoot(root, "SPT_Runtime/user/SPT413-ModPack-Receipt.json");
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static void WriteReceipt(string root, string? releaseId = null, string? bundleSha256 = null,
        IEnumerable<string>? managedFiles = null)
    {
        releaseId ??= ReadReceiptString(root, "ReleaseId");
        bundleSha256 ??= ReadReceiptString(root, "BundleSha256");
        var existingManagedFiles = managedFiles?.Select(NormalizeArchivePath)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? ReadReceiptManagedFiles(root)?.ToArray();
        var receipt = new
        {
            Bundle = "SPT413-Full-Mod-Pack-spt413-phetzy.1",
            ReleaseId = releaseId,
            BundleSha256 = bundleSha256,
            InstalledUtc = DateTime.UtcNow.ToString("O"),
            Target = root,
            SptServerVersion = ExpectedSptVersion,
            EftVersion = ExpectedEftVersion,
            ArchiveCount = ExpectedArchiveCount,
            InstallerPlatform = OperatingSystem.IsLinux() ? "linux-x64" : "win-x64",
            ManagedFiles = existingManagedFiles
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

    private static bool IsManagedRuntimePath(string relative) =>
        relative.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("SPT_Runtime/", StringComparison.OrdinalIgnoreCase);

    private static bool IsUserMutableConfiguration(string relative)
    {
        var normalized = NormalizeArchivePath(relative);
        var extension = Path.GetExtension(normalized);
        var configExtension = extension.Equals(".cfg", StringComparison.OrdinalIgnoreCase) ||
                              extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                              extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                              extension.Equals(".jsonc", StringComparison.OrdinalIgnoreCase) ||
                              extension.Equals(".toml", StringComparison.OrdinalIgnoreCase) ||
                              extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                              extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
        if (!configExtension) return false;

        var fileName = Path.GetFileName(normalized);
        return normalized.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/config/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/configs/", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("config.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("config.jsonc", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("settings.json", StringComparison.OrdinalIgnoreCase);
    }

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
    internal readonly record struct ArchiveRepairResult(int Missing, int Replaced, int Preserved, int Unchanged)
    {
        public static ArchiveRepairResult operator +(ArchiveRepairResult left, ArchiveRepairResult right) =>
            new(left.Missing + right.Missing, left.Replaced + right.Replaced,
                left.Preserved + right.Preserved, left.Unchanged + right.Unchanged);
    }
    internal static InstallProgress MapDownloadedBundleProgress(InstallProgress update) =>
        update with { Percent = 25 + Math.Clamp(update.Percent, 0, 100) * 75 / 100 };

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
    private sealed record UpdaterSource(string ManifestUrl, string? ReleaseCatalogUrl = null);
    private sealed record PackManifest(int SchemaVersion, string SptVersion, string EftVersion, BundleEntry Bundle,
        string? ReleaseId = null, string? Channel = null);
    internal sealed record ReleaseCatalog(int SchemaVersion, string Channel, string SptVersion, string EftVersion,
        string CurrentReleaseId, List<PackRelease> Releases);
    internal sealed record PackRelease(string ReleaseId, string Label, string PublishedUtc, string ManifestUrl,
        string BundleSha256, string Status, bool IsCurrent = false, string? Notes = null,
        List<ModChange>? Changes = null)
    {
        public override string ToString() => IsCurrent ? $"{Label} (current)" : Label;
    }
    internal sealed record ModChange(string Mod, string? PreviousVersion, string? Version, List<string> Details);
    internal sealed record BundleEntry(string FileName, string Url, long Bytes, string Sha256);
}
