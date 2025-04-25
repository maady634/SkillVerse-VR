using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;


    [RequireComponent(typeof(AudioSource))]
    public class AudioHandler : MonoBehaviour
    {
        [SerializeField] TeleportationProvider teleportationProvider;
        [Space]
        [SerializeField] AudioClip uiHoverEnterClip;
        [SerializeField] AudioClip uiClickClilp;
        [SerializeField] AudioClip uiHoverExitClip;
        [SerializeField] AudioClip uiSpawn;
        [SerializeField] AudioClip uiRemove;
        [Space]
        [SerializeField] AudioClip teleportClip;
        [Space]
        [SerializeField] AudioClip socketAttachClip;
        [Space]
        [SerializeField] AudioClip tickMarkClip;
        [SerializeField] AudioClip incorrectMarkClip;

        AudioSource commonAudioSource;

        protected void Awake()
        {
            commonAudioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            teleportationProvider.beginLocomotion += PlayTeleportationAudio;
        }

        private void OnDisable()
        {
            teleportationProvider.beginLocomotion -= PlayTeleportationAudio;
        }


        public void PlayButtonClickAudio(AudioClip clip = null)
            => PlayCommonAudioSourceClip(clip, uiClickClilp);
        public void PlayButtonHoverEnterAudio(AudioClip clip = null)
            => PlayCommonAudioSourceClip(clip, uiHoverEnterClip);

        public void PlayButtonHoverExitAudio(AudioClip clip = null)
            => PlayCommonAudioSourceClip(clip, uiHoverExitClip);

        public void PlaySocketAttachAudio(AudioClip clip = null)
            => PlayCommonAudioSourceClip(clip, socketAttachClip);

        public void PlayTickMarkAudio()
        {
            commonAudioSource.PlayOneShot(tickMarkClip);
        }

        public void PlayIncorrectMarkAudio()
        {
            commonAudioSource.PlayOneShot(incorrectMarkClip);
        }

        public void PlayUISpawnAudio(AudioClip clip = null)
            => PlayCommonAudioSourceClip(clip, uiSpawn);

        public void PlayUIRemoveAudio(AudioClip clip = null)
            => PlayCommonAudioSourceClip(clip, uiRemove);


        private void PlayCommonAudioSourceClip(AudioClip overrideClip, AudioClip original)
        {
            if (overrideClip == null)
                commonAudioSource.clip = original;
            else
                commonAudioSource.clip = overrideClip;

            commonAudioSource.Play();
        }

        private void PlayTeleportationAudio(LocomotionSystem system)
        {
            if (commonAudioSource != null)
            {
                commonAudioSource.clip = teleportClip;
                commonAudioSource.Play();
            }
        }
    }