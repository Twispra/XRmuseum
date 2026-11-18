using Museum.Diegetic;
using Museum.Exploration;
using Museum.Localization;
using Museum.Refined;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.SurfaceLine
{
    /// <summary>The visitor speaks from the surface. The original cable demonstration owns all timing and endpoints.</summary>
    [DefaultExecutionOrder(320)]
    public sealed class SurfaceCall : MonoBehaviour
    {
        public HelmetDiegeticStation station;
        public GameObject installation, restingTelephone;
        public XRSimpleInteractable key;
        public TextMeshPro speaker, words, keyLegend;
        public Transform keyTravel;
        public GameObject surfaceFlag;
        public Quaternion telephoneRotation = Quaternion.identity;
        bool telephonePositioned;
        public Renderer lamp;
        public Material amber, lit;
        public HelmetPhysicalControl Control { get; private set; }
        public const string Question = "Could you work alone\nin this helmet?";
        public const string Outgoing = "Diver, do you read me?";
        public const string Incoming = "Loud and clear, surface.";
        public const string Finding = "Diver and surface team exchange messages through a cable.";

        RefinedHelmetBinding eras;
        int displayed = -1;
        bool staticExchange;
        bool held, hovered;
        Vector3 keyHome;

        void Start()
        {
            eras = station.Inspection.GetComponent<RefinedHelmetBinding>();
            keyHome = keyTravel.localPosition;
            Control = key.gameObject.AddComponent<HelmetPhysicalControl>();
            Control.Configure(station, key, null, null, null, null, "", "", Color.white, Operate, true);
            station.RegisterControl(Control);
            station.Controls[HelmetDiegeticStation.Command.CloseDemo].transform.localScale *= .68f;
            station.Demonstration.PlaybackChanged += PoseTelephone;
            Refresh();
        }

        void OnEnable()
        {
            if (!Control) return;
            station.RegisterControl(Control);
            displayed = -1;
            Refresh();
        }

        void OnDisable()
        {
            if (!Control) return;
            Control.CancelPress();
            if (station) station.UnregisterControl(Control);
            if (installation) installation.SetActive(false);
        }

        void OnDestroy()
        {
            if (station && station.Demonstration) station.Demonstration.PlaybackChanged -= PoseTelephone;
            if (Control) Destroy(Control);
        }

        void LateUpdate() => Refresh();

        void PoseTelephone()
        {
            var demo = station.Demonstration;
            if (!demo.IsVisible || demo.CurrentClue != HelmetExplorationController.Clue.Voice)
            {
                telephonePositioned = false;
                return;
            }
            if (telephonePositioned) return;
            var phone = demo.EquipmentExhibits.First(e => e.Id == "surface-telephone");
            var center = phone.Root.TransformPoint(phone.CenterLocal);
            phone.Root.rotation = telephoneRotation * phone.Root.rotation;
            phone.Root.position += center - phone.Root.TransformPoint(phone.CenterLocal);
            telephonePositioned = true;
        }

        void Operate()
        {
            if (station.Demonstration.IsVisible && !station.Demonstration.IsPlaying &&
                station.Demonstration.CurrentClue == HelmetExplorationController.Clue.Voice)
                station.Execute(HelmetDiegeticStation.Command.CloseDemo);
            station.Execute(HelmetDiegeticStation.Command.Voice);
        }

        void Refresh()
        {
            if (!Control) return;
            var demo = station.Demonstration;
            bool voice = demo.IsVisible && demo.CurrentClue == HelmetExplorationController.Clue.Voice;
            bool show = station.isActiveAndEnabled && eras.AppliedEraIndex == 1 &&
                !station.IsVisitHandoffOpen && (!demo.IsVisible || voice) && !station.SettingsVisible && !station.AboutVisible;
            if (installation.activeSelf != show) installation.SetActive(show);
            if (!show) { Control.CancelPress(); displayed = -1; return; }

            // The saved model and its moving counterpart occupy the same pose.
            if (restingTelephone.activeSelf == voice) restingTelephone.SetActive(!voice);
            int phase = !voice ? 0 : demo.PhaseIndex <= 1 ? 1 : demo.PhaseIndex == 2 ? 2 : demo.PhaseIndex == 3 ? 3 : 4;
            bool still = voice && station.Inspection.ReduceMotion;
            key.enabled = !voice || !demo.IsPlaying;
            if (phase == displayed && still == staticExchange && held == Control.IsPressed && hovered == key.isHovered) return;
            displayed = phase;
            staticExchange = still;
            surfaceFlag.SetActive(phase == 1 || phase == 2 || still);
            held = Control.IsPressed;
            hovered = key.isHovered;
            keyTravel.localPosition = keyHome + (held ? .009f * Vector3.forward : Vector3.zero);
            lamp.sharedMaterial = (hovered || held || voice) ? lit : amber;
            string heading = phase == 3 || still ? "Diver  /  Staged conversation" :
                phase == 4 ? "Two-way voice" : "Mark V  /  Surface telephone";
            string line = still ? "I hear you through the cable." : phase == 3 ? Incoming : phase == 4 ? Finding :
                phase == 2 ? "Waiting for the diver..." : Question;
            MuseumText.Set(speaker, heading);
            MuseumText.Set(words, line);
            MuseumText.Set(keyLegend, phase == 0 ? (held ? "Release to call" : "Call the diver") :
                phase == 4 ? "Call again" : "On the line");
        }
    }
}
