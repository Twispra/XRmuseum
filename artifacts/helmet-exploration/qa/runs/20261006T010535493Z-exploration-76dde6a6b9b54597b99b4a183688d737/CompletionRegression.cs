#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Temporary runner: validate only saved assets, then the existing exploration regression.
public static class CompletionRegression
{
    [Serializable] sealed class ModuleReport { public int passed; public int failed; }
    public static void RunBatch()
    {
        try
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/localization/qa/runs"));
            var before = new HashSet<string>(Directory.GetFiles(root, "report.json", SearchOption.AllDirectories));
            Museum.Localization.EditorTools.LocalizationValidation.ValidateFromMenu();
            var created = Directory.GetFiles(root, "report.json", SearchOption.AllDirectories).Where(path => !before.Contains(path)).ToArray();
            if (created.Length != 1) throw new InvalidOperationException("Expected one new module report.");
            var result = JsonUtility.FromJson<ModuleReport>(File.ReadAllText(created[0]));
            if (result == null || result.failed != 0 || result.passed != 11)
                throw new InvalidOperationException("Saved module validation did not pass all 11 checks: " + created[0]);
            Debug.Log("COMPLETION_MODULE_PASSED: " + created[0]);
            Museum.Exploration.Editor.HelmetExplorationValidation.RunBatch();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1); else throw;
        }
    }
}
#endif
