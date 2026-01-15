using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PaintManagerCustom : MonoBehaviour
{
    public bool useMouse;
    public bool smoothVelocityPressure = true;
    public bool useComputePath = false;

    [Header("Paint Target (Mesh Plane)")]
    public Renderer paintTarget;                 // ✅ assign the plane's Renderer
    public RenderTexture targetTexture;
    public Material fragmentPaintMaterial;
    public Texture2D brushTexture;
    public ComputeShader brushCompute;

    [Header("Brush Settings")]
    public Color brushColor = Color.white;
    [Range(0.001f, 1f)] public float brushSize = 0.05f;
    public float stampInterval = 0.05f;
    public float distanceThreshold = 0.01f;

    [Header("Velocity Width (Resolution Independent)")]
    [Range(2, 8)] public int velocityWindow = 4;
    public float velMin = 0.0025f;
    public float velMax = 0.08f;
    public float pressureSmooth = 18f;
    public float minPressure = 0.2f;
    public float maxPressure = 1.0f;

    [Header("Legacy / Other")]
    public float maxSpeed = 2000f;

    public BasePaintCustom painter;
    private readonly List<RenderTexture> undoHistory = new List<RenderTexture>();
    public bool isDrawing;

    [Header("Undo")]
    [SerializeField, Range(1, 50)]
    private int maxUndo = 20;

    private Vector2 lastUV;

    [Header("Raycast Camera")]
    public Camera cam;                           // ✅ assign your camera used for ScreenPointToRay

    public BasePaintCustom.PaintMode paintMode = BasePaintCustom.PaintMode.StampInterval;

    public Image border;
    public Color selectedCanvasColor;
    public GameObject[] colorColumns;

    [Header("Stroke Chunking (Undo Segments)")]
    public bool chunkStrokes = true;
    [Min(0.05f)] public float chunkSeconds = 2.0f;

    private bool pendingReseed = false;
    private Vector2 reseedUV;

    [HideInInspector] public bool uiBlockingDrawing = false;
    private bool wasDrawingBeforeUI = false;

    private float nextChunkAt = -1f;
    private float chunkStartedAt = -1f;

    private bool seamBridgePending = false;
    private Vector2 seamBridgeUV;

    public SceneReferences sceneReferences;

    public enum CanvasSize
    {
        size_1024x1024,
        size_1920x1080,
        size_2560x1440,
        size_5120x2880
    }

    public CanvasSize canvasSize;

    // ---------- velocity window ----------
    private struct VelSample { public Vector2 px; public float t; }
    private readonly Queue<VelSample> velSamples = new Queue<VelSample>(8);
    private float smoothedPressure = 1f;

    public GameObject gazeCursor;
    private float gazeCursorSize;

    // ✅ StampInterval robustness (keep stamping briefly at last UV if raycast jitters)
    [Header("StampInterval robustness")]
    public float stampHoldGraceSeconds = 0.15f;

    private float lastValidUVTime = -999f;
    private Vector2 lastValidUV;

    [Header("Raycast Filtering")]
    [Tooltip("Optional: only hits on these layers will be considered. Leave empty to hit everything.")]
    public LayerMask paintLayerMask = ~0;

    private bool initialized = false;

    private void Awake()
    {
        if (gazeCursor != null)
            gazeCursorSize = gazeCursor.transform.localScale.x;

        if (useMouse)
        {
            sceneReferences.gazeUIHover.enabled = false;
            sceneReferences.TobiiSystem.SetActive(false);
        }
    }

    void Start()
    {
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (initialized) return;
        initialized = true;

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) Debug.LogError("PaintManagerCustom: No camera assigned and no Camera.main found.");
        }

        if (paintTarget == null)
            Debug.LogError("PaintManagerCustom: paintTarget is not assigned (plane Renderer). No painting will occur.");

        bool computeOn = useComputePath && (brushCompute != null);

        RenderTextureFormat textureFormat = computeOn
            ? RenderTextureFormat.ARGBHalf
            : RenderTextureFormat.ARGB32;

        if (targetTexture == null)
        {
            switch (canvasSize)
            {
                case CanvasSize.size_1024x1024:
                    targetTexture = new RenderTexture(1024, 1024, 0, textureFormat);
                    if (paintTarget != null) paintTarget.transform.localScale = new Vector3(1, 1, 1);
                    break;
                case CanvasSize.size_1920x1080:
                    targetTexture = new RenderTexture(1920, 1080, 0, textureFormat);
                    if (paintTarget != null) paintTarget.transform.localScale = new Vector3(1.77f, 1, 1);
                    break;
                case CanvasSize.size_2560x1440:
                    targetTexture = new RenderTexture(2560, 1440, 0, textureFormat);
                    if (paintTarget != null) paintTarget.transform.localScale = new Vector3(1.77f, 1, 1);
                    break;
                case CanvasSize.size_5120x2880:
                    targetTexture = new RenderTexture(5120, 2880, 0, textureFormat);
                    if (paintTarget != null) paintTarget.transform.localScale = new Vector3(1.77f, 1, 1);
                    break;
            }
        }
        else
        {
            if (computeOn && targetTexture.IsCreated() && !targetTexture.enableRandomWrite)
            {
                int w = targetTexture.width;
                int h = targetTexture.height;

                targetTexture.Release();
                Destroy(targetTexture);

                targetTexture = new RenderTexture(w, h, 0, textureFormat);
            }
        }

        targetTexture.enableRandomWrite = computeOn;
        if (!targetTexture.IsCreated()) targetTexture.Create();

        // Clear
        var prevRT = RenderTexture.active;
        RenderTexture.active = targetTexture;
        GL.Clear(true, true, selectedCanvasColor);
        RenderTexture.active = prevRT;

        if (paintTarget != null)
            paintTarget.material.mainTexture = targetTexture;

        painter = new BasePaintCustom();
        painter.useComputeIfAvailable = computeOn;

        painter.Init(
            targetTexture, fragmentPaintMaterial, brushTexture, brushColor,
            brushSize, stampInterval, distanceThreshold,
            minPressure, maxPressure, maxSpeed,
            brushCompute
        );

        painter.SetMode(paintMode);
        painter.useExternalPressureForVelocity = true;

        isDrawing = false;

        if (TryGetPointer(out var uv, out var px))
        {
            lastUV = uv;
            lastValidUV = uv;
            lastValidUVTime = Time.unscaledTime;
        }

        PushUndo();
        Debug.Log($"Mode: {painter.CurrentMode} | Compute: {computeOn}");
    }

    public void OnStartButtonPressed()
    {
        this.enabled = true;        // if you disabled the component
        BeginDrawingSession(); // starts drawing + seeds stroke
    }

    public void BeginDrawingSession()
    {
        InitializeIfNeeded();

        // Make sure we’re in a sane state
        uiBlockingDrawing = false;
        pendingReseed = false;
        seamBridgePending = false;

        // Turn drawing ON
        isDrawing = true;

        // Prime velocity
        Vector2 startPx = useMouse ? (Vector2)Input.mousePosition : default;
        if (!useMouse && TobiiGazeProvider.Instance != null)
            TobiiGazeProvider.Instance.TryGetGazeScreenPx(out startPx);

        velSamples.Clear();
        PrimeVelocity(startPx);

        // Seed UV + force an initial visible mark if we’re on the canvas
        if (TryGetPointer(out var uv0, out var _hitPx0))
        {
            lastUV = uv0;
            lastValidUV = uv0;
            lastValidUVTime = Time.unscaledTime;

            // Force a dot immediately so you don’t need a second toggle or movement
            painter.externalPressure = maxPressure;
            painter.UpdateStroke(uv0, 0f);
            painter.Flush();
        }
        else
        {
            // Wait until we get a hit to start stamping lines cleanly
            pendingReseed = true;
        }

        // Undo + chunk timers
        PushUndo();
        chunkStartedAt = Time.unscaledTime;
        nextChunkAt = Time.unscaledTime + chunkSeconds;
    }

    /*void Start()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) Debug.LogError("PaintManagerCustom: No camera assigned and no Camera.main found.");
        }

        if (paintTarget == null)
        {
            Debug.LogError("PaintManagerCustom: paintTarget is not assigned (plane Renderer). No painting will occur.");
        }

        bool computeOn = useComputePath && (brushCompute != null);

        RenderTextureFormat textureFormat = computeOn
            ? RenderTextureFormat.ARGBHalf
            : RenderTextureFormat.ARGB32;

        if (targetTexture == null)
        {
            switch (canvasSize)
            {
                case CanvasSize.size_1024x1024:
                    targetTexture = new RenderTexture(1024, 1024, 0, textureFormat);
                    if (paintTarget != null) paintTarget.transform.localScale = new Vector3(1, 1, 1);
                    break;

                case CanvasSize.size_1920x1080:
                    targetTexture = new RenderTexture(1920, 1080, 0, textureFormat);
                    if (paintTarget != null) paintTarget.transform.localScale = new Vector3(1.77f, 1, 1);
                    break;

                case CanvasSize.size_2560x1440:
                    targetTexture = new RenderTexture(2560, 1440, 0, textureFormat);
                    if (paintTarget != null) paintTarget.transform.localScale = new Vector3(1.77f, 1, 1);
                    break;

                case CanvasSize.size_5120x2880:
                    targetTexture = new RenderTexture(5120, 2880, 0, textureFormat);
                    if (paintTarget != null) paintTarget.transform.localScale = new Vector3(1.77f, 1, 1);
                    break;
            }
        }
        else
        {
            if (computeOn && targetTexture.IsCreated() && !targetTexture.enableRandomWrite)
            {
                int w = targetTexture.width;
                int h = targetTexture.height;

                targetTexture.Release();
                Destroy(targetTexture);

                targetTexture = new RenderTexture(w, h, 0, textureFormat);
            }
        }

        targetTexture.enableRandomWrite = computeOn;
        if (!targetTexture.IsCreated()) targetTexture.Create();

        // Clear canvas
        var prevRT = RenderTexture.active;
        RenderTexture.active = targetTexture;
        GL.Clear(true, true, Color.white);
        RenderTexture.active = prevRT;

        if (paintTarget != null)
            paintTarget.material.mainTexture = targetTexture;

        painter = new BasePaintCustom();
        painter.useComputeIfAvailable = computeOn;

        painter.Init(
            targetTexture, fragmentPaintMaterial, brushTexture, brushColor,
            brushSize, stampInterval, distanceThreshold,
            minPressure, maxPressure, maxSpeed,
            brushCompute
        );

        painter.SetMode(paintMode);
        painter.useExternalPressureForVelocity = true;

        isDrawing = false;

        if (TryGetPointer(out var uv, out var px))
        {
            lastUV = uv;
            lastValidUV = uv;
            lastValidUVTime = Time.unscaledTime;
        }

        PushUndo();
        Debug.Log($"Mode: {painter.CurrentMode} | Compute: {computeOn}");
    }*/

    void Update()
    {
        // --- Input: start/stop drawing (mouse only here) ---
        if (useMouse &&
            Input.GetMouseButtonDown(0) &&
            !SceneReferences.Instance.SelectCanvasScreen.activeSelf &&
            !uiBlockingDrawing &&
            !SceneReferences.Instance.settingsScreen.activeSelf)
        {
            StartStroke((Vector2)Input.mousePosition);
        }
        else if (useMouse && Input.GetMouseButtonUp(0))
        {
            StopStroke();
        }

        if (uiBlockingDrawing)
            goto AfterDraw;

        if (isDrawing)
        {
            // 1) Always sample pointer px for velocity (do NOT gate on raycast hit)
            Vector2 screenPx = useMouse ? (Vector2)Input.mousePosition : default;
            if (!useMouse)
            {
                if (TobiiGazeProvider.Instance == null ||
                    !TobiiGazeProvider.Instance.TryGetGazeScreenPx(out screenPx))
                {
                    goto AfterDraw;
                }
            }

            float pressure = ComputeVelocityPressure(screenPx, Time.deltaTime);
            painter.externalPressure = pressure;

            // 2) Raycast to plane
            bool hitCanvas = TryGetPointer(out var uv, out var _hitPx);

            if (hitCanvas)
            {
                lastValidUV = uv;
                lastValidUVTime = Time.unscaledTime;
            }

            if (pendingReseed && hitCanvas)
            {
                pendingReseed = false;
            }

            // 3) Wall-clock chunking
            if (chunkStrokes && chunkSeconds > 0f && nextChunkAt > 0f)
            {
                float remaining = Mathf.Max(0f, nextChunkAt - Time.unscaledTime);

                if (gazeCursor != null && gazeCursor.activeSelf)
                {
                    float t01 = Mathf.Clamp01(remaining / chunkSeconds);
                    float size = gazeCursorSize * t01;
                    gazeCursor.transform.localScale = new Vector3(size, size, size);
                }

                if (Time.unscaledTime >= nextChunkAt)
                {
                    Vector2 prevUV = lastUV;

                    painter.FinishStroke();
                    PushUndo();

                    chunkStartedAt = Time.unscaledTime;
                    nextChunkAt = Time.unscaledTime + chunkSeconds;

                    if (!hitCanvas)
                    {
                        pendingReseed = true;
                    }
                    else
                    {
                        seamBridgePending = true;
                        seamBridgeUV = prevUV;
                    }
                }
            }

            // 4) Paint: allow StampInterval to keep advancing briefly even if raycast jitters
            bool canUseHeldUV = (Time.unscaledTime - lastValidUVTime) <= stampHoldGraceSeconds;

            bool shouldStampWithoutHit =
                !hitCanvas &&
                canUseHeldUV &&
                painter.CurrentMode == BasePaintCustom.PaintMode.StampInterval;

            if ((hitCanvas || shouldStampWithoutHit) && !pendingReseed)
            {
                Vector2 paintUV = hitCanvas ? uv : lastValidUV;

                reseedUV = paintUV;

                if (seamBridgePending)
                {
                    seamBridgePending = false;
                    painter.UpdateStroke(seamBridgeUV, 0f);
                }

                painter.UpdateStroke(paintUV, Time.deltaTime);
                painter.Flush();
                lastUV = paintUV;
            }

            // Cursor follow
            if (SceneReferences.Instance.cursor.activeSelf)
            {
                SceneReferences.Instance.cursor.transform.position = screenPx;
            }
        }
        else
        {
            Vector2 screenPx = useMouse ? (Vector2)Input.mousePosition : default;
            if (!useMouse)
            {
                if (TobiiGazeProvider.Instance == null ||
                    !TobiiGazeProvider.Instance.TryGetGazeScreenPx(out screenPx))
                {
                    goto AfterDraw;
                }
            }

            if (SceneReferences.Instance.cursor.activeSelf)
            {
                SceneReferences.Instance.cursor.transform.position = screenPx;
            }
        }

        AfterDraw:

        if (Input.GetKeyDown(KeyCode.Z))
            Undo();

        if (Input.GetKeyDown(KeyCode.Space))
            CycleMode();
    }

    private void StartStroke(Vector2 startPx)
    {
        isDrawing = true;
        pendingReseed = false;

        velSamples.Clear();

        if (!useMouse)
        {
            if (TobiiGazeProvider.Instance == null ||
                !TobiiGazeProvider.Instance.TryGetGazeScreenPx(out startPx))
            {
                isDrawing = false;
                return;
            }
        }

        PrimeVelocity(startPx);

        if (TryGetPointer(out var uv0, out var _hitPx0))
        {
            lastUV = uv0;
            lastValidUV = uv0;
            lastValidUVTime = Time.unscaledTime;
        }

        PushUndo();

        chunkStartedAt = Time.unscaledTime;
        nextChunkAt = Time.unscaledTime + chunkSeconds;
    }

    private void StopStroke()
    {
        isDrawing = false;
        pendingReseed = false;
        painter.FinishStroke();

        nextChunkAt = -1f;
        chunkStartedAt = -1f;

        if (gazeCursor != null)
            gazeCursor.transform.localScale = new Vector3(gazeCursorSize, gazeCursorSize, gazeCursorSize);

        seamBridgePending = false;
    }

    void CycleMode()
    {
        int next = ((int)paintMode + 1) % System.Enum.GetValues(typeof(BasePaintCustom.PaintMode)).Length;
        paintMode = (BasePaintCustom.PaintMode)next;
        painter.SetMode(paintMode);
        Debug.Log($"Paint mode changed to: {paintMode}");
    }

    // Returns BOTH uv + screenPx (needed for velocity)
    bool TryGetPointer(out Vector2 uv, out Vector2 screenPx)
    {
        uv = lastUV;
        screenPx = default;

        if (cam == null || paintTarget == null)
            return false;

        if (useMouse)
        {
            screenPx = Input.mousePosition;
        }
        else
        {
            if (TobiiGazeProvider.Instance == null ||
                !TobiiGazeProvider.Instance.TryGetGazeScreenPx(out screenPx))
                return false;
        }

        Ray ray = cam.ScreenPointToRay(screenPx);

        // Use a layer mask to avoid hitting other colliders first.
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, paintLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Ensure we're hitting THIS paint target object (or a child collider)
            if (hit.collider != null && paintTarget != null)
            {
                // If your collider is on the same GameObject as the renderer, this passes.
                // If collider is on a child, this also passes.
                if (hit.collider.transform == paintTarget.transform || hit.collider.transform.IsChildOf(paintTarget.transform))
                {
                    uv = hit.textureCoord;
                    return true;
                }
            }
        }

        return false;
    }

    void PrimeVelocity(Vector2 screenPx)
    {
        float now = Time.unscaledTime;
        velSamples.Enqueue(new VelSample { px = screenPx, t = now });
        smoothedPressure = maxPressure;
    }

    float ComputeVelocityPressure(Vector2 screenPx, float dt)
    {
        float now = Time.unscaledTime;

        velSamples.Enqueue(new VelSample { px = screenPx, t = now });
        while (velSamples.Count > velocityWindow)
            velSamples.Dequeue();

        if (velSamples.Count < 2)
            return smoothedPressure;

        VelSample[] s = velSamples.ToArray();

        float dist = 0f;
        for (int i = 1; i < s.Length; i++)
            dist += (s[i].px - s[i - 1].px).magnitude;

        float time = Mathf.Max(1e-5f, s[^1].t - s[0].t);
        float pxPerSec = dist / time;

        float denom = Mathf.Max(1f, Mathf.Min(targetTexture.width, targetTexture.height));
        float normSpeed = pxPerSec / denom;

        float t01 = Mathf.InverseLerp(velMin, velMax, normSpeed);
        float targetPressure = Mathf.Lerp(maxPressure, minPressure, t01);

        if (smoothVelocityPressure)
        {
            float a = 1f - Mathf.Exp(-pressureSmooth * Mathf.Max(1e-5f, dt));
            smoothedPressure = Mathf.Lerp(smoothedPressure, targetPressure, a);
        }
        else
        {
            smoothedPressure = targetPressure;
        }

        return smoothedPressure;
    }

    void PushUndo()
    {
        if (targetTexture == null) return;

        var rt = new RenderTexture(targetTexture.descriptor)
        {
            useMipMap = false,
            autoGenerateMips = false
        };
        rt.Create();

        Graphics.Blit(targetTexture, rt);

        undoHistory.Add(rt);

        while (undoHistory.Count > maxUndo)
        {
            var oldest = undoHistory[0];
            undoHistory.RemoveAt(0);
            DisposeRT(oldest);
        }
    }

    public void Undo() => Undo(1);

    public void Undo(int steps)
    {
        bool resumeAfter = isDrawing;
        isDrawing = false;
        pendingReseed = false;
        painter.FinishStroke();

        nextChunkAt = -1f;
        chunkStartedAt = -1f;

        for (int i = 0; i < steps; i++)
        {
            if (undoHistory.Count < 2) break;

            var current = undoHistory[undoHistory.Count - 1];
            undoHistory.RemoveAt(undoHistory.Count - 1);
            DisposeRT(current);

            var prev = undoHistory[undoHistory.Count - 1];
            Graphics.Blit(prev, targetTexture);
        }

        if (resumeAfter && !uiBlockingDrawing)
        {
            isDrawing = true;
            pendingReseed = true;

            chunkStartedAt = Time.unscaledTime;
            nextChunkAt = Time.unscaledTime + chunkSeconds;
        }

        seamBridgePending = false;
    }

    public void BeginUIBlock()
    {
        uiBlockingDrawing = true;
        wasDrawingBeforeUI = isDrawing;

        isDrawing = false;
        pendingReseed = false;
        painter.FinishStroke();

        nextChunkAt = -1f;
        chunkStartedAt = -1f;

        seamBridgePending = false;
    }

    public void EndUIBlock()
    {
        uiBlockingDrawing = false;
        seamBridgePending = false;

        if (wasDrawingBeforeUI)
        {
            isDrawing = true;
            pendingReseed = true;

            chunkStartedAt = Time.unscaledTime;
            nextChunkAt = Time.unscaledTime + chunkSeconds;
        }
    }

    public void FloodFill(Image buttonImage)
    {
        Color uiColor = buttonImage.color;

        Color clearColor = (QualitySettings.activeColorSpace == ColorSpace.Linear)
            ? uiColor.linear
            : uiColor;

        selectedCanvasColor = clearColor;

        var prev = RenderTexture.active;
        RenderTexture.active = targetTexture;

        border.color = uiColor;
        GL.Clear(true, true, clearColor);

        RenderTexture.active = prev;
    }

    public void SetBrushColor(Image buttonImage)
    {
        Color c = buttonImage.color;
        painter.Flush();
        painter.SetBrushColor(c);
        brushColor = c;
        Debug.Log("SetBrushColor clicked");
    }

    public void SetBrushSize(float s)
    {
        painter.Flush();
        painter.SetBrushSize(s);
        brushSize = s;
    }

    public void SetBrushTexture(Texture2D tex)
    {
        painter.Flush();
        painter.SetBrushTexture(tex);
        brushTexture = tex;
    }

    public void QuitNow()
    {
        Application.Quit();
    }

    public void ToggleIsDrawing()
    {
        SceneReferences sceneReferences = SceneReferences.Instance;

        isDrawing = !isDrawing;
        if (!isDrawing)
        {
            sceneReferences.pauseCursor.SetActive(true);
            sceneReferences.gazeCursor.GetComponent<Image>().enabled = false;
            sceneReferences.pauseButton.SetActive(true);

            nextChunkAt = -1f;
            chunkStartedAt = -1f;

            painter.FinishStroke();
            pendingReseed = false;
            seamBridgePending = false;
        }
        else
        {
            sceneReferences.pauseCursor.SetActive(false);
            sceneReferences.gazeCursor.GetComponent<Image>().enabled = true;
            sceneReferences.pauseButton.SetActive(false);

            chunkStartedAt = Time.unscaledTime;
            nextChunkAt = Time.unscaledTime + chunkSeconds;

            Vector2 startPx = useMouse ? (Vector2)Input.mousePosition : default;
            if (!useMouse && TobiiGazeProvider.Instance != null)
                TobiiGazeProvider.Instance.TryGetGazeScreenPx(out startPx);

            velSamples.Clear();
            PrimeVelocity(startPx);
            PushUndo();
        }

        sceneReferences.playButton.interactable = false;
        Debug.Log("Toggle isDrawing Triggered");

        Invoke("RestartPauseButton", 1f);
    }

    public void RestartPauseButton()
    {
        SceneReferences.Instance.playButton.interactable = true;
    }

    public void EnableIsDrawing()
    {
        isDrawing = true;

        chunkStartedAt = Time.unscaledTime;
        nextChunkAt = Time.unscaledTime + chunkSeconds;
    }

    public void TrashDrawing()
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = targetTexture;

        border.color = selectedCanvasColor;

        GL.Clear(true, true, selectedCanvasColor);

        RenderTexture.active = prev;
    }

    public void ActivateColorColumn(int columnNum)
    {
        foreach (GameObject column in colorColumns)
            column.SetActive(false);

        colorColumns[columnNum].SetActive(true);
    }

    public void BlitImageContained(Texture source, Color clearColor)
    {
        if (targetTexture == null || source == null) return;

        float canvasW = targetTexture.width;
        float canvasH = targetTexture.height;

        float srcW = source.width;
        float srcH = source.height;

        float canvasAspect = canvasW / canvasH;
        float srcAspect = srcW / srcH;

        int drawW, drawH, drawX, drawY;

        if (srcAspect > canvasAspect)
        {
            drawW = (int)canvasW;
            drawH = Mathf.RoundToInt(canvasW / srcAspect);
            drawX = 0;
            drawY = Mathf.RoundToInt((canvasH - drawH) * 0.5f);
        }
        else
        {
            drawH = (int)canvasH;
            drawW = Mathf.RoundToInt(canvasH * srcAspect);
            drawX = Mathf.RoundToInt((canvasW - drawW) * 0.5f);
            drawY = 0;
        }

        var prev = RenderTexture.active;

        RenderTexture.active = targetTexture;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, canvasW, canvasH, 0);
        GL.Clear(true, true, clearColor);

        Graphics.DrawTexture(new Rect(drawX, drawY, drawW, drawH), source);

        GL.PopMatrix();

        RenderTexture.active = prev;
    }

    private static void DisposeRT(RenderTexture rt)
    {
        if (rt == null) return;
        if (rt.IsCreated()) rt.Release();
        Object.Destroy(rt);
    }

    public void SetStampInterval(float seconds)
    {
        stampInterval = Mathf.Max(0.0001f, seconds);
        if (painter != null)
            painter.SetStampInterval(stampInterval);
    }

    public void SetResolution(Toggle triggeredToggle)
    {
        if (!triggeredToggle.isOn) return;
        else
        {
            string toggleName = triggeredToggle.name;

            switch (toggleName)
            {
                case "Resolution_Toggle_1920x1080":
                    Debug.Log("Resolution switched to 1920x1080");
                    canvasSize = CanvasSize.size_1920x1080;
                    PlayerPrefs.SetInt("resolution",0);
                    break;
                case "Resolution_Toggle_2560x1440":
                    Debug.Log("Resolution switched to 2560x1440");
                    canvasSize = CanvasSize.size_2560x1440;
                    PlayerPrefs.SetInt("resolution", 1);
                    break;
                case "Resolution_Toggle_5120x2880":
                    Debug.Log("Resolution switched to 5120x2880");
                    canvasSize = CanvasSize.size_5120x2880;
                    PlayerPrefs.SetInt("resolution", 2);
                    break;
                default:
                    Debug.Log("DEFAULT TRIGGERED: Resolution switched to 1920x1080");
                    canvasSize = CanvasSize.size_1920x1080;
                    PlayerPrefs.SetInt("resolution", 0);
                    break;
            }
        }
    }
}
