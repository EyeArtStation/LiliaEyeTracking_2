using UnityEngine;
using UnityEngine.UI;
using SFB;
using System.IO;
using System.Threading.Tasks;
using XDPaint.Demo;

public class SaveLoadHandler : MonoBehaviour
{
    [Header("Refs")]
    public XDPaint.PaintManager paintManager;
    public Text folderLabel;

    [Header("Defaults")]
    [SerializeField] string baseName = "LiliaPainting";
    [SerializeField] string ext = "png";

    const string PREF_FOLDER = "saveFolder";

    string folder;

    public GameObject saveNotification;
    public Animator saveButtonAnimator;
    public Demo demo;

    void Start()
    {
        folder = PlayerPrefs.GetString(PREF_FOLDER, "");
        if (string.IsNullOrWhiteSpace(folder)) folder = Application.persistentDataPath;
        EnsureDir(folder);
        if (folderLabel) folderLabel.text = folder;
    }

    public void PickSaveFolder()
    {
        var pick = StandaloneFileBrowser.OpenFolderPanel("Select save folder", folder, false);
        if (pick != null && pick.Length > 0 && !string.IsNullOrWhiteSpace(pick[0]))
        {
            folder = pick[0];
            EnsureDir(folder);
            PlayerPrefs.SetString(PREF_FOLDER, folder);
            PlayerPrefs.Save();
            if (folderLabel) folderLabel.text = folder;
        }
    }

    public async void SaveTextureAsync() => await SaveNow();

    async Task SaveNow()
    {
        EnsureDir(folder);
        string name = GetNextName(folder, Sanitize(baseName), ext);
        string path = Path.Combine(folder, name);
        var tex = paintManager.GetResultTexture();
        var bytes = tex.EncodeToPNG();
        if (bytes == null || bytes.Length == 0) { Debug.LogError("PNG encode failed"); return; }
        await File.WriteAllBytesAsync(path, bytes);
        saveNotification.SetActive(true);
        Invoke("TurnOffNotification", 1f);
        //saveButtonAnimator.Play("ButtonFlicker_1"); 
        Debug.Log("Saved: " + path);
    }

    // --- helpers ---

    static void EnsureDir(string dir) { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); }

    static string GetNextName(string folder, string baseName, string extNoDot)
    {
        // Simple, reliable loop; fast enough for typical counts
        int i = 0;
        string file;
        do { file = $"{baseName}_{i}.{extNoDot}"; i++; }
        while (File.Exists(Path.Combine(folder, file)));
        return file;
    }

    static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), "_");
        return string.IsNullOrWhiteSpace(name) ? "Untitled" : name.Trim();
    }

    public void TurnOffNotification()
    {
        saveNotification.SetActive(false);
        //saveButtonAnimator.Play("GazeButtonAnimation");
    }

    public async void LoadTextureAsync()
    {
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Open Texture", "", new[] { new ExtensionFilter("Image Files", "png", "jpg", "jpeg") }, false);

        if (paths.Length > 0 && File.Exists(paths[0]))
        {
            string path = paths[0];
            try
            {
                byte[] fileData = await File.ReadAllBytesAsync(path);
                Texture2D texture = new Texture2D(2, 2); // Will resize automatically
                bool success = texture.LoadImage(fileData);

                if (success)
                {
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    demo.SetupLoadedPicture(sprite); // Or whatever you want to do with it
                    Debug.Log($"Loaded texture from {path}");
                }
                else
                {
                    Debug.LogError("Failed to load image data into texture.");
                }
            }
            catch (IOException ex)
            {
                Debug.LogError($"Failed to read file: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("Load cancelled.");
        }
    }
}
