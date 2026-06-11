using UnityEngine;

/// <summary>
/// Attach to your crazyflie/drone GameObject.
/// Controls directional movement and reset.
/// </summary>
public class DroneMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveAmount = 0.5f;       // units per button press
    public float moveSpeed = 2.0f;        // smoothing speed

    [Header("Origin Point")]
    public Vector3 originPoint = new Vector3(0.00678f, -0.999f, 2.573f);

    // Internal
    private Vector3 targetPosition;
    private bool isMoving = false;

    void Start()
    {
        // Start at origin
        transform.position = originPoint;
        targetPosition = originPoint;
    }

    void Update()
    {
        // Smoothly move toward target
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
    }

    // ─── BUTTON CALLBACKS ─────────────────────────────────────────

    public void MoveUp()
    {
        targetPosition += new Vector3(0, moveAmount, 0);
        isMoving = true;
        Debug.Log($"[DroneMovement] Move Up → {targetPosition}");
    }

    public void MoveDown()
    {
        targetPosition += new Vector3(0, -moveAmount, 0);
        isMoving = true;
        Debug.Log($"[DroneMovement] Move Down → {targetPosition}");
    }

    public void MoveLeft()
    {
        targetPosition += new Vector3(-moveAmount, 0, 0);
        isMoving = true;
        Debug.Log($"[DroneMovement] Move Left → {targetPosition}");
    }

    public void MoveRight()
    {
        targetPosition += new Vector3(moveAmount, 0, 0);
        isMoving = true;
        Debug.Log($"[DroneMovement] Move Right → {targetPosition}");
    }

    public void MoveForward()
    {
        targetPosition += new Vector3(0, 0, moveAmount);
        isMoving = true;
        Debug.Log($"[DroneMovement] Move Forward → {targetPosition}");
    }

    public void MoveBackward()
    {
        targetPosition += new Vector3(0, 0, -moveAmount);
        isMoving = true;
        Debug.Log($"[DroneMovement] Move Backward → {targetPosition}");
    }

    public void Reset()
    {
        targetPosition = originPoint;
        isMoving = true;
        Debug.Log($"[DroneMovement] Resetting to origin: {originPoint}");
    }
}