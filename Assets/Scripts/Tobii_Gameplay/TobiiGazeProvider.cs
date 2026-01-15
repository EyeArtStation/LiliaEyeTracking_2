using UnityEngine;
using Tobii.GameIntegration.Net;

/// <summary>
/// Centralized gaze provider:
/// - Calls TryGetLatestGazePoint (NO TobiiGameIntegrationApi.Update() here; manager should own it)
/// - Converts normalized gaze to screen pixels
/// - Optional Y flip
/// - Optional stabilization
/// - Optional clamping to screen edges
/// </summary>
public class TobiiGazeProvider : MonoBehaviour
{
    public static TobiiGazeProvider Instance { get; private set; }

    [Header("Gaze Options")]
    public bool stabilizeGaze = true;
    [Range(0.01f, 1f)]
    public float smoothingFactor = 0.15f;

    [Tooltip("If gaze Y feels inverted, toggle this.")]
    public bool flipY = false;

    [Header("Clamping")]
    public bool clampToScreen = true;

    [Header("Debug")]
    public bool logWhenMissing = false;
    public float missingLogIntervalSeconds = 2f;

    public Vector2 _smoothedPx;
    bool _smoothedInit;
    bool _hasValidThisFrame;
    Vector2 _latestPx;

    float _nextMissingLog;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Important: don't call TobiiGameIntegrationApi.Update() here.
        // Your existing TobiiTgiManager should be the only Update() caller.
    }

    void LateUpdate()
    {
        _hasValidThisFrame = TryComputeGazePx(out _latestPx);

        if (_hasValidThisFrame && stabilizeGaze)
        {
            if (!_smoothedInit)
            {
                _smoothedPx = _latestPx;
                _smoothedInit = true;
            }
            else
            {
                _smoothedPx = Vector2.Lerp(_smoothedPx, _latestPx, smoothingFactor);
            }
        }
        else if (_hasValidThisFrame)
        {
            _smoothedPx = _latestPx;
            _smoothedInit = true;
        }
        else
        {
            if (logWhenMissing && Time.unscaledTime >= _nextMissingLog)
            {
                _nextMissingLog = Time.unscaledTime + Mathf.Max(0.25f, missingLogIntervalSeconds);
                Debug.LogWarning("[TobiiGazeProvider] No gaze sample available this frame.");
            }
        }
    }

    /// <summary>
    /// Returns the best gaze screen pixel position for this frame.
    /// </summary>
    public bool TryGetGazeScreenPx(out Vector2 screenPx)
    {
        if (_hasValidThisFrame)
        {
            screenPx = stabilizeGaze ? _smoothedPx : _latestPx;
            return true;
        }

        screenPx = default;
        return false;
    }

    bool TryComputeGazePx(out Vector2 screenPx)
    {
        screenPx = default;

        if (!TobiiGameIntegrationApi.TryGetLatestGazePoint(out var gp))
            return false;

        // Clamp Tobii normalized space to [-1..1] so off-screen gaze pins to edge if enabled
        float nx = Mathf.Clamp(gp.X, -1f, 1f);
        float ny = Mathf.Clamp(gp.Y, -1f, 1f);

        // Map [-1..1] -> [0..1]
        float u = nx * 0.5f + 0.5f;
        float v = ny * 0.5f + 0.5f;

        if (flipY)
            v = 1f - v;

        // Convert to pixels
        float x = u * Screen.width;
        float y = v * Screen.height;

        if (clampToScreen)
        {
            x = Mathf.Clamp(x, 0f, Screen.width);
            y = Mathf.Clamp(y, 0f, Screen.height);
        }

        screenPx = new Vector2(x, y);
        return true;
    }
}
