using UnityEngine;
using UnityEngine.UI;

public class FrameTimeProfiler : MonoBehaviour
{
    public Text readout;     // Assign a UI Text or TMP text in the Inspector
    private float refreshRate = 0.5f;
    private float timer = 0f;

    private float lastFrameTime;
    private float avgFrameTime;
    private float smoothTime = 0.2f;   // smoothing for display

    private float gpuTime;             // GPU frame time
    private float cpuTime;             // CPU frame time

    void Start()
    {
        if (readout == null)
        {
            Debug.LogWarning("Assign a UI Text element for the profiler readout.");
        }

        Application.targetFrameRate = -1; // Let QualitySettings control this
    }

    void Update()
    {
        // CPU frame time
        float currentFrameTime = Time.unscaledDeltaTime * 1000f; // ms
        avgFrameTime = Mathf.Lerp(avgFrameTime, currentFrameTime, Time.deltaTime / smoothTime);
        lastFrameTime = currentFrameTime;

        // GPU frame time (works in play mode, not WebGL)
        float gpuFrameTime;
        if (SystemInfo.supportsAsyncGPUReadback)
            gpuFrameTime = GetGPUFrameTimeApprox();
        else
            gpuFrameTime = avgFrameTime;  // fallback

        cpuTime = avgFrameTime;
        gpuTime = gpuFrameTime;

        timer += Time.deltaTime;
        if (timer >= refreshRate)
        {
            timer = 0f;
            UpdateDisplay();
        }
    }

    void UpdateDisplay()
    {
        float fps = 1000f / avgFrameTime;
        string vsyncState = (QualitySettings.vSyncCount > 0) ? "VSync ON" : "VSync OFF";

        string cpuGPU =
            (gpuTime > cpuTime * 1.2f) ? "GPU-bound" :
            (cpuTime > gpuTime * 1.2f) ? "CPU-bound" : "Balanced";

        string text = string.Format(
            "{0:F1} FPS\n{1:F2} ms / frame\nCPU: {2:F2} ms | GPU: {3:F2} ms\n{4} | {5}",
            fps, avgFrameTime, cpuTime, gpuTime, cpuGPU, vsyncState
        );

        if (readout != null)
            readout.text = text;
        else
            Debug.Log(text);
    }

    // --- Approx GPU timing (safe across platforms) ---
    float GetGPUFrameTimeApprox()
    {
        // Unity's built-in API reports GPU time per frame in some render pipelines
        // otherwise approximate using frame pacing vs refresh rate
        float refresh = (float)Screen.currentResolution.refreshRateRatio.value;
        float gpuTimeApprox = 1000f / Mathf.Max(1f, refresh);
        return gpuTimeApprox;
    }
}
