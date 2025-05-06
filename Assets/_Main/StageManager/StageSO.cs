using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "StageSO", menuName = "StageSO")]
public class StageSO : ScriptableObject
{
    public enum CompletionType { AfterAudio, CodeTrigger, UIandTrigger }

    public string stepSubtitle;
    public AudioClip stepAudio;

    public string stepSubtitle2;
    public AudioClip stepAudio2;

    public string helpText;
    public AudioClip helpAudio;

    public string completionText;
    public AudioClip completionAudio;

    public CompletionType completionType;
    public float helpDelay = 30f; // Delay in seconds for help to appear after step starts

    public bool CorrectIcon = true;
    //public UnityEvent completionEvent;

    public GameObject prefab;
}

