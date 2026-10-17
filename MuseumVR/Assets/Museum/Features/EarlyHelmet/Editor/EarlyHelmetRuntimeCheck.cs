#if UNITY_EDITOR
using System;
using System.Linq;
using Museum.Eras;
using Museum.Experience;
using Museum.Hotspots;
using Museum.Inspection;
using Museum.Localization;
using Museum.Refined;
using UnityEditor;
using UnityEngine;

namespace Museum.EarlyHelmet.EditorTools
{
    /// <summary>Focused, non-destructive Editor Play check of the saved three-era exhibit.</summary>
    [InitializeOnLoad]
    public static class EarlyHelmetRuntimeCheck
    {
        static int phase = -1, nextFrame;
        static RefinedHelmetBinding binding;
        static HelmetEraSwitcher era;
        static HelmetInspectionController inspection;
        static HelmetSectionSurface section;
        static HelmetEraVisitorContent content;
        static HelmetHotspot frontMarker;
        static Vector3 closedGlass, markerInGlass;

        static EarlyHelmetRuntimeCheck() => EditorApplication.update += Update;

        [MenuItem("Museum/Early Helmet/Check 1881-47 in current Play scene")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Open saved MuseumStart and enter Play first.");
            binding = UnityEngine.Object.FindObjectOfType<RefinedHelmetBinding>();
            Check(binding && binding.EarlyVisual != null, "saved 1839 visual is absent");
            era = binding.GetComponent<HelmetEraSwitcher>();
            inspection = binding.GetComponent<HelmetInspectionController>();
            section = binding.GetComponent<HelmetSectionSurface>();
            content = UnityEngine.Object.FindObjectOfType<HelmetEraVisitorContent>();
            frontMarker = binding.GetComponentsInChildren<HelmetHotspot>(true).Single(h => h.Identifier == "front-window");
            Check(era && inspection && section && content, "existing exhibit interaction components are missing");
            Check(binding.EarlyVisual.renderers.Length == 56 &&
                  binding.EarlyVisual.renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3) == 21696,
                "imported model no longer matches the reviewed 56 parts and 21,696 triangles");
            era.ShowEra(0);
            phase = 0;
            nextFrame = Time.frameCount + 8;
            Debug.Log("EARLY_HELMET_QA_RUNNING editor Play; using public era/window/section APIs, not simulated XR selection.");
        }

        static void Update()
        {
            if (phase < 0 || !EditorApplication.isPlaying || Time.frameCount < nextFrame) return;
            try
            {
                var early = binding.EarlyVisual;
                if (phase == 0)
                {
                    Check(era.CurrentIndex == 0 && binding.AppliedEraIndex == 0 && content.IsEarlyModelContentActive,
                        "1839 era/caption was not applied together");
                    Check(early.renderers.All(r => r && r.enabled && r.gameObject.activeInHierarchy) &&
                          binding.RefinedRenderers.All(r => !r.enabled) &&
                          binding.AdditionalVisual.renderers.All(r => !r.enabled), "other eras bleed into 1839");
                    var originalProperty = new SerializedObject(binding).FindProperty("originalRenderers");
                    for (int i = 0; i < originalProperty.arraySize; i++)
                    {
                        var original = originalProperty.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                        Check(!original || !original.enabled, "original placeholder renderer remains visible: " + original.name);
                    }
                    Check(content.Instructions && content.Instructions.text.Contains("1881-47"), "sourced caption is missing");
                    foreach (var language in new[] { MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
                    {
                        var translated = MuseumText.Table.TranslateEnglish(HelmetEraVisitorContent.EarlyModelInstructions, language);
                        Check(translated != HelmetEraVisitorContent.EarlyModelInstructions && translated.Contains("1881-47"),
                            "1839 caption has no complete translation / " + language);
                    }
                    closedGlass = early.glass.bounds.center;
                    markerInGlass = early.glass.transform.InverseTransformPoint(frontMarker.transform.position);
                    inspection.SetCutaway(true);
                    inspection.SetSectionPlane(new Vector3(.25f, .3f, -.9f).normalized, inspection.SectionRange * .25f);
                    section.Refresh();
                    phase = 1;
                }
                else if (phase == 1)
                {
                    Check(section.TriangleCount > 0, "1839 section produced no triangles");
                    inspection.SetWindowAngle(45f);
                    phase = 2;
                }
                else if (phase == 2)
                {
                    Check(Vector3.Distance(early.glass.bounds.center, closedGlass) > .02f, "virtual front panel did not move");
                    Check(Vector3.Distance(early.glass.transform.InverseTransformPoint(frontMarker.transform.position), markerInGlass) < .002f,
                        "front observation marker did not follow the panel");
                    era.ShowEra(1);
                    phase = 3;
                }
                else if (phase == 3)
                {
                    Check(binding.AppliedEraIndex == 1 && !content.IsEarlyModelContentActive &&
                          early.renderers.All(r => !r.enabled) && binding.RefinedRenderers.All(r => r.enabled),
                        "Mark V did not restore cleanly");
                    era.ShowEra(2);
                    phase = 4;
                }
                else if (phase == 4)
                {
                    Check(binding.AppliedEraIndex == 2 && binding.AdditionalVisual.renderers.All(r => r.enabled) &&
                          early.renderers.All(r => !r.enabled), "SuperLite did not restore cleanly");
                    inspection.SetWindowAngle(0f);
                    inspection.SetCutaway(false);
                    era.ShowEra(0);
                    phase = 5;
                }
                else
                {
                    Check(binding.AppliedEraIndex == 0 && content.IsEarlyModelContentActive &&
                          early.renderers.All(r => r.enabled), "1839 round-trip failed");
                    phase = -1;
                    Debug.Log("EARLY_HELMET_QA_PASS model=56/21696; index0/1/2/0; trilingual caption; section; virtual panel; marker tracking. Editor simulation only; no headset or build claim.");
                    return;
                }
                nextFrame = Time.frameCount + 8;
            }
            catch (Exception error)
            {
                phase = -1;
                Debug.LogError("EARLY_HELMET_QA_FAIL " + error);
            }
        }

        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
