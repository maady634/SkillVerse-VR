using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class HandPoseController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private ActionBasedController controller;

    public bool poseLock;


    private void OnEnable()
    {
        controller = GetComponent<ActionBasedController>();

        controller.selectAction.action.performed += OnGrab;
        controller.selectAction.action.canceled += OnRelease;
    }

    private void OnDisable()
    {
        controller.selectAction.action.performed -= OnGrab;
        controller.selectAction.action.canceled -= OnRelease;
    }

    private void OnGrab(InputAction.CallbackContext context)
    {
        if(!poseLock)
        {
            animator.Play("grab");
        }
    }

    private void OnRelease(InputAction.CallbackContext context)
    {
        if (!poseLock)
        {
            animator.Play("release");
        }
    }

    public void LockGrabPose()
    {
        poseLock = true;

        animator.Play("grab");
    }

    public void UnlockGrabPose()
    {
        poseLock = false;

        animator.Play("release");
    }
}
