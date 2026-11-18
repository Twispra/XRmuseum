#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Museum.Telemetry.EditorTools
{
    /// <summary>会话日志的本地入口：打开目录、清空旧记录。不参与构建。</summary>
    static class SessionTelemetryMenu
    {
        [MenuItem("Museum/Telemetry/Open session log folder")]
        static void OpenFolder()
        {
            var dir = SessionTelemetry.LogDirectory;
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        [MenuItem("Museum/Telemetry/Show current session path")]
        static void ShowCurrent()
        {
            string path = SessionTelemetry.CurrentSessionFile;
            Debug.Log(string.IsNullOrEmpty(path)
                ? "[Telemetry] 当前没有正在记录的会话。日志故障见控制台警告。"
                : "[Telemetry] " + path);
        }

        [MenuItem("Museum/Telemetry/Clear session logs")]
        static void ClearLogs()
        {
            var dir = SessionTelemetry.LogDirectory;
            if (!Directory.Exists(dir))
            {
                Debug.Log("[Telemetry] 还没有日志目录：" + dir);
                return;
            }

            var files = Directory.GetFiles(dir, "*.csv");
            if (files.Length == 0)
            {
                Debug.Log("[Telemetry] 目录里没有 CSV。");
                return;
            }

            if (!EditorUtility.DisplayDialog("Clear session logs",
                    "删除 " + files.Length + " 个 CSV？\n" + dir, "删除", "取消"))
                return;

            for (int i = 0; i < files.Length; i++) File.Delete(files[i]);
            Debug.Log("[Telemetry] 已删除 " + files.Length + " 个文件。");
        }
    }
}
#endif
