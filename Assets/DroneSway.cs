using UnityEngine;

/// <summary>
/// Attach to the virtual drone GameObject.
/// Adds realistic quadcopter motion:
/// - Banking/tilting when moving
/// - Smooth idle sway (sine wave)
/// - High-frequency micro-jitter (random noise corrections)
/// - Propeller vibration shake
/// - Position drift jitter (small random position noise)
/// </summary>
public class DroneSway : MonoBehaviour
{
    [Header("Movement Tilt")]
    public float maxTiltAngle = 15f;
    public float tiltSmoothing = 5f;
    public float speedForMaxTilt = 2.0f;

    [Header("Idle Sway (smooth sine wave)")]
    public bool enableIdleSway = true;
    public float idleTiltAmount = 2.5f;
    public float idleBobAmount = 0.02f;
    public float idleSwaySpeed = 1.2f;

    [Header("Micro Jitter (random corrections like a real drone)")]
    public bool enableMicroJitter = true;
    [Tooltip("How much random rotation jitter in degrees")]
    public float jitterRotationAmount = 0.8f;
    [Tooltip("How fast jitter changes direction")]
    public float jitterSpeed = 12f;
    [Tooltip("How much random position jitter in world units")]
    public float jitterPositionAmount = 0.003f;

    [Header("Propeller Vibration (high frequency shake)")]
    public bool enablePropellerVibration = true;
    [Tooltip("Very small high-frequency shake simulating motor vibration")]
    public float vibrationAmount = 0.0015f;
    public float vibrationSpeed = 45f;

    // Internal — movement tracking
    private Vector3 lastPosition;
    private Vector3 currentVelocity;

    // Internal — idle sway
    private float idleTimerX;
    private float idleTimerZ;
    private float idleTimerBob;
    private Vector3 lastAppliedBobOffset = Vector3.zero;

    // Internal — micro jitter (Perlin noise based for smooth randomness)
    private float jitterSeedX;
    private float jitterSeedY;
    private float jitterSeedZ;
    private float jitterPosX;
    private float jitterPosZ;
    private Vector3 lastAppliedJitterPos = Vector3.zero;

    // Internal — propeller vibration
    private float vibrationTimer;

    void Start()
    {
        lastPosition = transform.position;

        // Random seeds so each drone instance has unique jitter
        idleTimerX = Random.Range(0f, 10f);
        idleTimerZ = Random.Range(0f, 10f);
        idleTimerBob = Random.Range(0f, 10f);
        jitterSeedX = Random.Range(0f, 100f);
        jitterSeedY = Random.Range(0f, 100f);
        jitterSeedZ = Random.Range(0f, 100f);
        jitterPosX = Random.Range(0f, 100f);
        jitterPosZ = Random.Range(0f, 100f);
        vibrationTimer = Random.Range(0f, 10f);
    }

    void Update()
    {
        // Calculate velocity from position change
        Vector3 frameDelta = transform.position - lastPosition;
        Vector3 instantVelocity = Time.deltaTime > 0f
            ? frameDelta / Time.deltaTime
            : Vector3.zero;

        currentVelocity = Vector3.Lerp(
            currentVelocity,
            instantVelocity,
            Time.deltaTime * tiltSmoothing
        );

        ApplyTiltAndIdleSway();
        ApplyIdleBob();
        ApplyMicroJitter();
        ApplyPropellerVibration();

        lastPosition = transform.position;
    }

    void ApplyTiltAndIdleSway()
    {
        float speedX = Mathf.Clamp(currentVelocity.x / speedForMaxTilt, -1f, 1f);
        float speedZ = Mathf.Clamp(currentVelocity.z / speedForMaxTilt, -1f, 1f);

        float movementRoll = -speedX * maxTiltAngle;
        float movementPitch = speedZ * maxTiltAngle;

        float idleRoll = 0f;
        float idlePitch = 0f;

        if (enableIdleSway)
        {
            idleTimerX += Time.deltaTime * idleSwaySpeed;
            idleTimerZ += Time.deltaTime * idleSwaySpeed * 0.73f;

            idleRoll = Mathf.Sin(idleTimerX) * idleTiltAmount;
            idlePitch = Mathf.Cos(idleTimerZ) * idleTiltAmount;
        }

        float speedMag = currentVelocity.magnitude;
        float idleFade = Mathf.Clamp01(1f - (speedMag / speedForMaxTilt));

        // Add micro jitter to rotation using Perlin noise
        float jitterRoll = 0f;
        float jitterPitch = 0f;
        float jitterYaw = 0f;

        if (enableMicroJitter)
        {
            float t = Time.time * jitterSpeed;
            // Perlin noise gives smoother, more organic jitter than pure Random
            jitterRoll = (Mathf.PerlinNoise(jitterSeedX + t * 0.3f, 0f) - 0.5f) * 2f * jitterRotationAmount;
            jitterPitch = (Mathf.PerlinNoise(0f, jitterSeedY + t * 0.4f) - 0.5f) * 2f * jitterRotationAmount;
            jitterYaw = (Mathf.PerlinNoise(jitterSeedZ + t * 0.2f, jitterSeedZ) - 0.5f) * 2f * jitterRotationAmount * 0.5f;
        }

        float finalRoll = movementRoll + (idleRoll * idleFade) + jitterRoll;
        float finalPitch = movementPitch + (idlePitch * idleFade) + jitterPitch;
        float finalYaw = transform.eulerAngles.y + jitterYaw;

        Quaternion targetRotation = Quaternion.Euler(finalPitch, finalYaw, finalRoll);

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

        float speedMag = currentVelocity.magnitude;
        float idleFade = Mathf.Clamp01(1f - (speedMag / speedForMaxTilt));
        bobOffset *= idleFade;

        Vector3 newBobOffset = new Vector3(0f, bobOffset, 0f);
        transform.position += (newBobOffset - lastAppliedBobOffset);
        lastAppliedBobOffset = newBobOffset;
    }

    void ApplyMicroJitter()
    {
        if (!enableMicroJitter) return;

        // Small random position corrections using Perlin noise
        // Simulates the drone's flight controller constantly micro-correcting
        float t = Time.time * jitterSpeed * 0.5f;
        float newJitterX = (Mathf.PerlinNoise(jitterPosX + t, 0f) - 0.5f) * 2f * jitterPositionAmount;
        float newJitterZ = (Mathf.PerlinNoise(0f, jitterPosZ + t) - 0.5f) * 2f * jitterPositionAmount;

        Vector3 newJitterPos = new Vector3(newJitterX, 0f, newJitterZ);
        transform.position += (newJitterPos - lastAppliedJitterPos);
        lastAppliedJitterPos = newJitterPos;
    }

    void ApplyPropellerVibration()
    {
        if (!enablePropellerVibration) return;

        // Very high frequency tiny shake simulating motor/propeller vibration
        // Uses sine waves at different frequencies to create complex vibration pattern
        vibrationTimer += Time.deltaTime * vibrationSpeed;

        float vx = Mathf.Sin(vibrationTimer * 1.0f) * vibrationAmount;
        float vy = Mathf.Sin(vibrationTimer * 1.3f) * vibrationAmount * 0.5f;
        float vz = Mathf.Sin(vibrationTimer * 0.9f) * vibrationAmount;

        // Apply as a tiny position offset — doesn't accumulate
        // because it oscillates around zero
        transform.position += new Vector3(vx, vy, vz) * Time.deltaTime * 60f;
    }
}