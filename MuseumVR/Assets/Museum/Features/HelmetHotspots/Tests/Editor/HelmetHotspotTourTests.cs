using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        public void ExplanationFadesAfterThirtySecondsWithoutAnotherVisit()
        {
            var panel = new GameObject("Information Panel");
            panel.transform.SetParent(root.transform);
            var information = new GameObject("Information").AddComponent<TMPro.TextMeshPro>();
            information.transform.SetParent(panel.transform);
            tour.Configure(3, information, null, null);

            tour.Visit("window", "Window", "View");
            tour.AdvanceExplanation(30f);

            Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f));
            Assert.That(information.color.a, Is.EqualTo(1f));

            tour.AdvanceExplanation(.5f);

            Assert.That(tour.ExplanationOpacity, Is.EqualTo(.5f).Within(.001f));
            Assert.That(information.color.a, Is.EqualTo(.5f).Within(.001f));

            tour.AdvanceExplanation(.5f);

            Assert.That(tour.ExplanationOpacity, Is.Zero);
            Assert.That(information.color.a, Is.Zero);
        }

        [Test]
        public void AnotherVisitRestoresExplanationAndRestartsTimeout()
        {
            var panel = new GameObject("Information Panel");
            panel.transform.SetParent(root.transform);
            var information = new GameObject("Information").AddComponent<TMPro.TextMeshPro>();
            information.transform.SetParent(panel.transform);
            tour.Configure(3, information, null, null);

            tour.Visit("window", "Window", "View");
            tour.AdvanceExplanation(30.75f);
            Assert.That(tour.ExplanationOpacity, Is.LessThan(1f));

            tour.Visit("window", "Window", "View");

            Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f));
            Assert.That(information.color.a, Is.EqualTo(1f));
            tour.AdvanceExplanation(30f);
            Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f));
        }

        [Test]
        public void GrabbingHelmetHidesExplanationUntilAnotherHotspotVisit()
        {
            var helmet = new GameObject("Helmet");
            var grab = helmet.AddComponent<XRGrabInteractable>();
            root.transform.SetParent(helmet.transform);
            var panel = new GameObject("Information Panel");
            panel.transform.SetParent(root.transform);
            var information = new GameObject("Information").AddComponent<TMPro.TextMeshPro>();
            information.transform.SetParent(panel.transform);
            tour.Configure(3, information, null, null);
            InvokeTourLifecycle(tour, "OnDisable");
            InvokeTourLifecycle(tour, "OnEnable");

            try
            {
                tour.Visit("window", "Window", "View");
                grab.selectEntered.Invoke(null);

                Assert.That(tour.ExplanationOpacity, Is.Zero);
                Assert.That(information.color.a, Is.Zero);

                grab.selectExited.Invoke(null);
                Assert.That(tour.ExplanationOpacity, Is.Zero);

                tour.Visit("shell", "Shell", "View");
                Assert.That(tour.ExplanationOpacity, Is.EqualTo(1f));
                Assert.That(information.color.a, Is.EqualTo(1f));
            }
            finally
            {
                root.transform.SetParent(null);
                Object.DestroyImmediate(helmet);
            }
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
        public void HotspotStatesUseNonColorVisualCues()
        {
            var hotspot = CreateVisualHotspot(out var interactable, out var hoverOutline, out var visitedCheck);
            var manager = CreateInteractionManager(interactable);
            var ray = CreateRegisteredRay(manager, "Ray");

            Assert.That(hoverOutline, Is.Not.Null);
            Assert.That(visitedCheck, Is.Not.Null);
            Assert.That(hoverOutline.GetComponent<LineRenderer>().loop, Is.True);
            Assert.That(hoverOutline.GetComponent<LineRenderer>().positionCount, Is.EqualTo(32));
            Assert.That(visitedCheck.GetComponent<LineRenderer>().loop, Is.False);
            Assert.That(visitedCheck.GetComponent<LineRenderer>().positionCount, Is.EqualTo(3));
            Assert.That(hoverOutline.gameObject.activeSelf, Is.False);
            Assert.That(visitedCheck.gameObject.activeSelf, Is.False);

            manager.HoverEnter((IXRHoverInteractor)ray, (IXRHoverInteractable)interactable);
            Assert.That(interactable.isHovered, Is.True);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.True);
            Assert.That(visitedCheck.gameObject.activeSelf, Is.False);
            Assert.That(tour.VisitedCount, Is.Zero, "Hovering alone must not visit the hotspot.");

            manager.HoverExit((IXRHoverInteractor)ray, (IXRHoverInteractable)interactable);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.False);

            hotspot.Activate();
            Assert.That(visitedCheck.gameObject.activeSelf, Is.True);

            manager.HoverEnter((IXRHoverInteractor)ray, (IXRHoverInteractable)interactable);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.True);
            Assert.That(visitedCheck.gameObject.activeSelf, Is.True);

            hotspot.SetMarkerVisible(false);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.False);
            Assert.That(visitedCheck.gameObject.activeSelf, Is.False);

            hotspot.SetMarkerVisible(true);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.True);
            Assert.That(visitedCheck.gameObject.activeSelf, Is.True);

            tour.ResetTour();
            Assert.That(hoverOutline.gameObject.activeSelf, Is.True, "Resetting visits must preserve the current hover.");
            Assert.That(visitedCheck.gameObject.activeSelf, Is.False);
            Assert.That(tour.VisitedCount, Is.Zero);

            manager.HoverExit((IXRHoverInteractor)ray, (IXRHoverInteractable)interactable);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void HoverOutlineRemainsUntilLastInteractorExits()
        {
            var hotspot = CreateVisualHotspot(out var interactable, out var hoverOutline, out var visitedCheck);
            var manager = CreateInteractionManager(interactable);
            var leftRay = CreateRegisteredRay(manager, "Left Ray");
            var rightRay = CreateRegisteredRay(manager, "Right Ray");

            manager.HoverEnter((IXRHoverInteractor)leftRay, (IXRHoverInteractable)interactable);
            manager.HoverEnter((IXRHoverInteractor)rightRay, (IXRHoverInteractable)interactable);
            Assert.That(interactable.interactorsHovering.Count, Is.EqualTo(2));
            Assert.That(hoverOutline.gameObject.activeSelf, Is.True);
            Assert.That(visitedCheck.gameObject.activeSelf, Is.False);

            hotspot.Activate();
            Assert.That(visitedCheck.gameObject.activeSelf, Is.True);

            manager.HoverExit((IXRHoverInteractor)leftRay, (IXRHoverInteractable)interactable);
            Assert.That(interactable.interactorsHovering.Count, Is.EqualTo(1));
            Assert.That(interactable.isHovered, Is.True);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.True, "The remaining ray must keep its hover outline.");
            Assert.That(visitedCheck.gameObject.activeSelf, Is.True);

            manager.HoverExit((IXRHoverInteractor)rightRay, (IXRHoverInteractable)interactable);
            Assert.That(interactable.interactorsHovering.Count, Is.Zero);
            Assert.That(interactable.isHovered, Is.False);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.False, "Only the final hover exit should remove the outline.");
            Assert.That(visitedCheck.gameObject.activeSelf, Is.True, "Hover exit must preserve the visit cue.");
            Assert.That(tour.VisitedCount, Is.EqualTo(1));
        }

        [Test]
        public void HoverOutlineRestoresAfterCallbacksRestartDuringHover()
        {
            var hotspot = CreateVisualHotspot(out var interactable, out var hoverOutline, out var visitedCheck);
            var manager = CreateInteractionManager(interactable);
            var ray = CreateRegisteredRay(manager, "Ray");

            manager.HoverEnter((IXRHoverInteractor)ray, (IXRHoverInteractable)interactable);
            hotspot.Activate();
            Assert.That(hoverOutline.gameObject.activeSelf, Is.True);
            Assert.That(visitedCheck.gameObject.activeSelf, Is.True);

            // EditMode does not run these MonoBehaviour callbacks automatically.
            // Restart only the hotspot callbacks; XRI retains the existing hover.
            InvokeHotspotLifecycle(hotspot, "OnDisable");
            Assert.That(interactable.interactorsHovering.Count, Is.EqualTo(1));
            Assert.That(hoverOutline.gameObject.activeSelf, Is.False);
            Assert.That(visitedCheck.gameObject.activeSelf, Is.False);

            InvokeHotspotLifecycle(hotspot, "OnEnable");
            Assert.That(interactable.interactorsHovering.Count, Is.EqualTo(1));
            Assert.That(interactable.isHovered, Is.True);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.True, "Re-enabling must restore an already active XRI hover.");
            Assert.That(visitedCheck.gameObject.activeSelf, Is.True);
            Assert.That(tour.VisitedCount, Is.EqualTo(1));

            manager.HoverExit((IXRHoverInteractor)ray, (IXRHoverInteractable)interactable);
            Assert.That(hoverOutline.gameObject.activeSelf, Is.False);
            Assert.That(visitedCheck.gameObject.activeSelf, Is.True);
            tour.ResetTour();
            Assert.That(visitedCheck.gameObject.activeSelf, Is.False);
        }

        HelmetHotspot CreateVisualHotspot(out XRSimpleInteractable interactable, out Transform hoverOutline, out Transform visitedCheck)
        {
            var hotspotObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hotspotObject.name = "Window Hotspot";
            hotspotObject.transform.SetParent(root.transform);
            hotspotObject.GetComponent<SphereCollider>().isTrigger = true;
            interactable = hotspotObject.AddComponent<XRSimpleInteractable>();
            var hotspot = hotspotObject.AddComponent<HelmetHotspot>();
            hotspot.Configure(tour, "window", "Front window", "Look through the glass.", hotspotObject.GetComponent<Renderer>());
            InvokeHotspotLifecycle(hotspot, "OnEnable");
            hoverOutline = hotspotObject.transform.Find("State Cues/Hover Outline");
            visitedCheck = hotspotObject.transform.Find("State Cues/Visited Check");
            return hotspot;
        }

        XRInteractionManager CreateInteractionManager(XRSimpleInteractable interactable)
        {
            var manager = root.AddComponent<XRInteractionManager>();
            interactable.interactionManager = manager;
            manager.RegisterInteractable((IXRInteractable)interactable);
            return manager;
        }

        XRRayInteractor CreateRegisteredRay(XRInteractionManager manager, string name)
        {
            var rayObject = new GameObject(name);
            rayObject.transform.SetParent(root.transform);
            var ray = rayObject.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            manager.RegisterInteractor((IXRInteractor)ray);
            return ray;
        }

        static void InvokeHotspotLifecycle(HelmetHotspot hotspot, string callback)
        {
            typeof(HelmetHotspot).GetMethod(callback, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hotspot, null);
        }

        static void InvokeTourLifecycle(HelmetHotspotTour target, string callback)
        {
            typeof(HelmetHotspotTour).GetMethod(callback, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
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
                var backing = instance.transform.Find("Information Panel/Panel Backing").GetComponent<Renderer>();

                Assert.That(instanceTour, Is.Not.Null);
                Assert.That(instanceTour.RequiredHotspots, Is.EqualTo(3));
                Assert.That(hotspots.Length, Is.EqualTo(3));
                Assert.That(backing.sharedMaterial.GetFloat("_Surface"), Is.EqualTo(1f));
                foreach (var hotspot in hotspots)
                {
                    Assert.That(hotspot.GetComponent<XRSimpleInteractable>(), Is.Not.Null);
                    Assert.That(hotspot.GetComponent<Collider>().isTrigger, Is.True);
                    Assert.That(hotspot.GetComponent<Renderer>().enabled, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HotspotMarkersFollowHelmetRayTargetVisibility()
        {
            var helmet = new GameObject("Helmet");
            root.transform.SetParent(helmet.transform);
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.transform.SetParent(helmet.transform);
            var external = new GameObject("External");
            var panel = new GameObject("Information Panel");
            panel.transform.SetParent(root.transform);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.SetParent(root.transform);
            marker.AddComponent<XRSimpleInteractable>();
            var hotspot = marker.AddComponent<HelmetHotspot>();
            hotspot.Configure(tour, "window", "Window", "View", marker.GetComponent<Renderer>());

            try
            {
                tour.SetHotspotsVisible(false);
                Assert.That(marker.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(tour.IsHelmetTarget(shell.transform), Is.True);
                Assert.That(tour.IsHelmetTarget(marker.transform), Is.True);
                Assert.That(tour.IsHelmetTarget(panel.transform), Is.False);
                Assert.That(tour.IsHelmetTarget(external.transform), Is.False);

                tour.SetHotspotsVisible(true);
                Assert.That(marker.GetComponent<Renderer>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(external);
                Object.DestroyImmediate(helmet);
            }
        }

        [Test]
        public void MainSceneContainsHelmetHotspotTourUnderExhibit()
        {
            EditorSceneManager.OpenScene("Assets/Museum/Scenes/MuseumStart.unity", OpenSceneMode.Single);

            var exhibit = GameObject.Find("HelmetExhibit");
            var sceneTour = Object.FindObjectOfType<HelmetHotspotTour>(true);

            Assert.That(exhibit, Is.Not.Null);
            Assert.That(sceneTour, Is.Not.Null);
            Assert.That(sceneTour.transform.IsChildOf(exhibit.transform), Is.True);
            Assert.That(sceneTour.GetComponentsInChildren<HelmetHotspot>(true).Length, Is.EqualTo(3));
        }

        [Test]
        public void MainSceneHelmetGrabHidesExplanationUntilNextVisit()
        {
            EditorSceneManager.OpenScene("Assets/Museum/Scenes/MuseumStart.unity", OpenSceneMode.Single);

            var sceneTour = Object.FindObjectOfType<HelmetHotspotTour>(true);
            Assert.That(sceneTour, Is.Not.Null);
            var grab = sceneTour.GetComponentInParent<XRGrabInteractable>();
            Assert.That(grab, Is.Not.Null);
            InvokeTourLifecycle(sceneTour, "OnDisable");
            InvokeTourLifecycle(sceneTour, "OnEnable");

            sceneTour.Visit("window", "Window", "View");
            grab.selectEntered.Invoke(null);

            Assert.That(sceneTour.ExplanationOpacity, Is.Zero);

            grab.selectExited.Invoke(null);
            Assert.That(sceneTour.ExplanationOpacity, Is.Zero);

            sceneTour.Visit("shell", "Shell", "View");
            Assert.That(sceneTour.ExplanationOpacity, Is.EqualTo(1f));
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
