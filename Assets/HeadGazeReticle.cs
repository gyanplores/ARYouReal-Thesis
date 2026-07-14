using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to any GameObject in the scene.
/// Tracks head gaze against TWO targets:
///   1. droneTransform     — the virtual drone
///   2. realDroneProxy     — invisible proxy at real drone's position
///
/// Maintains separate cumulative gaze times per trial for each.
/// Both timers reset when ResetGazeTimer() is called (new trial).
/// GetVirtualGazeTime() and GetRealGazeTime() return seconds gazed.
/// </summary>
public class HeadGazeReticle : MonoBehaviour
{
    [Header("Gaze Settings")]
    public Transform centerEyeAnchor;
    public float gazeDistance = 20f;

    [Header("Targets")]
    public Transform droneTransform;    // virtual drone
    public Transform realDroneProxy;    // invisible proxy for real drone

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

    // Internal — real drone proxy gaze
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

        CastGazeRay();
        UpdateTimers();
        UpdateReticleVisuals();
        UpdateTimerDisplay();
    }

    void CastGazeRay()
    {
        Ray gazeRay = new Ray(centerEyeAnchor.position, centerEyeAnchor.forward);

        bool hitVirtual = false;
        bool hitReal = false;

        if (Physics.Raycast(gazeRay, out RaycastHit hit, gazeDistance))
        {
            // Check virtual drone
            if (droneTransform != null &&
                (hit.collider.gameObject == droneTransform.gameObject ||
                 hit.collider.transform.IsChildOf(droneTransform)))
            {
                hitVirtual = true;
            }

            // Check real drone proxy
            if (realDroneProxy != null &&
                (hit.collider.gameObject == realDroneProxy.gameObject ||
                 hit.collider.transform.IsChildOf(realDroneProxy)))
            {
                hitReal = true;
            }
        }

        // Virtual drone gaze enter/exit
        if (hitVirtual && !isGazingAtVirtual)
        {
            isGazingAtVirtual = true;
            Debug.Log("[HeadGazeReticle] Gaze ENTER virtual drone");
        }
        else if (!hitVirtual && isGazingAtVirtual)
        {
            isGazingAtVirtual = false;
            Debug.Log($"[HeadGazeReticle] Gaze EXIT virtual drone — total: {virtualGazeTime:F2}s");
        }

        // Real drone proxy gaze enter/exit
        if (hitReal && !isGazingAtReal)
        {
            isGazingAtReal = true;
            Debug.Log("[HeadGazeReticle] Gaze ENTER real drone proxy");
        }
        else if (!hitReal && isGazingAtReal)
        {
            isGazingAtReal = false;
            Debug.Log($"[HeadGazeReticle] Gaze EXIT real drone proxy — total: {realGazeTime:F2}s");
        }
    }

    void UpdateTimers()
    {
        if (isGazingAtVirtual) virtualGazeTime += Time.deltaTime;
        if (isGazingAtReal) realGazeTime += Time.deltaTime;
    }

    void UpdateReticleVisuals()
    {
        // Turn green if looking at either drone
        bool gazingAtAny = isGazingAtVirtual || isGazingAtReal;
        Color targetColor = gazingAtAny ? gazeColor : idleColor;
        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * colorSmoothSpeed);
        reticleImage.color = currentColor;

        float targetSize = gazingAtAny ? reticleGazeSize : reticleIdleSize;
        currentSize = Mathf.Lerp(currentSize, targetSize, Time.deltaTime * sizeSmoothSpeed);
        reticleImage.rectTransform.sizeDelta = new Vector2(currentSize, currentSize);
    }

    void UpdateTimerDisplay()
    {
        if (gazeTimerText != null)
            gazeTimerText.text = $"Virtual: {virtualGazeTime:F1}s | Real: {realGazeTime:F1}s";
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
            gazeTimerText.text = "Virtual: 0.0s | Real: 0.0s";
        Debug.Log("[HeadGazeReticle] Both gaze timers reset");
    }

    // Keep old method name for backwards compatibility
    public float GetGazeTime() => virtualGazeTime;
}