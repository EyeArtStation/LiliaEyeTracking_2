using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class GazeUIHover : MonoBehaviour
{
    [Header("Gaze Input")]
    public bool debugUseMouse = false;
    public Vector2 gazeScreenPosition;

    [Header("UI Cursor (optional but usually what you want)")]
    [Tooltip("Assign the RectTransform of your visible cursor image.")]
    public RectTransform cursorRect;

    [Tooltip("The canvas containing the cursor. If null, will try FindObjectOfType<Canvas>().")]
    public Canvas cursorCanvas;

    [Tooltip("If your gaze Y=0 is top of screen, enable this.")]
    public bool gazeIsTopLeftOrigin = false;

    [Header("Behavior")]
    public bool sendMoveEvents = true;
    public bool requireValidScreenPoint = true;

    private PointerEventData _ped;
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(32);
    private GameObject _currentHover;

    public TobiiGazeProvider tobiiGazeProvider;

    [Header("UI Filtering")]
    [Tooltip("Only treat hits on these layers as UI. Leave at Everything if you don't use UI layers.")]
    public LayerMask uiLayerMask = ~0;

    [Tooltip("If true, this script will only run UI hover when actually over UI; otherwise it won't touch EventSystem.")]
    public bool onlyAffectUIWhenOverUI = true;

    // Expose whether gaze is over UI so your paint code can consult it if needed
    public bool IsGazeOverUI { get; private set; }

    public bool moveGazeCursor;

    void Awake()
    {
        if (EventSystem.current == null)
        {
            Debug.LogError("[GazeUIHover] No EventSystem in scene.");
            enabled = false;
            return;
        }

        _ped = new PointerEventData(EventSystem.current);

        if (cursorCanvas == null && cursorRect != null)
            cursorCanvas = cursorRect.GetComponentInParent<Canvas>();
    }

    void Update()
    {
        if (tobiiGazeProvider == null) return;

        // 1) Read gaze
        gazeScreenPosition = tobiiGazeProvider._smoothedPx;
        Vector2 pos = debugUseMouse ? (Vector2)Input.mousePosition : gazeScreenPosition;

        if (gazeIsTopLeftOrigin)
            pos.y = Screen.height - pos.y;

        if (requireValidScreenPoint)
        {
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y)) return;
            if (pos.x < 0 || pos.y < 0 || pos.x > Screen.width || pos.y > Screen.height) return;
        }

        // 2) UI raycast
        _ped.Reset();
        _ped.position = pos;

        _raycastResults.Clear();
        EventSystem.current.RaycastAll(_ped, _raycastResults);

        GameObject newHover = null;

        // Pick first valid UI hit (optionally filtered by layer)
        for (int i = 0; i < _raycastResults.Count; i++)
        {
            var go = _raycastResults[i].gameObject;
            if (((1 << go.layer) & uiLayerMask.value) != 0)
            {
                newHover = go;
                break;
            }
        }

        IsGazeOverUI = (newHover != null);

        // If we're not over UI, don't spam pointer events (and clear hover once)
        if (onlyAffectUIWhenOverUI && !IsGazeOverUI)
        {
            if (_currentHover != null)
            {
                ExecuteEvents.Execute(_currentHover, _ped, ExecuteEvents.pointerExitHandler);
                _currentHover = null;
            }
            return;
        }

        // 3) Hover transitions
        if (newHover != _currentHover)
        {
            if (_currentHover != null)
                ExecuteEvents.Execute(_currentHover, _ped, ExecuteEvents.pointerExitHandler);

            _currentHover = newHover;

            if (_currentHover != null)
                ExecuteEvents.Execute(_currentHover, _ped, ExecuteEvents.pointerEnterHandler);
        }

        if (sendMoveEvents && _currentHover != null)
            ExecuteEvents.Execute(_currentHover, _ped, ExecuteEvents.pointerMoveHandler);

        if (moveGazeCursor) { UpdateVisualCursor(pos); }
    }

    public void TurnOffMoveCursor() { moveGazeCursor = false; }

    private void UpdateVisualCursor(Vector2 screenPos)
    {
        if (cursorRect == null || cursorCanvas == null) return;

        // For Screen Space - Overlay: eventCamera should be null.
        // For Screen Space - Camera / World Space: use canvas.worldCamera.
        Camera cam = null;
        if (cursorCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = cursorCanvas.worldCamera;

        RectTransform canvasRect = (RectTransform)cursorCanvas.transform;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, cam, out Vector2 localPoint))
        {
            cursorRect.anchoredPosition = localPoint;
        }
    }
}
