using UnityEngine;

[DisallowMultipleComponent]
public class VelocityWidthTuner : MonoBehaviour
{
    [Header("Target")]
    public PaintManagerCustom paintManager;

    [Header("Auto apply")]
    public bool applyOnEnable = true;
    public bool applyEveryFrame = true; // live tuning while you drag sliders

    // ---------------- PaintManagerCustom knobs ----------------

    [Header("Manager: Speed → Pressure mapping")]
    [Range(2, 8)] public int velocityWindow = 6;

    [Tooltip("Normalized speed where thinning begins (higher = thick sooner).")]
    [Range(0.0001f, 0.05f)] public float velMin = 0.008f;

    [Tooltip("Normalized speed where you're fully thin (lower = more change while moving).")]
    [Range(0.005f, 0.2f)] public float velMax = 0.045f;

    [Tooltip("Manager-side smoothing speed. Lower = more lag/ink inertia.")]
    [Range(0.1f, 40f)] public float pressureSmooth = 8f;

    [Header("Manager: Pressure range (width envelope)")]
    [Range(0.01f, 1f)] public float minPressure = 0.28f;
    [Range(0.01f, 1f)] public float maxPressure = 0.95f;

    [Header("Manager toggles")]
    public bool smoothVelocityPressure = true;

    // ---------------- BasePaintCustom knobs ----------------

    [Header("Painter: Attack / Release shaping (ink feel)")]
    public bool enablePainterSmoothing = true;

    [Tooltip("How fast it THINS when speed increases (higher = thins quickly).")]
    [Range(0.1f, 200f)] public float thinAttackRate = 45f;

    [Tooltip("How fast it THICKENS when speed decreases (lower = thicker changes linger).")]
    [Range(0.1f, 200f)] public float thickReleaseRate = 10f;

    [Tooltip("Optional: ignore tiny speeds in BasePaint's internal speed path. Mostly irrelevant if using external pressure.")]
    [Range(0f, 0.01f)] public float speedDeadZone = 0f;

    [Header("Optional: keep Velocity mode selected")]
    public bool forceVelocityMode = false;

    void Reset()
    {
        // Try to find in scene automatically
        if (paintManager == null) paintManager = FindObjectOfType<PaintManagerCustom>();
    }

    void OnEnable()
    {
        if (applyOnEnable) Apply();
    }

    void Update()
    {
        if (applyEveryFrame) Apply();
    }

    public void Apply()
    {
        if (paintManager == null) return;

        // ---- Apply to PaintManagerCustom ----
        paintManager.velocityWindow = velocityWindow;
        paintManager.velMin = Mathf.Min(velMin, velMax - 0.0001f);
        paintManager.velMax = Mathf.Max(velMax, velMin + 0.0001f);
        paintManager.pressureSmooth = pressureSmooth;
        paintManager.minPressure = Mathf.Min(minPressure, maxPressure - 0.0001f);
        paintManager.maxPressure = Mathf.Max(maxPressure, minPressure + 0.0001f);
        paintManager.smoothVelocityPressure = smoothVelocityPressure;

        // ---- Apply to BasePaintCustom (painter) ----
        var p = paintManager.painter;
        if (p != null)
        {
            p.smoothPressure = enablePainterSmoothing;
            p.thinAttackRate = thinAttackRate;
            p.thickReleaseRate = thickReleaseRate;
            p.speedDeadZone = speedDeadZone;

            if (forceVelocityMode)
                p.SetMode(BasePaintCustom.PaintMode.VelocityLineWidth);
        }
    }

    // Nice for testing without playmode UI
    [ContextMenu("Apply Now")]
    public void ApplyNow() => Apply();
}
