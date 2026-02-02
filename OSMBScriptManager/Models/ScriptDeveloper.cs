using System;

namespace OSMBScriptManager.Models;

public class ScriptDeveloper
{
    public string Name { get; set; } = string.Empty;
    public string RepoUrl { get; set; } = string.Empty;
    // A regex to match relative paths inside the repo that point to .jar files
    public string PathRegex { get; set; } = ".*\\.jar$";
}
