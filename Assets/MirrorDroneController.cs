using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using TMPro;

/// <summary>
/// Receives drone pose data over UDP from main.py and moves the
/// virtual drone to the MIRRORED (opposite X) position.
///
/// Expected JSON from Python (UDP, port 5005):
///   {"type": "POSE",   "x": 0.5, "y": 1.2, "z": 0.3}
///   {"type": "TARGET", "x": 0.5, "y": 1.2, "z": 0.3}
/// </summary>
public class MirrorDroneController : MonoBehaviour
{
    [Header("UDP Settings")]
    public int listenPort = 5005;

    [Header("Mirror Settings")]
    [Tooltip("Which axis to flip. X is most common for left/right mirroring.")]
    public bool mirrorX = true;
    public bool mirrorY = false;
    public bool mirrorZ = false;

    [Header("Movement Smoothing")]
    public float moveSpeed = 3.0f; // units per second, smooths jitter from UDP packets

    [Header("UI Display (optional)")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI receivedText;

    // Internal
    private UdpClient udpClient;
    private Thread udpThread;
    private bool isRunning = false;
    private readonly object dataLock = new object();

    private Vector3 latestRealPosition = Vector3.zero;
    private Vector3 mirroredTargetPosition = Vector3.zero;
    private bool hasNewData = false;
    private string lastRawMessage = "Waiting for data...";
    private int messageCount = 0;
    private string lastPacketType = "-";

    void Start()
    {
        StartUDP();
        Debug.Log($"[MirrorDroneController] Listening on port {listenPort}");
        Debug.Log($"[MirrorDroneController] Device IP: {GetLocalIP()}");
    }

    void Update()
    {
        // Pull latest data from UDP thread safely
        Vector3 target;
        bool newData;
        lock (dataLock)
        {
            target = mirroredTargetPosition;
            newData = hasNewData;
        }

        // Smoothly move drone toward the mirrored target
        if (newData)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                moveSpeed * Time.deltaTime
            );
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        string raw;
        int count;
        string type;
        Vector3 real, mirrored;

        lock (dataLock)
        {
            raw = lastRawMessage;
            count = messageCount;
            type = lastPacketType;
            real = latestRealPosition;
            mirrored = mirroredTargetPosition;
        }

        if (statusText != null)
            statusText.text = $"UDP Listening on port {listenPort}\nDevice IP: {GetLocalIP()}\nMessages received: {count}\nLast type: {type}";

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
            Debug.LogError($"[MirrorDroneController] Failed to start UDP: {e.Message}");
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
                    Debug.LogWarning($"[MirrorDroneController] Receive error: {e.Message}");
            }
        }
    }

    void ParsePacket(string json)
    {
        try
        {
            // Lightweight manual parse to avoid extra JSON package dependencies.
            // Works for flat JSON like: {"type": "POSE", "x": 0.5, "y": 1.2, "z": 0.3}
            string type = ExtractStringField(json, "type");

            // Only POSE and TARGET packets contain x/y/z — ignore CONDITION/PROMPT here
            if (type == "POSE" || type == "TARGET")
            {
                float x = ExtractFloatField(json, "x");
                float y = ExtractFloatField(json, "y");
                float z = ExtractFloatField(json, "z");

                Vector3 real = new Vector3(x, y, z);
                Vector3 mirrored = new Vector3(
                    mirrorX ? -x : x,
                    mirrorY ? -y : y,
                    mirrorZ ? -z : z
                );

                lock (dataLock)
                {
                    latestRealPosition = real;
                    mirroredTargetPosition = mirrored;
                    hasNewData = true;
                    lastRawMessage = json;
                    lastPacketType = type;
                    messageCount++;
                }

                Debug.Log($"[MirrorDroneController] {type} real=({x:F2},{y:F2},{z:F2}) → mirrored=({mirrored.x:F2},{mirrored.y:F2},{mirrored.z:F2})");
            }
            else
            {
                // CONDITION / PROMPT / other packet types — log only
                lock (dataLock)
                {
                    lastRawMessage = json;
                    lastPacketType = type;
                    messageCount++;
                }
                Debug.Log($"[MirrorDroneController] Non-pose packet received: {json}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MirrorDroneController] Failed to parse '{json}': {e.Message}");
        }
    }

    // ─── MINI JSON FIELD EXTRACTORS ───────────────────────────────
    // Simple manual parsing — avoids needing a full JSON library for flat objects.

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
}
