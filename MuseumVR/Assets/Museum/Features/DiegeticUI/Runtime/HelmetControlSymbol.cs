using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Museum.Diegetic
{
    /// <summary>Small world-space vector symbols, sharing the control's fade and generous hit area.</summary>
    public static class HelmetControlSymbol
    {
        public enum Kind { Rotate, Section, Slide }
        public static LineRenderer[] Build(Transform parent,Material material,Kind kind)
        {
            var lines=new List<LineRenderer>();
            if(kind==Kind.Rotate)
            {
                Arc("Globe equator",.023f,.010f,0,360,true);
                Arc("Globe meridian",.010f,.023f,0,360,true);
                Arc("Globe edge",.023f,.023f,0,360,true);
                Arc("Turn arrow upper",.043f,.039f,22,151,false);
                Arc("Turn arrow lower",.043f,.039f,202,331,false);
                ArrowHead(151,.043f,.039f);ArrowHead(331,.043f,.039f);
            }
            else
            {
                // Two offset solid halves with a visible gap, crossed by a separate cutting plane.
                var a=new Vector3(-.035f,-.021f,-.010f);var b=new Vector3(-.006f,-.021f,-.010f);
                var c=new Vector3(-.006f,.020f,-.010f);var d=new Vector3(-.035f,.020f,-.010f);
                Stroke("Left cut solid",new[]{a,b,c,d,a},false);
                Vector3 lift=new Vector3(.013f,.012f,0);
                Stroke("Left depth",new[]{d,d+lift,c+lift,c,b},false);
                Stroke("Left top",new[]{c+lift,c},false);
                var shift=new Vector3(.046f,0,0);
                Stroke("Right cut solid",new[]{a+shift,b+shift,c+shift,d+shift,a+shift},false);
                Stroke("Right depth",new[]{d+shift,d+shift+lift,c+shift+lift,c+shift,b+shift},false);
                Stroke("Right top",new[]{c+shift+lift,c+shift},false);
                Stroke("Section plane",new[]{new Vector3(.003f,-.040f,-.018f),new Vector3(.003f,.039f,-.018f),new Vector3(.023f,.050f,-.018f),new Vector3(.023f,-.029f,-.018f),new Vector3(.003f,-.040f,-.018f)},false);
                if(kind==Kind.Slide)
                {
                    Stroke("Slide axis",new[]{new Vector3(-.044f,-.043f,-.020f),new Vector3(.046f,-.043f,-.020f)},false);
                    Stroke("Slide left arrow",new[]{new Vector3(-.036f,-.035f,-.020f),new Vector3(-.044f,-.043f,-.020f),new Vector3(-.036f,-.051f,-.020f)},false);
                    Stroke("Slide right arrow",new[]{new Vector3(.038f,-.035f,-.020f),new Vector3(.046f,-.043f,-.020f),new Vector3(.038f,-.051f,-.020f)},false);
                }
            }
            return lines.ToArray();

            void ArrowHead(float angle,float rx,float ry)
            {
                float t=angle*Mathf.Deg2Rad;
                var end=new Vector3(Mathf.Cos(t)*rx,Mathf.Sin(t)*ry,-.016f);
                var tangent=new Vector3(-Mathf.Sin(t),Mathf.Cos(t),0);
                var radial=new Vector3(Mathf.Cos(t),Mathf.Sin(t),0);
                Stroke("Turn arrow head",new[]{end-tangent*.010f+radial*.006f,end,end-tangent*.010f-radial*.006f},false);
            }
            void Arc(string name,float rx,float ry,float from,float to,bool loop)
            {
                int count=loop?48:26;var points=new Vector3[count];
                for(int i=0;i<count;i++){float a=Mathf.Lerp(from,to,i/(float)(loop?count:count-1))*Mathf.Deg2Rad;points[i]=new Vector3(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry,-.016f);}
                Stroke(name,points,loop);
            }
            void Stroke(string name,Vector3[] points,bool loop)
            {
                var line=new GameObject("Symbol - "+name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);
                line.sharedMaterial=material;line.useWorldSpace=false;line.loop=loop;line.positionCount=points.Length;line.SetPositions(points);
                line.widthMultiplier=.0022f;line.numCornerVertices=3;line.numCapVertices=3;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;lines.Add(line);
            }
        }
    }
}
