#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Museum.Inspection.Editor
{
    /// <summary>
    /// Geometry/API regression for section-guide orientation continuity, using the saved prefab.
    /// Batch: -executeMethod Museum.Inspection.Editor.HelmetSectionContinuityValidation.RunBatch
    /// Do not add -quit. The runner writes its report and exits 0/1; it never enters Play Mode.
    /// </summary>
    public static class HelmetSectionContinuityValidation
    {
        const string Feature = "Assets/Museum/Features/HelmetInspection";
        const string PrefabPath = Feature + "/Prefabs/InspectableHelmet.prefab";
        const string RunnerPath = Feature + "/Editor/HelmetSectionContinuityValidation.cs";
        const float PositionTolerance = .0001f;
        const float RootTolerance = .000001f;
        static readonly MethodInfo UpdateGuide = typeof(HelmetSectionInteractable).GetMethod(
            "UpdateGuidePose", BindingFlags.Instance | BindingFlags.NonPublic);
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static Report activeReport;

        [Serializable] sealed class Fingerprint
        {
            public string path, sha256 = "", error = "";
        }
        [Serializable] sealed class Provenance
        {
            public string gitCommit = "unknown", gitBranch = "unknown", gitStatusPorcelain = "", workingTreeState = "unknown";
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<string> errors = new List<string>();
        }
        [Serializable] sealed class Step
        {
            public float fromDegrees, toDegrees;
            public double normalDeltaDegrees, rotationDeltaDegrees, maximumRotationDegrees;
            public float knobDisplacementMeters, maximumKnobDisplacementMeters;
        }
        [Serializable] sealed class CaseResult
        {
            public string name;
            public Vector3 rootPosition, rootEulerDegrees;
            public bool passed = true;
            public int samples, transitions, assertions, failures;
            public double maximumNormalDeltaDegrees, maximumRotationDeltaDegrees, maximumForwardErrorDegrees;
            public float maximumKnobDisplacementMeters, maximumCenterErrorMeters, maximumRootDisplacementMeters;
            public double maximumRootRotationDegrees;
            public Step worstRotationStep, worstKnobStep;
            public List<string> failureSamples = new List<string>();
        }
        [Serializable] sealed class Report
        {
            public string runId, timestampUtc, unityVersion, prefab = PrefabPath, status = "running";
            public string method = "Edit Mode only: load the saved prefab into a temporary PrefabContents scene; call SetSectionPlane(normal, 0), then invoke its actual private UpdateGuidePose by reflection. Never save prefab contents.";
            public string limitations = "Explicit geometry/API regression, not simulated XR selection, physical dragging, hardware, rendered pixels, usability, or a player build. Root scales remain the prefab's unit scale. A baseline failure is reported as failed, never converted into an expected pass.";
            public string thresholds = "Normal n(theta)=(cos(theta),sin(theta),0). Rotation <= max(0.08 degrees, 3*normal delta+0.02 degrees); knob displacement <= 3*(2*radius*sin(normal delta/2))+0.00005 m; center/plane error <=0.0001 m; forward error <=0.08 degrees; root position/scale drift <=0.000001; root rotation drift <=0.0001 degrees. These are numerical continuity checks, not comfort thresholds.";
            public Provenance provenance;
            public double durationSeconds;
            public int totalCases, failedCases, assertions, failures;
            public double maximumRotationDeltaDegrees;
            public float maximumKnobDisplacementMeters;
            public List<CaseResult> cases = new List<CaseResult>();
            public List<string> errors = new List<string>();
        }

        [MenuItem("Museum/Inspection/Validate section guide continuity (Edit Mode)")]
        public static void RunInEditor() { Run(); }

        public static void RunBatch()
        {
            int code = 1;
            try { code = Run(); }
            catch (Exception exception) { Debug.LogException(exception); }
            EditorApplication.Exit(code);
        }

        static int Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Section continuity validation requires Edit Mode.");
            string runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-section-continuity-" + Guid.NewGuid().ToString("N");
            string directory = Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", runId);
            Directory.CreateDirectory(directory);
            var report = new Report { runId = runId, timestampUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                provenance = CaptureProvenance() };
            double started = EditorApplication.timeSinceStartup;
            activeReport = report;
            Application.logMessageReceived += OnLog;
            try
            {
                if (UpdateGuide == null) throw new MissingMethodException(typeof(HelmetSectionInteractable).FullName, "UpdateGuidePose");
                var positions = new[] { Vector3.zero, new Vector3(.6f,1.1f,-.4f), new Vector3(-1.2f,.4f,2.1f), new Vector3(.3f,1.4f,3.2f) };
                var rotations = new[] { Vector3.zero, new Vector3(23,47,-31), new Vector3(-32,153,71), new Vector3(0,180,0) };
                for (int pose = 0; pose < rotations.Length; pose++)
                {
                    RunCase(report, "positive threshold upward", positions[pose], rotations[pose], new[] {78.51f,78.53f});
                    RunCase(report, "positive threshold downward", positions[pose], rotations[pose], new[] {78.53f,78.51f});
                    RunCase(report, "negative threshold downward", positions[pose], rotations[pose], new[] {-78.51f,-78.53f});
                    RunCase(report, "negative threshold upward", positions[pose], rotations[pose], new[] {-78.53f,-78.51f});
                    RunCase(report, "positive same side below", positions[pose], rotations[pose], new[] {78.48f,78.50f});
                    RunCase(report, "positive same side above", positions[pose], rotations[pose], new[] {78.54f,78.56f});
                    RunCase(report, "negative same side below", positions[pose], rotations[pose], new[] {-78.48f,-78.50f});
                    RunCase(report, "negative same side above", positions[pose], rotations[pose], new[] {-78.54f,-78.56f});
                    RunCase(report, "continuous sweep through both poles increasing", positions[pose], rotations[pose], Sweep(-110,110,.25f));
                    RunCase(report, "continuous sweep through both poles decreasing", positions[pose], rotations[pose], Sweep(110,-110,-.25f));
                }
                foreach (var fingerprint in report.provenance.files)
                {
                    if (fingerprint.sha256.Length == 0) continue;
                    if (HashFile(Path.Combine(RepoRoot, fingerprint.path)) != fingerprint.sha256)
                        report.errors.Add("Input changed during validation: " + fingerprint.path);
                }
            }
            catch (Exception exception) { report.errors.Add(exception.ToString()); }
            finally
            {
                Application.logMessageReceived -= OnLog;
                activeReport = null;
                report.durationSeconds = EditorApplication.timeSinceStartup - started;
                report.totalCases = report.cases.Count;
                foreach (var result in report.cases)
                {
                    report.assertions += result.assertions;
                    report.failures += result.failures;
                    if (!result.passed) report.failedCases++;
                    report.maximumRotationDeltaDegrees = Math.Max(report.maximumRotationDeltaDegrees, result.maximumRotationDeltaDegrees);
                    report.maximumKnobDisplacementMeters = Mathf.Max(report.maximumKnobDisplacementMeters, result.maximumKnobDisplacementMeters);
                }
                report.status = report.totalCases == 40 && report.failedCases == 0 && report.errors.Count == 0 && report.provenance.errors.Count == 0 ? "passed" : "failed";
                string path = Path.Combine(directory, "report.json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("Section continuity " + report.status + ": " + path + "; failed cases=" + report.failedCases + "/" + report.totalCases +
                    "; max rotation=" + report.maximumRotationDeltaDegrees + " deg; max knob movement=" + report.maximumKnobDisplacementMeters + " m.");
            }
            return report.status == "passed" ? 0 : 1;
        }

        static void RunCase(Report report, string name, Vector3 position, Vector3 euler, float[] angles)
        {
            var result = new CaseResult { name = name, rootPosition = position, rootEulerDegrees = euler };
            report.cases.Add(result);
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
                root.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
                var controller = root.GetComponent<HelmetInspectionController>();
                var guide = root.GetComponentInChildren<HelmetSectionInteractable>(true);
                if (!controller || !guide) throw new InvalidOperationException("Saved prefab is missing controller or section guide.");
                controller.SetSectionPlane(Vector3.right, 0f);
                guide.Configure(controller);
                var knob = new SerializedObject(guide).FindProperty("handle")?.objectReferenceValue as Transform;
                if (!knob) throw new InvalidOperationException("Section guide has no configured handle after Configure.");
                Assert(result, Vector3.Distance(knob.localPosition,new Vector3(.28f,0,0)) < PositionTolerance,
                    "Fixture handle must be the existing local x=0.28 m knob, not a replacement at the plane center.");
                Quaternion rootRotation = root.transform.rotation;
                Vector3 rootScale = root.transform.localScale;
                Assert(result, Vector3.Distance(rootScale, Vector3.one) < RootTolerance, "Fixture must retain unit scale.");
                Vector3 previousNormal = Vector3.zero, previousKnob = Vector3.zero;
                Quaternion previousRotation = Quaternion.identity;
                float previousRadius = 0f;
                for (int sample = 0; sample < angles.Length; sample++)
                {
                    float radians = angles[sample] * Mathf.Deg2Rad;
                    var requestedNormal = new Vector3(Mathf.Cos(radians),Mathf.Sin(radians),0);
                    controller.SetSectionPlane(requestedNormal,0f);
                    UpdateGuide.Invoke(guide,null);
                    Vector4 plane = controller.WorldSectionPlane;
                    Vector3 normal = new Vector3(plane.x,plane.y,plane.z).normalized;
                    Vector3 center = root.transform.TransformPoint(controller.SectionPointLocal);
                    float centerError = Vector3.Distance(guide.transform.position,center);
                    double forwardError = VectorAngle(guide.transform.forward,normal);
                    float rootError = Vector3.Distance(root.transform.position,position);
                    double rootRotationError = RotationAngle(root.transform.rotation,rootRotation);
                    string at = "theta=" + angles[sample].ToString("F2",System.Globalization.CultureInfo.InvariantCulture) + ": ";
                    result.samples++;
                    result.maximumCenterErrorMeters = Mathf.Max(result.maximumCenterErrorMeters,centerError);
                    result.maximumForwardErrorDegrees = Math.Max(result.maximumForwardErrorDegrees,forwardError);
                    result.maximumRootDisplacementMeters = Mathf.Max(result.maximumRootDisplacementMeters,rootError);
                    result.maximumRootRotationDegrees = Math.Max(result.maximumRootRotationDegrees,rootRotationError);
                    Assert(result, VectorAngle(controller.SectionNormalLocal,requestedNormal) <= .001 && Mathf.Abs(controller.SectionOffset) <= RootTolerance,
                        at + "SetSectionPlane did not apply the requested normal and zero offset");
                    Assert(result, centerError <= PositionTolerance, at + "guide center left the section center by " + centerError + " m");
                    Assert(result, forwardError <= .08, at + "guide forward differs from plane normal by " + forwardError + " degrees");
                    Assert(result, Mathf.Abs(Vector3.Dot(normal,guide.transform.position)+plane.w) <= PositionTolerance,
                        at + "guide center is not on the section plane");
                    Assert(result, rootError <= RootTolerance && rootRotationError <= .0001 && Vector3.Distance(root.transform.localScale,rootScale) <= RootTolerance,
                        at + "updating the section guide changed the helmet root pose/scale");
                    float radius = Vector3.Distance(knob.position,guide.transform.position);
                    if (sample > 0)
                    {
                        double normalDelta = VectorAngle(previousNormal,normal);
                        double rotationDelta = RotationAngle(previousRotation,guide.transform.rotation);
                        float knobDelta = Vector3.Distance(previousKnob,knob.position);
                        double rotationLimit = Math.Max(.08,normalDelta * 3 + .02);
                        float knobLimit = (float)(3 * 2 * Math.Max(previousRadius,radius) * Math.Sin(normalDelta * Math.PI / 360)) + .00005f;
                        var step = new Step { fromDegrees = angles[sample-1], toDegrees = angles[sample], normalDeltaDegrees = normalDelta,
                            rotationDeltaDegrees = rotationDelta, maximumRotationDegrees = rotationLimit,
                            knobDisplacementMeters = knobDelta, maximumKnobDisplacementMeters = knobLimit };
                        result.transitions++;
                        result.maximumNormalDeltaDegrees = Math.Max(result.maximumNormalDeltaDegrees,normalDelta);
                        if (result.worstRotationStep == null || rotationDelta > result.maximumRotationDeltaDegrees) result.worstRotationStep = step;
                        if (result.worstKnobStep == null || knobDelta > result.maximumKnobDisplacementMeters) result.worstKnobStep = step;
                        result.maximumRotationDeltaDegrees = Math.Max(result.maximumRotationDeltaDegrees,rotationDelta);
                        result.maximumKnobDisplacementMeters = Mathf.Max(result.maximumKnobDisplacementMeters,knobDelta);
                        Assert(result, rotationDelta <= rotationLimit, at + "guide rotation delta " + rotationDelta + " exceeds " + rotationLimit + " degrees for normal delta " + normalDelta);
                        Assert(result, knobDelta <= knobLimit, at + "knob movement " + knobDelta + " exceeds " + knobLimit + " m for normal delta " + normalDelta);
                    }
                    previousNormal = normal; previousRotation = guide.transform.rotation; previousKnob = knob.position; previousRadius = radius;
                }
            }
            catch (Exception exception) { Assert(result,false,"Fixture or invocation error: " + exception); }
            finally { if (root) PrefabUtility.UnloadPrefabContents(root); }
        }

        static float[] Sweep(float start, float end, float step)
        {
            int count = Mathf.RoundToInt((end-start)/step)+1;
            var angles = new float[count];
            for (int i=0;i<count;i++) angles[i]=start+i*step;
            return angles;
        }

        static void Assert(CaseResult result, bool condition, string detail)
        {
            result.assertions++;
            if (condition) return;
            result.passed=false; result.failures++;
            if (result.failureSamples.Count<24) result.failureSamples.Add(detail);
        }

        // Double-precision angle calculation avoids Quaternion.Angle's float-dot quantization
        // swallowing a 0.02-degree input step. NaN results fail the <= checks above.
        static double VectorAngle(Vector3 a, Vector3 b)
        {
            double x=(double)a.y*b.z-(double)a.z*b.y, y=(double)a.z*b.x-(double)a.x*b.z, z=(double)a.x*b.y-(double)a.y*b.x;
            double dot=(double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z;
            return Math.Atan2(Math.Sqrt(x*x+y*y+z*z),dot)*180/Math.PI;
        }
        static double RotationAngle(Quaternion a, Quaternion b)
        {
            double dot=(double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z+(double)a.w*b.w;
            double a2=(double)a.x*a.x+(double)a.y*a.y+(double)a.z*a.z+(double)a.w*a.w;
            double b2=(double)b.x*b.x+(double)b.y*b.y+(double)b.z*b.z+(double)b.w*b.w;
            return 2*Math.Acos(Math.Min(1,Math.Abs(dot)/Math.Sqrt(a2*b2)))*180/Math.PI;
        }

        static Provenance CaptureProvenance()
        {
            var provenance = new Provenance();
            provenance.gitCommit = ReadGit("rev-parse --verify HEAD",provenance).Trim();
            provenance.gitBranch = ReadGit("branch --show-current",provenance).Trim();
            provenance.gitStatusPorcelain = ReadGit("-c core.quotepath=false status --porcelain=v1 --untracked-files=all",provenance);
            provenance.workingTreeState = provenance.errors.Count > 0 ? "unknown" : (string.IsNullOrEmpty(provenance.gitStatusPorcelain) ? "clean" : "dirty");
            var paths = new[] { "MuseumVR/"+RunnerPath, "MuseumVR/"+PrefabPath,
                "MuseumVR/"+Feature+"/Runtime/HelmetSectionInteractable.cs", "MuseumVR/"+Feature+"/Runtime/HelmetInspectionController.cs",
                "MuseumVR/Assets/Museum/Models/Helmet/helmet-graybox-v02.fbx", "MuseumVR/ProjectSettings/ProjectVersion.txt", "MuseumVR/Packages/manifest.json" };
            foreach (var path in paths)
            {
                var fingerprint = new Fingerprint { path=path };
                try { fingerprint.sha256=HashFile(Path.Combine(RepoRoot,path)); }
                catch (Exception exception) { fingerprint.error=exception.Message; provenance.errors.Add("Hash failed: "+path+": "+exception.Message); }
                provenance.files.Add(fingerprint);
            }
            return provenance;
        }
        static string HashFile(string path)
        {
            using (var stream=File.OpenRead(path))
            using (var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        static string ReadGit(string arguments, Provenance provenance)
        {
            try
            {
                using (var process=new System.Diagnostics.Process())
                {
                    process.StartInfo=new System.Diagnostics.ProcessStartInfo { FileName="git",Arguments=arguments,WorkingDirectory=RepoRoot,
                        UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,
                        RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
                    process.StartInfo.EnvironmentVariables["GIT_OPTIONAL_LOCKS"]="0";
                    if (!process.Start()) throw new InvalidOperationException("Git process did not start.");
                    var output=process.StandardOutput.ReadToEndAsync(); var errors=process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(10000)) { try { process.Kill(); } catch {} throw new TimeoutException("Git query exceeded 10 seconds."); }
                    if (!output.Wait(1000) || !errors.Wait(1000)) throw new TimeoutException("Git output was not completely read.");
                    if (process.ExitCode!=0) throw new InvalidOperationException("Exit "+process.ExitCode+": "+errors.Result);
                    return output.Result;
                }
            }
            catch (Exception exception) { provenance.errors.Add("git "+arguments+": "+exception.Message); return ""; }
        }
        static void OnLog(string message, string stack, LogType type)
        {
            if (activeReport!=null && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert))
                activeReport.errors.Add(type+": "+message+"\n"+stack);
        }
    }
}
#endif
