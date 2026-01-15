using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class GazeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float countdownDuration = 2f; // Set your countdown duration here
    public UnityEvent onCountdownComplete; // Unity action to trigger when countdown completes

    private float timer = 0f;
    private bool isGazing = false;
    public Animator animator;
    public Button thisButton;

    public bool loopGaze;

    void Start()
    {
        animator = GetComponent<Animator>();
        thisButton = GetComponent<Button>();


        if (animator == null)
        {
            Debug.LogError("Animator component missing on the GameObject.");
            //animator.Play("GazeButtonAnimation", -1, 0f);
        }
        else
        {
            animator.speed = 0f; // Freeze the animation so we can control its playback manually
        }
    }

    void Update()
    {
        if (isGazing)
        {
            timer += Time.deltaTime;

            // Calculate animation progress as a percentage of the timer
            float animationProgress = Mathf.Clamp01(timer / countdownDuration);

            if (animator != null)
            {
                //animator.Play("GazeButtonAnimation", -1, animationProgress);
                animator.SetFloat("animationProgress", animationProgress);
            }

            if (timer >= countdownDuration)
            {
                onCountdownComplete?.Invoke();
                thisButton.onClick.Invoke();
                Debug.Log("Timer Complete. Button Clicked.");
                if (loopGaze == false)
                {
                    isGazing = false;
                }
                //animator.enabled = false;
                //this.enabled = false;
                ResetTimer();
            }
        }
    }

    private void ResetTimer()
    {
        timer = 0f;

        if (animator != null)
        {
            //animator.Play("GazeButtonAnimation", -1, 0f); // Reset animation to start
            animator.SetFloat("animationProgress", 0);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isGazing = true;
        ResetTimer(); // Reset when entering to start fresh
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isGazing = false;
        ResetTimer(); // Reset when leaving
    }
}
