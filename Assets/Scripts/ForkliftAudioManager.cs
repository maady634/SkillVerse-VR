using UnityEngine;

public class ForkliftAudioManager : MonoBehaviour
{
    public static ForkliftAudioManager Instance;

    [Header("REFERENCES")]
    public G29VehicleInput vehicle;
    public ForkliftHolderController forkController;

    // ================= ENGINE =================
    [Header("ENGINE")]
    public AudioSource engineIdleSource;
    public AudioSource engineRunSource;
    public float minRPM = 700f;
    public float maxRPM = 2500f;

    // ================= TRANSMISSION =================
    [Header("TRANSMISSION")]
    public AudioSource gearSource;
    public AudioClip gearShiftClip;
    public AudioSource reverseBeepSource;
    private int lastGear;

    // ================= HYDRAULICS =================
    [Header("HYDRAULICS")]
    public AudioSource hydraulicSource;
    public AudioClip hydraulicLoopClip;
    public AudioClip hydraulicStartClip;

    private float lastMastHeight;
    private bool hydraulicActive;

    // ================= ROLLING =================
    [Header("ROLLING")]
    public AudioSource rollingSource;

    // ================= IMPACTS =================
    [Header("IMPACTS")]
    public AudioSource impactSource;
    public AudioClip[] lightImpactClips;
    public AudioClip[] heavyImpactClips;

    private int lastLightIndex = -1;
    private int lastHeavyIndex = -1;

    // ================= BUMPS =================
    [Header("BUMPS")]
    public AudioSource bumpSource;
    public AudioClip bumpClip;

    // =========================================================
    // ================= SINGLETON SAFE ========================
    // =========================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Ensure looping where required
        if (engineIdleSource) engineIdleSource.loop = true;
        if (engineRunSource) engineRunSource.loop = true;
        if (rollingSource) rollingSource.loop = true;
        if (reverseBeepSource) reverseBeepSource.loop = true;
        if (hydraulicSource) hydraulicSource.loop = true;
    }

    private void Update()
    {
        if (!vehicle)
            return;

        UpdateEngine();
        UpdateRolling();
        UpdateHydraulics();
        UpdateGear();
        UpdateReverseBeep();
    }

    // =========================================================
    // ================= ENGINE ================================
    // =========================================================
    void UpdateEngine()
    {
        float throttle = vehicle.AcceleratorValue;
        float rpm = Mathf.Lerp(minRPM, maxRPM, throttle);
        float rpm01 = Mathf.InverseLerp(minRPM, maxRPM, rpm);

        if (engineIdleSource)
        {
            engineIdleSource.volume = Mathf.Clamp01(0.5f * (1f - rpm01));
            engineIdleSource.pitch = 0.9f + rpm01 * 0.2f;

            if (!engineIdleSource.isPlaying)
                engineIdleSource.Play();
        }

        if (engineRunSource)
        {
            engineRunSource.volume = Mathf.Clamp01(0.75f * rpm01);
            engineRunSource.pitch = 0.9f + rpm01 * 0.4f;

            if (!engineRunSource.isPlaying)
                engineRunSource.Play();
        }
    }

    // =========================================================
    // ================= ROLLING ===============================
    // =========================================================
    void UpdateRolling()
    {
        if (!rollingSource) return;

        float speed = vehicle.CurrentSpeed;

        if (speed < 0.3f)
        {
            if (rollingSource.isPlaying)
                rollingSource.Stop();
            return;
        }

        float speed01 = Mathf.Clamp01(speed / 6f);

        rollingSource.volume = Mathf.Clamp01(0.35f * speed01);
        rollingSource.pitch = 0.8f + speed01 * 0.5f;

        if (!rollingSource.isPlaying)
            rollingSource.Play();
    }

    // =========================================================
    // ================= HYDRAULICS ============================
    // =========================================================
    void UpdateHydraulics()
    {
        if (!hydraulicSource || !forkController)
            return;

        float mast = vehicle.CurrentMastHeight;
        float delta = mast - lastMastHeight;
        float speed = Mathf.Abs(delta) / Mathf.Max(Time.deltaTime, 0.0001f);

        float mastInput = forkController.MastInput;
        bool shouldBeActive = Mathf.Abs(mastInput) > 0.01f;

        if (shouldBeActive && !hydraulicActive)
        {
            hydraulicActive = true;

            hydraulicSource.clip = hydraulicLoopClip;
            hydraulicSource.volume = 0.6f;
            hydraulicSource.pitch = 1f;
            hydraulicSource.Play();

            if (hydraulicStartClip)
                hydraulicSource.PlayOneShot(hydraulicStartClip, 0.7f);
        }

        if (hydraulicActive)
        {
            float speed01 = Mathf.Clamp01(speed / 0.15f);

            hydraulicSource.volume = Mathf.Lerp(0.6f, 0.9f, speed01);
            hydraulicSource.pitch = Mathf.Lerp(0.9f, 1.3f, speed01);

            if (!shouldBeActive)
            {
                hydraulicActive = false;
                hydraulicSource.Stop();
            }
        }

        lastMastHeight = mast;
    }

    // =========================================================
    // ================= GEAR SHIFT ============================
    // =========================================================
    void UpdateGear()
    {
        int gear = vehicle.GearState;

        if (gear != lastGear)
        {
            if (gearShiftClip && gearSource)
                gearSource.PlayOneShot(gearShiftClip, 0.8f);

            lastGear = gear;
        }
    }

    // =========================================================
    // ================= REVERSE BEEP ==========================
    // =========================================================
    void UpdateReverseBeep()
    {
        if (!reverseBeepSource) return;

        if (vehicle.GearState < 0)
        {
            if (!reverseBeepSource.isPlaying)
                reverseBeepSource.Play();
        }
        else
        {
            if (reverseBeepSource.isPlaying)
                reverseBeepSource.Stop();
        }
    }

    // =========================================================
    // ================= IMPACTS ===============================
    // =========================================================
    public void PlayImpactSound(float strength01)
    {
        if (strength01 < 0.3f)
            PlayRandom(lightImpactClips, ref lastLightIndex, 0.6f);
        else
            PlayRandom(heavyImpactClips, ref lastHeavyIndex, 0.9f);
    }

    void PlayRandom(AudioClip[] clips, ref int lastIndex, float volume)
    {
        if (clips == null || clips.Length == 0 || !impactSource)
            return;

        int index;
        do
        {
            index = Random.Range(0, clips.Length);
        }
        while (clips.Length > 1 && index == lastIndex);

        lastIndex = index;
        impactSource.PlayOneShot(clips[index], volume);
    }

    // =========================================================
    // ================= BUMPS =================================
    // =========================================================
    public void PlayBumpSound(float strength01)
    {
        if (!bumpClip || !bumpSource)
            return;

        bumpSource.PlayOneShot(bumpClip, Mathf.Clamp01(0.4f + strength01 * 0.6f));
    }
}