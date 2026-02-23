using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Runs a single performance trial: pick a load and place it at a target.
/// Tracks metrics and computes a score at the end.
/// </summary>
public class PerformanceEvaluator : MonoBehaviour
{
    [Header("References")]
    public G29VehicleInput vehicle;
    public ForkliftHolderController forkController;
    [Tooltip("Required: target transform where the load should be placed.")]
    public Transform targetPlacement;
    [Tooltip("Optional: the specific load GameObject to track placement accuracy.")]
    public Transform loadObject;
    [Tooltip("Optional: attach CollisionTracker (recommended).")]
    public CollisionTracker collisionTracker;

    [Header("Trial Settings")]
    public float maxTrialTime = 180f;
    [Tooltip("Radius to count as successful placement (meters).")]
    public float placementRadius = 0.6f;
    [Tooltip("If true, trial auto-starts when StartTrial() is called; otherwise external StartTrialControl must call StartTrial().")]
    public bool autoStartOnCommand = true;

    [Header("Scoring Weights (sum not required)")]
    [Range(0f, 1f)] public float weightTime = 0.25f;
    [Range(0f, 1f)] public float weightPlacement = 0.35f;
    [Range(0f, 1f)] public float weightTilt = 0.15f;
    [Range(0f, 1f)] public float weightCollisions = 0.15f;
    [Range(0f, 1f)] public float weightSmoothness = 0.1f;

    [Header("Scoring Thresholds")]
    public float idealPickTime = 15f;      // less is better
    public float idealPlaceTime = 20f;     // from pick -> place
    public float maxAcceptablePlaceTime = 120f;
    public float allowedPlacementRadius = 0.6f; // for placement score normalization
    public float maxAllowableTilt = 12f;      // degrees (tilt while carrying)
    public int collisionTolerance = 0;         // collisions allowed with no penalty
    public float maxCollisionImpulseForPenalty = 5f;

    [Header("Events")]
    public UnityEvent OnTrialStarted;
    public UnityEvent OnTrialCompleted;
    public UnityEvent OnTrialFailed;

    [Header("Optional Behavior")]
    [Tooltip("If true, calls StageManager.Instance.StepCompleted() when trial completes successfully.")]
    public bool completeStageOnSuccess = true;

    // live state
    public PerformanceResult LastResult { get; private set; }

    enum TrialPhase { Idle, Started, Picked, Placed, Completed, Failed }
    TrialPhase phase = TrialPhase.Idle;

    // runtime tracking
    float trialStartTime;
    float pickTime = -1f;
    float placeTime = -1f;
    float pickToPlaceDuration = -1f;

    Vector3 lastVehiclePos;
    float pathLengthWhileCarrying;
    List<float> speedSamples = new List<float>();
    List<float> accelSamples = new List<float>();
    Vector3 lastVelocity;
    float maxTiltDuringCarry = 0f;

    // sampling frequency control
    public float samplingInterval = 0.05f;
    float nextSample = 0f;

    // smoothness metric helper (jerk)
    float lastAccel = 0f;
    float sumAbsJerk = 0f;

    // flags
    bool trialRunning = false;

    void Awake()
    {
        ResetInternal();
    }

    void ResetInternal()
    {
        phase = TrialPhase.Idle;
        trialRunning = false;
        trialStartTime = 0f;
        pickTime = -1f;
        placeTime = -1f;
        pickToPlaceDuration = -1f;
        pathLengthWhileCarrying = 0f;
        speedSamples.Clear();
        accelSamples.Clear();
        sumAbsJerk = 0f;
        maxTiltDuringCarry = 0f;
        nextSample = 0f;
        LastResult = null;
        if (collisionTracker != null) collisionTracker.ResetTracker();
    }

    void Update()
    {
        if (!trialRunning) return;

        float t = Time.time;

        // timeout
        if (t - trialStartTime > maxTrialTime)
        {
            FailTrial("Timeout");
            return;
        }

        // detect pick: when vehicle.currentLoadKg > 0 (vehicle exposes currentLoadKg)
        bool carrying = vehicle != null && vehicle.AcceleratorValue >= -1f /* harmless */ ? vehicle.CurrentMastHeight >= 0f || vehicle.AcceleratorValue > -1f : false;
        // better detection: check vehicle.currentLoadKg if available
        bool hasLoad = false;
        try
        {
            // try reflection-free: vehicle has public currentLoadKg in your earlier script
            var vType = vehicle.GetType();
            var field = vType.GetField("currentLoadKg");
            if (field != null)
            {
                float cur = (float)field.GetValue(vehicle);
                hasLoad = cur > 0.0001f;
            }
            else
            {
                // fallback: ask forkController (ForksAreUnderLoad)
                hasLoad = forkController != null && forkController.ForksAreUnderLoad();
            }
        }
        catch
        {
            hasLoad = forkController != null && forkController.ForksAreUnderLoad();
        }

        // phase transitions
        if (phase == TrialPhase.Started)
        {
            if (hasLoad)
            {
                pickTime = Time.time - trialStartTime;
                phase = TrialPhase.Picked;
            }
        }
        else if (phase == TrialPhase.Picked)
        {
            // detect detach: currentLoadKg becomes zero after being >0
            if (!hasLoad)
            {
                placeTime = Time.time - trialStartTime;
                pickToPlaceDuration = placeTime - pickTime;
                phase = TrialPhase.Placed;
                CompleteTrialSuccess();
            }
        }

        // sampling for metrics while carrying
        if (phase == TrialPhase.Picked)
        {
            if (Time.time >= nextSample)
            {
                nextSample = Time.time + samplingInterval;
                SampleMetrics();
            }
        }
    }

    void SampleMetrics()
    {
        // path length
        if (vehicle != null && vehicle.rb != null)
        {
            Vector3 pos = vehicle.rb.transform.position;
            if (lastVehiclePos != Vector3.zero)
                pathLengthWhileCarrying += Vector3.Distance(pos, lastVehiclePos);
            lastVehiclePos = pos;

            // speed & accel
            Vector3 vel = vehicle.rb.velocity;
            float speed = vel.magnitude;
            speedSamples.Add(speed);

            float accel = 0f;
            if (lastVelocity != Vector3.zero)
            {
                accel = (vel - lastVelocity).magnitude / samplingInterval;
                accelSamples.Add(accel);
            }
            // jerk
            float jerk = Mathf.Abs(accel - lastAccel) / samplingInterval;
            sumAbsJerk += jerk;
            lastAccel = accel;
            lastVelocity = vel;
        }

        // tilt
        if (forkController != null)
        {
            float t = Mathf.Abs(forkController.CurrentMastTilt);
            if (t > maxTiltDuringCarry) maxTiltDuringCarry = t;
        }
    }

    // public API
    public void StartTrial()
    {
        ResetInternal();
        trialStartTime = Time.time;
        phase = TrialPhase.Started;
        trialRunning = true;
        nextSample = Time.time + samplingInterval;
        if (collisionTracker != null) collisionTracker.ResetTracker();
        OnTrialStarted?.Invoke();
    }

    public void AbortTrial(string reason = "Aborted")
    {
        if (!trialRunning) return;
        FailTrial(reason);
    }

    void CompleteTrialSuccess()
    {
        trialRunning = false;
        phase = TrialPhase.Completed;
        ComputeResult(true, null);
        OnTrialCompleted?.Invoke();
        if (completeStageOnSuccess && StageManager.Instance != null)
            StageManager.Instance.StepCompleted();
    }

    void FailTrial(string reason)
    {
        trialRunning = false;
        phase = TrialPhase.Failed;
        ComputeResult(false, reason);
        OnTrialFailed?.Invoke();
    }

    void ComputeResult(bool success, string failReason)
    {
        PerformanceResult r = new PerformanceResult();
        r.success = success;
        r.failReason = failReason;
        r.trialStart = trialStartTime;
        r.pickTime = pickTime;
        r.placeTime = placeTime;
        r.pickToPlaceDuration = pickToPlaceDuration;
        r.pathLength = pathLengthWhileCarrying;
        r.maxTiltDuringCarry = maxTiltDuringCarry;
        r.collisionCount = collisionTracker != null ? collisionTracker.collisionCount : 0;
        r.maxCollisionImpulse = collisionTracker != null ? collisionTracker.maxImpulse : 0f;
        r.totalSamples = speedSamples.Count;
        r.meanSpeed = speedSamples.Count > 0 ? Mean(speedSamples) : 0f;
        r.meanAccel = accelSamples.Count > 0 ? Mean(accelSamples) : 0f;
        r.smoothnessJerk = sumAbsJerk;

        // compute placement accuracy if loadObject provided
        if (loadObject != null && targetPlacement != null)
        {
            r.placementDistance = Vector3.Distance(loadObject.position, targetPlacement.position);
            r.placementSuccess = r.placementDistance <= placementRadius;
        }
        else
        {
            r.placementDistance = -1f;
            r.placementSuccess = (pickToPlaceDuration > 0 && r.collisionCount == 0); // fallback heuristic
        }

        // compute per-metric normalized scores [0..1]
        float timeScore = 0f;
        if (r.placeTime > 0f)
        {
            float totalTime = r.placeTime;
            // normalized: ideal place time -> 1, maxAcceptablePlaceTime -> 0
            timeScore = 1f - Mathf.Clamp01((totalTime - idealPlaceTime) / (maxAcceptablePlaceTime - idealPlaceTime));
        }
        else if (r.pickTime > 0f)
        {
            float totalTime = r.pickTime;
            timeScore = 1f - Mathf.Clamp01((totalTime - idealPickTime) / (maxAcceptablePlaceTime - idealPickTime));
        }
        else
        {
            timeScore = 0f;
        }

        float placementScore = 0f;
        if (r.placementDistance >= 0f)
            placementScore = 1f - Mathf.Clamp01(r.placementDistance / allowedPlacementRadius);

        float tiltScore = 1f - Mathf.Clamp01(r.maxTiltDuringCarry / maxAllowableTilt);

        float collisionScore = 1f;
        if (r.collisionCount <= collisionTolerance) collisionScore = 1f;
        else
        {
            // penalize linearly with collisions and impulse
            float c = Mathf.Clamp01((r.collisionCount - collisionTolerance) / 5f);
            float impulseFactor = Mathf.Clamp01(r.maxCollisionImpulse / maxCollisionImpulseForPenalty);
            collisionScore = 1f - Mathf.Clamp01(0.6f * c + 0.4f * impulseFactor);
        }

        // smoothness: lower jerk -> higher score. Normalize by an empirical value
        float smoothnessNorm = 1f;
        if (r.smoothnessJerk > 0f)
            smoothnessNorm = 1f - Mathf.Clamp01(r.smoothnessJerk / 300f);
        float smoothnessScore = smoothnessNorm;

        // final weighted score
        float totalWeight = weightTime + weightPlacement + weightTilt + weightCollisions + weightSmoothness;
        if (totalWeight <= 0f) totalWeight = 1f;

        float weighted = (timeScore * weightTime
                        + placementScore * weightPlacement
                        + tiltScore * weightTilt
                        + collisionScore * weightCollisions
                        + smoothnessScore * weightSmoothness) / totalWeight;

        r.finalScore = Mathf.RoundToInt(weighted * 100f);
        r.breakdown = new PerformanceBreakdown()
        {
            timeScore = Mathf.RoundToInt(timeScore * 100f),
            placementScore = Mathf.RoundToInt(placementScore * 100f),
            tiltScore = Mathf.RoundToInt(tiltScore * 100f),
            collisionScore = Mathf.RoundToInt(collisionScore * 100f),
            smoothnessScore = Mathf.RoundToInt(smoothnessScore * 100f),
        };

        LastResult = r;
    }

    float Mean(List<float> list)
    {
        if (list.Count == 0) return 0f;
        float s = 0f;
        for (int i = 0; i < list.Count; ++i) s += list[i];
        return s / list.Count;
    }
}

/// <summary>
/// Result container with breakdown and raw metrics.
/// </summary>
public class PerformanceResult
{
    public bool success;
    public string failReason;

    public float trialStart;
    public float pickTime;
    public float placeTime;
    public float pickToPlaceDuration;

    public float pathLength;
    public float maxTiltDuringCarry;
    public int collisionCount;
    public float maxCollisionImpulse;

    public int totalSamples;
    public float meanSpeed;
    public float meanAccel;
    public float smoothnessJerk;

    public float placementDistance;
    public bool placementSuccess;

    public int finalScore; // 0..100
    public PerformanceBreakdown breakdown;
}

public class PerformanceBreakdown
{
    public int timeScore;
    public int placementScore;
    public int tiltScore;
    public int collisionScore;
    public int smoothnessScore;
}