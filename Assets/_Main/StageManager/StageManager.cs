using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public TextMeshProUGUI subtitle;
    public AudioSource AudioHandler;

    public StageSO[] StageSOs; // Assign in Inspector
    public Stages[] Stages; // Gameobjects for each step
    private int currentStepIndex = 0;
    private bool isStepCompleted = true; // Flag to prevent next step from triggering prematurely
    public GameObject _CorrectIcon;
    public AudioClip _CorrectAudio;

    public bool EditorShortcut = false;
    private bool CompletionLock = true;

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

    private void Update()
    {
        if (!EditorShortcut)
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
        }
    }
    
    public void StartNextStep()
    {
        if (StageSOs != null && currentStepIndex < StageSOs.Length && Stages != null && isStepCompleted)
        {
            CompletionLock = false;
            isStepCompleted = false;
            StageSO currentStep = StageSOs[currentStepIndex];
            Stages currentStage = Stages[currentStepIndex];

            if (currentStep != null)
            {
                // Setup Subtitle
                if (subtitle != null) subtitle.text = currentStep.stepSubtitle;

                // Step start event
                if (currentStage?.StartEvent != null) currentStage.StartEvent.Invoke();
                Invoke("DelayEventFunc", currentStage?.DelayEventTime ?? 0);

                // Play Step Audio
                StartCoroutine(PlayAudioWithCompletion(AudioHandler, currentStep.stepAudio, () =>
                {
                    if (currentStep.completionType == StageSO.CompletionType.AfterAudio)
                    {
                        CompletionLock = false;
                        StepCompleted();
                    }
                }));

                Invoke(nameof(SecondSubtitle), currentStep.stepAudio.length);
                
                // Setup and Delay Help Text and Audio
                Invoke(nameof(ShowHelp), currentStep.helpDelay);
            }
        }
        else if (currentStepIndex >= (StageSOs?.Length ?? 0))
        {
            Debug.Log("All steps have been completed.");
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
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    private IEnumerator PlayAudioWithCompletion(AudioSource audioSource, AudioClip clip, System.Action onCompletion)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();

            // Wait until the audio has finished playing
            yield return new WaitForSeconds(clip.length);

            // Invoke the completion callback
            onCompletion?.Invoke();
        }
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
        if (!CompletionLock && Stages != null && currentStepIndex < Stages.Length)
        {
            isStepCompleted = true;
            Stages[currentStepIndex]?.EndEvent?.Invoke();

            if(StageSOs[currentStepIndex].CorrectIcon == true)
            {
                CorrectIcon();
            }

            ShowComplete();

            currentStepIndex++;

            if (currentStepIndex < Stages.Length)
            {
                Stages currentStage = Stages[currentStepIndex];
                StageSO currentStep = StageSOs[currentStepIndex];

                Invoke(nameof(StartNextStep), currentStep.completionAudio.length + (currentStage?.StepCompletionTime ?? 0));
            }

            CompletionLock = true;
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
}
