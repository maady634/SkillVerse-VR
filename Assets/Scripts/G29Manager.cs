using UnityEngine;
using static Logitech.LogitechGSDK;

public class G29Manager : MonoBehaviour
{
    public static G29Manager Instance;

    public DIJOYSTATE2ENGINES State;
    public bool Ready { get; private set; }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Force full reset (VERY IMPORTANT)
        LogiSteeringShutdown();
        System.Threading.Thread.Sleep(150);

        Ready = LogiSteeringInitialize(false);
        Debug.Log("🟢 Logitech Init = " + Ready);

        if (!Ready)
        {
            Debug.LogError("❌ Logitech SDK FAILED TO INITIALIZE");
            return;
        }

        // Enable force feedback
        LogiControllerPropertiesData props = new LogiControllerPropertiesData();
        LogiGetCurrentControllerProperties(0, ref props);

        props.forceEnable = true;
        props.overallGain = 100;
        props.springGain = 100;
        props.damperGain = 100;
        props.defaultSpringEnabled = false;
        props.allowGameSettings = true;

        bool ok = LogiSetPreferredControllerProperties(props);
        Debug.Log("🟢 Force Properties Set = " + ok);
    }

    void Update()
    {
        if (!Ready) return;

        if (!LogiUpdate()) return;
        if (!LogiIsConnected(0)) return;

        State = LogiGetStateUnity(0);
    }

    void OnApplicationQuit()
    {
        Debug.Log("🛑 Logitech Shutdown");
        LogiSteeringShutdown();
    }

    void OnDisable()
    {
        Debug.Log("🛑 Logitech Shutdown (Disable)");
        LogiSteeringShutdown();
    }
}
