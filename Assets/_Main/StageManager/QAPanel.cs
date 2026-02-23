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
    [Header("Follow Settings")]
    public float distanceFromCamera = 2.0f;
    public float heightOffset = -0.2f;
    public float followSpeed = 3f;
    public float rotationSpeed = 5f;

    private Transform cam;
    private Vector3 targetPosition;
    public void Start()
    {
        if (SubmitButton != null)
        {
            SubmitButton.onClick.AddListener(AnswerSubmitButton);
        }// Get main camera safely
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            SpawnInFrontOfCamera();
        }
        else
        {
            Debug.LogWarning("UIPanel: No Main Camera found.");
        }
    }
    void Update()
    {
        if (cam == null) return;

        FollowCameraSmooth();
    }
    void SpawnInFrontOfCamera()
    {
        targetPosition = cam.position + cam.forward * distanceFromCamera;
        targetPosition.y += heightOffset;

        transform.position = targetPosition;

        // Face the camera
        transform.LookAt(cam);
        transform.forward = -cam.forward;
    }

    void FollowCameraSmooth()
    {
        // Desired position in front of camera
        targetPosition = cam.position + cam.forward * distanceFromCamera;
        targetPosition.y += heightOffset;

        // Smooth position
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * followSpeed
        );

        // Smooth rotation
        Quaternion targetRotation = Quaternion.LookRotation(transform.position - cam.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
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

        if (selectedToggle !=Crtoption )
        {
            StageManager.Instance.AssessmentScore -= 50;
        } 

        Invoke("CompleteStep", 5);
    }

    public void CompleteStep()
    {
        StageManager.Instance.StepCompleted();
    }
}
