using UnityEngine;

public class HorizontalFollow : MonoBehaviour
{
    [Tooltip("Adjust this value to control the position follow smoothness")]
    [SerializeField] float positionSmoothness = 0.1f;
    [Tooltip("Adjust this value to control the rotation follow smoothness")]
    [SerializeField] float rotationSmoothness = 0.1f;
    [Tooltip("Depth offset from the camera's position")]
    [SerializeField] float offsetZ = 0.0f;
    [Tooltip("Offset for the y-axis rotation")]
    [SerializeField] float rotationOffsetY = 0.0f;
    [SerializeField] public bool isActive = false;

    [Space]
    [SerializeField] Transform targetTransform;

    private void Update()
    {
        if (!isActive) return;

        // If there's no target assigned, don't attempt to follow
        if (targetTransform == null) return;
        //{
        //    targetCamera = Camera.main.transform;
        //}

        // Calculate total desired rotation (camera's rotation + offset rotation)
        float desiredYRotation = targetTransform.eulerAngles.y + rotationOffsetY;

        // Calculate desired position based on rotation and initial offset
        Vector3 rotatedOffset = Quaternion.Euler(0, desiredYRotation, 0) * Vector3.forward * offsetZ;
        Vector3 desiredPosition = targetTransform.position + rotatedOffset;

        // Lerp to smoothly move towards the desired position
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothness);

        // Make the object face the camera
        transform.LookAt(targetTransform.position);
    }
}
