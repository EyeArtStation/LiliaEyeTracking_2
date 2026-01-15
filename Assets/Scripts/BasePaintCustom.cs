using System.Collections.Generic;
using UnityEngine;

public class BasePaintCustom
{
    [Header("Optimization Settings")]
    public bool useDirtyRegionBlit = true;
    public bool forceSafeBlit = false;
    public bool useComputeIfAvailable = true;

    // ✅ NEW: external pressure injection (set from PaintManagerCustom)
    public bool useExternalPressureForVelocity = false;
    [Range(0f, 1f)] public float externalPressure = 1f;

    public enum PaintMode
    {
        StampInterval,
        StampDistance,
        InterpolatedLine,
        VelocityLineWidth
    }

    // ---------- Shared fields ----------
    private RenderTexture target;
    private Material mat;
    private Texture2D brush;
    public Color color;

    // Size is treated as UV units (0..1 relative to canvas width) like your prior code
    private float size;
    private float interval;
    private float distanceThreshold;

    private float minP, maxP, maxSpeed;

    [Header("Velocity Width Tuning")]
    public bool smoothPressure = false;

    [Header("Velocity Width - Attack/Release")]
    public float thinAttackRate = 50f;
    public float thickReleaseRate = 50f;
    public float speedDeadZone = 0.0f;

    private Vector2? lastUV;
    private float stampTimer;

    private float currentPressure = 1f;
    private float smoothedSpeed = 0f;
    private PaintMode mode = PaintMode.StampInterval;

    private const int MaxBatch = 128;

    private struct BrushStamp 
    { 
        public Vector2 uv; 
        public float size;
        public float rotationRad;
        public Color color; 
    
    }
    private readonly List<BrushStamp> stampQueue = new List<BrushStamp>(MaxBatch);

    private readonly Vector4[] stampDataCache = new Vector4[MaxBatch];
    private readonly Vector4[] colorDataCache = new Vector4[MaxBatch];

    public PaintMode CurrentMode => mode;
    public void SetMode(PaintMode newMode) => mode = newMode;

    // Dirty region state
    private Vector2 dirtyMin = new Vector2(1f, 1f);
    private Vector2 dirtyMax = new Vector2(0f, 0f);
    private bool hasDirtyRegion = false;

    // ---------- Compute path ----------
    private ComputeShader compute;
    private int kPaintFull = -1;
    private int kPaintDirty = -1;

    private ComputeBuffer stampBuffer;
    private RenderTexture sourceRT;

    // float2 uv (8) + float size (4) + rotation (4) + float4 color (16) = 32
    private const int StampStride = 32;

    /// overlapInterval and strokeSmoothness govern distance between stamps
    public float overlapInterval = 0.20f;

    [Range(0.1f, 1.0f)]
    public float strokeSmoothness = 0.35f;

    public bool randomRotation = false;
    public float rotationAmount;

    public void Init(RenderTexture targetTexture, Material paintMat, Texture2D brushTex,
                     Color brushColor, float brushSize, float stampInterval,
                     float distThreshold, float minPressure, float maxPressure, float maxVel,
                     ComputeShader optionalCompute = null)
    {
        target = targetTexture;
        mat = paintMat;
        brush = brushTex;
        color = brushColor;

        size = brushSize;
        interval = stampInterval;
        distanceThreshold = distThreshold;

        minP = minPressure;
        maxP = maxPressure;
        maxSpeed = maxVel;

        // fragment material defaults
        mat.SetInt("_StampCount", 0);
        mat.SetFloat("_RegionX", 0f);
        mat.SetFloat("_RegionY", 0f);
        mat.SetFloat("_RegionW", 1f);
        mat.SetFloat("_RegionH", 1f);
        mat.SetInt("_RegionSample", 0);

        compute = optionalCompute;
        if (compute != null)
        {
            kPaintFull = compute.FindKernel("PaintFull");
            kPaintDirty = compute.FindKernel("PaintDirty");

            if (!target.enableRandomWrite)
            {
                Debug.LogWarning("BasePaintCustom: target must have enableRandomWrite=true for compute. Falling back to fragment path.");
                compute = null;
            }
            else
            {
                stampBuffer = new ComputeBuffer(MaxBatch, StampStride, ComputeBufferType.Structured);
            }
        }
    }

    public void SetBrushColor(Color c)
    {
        color = c;
    }

    public void SetBrushTexture(Texture2D tex)
    {
        if (tex != null) brush = tex;
    }

    public void SetBrushSize(float normalizedUVSize)
    {
        // Your code treats size as UV units (0..1 relative to canvas width) :contentReference[oaicite:2]{index=2}
        size = Mathf.Max(0.0001f, normalizedUVSize);
    }

    public void SetStampInterval(float seconds)
    {
        interval = Mathf.Max(0.0001f, seconds);
    }

    public void SetDistanceThreshold(float uvDist)
    {
        distanceThreshold = Mathf.Max(0.0000001f, uvDist);
    }

    public void Dispose()
    {
        stampQueue.Clear();

        if (stampBuffer != null)
        {
            stampBuffer.Dispose();
            stampBuffer = null;
        }

        if (sourceRT != null)
        {
            sourceRT.Release();
            sourceRT = null;
        }
    }

    public void UpdateStroke(Vector2 uv, float deltaTime)
    {
        if (target == null) return;

        stampTimer += deltaTime;

        // compute pressure (internal velocity or external injection)
        float dt = Mathf.Max(deltaTime, 0.0001f);
        float targetPressure = currentPressure;

        if (mode == PaintMode.VelocityLineWidth && useExternalPressureForVelocity)
        {
            // ✅ NEW: trust the manager (already windowed + normalized)
            targetPressure = Mathf.Clamp(externalPressure, minP, maxP);
        }
        else if (lastUV.HasValue)
        {
            // Original UV-speed-to-pressure mapping
            float distUV = Vector2.Distance(lastUV.Value, uv);
            float speed = distUV / dt;

            if (speedDeadZone > 0f && speed < speedDeadZone) speed = 0f;

            float k = 1f - Mathf.Exp(-dt / 0.005f);
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, k);

            float x = Mathf.Clamp01(smoothedSpeed / Mathf.Max(0.0001f, maxSpeed));
            x = Mathf.Pow(x, 4.0f);

            targetPressure = Mathf.Lerp(maxP, minP, x);
        }

        if (smoothPressure)
        {
            float rate = (targetPressure < currentPressure) ? thinAttackRate : thickReleaseRate;
            float a = 1f - Mathf.Exp(-rate * dt);
            currentPressure = Mathf.Lerp(currentPressure, targetPressure, a);
        }
        else
        {
            currentPressure = targetPressure;
        }

        switch (mode)
        {
            case PaintMode.StampInterval:
                if (stampTimer >= interval)
                {
                    QueueStamp(uv, size);
                    stampTimer = 0f;
                }
                break;

            case PaintMode.StampDistance:
                if (!lastUV.HasValue || Vector2.Distance(lastUV.Value, uv) >= distanceThreshold)
                {
                    QueueStamp(uv, size);
                    lastUV = uv;
                }
                break;

            case PaintMode.InterpolatedLine:
                if (lastUV.HasValue)
                    TryRenderLine(lastUV.Value, uv, 1f, 1f);
                else
                    QueueStamp(uv, size);

                lastUV = uv;
                break;

            case PaintMode.VelocityLineWidth:
                if (lastUV.HasValue)
                    TryRenderLine(lastUV.Value, uv, currentPressure, currentPressure);
                else
                    QueueStamp(uv, size * currentPressure);

                lastUV = uv;
                break;
        }

        if (stampQueue.Count >= MaxBatch)
            Flush();
    }

    public void FinishStroke()
    {
        lastUV = null;
        stampTimer = 0f;
        Flush();
    }

    private void QueueStamp(Vector2 uv, float scaledSize)
    {
        float rotRad = 0;
        if (randomRotation)
        {
            rotRad = Random.Range(-30f, 30f) * Mathf.Deg2Rad;
        }
        else
        {
            rotRad = rotationAmount * Mathf.Deg2Rad;
        }
        stampQueue.Add(new BrushStamp { uv = uv, size = scaledSize, rotationRad = rotRad, color = color });

        if (useDirtyRegionBlit)
        {
            // Expand dirty by brush footprint in UV units.
            // If your shader treats size as radius, this is correct.
            Vector2 brushExtent = Vector2.one * scaledSize;

            if (!hasDirtyRegion)
            {
                dirtyMin = uv - brushExtent;
                dirtyMax = uv + brushExtent;
                hasDirtyRegion = true;
            }
            else
            {
                dirtyMin = Vector2.Min(dirtyMin, uv - brushExtent);
                dirtyMax = Vector2.Max(dirtyMax, uv + brushExtent);
            }
        }
    }

    /// <summary>
    /// Resolution-independent line stamping that avoids gaps when brush gets thin:
    /// step size is based on the *effective stamp diameter* (size * pressure).
    /// </summary>
    private void TryRenderLine(Vector2 startUV, Vector2 endUV, float pressureStart, float pressureEnd)
    {
        float distUV = Vector2.Distance(startUV, endUV);
        if (distUV <= 0.0000001f)
        {
            QueueStamp(endUV, size * pressureEnd);
            return;
        }

        // Convert to pixel distance (using width keeps behavior stable across aspect)
        float distPixels = distUV * target.width;

        float avgPressure = Mathf.Clamp01((pressureStart + pressureEnd) * 0.5f);

        // Effective stamp diameter in pixels at this segment's pressure
        float stampPx = Mathf.Max(0.25f, (size * avgPressure) * target.width);

        // Artistic upper bound (fewer stamps = faster)
        float baseBrushPx = Mathf.Max(0.0001f, size * target.width);
        float maxStep = Mathf.Max(0.5f, baseBrushPx * strokeSmoothness);

        // Overlap control: lower => more overlap (less dashes)
        float overlapStep = stampPx * overlapInterval;

        float pixelStep = Mathf.Clamp(overlapStep, 0.5f, maxStep);

        int steps = Mathf.CeilToInt(distPixels / Mathf.Max(0.0001f, pixelStep));
        steps = Mathf.Clamp(steps, 1, 2048);

        float startP = Mathf.Clamp(pressureStart, 0.01f, 10f);
        float endP = Mathf.Clamp(pressureEnd, 0.01f, 10f);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 interpUV = Vector2.Lerp(startUV, endUV, t);

            float interpPressure = Mathf.SmoothStep(startP, endP, t);
            QueueStamp(interpUV, size * interpPressure);
        }
    }

    // ---------- Flush ----------
    public void Flush()
    {
        if (stampQueue.Count == 0)
            return;

        // Keep dirty region for the entire flush (multiple batches)
        bool wantDirty = useDirtyRegionBlit && hasDirtyRegion;

        // Render everything in MaxBatch chunks (don’t drop stamps)
        int safety = 0;
        while (stampQueue.Count > 0)
        {
            int count = Mathf.Min(stampQueue.Count, MaxBatch);

            for (int i = 0; i < count; i++)
            {
                var s = stampQueue[i];
                stampDataCache[i] = new Vector4(s.uv.x, s.uv.y, s.size, s.rotationRad);
                colorDataCache[i] = (Vector4)s.color;
            }

            bool didCompute = false;
            if (compute != null && useComputeIfAvailable && stampBuffer != null && target != null)
            {
                didCompute = ComputeFlush(count);
            }

            if (!didCompute)
            {
                mat.SetInt("_StampCount", count);
                mat.SetVectorArray("_StampData", stampDataCache);
                mat.SetVectorArray("_StampColors", colorDataCache);
                mat.SetTexture("_MainTex", target);
                mat.SetTexture("_BrushTex", brush);
                mat.SetFloat("_Aspect", (float)target.width / target.height);

                if (!wantDirty)
                {
                    RenderTexture temp = RenderTexture.GetTemporary(target.descriptor);
                    Graphics.Blit(target, temp, mat);
                    Graphics.Blit(temp, target);
                    RenderTexture.ReleaseTemporary(temp);
                }
                else
                {
                    // IMPORTANT: the dirty methods must NOT ClearDirty() per-batch anymore
                    if (forceSafeBlit) DoSafePingPongDirtyBlit();
                    else DoFastSingleDirtyBlit();
                }
            }

            // Remove the batch we just rendered
            stampQueue.RemoveRange(0, count);

            // Just in case (prevents infinite loops if something goes weird)
            if (++safety > 5000) { stampQueue.Clear(); break; }
        }

        // Clear dirty once at the end (not inside dirty blit methods anymore)
        if (wantDirty)
            ClearDirty();
    }


    // ---------- Compute flush ----------
    private bool ComputeFlush(int count)
    {
        StampUpload[] upload = new StampUpload[count];
        for (int i = 0; i < count; i++)
        {
            var s = stampQueue[i];
            upload[i] = new StampUpload
            {
                uv = s.uv,
                size = s.size,
                rotationRad = s.rotationRad,
                color = s.color
            };
        }
        stampBuffer.SetData(upload);

        EnsureSourceRT();
        Graphics.Blit(target, sourceRT);

        compute.SetInt("_CanvasW", target.width);
        compute.SetInt("_CanvasH", target.height);
        compute.SetFloat("_Aspect", (float)target.width / target.height);
        compute.SetInt("_StampCount", count);

        compute.SetTexture(kPaintFull, "_Source", sourceRT);
        compute.SetTexture(kPaintFull, "_Target", target);
        compute.SetTexture(kPaintDirty, "_Source", sourceRT);
        compute.SetTexture(kPaintDirty, "_Target", target);

        compute.SetTexture(kPaintFull, "_BrushTex", brush);
        compute.SetTexture(kPaintDirty, "_BrushTex", brush);

        compute.SetBuffer(kPaintFull, "_Stamps", stampBuffer);
        compute.SetBuffer(kPaintDirty, "_Stamps", stampBuffer);

        if (!useDirtyRegionBlit || !hasDirtyRegion)
        {
            compute.SetFloat("_RegionX", 0f);
            compute.SetFloat("_RegionY", 0f);
            compute.SetFloat("_RegionW", 1f);
            compute.SetFloat("_RegionH", 1f);

            int gx = (target.width + 7) / 8;
            int gy = (target.height + 7) / 8;
            compute.Dispatch(kPaintFull, gx, gy, 1);

            //ClearDirty();
            return true;
        }
        else
        {
            //const int EXTRA_PAD_PX = 4; // try 2, bump to 4 if needed

            int padPx = Mathf.RoundToInt(size * target.width * 2f);
            //int padPx = Mathf.RoundToInt(size * target.width * 2f) + EXTRA_PAD_PX;

            int xMin = Mathf.Max(0, Mathf.FloorToInt(dirtyMin.x * target.width) - padPx);
            int yMin = Mathf.Max(0, Mathf.FloorToInt(dirtyMin.y * target.height) - padPx);
            int xMax = Mathf.Min(target.width, Mathf.CeilToInt(dirtyMax.x * target.width) + padPx);
            int yMax = Mathf.Min(target.height, Mathf.CeilToInt(dirtyMax.y * target.height) + padPx);

            int w = Mathf.Max(1, xMax - xMin);
            int h = Mathf.Max(1, yMax - yMin);

            float rx = (float)xMin / target.width;
            float ry = (float)yMin / target.height;
            float rw = (float)w / target.width;
            float rh = (float)h / target.height;

            compute.SetFloat("_RegionX", rx);
            compute.SetFloat("_RegionY", ry);
            compute.SetFloat("_RegionW", rw);
            compute.SetFloat("_RegionH", rh);

            compute.SetInts("_DirtyMin", new int[] { xMin, yMin });
            compute.SetInts("_DirtySize", new int[] { w, h });

            int gx = (w + 7) / 8;
            int gy = (h + 7) / 8;
            compute.Dispatch(kPaintDirty, gx, gy, 1);

            //ClearDirty();
            return true;
        }
    }

    private struct StampUpload
    {
        public Vector2 uv;
        public float size;
        public float rotationRad;
        public Color color;
    }

    private void EnsureSourceRT()
    {
        if (sourceRT != null &&
            sourceRT.width == target.width &&
            sourceRT.height == target.height &&
            sourceRT.format == target.format)
            return;

        if (sourceRT != null) sourceRT.Release();

        sourceRT = new RenderTexture(target.width, target.height, 0, target.format)
        {
            enableRandomWrite = false,
            useMipMap = false,
            autoGenerateMips = false
        };
        sourceRT.Create();
    }

    private void DoFastSingleDirtyBlit()
    {
        int padPx = Mathf.RoundToInt(size * target.width * 2f);
        int xMin = Mathf.Max(0, Mathf.FloorToInt(dirtyMin.x * target.width) - padPx);
        int yMin = Mathf.Max(0, Mathf.FloorToInt(dirtyMin.y * target.height) - padPx);
        int xMax = Mathf.Min(target.width, Mathf.CeilToInt(dirtyMax.x * target.width) + padPx);
        int yMax = Mathf.Min(target.height, Mathf.CeilToInt(dirtyMax.y * target.height) + padPx);

        int w = Mathf.Max(1, xMax - xMin);
        int h = Mathf.Max(1, yMax - yMin);

        float regionX = (float)xMin / target.width;
        float regionY = (float)yMin / target.height;
        float regionW = (float)w / target.width;
        float regionH = (float)h / target.height;

        RenderTexture temp = RenderTexture.GetTemporary(w, h, 0, target.format);
        Graphics.CopyTexture(target, 0, 0, xMin, yMin, w, h, temp, 0, 0, 0, 0);

        mat.SetTexture("_MainTex", target);
        mat.SetFloat("_RegionX", regionX);
        mat.SetFloat("_RegionY", regionY);
        mat.SetFloat("_RegionW", regionW);
        mat.SetFloat("_RegionH", regionH);
        mat.SetInt("_RegionSample", 0);

        Graphics.Blit(target, temp, mat);
        Graphics.CopyTexture(temp, 0, 0, 0, 0, w, h, target, 0, 0, xMin, yMin);

        RenderTexture.ReleaseTemporary(temp);

        mat.SetFloat("_RegionX", 0f);
        mat.SetFloat("_RegionY", 0f);
        mat.SetFloat("_RegionW", 1f);
        mat.SetFloat("_RegionH", 1f);
        mat.SetInt("_RegionSample", 0);

        //ClearDirty();
    }

    private void DoSafePingPongDirtyBlit()
    {
        int padPx = Mathf.RoundToInt(size * target.width * 2f);
        int xMin = Mathf.Max(0, Mathf.FloorToInt(dirtyMin.x * target.width) - padPx);
        int yMin = Mathf.Max(0, Mathf.FloorToInt(dirtyMin.y * target.height) - padPx);
        int xMax = Mathf.Min(target.width, Mathf.CeilToInt(dirtyMax.x * target.width) + padPx);
        int yMax = Mathf.Min(target.height, Mathf.CeilToInt(dirtyMax.y * target.height) + padPx);

        int w = Mathf.Max(1, xMax - xMin);
        int h = Mathf.Max(1, yMax - yMin);

        float regionX = (float)xMin / target.width;
        float regionY = (float)yMin / target.height;
        float regionW = (float)w / target.width;
        float regionH = (float)h / target.height;

        RenderTexture tempA = RenderTexture.GetTemporary(w, h, 0, target.format);
        RenderTexture tempB = RenderTexture.GetTemporary(w, h, 0, target.format);

        Graphics.CopyTexture(target, 0, 0, xMin, yMin, w, h, tempA, 0, 0, 0, 0);

        mat.SetTexture("_MainTex", tempA);
        mat.SetFloat("_RegionX", regionX);
        mat.SetFloat("_RegionY", regionY);
        mat.SetFloat("_RegionW", regionW);
        mat.SetFloat("_RegionH", regionH);
        mat.SetInt("_RegionSample", 1);

        Graphics.Blit(tempA, tempB, mat);
        Graphics.CopyTexture(tempB, 0, 0, 0, 0, w, h, target, 0, 0, xMin, yMin);

        RenderTexture.ReleaseTemporary(tempA);
        RenderTexture.ReleaseTemporary(tempB);

        mat.SetFloat("_RegionX", 0f);
        mat.SetFloat("_RegionY", 0f);
        mat.SetFloat("_RegionW", 1f);
        mat.SetFloat("_RegionH", 1f);
        mat.SetInt("_RegionSample", 0);

        //ClearDirty();
    }

    private void ClearDirty()
    {
        hasDirtyRegion = false;
        dirtyMin = new Vector2(1f, 1f);
        dirtyMax = new Vector2(0f, 0f);
    }
}
