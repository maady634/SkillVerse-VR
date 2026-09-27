using UnityEngine;

/// <summary>
/// Floats a help arrow up and down on the spot, forever, so it reads as a hint rather than as
/// part of the scenery. Nothing else drives it - enabling the GameObject starts the loop and
/// disabling it stops it.
///
/// The bob is applied on top of the pose the arrow was authored at, which is captured in
/// OnEnable, so the arrow always returns to exactly where it was placed in the editor however
/// many times it is shown and hidden.
///
/// The arrow prefab is rotated to point downwards and sits under a scaled parent, so neither its
/// own local axes nor raw local units mean anything useful here. The offset is built in world
/// space and converted with InverseTransformVector, which undoes the parent's rotation and
/// scale - that keeps <see cref="amplitude"/> honest in metres.
/// </summary>
public class HelpArrowBobber : MonoBehaviour
{
    [Header("Bob")]
    [Tooltip("How far the arrow travels from its resting point, in metres, in each direction.")]
    public float amplitude = 0.15f;
    [Tooltip("Full up-and-down cycles per second.")]
    public float cyclesPerSecond = 0.8f;
    [Tooltip("Bob along world up. Turn off to bob along the arrow's own local Y instead.")]
    public bool useWorldUp = true;

    private Vector3 restLocalPosition;
    private float startTime;

    private void OnEnable()
    {
        restLocalPosition = transform.localPosition;
        startTime = Time.time;
    }

    private void OnDisable()
    {
        // Park the arrow back where it was placed, so the next OnEnable captures a clean pose.
        transform.localPosition = restLocalPosition;
    }

    private void Update()
    {
        float offset = Mathf.Sin((Time.time - startTime) * cyclesPerSecond * 2f * Mathf.PI) * amplitude;

        Vector3 delta;
        if (useWorldUp)
        {
            Vector3 world = Vector3.up * offset;
            delta = transform.parent != null ? transform.parent.InverseTransformVector(world) : world;
        }
        else
        {
            delta = Vector3.up * offset;
        }

        transform.localPosition = restLocalPosition + delta;
    }
}
