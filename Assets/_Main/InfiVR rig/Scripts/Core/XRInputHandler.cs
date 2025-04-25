using Cysharp.Threading.Tasks;
using InfiVR;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class XRInputHandler : MonoBehaviour
{
    [SerializeField] UnityEngine.XR.Interaction.Toolkit.XRController leftController;
    [SerializeField] UnityEngine.XR.Interaction.Toolkit.XRController rightController;
    [SerializeField] GameObject teleportInterator;

    public event Action chatButtonPerform;
    public event Action chatButtonCancel;

    private bool previousPress;

    private void FixedUpdate()
    {
        if (leftController.inputDevice.IsPressed(InputHelpers.Button.Primary2DAxisClick, out bool leftPressed))
        {
            if (leftPressed)
            {
                teleportInterator.SetActive(true);
            }
            else
            {
                TurnOffLate().Forget();
            }
        }

        if (rightController.inputDevice.IsPressed(InputHelpers.Button.Primary2DAxisClick, out bool rightPressed))
        {
            if (previousPress != rightPressed)
            {
                if (rightPressed)
                {
                    chatButtonPerform?.Invoke();
                }
                else
                {
                    chatButtonCancel?.Invoke();
                }

                previousPress = rightPressed;
            }
        }
    }

    private async UniTask TurnOffLate()
    {
        await UniTask.WaitForEndOfFrame(this);

        teleportInterator.SetActive(false);
    }
}
