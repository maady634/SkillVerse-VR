using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanel : MonoBehaviour
{
    public Button NextButton;

    public void Start()
    {
        if(NextButton != null)
        {
            NextButton.onClick.AddListener(NextStepButton);
        }
    }

    public void NextStepButton()
    {
        StageManager.Instance.StepCompleted();
        NextButton.interactable = false;
    }
}
