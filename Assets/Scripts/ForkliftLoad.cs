using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ForkliftLoad : MonoBehaviour
{
    public float weightKg = 500f;

    Rigidbody rb;
    bool isAttached;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        isAttached = false;
    }
    private void Update()
    {
        if (isAttached)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }
    public void AttachToFork(Transform attachPoint)
    {
        if (isAttached) return;

        isAttached = true;

        rb.isKinematic = true;
        rb.detectCollisions = false;

        transform.SetParent(attachPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Debug.Log($"[LOAD] Attached: {name}");
    }

    public void DetachFromFork()
    {
        if (!isAttached) return;

        isAttached = false;

        transform.SetParent(null);

        rb.isKinematic = false;
        rb.detectCollisions = true;

        Debug.Log($"[LOAD] Detached: {name}");
    }

    public bool IsAttached()
    {
        return isAttached;
    }
}
