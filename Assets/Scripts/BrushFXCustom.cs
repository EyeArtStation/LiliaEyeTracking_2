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
        mediumBrushSize = 0.01f;
        ChangeBrushSize(1);
        ChangeBrushMode(0); //Velocity Line
        cycleBrushTexture = false;
        randomizeOpacity = false;
    }

    public void Brush_Line()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[0]); //Circle Texture
        rotateStroke = false;
        paintManagerCustom.painter.rotationAmount = 0f;
        rotationAmount = 0;
        mediumBrushSize = 0.01f;
        ChangeBrushSize(1);
        ChangeBrushMode(1); //Fixed Width Line
        cycleBrushTexture = false;
        randomizeOpacity = false;
    }

    public void Brush_Leaf()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[7]);
        rotateStroke = true;
        rotationAmount = 10;
        mediumBrushSize = 0.01f;
        ChangeBrushSize(1);
        ChangeBrushMode(2); //Stamp Distance
        cycleBrushTexture = false;
        randomizeOpacity = false;
    }

    public void Brush_SpinLine()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[47]);
        rotateStroke = true;
        rotationAmount = 0.5f;
        mediumBrushSize = 0.05f;
        ChangeBrushSize(1);
        ChangeBrushMode(2); //Stamp Distance
        cycleBrushTexture = false;
        randomizeOpacity = false;

    }

    public void Brush_SprayCycle()
    {
        paintManagerCustom.SetBrushTexture(brushTextures[47]);
        rotateStroke = true;
        rotationAmount = 1;
        mediumBrushSize = 0.05f;
        ChangeBrushSize(1);
        ChangeBrushMode(3); //Stamp Interval
        cycleBrushTexture = true;
        randomizeOpacity = true;
        paintManagerCustom.SetStampInterval(0.0005f);
    }
}
