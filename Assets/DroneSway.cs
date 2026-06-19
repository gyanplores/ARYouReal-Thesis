using UnityEngine;

/// <summary>
/// Attach to the drone GameObject (same one being moved by
/// MirrorDroneController or DroneMovement).
/// Adds a banking/tilting sway based on movement direction,
/// like a real quadcopter leaning into its motion.
/// </summary>
public class DroneSway : MonoBehaviour
{
    [Header("Sway Settings")]
    [Tooltip("Max tilt angle in degrees when moving at full speed")]
    public float maxTiltAngle = 15f;

    [Tooltip("How quickly the tilt responds to velocity changes")]
    public float tiltSmoothing = 5f;

    [Tooltip("Speed (units/sec) at which max tilt is reached")]
    public float speedForMaxTilt = 2.0f;

    [Header("Idle Bob (optional subtle hover motion)")]
    public bool enableIdleBob = true;
    public float bobAmount = 0.03f;
    public float bobSpeed = 1.5f;

    // Internal
    private Vector3 lastPosition;
    private Vector3 currentVelocity;
    private Quaternion targetTiltRotation = Quaternion.identity;
    private Vector3 basePosition;
    private float bobTimer = 0f;

    void Start()
    {
        lastPosition = transform.position;
        basePosition = transform.position;
    }

    void Update()
    {
        // Calculate velocity based on position change since last frame
        Vector3 frameDelta = transform.position - lastPosition;
        Vector3 instantVelocity = Time.deltaTime > 0f ? frameDelta / Time.deltaTime : Vector3.zero;

        // Smooth the velocity to avoid jittery tilt from noisy UDP data
        currentVelocity = Vector3.Lerp(currentVelocity, instantVelocity, Time.deltaTime * tiltSmoothing);

        ApplyTilt();
        ApplyIdleBob();

        lastPosition = transform.position;
    }

    void ApplyTilt()
    {
        // X velocity → roll (tilt left/right, rotate around Z axis)
        // Z velocity → pitch (tilt forward/back, rotate around X axis)
        float speedX = Mathf.Clamp(currentVelocity.x / speedForMaxTilt, -1f, 1f);
        float speedZ = Mathf.Clamp(currentVelocity.z / speedForMaxTilt, -1f, 1f);

        // Moving right (+X) → roll/tilt toward the right → negative Z rotation
        float rollAngle = -speedX * maxTiltAngle;
        // Moving forward (+Z) → pitch nose down slightly → positive X rotation
        float pitchAngle = speedZ * maxTiltAngle;

        Quaternion targetRotation = Quaternion.Euler(pitchAngle, transform.eulerAngles.y, rollAngle);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * tiltSmoothing
        );
    }

    void ApplyIdleBob()
    {
        if (!enableIdleBob) return;

        // Only bob if drone is roughly stationary (low velocity)
        float speed = currentVelocity.magnitude;
        if (speed < 0.05f)
        {
            bobTimer += Time.deltaTime * bobSpeed;
            float bobOffset = Mathf.Sin(bobTimer) * bobAmount;

            // Apply bob as a small local Y offset on top of actual position
            // (doesn't fight with MirrorDroneController/DroneMovement since
            // those set the base XYZ — this just nudges Y visually each frame)
            Vector3 pos = transform.position;
            pos.y += bobOffset * Time.deltaTime; // gentle continuous nudge
        }
        else
        {
            bobTimer = 0f;
        }
    }
}
