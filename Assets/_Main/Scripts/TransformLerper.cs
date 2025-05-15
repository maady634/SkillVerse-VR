using System.Threading.Tasks;
using UnityEngine;

public class TransformLerper : MonoBehaviour
{
    [SerializeField] private GameObject targetObject;

    public async void MoveTargetTo(Vector3 targetPosition, Quaternion targetRotation, float duration)
    {
        if (targetObject == null)
        {
            Debug.LogWarning("Target GameObject is not assigned.");
            return;
        }

        Transform targetTransform = targetObject.transform;
        Vector3 startPos = targetTransform.position;
        Quaternion startRot = targetTransform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            targetTransform.position = Vector3.Lerp(startPos, targetPosition, t);
            targetTransform.rotation = Quaternion.Slerp(startRot, targetRotation, t);

            await Task.Yield(); // wait until the next frame
        }

        // Snap to exact final values
        targetTransform.position = targetPosition;
        targetTransform.rotation = targetRotation;
    }
}
