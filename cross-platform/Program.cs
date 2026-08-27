using Avalonia;

namespace Phetzy.Spt413Updater.CrossPlatform;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "--replace-updater")
                return UpdaterSelfUpdate.ReplaceRunningUpdater(args[1], int.Parse(args[2]));

            if (args.Length == 2 && args[0] == "--wait-for-parent")
            {
                UpdaterSelfUpdate.WaitForParent(int.Parse(args[1]));
                args = [];
            }

            if (args.Length == 1 && args[0] == "--check-updater")
            {
                var release = UpdaterSelfUpdate.CheckAsync().GetAwaiter().GetResult();
                Console.WriteLine(release is null ? "CURRENT" : $"AVAILABLE {release.TagName}");
                return 0;
            }

            if (args.Length == 1 && args[0] == "--check-embedded-source")
            {
                PackInstallEngine.ValidateEmbeddedSource();
                Console.WriteLine("EMBEDDED SOURCE VALID");
                return 0;
            }

            if (args.Length == 1 && args[0] == "--check-native-runtime")
            {
                _ = new SkiaSharp.SKImageInfo(1, 1).ColorType;
                Console.WriteLine("NATIVE RUNTIME VALID");
                return 0;
            }

            if (args.Length == 1 && args[0] == "--check-self-update-extraction")
            {
                UpdaterSelfUpdate.ValidateArchiveExtractionRuntime();
                Console.WriteLine("SELF-UPDATE EXTRACTION VALID");
                return 0;
            }

            if (args.Length == 1 && args[0] == "--self-update-child-smoke")
                return 0;

            if (args.Length == 1 && args[0] == "--check-self-update-handoff-runtime")
            {
                UpdaterSelfUpdate.ValidateProcessHandoffRuntime();
                Console.WriteLine("SELF-UPDATE HANDOFF VALID");
                return 0;
            }

            if (args.Length == 1 && args[0] == "--check-private-channel")
            {
                var engine = new PackInstallEngine();
                Console.WriteLine(engine.CheckPrivateChannelAsync().GetAwaiter().GetResult());
                return 0;
            }

            if (args.Length == 2 && args[0] == "--validate")
            {
                Console.WriteLine(PackInstallEngine.ValidateTarget(args[1], requireFresh: true));
                return 0;
            }

            if (args.Length == 3 && args[0] == "--install-bundle")
            {
                var engine = new PackInstallEngine();
                Console.WriteLine(engine.InstallFromBundleAsync(args[1], args[2],
                    new Progress<PackInstallEngine.InstallProgress>(_ => { })).GetAwaiter().GetResult());
                return 0;
            }

            if (args.Length == 2 && args[0] == "--apply-hotfix")
            {
                var engine = new PackInstallEngine();
                Console.WriteLine(engine.ApplyHotfix(args[1],
                    new Progress<PackInstallEngine.InstallProgress>(_ => { })));
                return 0;
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "updater-error.log"), ex.ToString());
            }
            catch
            {
            }
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
