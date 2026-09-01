using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to any GameObject in the scene.
///
/// The space is divided in the middle using the X axis:
///     X < 0 = LEFT  = Real drone
///     X > 0 = RIGHT = Virtual drone
///
/// Maintains separate cumulative gaze times per trial for each.
/// Both timers reset when ResetGazeTimer() is called.
/// </summary>
public class HeadGazeReticle : MonoBehaviour
{
    [Header("Gaze Settings")]
    public Transform centerEyeAnchor;

    [Header("Reticle")]
    public Image reticleImage;
    public Color idleColor = Color.white;
    public Color gazeColor = Color.green;
    public float reticleIdleSize = 20f;
    public float reticleGazeSize = 30f;

    [Header("Smoothing")]
    public float colorSmoothSpeed = 8f;
    public float sizeSmoothSpeed = 8f;

    [Header("Gaze Timer Display (optional)")]
    public TextMeshProUGUI gazeTimerText;

    // Internal — virtual drone gaze
    private bool isGazingAtVirtual = false;
    private float virtualGazeTime = 0f;

    // Internal — real drone gaze
    private bool isGazingAtReal = false;
    private float realGazeTime = 0f;

    // Reticle visuals
    private Color currentColor;
    private float currentSize;

    void Start()
    {
        if (reticleImage == null)
        {
            Debug.LogError("[HeadGazeReticle] Reticle Image not assigned!");
            return;
        }

        currentColor = idleColor;
        currentSize = reticleIdleSize;
        reticleImage.color = idleColor;
    }

    void Update()
    {
        if (centerEyeAnchor == null || reticleImage == null) return;

        CheckGazeDirection();
        UpdateTimers();
        UpdateReticleVisuals();
        UpdateTimerDisplay();
    }

    void CheckGazeDirection()
    {
        // Get the horizontal direction the user is looking
        Vector3 gazeDirection = centerEyeAnchor.forward;

        // LEFT SIDE: x < 0
        if (gazeDirection.x < 0)
        {
            isGazingAtReal = true;
            isGazingAtVirtual = false;
        }
        // RIGHT SIDE: x > 0
        else if (gazeDirection.x > 0)
        {
            isGazingAtVirtual = true;
            isGazingAtReal = false;
        }
        // CENTER: x == 0
        else
        {
            isGazingAtVirtual = false;
            isGazingAtReal = false;
        }
    }

    void UpdateTimers()
    {
        if (isGazingAtVirtual)
            virtualGazeTime += Time.deltaTime;

        if (isGazingAtReal)
            realGazeTime += Time.deltaTime;
    }

    void UpdateReticleVisuals()
    {
        // Turn green if looking at either side
        bool gazingAtAny = isGazingAtVirtual || isGazingAtReal;

        Color targetColor = gazingAtAny ? gazeColor : idleColor;
        currentColor = Color.Lerp(
            currentColor,
            targetColor,
            Time.deltaTime * colorSmoothSpeed
        );
        reticleImage.color = currentColor;

        float targetSize = gazingAtAny
            ? reticleGazeSize
            : reticleIdleSize;

        currentSize = Mathf.Lerp(
            currentSize,
            targetSize,
            Time.deltaTime * sizeSmoothSpeed
        );

        reticleImage.rectTransform.sizeDelta =
            new Vector2(currentSize, currentSize);
    }

    void UpdateTimerDisplay()
    {
        if (gazeTimerText != null)
        {
            gazeTimerText.text =
                $"Virtual: {virtualGazeTime:F1}s | Real: {realGazeTime:F1}s";
        }
    }

    // ─── PUBLIC API ───────────────────────────────────────────────

    public bool IsGazingAtDrone() => isGazingAtVirtual;

    public bool IsGazingAtReal() => isGazingAtReal;

    public float GetVirtualGazeTime() => virtualGazeTime;

    public float GetRealGazeTime() => realGazeTime;

    /// <summary>
    /// Resets both gaze timers — call on new trial.
    /// </summary>
    public void ResetGazeTimer()
    {
        virtualGazeTime = 0f;
        realGazeTime = 0f;

        isGazingAtVirtual = false;
        isGazingAtReal = false;

        if (gazeTimerText != null)
        {
            gazeTimerText.text =
                "Virtual: 0.0s | Real: 0.0s";
        }

        Debug.Log("[HeadGazeReticle] Both gaze timers reset");
    }

    // Keep old method name for backwards compatibility
    public float GetGazeTime() => virtualGazeTime;

}
