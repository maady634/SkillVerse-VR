//using Cysharp.Threading.Tasks;
//using System.Collections;
//using UnityEngine;
//using UnityEngine.Events;
//using UnityEngine.Splines;

//public class SplineController : MonoBehaviour
//{
//    [SerializeField]
//    private SplineAnimate splineAnim;

//    public UnityEvent OnReachEvent;

//    void Start()
//    {
//        splineAnim = GetComponent<SplineAnimate>();
//        MonitorSplineOffset().Forget();
//    }

//    // UniTask to monitor the StartOffset without using Update
//    private async UniTaskVoid MonitorSplineOffset()
//    {
//        // Continue checking as long as this object exists
//        while (this != null && splineAnim != null)
//        {
//            if (splineAnim.NormalizedTime >= 1f)
//            {
//                OnReachEvent.Invoke();
//                Debug.Log("Spline reached");
//                break;  // Stop checking once the event is invoked
//            }

//            await UniTask.Yield();  // Yield control back to the main thread to avoid blocking
//        }
//    }
//}
