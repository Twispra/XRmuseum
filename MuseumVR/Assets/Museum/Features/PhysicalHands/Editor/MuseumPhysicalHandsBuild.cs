#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.PhysicalHands.EditorTools
{
    /// <summary>Builds the saved PC VR scene. Never invokes generators, saves scenes, or changes packages.</summary>
    public static class MuseumPhysicalHandsBuild
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string RequiredUnity = "2022.3.62f3c1";
        const string SettingsPath = "MuseumVR/ProjectSettings/ProjectSettings.asset";
        const string BuildSettingsPath = "MuseumVR/ProjectSettings/EditorBuildSettings.asset";
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));

        [Serializable] sealed class Source
        {
            public string path, beforeSha256, afterSha256;
            public bool unchanged;
        }
        [Serializable] sealed class Restoration
        {
            public string path, beforeSha256, afterBuildSha256, restoredSha256, error = "";
            public bool buildChangedBytes, restored;
        }
        [Serializable] sealed class SceneEntry { public string path, guid; public bool enabled; }
        [Serializable] sealed class Report
        {
            public string status = "running", failure = "", utc, runId, commit, branch, dirtyBefore, dirtyAfter, unity, environment;
            public string scene = ScenePath, target = "StandaloneWindows64", options = "Development", activeTargetBefore, activeTargetAfter;
            public string method = "BuildPipeline.BuildPlayer from the saved MuseumStart scene only. No Setup/Builder calls, scene saves, target migration, package changes, or EditorBuildSettings scene-list changes are requested. Build-time preloaded-asset cache and protected settings bytes are restored afterward.";
            public string limits = "A successful Development build proves only this recorded checkout produced the reported Windows executable. The executable is not launched by this runner. Physical headset/controller tracking, OpenXR runtime behavior, hand comfort, device performance, stereo rendering, accessibility, and user research remain unverified.";
            public string exePath, exeSha256 = "MISSING", buildResult = "not started";
            public int totalErrors, totalWarnings;
            public long totalBytes;
            public double buildDurationSeconds, durationSeconds;
            public bool preloadedAssetsChanged, preloadedAssetsRestored, editorSceneListChanged, editorSceneListRestored;
            public List<SceneEntry> editorScenesBefore = new List<SceneEntry>();
            public List<Source> sources = new List<Source>();
            public List<Restoration> restorations = new List<Restoration>();
            public List<string> errors = new List<string>(), warnings = new List<string>();
        }

        // Run in a dedicated Editor batch process; omit -quit so the queued exit owns the result code.
        public static void RunBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run this entry point in a dedicated Unity batch process; it exits that Editor after writing the report.");
            var started = DateTime.UtcNow;
            string runId = started.ToString("yyyyMMddTHHmmssfffZ") + "-windows-development-" + Guid.NewGuid().ToString("N");
            string outputBase = Environment.GetEnvironmentVariable("MUSEUM_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputBase)) outputBase = Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs");
            string output = Path.Combine(Path.GetFullPath(outputBase), runId);
            Directory.CreateDirectory(output);
            var report = new Report
            {
                utc = started.ToString("o"), runId = runId, commit = Git("rev-parse HEAD"), branch = Git("branch --show-current"),
                dirtyBefore = Git("-c core.quotepath=false status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion,
                activeTargetBefore = EditorUserBuildSettings.activeBuildTarget.ToString(),
                environment = SystemInfo.operatingSystem + "; " + SystemInfo.processorType + "; " + SystemInfo.graphicsDeviceName
            };
            byte[] settingsBytes = null, buildSettingsBytes = null;
            UnityEngine.Object[] originalPreloaded = null;
            EditorBuildSettingsScene[] originalScenes = null;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run Windows builds from Edit Mode.");
                Require(!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty),
                    "Save or discard existing scene edits before building; this entry point does not save them.");
                Require(Application.unityVersion == RequiredUnity, "Use the project's existing Unity " + RequiredUnity + ".");
                Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64,
                    "Start the batch Editor with -buildTarget Win64; this entry point does not switch the project's active target.");
                Require(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64),
                    "This Unity installation needs Windows Build Support.");
                Require(File.Exists(Path.Combine(Application.dataPath, "Museum/Scenes/MuseumStart.unity")) && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                    "The saved MuseumStart scene must exist and import successfully.");
                report.exePath = ResolveExe(runId);
                Require(!File.Exists(report.exePath), "The output executable already exists; provide a fresh MUSEUM_BUILD_OUTPUT path.");
                Directory.CreateDirectory(Path.GetDirectoryName(report.exePath));
                settingsBytes = File.ReadAllBytes(Path.Combine(RepoRoot, SettingsPath));
                buildSettingsBytes = File.ReadAllBytes(Path.Combine(RepoRoot, BuildSettingsPath));
                originalPreloaded = PlayerSettings.GetPreloadedAssets();
                originalScenes = EditorBuildSettings.scenes.Select(s => new EditorBuildSettingsScene(s.path, s.enabled)).ToArray();
                report.editorScenesBefore = ReadSceneEntries();
                CaptureSources(report);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = report.exePath,
                    target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone, subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = BuildOptions.Development
                };
                var build = BuildPipeline.BuildPlayer(options);
                Require(build != null, "BuildPipeline returned no BuildReport.");
                var summary = build.summary;
                report.buildResult = summary.result.ToString();
                report.totalErrors = checked((int)summary.totalErrors); report.totalWarnings = checked((int)summary.totalWarnings);
                report.totalBytes = checked((long)summary.totalSize); report.buildDurationSeconds = summary.totalTime.TotalSeconds;
                foreach (var step in build.steps)
                foreach (var message in step.messages)
                {
                    string detail = step.name + ": " + message.content;
                    if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Assert) report.errors.Add(detail);
                    else if (message.type == LogType.Warning) report.warnings.Add(detail);
                }
                Require(summary.result == BuildResult.Succeeded, "Windows Development build result: " + summary.result);
                Require(File.Exists(report.exePath), "Build succeeded without producing the requested executable.");
                report.exeSha256 = HashFile(report.exePath);
            }
            catch (Exception exception)
            {
                report.failure = exception.ToString(); report.errors.Add(exception.Message);
            }
            finally
            {
                // Capture actual post-build bytes before cache restoration setters can rewrite them.
                string settingsAfterBuild = null, buildSettingsAfterBuild = null;
                if (settingsBytes != null) settingsAfterBuild = HashFile(Path.Combine(RepoRoot, SettingsPath));
                if (buildSettingsBytes != null) buildSettingsAfterBuild = HashFile(Path.Combine(RepoRoot, BuildSettingsPath));
                // Restoring the native cache as well as the bytes prevents a later Editor
                // save/exit from serializing build-only preloaded assets into the source file.
                if (originalPreloaded != null)
                {
                    try
                    {
                        report.preloadedAssetsChanged = !PlayerSettings.GetPreloadedAssets().SequenceEqual(originalPreloaded);
                        if (report.preloadedAssetsChanged) PlayerSettings.SetPreloadedAssets(originalPreloaded);
                        report.preloadedAssetsRestored = PlayerSettings.GetPreloadedAssets().SequenceEqual(originalPreloaded);
                        if (!report.preloadedAssetsRestored) report.errors.Add("The original preloaded-asset cache was not restored.");
                    }
                    catch (Exception exception) { report.errors.Add("Preloaded-asset cache restore: " + exception); }
                }
                if (originalScenes != null)
                {
                    try
                    {
                        report.editorSceneListChanged = !SameScenes(report.editorScenesBefore, ReadSceneEntries());
                        if (report.editorSceneListChanged) EditorBuildSettings.scenes = originalScenes;
                        report.editorSceneListRestored = SameScenes(report.editorScenesBefore, ReadSceneEntries());
                        if (!report.editorSceneListRestored) report.errors.Add("The original EditorBuildSettings scene list was not restored.");
                    }
                    catch (Exception exception) { report.errors.Add("EditorBuildSettings scene-list restore: " + exception); }
                }
                if (settingsBytes != null) RestoreBytes(SettingsPath, settingsBytes, settingsAfterBuild, report);
                if (buildSettingsBytes != null) RestoreBytes(BuildSettingsPath, buildSettingsBytes, buildSettingsAfterBuild, report);
                foreach (var source in report.sources)
                {
                    try
                    {
                        source.afterSha256 = HashFile(Path.Combine(RepoRoot, source.path));
                        source.unchanged = source.beforeSha256 == source.afterSha256;
                        if (!source.unchanged) report.errors.Add("Source changed during build: " + source.path);
                    }
                    catch (Exception exception) { report.errors.Add("Source verification: " + source.path + ": " + exception); }
                }
                report.activeTargetAfter = EditorUserBuildSettings.activeBuildTarget.ToString();
                if (report.activeTargetBefore != report.activeTargetAfter) report.errors.Add("The active build target changed during the build.");
                report.dirtyAfter = Git("-c core.quotepath=false status --porcelain=v1 --untracked-files=all");
                report.durationSeconds = (DateTime.UtcNow - started).TotalSeconds;
                report.errors = report.errors.Distinct().ToList(); report.warnings = report.warnings.Distinct().ToList();
                report.status = report.buildResult == BuildResult.Succeeded.ToString() && string.IsNullOrEmpty(report.failure) && report.totalErrors == 0 && report.errors.Count == 0 ? "passed" : "failed";
                string path = Path.Combine(output, "report.json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("PHYSICAL_HANDS_WINDOWS_BUILD_" + report.status.ToUpperInvariant() + " " + path);
                int exitCode = report.status == "passed" ? 0 : 1;
                EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
            }
        }

        static string ResolveExe(string runId)
        {
            string requested = Environment.GetEnvironmentVariable("MUSEUM_BUILD_OUTPUT");
            if (string.IsNullOrWhiteSpace(requested)) requested = Path.Combine(RepoRoot, "Builds/Windows", runId, "MuseumVR.exe");
            bool driveRooted = requested.Length >= 3 && char.IsLetter(requested[0]) && requested[1] == ':' && (requested[2] == '\\' || requested[2] == '/');
            Require(driveRooted || requested.StartsWith("\\\\", StringComparison.Ordinal), "MUSEUM_BUILD_OUTPUT must be an absolute Windows executable path.");
            string full = Path.GetFullPath(requested);
            Require(string.Equals(Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase), "MUSEUM_BUILD_OUTPUT must end in .exe.");
            foreach (string folder in new[] { Application.dataPath, Path.Combine(Application.dataPath, "../Packages"), Path.Combine(Application.dataPath, "../ProjectSettings") })
            {
                string sourceRoot = Path.GetFullPath(folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                Require(!full.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase), "Build output must be outside Assets, Packages, and ProjectSettings.");
            }
            return full;
        }
        static List<SceneEntry> ReadSceneEntries() => EditorBuildSettings.scenes.Select(s => new SceneEntry { path = s.path, enabled = s.enabled, guid = s.guid.ToString() }).ToList();
        static bool SameScenes(List<SceneEntry> first, List<SceneEntry> second)
        {
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++) if (first[i].path != second[i].path || first[i].guid != second[i].guid || first[i].enabled != second[i].enabled) return false;
            return true;
        }
        static void RestoreBytes(string relative, byte[] original, string afterBuildHash, Report report)
        {
            var result = new Restoration { path = relative, beforeSha256 = HashBytes(original) };
            report.restorations.Add(result);
            try
            {
                string absolute = Path.Combine(RepoRoot, relative);
                result.afterBuildSha256 = afterBuildHash ?? HashFile(absolute);
                result.buildChangedBytes = result.afterBuildSha256 != result.beforeSha256;
                if (HashFile(absolute) != result.beforeSha256) File.WriteAllBytes(absolute, original);
                result.restoredSha256 = HashFile(absolute); result.restored = result.restoredSha256 == result.beforeSha256;
                if (!result.restored) report.errors.Add("Protected settings bytes were not restored: " + relative);
            }
            catch (Exception exception) { result.error = exception.ToString(); report.errors.Add("Protected settings restore: " + relative + ": " + exception); }
        }
        static void CaptureSources(Report report)
        {
            // Hash the imported source tree, including binary assets, without inspecting
            // image/model content. Library/cache/build files are outside these folders.
            foreach (string folder in new[] { Application.dataPath, Path.Combine(Application.dataPath, "../Packages"), Path.Combine(Application.dataPath, "../ProjectSettings") })
            foreach (string file in Directory.GetFiles(Path.GetFullPath(folder), "*", SearchOption.AllDirectories).OrderBy(p => p))
                report.sources.Add(new Source { path = file.Substring(RepoRoot.Length + 1).Replace('\\', '/'), beforeSha256 = HashFile(file) });
        }
        static string HashFile(string path)
        {
            if (!File.Exists(path)) return "MISSING";
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return ConvertHash(sha.ComputeHash(stream));
        }
        static string HashBytes(byte[] value) { using (var sha = SHA256.Create()) return ConvertHash(sha.ComputeHash(value)); }
        static string ConvertHash(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static string Git(string arguments)
        {
            try
            {
                using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = RepoRoot, UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true, RedirectStandardError = true
                }))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(5000)) { process.Kill(); return "UNAVAILABLE: git timeout"; }
                    return process.ExitCode == 0 ? stdout.Result.Trim() : "UNAVAILABLE: " + stderr.Result.Trim();
                }
            }
            catch (Exception exception) { return "UNAVAILABLE: " + exception.Message; }
        }
    }
}
#endif
