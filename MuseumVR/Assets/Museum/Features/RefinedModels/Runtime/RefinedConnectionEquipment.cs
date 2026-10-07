using UnityEngine;
using UnityEngine.Rendering;

namespace Museum.RefinedModels
{
    /// <summary>
    /// Display-only equipment sampled by the existing connection demonstration's timeline.
    /// Source dimensions, small presentation scale and visible signal effects are independent.
    /// This component never advances a second clock or registers an XR interaction target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RefinedConnectionEquipment : MonoBehaviour
    {
        [SerializeField] Transform pump;
        [SerializeField] Transform surfaceTelephone;
        [SerializeField] Transform diverTelephone;
        [SerializeField] AnimationClip pumpCycle;
        [SerializeField] AnimationClip surfaceCycle;
        [SerializeField] AnimationClip diverCycle;
        [SerializeField] AudioClip airSound;
        [SerializeField] AudioClip voiceSound;
        [SerializeField, Min(.01f)] float pumpScale = .26f;
        [SerializeField, Min(.01f)] float surfaceScale = .45f;
        [SerializeField, Min(.01f)] float diverScale = .50f;
        [SerializeField, Range(0f, 1f)] float soundVolume = .25f;

        Transform airOutlet, telephoneOutlet, diverInlet, surfaceDiaphragm, diverDiaphragm;
        Transform pumpDisplay, telephoneDisplay, diverDisplay;
        Renderer[] pumpSurfaces = new Renderer[0], telephoneSurfaces = new Renderer[0], diverSurfaces = new Renderer[0];
        MaterialPropertyBlock attention;
        readonly LineRenderer[] sourceWaves = new LineRenderer[3], diverWaves = new LineRenderer[3];
        static readonly int FocusId = Shader.PropertyToID("_Focus");
        AudioSource soundSource;
        bool initialized, airMode;

        public Transform Pump => pump;
        public Transform SurfaceTelephone => surfaceTelephone;
        public Transform DiverTelephone => diverTelephone;
        public Transform AirOutlet { get { Initialize(); return airOutlet; } }
        public Transform TelephoneOutlet { get { Initialize(); return telephoneOutlet; } }
        public Transform DiverInlet { get { Initialize(); return diverInlet; } }
        public bool IsReady
        {
            get
            {
                Initialize();
                return pump && surfaceTelephone && diverTelephone && airOutlet && telephoneOutlet && diverInlet
                    && surfaceDiaphragm && diverDiaphragm && pumpCycle && surfaceCycle && diverCycle
                    && pumpSurfaces.Length > 0 && telephoneSurfaces.Length > 0 && diverSurfaces.Length > 0;
            }
        }
        public bool IsAudioPlaying => soundSource && soundSource.isPlaying;
        public float LastSampleTime { get; private set; }

        // Animation roots are the complete imported FBX instances, not just the named inner nodes.
        // A single FBX take contains all nine pump tracks and must be sampled together.
        public void Configure(Transform pumpRoot, Transform telephoneRoot, Transform diverRoot,
            AnimationClip pumpAnimation, AnimationClip telephoneAnimation, AnimationClip diverAnimation,
            AudioClip airAudio, AudioClip voiceAudio)
        {
            StopAudio();
            pump = pumpRoot; surfaceTelephone = telephoneRoot; diverTelephone = diverRoot;
            pumpCycle = pumpAnimation; surfaceCycle = telephoneAnimation; diverCycle = diverAnimation;
            airSound = airAudio; voiceSound = voiceAudio;
            initialized = false;
            Initialize();
        }

        public void SetPresentationScale(float pumpSize, float surfaceSize, float diverSize)
        {
            pumpScale = Mathf.Max(.01f, pumpSize);
            surfaceScale = Mathf.Max(.01f, surfaceSize);
            diverScale = Mathf.Max(.01f, diverSize);
        }

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            airOutlet = Find(pump, "Anchor_AirOutlet");
            telephoneOutlet = Find(surfaceTelephone, "Anchor_TelephoneOutlet");
            diverInlet = Find(diverTelephone, "Anchor_DiverTelephone");
            surfaceDiaphragm = Find(surfaceTelephone, "SurfaceDiaphragm");
            diverDiaphragm = Find(diverTelephone, "DiverDiaphragm");
            pumpSurfaces = Surfaces(pump); telephoneSurfaces = Surfaces(surfaceTelephone); diverSurfaces = Surfaces(diverTelephone);
            DisableIndependentPlayback(pump); DisableIndependentPlayback(surfaceTelephone); DisableIndependentPlayback(diverTelephone);
            // FBX takes can contain root transform curves. Keep display pose/scale outside their paths.
            pumpDisplay = DisplayPivot(pump, "Refined pump display pivot");
            telephoneDisplay = DisplayPivot(surfaceTelephone, "Refined telephone display pivot");
            diverDisplay = DisplayPivot(diverTelephone, "Refined diver display pivot");
        }

        public void InitializeFeedback(Material voiceMaterial)
        {
            Initialize();
            if (voiceMaterial && !sourceWaves[0])
            {
                for (int i = 0; i < sourceWaves.Length; i++)
                {
                    sourceWaves[i] = MakeWave("Refined surface voice wave " + i, voiceMaterial);
                    diverWaves[i] = MakeWave("Refined diver voice wave " + i, voiceMaterial);
                }
            }
            if (soundSource) return;
            soundSource = gameObject.AddComponent<AudioSource>();
            soundSource.playOnAwake = false; soundSource.loop = false;
            // Quiet illustrative tracks follow the existing SOUND switch; no forced headphone playback.
            soundSource.spatialBlend = 0f; soundSource.volume = soundVolume;
        }

        public void SetMode(bool air, bool showDiver)
        {
            Initialize(); airMode = air;
            if (pump) pump.gameObject.SetActive(air);
            if (surfaceTelephone) surfaceTelephone.gameObject.SetActive(!air);
            if (diverTelephone) diverTelephone.gameObject.SetActive(!air && showDiver);
            StopAudio(); HideWaves();
        }

        public void Position(Vector3 centre, Quaternion facing)
        {
            Initialize();
            Vector3 right = facing * Vector3.right;
            // Unity's imported FBX geometry retains its raw source axes in this pipeline.
            // X+90 maps raw -Z height to +Y and raw -Y front to -Z toward the viewer.
            Quaternion equipmentFacing = facing * Quaternion.Euler(90f, 0f, 0f);
            Position(pumpDisplay, centre + right * .50f - Vector3.up * .075f, equipmentFacing, pumpScale);
            Position(telephoneDisplay, centre + right * .44f - Vector3.up * .025f, equipmentFacing, surfaceScale);
            Position(diverDisplay, centre + right * .77f + Vector3.up * .045f, equipmentFacing, diverScale);
        }

        public Vector3 SourceLabelPosition => HighestPoint(airMode ? pumpSurfaces : telephoneSurfaces,
            airMode ? pump : surfaceTelephone) + Vector3.up * .045f;
        public Vector3 DiverLabelPosition => HighestPoint(diverSurfaces, diverTelephone) + Vector3.up * .045f;

        public void Evaluate(float elapsed, bool pumpMoving, bool surfaceSpeaking, bool diverSpeaking,
            bool surfaceReceiving, bool diverReceiving, bool moving, bool soundEnabled)
        {
            if (!IsReady) { StopAudio(); return; }
            LastSampleTime = elapsed;
            // The authored pump loop is two seconds per turn. 1.3 advances it at 0.65 turn/s.
            float pumpSeconds = moving && pumpMoving ? Mathf.Max(0f, elapsed - 2f) * 1.3f : 0f;
            // Unity may trim the FBX take's first frame. Normalize by its imported duration.
            Sample(pumpCycle, pump, pumpSeconds * pumpCycle.length / 2f);
            Sample(surfaceCycle, surfaceTelephone, moving && (surfaceSpeaking || surfaceReceiving) ? elapsed - (surfaceSpeaking ? 2f : 9f) : 0f);
            Sample(diverCycle, diverTelephone, moving && (diverSpeaking || diverReceiving) ? elapsed - (diverSpeaking ? 9f : 2f) : 0f);
            AnimateWaves(sourceWaves, surfaceDiaphragm, telephoneDisplay, elapsed, moving && surfaceSpeaking);
            AnimateWaves(diverWaves, diverDiaphragm, diverDisplay, elapsed, moving && diverSpeaking && diverTelephone.gameObject.activeInHierarchy);
            SyncAudio(elapsed, moving && soundEnabled);
        }

        public void SetAttention(bool air, float source, float destination)
        {
            ApplyFocus(pumpSurfaces, air ? source : 0f);
            ApplyFocus(telephoneSurfaces, air ? 0f : source);
            ApplyFocus(diverSurfaces, air ? 0f : destination);
        }

        public void Stop()
        {
            StopAudio(); HideWaves(); SetAttention(airMode, 0f, 0f);
        }
        void OnDisable() => Stop();
        void OnDestroy() => StopAudio();

        void SyncAudio(float elapsed, bool play)
        {
            AudioClip desired = airMode ? airSound : voiceSound;
            if (!play || !soundSource || !desired || elapsed >= desired.length || elapsed >= 16f)
            { StopAudio(); return; }
            if (soundSource.clip != desired) { soundSource.Stop(); soundSource.clip = desired; }
            float sampleTime = Mathf.Clamp(elapsed, 0f, Mathf.Max(0f, desired.length - .01f));
            if (!soundSource.isPlaying)
            {
                soundSource.time = sampleTime;
                soundSource.Play();
            }
            else if (Mathf.Abs(soundSource.time - sampleTime) > .15f) soundSource.time = sampleTime;
        }
        void StopAudio()
        {
            if (!soundSource) return;
            soundSource.Stop(); soundSource.clip = null;
        }

        LineRenderer MakeWave(string name, Material material)
        {
            var wave = new GameObject(name).AddComponent<LineRenderer>();
            wave.transform.SetParent(transform, false); wave.sharedMaterial = material;
            wave.useWorldSpace = true; wave.loop = true; wave.positionCount = 32;
            wave.startWidth = wave.endWidth = .0025f;
            wave.shadowCastingMode = ShadowCastingMode.Off; wave.receiveShadows = false;
            wave.gameObject.SetActive(false);
            return wave;
        }
        static void AnimateWaves(LineRenderer[] waves, Transform diaphragm, Transform display, float elapsed, bool emitting)
        {
            for (int i = 0; i < waves.Length; i++)
            {
                var wave = waves[i]; if (!wave) continue;
                wave.gameObject.SetActive(emitting && diaphragm);
                if (!emitting || !diaphragm) continue;
                float phase = Mathf.Repeat(elapsed * .72f + i / (float)waves.Length, 1f);
                // Raw front is -Y and height is -Z. Use the same display basis as the FBX
                // correction; these rings explain emission rather than acoustic physics.
                Vector3 normal = display.rotation * Vector3.down;
                Vector3 right = display.rotation * Vector3.right, up = display.rotation * Vector3.back;
                Vector3 centre = diaphragm.position + normal * (.012f + phase * .04f);
                float radius = Mathf.Lerp(.015f, .055f, phase);
                for (int p = 0; p < wave.positionCount; p++)
                {
                    float angle = p * Mathf.PI * 2f / wave.positionCount;
                    wave.SetPosition(p, centre + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius);
                }
            }
        }
        void HideWaves()
        {
            foreach (var wave in sourceWaves) if (wave) wave.gameObject.SetActive(false);
            foreach (var wave in diverWaves) if (wave) wave.gameObject.SetActive(false);
        }
        void ApplyFocus(Renderer[] surfaces, float value)
        {
            // Native-backed Unity objects must be created on the main thread, not during
            // MonoBehaviour construction/deserialization on Unity's loading thread.
            if (attention == null) attention = new MaterialPropertyBlock();
            foreach (var surface in surfaces)
            {
                if (!surface || !surface.sharedMaterial || !surface.sharedMaterial.HasProperty(FocusId)) continue;
                surface.GetPropertyBlock(attention); attention.SetFloat(FocusId, value); surface.SetPropertyBlock(attention);
            }
        }
        static void Sample(AnimationClip clip, Transform root, float seconds)
        {
            if (clip && root) clip.SampleAnimation(root.gameObject, Mathf.Repeat(Mathf.Max(0f, seconds), Mathf.Max(.001f, clip.length)));
        }
        static void Position(Transform root, Vector3 position, Quaternion rotation, float scale)
        {
            if (!root) return;
            root.SetPositionAndRotation(position, rotation); root.localScale = Vector3.one * scale;
        }
        static Transform DisplayPivot(Transform model, string name)
        {
            if (!model) return null;
            if (model.parent && model.parent.name == name) return model.parent;
            var pivot = new GameObject(name).transform;
            pivot.SetParent(model.parent, false);
            model.SetParent(pivot, false);
            return pivot;
        }
        static Vector3 HighestPoint(Renderer[] renderers, Transform fallback)
        {
            Bounds bounds = default(Bounds); bool found = false;
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            return found ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) : fallback ? fallback.position : Vector3.zero;
        }
        static Renderer[] Surfaces(Transform root) => root ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        static Transform Find(Transform root, string name)
        {
            if (!root) return null;
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }
        static void DisableIndependentPlayback(Transform root)
        {
            if (!root) return;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var animation in root.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
        }
    }
}
