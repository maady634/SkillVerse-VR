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
    public G29VehicleInput vehicle;

    public float MastInput { get; private set; }
    public float tiltInput { get; private set; }

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
    public float detachLiftT = 0.01f;   // must lower BELOW this

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
        if (!LogiUpdate()) return;
        //debug.Log(
        // $"[//debug] state={loadState} liftT={liftT:F3} mastInput={MastInput} underLoad={ForksAreUnderLoad()}"


        UpdateLift();
        UpdateTilt();
        UpdateLoadState();
    }

    // ================= LIFT =================
    void UpdateLift()
    {
        var s = LogiGetStateUnity(0);

        float input = 0f;
        if (s.rgbButtons[7] == 128) input = 1f;
        else if (s.rgbButtons[6] == 128) input = -1f;

        MastInput = input;

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
        var s = LogiGetStateUnity(0);

        float input = 0f;
        if (s.rgbButtons[4] == 128) input = -1f;
        else if (s.rgbButtons[5] == 128) input = 1f;

        tiltInput = input;
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

    void TryAttach()
    {
        if (loadState != LoadState.None) return;
        if (liftT < attachLiftT) return;

        Collider[] hits = Physics.OverlapBox(
            loadAttachPoint.position,
            new Vector3(0.6f, 0.2f, 0.6f),
            loadAttachPoint.rotation
        );

       // //debug.Log($"[ATTACH CHECK] liftT={liftT:F3}, hits={hits.Length}");

        foreach (var hit in hits)
        {
            ForkliftLoad load = hit.GetComponent<ForkliftLoad>();
            if (load == null) continue;

            //debug.Log("[ATTACH] ForkliftLoad detected");

            if (load.IsAttached())
            {
                //debug.Log("[ATTACH] Load already attached, skipping");
                continue;
            }

            attachedLoad = load;
            currentLoadKg = load.weightKg;
            load.AttachToFork(loadAttachPoint);

            loadState = LoadState.Attached;

            //debug.Log("[ATTACH SUCCESS] Load attached");
            vehicle.currentLoadKg = load.weightKg;
            break;
        }
    }


    void TryDetach()
    {
        if (liftT <= detachLiftT ||
            Mathf.Abs(currentTiltZ) >= maxTiltZ * 0.98f)
        {
            attachedLoad.DetachFromFork();
            attachedLoad = null;
            currentLoadKg = 0f;

            loadState = LoadState.Released;
            //debug.Log("[FORKLIFT] LOAD DETACHED");
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
