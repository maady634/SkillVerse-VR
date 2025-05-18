using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QAPanel : MonoBehaviour
{
    [SerializeField]
    public Toggle Crtoption;

    [SerializeField] private Toggle OptionA;
    [SerializeField] private Toggle OptionB;
    [SerializeField] private Toggle OptionC;

    [SerializeField] private ToggleGroup toggleGroup;

    public Button SubmitButton;

    public void Start()
    {
        if (SubmitButton != null)
        {
            SubmitButton.onClick.AddListener(AnswerSubmitButton);
        }
    }

    public void AnswerSubmitButton()
    {
        SubmitButton.interactable = false;

        if (Crtoption == OptionA)
        {
            OptionA.targetGraphic.color = Color.green;
            OptionB.targetGraphic.color = Color.red;
            OptionC.targetGraphic.color = Color.red;
        }
        else if(Crtoption == OptionB)
        {
            OptionB.targetGraphic.color = Color.green;
            OptionA.targetGraphic.color = Color.red;
            OptionC.targetGraphic.color = Color.red;
        }
        else
        {
            OptionC.targetGraphic.color = Color.green;
            OptionB.targetGraphic.color = Color.red;
            OptionA.targetGraphic.color = Color.red;
        }
        
        Toggle selectedToggle = toggleGroup.GetFirstActiveToggle();

        if (selectedToggle == Crtoption)
        {
            StageManager.Instance.AssessmentScore += 20;
        } 

        Invoke("CompleteStep", 5);
    }

    public void CompleteStep()
    {
        StageManager.Instance.StepCompleted();
    }
}
