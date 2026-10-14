using System;
using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhysicalHands
{
    /// <summary>Local Development-player observations only; never writes XR input or moves the visitor.</summary>
    [DisallowMultipleComponent]
    public sealed class MuseumHandDeviceDiagnostics : MonoBehaviour
    {
        MuseumPhysicalHandsRig rig;
        XROrigin origin;
        CharacterController character;
        ActionBasedContinuousMoveProvider move;
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        StreamWriter writer;
        string version;
        float nextSample, previousSampleTime, frameTotal, maximumFrame;
        int frames;
        Vector3 previousOrigin;
        bool failed;

        [Serializable] sealed class Header
        {
            public string kind = "device-observation", utc, version, buildGuid, unity, scene;
            public string limits = "Local observations of the running player, not a test-pass result. No input injection, device identifiers, account data or network addresses. Virtual scene positions are recorded to diagnose locomotion.";
        }
        [Serializable] sealed class HandSample
        {
            public bool tracked, grip, blocked, palmIgnoresBody, envelopeIgnoresBody;
            public float trackingError;
            public Vector3 target, proxy, visiblePalm;
            public string selected = "";
        }
        [Serializable] sealed class Sample
        {
            public string kind = "sample";
            public float time, interval, meanFrameSeconds, maximumFrameSeconds;
            public bool focused, displayRunning, moveEnabled, grounded;
            public Vector2 leftMove, rightMove;
            public Vector3 origin, originDelta, headRelative, characterVelocity;
            public int collisionFlags;
            public HandSample left, right;
        }

        public void Configure(MuseumPhysicalHandsRig source, string label)
        {
            rig = source; version = label;
            origin = rig && rig.LeftController ? rig.LeftController.GetComponentInParent<XROrigin>() : null;
            character = origin && origin.Origin ? origin.Origin.GetComponent<CharacterController>() : null;
            move = origin ? origin.GetComponentInChildren<ActionBasedContinuousMoveProvider>(true) : null;
            SubsystemManager.GetSubsystems(displays);
            Open();
        }
        void OnEnable() { if (rig) Open(); }
        void Open()
        {
            if (!Debug.isDebugBuild || failed || writer != null || !rig || !origin) return;
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "PhysicalHandsDiagnostics");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".jsonl");
                writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
                writer.WriteLine(JsonUtility.ToJson(new Header { utc = DateTime.UtcNow.ToString("o"), version = version,
                    buildGuid = Application.buildGUID, unity = Application.unityVersion, scene = gameObject.scene.path }));
                previousOrigin = origin.transform.position; previousSampleTime = Time.unscaledTime;
                nextSample = Time.unscaledTime; frameTotal = maximumFrame = 0f; frames = 0;
                Debug.Log("MUSEUM_HAND_DEVICE_DIAGNOSTICS " + version + " " + path);
            }
            catch (Exception exception) { Fail(exception); }
        }
        void LateUpdate()
        {
            if (writer == null || !rig || !origin) return;
            float dt = Time.unscaledDeltaTime;
            frameTotal += dt; maximumFrame = Mathf.Max(maximumFrame, dt); frames++;
            if (Time.unscaledTime < nextSample) return;
            try
            {
                Vector3 position = origin.transform.position;
                var sample = new Sample { time = Time.unscaledTime, interval = Time.unscaledTime - previousSampleTime,
                    focused = Application.isFocused, displayRunning = displays.Exists(display => display.running),
                    moveEnabled = move && move.isActiveAndEnabled,
                    leftMove = move && move.leftHandMoveAction.action != null ? move.leftHandMoveAction.action.ReadValue<Vector2>() : Vector2.zero,
                    rightMove = move && move.rightHandMoveAction.action != null ? move.rightHandMoveAction.action.ReadValue<Vector2>() : Vector2.zero,
                    origin = position, originDelta = position - previousOrigin,
                    headRelative = origin.Camera ? origin.transform.InverseTransformPoint(origin.Camera.transform.position) : Vector3.zero,
                    grounded = character && character.isGrounded, collisionFlags = character ? (int)character.collisionFlags : 0,
                    characterVelocity = character ? character.velocity : Vector3.zero,
                    meanFrameSeconds = frames > 0 ? frameTotal / frames : 0f, maximumFrameSeconds = maximumFrame,
                    left = ReadHand(rig.LeftHand, rig.LeftController), right = ReadHand(rig.RightHand, rig.RightController) };
                writer.WriteLine(JsonUtility.ToJson(sample));
                previousOrigin = position; previousSampleTime = Time.unscaledTime; nextSample = Time.unscaledTime + .15f;
                frameTotal = maximumFrame = 0f; frames = 0;
            }
            catch (Exception exception) { Fail(exception); }
        }
        HandSample ReadHand(MuseumPhysicalHand hand, ActionBasedController controller)
        {
            var result = new HandSample();
            var state = controller ? controller.currentControllerState : null;
            result.grip = state != null && state.selectInteractionState.active;
            if (!hand) return result;
            result.tracked = hand.IsTrackingValid; result.blocked = hand.IsBlocked; result.trackingError = hand.TrackingError;
            result.proxy = hand.transform.position; result.target = hand.TrackingTarget ? hand.TrackingTarget.position : hand.transform.position;
            var visual = hand.GetComponentInChildren<MuseumHandVisual>(true);
            result.visiblePalm = visual ? visual.transform.TransformPoint(MuseumHandGrabTransformer.PalmContactLocal) : result.proxy;
            result.palmIgnoresBody = character && hand.PalmCollider && Physics.GetIgnoreCollision(hand.PalmCollider, character);
            result.envelopeIgnoresBody = character && hand.CollisionCollider && Physics.GetIgnoreCollision(hand.CollisionCollider, character);
            if (hand.DirectInteractor && hand.DirectInteractor.hasSelection)
                result.selected = hand.DirectInteractor.interactablesSelected[0].transform.name;
            return result;
        }
        void Fail(Exception exception)
        {
            failed = true; Close();
            Debug.LogWarning("Museum local hand diagnostics stopped: " + exception.GetType().Name + ". " + exception.Message, this);
        }
        void Close() { writer?.Dispose(); writer = null; }
        void OnDisable() => Close();
        void OnDestroy() => Close();
        void OnApplicationQuit() => Close();
    }
}
