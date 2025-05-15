using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformTarget : MonoBehaviour
{
    public TransformLerper lerper;
    public Transform destination;
    public float Duration = 2.0f;

    public bool completeStep = false;

    public async void MoveTarget()
    {
        lerper.MoveTargetTo(destination.position, destination.rotation, Duration);

        int delay = (int)Duration;
        await UniTask.Delay(delay * 1000);

        if (completeStep)
        {
            StageManager.Instance.StepCompleted();
        }

        
    }

}
