using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Museum.Exploration
{
    /// <summary>Cached illustrative equipment; dimensions and mechanisms are teaching approximations.</summary>
    internal sealed class HelmetDemonstrationModels
    {
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly Material bronze, steel, enamel, wood, rubber, pale;
        readonly Material airSignal, voiceSignal;
        readonly Mesh ringMesh, arrowMesh;
        readonly Transform[] airPackets = new Transform[5];
        readonly Transform[] surfaceWaves = new Transform[3], diverWaves = new Transform[3];
        Transform pistonA, pistonB, wheelA, wheelB, crankA, crankB, rodA, rodB;
        Transform surfaceDiaphragm, diverDiaphragm;
        Vector3 pistonAHome, pistonBHome, surfaceDiaphragmHome, diverDiaphragmHome;
        Renderer[] pumpSurfaces,telephoneSurfaces,diverSurfaces;
        readonly MaterialPropertyBlock attentionBlock=new MaterialPropertyBlock();
        static readonly int FocusId=Shader.PropertyToID("_Focus");

        public Transform AirRoot { get; private set; }
        public Transform VoiceRoot { get; private set; }
        public Transform Pump { get; private set; }
        public Transform SurfaceTelephone { get; private set; }
        public Transform DiverTransducer { get; private set; }
        public Transform AirOutlet { get; private set; }
        public Transform TelephoneOutlet { get; private set; }
        public Transform TransducerInlet { get; private set; }
        public Transform[] AirPackets => airPackets;

        public HelmetDemonstrationModels(Transform parent, Transform signalParent, Material modelTemplate,
            Material airMaterial, Material voiceMaterial)
        {
            airSignal = airMaterial; voiceSignal = voiceMaterial;
            bronze = Tint(modelTemplate, "Diorama bronze", new Color(.76f,.48f,.21f));
            steel = Tint(modelTemplate, "Diorama steel", new Color(.58f,.70f,.74f));
            enamel = Tint(modelTemplate, "Diorama enamel", new Color(.06f,.28f,.31f));
            wood = Tint(modelTemplate, "Diorama telephone case", new Color(.34f,.16f,.065f));
            rubber = Tint(modelTemplate, "Diorama dark fittings", new Color(.045f,.065f,.075f));
            pale = Tint(modelTemplate, "Diorama dial", new Color(.89f,.87f,.71f));
            ringMesh = MakeRingMesh(); arrowMesh = MakeArrowMesh();
            AirRoot = Node(parent, "Air equipment diorama");
            VoiceRoot = Node(parent, "Voice equipment diorama");
            BuildPump(); BuildTelephone(); BuildTransducer();
            pumpSurfaces=Pump.GetComponentsInChildren<Renderer>(true);
            telephoneSurfaces=SurfaceTelephone.GetComponentsInChildren<Renderer>(true);
            diverSurfaces=DiverTransducer.GetComponentsInChildren<Renderer>(true);
            for (int i=0;i<airPackets.Length;i++)
            {
                airPackets[i] = MeshPart(signalParent, "Air packet " + i, arrowMesh, airSignal);
                airPackets[i].localScale = new Vector3(.020f,.014f,.045f);
            }
        }

        void BuildPump()
        {
            Pump = Node(AirRoot, "Manual air pump");
            Box(Pump,"Pump base",new Vector3(0,.018f,0),new Vector3(.31f,.036f,.19f),wood);
            Box(Pump,"Pump cross rail",new Vector3(0,.071f,.025f),new Vector3(.24f,.028f,.075f),steel);
            for(int i=0;i<2;i++)
            {
                float x=i==0?-.063f:.063f;
                Cylinder(Pump,"Pump cylinder "+i,new Vector3(x,.127f,.022f),.032f,.145f,bronze,Quaternion.identity);
                Cylinder(Pump,"Cylinder lower band "+i,new Vector3(x,.062f,.022f),.035f,.012f,steel,Quaternion.identity);
                Cylinder(Pump,"Cylinder upper cap "+i,new Vector3(x,.198f,.022f),.036f,.013f,steel,Quaternion.identity);
                var piston=Node(Pump,i==0?"Pump piston A":"Pump piston B");
                piston.localPosition=new Vector3(x,.196f,.022f);
                Cylinder(piston,"Piston rod",new Vector3(0,.027f,0),.007f,.090f,steel,Quaternion.identity);
                Box(piston,"Piston crosshead",new Vector3(0,.069f,0),new Vector3(.048f,.013f,.027f),bronze);
                var wheel=Node(Pump,i==0?"Pump flywheel":"Pump second flywheel");
                wheel.localPosition=new Vector3(i==0?-.091f:.091f,.124f,-.069f);
                var rim=MeshPart(wheel,"Iron handwheel rim",ringMesh,rubber); rim.localScale=Vector3.one*.152f;
                for(int spoke=0;spoke<6;spoke++)
                {
                    float angle=spoke*60f;
                    var part=Box(wheel,"Handwheel spoke "+spoke,Quaternion.Euler(0,0,angle)*new Vector3(.032f,0,0),new Vector3(.064f,.008f,.010f),steel);
                    part.localRotation=Quaternion.Euler(0,0,angle);
                }
                Cylinder(wheel,"Handwheel hub",Vector3.zero,.016f,.023f,bronze,Quaternion.Euler(90,0,0));
                var crank=Node(wheel,"Turning crank pin"); crank.localPosition=new Vector3(.048f,0,-.016f);
                Cylinder(crank,"Hand crank",new Vector3(0,0,-.012f),.007f,.035f,wood,Quaternion.Euler(90,0,0));
                var rod=Cylinder(Pump,"Piston connecting rod "+i,Vector3.zero,.004f,.10f,steel,Quaternion.identity);
                if(i==0){pistonA=piston;wheelA=wheel;crankA=crank;rodA=rod;pistonAHome=piston.localPosition;}
                else{pistonB=piston;wheelB=wheel;crankB=crank;rodB=rod;pistonBHome=piston.localPosition;}
                Cylinder(Pump,"Pump foot "+i,new Vector3(i==0?-.115f:.115f,.044f,0),.013f,.050f,steel,Quaternion.identity);
            }
            Cylinder(Pump,"Outlet manifold",new Vector3(0,.084f,.057f),.014f,.17f,enamel,Quaternion.Euler(0,0,90));
            AirOutlet=Node(Pump,"Pump hose outlet"); AirOutlet.localPosition=new Vector3(-.158f,.087f,.038f);
            Tube(Pump,"Manifold to hose union",new Vector3(-.085f,.084f,.057f),AirOutlet.localPosition,.012f,enamel);
            Cylinder(AirOutlet,"Hose union",Vector3.zero,.014f,.038f,bronze,Quaternion.Euler(0,0,90));
            AnimatePump(0,false);
        }

        void BuildTelephone()
        {
            SurfaceTelephone=Node(VoiceRoot,"Surface telephone box");
            Box(SurfaceTelephone,"Wooden telephone case",new Vector3(0,.064f,0),new Vector3(.232f,.116f,.145f),wood);
            Box(SurfaceTelephone,"Telephone face plate",new Vector3(0,.068f,-.077f),new Vector3(.205f,.090f,.012f),enamel);
            for(int i=0;i<4;i++)
                Cylinder(SurfaceTelephone,"Case screw "+i,new Vector3((i%2==0?-.092f:.092f),i<2?.036f:.103f,-.087f),.005f,.009f,bronze,Quaternion.Euler(90,0,0));
            // A paired headset represents the general equipment type, not an exact museum object.
            Vector3 previous=Vector3.zero;
            for(int i=0;i<=14;i++)
            {
                float angle=Mathf.PI*i/14;
                Vector3 p=new Vector3(Mathf.Cos(angle)*.078f,.130f+Mathf.Sin(angle)*.102f,-.006f);
                if(i>0) Tube(SurfaceTelephone,"Headband section "+i,previous,p,.007f,rubber);
                previous=p;
            }
            for(int ear=0;ear<2;ear++)
            {
                float x=ear==0?-.078f:.078f;
                Cylinder(SurfaceTelephone,"Headset receiver "+ear,new Vector3(x,.133f,-.009f),.030f,.027f,bronze,Quaternion.Euler(90,0,0));
                Cylinder(SurfaceTelephone,"Receiver cushion "+ear,new Vector3(x,.133f,-.028f),.025f,.014f,rubber,Quaternion.Euler(90,0,0));
            }
            Cylinder(SurfaceTelephone,"Surface microphone rim",new Vector3(-.035f,.067f,-.094f),.031f,.016f,bronze,Quaternion.Euler(90,0,0));
            surfaceDiaphragm=Cylinder(SurfaceTelephone,"Surface telephone diaphragm",new Vector3(-.035f,.067f,-.105f),.024f,.010f,rubber,Quaternion.Euler(90,0,0));
            surfaceDiaphragmHome=surfaceDiaphragm.localPosition;
            for(int slot=0;slot<3;slot++)
                Box(SurfaceTelephone,"Microphone grille "+slot,new Vector3(-.035f+(slot-1)*.011f,.067f,-.113f),new Vector3(.004f,.040f,.004f),steel);
            Cylinder(SurfaceTelephone,"Telephone control",new Vector3(.063f,.066f,-.095f),.016f,.021f,rubber,Quaternion.Euler(90,0,0));
            TelephoneOutlet=Node(SurfaceTelephone,"Telephone cable outlet");TelephoneOutlet.localPosition=new Vector3(.131f,.069f,.024f);
            Cylinder(TelephoneOutlet,"Cable plug",Vector3.zero,.012f,.038f,bronze,Quaternion.Euler(0,0,90));
            BuildWaves(SurfaceTelephone,surfaceWaves,surfaceDiaphragmHome,"Surface voice wave ");
        }

        void BuildTransducer()
        {
            DiverTransducer=Node(VoiceRoot,"Detached diver transducer");
            Box(DiverTransducer,"Interpretive transducer stand",new Vector3(0,.014f,0),new Vector3(.14f,.028f,.105f),enamel);
            Cylinder(DiverTransducer,"Transducer stand",new Vector3(0,.065f,0),.010f,.090f,steel,Quaternion.identity);
            Cylinder(DiverTransducer,"Transducer housing",new Vector3(0,.126f,0),.058f,.040f,bronze,Quaternion.Euler(90,0,0));
            var rim=MeshPart(DiverTransducer,"Transducer rim",ringMesh,steel);rim.localPosition=new Vector3(0,.126f,-.024f);rim.localScale=Vector3.one*.105f;
            diverDiaphragm=Cylinder(DiverTransducer,"Diver transducer diaphragm",new Vector3(0,.126f,-.029f),.044f,.008f,rubber,Quaternion.Euler(90,0,0));
            diverDiaphragmHome=diverDiaphragm.localPosition;
            for(int grille=0;grille<4;grille++)
                Box(DiverTransducer,"Transducer grille "+grille,new Vector3((grille-1.5f)*.016f,.126f,-.037f),new Vector3(.004f,.075f,.005f),steel);
            TransducerInlet=Node(DiverTransducer,"Diver cable inlet");TransducerInlet.localPosition=new Vector3(-.070f,.10f,.013f);
            Cylinder(TransducerInlet,"Transducer cable union",Vector3.zero,.011f,.035f,bronze,Quaternion.Euler(0,0,90));
            BuildWaves(DiverTransducer,diverWaves,diverDiaphragmHome,"Diver voice wave ");
        }

        void BuildWaves(Transform parent,Transform[] waves,Vector3 position,string name)
        {
            for(int i=0;i<waves.Length;i++)
            {
                waves[i]=MeshPart(parent,name+i,ringMesh,voiceSignal);
                waves[i].localPosition=position;
                waves[i].localScale=Vector3.one*.080f;
                waves[i].gameObject.SetActive(false);
            }
        }

        public void SetMode(bool air)
        {
            AirRoot.gameObject.SetActive(air);VoiceRoot.gameObject.SetActive(!air);
            foreach(var packet in airPackets) packet.gameObject.SetActive(air);
        }

        public void Position(Vector3 centre,Quaternion facing)
        {
            Vector3 right=facing*Vector3.right;
            Pump.SetPositionAndRotation(centre+right*.47f-Vector3.up*.075f,facing);
            SurfaceTelephone.SetPositionAndRotation(centre+right*.34f-Vector3.up*.025f,facing);
            DiverTransducer.SetPositionAndRotation(centre+right*.67f+Vector3.up*.045f,facing);
        }

        public void SetAttention(bool air,float source,float destination)
        {
            ApplyFocus(pumpSurfaces,air?source:0);
            ApplyFocus(telephoneSurfaces,air?0:source);
            ApplyFocus(diverSurfaces,air?0:destination);
        }
        void ApplyFocus(Renderer[] renderers,float value)
        {
            foreach(var renderer in renderers)
            {
                if(!renderer || !renderer.sharedMaterial || !renderer.sharedMaterial.HasProperty(FocusId))continue;
                renderer.GetPropertyBlock(attentionBlock);attentionBlock.SetFloat(FocusId,value);renderer.SetPropertyBlock(attentionBlock);
            }
        }

        public void AnimatePump(float seconds,bool moving)
        {
            float cycle=moving?seconds*3.9f:0f;
            wheelA.localRotation=Quaternion.Euler(0,0,-cycle*Mathf.Rad2Deg);
            wheelB.localRotation=Quaternion.Euler(0,0,-cycle*Mathf.Rad2Deg+180f);
            pistonA.localPosition=pistonAHome+Vector3.up*(moving?Mathf.Sin(cycle)*.020f:0);
            pistonB.localPosition=pistonBHome+Vector3.up*(moving?-Mathf.Sin(cycle)*.020f:0);
            PositionRod(rodA,crankA.position,pistonA.TransformPoint(new Vector3(0,.069f,0)),.004f);
            PositionRod(rodB,crankB.position,pistonB.TransformPoint(new Vector3(0,.069f,0)),.004f);
        }

        public void AnimateVoice(float seconds,bool surfaceSpeaking,bool diverSpeaking,bool moving)
        {
            AnimateDiaphragm(surfaceDiaphragm,surfaceDiaphragmHome,surfaceWaves,seconds,surfaceSpeaking&&moving);
            AnimateDiaphragm(diverDiaphragm,diverDiaphragmHome,diverWaves,seconds,diverSpeaking&&moving);
        }

        public void AnimateReception(float seconds,bool surfaceReceiving,bool diverReceiving)
        {
            // A small receiver response has no outward speech waves or new historical mechanism claim.
            if(surfaceReceiving)surfaceDiaphragm.localPosition=surfaceDiaphragmHome+Vector3.forward*(Mathf.Sin(seconds*16f)*.0015f);
            if(diverReceiving)diverDiaphragm.localPosition=diverDiaphragmHome+Vector3.forward*(Mathf.Sin(seconds*16f)*.0015f);
        }

        static void AnimateDiaphragm(Transform diaphragm,Vector3 home,Transform[] waves,float seconds,bool speaking)
        {
            diaphragm.localPosition=home+Vector3.forward*(speaking?Mathf.Sin(seconds*24f)*.003f:0);
            for(int i=0;i<waves.Length;i++)
            {
                waves[i].gameObject.SetActive(speaking);
                if(!speaking) continue;
                float phase=Mathf.Repeat(seconds*.72f+i/(float)waves.Length,1f);
                waves[i].localPosition=home+Vector3.back*(.035f+phase*.065f);
                waves[i].localScale=Vector3.one*Mathf.Lerp(.070f,.135f,phase);
            }
        }

        public void Dispose()
        {
            foreach(var material in materials) DestroyOwned(material);
            foreach(var mesh in meshes) DestroyOwned(mesh);
            materials.Clear();meshes.Clear();
        }

        Material Tint(Material template,string name,Color color)
        {
            var material=new Material(template){name=name};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(material.HasProperty("_InsideColor"))material.SetColor("_InsideColor",color*.8f);
            if(material.HasProperty("_CutEnabled"))material.SetFloat("_CutEnabled",0);
            if(material.HasProperty("_Hover"))material.SetFloat("_Hover",0);
            if(material.HasProperty("_Focus"))material.SetFloat("_Focus",0);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",.32f);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.4f);
            materials.Add(material);return material;
        }

        static Transform Node(Transform parent,string name)
        {var result=new GameObject(name).transform;result.SetParent(parent,false);return result;}
        static Transform Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {var part=Primitive(parent,name,PrimitiveType.Cube,material);part.localPosition=position;part.localScale=size;return part;}
        static Transform Cylinder(Transform parent,string name,Vector3 position,float radius,float height,Material material,Quaternion rotation)
        {var part=Primitive(parent,name,PrimitiveType.Cylinder,material);part.localPosition=position;part.localRotation=rotation;part.localScale=new Vector3(radius*2,height*.5f,radius*2);return part;}
        static Transform Tube(Transform parent,string name,Vector3 a,Vector3 b,float radius,Material material)
        {return Cylinder(parent,name,(a+b)*.5f,radius,(b-a).magnitude,material,Quaternion.FromToRotation(Vector3.up,b-a));}
        static Transform Primitive(Transform parent,string name,PrimitiveType type,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            var collider=go.GetComponent<Collider>();collider.enabled=false;DestroyOwned(collider);
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;
        }
        static Transform MeshPart(Transform parent,string name,Mesh mesh,Material material)
        {
            var part=Node(parent,name);part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return part;
        }
        static void PositionRod(Transform rod,Vector3 from,Vector3 to,float radius)
        {rod.SetPositionAndRotation((from+to)*.5f,Quaternion.FromToRotation(Vector3.up,to-from));rod.localScale=new Vector3(radius*2,(to-from).magnitude*.5f,radius*2);}

        Mesh MakeRingMesh()
        {
            const int n=32,m=8;var vertices=new Vector3[n*m];var triangles=new int[n*m*6];
            for(int i=0;i<n;i++)for(int j=0;j<m;j++)
            {
                float a=i*Mathf.PI*2/n,b=j*Mathf.PI*2/m;
                vertices[i*m+j]=new Vector3((.45f+.05f*Mathf.Cos(b))*Mathf.Cos(a),(.45f+.05f*Mathf.Cos(b))*Mathf.Sin(a),.05f*Mathf.Sin(b));
                int next=(i+1)%n*m+j,across=i*m+(j+1)%m,diagonal=(i+1)%n*m+(j+1)%m,k=(i*m+j)*6;
                triangles[k]=i*m+j;triangles[k+1]=next;triangles[k+2]=diagonal;triangles[k+3]=i*m+j;triangles[k+4]=diagonal;triangles[k+5]=across;
            }
            var mesh=new Mesh{name="Illustrative equipment ring"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);return mesh;
        }

        Mesh MakeArrowMesh()
        {
            // Flat-sided packets read as direction symbols rather than underwater bubbles.
            Vector2[] outline={new Vector2(-.25f,0),new Vector2(.25f,0),new Vector2(.25f,.56f),new Vector2(.5f,.56f),new Vector2(0,1),new Vector2(-.5f,.56f),new Vector2(-.25f,.56f)};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int side=0;side<2;side++)
            {
                float y=side==0?-.5f:.5f;int centre=vertices.Count;vertices.Add(new Vector3(0,y,.47f));
                foreach(var p in outline)vertices.Add(new Vector3(p.x,y,p.y));
                for(int i=0;i<outline.Length;i++){triangles.Add(centre);triangles.Add(centre+1+(side==0?i:(i+1)%outline.Length));triangles.Add(centre+1+(side==0?(i+1)%outline.Length:i));}
            }
            for(int i=0;i<outline.Length;i++)
            {
                int j=(i+1)%outline.Length,k=vertices.Count;
                vertices.Add(new Vector3(outline[i].x,-.5f,outline[i].y));vertices.Add(new Vector3(outline[j].x,-.5f,outline[j].y));vertices.Add(new Vector3(outline[j].x,.5f,outline[j].y));vertices.Add(new Vector3(outline[i].x,.5f,outline[i].y));
                triangles.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
            }
            var mesh=new Mesh{name="Directional air packet"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);return mesh;
        }

        static void DestroyOwned(Object target)
        {if(!target)return;if(Application.isPlaying)Object.Destroy(target);else Object.DestroyImmediate(target);}
    }
}
