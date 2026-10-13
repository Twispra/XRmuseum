using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.XRTracking
{
    /// <summary>Editor desktop simulation yields to a real XR runtime or headset.</summary>
    /// <remarks>Never changes the visitor camera pose or the runtime's tracking origin.</remarks>
    public sealed class MuseumXRSimulatorGate : MonoBehaviour
    {
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        GameObject simulationRoot;
        bool hardwareSelected;
        bool changingMode;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (!FindObjectOfType<XROrigin>() || FindObjectOfType<MuseumXRSimulatorGate>())
                return;

            var root = new GameObject("Museum XR Simulator Gate");
            DontDestroyOnLoad(root);
            root.AddComponent<MuseumXRSimulatorGate>();
        }
#endif

        void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshMode();
        }

        void Update() => RefreshMode();

        void OnDeviceChange(UnityEngine.InputSystem.InputDevice device, InputDeviceChange change)
        {
            // Catch hot-plug before the simulator can write its next input frame.
            if (device is XRHMD && !(device is XRSimulatedHMD))
                RefreshMode();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshMode();

        bool HasHardware()
        {
            SubsystemManager.GetInstances(displays);
            foreach (var display in displays)
                if (display.running)
                    return true;

            foreach (var device in InputSystem.devices)
                if (device is XRHMD && !(device is XRSimulatedHMD))
                    return true;

            return false;
        }

        void RefreshMode()
        {
            if (changingMode)
                return;

            changingMode = true;
            try
            {
                if (!hardwareSelected && HasHardware())
                {
                    // Stay in hardware mode during temporary tracking/disconnection loss.
                    hardwareSelected = true;
                    StopSimulation();
                    Debug.Log("[Museum XR] XR runtime/headset detected; desktop simulation is disabled.");
                }

                if (hardwareSelected)
                {
                    // Also stop manually added simulators, without deleting authored objects.
                    foreach (var simulator in FindObjectsOfType<XRDeviceSimulator>())
                        simulator.gameObject.SetActive(false);
                    return;
                }

#if UNITY_EDITOR
                if (!simulationRoot && !FindObjectOfType<XRDeviceSimulator>() && FindObjectOfType<XROrigin>())
                    StartSimulation();
#endif
            }
            finally
            {
                changingMode = false;
            }
        }

#if UNITY_EDITOR
        void StartSimulation()
        {
            // XRI's settings type is internal; read its configured prefab through the Editor API.
            var settings = Resources.Load<ScriptableObject>("XRDeviceSimulatorSettings");
            GameObject prefab = null;
            if (settings)
            {
                using (var serialized = new UnityEditor.SerializedObject(settings))
                    prefab = serialized.FindProperty("m_SimulatorPrefab")?.objectReferenceValue as GameObject;
            }
            if (!prefab)
            {
                Debug.LogWarning("[Museum XR] Desktop simulator prefab is missing from XR Device Simulator settings.");
                return;
            }

            // Configure BEFORE Awake/OnEnable. The stock prefab otherwise removes real HMDs.
            simulationRoot = new GameObject("Museum Desktop XR Simulation");
            simulationRoot.transform.SetParent(transform, false);
            simulationRoot.SetActive(false);
            var instance = Instantiate(prefab, simulationRoot.transform);
            instance.name = prefab.name;
            var simulator = instance.GetComponentInChildren<XRDeviceSimulator>(true);
            if (!simulator)
            {
                StopSimulation();
                Debug.LogWarning("[Museum XR] Desktop simulator prefab has no XRDeviceSimulator component.");
                return;
            }

            simulator.removeOtherHMDDevices = false;
            simulationRoot.SetActive(true);
        }
#endif

        void StopSimulation()
        {
            if (!simulationRoot)
                return;

            // OnDisable removes simulated devices immediately; Destroy is deferred by Unity.
            simulationRoot.SetActive(false);
            Destroy(simulationRoot);
            simulationRoot = null;
        }

        void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopSimulation();
        }
    }
}
