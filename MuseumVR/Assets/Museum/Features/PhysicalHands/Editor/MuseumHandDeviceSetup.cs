#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhysicalHands.EditorTools
{
    public static class MuseumHandDeviceSetup
    {
        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Report
        {
            public string status = "running", failure = "", unity, utc;
            public string limits = "Edit Mode derived mesh contacts and isolated native self-collision checks only; no Play Mode, physical-device input injection, visitor camera movement or headset acceptance.";
            public string selfCollisionReport;
            public List<Check> checks = new List<Check>();
        }
        public static void RunBatch()
        {
            var report = new Report { unity = Application.unityVersion, utc = DateTime.UtcNow.ToString("o") };
            string output = Environment.GetEnvironmentVariable("MUSEUM_QA_OUTPUT");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Use a fresh MUSEUM_QA_OUTPUT directory.");
            Directory.CreateDirectory(output);
            try
            {
                MuseumGripSurfaceBaker.BakeSavedHelmet();
                var root = PrefabUtility.LoadPrefabContents(MuseumGripSurfaceBaker.HelmetPrefabPath);
                try
                {
                    var grab = root.GetComponent<XRGrabInteractable>();
                    grab.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderVolume;
                    PrefabUtility.SaveAsPrefabAsset(root, MuseumGripSurfaceBaker.HelmetPrefabPath, out bool saved);
                    Require(report, saved, "saved root ranks by its touched collider surface");
                    var surface = root.GetComponent<MuseumGripSurface>();
                    Require(report, surface && surface.Cache && surface.Cache.entries.Length > 0, "new grip cache bound to the saved helmet");
                    ValidateMeshContacts(surface, report);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                MuseumHandSelfCollisionValidation.RunInEditor();
                report.selfCollisionReport = MuseumHandSelfCollisionValidation.LastReportPath;
                report.status = "passed";
            }
            catch (Exception exception) { report.status = "failed"; report.failure = exception.ToString(); Debug.LogException(exception); }
            finally
            {
                File.WriteAllText(Path.Combine(output, "surface-and-body-report.json"), JsonUtility.ToJson(report, true));
                Debug.Log("HAND_DEVICE_SETUP_" + report.status.ToUpperInvariant() + " " + output);
                int code = report.status == "passed" ? 0 : 1;
                double after = EditorApplication.timeSinceStartup + 20;
                EditorApplication.CallbackFunction finish = null;
                finish = () =>
                {
                    if (EditorApplication.timeSinceStartup < after || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                    EditorApplication.update -= finish; EditorApplication.Exit(code);
                };
                EditorApplication.update += finish;
            }
        }
        static void ValidateMeshContacts(MuseumGripSurface source, Report report)
        {
            foreach (var entry in source.Cache.entries)
            {
                int triangle = -1;
                for (int i = 0; i < entry.triangles.Length; i += 3)
                    if (Vector3.Cross(entry.vertices[entry.triangles[i + 1]] - entry.vertices[entry.triangles[i]],
                        entry.vertices[entry.triangles[i + 2]] - entry.vertices[entry.triangles[i]]).sqrMagnitude > 1e-12f) { triangle = i; break; }
                if (triangle < 0) continue;
                var test = new GameObject("Grip surface query fixture");
                try
                {
                    test.transform.position = new Vector3(12f, 10f, 12f);
                    test.transform.rotation = Quaternion.Euler(21, 37, -13);
                    test.transform.localScale = new Vector3(-.55f, .7f, .4f);
                    var filter = test.AddComponent<MeshFilter>(); filter.sharedMesh = entry.source;
                    test.AddComponent<MeshRenderer>();
                    var grab = test.AddComponent<XRGrabInteractable>();
                    var surface = test.AddComponent<MuseumGripSurface>(); surface.Configure(source.Cache, new[] { filter });
                    Vector3 a = test.transform.TransformPoint(entry.vertices[entry.triangles[triangle]]);
                    Vector3 b = test.transform.TransformPoint(entry.vertices[entry.triangles[triangle + 1]]);
                    Vector3 c = test.transform.TransformPoint(entry.vertices[entry.triangles[triangle + 2]]);
                    Vector3 triangleNormal = Vector3.Cross(b - a, c - a);
                    Vector3 query = (a + b + c) / 3f + triangleNormal / Mathf.Sqrt(triangleNormal.sqrMagnitude) * .04f;
                    bool resolved = surface.TryGetContact(grab, query, out var contact, out var normal);
                    Require(report, resolved &&
                        !surface.LastContactUsedColliderFallback && Vector3.Distance(query, contact) <= .0402f &&
                        Mathf.Abs(normal.magnitude - 1f) < .001f && Vector3.Dot(normal, query - contact) >= -.00001f,
                        "actual mesh contact under rotated nonuniform mirrored scale: " + entry.source.name,
                        "resolved=" + resolved + "; fallback=" + surface.LastContactUsedColliderFallback +
                        "; triangles=" + surface.LastTestedTriangleCount + "; distance=" + Vector3.Distance(query, contact) + "; normal=" + normal);
                    Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
                    if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(Vector3.up, normal);
                    Quaternion pose = Quaternion.LookRotation(forward.normalized, normal);
                    Vector3 handRoot = contact - pose * MuseumHandGrabTransformer.PalmContactLocal;
                    Require(report, Vector3.Distance(handRoot + pose * MuseumHandGrabTransformer.PalmContactLocal, contact) < .0001f,
                        "supporting palm contact matches recorded mesh surface: " + entry.source.name);
                }
                finally { UnityEngine.Object.DestroyImmediate(test); }
            }
        }
        static void Require(Report report, bool passed, string name, string detail = "")
        {
            report.checks.Add(new Check { name = name, passed = passed, detail = detail });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
    }
}
#endif
