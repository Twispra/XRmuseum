using UnityEngine;
using UnityEngine.Rendering;

namespace Museum.PhysicalHands.Editor
{
    /// <summary>Builds original, metre-scale museum gloves from Unity's built-in primitive meshes.</summary>
    public static class MuseumHandVisualFactory
    {
        /// <summary>Local origin is the palm centre; fingers point +Z and the back of the hand faces +Y.</summary>
        public static MuseumHandVisual Create(Transform parent, bool left, Material gloveMaterial, Material cuffMaterial)
        {
            var root = new GameObject(left ? "Left Museum Glove" : "Right Museum Glove");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // These are leaf meshes: no joint ever inherits a stretched primitive's local scale.
            AddMesh("Palm", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, -0.003f), Quaternion.identity, new Vector3(0.079f, 0.033f, 0.086f), gloveMaterial);
            AddMesh("Navy Back Panel", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0.014f, -0.008f), Quaternion.identity, new Vector3(0.055f, 0.008f, 0.056f), cuffMaterial);
            AddMesh("Wrist Cuff", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, -0.001f, -0.051f), Quaternion.identity, new Vector3(0.067f, 0.040f, 0.030f), cuffMaterial);

            var joints = new Transform[MuseumHandVisual.ExpectedFingerCount * MuseumHandVisual.JointsPerFinger];
            var rest = new Quaternion[joints.Length];
            float side = left ? 1f : -1f;

            BuildFinger("Thumb", 0, root.transform, new Vector3(side * 0.031f, -0.001f, -0.018f),
                Quaternion.Euler(0f, side * 52f, -side * 25f), new Vector3(0.025f, 0.023f, 0.018f),
                0.019f, gloveMaterial, cuffMaterial, joints, rest);
            BuildFinger("Index", 1, root.transform, new Vector3(side * 0.025f, 0f, 0.032f),
                Quaternion.Euler(0f, side * 3f, 0f), new Vector3(0.034f, 0.022f, 0.018f),
                0.016f, gloveMaterial, cuffMaterial, joints, rest);
            BuildFinger("Middle", 2, root.transform, new Vector3(side * 0.008f, 0f, 0.040f),
                Quaternion.identity, new Vector3(0.036f, 0.024f, 0.019f),
                0.017f, gloveMaterial, cuffMaterial, joints, rest);
            BuildFinger("Ring", 3, root.transform, new Vector3(-side * 0.010f, 0f, 0.035f),
                Quaternion.Euler(0f, -side * 2f, 0f), new Vector3(0.033f, 0.022f, 0.017f),
                0.016f, gloveMaterial, cuffMaterial, joints, rest);
            BuildFinger("Little", 4, root.transform, new Vector3(-side * 0.026f, -0.001f, 0.023f),
                Quaternion.Euler(0f, -side * 6f, 0f), new Vector3(0.026f, 0.017f, 0.014f),
                0.014f, gloveMaterial, cuffMaterial, joints, rest);

            var visual = root.AddComponent<MuseumHandVisual>();
            visual.Configure(left, joints, rest);
            return visual;
        }

        static void BuildFinger(string name, int finger, Transform palm, Vector3 basePosition, Quaternion baseRotation,
            Vector3 lengths, float diameter, Material glove, Material cuff, Transform[] joints, Quaternion[] rest)
        {
            Transform jointParent = palm;
            for (int segment = 0; segment < MuseumHandVisual.JointsPerFinger; segment++)
            {
                int index = finger * MuseumHandVisual.JointsPerFinger + segment;
                var joint = new GameObject(name + " Joint " + (segment + 1)).transform;
                joint.SetParent(jointParent, false);
                joint.localPosition = segment == 0 ? basePosition : new Vector3(0f, 0f, lengths[segment - 1]);
                joint.localRotation = segment == 0 ? baseRotation : Quaternion.identity;
                joint.localScale = Vector3.one;
                joints[index] = joint;
                rest[index] = joint.localRotation;

                float width = diameter * (1f - segment * 0.10f);
                float length = lengths[segment];
                AddMesh(name + " Segment " + (segment + 1), PrimitiveType.Capsule, joint,
                    new Vector3(0f, 0f, length * 0.5f), Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(width, length * 0.5f, width), glove);
                AddMesh(name + " Knuckle " + (segment + 1), PrimitiveType.Sphere, joint,
                    Vector3.zero, Quaternion.identity, Vector3.one * width, glove);

                if (segment == 0)
                    AddMesh(name + " Back Seam", PrimitiveType.Sphere, joint,
                        new Vector3(0f, width * 0.45f, length * 0.25f), Quaternion.identity,
                        new Vector3(width * 0.75f, width * 0.12f, length * 0.25f), cuff);
                jointParent = joint;
            }
        }

        static void AddMesh(string name, PrimitiveType primitive, Transform parent, Vector3 position,
            Quaternion rotation, Vector3 scale, Material material)
        {
            var mesh = GameObject.CreatePrimitive(primitive);
            mesh.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = position;
            mesh.transform.localRotation = rotation;
            mesh.transform.localScale = scale;
            // Factory output is purely visual; the separate physical hand owns all colliders.
            var collider = mesh.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }
}
