using UnityEngine;
using UnityEngine.UI;
using Tobii.GameIntegration.Net;

[RequireComponent(typeof(RectTransform))]
public class TobiiGazeCursorUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas canvas;                 // assign your Canvas (Screen Space - Overlay recommended)
    public Graphic cursorGraphic;         // assign the Image component (or leave blank to auto-find)

    [Header("Tracking")]
    public bool autoTrackRectangle = true;
    public float trackRetrySeconds = 1.0f;

    [Header("Smoothing")]
    public bool stabilizeGaze = true;
    [Range(0.01f, 1f)]
    public float smoothingFactor = 0.15f;

    [Header("Behavior")]
    public bool hideWhenNoGaze = true;
    public float logIntervalSeconds = 1.0f;

    RectTransform _rt;
    Vector2 _smoothed;
    float _nextTrackAttempt;
    float _nextLog;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (!cursorGraphic) cursorGraphic = GetComponent<Graphic>();

        TobiiGameIntegrationApi.SetApplicationName(Application.productName);
        TobiiGameIntegrationApi.Update(); // prime the API once
        Debug.Log($"[TobiiTGI] LoadedDll={TobiiGameIntegrationApi.LoadedDll}");

        // NOTE: Intentionally NOT calling Shutdown() anywhere while debugging in Editor.
    }

    void Update()
    {
        TobiiGameIntegrationApi.Update();

        // Periodic status log (helps diagnose NULL Device / disconnected states)
        if (Time.unscaledTime >= _nextLog)
        {
            _nextLog = Time.unscaledTime + Mathf.Max(0.25f, logIntervalSeconds);

            var info = TobiiGameIntegrationApi.GetTrackerInfo();
            Debug.Log($"[TobiiTGI] connected={TobiiGameIntegrationApi.IsTrackerConnected()} " +
                      $"enabled={TobiiGameIntegrationApi.IsTrackerEnabled()} present={TobiiGameIntegrationApi.IsPresent()} " +
                      $"tracker={(info == null ? "null" : info.FriendlyName)}");
        }

        // Retry tracking rectangle until it sticks
        if (autoTrackRectangle && Time.unscaledTime >= _nextTrackAttempt)
        {
            _nextTrackAttempt = Time.unscaledTime + Mathf.Max(0.2f, trackRetrySeconds);

            // Use current Unity screen size
            var rect = new TobiiRectangle
            {
                Left = 0,
                Top = 0,
                Right = Screen.width,
                Bottom = Screen.height
            };

            bool ok = TobiiGameIntegrationApi.TrackRectangle(rect);
            // Only log failures (success will happen once)
            if (!ok)
                Debug.LogWarning($"[TobiiTGI] TrackRectangle({rect.Right}x{rect.Bottom}) failed - will retry");
        }

        // Read gaze
        if (!TobiiGameIntegrationApi.TryGetLatestGazePoint(out var gp))
        {
            if (hideWhenNoGaze) SetVisible(false);
            return;
        }

        SetVisible(true);

        float nx = Mathf.Clamp(gp.X, -1f, 1f);
        float ny = Mathf.Clamp(gp.Y, -1f, 1f);

        Vector2 gazePx = new Vector2(
            (nx * 0.5f + 0.5f) * Screen.width,
            (ny * 0.5f + 0.5f) * Screen.height
        );

        if (stabilizeGaze)
            _smoothed = Vector2.Lerp(_smoothed == Vector2.zero ? gazePx : _smoothed, gazePx, smoothingFactor);
        else
            _smoothed = gazePx;

        // Move UI cursor (Overlay canvas => camera is null)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            _smoothed,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out Vector2 localPos
        );

        _rt.localPosition = localPos;
    }

    void SetVisible(bool v)
    {
        if (!cursorGraphic) return;
        cursorGraphic.enabled = v; // don't SetActive(false) or you disable your own Update()
    }

    public void ToggleStabilization() => stabilizeGaze = !stabilizeGaze;
}
