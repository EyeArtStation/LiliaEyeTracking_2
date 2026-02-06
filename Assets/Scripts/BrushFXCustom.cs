using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BrushFXCustom : MonoBehaviour
{
    public PaintManagerCustom paintManagerCustom;

    private float rainbowTimer = 0f;

    public float rainbowSeconds;

    public bool rainbowBool;
    public bool rotateStroke;

    private float mediumBrushSize = 0.01f;

    public Texture2D[] brushTextures;

    [Header("Tex Cycle Brushes")]
    public Texture2D[] texCycleBrush_1;

    public bool cycleBrushTexture;
    public int selectedCycleBrush;

    public float rotationAmount;

    public bool randomizeOpacity;
    private int curremtBrushTexture;

    public void RainbowMode()
    {
        rainbowTimer += Time.unscaledDeltaTime;

        if (rainbowTimer >= rainbowSeconds)
        {
            rainbowTimer = 0f;
            Color.RGBToHSV(paintManagerCustom.painter.color, out float h, out float s, out float v);

            h = Mathf.Repeat(h + 5f / 360f, 1f);

            paintManagerCustom.painter.color = Color.HSVToRGB(h, s, v);
        }
    }

    private void Update()
    {
        if (rainbowBool)
        {
            RainbowMode();
        }

        if(rotateStroke)
        {
            paintManagerCustom.painter.rotationAmount += rotationAmount;
        }

        if (cycleBrushTexture)
        {
            CycleBushTex();
        }

        if (randomizeOpacity)
        {
            RandomizeOpacity();
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            ChangeBrushMode(4);
            mediumBrushSize = 0.05f;
            ChangeBrushSize(1);
        }

        /*if (Input.GetKeyDown(KeyCode.I))
        {
            paintManagerCustom.painter.smudgeStrength -= 0.1f;
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            paintManagerCustom.painter.smudgeStrength += 0.1f;
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            paintManagerCustom.painter.smudgePull -= 0.1f;
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            paintManagerCustom.painter.smudgePull += 0.1f;
        }*/
        if (Input.GetKeyDown(KeyCode.Space))
        {
            paintManagerCustom.painter.SetBrushTexture(brushTextures[curremtBrushTexture]);
            curremtBrushTexture++;
        }
    }

    public void CycleBushTex()
    {
        switch (selectedCycleBrush)
        {
            case 0:
                int randomTex = Random.Range(0, texCycleBrush_1.Length);
                paintManagerCustom.painter.SetBrushTexture(texCycleBrush_1[randomTex]);
                break;
        }
    }

    public void RandomizeOpacity()
    {
        Color currentColor = paintManagerCustom.painter.color;
        currentColor.a = Random.Range(0, 0.7f);
        paintManagerCustom.painter.SetBrushColor(currentColor);
    }

    public void ToggleRainbowBool()
    {
        rainbowBool = !rainbowBool;
        Image rainbowImage = SceneReferences.Instance.rainbowButton.GetComponent<Image>();
        if (rainbowBool)
        {
            rainbowImage.color = Color.HSVToRGB(0, 0, 1);
        }
        else
        {
            rainbowImage.color = Color.HSVToRGB(0, 0, 0.5f);
        }
        Debug.Log("Rainbow Button Pushed");
    }

    public void SetOpacity(float opacity)
    {
        Color currentColor = paintManagerCustom.painter.color;
        currentColor.a = opacity;
        paintManagerCustom.painter.SetBrushColor(currentColor);
    }

    public void ChangeBrushSize(int sizeMode)
    {
        switch (sizeMode)
        {
            case 0:
                paintManagerCustom.SetBrushSize(mediumBrushSize / 2);
                break;
            case 1:
                paintManagerCustom.SetBrushSize(mediumBrushSize);
                break;
            case 2:
                paintManagerCustom.SetBrushSize(mediumBrushSize * 2);
                break;
        }
    }

    public void ChangeBrushMode(int modeNum)
    {
        switch (modeNum)
        {
            case 0:
                paintManagerCustom.painter.SetMode(BasePaintCustom.PaintMode.VelocityLineWidth);
                break;
            case 1:
                paintManagerCustom.painter.SetMode(BasePaintCustom.PaintMode.InterpolatedLine);
                break;
            case 2:
                paintManagerCustom.painter.SetMode(BasePaintCustom.PaintMode.StampDistance);
                break;
            case 3:
                paintManagerCustom.painter.SetMode(BasePaintCustom.PaintMode.StampInterval);
                break;
            case 4:
                paintManagerCustom.painter.SetMode(BasePaintCustom.PaintMode.Smudge);
                break;
            case 5:
                paintManagerCustom.painter.SetMode(BasePaintCustom.PaintMode.WetBleed);
                break;
            case 6:
                paintManagerCustom.painter.SetMode(BasePaintCustom.PaintMode.CloudConnect);
                break;
        }
    }



    //Brush Selection
    //////////////////

    public void Brush_Action()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[0]); //Circle Texture
        rotateStroke = false;
        paintManagerCustom.painter.rotationAmount = 0f;
        rotationAmount = 0;
        paintManagerCustom.painter.randomRotation = false;
        mediumBrushSize = 0.01f;
        ChangeBrushSize(1);
        ChangeBrushMode(0); //Velocity Line
        cycleBrushTexture = false;
        randomizeOpacity = false;
        paintManagerCustom.painter.overlapInterval = 0.20f;
        paintManagerCustom.painter.strokeSmoothness = 0.35f;
        SetOpacity(1f);
    }

    public void Brush_Line()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[0]); //Circle Texture
        rotateStroke = false;
        paintManagerCustom.painter.rotationAmount = 0f;
        rotationAmount = 0;
        paintManagerCustom.painter.randomRotation = false;
        mediumBrushSize = 0.01f;
        ChangeBrushSize(1);
        ChangeBrushMode(1); //Fixed Width Line
        cycleBrushTexture = false;
        randomizeOpacity = false;
        paintManagerCustom.SetStampInterval(0.0001f);
        paintManagerCustom.painter.overlapInterval = 0.20f;
        paintManagerCustom.painter.strokeSmoothness = 0.35f;
        SetOpacity(1f);
    }

    public void Brush_Leaf()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[7]);
        rotateStroke = true;
        rotationAmount = 10;
        paintManagerCustom.painter.randomRotation = false;
        mediumBrushSize = 0.01f;
        ChangeBrushSize(1);
        ChangeBrushMode(2); //Stamp Distance
        cycleBrushTexture = false;
        randomizeOpacity = false;
        paintManagerCustom.painter.overlapInterval = 0.20f;
        paintManagerCustom.painter.strokeSmoothness = 0.35f;
        SetOpacity(1f);
    }

    public void Brush_SpinLine()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[47]);
        rotateStroke = true;
        rotationAmount = 0.5f;
        paintManagerCustom.painter.randomRotation = false;
        mediumBrushSize = 0.05f;
        ChangeBrushSize(1);
        ChangeBrushMode(2); //Stamp Distance
        cycleBrushTexture = false;
        randomizeOpacity = false;
        paintManagerCustom.painter.overlapInterval = 0.20f;
        paintManagerCustom.painter.strokeSmoothness = 0.35f;
        SetOpacity(1f);
    }

    public void Brush_Bubble()
    {
        //The brush texture below doesn't matter because the cyclebrush is set true in update
        paintManagerCustom.SetBrushTexture(brushTextures[47]);
        rotateStroke = true;
        rotationAmount = 1;
        paintManagerCustom.painter.randomRotation = true;
        mediumBrushSize = 0.05f;
        
        ChangeBrushSize(1);
        ChangeBrushMode(3); //Stamp Interval
        cycleBrushTexture = true;
        randomizeOpacity = true;
        paintManagerCustom.SetStampInterval(0.0005f);
        paintManagerCustom.painter.overlapInterval = 0.08f;
        paintManagerCustom.painter.strokeSmoothness = 0.18f;
        SetOpacity(1f);
    }

    public void Brush_Smudge()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[0]);
        rotateStroke = false;
        rotationAmount = 1;
        paintManagerCustom.painter.randomRotation = false;
        mediumBrushSize = 0.05f;
        ChangeBrushSize(1);
        ChangeBrushMode(4);
        cycleBrushTexture = false;
        randomizeOpacity = false;
        paintManagerCustom.SetStampInterval(0.0005f);
        paintManagerCustom.painter.overlapInterval = 0.20f;
        paintManagerCustom.painter.strokeSmoothness = 0.35f;
        SetOpacity(1f);
    }

    public void Brush_Bleed()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[48]);
        rotateStroke = true;
        rotationAmount = 0;
        paintManagerCustom.painter.randomRotation = true;
        mediumBrushSize = 0.010f;
        ChangeBrushSize(1);
        ChangeBrushMode(5);
        cycleBrushTexture = false;
        randomizeOpacity = false;
        paintManagerCustom.SetStampInterval(0.0001f);
        SetOpacity(1f);
        paintManagerCustom.painter.overlapInterval = 0.08f;
        paintManagerCustom.painter.strokeSmoothness = 0.18f;

        
}

    public void Brush_CloudConnect()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[0]); //Circle Texture
        rotateStroke = false;
        paintManagerCustom.painter.rotationAmount = 0f;
        rotationAmount = 0;
        paintManagerCustom.painter.randomRotation = false;
        mediumBrushSize = 0.01f;
        ChangeBrushSize(1);
        ChangeBrushMode(6); //Fixed Width Line
        cycleBrushTexture = false;
        randomizeOpacity = false;
        paintManagerCustom.painter.overlapInterval = 0.20f;
        paintManagerCustom.painter.strokeSmoothness = 0.35f;
        SetOpacity(1f);
    }
}
