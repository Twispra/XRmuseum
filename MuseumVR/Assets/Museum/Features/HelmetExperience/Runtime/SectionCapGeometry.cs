using System.Collections.Generic;
using UnityEngine;

namespace Museum.Experience
{
    /// <summary>Even-odd scan conversion of a mesh/plane intersection. Nested contours keep cavities open.</summary>
    public static class SectionCapGeometry
    {
        struct Segment { public Vector2 a, b; public float At(float x) => Mathf.LerpUnclamped(a.y,b.y,(x-a.x)/(b.x-a.x)); }
        const float Epsilon = .000002f;
        public static int Build(Vector3[] vertices, int[] triangles, Matrix4x4 toRoot,
            Vector3 normal, Vector3 point, List<Vector3> result, List<int> indices)
        {
            result.Clear(); indices.Clear();
            normal.Normalize();
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right).normalized;
            Vector3 v = Vector3.Cross(normal,u);
            // Offset from vertex-aligned planes avoids ambiguous tangencies at mesh seams.
            Vector3 origin = point - normal * Epsilon;
            var segments = new List<Segment>();
            var knots = new List<float>();
            var hits = new Vector3[3];
            var duplicate = new HashSet<string>();
            for (int i=0;i+2<triangles.Length;i+=3)
            {
                Vector3 a=toRoot.MultiplyPoint3x4(vertices[triangles[i]]);
                Vector3 b=toRoot.MultiplyPoint3x4(vertices[triangles[i+1]]);
                Vector3 c=toRoot.MultiplyPoint3x4(vertices[triangles[i+2]]);
                int count=0;
                Intersect(a,b); Intersect(b,c); Intersect(c,a);
                if(count!=2 || (hits[0]-hits[1]).sqrMagnitude<Epsilon*Epsilon) continue;
                Vector2 p=Project(hits[0]),q=Project(hits[1]);
                if(p.x>q.x) { var swap=p;p=q;q=swap; }
                if(q.x-p.x<Epsilon) continue;
                string key=Quantize(p)+":"+Quantize(q);
                if(!duplicate.Add(key)) continue;
                segments.Add(new Segment{a=p,b=q}); knots.Add(p.x);knots.Add(q.x);
                void Intersect(Vector3 from,Vector3 to)
                {
                    float d0=Vector3.Dot(normal,from-origin),d1=Vector3.Dot(normal,to-origin);
                    if((d0>0)==(d1>0) || Mathf.Abs(d0-d1)<1e-12f) return;
                    if(count<3) hits[count++]=Vector3.LerpUnclamped(from,to,d0/(d0-d1));
                }
            }
            knots.Sort();
            var unique=new List<float>();
            foreach(float x in knots) if(unique.Count==0 || x-unique[unique.Count-1]>Epsilon) unique.Add(x);
            int rejected=0;
            var crossing=new List<Segment>();
            for(int i=0;i+1<unique.Count;i++)
            {
                float left=unique[i],right=unique[i+1],mid=(left+right)*.5f;
                crossing.Clear();
                foreach(var s in segments) if(s.a.x<mid && s.b.x>mid) crossing.Add(s);
                crossing.Sort((a,b)=>a.At(mid).CompareTo(b.At(mid)));
                // Open/nonmanifold contours are not filled by guessing a boundary.
                if(crossing.Count%2!=0) { rejected++;continue; }
                for(int j=0;j+1<crossing.Count;j+=2)
                {
                    var low=crossing[j];var high=crossing[j+1];
                    if(high.At(mid)-low.At(mid)<Epsilon) continue;
                    int k=result.Count;
                    result.Add(Unproject(left,low.At(left)));result.Add(Unproject(right,low.At(right)));
                    result.Add(Unproject(right,high.At(right)));result.Add(Unproject(left,high.At(left)));
                    indices.Add(k);indices.Add(k+1);indices.Add(k+2);indices.Add(k);indices.Add(k+2);indices.Add(k+3);
                }
            }
            return rejected;
            Vector2 Project(Vector3 x) => new Vector2(Vector3.Dot(x-origin,u),Vector3.Dot(x-origin,v));
            Vector3 Unproject(float x,float y) => origin+u*x+v*y;
        }
        static string Quantize(Vector2 p) => Mathf.RoundToInt(p.x/Epsilon)+","+Mathf.RoundToInt(p.y/Epsilon);
    }
}
