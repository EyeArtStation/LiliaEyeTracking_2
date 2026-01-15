using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintObject.Data;
using XDPaint.Core.PaintObject.RaycastProcessor.Base;
using XDPaint.Core.PaintObject.LineProcessor;
using XDPaint.Core.PaintObject.LineProcessor.Base;
using XDPaint.Core.PaintObject.LineProcessor.Data;
using XDPaint.Tools.Image.Base;
using XDPaint.Tools.Raycast.Data;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using UnityEngine.UI;

namespace XDPaint.Core.PaintObject.Base
{
    [Serializable]
    public abstract class BasePaintObject : BasePaintObjectRenderer
    {
        #region Events

        /// <summary>
        /// Mouse hover event
        /// </summary>
        public event Action<PointerData> OnPointerHover;

        /// <summary>
        /// Mouse down event
        /// </summary>
        public event Action<PointerData> OnPointerDown;

        /// <summary>
        /// Mouse press event
        /// </summary>
        public event Action<PointerData> OnPointerPress;

        /// <summary>
        /// Mouse up event
        /// </summary>
        public event Action<PointerUpData> OnPointerUp;

        /// <summary>
        /// Draw point event, can be used by the developer to obtain data about painting
        /// </summary>
        public event Action<DrawPointData> OnDrawPoint;

        /// <summary>
        /// Draw line event, can be used by the developer to obtain data about painting
        /// </summary>
        public event Action<DrawLineData> OnDrawLine;

        #endregion

        #region Properties and variables

        public bool velocityLineWidth;
        public float dripTimer = 0f;
        public float dripInterval = 0.5f;
        public Image dripImage;

        private bool isInitialized;

        // For Velocity Brush
        // Convert speed to pressure (invert it)
        // Example: faster = smaller pressure
        public float minPressure = 0.2f;
        public float maxPressure = 1.0f;
        public float maxSpeed = 2000f;  // adjust as needed

        private float smoothedPressure = 1f;

        [SerializeField] private AnimationCurve pressureCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Vector2 lastSampledPosition = Vector2.zero;
        private float currentPressure = 1f;
        private const float minSampleSpacing = 2f;       // pixels
        private const int smoothingWindow = 4;           // frames
        private const float smoothingSpeed = 10f;

        public bool lineActive = true;
        public bool isUsingDrip;
        public bool useDripImageCycle;
        public bool randomBrushAngle;

        public Vector2 randomSizeRange = new Vector2(0.05f,0.05f);

        public Vector2 randomOpacityRange = new Vector2(1f,1f);

        public Vector2 spacingThresholdRange;
        //private float spacingThreshold;

        private Vector2? lastUVHit = null;

        private readonly Dictionary<int, Vector2> lastUVsByFinger = new();
        //[SerializeField] private float uvDistanceThreshold = 0.001f; // adjust to set spacing in UV space

        public Texture[] dripImageArray;

        public bool InBounds
        {
            get
            {
                foreach (var frameDataBuffer in frameContainer.Data)
                {
                    if (frameDataBuffer.Count == 0)
                        continue;

                    if (frameDataBuffer.GetFrameData(0).State.InBounds)
                        return true;
                }

                return false;
            }
        }

        public bool IsPainting
        {
            get
            {
                foreach (var frameDataBuffer in frameContainer.Data)
                {
                    if (frameDataBuffer.Count == 0)
                        continue;

                    if (frameDataBuffer.GetFrameData(0).State.IsPainting)
                        return true;
                }

                return false;
            }
        }

        public bool IsPainted { get; private set; }
        public bool ProcessInput = true;
        protected Transform ObjectTransform { get; private set; }
        protected IPaintManager PaintManager;

        private int HistoryLength => CanSmoothLines ? 4 : 2;

        private Vector3 RenderOffset
        {
            get
            {
                if (PaintData.Brush == null)
                    return Vector3.zero;

                var renderOffset = PaintData.Brush.RenderOffset;
                if (renderOffset.x > 0)
                {
                    renderOffset.x = Paint.SourceTexture.texelSize.x / 2f;
                }

                if (renderOffset.y > 0)
                {
                    renderOffset.y = Paint.SourceTexture.texelSize.y / 2f;
                }

                return renderOffset;
            }
        }

        private FrameDataContainer frameContainer;
        private PaintStateData[] statesData;
        private ILineProcessor lineProcessor;
        private IRaycastProcessor raycastProcessor;
        private BaseWorldData worldData;
        private bool clearTexture = true;
        private bool writeClear;
        public bool makeDrawActive;

        #endregion

        #region Abstract methods

        public abstract bool CanSmoothLines { get; }
        public abstract Vector2 ConvertUVToTexturePosition(Vector2 uvPosition);
        public abstract Vector2 ConvertTextureToUVPosition(Vector2 texturePosition);
        protected abstract void Init();
        protected abstract bool IsInBounds(Ray ray);

        #endregion

        public void Init(IPaintManager paintManager, IPaintData paintData, Transform objectTransform, Paint paint)
        {
            PaintManager = paintManager;
            PaintData = paintData;
            ObjectTransform = objectTransform;
            Paint = paint;
            if (paintData.PaintSpace == PaintSpace.World)
            {
                worldData = new BaseWorldData();
            }

            InitRenderer(PaintManager, Paint);
            InitPaintStateData();
            InitStatesController();
            Init();
            isInitialized = true;
            Debug.Log("Init has been called");

        }

        public override void DoDispose()
        {
            if (PaintData.StatesController != null)
            {
                PaintData.StatesController.OnRenderTextureAction -= OnExtraDraw;
                PaintData.StatesController.OnClearTextureAction -= OnClearTexture;
                PaintData.StatesController.OnResetState -= OnResetState;
            }

            frameContainer.DoDispose();
            statesData = null;
            worldData = null;
            base.DoDispose();
        }

        private void InitPaintStateData()
        {
            frameContainer = new FrameDataContainer(HistoryLength);
            statesData = new PaintStateData[InputController.Instance.MaxTouchesCount];
            for (var i = 0; i < statesData.Length; i++)
            {
                statesData[i] = new PaintStateData();
            }
        }

        private void InitStatesController()
        {
            if (PaintData.StatesController == null)
                return;

            PaintData.StatesController.OnRenderTextureAction += OnExtraDraw;
            PaintData.StatesController.OnClearTextureAction += OnClearTexture;
            PaintData.StatesController.OnResetState += OnResetState;
        }

        private void OnResetState()
        {
            clearTexture = true;
        }

        #region Input

        /*public void Update()
        {
            if (!lineActive)
            {
                Debug.Log("This Fixed Update is getting called");
                TryRenderPoint(0);
            }
        }*/

        public void OnMouseHover(InputData inputData, RaycastData raycastData)
        {
            if (!IsPainting)
            {
                FrameData frameData;
                if (raycastData != null)
                {
                    frameData = new FrameData(inputData, raycastData, PaintData.Brush.Size);
                    frameContainer.Data[inputData.FingerId].AddFrameData(frameData);
                }
                else
                {
                    frameData = frameContainer.Data[inputData.FingerId].GetFrameData(0);
                }

                UpdatePaintData(frameData, true);
                if (OnPointerHover != null)
                {
                    var data = new PointerData(frameData.InputData, frameData.RaycastData, ConvertUVToTexturePosition(raycastData.UVHit));
                    OnPointerHover(data);
                }
            }
        }

        public void OnMouseHoverFailed(InputData inputData)
        {
            frameContainer.Data[inputData.FingerId].DoDispose();
        }

        public void OnMouseDown(InputData inputData, RaycastData raycastData)
        {
            //OnMouse(inputData, raycastData, true);
        }

        public void OnMouseButton(InputData inputData, RaycastData raycastData)
        {
            OnMouse(inputData, raycastData, false);
            Debug.Log("OnMouseButton called");
        }


        public void OnMouse(InputData inputData, RaycastData raycastData, bool isDown)
        {
            Debug.Log("BasePaintObject.OnMouse called");
            if (isDown)
            {
                frameContainer.Data[inputData.FingerId].DoDispose();
                makeDrawActive = true;
                Debug.Log("BasePaintObject.OnMouse isDown == TRUE");
            }

            //EFFECT: Variable Line Width
            /*if (velocityLineWidth)
            {
                var currentPos = inputData.Position;
                var frameBuffer = frameContainer.Data[inputData.FingerId];
                float pressure = 1f;

                // Only calculate velocity if we have a previous point
                if (frameBuffer.Count > 0)
                {
                    var previousFrame = frameBuffer.GetFrameData(0);
                    var previousPos = previousFrame.InputData.Position;
                    float delta = Vector2.Distance(currentPos, previousPos);
                    float speed = delta / Time.deltaTime;

                    pressure = Mathf.Clamp01(1f - speed / maxSpeed);
                    pressure = Mathf.Lerp(minPressure, maxPressure, pressure);
                }

                // Apply the velocity-based pressure
                inputData.Pressure = pressure;
            }*/


            //WORKS OKAY
            if (velocityLineWidth)
            {
                var currentPos = inputData.Position;
                var frameBuffer = frameContainer.Data[inputData.FingerId];
                float pressure = 1f;

                int frameCount = Mathf.Min(frameBuffer.Count, 4);
                if (frameCount >= 2)
                {
                    Vector2 sumDelta = Vector2.zero;
                    float totalTime = 0f;

                    for (int i = 1; i < frameCount; i++)
                    {
                        Vector2 p1 = frameBuffer.GetFrameData(frameBuffer.Count - i).InputData.Position;
                        Vector2 p0 = frameBuffer.GetFrameData(frameBuffer.Count - i - 1).InputData.Position;
                        sumDelta += (p1 - p0);
                        totalTime += Time.deltaTime;
                    }

                    float averageSpeed = sumDelta.magnitude / totalTime;
                    pressure = Mathf.Clamp01(1f - averageSpeed / maxSpeed);
                    pressure = Mathf.Lerp(minPressure, maxPressure, pressure);
                }

                // Apply the velocity-based pressure
                inputData.Pressure = pressure;
            }

            /*if (velocityLineWidth)
            {
                var currentPos = inputData.Position;
                var frameBuffer = frameContainer.Data[inputData.FingerId];

                // Only proceed if cursor has moved far enough
                if (Vector2.Distance(currentPos, lastSampledPosition) >= minSampleSpacing)
                {
                    lastSampledPosition = currentPos;

                    // Smoothed velocity calculation
                    int frameCount = Mathf.Min(frameBuffer.Count, smoothingWindow);
                    if (frameCount >= 2)
                    {
                        Vector2 sumDelta = Vector2.zero;
                        float totalTime = 0f;

                        for (int i = 1; i < frameCount; i++)
                        {
                            Vector2 p1 = frameBuffer.GetFrameData(frameBuffer.Count - i).InputData.Position;
                            Vector2 p0 = frameBuffer.GetFrameData(frameBuffer.Count - i - 1).InputData.Position;
                            sumDelta += (p1 - p0);
                            totalTime += Time.deltaTime;
                        }

                        float averageSpeed = sumDelta.magnitude / totalTime;

                        // Map speed to pressure
                        float targetPressure = Mathf.Clamp01(1f - averageSpeed / maxSpeed);
                        targetPressure = Mathf.Lerp(minPressure, maxPressure, targetPressure);

                        // Smooth pressure transition
                        currentPressure = Mathf.Lerp(currentPressure, targetPressure, smoothingSpeed * Time.deltaTime);
                    }

                    // Apply final smoothed pressure
                    inputData.Pressure = currentPressure;
                }
                else
                {
                    // Avoid drawing a stroke when spacing is too tight
                    inputData.Pressure = 0f;
                }
            }*/

            /*if (velocityLineWidth)
            {
                Debug.Log("Velocity draw is active");
                var currentPos = inputData.Position;
                var frameBuffer = frameContainer.Data[inputData.FingerId];
                //float targetPressure = 1f;
                float pressure = 1f;

                if (frameBuffer.Count > 0)
                {
                    var previousFrame = frameBuffer.GetFrameData(0);
                    var previousPos = previousFrame.InputData.Position;
                    float delta = Vector2.Distance(currentPos, previousPos);
                    float speed = delta / Time.deltaTime;

                    float rawPressure = Mathf.Clamp01(1f - speed / maxSpeed);
                    pressure = Mathf.Lerp(minPressure, maxPressure, pressure);
                    //float curvedPressure = Mathf.Pow(rawPressure, 2.5f);
                    //float curvedPressure = pressureCurve.Evaluate(rawPressure);
                    //targetPressure = Mathf.Lerp(minPressure, maxPressure, curvedPressure);
                }



                // Smooth the pressure change
                //float smoothingFactor = 0.01f; // Smaller = smoother, try 0.05–0.2
                //smoothedPressure = Mathf.Lerp(smoothedPressure, targetPressure, smoothingFactor);

                //inputData.Pressure = smoothedPressure;

                inputData.Pressure = pressure;
            }*/

            //THIS IS LINE MECHANIC

            var frameData = new FrameData(inputData, raycastData, PaintData.Brush.Size);
            frameContainer.Data[inputData.FingerId].AddFrameData(frameData);
            if (raycastData != null && raycastData.Triangle.Transform == ObjectTransform)
            {
                frameData.State.IsPainting = true;
                frameData.BrushSize = PaintData.Brush.Size;
                var paintState = statesData[frameData.InputData.FingerId];
                paintState.IsPainting = frameData.State.IsPainting;
                UpdatePaintData(frameData, false);
                if (frameData.RaycastData != null)
                {
                    frameData.State.IsPaintingPerformed = true;
                    paintState.IsPaintingPerformed = frameData.State.IsPaintingPerformed;
                    if (isDown)
                    {
                        if (OnPointerDown != null)
                        {
                            var data = new PointerData(frameData.InputData, frameData.RaycastData, ConvertUVToTexturePosition(raycastData.UVHit));
                            OnPointerDown.Invoke(data);
                        }
                    }
                    else
                    {
                        if (OnPointerPress != null)
                        {
                            var data = new PointerData(frameData.InputData, frameData.RaycastData, ConvertUVToTexturePosition(raycastData.UVHit));
                            OnPointerPress.Invoke(data);
                        }
                    }
                }
            }

            if (!lineActive)
            {
                //Debug.Log("This is also getting called");
                //TryRenderPoint(0);
            }
        }

        public void EyeTrackingOnMouse(InputData inputData, RaycastData raycastData, bool isDown)
        {
            if (isDown)
            {
                frameContainer.Data[inputData.FingerId].DoDispose();
            }

            var frameData = new FrameData(inputData, raycastData, PaintData.Brush.Size);
            frameContainer.Data[inputData.FingerId].AddFrameData(frameData);
            if (raycastData != null && raycastData.Triangle.Transform == ObjectTransform)
            {
                frameData.State.IsPainting = true;
                frameData.BrushSize = PaintData.Brush.Size;
                var paintState = statesData[frameData.InputData.FingerId];
                paintState.IsPainting = frameData.State.IsPainting;
                UpdatePaintData(frameData, false);
                if (frameData.RaycastData != null)
                {
                    frameData.State.IsPaintingPerformed = true;
                    paintState.IsPaintingPerformed = frameData.State.IsPaintingPerformed;
                    if (isDown)
                    {
                        if (OnPointerDown != null)
                        {
                            var data = new PointerData(frameData.InputData, frameData.RaycastData, ConvertUVToTexturePosition(raycastData.UVHit));
                            OnPointerDown.Invoke(data);
                        }
                    }
                    else
                    {
                        if (OnPointerPress != null)
                        {
                            var data = new PointerData(frameData.InputData, frameData.RaycastData, ConvertUVToTexturePosition(raycastData.UVHit));
                            OnPointerPress.Invoke(data);
                        }
                    }
                }
            }
            Debug.Log("OnEyetrackingMouseDown called");
        }

        public void OnMouseFailed(InputData inputData)
        {
            frameContainer.Data[inputData.FingerId].DoDispose();
        }

        public void OnMouseUp(InputData inputData)
        {
            FinishPainting(inputData.FingerId);
            if (OnPointerUp != null)
            {
                var data = new PointerUpData(inputData, IsInBounds(inputData.Ray));
                OnPointerUp.Invoke(data);
            }
            Debug.Log("BasePaintObject.OnMouseUp called");
        }

        public void OnEyeTrackingMouseUp(InputData inputData)
        {
            makeDrawActive = false;
            FinishPainting(inputData.FingerId);
            if (OnPointerUp != null)
            {
                var data = new PointerUpData(inputData, IsInBounds(inputData.Ray));
                OnPointerUp.Invoke(data);
            }

            OnMouseDown(inputData, null);
        }

        public Vector2? GetTexturePosition(InputData inputData, RaycastData raycastData)
        {
            if (ObjectTransform == null)
            {
                Debug.LogError("ObjectForPainting has been destroyed!");
                return null;
            }

            var frameData = frameContainer.Data[inputData.FingerId].GetFrameData(0);
            UpdatePaintData(frameData, true);
            if (frameData.State.InBounds && raycastData != null)
            {
                return ConvertUVToTexturePosition(frameData.RaycastData.UVHit);
            }

            return null;
        }

        #endregion

        #region Drawing from code

        public PaintStateContainer SavePaintState(int fingerId = 0)
        {
            if (fingerId < 0 || fingerId >= frameContainer.Data.Length)
                throw new ArgumentOutOfRangeException(nameof(fingerId));

            var paintDataStorage = new PaintStateContainer(HistoryLength);
            if (frameContainer.Data[fingerId].Count > 0)
            {
                var dataStorage = paintDataStorage;
                for (var i = 0; i < frameContainer.Data[fingerId].Count; i++)
                {
                    var frameData = frameContainer.Data[fingerId].GetFrameData(i);
                    dataStorage.FrameBuffer.AddFrameData(frameData);
                }
            }

            paintDataStorage.PaintState.CopyFrom(statesData[fingerId]);
            return paintDataStorage;
        }

        public void RestorePaintState(PaintStateContainer paintContainerStorage, int fingerId = 0)
        {
            if (fingerId < 0 || fingerId >= frameContainer.Data.Length)
                throw new ArgumentOutOfRangeException(nameof(fingerId));

            if (paintContainerStorage.Equals(default))
            {
                Debug.LogError("Saved states cannot be default!");
                return;
            }

            if (paintContainerStorage.FrameBuffer.Count > 0)
            {
                var frameData = frameContainer.Data[fingerId];
                frameData.DoDispose();
                for (var i = 0; i < paintContainerStorage.FrameBuffer.Count; i++)
                {
                    var data = paintContainerStorage.FrameBuffer.GetFrameData(i);
                    frameData.AddFrameData(data);
                }
            }

            statesData[fingerId].CopyFrom(paintContainerStorage.PaintState);
        }

        /// <summary>
        /// Draws a brush sample (point)
        /// </summary>
        /// <param name="texturePosition"></param>
        /// <param name="pressure"></param>
        /// <param name="fingerId"></param>
        /// <param name="onFinish"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void DrawPoint(Vector2 texturePosition, float pressure = 1f, int fingerId = 0, Action onFinish = null)
        {
            if (frameContainer.Data == null)
            {
                Debug.Log("frameContainer.Data doesn't exist");
            }
            else
            {
                Debug.Log("frameContainer.Data DOES exist");
            }

            if (fingerId < 0 || fingerId >= frameContainer.Data.Length)
                throw new ArgumentOutOfRangeException(nameof(fingerId));

            if (PaintData.PaintSpace != PaintSpace.UV)
            {
                Debug.LogWarning("Paint Space is not UV!");
                return;
            }

            var state = SavePaintState(fingerId);
            frameContainer.Data[fingerId].DoDispose();
            var frameData = new FrameData(
                new InputData(fingerId, pressure),
                new RaycastData(null)
                {
                    UVHit = ConvertTextureToUVPosition(texturePosition)
                },
                PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };

            statesData[fingerId].InBounds = true;
            statesData[fingerId].IsPainting = true;
            statesData[fingerId].IsPaintingPerformed = true;

            frameContainer.Data[fingerId].AddFrameData(frameData);
            IsPainted |= TryRenderPoint(fingerId);
            RenderToTextures();
            onFinish?.Invoke();
            frameContainer.Data[fingerId].DoDispose();
            RestorePaintState(state, fingerId);

            //onFinish?.Invoke();
            Debug.Log("onFinish callback executed");
        }

        /// <summary>
        /// Draws a brush sample (point)
        /// </summary>
        /// <param name="drawPointData"></param>
        /// <param name="onFinish"></param>
        public void DrawPoint(DrawPointData drawPointData, Action onFinish = null)
        {
            DrawPoint(drawPointData.InputData, drawPointData.RaycastData, onFinish);
        }

        /// <summary>
        /// Draws a brush sample (point)
        /// </summary>
        /// <param name="inputData"></param>
        /// <param name="raycastData"></param>
        /// <param name="onFinish"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void DrawPoint(InputData inputData, RaycastData raycastData, Action onFinish = null)
        {
            if (inputData.FingerId < 0 || inputData.FingerId >= frameContainer.Data.Length)
                throw new ArgumentOutOfRangeException(nameof(inputData.FingerId));

            var state = SavePaintState(inputData.FingerId);
            frameContainer.Data[inputData.FingerId].DoDispose();
            var frameData = new FrameData(inputData, raycastData, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };

            statesData[inputData.FingerId].InBounds = true;
            statesData[inputData.FingerId].IsPainting = true;
            statesData[inputData.FingerId].IsPaintingPerformed = true;

            frameContainer.Data[inputData.FingerId].AddFrameData(frameData);
            IsPainted |= TryRenderPoint(inputData.FingerId);
            RenderToTextures();
            onFinish?.Invoke();
            frameContainer.Data[inputData.FingerId].DoDispose();
            RestorePaintState(state, inputData.FingerId);

            Debug.Log("This drawpoint got called");
        }

        /// <summary>
        /// Draws a line with brush samples
        /// </summary>
        /// <param name="texturePositionStart"></param>
        /// <param name="texturePositionEnd"></param>
        /// <param name="pressureStart"></param>
        /// <param name="pressureEnd"></param>
        /// <param name="fingerId"></param>
        /// <param name="onFinish"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void DrawLine(Vector2 texturePositionStart, Vector2 texturePositionEnd, float pressureStart = 1f, float pressureEnd = 1f, int fingerId = 0, Action onFinish = null)
        {
            Debug.Log("DrawLine called");

            if (fingerId < 0 || fingerId >= frameContainer.Data.Length)
                throw new ArgumentOutOfRangeException(nameof(fingerId));

            if (PaintData.PaintSpace != PaintSpace.UV)
            {
                Debug.LogWarning("Paint Space is not UV!");
                return;
            }

            var state = SavePaintState(fingerId);
            frameContainer.Data[fingerId].DoDispose();
            var frameDataStart = new FrameData(new InputData(fingerId), null, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };

            statesData[fingerId].InBounds = true;
            statesData[fingerId].IsPainting = true;
            statesData[fingerId].IsPaintingPerformed = true;
            frameContainer.Data[fingerId].AddFrameData(frameDataStart);

            var frameDataEnd = new FrameData(new InputData(fingerId), null, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };

            frameContainer.Data[fingerId].AddFrameData(frameDataEnd);
            var texturePositions = new List<Vector2>(2)
            {
                texturePositionStart,
                texturePositionEnd
            };

            var brushes = new List<float>(2)
            {
                pressureStart * PaintData.Brush.Size,
                pressureEnd * PaintData.Brush.Size
            };

            LineDrawer.RenderLineUVInterpolated(texturePositions, RenderOffset, PaintData.Brush.RenderTexture, PaintData.Brush.Size, brushes, Tool.RandomizeLinesQuadsAngle);
            IsPainted = true;
            RenderToTextures();
            onFinish?.Invoke();
            frameContainer.Data[fingerId].DoDispose();
            RestorePaintState(state, fingerId);
        }

        /// <summary>
        /// Draws a line with brush samples
        /// </summary>
        /// <param name="drawLineData"></param>
        /// <param name="onFinish"></param>
        public void DrawLine(DrawLineData drawLineData, Action onFinish = null)
        {
            DrawLine(drawLineData.StartPointData.InputData, drawLineData.EndPointData.InputData,
                drawLineData.StartPointData.RaycastData, drawLineData.EndPointData.RaycastData, drawLineData.LineData, onFinish);
        }

        /// <summary>
        /// Draws a line with brush samples
        /// </summary>
        /// <param name="inputDataStart"></param>
        /// <param name="inputDataEnd"></param>
        /// <param name="raycastDataStart"></param>
        /// <param name="raycastDataEnd"></param>
        /// <param name="raycasts"></param>
        /// <param name="onFinish"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void DrawLine(InputData inputDataStart, InputData inputDataEnd, RaycastData raycastDataStart, RaycastData raycastDataEnd, KeyValuePair<Ray, RaycastData>[] raycasts = null, Action onFinish = null)
        {
            Debug.Log("DrawLine called");

            if (inputDataStart.FingerId < 0 || inputDataStart.FingerId >= frameContainer.Data.Length)
                throw new ArgumentOutOfRangeException(nameof(inputDataStart.FingerId));

            var state = SavePaintState(inputDataStart.FingerId);
            frameContainer.Data[inputDataStart.FingerId].DoDispose();
            var frameDataStart = new FrameData(inputDataStart, raycastDataStart, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };

            statesData[inputDataStart.FingerId].InBounds = true;
            statesData[inputDataStart.FingerId].IsPainting = true;
            statesData[inputDataStart.FingerId].IsPaintingPerformed = true;
            frameContainer.Data[inputDataStart.FingerId].AddFrameData(frameDataStart);

            var frameDataEnd = new FrameData(inputDataEnd, raycastDataEnd, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };
            frameContainer.Data[inputDataStart.FingerId].AddFrameData(frameDataEnd);

            if (Tool.Smoothing > 1)
            {
                frameContainer.Data[inputDataStart.FingerId].AddFrameData(frameDataEnd);
            }

            IsPainted |= TryRenderLine(inputDataStart.FingerId, false, raycasts);
            RenderToTextures();
            onFinish?.Invoke();
            frameContainer.Data[inputDataStart.FingerId].DoDispose();
            RestorePaintState(state, inputDataStart.FingerId);
        }

        #endregion

        public void FinishPainting(int fingerId = 0, bool forceFinish = false)
        {
            lastUVsByFinger.Clear();

            var render = false;
            if (forceFinish)
            {
                render = true;
                RenderGeometries(true);
            }

            var frameData = frameContainer.Data[fingerId].GetFrameData(0);
            if (statesData[fingerId].IsPaintingPerformed || forceFinish)
            {
                if (PaintData.PaintMode.UsePaintInput)
                {
                    BakeInputToPaint();
                    ClearTexture(RenderTarget.Input);
                }

                if (frameData != null)
                {
                    frameData.State.IsPainting = false;
                    frameData.State.IsPaintingPerformed = false;
                }

                if ((statesData[fingerId].IsPaintingPerformed || forceFinish) && Tool.ProcessingFinished)
                {
                    SaveUndoTexture();
                }

                var paintState = statesData[fingerId];
                paintState.IsPainting = false;
                paintState.IsPaintingPerformed = false;

                frameData?.DoDispose();
                frameData = null;

                if (!PaintData.PaintMode.UsePaintInput)
                {
                    ClearTexture(RenderTarget.Input);
                    RenderToTextures();
                    render = false;
                }
            }

            if (render)
            {
                RenderToTextures();
            }

            Paint.SetPreviewVector(Vector4.zero);
            statesData[fingerId].DoDispose();
            frameData?.DoDispose();
        }

        /// <summary>
        /// Renders Points and Lines
        /// </summary>
        /// <param name="finishPainting"></param>
        /*public void RenderGeometries(bool finishPainting = false)
        {
            if (clearTexture)
            {
                ClearTexture(RenderTarget.Input);
                clearTexture = false;
                if (writeClear && Tool.RenderToTextures)
                {
                    SaveUndoTexture();
                    writeClear = false;
                }
            }

            IsPainted = false;

            // ✅ Global drip timer (runs once per frame, not per finger)
            if (isUsingDrip)
            {
                dripTimer += Time.deltaTime;
            }

            for (var i = 0; i < frameContainer.Data.Length; i++)
            {
                if (frameContainer.Data[i].Count == 0)
                    continue;

                var frameData = frameContainer.Data[i].GetFrameData(0);

                if (IsPainting &&
                    (!Tool.ConsiderPreviousPosition ||
                     frameContainer.Data[i].Count == 1 ||
                     (frameContainer.Data[i].Count > 1 &&
                      frameData.InputData.Position != frameContainer.Data[i].GetFrameData(1).InputData.Position &&
                      frameData.InputData.InputSource == frameContainer.Data[i].GetFrameData(1).InputData.InputSource)) &&
                    Tool.AllowRender)
                {
                    if (frameContainer.Data[i].Count == 1 && frameData.RaycastData != null)
                    {
                        IsPainted |= TryRenderPoint(i);
                    }
                    else if (Tool.BaseSettings.CanPaintLines && AreRaycastDataValid(i, 2))
                    {
                        Debug.Log("This is getting called");
                        if (lineActive)
                        {
                            IsPainted |= TryRenderLine(i, finishPainting);

                        }
                        else { IsPainted |= TryRenderPoint(i); }
                        
                    }
                }

                frameData.State.IsPaintingPerformed |= IsPainted;
                statesData[frameData.InputData.FingerId].IsPaintingPerformed |= frameData.State.IsPaintingPerformed;

                // ✅ Stamp once per interval at the *first* valid stroke
                if (isUsingDrip && dripTimer >= dripInterval && frameData?.RaycastData != null)
                {
                    dripTimer = 0f;
                    var uv = ConvertUVToTexturePosition(frameData.RaycastData.UVHit);
                    var pressure = frameData.InputData.Pressure;
                    StampDripAt(uv, pressure);
                    break; // ✅ ensure only one drip is placed this frame
                }
            }
        }*/

        public void RenderGeometries(bool finishPainting = false)
        {
            if (clearTexture)
            {
                ClearTexture(RenderTarget.Input);
                clearTexture = false;
                if (writeClear && Tool.RenderToTextures)
                {
                    SaveUndoTexture();
                    writeClear = false;
                }
            }

            IsPainted = false;

            // ✅ Global drip timer (runs once per frame, not per finger)
            if (isUsingDrip)
            {
                dripTimer += Time.deltaTime;
            }

            for (var i = 0; i < frameContainer.Data.Length; i++)
            {
                if (frameContainer.Data[i].Count == 0)
                    continue;

                var frameData = frameContainer.Data[i].GetFrameData(0);

                if (IsPainting &&
                    (!Tool.ConsiderPreviousPosition ||
                     frameContainer.Data[i].Count == 1 ||
                     (frameContainer.Data[i].Count > 1 &&
                      frameData.InputData.Position != frameContainer.Data[i].GetFrameData(1).InputData.Position &&
                      frameData.InputData.InputSource == frameContainer.Data[i].GetFrameData(1).InputData.InputSource)) &&
                    Tool.AllowRender)
                {
                    if (frameContainer.Data[i].Count == 1 && frameData.RaycastData != null)
                    {
                        // ✅ Apply distance-based filtering before calling TryRenderPoint
                        var currentUV = frameData.RaycastData.UVHit;
                        bool shouldRender = true;

                        /*if (lastUVsByFinger.TryGetValue(i, out var lastUV))
                        {
                            float distance = Vector2.Distance(currentUV, lastUV);
                            shouldRender = distance >= uvDistanceThreshold;
                        }*/
                        if (lastUVsByFinger.TryGetValue(i, out var lastUV))
                        {
                            float distance = Vector2.Distance(currentUV, lastUV);
                            float spacingThreshold = Random.Range(spacingThresholdRange.x, spacingThresholdRange.y);
                            shouldRender = distance >= spacingThreshold;
                        }

                        if (shouldRender)
                        {
                            IsPainted |= TryRenderPoint(i);
                            lastUVsByFinger[i] = currentUV;
                        }
                    }
                    else if (Tool.BaseSettings.CanPaintLines && AreRaycastDataValid(i, 2))
                    {
                        
                        if (lineActive)
                        {
                            IsPainted |= TryRenderLine(i, finishPainting);
                        }
                        
                        if (!lineActive && !isUsingDrip)
                        {
                            Debug.Log("This is getting called");
                            // ✅ Apply distance-based filtering before calling TryRenderPoint
                            var currentUV = frameData.RaycastData.UVHit;
                            bool shouldRender = true;

                            /*if (lastUVsByFinger.TryGetValue(i, out var lastUV))
                            {
                                float distance = Vector2.Distance(currentUV, lastUV);
                                shouldRender = distance >= uvDistanceThreshold;
                            }*/
                            if (lastUVsByFinger.TryGetValue(i, out var lastUV))
                            {
                                float distance = Vector2.Distance(currentUV, lastUV);
                                float spacingThreshold = Random.Range(spacingThresholdRange.x, spacingThresholdRange.y);
                                shouldRender = distance >= spacingThreshold;
                            }

                            if (shouldRender)
                            {
                                IsPainted |= TryRenderPoint(i);
                                lastUVsByFinger[i] = currentUV;
                            }
                        }
                    }
                }

                frameData.State.IsPaintingPerformed |= IsPainted;
                statesData[frameData.InputData.FingerId].IsPaintingPerformed |= frameData.State.IsPaintingPerformed;

                // ✅ Stamp once per interval at the *first* valid stroke
                if (isUsingDrip && dripTimer >= dripInterval && frameData?.RaycastData != null)
                {
                    dripTimer = 0f;
                    var uv = ConvertUVToTexturePosition(frameData.RaycastData.UVHit);
                    var pressure = frameData.InputData.Pressure;
                    StampDripAt(uv, pressure);
                    break; // ✅ ensure only one drip is placed this frame
                }
            }
        }


        private void StampDripAt(Vector2 texturePosition, float pressure)
        {
            float currentBrushSize = PaintManager.Brush.Size;
            float randomAngle = Random.Range(0, 360);
            if (randomBrushAngle)
            {
                PaintManager.Brush.RenderAngle = randomAngle;
                //Debug.Log("Brush angle is: " + PaintManager.Brush.RenderAngle);
            }
            //if (randomSize)
            //{
            PaintManager.Brush.Size = Random.Range(randomSizeRange.x, randomSizeRange.y);
            //}

            if (!isInitialized)
            {
                Debug.LogWarning("Paint system not initialized.");
                return;
            }

            if (PaintData.PaintSpace != PaintSpace.UV)
            {
                Debug.LogWarning("Drip stamp aborted — not in UV paint space.");
                return;
            }

            var mainTexture = PaintManager.Brush.SourceTexture;
            //float mainSize = PaintManager.Brush.Size;

            Texture texture = null;

            if (!useDripImageCycle)
            {
                texture = dripImage?.sprite?.texture;
            }
            else
            {
                int dripImageNum = Random.Range(0, dripImageArray.Length -1);
                texture = dripImageArray[dripImageNum];
            }
            
            if (texture == null)
            {
                Debug.LogWarning("Drip texture is null.");
                return;
            }

            // Set drip brush
            PaintManager.Brush.SetTexture(texture, true, false);
            //PaintManager.Brush.Size = 1f;

            // ✅ Use the correct overload for UV-based painting
            DrawPoint(texturePosition, pressure * 0.8f, 0, () =>
            {
                // Restore main brush
                PaintManager.Brush.SetTexture(mainTexture, true, false);
                PaintManager.Brush.Size = currentBrushSize;
                Debug.Log("Drip brush reset.");
            });

            Debug.Log("Drip brush stamp triggered.");
        }



        /// <summary>
        /// Combines textures, render preview
        /// </summary>
        public void RenderToTextures()
        {
            DrawPreProcess();
            ClearTexture(RenderTarget.Combined);
            DrawProcess();
        }

        public void RenderToTextureWithoutPreview(RenderTexture resultTexture)
        {
            DrawPreProcess();
            ClearTexture(RenderTarget.Combined);

            var boundsStack = new Stack<bool>();
            foreach (var buffer in frameContainer.Data)
            {
                for (var i = 0; i < buffer.Count; i++)
                {
                    boundsStack.Push(buffer.GetFrameData(i).State.InBounds);
                    buffer.GetFrameData(i).State.InBounds = false;
                }
            }

            DrawProcess();

            foreach (var buffer in frameContainer.Data)
            {
                for (var i = 0; i < buffer.Count; i++)
                {
                    buffer.GetFrameData(i).State.InBounds = boundsStack.Peek();
                }
            }

            Graphics.Blit(PaintData.TextureHelper.GetTexture(RenderTarget.Combined), resultTexture);
        }

        public void SaveUndoTexture()
        {
            PaintData.LayersController.ActiveLayer.SaveState();
        }

        public void SetRaycastProcessor(BaseRaycastProcessor processor)
        {
            raycastProcessor = processor;
        }

        /// <summary>
        /// Restores texture when Undo/Redo invoking
        /// </summary>
        private void OnExtraDraw()
        {
            if (!PaintData.PaintMode.UsePaintInput)
            {
                ClearTexture(RenderTarget.Input);
            }

            RenderToTextures();
        }

        private void OnClearTexture(RenderTexture renderTexture)
        {
            ClearTexture(renderTexture, Color.clear);
            RenderToTextures();
        }

        private void UpdatePaintData(FrameData frameData, bool updateBrushPreview)
        {
            frameData.State.InBounds = IsInBounds(frameData.InputData.Ray);
            var paintState = statesData[frameData.InputData.FingerId];
            paintState.InBounds |= frameData.State.InBounds;
            if (frameData.State.InBounds)
            {
                if (updateBrushPreview)
                {
                    UpdateBrushPreview(frameData);
                }
            }
        }

        /*public bool TryRenderPoint(int fingerId = 0)
        {
            var frameData = frameContainer.Data[fingerId].GetFrameData(0);
            if (frameData.RaycastData == null || (raycastProcessor != null && 
                !raycastProcessor.TryProcessRaycastPosition(frameData.InputData.Ray, frameData.RaycastData, out _)))
                return false;
            
            var texturePosition = ConvertUVToTexturePosition(frameData.RaycastData.UVHit);
            if (OnDrawPoint != null)
            {
                var data = new DrawPointData(frameData.InputData, frameData.RaycastData, texturePosition);
                OnDrawPoint.Invoke(data);
            }

            if (PaintData.PaintSpace == PaintSpace.UV)
            {
                UpdateQuadMesh(texturePosition, RenderOffset, PaintData.Brush.Size * frameData.InputData.Pressure, Tool.RandomizePointsQuadsAngle);
            }

            if (PaintData.PaintSpace == PaintSpace.World)
            {
                worldData.Positions[0] = ObjectTransform.TransformPoint(frameData.RaycastData.Hit);
                worldData.Rotations[0] = Tool.RandomizePointsQuadsAngle ? Random.value * 360f : 0f;
                worldData.Normals[0] = frameData.RaycastData.Triangle.WorldNormal;
                worldData.Count = 1;
                var brushSizes = new[] { PaintData.Brush.Size * frameData.InputData.Pressure, PaintData.Brush.Size * frameData.InputData.Pressure };
                SetPaintWorldProperties(worldData, frameData.InputData.Ray.origin, brushSizes);
            }

            RenderMesh();
            return true;
        }*/

        public bool TryRenderPoint(int fingerId = 0)
        {
            float randomAngle = Random.Range(0, 360);
            if (randomBrushAngle)
            {
                PaintManager.Brush.RenderAngle = randomAngle;
                //Debug.Log("Brush angle is: " + PaintManager.Brush.RenderAngle);
            }

            PaintManager.Brush.Size = Random.Range(randomSizeRange.x, randomSizeRange.y);

            Color paintColor = PaintManager.Brush.Color;
            paintColor.a = Random.Range(randomOpacityRange.x, randomOpacityRange.y);
            PaintManager.Brush.SetColor(paintColor);

            var frameData = frameContainer.Data[fingerId].GetFrameData(0);
            if (frameData.RaycastData == null || (raycastProcessor != null &&
                !raycastProcessor.TryProcessRaycastPosition(frameData.InputData.Ray, frameData.RaycastData, out _)))
                return false;

            var currentUV = frameData.RaycastData.UVHit;
            var texturePosition = ConvertUVToTexturePosition(currentUV);

            // Interpolation threshold (can be adjusted)
            //float spacingThreshold = 0.1f;

            // Interpolate from last stamped point to this one if needed
            if (lastUVHit.HasValue)
            {
                var lastUV = lastUVHit.Value;
                float distance = Vector2.Distance(currentUV, lastUV);
                //int steps = Mathf.FloorToInt(distance / spacingThreshold);
                float spacingThreshold = Random.Range(spacingThresholdRange.x, spacingThresholdRange.y);
                int steps = Mathf.FloorToInt(distance / spacingThreshold);
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)(steps + 1);
                    Vector2 interpUV = Vector2.Lerp(lastUV, currentUV, t);
                    Vector2 interpTexPos = ConvertUVToTexturePosition(interpUV);

                    var interpData = new DrawPointData(frameData.InputData, frameData.RaycastData, interpTexPos);
                    OnDrawPoint?.Invoke(interpData);



                    if (PaintData.PaintSpace == PaintSpace.UV)
                    {
                        UpdateQuadMesh(interpTexPos, RenderOffset, PaintData.Brush.Size * frameData.InputData.Pressure, Tool.RandomizePointsQuadsAngle);
                    }
                }
            }

            // Stamp at the current position
            if (OnDrawPoint != null)
            {
                //Debug.Log("This code is getting called");
                var data = new DrawPointData(frameData.InputData, frameData.RaycastData, texturePosition);
                OnDrawPoint.Invoke(data);
            }

            if (PaintData.PaintSpace == PaintSpace.UV)
            {
                UpdateQuadMesh(texturePosition, RenderOffset, PaintData.Brush.Size * frameData.InputData.Pressure, Tool.RandomizePointsQuadsAngle);
            }

            if (PaintData.PaintSpace == PaintSpace.World)
            {
                worldData.Positions[0] = ObjectTransform.TransformPoint(frameData.RaycastData.Hit);
                worldData.Rotations[0] = Tool.RandomizePointsQuadsAngle ? Random.value * 360f : 0f;
                worldData.Normals[0] = frameData.RaycastData.Triangle.WorldNormal;
                worldData.Count = 1;
                var brushSizes = new[] { PaintData.Brush.Size * frameData.InputData.Pressure, PaintData.Brush.Size * frameData.InputData.Pressure };
                SetPaintWorldProperties(worldData, frameData.InputData.Ray.origin, brushSizes);
            }

            lastUVHit = currentUV; // Store the current UV for next frame
            RenderMesh();
            return true;
        }

        /* public bool TryRenderPoint(int fingerId = 0)
         {
             var frameData = frameContainer.Data[fingerId].GetFrameData(0);
             if (frameData.RaycastData == null || (raycastProcessor != null &&
                 !raycastProcessor.TryProcessRaycastPosition(frameData.InputData.Ray, frameData.RaycastData, out _)))
                 return false;

             var currentUV = frameData.RaycastData.UVHit;
             var texturePosition = ConvertUVToTexturePosition(currentUV);

             // Spacing threshold in UV space
             float spacingThreshold = 0.01f;

             // Interpolation logic
             if (lastUVHit.HasValue)
             {
                 var lastUV = lastUVHit.Value;
                 float distance = Vector2.Distance(currentUV, lastUV);
                 int steps = Mathf.FloorToInt(distance / spacingThreshold);

                 if (steps > 0)
                     Debug.Log($"Interpolating {steps} steps from {lastUV} to {currentUV}");

                 for (int i = 1; i <= steps; i++)
                 {
                     float t = i / (float)(steps + 1);
                     Vector2 interpUV = Vector2.Lerp(lastUV, currentUV, t);
                     Vector2 interpTexPos = ConvertUVToTexturePosition(interpUV);

                     var data = new DrawPointData(frameData.InputData, frameData.RaycastData, interpTexPos);
                     OnDrawPoint?.Invoke(data);

                     if (PaintData.PaintSpace == PaintSpace.UV)
                     {
                         UpdateQuadMesh(interpTexPos, RenderOffset, PaintData.Brush.Size * frameData.InputData.Pressure, Tool.RandomizePointsQuadsAngle);
                     }
                 }

                 if (steps > 0)
                     RenderMesh();
             }

             // Final actual point
             if (OnDrawPoint != null)
             {
                 var data = new DrawPointData(frameData.InputData, frameData.RaycastData, texturePosition);
                 OnDrawPoint.Invoke(data);
             }

             if (PaintData.PaintSpace == PaintSpace.UV)
             {
                 UpdateQuadMesh(texturePosition, RenderOffset, PaintData.Brush.Size * frameData.InputData.Pressure, Tool.RandomizePointsQuadsAngle);
             }

             if (PaintData.PaintSpace == PaintSpace.World)
             {
                 worldData.Positions[0] = ObjectTransform.TransformPoint(frameData.RaycastData.Hit);
                 worldData.Rotations[0] = Tool.RandomizePointsQuadsAngle ? Random.value * 360f : 0f;
                 worldData.Normals[0] = frameData.RaycastData.Triangle.WorldNormal;
                 worldData.Count = 1;
                 var brushSizes = new[] { PaintData.Brush.Size * frameData.InputData.Pressure, PaintData.Brush.Size * frameData.InputData.Pressure };
                 SetPaintWorldProperties(worldData, frameData.InputData.Ray.origin, brushSizes);
             }

             RenderMesh(); // Ensure the final point is rendered
             lastUVHit = currentUV;
             return true;
         }*/

        private bool TryRenderLine(int fingerId = 0, bool finishPainting = false, IList<KeyValuePair<Ray, RaycastData>> raycasts = null)
        {
            Debug.Log("TryRenderLine called");

            var frameData = frameContainer.Data[fingerId].GetFrameData(0);
            if (!CanSmoothLines || Tool.Smoothing == 1)
            {
                if (PaintData.PaintSpace == PaintSpace.UV)
                {
                    if (raycasts == null)
                    {
                        raycasts = GetRaycasts(!CanSmoothLines, 2, fingerId);
                    }

                    if (!(lineProcessor is LineUVProcessor))
                    {
                        lineProcessor = new LineUVProcessor(ConvertUVToTexturePosition);
                    }

                    if (lineProcessor.TryProcessLine(frameContainer.Data[fingerId], raycasts, finishPainting, out var linesData))
                    {
                        if (OnDrawLine != null)
                        {
                            var frameDataStart = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1);
                            var frameDataEnd = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(0);
                            var data = new DrawLineData(
                                new DrawPointData(frameDataStart.InputData, frameDataStart.RaycastData, ConvertUVToTexturePosition(frameDataStart.RaycastData.UVHit)),
                                new DrawPointData(frameDataEnd.InputData, frameDataEnd.RaycastData, ConvertUVToTexturePosition(frameDataEnd.RaycastData.UVHit)), raycasts.ToArray());
                            OnDrawLine.Invoke(data);
                        }

                        var uvLineData = (TextureLineData)linesData[0];
                        if (CanSmoothLines)
                        {
                            LineDrawer.RenderLineUVInterpolated(uvLineData.TexturePositions, RenderOffset, PaintData.Brush.RenderTexture, PaintData.Brush.Size, uvLineData.Pressures, Tool.RandomizeLinesQuadsAngle);
                        }
                        else
                        {
                            LineDrawer.RenderLineUV(uvLineData.TexturePositions, RenderOffset, PaintData.Brush.RenderTexture, uvLineData.Pressures, Tool.RandomizeLinesQuadsAngle);
                        }

                        return true;
                    }

                    return false;
                }

                if (PaintData.PaintSpace == PaintSpace.World)
                {
                    if (raycasts == null)
                    {
                        raycasts = GetRaycasts(true, 2, fingerId);
                    }

                    if (!(lineProcessor is LineWorldProcessor))
                    {
                        lineProcessor = new LineWorldProcessor();
                    }

                    if (lineProcessor.TryProcessLine(frameContainer.Data[fingerId], raycasts, finishPainting, out var linesData))
                    {
                        if (OnDrawLine != null)
                        {
                            var frameDataStart = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1);
                            var frameDataEnd = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(0);
                            var data = new DrawLineData(
                                new DrawPointData(frameDataStart.InputData, frameDataStart.RaycastData, ConvertUVToTexturePosition(frameDataStart.RaycastData.UVHit)),
                                new DrawPointData(frameDataEnd.InputData, frameDataEnd.RaycastData, ConvertUVToTexturePosition(frameDataEnd.RaycastData.UVHit)), raycasts.ToArray());
                            OnDrawLine.Invoke(data);
                        }

                        var brushSizes = new float[2] { PaintData.Brush.Size, PaintData.Brush.Size };
                        for (var i = 0; i < brushSizes.Length; i++)
                        {
                            if (frameContainer.Data[fingerId].GetFrameData(i).RaycastData == null)
                                break;

                            brushSizes[i] *= frameContainer.Data[fingerId].GetFrameData(i).InputData.Pressure;
                        }

                        foreach (var lineData in linesData)
                        {
                            var worldLineData = (WorldLineData)lineData;
                            for (var i = 0; i < worldLineData.Positions.Length; i++)
                            {
                                worldData.Positions[i] = worldLineData.Positions[i];
                                worldData.Normals[i] = worldLineData.Normals[i];
                            }

                            for (var i = 0; i < worldData.Rotations.Length; i++)
                            {
                                worldData.Rotations[i] = Tool.RandomizeLinesQuadsAngle ? Random.value * 360f : 0f;
                            }

                            worldData.Count = worldLineData.Count;
                            SetPaintWorldProperties(worldData, worldLineData.PointerPosition, brushSizes);
                            LineDrawer.RenderLineWorld();
                        }

                        return true;
                    }

                    return false;
                }
            }

            if (CanSmoothLines && AreRaycastDataValid(fingerId, 3))
            {
                if (raycasts == null)
                {
                    raycasts = GetRaycasts(false, 4, fingerId);
                }

                if (!(lineProcessor is LineSmoothUVProcessor))
                {
                    lineProcessor = new LineSmoothUVProcessor(ConvertUVToTexturePosition);
                }

                ((LineSmoothUVProcessor)lineProcessor).SetSmoothing(Tool.Smoothing);
                if (lineProcessor.TryProcessLine(frameContainer.Data[fingerId], raycasts, finishPainting, out var linesData))
                {
                    var textureLineSmoothData = (TextureSmoothLinesData)linesData[0];
                    foreach (var textureLineData in textureLineSmoothData.Data)
                    {
                        if (OnDrawLine != null)
                        {
                            const int lineElements = 3;
                            if (finishPainting)
                            {
                                var frameDataStart = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1);
                                var frameDataEnd = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(0);
                                var data = new DrawLineData(
                                    new DrawPointData(frameDataStart.InputData, frameDataStart.RaycastData, ConvertUVToTexturePosition(frameDataStart.RaycastData.UVHit)),
                                    new DrawPointData(frameDataEnd.InputData, frameDataEnd.RaycastData, ConvertUVToTexturePosition(frameDataEnd.RaycastData.UVHit)),
                                    null);
                                OnDrawLine.Invoke(data);
                            }
                            else
                            {
                                var frameDataStart = textureLineData.TexturePositions.Length == lineElements
                                    ? frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1)
                                    : frameContainer.Data[frameData.InputData.FingerId].GetFrameData(2);
                                var frameDataEnd = textureLineData.TexturePositions.Length == lineElements
                                    ? frameContainer.Data[frameData.InputData.FingerId].GetFrameData(0)
                                    : frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1);
                                var data = new DrawLineData(
                                    new DrawPointData(frameDataStart.InputData, frameDataStart.RaycastData, ConvertUVToTexturePosition(frameDataStart.RaycastData.UVHit)),
                                    new DrawPointData(frameDataEnd.InputData, frameDataEnd.RaycastData, ConvertUVToTexturePosition(frameDataEnd.RaycastData.UVHit)),
                                    null);
                                OnDrawLine.Invoke(data);
                            }
                        }
                    }

                    foreach (var textureLineData in textureLineSmoothData.Data)
                    {
                        LineDrawer.RenderLineUVInterpolated(textureLineData.TexturePositions, RenderOffset, PaintData.Brush.RenderTexture, PaintData.Brush.Size, textureLineData.Pressures, Tool.RandomizeLinesQuadsAngle);
                    }

                    return true;
                }

                return false;
            }

            return false;
        }

        protected void UpdateBrushPreview(FrameData frameData)
        {
            if (PaintData.Brush.Preview && frameData.State.InBounds)
            {
                if (frameData.RaycastData != null)
                {
                    if (PaintData.PaintSpace == PaintSpace.UV)
                    {
                        var previewVector = GetPreviewVector();
                        Paint.SetPreviewVector(previewVector);
                    }
                    else if (PaintData.PaintSpace == PaintSpace.World)
                    {
                        worldData.Positions[0] = ObjectTransform.TransformPoint(frameData.RaycastData.Hit);
                        worldData.Rotations[0] = 0f;
                        worldData.Normals[0] = frameData.RaycastData.Triangle.WorldNormal;
                        var brushSizes = new[] { PaintData.Brush.Size * frameData.InputData.Pressure, PaintData.Brush.Size * frameData.InputData.Pressure };
                        if (raycastProcessor != null)
                        {
                            worldData.Count = raycastProcessor.TryProcessRaycastPosition(frameData.InputData.Ray, frameData.RaycastData, out _) ? 1 : 0;
                        }
                        else
                        {
                            worldData.Count = 1;
                        }

                        SetPaintWorldProperties(worldData, frameData.InputData.Ray.origin, brushSizes);
                    }
                }
                else
                {
                    if (PaintData.PaintSpace == PaintSpace.UV)
                    {
                        Paint.SetPreviewVector(Vector4.zero);
                    }
                    else if (PaintData.PaintSpace == PaintSpace.World)
                    {
                        worldData.Count = 0;
                        SetPaintWorldCount(worldData.Count);
                    }
                }
            }

            return;

            Vector4 GetPreviewVector()
            {
                var brushRatio = new Vector2(
                    Paint.SourceTexture.width / (float)PaintData.Brush.RenderTexture.width,
                    Paint.SourceTexture.height / (float)PaintData.Brush.RenderTexture.height) / PaintData.Brush.Size / frameData.InputData.Pressure;
                var texturePosition = frameData.RaycastData.UVHit * new Vector2(Paint.SourceTexture.width, Paint.SourceTexture.height);
                var brushOffset = new Vector4(
                    texturePosition.x / Paint.SourceTexture.width * brushRatio.x + PaintData.Brush.RenderOffset.x,
                    texturePosition.y / Paint.SourceTexture.height * brushRatio.y + PaintData.Brush.RenderOffset.y,
                    brushRatio.x, brushRatio.y);
                return brushOffset;
            }
        }

        private IList<KeyValuePair<Ray, RaycastData>> GetRaycasts(bool raycast, int count, int fingerId = 0)
        {
            var raycasts = new List<KeyValuePair<Ray, RaycastData>>();
            if (frameContainer.Data[fingerId].Count >= 2 && raycast)
            {
                var raycastsData = new RaycastData[2];
                var averageBrushSize = 0f;
                var frameData = frameContainer.Data[fingerId];
                for (var i = 0; i < 2; i++)
                {
                    var data = frameData.GetFrameData(i);
                    if (data.RaycastData == null)
                        break;

                    raycastsData[i] = data.RaycastData;
                    averageBrushSize += data.InputData.Pressure * PaintData.Brush.Size;
                }

                averageBrushSize /= 2f;
                raycasts = LineDrawer.GetLineRaycasts(raycastsData[1], raycastsData[0], frameContainer.Data[fingerId].GetFrameData(0).InputData.Ray.origin, averageBrushSize, fingerId);
            }
            else
            {
                count = Mathf.Min(count, frameContainer.Data[fingerId].Count);
                for (var i = 0; i < count; i++)
                {
                    var data = frameContainer.Data[fingerId].GetFrameData(i);
                    if (data.RaycastData == null)
                        break;

                    raycasts.Add(new KeyValuePair<Ray, RaycastData>(data.InputData.Ray, data.RaycastData));
                }
            }

            if (raycastProcessor != null)
            {
                for (var i = raycasts.Count - 1; i >= 0; i--)
                {
                    var pair = raycasts[i];
                    if (!raycastProcessor.TryProcessRaycastPosition(pair.Key, pair.Value, out _))
                    {
                        raycasts.RemoveAt(i);
                    }
                }
            }

            return raycasts;
        }

        private bool AreRaycastDataValid(int fingerId, int frames)
        {
            if (frameContainer.Data[fingerId].Count < frames)
                return false;

            for (var i = 0; i < frames; i++)
            {
                if (frameContainer.Data[fingerId].GetFrameData(i).RaycastData == null)
                {
                    return false;
                }
            }
            return true;
        }
    }
}