using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Hotspots.Tests
{
    public sealed class HelmetHotspotTourTests
    {
        GameObject root;
        HelmetHotspotTour tour;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Tour");
            tour = root.AddComponent<HelmetHotspotTour>();
            tour.Configure(3, null, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void VisitCountsEachHotspotOnce()
        {
            Assert.That(tour.Visit("window", "Window", "View"), Is.True);
            Assert.That(tour.Visit("window", "Window", "View"), Is.False);
            Assert.That(tour.VisitedCount, Is.EqualTo(1));
            Assert.That(tour.IsComplete, Is.False);
        }

        [Test]
        public void VisitCompletesAfterRequiredUniqueHotspots()
        {
            int completionCount = 0;
            tour.Completed += () => completionCount++;

            tour.Visit("window", "Window", "View");
            tour.Visit("shell", "Shell", "View");
            tour.Visit("base", "Base", "View");
            tour.Visit("base", "Base", "View");

            Assert.That(tour.IsComplete, Is.True);
            Assert.That(tour.VisitedCount, Is.EqualTo(3));
            Assert.That(completionCount, Is.EqualTo(1));
        }

        [Test]
        public void ResetTourClearsProgress()
        {
            tour.Visit("window", "Window", "View");
            tour.Visit("shell", "Shell", "View");

            tour.ResetTour();

            Assert.That(tour.VisitedCount, Is.Zero);
            Assert.That(tour.IsComplete, Is.False);
        }

        [Test]
        public void VisitRejectsMissingIdentifier()
        {
            Assert.That(tour.Visit("", "Window", "View"), Is.False);
            Assert.That(tour.Visit(null, "Window", "View"), Is.False);
            Assert.That(tour.VisitedCount, Is.Zero);
        }

        [Test]
        public void HotspotActivationMarksItVisited()
        {
            var hotspotObject = new GameObject("Window Hotspot");
            hotspotObject.transform.SetParent(root.transform);
            hotspotObject.AddComponent<SphereCollider>().isTrigger = true;
            hotspotObject.AddComponent<XRSimpleInteractable>();
            var hotspot = hotspotObject.AddComponent<HelmetHotspot>();
            hotspot.Configure(tour, "window", "Front window", "Look through the glass.", null);

            hotspot.Activate();

            Assert.That(hotspot.IsVisited, Is.True);
            Assert.That(tour.VisitedCount, Is.EqualTo(1));
        }

        [Test]
        public void ActivateEventMarksHotspotVisited()
        {
            var hotspotObject = new GameObject("Window Hotspot");
            hotspotObject.transform.SetParent(root.transform);
            hotspotObject.AddComponent<SphereCollider>().isTrigger = true;
            var interactable = hotspotObject.AddComponent<XRSimpleInteractable>();
            var hotspot = hotspotObject.AddComponent<HelmetHotspot>();
            hotspot.Configure(tour, "window", "Front window", "Look through the glass.", null);
            typeof(HelmetHotspot).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hotspot, null);

            interactable.activated.Invoke(null);

            Assert.That(hotspot.IsVisited, Is.True);
            Assert.That(tour.VisitedCount, Is.EqualTo(1));
        }

        [Test]
        public void ResetTourClearsHotspotVisitedState()
        {
            var hotspotObject = new GameObject("Window Hotspot");
            hotspotObject.transform.SetParent(root.transform);
            hotspotObject.AddComponent<SphereCollider>().isTrigger = true;
            hotspotObject.AddComponent<XRSimpleInteractable>();
            var hotspot = hotspotObject.AddComponent<HelmetHotspot>();
            hotspot.Configure(tour, "window", "Front window", "Look through the glass.", null);
            hotspot.Activate();

            tour.ResetTour();

            Assert.That(hotspot.IsVisited, Is.False);
        }

        [Test]
        public void StandalonePrefabContainsThreeSelectableHotspots()
        {
            const string path = "Assets/Museum/Features/HelmetHotspots/Prefabs/HelmetHotspotTour.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null);

            var instance = Object.Instantiate(prefab);
            try
            {
                var instanceTour = instance.GetComponent<HelmetHotspotTour>();
                var hotspots = instance.GetComponentsInChildren<HelmetHotspot>(true);

                Assert.That(instanceTour, Is.Not.Null);
                Assert.That(instanceTour.RequiredHotspots, Is.EqualTo(3));
                Assert.That(hotspots.Length, Is.EqualTo(3));
                foreach (var hotspot in hotspots)
                {
                    Assert.That(hotspot.GetComponent<XRSimpleInteractable>(), Is.Not.Null);
                    Assert.That(hotspot.GetComponent<Collider>().isTrigger, Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void TourEnablesTriggerQueriesAndRestoresPreviousValues()
        {
            var rayObject = new GameObject("Ray");
            var directObject = new GameObject("Direct");
            var compatibilityRoot = new GameObject("Compatibility Tour");
            try
            {
                var ray = rayObject.AddComponent<XRRayInteractor>();
                var direct = directObject.AddComponent<XRDirectInteractor>();
                ray.raycastTriggerInteraction = QueryTriggerInteraction.Ignore;
                direct.physicsTriggerInteraction = QueryTriggerInteraction.Ignore;

                var compatibilityTour = compatibilityRoot.AddComponent<HelmetHotspotTour>();
                compatibilityTour.EnableCompatibleInteractors();

                Assert.That(ray.raycastTriggerInteraction, Is.EqualTo(QueryTriggerInteraction.Collide));
                Assert.That(ray.allowHoveredActivate, Is.True);
                Assert.That(direct.physicsTriggerInteraction, Is.EqualTo(QueryTriggerInteraction.Collide));

                compatibilityTour.RestoreInteractorQueries();

                Assert.That(ray.raycastTriggerInteraction, Is.EqualTo(QueryTriggerInteraction.Ignore));
                Assert.That(ray.allowHoveredActivate, Is.False);
                Assert.That(direct.physicsTriggerInteraction, Is.EqualTo(QueryTriggerInteraction.Ignore));
            }
            finally
            {
                Object.DestroyImmediate(compatibilityRoot);
                Object.DestroyImmediate(directObject);
                Object.DestroyImmediate(rayObject);
            }
        }
    }
}
