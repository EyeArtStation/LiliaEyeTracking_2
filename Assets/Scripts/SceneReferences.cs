using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SceneReferences : MonoBehaviour
{
    public GameObject SelectCanvasScreen;
    public GameObject settingsScreen;
    public PaintManagerCustom paintManagerCustom;
    public GameObject cursor;
    public GameObject pauseCursor;
    public GameObject gazeCursor;
    public Button rainbowButton;
    public GameObject pauseButton;
    public Button playButton;
    public GameObject TobiiSystem;
    public GazeUIHover gazeUIHover;

    public static SceneReferences Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
