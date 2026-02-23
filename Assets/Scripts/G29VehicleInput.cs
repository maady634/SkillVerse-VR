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
/// </summary>
public enum ExpectedInputType
{
    None,
    Accelerator,
    Brake,
    SteerLeft,
    SteerRight,
    GearForward,
    GearReverse
}

public class G29VehicleInput : MonoBehaviour
{
    // ================= PUBLIC READ =================
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

    void Start()
    {
        if (rb && baseCenterOfMass)
            rb.centerOfMass = baseCenterOfMass.localPosition;
    }

    void Update()
    {
        if (G29Manager.Instance == null || !G29Manager.Instance.Ready)
            return;

        ReadG29_Raw();

        // process events and gating in Update (non-physics)
        HandleUnityEvents();          // fires events (Pressed/Held/Released) when enabled
        HandleGuidedGate();           // enforces expected input and blocks others if tutorialActive
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

        // by default, gated inputs follow raw ones; guided gate can override them
        throttleInput = rawThrottleInput;
        brakeInput = rawBrakeInput;
        steerInput = rawSteerInput;
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
        float effectiveBrakeForce = brakeForce * Mathf.Lerp(1f, 1.35f, loadRatio);

        float brakes = brakeInput * effectiveBrakeForce;
        driveLeft.brakeTorque = brakes;
        driveRight.brakeTorque = brakes;

        // enforce optional speed limit
        if (speedLimit > 0f && rb)
        {
            float speed = rb.velocity.magnitude;
            if (speed > speedLimit)
            {
                // simple dampening: reduce motor torque when above limit
                driveLeft.motorTorque *= 0f;
                driveRight.motorTorque *= 0f;
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
    }

    public void SetSpeedLimit(float maxSpeed)
    {
        speedLimit = maxSpeed;
    }

    // ================= UNITYEVENTS LOGIC =================
    void HandleUnityEvents()
    {
        if (!eventsEnabled) return;

        float now = Time.time;

        // Accelerator events
        bool accelNow = rawThrottleInput > pedalPressThreshold;
        if (accelNow && !accelPressedState && now - accelLastFiredTimestamp >= eventCooldown)
        {
            accelPressedState = true;
            accelPressTimestamp = now;
            accelLastFiredTimestamp = now;
            OnAcceleratorPressed?.Invoke();
        }
        if (accelPressedState && !accelNow)
        {
            accelPressedState = false;
            accelHeldState = false;
            OnAcceleratorReleased?.Invoke();
        }
        if (accelPressedState && !accelHeldState && now - accelPressTimestamp >= holdTime)
        {
            accelHeldState = true;
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
            OnBrakePressed?.Invoke();
        }
        if (brakePressedState && !brakeNow)
        {
            brakePressedState = false;
            brakeHeldState = false;
            OnBrakeReleased?.Invoke();
        }
        if (brakePressedState && !brakeHeldState && now - brakePressTimestamp >= holdTime)
        {
            brakeHeldState = true;
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
            OnSteeringLeftPressed?.Invoke();
        }
        if (steerLeftPressedState && !steerLeftNow)
        {
            steerLeftPressedState = false;
            steerLeftHeldState = false;
            OnSteeringLeftReleased?.Invoke();
        }
        if (steerLeftPressedState && !steerLeftHeldState && now - steerLeftPressTimestamp >= holdTime)
        {
            steerLeftHeldState = true;
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
            OnSteeringRightPressed?.Invoke();
        }
        if (steerRightPressedState && !steerRightNow)
        {
            steerRightPressedState = false;
            steerRightHeldState = false;
            OnSteeringRightReleased?.Invoke();
        }
        if (steerRightPressedState && !steerRightHeldState && now - steerRightPressTimestamp >= holdTime)
        {
            steerRightHeldState = true;
            OnSteeringRightHeld?.Invoke();
            steerRightLastFiredTimestamp = now;
        }

        // Gear forward/reverse events
        bool gearFwdNow = gearState == 1;
        if (gearFwdNow && !gearFwdState && now - gearFwdLastFiredTimestamp >= eventCooldown)
        {
            gearFwdState = true;
            gearFwdLastFiredTimestamp = now;
            OnGearForwardPressed?.Invoke();
        }
        if (gearFwdState && !gearFwdNow)
        {
            gearFwdState = false;
            OnGearForwardReleased?.Invoke();
            gearFwdLastFiredTimestamp = now;
        }

        bool gearRevNow = gearState == -1;
        if (gearRevNow && !gearRevState && now - gearRevLastFiredTimestamp >= eventCooldown)
        {
            gearRevState = true;
            gearRevLastFiredTimestamp = now;
            OnGearReversePressed?.Invoke();
        }
        if (gearRevState && !gearRevNow)
        {
            gearRevState = false;
            OnGearReverseReleased?.Invoke();
            gearRevLastFiredTimestamp = now;
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
            return;
        }

        if (!tutorialActive)
        {
            // not active, simply use raw inputs (unless you want to globally block movement while stage manager waits)
            throttleInput = rawThrottleInput;
            brakeInput = rawBrakeInput;
            steerInput = rawSteerInput;
            return;
        }

        // tutorialActive -> enforce expected input, block others
        float tThreshold = gateInputThreshold > 0f ? gateInputThreshold : pedalPressThreshold;
        switch (expectedInput)
        {
            case ExpectedInputType.Accelerator:
                if (rawThrottleInput > tThreshold)
                {
                    gateHoldTimer += Time.deltaTime;
                    throttleInput = rawThrottleInput;
                    // allow brake/steer only if they are also expected (not here)
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
                break;

            case ExpectedInputType.None:
            default:
                // no expectation -> allow raw
                throttleInput = rawThrottleInput;
                brakeInput = rawBrakeInput;
                steerInput = rawSteerInput;
                break;
        }

        // completion
        if (gateHoldTimer >= gateRequiredHoldTime)
        {
            gateHoldTimer = 0f;
            CompleteTutorialStep();
        }
    }

    void CompleteTutorialStep()
    {
        // stop gate and restore normal input behavior
        tutorialActive = false;
        expectedInput = ExpectedInputType.None;
        gateHoldTimer = 0f;

        // call StageManager
        if (StageManager.Instance != null)
            StageManager.Instance.StepCompleted();
    }

    // ================= PUBLIC CONTROL API =================

    /// <summary>
    /// Starts a guided tutorial step for the given expected input.
    /// If requireNeutralBeforeStart==true, the method waits until raw inputs are neutral then activates the gate.
    /// </summary>
    public void StartTutorialStep(ExpectedInputType input, bool requireNeutral = true, float optionalHoldTime = -1f, float optionalThreshold = -1f)
    {
        StopAllCoroutines(); // cancel any previous start requests
        if (optionalHoldTime > 0f) gateRequiredHoldTime = optionalHoldTime;
        if (optionalThreshold >= 0f) gateInputThreshold = optionalThreshold;

        if (requireNeutral && requireNeutralBeforeStart)
        {
            StartCoroutine(WaitForNeutralThenStart(input));
            return;
        }

        expectedInput = input;
        tutorialActive = true;
        gateHoldTimer = 0f;
        gateStartRequestedAt = Time.time;
    }

    IEnumerator WaitForNeutralThenStart(ExpectedInputType input)
    {
        // wait until all raw inputs are neutral (below thresholds)
        while (Mathf.Abs(rawSteerInput) > 0.05f || rawThrottleInput > 0.02f || rawBrakeInput > 0.02f || gearState != 0)
        {
            // if hardware not ready, break to avoid lock
            if (G29Manager.Instance == null || !G29Manager.Instance.Ready) yield break;
            yield return null;
        }

        expectedInput = input;
        tutorialActive = true;
        gateHoldTimer = 0f;
        gateStartRequestedAt = Time.time;
    }

    public void StopTutorial()
    {
        tutorialActive = false;
        expectedInput = ExpectedInputType.None;
        gateHoldTimer = 0f;
    }

    /// <summary>
    /// Enable/disable UnityEvents firing (useful in assessment or replay).
    /// </summary>
    public void EnableEvents(bool enable)
    {
        eventsEnabled = enable;
    }

    /// <summary>
    /// Backwards-compatible method: set drive enabled (still respected while gating)
    /// </summary>
   
}