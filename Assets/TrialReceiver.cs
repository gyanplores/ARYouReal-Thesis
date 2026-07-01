using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using TMPro;

/// <summary>
/// Attach to a GameObject in the scene (e.g. a "TrialReceiver" empty object).
/// Listens for the unified TRIAL packet from trial_sequence.py and:
///   1. Parses the affordance string (VAH, VA, AH, VH, V, A, H)
///      and applies it via the SensoryManager (Visual/Audio/Haptics toggles)
///   2. Parses the x/y/z coordinate and moves the drone there (mirrored if desired)
///
/// Expected JSON from Python (UDP, port 5005):
///   {"type": "TRIAL", "affordance": "VAH", "x": 1.5, "y": 1.0, "z": 2.0}
/// </summary>
public class TrialReceiver : MonoBehaviour
{
    [Header("UDP Settings")]
    public int listenPort = 5005;

    [Header("References")]
    [Tooltip("Drag the SensoryManager component here to apply V/A/H toggles")]
    public SensoryManager sensoryManager;

    [Tooltip("The drone GameObject to move")]
    public Transform droneTransform;

    [Tooltip("Drag UnityTCPClient here so it gets notified when a new trial arrives")]
    public UnityTCPClient tcpClient;

    [Header("Drone Mode (set by StartupMenu)")]
    [Tooltip("True = MIRROR (opposite X). False = MASK (same direction). Set automatically by StartupMenu.cs")]
    public bool isMirrorMode = true;

    [Header("Movement Smoothing")]
    public float moveSpeed = 3.0f;

    [Header("UI Display (optional)")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI receivedText;

    // Internal
    private UdpClient udpClient;
    private Thread udpThread;
    private bool isRunning = false;
    private readonly object dataLock = new object();

    private string lastAffordance = "-";
    private Vector3 latestRealPosition = Vector3.zero;
    private Vector3 mirroredTargetPosition = Vector3.zero;
    private bool hasNewPositionData = false;
    private bool hasNewAffordance = false;
    private string lastRawMessage = "Waiting for data...";
    private int messageCount = 0;

    // Relative/offset movement tracking
    private Vector3 realBaseline = Vector3.zero;      // first real position received (Crazyflie space)
    private Vector3 unityStartPosition = Vector3.zero; // where the Unity drone is when first packet arrives
    private bool baselineSet = false;                  // whether baseline has been captured yet

    void Start()
    {
        if (droneTransform == null)
            droneTransform = transform;

        // Record where the Unity drone starts in the scene
        // (e.g. (0, 0, 2) — 2 meters in front of user)
        // All real drone movement will be applied as a delta on top of this
        unityStartPosition = droneTransform.position;

        StartUDP();
        Debug.Log($"[TrialReceiver] Listening on port {listenPort}");
        Debug.Log($"[TrialReceiver] Device IP: {GetLocalIP()}");
        Debug.Log($"[TrialReceiver] Unity drone start position: {unityStartPosition}");
    }

    void Update()
    {
        Vector3 target;
        bool newPosition;
        bool newAffordance;
        string affordance;

        lock (dataLock)
        {
            target = mirroredTargetPosition;
            newPosition = hasNewPositionData;
            newAffordance = hasNewAffordance;
            affordance = lastAffordance;
            hasNewAffordance = false; // consume the flag, apply once
        }

        // Move drone smoothly toward the mirrored target position
        if (newPosition)
        {
            droneTransform.position = Vector3.MoveTowards(
                droneTransform.position,
                target,
                moveSpeed * Time.deltaTime
            );
        }

        // Apply affordance to SensoryManager once per new trial
        if (newAffordance && sensoryManager != null)
        {
            sensoryManager.ApplyAffordanceString(affordance);
        }

        // Notify TCP client that a new trial has arrived (starts prompt countdown)
        if (newAffordance && tcpClient != null)
        {
            tcpClient.OnTrialReceived();
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        string raw, affordance;
        int count;
        Vector3 real, mirrored;

        lock (dataLock)
        {
            raw = lastRawMessage;
            count = messageCount;
            affordance = lastAffordance;
            real = latestRealPosition;
            mirrored = mirroredTargetPosition;
        }

        if (statusText != null)
            statusText.text = $"UDP Listening on port {listenPort}\nDevice IP: {GetLocalIP()}\nMessages received: {count}\nAffordance: {affordance}";

        if (receivedText != null)
            receivedText.text = $"Real drone:  ({real.x:F2}, {real.y:F2}, {real.z:F2})\nMirrored target: ({mirrored.x:F2}, {mirrored.y:F2}, {mirrored.z:F2})\n\nRaw: {raw}";
    }

    // ─── UDP LISTENER ─────────────────────────────────────────────

    void StartUDP()
    {
        try
        {
            udpClient = new UdpClient(listenPort);
            isRunning = true;
            udpThread = new Thread(ReceiveLoop);
            udpThread.IsBackground = true;
            udpThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[TrialReceiver] Failed to start UDP: {e.Message}");
        }
    }

    void ReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);
                ParsePacket(message);
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogWarning($"[TrialReceiver] Receive error: {e.Message}");
            }
        }
    }

    void ParsePacket(string json)
    {
        try
        {
            string type = ExtractStringField(json, "type");

            if (type == "TRIAL")
            {
                string affordance = ExtractStringField(json, "affordance");
                float x = ExtractFloatField(json, "x");
                float y = ExtractFloatField(json, "y");
                float z = ExtractFloatField(json, "z");

                Vector3 real = new Vector3(x, y, z);

                // ── Mode now comes from Unity's own StartupMenu choice ───
                // (Python no longer sends "mode" — removed from the packet)
                bool isMirror = isMirrorMode;

                // ── Axis conversion: Crazyflie → Unity ──────────────────
                // Crazyflie X → Unity X (horizontal, same)
                // Crazyflie Y → Unity Z (horizontal depth)
                // Crazyflie Z → Unity Y (vertical height) ← KEY SWAP
                float unityX = x;
                float unityY = z; // Crazyflie Z (altitude) → Unity Y (up)
                float unityZ = y; // Crazyflie Y (depth)    → Unity Z (depth)

                // ── Set baseline on first packet ─────────────────────────
                // We capture the real drone's starting Crazyflie position
                // and the Unity drone's starting scene position once,
                // then all subsequent moves are deltas from that baseline.
                bool isFirstPacket = false;
                lock (dataLock) { isFirstPacket = !baselineSet; }

                if (isFirstPacket)
                {
                    lock (dataLock)
                    {
                        realBaseline = new Vector3(unityX, unityY, unityZ);
                        unityStartPosition = droneTransform.position;
                        baselineSet = true;
                    }
                    Debug.Log($"[TrialReceiver] Baseline set — real: ({unityX:F2},{unityY:F2},{unityZ:F2}) unity start: {unityStartPosition}");
                }

                // ── Compute relative offset from baseline ─────────────────
                Vector3 baseline;
                Vector3 unityStart;
                lock (dataLock)
                {
                    baseline = realBaseline;
                    unityStart = unityStartPosition;
                }

                Vector3 delta = new Vector3(unityX, unityY, unityZ) - baseline;

                // ── Apply mask or mirror on delta ────────────────────────
                // MASK:   delta applies as-is (same direction)
                // MIRROR: flip X only, Y and Z stay the same
                Vector3 mirroredDelta = new Vector3(
                    isMirror ? -delta.x : delta.x,   // flip X for mirror, keep for mask
                    delta.y,                           // Y always same (vertical height)
                    delta.z                            // Z always same (horizontal depth)
                );

                // ── Final target = Unity start position + mirrored delta ──
                Vector3 mirrored = unityStart + mirroredDelta;

                lock (dataLock)
                {
                    latestRealPosition = real;
                    mirroredTargetPosition = mirrored;
                    hasNewPositionData = true;
                    lastAffordance = affordance;
                    hasNewAffordance = true;
                    lastRawMessage = json;
                    messageCount++;
                }

                Debug.Log($"[TrialReceiver] TRIAL '{affordance}' mode={(isMirror ? "MIRROR" : "MASK")} real=({x:F2},{y:F2},{z:F2}) → unity=({(unityStart + mirroredDelta).x:F2},{(unityStart + mirroredDelta).y:F2},{(unityStart + mirroredDelta).z:F2})");
            }
            else
            {
                lock (dataLock)
                {
                    lastRawMessage = json;
                    messageCount++;
                }
                Debug.Log($"[TrialReceiver] Ignored packet type: {type}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TrialReceiver] Failed to parse '{json}': {e.Message}");
        }
    }

    // ─── MINI JSON FIELD EXTRACTORS ───────────────────────────────

    string ExtractStringField(string json, string key)
    {
        string pattern = $"\"{key}\"";
        int keyIndex = json.IndexOf(pattern);
        if (keyIndex < 0) return "";

        int colonIndex = json.IndexOf(':', keyIndex);
        int firstQuote = json.IndexOf('"', colonIndex + 1);
        int secondQuote = json.IndexOf('"', firstQuote + 1);

        if (firstQuote < 0 || secondQuote < 0) return "";
        return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
    }

    float ExtractFloatField(string json, string key)
    {
        string pattern = $"\"{key}\"";
        int keyIndex = json.IndexOf(pattern);
        if (keyIndex < 0) return 0f;

        int colonIndex = json.IndexOf(':', keyIndex);
        int valueStart = colonIndex + 1;
        int valueEnd = valueStart;

        while (valueEnd < json.Length &&
               json[valueEnd] != ',' &&
               json[valueEnd] != '}')
        {
            valueEnd++;
        }

        string valueStr = json.Substring(valueStart, valueEnd - valueStart).Trim();
        float.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float result);
        return result;
    }

    string GetLocalIP()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            return "Unknown";
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        udpClient?.Close();
        udpThread?.Abort();
    }

    // ─── PUBLIC API — called by StartupMenu.cs ─────────────────────

    /// <summary>
    /// Called once by StartupMenu when the user selects Mirror or Mask.
    /// </summary>
    public void SetMirrorMode(bool mirror)
    {
        isMirrorMode = mirror;
        Debug.Log($"[TrialReceiver] Mode set to: {(mirror ? "MIRROR" : "MASK")}");
    }
}