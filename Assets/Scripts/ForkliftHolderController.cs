using UnityEngine;
using static Logitech.LogitechGSDK;

public class ForkliftHolderController : MonoBehaviour
{
    [Header("References")]
    public Transform holder;
    public Transform mast;
    public Transform lowerPoint;
    public Transform upperPoint;
    public Transform loadAttachPoint;
    public G29VehicleInput vehicle; // optional — if assigned, read mast/tilt from it

    // these are the values used by other systems (kept as settable props)
    public float MastInput { get; set; }    // -1 = down, +1 = up
    public float tiltInput { get; set; }    // -1 = tilt down, +1 = tilt up

    [Header("Load")]
    public float currentLoadKg = 0f;
    public float maxLoadKg = 2000f;

    [Header("Lift")]
    public float emptyLiftSpeed = 1.0f;
    public float heavyLiftSpeed = 0.35f;

    [Header("Tilt")]
    public float tiltSpeed = 40f;
    public float minTiltZ = -15f;
    public float maxTiltZ = 15f;

    [Header("Thresholds (HYSTERESIS)")]
    public float attachLiftT = 0.05f;   // must lift ABOVE this
    public float detachLiftT = 0.02f;   // must lower BELOW this

    [Header("Input")]
    [Range(0f, 0.2f)] public float deadZone = 0.08f;

    float liftT;
    float currentTiltZ;

    Vector3 localLowerPos;
    Vector3 localUpperPos;

    ForkliftLoad attachedLoad;

    enum LoadState
    {
        None,
        Attached,
        Released
    }

    LoadState loadState = LoadState.None;

    void Awake()
    {
        localLowerPos = holder.parent.InverseTransformPoint(lowerPoint.position);
        localUpperPos = holder.parent.InverseTransformPoint(upperPoint.position);

        liftT = Mathf.InverseLerp(
            localLowerPos.y,
            localUpperPos.y,
            holder.localPosition.y
        );

        currentTiltZ = holder.localEulerAngles.z;
        if (currentTiltZ > 180f) currentTiltZ -= 360f;
    }

    void FixedUpdate()
    {
        // keep the original LogiUpdate call for compatibility (it won't hurt if vehicle is present)
        if (!LogiUpdate()) return;

        UpdateLift();
        UpdateTilt();
        UpdateLoadState();
    }

    // ================= LIFT =================
    void UpdateLift()
    {
        float input = 0f;

        if (vehicle != null)
        {
            // read mast input routed through vehicle (this allows gating)
            input = vehicle.MastRawInput;
        }
        else
        {
            // fallback to original direct Logitech read
            var s = LogiGetStateUnity(0);
            if (s.rgbButtons[7] == 128) input = 1f;
            else if (s.rgbButtons[6] == 128) input = -1f;
            else input = 0f;
        }

        // apply deadzone
        if (Mathf.Abs(input) < deadZone) input = 0f;

        MastInput = input; // exposed value for other systems

        float loadRatio = Mathf.Clamp01(currentLoadKg / maxLoadKg);
        float speed = Mathf.Lerp(emptyLiftSpeed, heavyLiftSpeed, loadRatio);

        if (Mathf.Abs(input) > 0.001f)
            liftT += input * speed * Time.fixedDeltaTime;

        liftT = Mathf.Clamp01(liftT);

        Vector3 pos = holder.localPosition;
        pos.y = Mathf.Lerp(localLowerPos.y, localUpperPos.y, liftT);
        holder.localPosition = pos;
    }

    // ================= TILT (TRUE HOLD) =================
    void UpdateTilt()
    {
        float input = 0f;

        if (vehicle != null)
        {
            // read tilt input routed through vehicle (this allows gating)
            input = vehicle.TiltRawInput;
        }
        else
        {
            var s = LogiGetStateUnity(0);
            if (s.rgbButtons[4] == 128) input = -1f;
            else if (s.rgbButtons[5] == 128) input = 1f;
            else input = 0f;
        }

        // apply deadzone
        if (Mathf.Abs(input) < deadZone) input = 0f;

        tiltInput = input; // exposed value for other systems

        if (Mathf.Abs(input) < 0.001f)
        {
            holder.localRotation = Quaternion.Euler(0f, 0f, currentTiltZ);
            return;
        }

        currentTiltZ += input * tiltSpeed * Time.fixedDeltaTime;
        currentTiltZ = Mathf.Clamp(currentTiltZ, minTiltZ, maxTiltZ);
        holder.localRotation = Quaternion.Euler(0f, 0f, currentTiltZ);
    }

    // ================= LOAD STATE MACHINE =================
    void UpdateLoadState()
    {
        switch (loadState)
        {
            case LoadState.None:
                TryAttach();
                break;

            case LoadState.Attached:
                TryDetach();
                break;

            case LoadState.Released:
                // must lift again before reattach allowed
                if (liftT > attachLiftT)
                    loadState = LoadState.None;
                break;
        }
    }

     public void TryAttach()
    {
        if (loadState != LoadState.None) return;
        if (liftT < attachLiftT) return;

        Collider[] hits = Physics.OverlapBox(
            loadAttachPoint.position,
            new Vector3(0.6f, 0.2f, 0.6f),
            loadAttachPoint.rotation
        );

        foreach (var hit in hits)
        {
            ForkliftLoad load = hit.GetComponent<ForkliftLoad>();
            if (load == null) continue;

            if (load.IsAttached())
            {
                continue;
            }

            attachedLoad = load;
            currentLoadKg = load.weightKg;
            load.AttachToFork(loadAttachPoint);

            loadState = LoadState.Attached;
            vehicle?.GetType(); // no-op to avoid analysis warnings if vehicle null
            if (vehicle != null) vehicle.currentLoadKg = load.weightKg;
            break;
        }
    }

   public void TryDetach()
    {
        if (attachedLoad == null) return;

        if (liftT <= detachLiftT || Mathf.Abs(currentTiltZ) >= maxTiltZ * 0.98f)
        {
            attachedLoad.DetachFromFork();
            attachedLoad = null;
            currentLoadKg = 0f;

            loadState = LoadState.Released;
        }
    }

    // ================= EVALUATOR HELPERS =================
    public float CurrentForkHeight =>
        holder.position.y - lowerPoint.position.y;

    public float CurrentMastTilt => currentTiltZ;

    public bool ForksAreUnderLoad()
    {
        Collider[] hits = Physics.OverlapBox(
            loadAttachPoint.position,
            new Vector3(0.6f, 0.2f, 0.6f),
            loadAttachPoint.rotation
        );

        foreach (var hit in hits)
            if (hit.GetComponent<ForkliftLoad>() != null)
                return true;

        return false;
    }
}