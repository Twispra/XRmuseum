using System.Linq;
using System.Reflection;
using Museum.Hotspots;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Hotspots.Tests
{
    // Review-only fixture, copied into the isolated checkout's test assembly.
    // No tracked PR source changes; no saved scene writes or simulated devices.
    public sealed class Pr55MovementReview
    {
        [Test]
        public void TurnButtonMovementHidesExplanationWithoutGrabbing()
        {
            EditorSceneManager.OpenScene("Assets/Museum/Scenes/MuseumStart.unity", OpenSceneMode.Single);
            try
            {
                var tour = Object.FindObjectOfType<HelmetHotspotTour>(true);
                var grab = tour.GetComponentInParent<XRGrabInteractable>();
                var inspection = tour.GetComponentsInParent<MonoBehaviour>(true)
                    .Single(c => c.GetType().FullName == "Museum.Inspection.HelmetInspectionController");
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(HelmetHotspotTour).GetMethod("OnDisable", flags).Invoke(tour, null);
                typeof(HelmetHotspotTour).GetMethod("OnEnable", flags).Invoke(tour, null);
                int grabEvents = 0;
                grab.selectEntered.AddListener(_ => grabEvents++);
                tour.Visit("window", "Window", "View");
                Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f));
                Assert.That(grab.isSelected, Is.False);
                var before = inspection.transform.rotation;
                // This is the same production method used by the personal palette Turn buttons.
                inspection.GetType().GetMethod("RotateDisplay").Invoke(inspection, new object[] { 30f });
                var angle = Quaternion.Angle(before, inspection.transform.rotation);
                tour.AdvanceExplanation(.1f);
                Debug.Log("PR55 movement review: angle=" + angle + "; grabEvents=" + grabEvents + "; opacity=" + tour.ExplanationOpacity + "; visits=" + tour.VisitedCount);
                Assert.That(angle, Is.GreaterThan(29f), "Production rotation must actually occur.");
                Assert.That(grabEvents, Is.Zero, "Turn uses no whole-helmet grab event.");
                Assert.That(tour.ExplanationOpacity, Is.Zero, "Issue #54 requires explanation to hide when the helmet starts moving, including a Turn button.");
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
    }
}
