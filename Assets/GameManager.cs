using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public Texture2D customCursor;
    private bool usingCustom = false;

    public void ChangeCursor(bool useCursor)
    {
        if (useCursor)
        {
            Cursor.SetCursor(customCursor, Vector2.zero, CursorMode.Auto);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // reset
        }
    }
}
