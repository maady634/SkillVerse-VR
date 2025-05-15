using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

    public class InfiVRRigManager : MonoBehaviour
    {
        [Header("Teleportation")]
        [SerializeField] public TeleportationProvider teleportationProvider;
        [SerializeField] Image cameraFadeImage;
        [Tooltip("1/3 fade in, another 1/3 full black, and last 1/3 fade out")]
        [SerializeField] float totalTeleportTime = 3.9f;

        [Header("Hands")]
        [SerializeField] XRBaseController leftHandcontroller;
        [SerializeField] XRBaseController rightHandcontroller;
        [SerializeField] SkinnedMeshRenderer leftHandMesh;
        [SerializeField] SkinnedMeshRenderer rightHandMesh;
        [SerializeField] Material defaultMat;
        [SerializeField] Material abrationMat;
        [SerializeField] Material chemicalMat;
        [SerializeField] Material heatMat;
        [SerializeField] Material highVoltageMat;
        [SerializeField] bool useModuleConfigForHandGlove;

        [Header("UI panel")]
        [SerializeField] public bool followTeleport;
        [SerializeField] public float distanceFromCamera = 2.1f;
        [SerializeField] public float hightOffsetFromCamera = 0.2f;
        [SerializeField] public float minimumUIHeight = 1.5f;

        protected Transform mainCamera;

        Color black = Color.black;
        Color transperent = Color.clear;

        HandPoseController leftHandPoseController;
        HandPoseController rightHandPoseController;

        public XRBaseController LeftHand
        {
            get => leftHandcontroller;
        }
        public XRBaseController RightHand
        {
            get => rightHandcontroller;
        }

        public HandPoseController LeftHandPoseController
        {
            get
            {
                if (leftHandPoseController == null)
                {
                    leftHandPoseController = LeftHand.GetComponent<HandPoseController>();
                }

                return leftHandPoseController;
            }
        }
        public HandPoseController RightHandPoseController
        {
            get
            {
                if (rightHandPoseController == null)
                {
                    rightHandPoseController = RightHand.GetComponent<HandPoseController>();
                }

                return rightHandPoseController;
            }
        }

        private void Start()
        {
            mainCamera = Camera.main.transform;

            if (teleportationProvider != null)
            {
                teleportationProvider.endLocomotion += OnUserTeleport;
            }

            /*try
            {
                Bridge.TeleportTo += OnStepRequestTeleport;
            }
            catch { }

            if (useModuleConfigForHandGlove && ModuleData.Instance != null)
            {
                if (ModuleData.Instance.SelectedModuleConfig.ppeConfig.abrationResistentGloves)
                {
                    SetHand_AbrationResistance();
                }
                else if (ModuleData.Instance.SelectedModuleConfig.ppeConfig.chemicalResistentGlove)
                {
                    SetHand_ChemicalResistance();
                }
                else if (ModuleData.Instance.SelectedModuleConfig.ppeConfig.heatResistentGloves)
                {
                    SetHand_HeatResistance();
                }
                else if (ModuleData.Instance.SelectedModuleConfig.ppeConfig.highVisibilityVest)
                {
                    SetHand_HighVoltageResistance();
                }
            }*/
        }

        public virtual void OnUserTeleport(LocomotionSystem system)
        {
            if (!followTeleport) return;

            Transform targetPanel = null;

            //var found = FindObjectOfType<UIPanel>();

            /*if (found != null && found.FollowCamera)
            {
                targetPanel = found.transform;
                UpdatePanelPosition(targetPanel);
            }*/
        }

        public void UpdatePanelPosition(Transform targetPanel)
        {
            if (targetPanel != null && mainCamera != null)
            {
                Vector3 newPosition = mainCamera.position + mainCamera.forward * distanceFromCamera;
                float cameraRelativeHeight = mainCamera.position.y + hightOffsetFromCamera;
                newPosition.y = cameraRelativeHeight < minimumUIHeight ? minimumUIHeight : cameraRelativeHeight;

                targetPanel.position = newPosition;

                targetPanel.LookAt(mainCamera);
                targetPanel.rotation = Quaternion.Euler(0, targetPanel.eulerAngles.y, 0);
            }
        }

        void OnStepRequestTeleport(Transform newTransform, Action onFullBlack = null, Action onTeleportComplete = null)
        {
            ProcessTeleportRequest(newTransform, 2f, onFullBlack, onTeleportComplete).Forget();
        }

        async UniTask ProcessTeleportRequest(Transform transform, float time, Action onFullBlack = null, Action onTeleportComplete = null)
        {
            float timeToFade = totalTeleportTime/3;

            float timer = 0, t = 0;

            // Camera fade-in
            while (t < 1)
            {
                timer += Time.deltaTime;

                t = timer / timeToFade;

                cameraFadeImage.color = Color.Lerp(transperent, black, t);

                await UniTask.Yield();
            }
            //------------------------------

            onFullBlack?.Invoke();

            TeleportRequest teleportRequest = new()
            {
                destinationPosition = transform.position,
                destinationRotation = transform.rotation,
                matchOrientation = MatchOrientation.TargetUpAndForward
            };
            teleportationProvider.QueueTeleportRequest(teleportRequest);

            await UniTask.WaitForSeconds(timeToFade);

            timer = 0;
            t = 0;

            // Camera fade-out
            while (t < 1)
            {
                timer += Time.deltaTime;

                t = timer / timeToFade;

                cameraFadeImage.color = Color.Lerp(black, transperent, t);

                await UniTask.Yield();
            }
            //------------------------------

            onTeleportComplete?.Invoke();
        }



        void SetGlovesMaterial(Material mat)
        {
            leftHandMesh.material = mat;
            rightHandMesh.material = mat;
        }

        public void SetHandDefaultMaterial()
        {
            SetGlovesMaterial(defaultMat);
        }

        public void SetHand_AbrationResistance()
        {
            SetGlovesMaterial(abrationMat);
        }

        public void SetHand_ChemicalResistance()
        {
            SetGlovesMaterial(chemicalMat);
        }

        public void SetHand_HeatResistance()
        {
            SetGlovesMaterial(heatMat);
        }

        public void SetHand_HighVoltageResistance()
        {
            SetGlovesMaterial(highVoltageMat);
        }
    }