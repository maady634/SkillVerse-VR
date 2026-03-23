using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runs a single performance trial: pick a load and place it at a target.
/// This variant is instrumented with extensive debug logging and gizmos to help
/// diagnose whether the evaluator sees a pick, sees a drop, and measures placement.
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

    [Header("UI")]
    public TextMeshProUGUI timerText;

    float timerStartTime;
    float timerValue;
    bool timerRunning = false;


    [Header("Optional Behavior")]
    [Tooltip("If true, calls StageManager.Instance.StepCompleted() when trial completes successfully.")]
    public bool completeStageOnSuccess = true;

    [Header("Debug")]
    [Tooltip("When true, prints detailed debug messages (very verbose).")]
    public bool verboseLogs = false;

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
    [Tooltip("Seconds between metric samples (lower -> more samples)")]
    public float samplingInterval = 0.05f;
    float nextSample = 0f;

    // smoothness metric helper (jerk)
    float lastAccel = 0f;
    float sumAbsJerk = 0f;

    // flags
    bool trialRunning = false;

    public static PerformanceEvaluator Instance { get; private set; }
    void Awake()
    {
        ResetInternal();
    }
    private void Start()
    {
         if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }
    void ResetInternal()
    {
        timerRunning = false;
        timerValue = 0f;

        if (timerText != null)
            timerText.text = "Time : 00:00";
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
        lastVehiclePos = Vector3.zero;
        lastVelocity = Vector3.zero;
        lastAccel = 0f;
        if (collisionTracker != null) collisionTracker.ResetTracker();
    }

    void Update()
    {
        if (!trialRunning) return;

        UpdateTimerUI();

        float t = Time.time;

        // timeout
        if (t - trialStartTime > maxTrialTime)
        {
            FailTrial("Timeout");
            return;
        }

        // detect pick/detach using reflection-first approach with fallbacks.
        bool hasLoad = false;
        bool checkedVehicleField = false;
        float debugCurrentLoadKg = -1f;

        if (vehicle != null)
        {
            try
            {
                var vType = vehicle.GetType();
                var currLoadField = vType.GetField("currentLoadKg");
                if (currLoadField != null)
                {
                    object val = currLoadField.GetValue(vehicle);
                    if (val is float)
                    {
                        debugCurrentLoadKg = (float)val;
                        hasLoad = debugCurrentLoadKg > 0.0001f;
                        checkedVehicleField = true;
                    }
                    else
                    {
                        // if field exists but isn't float, still treat as unknown
                        if (verboseLogs)
                            Debug.LogWarning($"[PE DEBUG] currentLoadKg field exists but is type {val?.GetType().Name ?? "null"}");
                    }
                }

                if (!checkedVehicleField)
                {
                    // fallback to ForkliftHolderController
                    hasLoad = forkController != null && forkController.ForksAreUnderLoad();
                }
            }
            catch (System.Exception ex)
            {
                if (verboseLogs) Debug.LogWarning($"[PE DEBUG] Exception while reading currentLoadKg: {ex.Message}");
                // Fail-safe fallback
                hasLoad = forkController != null && forkController.ForksAreUnderLoad();
            }
        }
        else
        {
            // If no vehicle reference, try forkController info
            hasLoad = forkController != null && forkController.ForksAreUnderLoad();
        }

        // extra debug: show forkController state if available
        if (forkController != null && verboseLogs)
        {
            bool forksUnder = forkController.ForksAreUnderLoad();
            Debug.Log($"[PE DEBUG] forkController.ForksAreUnderLoad() => {forksUnder}");
        }

        // print a debug snapshot each frame while trial running
        DebugState(hasLoad, debugCurrentLoadKg);

        // phase transitions
        if (phase == TrialPhase.Started)
        {
            if (hasLoad)
            {
                pickTime = Time.time - trialStartTime;
                phase = TrialPhase.Picked;
                timerStartTime = Time.time;
                timerRunning = true;
                if (verboseLogs) Debug.Log($"[PerformanceEvaluator] Pick detected at {pickTime:F2}s");
            }
        }
        else if (phase == TrialPhase.Picked)
        {
            if (!hasLoad)
            {
                if (verboseLogs) Debug.Log("[PE DEBUG] Load detached detected.");

                if (loadObject != null && targetPlacement != null)
                {
                    float dist = Vector3.Distance(loadObject.position, targetPlacement.position);

                    if (verboseLogs) Debug.Log($"[PE DEBUG] Placement Distance: {dist:F3} | Required <= {placementRadius:F3}");

                    if (dist <= placementRadius)
                    {
                        if (verboseLogs) Debug.Log("[PE DEBUG] Placement VALID → Completing Trial");
                        timerRunning = false;
                        placeTime = Time.time - trialStartTime;
                        pickToPlaceDuration = placeTime - pickTime;
                        phase = TrialPhase.Placed;
                        CompleteTrialSuccess();
                    }
                    else
                    {
                        if (verboseLogs) Debug.LogWarning("[PE DEBUG] Placement INVALID → Failing Trial");
                        FailTrial("Load dropped outside target zone");
                    }
                }
                else
                {
                    if (verboseLogs) Debug.LogWarning("[PE DEBUG] Missing loadObject or targetPlacement reference; using fallback success.");
                    // fallback if no placement references
                    placeTime = Time.time - trialStartTime;
                    pickToPlaceDuration = placeTime - pickTime;
                    phase = TrialPhase.Placed;
                    CompleteTrialSuccess();
                }
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
    void UpdateTimerUI()
    {
        if (!timerRunning || timerText == null) return;

        timerValue = Time.time - timerStartTime;

        int minutes = Mathf.FloorToInt(timerValue / 60f);
        int seconds = Mathf.FloorToInt(timerValue % 60f);

        timerText.text = $"Time : {minutes:00}:{seconds:00}";
    }
    void SampleMetrics()
    {
        // path length and speeds
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
    /// <summary>
    /// Start the trial. If autoStartOnCommand==false then external control must call this when ready.
    /// </summary>
    public void StartTrial()
    {
        if (trialRunning)
        {
            if (verboseLogs) Debug.LogWarning("[PerformanceEvaluator] StartTrial called but a trial is already running.");
            return;
        }

        ResetInternal();
        trialStartTime = Time.time;
        phase = TrialPhase.Started;
        trialRunning = true;
        nextSample = Time.time + samplingInterval;
        if (collisionTracker != null) collisionTracker.ResetTracker();
        if (verboseLogs) Debug.Log("[PerformanceEvaluator] Trial started.");
        OnTrialStarted?.Invoke();
    }

    /// <summary>
    /// Abort the current running trial.
    /// </summary>
    public void AbortTrial(string reason = "Aborted")
    {
        if (!trialRunning)
        {
            if (verboseLogs) Debug.LogWarning("[PerformanceEvaluator] AbortTrial called but no trial is running.");
            return;
        }
        FailTrial(reason);
    }

    /// <summary>
    /// Force-complete trial successfully (useful for testing).
    /// </summary>
    public void ForceCompleteSuccess()
    {
        if (!trialRunning)
        {
            if (verboseLogs) Debug.LogWarning("[PerformanceEvaluator] ForceCompleteSuccess called but no trial is running.");
            return;
        }

        // mark placeTime as current time relative to start
        placeTime = Time.time - trialStartTime;
        if (pickTime < 0f) pickTime = 0f;
        pickToPlaceDuration = placeTime - pickTime;
        CompleteTrialSuccess();
    }

   public void CompleteTrialSuccess()
    {
        trialRunning = false;
        phase = TrialPhase.Completed;
        ComputeResult(true, null);
        if (verboseLogs) Debug.Log($"[PerformanceEvaluator] Trial completed successfully. Score={LastResult?.finalScore}");
        if (verboseLogs) Debug.Log($"[PE DEBUG] Result placementDistance={LastResult?.placementDistance:F3} placementSuccess={LastResult?.placementSuccess}");
        OnTrialCompleted?.Invoke();
        if (completeStageOnSuccess && StageManager.Instance != null)
            StageManager.Instance.TrainingScore = GetLastResult().finalScore;
            StageManager.Instance.StepCompleted();
    }

    void FailTrial(string reason)
    {
        trialRunning = false;
        phase = TrialPhase.Failed;
        ComputeResult(false, reason);
        if (verboseLogs) Debug.LogWarning($"[PerformanceEvaluator] Trial failed: {reason}. Score={LastResult?.finalScore}");
        if (verboseLogs) Debug.Log($"[PE DEBUG] Result placementDistance={LastResult?.placementDistance:F3} placementSuccess={LastResult?.placementSuccess}");
        OnTrialFailed?.Invoke();
    }

    /// <summary>
    /// Returns the last computed result (after completion/fail).
    /// </summary>
    public PerformanceResult GetLastResult()
    {
        return LastResult;
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
        // Convert each metric to 0–100
        float timeScore100 = timeScore * 100f;
        float placementScore100 = placementScore * 100f;
        float tiltScore100 = tiltScore * 100f;
        float collisionScore100 = collisionScore * 100f;
        float smoothnessScore100 = smoothnessScore * 100f;

        // Normalize weights so they represent percentage contribution
        float totalWeight = weightTime + weightPlacement + weightTilt + weightCollisions + weightSmoothness;
        if (totalWeight <= 0f) totalWeight = 1f;

        float timeWeightPercent = weightTime / totalWeight;
        float placementWeightPercent = weightPlacement / totalWeight;
        float tiltWeightPercent = weightTilt / totalWeight;
        float collisionWeightPercent = weightCollisions / totalWeight;
        float smoothnessWeightPercent = weightSmoothness / totalWeight;

        // Final combined score out of 100
        float finalCombinedScore =
            (timeScore100 * timeWeightPercent) +
            (placementScore100 * placementWeightPercent) +
            (tiltScore100 * tiltWeightPercent) +
            (collisionScore100 * collisionWeightPercent) +
            (smoothnessScore100 * smoothnessWeightPercent);

        r.finalScore = Mathf.Clamp(Mathf.RoundToInt(finalCombinedScore), 0, 100);
        LastResult = r;
    }

    float Mean(List<float> list)
    {
        if (list.Count == 0) return 0f;
        float s = 0f;
        for (int i = 0; i < list.Count; ++i) s += list[i];
        return s / list.Count;
    }

    /// <summary>
    /// Prints a compact snapshot of internal state when verboseLogs == true.
    /// </summary>
    void DebugState(bool hasLoad, float currentLoadKg)
    {
        if (!verboseLogs) return;

        float dist = -1f;
        if (loadObject != null && targetPlacement != null)
            dist = Vector3.Distance(loadObject.position, targetPlacement.position);

        string currLoadStr = (currentLoadKg >= 0f) ? $"{currentLoadKg:F3}" : "n/a";
        Debug.Log($"[PE DEBUG] Phase:{phase} | HasLoad:{hasLoad} | currentLoadKg:{currLoadStr} | PickTime:{pickTime:F2} | PlaceTime:{placeTime:F2} | Distance:{dist:F3} | Radius:{placementRadius:F3} | Samples:{speedSamples.Count}");
    }

    // Visual debug helpers for the editor
    void OnDrawGizmosSelected()
    {
        if (targetPlacement != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
            Gizmos.DrawWireSphere(targetPlacement.position, placementRadius);
        }

        if (loadObject != null && targetPlacement != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(loadObject.position, targetPlacement.position);
        }

        // draw last vehicle position path indicator (small sphere)
        if (Application.isPlaying && lastVehiclePos != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(lastVehiclePos, 0.02f);
        }
    }
}

/// <summary>
/// Result container with breakdown and raw metrics.
/// Kept the same shape you used previously.
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