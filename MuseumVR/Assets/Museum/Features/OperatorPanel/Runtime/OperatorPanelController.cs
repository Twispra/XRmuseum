using System;
using System.Collections.Generic;
using System.Linq;
using Museum.Diegetic;
using Museum.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.OperatorPanel
{
    /// <summary>Summonable operator panel: brings the saved settings and exhibit-information
    /// panels in front of the viewer once, faces the viewer, then stays fixed in space.
    /// The diegetic station remains authoritative for state; this module only repositions
    /// and adds one fixed pedestal entry, never resetting exhibit state.</summary>
    [DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class OperatorPanelController : MonoBehaviour
    {
        [SerializeField] HelmetDiegeticStation station;
        [SerializeField] Transform viewer;
        [SerializeField] Material uiMaterial;
        [SerializeField] Vector3 entryLocalPosition = new Vector3(-.45f, -.22f, -.005f);
        [SerializeField, Range(.3f, 1.2f)] float summonDistance = .65f;
        [SerializeField, Range(0f, .3f)] float summonBelowEye = .06f;
        [SerializeField, Range(.3f, 1.2f)] float minimumHelmetClearance = .55f;
        [SerializeField] Vector3 settingsOffset = new Vector3(-.42f, 0f, 0f);
        [SerializeField] Vector3 informationOffset = new Vector3(.42f, 0f, 0f);
        HelmetPhysicalControl entry;
        int previousVisibleState = -1;
        bool entryBuilt, placementPoseValid;
        Vector3 placementCenter;
        Quaternion placementFacing;
        const float IdleOpacity = .22f, HandRevealDistance = .18f, FadeOutDelay = .65f;
        static readonly Color Teal = new Color(.2f, .88f, .82f);
        public HelmetDiegeticStation Station => station;
        public HelmetPhysicalControl Entry => entry;
        public bool EntryReady => entryBuilt;
        public bool PanelShown => station && station.IsReady && (station.SettingsVisible || station.AboutVisible);
        const string PedestalPath = "Pedestal edge controls";
        const string SettingsPath = "Fixed settings";
        const string InformationPath = "Fixed exhibit information";
        public const string ShowTitle = "Show panel";
        public const string HideTitle = "Hide panel";
        public const string EntryHint = "Open or close the panel";

        void Start() => TryBuild();
        void TryBuild()
        {
            if (entryBuilt || !station) station = GetComponent<HelmetDiegeticStation>();
            if (entryBuilt || !station || !station.IsReady) return;
            if (!viewer && Camera.main) viewer = Camera.main.transform;
            BuildEntry();
            entryBuilt = true;
        }
        void BuildEntry()
        {
            var pedestal = station.transform.Find(PedestalPath);
            if (!pedestal) throw new InvalidOperationException("Pedestal edge controls missing; install the diegetic station first.");
            if (!pedestal.Find(SettingsPath) || !pedestal.Find(InformationPath))
                throw new InvalidOperationException("Saved settings or exhibit-information panel missing.");
            var go = new GameObject("Physical UI - PanelEntry");
            go.transform.SetParent(pedestal, false);
            go.transform.localPosition = entryLocalPosition;
            go.SetActive(false); // Register XRI only after the trigger collider list is complete.
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "Inset target";
            face.transform.SetParent(go.transform, false);
            face.transform.localScale = new Vector3(.168f, .074f, .012f);
            var collider = face.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            var renderer = face.GetComponent<Renderer>();
            renderer.sharedMaterial = uiMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var target = go.AddComponent<XRSimpleInteractable>();
            target.colliders.Add(collider);
            target.interactionLayers = 1;
            var rim = new GameObject("Target outline").AddComponent<LineRenderer>();
            rim.transform.SetParent(go.transform, false);
            rim.sharedMaterial = uiMaterial;
            rim.useWorldSpace = false;
            rim.loop = true;
            rim.positionCount = 8;
            rim.widthMultiplier = .0015f;
            rim.numCornerVertices = 3;
            rim.shadowCastingMode = ShadowCastingMode.Off;
            rim.receiveShadows = false;
            rim.SetPositions(new[]
            {
                new Vector3(-.075f,-.037f,-.008f), new Vector3(.075f,-.037f,-.008f),
                new Vector3(.084f,-.029f,-.008f), new Vector3(.084f,.029f,-.008f),
                new Vector3(.075f,.037f,-.008f), new Vector3(-.075f,.037f,-.008f),
                new Vector3(-.084f,.029f,-.008f), new Vector3(-.084f,-.029f,-.008f)
            });
            var text = MakeLabel(go.transform, "Action", new Vector3(0, 0, -.015f), new Vector2(.156f, .071f));
            text.enableAutoSizing = true;
            text.fontSizeMin = .23f;
            text.fontSizeMax = .26f;
            var detail = MakeLabel(go.transform, "Action hint", new Vector3(0, .062f, -.015f), new Vector2(.29f, .035f));
            entry = go.AddComponent<HelmetPhysicalControl>();
            entry.Configure(station, target, renderer, rim, text, detail, ShowTitle, EntryHint, Teal, Toggle);
            entry.ConfigureVisibility(IdleOpacity, HandRevealDistance, FadeOutDelay);
            station.RegisterControl(entry);
            go.SetActive(true);
        }
        static TextMeshPro MakeLabel(Transform parent, string name, Vector3 position, Vector2 size)
        {
            var label = new GameObject(name).AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            label.font = MuseumText.Table ? MuseumText.Table.FontAsset : TMP_Settings.defaultFontAsset;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = size;
            label.fontSize = .26f;
            label.enableAutoSizing = false;
            label.enableWordWrapping = true;
            label.color = Color.white;
            return label;
        }
        /// <summary>True while any station gesture is mid-drag; summon and hide refuse during one
        /// so the panel never jumps as a side effect of model rotation. The dynamic entry also
        /// inherits the station's select filter, including equipment drags.</summary>
        public static bool IsAnyGestureDragging(HelmetDiegeticStation station)
        {
            return station && station.HasActiveGesture;
        }
        public void Toggle()
        {
            if (!station || !station.IsReady) return;
            if (IsAnyGestureDragging(station)) return; // Explicit refusal; no hidden state change.
            if (station.SettingsVisible) station.Execute(HelmetDiegeticStation.Command.Settings);
            else if (station.AboutVisible) station.Execute(HelmetDiegeticStation.Command.About);
            else station.Execute(HelmetDiegeticStation.Command.Settings);
        }
        void LateUpdate()
        {
            if (!entryBuilt) { TryBuild(); if (!entryBuilt) return; }
            bool shown = PanelShown;
            int visibleState = (station.SettingsVisible ? 1 : 0) | (station.AboutVisible ? 2 : 0);
            if (shown)
            {
                if (!placementPoseValid || visibleState != previousVisibleState) PlaceVisiblePanels();
                entry.SetTitle(HideTitle);
            }
            else
            {
                placementPoseValid = false;
                entry.SetTitle(ShowTitle);
            }
            previousVisibleState = visibleState;
        }
        /// <summary>Place every visible panel once at the viewer's current position, yaw-facing
        /// the viewer, clear of the exhibit. Panels then stay fixed; head movement never moves them,
        /// and a later summon re-places them from the viewer's position at that moment.</summary>
        void PlaceVisiblePanels()
        {
            if (!viewer) return;
            var forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var center = viewer.position + viewer.forward.normalized * summonDistance - Vector3.up * summonBelowEye;
            placementFacing = Quaternion.LookRotation(forward, Vector3.up);
            var panels = new[] { (SettingsPath, settingsOffset), (InformationPath, informationOffset) }
                .Select(p => (panel: station.transform.Find(PedestalPath + "/" + p.Item1), offset: p.Item2))
                .Where(p => p.panel && p.panel.gameObject.activeSelf)
                .Select(p => (p.panel, p.offset, bounds: LocalEnvelope(p.panel))).ToArray();
            var obstacles = ObstacleBounds(panels.Select(p => p.panel).ToArray());
            // Validate FINAL panel envelopes, including offsets, before moving anything.
            // A small vertical adjustment keeps a horizontal panel inside pitched-eye views.
            // The nearest clear candidate wins, within the same view and distance limits.
            var candidates = new List<Vector3> { center };
            for (int depth = -2; depth <= 12; depth++)
                for (int side = -8; side <= 8; side++)
                    for (int height = -4; height <= 4; height++)
                        candidates.Add(center + forward * (depth * .10f) + right * (side * .15f) + Vector3.up * (height * .10f));
            bool clear = false;
            foreach (var candidate in candidates.OrderBy(p => (p - center).sqrMagnitude))
            {
                if (panels.Any(p => !IsClear(WorldEnvelope(p.bounds, candidate + placementFacing * p.offset, placementFacing), obstacles))) continue;
                placementCenter = candidate;
                clear = true;
                break;
            }
            if (!clear)
            {
                // A crowded exhibition is an explicit refusal, never an overlapping invisible target.
                if (station.SettingsVisible) station.Execute(HelmetDiegeticStation.Command.Settings);
                else if (station.AboutVisible) station.Execute(HelmetDiegeticStation.Command.About);
                placementPoseValid = false;
                return;
            }
            placementPoseValid = true;
            MoveIfVisible(SettingsPath, settingsOffset);
            MoveIfVisible(InformationPath, informationOffset);
        }
        static Bounds LocalEnvelope(Transform panel)
        {
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true, true);
            var result = new Bounds(Vector3.zero, Vector3.one * .02f);
            foreach (var renderer in panel.GetComponentsInChildren<Renderer>(true))
            {
                var bounds = renderer.localBounds;
                foreach (var corner in Corners(bounds))
                    result.Encapsulate(panel.InverseTransformPoint(renderer.transform.TransformPoint(corner)));
            }
            foreach (var box in panel.GetComponentsInChildren<BoxCollider>(true))
                foreach (var corner in Corners(new Bounds(box.center, box.size)))
                    result.Encapsulate(panel.InverseTransformPoint(box.transform.TransformPoint(corner)));
            result.Expand(.08f);
            return result;
        }
        static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            for (int i = 0; i < 8; i++)
                yield return bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        }
        static Bounds WorldEnvelope(Bounds local, Vector3 position, Quaternion rotation)
        {
            var result = new Bounds(position + rotation * local.center, Vector3.zero);
            foreach (var corner in Corners(local)) result.Encapsulate(position + rotation * corner);
            return result;
        }
        List<Bounds> ObstacleBounds(Transform[] panels)
        {
            var renderers = new List<Renderer>();
            renderers.AddRange(FindObjectsOfType<Renderer>().Where(r => r.name == "Plinth"));
            if (station.Inspection) renderers.AddRange(station.Inspection.GetComponentsInChildren<Renderer>(false));
            if (station.Demonstration)
                foreach (var equipment in station.Demonstration.EquipmentExhibits)
                    if (equipment.IsVisible) renderers.AddRange(equipment.Renderers);
            var pedestal = station.transform.Find(PedestalPath);
            if (pedestal) renderers.AddRange(pedestal.GetComponentsInChildren<Renderer>(false)
                .Where(r => !panels.Any(p => r.transform == p || r.transform.IsChildOf(p))));
            return renderers.Where(r => r && r.enabled && r.gameObject.activeInHierarchy)
                .Select(r => { var bounds = r.bounds; bounds.Expand(.06f); return bounds; }).ToList();
        }
        bool IsClear(Bounds bounds, List<Bounds> obstacles)
        {
            float eyeDistance = Vector3.Distance(viewer.position, bounds.center);
            if (eyeDistance < .35f || eyeDistance > 1.6f) return false;
            var camera = viewer.GetComponent<Camera>();
            if (camera)
                foreach (var corner in Corners(bounds))
                {
                    var viewport = camera.WorldToViewportPoint(corner, Camera.MonoOrStereoscopicEye.Mono);
                    if (viewport.z <= camera.nearClipPlane || viewport.x < .03f || viewport.x > .97f || viewport.y < .03f || viewport.y > .97f) return false;
                }
            if (obstacles.Any(bounds.Intersects)) return false;
            // World-space non-overlap alone can still put the panel behind the solid plinth
            // or the specimen. Keep the complete panel visible from the actual eye.
            foreach (var point in Corners(bounds).Concat(new[] { bounds.center }))
            {
                var direction = point - viewer.position;
                var ray = new Ray(viewer.position, direction.normalized);
                if (obstacles.Any(obstacle => obstacle.IntersectRay(ray, out var distance) && distance < direction.magnitude - .02f)) return false;
            }
            if (!station.Inspection) return true;
            var helmet = station.Inspection.transform.position;
            return new Vector2(bounds.center.x - helmet.x, bounds.center.z - helmet.z).sqrMagnitude >= minimumHelmetClearance * minimumHelmetClearance;
        }
        void OnDestroy()
        {
            if (station && entry) station.UnregisterControl(entry);
            if (entry) Destroy(entry.gameObject);
        }
        void MoveIfVisible(string panelName, Vector3 offset)
        {
            var pedestal = station.transform.Find(PedestalPath);
            var panel = pedestal ? pedestal.Find(panelName) : null;
            if (!panel || !panel.gameObject.activeSelf) return;
            panel.position = placementCenter + placementFacing * offset;
            panel.rotation = placementFacing;
        }
    }
}
