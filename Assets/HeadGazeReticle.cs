using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any GameObject in the scene.
/// Casts a ray from the CenterEyeAnchor forward every frame.
/// Shows a small reticle dot in the center of the screen.
/// Dot changes color when the gaze ray hits the drone.
///
/// Setup:
/// 1. Attach this script to any GameObject
/// 2. Create a Canvas (World Space or Screen Space Overlay) with a small Image (the reticle dot)
/// 3. Assign CenterEyeAnchor, ReticleImage, and DroneTransform in Inspector
/// </summary>
public class HeadGazeReticle : MonoBehaviour
{
    [Header("Gaze Settings")]
    public Transform centerEyeAnchor;   // OVRCameraRig → TrackingSpace → CenterEyeAnchor
    public float gazeDistance = 20f;    // how far the ray extends

    [Header("Target")]
    public Transform droneTransform;    // the drone GameObject to detect gaze on

    [Header("Reticle")]
    public Image reticleImage;          // small circle Image on a Canvas
    public Color idleColor = Color.white;    // color when not looking at drone
    public Color gazeColor = Color.green;    // color when looking at drone
    public float reticleIdleSize = 20f;      // size in pixels when idle
    public float reticleGazeSize = 30f;      // slightly bigger when hitting drone

    [Header("Smoothing")]
    public float colorSmoothSpeed = 8f;
    public float sizeSmoothSpeed = 8f;

    // Internal
    private bool isGazingAtDrone = false;
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
        UpdateReticleVisuals();
    }

    void CastGazeRay()
    {
        Ray gazeRay = new Ray(centerEyeAnchor.position, centerEyeAnchor.forward);
        bool hitDrone = false;

        if (Physics.Raycast(gazeRay, out RaycastHit hit, gazeDistance))
        {
            // Check if ray hit the drone or any of its children
            if (droneTransform != null &&
                (hit.collider.gameObject == droneTransform.gameObject ||
                 hit.collider.transform.IsChildOf(droneTransform)))
            {
                hitDrone = true;
            }
        }

        // Fire events on state change
        if (hitDrone && !isGazingAtDrone)
        {
            isGazingAtDrone = true;
            OnGazeEnter();
        }
        else if (!hitDrone && isGazingAtDrone)
        {
            isGazingAtDrone = false;
            OnGazeExit();
        }
    }

    void OnGazeEnter()
    {
        Debug.Log("[HeadGazeReticle] Gaze ENTER drone");
    }

    void OnGazeExit()
    {
        Debug.Log("[HeadGazeReticle] Gaze EXIT drone");
    }

    void UpdateReticleVisuals()
    {
        // Smoothly interpolate color
        Color targetColor = isGazingAtDrone ? gazeColor : idleColor;
        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * colorSmoothSpeed);
        reticleImage.color = currentColor;

        // Smoothly interpolate size (pulse slightly when gazing)
        float targetSize = isGazingAtDrone ? reticleGazeSize : reticleIdleSize;
        currentSize = Mathf.Lerp(currentSize, targetSize, Time.deltaTime * sizeSmoothSpeed);

        RectTransform rt = reticleImage.rectTransform;
        rt.sizeDelta = new Vector2(currentSize, currentSize);
    }

    // Public getter so other scripts can check if user is gazing at drone
    public bool IsGazingAtDrone() => isGazingAtDrone;
}
