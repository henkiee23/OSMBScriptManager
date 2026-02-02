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

namespace OSMBScriptManager;

public partial class MainWindow : Window
{
    private readonly GitRepoService _gitService = new();
    private readonly PersistenceService _persistence = new();
    private readonly DeveloperConfigService _devConfig = new();
    private readonly SettingsService _settingsService = new();
    private Dictionary<string, string> _savedState = new();
    private List<PluginDeveloper> _developers = new();
    private readonly Dictionary<string, List<TrackedJar>> _repoJars = new();

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
            _developers = new List<PluginDeveloper>
            {
                new PluginDeveloper { Name = "JustDavyy", RepoUrl = "https://github.com/JustDavyy/osmb-scripts.git", PathRegex = ".*\\.jar$" },
                new PluginDeveloper { Name = "Butter", RepoUrl = "https://github.com/ButterB21/Butter-Scripts.git", PathRegex = ".*\\.jar$" },
                new PluginDeveloper { Name = "Jose", RepoUrl = "https://github.com/joseOSMB/JOSE-OSMB-SCRIPTS.git", PathRegex = ".*\\.jar$" },
                new PluginDeveloper { Name = "Fru", RepoUrl = "https://github.com/fru-art/fru-scripts.git", PathRegex = ".*\\.jar$" }
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

        // Show font registration report (if any) in the status area for diagnostics
        try
        {
            var report = Application.Current?.Resources?[(object)"FontRegistrationReport"] as string;
            if (!string.IsNullOrEmpty(report))
            {
                SetStatus("Fonts: " + report);
                // keep it visible briefly then clear
                _ = Task.Run(async () => { await Task.Delay(3000); ClearStatus(); });
            }
        }
        catch { }

        // save target dir when user edits the textbox
        this.FindControl<TextBox>("TargetDirTextBox")!.LostFocus += TargetDirTextBox_LostFocus;

        // wire installed plugins buttons
        this.FindControl<Button>("RefreshInstalledButton")!.Click += RefreshInstalledButton_Click;
        this.FindControl<Button>("UpdateInstalledButton")!.Click += UpdateInstalledButton_Click;
        this.FindControl<Button>("UpdateAllInstalledButton")!.Click += UpdateAllInstalledButton_Click;

        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        devList.SelectionChanged += DevelopersListBox_SelectionChanged;

        // wire developer management buttons
        this.FindControl<Button>("AddDevButton")!.Click += AddDevButton_Click;
        this.FindControl<Button>("UpdateDevButton")!.Click += UpdateDevButton_Click;
        this.FindControl<Button>("RemoveDevButton")!.Click += RemoveDevButton_Click;
        this.FindControl<Button>("SaveDevsButton")!.Click += SaveDevsButton_Click;

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

        // start background fetch of all repos (cache only)
        _ = Task.Run(async () => await BackgroundFetchAllReposAsync());
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
        // populate edit fields
        this.FindControl<TextBox>("DevNameTextBox")!.Text = dev.Name;
        this.FindControl<TextBox>("DevRepoTextBox")!.Text = dev.RepoUrl;
        this.FindControl<TextBox>("DevRegexTextBox")!.Text = dev.PathRegex;

        await LoadJarsForDeveloperAsync(dev);
    }

    private async void AddDevButton_Click(object? sender, RoutedEventArgs e)
    {
        var name = this.FindControl<TextBox>("DevNameTextBox")!.Text?.Trim() ?? string.Empty;
        var repo = this.FindControl<TextBox>("DevRepoTextBox")!.Text?.Trim() ?? string.Empty;
        var regex = this.FindControl<TextBox>("DevRegexTextBox")!.Text?.Trim() ?? ".*\\.jar$";
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(repo))
            return;

        var dev = new PluginDeveloper { Name = name, RepoUrl = repo, PathRegex = regex };
        _developers.Add(dev);
        PopulateDevelopersList();
        await _devConfig.SaveAsync(_developers);
    }

    private async void UpdateDevButton_Click(object? sender, RoutedEventArgs e)
    {
        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        if (devList.SelectedIndex < 0) return;
        var idx = devList.SelectedIndex;
        var name = this.FindControl<TextBox>("DevNameTextBox")!.Text?.Trim() ?? string.Empty;
        var repo = this.FindControl<TextBox>("DevRepoTextBox")!.Text?.Trim() ?? string.Empty;
        var regex = this.FindControl<TextBox>("DevRegexTextBox")!.Text?.Trim() ?? ".*\\.jar$";
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(repo))
            return;

        _developers[idx].Name = name;
        _developers[idx].RepoUrl = repo;
        _developers[idx].PathRegex = regex;
        PopulateDevelopersList();
        await _devConfig.SaveAsync(_developers);
    }

    private async void RemoveDevButton_Click(object? sender, RoutedEventArgs e)
    {
        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        if (devList.SelectedIndex < 0) return;
        var idx = devList.SelectedIndex;
        _developers.RemoveAt(idx);
        PopulateDevelopersList();
        this.FindControl<TextBox>("DevNameTextBox")!.Text = string.Empty;
        this.FindControl<TextBox>("DevRepoTextBox")!.Text = string.Empty;
        this.FindControl<TextBox>("DevRegexTextBox")!.Text = ".*\\.jar$";
        await _devConfig.SaveAsync(_developers);
    }

    private async void SaveDevsButton_Click(object? sender, RoutedEventArgs e)
    {
        await _devConfig.SaveAsync(_developers);
    }

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
            installed.ItemsSource = new List<InstalledPlugin> { new InstalledPlugin { FileName = "Please choose a target directory first." } };
            return;
        }

        var installedList = this.FindControl<ListBox>("InstalledListBox")!;
        var toUpdate = installedList.Items?.Cast<object>().OfType<InstalledPlugin>().Where(p => p.IsSelected && p.Matched).ToList() ?? new List<InstalledPlugin>();
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
                list.Add(new InstalledPlugin { FileName = "Update error: " + ex.Message });
                installedList.ItemsSource = list.Cast<InstalledPlugin>().ToList();
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
            installed.ItemsSource = new List<InstalledPlugin> { new InstalledPlugin { FileName = "Please choose a target directory first." } };
            return;
        }

        var installedList = this.FindControl<ListBox>("InstalledListBox")!;
        var toUpdate = installedList.Items?.Cast<object>().OfType<InstalledPlugin>().Where(p => p.Matched).ToList() ?? new List<InstalledPlugin>();
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
                list.Add(new InstalledPlugin { FileName = "Update error: " + ex.Message });
                installedList.ItemsSource = list.Cast<InstalledPlugin>().ToList();
            }
        }

        await _persistence.SaveStateAsync(_savedState);
        await ScanInstalledPluginsAsync();
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

    private async Task LoadJarsForDeveloperAsync(PluginDeveloper dev)
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
            var err = new TrackedJar { RelativePath = "Error scanning repo: " + ex.Message, CommitId = string.Empty, Status = "" };
            jarsList.ItemsSource = new List<TrackedJar> { err };
            SetStatus("Error scanning repo: " + ex.Message);
        }
    }

    private async Task ScanInstalledPluginsAsync()
    {
        var target = this.FindControl<TextBox>("TargetDirTextBox")!.Text;
        var installedList = this.FindControl<ListBox>("InstalledListBox")!;
        if (string.IsNullOrEmpty(target) || !Directory.Exists(target))
        {
            installedList.ItemsSource = new List<InstalledPlugin> { new InstalledPlugin { FileName = "Please choose a valid target directory." } };
            return;
        }

        SetStatus("Scanning installed plugins...", indeterminate: true);

        var files = Directory.EnumerateFiles(target, "*.jar", SearchOption.TopDirectoryOnly).ToList();
        var installed = new List<InstalledPlugin>();
        foreach (var f in files)
        {
            var fileName = Path.GetFileName(f);
            var p = new InstalledPlugin { FileName = fileName, FullPath = f };

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
            jarsList.ItemsSource = new List<TrackedJar> { new TrackedJar { RelativePath = "Please choose a target directory first." } };
            return;
        }

        var jarsList2 = this.FindControl<ListBox>("JarsListBox")!;
        var jarsItems = jarsList2.Items?.Cast<object>().OfType<TrackedJar>().Where(j => j.IsSelected).ToList() ?? new List<TrackedJar>();
        foreach (var jar in jarsItems)
        {
            try
            {
                await _gitService.DownloadJarAsync(jar.RepoUrl, jar.RelativePath, target);
                // update saved state to this commit
                _savedState[StateKey(jar.RepoUrl, jar.RelativePath)] = jar.CommitId ?? string.Empty;
            }
            catch (Exception ex)
            {
                // append error item to the list
                var list = jarsList2.Items?.Cast<object>().ToList() ?? new List<object>();
                list.Add(new TrackedJar { RelativePath = "Download error: " + ex.Message });
                jarsList2.ItemsSource = list.Cast<TrackedJar>().ToList();
            }
        }

        await _persistence.SaveStateAsync(_savedState);
        // refresh view
        var devList = this.FindControl<ListBox>("DevelopersListBox")!;
        if (devList.SelectedIndex >= 0)
            await LoadJarsForDeveloperAsync(_developers[devList.SelectedIndex]);
    }
}
