using InfiVR;
using System.Collections.Generic;
using UnityEngine;

public class InventoryObject : MonoBehaviour
{
    [SerializeField] bool leftHand;
    [SerializeField] bool rightHand;
    [Tooltip("All visible children")]
    [SerializeField] List<GameObject> items = new();

    List<Vector3> initialPosition = new();
    List<Quaternion> initialRotation = new();

    Transform parent;

    bool isInitialize;
    public int id = -1;

    public InfiVRRigManager rigManager;
    public InfrontMenu infrontMenu;
    public InventoryManager inventoryManager;

    private void Start()
    {
        Initialize();
    }

    public virtual void OnInventoryObjectSelected()
    {
        if (leftHand)
        {
            //HapticController.Instance.PlayHaptics(rigManager.LeftHand, 0.1f, 0.5f);
            rigManager.LeftHandPoseController.LockGrabPose();
        }
        else if (rightHand)
        {
            //HapticController.Instance.PlayHaptics(rigManager.RightHand, 0.1f, 0.5f);
            rigManager.RightHandPoseController.LockGrabPose();
        }
    }

    [ContextMenu("Send back")]
    public void SendBackToInventory()
    {
        //Debug.Log($"<color=white>Hiding object --------------</color>", gameObject);

        if (!gameObject.activeInHierarchy) return;

        if (!isInitialize)
        {
            Initialize();
        }

        transform.SetParent(parent);

        for (int i = 0; i < items.Count; i++)
        {
            items[i].transform.SetLocalPositionAndRotation(initialPosition[i], initialRotation[i]);
        }

        gameObject.SetActive(false);

        inventoryManager.OnObjectBackToInventory(id);
    }

    private void Initialize()
    {
        parent = transform.parent;

        for (int i = 0; i < items.Count; i++)
        {
            initialPosition.Add(items[i].transform.localPosition);
            initialRotation.Add(items[i].transform.localRotation);
        }

        isInitialize = true;
    }
}
