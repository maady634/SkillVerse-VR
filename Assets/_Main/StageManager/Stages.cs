using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Stages : MonoBehaviour
{
    public int StepCompletionTime = 1;
    public int DelayEventTime = 0;
    public UnityEvent StartEvent;
    public UnityEvent EndEvent;
    public UnityEvent DelayEvent;
}
