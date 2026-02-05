using UnityEngine;
using static Logitech.LogitechGSDK;

public class G29ForceFeedback : MonoBehaviour
{
    public G29VehicleInput vehicle;
    public ForkliftHolderController forkController;

    // ================= STEERING CORE =================
    [Header("Steering Feel (Realistic Forklift)")]

    [Range(0, 100)] public int baseDamping = 35;        // resists fast spin
    [Range(0, 100)] public int staticFriction = 12;     // prevents 1-finger spin
    [Range(0, 100)] public int centeringStrength = 22;  // return to center
    [Range(0, 100)] public int highSpeedCentering = 55; // stronger when moving
    [Range(0, 100)] public int angleResistance = 28;    // heavy at lock

    // ================= EVENTS =================
    [Header("Events")]
    [Range(0, 100)] public int gearKickStrength = 30;
    [Range(0, 100)] public int hydraulicStrength = 35;
    [Range(0, 100)] public int bumpStrength = 40;
    [Range(0, 100)] public int maxCollisionForce = 55;

    ForkliftAudioManager audioManager;

    // ================= INTERNAL =================
    float lastBumpTime;
    float lastImpactTime;
    int lastGearState;

    float lastSuspensionForceL;
    float lastSuspensionForceR;

    bool eventActive;

    Collider lastCollider;
    float contactStartTime = -1f;

    void Start()
    {
        audioManager = GetComponent<ForkliftAudioManager>();
    }

    void Update()
    {
        if (vehicle == null) return;

        if (!eventActive)
            ApplySteeringRealistic();

        ApplyGearKick();
        ApplyHydraulic();
    }

    void FixedUpdate()
    {
        ApplyWheelBumps();
    }

    // ================= STEERING (FIXED) =================
    void ApplySteeringRealistic()
    {
        float speed = vehicle.CurrentSpeed;
        float speed01 = Mathf.Clamp01(speed / 4.5f);

        float steer = vehicle.SteeringValue;      // -1 .. +1
        float absSteer = Mathf.Abs(steer);

        // ---- 1. DAMPING (spin resistance) ----
        int damper = Mathf.RoundToInt(
            Mathf.Lerp(baseDamping, baseDamping + 20, speed01));
        LogiPlayDamperForce(0, damper);

        // ---- 2. STATIC FRICTION (always-on resistance) ----
        int staticForce = Mathf.RoundToInt(staticFriction * Mathf.Sign(steer));
        if (absSteer < 0.02f)
            staticForce = 0;

        // ---- 3. ANGLE-BASED RESISTANCE (heavy near lock) ----
        float angleForce =
            absSteer * absSteer * angleResistance * Mathf.Sign(steer);

        // ---- 4. CENTERING (spring) ----
        int springStrength = Mathf.RoundToInt(
            Mathf.Lerp(centeringStrength, highSpeedCentering, speed01));

        LogiPlaySpringForce(0, 0, springStrength, 100);

        // ---- 5. COMBINE CONSTANT TORQUES ----
        int constant =
            Mathf.RoundToInt(-angleForce - staticForce);

        constant = Mathf.Clamp(constant, -100, 100);
        LogiPlayConstantForce(0, constant);
    }

    // ================= GEAR KICK =================
    void ApplyGearKick()
    {
        int gear = vehicle.GearState;
        if (gear == lastGearState) return;

        int direction = gear >= 0 ? 1 : -1;
        int kick = Mathf.RoundToInt(gearKickStrength * 0.6f) * direction;
        kick = Mathf.Clamp(kick, -60, 60);

        PlayEventForce(() =>
            LogiPlayConstantForce(0, kick),
            0.07f);

        lastGearState = gear;
    }

    // ================= HYDRAULIC =================
    void ApplyHydraulic()
    {
        if (forkController == null || eventActive) return;

        float intent =
            Mathf.Max(
                Mathf.Abs(forkController.MastInput),
                Mathf.Abs(forkController.tiltInput));

        if (intent < 0.05f) return;

        float pulse = Mathf.Abs(Mathf.Sin(Time.time * 5f));
        int strength = Mathf.RoundToInt(
            hydraulicStrength * (0.6f + pulse * 0.4f));

        strength = Mathf.Clamp(strength, 20, 70);

        PlayEventForce(() =>
            LogiPlaySurfaceEffect(
                0,
                LOGI_PERIODICTYPE_SQUARE,
                strength,
                100),
            0.1f);
    }

    // ================= BUMPS =================
    void ApplyWheelBumps()
    {
        if (eventActive) return;
        if (vehicle.CurrentSpeed < 0.8f) return;

        float deltaForce = 0f;

        if (vehicle.driveLeft.GetGroundHit(out WheelHit l))
        {
            float df = l.force - lastSuspensionForceL;
            lastSuspensionForceL = l.force;
            deltaForce = Mathf.Max(deltaForce, df);
        }

        if (vehicle.driveRight.GetGroundHit(out WheelHit r))
        {
            float df = r.force - lastSuspensionForceR;
            lastSuspensionForceR = r.force;
            deltaForce = Mathf.Max(deltaForce, df);
        }

        if (Time.time - lastBumpTime < 0.35f) return;
        if (deltaForce < 4200f) return;

        lastBumpTime = Time.time;

        float strength01 = Mathf.Clamp01(Mathf.Pow(deltaForce / 9000f, 0.5f));
        int bump = Mathf.RoundToInt(strength01 * bumpStrength);
        bump = Mathf.Clamp(bump, 8, 55);

        PlayEventForce(() =>
            LogiPlayBumpyRoadEffect(0, bump),
            0.08f);

        if (audioManager != null)
            audioManager.PlayBumpSound(strength01 * 0.6f);
    }

    // ================= COLLISION =================
    void OnCollisionEnter(Collision col)
    {
        if (Time.time - lastImpactTime < 0.5f) return;

        ContactPoint cp = col.contacts[0];
        Vector3 n = cp.normal;

        Vector3 v = col.relativeVelocity;
        float normalSpeed = Mathf.Max(0f, Vector3.Dot(v, -n));
        if (normalSpeed < 1.4f) return;

        if (lastCollider != col.collider)
        {
            lastCollider = col.collider;
            contactStartTime = Time.time;
            return;
        }

        if (Time.time - contactStartTime > 0.07f) return;

        lastImpactTime = Time.time;

        float impulse = vehicle.rb.mass * normalSpeed;
        if (impulse < 450f) return;

        float stiffness = 1f;
        if (col.collider.CompareTag("Rack")) stiffness = 0.75f;
        else if (col.collider.CompareTag("Pallet")) stiffness = 0.35f;
        else if (col.collider.CompareTag("Load")) stiffness = 0.25f;

        impulse *= stiffness;

        float strength01 = Mathf.Clamp01(Mathf.Pow(impulse / 1800f, 0.75f));
        int force = Mathf.RoundToInt(strength01 * maxCollisionForce);
        if (force < 10) return;

        Vector3 local = transform.InverseTransformDirection(-n);
        int torque = 0;

        if (local.z > 0.6f) torque = -force;
        else if (local.z < -0.6f) torque = force;
        else if (local.x > 0.5f) torque = -force;
        else if (local.x < -0.5f) torque = force;
        else return;

        PlayEventForce(() =>
            LogiPlayConstantForce(0, torque),
            0.15f);

        if (audioManager != null)
            audioManager.PlayImpactSound(strength01 * 0.5f);
    }

    // ================= EVENT CORE =================
    void PlayEventForce(System.Action playFunc, float duration)
    {
        if (eventActive) return;

        eventActive = true;

        LogiStopConstantForce(0);
        LogiStopSurfaceEffect(0);
        LogiStopDirtRoadEffect(0);
        LogiStopBumpyRoadEffect(0);

        playFunc.Invoke();
        Invoke(nameof(ClearEventForce), duration);
    }

    void ClearEventForce()
    {
        eventActive = false;
        LogiStopBumpyRoadEffect(0);
        LogiStopConstantForce(0);
        LogiStopSurfaceEffect(0);
    }
}
