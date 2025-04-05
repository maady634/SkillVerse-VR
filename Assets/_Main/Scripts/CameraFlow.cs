using UnityEngine;

public class CameraFlow : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;
    public float zoomSpeed = 10f;

    private void Update()
    {
        // Movement
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        float upDownInput = Input.GetAxis("Mouse ScrollWheel");

        Vector3 moveDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);

        // Up and Down Movement
        if (upDownInput != 0)
        {
            transform.Translate(Vector3.up * upDownInput * zoomSpeed * Time.deltaTime);
        }

        // Rotation
        if (Input.GetMouseButton(1)) // Right Mouse Button
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            transform.Rotate(Vector3.up * mouseX * mouseSensitivity);
            transform.Rotate(Vector3.left * mouseY * mouseSensitivity, Space.Self);
        }
    }
}