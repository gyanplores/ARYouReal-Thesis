using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

public enum DroneMode
{
    Stationary,
    Active,
    CoordinatesMirror,
    CoordinatesOverlay
}

public class DroneController : MonoBehaviour
{
    [Header("Mode Settings")]
    public DroneMode currentMode = DroneMode.Stationary;

    [Header("Active Mode Settings")]
    public float moveSpeed = 0.5f;
    public float moveRange = 1.0f; // 1 meter each axis
    public float waitTimeAtTarget = 1.5f;

    [Header("UDP Settings")]
    public int udpPort = 5005;

    [Header("Mirror Mode Settings")]
    public float mirrorYOffset = -1.5f; // -1.5Y away from coordinates

    // Internal state
    private Vector3 stationaryPosition;
    private Vector3 targetPosition;
    private Vector3 receivedCoordinates;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private bool hasNewCoordinates = false;

    // UDP
    private UdpClient udpClient;
    private Thread udpThread;
    private bool isRunning = false;
    private readonly object coordLock = new object();

    void Start()
    {
        stationaryPosition = transform.position;
        targetPosition = transform.position;
        receivedCoordinates = Vector3.zero;

        StartUDPListener();
    }

    void Update()
    {
        switch (currentMode)
        {
            case DroneMode.Stationary:
                HandleStationary();
                break;
            case DroneMode.Active:
                HandleActive();
                break;
            case DroneMode.CoordinatesMirror:
                HandleCoordinatesMirror();
                break;
            case DroneMode.CoordinatesOverlay:
                HandleCoordinatesOverlay();
                break;
        }
    }

    // ─── MODE HANDLERS ───────────────────────────────────────────

    void HandleStationary()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            stationaryPosition,
            moveSpeed * Time.deltaTime
        );
    }

    void HandleActive()
    {
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                SetNewRandomTarget();
            }
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            isWaiting = true;
            waitTimer = waitTimeAtTarget;
        }
    }

    void HandleCoordinatesMirror()
    {
        Vector3 coords;
        bool newData = false;

        lock (coordLock)
        {
            coords = receivedCoordinates;
            newData = hasNewCoordinates;
            hasNewCoordinates = false;
        }

        if (newData)
        {
            // Crazyflie → Unity axis swap + mirror Y offset
            Vector3 mirrorTarget = new Vector3(
                coords.x,
                coords.z + mirrorYOffset, // CF Z → Unity Y, then offset
                coords.y                   // CF Y → Unity Z
            );

            transform.position = Vector3.MoveTowards(
                transform.position,
                mirrorTarget,
                moveSpeed * Time.deltaTime
            );
        }
    }

    void HandleCoordinatesOverlay()
    {
        Vector3 coords;
        bool newData = false;

        lock (coordLock)
        {
            coords = receivedCoordinates;
            newData = hasNewCoordinates;
            hasNewCoordinates = false;
        }

        if (newData)
        {
            // Crazyflie → Unity axis swap, directly on top
            Vector3 overlayTarget = new Vector3(
                coords.x,
                coords.z, // CF Z → Unity Y
                coords.y  // CF Y → Unity Z
            );

            transform.position = Vector3.MoveTowards(
                transform.position,
                overlayTarget,
                moveSpeed * Time.deltaTime
            );
        }
    }

    // ─── ACTIVE MODE HELPERS ─────────────────────────────────────

    void SetNewRandomTarget()
    {
        targetPosition = stationaryPosition + new Vector3(
            UnityEngine.Random.Range(-moveRange, moveRange),
            UnityEngine.Random.Range(-moveRange * 0.3f, moveRange * 0.3f), // less vertical
            UnityEngine.Random.Range(-moveRange, moveRange)
        );
    }

    // ─── UDP LISTENER ─────────────────────────────────────────────

    void StartUDPListener()
    {
        try
        {
            udpClient = new UdpClient(udpPort);
            isRunning = true;
            udpThread = new Thread(UDPReceiveLoop);
            udpThread.IsBackground = true;
            udpThread.Start();
            Debug.Log($"[DroneController] UDP listener started on port {udpPort}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DroneController] UDP failed to start: {e.Message}");
        }
    }

    void UDPReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);
                ParseCoordinates(message);
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogWarning($"[DroneController] UDP receive error: {e.Message}");
            }
        }
    }

    void ParseCoordinates(string message)
    {
        // Expected format: "x,y,z"  e.g. "0.5,0.3,1.2"
        try
        {
            string[] parts = message.Trim().Split(',');
            if (parts.Length >= 3)
            {
                float x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                float y = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                float z = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

                lock (coordLock)
                {
                    receivedCoordinates = new Vector3(x, y, z);
                    hasNewCoordinates = true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DroneController] Failed to parse coordinates '{message}': {e.Message}");
        }
    }

    // ─── PUBLIC API (called by UI) ────────────────────────────────

    public void SetMode(DroneMode mode)
    {
        currentMode = mode;
        Debug.Log($"[DroneController] Mode set to: {mode}");

        if (mode == DroneMode.Stationary)
            stationaryPosition = transform.position;

        if (mode == DroneMode.Active)
        {
            stationaryPosition = transform.position;
            SetNewRandomTarget();
            isWaiting = false;
        }
    }

    public DroneMode GetMode() => currentMode;

    // ─── CLEANUP ──────────────────────────────────────────────────

    void OnDestroy()
    {
        isRunning = false;
        udpClient?.Close();
        udpThread?.Abort();
    }
}
