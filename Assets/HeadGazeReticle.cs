using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to any GameObject in the scene.
/// Casts a ray from the CenterEyeAnchor forward every frame.
/// Shows a small reticle dot in the center of the screen.
/// Dot changes color when the gaze ray hits the drone.
/// Also tracks cumulative gaze time per trial.
/// </summary>
public class HeadGazeReticle : MonoBehaviour
{
    [Header("Gaze Settings")]
    public Transform centerEyeAnchor;
    public float gazeDistance = 20f;

    [Header("Target")]
    public Transform droneTransform;

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
    [Tooltip("Assign a TMP text to show live gaze time in headset")]
    public TextMeshProUGUI gazeTimerText;

    // Internal
    private bool isGazingAtDrone = false;
    private Color currentColor;
    private float currentSize;

    // Gaze timer — counts up while looking, pauses when not
    private float totalGazeTime = 0f;
    private bool timerActive = false;

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

        CastGazeRay();
        UpdateReticleVisuals();
        UpdateGazeTimer();
    }

    void CastGazeRay()
    {
        Ray gazeRay = new Ray(centerEyeAnchor.position, centerEyeAnchor.forward);
        bool hitDrone = false;

        if (Physics.Raycast(gazeRay, out RaycastHit hit, gazeDistance))
        {
            if (droneTransform != null &&
                (hit.collider.gameObject == droneTransform.gameObject ||
                 hit.collider.transform.IsChildOf(droneTransform)))
            {
                hitDrone = true;
            }
        }

        if (hitDrone && !isGazingAtDrone)
        {
            isGazingAtDrone = true;
            timerActive = true;
            Debug.Log("[HeadGazeReticle] Gaze ENTER drone");
        }
        else if (!hitDrone && isGazingAtDrone)
        {
            isGazingAtDrone = false;
            timerActive = false;
            Debug.Log($"[HeadGazeReticle] Gaze EXIT drone — total gaze: {totalGazeTime:F2}s");
        }
    }

    void UpdateGazeTimer()
    {
        if (!timerActive) return;

        totalGazeTime += Time.deltaTime;

        if (gazeTimerText != null)
            gazeTimerText.text = $"Gaze: {totalGazeTime:F1}s";
    }

    void UpdateReticleVisuals()
    {
        Color targetColor = isGazingAtDrone ? gazeColor : idleColor;
        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * colorSmoothSpeed);
        reticleImage.color = currentColor;

        float targetSize = isGazingAtDrone ? reticleGazeSize : reticleIdleSize;
        currentSize = Mathf.Lerp(currentSize, targetSize, Time.deltaTime * sizeSmoothSpeed);

        RectTransform rt = reticleImage.rectTransform;
        rt.sizeDelta = new Vector2(currentSize, currentSize);
    }

    // ─── PUBLIC API ───────────────────────────────────────────────

    public bool IsGazingAtDrone() => isGazingAtDrone;

    /// <summary>
    /// Returns total cumulative gaze time for this trial in seconds.
    /// Called by QuestionnaireUI on confirm.
    /// </summary>
    public float GetGazeTime() => totalGazeTime;

    /// <summary>
    /// Resets gaze timer to 0 — called by TrialReceiver when new trial starts.
    /// </summary>
    public void ResetGazeTimer()
    {
        totalGazeTime = 0f;
        timerActive = false;
        if (gazeTimerText != null)
            gazeTimerText.text = "Gaze: 0.0s";
        Debug.Log("[HeadGazeReticle] Gaze timer reset");
    }
}