using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required for TextMeshPro

public class FPSDisplay : MonoBehaviour
{
    public float updateInterval = 0.5f; // How often to update the FPS display
    private float accum = 0; // FPS accumulated over the interval
    private int frames = 0; // Frames drawn over the interval
    private float timeleft; // Left time for current interval

    public Text fpsText;
    //public TextMeshProUGUI fpsText; // Reference to your TextMeshProUGUI element

    [Header("Toggle Settings")]
    //public KeyCode toggleKey = KeyCode.F1;   // Press to toggle
    public bool vsyncEnabled = true;         // Default: use quality settings (normal mode)

    // We'll cache the original quality settings at startup
    private int originalVSyncCount;
    private int originalTargetFrameRate;

    void Start()
    {
        // Store whatever the project is currently using
        originalVSyncCount = QualitySettings.vSyncCount;
        originalTargetFrameRate = Application.targetFrameRate;

        ApplySettings();
        Debug.Log($"[VSyncToggle] Started with VSync {(vsyncEnabled ? "ENABLED (using quality settings)" : "DISABLED (manual override)")}.");

        if (fpsText == null)
        {
            Debug.LogError("FPS TextMeshProUGUI not assigned!");
            enabled = false; // Disable the script if no text element is assigned
            return;
        }
        timeleft = updateInterval;
    }

    void Update()
    {
        timeleft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        ++frames;

        // Interval ended - update GUI text and start new interval
        if (timeleft <= 0.0f)
        {
            float fps = accum / frames;
            string format = System.String.Format("{0:F2} FPS", fps);
            fpsText.text = format;

            if (fps < 30)
                fpsText.color = Color.yellow;
            else if (fps < 10)
                fpsText.color = Color.red;
            else
                fpsText.color = Color.green;

            timeleft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            vsyncEnabled = !vsyncEnabled;
            ApplySettings();
        }
    }

    void ApplySettings()
    {
        if (vsyncEnabled)
        {
            // Revert to whatever Unity quality settings had originally
            QualitySettings.vSyncCount = originalVSyncCount;
            Application.targetFrameRate = originalTargetFrameRate;
            Debug.Log("[VSyncToggle] Restored original Quality Settings for VSync and frame rate.");
        }
        else
        {
            // Manual override: disable VSync and uncap FPS
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Debug.Log("[VSyncToggle] VSync DISABLED and frame rate UNCAPPED (diagnostic mode).");
        }
    }
}