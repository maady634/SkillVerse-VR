using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowDisabler : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("ForkLift"))
        {
            this.gameObject.SetActive(false);
        }
    }
}
