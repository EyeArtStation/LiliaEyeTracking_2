using UnityEngine;
using UnityEngine.EventSystems;

public class UndoDwellController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public PaintManagerCustom paint;

    [Tooltip("Undo 1 step the first time you enter, then 2 steps per subsequent dwell tick while still hovered.")]
    public bool twoStepsAfterFirst = true;

    private bool hovered = false;
    private bool didFirstUndoThisHover = false;

    // Call this from your gaze timer when the dwell completes.
    public void OnDwellTriggered()
    {
        if (!hovered || paint == null) return;

        int steps = 1;

        if (twoStepsAfterFirst && didFirstUndoThisHover)
            steps = 2;

        paint.Undo(steps);

        didFirstUndoThisHover = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        didFirstUndoThisHover = false;
        paint.BeginUIBlock();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        didFirstUndoThisHover = false;
        paint.EndUIBlock();
    }


}
