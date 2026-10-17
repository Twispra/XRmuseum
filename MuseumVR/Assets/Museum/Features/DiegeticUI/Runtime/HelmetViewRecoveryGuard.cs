using UnityEngine;
using UnityEngine.InputSystem;

namespace Museum.Diegetic
{
    // Runs before VisitorViewRecovery moves the rig. A rig jump must never become hand motion.
    [DefaultExecutionOrder(-220)]
    public sealed class HelmetViewRecoveryGuard : MonoBehaviour
    {
        HelmetDiegeticStation station;
        public void Configure(HelmetDiegeticStation value)=>station=value;
        void Update()
        {
            if(station)station.PruneReleasedHands();
            if(station && station.isActiveAndEnabled && Keyboard.current!=null && Keyboard.current.f11Key.wasPressedThisFrame)
                station.CancelInteractionForRecovery();
        }
    }
}
