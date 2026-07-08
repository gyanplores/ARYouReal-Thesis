using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using TMPro;

/// <summary>
/// Bare minimum UDP receiver for diagnostic purposes.
/// Attach to any GameObject. Assign DiagText to any TMP text.
/// This receives on port 5005 and displays ANYTHING it gets.
/// No JSON parsing, no logic — just raw receive and display.
/// </summary>
public class UDPDiagnostic : MonoBehaviour
{
    public int port = 5005;
    public TextMeshProUGUI diagText;

    private UdpClient client;
    private Thread thread;
    private string lastMessage = "WAITING...";
    private int count = 0;
    private bool dirty = false;
    private readonly object lk = new object();

    void Start()
    {
        try
        {
            client = new UdpClient(port);
            thread = new Thread(() =>
            {
                Debug.Log("[UDPDiag] Thread started on port " + port);
                var ep = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    try
                    {
                        byte[] data = client.Receive(ref ep);
                        string msg = Encoding.UTF8.GetString(data);
                        Debug.Log("[UDPDiag] GOT: " + msg);
                        lock (lk) { lastMessage = msg; count++; dirty = true; }
                    }
                    catch { break; }
                }
            });
            thread.IsBackground = true;
            thread.Start();
            Debug.Log("[UDPDiag] Listening on port " + port);
            if (diagText != null)
                diagText.text = "LISTENING on port " + port;
        }
        catch (Exception e)
        {
            Debug.LogError("[UDPDiag] FAILED TO START: " + e.Message);
            if (diagText != null)
                diagText.text = "FAILED: " + e.Message;
        }
    }

    void Update()
    {
        bool d; string msg; int c;
        lock (lk) { d = dirty; msg = lastMessage; c = count; dirty = false; }
        if (d && diagText != null)
            diagText.text = $"Received {c} packets\nLast: {msg}";
    }

    void OnDestroy()
    {
        client?.Close();
        thread?.Abort();
    }
}
