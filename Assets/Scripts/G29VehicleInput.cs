using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using static Logitech.LogitechGSDK;

/// <summary>
/// G29VehicleInput
/// - Full forklift physics & visuals preserved
/// - UnityEvents for input Pressed/Held/Released
/// - Guided tutorial gate: locks other inputs and enforces expected input with hold-time
/// - Public API: StartTutorialStep(...), StopTutorial(), EnableEvents(bool)
/// - Extensive debug logging added (toggle with enableDebugLogs)
/// </summary>
public enum ExpectedInputType
{
    None,
    Accelerator,
    Brake,
    SteerLeft,
    SteerRight,
    GearForward,
    GearReverse,

    MastUp,
    MastDown,
    TiltUp,
    TiltDown
}
public enum OperationMode
{
    Tutorial,
    Performance
}

public class G29VehicleInput : MonoBehaviour
{
    // ================= DEBUG =================
    [Header("Debug")]
    public bool enableDebugLogs = true;
    [Tooltip("If true, logs fastest path checks (high volume). Turn off for release.")]
    public bool enableVerboseEventLogs = false;

    void LogDebug(string msg)
    {
        if (enableDebugLogs)
            Debug.Log("[G29VehicleInput] " + msg);
    }

    void LogVerbose(string msg)
    {
        if (enableDebugLogs && enableVerboseEventLogs)
            Debug.Log("[G29VehicleInput - VERBOSE] " + msg);
    }

    // ================= PUBLIC READ =================

    [Header("External Controllers")]
    public ForkliftHolderController forkController;

    public float AcceleratorValue => throttleInput;
    public float BrakeValue => brakeInput;
    public float SteeringValue => steerInput;
    public int GearState => gearState;
    public float CurrentSpeed => rb ? rb.velocity.magnitude : 0f;
    public float CurrentMastHeight => mastHeight;

    // ================= TRAINING GATES =================
    bool driveEnabled = true;
    float speedLimit = -1f; // -1 = no limit

    // ================= LOAD MODEL =================
    [Header("Load Model")]
    public float currentLoadKg = 0f;
    public float maxLoadKg = 2000f;

    bool tutorialStepAlreadyCompleted = false;

    // expose raw mast/tilt inputs so other systems (ForkliftHolderController) read them
    // G29VehicleInput will zero these when the guided gate blocks mast/tilt.
    public float MastRawInput { get; private set; } = 0f; // -1 down, 0 neutral, +1 up
    public float TiltRawInput { get; private set; } = 0f; // -1 tilt down, +1 tilt up

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

    // ================= RUNTIME (gated) =================
    // raw inputs from hardware
    float rawSteerInput, rawThrottleInput, rawBrakeInput;
    // gated inputs used by physics and StageManager
    float steerInput, throttleInput, brakeInput;
    int gearState;
    float targetSteer;
    float mastHeight;

    float dlRot, drRot, slRot, srRot;
    DIJOYSTATE2ENGINES state;

    // ================= TUTORIAL / EVENTS =================
    [Header("Tutorial Events - Accelerator")]
    public UnityEvent OnAcceleratorPressed;
    public UnityEvent OnAcceleratorHeld;
    public UnityEvent OnAcceleratorReleased;

    [Header("Tutorial Events - Brake")]
    public UnityEvent OnBrakePressed;
    public UnityEvent OnBrakeHeld;
    public UnityEvent OnBrakeReleased;

    [Header("Tutorial Events - Steering")]
    public UnityEvent OnSteeringLeftPressed;
    public UnityEvent OnSteeringLeftHeld;
    public UnityEvent OnSteeringLeftReleased;
    public UnityEvent OnSteeringRightPressed;
    public UnityEvent OnSteeringRightHeld;
    public UnityEvent OnSteeringRightReleased;

    [Header("Tutorial Events - Gears")]
    public UnityEvent OnGearForwardPressed;
    public UnityEvent OnGearForwardReleased;
    public UnityEvent OnGearReversePressed;
    public UnityEvent OnGearReverseReleased;

    [Header("Input Event Settings")]
    [Tooltip("Fraction [0..1] of pedal input required to consider it 'pressed'.")]
    [Range(0f, 1f)] public float pedalPressThreshold = 0.25f;
    [Tooltip("Fraction [0..1] of steering input magnitude required to consider steering 'pressed'.")]
    [Range(0f, 1f)] public float steerPressThreshold = 0.25f;
    [Tooltip("Seconds the input must be maintained before 'Held' fires.")]
    public float holdTime = 0.6f;
    [Tooltip("Seconds of cooldown after a press/hold to prevent immediate re-fire.")]
    public float eventCooldown = 0.5f;

    // internal event state tracking
    bool accelPressedState;
    bool accelHeldState;
    float accelPressTimestamp;
    float accelLastFiredTimestamp;

    bool brakePressedState;
    bool brakeHeldState;
    float brakePressTimestamp;
    float brakeLastFiredTimestamp;

    bool steerLeftPressedState;
    bool steerLeftHeldState;
    float steerLeftPressTimestamp;
    float steerLeftLastFiredTimestamp;

    bool steerRightPressedState;
    bool steerRightHeldState;
    float steerRightPressTimestamp;
    float steerRightLastFiredTimestamp;

    bool gearFwdState;
    float gearFwdLastFiredTimestamp;
    bool gearRevState;
    float gearRevLastFiredTimestamp;

    public bool VehicleOnOff = true;

    public void SetG29VehicleInput(bool vehicleOnOff)
    {
        VehicleOnOff = vehicleOnOff;
    }

    // ================= GUIDED TUTORIAL GATE CONFIG =================
    [Header("Guided Tutorial Gate")]
    [Tooltip("If true, when a tutorial step is active only the expected input is accepted and other inputs are blocked.")]
    public bool guidedGateEnabled = true;
    public ExpectedInputType expectedInput = ExpectedInputType.None;
    [Tooltip("Input threshold used by the guided gate (overrides pedalPressThreshold/steerPressThreshold for gate detection when > 0).")]
    [Range(0f, 1f)] public float gateInputThreshold = 0.3f;
    [Tooltip("Seconds required to hold the expected input to complete the step.")]
    public float gateRequiredHoldTime = 0.5f;
    [Tooltip("If true, the gate waits until all inputs are neutral before activating (recommended).")]
    public bool requireNeutralBeforeStart = true;

    bool tutorialActive = false;
    float gateHoldTimer = 0f;
    float gateStartRequestedAt = 0f;

    // controls whether UnityEvents are fired (useful for disabling during assessment)
    bool eventsEnabled = true;

    [Header("MODE")]
    public OperationMode currentMode = OperationMode.Tutorial;


    void Start()
    {
        if (rb && baseCenterOfMass)
            rb.centerOfMass = baseCenterOfMass.localPosition;

        LogDebug("Start(): ready. guidedGateEnabled=" + guidedGateEnabled);
    }

    void Update()
    {
        if (G29Manager.Instance == null || !G29Manager.Instance.Ready)
        {
            LogVerbose("G29Manager not ready");
            return;
        }
        ReadG29_Raw();
        if (currentMode == OperationMode.Tutorial)
        {
            HandleUnityEvents();
            HandleGuidedGate();
        }
        else
        {
            // Performance mode
            tutorialActive = false;
            expectedInput = ExpectedInputType.None;
        }
    }

    void FixedUpdate()
    {
        // physics uses gated values (throttleInput, brakeInput, steerInput)
        ApplyDynamicCenterOfMass();
        ApplyRollInstability();
        ApplyDrivePhysics();
        ApplyDownforce();
        UpdateWheelVisuals();
    }

    // ================= INPUT (raw read) =================
    void ReadG29_Raw()
    {
        state = G29Manager.Instance.State;

        rawSteerInput = state.lX / 32767f;
        rawThrottleInput = 1f - ((state.lY + 32768f) / 65535f);
        rawBrakeInput = 1f - ((state.lRz + 32768f) / 65535f);

        bool gearFwd = state.rgbButtons[12] == 128;
        bool gearRev = state.rgbButtons[13] == 128;
        gearState = gearFwd ? 1 : gearRev ? -1 : 0;

        // Mast buttons — match your ForkliftHolderController mapping:
        // button 7 = mast up, button 6 = mast down
        if (state.rgbButtons[7] == 128) MastRawInput = 1f;
        else if (state.rgbButtons[6] == 128) MastRawInput = -1f;
        else MastRawInput = 0f;

        // Tilt buttons — button 5 = tilt up, button 4 = tilt down
        if (state.rgbButtons[5] == 128) TiltRawInput = 1f;
        else if (state.rgbButtons[4] == 128) TiltRawInput = -1f;
        else TiltRawInput = 0f;

        // by default, gated inputs follow raw ones; guided gate can override them
        throttleInput = rawThrottleInput;
        brakeInput = rawBrakeInput;
        steerInput = rawSteerInput;

        LogVerbose($"Raw inputs -> Throttle:{rawThrottleInput:F2} Brake:{rawBrakeInput:F2} Steer:{rawSteerInput:F2} Gear:{gearState} Mast:{MastRawInput} Tilt:{TiltRawInput}");
    }

    // ================= CORE PHYSICS =================
    void ApplyDrivePhysics()
    {
        float loadRatio = Mathf.Clamp01(currentLoadKg / maxLoadKg);

        // ---------- Steering (load stiffening) ----------
        float effectiveSteerAngle = maxSteerAngle * Mathf.Lerp(1f, 0.55f, loadRatio);

        targetSteer = Mathf.Lerp(
            targetSteer,
            -steerInput * effectiveSteerAngle,
            steerSpeed * Time.fixedDeltaTime
        );

        if (steerLeft != null) steerLeft.steerAngle = targetSteer;
        if (steerRight != null) steerRight.steerAngle = targetSteer;

        // ---------- Acceleration (inertia) ----------
        float inertiaMultiplier = Mathf.Lerp(1f, 0.4f, loadRatio);
        float effectiveMotorForce = motorForce * inertiaMultiplier;

        float torque = -throttleInput * effectiveMotorForce * gearState;
        if (driveLeft != null) driveLeft.motorTorque = torque;
        if (driveRight != null) driveRight.motorTorque = torque;

        if (!driveEnabled)
        {
            // Kill forward motion ONLY
            throttleInput = 0f;
            brakeInput = Mathf.Max(brakeInput, 0.2f); // gentle hold
        }

        // ---------- Braking (mass amplification) ----------
        float effectiveBrakeForce = brakeForce * Mathf.Lerp(1f, 1.35f, loadRatio);

        float brakes = brakeInput * effectiveBrakeForce;
        if (driveLeft != null) driveLeft.brakeTorque = brakes;
        if (driveRight != null) driveRight.brakeTorque = brakes;

        // enforce optional speed limit
        if (speedLimit > 0f && rb)
        {
            float speed = rb.velocity.magnitude;
            if (speed > speedLimit)
            {
                // simple dampening: reduce motor torque when above limit
                if (driveLeft != null) driveLeft.motorTorque *= 0f;
                if (driveRight != null) driveRight.motorTorque *= 0f;
            }
        }
    }

    // ================= STABILITY =================
    void ApplyDynamicCenterOfMass()
    {
        if (!rb || !baseCenterOfMass) return;

        float loadRatio = Mathf.Clamp01(currentLoadKg / maxLoadKg);
        float heightRatio = Mathf.Clamp01(mastHeight / maxMastHeight);

        float forwardShift = loadRatio * heightRatio * maxCOMForwardShift;

        rb.centerOfMass = baseCenterOfMass.localPosition + Vector3.forward * forwardShift;
    }

    void ApplyRollInstability()
    {
        if (currentLoadKg <= 0f) return;

        float heightRatio = Mathf.Clamp01(mastHeight / maxMastHeight);

        float lateralSpeed = Vector3.Dot(rb.velocity, transform.right);

        float rollTorque = lateralSpeed * currentLoadKg * heightRatio * 0.012f;

        rb.AddTorque(transform.forward * rollTorque, ForceMode.Force);
    }

    void ApplyDownforce()
    {
        if (!rb) return;
        rb.AddForce(-Vector3.up * downForce * rb.velocity.magnitude);
    }

    // ================= VISUALS =================
    void UpdateWheelVisuals()
    {
        UpdateSingleWheel(driveLeft, driveLeftMesh, ref dlRot);
        UpdateSingleWheel(driveRight, driveRightMesh, ref drRot);
        UpdateSingleWheel(steerLeft, steerLeftMesh, ref slRot, true);
        UpdateSingleWheel(steerRight, steerRightMesh, ref srRot, true);
    }

    void UpdateSingleWheel(WheelCollider wc, Transform mesh, ref float rotation, bool steering = false)
    {
        if (!wc || !mesh) return;

        wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;

        rotation += wc.rpm * 6f * Time.deltaTime;
        Quaternion spin = Quaternion.Euler(0f, 90f, rotation);

        mesh.rotation = steering ? rot * spin : Quaternion.LookRotation(transform.forward) * spin;
    }

    // ================= TRAINING CONTROL =================
    public void SetDriveEnabled(bool value)
    {
        driveEnabled = value;
        LogDebug("SetDriveEnabled: " + value);
    }

    public void SetSpeedLimit(float maxSpeed)
    {
        speedLimit = maxSpeed;
        LogDebug("SetSpeedLimit: " + maxSpeed);
    }

    // ================= UNITYEVENTS LOGIC =================
    void HandleUnityEvents()
    {
        if (!eventsEnabled) return;

        float now = Time.time;

        LogVerbose($"HandleUnityEvents start - expectedInput={expectedInput} tutorialActive={tutorialActive}");

        // Accelerator events
        bool accelNow = rawThrottleInput > pedalPressThreshold;
        if (accelNow && !accelPressedState && now - accelLastFiredTimestamp >= eventCooldown)
        {
            accelPressedState = true;
            accelPressTimestamp = now;
            accelLastFiredTimestamp = now;
            LogDebug("OnAcceleratorPressed invoked");
            OnAcceleratorPressed?.Invoke();
            CompleteAccelerator_Step();
        }
        if (accelPressedState && !accelNow)
        {
            accelPressedState = false;
            accelHeldState = false;
            LogVerbose("OnAcceleratorReleased invoked");
            OnAcceleratorReleased?.Invoke();
        }
        if (accelPressedState && !accelHeldState && now - accelPressTimestamp >= holdTime)
        {
            accelHeldState = true;
            LogVerbose("OnAcceleratorHeld invoked");
            OnAcceleratorHeld?.Invoke();
            accelLastFiredTimestamp = now;
        }

        // Brake events
        bool brakeNow = rawBrakeInput > pedalPressThreshold;
        if (brakeNow && !brakePressedState && now - brakeLastFiredTimestamp >= eventCooldown)
        {
            brakePressedState = true;
            brakePressTimestamp = now;
            brakeLastFiredTimestamp = now;
            LogDebug("OnBrakePressed invoked");
            OnBrakePressed?.Invoke();
            CompleteBrake_Step();
        }
        if (brakePressedState && !brakeNow)
        {
            brakePressedState = false;
            brakeHeldState = false;
            LogVerbose("OnBrakeReleased invoked");
            OnBrakeReleased?.Invoke();
        }
        if (brakePressedState && !brakeHeldState && now - brakePressTimestamp >= holdTime)
        {
            brakeHeldState = true;
            LogVerbose("OnBrakeHeld invoked");
            OnBrakeHeld?.Invoke();
            brakeLastFiredTimestamp = now;
        }

        // Steering Left
        bool steerLeftNow = rawSteerInput < -steerPressThreshold;
        if (steerLeftNow && !steerLeftPressedState && now - steerLeftLastFiredTimestamp >= eventCooldown)
        {
            steerLeftPressedState = true;
            steerLeftPressTimestamp = now;
            steerLeftLastFiredTimestamp = now;
            LogDebug("OnSteeringLeftPressed invoked");
            OnSteeringLeftPressed?.Invoke();
            CompleteSteerLeft_Step();
        }
        if (steerLeftPressedState && !steerLeftNow)
        {
            steerLeftPressedState = false;
            steerLeftHeldState = false;
            LogVerbose("OnSteeringLeftReleased invoked");
            OnSteeringLeftReleased?.Invoke();
        }
        if (steerLeftPressedState && !steerLeftHeldState && now - steerLeftPressTimestamp >= holdTime)
        {
            steerLeftHeldState = true;
            LogVerbose("OnSteeringLeftHeld invoked");
            OnSteeringLeftHeld?.Invoke();
            steerLeftLastFiredTimestamp = now;
        }

        // Steering Right
        bool steerRightNow = rawSteerInput > steerPressThreshold;
        if (steerRightNow && !steerRightPressedState && now - steerRightLastFiredTimestamp >= eventCooldown)
        {
            steerRightPressedState = true;
            steerRightPressTimestamp = now;
            steerRightLastFiredTimestamp = now;
            LogDebug("OnSteeringRightPressed invoked");
            OnSteeringRightPressed?.Invoke();
            CompleteSteerRight_Step();
        }
        if (steerRightPressedState && !steerRightNow)
        {
            steerRightPressedState = false;
            steerRightHeldState = false;
            LogVerbose("OnSteeringRightReleased invoked");
            OnSteeringRightReleased?.Invoke();
        }
        if (steerRightPressedState && !steerRightHeldState && now - steerRightPressTimestamp >= holdTime)
        {
            steerRightHeldState = true;
            LogVerbose("OnSteeringRightHeld invoked");
            OnSteeringRightHeld?.Invoke();
            steerRightLastFiredTimestamp = now;
        }

        // Gear forward/reverse events
        bool gearFwdNow = gearState == 1;
        if (gearFwdNow && !gearFwdState && now - gearFwdLastFiredTimestamp >= eventCooldown)
        {
            gearFwdState = true;
            gearFwdLastFiredTimestamp = now;
            LogDebug("OnGearForwardPressed invoked");
            OnGearForwardPressed?.Invoke();
            CompleteGearForward_Step();
        }
        if (gearFwdState && !gearFwdNow)
        {
            gearFwdState = false;
            LogVerbose("OnGearForwardReleased invoked");
            OnGearForwardReleased?.Invoke();
            gearFwdLastFiredTimestamp = now;
        }

        bool gearRevNow = gearState == -1;
        if (gearRevNow && !gearRevState && now - gearRevLastFiredTimestamp >= eventCooldown)
        {
            gearRevState = true;
            gearRevLastFiredTimestamp = now;
            LogDebug("OnGearReversePressed invoked");
            OnGearReversePressed?.Invoke();
            CompleteGearReverse_Step();
        }
        if (gearRevState && !gearRevNow)
        {
            gearRevState = false;
            LogVerbose("OnGearReverseReleased invoked");
            OnGearReverseReleased?.Invoke();
            gearRevLastFiredTimestamp = now;
            CompleteGearReverse_Step();
        }

        // Mast / Tilt quick checks (these are high-frequency; verbose-only recommended)
        bool mastUpNow = MastRawInput > 0.5f;
        if (mastUpNow)
        {
            LogVerbose("MastUp detected (MastRawInput=" + MastRawInput + ")");
            if (expectedInput == ExpectedInputType.MastUp && tutorialActive)
            {
                LogDebug("MastUp matches expected -> CompleteMastUp_Step()");
                CompleteMastUp_Step();
            }
            else
            {
                LogVerbose("MastUp pressed but not expected (expected=" + expectedInput + ")");
            }
        }

        bool mastDownNow = MastRawInput < -0.5f;
        if (mastDownNow)
        {
            LogVerbose("MastDown detected (MastRawInput=" + MastRawInput + ")");
            if (expectedInput == ExpectedInputType.MastDown && tutorialActive)
            {
                LogDebug("MastDown matches expected -> CompleteMastDown_Step()");
                CompleteMastDown_Step();
            }
            else
            {
                LogVerbose("MastDown pressed but not expected (expected=" + expectedInput + ")");
            }
        }

        bool tiltUpNow = TiltRawInput > 0.5f;
        if (tiltUpNow)
        {
            LogVerbose("TiltUp detected (TiltRawInput=" + TiltRawInput + ")");
            if (expectedInput == ExpectedInputType.TiltUp && tutorialActive)
            {
                LogDebug("TiltUp matches expected -> CompleteTiltUp_Step()");
                CompleteTiltUp_Step();
            }
            else
            {
                LogVerbose("TiltUp pressed but not expected (expected=" + expectedInput + ")");
            }
        }

        bool tiltDownNow = TiltRawInput < -0.5f;
        if (tiltDownNow)
        {
            LogVerbose("TiltDown detected (TiltRawInput=" + TiltRawInput + ")");
            if (expectedInput == ExpectedInputType.TiltDown && tutorialActive)
            {
                LogDebug("TiltDown matches expected -> CompleteTiltDown_Step()");
                CompleteTiltDown_Step();
            }
            else
            {
                LogVerbose("TiltDown pressed but not expected (expected=" + expectedInput + ")");
            }
        }
    }

    // ================= GUIDED TUTORIAL GATE =================
    void HandleGuidedGate()
    {
        if (!guidedGateEnabled)
        {
            // gate not used; ensure gated inputs are raw
            throttleInput = rawThrottleInput;
            brakeInput = rawBrakeInput;
            steerInput = rawSteerInput;

            // propagate mast/tilt raw to forkController (normal operation)
            if (forkController != null)
            {
                forkController.MastInput = MastRawInput;
                forkController.tiltInput = TiltRawInput;
            }

            return;
        }

        if (!tutorialActive)
        {
            // not active, simply use raw inputs (unless you want to globally block movement while stage manager waits)
            throttleInput = rawThrottleInput;
            brakeInput = rawBrakeInput;
            steerInput = rawSteerInput;

            if (forkController != null)
            {
                forkController.MastInput = MastRawInput;
                forkController.tiltInput = TiltRawInput;
            }

            return;
        }

        // tutorialActive -> enforce expected input, block others
        float tThreshold = gateInputThreshold > 0f ? gateInputThreshold : pedalPressThreshold;
        LogVerbose($"HandleGuidedGate: expected={expectedInput} threshold={tThreshold} gateHoldTimer={gateHoldTimer:F2}");

        switch (expectedInput)
        {
            case ExpectedInputType.Accelerator:
                if (rawThrottleInput > tThreshold)
                {
                    gateHoldTimer += Time.deltaTime;
                    throttleInput = rawThrottleInput;
                    steerInput = 0f;
                    brakeInput = 0f;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    steerInput = 0f;
                    brakeInput = 0f;
                }
                // fork mast/tilt blocked
                if (forkController != null) { forkController.MastInput = 0f; forkController.tiltInput = 0f; }
                break;

            case ExpectedInputType.Brake:
                if (rawBrakeInput > tThreshold)
                {
                    gateHoldTimer += Time.deltaTime;
                    brakeInput = rawBrakeInput;
                    throttleInput = 0f;
                    steerInput = 0f;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    steerInput = 0f;
                    brakeInput = 0f;
                }
                if (forkController != null) { forkController.MastInput = 0f; forkController.tiltInput = 0f; }
                break;

            case ExpectedInputType.SteerLeft:
                if (rawSteerInput < -tThreshold)
                {
                    gateHoldTimer += Time.deltaTime;
                    steerInput = rawSteerInput;
                    throttleInput = 0f;
                    brakeInput = 0f;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    steerInput = 0f;
                    brakeInput = 0f;
                }
                if (forkController != null) { forkController.MastInput = 0f; forkController.tiltInput = 0f; }
                break;

            case ExpectedInputType.SteerRight:
                if (rawSteerInput > tThreshold)
                {
                    gateHoldTimer += Time.deltaTime;
                    steerInput = rawSteerInput;
                    throttleInput = 0f;
                    brakeInput = 0f;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    steerInput = 0f;
                    brakeInput = 0f;
                }
                if (forkController != null) { forkController.MastInput = 0f; forkController.tiltInput = 0f; }
                break;

            case ExpectedInputType.GearForward:
                // for gears, detect state == 1 and count as instant (or require hold if desired)
                if (gearState == 1)
                {
                    gateHoldTimer += Time.deltaTime;
                    throttleInput = 0f;
                    steerInput = 0f;
                    brakeInput = 0f;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    steerInput = 0f;
                    brakeInput = 0f;
                }
                if (forkController != null) { forkController.MastInput = 0f; forkController.tiltInput = 0f; }
                break;

            case ExpectedInputType.GearReverse:
                if (gearState == -1)
                {
                    gateHoldTimer += Time.deltaTime;
                    throttleInput = 0f;
                    steerInput = 0f;
                    brakeInput = 0f;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    steerInput = 0f;
                    brakeInput = 0f;
                }
                if (forkController != null) { forkController.MastInput = 0f; forkController.tiltInput = 0f; }
                break;

            case ExpectedInputType.MastUp:
                if (MastRawInput > tThreshold)
                {
                    gateHoldTimer += Time.deltaTime;
                    throttleInput = 0f;
                    brakeInput = 0f;
                    steerInput = 0f;
                    // allow mast only
                    if (forkController != null)
                        forkController.MastInput = MastRawInput;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    brakeInput = 0f;
                    steerInput = 0f;
                    if (forkController != null)
                        forkController.MastInput = 0f;
                }
                // tilt blocked
                if (forkController != null) forkController.tiltInput = 0f;
                break;

            case ExpectedInputType.MastDown:
                if (MastRawInput < -tThreshold)
                {
                    gateHoldTimer += Time.deltaTime;
                    throttleInput = 0f;
                    brakeInput = 0f;
                    steerInput = 0f;
                    if (forkController != null)
                        forkController.MastInput = MastRawInput;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    brakeInput = 0f;
                    steerInput = 0f;
                    if (forkController != null)
                        forkController.MastInput = 0f;
                }
                if (forkController != null) forkController.tiltInput = 0f;
                break;

            case ExpectedInputType.TiltUp:
                if (TiltRawInput > tThreshold)
                {
                    gateHoldTimer += Time.deltaTime;
                    throttleInput = 0f;
                    brakeInput = 0f;
                    steerInput = 0f;
                    if (forkController != null)
                        forkController.tiltInput = TiltRawInput;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    brakeInput = 0f;
                    steerInput = 0f;
                    if (forkController != null)
                        forkController.tiltInput = 0f;
                }
                if (forkController != null) forkController.MastInput = 0f;
                break;

            case ExpectedInputType.TiltDown:
                if (TiltRawInput < -tThreshold)
                {
                    gateHoldTimer += Time.deltaTime;
                    throttleInput = 0f;
                    brakeInput = 0f;
                    steerInput = 0f;
                    if (forkController != null)
                        forkController.tiltInput = TiltRawInput;
                }
                else
                {
                    gateHoldTimer = 0f;
                    throttleInput = 0f;
                    brakeInput = 0f;
                    steerInput = 0f;
                    if (forkController != null)
                        forkController.tiltInput = 0f;
                }
                if (forkController != null) forkController.MastInput = 0f;
                break;

            case ExpectedInputType.None:
            default:
                // no expectation -> allow raw
                throttleInput = rawThrottleInput;
                brakeInput = rawBrakeInput;
                steerInput = rawSteerInput;
                if (forkController != null)
                {
                    forkController.MastInput = MastRawInput;
                    forkController.tiltInput = TiltRawInput;
                }
                break;
        }

        // completion via hold-time
        if (gateHoldTimer >= gateRequiredHoldTime)
        {
            LogDebug($"Gate hold complete for expected={expectedInput}. gateHoldTimer={gateHoldTimer:F2}");
            gateHoldTimer = 0f;
            CompleteTutorialStep();
        }

        // safety: if gating active and tutorialActive, ensure mast/tilt raw values are zeroed if not allowed
        if (guidedGateEnabled && tutorialActive)
        {
            bool allowMast = expectedInput == ExpectedInputType.MastUp || expectedInput == ExpectedInputType.MastDown;
            bool allowTilt = expectedInput == ExpectedInputType.TiltUp || expectedInput == ExpectedInputType.TiltDown;

            if (!allowMast)
            {
                if (MastRawInput != 0f) LogVerbose("Blocking MastRawInput due to gate");
                MastRawInput = 0f;
                if (forkController != null) forkController.MastInput = 0f;
            }
            if (!allowTilt)
            {
                if (TiltRawInput != 0f) LogVerbose("Blocking TiltRawInput due to gate");
                TiltRawInput = 0f;
                if (forkController != null) forkController.tiltInput = 0f;
            }
        }
    }

    void CompleteTutorialStep()
    {
        // stop gate and restore normal input behavior
        LogDebug("CompleteTutorialStep() called. expectedInput=" + expectedInput + " tutorialActive=" + tutorialActive);

        tutorialActive = false;
        expectedInput = ExpectedInputType.None;
        gateHoldTimer = 0f;

        // call StageManager
        if (StageManager.Instance != null)
        {
            LogDebug("Calling StageManager.Instance.StepCompleted()");
            StageManager.Instance.StepCompleted();
        }
        else
        {
            LogDebug("StageManager.Instance is null — cannot call StepCompleted()");
        }
    }

    // ================= PUBLIC CONTROL API =================

    /// <summary>
    /// Starts a guided tutorial step for the given expected input.
    /// If requireNeutralBeforeStart==true, the method waits until raw inputs are neutral then activates the gate.
    /// </summary>
   public void StartTutorialStep(ExpectedInputType input, bool requireNeutral = false, float optionalHoldTime = -1f, float optionalThreshold = -1f)
{
        if (currentMode != OperationMode.Tutorial)
            return;

        StopAllCoroutines();

    tutorialStepAlreadyCompleted = false;   // 🔥 RESET HERE

    expectedInput = input;
    tutorialActive = true;
    gateHoldTimer = 0f;

    Debug.Log("[G29] Tutorial Started: " + expectedInput);
}

    public void StopTutorial()
    {
        LogDebug("StopTutorial() called. expectedInput=" + expectedInput + " tutorialActive=" + tutorialActive);
        tutorialActive = false;
        expectedInput = ExpectedInputType.None;
        gateHoldTimer = 0f;
    }

    void ApplyHardInputLock(ExpectedInputType allowedInput)
    {
        // Reset everything first
        throttleInput = 0f;
        brakeInput = 0f;
        steerInput = 0f;

        if (forkController != null)
        {
            forkController.MastInput = 0f;
            forkController.tiltInput = 0f;
        }

        // Allow ONLY the required input
        switch (allowedInput)
        {
            case ExpectedInputType.Accelerator:
                throttleInput = rawThrottleInput;
                break;

            case ExpectedInputType.Brake:
                brakeInput = rawBrakeInput;
                break;

            case ExpectedInputType.SteerLeft:
            case ExpectedInputType.SteerRight:
                steerInput = rawSteerInput;
                break;

            case ExpectedInputType.GearForward:
            case ExpectedInputType.GearReverse:
                // nothing else allowed; gearState already handled
                break;

            case ExpectedInputType.MastUp:
            case ExpectedInputType.MastDown:
                // allow mast only (handled in HandleGuidedGate)
                break;

            case ExpectedInputType.TiltUp:
            case ExpectedInputType.TiltDown:
                // allow tilt only (handled in HandleGuidedGate)
                break;
        }

        LogDebug("ApplyHardInputLock -> allowedInput=" + allowedInput + " throttle=" + throttleInput + " brake=" + brakeInput + " steer=" + steerInput);
    }

    /// <summary>
    /// Enable/disable UnityEvents firing (useful in assessment or replay).
    /// </summary>
    public void EnableEvents(bool enable)
    {
        eventsEnabled = enable;
        LogDebug("EnableEvents: " + enable);
    }

    // ===== WRAPPERS FOR UNITYEVENT =====

    public void StartStep_Accelerator()
    {
        StartTutorialStep(ExpectedInputType.Accelerator);
    }

    public void StartStep_Brake()
    {
        StartTutorialStep(ExpectedInputType.Brake);
    }

    public void StartStep_SteerLeft()
    {
        StartTutorialStep(ExpectedInputType.SteerLeft);
    }

    public void StartStep_SteerRight()
    {
        StartTutorialStep(ExpectedInputType.SteerRight);
    }

    public void StartStep_GearForward()
    {
        StartTutorialStep(ExpectedInputType.GearForward);
    }

    public void StartStep_GearReverse()
    {
        StartTutorialStep(ExpectedInputType.GearReverse);
    }
    public void StartStep_MastUp()
    {
        StartTutorialStep(ExpectedInputType.MastUp);
    }

    public void StartStep_MastDown()
    {
        StartTutorialStep(ExpectedInputType.MastDown);
    }

    public void StartStep_TiltUp()
    {
        StartTutorialStep(ExpectedInputType.TiltUp);
    }

    public void StartStep_TiltDown()
    {
        StartTutorialStep(ExpectedInputType.TiltDown);
    }

    // Generic completion helper used by specific "CompleteX" wrappers
    public void CompleteStepIfMatches(ExpectedInputType stepType)
    {
        if (currentMode != OperationMode.Tutorial)
            return;

        if (!tutorialActive)
            return;

        if (tutorialStepAlreadyCompleted)
            return;

        if (expectedInput != stepType)
            return;

        tutorialStepAlreadyCompleted = true;

        CompleteTutorialStep();
    }
    public void EnableTutorialMode()
    {
        currentMode = OperationMode.Tutorial;
        Debug.Log("[G29] Switched to Tutorial Mode");
    }

    public void EnablePerformanceMode()
    {
        currentMode = OperationMode.Performance;

        tutorialActive = false;
        expectedInput = ExpectedInputType.None;
        tutorialStepAlreadyCompleted = false;

        Debug.Log("[G29] Switched to Performance Mode");
    }
    public void CompleteAccelerator_Step() { CompleteStepIfMatches(ExpectedInputType.Accelerator); }
    public void CompleteBrake_Step() { CompleteStepIfMatches(ExpectedInputType.Brake); }
    public void CompleteSteerLeft_Step() { CompleteStepIfMatches(ExpectedInputType.SteerLeft); }
    public void CompleteSteerRight_Step() { CompleteStepIfMatches(ExpectedInputType.SteerRight); }
    public void CompleteGearForward_Step() { CompleteStepIfMatches(ExpectedInputType.GearForward); }
    public void CompleteGearReverse_Step() { CompleteStepIfMatches(ExpectedInputType.GearReverse); }
    public void CompleteMastUp_Step() { CompleteStepIfMatches(ExpectedInputType.MastUp); }
    public void CompleteMastDown_Step() { CompleteStepIfMatches(ExpectedInputType.MastDown); }
    public void CompleteTiltUp_Step() { CompleteStepIfMatches(ExpectedInputType.TiltUp); }
    public void CompleteTiltDown_Step() { CompleteStepIfMatches(ExpectedInputType.TiltDown); }

    // End of class
}