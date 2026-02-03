using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia;
using System;

namespace OSMBScriptManager.Views;

public class ErrorDialog : Window
{
    public ErrorDialog(string title, string message, string details = "")
    {
        Title = title;
        Width = 640;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
        var text = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(4) };
        panel.Children.Add(text);

        if (!string.IsNullOrEmpty(details))
        {
            var scroll = new ScrollViewer { Height = 200 };
            var detailsText = new TextBlock { Text = details, TextWrapping = Avalonia.Media.TextWrapping.NoWrap };
            scroll.Content = detailsText;
            panel.Children.Add(scroll);
        }

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "OK", Width = 90 };
        ok.Click += (_, __) => { Close(); };
        btnPanel.Children.Add(ok);
        panel.Children.Add(btnPanel);

        Content = panel;
    }

    public new void Show()
    {
        base.Show();
    }
}
