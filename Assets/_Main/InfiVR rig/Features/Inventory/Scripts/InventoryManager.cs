using InfiVR;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    [SerializeField] bool isInventoryObjectSelected = false;
    [SerializeField] List<InventoryObject> inventoryObjects = new();
    [SerializeField] List<Button> inventoryButtons = new();

    InventoryObject selectedObj;
    int selectedObjIndex = -1;

    public InfiVRRigManager rigManager;
    public InfrontMenu infrontMenu;
    public bool IsInventoryObjectSelected
    {
        get => isInventoryObjectSelected;
    }


    private void Start()
    {
        for (int i = 0; i < inventoryObjects.Count; i++)
        {
            int index = i;

            inventoryObjects[index].id = index;
            inventoryButtons[index].onClick.AddListener(() => OnButtonClick(index));
        }
    }

    private void OnButtonClick(int index)
    {
        SetInventoryUsed();

        if (selectedObj != null)
        {
            selectedObj.SendBackToInventory();
        }

        inventoryObjects[index].gameObject.SetActive(true);
        inventoryObjects[index].OnInventoryObjectSelected();

        selectedObj = inventoryObjects[index];
        selectedObjIndex = index;
    }

    public void SetInventoryUsed()
    {
        // This line of code might be required for HTC vive.
        // HTC vive button is default assigned to simulate 
        // selected UI button press.
        //EventSystem.current.SetSelectedGameObject(null);

        infrontMenu.OnInventoryCloseButtonClick();

        isInventoryObjectSelected = true;
    }

    public void SetInventoryNotUsed()
    {
        rigManager.RightHandPoseController.UnlockGrabPose();
        rigManager.LeftHandPoseController.UnlockGrabPose();

        if (selectedObj != null)
        {
            selectedObj.SendBackToInventory();
        }

        isInventoryObjectSelected = false;
    }


    [ContextMenu("Detach form hand")]
    public void DetachObjectFromHand()
    {
        rigManager.RightHandPoseController.UnlockGrabPose();
        rigManager.LeftHandPoseController.UnlockGrabPose();

        selectedObj.transform.SetParent(null, true);

        isInventoryObjectSelected = false;

        inventoryButtons[selectedObjIndex].gameObject.SetActive(false);

        selectedObj = null;
        selectedObjIndex = -1;
    }

    public void OnObjectBackToInventory(int id)
    {
        inventoryButtons[id].gameObject.SetActive(true);
    }
}
