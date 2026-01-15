using UnityEngine;
using Tobii.GameIntegration.Net;

public class TobiiTgiDiagnostics : MonoBehaviour
{
    public float logIntervalSeconds = 0.5f;

    float _nextLog;

    void Start()
    {
        Debug.Log($"[TobiiTGI] LoadedDll = {TobiiGameIntegrationApi.LoadedDll}");
        TobiiGameIntegrationApi.SetApplicationName(Application.productName);

        // Tell TGI what coordinate space we want gaze in.
        // This uses the primary display area in OS coordinates (0..width, 0..height).
        // If you have multiple monitors or negative coords, we can refine this later.
        var rect = new TobiiRectangle
        {
            Left = 0,
            Top = 0,
            Right = Display.main.systemWidth,
            Bottom = Display.main.systemHeight
        };

        bool tracking = TobiiGameIntegrationApi.TrackRectangle(rect);
        Debug.Log($"[TobiiTGI] TrackRectangle({rect.Right}x{rect.Bottom}) => {tracking}");

        // Optional: try to pull info immediately
        var info = TobiiGameIntegrationApi.GetTrackerInfo();
        if (info != null)
        {
            Debug.Log($"[TobiiTGI] TrackerInfo: {info.FriendlyName} | Type={info.Type} | Caps={info.Capabilities} | Attached={info.IsAttached}");
        }
        else
        {
            Debug.Log("[TobiiTGI] TrackerInfo: (none yet)");
        }
    }

    void Update()
    {
        TobiiGameIntegrationApi.Update();

        if (Time.unscaledTime < _nextLog) return;
        _nextLog = Time.unscaledTime + Mathf.Max(0.1f, logIntervalSeconds);

        bool connected = TobiiGameIntegrationApi.IsTrackerConnected();
        bool enabled = TobiiGameIntegrationApi.IsTrackerEnabled();
        bool present = TobiiGameIntegrationApi.IsPresent();

        bool hasGaze = TobiiGameIntegrationApi.TryGetLatestGazePoint(out var gp);
        bool hasHead = TobiiGameIntegrationApi.TryGetLatestHeadPose(out var hp);

        Debug.Log(
            $"[TobiiTGI] connected={connected} enabled={enabled} present={present} " +
            $"gaze={(hasGaze ? $"({gp.X:0.0},{gp.Y:0.0})" : "none")} " +
            $"head={(hasHead ? $"pos({hp.Position.X:0.0},{hp.Position.Y:0.0},{hp.Position.Z:0.0})" : "none")}"
        );
    }

    void OnDestroy()
    {
        // Not strictly required, but good hygiene.
        TobiiGameIntegrationApi.Shutdown();
    }
}
