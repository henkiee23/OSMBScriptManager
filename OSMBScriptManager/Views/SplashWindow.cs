using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;

namespace OSMBScriptManager.Views;

public class SplashWindow : Window
{
    public SplashWindow()
    {
        Title = "OSMBScriptManager";
        Width = 420;
        Height = 140;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var panel = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
        StatusText = new TextBlock { Text = "Checking for updates...", FontSize = 14, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        panel.Children.Add(StatusText);
        Progress = new ProgressBar { IsIndeterminate = true, Margin = new Thickness(0,10,0,0), Minimum = 0, Maximum = 100 };
        panel.Children.Add(Progress);
        Content = panel;
        Topmost = true;
    }

    public ProgressBar? Progress { get; private set; }
    public TextBlock? StatusText { get; private set; }

    public void SetStatus(string text, bool indeterminate = false)
    {
        if (StatusText != null) StatusText.Text = text;
        if (Progress != null) Progress.IsIndeterminate = indeterminate;
    }

    public void UpdateProgress(double fraction)
    {
        if (Progress != null)
        {
            Progress.IsIndeterminate = false;
            Progress.Value = Math.Min(100, Math.Max(0, fraction * 100));
        }
    }
}
