using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
        // At startup, attempt to register embedded Font Awesome OTFs as FontFamily resources.
        // Only register a FontFamily if we can successfully create a Typeface and access its GlyphTypeface.
        try
        {
            var assetBase = "avares://OSMBScriptManager/Resources/Fonts/";

            var probes = new List<(string file, string[] familyNames, string resourceKey)>
            {
                ("fa-solid-900.otf", new[]{"Font Awesome 6 Free Solid","Font Awesome 6 Free"}, "FAFreeSolid"),
                ("fa-regular-400.otf", new[]{"Font Awesome 6 Free Regular","Font Awesome 6 Free"}, "FAFreeRegular"),
                ("fa-brands-400.otf", new[]{"Font Awesome 6 Brands Regular","Font Awesome 6 Brands"}, "FABrands")
            };

            var reportLines = new List<string>();



            foreach (var probe in probes)
            {
                var uri = assetBase + probe.file;
                foreach (var family in probe.familyNames)
                {
                    try
                    {
                        var ff = new FontFamily(uri + "#" + family);
                        var tf = new Typeface(ff, FontStyle.Normal, FontWeight.Normal);
                        // Access GlyphTypeface to force loading; if this succeeds we register the family.
                        var gt = tf.GlyphTypeface;
                        if (gt != null)
                        {
                            // try to probe any available name properties on the GlyphTypeface via reflection
                            var names = new List<string>();
                            try
                            {
                                var prop = gt.GetType().GetProperty("FaceNames") ?? gt.GetType().GetProperty("FamilyNames") ?? gt.GetType().GetProperty("Names");
                                if (prop != null)
                                {
                                    var val = prop.GetValue(gt);
                                    if (val is System.Collections.IDictionary dict)
                                    {
                                        foreach (var obj in dict.Values)
                                        {
                                            var s = obj?.ToString();
                                            if (!string.IsNullOrEmpty(s)) names.Add(s);
                                        }
                                    }
                                    else if (val is System.Collections.IEnumerable en)
                                    {
                                        foreach (var obj in en)
                                        {
                                            var s = obj?.ToString();
                                            if (!string.IsNullOrEmpty(s)) names.Add(s);
                                        }
                                    }
                                }
                            }
                            catch { }

                            this.Resources[probe.resourceKey] = ff;
                            if (!this.Resources.ContainsKey("FontAwesome") && probe.resourceKey == "FAFreeSolid")
                                this.Resources["FontAwesome"] = ff;

                            var line = $"Registered {probe.resourceKey} -> {probe.file}" + (names.Count > 0 ? $" (names: {string.Join(", ", names)})" : "");
                            reportLines.Add(line);
                            break;
                        }
                    }
                    catch
                    {
                        // try next family name
                    }
                }
                if (!reportLines.Exists(r => r.Contains(probe.resourceKey)))
                {
                    reportLines.Add($"Failed to register {probe.resourceKey} ({probe.file})");
                }
            }

            // Register icon resources (use FA glyphs if available, otherwise fall back to Unicode symbols)
            bool hasFa = this.Resources.ContainsKey("FAFreeSolid") || this.Resources.ContainsKey("FAFreeRegular");
            if (hasFa)
            {
                this.Resources["IconFontFamily"] = this.Resources.ContainsKey("FAFreeSolid") ? this.Resources["FAFreeSolid"] : this.Resources["FAFreeRegular"];
                this.Resources["IconRefresh"] = "\uf021"; // fa-sync
                this.Resources["IconDownload"] = "\uf019"; // fa-download
                this.Resources["IconCloud"] = "\uf0ed"; // fa-cloud-download
            }
            else
            {
                // fallback unicode glyphs
                this.Resources["IconFontFamily"] = new FontFamily("Segoe UI Symbol");
                this.Resources["IconRefresh"] = "\u21BB"; // ↻
                this.Resources["IconDownload"] = "\u2B07"; // ⬇
                this.Resources["IconCloud"] = "\u2601"; // ☁
            }

            // add report resource
            this.Resources["FontRegistrationReport"] = string.Join(" | ", reportLines);
        }
        catch
        {
            // ignore font registration errors — app should continue without embedded icons
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}