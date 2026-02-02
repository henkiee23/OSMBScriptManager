using System;

namespace OSMBScriptManager.Models;

public class InstalledPlugin
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool Matched { get; set; }
    public string RepoUrl { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string RepoCommitId { get; set; } = string.Empty;
    public string SavedCommitId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
