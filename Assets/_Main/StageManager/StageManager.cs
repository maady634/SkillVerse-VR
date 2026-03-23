using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.ComponentModel;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public TextMeshProUGUI subtitle;
    public AudioSource AudioHandler;
    public Image FadeImage;

    public GameObject User;

    public int currentStepIndex = 0;
    public int completedStepIndex = 0;

    public StageSO[] StageSOs; // Assign in Inspector
    public Stages[] Stages; // Gameobjects for each step

    private bool isStepCompleted = true; // Flag to prevent next step from triggering prematurely
    public GameObject _CorrectIcon;
    public AudioClip _CorrectAudio;

    public bool EditorShortcut = false;
    private bool CompletionLock = true;

    public int AssessmentScore = 0;
    public int TrainingScore = 0;
    Coroutine currentAudioCoroutine;

    [ReadOnly(true)]
    GameObject PanelTemp;

    // Robust per-step guards
    bool[] stepHasStarted;
    bool[] stepHasCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Prevent duplicates
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        InitializeStepState();
        if (GameSession.SelectedMode == ModeType.Practice)
        {
            currentStepIndex = 13;
            completedStepIndex = 13;
        }
        else
        {
            currentStepIndex = 0;
            completedStepIndex = 0;
        }


        if (StageSOs != null && StageSOs.Length > 0)
        {
            StartNextStep();
        }
        else
        {
            Debug.LogError("No Stage Steps assigned in the Inspector.");
        }

        //_CorrectIcon = SensoryProfileControl.Instance.Selected;
    }

    void InitializeStepState()
    {
        int len = StageSOs != null ? StageSOs.Length : 0;
        stepHasStarted = new bool[len];
        stepHasCompleted = new bool[len];
    }

    private void Update()
    {
        /*if (!EditorShortcut)
        {
            return;
        }
        else
        {
            if (Keyboard.current.spaceKey.isPressed)
            {
                StepCompleted();
                CompletionLock = true;
                Debug.Log("Force Complete step!");
            }
        }*/
    }

    public void StartNextStep()
    {
        // Cancel any previously scheduled invokes (VERY IMPORTANT)
        CancelInvoke();

        // Stop any running audio coroutine
        if (currentAudioCoroutine != null)
        {
            StopCoroutine(currentAudioCoroutine);
            currentAudioCoroutine = null;
        }

        // Stop any currently playing audio
        if (AudioHandler != null)
        {
            AudioHandler.Stop();
        }

        // Basic bounds check
        if (StageSOs == null || Stages == null)
        {
            Debug.LogWarning("[StageManager] StartNextStep called but StageSOs or Stages is null.");
            return;
        }

        if (currentStepIndex >= StageSOs.Length)
        {
            Debug.Log("[StageManager] All steps have been completed.");
            return;
        }

        // Prevent re-starting the same step if it's already started
        if (stepHasStarted != null &&
            currentStepIndex < stepHasStarted.Length &&
            stepHasStarted[currentStepIndex])
        {
            Debug.LogWarning($"[StageManager] Step {currentStepIndex} already started. Ignoring duplicate call.");
            return;
        }

        // Preserve original gating behaviour
        if (!isStepCompleted)
        {
            Debug.LogWarning($"[StageManager] Previous step not marked completed. currentStepIndex={currentStepIndex}");
            return;
        }

        CompletionLock = false;
        isStepCompleted = false;

        StageSO currentStep = StageSOs[currentStepIndex];
        Stages currentStage = Stages[currentStepIndex];

        // Mark step as started
        if (stepHasStarted != null && currentStepIndex < stepHasStarted.Length)
            stepHasStarted[currentStepIndex] = true;

        if (currentStep == null)
            return;

        Debug.Log($"[StageManager] Starting step {currentStepIndex}: {currentStep.stepSubtitle}");

        // Subtitle
        if (subtitle != null)
            subtitle.text = currentStep.stepSubtitle;

        // Start event
        currentStage?.StartEvent?.Invoke();
        Invoke(nameof(DelayEventFunc), currentStage?.DelayEventTime ?? 0);

        // Position user
        if (currentStage?.userPosition != null)
        {
            User.transform.position = currentStage.userPosition.transform.position;
            User.transform.rotation = currentStage.userPosition.transform.rotation;
        }

        // UI Prefab
        if (currentStep.completionType == StageSO.CompletionType.UIandTrigger &&
            currentStep.prefab != null &&
            currentStage?.prefabPosition != null)
        {
            PanelTemp = Instantiate(
                currentStep.prefab
            );
        }

        // Play Step Audio SAFELY
        currentAudioCoroutine = StartCoroutine(
            PlayAudioWithCompletion(AudioHandler, currentStep.stepAudio, () =>
            {
                if (currentStep.completionType == StageSO.CompletionType.AfterAudio)
                {
                    CompletionLock = false;
                    StepCompleted();
                }
            })
        );

        // Secondary audio + help
        if (currentStepIndex == completedStepIndex)
        {
            if (currentStep.stepAudio2 != null)
            {
                Invoke(nameof(SecondSubtitle), currentStep.stepAudio.length);
            }

            if (currentStep.helpAudio != null)
            {
                Invoke(nameof(ShowHelp), currentStep.helpDelay);
            }
        }
    }

    private void SecondSubtitle()
    {
        if (StageSOs != null && currentStepIndex < StageSOs.Length)
        {
            StageSO currentStep = StageSOs[currentStepIndex];
            if (currentStep != null)
            {
                if (subtitle != null) subtitle.text = currentStep.stepSubtitle2;
                PlayAudio(AudioHandler, currentStep.stepAudio2);
            }
        }
    }

    private void ShowHelp()
    {
        if (StageSOs != null && currentStepIndex < StageSOs.Length)
        {
            StageSO currentStep = StageSOs[currentStepIndex];
            if (currentStep != null)
            {
                if (subtitle != null) subtitle.text = currentStep.helpText;
                PlayAudio(AudioHandler, currentStep.helpAudio);
            }
        }

        // keep original scoring logic
        if (StageSOs != null && StageSOs.Length > currentStepIndex && StageSOs[currentStepIndex].Score == true)
        {
            TrainingScore -= 10;
        }
    }

    private void ShowComplete()
    {
        if (StageSOs != null && currentStepIndex < StageSOs.Length)
        {
            StageSO currentStep = StageSOs[currentStepIndex];
            if (currentStep != null)
            {
                if (subtitle != null) subtitle.text = currentStep.completionText;
                PlayAudio(AudioHandler, currentStep.completionAudio);
            }
        }
    }

    private void PlayAudio(AudioSource audioSource, AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    private IEnumerator PlayAudioWithCompletion(AudioSource audioSource, AudioClip clip, System.Action onCompletion)
    {
        if (audioSource == null)
            yield break;

        // Stop previous audio immediately
        audioSource.Stop();

        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();

            yield return new WaitForSeconds(clip.length);
        }

        currentAudioCoroutine = null;
        onCompletion?.Invoke();
    }
    private void DelayEventFunc()
    {
        if (Stages != null && currentStepIndex < Stages.Length)
        {
            Stages currentStage = Stages[currentStepIndex];
            if (currentStage?.DelayEvent != null) currentStage.DelayEvent.Invoke();
        }
    }

    [ContextMenu("CompleteStep")]
    public void StepCompleted() // Call this from your code trigger if CompletionType is CodeTrigger
    {
        // Guard: bounds check
        if (StageSOs == null || Stages == null)
        {
            Debug.LogWarning("[StageManager] StepCompleted called but StageSOs or Stages is null.");
            return;
        }

        if (currentStepIndex >= StageSOs.Length)
        {
            Debug.LogWarning("[StageManager] StepCompleted called but currentStepIndex out of range.");
            return;
        }

        // Prevent duplicate completion for the same step
        if (stepHasCompleted != null && currentStepIndex < stepHasCompleted.Length && stepHasCompleted[currentStepIndex])
        {
            Debug.LogWarning($"[StageManager] StepCompleted: step {currentStepIndex} already completed — ignoring duplicate complete.");
            return;
        }

        // Only allow completion when not locked (preserve original CompletionLock semantics)
        if (CompletionLock)
        {
            Debug.LogWarning($"[StageManager] StepCompleted called but CompletionLock is true. Ignoring. currentStepIndex={currentStepIndex}");
            return;
        }

        // Mark as completed
        if (stepHasCompleted != null && currentStepIndex < stepHasCompleted.Length)
            stepHasCompleted[currentStepIndex] = true;

        // Execute completion
        isStepCompleted = true;
        StageSO currentStep = StageSOs[currentStepIndex];
        Stages[currentStepIndex]?.EndEvent?.Invoke();

        if (currentStep != null && currentStep.CorrectIcon == true)
        {
            CorrectIcon();
        }

        ShowComplete();

        // Preserve original completedStepIndex behavior: increment once on successful completion
        completedStepIndex++;

        // Destroy prefab panel if present
        if (PanelTemp != null)
        {
            Destroy(PanelTemp);
            PanelTemp = null;
        }

        // Advance index only after completion actions
        currentStepIndex++;

        Debug.Log("Step Completed : " + currentStepIndex);

        // Schedule next step if available
        if (currentStepIndex < Stages.Length)
        {
            Stages currentStage = Stages[currentStepIndex];
            StageSO nextStep = StageSOs[currentStepIndex];

            float delay = 0f;
            if (currentStep != null && currentStep.completionAudio != null)
            {
                delay += currentStep.completionAudio.length;
            }

            delay += (currentStage?.StepCompletionTime ?? 0);

            // Ensure we mark the state so StartNextStep can run
            CompletionLock = true; // match original behaviour
            // Set isStepCompleted true (so StartNextStep can run). This mirrors original flow where StepCompleted sets isStepCompleted = true.
            isStepCompleted = true;

            Invoke(nameof(StartNextStep), delay);
        }
        else
        {
            CompletionLock = true;
            Debug.Log("[StageManager] All steps finished.");
        }
    }

    public void StepJump(int index) // Call this from your code trigger if CompletionType is CodeTrigger
    {
        if (Stages != null && currentStepIndex < Stages.Length)
        {
            isStepCompleted = true;
            Stages[currentStepIndex]?.EndEvent?.Invoke();
            //CorrectIcon();
            currentStepIndex = index;

            if (PanelTemp != null)
            {
                Destroy(PanelTemp);
            }

            if (currentStepIndex < Stages.Length)
            {
                Stages currentStage = Stages[currentStepIndex];
                Invoke(nameof(StartNextStep), 2 + (currentStage?.StepCompletionTime ?? 0));
            }
            Debug.Log("Next step");
        }
    }
    public void CorrectIcon()
    {
        StartCoroutine("CorrectIconFunc");
    }

    public IEnumerator CorrectIconFunc()
    {
        if (_CorrectIcon != null)
        {
            _CorrectIcon.SetActive(true);
        }
        if (AudioHandler != null && _CorrectAudio != null)
        {
            AudioHandler.PlayOneShot(_CorrectAudio);
        }
        yield return new WaitForSeconds(2.5f);
        if (_CorrectIcon != null)
        {
            _CorrectIcon.SetActive(false);
        }
    }

    public void ReloadScene()
    {
        StartCoroutine(FadeAndReload());
    }

    private IEnumerator FadeAndReload()
    {
        float duration = 0.5f; // fade-in duration
        float t = 0f;

        Color startColor = FadeImage.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 1f); // fully opaque

        while (t < duration)
        {
            t += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(t / duration);
            FadeImage.color = Color.Lerp(startColor, endColor, normalizedTime);
            yield return null;
        }

        // Fully opaque
        FadeImage.color = endColor;

        // Reload scene
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}