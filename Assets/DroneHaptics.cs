using UnityEngine;

/// <summary>
/// Attach this to the Drone GameObject.
/// Vibrates the correct controller when its ray hovers over the drone.
/// </summary>
public class DroneHaptics : MonoBehaviour
{
    [Header("Haptic Settings")]
    [Range(0f, 1f)] public float frequency = 0.3f;
    [Range(0f, 1f)] public float amplitude = 0.3f;
    public float duration = 0.1f;

    [Header("Continuous Haptics")]
    [Tooltip("If true, haptics keep firing while hovering. If false, only fires once on hover enter.")]
    public bool continuousHaptics = false;
    public float continuousInterval = 0.2f; // seconds between pulses if continuous

    private float continuousTimer = 0f;
    private bool isLeftHovering = false;
    private bool isRightHovering = false;

    void Update()
    {
        if (!continuousHaptics) return;

        continuousTimer -= Time.deltaTime;
        if (continuousTimer <= 0f)
        {
            continuousTimer = continuousInterval;

            if (isLeftHovering)
                Vibrate(OVRInput.Controller.LTouch);

            if (isRightHovering)
                Vibrate(OVRInput.Controller.RTouch);
        }
    }

    // ─── CALLED BY RAY INTERACTION EVENTS ────────────────────────

    public void OnLeftRayHoverEnter()
    {
        isLeftHovering = true;
        Vibrate(OVRInput.Controller.LTouch);
        Debug.Log("[DroneHaptics] Left ray hover ENTER");
    }

    public void OnLeftRayHoverExit()
    {
        isLeftHovering = false;
        StopVibrate(OVRInput.Controller.LTouch);
        Debug.Log("[DroneHaptics] Left ray hover EXIT");
    }

    public void OnRightRayHoverEnter()
    {
        isRightHovering = true;
        Vibrate(OVRInput.Controller.RTouch);
        Debug.Log("[DroneHaptics] Right ray hover ENTER");
    }

    public void OnRightRayHoverExit()
    {
        isRightHovering = false;
        StopVibrate(OVRInput.Controller.RTouch);
        Debug.Log("[DroneHaptics] Right ray hover EXIT");
    }

    // ─── HAPTIC HELPERS ───────────────────────────────────────────

    void Vibrate(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
        if (!continuousHaptics)
            Invoke(nameof(StopAllVibrations), duration);
    }

    void StopVibrate(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0f, 0f, controller);
    }

    void StopAllVibrations()
    {
        if (!isLeftHovering)
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        if (!isRightHovering)
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }

    void OnDisable()
    {
        // Safety — stop all vibrations if script is disabled
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }
}
