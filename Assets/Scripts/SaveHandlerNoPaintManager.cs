using UnityEngine;
using UnityEngine.UI;
using SFB;
using System.IO;
using System.Threading.Tasks;
using XDPaint.Demo;

public class SaveHandlerNoPaintManager : MonoBehaviour
{
    [Header("Refs")]
    //public XDPaint.PaintManager paintManager;
    public MeshRenderer canvasPlaneRenderer;
    public Text folderLabel;

    [Header("Defaults")]
    [SerializeField] string baseName = "LiliaPainting";
    [SerializeField] string ext = "png";

    const string PREF_FOLDER = "saveFolder";

    string folder;

    public GameObject saveNotification;
    //public Animator saveButtonAnimator;
    //public Demo demo;

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
        //var tex = paintManager.GetResultTexture();
        Texture source = canvasPlaneRenderer.material.mainTexture;
        Texture2D tex = ConvertToTexture2D(source);
        var bytes = tex.EncodeToPNG();
        if (bytes == null || bytes.Length == 0) { Debug.LogError("PNG encode failed"); return; }
        await File.WriteAllBytesAsync(path, bytes);
        saveNotification.SetActive(true);
        Invoke("TurnOffNotification", 1f);
        //saveButtonAnimator.Play("ButtonFlicker_1"); 
        Debug.Log("Saved: " + path);
        if (source is RenderTexture) Destroy(tex);
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

                    SceneReferences.Instance.paintManagerCustom.gameObject.SetActive(true);
                    /*Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    demo.SetupLoadedPicture(sprite); // Or whatever you want to do with it*/
                    //canvasPlaneRenderer.material.mainTexture = texture;

                    /*var dst = canvasPlaneRenderer.material.mainTexture as RenderTexture;
                    if (dst != null) Graphics.Blit(texture, dst);
                    else canvasPlaneRenderer.material.mainTexture = texture;

                    FitPlaneToTexture(canvasPlaneRenderer, texture, 1f);*/

                    // Keep the canvas plane aspect fixed; do NOT FitPlaneToTexture for painting.
                    var pm = SceneReferences.Instance.paintManagerCustom;

                    // Make load undoable (optional)
                    pm.SendMessage("PushUndo", SendMessageOptions.DontRequireReceiver);

                    // Use your current canvas background color (or Color.white)
                    pm.BlitImageContained(texture, SceneReferences.Instance.paintManagerCustom.selectedCanvasColor);

                    Debug.Log($"Loaded texture from {path}");
                    SceneReferences.Instance.SelectCanvasScreen.SetActive(false);


                    
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

    void FitPlaneToTexture(Renderer planeRenderer, Texture texture, float targetHeight = 1f)
    {
        if (texture == null) return;

        float texAspect = (float)texture.width / texture.height;

        Transform t = planeRenderer.transform;

        // Plane faces X (width) × Y (height)
        t.localScale = new Vector3(
            targetHeight * texAspect,
            targetHeight,
            t.localScale.z
        );
    }

    private Texture2D ConvertToTexture2D(Texture source)
    {
        if (source is Texture2D t2d)
            return t2d;

        if (source is RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            // Read raw pixels from the RT
            var linearTex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            linearTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            linearTex.Apply(false);

            RenderTexture.active = prev;

            // If project is Linear, PNG viewers expect sRGB (gamma) values.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                var pixels = linearTex.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = pixels[i].gamma;   // convert linear -> gamma

                var gammaTex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                gammaTex.SetPixels(pixels);
                gammaTex.Apply(false);

                Destroy(linearTex);
                return gammaTex;
            }

            return linearTex;
        }

        Debug.LogError($"Unsupported texture type on material: {source.GetType().Name}");
        return null;
    }


}
