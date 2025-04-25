using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

    public class InfrontMenu : MonoBehaviour
    {
        [Header("General menu")]
        [SerializeField] bool isGeneralMenuActive = true;
        [SerializeField] GameObject menuPanel;
        [SerializeField] Button continueButton;
        [SerializeField] Button lobbyButton;

        [Header("Inventory menu")]
        [SerializeField] bool isInventoryMenuActive = true;
        [SerializeField] GameObject inventoryPanel;
        [SerializeField] Button closeInventoryButton;

        [Header("Message")]
        [SerializeField] GameObject correctMsg;
        [SerializeField] GameObject incorrectMsg;

        [Header("Controller")]
        [SerializeField] InputActionReference inventoryInputReference;
        [SerializeField] InputActionReference menuInputReference;

        protected void Awake()
        {
            continueButton.onClick.AddListener(OnContinueButtonClick);
            lobbyButton.onClick.AddListener(OnLobbyButtonClick);
            closeInventoryButton.onClick.AddListener(OnInventoryCloseButtonClick);

            /*Bridge.OnQuizCorrectSubmit += () =>
            {
                ShowTickMark().Forget();
            };

            Bridge.OnQuizIncorrectSubmit += () =>
            {
                ShowIncorrectMark().Forget();
            };*/
        }

    public AudioHandler thisAudio;
        private void OnEnable()
        {
        thisAudio = FindAnyObjectByType<AudioHandler>();

        inventoryInputReference.action.performed += ToggleInventory;
            menuInputReference.action.performed += ToggleGeneralMenu;
        }

        private void OnDisable()
        {
            inventoryInputReference.action.performed -= ToggleInventory;
            menuInputReference.action.performed -= ToggleGeneralMenu;
        }



    



    #region PUBLIC

    public async UniTask ShowTickMark()
        {
            correctMsg.SetActive(true);
            thisAudio.PlayTickMarkAudio();

            await UniTask.WaitForSeconds(2);

            correctMsg.SetActive(false);
        }

        public async UniTask ShowIncorrectMark()
        {
            incorrectMsg.SetActive(true);
            thisAudio.PlayIncorrectMarkAudio();

            await UniTask.WaitForSeconds(2);

            incorrectMsg.SetActive(false);
        }

        #endregion


        #region INVENTORY PANEL

        private void ToggleInventory(InputAction.CallbackContext context)
        {
            if (!isInventoryMenuActive) return;

            /*if (InventoryManager.Instance.IsInventoryObjectSelected)
            {
                InventoryManager.Instance.SetInventoryNotUsed();
            }
            else
            {*/
                menuPanel.SetActive(false);

                inventoryPanel.SetActive(!inventoryPanel.activeInHierarchy);
            //}
        }

        public void OnInventoryCloseButtonClick()
        {
            inventoryPanel.SetActive(false);
        }

        #endregion


        #region MENU PANEL

        private void ToggleGeneralMenu(InputAction.CallbackContext context)
        {
            if (!isGeneralMenuActive) return;

            inventoryPanel.SetActive(false);

            menuPanel.SetActive(!menuPanel.activeInHierarchy);
        }

        private void OnLobbyButtonClick()
        {
            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                menuPanel.SetActive(false);
                return;
            }

            /*ModuleData.Instance.skipLogin = true;

            Bridge.StopStepSystem();

            SceneHandler.Instance.LoadSceneByName(SceneHandler.LOBBY_SCENE_NAME);*/
        }

        private void OnContinueButtonClick()
        {
            menuPanel.SetActive(false);
        }

        #endregion
    }

