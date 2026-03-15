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
        VelocityLineWidth,
        Smudge, // NEW
        WetBleed,
        CloudConnect
    }

    // ---------- Shared fields ----------
    private RenderTexture target;
    //private Material mat;
    private Material paintMat;
    private Material smudgeMat;
    private Material bleedMat;

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
    private Material ActiveMat =>
     (mode == PaintMode.Smudge && smudgeMat != null) ? smudgeMat :
     (mode == PaintMode.WetBleed && bleedMat != null) ? bleedMat :
     paintMat;

    private const int MaxBatch = 128;

    [Header("Smudge")]
    [Range(0f, 2f)] public float smudgeStrength = 0.9f;   // how strongly to blend pulled color
    [Range(0f, 2f)] public float smudgePull = 0.35f;      // how far “behind” to sample, in brush radii
    [Range(0.5f, 8f)] public float smudgeSoftness = 2.5f;

    [Header("Wet Bleed")]
    [Range(0f, 2f)] public float bleedStrength = 0.8f; // how strong the bleed is
    [Range(0f, 1f)] public float bleedPull = 0.25f;    // how far inward to sample (in brush radii)
    [Range(0f, 1f)] public float bleedEdgeStart = 0.55f; // start bleeding near edge (0=center, 1=edge)
    [Range(0.5f, 8f)] public float bleedSoftness = 2.5f; // edge softness curve

    [Header("Cloud Connect (Procedural Brush)")]
    [Range(3, 12)] public int cloudPointCount = 6;           // "5 or 6 points"
    [Range(1, 24)] public int cloudConnections = 8;          // how many lines from prev cloud to current
    [Range(0f, 2f)] public float cloudRadiusInBrushRadii = 0.35f; // cloud radius relative to brush size
    [Range(0f, 1f)] public float internalLineChance = 0.35f; // chance to draw internal lines
    [Range(0.05f, 2f)] public float cloudStepInBrushRadii = 0.55f; // spacing between clouds
    [Header("Cloud Connect")]
    [Range(0.01f, 1f)] public float cloudLineThickness = 0.15f;

    public bool connectNearest = true;                       // if false, connects random pairs

    // runtime state
    private readonly List<Vector2> prevCloud = new List<Vector2>(12);
    private readonly List<Vector2> curCloud = new List<Vector2>(12);
    private float cloudTravelAccum = 0f;
    private bool hasPrevCloud = false;
    private uint cloudRngState = 0x12345678u; // deterministic RNG per stroke

    private struct BrushStamp
    {
        public Vector2 uv;
        public float size;
        public float rotationRad;
        public Vector2 dir;     // NEW (for smudge)
        public float strength;  // NEW (for smudge)
        public Color color;
    }

    private readonly List<BrushStamp> stampQueue = new List<BrushStamp>(MaxBatch);

    private readonly Vector4[] stampDataCache = new Vector4[MaxBatch];
    private readonly Vector4[] colorDataCache = new Vector4[MaxBatch];
    private readonly Vector4[] stampData2Cache = new Vector4[MaxBatch]; // dirX, dirY, strength, unused


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

    public void Init(RenderTexture targetTexture,
                 Material paintMaterial,
                 Material smudgeMaterial,
                 Material bleedMaterial,
                 Texture2D brushTex,
                 Color brushColor,
                 float brushSize, 
                 float stampInterval,
                 float distThreshold, 
                 float minPressure, 
                 float maxPressure, 
                 float maxVel,
                 ComputeShader optionalCompute = null)
    {
        target = targetTexture;
        paintMat = paintMaterial;
        smudgeMat = smudgeMaterial;
        bleedMat = bleedMaterial;
        brush = brushTex;
        color = brushColor;

        size = brushSize;
        interval = stampInterval;
        distanceThreshold = distThreshold;

        minP = minPressure;
        maxP = maxPressure;
        maxSpeed = maxVel;

        // defaults (for BOTH)
        paintMat.SetInt("_StampCount", 0);
        paintMat.SetFloat("_RegionX", 0f);
        paintMat.SetFloat("_RegionY", 0f);
        paintMat.SetFloat("_RegionW", 1f);
        paintMat.SetFloat("_RegionH", 1f);
        paintMat.SetInt("_RegionSample", 0);

        if (smudgeMat != null)
        {
            smudgeMat.SetInt("_StampCount", 0);
            smudgeMat.SetFloat("_RegionX", 0f);
            smudgeMat.SetFloat("_RegionY", 0f);
            smudgeMat.SetFloat("_RegionW", 1f);
            smudgeMat.SetFloat("_RegionH", 1f);
            smudgeMat.SetInt("_RegionSample", 0);
            smudgeMat.SetFloat("_SmudgeSoftness", smudgeSoftness);
        }

        if (bleedMat != null)
        {
            bleedMat.SetInt("_StampCount", 0);
            bleedMat.SetFloat("_RegionX", 0f);
            bleedMat.SetFloat("_RegionY", 0f);
            bleedMat.SetFloat("_RegionW", 1f);
            bleedMat.SetFloat("_RegionH", 1f);
            bleedMat.SetInt("_RegionSample", 0);
        }

        // keep your compute setup as-is (we’ll just NOT use compute for smudge in this simple version)
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
            //targetPressure = Mathf.Clamp(externalPressure, minP, maxP);
            targetPressure = Mathf.Clamp(externalPressure, 0.01f, 10f);
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
            case PaintMode.Smudge:
                // Smudge needs direction, so we need lastUV
                if (lastUV.HasValue)
                {
                    Vector2 dir = uv - lastUV.Value;

                    // stamp along line like InterpolatedLine to avoid gaps
                    TryRenderSmudgeLine(lastUV.Value, uv, dir, currentPressure);
                }
                else
                {
                    // first touch: no real direction yet
                    QueueSmudgeStamp(uv, size, Vector2.right, currentPressure);
                }

                lastUV = uv;
                break;
            case PaintMode.WetBleed:
                // Bleed doesn't need direction, so treat it like your normal paint stamping.
                // Use interpolated line so you don't get gaps.
                if (lastUV.HasValue)
                    TryRenderLine(lastUV.Value, uv, 1f, 1f);
                else
                    QueueStamp(uv, size);

                lastUV = uv;
                break;
            case PaintMode.CloudConnect:
                {
                    // Determine pressure (use your external pressure if that's your active workflow)
                    /*float p = (mode == PaintMode.VelocityLineWidth && useExternalPressureForVelocity)
                        ? Mathf.Clamp(externalPressure, minP, maxP)
                        : currentPressure;*/

                    float p = currentPressure;

                    // First frame of stroke
                    if (!lastUV.HasValue)
                    {
                        ResetCloudStroke(uv);
                        BuildCloud(curCloud, uv, size, cloudRadiusInBrushRadii);

                        // Drop a tiny seed so it starts visible immediately
                        for (int i = 0; i < curCloud.Count; i++)
                            QueueStamp(curCloud[i], size * 0.30f);

                        prevCloud.Clear();
                        prevCloud.AddRange(curCloud);
                        hasPrevCloud = true;

                        lastUV = uv;
                        break;
                    }

                    // accumulate distance so we don't generate clouds *every* frame
                    float dist = Vector2.Distance(lastUV.Value, uv);
                    cloudTravelAccum += dist;

                    float step = Mathf.Max(0.00001f, size * cloudStepInBrushRadii);

                    if (cloudTravelAccum >= step)
                    {
                        cloudTravelAccum = 0f;

                        BuildCloud(curCloud, uv, size, cloudRadiusInBrushRadii);

                        // Connect previous cloud -> current cloud
                        if (hasPrevCloud)
                            DrawCloudConnections(prevCloud, curCloud, p);

                        // Also add a couple tiny stamps to ensure you see nodes
                        for (int i = 0; i < curCloud.Count; i++)
                            QueueStamp(curCloud[i], size * 0.25f);

                        // shift current -> previous
                        prevCloud.Clear();
                        prevCloud.AddRange(curCloud);
                        hasPrevCloud = true;
                    }

                    lastUV = uv;
                    break;
                }
        }

        if (stampQueue.Count >= MaxBatch)
            Flush();
    }

    public void FinishStroke()
    {
        lastUV = null;
        stampTimer = 0f;

        prevCloud.Clear();
        curCloud.Clear();
        hasPrevCloud = false;
        cloudTravelAccum = 0f;

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

        //float avgPressure = Mathf.Clamp01((pressureStart + pressureEnd) * 0.5f);
        float avgPressure = Mathf.Clamp((pressureStart + pressureEnd) * 0.5f, 0.01f, 10f);

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

        // Smudge uses a different material + we keep it fragment-only for the simple modular version
        bool isSmudge = (mode == PaintMode.Smudge);
        bool isBleed = (mode == PaintMode.WetBleed);
        Material matToUse = ActiveMat; // <- property you added: smudgeMat when Smudge, else paintMat

        if (matToUse == null || target == null)
        {
            stampQueue.Clear();
            ClearDirty();
            return;
        }

        // Keep dirty region for the entire flush (multiple batches)
        bool wantDirty = (!isSmudge && !isBleed && useDirtyRegionBlit && hasDirtyRegion);


        int safety = 0;

        // Render everything in MaxBatch chunks (don’t drop stamps)
        while (stampQueue.Count > 0)
        {
            int shaderBatch = isBleed ? 32 : MaxBatch; // WetBleed shader only handles 32
            int count = Mathf.Min(stampQueue.Count, shaderBatch);

            for (int i = 0; i < count; i++)
            {
                var s = stampQueue[i];

                // Always fill base stamp data (uv, size, rotation)
                stampDataCache[i] = new Vector4(s.uv.x, s.uv.y, s.size, s.rotationRad);

                // Paint uses colors; Smudge can ignore this (harmless to fill anyway)
                colorDataCache[i] = (Vector4)s.color;

                // Smudge extras (dir + strength); Paint can ignore (harmless to fill anyway)
                stampData2Cache[i] = new Vector4(s.dir.x, s.dir.y, s.strength, 0f);
            }

            // Compute is allowed for your normal paint modes, but keep Smudge fragment-only for simplicity.
            bool didCompute = false;
            bool allowCompute = (!isSmudge && !isBleed);

            if (compute != null && useComputeIfAvailable && allowCompute && stampBuffer != null && target != null)
            {
                didCompute = ComputeFlush(count);
            }

            if (!didCompute)
            {
                // ---- set common uniforms ----
                matToUse.SetInt("_StampCount", count);
                matToUse.SetVectorArray("_StampData", stampDataCache);

                matToUse.SetTexture("_MainTex", target);
                matToUse.SetTexture("_BrushTex", brush);
                matToUse.SetFloat("_Aspect", (float)target.width / target.height);

                // ---- set mode-specific uniforms ----
                if (isSmudge)
                {
                    matToUse.SetVectorArray("_StampData2", stampData2Cache);
                    matToUse.SetFloat("_SmudgeStrength", smudgeStrength);
                    matToUse.SetFloat("_SmudgePull", smudgePull);
                    matToUse.SetFloat("_SmudgeSoftness", smudgeSoftness);
                }
                else if (mode == PaintMode.WetBleed)
                {
                    matToUse.SetVectorArray("_StampColors", colorDataCache);
                    matToUse.SetFloat("_MaxStamps", 32f); // IMPORTANT: match shader loop
                    //matToUse.SetVectorArray("_StampColors", colorDataCache); // still uses color
                    //matToUse.SetFloat("_BleedStrength", bleedStrength);
                    //matToUse.SetFloat("_BleedPull", bleedPull);
                    //matToUse.SetFloat("_BleedEdgeStart", bleedEdgeStart);
                    //matToUse.SetFloat("_BleedSoftness", bleedSoftness);
                }
                else
                {
                    matToUse.SetVectorArray("_StampColors", colorDataCache);
                }

                // ---- blit full or dirty region ----
                if (!wantDirty)
                {
                    // Full-frame ping-pong
                    RenderTexture temp = RenderTexture.GetTemporary(target.descriptor);
                    Graphics.Blit(target, temp, matToUse);
                    Graphics.Blit(temp, target);
                    RenderTexture.ReleaseTemporary(temp);
                }
                else
                {
                    // IMPORTANT:
                    // These dirty methods must use ActiveMat internally (or accept a Material param).
                    // If you haven’t updated them yet, change their internal "mat" usage to ActiveMat.
                    if (forceSafeBlit) DoSafePingPongDirtyBlit();
                    else DoFastSingleDirtyBlit();
                }
            }

            // Remove the batch we just rendered
            stampQueue.RemoveRange(0, count);

            // Just in case (prevents infinite loops if something goes weird)
            if (++safety > 5000)
            {
                stampQueue.Clear();
                break;
            }
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

        ActiveMat.SetTexture("_MainTex", target);
        ActiveMat.SetFloat("_RegionX", regionX);
        ActiveMat.SetFloat("_RegionY", regionY);
        ActiveMat.SetFloat("_RegionW", regionW);
        ActiveMat.SetFloat("_RegionH", regionH);
        ActiveMat.SetInt("_RegionSample", 0);

        Graphics.Blit(target, temp, ActiveMat);
        Graphics.CopyTexture(temp, 0, 0, 0, 0, w, h, target, 0, 0, xMin, yMin);

        RenderTexture.ReleaseTemporary(temp);

        ActiveMat.SetFloat("_RegionX", 0f);
        ActiveMat.SetFloat("_RegionY", 0f);
        ActiveMat.SetFloat("_RegionW", 1f);
        ActiveMat.SetFloat("_RegionH", 1f);
        ActiveMat.SetInt("_RegionSample", 0);

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

        ActiveMat.SetTexture("_MainTex", tempA);
        ActiveMat.SetFloat("_RegionX", regionX);
        ActiveMat.SetFloat("_RegionY", regionY);
        ActiveMat.SetFloat("_RegionW", regionW);
        ActiveMat.SetFloat("_RegionH", regionH);
        ActiveMat.SetInt("_RegionSample", 1);

        Graphics.Blit(tempA, tempB, ActiveMat);
        Graphics.CopyTexture(tempB, 0, 0, 0, 0, w, h, target, 0, 0, xMin, yMin);

        RenderTexture.ReleaseTemporary(tempA);
        RenderTexture.ReleaseTemporary(tempB);

        ActiveMat.SetFloat("_RegionX", 0f);
        ActiveMat.SetFloat("_RegionY", 0f);
        ActiveMat.SetFloat("_RegionW", 1f);
        ActiveMat.SetFloat("_RegionH", 1f);
        ActiveMat.SetInt("_RegionSample", 0);

        //ClearDirty();
    }

    private void ClearDirty()
    {
        hasDirtyRegion = false;
        dirtyMin = new Vector2(1f, 1f);
        dirtyMax = new Vector2(0f, 0f);
    }

    private void QueueSmudgeStamp(Vector2 uv, float scaledSize, Vector2 dir, float strength)
    {
        // normalize dir safely
        float mag = dir.magnitude;
        if (mag < 1e-6f) dir = Vector2.right;
        else dir /= mag;

        stampQueue.Add(new BrushStamp
        {
            uv = uv,
            size = scaledSize,
            rotationRad = 0f,
            dir = dir,
            strength = strength,
            color = Color.clear
        });

        // dirty region same as normal
        if (useDirtyRegionBlit)
        {
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

    private void TryRenderSmudgeLine(Vector2 startUV, Vector2 endUV, Vector2 dir, float pressure)
    {
        float distUV = Vector2.Distance(startUV, endUV);
        if (distUV <= 1e-7f)
        {
            QueueSmudgeStamp(endUV, size * pressure, dir, pressure);
            return;
        }

        float distPixels = distUV * target.width;

        //float avgPressure = Mathf.Clamp01(pressure);
        float avgPressure = Mathf.Clamp(pressure, 0.01f, 10f);
        float stampPx = Mathf.Max(0.25f, (size * avgPressure) * target.width);

        float baseBrushPx = Mathf.Max(0.0001f, size * target.width);
        float maxStep = Mathf.Max(0.5f, baseBrushPx * strokeSmoothness);
        float overlapStep = stampPx * overlapInterval;

        float pixelStep = Mathf.Clamp(overlapStep, 0.25f, maxStep); // smaller min step than paint


        int steps = Mathf.CeilToInt(distPixels / Mathf.Max(0.0001f, pixelStep));
        steps = Mathf.Clamp(steps, 1, 8192);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 interpUV = Vector2.Lerp(startUV, endUV, t);
            QueueSmudgeStamp(interpUV, size * avgPressure, dir, avgPressure);
        }
    }

    //HELPERS FOR CLOUD BRUSH
    // Small, fast deterministic RNG (no allocations)
    private uint NextU()
    {
        cloudRngState ^= cloudRngState << 13;
        cloudRngState ^= cloudRngState >> 17;
        cloudRngState ^= cloudRngState << 5;
        return cloudRngState;
    }
    private float Next01() => (NextU() & 0x00FFFFFFu) / 16777215f;

    private Vector2 RandomInUnitCircle()
    {
        // polar method
        float a = Next01() * Mathf.PI * 2f;
        float r = Mathf.Sqrt(Next01());
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }

    private void ResetCloudStroke(Vector2 startUV)
    {
        prevCloud.Clear();
        curCloud.Clear();
        hasPrevCloud = false;
        cloudTravelAccum = 0f;

        // seed from UV so each stroke is stable but different
        unchecked
        {
            uint sx = (uint)(Mathf.Abs(startUV.x) * 100000f);
            uint sy = (uint)(Mathf.Abs(startUV.y) * 100000f);
            cloudRngState = 0x9E3779B9u ^ (sx * 374761393u) ^ (sy * 668265263u) ^ (uint)Time.frameCount;
            if (cloudRngState == 0) cloudRngState = 0x12345678u;
        }
    }

    private void BuildCloud(List<Vector2> dst, Vector2 centerUV, float brushSizeWidthNorm, float radiusInRadii)
    {
        dst.Clear();

        // brushSizeWidthNorm is your "size" which is width-normalized UV units
        // cloud radius in UV = brushSize * radiusInRadii
        float rad = brushSizeWidthNorm * Mathf.Max(0.0001f, radiusInRadii);

        for (int i = 0; i < cloudPointCount; i++)
        {
            Vector2 off = RandomInUnitCircle() * rad;
            Vector2 p = centerUV + off;

            // clamp to canvas UV (avoid drawing outside)
            p.x = Mathf.Clamp01(p.x);
            p.y = Mathf.Clamp01(p.y);

            dst.Add(p);
        }
    }

    private int FindNearestIndex(List<Vector2> pts, Vector2 target)
    {
        int best = 0;
        float bestD = float.PositiveInfinity;
        for (int i = 0; i < pts.Count; i++)
        {
            float d = (pts[i] - target).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    private void DrawCloudConnections(List<Vector2> fromPts, List<Vector2> toPts, float pressure)
    {
        if (fromPts.Count == 0 || toPts.Count == 0) return;

        // Connection thickness scales with pressure but clamps so it stays visible
        float p = Mathf.Clamp(pressure, 0.15f, 1.0f);

        // Draw several lines between the two clouds
        int links = Mathf.Clamp(cloudConnections, 1, 64);

        for (int k = 0; k < links; k++)
        {
            // pick a "to" point
            int b = Mathf.FloorToInt(Next01() * toPts.Count);
            b = Mathf.Clamp(b, 0, toPts.Count - 1);

            int a;
            if (connectNearest)
            {
                a = FindNearestIndex(fromPts, toPts[b]);
            }
            else
            {
                a = Mathf.FloorToInt(Next01() * fromPts.Count);
                a = Mathf.Clamp(a, 0, fromPts.Count - 1);
            }

            // Render a line between those points using your existing stamping line renderer.
            // This automatically queues stamps along the segment, so it *won't* be just 2 dots.
            //TryRenderLine(fromPts[a], toPts[b], p, p);
            TryRenderWireLine(fromPts[a], toPts[b]);
        }

        // Optional: some internal "web" lines inside current cloud
        if (Next01() < internalLineChance && toPts.Count >= 2)
        {
            int internalLinks = Mathf.Max(1, Mathf.Min(3, toPts.Count / 2));
            for (int j = 0; j < internalLinks; j++)
            {
                int i0 = Mathf.FloorToInt(Next01() * toPts.Count);
                int i1 = Mathf.FloorToInt(Next01() * toPts.Count);
                if (i0 == i1) i1 = (i1 + 1) % toPts.Count;

                TryRenderWireLine(toPts[i0], toPts[i1]);
            }
        }
    }

    private void TryRenderWireLine(Vector2 startUV, Vector2 endUV)
    {
        float distUV = Vector2.Distance(startUV, endUV);
        if (distUV <= 0.0000001f)
        {
            QueueStamp(endUV, size * cloudLineThickness);
            return;
        }

        float distPixels = distUV * target.width;

        // IMPORTANT: use THINNER effective diameter
        float wireSize = size * cloudLineThickness;
        float stampPx = Mathf.Max(0.25f, wireSize * target.width);

        // Keep original smoothness logic
        float baseBrushPx = Mathf.Max(0.0001f, wireSize * target.width);
        float maxStep = Mathf.Max(0.5f, baseBrushPx * strokeSmoothness);

        float overlapStep = stampPx * overlapInterval;
        float pixelStep = Mathf.Clamp(overlapStep, 0.5f, maxStep);

        int steps = Mathf.CeilToInt(distPixels / Mathf.Max(0.0001f, pixelStep));
        steps = Mathf.Clamp(steps, 1, 2048);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 interpUV = Vector2.Lerp(startUV, endUV, t);
            QueueStamp(interpUV, wireSize);
        }
    }

}
