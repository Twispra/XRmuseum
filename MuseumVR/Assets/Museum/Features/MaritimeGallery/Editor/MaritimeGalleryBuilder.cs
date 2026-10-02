#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.Inspection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.MaritimeGallery.Editor
{
    /// <summary>Authored gallery environment. Never invokes the original prototype generators.</summary>
    public static class MaritimeGalleryBuilder
    {
        public const string Root = "Assets/Museum/Features/MaritimeGallery";
        public const string PrefabPath = Root + "/Prefabs/MaritimeGallery.prefab";
        public const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        public const string EnvironmentName = "Maritime Gallery Environment";
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, List<CombineInstance>> Geometry = new Dictionary<string, List<CombineInstance>>();
        static readonly List<Mesh> TemporaryMeshes = new List<Mesh>();
        static Mesh cubeMesh, cylinderMesh;
        static Transform root;
        static Color Ink = new Color(.025f,.065f,.085f), Warm = new Color(.92f,.89f,.78f), Aqua = new Color(.30f,.67f,.69f);

        [MenuItem("Museum/Maritime Gallery/Install environment (first time only)")]
        public static void Install()
        {
            Guard();
            if (File.Exists(PrefabPath) || File.Exists(PrefabPath + ".meta"))
                throw new InvalidOperationException("Saved environment already exists. Open MuseumStart; do not reinstall it.");
            BuildAndIntegrate(false);
        }

        // Deliberately no menu: development-only revision of this task's own output, with a backup.
        // It is never called on project open or by a runtime component.
        public static void ReviseOwnedEnvironment()
        {
            Guard();
            if (!File.Exists(PrefabPath)) throw new InvalidOperationException("Expected this task's existing environment prefab.");
            BuildAndIntegrate(true);
        }

        public static void InstallBatch() { RunBatch(Install); }
        public static void ReviseBatch() { RunBatch(ReviseOwnedEnvironment); }
        static void RunBatch(Action action)
        {
            try { action(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before opening MuseumStart.");
        }

        static void BuildAndIntegrate(bool revision)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var existing = GameObject.Find(EnvironmentName);
            if (revision != (existing != null)) throw new InvalidOperationException("Scene installation state does not match the requested operation.");
            var helmet = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            var floor = GameObject.Find("Floor - Teleport Area");
            var oldLight = GameObject.Find("Gallery Daylight")?.GetComponent<Light>();
            if (!helmet || !floor || !floor.GetComponent<TeleportationArea>() || !oldLight || !Camera.main)
                throw new InvalidOperationException("Expected saved main-gallery components missing; installation refused.");
            Vector3 cameraPosition = Camera.main.transform.position;
            Quaternion cameraRotation = Camera.main.transform.rotation;
            string backup = Path.GetFullPath("../tmp/maritime-install-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, Path.Combine(backup, "MuseumStart.before.unity"));
            if (revision)
            {
                // Include meshes/materials and their GUIDs: the prefab alone cannot restore an earlier appearance.
                foreach (string file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
                {
                    string destination = Path.Combine(backup, "MaritimeGallery", file.Substring(Root.Length + 1));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(file, destination);
                }
            }
            foreach (var directory in new[] {"Materials","Meshes","Prefabs"}) Directory.CreateDirectory(Root + "/" + directory);
            AssetDatabase.Refresh();
            CreateMaterials();
            Geometry.Clear(); TemporaryMeshes.Clear();
            cubeMesh = PrimitiveMesh(PrimitiveType.Cube); cylinderMesh = PrimitiveMesh(PrimitiveType.Cylinder);
            var environment = new GameObject(EnvironmentName);
            root = environment.transform;
            try
            {
                BuildEnvelope();
                BuildCeiling();
                BuildFloorDetail();
                BuildFeatureWall();
                BuildSideGalleries();
                BuildArrivalWall();
                BuildPlinthDetails();
                FlushGeometry();
                CreateLighting();
                PrefabUtility.SaveAsPrefabAsset(environment, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(environment);
                foreach (var mesh in TemporaryMeshes) if (mesh) UnityEngine.Object.DestroyImmediate(mesh);
                TemporaryMeshes.Clear();
            }
            if (existing) UnityEngine.Object.DestroyImmediate(existing);
            var installed = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            installed.name = EnvironmentName;
            RestyleSavedSurfaces(scene);
            // Soft, static gallery lighting. No post-processing, moving water, flashing strips or camera effects.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.48f,.52f,.55f);
            RenderSettings.fog = false;
            oldLight.color = new Color(1f,.93f,.80f);
            oldLight.intensity = 1.12f;
            EditorUtility.SetDirty(oldLight);
            if (Vector3.Distance(Camera.main.transform.position,cameraPosition) > .00001f || Quaternion.Angle(Camera.main.transform.rotation,cameraRotation) > .001f)
                throw new InvalidOperationException("Unexpected XR camera change; refusing to save.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("MARITIME_GALLERY_INSTALLED: saved prefab and original MuseumStart. Backup: " + backup);
        }

        static void CreateMaterials()
        {
            Materials.Clear();
            Material("Limestone", new Color(.65f,.65f,.59f), false, .03f, .24f);
            Material("Porcelain", new Color(.80f,.81f,.76f), false, .04f, .30f);
            Material("Navy", new Color(.022f,.062f,.087f), true);
            Material("Graphite", new Color(.058f,.095f,.112f), false, .28f, .28f);
            Material("Bronze", new Color(.57f,.38f,.17f), false, .56f, .36f);
            Material("Walnut", new Color(.26f,.16f,.087f), false, 0f, .23f);
            Material("Floor", new Color(.29f,.34f,.35f), false, .08f, .25f);
            Material("FloorSeam", new Color(.21f,.26f,.28f), false, .08f, .20f);
            Material("WarmLight", new Color(.94f,.82f,.57f), true);
            Material("CoolLight", new Color(.27f,.63f,.66f), true);
            Material("Ink", new Color(.017f,.041f,.065f), true);
            Material("Ocean", new Color(.03f,.12f,.17f), true);
            Material("Contour", new Color(.08f,.25f,.30f), true);
        }
        static void Material(string name, Color color, bool unlit, float metallic = 0f, float smoothness = .2f)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("Required URP shader unavailable: " + name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) {name = "Gallery " + name}; AssetDatabase.CreateAsset(material,path); }
            material.shader = shader; material.SetColor("_BaseColor",color);
            if (!unlit)
            {
                material.SetFloat("_Metallic",metallic); material.SetFloat("_Smoothness",smoothness);
                // URP Lit controls shadow reception through the material, not MeshRenderer.receiveShadows.
                bool receiveShadows = name != "Porcelain";
                material.SetFloat("_ReceiveShadows",receiveShadows ? 1f : 0f);
                if(receiveShadows) material.DisableKeyword("_RECEIVE_SHADOWS_OFF"); else material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material); Materials.Add(name,material);
        }

        static void BuildEnvelope()
        {
            // The old floor and side/rear wall colliders remain authoritative. Only close the missing edges.
            SolidBoundary("Front boundary",new Vector3(0,2.1f,-4.98f),new Vector3(12,4.2f,.16f),"Navy");
            SolidBoundary("Ceiling",new Vector3(0,4.15f,2),new Vector3(12,.20f,14),"Graphite");
            foreach(float x in new[]{-5.84f,5.84f})
            {
                Box("Wall lining","Porcelain",new Vector3(x,2.0f,2),new Vector3(.08f,3.98f,13.9f));
                Box("Baseboards","Graphite",new Vector3(x, .12f,2),new Vector3(.13f,.24f,13.9f));
                Box("Coves","Graphite",new Vector3(x,3.76f,2),new Vector3(.20f,.22f,13.9f));
                Box("Cove light","WarmLight",new Vector3(x*.988f,3.66f,2),new Vector3(.025f,.025f,13.65f));
                foreach(float z in new[]{-4.65f,-1.45f,1.9f,5.25f,8.65f})
                {
                    Box("Wall piers","Limestone",new Vector3(x*.984f,1.95f,z),new Vector3(.22f,3.65f,.20f));
                    Box("Pier inlay","Bronze",new Vector3(x*.962f,1.95f,z),new Vector3(.008f,3.35f,.036f));
                }
            }
            Box("Rear baseboard","Graphite",new Vector3(0,.12f,8.78f),new Vector3(11.8f,.24f,.12f));
            Box("Rear cornice","Limestone",new Vector3(0,3.91f,8.78f),new Vector3(11.8f,.2f,.14f));
            Box("Rear cove","WarmLight",new Vector3(0,3.68f,8.67f),new Vector3(11.5f,.035f,.025f));
        }
        static void BuildCeiling()
        {
            for(int i=0;i<12;i++)
            {
                float z=-4.1f+i*1.12f;
                Box("Ceiling ribs","Porcelain",new Vector3(0,3.94f,z),new Vector3(11.55f,.12f,.25f));
                if(i%2==0) Box("Ceiling slots","WarmLight",new Vector3(0,3.865f,z),new Vector3(9.8f,.018f,.038f));
            }
            // Thin, suspended concentric light frames establish the central exhibit's volume.
            Ring("Pendant","Graphite",new Vector3(0,3.46f,3.5f),1.43f,.055f,new Vector3(90,0,0),96);
            Ring("Pendant bronze","Bronze",new Vector3(0,3.455f,3.5f),1.49f,.016f,new Vector3(90,0,0),96);
            Ring("Pendant diffuser","WarmLight",new Vector3(0,3.405f,3.5f),1.43f,.017f,new Vector3(90,0,0),96);
            foreach(float x in new[]{-1.02f,1.02f}) foreach(float z in new[]{2.5f,4.5f})
                Tube("Suspension","Graphite",new Vector3(x,3.49f,z),new Vector3(x,4.02f,z),.007f);
        }
        static void BuildFloorDetail()
        {
            for(int x=-4;x<=4;x+=2) Box("Floor joints","FloorSeam",new Vector3(x,.0025f,2),new Vector3(.012f,.003f,13.75f));
            for(int z=-3;z<=7;z+=2) Box("Floor joints","FloorSeam",new Vector3(0,.0025f,z),new Vector3(11.7f,.003f,.012f));
            // Flat inlays, no raised dais or steps around the visitor and no extra floor collider.
            foreach(float x in new[]{-4.75f,4.75f}) Box("Floor perimeter","Bronze",new Vector3(x,.004f,2),new Vector3(.018f,.003f,11.9f));
            foreach(float z in new[]{-3.95f,7.95f}) Box("Floor perimeter","Bronze",new Vector3(0,.004f,z),new Vector3(9.5f,.003f,.018f));
            for(int i=0;i<3;i++)
                Box("Arrival inlays","Bronze",new Vector3(0,.004f,-2.65f+i*.42f),new Vector3(.42f-i*.10f,.003f,.025f));
        }
        static void BuildFeatureWall()
        {
            Box("Feature wall","Navy",new Vector3(0,2.05f,8.60f),new Vector3(11.35f,3.7f,.18f));
            // Architectural relief; it is not a geographic map or a reconstruction of underwater scenery.
            Disc("Ocean medallion","Ocean",new Vector3(0,1.79f,8.487f),1.45f,.025f);
            Ring("Medallion rim","Bronze",new Vector3(0,1.79f,8.452f),1.49f,.021f,Vector3.zero,112);
            Ring("Medallion hairline","CoolLight",new Vector3(0,1.79f,8.449f),1.41f,.005f,Vector3.zero,112);
            for(int j=0;j<12;j++)
            {
                float y=-1.08f+j*.18f;
                float half=Mathf.Sqrt(Mathf.Max(0,1.30f*1.30f-y*y));
                Vector3 previous=Vector3.zero;
                for(int i=0;i<=40;i++)
                {
                    float x=Mathf.Lerp(-half,half,i/40f);
                    float wave=.043f*Mathf.Sin(x*3.2f+j*.65f)*Mathf.Sin(i/40f*Mathf.PI);
                    var p=new Vector3(x,1.79f+y+wave,8.449f);
                    if(i>0)
                    {
                        Vector3 d=p-previous;
                        Box("Ocean contour relief","Contour",(previous+p)*.5f,new Vector3(d.magnitude,.012f,.002f),new Vector3(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg));
                    }
                    previous=p;
                }
            }
            Label("Gallery masthead","B E N E A T H   T H E   S U R F A C E",new Vector3(0,3.35f,8.475f),new Vector2(9.4f,.56f),5.2f,Warm);
            Label("Gallery subtitle","THE DIVING GALLERY",new Vector3(0,2.94f,8.469f),new Vector2(7.8f,.24f),2f,Aqua);
            // The essential question sits beside the helmet, outside its close-view silhouette.
            Label("Central question","Could a diver\nwork alone\nin this helmet?",new Vector3(3.15f,2.10f,8.411f),new Vector2(2.36f,1.04f),2.70f,Warm);
            Label("Central prompt","LOOK CLOSER",new Vector3(0,.85f,8.410f),new Vector2(2.20f,.27f),1.80f,new Color(.62f,.77f,.78f));
            foreach(float x in new[]{-4.75f,-4.50f,-4.25f,4.25f,4.50f,4.75f})
                Box("Rear fins","Walnut",new Vector3(x,1.89f,8.425f),new Vector3(.065f,3.08f,.14f));
            Label("Left rear marker","01 / OBSERVE",new Vector3(-3.15f,2.43f,8.466f),new Vector2(2.1f,.28f),2.0f,Warm);
            Label("Left rear prompt","The shell.\nThe window.\nThe view within.",new Vector3(-3.15f,1.84f,8.466f),new Vector2(2.1f,.66f),1.72f,new Color(.62f,.75f,.75f));
            Label("Right rear marker","02 / CONNECT",new Vector3(3.15f,2.91f,8.466f),new Vector2(2.1f,.28f),2.0f,Aqua);
            Label("Right rear prompt","Where does air come from?\nHow does a message travel?",new Vector3(3.15f,1.23f,8.466f),new Vector2(2.25f,.55f),1.48f,new Color(.62f,.75f,.75f));
        }
        static void BuildSideGalleries()
        {
            foreach(int side in new[]{-1,1})
            {
                var rot=new Vector3(0,side<0?-90:90,0);
                Box("Side inset","Navy",new Vector3(side*5.55f,1.91f,3.50f),new Vector3(.16f,2.45f,5.70f));
                Box("Inset cap","Bronze",new Vector3(side*5.456f,3.15f,3.50f),new Vector3(.018f,.019f,5.70f));
                Label("Side masthead "+side,side<0?"A WINDOW INTO HISTORY":"CONNECTED TO THE SURFACE",new Vector3(side*5.455f,2.81f,3.5f),new Vector2(5.12f,.34f),2.8f,Warm,rot);
                Label("Side caption "+side,side<0?"Look around the object.\nFind the details that reveal its purpose.":"Air. Communication. Support.\nA helmet is part of a larger story.",new Vector3(side*5.453f,2.30f,3.5f),new Vector2(5.12f,.48f),1.52f,new Color(.61f,.75f,.76f),rot);
                // Repeated, physically modelled graphic motifs read as designed displays from oblique views.
                for(int i=0;i<3;i++)
                {
                    Vector3 c=new Vector3(side*5.43f,1.50f,1.8f+i*1.7f);
                    Ring("Side diagram frames","Bronze",c,.43f,.01f,rot,56);
                    Ring("Side diagram ink","Contour",c,.36f,.008f,rot,48);
                    if(side<0)
                    {
                        Ring("Window graphic","CoolLight",c,.20f,.009f,rot,48);
                        var rotation=Quaternion.Euler(rot);
                        for(int n=-1;n<=1;n++)
                            Tube("Window graphic bars","Contour",c+rotation*new Vector3(n*.1f,-.17f,-.005f),c+rotation*new Vector3(n*.1f,.17f,-.005f),.006f);
                    }
                    else
                    {
                        var rotation=Quaternion.Euler(rot);
                        for(int n=0;n<5;n++)
                        {
                            float height=.08f+.15f*Mathf.Sin((n+1)*.65f+i*.4f);
                            Tube("Connection bars","CoolLight",c+rotation*new Vector3((n-2)*.09f,-height,-.01f),c+rotation*new Vector3((n-2)*.09f,height,-.01f),.009f);
                        }
                    }
                }
                for(int i=0;i<11;i++)
                    Box("Wall acoustic fins","Walnut",new Vector3(side*5.70f,1.9f,-3.92f+i*.18f),new Vector3(.12f,3.28f,.047f));
                Label("Side quiet marker "+side,"TAKE YOUR\nTIME",new Vector3(side*5.69f,2.54f,7.48f),new Vector2(1.80f,.62f),2.0f,Ink,rot);
                Label("Side quiet text "+side,"Pause. Look again.\nFollow your curiosity.",new Vector3(side*5.69f,1.75f,7.48f),new Vector2(1.80f,.60f),1.40f,Ink,rot);
            }
        }
        static void BuildArrivalWall()
        {
            Box("Arrival frame","Limestone",new Vector3(0,2.03f,-4.835f),new Vector3(6.9f,3.62f,.10f));
            Box("Arrival panel","Navy",new Vector3(0,2.10f,-4.75f),new Vector3(6.45f,3.16f,.09f));
            foreach(float x in new[]{-3.27f,3.27f}) Box("Arrival bronze","Bronze",new Vector3(x,2.1f,-4.69f),new Vector3(.025f,3.22f,.02f));
            var rear=new Vector3(0,180,0);
            Label("Arrival brand","THE DIVING GALLERY",new Vector3(0,3.17f,-4.686f),new Vector2(5.6f,.49f),4.0f,Warm,rear);
            Label("Arrival introduction","One object. Many ways to explore.",new Vector3(0,2.64f,-4.681f),new Vector2(5.6f,.32f),2.20f,Aqua,rear);
            Label("Arrival instructions","Start with the helmet.\nBring it closer, turn it, and look inside.",new Vector3(0,1.87f,-4.681f),new Vector2(5.5f,.68f),2.32f,Warm,rear);
            Label("Arrival accessibility","EXPLORE SEATED OR STANDING  /  ONE HAND",new Vector3(0,.98f,-4.681f),new Vector2(5.65f,.26f),1.47f,new Color(.67f,.78f,.77f),rear);
            for(int i=0;i<5;i++)
            {
                float x=(i-2)*.54f;
                Box("Arrival rhythm","CoolLight",new Vector3(x,.61f,-4.68f),new Vector3(.3f,.014f,.008f));
            }
        }
        static void BuildPlinthDetails()
        {
            foreach(float x in new[]{-3.1f,0,3.1f})
            {
                Box("Plinth foot","Graphite",new Vector3(x,.055f,3.5f),new Vector3(1.40f,.10f,1.40f));
                foreach(float z in new[]{2.813f,4.187f})
                {
                    Box("Plinth trim","Bronze",new Vector3(x,.21f,z),new Vector3(1.38f,.022f,.015f));
                    Box("Plinth base glow","CoolLight",new Vector3(x,.11f,z),new Vector3(1.30f,.012f,.013f));
                }
                foreach(float xx in new[]{x-.686f,x+.686f})
                {
                    Box("Plinth trim","Bronze",new Vector3(xx,.21f,3.5f),new Vector3(.015f,.022f,1.38f));
                    Box("Plinth inset","Navy",new Vector3(xx,.66f,3.5f),new Vector3(.012f,.59f,1.13f));
                }
                string title=x==0?"DIVING HELMET":x<0?"LOOK CLOSER":"STAY CONNECTED";
                Label("Plinth title "+x,title,new Vector3(x,.62f,2.813f),new Vector2(1.20f,.19f),1.70f,Ink);
                Label("Plinth caption "+x,x==0?"MARK V-INSPIRED STUDY":"INTERPRETIVE DISPLAY",new Vector3(x,.43f,2.811f),new Vector2(1.20f,.16f),.84f,Ink);
            }
            // Porthole study, not another fabricated historical exhibit.
            var left=new Vector3(-3.1f,1.63f,3.5f);
            Box("Study mounts","Graphite",new Vector3(-3.1f,1.19f,3.5f),new Vector3(.72f,.06f,.46f));
            Tube("Study stems","Bronze",new Vector3(-3.1f,1.2f,3.5f),left-Vector3.up*.33f,.018f);
            Ring("Porthole study","Bronze",left,.33f,.041f,new Vector3(0,18,0),72);
            Ring("Porthole inner","CoolLight",left+Vector3.back*.01f,.258f,.008f,new Vector3(0,18,0),64);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;
                var p=left+Quaternion.Euler(0,18,0)*new Vector3(Mathf.Cos(a)*.33f,Mathf.Sin(a)*.33f,-.044f);
                Box("Porthole fastener studies","Graphite",p,new Vector3(.03f,.03f,.019f),new Vector3(0,18,0));
            }
            // Two visually distinct channels introduce the future air/voice story without claiming an animation exists.
            Box("Connection mount","Graphite",new Vector3(3.1f,1.19f,3.5f),new Vector3(.74f,.06f,.46f));
            for(int strand=0;strand<2;strand++)
            {
                Vector3 previous=Vector3.zero;
                for(int i=0;i<=24;i++)
                {
                    float t=i/24f;
                    var p=new Vector3(3.1f+(strand==0?-.16f:.16f)+.055f*Mathf.Sin(t*Mathf.PI*2),1.25f+t*.60f,3.5f+.065f*Mathf.Cos(t*Mathf.PI*2));
                    if(i>0) Tube("Connection strands",strand==0?"Bronze":"CoolLight",previous,p,.020f);
                    previous=p;
                }
            }
            // A two-sided physical sign avoids mirrored TMP lettering when visitors look from behind.
            Box("Connection sign","Graphite",new Vector3(3.1f,1.34f,3.25f),new Vector3(.80f,.16f,.020f));
            Label("Connection key","AIR  /  VOICE",new Vector3(3.1f,1.34f,3.235f),new Vector2(.74f,.11f),.76f,Warm);
            Label("Connection key rear","AIR  /  VOICE",new Vector3(3.1f,1.34f,3.265f),new Vector2(.74f,.11f),.76f,Warm,new Vector3(0,180,0));
        }
        static void CreateLighting()
        {
            foreach(float x in new[]{-3.1f,0,3.1f})
            {
                var go=new GameObject("Exhibit spotlight "+x);go.transform.SetParent(root,false);
                go.transform.position=new Vector3(x,3.65f,2.6f);
                go.transform.rotation=Quaternion.LookRotation(new Vector3(x,1.4f,3.5f)-go.transform.position);
                var light=go.AddComponent<Light>();light.type=LightType.Spot;light.range=5.2f;
                light.spotAngle=65f;light.innerSpotAngle=38f;light.intensity=x==0?2.5f:1.5f;
                light.color=new Color(1,.90f,.71f);light.shadows=LightShadows.None;
                Box("Spot housings","Graphite",go.transform.position,new Vector3(.16f,.13f,.19f));
            }
            // Housings were added after the first flush; flush only the newly queued group.
            FlushGeometry();
        }
        static void RestyleSavedSurfaces(UnityEngine.SceneManagement.Scene scene)
        {
            foreach(var renderer in scene.GetRootGameObjects().Where(go=>go.name!=EnvironmentName).SelectMany(go=>go.GetComponentsInChildren<MeshRenderer>(true)))
            {
                string name=renderer.name;
                string material=name=="Floor - Teleport Area"?"Floor":name=="Plinth"?"Porcelain":name=="Display Top"?"Graphite":
                    (name=="Left Wall"||name=="Right Wall"||name=="Rear Wall")?"Navy":null;
                if(material!=null) { renderer.sharedMaterial=Materials[material]; EditorUtility.SetDirty(renderer); }
                if(name=="Plinth") { renderer.receiveShadows=false;EditorUtility.SetDirty(renderer); }
                if(name=="Left Wall"||name=="Right Wall"||name=="Rear Wall") { renderer.shadowCastingMode=ShadowCastingMode.Off;EditorUtility.SetDirty(renderer); }
                if(new[]{"Welcome Panel","Museum Title","Museum Subtitle","Welcome","Exhibit Number"}.Contains(name))
                { renderer.enabled=false;EditorUtility.SetDirty(renderer); }
                if(name=="Replace With Museum Exhibit")
                {
                    if(renderer.GetComponent<XRBaseInteractable>() || renderer.GetComponent<Rigidbody>())
                        throw new InvalidOperationException("Placeholder has acquired an interaction; do not hide another member's work.");
                    renderer.gameObject.SetActive(false);EditorUtility.SetDirty(renderer.gameObject);
                }
            }
        }

        static void SolidBoundary(string name,Vector3 center,Vector3 size,string material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);
            go.transform.localPosition=center;go.transform.localScale=size;
            go.GetComponent<MeshRenderer>().sharedMaterial=Materials[material];
            go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            go.isStatic=true;
        }
        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go=GameObject.CreatePrimitive(type);var mesh=go.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(go);return mesh;
        }
        static void Add(string group,string material,Mesh mesh,Matrix4x4 matrix)
        {
            string key=group+"__"+material;
            if(!Geometry.TryGetValue(key,out var list)) Geometry[key]=list=new List<CombineInstance>();
            list.Add(new CombineInstance{mesh=mesh,transform=matrix});
        }
        static void Box(string group,string material,Vector3 center,Vector3 size,Vector3 euler=default)
            =>Add(group,material,cubeMesh,Matrix4x4.TRS(center,Quaternion.Euler(euler),size));
        static void Tube(string group,string material,Vector3 a,Vector3 b,float radius)
        {
            var d=b-a;if(d.sqrMagnitude<.0000001f)return;
            Add(group,material,cylinderMesh,Matrix4x4.TRS((a+b)*.5f,Quaternion.FromToRotation(Vector3.up,d),new Vector3(radius*2,d.magnitude*.5f,radius*2)));
        }
        static void Disc(string group,string material,Vector3 center,float radius,float depth)
            =>Add(group,material,cylinderMesh,Matrix4x4.TRS(center,Quaternion.Euler(90,0,0),new Vector3(radius*2,depth*.5f,radius*2)));
        static void Ring(string group,string material,Vector3 center,float radius,float tube,Vector3 euler,int segments)
        {
            const int sides=8;var vertices=new Vector3[(segments+1)*(sides+1)];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*sides*6];int index=0;
            for(int i=0;i<=segments;i++)for(int j=0;j<=sides;j++)
            {
                float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides;int v=i*(sides+1)+j;
                var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);var n=radial*Mathf.Cos(b)+Vector3.forward*Mathf.Sin(b);
                vertices[v]=radial*radius+n*tube;normals[v]=n;uv[v]=new Vector2(i/(float)segments,j/(float)sides);
                if(i<segments&&j<sides)
                {
                    int next=v+sides+1;
                    triangles[index++]=v;triangles[index++]=next;triangles[index++]=next+1;
                    triangles[index++]=v;triangles[index++]=next+1;triangles[index++]=v+1;
                }
            }
            var mesh=new Mesh{name="Temporary torus"};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
            TemporaryMeshes.Add(mesh);Add(group,material,mesh,Matrix4x4.TRS(center,Quaternion.Euler(euler),Vector3.one));
        }
        static void FlushGeometry()
        {
            foreach(var pair in Geometry)
            {
                var mesh=new Mesh{name=pair.Key,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string path=Root+"/Meshes/"+pair.Key.Replace(" ","-")+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing) { EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(existing); }
                else AssetDatabase.CreateAsset(mesh,path);
                var go=new GameObject(pair.Key);go.transform.SetParent(root,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=Materials[pair.Key.Split(new[]{"__"},StringSplitOptions.None)[1]];
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;go.isStatic=true;
            }
            Geometry.Clear();
        }
        static void Label(string name,string text,Vector3 position,Vector2 size,float maximumSize,Color color,Vector3 euler=default)
        {
            var label=new GameObject(name).AddComponent<TextMeshPro>();label.transform.SetParent(root,false);
            label.transform.localPosition=position;label.transform.localRotation=Quaternion.Euler(euler);
            label.font=TMP_Settings.defaultFontAsset;label.text=text;label.color=color;
            label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=size;
            label.enableAutoSizing=true;label.fontSizeMin=maximumSize*.55f;label.fontSizeMax=maximumSize;label.fontSize=maximumSize;
            label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Overflow;
            label.margin=Vector4.zero;label.ForceMeshUpdate(true);
            var renderer=label.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
    }
}
#endif
