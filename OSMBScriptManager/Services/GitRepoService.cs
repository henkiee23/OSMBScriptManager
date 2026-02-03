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
        
        try
        {
            // Use sparse checkout to only get .jar files - much faster for large repos
            // Initialize empty repo
            var (initCode, _, initErr) = await RunGit(temp, "init");
            if (initCode != 0) throw new Exception($"git init failed: {initErr}");
            
            // Configure sparse checkout
            var (configCode, _, configErr) = await RunGit(temp, "config core.sparseCheckout true");
            if (configCode != 0) throw new Exception($"git config failed: {configErr}");
            
            // Set sparse checkout pattern to only include .jar files
            var sparseFile = Path.Combine(temp, ".git", "info", "sparse-checkout");
            Directory.CreateDirectory(Path.GetDirectoryName(sparseFile)!);
            await File.WriteAllTextAsync(sparseFile, "*.jar\n**/*.jar\n");
            
            // Add remote and fetch with depth 1
            var (remoteCode, _, remoteErr) = await RunGit(temp, $"remote add origin \"{repoUrl}\"");
            if (remoteCode != 0) throw new Exception($"git remote add failed: {remoteErr}");
            
            var (fetchCode, _, fetchErr) = await RunGit(temp, "fetch --depth 1 origin");
            if (fetchCode != 0) throw new Exception($"git fetch failed: {fetchErr}");
            
            // Checkout the default branch
            var (checkoutCode, _, checkoutErr) = await RunGit(temp, "checkout FETCH_HEAD");
            if (checkoutCode != 0) throw new Exception($"git checkout failed: {checkoutErr}");
        }
        catch
        {
            // If sparse checkout fails, fall back to regular shallow clone
            try { Directory.Delete(temp, true); } catch { }
            Directory.CreateDirectory(temp);
            
            var (code, outp, err) = await RunGit(Directory.GetCurrentDirectory(), $"clone --depth 1 \"{repoUrl}\" \"{temp}\"");
            if (code != 0)
            {
                Directory.Delete(temp, true);
                throw new Exception($"git clone failed: {err}");
            }
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
            var matchedFiles = new List<string>();
            
            foreach (var f in files)
            {
                var rel = Path.GetRelativePath(temp, f).Replace("\\", "/");
                if (regex.IsMatch(rel))
                {
                    matchedFiles.Add(rel);
                }
            }

            // Batch git log: get all file commits in one command
            var commitMap = await GetFileCommitsBatch(temp, matchedFiles);

            foreach (var rel in matchedFiles)
            {
                string commit = string.Empty;
                string commitDate = string.Empty;
                
                if (commitMap.TryGetValue(rel, out var commitInfo))
                {
                    commit = commitInfo.Item1;
                    commitDate = commitInfo.Item2;
                }

                result.Add(new TrackedScript
                {
                    RepoUrl = dev.RepoUrl,
                    RelativePath = rel,
                    CommitId = commit,
                    CommitDate = commitDate,
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

    // Batch get commits for multiple files in a single git command
    private static async Task<Dictionary<string, (string commit, string date)>> GetFileCommitsBatch(string repoPath, List<string> files)
    {
        var result = new Dictionary<string, (string, string)>();
        if (files.Count == 0) return result;

        // Use git log with --name-only to get commits for all files efficiently
        // Format: commit hash|date|filepath
        var (code, outp, err) = await RunGit(repoPath, "log --all --name-only --pretty=format:%H|%cs");
        
        if (code != 0) return result;

        var lines = outp.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? currentCommit = null;
        string? currentDate = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Check if it's a commit line (contains |)
            if (trimmed.Contains('|'))
            {
                var parts = trimmed.Split('|', 2);
                if (parts.Length == 2)
                {
                    currentCommit = parts[0];
                    currentDate = parts[1];
                }
            }
            else if (currentCommit != null && currentDate != null)
            {
                // It's a file path - check if we need this file and haven't recorded it yet
                var normalizedPath = trimmed.Replace("\\", "/");
                if (files.Contains(normalizedPath) && !result.ContainsKey(normalizedPath))
                {
                    result[normalizedPath] = (currentCommit, currentDate);
                }
            }
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
