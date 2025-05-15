using Cysharp.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InfiVR
{
    public class SceneHandler : MonoBehaviour
    {
        [SerializeField] bool smoothSceneTransition;
        [SerializeField] GameObject simpleCameraRig;
        [SerializeField] Camera simpleCamera;
        [SerializeField] GameObject mainXRRig;
        [Space]
        [SerializeField] CanvasGroup loadingCanvasGroup;
        [SerializeField] Image loadingImage;

        Canvas loadingCanvas;

        public const string PPE_SCENE_NAME = "WelcomeAndPPE";
        public const string LOBBY_SCENE_NAME = "Lobby";

        protected void Awake()
        {
            // This script can be initialized multiple time.
            try
            {
                //base.Awake();
            }
            catch { }

            transform.parent = null;

            DontDestroyOnLoad(gameObject);
            if (smoothSceneTransition)
            {
                loadingCanvas = loadingCanvasGroup.GetComponent<Canvas>();
            }
        }

        public void LoadSceneByName(string name)
        {
            LoadScene(name).Forget();
        }

        public void LoadSceneLateByName(string name, float delay)
        {
            LoadScene(name, delay).Forget();
        }

        private async UniTask LoadScene(string name, float delayTime = 0)
        {
            await UniTask.WaitForSeconds(delayTime);

            if (smoothSceneTransition)
            {
                Debug.Log($"<color=white>LoadScene start</color>");
                await ToggleLoadingPanel(true);

                await UniTask.WaitForSeconds(0.5f);

                Destroy(mainXRRig);
                simpleCameraRig.SetActive(true);
                loadingCanvas.worldCamera = simpleCamera;
                loadingImage.fillAmount = 0;
                loadingImage.transform.parent.gameObject.SetActive(true);

                await UniTask.WaitForSeconds(1);

                var operation = SceneManager.LoadSceneAsync(name);

                while (!operation.isDone)
                {
                    loadingImage.fillAmount = operation.progress;
                    await UniTask.Yield();
                }

                await UniTask.WaitForSeconds(1);

                mainXRRig = FindObjectOfType<InfiVRRigManager>(true).gameObject;
                mainXRRig.SetActive(true);
                simpleCameraRig.SetActive(false);
                loadingImage.transform.parent.gameObject.SetActive(false);

                loadingCanvas.worldCamera = mainXRRig.GetComponentInChildren<Camera>();

                await UniTask.WaitForSeconds(0.5f);

                await ToggleLoadingPanel(false);
                Debug.Log($"<color=white>LoadScene finish</color>");
            }
            else
            {
                await SceneManager.LoadSceneAsync(name);
            }
        }

        private async UniTask ToggleLoadingPanel(bool isOn)
        {
            if (isOn) loadingCanvas.gameObject.SetActive(true);

            float timeToFade = 1f;

            float timer = 0, t = 0;

            while (t < 1)
            {
                timer += Time.deltaTime;

                t = timer / timeToFade;

                if (isOn)
                    loadingCanvasGroup.alpha = t;
                else
                    loadingCanvasGroup.alpha = 1 - t;

                await UniTask.Yield();
            }

            if (!isOn) loadingCanvas.gameObject.SetActive(false);
        }
    }
}
