using System;
using System.IO;
using System.Windows.Forms; // Requires adding a reference to System.Windows.Forms
using UnityEngine;
using UnityEngine.UI;
using XDPaint;
using XDPaint.Demo;
using System.Threading.Tasks;

public class SaveHandler : MonoBehaviour
{
    private const string DefaultTextureFilename = "Texture.png";

    public PaintManager currentPaintManager;
    public SpriteRenderer spriteRenderer;
    public UnityEngine.UI.Button okayButton;
    public GameObject xdPaintHandler;
    //public GameObject[] startObjects;

    public Demo demo;

    private void Start()
    {
        demo = GetComponent<Demo>();
    }

    public async void DownloadCurrentCanvasAsync()
    {
        await SaveResultTextureToFileAsync(currentPaintManager);
        Debug.Log("Save completed. You may now resume other tasks.");
    }

    private async Task SaveResultTextureToFileAsync(PaintManager paintManager)
    {
        var texture2D = paintManager.GetResultTexture(); // Get texture
        string path = ShowSaveFileDialog(DefaultTextureFilename, "PNG Files|*.png");

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                byte[] pngData = texture2D.EncodeToPNG();
                if (pngData != null)
                {
                    await File.WriteAllBytesAsync(path, pngData); // ✅ async write
                    Debug.Log($"Texture saved to {path}");
                }
                else
                {
                    Debug.LogError("Failed to encode texture to PNG.");
                }
            }
            catch (IOException ex)
            {
                Debug.LogError($"File IO error: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("Save operation was cancelled.");
        }
    }

    public async void LoadTextureFromFileAsync()
    {
        string path = ShowOpenFileDialog("Image Files|*.png;*.jpg;*.jpeg");

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                byte[] fileData = await File.ReadAllBytesAsync(path); // ✅ async read
                Texture2D texture = new Texture2D(2, 2); // Will resize in LoadImage
                bool success = texture.LoadImage(fileData); // Still sync

                if (success)
                {
                    Debug.Log($"Loaded texture from: {path}");
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    demo.SetupLoadedPicture(sprite); // Your handler for applying it
                }
                else
                {
                    Debug.LogError("Failed to load image data into texture.");
                }
            }
            catch (IOException ex)
            {
                Debug.LogError($"IO error while loading texture: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("Load operation was cancelled or file not found.");
        }
    }

    public void DownloadCurrentCanvas(PaintManager paintManager)
    {
        SaveResultTextureToFile(paintManager);
    }

    public void DownloadCurrentCanvas()
    {
        SaveResultTextureToFile(currentPaintManager);
    }

    private void SaveResultTextureToFile(PaintManager paintManager)
    {
        var texture2D = paintManager.GetResultTexture(); // Get the texture to save

        // Open a file dialog to select the save path
        string path = ShowSaveFileDialog(DefaultTextureFilename, "PNG Files|*.png");

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                // Encode the texture to PNG
                byte[] pngData = texture2D.EncodeToPNG();
                if (pngData != null)
                {
                    File.WriteAllBytes(path, pngData); // Save the file to the selected path
                    Debug.Log($"Texture saved successfully to {path}");
                }
                else
                {
                    Debug.LogError("Failed to encode texture to PNG.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while saving the texture: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("Save operation was cancelled.");
        }
    }

    
    private string ShowSaveFileDialog(string defaultFilename, string filter)
    {
        using (SaveFileDialog saveFileDialog = new SaveFileDialog())
        {
            saveFileDialog.FileName = defaultFilename;
            saveFileDialog.Filter = filter;
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                return saveFileDialog.FileName;
            }
        }
        return null; // User canceled the dialog
    }

    [UnityEngine.ContextMenu("Load File Method")]
    // Call this method to prompt the user to load a texture
    public void LoadTextureFromFile()
    {
        string path = ShowOpenFileDialog("Image Files|*.png;*.jpg;*.jpeg");

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2); // Temporary size; will resize on LoadImage
                if (texture.LoadImage(fileData)) // Automatically resizes to correct dimensions
                {
                    Debug.Log($"Loaded texture from: {path}");

                    // Example: apply to a material
                    // GetComponent<Renderer>().material.mainTexture = texture;

                    // Or store the texture for later use
                    // myPaintManager.SetTexture(texture); // or however you use it
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    //spriteRenderer.sprite = sprite;
                    //GetComponent<Demo>().SwitchColorBackground();
                    //okayButton.onClick.Invoke();
                    demo.SetupLoadedPicture(sprite);

                }
                else
                {
                    Debug.LogError("Failed to load image data into texture.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while loading the texture: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("Load operation was cancelled or file not found.");
        }
    }

    private string ShowOpenFileDialog(string filter)
    {
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = filter;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            openFileDialog.Title = "Select an image file";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                return openFileDialog.FileName;
            }
        }
        return null;
    }
}


/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XDPaint;
using System;
using System.IO;
using UnityEditor;
using UnityEngine.UI;
using Component = UnityEngine.Component;
using UnityEditor;

public class SaveHandler : MonoBehaviour
{

    private const string DefaultTextureFilename = "Texture.png";
    private string[] TextureImportPlatforms =
        {
            "Standalone", "Web", "iPhone", "Android", "WebGL", "Windows Store Apps", "PS4", "XboxOne", "Nintendo 3DS", "tvOS"
        };

    public void DownloadCurrentCanvas(PaintManager paintManager)
    {
        SaveResultTextureToFile(paintManager);
    }

    public void SaveResultTextureToFile(PaintManager paintManager)
    {
        var sourceTexture = paintManager.Material.PaintMaterial.mainTexture;
        var texturePath = AssetDatabase.GetAssetPath(sourceTexture);
        if (string.IsNullOrEmpty(texturePath))
        {
            texturePath = Application.dataPath + "/" + DefaultTextureFilename;
        }

        var textureImporterSettings = new TextureImporterSettings();
        var assetImporter = AssetImporter.GetAtPath(texturePath);
        var defaultPlatformSettings = new TextureImporterPlatformSettings();
        var platformsSettings = new Dictionary<string, TextureImporterPlatformSettings>();
        if (assetImporter != null)
        {
            var textureImporter = (TextureImporter)assetImporter;
            textureImporter.ReadTextureSettings(textureImporterSettings);
            defaultPlatformSettings = textureImporter.GetDefaultPlatformTextureSettings();
            foreach (var platform in TextureImportPlatforms)
            {
                var platformSettings = textureImporter.GetPlatformTextureSettings(platform);
                if (platformSettings != null)
                {
                    platformsSettings.Add(platform, platformSettings);
                }
            }
        }

        var directoryInfo = new FileInfo(texturePath).Directory;
        if (directoryInfo != null)
        {
            var directory = directoryInfo.FullName;
            var fileName = Path.GetFileName(texturePath);
            var path = EditorUtility.SaveFilePanel("Save texture as PNG", directory, fileName, "png");
            if (path.Length > 0)
            {
                var texture2D = paintManager.GetResultTexture();
                var pngData = texture2D.EncodeToPNG();
                if (pngData != null)
                {
                    File.WriteAllBytes(path, pngData);
                }

                var importPath = path.Replace(Application.dataPath, "Assets");
                var importer = AssetImporter.GetAtPath(importPath);
                if (importer != null)
                {
                    var texture2DImporter = (TextureImporter)importer;
                    texture2DImporter.SetTextureSettings(textureImporterSettings);
                    texture2DImporter.SetPlatformTextureSettings(defaultPlatformSettings);
                    foreach (var platform in platformsSettings)
                    {
                        texture2DImporter.SetPlatformTextureSettings(platform.Value);
                    }

                    if (!Application.isPlaying)
                    {
                        AssetDatabase.ImportAsset(importPath, ImportAssetOptions.ForceUpdate);
                        AssetDatabase.Refresh();
                    }
                }
            }
        }
    }
}
*/