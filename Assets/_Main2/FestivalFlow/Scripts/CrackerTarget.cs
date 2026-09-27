using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// One cracker the player lights with the lighter. A rocket climbs into the sky and bursts;
/// a fire shower sprays a sparkling fountain on the spot.
/// Ignition is a distance check rather than a trigger collider, because the cracker meshes in
/// this scene carry extreme import scales (46x and 1000x) that make authored colliders useless.
/// Set up by Tools > SkillVerse > Festival (FestivalInteractionBuilder).
/// </summary>
public class CrackerTarget : MonoBehaviour
{
    public enum CrackerType { Rocket, FireShower }

    [Header("Identity")]
    public CrackerType type = CrackerType.Rocket;

    [Header("Ignition")]
    [Tooltip("Where the fuse spark appears, and the fallback touch point if there are no renderers.")]
    public Transform ignitePoint;
    [Tooltip("How close (metres) the lighter has to get to this cracker's mesh.")]
    public float igniteRadius = 0.25f;

    [Header("Effects")]
    [Tooltip("Parent for spawned effects. Must be unscaled - these meshes are not.")]
    public Transform effectRoot;
    public ParticleSystem fuseSparkPrefab;
    public ParticleSystem rocketTrailPrefab;
    public ParticleSystem rocketBurstPrefab;
    public ParticleSystem fireShowerPrefab;
    public AudioSource audioSource;
    public AudioClip igniteClip;

    [Header("Rocket flight")]
    [Tooltip("Seconds the fuse burns before the cracker goes off.")]
    public float fuseDelay = 0.8f;
    [Tooltip("Seconds the rocket takes to reach the top of its climb.")]
    public float flightTime = 1.4f;
    [Tooltip("Metres the rocket climbs before it bursts.")]
    public float flightHeight = 16f;
    [Tooltip("How far the rocket drifts sideways on the way up.")]
    public float driftRadius = 1.2f;

    [Header("Fire shower")]
    [Tooltip("Seconds the fountain sprays for.")]
    public float showerDuration = 4f;

    /// <summary>True once this cracker has been lit. It only ever fires once.</summary>
    public bool Ignited { get; private set; }

    [Header("Events")]
    public UnityEvent OnIgnited;

    /// <summary>World position of the fuse - where the spark and the fountain appear.</summary>
    public Vector3 IgnitePosition => ignitePoint != null ? ignitePoint.position : transform.position;

    /// <summary>
    /// The cracker's mesh in world space, captured before it is lit. The touch test measures
    /// against this rather than a single fuse point: a rocket is nearly a metre tall, so a point
    /// test at its mid-height makes the visible head of the rocket impossible to light.
    /// </summary>
    private Bounds touchBounds;
    private bool touchBoundsReady;

    private void Awake()
    {
        CacheTouchBounds();
    }

    private void CacheTouchBounds()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            touchBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) touchBounds.Encapsulate(renderers[i].bounds);
        }
        else
        {
            touchBounds = new Bounds(IgnitePosition, Vector3.one * 0.05f);
        }
        touchBoundsReady = true;
    }

    /// <summary>
    /// True when any point along the lighter - from the hand to the burning tip - is within
    /// <see cref="igniteRadius"/> of this cracker. Sampling the whole stick rather than the tip
    /// alone matches what the player sees: the lighter crossing the cracker.
    /// </summary>
    public bool IsTouchedBy(Vector3 from, Vector3 to, int samples = 10)
    {
        if (!touchBoundsReady) CacheTouchBounds();

        float sqrRadius = igniteRadius * igniteRadius;
        for (int i = 0; i <= samples; i++)
        {
            Vector3 point = Vector3.Lerp(from, to, i / (float)samples);
            if (touchBounds.SqrDistance(point) <= sqrRadius) return true;
        }
        return false;
    }

    /// <summary>Lights the cracker. Safe to call more than once - later calls do nothing.</summary>
    [ContextMenu("Ignite")]
    public void Ignite()
    {
        if (Ignited || !isActiveAndEnabled) return;
        Ignited = true;

        if (audioSource != null && igniteClip != null)
            audioSource.PlayOneShot(igniteClip);

        Spawn(fuseSparkPrefab, IgnitePosition, 2f);

        StartCoroutine(type == CrackerType.Rocket ? RunRocket() : RunFireShower());

        OnIgnited?.Invoke();
    }

    private IEnumerator RunRocket()
    {
        yield return new WaitForSeconds(fuseDelay);

        Vector3 start = transform.position;
        Vector2 drift = Random.insideUnitCircle * driftRadius;
        Vector3 apex = start + new Vector3(drift.x, flightHeight, drift.y);

        // The trail rides an unscaled flyer rather than the rocket itself, which is imported
        // at ~1000x and would blow the particle sizes up with it.
        ParticleSystem trail = Spawn(rocketTrailPrefab, start, 0f);
        Transform flyer = trail != null ? trail.transform : null;

        float t = 0f;
        while (t < flightTime)
        {
            t += Time.deltaTime;
            // Ease out - fast off the ground, slowing as it reaches the top.
            float k = Mathf.Clamp01(t / flightTime);
            Vector3 pos = Vector3.Lerp(start, apex, 1f - (1f - k) * (1f - k));

            transform.position = pos;
            if (flyer != null) flyer.position = pos;
            yield return null;
        }

        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        if (trail != null)
        {
            trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(trail.gameObject, 3f);
        }

        Spawn(rocketBurstPrefab, apex, 6f);
    }

    private IEnumerator RunFireShower()
    {
        yield return new WaitForSeconds(fuseDelay);

        Vector3 spout = IgnitePosition;
        ParticleSystem shower = Spawn(fireShowerPrefab, spout, 0f);
        if (shower == null) yield break;

        yield return new WaitForSeconds(showerDuration);

        shower.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(shower.gameObject, 3f);
    }

    /// <summary>Instantiates an effect unscaled at a world position and plays it.</summary>
    private ParticleSystem Spawn(ParticleSystem prefab, Vector3 position, float autoDestroyAfter)
    {
        if (prefab == null) return null;

        var instance = Instantiate(prefab, position, Quaternion.identity, effectRoot);
        instance.transform.position = position;
        instance.transform.localScale = Vector3.one;
        instance.Play(true);

        if (autoDestroyAfter > 0f) Destroy(instance.gameObject, autoDestroyAfter);
        return instance;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = type == CrackerType.Rocket
            ? new Color(1f, 0.5f, 0.1f, 0.6f)
            : new Color(1f, 0.9f, 0.3f, 0.6f);

        // The volume the lighter actually has to reach.
        CacheTouchBounds();
        Gizmos.DrawWireCube(touchBounds.center, touchBounds.size + Vector3.one * (igniteRadius * 2f));

        // The fuse - where the spark and the fountain appear.
        Gizmos.DrawWireSphere(IgnitePosition, 0.03f);
    }
}
