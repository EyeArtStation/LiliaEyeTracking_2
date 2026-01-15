#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Tobii.GameIntegration.Net;

public class TobiiTgiManager : MonoBehaviour
{
    public static TobiiTgiManager Instance { get; private set; }

    [Header("Tracking")]
    [Tooltip("In a Windows player build, prefer TrackWindow (best for multi-monitor).")]
    public bool preferTrackWindowInPlayer = true;

    [Tooltip("Seconds between retrying TrackWindow / TrackRectangle if it fails.")]
    public float trackRetrySeconds = 1.0f;

    [Header("Debug")]
    public bool logStatus = true;
    public float logIntervalSeconds = 2.0f;

    // --- internal state ---
    IntPtr _hwnd = IntPtr.Zero;

    bool _isTrackingSet;
    float _nextTrackAttempt;
    float _nextWarn;
    float _nextLog;

    int _lastW, _lastH;
    bool _lastPresent;
    bool _lastConnected;
    bool _lastEnabled;

    public bool IsTrackerConnected { get; private set; }
    public bool IsTrackerEnabled { get; private set; }
    public bool IsPresent { get; private set; }
    public string TrackerName { get; private set; } = "(unknown)";

    void Awake()
    {
        // --- Singleton enforcement ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Identify the app to Tobii
        TobiiGameIntegrationApi.SetApplicationName(Application.productName);

        UnityEngine.Debug.Log($"[TobiiTGI] LoadedDll={TobiiGameIntegrationApi.LoadedDll}");

        // Prime the API once
        TobiiGameIntegrationApi.Update();

        _nextTrackAttempt = 0f;
        _nextWarn = 0f;
        _nextLog = 0f;

        _lastW = Display.main.systemWidth;
        _lastH = Display.main.systemHeight;
        _lastPresent = false;
        _lastConnected = false;
        _lastEnabled = false;

#if !UNITY_EDITOR
        if (preferTrackWindowInPlayer)
        {
            _hwnd = TobiiWin32Window.FindUnityWindowHwnd();
            if (_hwnd != IntPtr.Zero)
                UnityEngine.Debug.Log($"[TobiiTGI] Unity HWND: 0x{_hwnd.ToInt64():X}");
            else
                UnityEngine.Debug.LogWarning("[TobiiTGI] Could not find Unity HWND yet; will retry.");
        }
#endif
    }

    void Update()
    {
        // IMPORTANT: this is the ONLY place Update() is called
        TobiiGameIntegrationApi.Update();

        // Cache tracker state
        IsTrackerConnected = TobiiGameIntegrationApi.IsTrackerConnected();
        IsTrackerEnabled = TobiiGameIntegrationApi.IsTrackerEnabled();
        IsPresent = TobiiGameIntegrationApi.IsPresent();

        var info = TobiiGameIntegrationApi.GetTrackerInfo();
        TrackerName = info == null ? "NULL Device" : info.FriendlyName;

        // Detect conditions that should cause retracking
        int w = Display.main.systemWidth;
        int h = Display.main.systemHeight;

        bool displayChanged = (w != _lastW) || (h != _lastH);
        bool presentChanged = (IsPresent != _lastPresent);
        bool trackerChanged =
            (IsTrackerConnected != _lastConnected) ||
            (IsTrackerEnabled != _lastEnabled);

        // If we lost tracker/presence or display changed, we should re-apply tracking.
        // Otherwise, once we succeed once, we stop spamming calls.
        bool shouldTrack =
            !_isTrackingSet ||
            displayChanged ||
            presentChanged ||
            trackerChanged ||
            !IsTrackerConnected ||
            !IsTrackerEnabled;

        if (shouldTrack && Time.unscaledTime >= _nextTrackAttempt)
        {
            _nextTrackAttempt = Time.unscaledTime + Mathf.Max(0.2f, trackRetrySeconds);

#if !UNITY_EDITOR
            // In builds, prefer TrackWindow (multi-monitor robust). If hwnd not found yet, keep trying.
            if (preferTrackWindowInPlayer && _hwnd == IntPtr.Zero)
                _hwnd = TobiiWin32Window.FindUnityWindowHwnd();
#endif

            bool ok = EnsureTracking();

            if (ok)
            {
                _isTrackingSet = true;
            }
            else
            {
                // Rate-limit warnings so you don't get cascades forever
                if (Time.unscaledTime >= _nextWarn)
                {
                    _nextWarn = Time.unscaledTime + 2.0f; // warn at most every 2s
                    UnityEngine.Debug.LogWarning($"[TobiiTGI] Tracking target rejected (connected={IsTrackerConnected}, enabled={IsTrackerEnabled}, present={IsPresent}). Will retry.");
                }
            }
        }

        if (logStatus && Time.unscaledTime >= _nextLog)
        {
            _nextLog = Time.unscaledTime + Mathf.Max(0.5f, logIntervalSeconds);
            UnityEngine.Debug.Log(
                $"[TobiiTGI] connected={IsTrackerConnected} " +
                $"enabled={IsTrackerEnabled} present={IsPresent} tracker={TrackerName} " +
                $"trackingSet={_isTrackingSet} hwnd=0x{_hwnd.ToInt64():X}"
            );
        }

        // Cache last values
        _lastW = w; _lastH = h;
        _lastPresent = IsPresent;
        _lastConnected = IsTrackerConnected;
        _lastEnabled = IsTrackerEnabled;
    }

    bool EnsureTracking()
    {
#if UNITY_EDITOR
        // Editor: Track OS display rectangle (GameView HWND is unreliable)
        var rect = new TobiiRectangle
        {
            Left = 0,
            Top = 0,
            Right = Display.main.systemWidth,
            Bottom = Display.main.systemHeight
        };

        return TobiiGameIntegrationApi.TrackRectangle(rect);
#else
        // Player build: TrackWindow is preferred for multi-monitor correctness.
        if (preferTrackWindowInPlayer && _hwnd != IntPtr.Zero)
        {
            return TobiiGameIntegrationApi.TrackWindow(_hwnd);
        }

        // Fallback: track the OS display rectangle
        var rect = new TobiiRectangle
        {
            Left = 0,
            Top = 0,
            Right = Display.main.systemWidth,
            Bottom = Display.main.systemHeight
        };

        return TobiiGameIntegrationApi.TrackRectangle(rect);
#endif
    }

    // NOTE:
    // While iterating in the Unity Editor, DO NOT call Shutdown().
    // In a production standalone build, you may optionally add:
    //
    // void OnApplicationQuit()
    // {
    //     TobiiGameIntegrationApi.Shutdown();
    // }

    /// <summary>
    /// Win32 helper to reliably locate the current Unity window handle in a player build.
    /// </summary>
    static class TobiiWin32Window
    {
        // Try a few strategies:
        // 1) active window (often correct)
        // 2) process main window handle
        // 3) enumerate windows for this process and pick best candidate
        public static IntPtr FindUnityWindowHwnd()
        {
            // Strategy 1: active window
            IntPtr active = GetForegroundWindow();
            if (active != IntPtr.Zero)
            {
                uint pid;
                GetWindowThreadProcessId(active, out pid);
                if (pid == (uint)Process.GetCurrentProcess().Id && IsGoodCandidate(active))
                    return active;
            }

            // Strategy 2: main window handle
            try
            {
                var p = Process.GetCurrentProcess();
                if (p.MainWindowHandle != IntPtr.Zero && IsGoodCandidate(p.MainWindowHandle))
                    return p.MainWindowHandle;
            }
            catch { /* ignore */ }

            // Strategy 3: enumerate
            IntPtr best = IntPtr.Zero;
            uint myPid = (uint)Process.GetCurrentProcess().Id;

            EnumWindows((hWnd, lParam) =>
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid != myPid) return true;

                if (!IsGoodCandidate(hWnd)) return true;

                // Prefer visible, non-tool windows with largest area.
                if (best == IntPtr.Zero)
                {
                    best = hWnd;
                }
                else
                {
                    long areaBest = GetArea(best);
                    long areaNew = GetArea(hWnd);
                    if (areaNew > areaBest)
                        best = hWnd;
                }

                return true;
            }, IntPtr.Zero);

            return best;
        }

        static bool IsGoodCandidate(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            if (!IsWindowVisible(hWnd)) return false;

            // Filter out tool/owned windows
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;

            // Must have a non-trivial rect
            if (!GetWindowRect(hWnd, out RECT r)) return false;
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w < 100 || h < 100) return false;

            return true;
        }

        static long GetArea(IntPtr hWnd)
        {
            if (!GetWindowRect(hWnd, out RECT r)) return 0;
            long w = (long)(r.Right - r.Left);
            long h = (long)(r.Bottom - r.Top);
            return Math.Max(0, w) * Math.Max(0, h);
        }

        // --- Win32 ---
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
#endif
