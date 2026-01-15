using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XDPaint;
using UnityEngine.UI;

public class BrushEffectController : MonoBehaviour
{
    public PaintManager paintManager;
    private GlobalBrushValues globalBrushValues;

    //public Demo demo;
    public Image[] brushes;

    public float newMinPressure = 0.2f;
    public float newMaxPressure = 1.0f;
    public float newMaxSpeed = 2000f;

    private float _minPressure = 0.2f;
    private float _maxPressure = 1.0f;
    private float _maxSpeed = 2000f;

    private PaintManager[] paintManagers;

    public int brushEffectNumber = 0;

    private float smallBrushSize;
    private float mediumBrushSize;
    private float largeBrushSize;

    private Vector2 smallBrushSizeRange;
    private Vector2 mediumBrushSizeRange;
    private Vector2 largeBrushSizeRange;

    private float currentMediumBrushSize = 0.025f;
    private Vector2 currentMediumRandomBrushRange;

    public Texture[] dripDotTextures;

    [Header("Line Brush Sizes")]
    public float lineBrushMediumSize = 0.05f;
    public Vector2 lineBrushMediumRandomRange = new Vector2(0.05f, 0.05f);

    [Header("Action Brush Sizes")]
    public float actionBrushMediumSize = 0.1f;
    public Vector2 actionBrushMediumRandomRange = new Vector2(0.5f, 1f);

    [Header("Spray Brush Sizes")]
    public float sprayBrushMediumSize = 0.05f;
    public Vector2 sprayBrushMediumRandomRange = new Vector2(0.2f, 1f);

    [Header("Bubbles Brush Sizes")]
    public float bubblesBrushMediumSize = 0.05f;
    public Vector2 bubblesBrushMediumRandomRange = new Vector2(0.2f, 1f);

    [Header("Dots Brush Sizes")]
    public float dotsBrushMediumSize = 0.05f;
    public Vector2 dotsBrushMediumRandomRange = new Vector2(0.2f, 1f);

    [Header("Arrow Brush Sizes")]
    public float arrowBrushMediumSize = 0.1f;
    public Vector2 arrowBrushMediumRandomRange = new Vector2(0.2f, 1f);


    // Start is called before the first frame update
    void Start()
    {
        //Initial Brush Selection made by Okay Button on Canvas select screen

        globalBrushValues = GlobalBrushValues.Instance;
    }

    /*public float MinPressure
    {
        get
        {
            return _minPressure;
        }
        set
        {
            // Optional: Add validation or logic
            if (value < 0)
            {
                Debug.LogWarning("Speed cannot be negative. Setting to 0.");
                _minPressure = 0;
            }
            else
            {
                _minPressure = value;
            }

            PaintManager[] paintManagers = FindObjectsOfType<PaintManager>();
            foreach (PaintManager manager in paintManagers)
            {
                manager.PaintObject.minPressure = _minPressure;
            }
        }
    }

    public float MaxPressure
    {
        get
        {
            return _maxPressure;
        }
        set
        {
            // Optional: Add validation or logic
            if (value < 0)
            {
                Debug.LogWarning("Speed cannot be negative. Setting to 0.");
                _maxPressure = 0;
            }
            else
            {
                _maxPressure = value;
            }

            PaintManager[] paintManagers = FindObjectsOfType<PaintManager>();
            foreach (PaintManager manager in paintManagers)
            {
                manager.PaintObject.maxPressure = _maxPressure;
            }
        }
    }

    public float MaxSpeed
    {
        get
        {
            return _maxSpeed;
        }
        set
        {
            // Optional: Add validation or logic
            if (value < 0)
            {
                Debug.LogWarning("Speed cannot be negative. Setting to 0.");
                _maxSpeed = 0;
            }
            else
            {
                _maxSpeed = value;
            }

            PaintManager[] paintManagers = FindObjectsOfType<PaintManager>();
            foreach (PaintManager manager in paintManagers)
            {
                manager.PaintObject.maxSpeed = _maxSpeed;
            }
        }
    }*/

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            ActionBrush();
        }

        /*if (paintManagers != null)
        {
            foreach (PaintManager manager in paintManagers)
            {
                bool trythis;
                trythis = manager.PaintObject.TryRenderPoint(0);
                manager.PaintObject.lineActive = false; 
            }
        }*/


    }

    public void LineBrush()
    {
        //Line
        paintManager.PaintObject.velocityLineWidth = false;
        paintManager.PaintObject.lineActive = true;

        //Single Brush Texture
        Texture brushtexture = brushes[0].mainTexture;
        paintManager.Brush.SetTexture(brushtexture, true, false);
        paintManager.PaintObject.randomBrushAngle = false;
        paintManager.PaintObject.spacingThresholdRange = new Vector2(0.1f, 0.1f);

        //Drip
        //manager.PaintObject.dripImageArray = dripDotTextures;
        //manager.PaintObject.dripImage = brushes[2];
        paintManager.PaintObject.isUsingDrip = false;
        paintManager.PaintObject.useDripImageCycle = false;
        //manager.PaintObject.dripInterval = 0.5f;

        //Size
        paintManager.Brush.Size = lineBrushMediumSize;
        globalBrushValues.brushSize = lineBrushMediumSize;
        currentMediumBrushSize = lineBrushMediumSize;
        paintManager.PaintObject.randomSizeRange = lineBrushMediumRandomRange;
        currentMediumRandomBrushRange = lineBrushMediumRandomRange;

        //Opacity
        paintManager.PaintObject.randomOpacityRange = new Vector2(1f, 1f);
    }

    [ContextMenu("Action Brush")]
    public void ActionBrush()
    {
        /*MinPressure = newMinPressure;
        MaxPressure = newMinPressure;
        MaxSpeed = newMaxSpeed;*/

        //Line
        paintManager.PaintObject.velocityLineWidth = true;
        paintManager.PaintObject.lineActive = true;
            
        //Single Brush Texture
        Texture brushtexture = brushes[0].mainTexture;
        paintManager.Brush.SetTexture(brushtexture, true, false);
        paintManager.PaintObject.randomBrushAngle = true;
        paintManager.PaintObject.spacingThresholdRange = new Vector2(0.7f, 1.2f);

        //Drip
        paintManager.PaintObject.dripImageArray = dripDotTextures;
        paintManager.PaintObject.dripImage = brushes[1];
        paintManager.PaintObject.isUsingDrip = true;
        paintManager.PaintObject.useDripImageCycle = true;
        paintManager.PaintObject.dripInterval = 0.5f;

        //Size
        paintManager.Brush.Size = actionBrushMediumSize;
        globalBrushValues.brushSize = actionBrushMediumSize;
        currentMediumBrushSize = actionBrushMediumSize;
        paintManager.PaintObject.randomSizeRange = actionBrushMediumRandomRange;
        currentMediumRandomBrushRange = actionBrushMediumRandomRange;

        //Opacity
        paintManager.PaintObject.randomOpacityRange = new Vector2(1f, 1f);

    }

    public void SprayBrush1()
    {
        //Line
        paintManager.PaintObject.velocityLineWidth = false;
        paintManager.PaintObject.lineActive = false;

        //Single Brush Texture
        Texture brushtexture = brushes[0].mainTexture;
        paintManager.Brush.SetTexture(brushtexture, true, false);
        paintManager.PaintObject.randomBrushAngle = true;
        paintManager.PaintObject.spacingThresholdRange = new Vector2(0.1f, 0.1f);

        //Drip
        //manager.PaintObject.dripImageArray = dripDotTextures;
        paintManager.PaintObject.dripImage = brushes[2];
        paintManager.PaintObject.isUsingDrip = true;
        paintManager.PaintObject.useDripImageCycle = false;
        paintManager.PaintObject.dripInterval = 0.02f;

        //Size
        paintManager.Brush.Size = sprayBrushMediumSize;
        globalBrushValues.brushSize = sprayBrushMediumSize;
        currentMediumBrushSize = sprayBrushMediumSize;
        paintManager.PaintObject.randomSizeRange = sprayBrushMediumRandomRange;
        currentMediumRandomBrushRange = sprayBrushMediumRandomRange;

        //Opacity
        paintManager.PaintObject.randomOpacityRange = new Vector2(0.5f, 1f);
    }

    //Time Based Dot Brush
    public void BubblesBrush1()
    {
        //Line
        paintManager.PaintObject.velocityLineWidth = false;
        paintManager.PaintObject.lineActive = false;

        //Single Brush Texture
        Texture brushtexture = brushes[0].mainTexture;
        paintManager.Brush.SetTexture(brushtexture, true, false);
        paintManager.PaintObject.randomBrushAngle = true;
        paintManager.PaintObject.spacingThresholdRange = new Vector2(0.1f, 0.1f);

        //Drip
        paintManager.PaintObject.dripImageArray = dripDotTextures;
        paintManager.PaintObject.dripImage = brushes[0];
        paintManager.PaintObject.isUsingDrip = true;
        paintManager.PaintObject.useDripImageCycle = true;
        paintManager.PaintObject.dripInterval = 0.02f;

        //Size
        paintManager.Brush.Size = bubblesBrushMediumSize;
        globalBrushValues.brushSize = bubblesBrushMediumSize;
        currentMediumBrushSize = bubblesBrushMediumSize;
        paintManager.PaintObject.randomSizeRange = bubblesBrushMediumRandomRange;
        currentMediumRandomBrushRange = bubblesBrushMediumRandomRange;

        //Opacity
        paintManager.PaintObject.randomOpacityRange = new Vector2(0.5f, 1f);
        
    }

    public void DotsBrush2()
    {
        //Line
        paintManager.PaintObject.velocityLineWidth = false;
        paintManager.PaintObject.lineActive = false;

        //Single Brush Texture
        Texture brushtexture = brushes[0].mainTexture;
        paintManager.Brush.SetTexture(brushtexture, true, false);
        paintManager.PaintObject.randomBrushAngle = true;
        paintManager.PaintObject.spacingThresholdRange = new Vector2(0.1f, 0.1f);

        //Drip
        //manager.PaintObject.dripImageArray = dripDotTextures;
        paintManager.PaintObject.dripImage = brushes[0];
        paintManager.PaintObject.isUsingDrip = true;
        paintManager.PaintObject.useDripImageCycle = false;
        paintManager.PaintObject.dripInterval = 0.05f;

        //Size
        paintManager.Brush.Size = dotsBrushMediumSize;
        globalBrushValues.brushSize = dotsBrushMediumSize;
        currentMediumBrushSize = dotsBrushMediumSize;
        paintManager.PaintObject.randomSizeRange = dotsBrushMediumRandomRange;
        currentMediumRandomBrushRange = dotsBrushMediumRandomRange;

        //Opacity
        paintManager.PaintObject.randomOpacityRange = new Vector2(0.5f, 1f);
    }

    public void ArrowBrush()
    {
        //Line
        paintManager.PaintObject.velocityLineWidth = false;
        paintManager.PaintObject.lineActive = false;

        //Single Brush Texture
        Texture brushtexture = brushes[3].mainTexture;
        paintManager.Brush.SetTexture(brushtexture, true, false);
        paintManager.PaintObject.randomBrushAngle = true;
        paintManager.PaintObject.spacingThresholdRange = new Vector2(0.1f, 0.5f);

        //Drip
        paintManager.PaintObject.dripImageArray = dripDotTextures;
        paintManager.PaintObject.dripImage = brushes[3];
        paintManager.PaintObject.isUsingDrip = false;
        paintManager.PaintObject.useDripImageCycle = true;
        paintManager.PaintObject.dripInterval = 0.02f;

        //Size
        paintManager.Brush.Size = arrowBrushMediumSize;
        globalBrushValues.brushSize = arrowBrushMediumSize;
        currentMediumBrushSize = arrowBrushMediumSize;
        paintManager.PaintObject.randomSizeRange = arrowBrushMediumRandomRange;
        currentMediumRandomBrushRange = arrowBrushMediumRandomRange;

        //Opacity
        paintManager.PaintObject.randomOpacityRange = new Vector2(0.5f, 1f);
    }


    public void DistanceBasedBrush1()
    {
        //Line
        paintManager.PaintObject.velocityLineWidth = false;
        paintManager.PaintObject.lineActive = false;

        //Single Brush Texture
        Texture brushtexture = brushes[2].mainTexture;
        paintManager.Brush.SetTexture(brushtexture, true, false);
        paintManager.PaintObject.randomBrushAngle = true;
        paintManager.PaintObject.spacingThresholdRange = new Vector2(0.1f, 0.1f);

        //Drip
        //manager.PaintObject.dripImageArray = dripDotTextures;
        //manager.PaintObject.dripImage = brushes[2];
        paintManager.PaintObject.isUsingDrip = false;
        paintManager.PaintObject.useDripImageCycle = false;
        //manager.PaintObject.dripInterval = 0.02f;

        //Size
        currentMediumBrushSize = 0.5f;
        paintManager.Brush.Size = currentMediumBrushSize;
        globalBrushValues.brushSize = currentMediumBrushSize;
        currentMediumRandomBrushRange = new Vector2(0.2f, 1f);
        paintManager.PaintObject.randomSizeRange = currentMediumRandomBrushRange;

        //Opacity
        paintManager.PaintObject.randomOpacityRange = new Vector2(0.5f, 1f);

            /*manager.PaintObject.velocityLineWidth = false;
            //manager.PaintObject.dripImage = brushes[2];
            manager.PaintObject.isUsingDrip = false;
            manager.PaintObject.lineActive = false;
            //manager.PaintObject.dripInterval = 0.5f;
            Texture brushtexture = brushes[2].mainTexture;
            manager.Brush.SetTexture(brushtexture, true, false);
            manager.PaintObject.randomBrushAngle = true;
            manager.PaintObject.spacingThresholdRange = new Vector2(0.1f, 0.1f);
            //manager.PaintObject.useTimeBasedSpacing = true;

            //Drip
            manager.PaintObject.useDripImageCycle = false;

            //Size
            currentMediumBrushSize = 0.1f;
            manager.Brush.Size = currentMediumBrushSize;
            currentMediumRandomBrushRange = new Vector2(0.2f, 1f);
            manager.PaintObject.randomSizeRange = currentMediumRandomBrushRange;

            //Opacity
            manager.PaintObject.randomOpacityRange = new Vector2(0.5f, 1f);*/
        
    }





    public void ChangeBrushSize(int brushSize)
    {
        brushEffectNumber = brushSize;
        switch (brushEffectNumber)
        {
            case 0:
                globalBrushValues.brushSize = currentMediumBrushSize * 0.5f;
                globalBrushValues.randomSizeRange = new Vector2(currentMediumRandomBrushRange.x * 0.5f, currentMediumRandomBrushRange.y * 0.5f);

                paintManager.Brush.Size = currentMediumBrushSize * 0.5f;
                paintManager.PaintObject.randomSizeRange = new Vector2(currentMediumRandomBrushRange.x * 0.5f, currentMediumRandomBrushRange.y * 0.5f);
                break;
            case 1:
                globalBrushValues.brushSize = currentMediumBrushSize;
                globalBrushValues.randomSizeRange = new Vector2(currentMediumRandomBrushRange.x, currentMediumRandomBrushRange.y);

                paintManager.Brush.Size = currentMediumBrushSize;
                paintManager.PaintObject.randomSizeRange = new Vector2(currentMediumRandomBrushRange.x, currentMediumRandomBrushRange.y);
                break;
            case 2:
                globalBrushValues.brushSize = currentMediumBrushSize * 1.5f;
                globalBrushValues.randomSizeRange = new Vector2(currentMediumRandomBrushRange.x * 1.5f, currentMediumRandomBrushRange.y * 1.5f);

                paintManager.Brush.Size = currentMediumBrushSize * 1.5f;
                paintManager.PaintObject.randomSizeRange = new Vector2(currentMediumRandomBrushRange.x * 1.5f, currentMediumRandomBrushRange.y * 1.5f);
                break;
        }
        
    }

    /*public void NewBrushSettings(bool velocityLineWidth, bool lineActive, float dripInterval, int brushNum, float brushSize, float spacingThreshold, bool randomBrushAngle, Vector2 randomSizeRange, bool randomSize, bool randomOpacity, Vector2 randomOpacityRange )
    {
        paintManagers = FindObjectsOfType<PaintManager>();
        foreach (PaintManager manager in paintManagers)
        {
            manager.PaintObject.velocityLineWidth = velocityLineWidth;
            manager.PaintObject.lineActive = lineActive;
            manager.PaintObject.dripInterval = dripInterval;
            Texture brushtexture = brushes[brushNum].mainTexture;
            manager.Brush.SetTexture(brushtexture, true, false);
            manager.Brush.Size = brushSize;
            manager.PaintObject.spacingThreshold = spacingThreshold;
            manager.PaintObject.randomBrushAngle = randomBrushAngle;
            manager.PaintObject.randomSizeRange = randomSizeRange;
            manager.PaintObject.randomSize = randomSize;
            manager.PaintObject.randomOpacity = randomOpacity;
            manager.PaintObject.randomOpacityRange = randomOpacityRange;
        }
    }*/
}
