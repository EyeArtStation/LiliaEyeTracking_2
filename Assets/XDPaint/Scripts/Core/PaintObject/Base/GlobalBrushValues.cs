using UnityEngine;

public class GlobalBrushValues : MonoBehaviour
{
    public static GlobalBrushValues Instance { get; private set; }

    [Header("Brush Settings")]
    public float brushSize = 0.05f;
    public Vector2 randomSizeRange = new Vector2(0.05f, 0.05f);
    public Vector2 randomDripRange = new Vector2(1f, 3f);
    public Color brushColor = Color.white;
    public float brushOpacity = 1.0f;

    private void Awake()
    {
        // Ensure there's only one instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Keep between scenes
    }

    // Optional: Add methods to update values globally
    public void SetBrushSize(float size)
    {
        brushSize = size;
    }

    public void SetBrushColor(Color color)
    {
        brushColor = color;
    }

    public void SetBrushOpacity(float opacity)
    {
        brushOpacity = opacity;
    }
}
