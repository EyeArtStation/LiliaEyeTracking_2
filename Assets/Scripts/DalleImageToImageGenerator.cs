using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using XDPaint.Demo;

public class DalleImageToImageGenerator : MonoBehaviour
{
    [Header("OpenAI Settings")]
    [Tooltip("Your OpenAI API key")]
    public string apiKey = "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";

    [Tooltip("One of: 256x256, 512x512, or 1024x1024")]
    public string size = "512x512";

    [Header("Inputs")]
    [Tooltip("The source image you want to vary")]
    public Texture2D inputImage;

    [Header("UI")]
    //[Tooltip("RawImage where the variation will be displayed")]
    public RawImage outputImage;
    //public RawImage outputImage;

    public Demo demo;
    public GameObject thinkingText;

    [ContextMenu("Generate Variation")]
    public void GenerateVariation() => StartCoroutine(GenerateVariationCoroutine());

    private IEnumerator GenerateVariationCoroutine()
    {
        inputImage = demo.PaintManager.GetResultTexture();

        if (inputImage == null)
        {
            Debug.LogError("Input image is not set!");
            yield break;
        }

        // 1) Encode to PNG
        byte[] imageBytes = inputImage.EncodeToPNG();
        Debug.Log($"Encoded image bytes: {imageBytes.Length}");
        if (imageBytes.Length == 0)
        {
            Debug.LogError("Failed to encode inputImage—make sure Read/Write is enabled and compression is None.");
            yield break;
        }

        // 2) Build multipart/form-data form
        var form = new WWWForm();
        form.AddBinaryData("image", imageBytes, "image.png", "image/png");
        form.AddField("n", "1");
        form.AddField("size", size);
        // Optionally force GPT Image for variations (may be unsupported):
        // form.AddField("model", "gpt-image-1");

        // 3) Send to /v1/images/variations
        using var req = UnityWebRequest.Post("https://api.openai.com/v1/images/variations", form);
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        Debug.Log("Sending variation request...");
        yield return req.SendWebRequest();

        thinkingText.SetActive(false);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Variation failed ({req.error}):\n{req.downloadHandler.text}");
            yield break;
        }

        // 4) Parse the JSON response
        Debug.Log("RAW JSON response: " + req.downloadHandler.text);
        var wrapper = JsonUtility.FromJson<DalleResponse>(req.downloadHandler.text);
        if (wrapper?.data == null || wrapper.data.Length == 0)
        {
            Debug.LogError("No variation data returned!");
            yield break;
        }

        string url = wrapper.data[0].url;
        Debug.Log("Variation returned URL: " + url);

        // 5) Download & display the variation
        using var dl = UnityWebRequestTexture.GetTexture(url);
        yield return dl.SendWebRequest();

        if (dl.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Variation download failed: " + dl.error);
            yield break;
        }

        Texture2D downloadedTexture = ((DownloadHandlerTexture) dl.downloadHandler).texture;
        demo.ResetBackgroundTexture(downloadedTexture);
        //outputImage.texture = ((DownloadHandlerTexture)dl.downloadHandler).texture;
        //outputImage.SetNativeSize();

        demo.PaintManager.Init();

        Debug.Log("Variation displayed.");
    }

    [Serializable]
    private class DalleResponse
    {
        public DalleImageData[] data;
    }

    [Serializable]
    private class DalleImageData
    {
        public string url;
    }
}


/*using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class DalleImageToImageGenerator : MonoBehaviour
{
    [Header("OpenAI Settings")]
    [TextArea, Tooltip("How to transform your image")]
    public string prompt =
        "Interpret the attached abstract painting as a cohesive landscape. " +
        "Preserve the exact layout, proportions, and color palette; map large " +
        "color fields to sky, hills, water, or foreground shapes; apply a realistic, painterly style.";

    [Tooltip("256x256, 512x512, or 1024x1024")]
    public string size = "512x512";

    [Tooltip("Your OpenAI API key")]
    public string apiKey = "sk-your_key_here";

    [Header("Inputs")]
    [Tooltip("The image you want to edit")]
    public Texture2D inputImage;

    [Header("UI")]
    [Tooltip("Where the result will appear")]
    public RawImage outputImage;

    [ContextMenu("Generate Prompted Edit")]
    public void GenerateEdit() => StartCoroutine(GenerateEditCoroutine());

    private IEnumerator GenerateEditCoroutine()
    {
        // 1) encode input
        byte[] imageBytes = inputImage.EncodeToPNG();
        if (imageBytes.Length == 0)
        {
            Debug.LogError("EncodeToPNG failed—check Read/Write & compression.");
            yield break;
        }

        // 2) build full-transparent mask
        int w = inputImage.width, h = inputImage.height;
        var mask = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
        mask.SetPixels(pixels);
        mask.Apply();
        byte[] maskBytes = mask.EncodeToPNG();

        // 3) build form
        var form = new WWWForm();
        form.AddBinaryData("image", imageBytes, "image.png", "image/png");
        form.AddBinaryData("mask", maskBytes, "mask.png", "image/png");
        form.AddField("prompt", prompt);
        form.AddField("n", "1");
        form.AddField("size", size);
        form.AddField("model", "gpt-image-1");     // force GPT Image
        // no response_format field

        // 4) send edit request
        using var req = UnityWebRequest.Post("https://api.openai.com/v1/images/edits", form);
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        Debug.Log("Sending edit request...");
        yield return req.SendWebRequest();

        // always log raw JSON so you can inspect
        string json = req.downloadHandler.text;
        Debug.Log("RAW JSON response:\n" + json);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Edit failed ({req.error}):\n{json}");
            yield break;
        }

        // 5) parse and handle both possible formats
        var wrapper = JsonUtility.FromJson<DalleResponse>(json);
        if (wrapper?.data == null || wrapper.data.Length == 0)
        {
            Debug.LogError("No data returned!\n" + json);
            yield break;
        }

        var item = wrapper.data[0];
        if (!string.IsNullOrEmpty(item.url))
        {
            yield return DownloadAndDisplay(item.url);
        }
        else if (!string.IsNullOrEmpty(item.b64_json))
        {
            byte[] bytes = Convert.FromBase64String(item.b64_json);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            outputImage.texture = tex;
            outputImage.SetNativeSize();
            Debug.Log("Displayed image from base64 payload");
        }
        else
        {
            Debug.LogError("Neither url nor b64_json was present:\n" + json);
        }
    }

    private IEnumerator DownloadAndDisplay(string url)
    {
        using var dl = UnityWebRequestTexture.GetTexture(url);
        yield return dl.SendWebRequest();
        if (dl.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Download failed: " + dl.error);
            yield break;
        }
        outputImage.texture = ((DownloadHandlerTexture)dl.downloadHandler).texture;
        outputImage.SetNativeSize();
        Debug.Log("Displayed image from URL");
    }

    [Serializable]
    private class DalleResponse
    {
        public DalleImageData[] data;
    }

    [Serializable]
    private class DalleImageData
    {
        public string url;
        public string b64_json;
    }
}*/


/*using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class DalleImageToImageGenerator : MonoBehaviour
{
    [Header("OpenAI Settings")]
    [TextArea]
    [Tooltip("What do you want DALL·E to do to your image?")]
    public string prompt = "turn my sketch into a watercolor painting";

    [Tooltip("256x256, 512x512, or 1024x1024")]
    public string size = "512x512";

    [Tooltip("Your OpenAI secret key")]
    public string apiKey = "sk-your_actual_key_here";

    [Header("Inputs")]
    [Tooltip("The image you want to edit")]
    public Texture2D inputImage;

    [Header("UI")]
    [Tooltip("Where the edited image will appear")]
    public RawImage outputImage;

    [ContextMenu("Generate Edited Image")]
    public void GenerateEdit() => StartCoroutine(GenerateEditCoroutine());

    private IEnumerator GenerateEditCoroutine()
    {
        // 1) Encode your image
        byte[] imageBytes = inputImage.EncodeToPNG();
        if (imageBytes.Length == 0)
        {
            Debug.LogError("Image encoding failed! Enable Read/Write and disable compression.");
            yield break;
        }

        // 2) Build form-data (transparent full-mask)
        var form = new WWWForm();
        form.AddBinaryData("image", imageBytes, "image.png", "image/png");

        // transparent mask so entire image is editable
        int w = inputImage.width, h = inputImage.height;
        var mask = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
        mask.SetPixels(pixels);
        mask.Apply();
        form.AddBinaryData("mask", mask.EncodeToPNG(), "mask.png", "image/png");

        form.AddField("prompt", prompt);
        form.AddField("n", "1");
        form.AddField("size", size);

        // ? tell the edits endpoint to use the new GPT Image model
        form.AddField("model", "gpt-image-1");

        // 3) Send to edits endpoint
        using var req = UnityWebRequest.Post("https://api.openai.com/v1/images/edits", form);
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Edit failed ({req.error}):\n{req.downloadHandler.text}");
            yield break;
        }

        // 4) Log raw JSON for debugging
        string json = req.downloadHandler.text;
        Debug.Log("RAW JSON response: " + json);

        // 5) Parse both possible fields
        var wrapper = JsonUtility.FromJson<DalleResponse>(json);
        if (wrapper == null || wrapper.data == null || wrapper.data.Length == 0)
        {
            Debug.LogError("No data in response!");
            yield break;
        }

        var item = wrapper.data[0];
        if (!string.IsNullOrEmpty(item.url))
        {
            // 6a) If we got a URL, download it
            StartCoroutine(DownloadImage(item.url));
        }
        else if (!string.IsNullOrEmpty(item.b64_json))
        {
            // 6b) Otherwise decode base64 directly
            byte[] bytes = Convert.FromBase64String(item.b64_json);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            outputImage.texture = tex;
            outputImage.SetNativeSize();
            Debug.Log("Edited image (b64) displayed");
        }
        else
        {
            Debug.LogError("Neither url nor b64_json present in response!");
        }
    }

    private IEnumerator DownloadImage(string url)
    {
        using var r = UnityWebRequestTexture.GetTexture(url);
        yield return r.SendWebRequest();

        if (r.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image download failed: " + r.error);
        }
        else
        {
            outputImage.texture = ((DownloadHandlerTexture)r.downloadHandler).texture;
            outputImage.SetNativeSize();
            Debug.Log("Edited image (URL) displayed");
        }
    }

    [Serializable]
    private class DalleResponse
    {
        public DalleImageData[] data;
    }

    [Serializable]
    private class DalleImageData
    {
        public string url;
        public string b64_json;
    }
}
*/

/*using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class DalleImageToImageGenerator : MonoBehaviour
{
    [Header("OpenAI Settings")]
    [TextArea]
    [Tooltip("Act as a Photoshop watercolor filter. Preserve all original lines, shading, and composition. Apply only a gentle watercolor wash—light color washes, minimal alteration to shapes or details, low saturation. Do not re-draw or re-compose; just overlay a watercolor texture.")]
    public string prompt = "Please act as a Photoshop watercolor filter. Preserve all original lines, shading, and composition. Apply only a gentle watercolor wash—light color washes, minimal alteration to shapes or details, low saturation. Do not re-draw or re-compose; just overlay a watercolor texture.";

    [Tooltip("256x256, 512x512, or 1024x1024")]
    public string size = "512x512";

    [Tooltip("Your OpenAI secret key")]
    public string apiKey = "sk-your_actual_key_here";

    [Range(0f, 1f)]
    [Tooltip("0 = original, 1 = full AI result")]
    public float blendAmount = 0.3f;

    [Header("Inputs")]
    [Tooltip("The image you want to edit")]
    public Texture2D inputImage;

    [Header("UI")]
    [Tooltip("Where the final image will be shown")]
    public RawImage outputImage;

    [ContextMenu("Apply Watercolor Filter")]
    public void ApplyFilter() => StartCoroutine(ApplyWatercolorFilterCoroutine());

    private IEnumerator ApplyWatercolorFilterCoroutine()
    {
        // 1) Encode input to PNG
        byte[] imageBytes = inputImage.EncodeToPNG();
        Debug.Log($"Image bytes: {imageBytes.Length}");
        if (imageBytes.Length == 0)
        {
            Debug.LogError("Image encoding failed! Enable Read/Write and disable compression.");
            yield break;
        }

        // 2) Make a fully-transparent mask
        int w = inputImage.width, h = inputImage.height;
        var transparentMask = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var maskPixels = new Color[w * h];
        for (int i = 0; i < maskPixels.Length; i++)
            maskPixels[i] = Color.clear; // RGBA(0,0,0,0)
        transparentMask.SetPixels(maskPixels);
        transparentMask.Apply();

        byte[] maskBytes = transparentMask.EncodeToPNG();
        Debug.Log($"Mask bytes:   {maskBytes.Length}");
        if (maskBytes.Length == 0)
        {
            Debug.LogError("Mask encoding failed!");
            yield break;
        }

        // 3) Build form-data
        var form = new WWWForm();
        form.AddBinaryData("image", imageBytes, "image.png", "image/png");
        form.AddBinaryData("mask", maskBytes, "mask.png", "image/png");
        form.AddField("prompt", prompt);
        form.AddField("n", "1");
        form.AddField("size", size);

        // ? Add this line to use the GPT Image model for finer edits:
        form.AddField("model", "gpt-image-1");
        form.AddField("response_format", "url");

        // 4) Send to edits endpoint
        using var req = UnityWebRequest.Post("https://api.openai.com/v1/images/edits", form);
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        Debug.Log("Sending edit request...");
        yield return req.SendWebRequest();
        Debug.Log("RAW JSON response: " + req.downloadHandler.text);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Edit failed ({req.error}):\n{req.downloadHandler.text}");
            yield break;
        }

        // 5) Parse response URL
        var wrapper = JsonUtility.FromJson<DalleResponse>(req.downloadHandler.text);
        if (wrapper == null || wrapper.data == null || wrapper.data.Length == 0)
        {
            Debug.LogError("No image URL in response! RAW JSON:\n" + req.downloadHandler.text);
            yield break;
        }

        string url = wrapper.data[0].url;
        Debug.Log("Edit returned URL: " + url);

        // 6) Download & blend
        yield return DownloadAndBlendImage(url);
    }

    private IEnumerator DownloadAndBlendImage(string url)
    {
        using var r = UnityWebRequestTexture.GetTexture(url);
        yield return r.SendWebRequest();
        if (r.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image download failed: " + r.error);
            yield break;
        }

        Texture2D aiResult = ((DownloadHandlerTexture)r.downloadHandler).texture;
        Texture2D finalTex = BlendTextures(inputImage, aiResult, blendAmount);
        outputImage.texture = finalTex;
        outputImage.SetNativeSize();
        Debug.Log($"Watercolor filter applied (blend {blendAmount:P0}).");
    }

    private Texture2D BlendTextures(Texture2D original, Texture2D stylized, float t)
    {
        int w = original.width, h = original.height;
        var blended = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] orig = original.GetPixels();
        Color[] style = stylized.GetPixels();
        Color[] result = new Color[w * h];
        for (int i = 0; i < result.Length; i++)
            result[i] = Color.Lerp(orig[i], style[i], t);
        blended.SetPixels(result);
        blended.Apply();
        return blended;
    }

    [Serializable]
    private class DalleResponse { public DalleImageData[] data; }
    [Serializable]
    private class DalleImageData { public string url; }
}
*/

/*using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class DalleImageToImageGenerator : MonoBehaviour
{
    [Header("OpenAI Settings")]
    [TextArea]
    [Tooltip("What do you want DALL·E to do to your image?")]
    public string prompt = "turn my sketch into a watercolor painting";

    [Tooltip("256x256, 512x512, or 1024x1024")]
    public string size = "512x512";

    [Tooltip("Your OpenAI secret key")]
    public string apiKey = "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";

    [Header("Inputs")]
    [Tooltip("The image you want to edit")]
    public Texture2D inputImage;

    [Header("UI")]
    [Tooltip("Where the edited image will appear")]
    public RawImage outputImage;

    [ContextMenu("Generate Edited Image")]
    public void GenerateEdit() => StartCoroutine(GenerateEditCoroutine());

    private IEnumerator GenerateEditCoroutine()
    {
        // 1) Encode your image to PNG
        byte[] imageBytes = inputImage.EncodeToPNG();
        Debug.Log($"Image bytes: {imageBytes.Length}");
        if (imageBytes.Length == 0)
        {
            Debug.LogError("Image encoding failed! Make sure Read/Write is enabled and compression is None.");
            yield break;
        }

        // 2) Generate a fully-transparent mask (alpha=0 everywhere)
        int w = inputImage.width, h = inputImage.height;
        var transparentMask = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var maskPixels = new Color[w * h];
        for (int i = 0; i < maskPixels.Length; i++)
            maskPixels[i] = Color.clear; // RGBA(0,0,0,0)
        transparentMask.SetPixels(maskPixels);
        transparentMask.Apply();

        byte[] maskBytes = transparentMask.EncodeToPNG();
        Debug.Log($"Mask bytes:   {maskBytes.Length}");
        if (maskBytes.Length == 0)
        {
            Debug.LogError("Mask encoding failed!");
            yield break;
        }

        // 3) Build multipart/form-data form
        var form = new WWWForm();
        form.AddBinaryData("image", imageBytes, "image.png", "image/png");
        form.AddBinaryData("mask", maskBytes, "mask.png", "image/png");
        form.AddField("prompt", prompt);
        form.AddField("n", "1");
        form.AddField("size", size);

        // 4) Send to /v1/images/edits
        using var req = UnityWebRequest.Post("https://api.openai.com/v1/images/edits", form);
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        Debug.Log("Sending edit request...");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Edit failed ({req.error}):\n{req.downloadHandler.text}");
            yield break;
        }

        // 5) Parse response and log URL
        var wrapper = JsonUtility.FromJson<DalleResponse>(req.downloadHandler.text);
        string url = wrapper.data[0].url;
        Debug.Log("Edit returned URL: " + url);

        // 6) Download & display the edited image
        StartCoroutine(DownloadImage(url));
    }

    private IEnumerator DownloadImage(string url)
    {
        using var r = UnityWebRequestTexture.GetTexture(url);
        yield return r.SendWebRequest();
        if (r.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image download failed: " + r.error);
        }
        else
        {
            outputImage.texture = ((DownloadHandlerTexture)r.downloadHandler).texture;
            outputImage.SetNativeSize();
            Debug.Log("Edited image displayed");
        }
    }

    [Serializable]
    private class DalleResponse { public DalleImageData[] data; }
    [Serializable]
    private class DalleImageData { public string url; }
}
*/


/*using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class DalleImageToImageGenerator : MonoBehaviour
{
    [Header("OpenAI Settings")]
    [TextArea] public string prompt = "turn my sketch into a watercolor painting";
    public string size = "512x512";
    public string apiKey = "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";

    [Header("Inputs")]
    public Texture2D inputImage;    // assign in Inspector
    public Texture2D maskImage;     // optional: white=visible/edit, black=keep original

    [Header("UI")]
    public RawImage outputImage;

    [ContextMenu("Generate Edited Image")]
    public void GenerateEdit() => StartCoroutine(GenerateEditCoroutine());

    private IEnumerator GenerateEditCoroutine()
    {
        // 1) Convert textures to PNG bytes
        byte[] imageBytes = inputImage.EncodeToPNG();
        byte[] maskBytes = maskImage != null ? maskImage.EncodeToPNG() : null;

        // 2) Build multipart form
        var form = new List<IMultipartFormSection>();
        form.Add(new MultipartFormFileSection("image", imageBytes, "image.png", "image/png"));
        if (maskBytes != null)
            form.Add(new MultipartFormFileSection("mask", maskBytes, "mask.png", "image/png"));
        form.Add(new MultipartFormDataSection("prompt", prompt));
        form.Add(new MultipartFormDataSection("n", "1"));
        form.Add(new MultipartFormDataSection("size", size));

        // 3) Choose your endpoint:
        //    For editing (with prompt + mask): /v1/images/edits
        //    For pure variations (no prompt, no mask):   /v1/images/variations
        string url = "https://api.openai.com/v1/images/edits";

        // 4) Send the request
        UnityWebRequest req = UnityWebRequest.Post(url, form);
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Edit failed: {req.error}\n{req.downloadHandler.text}");
            yield break;
        }

        // 5) Parse response and download the generated image
        var wrapper = JsonUtility.FromJson<DalleResponse>(req.downloadHandler.text);
        string newUrl = wrapper.data[0].url;
        Debug.Log("edit endpoint returned URL: " + newUrl);
        StartCoroutine(DownloadImage(wrapper.data[0].url));
    }

    private IEnumerator DownloadImage(string url)
    {
        UnityWebRequest r = UnityWebRequestTexture.GetTexture(url);
        yield return r.SendWebRequest();
        if (r.result != UnityWebRequest.Result.Success)
            Debug.LogError("Image download failed: " + r.error);
        else
        {
            outputImage.texture = ((DownloadHandlerTexture)r.downloadHandler).texture;
            outputImage.SetNativeSize();
            Debug.Log("Image downloaded");
        }
    }

    [Serializable]
    private class DalleResponse { public DalleImageData[] data; }
    [Serializable]
    private class DalleImageData { public string url; }
}
*/