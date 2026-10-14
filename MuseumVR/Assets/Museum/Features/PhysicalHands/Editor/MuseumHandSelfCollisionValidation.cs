#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhysicalHands.EditorTools
{
    /// <summary>Isolated native character/self-hand collision checks without Play Mode or XR input devices.</summary>
    public static class MuseumHandSelfCollisionValidation
    {
        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Report
        {
            public string status = "running", utc, unity, failure = "", runtimeSha256, fixtureSha256;
            public string method = "Edit Mode isolated Physics3D scene; native CharacterController.Move; instance-only runInEditMode hand callbacks; manual tracking-validity override. No Play Mode, input devices, saved assets, global collision layers or default-scene simulation.";
            public string limits = "Native character movement and self-collision pair bookkeeping only. Hand sweeps use default Physics queries and remain covered by the separate Play Mode validator; lifecycle steps here use clear remote coordinates and do not establish those sweep results. Not headset locomotion/comfort or a build acceptance result.";
            public List<Check> checks = new List<Check>();
        }
        public static string LastReportPath { get; private set; }

        [MenuItem("Museum/Physical Hands/Validate self collision (isolated Edit Mode)")]
        public static void RunInEditor() => Validate();
        public static void RunBatch()
        {
            int result = 0;
            try { Validate(); }
            catch (Exception exception) { Debug.LogException(exception); result = 1; }
            finally { EditorApplication.Exit(result); }
        }

        static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run the self-collision fixture from Edit Mode.");
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var report = new Report { utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion,
                runtimeSha256 = Hash(Path.Combine(Application.dataPath, "Museum/Features/PhysicalHands/Runtime/MuseumPhysicalHand.cs")),
                fixtureSha256 = Hash(Path.Combine(Application.dataPath, "Museum/Features/PhysicalHands/Editor/MuseumHandSelfCollisionValidation.cs")) };
            string outputBase = Environment.GetEnvironmentVariable("MUSEUM_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputBase)) outputBase = Path.Combine(repo, "artifacts/helmet-interaction/qa/runs");
            string output = Path.Combine(Path.GetFullPath(outputBase), DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-hand-self-collision-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output); LastReportPath = Path.Combine(output, "report.json");
            Scene scene = default;
            GameObject root = null;
            MuseumPhysicalHand hand = null;
            try
            {
                // The runtime scene API is preflighted rather than falling back to
                // the loaded scene if this editor cannot create local physics here.
                scene = EditorSceneManager.NewPreviewScene();
                var physics = scene.GetPhysicsScene();
                Require(report, scene.IsValid() && scene.isLoaded && physics.IsValid() && !physics.Equals(Physics.defaultPhysicsScene),
                    "fixture owns a valid nondefault physics scene");
                root = new GameObject("Hand self-collision fixture only"); SceneManager.MoveGameObjectToScene(root, scene);
                Vector3 anchor = new Vector3(200f, 100f, 200f);
                var originA = Origin(root, "Fixture Origin A", anchor, out var ccA, out var controllerA);
                var originB = Origin(root, "Fixture Origin B", anchor + Vector3.right * 4f, out var ccB, out var controllerB);
                var handObject = Child(root, "Fixture world-space hand");
                hand = handObject.AddComponent<MuseumPhysicalHand>();
                hand.enabled = false; hand.runInEditMode = true;
                var palm = hand.PalmCollider;
                var envelope = hand.CollisionCollider;
                palm.enabled = true; envelope.enabled = true;

                foreach (var direction in new[] { Vector3.forward, Vector3.back })
                {
                    ResetCharacter(ccA, anchor);
                    PlaceHand(hand, anchor + Vector3.up * .9f + direction * .36f);
                    Physics.IgnoreCollision(palm, ccA, false); Physics.IgnoreCollision(envelope, ccA, false);
                    Sync(physics);
                    ccA.Move(direction * .6f);
                    float travel = Vector3.Dot(ccA.transform.position - anchor, direction);
                    Require(report, travel >= -.01f && travel < .5f,
                        "positive control: an unisolated hand obstructs character " + Direction(direction), "travel=" + travel);
                }

                ResetCharacter(ccA, anchor);
                controllerA.transform.localPosition = new Vector3(0, .9f, .36f);
                Physics.IgnoreCollision(palm, ccA, false); Physics.IgnoreCollision(envelope, ccA, false);
                hand.Configure(controllerA.transform, controllerA, null, null, true);
                hand.SetTrackingOverride(true); hand.enabled = true;
                hand.StepPhysics(.02f); Sync(physics);
                RequirePairs(report, palm, envelope, ccA, true, true, "both native self pairs are isolated");
                foreach (var direction in new[] { Vector3.forward, Vector3.back })
                {
                    ResetCharacter(ccA, anchor);
                    PlaceHand(hand, anchor + Vector3.up * .9f + direction * .36f);
                    hand.SynchronizeQueries(); Sync(physics);
                    ccA.Move(direction * .6f);
                    float travel = Vector3.Dot(ccA.transform.position - anchor, direction);
                    Require(report, Mathf.Abs(travel - .6f) < .02f,
                        "isolated hand does not obstruct character " + Direction(direction), "travel=" + travel);
                }

                // Native environment collision stays intact; do not change global layers.
                ResetCharacter(ccA, anchor); hand.SynchronizeQueries();
                PlaceHand(hand, anchor + Vector3.right * 2f + Vector3.up * .9f);
                var wall = Box(root, "Fixture wall", anchor + new Vector3(0, .85f, .45f), new Vector3(1f, 1.7f, .04f));
                Sync(physics);
                var wallFlags = ccA.Move(Vector3.forward * .8f);
                float wallTravel = ccA.transform.position.z - anchor.z;
                Require(report, wallTravel > .1f && wallTravel < .4f && (wallFlags & CollisionFlags.Sides) != 0 &&
                    !Physics.GetIgnoreCollision(palm, wall) && !Physics.GetIgnoreCollision(envelope, wall),
                    "wall still blocks the character and retains hand collision pairs", "travel=" + wallTravel + "; flags=" + wallFlags);
                UnityEngine.Object.DestroyImmediate(wall.gameObject);
                ResetCharacter(ccA, anchor); hand.SynchronizeQueries();
                var table = Box(root, "Fixture tabletop", anchor + Vector3.down * .07f, new Vector3(2f, .1f, 2f));
                Sync(physics);
                var tableFlags = ccA.Move(Vector3.down * .6f);
                float drop = anchor.y - ccA.transform.position.y;
                Require(report, drop >= -.02f && drop < .06f && (tableFlags & CollisionFlags.Below) != 0 &&
                    !Physics.GetIgnoreCollision(palm, table) && !Physics.GetIgnoreCollision(envelope, table),
                    "table still blocks the character and retains hand collision pairs", "drop=" + drop + "; flags=" + tableFlags);
                UnityEngine.Object.DestroyImmediate(table.gameObject);

                // Check distinct prior bits while colliders stay native-active.
                ResetCharacter(ccA, anchor);
                hand.Configure(controllerA.transform, null, null, null, true);
                RequirePairs(report, palm, envelope, ccA, false, false, "clearing the binding restores original false bits");
                Physics.IgnoreCollision(palm, ccA, true); Physics.IgnoreCollision(envelope, ccA, false);
                Physics.IgnoreCollision(palm, ccB, false); Physics.IgnoreCollision(envelope, ccB, true);
                hand.Configure(controllerA.transform, controllerA, null, null, true);
                hand.Configure(controllerB.transform, controllerB, null, null, true);
                RequirePairs(report, palm, envelope, ccA, true, false, "rebinding restores each distinct previous-owner bit");
                RequirePairs(report, palm, envelope, ccB, true, true, "rebinding isolates both new-owner pairs");
                hand.Configure(controllerA.transform, null, null, null, true);
                RequirePairs(report, palm, envelope, ccB, false, true, "clearing the new binding restores its distinct bits");

                Physics.IgnoreCollision(palm, ccA, false); Physics.IgnoreCollision(envelope, ccA, false);
                hand.Configure(controllerA.transform, controllerA, null, null, true);
                hand.SetTrackingOverride(false); hand.StepPhysics(.02f);
                Require(report, !palm.enabled && !envelope.enabled, "tracking loss disables both self colliders");
                hand.SetTrackingOverride(true); hand.StepPhysics(.02f); Sync(physics);
                RequirePairs(report, palm, envelope, ccA, true, true, "tracking recovery reestablishes both self pairs");
                palm.enabled = false; envelope.enabled = false;
                palm.enabled = true; envelope.enabled = true;
                // Simulate a native pair reset to prove the maintenance path repairs it.
                Physics.IgnoreCollision(palm, ccA, false); Physics.IgnoreCollision(envelope, ccA, false);
                hand.SynchronizeQueries();
                RequirePairs(report, palm, envelope, ccA, true, true, "reenabled collider/native pair reset is repaired during synchronization");

                hand.enabled = false; // runInEditMode delivers the real OnDisable callback.
                Require(report, !palm.enabled && !envelope.enabled, "module disable executes the hand collider lifecycle");
                palm.enabled = true; envelope.enabled = true; Sync(physics);
                RequirePairs(report, palm, envelope, ccA, false, false, "module disable restores the original owner pairs");
                hand.enabled = true; hand.StepPhysics(.02f); Sync(physics);
                RequirePairs(report, palm, envelope, ccA, true, true, "module enable reapplies the owner isolation");
                UnityEngine.Object.DestroyImmediate(hand); hand = null;
                palm.enabled = true; envelope.enabled = true; Sync(physics);
                RequirePairs(report, palm, envelope, ccA, false, false, "component destruction restores the original owner pairs");
                report.status = "passed";
            }
            catch (Exception exception) { report.status = "failed"; report.failure = exception.ToString(); throw; }
            finally
            {
                if (hand) { hand.enabled = false; hand.runInEditMode = false; }
                if (root) UnityEngine.Object.DestroyImmediate(root);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
                File.WriteAllText(LastReportPath, JsonUtility.ToJson(report, true));
                Debug.Log("Museum hand self-collision " + report.status + ": " + LastReportPath);
            }
        }

        static XROrigin Origin(GameObject root, string name, Vector3 position, out CharacterController character, out ActionBasedController controller)
        {
            var originObject = Child(root, name); originObject.transform.position = position;
            character = originObject.AddComponent<CharacterController>();
            character.height = 1.7f; character.radius = .1f; character.center = Vector3.up * .85f;
            character.skinWidth = .01f; character.stepOffset = 0; character.minMoveDistance = 0;
            var origin = originObject.AddComponent<XROrigin>(); origin.Origin = originObject;
            var controllerObject = Child(originObject, name + " input"); controllerObject.transform.localPosition = new Vector3(0, .9f, .36f);
            controller = controllerObject.AddComponent<ActionBasedController>();
            controller.enableInputTracking = false; controller.enableInputActions = false;
            return origin;
        }
        static GameObject Child(GameObject root, string name)
        {
            var instance = new GameObject(name); SceneManager.MoveGameObjectToScene(instance, root.scene);
            instance.transform.SetParent(root.transform, false); return instance;
        }
        static BoxCollider Box(GameObject root, string name, Vector3 position, Vector3 size)
        {
            var instance = Child(root, name); instance.transform.position = position;
            var box = instance.AddComponent<BoxCollider>(); box.size = size; return box;
        }
        static void ResetCharacter(CharacterController character, Vector3 position)
        { character.enabled = false; character.transform.position = position; character.enabled = true; }
        static void PlaceHand(MuseumPhysicalHand hand, Vector3 position)
        { hand.transform.SetPositionAndRotation(position, Quaternion.identity); hand.Body.position = position; hand.Body.rotation = Quaternion.identity; }
        static void Sync(PhysicsScene physics)
        {
            if (!physics.IsValid() || physics.Equals(Physics.defaultPhysicsScene))
                throw new InvalidOperationException("Refusing to simulate the loaded default scene.");
            Physics.SyncTransforms(); physics.Simulate(.02f);
        }
        static string Direction(Vector3 direction) => direction.z > 0 ? "forward" : "backward";
        static void RequirePairs(Report report, Collider palm, Collider envelope, CharacterController character, bool expectedPalm, bool expectedEnvelope, string name)
        {
            bool p = Physics.GetIgnoreCollision(palm, character), e = Physics.GetIgnoreCollision(envelope, character);
            Require(report, p == expectedPalm && e == expectedEnvelope, name, "palm=" + p + "; box=" + e);
        }
        static void Require(Report report, bool passed, string name, string detail = "")
        {
            report.checks.Add(new Check { passed = passed, name = name, detail = detail });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
        static string Hash(string path)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    }
}
#endif
