using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia;
using System.Threading.Tasks;
using System;

namespace OSMBScriptManager.Views;

public class UpdateDialog : Window
{
    public UpdateDialog(string title, string message, string version)
    {
        Title = title;
        Width = 460;
        Height = 160;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };
        panel.Children.Add(text);

        var skipCheckbox = new CheckBox { Content = "Don't check again for this version", Margin = new Thickness(0,6,0,0) };
        panel.Children.Add(skipCheckbox);

        var progress = new ProgressBar { IsVisible = false, Minimum = 0, Maximum = 100, Height = 16, Margin = new Thickness(0,6,0,0) };
        var status = new TextBlock { Text = string.Empty, FontSize = 12, Margin = new Thickness(0,4,0,0) };
        panel.Children.Add(progress);
        panel.Children.Add(status);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var later = new Button { Content = "Later", Width = 90 };
        var install = new Button { Content = $"Install {version}", Width = 140 };
        later.Click += (_, __) => { SkipThisVersion = skipCheckbox.IsChecked == true; DialogResult = false; Close(false); };
        install.Click += (_, __) => { SkipThisVersion = skipCheckbox.IsChecked == true; DialogResult = true; Close(true); };
        btnPanel.Children.Add(later);
        btnPanel.Children.Add(install);

        panel.Children.Add(btnPanel);
        Content = panel;

        // expose controls for progress updates
        ProgressBarControl = progress;
        StatusTextControl = status;
        SkipCheckboxControl = skipCheckbox;
    }

    public Task<bool?> ShowDialogAsync(Window owner) => ShowDialog<bool?>(owner);

    public bool SkipThisVersion { get; private set; }
    public bool? DialogResult { get; private set; }
    public ProgressBar? ProgressBarControl { get; private set; }
    public TextBlock? StatusTextControl { get; private set; }
    public CheckBox? SkipCheckboxControl { get; private set; }

    public void SetDownloadingState()
    {
        if (ProgressBarControl != null) ProgressBarControl.IsVisible = true;
        if (StatusTextControl != null) StatusTextControl.Text = "Downloading...";
    }

    public void UpdateProgress(double fraction)
    {
        if (ProgressBarControl != null) ProgressBarControl.Value = Math.Min(100, Math.Max(0, fraction * 100));
        if (StatusTextControl != null) StatusTextControl.Text = $"Downloading... {Math.Round(fraction * 100)}%";
    }
}
