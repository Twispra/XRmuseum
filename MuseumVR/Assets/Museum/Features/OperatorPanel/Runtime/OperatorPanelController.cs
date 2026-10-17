using System;
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
        int previousVisibleCount = -1;
        bool entryBuilt, placementPending, placementPoseValid;
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
        /// so the panel never jumps as a side effect of model rotation. Equipment drags are already
        /// blocked at the XRI layer by the station's select filter, which prevents this entry
        /// from being confirmed at all while they run.</summary>
        public static bool IsAnyGestureDragging(HelmetDiegeticStation station)
        {
            if (!station) return false;
            if (station.RotationGesture && station.RotationGesture.IsDragging) return true;
            if (station.SectionGesture && station.SectionGesture.IsDragging) return true;
            return false;
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
            int visibleCount = (station.SettingsVisible ? 1 : 0) + (station.AboutVisible ? 1 : 0);
            if (shown)
            {
                if (!placementPoseValid || visibleCount != previousVisibleCount) PlaceVisiblePanels();
                entry.SetTitle(HideTitle);
            }
            else
            {
                placementPoseValid = false;
                entry.SetTitle(ShowTitle);
            }
            previousVisibleCount = visibleCount;
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
            var center = viewer.position + forward * summonDistance;
            center.y = viewer.position.y - summonBelowEye;
            if (station.Inspection)
            {
                var helmet = station.Inspection.transform.position;
                var flatHelmet = new Vector3(helmet.x, center.y, helmet.z);
                var offset = center - flatHelmet;
                offset.y = 0f;
                if (offset.sqrMagnitude < minimumHelmetClearance * minimumHelmetClearance)
                {
                    var push = offset.sqrMagnitude < .0001f ? right : offset.normalized;
                    center = flatHelmet + push * minimumHelmetClearance;
                }
            }
            placementCenter = center;
            placementFacing = Quaternion.LookRotation(forward, Vector3.up);
            placementPoseValid = true;
            MoveIfVisible(SettingsPath, settingsOffset);
            MoveIfVisible(InformationPath, informationOffset);
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
