using UnityEngine;
using UnityEngine.UI;
using System.IO;
#if UNITY_STANDALONE_WIN
using SFB;
#endif

public class SaveTextureAsPNG : MonoBehaviour
{
    //public DrawScript drawScript;
    [SerializeField] public Texture2D _textureToSave;

    [SerializeField] public RenderTexture _renderTextureToSave;

    private void Start()
    {
        
    }

    public void SaveTexture(Image buttonImage)
    {
        //_textureToSave = drawScript._renderTexture;
        _textureToSave = (Texture2D)buttonImage.material.mainTexture;

        if (_textureToSave == null)
        {
            Debug.LogError("No texture assigned to save.");
            return;
        }

#if UNITY_STANDALONE_WIN
        // Use StandaloneFileBrowser for standalone builds
        string filePath = StandaloneFileBrowser.SaveFilePanel("Save Texture as PNG", "", "SavedImage", "png");
#else
        // Use a default path in case of other platforms
        string filePath = Path.Combine(Application.persistentDataPath, "SavedImage.png");
        Debug.LogWarning("File dialog not supported. Saving to " + filePath);
#endif

        if (!string.IsNullOrEmpty(filePath))
        {
            // Encode texture to PNG
            byte[] bytes = _textureToSave.EncodeToPNG();

            // Write PNG to the selected file path
            try
            {
                File.WriteAllBytes(filePath, bytes);
                Debug.Log("Texture saved to " + filePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to save texture: " + ex.Message);
            }
        }
    }

    public void StoreRenderTexture()
    {
        if (_renderTextureToSave == null)
        {
            Debug.LogError("No RenderTexture assigned to save.");
            return;
        }

        // Create a new Texture2D with the same dimensions as the RenderTexture
        Texture2D texture = new Texture2D(_renderTextureToSave.width, _renderTextureToSave.height, TextureFormat.RGBA32, false);

        // Set the active RenderTexture and read pixels into the Texture2D
        RenderTexture.active = _renderTextureToSave;
        texture.ReadPixels(new Rect(0, 0, _renderTextureToSave.width, _renderTextureToSave.height), 0, 0);
        texture.Apply();
        RenderTexture.active = null; // Release the active RenderTexture

        // Encode the texture to PNG
        byte[] bytes = texture.EncodeToPNG();

        // Specify file path (modify as needed for your environment)
        string filePath = Path.Combine(Application.persistentDataPath, "SavedImage.png");

        try
        {
            File.WriteAllBytes(filePath, bytes);
            Debug.Log("Texture saved to " + filePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to save texture: " + ex.Message);
        }

        // Clean up the Texture2D to free memory
        Destroy(texture);
    }
}
