using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Museum.Eras;
using Museum.Exploration;
using Museum.Inspection;
using Museum.Localization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Telemetry
{
    /// <summary>
    /// 会话记录器：旁听访客在场景里的状态变化与选择事件，逐行写成 CSV。
    /// 它只读不写：不改变任何交互行为，也不碰工程文件；日志落在 Application.persistentDataPath。
    /// 用于 Phase 3 的定量证据（每次选择、求助次数、误触、各阶段耗时）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Museum/Session Telemetry")]
    public sealed class SessionTelemetry : MonoBehaviour
    {
        public const string FolderName = "MuseumSessions";

        const string ColumnHeader =
            "t_seconds,event,target,detail,page,clue,hints,air_seen,voice_seen," +
            "era_index,era_name,language,cutaway,floating,window_open,resets";

        const string OffSwitch = "-museumTelemetryOff";

        /// <summary>状态差分采样间隔（秒）。事件本身由回调即时记录，不受此值影响。</summary>
        [SerializeField] float sampleInterval = 0.2f;

        public static string LogDirectory => Path.Combine(Application.persistentDataPath, FolderName);

        static SessionTelemetry instance;

        StreamWriter writer;
        string sessionFile;
        float elapsed;
        float nextSampleAt;
        float nextResolveAt;
        Snapshot previous;
        float lastSelectAt = -1000f;
        string lastSelectTarget = string.Empty;
        int selectCount;
        int activateCount;
        int hoverCount;
        float lastHoverAt = -1000f;
        string lastHoverTarget = string.Empty;

        readonly List<XRBaseInteractable> subscribed = new List<XRBaseInteractable>();
        HelmetInspectionController inspection;
        HelmetExplorationController exploration;
        HelmetEraSwitcher eras;
        LanguageChannel channel;
        HelmetExplorationController hookedExploration;
        LanguageChannel hookedChannel;

        /// <summary>当前会话的 CSV 路径；未开始记录时为空串。</summary>
        public static string CurrentSessionFile => instance ? instance.sessionFile : string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance) return;
            if (HasCommandLineSwitch(OffSwitch)) return;
            var host = new GameObject("SessionTelemetry");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<SessionTelemetry>();
        }

        static bool HasCommandLineSwitch(string needle)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], needle, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void OnEnable()
        {
            if (instance && instance != this) { Destroy(this); return; }
            instance = this;

            OpenLog();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveSources();
            SubscribeInteractables();

            previous = Capture();
            nextResolveAt = elapsed + 2f;
            Write("session_start", Application.productName,
                "scene=" + SceneManager.GetActiveScene().name
                + ";interactables=" + subscribed.Count.ToString(CultureInfo.InvariantCulture)
                + ";exploration=" + (exploration ? "1" : "0")
                + ";eras=" + (eras ? "1" : "0")
                + ";inspection=" + (inspection ? "1" : "0")
                + ";unity=" + Application.unityVersion, previous);
        }

        void OnDisable()
        {
            if (instance == this) instance = null;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeInteractables();
            if (hookedExploration) hookedExploration.Changed -= HandleExplorationChanged;
            if (hookedChannel) hookedChannel.LanguageChanged -= HandleLanguageChanged;

            if (writer == null) return;
            // 退出时场景对象可能已被销毁，用最后一次采到的快照，避免把终态记成空值。
            Write("session_end", string.Empty,
                "duration=" + elapsed.ToString("0.000", CultureInfo.InvariantCulture)
                + ";selects=" + selectCount.ToString(CultureInfo.InvariantCulture)
                + ";activates=" + activateCount.ToString(CultureInfo.InvariantCulture)
                + ";hovers=" + hoverCount.ToString(CultureInfo.InvariantCulture), previous);
            writer.Flush();
            writer.Dispose();
            writer = null;
        }

        void Update()
        {
            if (writer == null) return;

            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= nextResolveAt)
            {
                nextResolveAt = elapsed + 2f;
                if (subscribed.Count == 0) { ResolveSources(); SubscribeInteractables(); }
            }

            if (elapsed < nextSampleAt) return;
            nextSampleAt = elapsed + Mathf.Max(0.05f, sampleInterval);
            Sample();
        }

        void Sample()
        {
            if (writer == null) return;
            var now = Capture();
            string diff = Diff(previous, now);
            if (diff.Length == 0) return;
            previous = now;
            Write("state", string.Empty, diff, now);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            inspection = null;
            exploration = null;
            eras = null;
            channel = null;
            ResolveSources();
            SubscribeInteractables();
            Write("scene_loaded", scene.name, "mode=" + mode, Capture());
        }

        void HandleSelectEntered(SelectEnterEventArgs args)
        {
            var component = args.interactableObject as Component;
            string target = component ? DescribePath(component.transform) : "unknown";
            bool first = selectCount == 0 && activateCount == 0;
            float gap = first ? 0f : elapsed - lastSelectAt;
            bool rapid = !first && gap < 2f && !string.Equals(target, lastSelectTarget, StringComparison.Ordinal);

            lastSelectAt = elapsed;
            lastSelectTarget = target;
            selectCount++;

            Write("select", target,
                (rapid ? "rapid_reselect;" : string.Empty)
                + (first ? "first;" : "gap=" + gap.ToString("0.00", CultureInfo.InvariantCulture) + ";")
                + "n=" + selectCount.ToString(CultureInfo.InvariantCulture), Capture());
        }

        /// <summary>
        /// 本工程的热点同时监听 selectEntered 与 activated：前者对应抓握（Grip），
        /// 后者对应扳机（Trigger）。两路都要记，否则会漏掉"按了扳机但没抓握"的访客。
        /// </summary>
        void HandleActivated(ActivateEventArgs args)
        {
            var component = args.interactableObject as Component;
            string target = component ? DescribePath(component.transform) : "unknown";
            bool first = selectCount == 0 && activateCount == 0;
            float gap = first ? 0f : elapsed - lastSelectAt;
            bool rapid = !first && gap < 2f && !string.Equals(target, lastSelectTarget, StringComparison.Ordinal);

            lastSelectAt = elapsed;
            lastSelectTarget = target;
            activateCount++;

            Write("activate", target,
                (rapid ? "rapid_reselect;" : string.Empty)
                + (first ? "first;" : "gap=" + gap.ToString("0.00", CultureInfo.InvariantCulture) + ";")
                + "n=" + activateCount.ToString(CultureInfo.InvariantCulture), Capture());
        }

        /// <summary>只记录“指着了但没按下去”的线索，用于发现瞄准困难；同一目标 1 秒内只记一次。</summary>
        void HandleHoverEntered(HoverEnterEventArgs args)
        {
            var component = args.interactableObject as Component;
            string target = component ? DescribePath(component.transform) : "unknown";
            if (string.Equals(target, lastHoverTarget, StringComparison.Ordinal)) return;
            if (elapsed - lastHoverAt < 1f) return;

            lastHoverAt = elapsed;
            lastHoverTarget = target;
            hoverCount++;
            Write("hover", target, "n=" + hoverCount.ToString(CultureInfo.InvariantCulture), Capture());
        }

        void HandleExplorationChanged() => Sample();

        void HandleLanguageChanged(MuseumLanguage language) => Sample();

        void OpenLog()
        {
            Directory.CreateDirectory(LogDirectory);
            string product = string.IsNullOrEmpty(Application.productName) ? "museum" : Application.productName;
            product = product.Replace(' ', '-');
            sessionFile = Path.Combine(LogDirectory,
                DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + product + ".csv");

            writer = new StreamWriter(sessionFile, false, new UTF8Encoding(true));
            writer.WriteLine(ColumnHeader);
            writer.Flush();
        }

        void ResolveSources()
        {
            if (!inspection) inspection = FindFirst<HelmetInspectionController>();
            if (!exploration) exploration = FindFirst<HelmetExplorationController>();
            if (!eras) eras = FindFirst<HelmetEraSwitcher>();
            channel = MuseumText.Channel;
            Hook();
        }

        void Hook()
        {
            if (hookedExploration != exploration)
            {
                if (hookedExploration) hookedExploration.Changed -= HandleExplorationChanged;
                hookedExploration = exploration;
                if (hookedExploration) hookedExploration.Changed += HandleExplorationChanged;
            }

            if (hookedChannel != channel)
            {
                if (hookedChannel) hookedChannel.LanguageChanged -= HandleLanguageChanged;
                hookedChannel = channel;
                if (hookedChannel) hookedChannel.LanguageChanged += HandleLanguageChanged;
            }
        }

        void SubscribeInteractables()
        {
            UnsubscribeInteractables();
            var all = FindObjectsOfType<XRBaseInteractable>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i]) continue;
                all[i].selectEntered.AddListener(HandleSelectEntered);
                all[i].activated.AddListener(HandleActivated);
                all[i].hoverEntered.AddListener(HandleHoverEntered);
                subscribed.Add(all[i]);
            }
        }

        void UnsubscribeInteractables()
        {
            for (int i = 0; i < subscribed.Count; i++)
                if (subscribed[i])
                {
                    subscribed[i].selectEntered.RemoveListener(HandleSelectEntered);
                    subscribed[i].activated.RemoveListener(HandleActivated);
                    subscribed[i].hoverEntered.RemoveListener(HandleHoverEntered);
                }
            subscribed.Clear();
        }

        Snapshot Capture()
        {
            var s = new Snapshot
            {
                page = "-",
                clue = "-",
                era = "-",
                language = "-",
                eraIndex = -1,
            };

            if (!inspection) inspection = FindFirst<HelmetInspectionController>();
            if (!exploration) exploration = FindFirst<HelmetExplorationController>();
            if (!eras) eras = FindFirst<HelmetEraSwitcher>();
            if (!channel) { channel = MuseumText.Channel; Hook(); }

            if (exploration)
            {
                s.page = exploration.CurrentPage.ToString();
                s.clue = exploration.HasSelectedClue ? exploration.SelectedClue.ToString() : "-";
                s.hints = exploration.HintRequests;
                s.air = exploration.AirSeen;
                s.voice = exploration.VoiceSeen;
            }

            if (eras)
            {
                s.eraIndex = eras.CurrentIndex;
                var era = eras.CurrentEra;
                s.era = era ? era.DisplayName : "-";
            }

            if (inspection)
            {
                s.cutaway = inspection.IsCutaway;
                s.floating = inspection.IsFloating;
                s.windowOpen = inspection.WindowTargetOpen;
                s.resets = inspection.ResetCount;
            }

            if (channel) s.language = channel.Current.ToString();
            return s;
        }

        static string Diff(Snapshot a, Snapshot b)
        {
            var parts = new List<string>(8);
            if (a.page != b.page) parts.Add("page");
            if (a.clue != b.clue) parts.Add("clue");
            if (a.hints != b.hints) parts.Add("hints");
            if (a.air != b.air) parts.Add("air_seen");
            if (a.voice != b.voice) parts.Add("voice_seen");
            if (a.eraIndex != b.eraIndex) parts.Add("era");
            if (a.language != b.language) parts.Add("language");
            if (a.cutaway != b.cutaway) parts.Add("cutaway");
            if (a.floating != b.floating) parts.Add("floating");
            if (a.windowOpen != b.windowOpen) parts.Add("window_open");
            if (a.resets != b.resets) parts.Add("resets");
            return parts.Count == 0 ? string.Empty : string.Join("+", parts.ToArray());
        }

        void Write(string evt, string target, string detail, Snapshot s)
        {
            if (writer == null) return;

            var sb = new StringBuilder(192);
            sb.Append(elapsed.ToString("0.000", CultureInfo.InvariantCulture)).Append(',');
            Append(sb, evt); sb.Append(',');
            Append(sb, target); sb.Append(',');
            Append(sb, detail); sb.Append(',');
            Append(sb, s.page); sb.Append(',');
            Append(sb, s.clue); sb.Append(',');
            sb.Append(s.hints.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.air ? "1" : "0").Append(',');
            sb.Append(s.voice ? "1" : "0").Append(',');
            sb.Append(s.eraIndex.ToString(CultureInfo.InvariantCulture)).Append(',');
            Append(sb, s.era); sb.Append(',');
            Append(sb, s.language); sb.Append(',');
            sb.Append(s.cutaway ? "1" : "0").Append(',');
            sb.Append(s.floating ? "1" : "0").Append(',');
            sb.Append(s.windowOpen ? "1" : "0").Append(',');
            sb.Append(s.resets.ToString(CultureInfo.InvariantCulture));

            writer.WriteLine(sb.ToString());
            writer.Flush();
        }

        static void Append(StringBuilder sb, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            {
                sb.Append(value);
                return;
            }

            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '"') sb.Append('"');
                sb.Append(value[i]);
            }
            sb.Append('"');
        }

        static string DescribePath(Transform t)
        {
            if (!t) return "unknown";
            string path = t.name;
            for (int i = 0; i < 2 && t.parent; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        static T FindFirst<T>() where T : Component
        {
            var found = FindObjectsOfType<T>(true);
            return found.Length > 0 ? found[0] : null;
        }

        struct Snapshot
        {
            public string page;
            public string clue;
            public string era;
            public string language;
            public int hints;
            public int eraIndex;
            public int resets;
            public bool air;
            public bool voice;
            public bool cutaway;
            public bool floating;
            public bool windowOpen;
        }
    }
}
