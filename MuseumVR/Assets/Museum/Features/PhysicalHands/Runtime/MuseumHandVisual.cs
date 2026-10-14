using System;
using System.Collections.Generic;
using UnityEngine;

namespace Museum.PhysicalHands
{
    /// <summary>Controller-driven glove articulation. Tracking and collision belong to the hand driver.</summary>
    [DisallowMultipleComponent]
    public sealed class MuseumHandVisual : MonoBehaviour
    {
        public const int JointsPerFinger = 3;
        public const int ExpectedFingerCount = 5;

        [SerializeField] bool left;
        [SerializeField] Transform[] fingerJoints = Array.Empty<Transform>();
        [SerializeField] Quaternion[] restRotations = Array.Empty<Quaternion>();
        [SerializeField, Min(1f)] float poseResponse = 18f;

        IReadOnlyList<Transform> jointView;
        float targetGrip;
        float targetTrigger;
        float currentGrip;
        float currentTrigger;
        bool isHeld;

        public bool IsLeft => left;
        public float GripAmount => currentGrip;
        public int FingerCount => JointCount / JointsPerFinger;
        public int JointCount => fingerJoints != null ? fingerJoints.Length : 0;

        /// <summary>Thumb, index, middle, ring, little; three joints per finger from base to tip.</summary>
        public IReadOnlyList<Transform> FingerJoints => jointView ??
            (jointView = Array.AsReadOnly(fingerJoints ?? Array.Empty<Transform>()));

        public void Configure(bool isLeft, Transform[] joints, Quaternion[] neutralRotations)
        {
            int count = ExpectedFingerCount * JointsPerFinger;
            if (joints == null || neutralRotations == null || joints.Length != count || neutralRotations.Length != count)
                throw new ArgumentException("A glove requires five fingers with three joints and rest rotations each.");
            for (int i = 0; i < count; i++)
                if (joints[i] == null)
                    throw new ArgumentException("Every glove joint must reference a saved transform.", nameof(joints));

            left = isLeft;
            fingerJoints = (Transform[])joints.Clone();
            restRotations = (Quaternion[])neutralRotations.Clone();
            jointView = Array.AsReadOnly(fingerJoints);
            ApplyPoseImmediate(0f, 0f, false);
        }

        /// <summary>Grip curls the whole hand; trigger independently curls the index finger.</summary>
        public void SetPose(float grip, float trigger, bool held)
        {
            targetGrip = SafeUnitValue(grip);
            targetTrigger = SafeUnitValue(trigger);
            isHeld = held;
        }

        public void ApplyPoseImmediate(float grip, float trigger, bool held)
        {
            SetPose(grip, trigger, held);
            ApplyPoseImmediate();
        }

        /// <summary>Applies the last requested pose without waiting for a player frame.</summary>
        public void ApplyPoseImmediate()
        {
            currentGrip = EffectiveGrip;
            currentTrigger = targetTrigger;
            ApplyCurrentPose();
        }

        float EffectiveGrip => isHeld ? Mathf.Max(targetGrip, 0.72f) : targetGrip;

        void OnEnable()
        {
            ApplyCurrentPose();
        }

        void Update()
        {
            float blend = 1f - Mathf.Exp(-Mathf.Max(1f, poseResponse) * Time.deltaTime);
            currentGrip = Mathf.Lerp(currentGrip, EffectiveGrip, blend);
            currentTrigger = Mathf.Lerp(currentTrigger, targetTrigger, blend);
            ApplyCurrentPose();
        }

        void ApplyCurrentPose()
        {
            if (fingerJoints == null || restRotations == null ||
                fingerJoints.Length != ExpectedFingerCount * JointsPerFinger || restRotations.Length != fingerJoints.Length)
                return;

            float thumb = Mathf.Clamp01(currentGrip * 0.92f);
            float index = Mathf.Max(currentTrigger, currentGrip * 0.82f);
            PoseFinger(0, thumb, new Vector3(7f, 7f, 5f), new Vector3(43f, 52f, 45f));
            PoseFinger(1, index, new Vector3(4f, 8f, 6f), new Vector3(66f, 84f, 56f));
            PoseFinger(2, currentGrip * 0.96f, new Vector3(6f, 10f, 8f), new Vector3(70f, 89f, 58f));
            PoseFinger(3, currentGrip, new Vector3(9f, 12f, 10f), new Vector3(75f, 88f, 62f));
            PoseFinger(4, Mathf.Clamp01(currentGrip * 1.05f), new Vector3(12f, 14f, 12f), new Vector3(79f, 90f, 65f));

            // Oppose the thumb across the palm as the hand closes, mirrored for left/right gloves.
            float side = left ? 1f : -1f;
            SetJoint(0, new Vector3(Mathf.Lerp(7f, 43f, thumb), -side * 78f * thumb, side * 20f * thumb));
        }

        void PoseFinger(int finger, float curl, Vector3 openAngles, Vector3 closedAngles)
        {
            Vector3 angles = Vector3.Lerp(openAngles, closedAngles, Mathf.Clamp01(curl));
            int first = finger * JointsPerFinger;
            SetJoint(first, new Vector3(angles.x, 0f, 0f));
            SetJoint(first + 1, new Vector3(angles.y, 0f, 0f));
            SetJoint(first + 2, new Vector3(angles.z, 0f, 0f));
        }

        void SetJoint(int joint, Vector3 localAngles)
        {
            if (fingerJoints[joint] != null)
                fingerJoints[joint].localRotation = restRotations[joint] * Quaternion.Euler(localAngles);
        }

        static float SafeUnitValue(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        }
    }
}
