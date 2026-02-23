using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enums : MonoBehaviour
{
    public enum ExpectedInputType
    {
        None,
        Accelerator,
        Brake,
        SteerLeft,
        SteerRight,
        GearForward,
        GearReverse
    }
}
