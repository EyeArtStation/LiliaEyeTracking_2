using UnityEngine;
using UnityEngine.UI;

public class DigitalPainter : MonoBehaviour
{
    public RawImage canvasDisplay; // Assign in the Inspector
    public Texture2D brushTexture; // Soft brush texture, placed in Resources folder

    private Texture2D canvas;
    private Vector2? lastMousePos = null;

    void Start()
    {
        // Create the drawing canvas
        canvas = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        canvas.filterMode = FilterMode.Bilinear; // Smooth appearance
        ClearCanvas(Color.white);
        canvas.Apply();

        if (canvasDisplay != null)
        {
            canvasDisplay.texture = canvas;
        }

        // Load brush texture from Resources folder
        if (brushTexture == null)
        {
            brushTexture = Resources.Load<Texture2D>("A_grayscale_digital_image_displays_a_circular_shap");
        }
    }

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector2 currentMouse = Input.mousePosition;
            int x = (int)currentMouse.x;
            int y = (int)(Screen.height - currentMouse.y); // Flip Y for texture space

            // Interpolate between last and current mouse position for smooth strokes
            if (lastMousePos.HasValue)
            {
                Vector2 last = lastMousePos.Value;
                float distance = Vector2.Distance(last, currentMouse);
                int steps = Mathf.CeilToInt(distance / 2f); // adjust for smoothness

                for (int i = 0; i <= steps; i++)
                {
                    Vector2 interp = Vector2.Lerp(last, currentMouse, i / (float)steps);
                    int ix = (int)interp.x;
                    int iy = (int)(Screen.height - interp.y);
                    StampBrush(ix, iy, Color.black);
                }
            }
            else
            {
                StampBrush(x, y, Color.black);
            }

            lastMousePos = currentMouse;
        }
        else
        {
            lastMousePos = null;
        }
    }

    void ClearCanvas(Color color)
    {
        Color[] pixels = new Color[canvas.width * canvas.height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        canvas.SetPixels(pixels);
    }

    void StampBrush(int centerX, int centerY, Color brushColor, float opacity = 1f)
    {
        if (brushTexture == null || canvas == null) return;

        int w = brushTexture.width;
        int h = brushTexture.height;
        int startX = centerX - w / 2;
        int startY = centerY - h / 2;

        Color[] brushPixels = brushTexture.GetPixels();
        Color[] canvasPixels = canvas.GetPixels();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int px = startX + x;
                int py = startY + y;

                if (px >= 0 && px < canvas.width && py >= 0 && py < canvas.height)
                {
                    int canvasIndex = py * canvas.width + px;
                    int brushIndex = y * w + x;

                    float alpha = brushPixels[brushIndex].a * opacity;
                    Color existing = canvasPixels[canvasIndex];
                    Color blended = Color.Lerp(existing, brushColor, alpha);

                    canvasPixels[canvasIndex] = blended;
                }
            }
        }

        canvas.SetPixels(canvasPixels);
        canvas.Apply();
    }

}
