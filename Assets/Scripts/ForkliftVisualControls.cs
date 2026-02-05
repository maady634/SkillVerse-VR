using UnityEngine;

public class ForkliftVisualControls : MonoBehaviour
{
    [Header("Vehicle")]
    public G29VehicleInput vehicle;

    [Header("Fork Controller (SOURCE OF TRUTH)")]
    public ForkliftHolderController forkController;

    // ================= STEERING =================
    [Header("Steering Wheel")]
    public Transform steeringWheel;
    public float maxSteeringRotation = 540f;
    public Vector3 steeringAxis = Vector3.forward;

    Quaternion steeringInitialRot;

    // ================= LIFT LEVER =================
    [Header("Lift Lever")]
    public Transform liftLever;
    public Vector3 liftLeverAxis = Vector3.right;
    public float liftLeverMaxAngle = 20f;

    Quaternion liftInitialRot;
    float liftVisual;

    // ================= TILT LEVER =================
    [Header("Tilt Lever")]
    public Transform tiltLever;
    public Vector3 tiltLeverAxis = Vector3.right;
    public float tiltLeverMaxAngle = 20f;

    Quaternion tiltInitialRot;
    float tiltVisual;

    // ================= GEAR LEVER =================
    [Header("Gear Lever (INSTANT SNAP)")]
    public Transform gearLever;
    public Vector3 gearLeverAxis = Vector3.forward;

    [Tooltip("Index = GearState + 1  (-1,0,1 => 0,1,2)")]
    public float[] gearAngles = new float[3] { -25f, 0f, 25f };

    Quaternion gearInitialRot;
    int lastGearState = int.MinValue;

    // ================= START =================
    void Start()
    {
        if (steeringWheel) steeringInitialRot = steeringWheel.localRotation;
        if (liftLever) liftInitialRot = liftLever.localRotation;
        if (tiltLever) tiltInitialRot = tiltLever.localRotation;

        if (gearLever)
            gearInitialRot = gearLever.localRotation;
    }

    void Update()
    {
        if (vehicle == null || forkController == null)
            return;

        UpdateSteeringWheel();
        UpdateLiftLever();
        UpdateTiltLever();
        UpdateGearLever();   // ✅ SNAP LOGIC
    }

    // ================= STEERING =================
    void UpdateSteeringWheel()
    {
        if (!steeringWheel) return;

        float steer = vehicle.SteeringValue;
        float angle = steer * maxSteeringRotation;

        steeringWheel.localRotation =
            steeringInitialRot *
            Quaternion.AngleAxis(angle, steeringAxis.normalized);
    }

    // ================= LIFT LEVER =================
    void UpdateLiftLever()
    {
        if (!liftLever) return;

        liftVisual = Mathf.Lerp(
            liftVisual,
            forkController.MastInput,
            Time.deltaTime * 8f);

        liftLever.localRotation =
            liftInitialRot *
            Quaternion.AngleAxis(
                liftVisual * liftLeverMaxAngle,
                liftLeverAxis.normalized);
    }

    // ================= TILT LEVER =================
    void UpdateTiltLever()
    {
        if (!tiltLever) return;

        tiltVisual = Mathf.Lerp(
            tiltVisual,
            forkController.tiltInput,
            Time.deltaTime * 8f);

        tiltLever.localRotation =
            tiltInitialRot *
            Quaternion.AngleAxis(
                tiltVisual * tiltLeverMaxAngle,
                tiltLeverAxis.normalized);
    }

    // ================= GEAR LEVER (INSTANT SNAP) =================
    void UpdateGearLever()
    {
        if (!gearLever || gearAngles == null || gearAngles.Length < 3)
            return;

        int gear = vehicle.GearState;
        if (gear == lastGearState) return;   // no change → no work

        lastGearState = gear;

        int index = gear + 1; // -1→0, 0→1, 1→2
        index = Mathf.Clamp(index, 0, gearAngles.Length - 1);

        float angle = gearAngles[index];

        // 🔥 INSTANT SNAP (NO LERP)
        gearLever.localRotation =
            gearInitialRot *
            Quaternion.AngleAxis(angle, gearLeverAxis.normalized);
    }
}
