using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Minimal collision recorder. Attach to vehicle root (has collider + Rigidbody).
/// Records collision count and max impulse magnitude over the trial.
/// </summary>
public class CollisionTracker : MonoBehaviour
{
    public int collisionCount { get; private set; }
    public float maxImpulse { get; private set; }

    // optional: record timestamps for post-analysis
    public List<float> collisionTimes { get; private set; } = new List<float>();

    void Awake()
    {
        ResetTracker();
    }

    public void ResetTracker()
    {
        collisionCount = 0;
        maxImpulse = 0f;
        collisionTimes.Clear();
    }

    void OnCollisionEnter(Collision collision)
    {
        // compute approximate impulse magnitude using relative velocity and mass
        float normalSpeed = collision.relativeVelocity.magnitude;
        // approximate impulse = normalSpeed (this is coarse but useful)
        float impulse = normalSpeed;

        collisionCount++;
        maxImpulse = Mathf.Max(maxImpulse, impulse);
        collisionTimes.Add(Time.time);
    }
}