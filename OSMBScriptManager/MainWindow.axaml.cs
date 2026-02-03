using Avalonia.Controls;
using Avalonia.Interactivity;
using OSMBScriptManager.Models;
using OSMBScriptManager.Services;
using Avalonia.Threading;
using System;
using Avalonia;
using Avalonia.Styling;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using OSMBScriptManager.Views;
using System.Net.Http;

namespace OSMBScriptManager;

public partial class MainWindow : Window
{
    private readonly GitRepoService _gitService = new();
    private readonly PersistenceService _persistence = new();
    private readonly DeveloperConfigService _devConfig = new();
    private readonly SettingsService _settingsService = new();
    private Dictionary<string, string> _savedState = new();
    private List<ScriptDeveloper> _developers = new();
    private readonly Dictionary<string, List<TrackedScript>> _repoJars = new();
    private readonly AutoUpdateService _updateService = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void TargetDirTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        var tb = this.FindControl<TextBox>("TargetDirTextBox")!;
        var path = tb.Text?.Trim() ?? string.Empty;
        try
        {
            await _settingsService.SaveAsync(new Settings { TargetDirectory = path });
        }
        catch
        {
        }
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // load saved state
        _savedState = await _persistence.LoadStateAsync();

        // load developers from config
        var loaded = await _devConfig.LoadAsync();
        if (loaded != null && loaded.Count > 0)
            _developers = loaded;
        else
        {
            // fallback sample
            _developers = new List<ScriptDeveloper>
            {
                new ScriptDeveloper { Name = "Davyy", RepoUrl = "https://github.com/JustDavyy/osmb-scripts.git", PathRegex = ".*\\.jar$" },
                new ScriptDeveloper { Name = "Sainty", RepoUrl = "https://github.com/ytniaS/SaintyScripts.git", PathRegex = ".*\\.jar$" },
                new ScriptDeveloper { Name = "Tidal", RepoUrl = "https://github.com/Manokit/tidals-scripts.git", PathRegex = ".*\\.jar$" },
                new ScriptDeveloper { Name = "Kiko", RepoUrl = "https://github.com/999kiko/OSMB.git", PathRegex = ".*\\.jar$" },
                new ScriptDeveloper { Name = "Rats", RepoUrl = "https://gitlab.com/rats_rs/osmb.git", PathRegex = ".*\\.jar$" },
                new ScriptDeveloper { Name = "Butter", RepoUrl = "https://github.com/ButterB21/Butter-Scripts.git", PathRegex = ".*\\.jar$" },
                new ScriptDeveloper { Name = "Fru", RepoUrl = "https://github.com/fru-art/fru-scripts.git", PathRegex = ".*\\.jar$" },
                new ScriptDeveloper { Name = "Jose", RepoUrl = "https://github.com/joseOSMB/JOSE-OSMB-SCRIPTS.git", PathRegex = ".*\\.jar$" },
            };
        }

        PopulateDevelopersList();

        // load saved settings (target dir)
        var settings = await _settingsService.LoadAsync();
        if (!string.IsNullOrEmpty(settings.TargetDirectory))
        {
            this.FindControl<TextBox>("TargetDirTextBox")!.Text = settings.TargetDirectory;
            // scan installed plugins immediately for the saved folder
            await ScanInstalledPluginsAsync();
        }

        // apply saved theme settings
        try
        {
            ApplyThemeSettings(settings);
            // Update UI controls
            var follow = this.FindControl<CheckBox>("FollowSystemThemeCheckBox");
            var combo = this.FindControl<ComboBox>("ThemeComboBox");
            if (follow != null) follow.IsChecked = settings.FollowSystemTheme;
            if (combo != null)
            {
                combo.IsEnabled = !settings.FollowSystemTheme;
                if (settings.PreferredTheme == ThemePreference.Dark)
                    combo.SelectedIndex = 1;
                else
                    combo.SelectedIndex = 0;
            }
        }
        catch
        {
            // ignore theme apply errors
        }

        var browse = this.FindControl<Button>("BrowseButton")!;
        browse.Click += BrowseButton_Click;

        var fetchBtn = this.FindControl<Button>("FetchButton");
        if (fetchBtn != null) fetchBtn.Click += FetchButton_Click;

        var installBtn = this.FindControl<Button>("InstallButton");
        if (installBtn != null) installBtn.Click += DownloadButton_Click;

        var deleteBtn = this.FindControl<Button>("DeleteInstalledButton");
        if (deleteBtn != null) deleteBtn.Click += DeleteInstalledButton_Click;

        // Font registration diagnostics removed.

        // save target dir when user edits the textbox
        this.FindControl<TextBox>("TargetDirTextBox")!.LostFocus += TargetDirTextBox_LostFocus;

        // wire installed plugins buttons
        this.FindControl<Button>("RefreshInstalledButton")!.Click += RefreshInstalledButton_Click;
        this.FindControl<Button>("UpdateInstalledButton")!.Click += UpdateInstalledButton_Click;
        this.FindControl<Button>("UpdateAllInstalledButton")!.Click += UpdateAllInstalledButton_Click;

        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        devList.SelectionChanged += DevelopersListBox_SelectionChanged;

        // developer repos are read-only in the UI; editing removed

        // wire settings controls
        var followCb = this.FindControl<CheckBox>("FollowSystemThemeCheckBox");
        var themeCombo = this.FindControl<ComboBox>("ThemeComboBox");
        if (followCb != null)
        {
            followCb.Click += async (_, __) =>
            {
                var s = await _settingsService.LoadAsync();
                s.FollowSystemTheme = followCb.IsChecked == true;
                if (themeCombo != null) themeCombo.IsEnabled = !s.FollowSystemTheme;
                await _settingsService.SaveAsync(s);
                ApplyThemeSettings(s);
            };
        }

        if (themeCombo != null)
        {
            themeCombo.SelectionChanged += async (_, __) =>
            {
                var s = await _settingsService.LoadAsync();
                s.PreferredTheme = themeCombo.SelectedIndex == 1 ? ThemePreference.Dark : ThemePreference.Light;
                await _settingsService.SaveAsync(s);
                ApplyThemeSettings(s);
            };
        }

        // show current app version in settings
        try
        {
            var versionTb = this.FindControl<TextBlock>("VersionTextBlock");
            if (versionTb != null)
            {
                var cur = GetCurrentVersionString();
                var isDev = App.IsDevelopmentBuild();
                versionTb.Text = cur + (isDev ? " (development build — auto-update disabled)" : string.Empty);
            }
        }
        catch { }

        // start background fetch of all repos (cache only)
        _ = Task.Run(async () => await BackgroundFetchAllReposAsync());

        // update check is handled at startup in App; do not run it again here.
    }

    private void PopulateDevelopersList()
    {
        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        devList.ItemsSource = _developers.Select(d => d.Name).ToList();
    }

    private async void DevelopersListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        if (devList.SelectedIndex < 0) return;
        var dev = _developers[devList.SelectedIndex];
        await LoadJarsForDeveloperAsync(dev);
    }

    // Developer editing is disabled; repository list is read-only in the UI.

    private async void RefreshInstalledButton_Click(object? sender, RoutedEventArgs e)
    {
        await ScanInstalledPluginsAsync();
    }

    private async void SetStatus(string message, bool indeterminate = false, double? progress = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var txt = this.FindControl<TextBlock>("StatusText");
            var bar = this.FindControl<ProgressBar>("TaskProgressBar");
            if (txt != null) txt.Text = message ?? string.Empty;
            if (bar != null)
            {
                if (indeterminate)
                {
                    bar.IsVisible = true;
                    bar.IsIndeterminate = true;
                }
                else if (progress.HasValue)
                {
                    bar.IsVisible = true;
                    bar.IsIndeterminate = false;
                    bar.Value = Math.Max(bar.Minimum, Math.Min(bar.Maximum, progress.Value));
                }
                else
                {
                    bar.IsVisible = false;
                    bar.IsIndeterminate = false;
                }
            }
        });
    }

    private async void ClearStatus()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var txt = this.FindControl<TextBlock>("StatusText");
            var bar = this.FindControl<ProgressBar>("TaskProgressBar");
            if (txt != null) txt.Text = string.Empty;
            if (bar != null) { bar.IsVisible = false; bar.IsIndeterminate = false; bar.Value = bar.Minimum; }
        });
    }

    private async void UpdateInstalledButton_Click(object? sender, RoutedEventArgs e)
    {
        var target = this.FindControl<TextBox>("TargetDirTextBox")!.Text;
        if (string.IsNullOrEmpty(target))
        {
            var installed = this.FindControl<ListBox>("InstalledListBox")!;
            installed.ItemsSource = new List<InstalledScript> { new InstalledScript { FileName = "Please choose a target directory first." } };
            return;
        }

        var installedList = this.FindControl<ListBox>("InstalledListBox")!;
        var toUpdate = installedList.Items?.Cast<object>().OfType<InstalledScript>().Where(p => p.IsSelected && p.Matched).ToList() ?? new List<InstalledScript>();
        int total = toUpdate.Count;
        for (int i = 0; i < total; i++)
        {
            var p = toUpdate[i];
            try
            {
                SetStatus($"Updating {p.FileName} ({i + 1}/{total})", indeterminate: false, progress: total > 0 ? (double)(i + 1) / total : 0);
                await _gitService.DownloadJarAsync(p.RepoUrl, p.RelativePath, target);
                _savedState[StateKey(p.RepoUrl, p.RelativePath)] = p.RepoCommitId ?? string.Empty;
            }
            catch (Exception ex)
            {
                var list = installedList.Items?.Cast<object>().ToList() ?? new List<object>();
                list.Add(new InstalledScript { FileName = "Update error: " + ex.Message });
                installedList.ItemsSource = list.Cast<InstalledScript>().ToList();
            }
        }

        await _persistence.SaveStateAsync(_savedState);
        await ScanInstalledPluginsAsync();
    }

    private async void UpdateAllInstalledButton_Click(object? sender, RoutedEventArgs e)
    {
        var target = this.FindControl<TextBox>("TargetDirTextBox")!.Text;
        if (string.IsNullOrEmpty(target))
        {
            var installed = this.FindControl<ListBox>("InstalledListBox")!;
            installed.ItemsSource = new List<InstalledScript> { new InstalledScript { FileName = "Please choose a target directory first." } };
            return;
        }

        var installedList = this.FindControl<ListBox>("InstalledListBox")!;
        var toUpdate = installedList.Items?.Cast<object>().OfType<InstalledScript>().Where(p => p.Matched).ToList() ?? new List<InstalledScript>();
        int total = toUpdate.Count;
        for (int i = 0; i < total; i++)
        {
            var p = toUpdate[i];
            try
            {
                SetStatus($"Updating {p.FileName} ({i + 1}/{total})", indeterminate: false, progress: total > 0 ? (double)(i + 1) / total : 0);
                await _gitService.DownloadJarAsync(p.RepoUrl, p.RelativePath, target);
                _savedState[StateKey(p.RepoUrl, p.RelativePath)] = p.RepoCommitId ?? string.Empty;
            }
            catch (Exception ex)
            {
                var list = installedList.Items?.Cast<object>().ToList() ?? new List<object>();
                list.Add(new InstalledScript { FileName = "Update error: " + ex.Message });
                installedList.ItemsSource = list.Cast<InstalledScript>().ToList();
            }
        }

        await _persistence.SaveStateAsync(_savedState);
        await ScanInstalledPluginsAsync();
    }

    private async void DeleteInstalledButton_Click(object? sender, RoutedEventArgs e)
    {
        var installedList = this.FindControl<ListBox>("InstalledListBox")!;
        var toDelete = installedList.Items?.Cast<object>().OfType<InstalledScript>().Where(p => p.IsSelected).ToList() ?? new List<InstalledScript>();
        if (toDelete.Count == 0)
        {
            SetStatus("No installed scripts selected to delete.");
            return;
        }

        var target = this.FindControl<TextBox>("TargetDirTextBox")!.Text;
        if (string.IsNullOrEmpty(target) || !Directory.Exists(target))
        {
            SetStatus("Please set a valid target directory before deleting.");
            return;
        }

        int deleted = 0;
        foreach (var p in toDelete)
        {
            try
            {
                if (!string.IsNullOrEmpty(p.FullPath) && File.Exists(p.FullPath))
                {
                    File.Delete(p.FullPath);
                    deleted++;
                }

                // remove saved state if tracked
                if (!string.IsNullOrEmpty(p.RepoUrl) && !string.IsNullOrEmpty(p.RelativePath))
                {
                    var key = StateKey(p.RepoUrl, p.RelativePath);
                    if (_savedState.ContainsKey(key)) _savedState.Remove(key);
                }
            }
            catch (Exception ex)
            {
                // continue on errors but report
                SetStatus("Error deleting " + p.FileName + ": " + ex.Message);
            }
        }

        await _persistence.SaveStateAsync(_savedState);
        await ScanInstalledPluginsAsync();
        SetStatus($"Deleted {deleted} file(s).");
    }

    private async Task BackgroundFetchAllReposAsync()
    {
        int total = _developers.Count;
        for (int i = 0; i < total; i++)
        {
            var dev = _developers[i];
            try
            {
                SetStatus($"Background scan: {dev.Name} ({i + 1}/{total})", indeterminate: false, progress: total > 0 ? (double)(i + 1) / total : 0);
                var jars = await _gitService.ScanRepoAsync(dev);
                lock (_repoJars)
                {
                    _repoJars[dev.RepoUrl] = jars;
                }

                // update installed list on UI thread so matches appear progressively
                await Dispatcher.UIThread.InvokeAsync(async () => await ScanInstalledPluginsAsync());
            }
            catch
            {
                // ignore per-repo errors in background
            }
        }

        ClearStatus();
    }

    private string GetCurrentVersionString()
    {
        // delegate to App's version helper for consistency
        try { return App.GetCurrentVersionString(); } catch { return "0.0.0"; }
    }

    private static Version? NormalizeVersion(string tagOrVersion)
    {
        if (string.IsNullOrEmpty(tagOrVersion)) return null;
        var t = tagOrVersion.Trim();
        if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase)) t = t.Substring(1);
        // strip build metadata or prerelease suffix
        var plusIdx = t.IndexOf('+'); if (plusIdx >= 0) t = t.Substring(0, plusIdx);
        var dashIdx = t.IndexOf('-'); if (dashIdx >= 0) t = t.Substring(0, dashIdx);
        // match leading numeric version tokens
        var m = System.Text.RegularExpressions.Regex.Match(t, "^(?<ver>\\d+(?:\\.\\d+){0,2})");
        if (!m.Success) return null;
        var ver = m.Groups["ver"].Value;
        if (Version.TryParse(ver, out var v2)) return v2;
        return null;
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var info = await _updateService.GetLatestReleaseAsync("henkiee23", "OSMBScriptManager");
            if (info == null || string.IsNullOrEmpty(info.AssetUrl)) return;

            var latest = NormalizeVersion(info.TagName);
            var current = NormalizeVersion(GetCurrentVersionString());
            if (latest == null) return;
            if (current != null && latest <= current) return; // not newer

            // ask user on UI thread
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    var dialog = new UpdateDialog("Update available", $"A new version ({info.TagName}) is available. Install now?", info.TagName);
                    var res = await dialog.ShowDialogAsync(this);
                    if (res == true)
                    {
                        SetStatus("Downloading update...", indeterminate: true);
                        var tmp = Path.Combine(Path.GetTempPath(), info.AssetName ?? ("osmb-update-" + info.TagName + ".exe"));
                        await _updateService.DownloadAssetAsync(info.AssetUrl, tmp, new Progress<double>(p => { /* could update UI */ }));
                        SetStatus("Launching installer...", indeterminate: true);
                        try
                        {
                            Process.Start(new ProcessStartInfo(tmp) { UseShellExecute = true });
                        }
                        catch { }
                        // shutdown app to allow installer to run
                        try { Close(); } catch { }
                    }
                }
                catch { }
            });
        }
        catch
        {
            // ignore update errors
        }
    }

    private async Task LoadJarsForDeveloperAsync(ScriptDeveloper dev)
    {
        var jarsList = this.FindControl<ListBox>("JarsListBox")!;
        SetStatus($"Scanning {dev.Name}...", indeterminate: true);
        try
        {
            var jars = await _gitService.ScanRepoAsync(dev);
            foreach (var jar in jars)
            {
                var key = StateKey(jar.RepoUrl, jar.RelativePath);
                if (_savedState.TryGetValue(key, out var savedCommit))
                {
                    jar.Status = savedCommit == jar.CommitId ? "Up-to-date" : $"Outdated (local {savedCommit})";
                }
                jar.IsSelected = false;
            }

            jarsList.ItemsSource = jars;
            // cache for matching against installed files
            _repoJars[dev.RepoUrl] = jars;
            ClearStatus();
        }
        catch (Exception ex)
        {
            // show error as a single item
            var err = new TrackedScript { RelativePath = "Error scanning repo: " + ex.Message, CommitId = string.Empty, Status = "" };
            jarsList.ItemsSource = new List<TrackedScript> { err };
            SetStatus("Error scanning repo: " + ex.Message);
        }
    }

    private async Task ScanInstalledPluginsAsync()
    {
        var target = this.FindControl<TextBox>("TargetDirTextBox")!.Text;
        var installedList = this.FindControl<ListBox>("InstalledListBox")!;
        if (string.IsNullOrEmpty(target) || !Directory.Exists(target))
        {
            installedList.ItemsSource = new List<InstalledScript> { new InstalledScript { FileName = "Please choose a valid target directory." } };
            return;
        }

        SetStatus("Scanning installed scripts...", indeterminate: true);

        var files = Directory.EnumerateFiles(target, "*.jar", SearchOption.TopDirectoryOnly).ToList();
        var installed = new List<InstalledScript>();
        foreach (var f in files)
        {
            var fileName = Path.GetFileName(f);
            var p = new InstalledScript { FileName = fileName, FullPath = f };

            // try to match against cached repo jars
            bool matched = false;
            foreach (var kv in _repoJars)
            {
                var repoUrl = kv.Key;
                var jars = kv.Value;
                var match = jars.FirstOrDefault(j => Path.GetFileName(j.RelativePath).Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    matched = true;
                    p.Matched = true;
                    p.RepoUrl = repoUrl;
                    p.RelativePath = match.RelativePath;
                    p.RepoCommitId = match.CommitId ?? string.Empty;
                    var key = StateKey(repoUrl, match.RelativePath);
                    _savedState.TryGetValue(key, out var saved);
                    p.SavedCommitId = saved ?? string.Empty;
                    if (!string.IsNullOrEmpty(saved))
                        p.Status = saved == p.RepoCommitId ? "Up-to-date" : $"Outdated (local {saved})";
                    else
                        p.Status = "Matched (not tracked)";
                    break;
                }
            }

            if (!matched)
                p.Status = "Unmanaged";

            installed.Add(p);
        }

        installedList.ItemsSource = installed;
        ClearStatus();
    }

    private static string StateKey(string repoUrl, string relativePath) => repoUrl + "|" + relativePath;

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        // Prefer StorageProvider API when available (Avalonia 11+). Use reflection so this compiles
        // against different Avalonia versions. If unavailable, fall back to OpenFolderDialog.
        try
        {
            var storageProp = typeof(Window).GetProperty("StorageProvider");
            if (storageProp != null)
            {
                var storage = storageProp.GetValue(this);
                if (storage != null)
                {
                    // Find an OpenFolderPickerAsync method
                    var methods = storage.GetType().GetMethods().Where(m => m.Name == "OpenFolderPickerAsync").ToArray();
                    object? folderObj = null;
                    foreach (var m in methods)
                    {
                        var parameters = m.GetParameters();
                        object? taskObj;
                        if (parameters.Length == 0)
                        {
                            taskObj = m.Invoke(storage, null);
                        }
                        else if (parameters.Length == 1)
                        {
                            // try passing this Window as owner if accepted
                            taskObj = m.Invoke(storage, new object?[] { this });
                        }
                        else
                        {
                            continue;
                        }

                        if (taskObj == null) continue;

                        // await the returned Task and extract Result via reflection
                        var task = taskObj as System.Threading.Tasks.Task;
                        if (task == null) continue;
                        await task.ConfigureAwait(true);
                        var resProp = task.GetType().GetProperty("Result");
                        if (resProp != null)
                        {
                            folderObj = resProp.GetValue(task);
                            if (folderObj != null) break;
                        }
                    }

                    if (folderObj != null)
                    {
                        // Try to get a local path from the folder object
                        var tryGet = folderObj.GetType().GetMethod("TryGetLocalPath");
                        string? localPath = null;
                        if (tryGet != null)
                        {
                            var args = new object?[] { null };
                            var ok = (bool)tryGet.Invoke(folderObj, args)!;
                            if (ok) localPath = args[0] as string;
                        }

                        if (localPath == null)
                        {
                            var prop = folderObj.GetType().GetProperty("Path") ?? folderObj.GetType().GetProperty("FullPath") ?? folderObj.GetType().GetProperty("Name");
                            if (prop != null)
                                localPath = prop.GetValue(folderObj) as string;
                        }

                        if (!string.IsNullOrEmpty(localPath))
                        {
                            var tb = this.FindControl<TextBox>("TargetDirTextBox")!;
                            tb.Text = localPath!;
                            // persist chosen folder
                            try
                            {
                                await _settingsService.SaveAsync(new Settings { TargetDirectory = localPath! });
                                // scan immediately after user picks
                                await ScanInstalledPluginsAsync();
                            }
                            catch
                            {
                                // ignore save errors
                            }
                            return;
                        }
                    }

                    
                }
            }
        }
        catch
        {
            // if anything fails, fall back to the older dialog below
        }

        // Fallback to OpenFolderDialog for environments where StorageProvider isn't available
#pragma warning disable CS0618
        var dlg = new OpenFolderDialog();
        var path = await dlg.ShowAsync(this);
#pragma warning restore CS0618
        if (!string.IsNullOrEmpty(path))
        {
            var tb = this.FindControl<TextBox>("TargetDirTextBox")!;
            tb.Text = path;
            try
            {
                await _settingsService.SaveAsync(new Settings { TargetDirectory = path });
                SetStatus("Saved target folder.", indeterminate: false, progress: 1);
                await ScanInstalledPluginsAsync();
            }
            catch (Exception ex)
            {
                SetStatus("Failed saving target folder: " + ex.Message);
            }
        }
    }

    private async void FetchButton_Click(object? sender, RoutedEventArgs e)
    {
        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        if (devList.SelectedIndex < 0) return;
        await LoadJarsForDeveloperAsync(_developers[devList.SelectedIndex]);
    }

    private void ApplyThemeSettings(Settings s)
    {
        try
        {
            var app = Application.Current;
            if (app == null) return;

            if (s.FollowSystemTheme || s.PreferredTheme == ThemePreference.System)
            {
                app.RequestedThemeVariant = null;
            }
            else
            {
                app.RequestedThemeVariant = s.PreferredTheme == ThemePreference.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
            }
        }
        catch
        {
            // ignore theme application errors
        }
    }

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        if (devList.SelectedIndex < 0) return;
        await LoadJarsForDeveloperAsync(_developers[devList.SelectedIndex]);
    }

    private async void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        var target = this.FindControl<TextBox>("TargetDirTextBox")!.Text;
        if (string.IsNullOrEmpty(target))
        {
            var jarsList = this.FindControl<ListBox>("JarsListBox")!;
            jarsList.ItemsSource = new List<TrackedScript> { new TrackedScript { RelativePath = "Please choose a target directory first." } };
            return;
        }
        var installBtn = this.FindControl<Button>("InstallButton");
        if (installBtn != null) installBtn.IsEnabled = false;

        var jarsList2 = this.FindControl<ListBox>("JarsListBox")!;
        var jarsItems = jarsList2.Items?.Cast<object>().OfType<TrackedScript>().Where(j => j.IsSelected).ToList() ?? new List<TrackedScript>();
        int total = jarsItems.Count;
        if (total == 0)
        {
            SetStatus("No scripts selected.");
            if (installBtn != null) installBtn.IsEnabled = true;
            return;
        }

        for (int i = 0; i < total; i++)
        {
            var jar = jarsItems[i];
            try
            {
                SetStatus($"Installing {Path.GetFileName(jar.RelativePath)} ({i + 1}/{total})", indeterminate: false, progress: (double)(i) / Math.Max(1, total));
                await _gitService.DownloadJarAsync(jar.RepoUrl, jar.RelativePath, target);
                // update saved state to this commit
                _savedState[StateKey(jar.RepoUrl, jar.RelativePath)] = jar.CommitId ?? string.Empty;
                SetStatus($"Installed {Path.GetFileName(jar.RelativePath)} ({i + 1}/{total})", indeterminate: false, progress: (double)(i + 1) / Math.Max(1, total));
            }
            catch (Exception ex)
            {
                // append error item to the list
                var list = jarsList2.Items?.Cast<object>().ToList() ?? new List<object>();
                list.Add(new TrackedScript { RelativePath = "Download error: " + ex.Message });
                jarsList2.ItemsSource = list.Cast<TrackedScript>().ToList();
                SetStatus("Error installing: " + ex.Message);
            }
        }

        await _persistence.SaveStateAsync(_savedState);
        // refresh view
        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        if (devList.SelectedIndex >= 0)
            await LoadJarsForDeveloperAsync(_developers[devList.SelectedIndex]);

        SetStatus($"Installed {total} script(s).", indeterminate: false, progress: 1);
        if (installBtn != null) installBtn.IsEnabled = true;
    }
}
