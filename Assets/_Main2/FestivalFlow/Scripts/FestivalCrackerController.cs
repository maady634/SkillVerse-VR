using TMPro;
using UnityEngine;

/// <summary>
/// Drives the interactive half of the festival flow:
///   step "pick up the lighter"  - completes as soon as the lighter is stuck to a hand
///   step "light the crackers"   - completes once all four crackers have been lit
/// Completion is reported to <see cref="StageManager"/>, which then brings up the restart panel.
/// Set up by Tools > SkillVerse > Festival (FestivalInteractionBuilder).
/// </summary>
public class FestivalCrackerController : MonoBehaviour
{
    [Header("Scene references")]
    public LighterGrab lighter;
    public CrackerTarget[] targets;

    [Header("Stage hookup")]
    [Tooltip("Index of the 'pick up the lighter' step inside StageManager.StageSOs.")]
    public int pickupStepIndex = 1;
    [Tooltip("Index of the 'light the crackers' step inside StageManager.StageSOs.")]
    public int igniteStepIndex = 2;
    [Tooltip("Seconds to let the last cracker finish before the restart panel appears.")]
    public float completionDelay = 3.5f;

    [Header("Help arrow")]
    [Tooltip("Arrow that points at the lighter. Shown while the pick-up step is running and " +
             "hidden for good the moment the lighter is in a hand.")]
    public GameObject helpArrow;

    [Header("Ignition")]
    [Tooltip("Light crackers with any part of the lighter. Turn off to require the burning tip.")]
    public bool wholeLighterLights = true;

    [Header("Feedback")]
    [Tooltip("Write a 'x of y lit' counter into the HUD subtitle while the step runs.")]
    public bool updateSubtitle = true;
    public string progressFormat = "{0} of {1} crackers lit.";

    private int ignitedCount;
    private bool pickupPending;
    private bool pickupReported;
    private bool ignitePending;
    private bool igniteReported;
    private float igniteReadyAt;

    private void OnEnable()
    {
        if (lighter != null) lighter.OnPickedUp.AddListener(HandlePickedUp);

        if (targets == null) return;
        foreach (var target in targets)
            if (target != null) target.OnIgnited.AddListener(HandleIgnited);
    }

    private void OnDisable()
    {
        if (lighter != null) lighter.OnPickedUp.RemoveListener(HandlePickedUp);

        if (targets == null) return;
        foreach (var target in targets)
            if (target != null) target.OnIgnited.RemoveListener(HandleIgnited);
    }

    private void Start()
    {
        // Both of these belong to the pick-up step only, so they start off whatever the scene was
        // saved with: no arrow, and a lighter that cannot be grabbed during the welcome step.
        SetHelpArrow(false);
        if (lighter != null) lighter.SetGrabbable(false);
    }

    private void Update()
    {
        ReportPendingSteps();
        UpdatePickupStep();
        CheckForIgnitions();
    }

    /// <summary>
    /// Opens the pick-up step: the hint arrow appears and the lighter becomes grabbable, both only
    /// while that step is the current one and the lighter is still on the table. Gating the grab
    /// keeps the player from taking the lighter during the welcome step and running ahead of the
    /// narration. The arrow sits at the scene root above the lighter's resting spot, so leaving it
    /// on afterwards would hang it in mid-air pointing at nothing.
    /// </summary>
    private void UpdatePickupStep()
    {
        var stage = StageManager.Instance;
        if (stage == null) return;

        bool wanted = lighter != null && !lighter.IsHeld && stage.currentStepIndex == pickupStepIndex;

        if (helpArrow != null && helpArrow.activeSelf != wanted) SetHelpArrow(wanted);
        if (lighter != null) lighter.SetGrabbable(wanted);
    }

    private void SetHelpArrow(bool on)
    {
        if (helpArrow != null) helpArrow.SetActive(on);
    }

    /// <summary>Lights any cracker the lighter is currently touching.</summary>
    private void CheckForIgnitions()
    {
        if (lighter == null || !lighter.IsHeld || targets == null) return;

        Vector3 tip = lighter.Tip.position;
        // Sampling the whole stick, not just the tip, matches what the player sees. The lighter
        // is 1.4m long, so a tip-only test makes it look like you are touching a cracker while
        // the point being measured is nowhere near it.
        Vector3 from = wholeLighterLights ? lighter.transform.position : tip;

        foreach (var target in targets)
        {
            if (target == null || target.Ignited) continue;

            if (target.IsTouchedBy(from, tip))
                target.Ignite();
        }
    }

    /// <summary>
    /// StageManager only accepts a completion while the matching step is the current one, so a
    /// player who grabs the lighter early is held until the step actually starts.
    /// </summary>
    private void ReportPendingSteps()
    {
        var stage = StageManager.Instance;
        if (stage == null) return;

        if (pickupPending && !pickupReported && stage.currentStepIndex == pickupStepIndex)
        {
            pickupPending = false;
            pickupReported = true;
            stage.StepCompleted();
        }

        if (ignitePending && !igniteReported && Time.time >= igniteReadyAt &&
            stage.currentStepIndex == igniteStepIndex)
        {
            ignitePending = false;
            igniteReported = true;
            stage.StepCompleted();
        }
    }

    private void HandlePickedUp()
    {
        pickupPending = true;
    }

    private void HandleIgnited()
    {
        ignitedCount++;
        ShowProgress();

        if (targets == null || ignitedCount < targets.Length) return;

        ignitePending = true;
        igniteReadyAt = Time.time + completionDelay;
    }

    private void ShowProgress()
    {
        if (!updateSubtitle) return;

        TextMeshProUGUI subtitle = StageManager.Instance != null ? StageManager.Instance.subtitle : null;
        if (subtitle == null || targets == null) return;

        subtitle.text = string.Format(progressFormat, ignitedCount, targets.Length);
    }
}
