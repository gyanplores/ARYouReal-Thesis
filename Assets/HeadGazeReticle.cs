using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to any GameObject in the scene.
///
/// The space between the real (physical, passthrough) drone and the
/// virtual drone is split using a boundaryOrigin transform, auto-derived
/// as the midpoint between realDroneReference and virtualDroneReference:
///     -X side (relative to boundaryOrigin) = Real drone
///     +X side (relative to boundaryOrigin) = Virtual drone
///
/// Maintains separate cumulative gaze times per trial for each.
/// Both timers reset when ResetGazeTimer() is called.
/// </summary>
public class HeadGazeReticle : MonoBehaviour
{
    private enum GazeSide { None, Real, Virtual }

    [Header("Gaze Settings")]
    public Transform centerEyeAnchor;

    [Header("Passthrough Calibration")]
    [Tooltip("Auto-computed at runtime as the midpoint between realDroneReference and virtualDroneReference, oriented so +X points toward the virtual drone. You can also assign a boundaryOrigin manually and leave the references empty to skip auto-calibration.")]
    public Transform boundaryOrigin;

    [Tooltip("Transform marking the calibrated real-world drone position (e.g. RealDroneProxy).")]
    public Transform realDroneReference;

    [Tooltip("Transform marking the virtual drone (e.g. FINAL_DRONE_1.4).")]
    public Transform virtualDroneReference;

    [Header("Gaze Threshold / Hysteresis")]
    [Tooltip("How far past center (in normalized direction units, -1 to 1) the gaze must go to COUNT as looking at a side.")]
    public float enterThreshold = 0.08f;
    [Tooltip("How far back toward center the gaze must return before it stops counting as that side. Must be smaller than enterThreshold.")]
    public float exitThreshold = 0.03f;

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

    // Internal — current resolved gaze state (with hysteresis applied)
    private GazeSide currentSide = GazeSide.None;

    // Internal — virtual drone gaze
    private float virtualGazeTime = 0f;

    // Internal — real drone gaze
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

        if (exitThreshold >= enterThreshold)
        {
            Debug.LogWarning("[HeadGazeReticle] exitThreshold should be smaller than enterThreshold for hysteresis to work correctly.");
        }

        // If no boundaryOrigin was manually assigned but both drone
        // references are, derive the boundary automatically. Since these
        // drones sit outside the Camera Rig / TrackingSpace hierarchy,
        // this stays valid for the whole session even across recenter
        // events — you only need to re-run this if you physically
        // re-calibrate the drone positions mid-session (call
        // RecalibrateFromReferences() again in that case).
        if (boundaryOrigin == null && realDroneReference != null && virtualDroneReference != null)
        {
            RecalibrateFromReferences();
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
        // Get the horizontal direction the user is looking, expressed
        // relative to boundaryOrigin rather than raw world axes. This
        // way, if the tracking origin drifts or the user recenters
        // mid-session, the left/right split still tracks the calibrated
        // physical midpoint instead of silently shifting with it.
        float x;
        if (boundaryOrigin != null)
        {
            Vector3 localGaze = boundaryOrigin.InverseTransformDirection(centerEyeAnchor.forward);
            x = localGaze.x;
        }
        else
        {
            // Fallback: raw world space. Only reliable if the rig's
            // neutral facing direction is aligned with world +Z and
            // tracking origin never shifts during the session.
            x = centerEyeAnchor.forward.x;
        }

        // Hysteresis (Schmitt trigger) state machine:
        // Once we've committed to a side, require the gaze to cross back
        // past a SMALLER threshold before we drop out of that side. This
        // prevents rapid on/off flicker when the user's gaze sits near
        // the boundary, which is the main reason the raw sign-check
        // version misbehaves.
        switch (currentSide)
        {
            case GazeSide.None:
                if (x <= -enterThreshold) currentSide = GazeSide.Real;
                else if (x >= enterThreshold) currentSide = GazeSide.Virtual;
                break;

            case GazeSide.Real:
                if (x > -exitThreshold) currentSide = GazeSide.None;
                break;

            case GazeSide.Virtual:
                if (x < exitThreshold) currentSide = GazeSide.None;
                break;
        }
    }

    void UpdateTimers()
    {
        if (currentSide == GazeSide.Virtual)
            virtualGazeTime += Time.deltaTime;

        if (currentSide == GazeSide.Real)
            realGazeTime += Time.deltaTime;
    }

    void UpdateReticleVisuals()
    {
        // Turn green if looking at either side
        bool gazingAtAny = currentSide != GazeSide.None;

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

    public bool IsGazingAtDrone() => currentSide == GazeSide.Virtual;

    public bool IsGazingAtReal() => currentSide == GazeSide.Real;

    public float GetVirtualGazeTime() => virtualGazeTime;

    public float GetRealGazeTime() => realGazeTime;

    /// <summary>
    /// Resets both gaze timers — call on new trial.
    /// </summary>
    public void ResetGazeTimer()
    {
        virtualGazeTime = 0f;
        realGazeTime = 0f;

        currentSide = GazeSide.None;

        if (gazeTimerText != null)
        {
            gazeTimerText.text =
                "Virtual: 0.0s | Real: 0.0s";
        }

        Debug.Log("[HeadGazeReticle] Both gaze timers reset");
    }

    // Keep old method name for backwards compatibility
    public float GetGazeTime() => virtualGazeTime;

    /// <summary>
    /// Recomputes boundaryOrigin as the midpoint between realDroneReference
    /// and virtualDroneReference, oriented so its local +X axis points
    /// toward the virtual drone (and therefore -X points toward the real
    /// drone). Call this once after you finish manually calibrating drone
    /// positions each session. Since both reference transforms live
    /// outside the Camera Rig hierarchy, you do NOT need to call this
    /// again after a recenter event — only if the drones themselves get
    /// physically/virtually repositioned.
    /// </summary>
    public void RecalibrateFromReferences()
    {
        if (realDroneReference == null || virtualDroneReference == null)
        {
            Debug.LogWarning("[HeadGazeReticle] Cannot auto-calibrate — realDroneReference or virtualDroneReference is not assigned.");
            return;
        }

        if (boundaryOrigin == null)
        {
            GameObject go = new GameObject("boundaryOrigin (auto)");
            boundaryOrigin = go.transform;
        }

        Vector3 midpoint = (realDroneReference.position + virtualDroneReference.position) * 0.5f;

        // Flatten to horizontal plane so a height difference between the
        // two drones doesn't tilt the boundary — adjust if you need a
        // vertical split instead.
        Vector3 axis = virtualDroneReference.position - realDroneReference.position;
        axis.y = 0f;

        if (axis.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning("[HeadGazeReticle] Real and virtual drone references are at (nearly) the same horizontal position — cannot derive a meaningful boundary axis.");
            return;
        }

        axis.Normalize();
        Vector3 forward = Vector3.Cross(axis, Vector3.up).normalized;

        boundaryOrigin.SetPositionAndRotation(midpoint, Quaternion.LookRotation(forward, Vector3.up));

        Debug.Log($"[HeadGazeReticle] boundaryOrigin auto-calibrated at {midpoint}, +X toward virtual drone.");
    }

    /// <summary>
    /// Manually place boundaryOrigin at a given world position/rotation,
    /// for cases where you'd rather calibrate by hand (e.g. controller
    /// point-and-confirm) instead of using drone reference transforms.
    /// </summary>
    public void RecalibrateBoundaryOrigin(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (boundaryOrigin == null)
        {
            GameObject go = new GameObject("boundaryOrigin (runtime)");
            boundaryOrigin = go.transform;
        }

        boundaryOrigin.SetPositionAndRotation(worldPosition, worldRotation);
        Debug.Log($"[HeadGazeReticle] boundaryOrigin recalibrated to {worldPosition}, {worldRotation.eulerAngles}");
    }

}