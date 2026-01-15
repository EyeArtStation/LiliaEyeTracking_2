using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Demo;

public class TimeoutBrushstroke : MonoBehaviour
{
    public Demo demoScript;
    public InputController inputController;

    public bool timerActive = false; // Controls whether the timer is running
    public float timer = 0f;        // Tracks the elapsed time
    public  float countdownTime = 3f; // Time to count down to

    public Animator cursorAnimator;

    public bool eyeButtonIsDown;


    private void Start()
    {
        //demoScript.currentPaintManager.TestPaintTrigger(2);
        demoScript.currentPaintManager.TestPaintTrigger(1);
    }

    // Update is called once per frame
    void Update()
    {
        cursorAnimator.gameObject.transform.position = Input.mousePosition;

        if (Input.GetKeyDown(KeyCode.Y))
        {
            StartNewStroke();
        }
        else if (Input.GetKeyUp(KeyCode.Y))
        {
            EndStroke();
        }

        if (eyeButtonIsDown)
        {
            ContinueStroke();
        }

        if (timerActive)
        {
            timer += Time.deltaTime;
            float mappedValue = MapToZeroOne(timer, 0, countdownTime);
            //cursorAnimator.SetFloat("ScaleProgress", mappedValue);

            ContinueStroke();  // Continue drawing while the timer is running

            if (timer >= countdownTime)
            {
                RestartStroke();
            }
        }
    }

    void StartNewStroke()
    {
        demoScript.currentPaintManager.TestPaintTrigger(1); // Start stroke
        eyeButtonIsDown = true;
        timerActive = true;
        timer = 0f;
    }

    void ContinueStroke()
    {
        demoScript.currentPaintManager.TestPaintTrigger(2); // Continue stroke
    }

    void EndStroke()
    {
        demoScript.currentPaintManager.TestPaintTrigger(0); // End stroke
        eyeButtonIsDown = false;
        timerActive = false;
        timer = 0f;
    }

    void RestartStroke()
    {
        demoScript.currentPaintManager.TestPaintTrigger(0); // End current stroke
        demoScript.currentPaintManager.TestPaintTrigger(1); // Start a new stroke immediately
        timer = 0f;  // Reset the timer
    }

    float MapToZeroOne(float value, float min, float max)
    {
        return Mathf.Clamp01((value - min) / (max - min));
    }

    public void ToggleTimer(bool isActive)
    {
        timerActive = isActive;
    }
}
