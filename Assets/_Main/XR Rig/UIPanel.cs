/*using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

    public class UIPanel : MonoBehaviour
    {
        [SerializeField] bool followCamera;
        [SerializeField] AudioClip spawnAudioOverride;
        [SerializeField] AudioClip removeAudioOverride;

        //BasePanel panel;

        CancellationTokenSource objectAliveTokenSource;

    public AudioHandler thisAudio;

    public InfiVRRigManager rigManager;

    

    public bool FollowCamera
        {
            get => followCamera;
        }

        private void Awake()
        {
            if (TryGetComponent<BasePanel>(out var panel))
            {
                this.panel = panel;

                panel.onRemoveRequest += (callback) =>
                {
                    Remove(callback).Forget();
                };
            }
        }

        private void OnEnable()
        {
        thisAudio = FindAnyObjectByType<AudioHandler>();

        UpdatePositionRelativeToUser();

            if (panel != null)
            {
                ShowPanel().Forget();
            }
        }

        private void OnDisable()
        {
            objectAliveTokenSource?.Cancel();
        }

        [ContextMenu("Update Position")]
        public void UpdatePositionRelativeToUser()
        {
        thisAudio.PlayUISpawnAudio(spawnAudioOverride);

        rigManager.UpdatePanelPosition(transform);
        }

        [ContextMenu("Show Panel")]
        private async UniTask ShowPanel()
        {
            objectAliveTokenSource = new();

            float timer = 0;
            float t = 0;

            while (t < 1)
            {
                timer += Time.deltaTime;

                // Panel can be destroyed on scene change
                // panel might become null in that case.
                if (panel != null)
                {
                    t = timer / panel.WaitBeforeDestroy;
                }
                else
                {
                    transform.localScale = Vector3.one;
                    break;
                }

                transform.localScale = Vector3.one * UnityEngine.Mathf.Lerp(0.3f, 1, t);

                await UniTask.Yield(objectAliveTokenSource.Token);
            }
        }

        public virtual async UniTask Remove(Action callback)
        {
            thisAudio.PlayUIRemoveAudio(removeAudioOverride);

            float timer = 0;
            float t = 0;
            float fastScaleDuration = 0.2f; // Reduced duration for faster scaling

            while (t < 1)
            {
                timer += Time.deltaTime;
                t = timer / fastScaleDuration; // Faster transition

                transform.localScale = Vector3.one * Mathf.Lerp(1, 0.3f, t);

                await UniTask.Yield(objectAliveTokenSource.Token);
            }

            callback.Invoke();
        }
    }
*/