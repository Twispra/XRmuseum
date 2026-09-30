using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Inspection
{
    /// <summary>Desktop review aid only. The exhibit itself uses XRI and world-space controls.</summary>
    public sealed class HelmetInspectionDesktopPreview : MonoBehaviour
    {
        [SerializeField] HelmetInspectionController exhibit;
        [SerializeField] Camera previewCamera;
        Camera[] sceneCameras;
        bool[] cameraStates;
        XRDeviceSimulator simulator;
        bool simulatorWasEnabled;
        float yaw = -22f, elevation = 12f;
        bool active;
        GUIStyle title, body, button, caption;
        const float PanelWidth = 304;

        public void Configure(HelmetInspectionController controller, Camera camera)
        { exhibit = controller; previewCamera = camera; }

        void Start()
        {
            if (!exhibit || !previewCamera || XRSettings.isDeviceActive) { enabled = false; return; }
            sceneCameras = FindObjectsOfType<Camera>();
            cameraStates = new bool[sceneCameras.Length];
            for (int i=0;i<sceneCameras.Length;i++)
            { cameraStates[i] = sceneCameras[i].enabled; sceneCameras[i].enabled = sceneCameras[i] == previewCamera; }
            active = true;
            UpdateCamera();
        }

        void Update()
        {
            if (!active) return;
            if (XRSettings.isDeviceActive) { enabled = false; return; }
            // The official simulator is created after scene load. Disable only in this explicit desktop aid.
            if (!simulator)
            {
                simulator = FindObjectOfType<XRDeviceSimulator>();
                if (simulator) { simulatorWasEnabled = simulator.enabled; simulator.gameObject.SetActive(false); }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.position.ReadValue().x > PanelWidth)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x*.2f; elevation = Mathf.Clamp(elevation-delta.y*.15f,-20f,55f);
                }
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    var ray = previewCamera.ScreenPointToRay(mouse.position.ReadValue());
                    if (Physics.Raycast(ray, out var hit, 5f, ~0, QueryTriggerInteraction.Collide) &&
                        hit.collider.GetComponentInParent<HelmetWindowInteractable>()) exhibit.ToggleWindow();
                }
            }
            UpdateCamera();
        }

        void UpdateCamera()
        {
            Vector3 target = exhibit.transform.position + Vector3.up*.27f;
            Quaternion rotation = Quaternion.Euler(elevation,yaw,0);
            previewCamera.transform.SetPositionAndRotation(target + rotation*new Vector3(0,0,-1.22f),rotation);
        }

        void OnDisable()
        {
            if (!active) return;
            if (sceneCameras != null) for(int i=0;i<sceneCameras.Length;i++) if(sceneCameras[i]) sceneCameras[i].enabled = cameraStates[i];
            if (previewCamera) previewCamera.enabled = false;
            if (simulator && simulatorWasEnabled) simulator.gameObject.SetActive(true);
            active=false;
        }

        void OnGUI()
        {
            if (!active) return;
            if (title==null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize=23, fontStyle=FontStyle.Bold, wordWrap=true };
                body = new GUIStyle(GUI.skin.label) { fontSize=15, wordWrap=true };
                caption = new GUIStyle(body) { fontSize=12 };
                button = new GUIStyle(GUI.skin.button) { fontSize=16, fixedHeight=42, wordWrap=true };
            }
            GUI.Box(new Rect(10,10,PanelWidth-20,Screen.height-20),GUIContent.none);
            GUILayout.BeginArea(new Rect(24,24,PanelWidth-48,Screen.height-48));
            GUILayout.Label("INSIDE THE\nDIVING HELMET",title);
            GUILayout.Space(10);
            GUILayout.Label("01 / Open the front window",body);
            GUILayout.Label("Click the window itself, or use the button below.",caption);
            if(GUILayout.Button(exhibit.WindowAngle>5 ? "Close window" : "Open window",button)) exhibit.ToggleWindow();
            GUILayout.Space(12);
            GUILayout.Label("02 / Look beyond the shell",body);
            if(GUILayout.Button(exhibit.IsCutaway ? "Restore full shell" : "Reveal half section",button)) exhibit.ToggleCutaway();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Turn left",button)) exhibit.RotateDisplay(-30);
            if(GUILayout.Button("Turn right",button)) exhibit.RotateDisplay(30);
            GUILayout.EndHorizontal();
            if(GUILayout.Button("View into section",button)) { yaw=-65; elevation=14; }
            if(GUILayout.Button("Front view",button)) { yaw=-22; elevation=12; }
            GUILayout.Space(12);
            if(GUILayout.Button("Reset everything  [Home]",button)) { exhibit.GetComponent<HelmetExhibitReset>().ResetToDisplay(); yaw=-22; elevation=12; }
            GUILayout.Space(12);
            GUILayout.Label("Window: "+exhibit.WindowAngle.ToString("0")+"°\nShell: "+(exhibit.IsCutaway?"half section":"complete"),body);
            GUILayout.Space(10);
            GUILayout.Label("Right-drag the scene to orbit.\nDesktop preview · not a headset test.\n\nSection and hinge range are teaching aids. The hollow shell is not a complete reconstruction of historical internal equipment.",caption);
            GUILayout.EndArea();
        }
    }
}
