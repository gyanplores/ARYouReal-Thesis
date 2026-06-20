using UnityEngine;

/// <summary>
/// Attach to the drone GameObject (same one being moved by
/// MirrorDroneController or DroneMovement).
/// Adds a banking/tilting sway based on movement direction,
/// PLUS a constant idle hover sway even when stationary —
/// like a real quadcopter making tiny constant corrections.
/// </summary>
public class DroneSway : MonoBehaviour
{
    [Header("Movement Tilt Settings")]
    [Tooltip("Max tilt angle in degrees when moving at full speed")]
    public float maxTiltAngle = 15f;

    [Tooltip("How quickly the tilt responds to velocity changes")]
    public float tiltSmoothing = 5f;

    [Tooltip("Speed (units/sec) at which max tilt is reached")]
    public float speedForMaxTilt = 2.0f;

    [Header("Idle Hover Sway (always active, even when stationary)")]
    public bool enableIdleSway = true;

    [Tooltip("How much the drone tilts side to side while hovering, in degrees")]
    public float idleTiltAmount = 2.5f;

    [Tooltip("How much the drone bobs up/down while hovering, in local units")]
    public float idleBobAmount = 0.02f;

    [Tooltip("Speed of the idle sway/bob cycle")]
    public float idleSwaySpeed = 1.2f;

    // Internal — movement tracking
    private Vector3 lastPosition;
    private Vector3 currentVelocity;

    // Internal — idle motion
    private float idleTimerX;
    private float idleTimerZ;
    private float idleTimerBob;
    private Vector3 lastAppliedBobOffset = Vector3.zero;

    void Start()
    {
        lastPosition = transform.position;

        // Slight random offset per drone instance so multiple drones
        // (if ever used) don't sway perfectly in sync
        idleTimerX = Random.Range(0f, 10f);
        idleTimerZ = Random.Range(0f, 10f);
        idleTimerBob = Random.Range(0f, 10f);
    }

    void Update()
    {
        // ── Calculate velocity based on position change since last frame ──
        Vector3 frameDelta = transform.position - lastPosition;
        Vector3 instantVelocity = Time.deltaTime > 0f ? frameDelta / Time.deltaTime : Vector3.zero;

        // Smooth the velocity to avoid jittery tilt from noisy UDP data
        currentVelocity = Vector3.Lerp(currentVelocity, instantVelocity, Time.deltaTime * tiltSmoothing);

        ApplyTiltAndIdleSway();
        ApplyIdleBob();

        lastPosition = transform.position;
    }

    void ApplyTiltAndIdleSway()
    {
        // Movement-based tilt (banking into direction of travel)
        float speedX = Mathf.Clamp(currentVelocity.x / speedForMaxTilt, -1f, 1f);
        float speedZ = Mathf.Clamp(currentVelocity.z / speedForMaxTilt, -1f, 1f);

        float movementRoll = -speedX * maxTiltAngle;
        float movementPitch = speedZ * maxTiltAngle;

        float idleRoll = 0f;
        float idlePitch = 0f;

        if (enableIdleSway)
        {
            idleTimerX += Time.deltaTime * idleSwaySpeed;
            idleTimerZ += Time.deltaTime * idleSwaySpeed * 0.8f; // slightly different speed so it's not symmetric

            idleRoll = Mathf.Sin(idleTimerX) * idleTiltAmount;
            idlePitch = Mathf.Cos(idleTimerZ) * idleTiltAmount;
        }

        // Idle sway fades out the faster the drone moves, so it doesn't
        // fight with the movement tilt at high speed
        float speedMagnitude = currentVelocity.magnitude;
        float idleFade = Mathf.Clamp01(1f - (speedMagnitude / speedForMaxTilt));

        float finalRoll = movementRoll + (idleRoll * idleFade);
        float finalPitch = movementPitch + (idlePitch * idleFade);

        Quaternion targetRotation = Quaternion.Euler(finalPitch, transform.eulerAngles.y, finalRoll);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * tiltSmoothing
        );
    }

    void ApplyIdleBob()
    {
        if (!enableIdleSway) return;

        idleTimerBob += Time.deltaTime * idleSwaySpeed;
        float bobOffset = Mathf.Sin(idleTimerBob) * idleBobAmount;

        // Fade out bob at higher speeds, same as tilt
        float speedMagnitude = currentVelocity.magnitude;
        float idleFade = Mathf.Clamp01(1f - (speedMagnitude / speedForMaxTilt));
        bobOffset *= idleFade;

        // Remove last frame's bob, apply this frame's bob —
        // so this never permanently drifts the drone's real position,
        // and doesn't conflict with MirrorDroneController/DroneMovement
        // setting transform.position to its own target each frame.
        Vector3 newBobOffset = new Vector3(0f, bobOffset, 0f);
        transform.position += (newBobOffset - lastAppliedBobOffset);
        lastAppliedBobOffset = newBobOffset;
    }
}