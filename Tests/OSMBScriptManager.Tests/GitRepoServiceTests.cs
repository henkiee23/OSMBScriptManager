using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OSMBScriptManager.Models;
using OSMBScriptManager.Services;
using Xunit;

namespace OSMBScriptManager.Tests;

public class GitRepoServiceTests
{
    private static void RunGit(string workingDir, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };

        using var p = Process.Start(psi)!;
        string outp = p.StandardOutput.ReadToEnd();
        string err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new Exception($"git failed ({args}): {err}\n{outp}");
    }

    [Fact]
    public async Task ScanRepoAndDownloadJar_LocalRepoIntegration()
    {
        var repoDir = Path.Combine(Path.GetTempPath(), "git_repo_test_" + Guid.NewGuid().ToString("N"));
        var cloneTarget = Path.Combine(Path.GetTempPath(), "git_clone_target_" + Guid.NewGuid().ToString("N"));
        var downloadTarget = Path.Combine(Path.GetTempPath(), "git_download_target_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoDir);
        Directory.CreateDirectory(cloneTarget);
        Directory.CreateDirectory(downloadTarget);

        try
        {
            // init repo
            RunGit(repoDir, "init");
            RunGit(repoDir, "config user.email \"test@example.com\"");
            RunGit(repoDir, "config user.name \"Tester\"");

            // create a nested folder and a jar file
            var libs = Path.Combine(repoDir, "libs");
            Directory.CreateDirectory(libs);
            var jarPath = Path.Combine(libs, "example.jar");
            var content = new byte[128];
            new Random(123).NextBytes(content);
            File.WriteAllBytes(jarPath, content);

            RunGit(repoDir, "add .");
            RunGit(repoDir, "commit -m \"add jar\"");

            var svc = new GitRepoService();

            var dev = new ScriptDeveloper { Name = "Local", RepoUrl = repoDir, PathRegex = ".*\\.jar$" };

            var found = await svc.ScanRepoAsync(dev);

            Assert.Single(found);
            var tracked = found.First();
            Assert.EndsWith("libs/example.jar", tracked.RelativePath.Replace("\\", "/"));
            Assert.Equal(repoDir, tracked.RepoUrl);
            Assert.False(string.IsNullOrEmpty(tracked.CommitId));

            // test download
            await svc.DownloadJarAsync(repoDir, tracked.RelativePath, downloadTarget);
            var downloaded = Directory.GetFiles(downloadTarget).FirstOrDefault();
            Assert.NotNull(downloaded);
            var bytes = File.ReadAllBytes(downloaded!);
            Assert.Equal(content.Length, bytes.Length);
        }
        finally
        {
            try { Directory.Delete(repoDir, true); } catch { }
            try { Directory.Delete(cloneTarget, true); } catch { }
            try { Directory.Delete(downloadTarget, true); } catch { }
        }
    }
}
