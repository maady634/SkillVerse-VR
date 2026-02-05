using UnityEngine;
using static Logitech.LogitechGSDK;

public class G29VehicleInput : MonoBehaviour
{
    // ================= PUBLIC READ =================
    public float AcceleratorValue => throttleInput;
    public float BrakeValue => brakeInput;
    public float SteeringValue => steerInput;
    public int GearState => gearState;
    public float CurrentSpeed => rb.velocity.magnitude;
    public float CurrentMastHeight => mastHeight;


    // ================= TRAINING GATES =================
    bool driveEnabled = true;
    float speedLimit = -1f; // -1 = no limit

    // ================= LOAD MODEL =================
    [Header("Load Model")]
    public float currentLoadKg = 0f;
    public float maxLoadKg = 2000f;

    // ================= WHEELS =================
    [Header("Wheel Colliders")]
    public WheelCollider driveLeft;
    public WheelCollider driveRight;
    public WheelCollider steerLeft;
    public WheelCollider steerRight;

    [Header("Wheel Meshes")]
    public Transform driveLeftMesh;
    public Transform driveRightMesh;
    public Transform steerLeftMesh;
    public Transform steerRightMesh;

    // ================= MAST SOURCE =================
    [Header("Mast")]
    public Transform mastSource;
    public float maxMastHeight = 5f;

    // ================= DRIVE =================
    [Header("Drive")]
    public float motorForce = 2000f;
    public float brakeForce = 4000f;
    public float maxSteerAngle = 35f;
    public float steerSpeed = 2f;

    // ================= PHYSICS =================
    [Header("Physics")]
    public Rigidbody rb;
    public Transform baseCenterOfMass;
    public float maxCOMForwardShift = 0.45f;
    public float downForce = 4000f;

    // ================= RUNTIME =================
    float steerInput, throttleInput, brakeInput;
    int gearState;
    float targetSteer;
    float mastHeight;

    float dlRot, drRot, slRot, srRot;
    DIJOYSTATE2ENGINES state;

   


    void Start()
    {
        if (rb && baseCenterOfMass)
            rb.centerOfMass = baseCenterOfMass.localPosition;
    }

    void Update()
    {
        if (G29Manager.Instance == null || !G29Manager.Instance.Ready)
            return;

        ReadG29();

        if (mastSource != null)
            mastHeight = mastSource.localPosition.y;

    }

    void FixedUpdate()
    {
        ApplyDynamicCenterOfMass();
        ApplyRollInstability();
        ApplyDrivePhysics();
        ApplyDownforce();
        UpdateWheelVisuals();
    }

    // ================= INPUT =================
    void ReadG29()
    {
        state = G29Manager.Instance.State;

        steerInput = state.lX / 32767f;
        throttleInput = 1f - ((state.lY + 32768f) / 65535f);
        brakeInput = 1f - ((state.lRz + 32768f) / 65535f);

        bool gearFwd = state.rgbButtons[12] == 128;
        bool gearRev = state.rgbButtons[13] == 128;
        gearState = gearFwd ? 1 : gearRev ? -1 : 0;
    }

    // ================= CORE PHYSICS =================
    void ApplyDrivePhysics()
    {
        float loadRatio = Mathf.Clamp01(currentLoadKg / maxLoadKg);

        // ---------- Steering (load stiffening) ----------
        float effectiveSteerAngle =
            maxSteerAngle * Mathf.Lerp(1f, 0.55f, loadRatio);

        targetSteer = Mathf.Lerp(
            targetSteer,
            -steerInput * effectiveSteerAngle,
            steerSpeed * Time.fixedDeltaTime
        );

        steerLeft.steerAngle = targetSteer;
        steerRight.steerAngle = targetSteer;

        // ---------- Acceleration (inertia) ----------
        float inertiaMultiplier = Mathf.Lerp(1f, 0.4f, loadRatio);
        float effectiveMotorForce = motorForce * inertiaMultiplier;

        float torque = -throttleInput * effectiveMotorForce * gearState;
        driveLeft.motorTorque = torque;
        driveRight.motorTorque = torque;
        if (!driveEnabled)
        {
            // Kill forward motion ONLY
            throttleInput = 0f;
            brakeInput = Mathf.Max(brakeInput, 0.2f); // gentle hold
        }


        // ---------- Braking (mass amplification) ----------
        float effectiveBrakeForce =
            brakeForce * Mathf.Lerp(1f, 1.35f, loadRatio);

        float brakes = brakeInput * effectiveBrakeForce;
        driveLeft.brakeTorque = brakes;
        driveRight.brakeTorque = brakes;
    }

    // ================= STABILITY =================
    void ApplyDynamicCenterOfMass()
    {
        if (!rb || !baseCenterOfMass) return;

        float loadRatio = Mathf.Clamp01(currentLoadKg / maxLoadKg);
        float heightRatio = Mathf.Clamp01(mastHeight / maxMastHeight);

        float forwardShift =
            loadRatio * heightRatio * maxCOMForwardShift;

        rb.centerOfMass =
            baseCenterOfMass.localPosition +
            Vector3.forward * forwardShift;
    }

    void ApplyRollInstability()
    {
        if (currentLoadKg <= 0f) return;

        float heightRatio = Mathf.Clamp01(mastHeight / maxMastHeight);

        float lateralSpeed =
            Vector3.Dot(rb.velocity, transform.right);

        float rollTorque =
            lateralSpeed *
            currentLoadKg *
            heightRatio *
            0.012f;

        rb.AddTorque(
            transform.forward * rollTorque,
            ForceMode.Force
        );
    }

    void ApplyDownforce()
    {
        rb.AddForce(
            -Vector3.up *
            downForce *
            rb.velocity.magnitude
        );
    }

    // ================= VISUALS =================
    void UpdateWheelVisuals()
    {
        UpdateSingleWheel(driveLeft, driveLeftMesh, ref dlRot);
        UpdateSingleWheel(driveRight, driveRightMesh, ref drRot);
        UpdateSingleWheel(steerLeft, steerLeftMesh, ref slRot, true);
        UpdateSingleWheel(steerRight, steerRightMesh, ref srRot, true);
    }

    void UpdateSingleWheel(
        WheelCollider wc,
        Transform mesh,
        ref float rotation,
        bool steering = false)
    {
        if (!wc || !mesh) return;

        wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;

        rotation += wc.rpm * 6f * Time.deltaTime;
        Quaternion spin = Quaternion.Euler(0f, 90f, rotation);

        mesh.rotation = steering
            ? rot * spin
            : Quaternion.LookRotation(transform.forward) * spin;
    }

    // ================= TRAINING CONTROL =================
    public void SetDriveEnabled(bool value)
    {
        driveEnabled = value;
    }

    public void SetSpeedLimit(float maxSpeed)
    {
        speedLimit = maxSpeed;
    }

}
