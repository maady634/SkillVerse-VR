using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanel : MonoBehaviour
{
    public Button NextButton;
    [Header("Follow Settings")]
    private float distanceFromCamera = 2.3f;
    private float heightOffset = 0.02f;
    private float followSpeed = 2f;
    private float rotationSpeed = 5f;

    private Transform cam;
    private Vector3 targetPosition;
    public void Start()
    {
        if(NextButton != null)
        {
            NextButton.onClick.AddListener(NextStepButton);
        }
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

    public void NextStepButton()
    {
        StageManager.Instance.StepCompleted();
        NextButton.interactable = false;
    }
}
