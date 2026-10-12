using System.Linq;
using Museum.Eras;
using Museum.Experience;
using Museum.Exploration;
using Museum.Inspection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace Museum.Refined
{
    /// <summary>Changes only exhibit visuals. The existing XRI, reset and inspection objects retain their identity.</summary>
    [DefaultExecutionOrder(80), DisallowMultipleComponent]
    public sealed class RefinedHelmetBinding : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] Renderer[] originalRenderers, refinedRenderers, originalClipped, originalWindowRenderers;
        [SerializeField] Transform refinedWindow, airOutlet, telephoneOutlet, originalAirOutlet, originalTelephoneOutlet, response;
        [SerializeField] Renderer refinedGlass, refinedShell, refinedAir, originalAir;
        [SerializeField] Renderer[] refinedTelephone, originalTelephone;
        [SerializeField] Vector3 originalHinge, refinedHinge, originalWindowCenter, originalWindowSize, refinedWindowCenter, refinedWindowSize;
        [SerializeField] Transform toggle, nut;
        [SerializeField] HelmetSectionMeshData sectionData;
        [SerializeField] Material sectionMaterial;
        [SerializeField] int refinedEraIndex = 1;
        [System.Serializable]
        public sealed class EraVisual
        {
            public int eraIndex;
            public Renderer[] renderers;
            public Transform window, airOutlet, telephoneOutlet;
            public Renderer glass, shell, air;
            public Renderer[] telephone;
            public Vector3 hinge, triggerCenter, triggerSize;
            public HelmetSectionMeshData sectionData;
        }
        [SerializeField] EraVisual additionalVisual;
        EraVisual activeAdditional;
        HelmetInspectionController inspection;
        HelmetEraSwitcher eras;
        HelmetRearConnections rear;
        HelmetWindowInteractable window;
        HelmetSectionInteractable guide;
        HelmetSectionSurface sectionSurface;
        XRSimpleInteractable[] priorityTargets;
        Quaternion toggleClosed, nutClosed;
        bool initialized;
        bool priorityRegistered;
        int observedEra = int.MinValue;
        float observedLockRelease = float.NaN;
        public bool IsRefinedActive { get; private set; }
        public Renderer[] RefinedRenderers => refinedRenderers;
        public Transform RefinedWindow => refinedWindow;
        public Renderer RefinedGlass => refinedGlass;
        public Renderer RefinedShell => refinedShell;
        bool HasAdditionalVisual => additionalVisual != null && additionalVisual.renderers != null && additionalVisual.renderers.Length > 0;
        public EraVisual AdditionalVisual => HasAdditionalVisual ? additionalVisual : null;
        public bool IsAdditionalVisualActive => activeAdditional != null;
        public Transform AirOutlet => activeAdditional != null ? activeAdditional.airOutlet : IsRefinedActive ? airOutlet : originalAirOutlet;
        public Transform TelephoneOutlet => activeAdditional != null ? activeAdditional.telephoneOutlet : IsRefinedActive ? telephoneOutlet : originalTelephoneOutlet;
        public Renderer ActiveGlass => activeAdditional != null ? activeAdditional.glass : IsRefinedActive ? refinedGlass : originalWindowRenderers.First(r => r.sharedMaterial.shader.name == "Museum/Inspection Glass");
        public Renderer ActiveShell => activeAdditional != null ? activeAdditional.shell : IsRefinedActive ? refinedShell : originalClipped.First(r => r.name == "GB_Bonnet_Hollow");
        public void ConfigureAdditionalEra(EraVisual visual)
        { additionalVisual = visual; RefreshVisualBinding(); }

        public void Configure(Renderer[] originals, Renderer[] refined, Renderer[] clipped, Renderer[] windowParts,
            Transform door, Renderer glass, Renderer shell, Renderer air, Transform airEnd, Renderer[] phone, Transform phoneEnd,
            Vector3 hinge, Vector3 triggerCenter, Vector3 triggerSize, HelmetSectionMeshData data, Material cap)
        {
            inspection = GetComponent<HelmetInspectionController>(); rear = GetComponent<HelmetRearConnections>();
            window = GetComponentInChildren<HelmetWindowInteractable>(true);
            originalRenderers = originals; refinedRenderers = refined; originalClipped = clipped; originalWindowRenderers = windowParts;
            refinedWindow = door; refinedGlass = glass; refinedShell = shell; refinedAir = air; airOutlet = airEnd; refinedTelephone = phone; telephoneOutlet = phoneEnd;
            originalAir = rear.AirConnector; originalAirOutlet = rear.AirOutlet; originalTelephone = rear.TelephoneSurfaces; originalTelephoneOutlet = rear.TelephoneOutlet;
            response = transform.Find("Rear connections/Telephone receive response");
            if (!response) response = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Telephone receive indicator");
            originalHinge = inspection.WindowPivot.localPosition; refinedHinge = hinge;
            var hit = window.colliders.OfType<BoxCollider>().First(); originalWindowCenter = hit.center; originalWindowSize = hit.size;
            refinedWindowCenter = triggerCenter; refinedWindowSize = triggerSize; sectionData = data; sectionMaterial = cap;
            toggle = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DoorTogglePivot");
            nut = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DoorToggleNutPivot");
            Initialize(); Apply(refinedEraIndex);
        }
        void Awake() => Initialize();
        void OnEnable()
        {
            Initialize();
            priorityTargets = GetComponentsInChildren<Museum.Hotspots.HelmetHotspot>(true)
                .Select(h => h.GetComponent<XRSimpleInteractable>()).Concat(new XRSimpleInteractable[] { window }).Where(t => t).Distinct().ToArray();
            foreach (var target in priorityTargets) target.selectFilters.Add(this);
            priorityRegistered = true;
        }
        void OnDisable()
        {
            if (!priorityRegistered) return;
            foreach (var target in priorityTargets) if (target) target.selectFilters.Remove(this);
            priorityRegistered = false;
        }
        bool IXRSelectFilter.canProcess => isActiveAndEnabled && IsRefinedActive;
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable target)
        {
            // The finer window and nearby marker overlap some wrist poses of the guide.
            // A direct grip physically inside the shown knob owns that knob. Rays and
            // already-held window motion keep the existing selection behaviour.
            if (!(interactor is XRDirectInteractor) || !guide || !guide.ShowTool || !inspection.IsCutaway || inspection.IsWindowHeld) return true;
            // Only yield a nearby part when the guide can actually accept this hand.
            if (!guide.IsSelectableBy(interactor) || (guide.isSelected && !guide.interactorsSelecting.Contains(interactor))) return true;
            var attach = interactor.GetAttachTransform(target);
            if (!attach) return true;
            foreach (var collider in guide.colliders)
                if (collider && collider.enabled && collider.gameObject.activeInHierarchy &&
                    (collider.ClosestPoint(attach.position) - attach.position).sqrMagnitude < .000001f) return false;
            return true;
        }
        void Initialize()
        {
            if (initialized) return;
            inspection = GetComponent<HelmetInspectionController>(); eras = GetComponent<HelmetEraSwitcher>(); rear = GetComponent<HelmetRearConnections>();
            window = GetComponentInChildren<HelmetWindowInteractable>(true);
            guide = GetComponentInChildren<HelmetSectionInteractable>(true);
            sectionSurface = GetComponent<HelmetSectionSurface>();
            if (toggle) toggleClosed = toggle.localRotation;
            if (nut) nutClosed = nut.localRotation;
            initialized = true;
        }
        void LateUpdate()
        {
            Initialize();
            int index = eras ? eras.CurrentIndex : refinedEraIndex;
            if (index >= 0 && index != observedEra && !inspection.IsWindowHeld && !inspection.IsSectionHeld)
            { Apply(index); observedEra = index; }
            if (IsRefinedActive && activeAdditional == null)
            {
                float release = Mathf.Clamp01(inspection.WindowAngle / 10f);
                if (toggle) toggle.localRotation = toggleClosed * Quaternion.AngleAxis(release * -65f, Vector3.back);
                if (nut) nut.localRotation = nutClosed * Quaternion.AngleAxis(release * 720f, Vector3.up);
                // Preserve the cap's .05-degree window threshold while invalidating
                // a Refresh called before these articulated transforms were updated.
                if (float.IsNaN(observedLockRelease) || Mathf.Abs(observedLockRelease - release) > .005f)
                {
                    observedLockRelease = release;
                    if (sectionSurface) sectionSurface.InvalidateGeometry();
                }
            }
        }
        public void RefreshVisualBinding()
        { Initialize(); int index = !eras || eras.CurrentIndex < 0 ? refinedEraIndex : eras.CurrentIndex; Apply(index); observedEra = index; }
        void Apply(int index)
        {
            if (!inspection || refinedRenderers == null || originalRenderers == null) return;
            Vector3 normal = inspection.SectionNormalLocal;
            float relativeDepth = inspection.SectionRange > .0001f ? inspection.SectionOffset / inspection.SectionRange : 0f;
            activeAdditional = HasAdditionalVisual && additionalVisual.eraIndex == index ? additionalVisual : null;
            bool markV = index == refinedEraIndex && activeAdditional == null;
            IsRefinedActive = markV || activeAdditional != null;
            foreach (var r in originalRenderers) if (r) r.enabled = !IsRefinedActive;
            foreach (var r in refinedRenderers) if (r) r.enabled = markV;
            var additional = additionalVisual != null && additionalVisual.renderers != null ? additionalVisual.renderers : System.Array.Empty<Renderer>();
            foreach (var r in additional) if (r) r.enabled = activeAdditional != null;
            // Both window sets are children of the same identity hinge. Repositioning changes no root pose/physics.
            inspection.WindowPivot.localPosition = activeAdditional != null ? activeAdditional.hinge : markV ? refinedHinge : originalHinge;
            var hit = window.colliders.OfType<BoxCollider>().First();
            hit.center = activeAdditional != null ? activeAdditional.triggerCenter : markV ? refinedWindowCenter : originalWindowCenter;
            hit.size = activeAdditional != null ? activeAdditional.triggerSize : markV ? refinedWindowSize : originalWindowSize;
            var visuals = refinedRenderers.Concat(additional);
            var allClipped = originalClipped.Concat(visuals.Where(r => !r.transform.IsChildOf(inspection.WindowPivot))).ToArray();
            var allWindow = originalWindowRenderers.Concat(visuals.Where(r => r.transform.IsChildOf(inspection.WindowPivot))).ToArray();
            var activeAir = activeAdditional != null ? activeAdditional.air : markV ? refinedAir : originalAir;
            var telephone = activeAdditional != null ? activeAdditional.telephone : markV ? refinedTelephone : originalTelephone;
            bool illustrativeTelephone = activeAdditional != null && (telephone == null || telephone.Length == 0);
            rear.Configure(activeAir, AirOutlet, telephone, TelephoneOutlet, response, illustrativeTelephone);
            inspection.Configure(inspection.WindowPivot, allClipped, allWindow, -1);
            // Configure resets the plane. Restore its direction before measuring the new
            // model's extent, so an oblique cut keeps the same normalized depth.
            inspection.SetSectionPlane(normal, 0f);
            inspection.SetSectionPlane(normal, relativeDepth * inspection.SectionRange);
            var guide = GetComponentInChildren<HelmetSectionInteractable>(true);
            if (guide) guide.RefreshGeometryCenter();
            if (response && TelephoneOutlet) response.position = TelephoneOutlet.position;
            var section = GetComponent<HelmetSectionSurface>(); if (section) section.Configure(activeAdditional != null ? activeAdditional.sectionData : sectionData, sectionMaterial);
            foreach (var clue in GetComponentsInChildren<HelmetExplorationHotspot>(true)) clue.RebindConnector(activeAir);
            if (Application.isPlaying)
                foreach (var demo in FindObjectsOfType<HelmetConnectionDemonstration>()) demo.RebindConnector(activeAir, rear);
        }
    }
}
