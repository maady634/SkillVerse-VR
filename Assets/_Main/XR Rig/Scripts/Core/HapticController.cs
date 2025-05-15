using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;


namespace InfiVR
{
    public class HapticController : MonoBehaviour
    {
        [Range(0, 1)]
        [SerializeField] private float amplitude = .1f;
        [SerializeField] private float duration = 0.25f;
        private bool _sendingImpulse;

        public void PlayHaptics(XRBaseController controller)
        {
            controller.SendHapticImpulse(amplitude, duration);
        }

        public void PlayHaptics(XRBaseController controller, float ampl, float dur)
        {
            controller.SendHapticImpulse(ampl, dur);
        }
    } 
}
