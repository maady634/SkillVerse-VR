using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreCard : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI TrainingScoreText;

    [SerializeField]
    TextMeshProUGUI AssessmentScoreText;

    [SerializeField]
    Slider TrainingSlider;

    [SerializeField]
    Slider AssessmentSlider;

    [SerializeField]
    Button RetryButton;
    [Header("Follow Settings")]
    public float distanceFromCamera = 2.0f;
    public float heightOffset = -0.2f;
    public float followSpeed = 3f;
    public float rotationSpeed = 5f;

    private Transform cam;
    private Vector3 targetPosition;
    private void Start()
    {
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
    private void OnEnable()
    {
        int assScore = StageManager.Instance.AssessmentScore;
        AssessmentScoreText.text = assScore.ToString() + " %";

        // Invert value since slider visually behaves backwards
        AssessmentSlider.value = 1f - (assScore / 100f);

        int trainScore = StageManager.Instance.TrainingScore;
        TrainingScoreText.text = trainScore.ToString() + " %";

        // Invert value since slider visually behaves backwards
        TrainingSlider.value = 1f - (trainScore / 110f);

        RetryButton.onClick.AddListener(StageManager.Instance.ReloadScene);
    }
}
