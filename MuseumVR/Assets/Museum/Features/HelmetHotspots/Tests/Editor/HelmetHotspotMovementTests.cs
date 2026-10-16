using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Object = UnityEngine.Object;

namespace Museum.Hotspots.Tests
{
    public sealed class HelmetHotspotMovementTests
    {
        GameObject helmet;
        HelmetHotspotTour tour;

        [SetUp]
        public void SetUp()
        {
            helmet = new GameObject("Moving helmet");
            helmet.AddComponent<XRGrabInteractable>();
            var tourObject = new GameObject("Hotspot tour");
            tourObject.transform.SetParent(helmet.transform);
            tour = tourObject.AddComponent<HelmetHotspotTour>();
            tour.Configure(3, null, null, null);
            Callback(tour, "OnEnable");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(helmet);

        [TestCase(false)]
        [TestCase(true)]
        public void WholeHelmetMotionHidesWithoutGrabAndStaysHidden(bool rotation)
        {
            tour.Visit("window", "Window", "View");
            var grab = helmet.GetComponent<XRGrabInteractable>();
            int grabEvents = 0;
            grab.selectEntered.AddListener(_ => grabEvents++);
            if (rotation) helmet.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            else helmet.transform.position += Vector3.right * .1f;
            Callback(tour, "LateUpdate");
            Assert.That(grabEvents, Is.Zero);
            Assert.That(tour.ExplanationOpacity, Is.Zero);
            tour.AdvanceExplanation(60f);
            Callback(tour, "LateUpdate");
            tour.RefreshSelectedContent("window", "Translated window", "Translated view");
            Assert.That(tour.ExplanationOpacity, Is.Zero, "Stopping and changing language must not restore old text.");
            Assert.That(tour.VisitedCount, Is.EqualTo(1));
            Assert.That(tour.HasVisited("window"), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SmallMotionAccumulatesFromVisitButPoseNoiseDoesNotHide(bool rotation)
        {
            tour.Visit("window", "Window", "View");
            for (int i = 0; i < 10; i++)
            {
                if (rotation) helmet.transform.rotation = Quaternion.Euler(0f, i % 2 == 0 ? .1f : 0f, 0f);
                else helmet.transform.position = Vector3.right * (i % 2 == 0 ? .0005f : 0f);
                Callback(tour, "LateUpdate");
                Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f), "Bounded pose noise should not hide the explanation.");
            }
            for (int i = 1; i <= 6; i++)
            {
                if (rotation) helmet.transform.rotation = Quaternion.Euler(0f, i * .1f, 0f);
                else helmet.transform.position = Vector3.right * (i * .0005f);
                Callback(tour, "LateUpdate");
            }
            Assert.That(tour.ExplanationOpacity, Is.Zero, "Repeated small movement must accumulate rather than rebaseline every frame.");
        }

        [Test]
        public void RevisitCapturesNewPoseAndPreservesThirtySecondLifetime()
        {
            tour.Visit("window", "Window", "View");
            helmet.transform.position += Vector3.right;
            Callback(tour, "LateUpdate");
            Assert.That(tour.ExplanationOpacity, Is.Zero);
            Assert.That(tour.Visit("window", "Window", "View"), Is.False);
            Callback(tour, "LateUpdate");
            tour.AdvanceExplanation(30f);
            Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f));
            tour.AdvanceExplanation(.5f);
            Assert.That(tour.ExplanationOpacity, Is.EqualTo(.5f).Within(.001f));
            tour.AdvanceExplanation(.5f);
            Assert.That(tour.ExplanationOpacity, Is.Zero);
            Assert.That(tour.VisitedCount, Is.EqualTo(1));
            tour.Visit("shell", "Shell", "View");
            Callback(tour, "LateUpdate");
            Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f));
            Assert.That(tour.VisitedCount, Is.EqualTo(2));
        }

        [Test]
        public void MotionDuringDisableIsDetectedOnReenable()
        {
            tour.Visit("window", "Window", "View");
            Callback(tour, "OnDisable");
            helmet.transform.position += Vector3.forward * .1f;
            Callback(tour, "OnEnable");
            Callback(tour, "LateUpdate");
            Assert.That(tour.ExplanationOpacity, Is.Zero);
        }

        [Test]
        public void ChildMotionDoesNotHideAndResetRetainsPrompt()
        {
            tour.Visit("window", "Window", "View");
            var window = new GameObject("Window");
            window.transform.SetParent(helmet.transform);
            window.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Callback(tour, "LateUpdate");
            Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f));
            tour.ResetTour();
            helmet.transform.position += Vector3.right;
            Callback(tour, "LateUpdate");
            Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f));
            Assert.That(tour.VisitedCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProductionTurnAndTiltHideWithoutGrab(bool tilt)
        {
            WithMainScene(sceneTour =>
            {
                var inspection = sceneTour.GetComponentsInParent<MonoBehaviour>(true)
                    .Single(c => c.GetType().FullName == "Museum.Inspection.HelmetInspectionController");
                var grab = sceneTour.GetComponentInParent<XRGrabInteractable>();
                int grabEvents = 0;
                grab.selectEntered.AddListener(_ => grabEvents++);
                sceneTour.Visit("window", "Window", "View");
                var before = inspection.transform.rotation;
                if (tilt) inspection.GetType().GetMethod("RotateDisplayAround").Invoke(inspection, new object[] { Vector3.right, 15f });
                else inspection.GetType().GetMethod("RotateDisplay").Invoke(inspection, new object[] { 30f });
                Assert.That(Quaternion.Angle(before, inspection.transform.rotation), Is.GreaterThan(14f));
                Assert.That(grabEvents, Is.Zero);
                Callback(sceneTour, "LateUpdate");
                Assert.That(sceneTour.ExplanationOpacity, Is.Zero);
                Assert.That(sceneTour.VisitedCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void ProductionExhibitResetHidesButVisitorResetViewDoesNot()
        {
            WithMainScene(sceneTour =>
            {
                var grab = sceneTour.GetComponentInParent<XRGrabInteractable>();
                var reset = grab.GetComponents<MonoBehaviour>().Single(c => c.GetType().FullName == "Museum.HelmetExhibitReset");
                grab.transform.position += Vector3.right * .2f;
                sceneTour.Visit("window", "Window", "View");
                var before = grab.transform.position;
                reset.GetType().GetMethod("RequestResetToDisplay").Invoke(reset, null);
                Assert.That(Vector3.Distance(before, grab.transform.position), Is.GreaterThan(.1f));
                Callback(sceneTour, "LateUpdate");
                Assert.That(sceneTour.ExplanationOpacity, Is.Zero);

                var origin = Object.FindObjectsOfType<MonoBehaviour>(true).Single(c => c.GetType().FullName == "Unity.XR.CoreUtils.XROrigin");
                var recoveryType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Museum.Exploration.VisitorViewRecovery")).First(t => t != null);
                var recovery = origin.gameObject.AddComponent(recoveryType);
                recoveryType.GetMethod("Configure").Invoke(recovery, new object[] { origin, null });
                before = grab.transform.position;
                var rotation = grab.transform.rotation;
                origin.transform.position += Vector3.right;
                sceneTour.Visit("window", "Window", "View");
                Assert.That(recoveryType.GetMethod("ResetView").Invoke(recovery, null), Is.EqualTo(true));
                Assert.That(Vector3.Distance(before, grab.transform.position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(rotation, grab.transform.rotation), Is.LessThan(.01f));
                Callback(sceneTour, "LateUpdate");
                Assert.That(sceneTour.ExplanationOpacity, Is.EqualTo(1f), "Visitor movement is not helmet movement.");
                Assert.That(sceneTour.VisitedCount, Is.EqualTo(1));
            });
        }

        static void WithMainScene(Action<HelmetHotspotTour> check)
        {
            EditorSceneManager.OpenScene("Assets/Museum/Scenes/MuseumStart.unity", OpenSceneMode.Single);
            try
            {
                var sceneTour = Object.FindObjectOfType<HelmetHotspotTour>(true);
                Assert.That(sceneTour, Is.Not.Null);
                Callback(sceneTour, "OnDisable");
                Callback(sceneTour, "OnEnable");
                check(sceneTour);
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        static void Callback(HelmetHotspotTour target, string name) =>
            typeof(HelmetHotspotTour).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
