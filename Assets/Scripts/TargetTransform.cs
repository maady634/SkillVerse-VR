using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows;

public class TargetTransform : MonoBehaviour
{
    public G29VehicleInput G29VehicleInput;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Box"))
        {
           PerformanceEvaluator.Instance.CompleteTrialSuccess();
        }
       
    }
}
