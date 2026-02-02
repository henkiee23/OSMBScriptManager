using System;
using System.IO;

namespace OSMBScriptManager.Models;

public class TrackedScript
{
    public string RepoUrl { get; set; } = string.Empty;
    // Path relative to repo root
    public string RelativePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(RelativePath);
    public string CommitId { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public string Status { get; set; } = string.Empty;
}
