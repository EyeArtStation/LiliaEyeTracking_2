using UnityEngine;
using Tobii.GameIntegration.Net; // this is the namespace your .cs wrapper defines

public class TobiiTgiSmokeTest : MonoBehaviour
{
    [Header("Logging")]
    public bool logEveryFrame = false;
    public float logIntervalSeconds = 0.25f;

    float _nextLogTime;

    void Awake()
    {
        // Give Tobii a readable app name
        TobiiGameIntegrationApi.SetApplicationName(Application.productName);

        Debug.Log("[TobiiTGI] Awake() - SetApplicationName done. If DLL is not found, you’ll see DllNotFoundException soon.");
    }

    void Update()
    {
        // REQUIRED: pump TGI once per frame
        TobiiGameIntegrationApi.Update();

        bool hasGaze = TobiiGameIntegrationApi.TryGetLatestGazePoint(out var gp);

        if (logEveryFrame)
        {
            if (hasGaze)
                Debug.Log($"[TobiiTGI] GazePoint: ({gp.X:0.0}, {gp.Y:0.0}) ts={gp.TimeStampMicroSeconds}");
            else
                Debug.Log("[TobiiTGI] No gaze point available (yet).");
            return;
        }

        // Interval logging so your console doesn’t explode
        if (Time.unscaledTime >= _nextLogTime)
        {
            _nextLogTime = Time.unscaledTime + Mathf.Max(0.05f, logIntervalSeconds);

            if (hasGaze)
                Debug.Log($"[TobiiTGI] GazePoint: ({gp.X:0.0}, {gp.Y:0.0}) ts={gp.TimeStampMicroSeconds}");
            else
                Debug.Log("[TobiiTGI] No gaze point available (yet). Tracker connected? Tobii Experience running?");
        }
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    void OnDestroy()
    {
        Shutdown();
    }

    bool _didShutdown;
    void Shutdown()
    {
        if (_didShutdown) return;
        _didShutdown = true;

        try
        {
            TobiiGameIntegrationApi.Shutdown();
            Debug.Log("[TobiiTGI] Shutdown() complete.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TobiiTGI] Shutdown() threw (often safe to ignore on domain reload): " + e.Message);
        }
    }
}
