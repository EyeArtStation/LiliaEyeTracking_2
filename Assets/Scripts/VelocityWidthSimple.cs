using UnityEngine;

[System.Serializable]
public class VelocityWidthSimple
{
    [Header("Only 3 tuning knobs")]
    [Tooltip("Normalized speed at which the stroke becomes fully thin. (Higher = stays thick longer.)")]
    [Range(0.001f, 0.2f)] public float thinSpeed = 0.05f;

    [Tooltip("How much thicker when stopped. 0.8 means 1.0 -> 1.8x at standstill.")]
    [Range(0f, 3f)] public float widthRange = 0.8f;

    [Tooltip("Seconds of 'ink inertia'. Higher = changes lag more / feel viscous.")]
    [Range(0f, 0.5f)] public float inertiaSeconds = 0.10f;

    // runtime state
    Vector2 _prevPx;
    bool _hasPrev;
    float _speedNormSmoothed = 0f;
    float _widthSmoothed = 1f;

    /// Call when a stroke begins (mouse down / start drawing)
    public void Reset(Vector2 startPx)
    {
        _prevPx = startPx;
        _hasPrev = true;
        _speedNormSmoothed = 0f;
        _widthSmoothed = 1f + widthRange; // start juicy (optional; change to 1f if you want)
    }

    /// Returns width multiplier (1 = thin, 1+widthRange = max thick)
    public float Update(Vector2 currentPx, float dt, int canvasMinDimPx)
    {
        dt = Mathf.Max(1e-5f, dt);

        // 1) compute instantaneous px/sec
        float pxPerSec = 0f;
        if (_hasPrev)
        {
            float dist = (currentPx - _prevPx).magnitude;
            pxPerSec = dist / dt;
        }
        _prevPx = currentPx;
        _hasPrev = true;

        // 2) normalize by canvas min dimension so it behaves similarly across resolutions
        float denom = Mathf.Max(1f, canvasMinDimPx);
        float speedNorm = pxPerSec / denom;

        // 3) smooth speed a bit so it isn’t twitchy (tied to inertiaSeconds)
        float speedAlpha = InertiaAlpha(dt, inertiaSeconds * 0.5f); // speed reacts a bit quicker than width
        _speedNormSmoothed = Mathf.Lerp(_speedNormSmoothed, speedNorm, speedAlpha);

        // 4) map speed → width with a smoothstep curve:
        //    speed=0 => 1+widthRange, speed>=thinSpeed => 1
        float t = (_speedNormSmoothed <= 0f) ? 0f : Mathf.Clamp01(_speedNormSmoothed / Mathf.Max(1e-6f, thinSpeed));
        t = Smooth01(t); // nicer than linear
        float widthTarget = 1f + widthRange * (1f - t);

        // 5) smooth width with inertia (this is the “ink lag”)
        float widthAlpha = InertiaAlpha(dt, inertiaSeconds);
        _widthSmoothed = Mathf.Lerp(_widthSmoothed, widthTarget, widthAlpha);

        return _widthSmoothed;
    }

    static float Smooth01(float x) => x * x * (3f - 2f * x); // smoothstep

    static float InertiaAlpha(float dt, float inertiaSeconds)
    {
        if (inertiaSeconds <= 1e-6f) return 1f; // no lag
        // classic 1-pole lowpass: alpha = 1 - exp(-dt / tau)
        return 1f - Mathf.Exp(-dt / inertiaSeconds);
    }
}
