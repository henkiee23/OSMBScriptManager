using Avalonia;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using OSMBScriptManager.Services;
using OSMBScriptManager.Views;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace OSMBScriptManager;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Global exception handlers to show user-friendly error dialog and write logs
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            _ = ReportUnhandledException(ex);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            _ = ReportUnhandledException(e.Exception);
            e.SetObserved();
        };

        // Font glyph registration removed: app will not register embedded Font Awesome fonts.

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // show a splash window while checking for updates; only open main UI after check
            var splash = new SplashWindow();
            desktop.MainWindow = splash;
            splash.Show();

            Task.Run(async () =>
            {
                var updater = new AutoUpdateService();
                var settingsSvc = new SettingsService();
                var settings = await settingsSvc.LoadAsync();
                try
                {
                    if (!settings.EnableAutoUpdateCheck)
                    {
                        // skip checks, go to main UI
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            try
                            {
                                var main = new MainWindow();
                                desktop.MainWindow = main;
                                main.Show();
                            }
                            finally { try { splash.Close(); } catch { } }
                        });
                        return;
                    }

                    ReleaseInfo? info = null;

                    if (IsDevelopmentBuild())
                    {
                        // development builds (local VS/VSCode runs) should not prompt for updates
                        await Dispatcher.UIThread.InvokeAsync(() => splash.SetStatus("Development build — skipping update check", indeterminate: false));
                        await Task.Delay(600);
                        info = null;
                    }
                    else
                    {
                        info = await updater.GetLatestReleaseAsync("henkiee23", "OSMBScriptManager");
                        if (info == null || string.IsNullOrEmpty(info.AssetUrl))
                        {
                            // no release info -> continue
                            info = null;
                        }
                        else
                        {
                            // respect user's skipped version
                            if (!string.IsNullOrEmpty(settings.SkipUpdateVersion) && settings.SkipUpdateVersion == info.TagName)
                            {
                                info = null;
                            }
                            else
                            {
                                // compare versions: skip if latest is not newer than current
                                var currentVer = GetCurrentVersionString();
                                var latestTag = info.TagName ?? string.Empty;
                                if (!string.IsNullOrEmpty(latestTag))
                                {
                                    var cmp = CompareVersionStrings(latestTag, currentVer);
                                    if (cmp <= 0)
                                    {
                                        // latest is older or equal -> do not prompt
                                        info = null;
                                    }
                                }
                            }
                        }
                    }

                    if (info == null)
                    {
                        // nothing to do, continue to main UI
                    }
                    else
                    {
                        // prompt user on UI thread
                        var dlg = await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var d = new UpdateDialog("Update available", $"A new version ({info.TagName}) is available. Install now?", info.TagName ?? string.Empty);
                            await d.ShowDialogAsync(splash);
                            return d;
                        });

                        var accepted = dlg != null && dlg.DialogResult == true;
                        // Check skip preference set in dialog
                        if (dlg != null && dlg.SkipThisVersion && !string.IsNullOrEmpty(info.TagName))
                        {
                            settings.SkipUpdateVersion = info.TagName;
                            await settingsSvc.SaveAsync(settings);
                        }

                        if (accepted)
                        {
                            // attempt download with progress and retries
                            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), info.AssetName ?? ("osmb-update-" + (info.TagName ?? "new") + ".exe"));
                            int attempts = 0;
                            const int maxAttempts = 3;
                            bool downloaded = false;
                            while (attempts < maxAttempts && !downloaded)
                            {
                                attempts++;
                                try
                                {
                                    // update splash UI
                                    await Dispatcher.UIThread.InvokeAsync(() => { splash.SetStatus("Downloading update...", indeterminate: false); splash.UpdateProgress(0); });
                                    var progress = new Progress<double>(p => Dispatcher.UIThread.InvokeAsync(() => splash.UpdateProgress(p)));
                                    await updater.DownloadAssetAsync(info.AssetUrl, tmp, progress);
                                    downloaded = true;
                                }
                                catch
                                {
                                    // on failure, prompt user to retry or skip
                                    var retryDlg = await Dispatcher.UIThread.InvokeAsync(async () =>
                                    {
                                        var msg = "Download failed. Retry?";
                                        var rd = new UpdateDialog("Download failed", msg, info.TagName ?? string.Empty);
                                        await rd.ShowDialogAsync(splash);
                                        return rd;
                                    });

                                    if (retryDlg != null && retryDlg.SkipThisVersion && !string.IsNullOrEmpty(info.TagName))
                                    {
                                        settings.SkipUpdateVersion = info.TagName;
                                        await settingsSvc.SaveAsync(settings);
                                    }

                                    var wantsRetry = retryDlg != null && retryDlg.DialogResult == true;
                                    if (!wantsRetry) break; // give up
                                }
                            }

                            if (downloaded)
                            {
                                try
                                {
                                    await Dispatcher.UIThread.InvokeAsync(() => splash.SetStatus("Launching installer...", indeterminate: true));
                                    var psi = new System.Diagnostics.ProcessStartInfo(tmp) { UseShellExecute = true };
                                    System.Diagnostics.Process.Start(psi);
                                }
                                catch { }
                                // shutdown the app to let installer run
                                desktop.Shutdown();
                                return;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore update errors and continue
                }

                // no update or user declined -> open main window
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        var main = new MainWindow();
                        desktop.MainWindow = main;
                        main.Show();
                    }
                    finally
                    {
                        try { splash.Close(); } catch { }
                    }
                });
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static async System.Threading.Tasks.Task ReportUnhandledException(Exception? ex)
    {
        try
        {
            var logs = System.IO.Path.Combine(AppContext.BaseDirectory ?? System.IO.Directory.GetCurrentDirectory(), "logs");
            try { System.IO.Directory.CreateDirectory(logs); } catch { }
            var file = System.IO.Path.Combine(logs, $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
            try { System.IO.File.WriteAllText(file, ex?.ToString() ?? "(null)"); } catch { }

            // show dialog on UI thread if possible
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var dlg = new Views.ErrorDialog("Unexpected error", ex?.Message ?? "An unexpected error occurred.", ex?.ToString() ?? string.Empty);
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var owner = desktop.MainWindow;
                        if (owner != null)
                        {
                            dlg.ShowDialog(owner);
                            return;
                        }
                    }

                    // fallback
                    dlg.Show();
                }
                catch { }
            });
        }
        catch { }
    }

    public static string GetCurrentVersionString()
    {
        try
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            if (asm != null)
            {
                // prefer informational version attribute
                var infoAttr = asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>();
                if (infoAttr != null && !string.IsNullOrEmpty(infoAttr.InformationalVersion))
                    return infoAttr.InformationalVersion;

                var fileAttr = asm.GetCustomAttribute<System.Reflection.AssemblyFileVersionAttribute>();
                if (fileAttr != null && !string.IsNullOrEmpty(fileAttr.Version))
                    return fileAttr.Version;

                try
                {
                    var loc = asm.Location;
                    if (!string.IsNullOrEmpty(loc))
                    {
                        var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(loc);
                        if (!string.IsNullOrEmpty(fvi.ProductVersion)) return fvi.ProductVersion;
                    }
                }
                catch { }

                var v = asm.GetName().Version;
                if (v != null) return v.ToString();
            }

            return "0.0.0";
        }
        catch
        {
            return "0.0.0";
        }
    }

    private static int CompareVersionStrings(string a, string b)
    {
        // parse numeric major.minor.patch components from the start of the string (ignore pre-release and build metadata)
        int[] Parse(string s)
        {
            var nums = new int[3];
            if (string.IsNullOrEmpty(s)) return nums;
            s = s.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            // remove any +build or -prerelease suffix
            var cutIdx = s.IndexOf('+');
            if (cutIdx >= 0) s = s.Substring(0, cutIdx);
            cutIdx = s.IndexOf('-');
            if (cutIdx >= 0) s = s.Substring(0, cutIdx);
            // match leading numeric version tokens
            var m = System.Text.RegularExpressions.Regex.Match(s, "^(?<ver>\\d+(?:\\.\\d+){0,2})");
            if (!m.Success) return nums;
            var ver = m.Groups["ver"].Value;
            var parts = ver.Split('.');
            for (int i = 0; i < Math.Min(3, parts.Length); i++)
            {
                if (int.TryParse(parts[i], out var n)) nums[i] = n;
            }
            return nums;
        }

        var A = Parse(a);
        var B = Parse(b);
        for (int i = 0; i < 3; i++)
        {
            if (A[i] < B[i]) return -1;
            if (A[i] > B[i]) return 1;
        }
        return 0;
    }

    public static bool IsDevelopmentBuild()
    {
        try
        {
            var s = GetCurrentVersionString();
            if (string.IsNullOrEmpty(s)) return true;
            var t = s.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase)) t = t.Substring(1);
            // treat only explicit 0.0.0 (or unversioned/unparsable) as development
            if (t == "0.0.0" || t == "0.0.0.0" || t == "0.0") return true;
            // strip any pre-release/build metadata and parse the leading numeric version
            var m = System.Text.RegularExpressions.Regex.Match(t, "^(?<ver>\\d+(\\.\\d+){1,2})");
            if (m.Success)
            {
                var num = m.Groups["ver"].Value;
                if (Version.TryParse(num, out var v))
                {
                    // consider development only when version is 0.0.x
                    return v.Major == 0 && v.Minor == 0 && (v.Build == 0 || v.Build == -1);
                }
            }
            // if we cannot parse a reasonable numeric prefix, treat as development
            return true;
        }
        catch
        {
            return true;
        }
    }
}