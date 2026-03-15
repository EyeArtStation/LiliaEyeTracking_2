using UnityEngine;
using System;
using System.Reflection;

[DisallowMultipleComponent]
public class VelocityWidthTuner : MonoBehaviour
{
    [Header("Target")]
    public PaintManagerCustom paintManager;

    [Header("Auto apply")]
    public bool applyOnEnable = true;
    public bool applyEveryFrame = true;

    // ---------------- Manager knobs (these exist) ----------------
    [Header("Manager: Pressure range (width envelope)")]
    [Range(0.01f, 2f)] public float minPressure = 0.28f;
    [Range(0.01f, 4f)] public float maxPressure = 1.20f;

    [Header("Manager toggles")]
    public bool smoothVelocityPressure = true;

    // ---------------- VelocityWidthSimple knobs (best-guess) ----------------
    // These are NOT on PaintManagerCustom directly anymore — they should map to velWidth.
    // We apply them via reflection so you don't get compile errors if your VelocityWidthSimple uses different names.
    [Header("VelocityWidthSimple (velWidth)")]
    [Tooltip("How much the width can expand above 1.0 (if your class uses a range/boost concept).")]
    [Range(0f, 3f)] public float widthRange = 0.75f;

    [Tooltip("Speed->width smoothing (higher = more lag).")]
    [Range(0.01f, 80f)] public float smooth = 12f;

    [Tooltip("Optional: normalized speed min (where effect begins).")]
    [Range(0.0001f, 1f)] public float speedMin = 0.02f;

    [Tooltip("Optional: normalized speed max (where effect is max).")]
    [Range(0.0001f, 1f)] public float speedMax = 0.12f;

    // ---------------- Painter knobs (these exist on BasePaintCustom) ----------------
    [Header("Painter: Attack / Release shaping (ink feel)")]
    public bool enablePainterSmoothing = true;

    [Range(0.1f, 200f)] public float thinAttackRate = 45f;
    [Range(0.1f, 200f)] public float thickReleaseRate = 10f;
    [Range(0f, 0.01f)] public float speedDeadZone = 0f;

    [Header("Optional: keep Velocity mode selected")]
    public bool forceVelocityMode = false;

    void Reset()
    {
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

        // ---- Apply to PaintManagerCustom (these fields EXIST) ----
        paintManager.smoothVelocityPressure = smoothVelocityPressure;
        paintManager.minPressure = Mathf.Min(minPressure, maxPressure - 0.0001f);
        paintManager.maxPressure = Mathf.Max(maxPressure, minPressure + 0.0001f);

        // ---- Apply to velWidth (best-effort, NO compile errors) ----
        ApplyToVelWidth(paintManager.velWidth);

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

    private void ApplyToVelWidth(object velWidthObj)
    {
        if (velWidthObj == null) return;

        // Try common names people use in this exact setup.
        // If your VelocityWidthSimple uses different names, this will just skip them harmlessly.
        TrySetFieldOrProp(velWidthObj, "widthRange", widthRange);
        TrySetFieldOrProp(velWidthObj, "WidthRange", widthRange);
        TrySetFieldOrProp(velWidthObj, "range", widthRange);
        TrySetFieldOrProp(velWidthObj, "Range", widthRange);

        TrySetFieldOrProp(velWidthObj, "smooth", smooth);
        TrySetFieldOrProp(velWidthObj, "Smooth", smooth);
        TrySetFieldOrProp(velWidthObj, "smoothing", smooth);
        TrySetFieldOrProp(velWidthObj, "Smoothing", smooth);

        // Optional speed mapping knobs
        float sMin = Mathf.Min(speedMin, speedMax - 0.0001f);
        float sMax = Mathf.Max(speedMax, speedMin + 0.0001f);

        TrySetFieldOrProp(velWidthObj, "speedMin", sMin);
        TrySetFieldOrProp(velWidthObj, "SpeedMin", sMin);
        TrySetFieldOrProp(velWidthObj, "velMin", sMin);
        TrySetFieldOrProp(velWidthObj, "VelMin", sMin);

        TrySetFieldOrProp(velWidthObj, "speedMax", sMax);
        TrySetFieldOrProp(velWidthObj, "SpeedMax", sMax);
        TrySetFieldOrProp(velWidthObj, "velMax", sMax);
        TrySetFieldOrProp(velWidthObj, "VelMax", sMax);
    }

    private void TrySetFieldOrProp(object obj, string name, float value)
    {
        var t = obj.GetType();

        // field
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(float))
        {
            f.SetValue(obj, value);
            return;
        }

        // property
        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite && p.PropertyType == typeof(float))
        {
            p.SetValue(obj, value);
        }
    }

    [ContextMenu("Apply Now")]
    public void ApplyNow() => Apply();
}
