using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OSMBScriptManager.Models;

namespace OSMBScriptManager.Services;

public class GitRepoService
{
    private static async Task<(int exitCode, string output, string error)> RunGit(string workingDir, string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };

        using var p = Process.Start(psi)!;
        string outp = await p.StandardOutput.ReadToEndAsync();
        string err = await p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        return (p.ExitCode, outp.Trim(), err.Trim());
    }

    // Clones the repo to a temporary folder and returns the path to the clone
    private static async Task<string> CloneRepoToTemp(string repoUrl)
    {
        var temp = Path.Combine(Path.GetTempPath(), "osmb_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        // Use a shallow clone to reduce network and disk usage
        var (code, outp, err) = await RunGit(Directory.GetCurrentDirectory(), $"clone --depth 1 \"{repoUrl}\" \"{temp}\"");
        if (code != 0)
        {
            Directory.Delete(temp, true);
            throw new Exception($"git clone failed: {err}");
        }
        return temp;
    }

    public async Task<List<TrackedScript>> ScanRepoAsync(Models.ScriptDeveloper dev)
    {
        var result = new List<TrackedScript>();
        string temp = await CloneRepoToTemp(dev.RepoUrl);
        try
        {
            var regex = new Regex(dev.PathRegex, RegexOptions.IgnoreCase);
            var files = Directory.EnumerateFiles(temp, "*.jar", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                var rel = Path.GetRelativePath(temp, f).Replace("\\", "/");
                if (!regex.IsMatch(rel))
                    continue;

                // get last commit for the file
                var (code, outp, err) = await RunGit(temp, $"log -n 1 --pretty=format:%H -- \"{rel}\"");
                string commit = code == 0 ? outp.Split('\n')[0] : string.Empty;

                result.Add(new TrackedScript
                {
                    RepoUrl = dev.RepoUrl,
                    RelativePath = rel,
                    CommitId = commit,
                    Status = string.IsNullOrEmpty(commit) ? "Unknown" : "Found"
                });
            }
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }

        return result;
    }

    public async Task DownloadJarAsync(string repoUrl, string relativePath, string targetDirectory)
    {
        string temp = await CloneRepoToTemp(repoUrl);
        try
        {
            var source = Path.Combine(temp, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
                throw new FileNotFoundException("File not found in cloned repo", source);

            Directory.CreateDirectory(targetDirectory);
            var dest = Path.Combine(targetDirectory, Path.GetFileName(source));
            File.Copy(source, dest, true);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}
