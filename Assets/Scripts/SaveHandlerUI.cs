using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms; // Windows-only file dialogs
using UnityEngine;
using UnityEngine.UI;

public class SaveHandlerUI : MonoBehaviour
{
    private const string DefaultTextureFilename = "Texture.png";

    [Header("Target UI (pick one)")]
    [Tooltip("Preferred: assign a RawImage if your UI displays a Texture.")]
    public RawImage targetRawImage;

    [Tooltip("Optional: assign an Image if your UI displays a Sprite.")]
    public Image targetImage;

    [Header("Save Options")]
    [Tooltip("If true, will render the UI element into a new Texture2D before saving (works even if texture is not readable).")]
    public bool forceRenderToTexture = true;

    [Tooltip("Only used when forceRenderToTexture is true and targetRawImage is used.")]
    public Camera uiCamera;

    [Tooltip("Only used when forceRenderToTexture is true. PNG output resolution.")]
    public int renderWidth = 1024;
    public int renderHeight = 1024;

    // -------------------------
    // PUBLIC API (SYNC)
    // -------------------------

    [UnityEngine.ContextMenu("Save UI Image (Sync)")]
    public void SaveCurrentUITexture()
    {
        var tex = GetTextureToSave();
        if (tex == null)
        {
            Debug.LogError("[SaveHandlerUI] Nothing to save. Assign targetRawImage or targetImage and ensure it has content.");
            return;
        }

        string path = ShowSaveFileDialog(DefaultTextureFilename, "PNG Files|*.png");
        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("[SaveHandlerUI] Save cancelled.");
            return;
        }

        try
        {
            var pngData = tex.EncodeToPNG();
            if (pngData == null || pngData.Length == 0)
            {
                Debug.LogError("[SaveHandlerUI] Failed to encode PNG.");
                return;
            }

            File.WriteAllBytes(path, pngData);
            Debug.Log($"[SaveHandlerUI] Saved to: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveHandlerUI] Save error: {ex.Message}");
        }
        finally
        {
            // If we created a temporary texture by rendering, destroy it
            CleanupIfTemporary(tex);
        }
    }

    [UnityEngine.ContextMenu("Load UI Image (Sync)")]
    public void LoadTextureFromFile()
    {
        string path = ShowOpenFileDialog("Image Files|*.png;*.jpg;*.jpeg");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.Log("[SaveHandlerUI] Load cancelled or file not found.");
            return;
        }

        try
        {
            byte[] fileData = File.ReadAllBytes(path);
            ApplyLoadedBytes(fileData, path);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveHandlerUI] Load error: {ex.Message}");
        }
    }

    // -------------------------
    // PUBLIC API (ASYNC)
    // -------------------------

    public async void SaveCurrentUITextureAsync()
    {
        var tex = GetTextureToSave();
        if (tex == null)
        {
            Debug.LogError("[SaveHandlerUI] Nothing to save. Assign targetRawImage or targetImage and ensure it has content.");
            return;
        }

        string path = ShowSaveFileDialog(DefaultTextureFilename, "PNG Files|*.png");
        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("[SaveHandlerUI] Save cancelled.");
            CleanupIfTemporary(tex);
            return;
        }

        try
        {
            var pngData = tex.EncodeToPNG();
            if (pngData == null || pngData.Length == 0)
            {
                Debug.LogError("[SaveHandlerUI] Failed to encode PNG.");
                return;
            }

            await File.WriteAllBytesAsync(path, pngData);
            Debug.Log($"[SaveHandlerUI] Saved to: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveHandlerUI] Save error: {ex.Message}");
        }
        finally
        {
            CleanupIfTemporary(tex);
        }
    }

    public async void LoadTextureFromFileAsync()
    {
        string path = ShowOpenFileDialog("Image Files|*.png;*.jpg;*.jpeg");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.Log("[SaveHandlerUI] Load cancelled or file not found.");
            return;
        }

        try
        {
            byte[] fileData = await File.ReadAllBytesAsync(path);
            ApplyLoadedBytes(fileData, path);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveHandlerUI] Load error: {ex.Message}");
        }
    }

    // -------------------------
    // CORE LOGIC
    // -------------------------

    private Texture2D GetTextureToSave()
    {
        // If forceRenderToTexture is enabled, we try to render the UI element to a new Texture2D,
        // which avoids “texture is not readable” issues.
        if (forceRenderToTexture)
        {
            var rendered = RenderTargetGraphicToTexture2D();
            if (rendered != null)
                return rendered;
        }

        // Otherwise try direct extraction
        if (targetRawImage != null && targetRawImage.texture != null)
        {
            return CopyToReadableTexture(targetRawImage.texture);
        }

        if (targetImage != null && targetImage.sprite != null && targetImage.sprite.texture != null)
        {
            // Note: sprite may be a sub-rect of a texture atlas. We’ll crop to the sprite rect.
            return CopySpriteToTexture(targetImage.sprite);
        }

        return null;
    }

    private Texture2D RenderTargetGraphicToTexture2D()
    {
        // This path is best if your UI texture/sprite is not readable or is atlased.
        // It renders the UI element into a RenderTexture and reads back.
        Graphic g = null;
        if (targetRawImage != null) g = targetRawImage;
        else if (targetImage != null) g = targetImage;

        if (g == null)
            return null;

        if (uiCamera == null)
        {
            // Try to auto-find a camera that renders this canvas
            var canvas = g.canvas;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = canvas.worldCamera;

            if (uiCamera == null)
                uiCamera = Camera.main;
        }

        if (uiCamera == null)
        {
            Debug.LogWarning("[SaveHandlerUI] forceRenderToTexture is enabled, but uiCamera is null. Falling back to direct copy.");
            return null;
        }

        // Temporarily isolate by rendering full UI camera output.
        // If you need ONLY that element (not whole canvas), tell me and I’ll give you a tight rect crop version.
        var prevRT = uiCamera.targetTexture;
        var prevActive = RenderTexture.active;

        var rt = RenderTexture.GetTemporary(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32);
        uiCamera.targetTexture = rt;
        uiCamera.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
        tex.Apply();

        RenderTexture.active = prevActive;
        uiCamera.targetTexture = prevRT;
        RenderTexture.ReleaseTemporary(rt);

        tex.name = "SaveHandlerUI_Rendered";
        MarkTemporary(tex);
        return tex;
    }

    private Texture2D CopyToReadableTexture(Texture source)
    {
        // If source is already Texture2D and readable, we can just return a copy safely.
        // But many textures aren’t readable, so we use a GPU blit into a RT and ReadPixels.
        var src2D = source as Texture2D;
        if (src2D != null)
        {
            try
            {
                // Attempt read; if not readable this can throw or return garbage depending on platform
                src2D.GetPixel(0, 0);
                // Make a copy so EncodeToPNG is safe even if texture is compressed/atlas etc.
                var copy = new Texture2D(src2D.width, src2D.height, TextureFormat.ARGB32, false);
                copy.SetPixels32(src2D.GetPixels32());
                copy.Apply();
                copy.name = "SaveHandlerUI_Copy";
                MarkTemporary(copy);
                return copy;
            }
            catch
            {
                // fall through to RT method
            }
        }

        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        var tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
        tex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        tex.name = "SaveHandlerUI_BlitCopy";
        MarkTemporary(tex);
        return tex;
    }

    private Texture2D CopySpriteToTexture(Sprite sprite)
    {
        var srcTex = sprite.texture;
        var r = sprite.rect;

        // Easiest reliable path: blit whole texture then crop.
        // (Atlas-safe and readable-safe)
        var full = CopyToReadableTexture(srcTex);

        int x = Mathf.RoundToInt(r.x);
        int y = Mathf.RoundToInt(r.y);
        int w = Mathf.RoundToInt(r.width);
        int h = Mathf.RoundToInt(r.height);

        // Crop
        var pixels = full.GetPixels32();
        var cropped = new Texture2D(w, h, TextureFormat.ARGB32, false);

        // Manual crop from full texture pixels
        var outPixels = new Color32[w * h];
        int fullW = full.width;

        for (int row = 0; row < h; row++)
        {
            int srcRow = (y + row) * fullW;
            int dstRow = row * w;
            for (int col = 0; col < w; col++)
            {
                outPixels[dstRow + col] = pixels[srcRow + (x + col)];
            }
        }

        cropped.SetPixels32(outPixels);
        cropped.Apply();
        cropped.name = "SaveHandlerUI_SpriteCrop";
        MarkTemporary(cropped);

        // full is temporary too
        CleanupIfTemporary(full);

        return cropped;
    }

    private void ApplyLoadedBytes(byte[] fileData, string pathForLog)
    {
        var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        bool ok = tex.LoadImage(fileData);
        if (!ok)
        {
            Debug.LogError("[SaveHandlerUI] Failed to LoadImage().");
            Destroy(tex);
            return;
        }

        Debug.Log($"[SaveHandlerUI] Loaded: {pathForLog} ({tex.width}x{tex.height})");

        if (targetRawImage != null)
        {
            targetRawImage.texture = tex;
            targetRawImage.SetNativeSize();
            return;
        }

        if (targetImage != null)
        {
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            targetImage.sprite = sprite;
            targetImage.SetNativeSize();
            return;
        }

        Debug.LogWarning("[SaveHandlerUI] Loaded texture, but no targetRawImage/targetImage is assigned to apply it.");
        // If nothing assigned, keep tex alive so you can grab it from debugger; otherwise destroy it:
        // Destroy(tex);
    }

    // -------------------------
    // WINDOWS FILE DIALOGS
    // -------------------------

    private string ShowSaveFileDialog(string defaultFilename, string filter)
    {
        using (SaveFileDialog saveFileDialog = new SaveFileDialog())
        {
            saveFileDialog.FileName = defaultFilename;
            saveFileDialog.Filter = filter;
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                return saveFileDialog.FileName;
        }
        return null;
    }

    private string ShowOpenFileDialog(string filter)
    {
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = filter;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            openFileDialog.Title = "Select an image file";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
                return openFileDialog.FileName;
        }
        return null;
    }

    // -------------------------
    // TEMP TEXTURE TRACKING
    // -------------------------

    // We mark textures we create so we can Destroy them after saving.
    private const string TempPrefix = "__TEMP__";

    private void MarkTemporary(Texture2D tex)
    {
        if (tex != null && (tex.name == null || !tex.name.StartsWith(TempPrefix)))
            tex.name = TempPrefix + tex.name;
    }

    private bool IsTemporary(Texture2D tex)
    {
        return tex != null && tex.name != null && tex.name.StartsWith(TempPrefix);
    }

    private void CleanupIfTemporary(Texture2D tex)
    {
        if (IsTemporary(tex))
            Destroy(tex);
    }
}
