using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Makes the lighter grabbable and, the moment it is grabbed, makes it a permanent child of the
/// hand that took it, so the player cannot fumble or drop it half way through the training.
///
/// The XRGrabInteractable and the Rigidbody are both destroyed on pickup. That is deliberate:
/// a Rigidbody writes its own world pose back onto the Transform every frame (especially with
/// interpolation on), which overrides the parent hierarchy and leaves the lighter floating where
/// the hand used to be. With no physics component left, plain parenting is all that is needed -
/// there is no per-frame code keeping the lighter in place.
///
/// Set up by Tools > SkillVerse > Festival (FestivalInteractionBuilder).
/// </summary>
public class LighterGrab : MonoBehaviour
{
    [Header("Pose in the hand")]
    [Tooltip("Local position the lighter snaps to once it is parented to the hand.")]
    public Vector3 heldLocalPosition = new Vector3(0f, 0f, 0.71f);
    [Tooltip("Local euler angles the lighter snaps to once it is parented to the hand.")]
    public Vector3 heldLocalEuler = new Vector3(90f, 0f, 0f);

    [Header("References")]
    [Tooltip("Empty transform at the burning end. This is what the crackers are measured against.")]
    public Transform tip;

    [Header("Events")]
    public UnityEvent OnPickedUp;

    /// <summary>True once the lighter has been parented to a hand.</summary>
    public bool IsHeld { get; private set; }

    /// <summary>The burning end of the lighter (falls back to this transform).</summary>
    public Transform Tip => tip != null ? tip : transform;

    /// <summary>
    /// Turns the grab interaction on and off so the lighter can only be picked up during the step
    /// that asks for it. Once the lighter is in a hand the interactable has already been destroyed,
    /// so this quietly does nothing from then on.
    /// </summary>
    public void SetGrabbable(bool on)
    {
        if (grab != null) grab.enabled = on;
    }

    private XRGrabInteractable grab;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null) grab.selectEntered.AddListener(HandleSelectEntered);
    }

    private void OnDestroy()
    {
        if (grab != null) grab.selectEntered.RemoveListener(HandleSelectEntered);
    }

    private void HandleSelectEntered(SelectEnterEventArgs args)
    {
        if (IsHeld) return;

        var interactor = args.interactorObject;
        if (interactor == null) return;

        var hand = interactor.GetAttachTransform(args.interactableObject);
        if (hand == null) hand = interactor.transform;

        StartCoroutine(AttachToHand(hand));
    }

    /// <summary>
    /// Tears down the physics and interaction components, then parents the lighter to the hand.
    /// Each step waits a frame so the destroys have actually flushed before the next one runs.
    /// </summary>
    private IEnumerator AttachToHand(Transform hand)
    {
        // Let XRI finish its select bookkeeping before we pull the interactable out from under it.
        yield return null;

        if (grab != null)
        {
            grab.selectEntered.RemoveListener(HandleSelectEntered);
            Destroy(grab);      // unregisters from the interaction manager and cancels the select
            grab = null;
        }

        // Destroy flushes at the end of the frame, and the Rigidbody cannot go before the
        // XRGrabInteractable that requires it has actually gone.
        yield return null;

        var body = GetComponent<Rigidbody>();
        if (body != null) Destroy(body);

        // Nothing needs the colliders any more - ignition is a distance check - and leaving them
        // on would let the stick shove the environment around while it is in the hand.
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        yield return null;

        if (hand == null) yield break;

        transform.SetParent(hand, false);
        transform.localPosition = heldLocalPosition;
        transform.localRotation = Quaternion.Euler(heldLocalEuler);

        IsHeld = true;
        OnPickedUp?.Invoke();
    }
}
