#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Museum.Experience;
using Museum.Exploration;
using Museum.Hotspots;
using Museum.Inspection;
using Museum.PhysicalHands.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Localization.EditorTools
{
    /// <summary>Saved-scene geometry and input regression. The caller owns the synthetic devices and Dynamic input queue.</summary>
    public static class HelmetHotspotInteractionValidation
    {
        const string Prompt = "Select a highlighted point to inspect the helmet.";
        const string Complete = "Exploration complete";
        static readonly MuseumLanguage[] Languages = { MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese, MuseumLanguage.English };

        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Selection { public string target, mode, selector; public int events; public Vector3 controllerPosition; public bool rootSelected; }
        [Serializable] sealed class Sample { public string phase, language, name, text, source; public int visited; public float fontSize; public bool visible, overflow; }
        [Serializable] sealed class Capture { public string file; public Vector3 position; public Quaternion rotation; public float fieldOfView = 48f; }
        [Serializable] sealed class Report
        {
            public string status = "running", unity, scene;
            public string inputMethod = "Caller-provided queued synthetic controller poses and grip states, through production Input Actions and saved XRDirectInteractor/XRRayInteractor. No SelectEnter, SelectExit, activated.Invoke or reflected lifecycle calls.";
            public string languageMethod = "Public LanguageChannel.SetLanguage API; this module does not claim to test the language buttons.";
            public string fixtureMethods = "Public ResetTour/ResetInspection/SetWindowOpen preconditions and cleanup, ReturnInspectionToDisplay for the smooth-return API regression, ReduceMotion=false for that fixture, component/GameObject disable-enable, and saved SerializedObject field reads. Helmet displacement/rotation and subsequent hotspot selection use production controller input. No asset writes or generator calls.";
            public string limitations = "Automated poses are not physical controller/headset testing. PNGs are diagnostic monocular panel close-ups, not the visitor camera or readability/user-study evidence.";
            public List<Check> checks = new List<Check>();
            public List<Selection> selections = new List<Selection>();
            public List<Sample> samples = new List<Sample>();
            public List<Capture> captures = new List<Capture>();
        }

        sealed class Context
        {
            public LocalizationTable table;
            public LanguageChannel channel;
            public HelmetHotspotTour tour;
            public HelmetInspectionController inspection;
            public HelmetExhibitReset reset;
            public HelmetRearConnections rear;
            public HelmetExplorationHotspot airClue;
            public XRGrabInteractable root;
            public HelmetHotspot[] hotspots;
            public TMP_Text information, progress, completion;
            public Action<bool, string, string, string> require;
            public Action<Vector3, Quaternion, bool> move;
            public Action park;
            public string output;
            public Report report = new Report();
            public void Check(bool passed, string name, string detail = "")
            {
                report.checks.Add(new Check { passed = passed, name = name, detail = detail });
                if (!passed)
                {
                    report.status = "failed";
                    File.WriteAllText(Path.Combine(output, "hotspot-interaction.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                }
                require(passed, "helmet hotspots", name, detail);
            }
        }

        public static IEnumerator Run(LocalizationTable table, LanguageChannel channel,
            Action<bool, string, string, string> require, Action<Vector3, Quaternion, bool> moveRight,
            Action parkHands, string output)
        {
            var c = new Context { table = table, channel = channel, require = require, move = moveRight, park = parkHands, output = output };
            Directory.CreateDirectory(output);
            c.report.unity = Application.unityVersion; c.report.scene = SceneManager.GetActiveScene().path;
            var tours = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HelmetHotspotTour>(true)).ToArray();
            c.Check(tours.Length == 1 && table && channel, "one saved scene tour and localization assets", "tours=" + tours.Length);
            c.tour = tours[0]; c.inspection = c.tour.GetComponentInParent<HelmetInspectionController>();
            c.root = c.tour.GetComponentInParent<XRGrabInteractable>();
            c.hotspots = c.tour.GetComponentsInChildren<HelmetHotspot>(true).OrderBy(h => Source(h, "identifier"), StringComparer.Ordinal).ToArray();
            c.Check(c.inspection && c.root && c.hotspots.Length == 3 && c.tour.RequiredHotspots == 3,
                "three saved hotspots attached to the inspectable helmet", "hotspots=" + c.hotspots.Length);
            c.information = Label(c.tour, "Information"); c.progress = Label(c.tour, "Progress"); c.completion = Label(c.tour, "Complete");
            c.Check(c.information && c.progress && c.completion, "saved panel labels exist");
            c.reset = c.root.GetComponent<HelmetExhibitReset>();
            c.rear = c.root.GetComponent<HelmetRearConnections>();
            c.airClue = c.root.GetComponentInChildren<HelmetExplorationHotspot>(true);
            c.Check(c.reset && c.rear && c.rear.AirOutlet && c.rear.TelephoneOutlet && c.airClue,
                "integrated saved helmet has smooth return, rear outlets and AIR clue");
            var previousLanguage = channel.Current;
            bool previousReduceMotion = c.inspection.ReduceMotion;
            bool previousActive = c.tour.gameObject.activeSelf, previousEnabled = c.tour.enabled;
            var markerEnabled = c.hotspots.Select(h => h.enabled).ToArray();
            bool completed = false;
            try
            {
                c.Check(previousActive && previousEnabled && markerEnabled.All(value => value), "saved tour and markers start enabled");
                ValidateCorpus(c);
                c.park(); c.inspection.ResetInspection(); c.tour.ResetTour(); channel.SetLanguage(MuseumLanguage.English);
                yield return Wait(.4f);
                foreach (var hotspot in c.hotspots)
                {
                    var target = hotspot.GetComponent<XRSimpleInteractable>();
                    var collider = target.colliders.Single();
                    var distance = target.GetDistance(collider.bounds.center);
                    c.Check(collider.isTrigger && distance.collider == collider && distance.distanceSqr < .000001f,
                        "saved trigger has finite surface distance / " + hotspot.name,
                        "mode=" + target.distanceCalculationMode + "; distanceSqr=" + distance.distanceSqr);
                }
                yield return LanguagesAt(c, 0, Prompt, "direct-progress-0", true);
                for (int index = 0; index < c.hotspots.Length; index++)
                {
                    var hotspot = c.hotspots[index];
                    yield return SelectHotspot(c, hotspot, false);
                    yield return LanguagesAt(c, index + 1, Detail(hotspot), "direct-progress-" + (index + 1), index == 2);
                }

                // Disable a real saved component, rather than invoking its lifecycle methods by reflection.
                var first = c.hotspots[0]; var firstTarget = first.GetComponent<XRSimpleInteractable>();
                var installedDistance = firstTarget.getDistanceOverride;
                first.enabled = false; yield return Wait(.08f);
                c.Check(installedDistance != null && firstTarget.getDistanceOverride == null,
                    "disabling marker restores the saved default distance callback");
                first.enabled = true; yield return Wait(.08f);
                c.Check(firstTarget.getDistanceOverride != null && firstTarget.GetDistance(first.transform.position).distanceSqr < .000001f && first.IsVisited,
                    "reenabling marker restores finite distance and its visited state");

                c.tour.enabled = false; channel.SetLanguage(MuseumLanguage.TraditionalChinese); yield return Wait(.1f);
                c.tour.enabled = true; yield return Wait(.15f);
                Audit(c, 3, Detail(c.hotspots[2]), "tour-component-reenabled");
                c.tour.gameObject.SetActive(false); channel.SetLanguage(MuseumLanguage.Chinese); yield return Wait(.1f);
                c.tour.gameObject.SetActive(true); yield return Wait(.15f);
                Audit(c, 3, Detail(c.hotspots[2]), "tour-object-reenabled");
                CheckMarkerColors(c, true, "completed state survives disable-enable");

                c.tour.gameObject.SetActive(false); c.tour.ResetTour(); channel.SetLanguage(MuseumLanguage.English);
                c.tour.gameObject.SetActive(true); yield return Wait(.15f);
                Audit(c, 0, Prompt, "reset-while-inactive"); CheckMarkerColors(c, false, "saved markers clear after inactive reset");
                for (int index = 0; index < c.hotspots.Length; index++)
                {
                    yield return SelectHotspot(c, c.hotspots[index], true);
                    Audit(c, index + 1, Detail(c.hotspots[index]), "ray-progress-" + (index + 1));
                }
                yield return SelectWindow(c, false);
                yield return SelectWindow(c, true);
                yield return OrdinaryGrab(c);
                c.Check(c.tour.VisitedCount == 3 && c.hotspots.All(h => h.IsVisited), "window and ordinary grabs do not change completed progress");
                yield return ReturnWithProgress(c, 1);
                yield return ReturnWithProgress(c, 3);
                completed = true;
            }
            finally
            {
                c.park();
                if (c.tour)
                {
                    c.tour.gameObject.SetActive(true); c.tour.enabled = true;
                    for (int i = 0; i < c.hotspots.Length; i++) if (c.hotspots[i]) c.hotspots[i].enabled = markerEnabled[i];
                    c.tour.ResetTour(); c.tour.enabled = previousEnabled; c.tour.gameObject.SetActive(previousActive);
                }
                if (c.inspection) { c.inspection.ResetInspection(); c.inspection.ReduceMotion = previousReduceMotion; }
                channel.SetLanguage(previousLanguage);
                c.report.status = completed ? "passed" : "failed";
                string path = Path.Combine(output, "hotspot-interaction.json");
                File.WriteAllText(path, JsonUtility.ToJson(c.report, true), new UTF8Encoding(false));
                Debug.Log("HELMET_HOTSPOT_INTERACTION_QA_" + c.report.status.ToUpperInvariant() + " " + path);
            }
        }

        static void ValidateCorpus(Context c)
        {
            var sources = new List<string> { Prompt, Complete };
            sources.AddRange(Enumerable.Range(0, 4).Select(count => count + " / 3 inspected"));
            sources.AddRange(c.hotspots.Select(Detail));
            foreach (string source in sources)
            {
                c.Check(c.table.ContainsEnglish(source), "hotspot source exists in table", source);
                var entry = c.table.Entries.First(e => e.English == source);
                foreach (var language in Languages)
                {
                    string translated = entry.For(language);
                    c.Check(!string.IsNullOrWhiteSpace(translated), "hotspot translation exists / " + language, source);
                    string missing = Missing(c.table.FontAsset, translated);
                    c.Check(missing.Length == 0, "stored hotspot glyphs / " + language + "/" + entry.Key, missing);
                }
            }
        }

        static IEnumerator LanguagesAt(Context c, int count, string information, string phase, bool capture)
        {
            foreach (var language in Languages)
            {
                var visited = c.hotspots.Select(h => h.IsVisited).ToArray();
                c.channel.SetLanguage(language); yield return Wait(.12f);
                Audit(c, count, information, phase + "/" + language);
                c.Check(c.hotspots.Select(h => h.IsVisited).SequenceEqual(visited), "language change preserves visited markers / " + phase + "/" + language);
                if (capture) CapturePanel(c, phase + "-" + language);
            }
        }

        static void Audit(Context c, int count, string information, string phase)
        {
            c.Check(c.tour.VisitedCount == count && c.tour.IsComplete == (count == 3) && c.hotspots.Count(h => h.IsVisited) == count,
                "progress retained / " + phase, "visited=" + c.tour.VisitedCount);
            c.Check(c.completion.gameObject.activeInHierarchy == (count == 3), "completion visibility / " + phase);
            CheckText(c, c.information, information, phase); CheckText(c, c.progress, count + " / 3 inspected", phase);
            // Inactive completion labels may defer a language event; inspect them once displayed.
            if (count == 3) CheckText(c, c.completion, Complete, phase);
        }

        static void CheckText(Context c, TMP_Text label, string source, string phase)
        {
            var binding = label.GetComponent<LocalizedSourceText>(); label.ForceMeshUpdate(true, true);
            string expected = c.table.TranslateEnglish(source, c.channel.Current);
            c.report.samples.Add(new Sample { phase = phase, language = c.channel.Current.ToString(), name = label.name,
                text = label.text, source = binding ? binding.SourceEnglish : "", visited = c.tour.VisitedCount,
                fontSize = label.fontSize, visible = label.gameObject.activeInHierarchy, overflow = label.isTextOverflowing });
            c.Check(binding && binding.SourceEnglish == source && label.text == expected, "exact dynamic translation / " + phase + "/" + label.name, "expected=" + expected + "; actual=" + label.text);
            c.Check(label.font == c.table.FontAsset && Missing(label.font, label.text).Length == 0, "font and stored glyphs / " + phase + "/" + label.name);
            c.Check(!label.isTextOverflowing && label.textInfo.characterCount > 0 && label.GetComponent<Renderer>().enabled,
                "visible layout / " + phase + "/" + label.name, "fontSize=" + label.fontSize + "; rect=" + label.rectTransform.sizeDelta);
            var camera = Camera.main;
            var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
            c.Check(corners.All(corner =>
            {
                var viewport = camera.WorldToViewportPoint(corner);
                return viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1;
            }), "panel text stays in production camera view / " + phase + "/" + label.name);
            var blockers = new HashSet<string>();
            foreach (var character in label.textInfo.characterInfo.Take(label.textInfo.characterCount))
            {
                if (!character.isVisible) continue;
                var center = label.transform.TransformPoint((character.bottomLeft + character.topRight) * .5f);
                var offset = center - camera.transform.position;
                if (Physics.Raycast(camera.transform.position, offset.normalized, out var hit, offset.magnitude - .002f, ~0, QueryTriggerInteraction.Ignore))
                    blockers.Add(hit.collider.name);
            }
            c.Check(blockers.Count == 0, "panel glyphs clear of solid scene geometry / " + phase + "/" + label.name, string.Join(", ", blockers));
        }

        static IEnumerator SelectHotspot(Context c, HelmetHotspot hotspot, bool ray)
        {
            var target = hotspot.GetComponent<XRSimpleInteractable>();
            string id = Source(hotspot, "identifier");
            Vector3 outward = c.root.transform.TransformDirection(id == "bonnet" ? Vector3.right : Vector3.back);
            Vector3 point = target.colliders.Single().bounds.center;
            Vector3 hand = ray ? point + outward * .65f : MuseumPhysicalHandsTestPose.OutsideTouch(
                target.colliders.Single(), MuseumPhysicalHandsTestPose.Controller(false), Quaternion.identity, outward, point);
            Quaternion rotation = ray ? Quaternion.LookRotation(point - hand, c.root.transform.up) : Quaternion.identity;
            yield return Select(c, target, hand, rotation, ray, "hotspot " + id);
            c.Check(hotspot.IsVisited, "selected hotspot records visit / " + id + "/" + (ray ? "ray" : "direct"));
            c.Check(!target.isHovered, "production hover exit after hands park / hotspot " + id + "/" + (ray ? "ray" : "direct"));
        }

        static IEnumerator SelectWindow(Context c, bool ray)
        {
            c.inspection.SetWindowOpen(false, true); yield return Wait(.1f);
            var window = c.root.GetComponentInChildren<HelmetWindowInteractable>(true);
            c.Check(window, "original window still exists");
            Vector3 point = window.colliders.Single().bounds.center;
            Vector3 hand = ray ? point - c.root.transform.forward * .65f : MuseumPhysicalHandsTestPose.WindowTouch(
                window.colliders.Single(), MuseumPhysicalHandsTestPose.Controller(false), Quaternion.identity, -c.root.transform.forward, point);
            Quaternion rotation = ray ? Quaternion.LookRotation(point - hand, c.root.transform.up) : Quaternion.identity;
            yield return Select(c, window, hand, rotation, ray, "original window outside marker");
            yield return Wait(.4f);
            c.Check(c.inspection.WindowAngle > 1f, "original window click still opens / " + (ray ? "ray" : "direct"), "angle=" + c.inspection.WindowAngle);
            c.inspection.SetWindowOpen(false, true);
        }

        static IEnumerator Select(Context c, XRSimpleInteractable target, Vector3 hand, Quaternion rotation, bool ray, string name)
        {
            int events = 0; IXRSelectInteractor selector = null;
            UnityAction<SelectEnterEventArgs> listener = args => { events++; selector = args.interactorObject; };
            target.selectEntered.AddListener(listener);
            try
            {
                c.park(); yield return Wait(.15f);
                c.move(hand, rotation, false); yield return Wait(.22f);
                if (!ray) yield return MuseumPhysicalHandsTestPose.WaitForPalm(MuseumPhysicalHandsTestPose.Controller(false), name);
                c.Check(target.isHovered, "production hover / " + name + "/" + (ray ? "ray" : "direct"), "target=" + target.name +
                    (ray ? "" : "; " + MuseumPhysicalHandsTestPose.SelectionDiagnostics(MuseumPhysicalHandsTestPose.Controller(false), target)));
                c.move(hand, rotation, true); yield return Wait(.18f);
                c.report.selections.Add(new Selection { target = target.name, mode = ray ? "ray" : "direct",
                    selector = selector == null ? "none" : selector.GetType().Name, events = events, controllerPosition = hand, rootSelected = c.root.isSelected });
                c.Check(events == 1 && target.isSelected && (ray ? selector is XRRayInteractor : selector is XRDirectInteractor),
                    "production select / " + name + "/" + (ray ? "ray" : "direct"), "events=" + events + "; selector=" + selector +
                    (ray ? "" : "; " + MuseumPhysicalHandsTestPose.SelectionDiagnostics(MuseumPhysicalHandsTestPose.Controller(false), target)));
                c.Check(!c.root.isSelected, "child selection does not grab helmet / " + name + "/" + (ray ? "ray" : "direct"));
                c.move(hand, rotation, false); yield return Wait(.15f);
                c.Check(!target.isSelected, "production release / " + name + "/" + (ray ? "ray" : "direct"));
                c.park(); yield return Wait(.12f);
            }
            finally { target.selectEntered.RemoveListener(listener); }
        }

        static Vector3 RootTouch(Context c)
        {
            var collider = c.root.colliders.OrderByDescending(col => col.bounds.size.sqrMagnitude).First();
            return MuseumPhysicalHandsTestPose.OutsideTouch(collider, MuseumPhysicalHandsTestPose.Controller(false),
                Quaternion.identity, -c.root.transform.right, collider.bounds.center);
        }

        static IEnumerator OrdinaryGrab(Context c)
        {
            Vector3 touch = RootTouch(c);
            c.park(); yield return Wait(.15f); c.move(touch, Quaternion.identity, false); yield return Wait(.22f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(MuseumPhysicalHandsTestPose.Controller(false), "ordinary shell grab");
            c.move(touch, Quaternion.identity, true); yield return Wait(.18f);
            c.Check(c.root.isSelected && c.root.interactorsSelecting[0] is XRDirectInteractor, "ordinary shell remains directly grabbable");
            Vector3 overMarker = c.hotspots[0].transform.position;
            c.move(overMarker, Quaternion.identity, true); yield return Wait(.18f);
            c.Check(c.root.isSelected && c.hotspots.All(h => !h.GetComponent<XRSimpleInteractable>().isSelected), "held helmet retains selection across a marker");
            c.move(touch, Quaternion.identity, true); yield return Wait(.18f);
            c.move(touch, Quaternion.identity, false); yield return Wait(.15f); c.park(); yield return Wait(.15f);
            c.inspection.ResetInspection(); yield return Wait(.25f);
        }

        sealed class RelativePose
        {
            public Transform target;
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        static IEnumerator ReturnWithProgress(Context c, int count)
        {
            string phase = "smooth-return-progress-" + count;
            c.park(); c.inspection.ResetInspection(); c.tour.ResetTour();
            c.inspection.ReduceMotion = false; c.channel.SetLanguage(MuseumLanguage.English);
            yield return Wait(.4f);
            for (int i = 0; i < count; i++) yield return SelectHotspot(c, c.hotspots[i], false);
            string information = Detail(c.hotspots[count - 1]);
            var visited = c.hotspots.Select(h => h.IsVisited).ToArray();
            Audit(c, count, information, phase + "/before");
            CheckMarkerColors(c, visited, phase + "/before");
            CheckAirAnchor(c, phase + "/before");

            var followed = new[] { c.tour.transform, c.airClue.transform, c.rear.AirOutlet, c.rear.TelephoneOutlet }
                .Concat(c.hotspots.Select(h => h.transform)).Select(target => new RelativePose {
                    target = target, position = c.root.transform.InverseTransformPoint(target.position),
                    rotation = Quaternion.Inverse(c.root.transform.rotation) * target.rotation, scale = target.localScale
                }).ToArray();
            var home = c.reset.HomePose;
            Vector3 touch = RootTouch(c);
            c.park(); yield return Wait(.15f); c.move(touch, Quaternion.identity, false); yield return Wait(.22f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(MuseumPhysicalHandsTestPose.Controller(false), "smooth return grab / " + phase);
            c.move(touch, Quaternion.identity, true); yield return Wait(.18f);
            c.Check(c.root.isSelected && c.root.interactorsSelecting[0] is XRDirectInteractor,
                "production grab before smooth return / " + phase);
            c.move(touch + new Vector3(.24f, .14f, -.12f), Quaternion.Euler(0, 28, 0), true);
            yield return Wait(.22f);
            Vector3 from = c.root.transform.position;
            c.Check(Vector3.Distance(from, home.position) > .05f && Quaternion.Angle(c.root.transform.rotation, home.rotation) > 5f,
                "production grab displaces and rotates helmet / " + phase);
            CheckRelativePoses(c, followed, phase + "/displaced");
            CheckAirAnchor(c, phase + "/displaced");

            // The API starts the real presentation return while the selecting hand still holds Grip.
            c.inspection.ReturnInspectionToDisplay();
            c.Check(c.reset.IsReturning && !c.root.isSelected && Vector3.Distance(c.root.transform.position, from) < .001f,
                "smooth return releases held exhibit without initial teleport / " + phase);
            yield return Wait(.08f);
            c.Check(c.reset.IsReturning && c.reset.ReturnProgress > 0 && c.reset.ReturnProgress < 1 &&
                Vector3.Distance(c.root.transform.position, from) > .001f && Vector3.Distance(c.root.transform.position, home.position) > .001f,
                "smooth return has intermediate pose with tour attached / " + phase);
            CheckRelativePoses(c, followed, phase + "/midway");
            CheckRetainedProgress(c, visited, information, phase + "/midway");
            CheckAirAnchor(c, phase + "/midway");
            c.Check(!c.root.isSelected, "held Grip cannot reacquire returning helmet / " + phase);
            float timeout = Time.realtimeSinceStartup + 2f;
            while (c.reset.IsReturning && Time.realtimeSinceStartup < timeout) yield return null;
            c.Check(!c.reset.IsReturning && Vector3.Distance(c.root.transform.position, home.position) < .02f &&
                Quaternion.Angle(c.root.transform.rotation, home.rotation) < 1f,
                "smooth return reaches home with visited tour / " + phase);
            CheckRelativePoses(c, followed, phase + "/home");
            CheckRetainedProgress(c, visited, information, phase + "/home");
            CheckAirAnchor(c, phase + "/home");
            touch = RootTouch(c);
            c.move(touch, Quaternion.identity, true); yield return Wait(.18f);
            c.Check(!c.root.isSelected, "held Grip remains blocked after smooth return until release / " + phase);
            c.move(touch, Quaternion.identity, false); yield return Wait(.15f); c.park(); yield return Wait(.15f);
            yield return LanguagesAt(c, count, information, phase + "/home-languages", false);

            // Revisit only the recorded markers so this also checks that selection never increments duplicates.
            for (int i = 0; i < count; i++)
            {
                yield return SelectHotspot(c, c.hotspots[i], false);
                yield return SelectHotspot(c, c.hotspots[i], true);
            }
            Audit(c, count, information, phase + "/after-direct-ray-revisit");
            CheckMarkerColors(c, visited, phase + "/after-direct-ray-revisit");
            yield return OrdinaryGrab(c);
            Audit(c, count, information, phase + "/after-ordinary-grab");
            CheckMarkerColors(c, visited, phase + "/after-ordinary-grab");
            c.tour.ResetTour(); yield return Wait(.12f);
            Audit(c, 0, Prompt, phase + "/explicit-tour-reset");
            CheckMarkerColors(c, false, phase + "/explicit-tour-reset");
        }

        static void CheckRelativePoses(Context c, RelativePose[] saved, string phase)
        {
            foreach (var pose in saved)
                c.Check(pose.target && Vector3.Distance(c.root.transform.InverseTransformPoint(pose.target.position), pose.position) < .0001f &&
                    Quaternion.Angle(Quaternion.Inverse(c.root.transform.rotation) * pose.target.rotation, pose.rotation) < .01f &&
                    Vector3.Distance(pose.target.localScale, pose.scale) < .0001f,
                    "helmet-local pose retained / " + phase + "/" + pose.target.name);
        }

        static void CheckRetainedProgress(Context c, bool[] visited, string information, string phase)
        {
            int count = visited.Count(value => value);
            c.Check(c.hotspots.Select(h => h.IsVisited).SequenceEqual(visited) && c.tour.VisitedCount == count &&
                c.tour.IsComplete == (count == c.tour.RequiredHotspots) && c.completion.gameObject.activeInHierarchy == c.tour.IsComplete,
                "exact visited and completion state retained / " + phase);
            var labels = count == c.tour.RequiredHotspots ? new[] { c.information, c.progress, c.completion } : new[] { c.information, c.progress };
            var sources = new[] { information, count + " / 3 inspected", Complete };
            for (int i = 0; i < labels.Length; i++)
            {
                var binding = labels[i].GetComponent<LocalizedSourceText>();
                c.Check(binding && binding.SourceEnglish == sources[i] && labels[i].text == c.table.TranslateEnglish(sources[i], c.channel.Current) &&
                    labels[i].font == c.table.FontAsset && Missing(labels[i].font, labels[i].text).Length == 0,
                    "dynamic detail and font retained during return / " + phase + "/" + labels[i].name);
            }
            CheckMarkerColors(c, visited, phase);
        }

        static void CheckAirAnchor(Context c, string phase)
        {
            var source = new SerializedObject(c.airClue);
            var connector = source.FindProperty("airConnector").objectReferenceValue as Renderer;
            Vector3 local = source.FindProperty("localAnchor").vector3Value;
            c.Check(connector && connector == c.rear.AirConnector && source.FindProperty("anchorReady").boolValue &&
                local.x < -.03f && Vector3.Distance(c.airClue.transform.position, c.root.transform.TransformPoint(local)) < .0001f,
                "AIR clue uses relocated fitting anchor / " + phase);
            // A rotated asymmetric fitting's world AABB centre is not a rigid local anchor.
            // Compare that authoring centre only at the saved orientation; motion uses the cached local point above.
            if (Quaternion.Angle(c.root.transform.rotation, c.reset.HomePose.rotation) < .01f)
                c.Check(connector && Vector3.Distance(local, c.root.transform.InverseTransformPoint(connector.bounds.center)) < .0001f,
                    "AIR cached anchor matches relocated saved renderer / " + phase);
        }

        static void CheckMarkerColors(Context c, bool[] visited, string phase)
        {
            for (int i = 0; i < c.hotspots.Length; i++)
            {
                var hotspot = c.hotspots[i]; var serialized = new SerializedObject(hotspot);
                var renderer = serialized.FindProperty("targetRenderer").objectReferenceValue as Renderer;
                var target = hotspot.GetComponent<XRSimpleInteractable>();
                Color expected = serialized.FindProperty(target.isHovered ? "hoverColor" : visited[i] ? "visitedColor" : "availableColor").colorValue;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); Color actual = block.GetColor("_BaseColor");
                c.Check(hotspot.IsVisited == visited[i] && ((Vector4)(actual - expected)).sqrMagnitude < .000001f,
                    phase + "/" + hotspot.name, "actual=" + actual + "; expected=" + expected + "; hovered=" + target.isHovered + "; selected=" + target.isSelected +
                    "; hovering=" + string.Join(", ", target.interactorsHovering.Select(interactor => interactor.ToString())) +
                    "; selecting=" + string.Join(", ", target.interactorsSelecting.Select(interactor => interactor.ToString())));
            }
        }

        static void CheckMarkerColors(Context c, bool visited, string phase)
        {
            foreach (var hotspot in c.hotspots)
            {
                var serialized = new SerializedObject(hotspot);
                var renderer = serialized.FindProperty("targetRenderer").objectReferenceValue as Renderer;
                var target = hotspot.GetComponent<XRSimpleInteractable>();
                Color expected = serialized.FindProperty(target.isHovered ? "hoverColor" : visited ? "visitedColor" : "availableColor").colorValue;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); Color actual = block.GetColor("_BaseColor");
                c.Check(hotspot.IsVisited == visited && ((Vector4)(actual - expected)).sqrMagnitude < .000001f,
                    phase + "/" + hotspot.name, "actual=" + actual + "; expected=" + expected + "; hovered=" + target.isHovered + "; selected=" + target.isSelected +
                    "; hovering=" + string.Join(", ", target.interactorsHovering.Select(interactor => interactor.ToString())) +
                    "; selecting=" + string.Join(", ", target.interactorsSelecting.Select(interactor => interactor.ToString())));
            }
        }

        static TMP_Text Label(HelmetHotspotTour tour, string name) => tour.GetComponentsInChildren<TMP_Text>(true).SingleOrDefault(label => label.name == name);
        static string Source(HelmetHotspot hotspot, string field) => new SerializedObject(hotspot).FindProperty(field).stringValue;
        static string Detail(HelmetHotspot hotspot) => Source(hotspot, "title") + "\n" + Source(hotspot, "description");
        static string Missing(TMP_FontAsset font, string value) => !font ? "missing font" : new string(Regex.Replace(value, "<[^>]*>", "").Where(ch => !char.IsWhiteSpace(ch)).Distinct().Where(ch => !font.HasCharacter(ch, false, false)).ToArray());
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        static void CapturePanel(Context c, string name)
        {
            var labels = new[] { c.information, c.progress, c.completion }.Where(label => label.gameObject.activeInHierarchy).ToArray();
            foreach (var label in labels) label.ForceMeshUpdate(true, true);
            var bounds = labels[0].GetComponent<Renderer>().bounds;
            foreach (var label in labels.Skip(1)) bounds.Encapsulate(label.GetComponent<Renderer>().bounds);
            Vector3 position = bounds.center - c.information.transform.forward * Mathf.Max(.35f, (bounds.extents.magnitude + .045f) / Mathf.Tan(24f * Mathf.Deg2Rad));
            Quaternion rotation = Quaternion.LookRotation(bounds.center - position, c.information.transform.up);
            var temporary = new GameObject("Temporary hotspot QA close-up camera") { hideFlags = HideFlags.HideAndDontSave };
            var previous = RenderTexture.active; var target = RenderTexture.GetTemporary(1200, 900, 24, RenderTextureFormat.ARGB32); Texture2D pixels = null;
            try
            {
                var camera = temporary.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false;
                camera.stereoTargetEye = StereoTargetEyeMask.None; camera.targetTexture = null; camera.fieldOfView = 48f; camera.aspect = 4f / 3f;
                camera.transform.SetPositionAndRotation(position, rotation);
                var additional = temporary.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                additional.allowXRRendering = false; additional.renderPostProcessing = false;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels = new Texture2D(1200, 900, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); pixels.Apply();
                string file = "hotspots-" + name + "-diagnostic.png"; File.WriteAllBytes(Path.Combine(c.output, file), pixels.EncodeToPNG());
                c.report.captures.Add(new Capture { file = file, position = position, rotation = rotation });
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(temporary); }
        }
    }
}
#endif
