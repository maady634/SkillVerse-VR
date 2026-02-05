using UnityEngine;

public class ForkliftAudioManager : MonoBehaviour
{
    public G29VehicleInput vehicle;

    [Header("ENGINE")]
    public AudioSource engineIdleSource;
    public AudioSource engineRunSource;
    public float minRPM = 700f;
    public float maxRPM = 2500f;

    [Header("TRANSMISSION")]
    public AudioSource gearSource;
    public AudioClip gearShiftClip;
    public AudioSource reverseBeepSource;
    int lastGear;

    [Header("HYDRAULICS")]
    public ForkliftHolderController forkController;
    public AudioSource hydraulicSource;
    public AudioClip hydraulicLoopClip;
    public AudioClip hydraulicStartClip;
    public float hydraulicDeadZone = 0.0003f;
    public float hydraulicResponse = 6f;

    float lastMastHeight;
    bool hydraulicActive;

    [Header("ROLLING")]
    public AudioSource rollingSource;

    [Header("IMPACTS")]
    public AudioSource impactSource;
    public AudioClip[] lightImpactClips;
    public AudioClip[] heavyImpactClips;

    int lastLightIndex = -1;
    int lastHeavyIndex = -1;

    [Header("BUMPS")]
    public AudioSource bumpSource;
    public AudioClip bumpClip;

    public static ForkliftAudioManager instance;

    private void Start()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }
    void Update()
    {
        if (!vehicle)
        {
            Debug.LogError("[AUDIO] Vehicle reference NULL");
            return;
        }

        UpdateEngine();
        UpdateRolling();
        UpdateHydraulics();
        UpdateGear();
        UpdateReverseBeep();
    }

    // ================= ENGINE =================
    void UpdateEngine()
    {
        float throttle = vehicle.AcceleratorValue;
        float rpm = Mathf.Lerp(minRPM, maxRPM, throttle);
        float rpm01 = (rpm - minRPM) / (maxRPM - minRPM);

        Debug.Log($"[ENGINE] throttle={throttle:F2} rpm={rpm:F0}");

        engineIdleSource.volume = Mathf.Clamp01(0.5f * (1f - rpm01));
        engineRunSource.volume = Mathf.Clamp01(0.75f * rpm01);

        engineIdleSource.pitch = 0.9f + rpm01 * 0.2f;
        engineRunSource.pitch = 0.9f + rpm01 * 0.4f;

        if (!engineIdleSource.isPlaying)
        {
            Debug.Log("[ENGINE] Idle START");
            engineIdleSource.Play();
        }

        if (!engineRunSource.isPlaying)
        {
            Debug.Log("[ENGINE] Run START");
            engineRunSource.Play();
        }
    }

    // ================= ROLLING =================
    void UpdateRolling()
    {
        float speed = vehicle.CurrentSpeed;

        Debug.Log($"[ROLLING] speed={speed:F2}");

        if (speed < 0.3f)
        {
            rollingSource.volume = 0f;
            return;
        }

        float speed01 = Mathf.Clamp01(speed / 6f);

        rollingSource.volume = Mathf.Clamp01(0.35f * speed01);
        rollingSource.pitch = 0.8f + speed01 * 0.5f;

        if (!rollingSource.isPlaying)
        {
            Debug.Log("[ROLLING] START");
            rollingSource.Play();
        }
    }

    // ================= HYDRAULICS =================
    void UpdateHydraulics()
    {
        float mast = vehicle.CurrentMastHeight;
        float delta = mast - lastMastHeight;
        float speed = Mathf.Abs(delta) / Mathf.Max(Time.deltaTime, 0.0001f);
        float mastInput = forkController.MastInput; // -1, 0, 1
        bool shouldBeActive = Mathf.Abs(mastInput) > 0.01f;


        Debug.Log(
            $"[HYD] mast={mast:F4} delta={delta:F6} speed={speed:F5} " +
            $"shouldBeActive={shouldBeActive} active={hydraulicActive} " +
            $"isPlaying={hydraulicSource.isPlaying}"
        );

        if (shouldBeActive && !hydraulicActive)
        {
            hydraulicActive = true;

            Debug.Log("[HYD] ACTIVATED");

            hydraulicSource.clip = hydraulicLoopClip;
            hydraulicSource.loop = true;
            hydraulicSource.volume = 0.6f;
            hydraulicSource.pitch = 1f;
            hydraulicSource.Play();

            if (hydraulicStartClip)
            {
                Debug.Log("[HYD] Start hiss");
                hydraulicSource.PlayOneShot(hydraulicStartClip, 0.7f);
            }
            else
            {
                Debug.LogWarning("[HYD] Start clip NULL");
            }
        }

        if (hydraulicActive)
        {
            float speed01 = Mathf.Clamp01(speed / 0.15f);
            float targetVolume = Mathf.Clamp01(0.6f + speed01 * 0.3f);

            hydraulicSource.volume = targetVolume;
            hydraulicSource.pitch = Mathf.Lerp(0.9f, 1.3f, speed01);

            Debug.Log($"[HYD] RUN vol={hydraulicSource.volume:F2} pitch={hydraulicSource.pitch:F2}");

            if (!shouldBeActive)
            {
                Debug.Log("[HYD] DEACTIVATE");
                hydraulicActive = false;
            }
        }
        else
        {
            if (hydraulicSource.isPlaying)
            {
                Debug.Log("[HYD] STOP");
                hydraulicSource.Stop();
            }
        }

        lastMastHeight = mast;
    }

    // ================= GEAR =================
    void UpdateGear()
    {
        int gear = vehicle.GearState;

        if (gear != lastGear)
        {
            Debug.Log($"[GEAR] {lastGear} → {gear}");

            if (gearShiftClip)
                gearSource.PlayOneShot(gearShiftClip, 0.8f);
            else
                Debug.LogWarning("[GEAR] Shift clip NULL");

            lastGear = gear;
        }
    }

    // ================= REVERSE =================
    void UpdateReverseBeep()
    {
        if (vehicle.GearState < 0)
        {
            if (!reverseBeepSource.isPlaying)
            {
                Debug.Log("[REVERSE] BEEP START");
                reverseBeepSource.Play();
            }
        }
        else if (reverseBeepSource.isPlaying)
        {
            Debug.Log("[REVERSE] BEEP STOP");
            reverseBeepSource.Stop();
        }
    }

    // ================= IMPACTS =================
    public void PlayImpactSound(float strength01)
    {
        Debug.Log($"[IMPACT] strength={strength01:F2}");

        if (strength01 < 0.3f)
            PlayRandom(lightImpactClips, ref lastLightIndex, 0.6f);
        else
            PlayRandom(heavyImpactClips, ref lastHeavyIndex, 0.9f);
    }

    void PlayRandom(AudioClip[] clips, ref int lastIndex, float volume)
    {
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("[IMPACT] Clips NULL or empty");
            return;
        }

        int index;
        do { index = Random.Range(0, clips.Length); }
        while (clips.Length > 1 && index == lastIndex);

        lastIndex = index;
        impactSource.PlayOneShot(clips[index], volume);

        Debug.Log($"[IMPACT] Played clip {clips[index].name}");
    }

    // ================= BUMPS =================
    public void PlayBumpSound(float strength01)
    {
        Debug.Log($"[BUMP] strength={strength01:F2}");

        if (!bumpClip)
        {
            Debug.LogWarning("[BUMP] Clip NULL");
            return;
        }

        bumpSource.PlayOneShot(bumpClip, Mathf.Clamp01(0.4f + strength01 * 0.6f));
    }
}
