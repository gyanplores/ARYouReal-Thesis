using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using TMPro;

/// <summary>
/// Attach to any GameObject in your scene.
/// Displays received UDP messages on screen.
/// </summary>
public class UDPTest : MonoBehaviour
{
    [Header("UDP Settings")]
    public int listenPort = 5005;

    [Header("UI Display (optional)")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI receivedText;

    // Internal
    private UdpClient udpClient;
    private Thread udpThread;
    private bool isRunning = false;
    private string lastMessage = "Waiting for data...";
    private int messageCount = 0;
    private readonly object msgLock = new object();

    void Start()
    {
        StartUDP();
        Debug.Log($"[UDPTest] Listening on port {listenPort}");
        Debug.Log($"[UDPTest] Device IP: {GetLocalIP()}");
    }

    void Update()
    {
        // Update UI every frame with latest message
        string msg;
        lock (msgLock) { msg = lastMessage; }

        if (statusText != null)
            statusText.text = $"UDP Listening on port {listenPort}\nDevice IP: {GetLocalIP()}\nMessages received: {messageCount}";

        if (receivedText != null)
            receivedText.text = $"Last message:\n{msg}";

        // Also log to console
        // (already logged in receive thread)
    }

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
            Debug.LogError($"[UDPTest] Failed to start UDP: {e.Message}");
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

                lock (msgLock)
                {
                    lastMessage = $"From: {remoteEP.Address}:{remoteEP.Port}\nData: {message}";
                    messageCount++;
                }

                Debug.Log($"[UDPTest] Received from {remoteEP.Address}: {message}");
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogWarning($"[UDPTest] Receive error: {e.Message}");
            }
        }
    }

    string GetLocalIP()
    {
        try
        {
            // Gets the device's local network IP
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
