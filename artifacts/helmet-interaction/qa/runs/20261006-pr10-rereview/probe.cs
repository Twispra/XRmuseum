using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Museum.Eras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

// Temporary review probe; copy only into the isolated checkout's Editor directory.
// No generator calls, saved scenes, or asset writes. All modified data is in-memory.
public static class EraReviewProbe
{
    const string Key = "XRmuseum.EraRereviewProbe.";
    const string PrefabPath = "Assets/Museum/Features/HelmetEras/Prefabs/HelmetEraShowcase.prefab";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly FieldInfo Duration = typeof(HelmetEraSwitcher).GetField("transitionDuration", PrivateInstance);
    static readonly FieldInfo Profiles = typeof(HelmetEraCatalogue).GetField("eras", PrivateInstance);
    static readonly FieldInfo Fade = typeof(HelmetEraProfile).GetField("fadeDuringSwap", PrivateInstance);
    static readonly List<ScriptableObject> owned = new List<ScriptableObject>();
    static GameObject fixture;
    static HelmetEraSwitcher switcher;
    static HelmetEraChannel channel;
    static Report report;
    static int phase;
    static double due;

    [Serializable] public sealed class Check { public string name; public bool passed; public string detail; }
    [Serializable] public sealed class Report
    {
        public string startedUtc, finishedUtc, unityVersion, project, failure;
        public string scope = "Unsaved empty scene; instances of saved showcase; cloned in-memory catalogue/profiles and fresh channel. No asset saves, generators or unrelated input probe. Default API/button dispatch; one-second fade interruption by disable, duration=0, target FadeDuringSwap=false; target reselection. No physical XR input or image assertions.";
        public List<Check> checks = new List<Check>();
        public List<string> errors = new List<string>();
        public int passed, failed;
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run from Edit Mode.");
        string output = Argument("-eraReviewOutput");
        if (string.IsNullOrEmpty(output)) throw new ArgumentException("Supply a unique absolute -eraReviewOutput directory.");
        if (!Path.IsPathRooted(output)) throw new ArgumentException("-eraReviewOutput must be absolute.");
        string reportPath = Path.Combine(output, "review-playmode.json");
        if (File.Exists(reportPath)) throw new IOException("Refusing to overwrite an existing review report: " + reportPath);
        Directory.CreateDirectory(output);
        SessionState.SetString(Key + "output", output);
        SessionState.SetString(Key + "mode", "play");
        SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 180f);
        SessionState.SetBool(Key + "active", true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Hook();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume() { if (SessionState.GetBool(Key + "active", false)) Hook(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void PrepareInput()
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>("Era rereview batch keyboard");
        if (Mouse.current == null) InputSystem.AddDevice<Mouse>("Era rereview batch mouse");
    }
    static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    static void Tick()
    {
        if (!SessionState.GetBool(Key + "active", false)) { EditorApplication.update -= Tick; return; }
        if (SessionState.GetString(Key + "mode", "") == "exit")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            float exitAt = SessionState.GetFloat(Key + "exitAt", -1f);
            if (exitAt < 0f) { SessionState.SetFloat(Key + "exitAt", (float)EditorApplication.timeSinceStartup + 20f); return; }
            if (EditorApplication.timeSinceStartup < exitAt) return;
            int code = SessionState.GetInt(Key + "exitCode", 1);
            SessionState.SetBool(Key + "active", false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
            return;
        }
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (report == null)
            {
                report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion, project = Application.dataPath };
                Application.logMessageReceived += Log;
                NewFixture(); Go(0, .5);
            }
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0)) throw new TimeoutException("Probe exceeded 180 seconds.");
            if (EditorApplication.timeSinceStartup < due) return;
            switch (phase)
            {
                case 0:
                    Add("default era applied after Start", Consistent(0), State());
                    switcher.ShowEra(1); Add("default instant ShowEra(1)", Consistent(1), State());
                    switcher.ShowEra(2); Add("default instant ShowEra(2)", Consistent(2), State());
                    Press(HelmetEraButton.StepDirection.Next);
                    Add("saved next button wraps to era zero", Consistent(0), State());
                    Press(HelmetEraButton.StepDirection.Previous);
                    Add("saved previous button wraps to era two", Consistent(2), State());
                    Press(HelmetEraButton.StepDirection.Previous);
                    Add("saved previous button steps to era one", Consistent(1), State());
                    Press(HelmetEraButton.StepDirection.Next);
                    Add("saved next button steps to era two", Consistent(2), State());
                    switcher.ShowEra(0);
                    Duration.SetValue(switcher, 1f); switcher.ShowEra(1); Go(1, .15); break;
                case 1:
                    Add("disable test reached an unfinished fade", switcher.IsSwitching && HasOverride() && Matches(0), State());
                    switcher.enabled = false;
                    Add("disable immediately completes target and clears temporary overrides", Consistent(1), State());
                    switcher.enabled = true;
                    Add("reenable immediately retains consistent state", Consistent(1), State());
                    switcher.ShowEra(1);
                    Add("same-target reselection after reenable remains clear and consistent", Consistent(1), State());
                    Go(2, 1.3); break;
                case 2:
                    Add("disabled transition cannot write later", Consistent(1), State());
                    NewFixture(); Go(3, .5); break;
                case 3:
                    Add("duration-zero test starts settled", Consistent(0), State());
                    Duration.SetValue(switcher, 1f); switcher.ShowEra(1); Go(4, .15); break;
                case 4:
                    Add("duration-zero test reached an unfinished fade", switcher.IsSwitching && HasOverride() && Matches(0), State());
                    Duration.SetValue(switcher, 0f); switcher.ShowEra(2);
                    Add("duration-zero request immediately replaces old transition", Consistent(2), State());
                    Go(5, 1.3); break;
                case 5:
                    Add("duration-zero request survives old transition deadline", Consistent(2), State());
                    switcher.ShowEra(2);
                    Add("same-target reselection after instant replacement has no override", Consistent(2), State());
                    NewFixture(); Go(6, .5); break;
                case 6:
                    Add("no-fade-profile test starts settled", Consistent(0), State());
                    Fade.SetValue(switcher.Catalogue[2], false);
                    Add("no-fade setting belongs to an unsaved cloned profile", !switcher.Catalogue[2].FadeDuringSwap && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(switcher.Catalogue[2])), "Only clone index 2 changed; saved profile remains untouched.");
                    Duration.SetValue(switcher, 1f); switcher.ShowEra(1); Go(7, .15); break;
                case 7:
                    Add("no-fade-profile test reached an unfinished fade", switcher.IsSwitching && HasOverride() && Matches(0), State());
                    switcher.ShowEra(2);
                    Add("no-fade-profile request immediately replaces old transition", Consistent(2), State());
                    Go(8, 1.3); break;
                case 8:
                    Add("no-fade-profile request survives old transition deadline", Consistent(2), State());
                    switcher.ShowEra(2);
                    Add("no-fade-profile same-target reselection has no override", Consistent(2), State());
                    Finish(null); break;
            }
        }
        catch (Exception ex) { Finish(ex.ToString()); }
    }

    static void Go(int next, double seconds) { phase = next; due = EditorApplication.timeSinceStartup + seconds; }
    static void NewFixture()
    {
        if (fixture) Object.Destroy(fixture);
        foreach (var data in owned) if (data) Object.Destroy(data);
        owned.Clear();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab) throw new InvalidOperationException("Saved showcase missing.");
        fixture = Object.Instantiate(prefab); fixture.name = "Era Rereview Probe Instance";
        foreach (var preview in fixture.GetComponentsInChildren<HelmetEraPlayModePreview>()) preview.enabled = false;
        switcher = fixture.GetComponentInChildren<HelmetEraSwitcher>();
        if (!switcher || Duration == null || Profiles == null || Fade == null || switcher.Catalogue.Count < 3)
            throw new InvalidOperationException("Expected switcher, reflected configuration fields and three eras.");
        var original = switcher.Catalogue;
        var catalogue = Object.Instantiate(original); catalogue.hideFlags = HideFlags.DontSave; owned.Add(catalogue);
        var profiles = new HelmetEraProfile[original.Count];
        for (int i = 0; i < profiles.Length; i++)
        {
            profiles[i] = Object.Instantiate(original[i]); profiles[i].hideFlags = HideFlags.DontSave; owned.Add(profiles[i]);
        }
        Profiles.SetValue(catalogue, profiles); catalogue.InvalidateCache();
        channel = ScriptableObject.CreateInstance<HelmetEraChannel>(); channel.hideFlags = HideFlags.DontSave; owned.Add(channel);
        switcher.Configure(catalogue, channel);
    }
    static void Press(HelmetEraButton.StepDirection direction)
    {
        foreach (var button in fixture.GetComponentsInChildren<HelmetEraButton>())
            if (button.Direction == direction) { button.TriggerProgrammatically(); return; }
        throw new InvalidOperationException("Saved button missing: " + direction);
    }
    static bool Consistent(int index)
    { return switcher.CurrentIndex == index && Matches(index) && channel.Current == switcher.Catalogue[index] && !switcher.IsSwitching && !HasOverride(); }
    static bool Matches(int index)
    {
        foreach (var binding in switcher.Bindings)
            if (binding.target.sharedMaterials[binding.subMaterialIndex] != switcher.Catalogue[index].MaterialFor(binding.slot)) return false;
        return true;
    }
    static bool HasOverride()
    {
        foreach (var binding in switcher.Bindings)
        {
            var block = new MaterialPropertyBlock(); binding.target.GetPropertyBlock(block);
            if (!block.isEmpty) return true;
        }
        return false;
    }
    static string State()
    {
        return "index=" + switcher.CurrentIndex + "; channel=" + (channel.Current ? channel.Current.DisplayName : "null") +
            "; materialsMatch0=" + Matches(0) + "; materialsMatch1=" + Matches(1) + "; materialsMatch2=" + Matches(2) +
            "; switching=" + switcher.IsSwitching + "; propertyBlockPresent=" + HasOverride();
    }
    static void Add(string name, bool passed, string detail) { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); }
    static void Log(string condition, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + stack); }
    static void Finish(string failure)
    {
        Application.logMessageReceived -= Log;
        report.failure = failure ?? ""; report.finishedUtc = DateTime.UtcNow.ToString("o");
        foreach (var check in report.checks) { if (check.passed) report.passed++; else report.failed++; }
        string output = Path.Combine(SessionState.GetString(Key + "output", ""), "review-playmode.json");
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        int code = !string.IsNullOrEmpty(failure) || report.errors.Count > 0 ? 1 : report.failed > 0 ? 2 : 0;
        Debug.Log("ERA_REVIEW_PROBE: passed=" + report.passed + " failed=" + report.failed + " errors=" + report.errors.Count + " report=" + output);
        SessionState.SetInt(Key + "exitCode", code); SessionState.SetFloat(Key + "exitAt", -1f);
        SessionState.SetString(Key + "mode", "exit"); EditorApplication.ExitPlaymode();
    }
    static string Argument(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == key) return args[i + 1];
        return null;
    }
}
