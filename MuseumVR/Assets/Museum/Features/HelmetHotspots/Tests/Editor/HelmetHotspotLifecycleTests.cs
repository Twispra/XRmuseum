using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Museum.Hotspots.Tests
{
    public sealed class HelmetHotspotLifecycleTests
    {
        const string PrefabPath = "Assets/Museum/Features/HelmetHotspots/Prefabs/HelmetHotspotTour.prefab";
        static readonly Color Available = new Color(.12f, .8f, .9f, .75f);
        static readonly Color Visited = new Color(.25f, .95f, .5f, .85f);
        readonly List<GameObject> instances = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator EnterRuntime()
        {
            // The test runner re-enters this iterator after the Play Mode domain reload.
            if (Application.isPlaying) yield break;

            // Keep the fixture independent of the scene left open by other Editor tests.
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                Assert.That(EditorSceneManager.GetSceneAt(i).isDirty, Is.False,
                    "Save or close modified scenes before running the saved-prefab lifecycle tests.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeaveRuntime()
        {
            // Instances are created after EnterPlayMode and disposed before ExitPlayMode.
            // A replay after exit may have a fresh, empty list; cleanup remains harmless.
            foreach (var instance in instances)
            {
                if (!instance) continue;
                if (Application.isPlaying) Object.Destroy(instance);
                else Object.DestroyImmediate(instance);
            }
            instances.Clear();
            if (EditorApplication.isPlaying)
            {
                yield return null;
                yield return new ExitPlayMode();
            }
        }

        [UnityTest]
        public IEnumerator SavedPrefabResetsAcrossDisabledAndRepeatedlyEnabledHotspots()
        {
            var tour = InstantiateSavedTour();
            var hotspots = tour.GetComponentsInChildren<HelmetHotspot>(true);
            yield return null;

            Assert.That(hotspots.Length, Is.EqualTo(3));
            foreach (var hotspot in hotspots)
            {
                AssertState(hotspot, false);
                hotspot.Activate();
                AssertState(hotspot, true);
            }
            Assert.That(tour.VisitedCount, Is.EqualTo(3));
            Assert.That(tour.IsComplete, Is.True);

            // These instances have never called Configure: all references came from the saved prefab.
            tour.ResetTour();
            Assert.That(tour.VisitedCount, Is.Zero);
            Assert.That(tour.IsComplete, Is.False);
            foreach (var hotspot in hotspots) AssertState(hotspot, false);

            var target = hotspots[0];
            target.Activate();
            target.gameObject.SetActive(false);
            yield return null;
            tour.ResetTour();
            Assert.That(target.IsVisited, Is.False, "A disabled hotspot must read the reset tour state.");
            target.gameObject.SetActive(true);
            yield return null;
            AssertState(target, false);
            Assert.That(tour.VisitedCount, Is.Zero);

            target.Activate();
            target.gameObject.SetActive(false);
            yield return null;
            target.gameObject.SetActive(true);
            yield return null;
            AssertState(target, true);
            Assert.That(tour.VisitedCount, Is.EqualTo(1), "Disabling a hotspot alone must preserve its visit.");

            for (int cycle = 0; cycle < 3; cycle++)
            {
                target.enabled = false;
                yield return null;
                target.enabled = true;
                yield return null;
                target.Activate();
                AssertState(target, true);
                Assert.That(tour.VisitedCount, Is.EqualTo(1), "Repeated enable/activation must not duplicate visits.");
            }
            tour.ResetTour();
            Assert.That(tour.VisitedCount, Is.Zero);
            foreach (var hotspot in hotspots) AssertState(hotspot, false);
        }

        [UnityTest]
        public IEnumerator SavedPrefabReconfigurationFollowsOnlyTheCurrentTourAndIdentifier()
        {
            var originalTour = InstantiateSavedTour();
            var replacementTour = InstantiateSavedTour();
            var hotspot = originalTour.GetComponentsInChildren<HelmetHotspot>(true)[0];
            var renderer = hotspot.GetComponent<Renderer>();
            yield return null;

            hotspot.Activate();
            AssertState(hotspot, true);
            Assert.That(originalTour.VisitedCount, Is.EqualTo(1));
            hotspot.Configure(replacementTour, "rebound", "Rebound", "Lifecycle fixture", renderer);
            AssertState(hotspot, false);
            Assert.That(replacementTour.VisitedCount, Is.Zero);
            hotspot.Activate();
            AssertState(hotspot, true);
            Assert.That(replacementTour.VisitedCount, Is.EqualTo(1));

            originalTour.ResetTour();
            AssertState(hotspot, true);
            Assert.That(replacementTour.VisitedCount, Is.EqualTo(1), "The old tour cannot clear the new tour's visit.");

            for (int configuration = 0; configuration < 3; configuration++)
            {
                hotspot.Configure(replacementTour, "rebound", "Rebound", "Lifecycle fixture", renderer);
                AssertState(hotspot, true);
                hotspot.Activate();
                AssertState(hotspot, true);
                Assert.That(replacementTour.VisitedCount, Is.EqualTo(1), "Same-ID configuration must keep model and marker consistent.");
            }
            replacementTour.ResetTour();
            AssertState(hotspot, false);
            Assert.That(replacementTour.VisitedCount, Is.Zero);

            hotspot.gameObject.SetActive(false);
            yield return null;
            hotspot.Configure(originalTour, "rebound-while-disabled", "Disabled", "Lifecycle fixture", renderer);
            originalTour.Visit("rebound-while-disabled", "Disabled", "Lifecycle fixture");
            Assert.That(hotspot.IsVisited, Is.True, "The visit state must follow the current tour even while disabled.");
            originalTour.ResetTour();
            Assert.That(hotspot.IsVisited, Is.False);
            hotspot.gameObject.SetActive(true);
            yield return null;
            AssertState(hotspot, false);
            hotspot.Activate();
            AssertState(hotspot, true);
            Assert.That(originalTour.VisitedCount, Is.EqualTo(1));
            replacementTour.ResetTour();
            AssertState(hotspot, true);
            originalTour.ResetTour();
            AssertState(hotspot, false);
            Assert.That(originalTour.VisitedCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ExpiredExplanationStaysHiddenAfterTourComponentAndObjectReenable()
        {
            var tour = InstantiateSavedTour();
            var hotspot = tour.GetComponentsInChildren<HelmetHotspot>(true)[0];

            foreach (bool wholeObject in new[] { false, true })
            {
                hotspot.Activate();
                tour.AdvanceExplanation(31f);
                AssertExplanationOpacity(tour, 0f);

                SetTourEnabled(tour, wholeObject, false);
                yield return null;
                SetTourEnabled(tour, wholeObject, true);
                AssertExplanationOpacity(tour, 0f);
                tour.AdvanceExplanation(60f);
                AssertExplanationOpacity(tour, 0f);

                hotspot.Activate();
                AssertExplanationOpacity(tour, 1f);
                Assert.That(tour.VisitedCount, Is.EqualTo(1), "Reselection restarts the explanation without adding a visit.");
                tour.AdvanceExplanation(30.5f);
                AssertExplanationOpacity(tour, .5f);

                tour.ResetTour();
                AssertExplanationOpacity(tour, 1f);
                Assert.That(tour.VisitedCount, Is.Zero);
                tour.AdvanceExplanation(60f);
                AssertExplanationOpacity(tour, 1f);
            }
        }

        [UnityTest]
        public IEnumerator FadingExplanationKeepsItsOpacityAfterTourComponentAndObjectReenable()
        {
            var tour = InstantiateSavedTour();
            var hotspot = tour.GetComponentsInChildren<HelmetHotspot>(true)[0];

            foreach (bool wholeObject in new[] { false, true })
            {
                hotspot.Activate();
                tour.AdvanceExplanation(30.5f);
                AssertExplanationOpacity(tour, .5f);

                SetTourEnabled(tour, wholeObject, false);
                yield return null;
                SetTourEnabled(tour, wholeObject, true);
                AssertExplanationOpacity(tour, .5f);

                tour.AdvanceExplanation(.25f);
                AssertExplanationOpacity(tour, .25f);
                tour.AdvanceExplanation(.25f);
                AssertExplanationOpacity(tour, 0f);
            }
        }

        [UnityTest]
        public IEnumerator HoldingExplanationKeepsItsRemainingTimeAfterTourComponentAndObjectReenable()
        {
            var tour = InstantiateSavedTour();
            var hotspot = tour.GetComponentsInChildren<HelmetHotspot>(true)[0];

            foreach (bool wholeObject in new[] { false, true })
            {
                hotspot.Activate();
                tour.AdvanceExplanation(29.5f);
                AssertExplanationOpacity(tour, 1f);

                SetTourEnabled(tour, wholeObject, false);
                yield return null;
                SetTourEnabled(tour, wholeObject, true);
                AssertExplanationOpacity(tour, 1f);

                tour.AdvanceExplanation(.75f);
                AssertExplanationOpacity(tour, .75f);
                tour.AdvanceExplanation(.75f);
                AssertExplanationOpacity(tour, 0f);
            }
        }

        static void SetTourEnabled(HelmetHotspotTour tour, bool wholeObject, bool enabled)
        {
            if (wholeObject) tour.gameObject.SetActive(enabled);
            else tour.enabled = enabled;
        }

        static void AssertExplanationOpacity(HelmetHotspotTour tour, float expected)
        {
            Assert.That(tour.ExplanationOpacity, Is.EqualTo(expected).Within(.001f), "Explanation lifetime opacity");
            var panel = tour.transform.Find("Information Panel");
            Assert.That(panel, Is.Not.Null);
            foreach (var label in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
                Assert.That(label.color.a, Is.EqualTo(expected).Within(.001f), label.name + " visible opacity");

            var backing = panel.Find("Panel Backing").GetComponent<Renderer>();
            var material = backing.sharedMaterial;
            string colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            float expectedAlpha = material.GetColor(colorProperty).a * expected;
            var block = new MaterialPropertyBlock();
            backing.GetPropertyBlock(block);
            foreach (string property in new[] { "_BaseColor", "_Color" })
                Assert.That(block.GetColor(property).a, Is.EqualTo(expectedAlpha).Within(.001f),
                    "Explanation backing " + property + " visible opacity");
        }

        HelmetHotspotTour InstantiateSavedTour()
        {
            Assert.That(Application.isPlaying, Is.True);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "The delivered prefab must exist; do not generate replacement assets.");
            var instance = Object.Instantiate(prefab);
            instances.Add(instance);
            var tour = instance.GetComponent<HelmetHotspotTour>();
            Assert.That(tour, Is.Not.Null);
            return tour;
        }

        static void AssertState(HelmetHotspot hotspot, bool visited)
        {
            Assert.That(hotspot.IsVisited, Is.EqualTo(visited), hotspot.name + " visit state");
            var renderer = hotspot.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Color expected = visited ? Visited : Available;
            foreach (string property in new[] { "_BaseColor", "_Color" })
                Assert.That(Vector4.Distance(block.GetColor(property), expected), Is.LessThan(.0001f),
                    hotspot.name + " " + property + " must match its visit state.");
        }
    }
}
