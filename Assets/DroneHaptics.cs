using UnityEngine;

/// <summary>
/// Attach this to the Drone GameObject.
/// Uses Physics.Raycast from each controller's position/direction
/// to detect when the ray hover over this drone, then vibrates
/// the corresponding controller while hovering.
///
/// Requires:
/// 1. A Collider on this drone GameObject (Box, Sphere, or Mesh)
/// 2. OVRCameraRig in the scene with TrackingSpace/LeftHandAnchor/RightHandAnchor
/// </summary>
public class DroneHaptics : MonoBehaviour
{
    [Header("Haptic Settings")]
    [Range(0f, 1f)] public float frequency = 0.5f;
    [Range(0f, 1f)] public float amplitude = 0.5f;

    [Header("Ray Settings")]
    public float rayDistance = 20f; // how far the ray extends

    [Header("Controller Anchors")]
    [Tooltip("Drag LeftHandAnchor from OVRCameraRig → TrackingSpace here")]
    public Transform leftHandAnchor;
    [Tooltip("Drag RightHandAnchor from OVRCameraRig → TrackingSpace here")]
    public Transform rightHandAnchor;

    // Internal state
    private bool isLeftHovering = false;
    private bool isRightHovering = false;

    void Update()
    {
        CheckRayHover(leftHandAnchor, ref isLeftHovering, OVRInput.Controller.LTouch, "Left");
        CheckRayHover(rightHandAnchor, ref isRightHovering, OVRInput.Controller.RTouch, "Right");
    }

    void CheckRayHover(Transform anchor, ref bool isHovering, OVRInput.Controller controller, string side)
    {
        if (anchor == null) return;

        // Cast ray from controller anchor forward
        Ray ray = new Ray(anchor.position, anchor.forward);
        bool hitThis = false;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            // Check if the ray hit THIS drone's collider
            if (hit.collider.gameObject == gameObject ||
                hit.collider.transform.IsChildOf(transform))
            {
                hitThis = true;
            }
        }

        // Hover Enter
        if (hitThis && !isHovering)
        {
            isHovering = true;
            OVRInput.SetControllerVibration(frequency, amplitude, controller);
            Debug.Log($"[DroneHaptics] {side} ray hover ENTER");
        }

        // Hover Exit
        if (!hitThis && isHovering)
        {
            isHovering = false;
            OVRInput.SetControllerVibration(0f, 0f, controller);
            Debug.Log($"[DroneHaptics] {side} ray hover EXIT");
        }

        // Keep vibrating while hovering
        if (hitThis && isHovering)
        {
            OVRInput.SetControllerVibration(frequency, amplitude, controller);
        }
    }

    void OnDisable()
    {
        // Safety — stop all vibrations when disabled
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        isLeftHovering = false;
        isRightHovering = false;
    }
}