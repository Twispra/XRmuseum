using System;
using UnityEngine;

namespace Museum.Experience
{
    /// <summary>Editor-captured mesh positions for section geometry; original FBX/import settings stay unchanged.</summary>
    public sealed class HelmetSectionMeshData : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public Mesh source;
            public Vector3[] vertices;
            public int[] triangles;
        }
        public Entry[] entries = new Entry[0];
        public Entry Find(Mesh mesh) => Array.Find(entries, e => e.source == mesh);
    }
}
