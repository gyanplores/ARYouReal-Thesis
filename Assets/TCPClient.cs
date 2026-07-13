using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>
/// Attach to any GameObject (e.g. "NetworkManager").
///
/// On Start(): connects to Python and sends {"cmd":"READY"}\n
/// This tells Python to stop waiting and show trial info.
///
/// SendAnswer(int value): sends {"cmd":"ANSWER","value":N}\n to Python.
/// Called by the QuestionnaireUI when the user confirms their slider answer.
/// </summary>
public class TCPClient : MonoBehaviour
{
    [Header("Connection Settings")]
    public string pythonIP = "192.168.1.xxx";
    public int pythonPort = 5007;

    // Internal
    private TcpClient client;
    private NetworkStream stream;
    private bool isConnected = false;
    private readonly object streamLock = new object();

    void Start()
    {
        // Connect in background — never block main thread
        Thread t = new Thread(ConnectAndSendReady);
        t.IsBackground = true;
        t.Start();
    }

    void ConnectAndSendReady()
    {
        try
        {
            client = new TcpClient();

            // 3 second connection timeout
            IAsyncResult result = client.BeginConnect(pythonIP, pythonPort, null, null);
            bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));

            if (!connected)
            {
                client.Close();
                Debug.LogWarning("[TCPClient] Could not connect to Python within 3 seconds. Answers will not be sent.");
                return;
            }

            client.EndConnect(result);
            stream = client.GetStream();
            isConnected = true;
            Debug.Log($"[TCPClient] Connected to Python at {pythonIP}:{pythonPort}");

            // Immediately send READY signal so Python stops waiting
            SendPacket(new ReadyPacket { cmd = "READY" });
            Debug.Log("[TCPClient] Sent READY signal to Python");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TCPClient] Connection failed: {e.Message}");
        }
    }

    // ─── PUBLIC API — called by QuestionnaireUI ───────────────────

    /// <summary>
    /// Sends all 3 answers to Python.
    /// Python receives: {"cmd":"ANSWER","q1":3,"q2":5,"q3":2}
    /// </summary>
    public void SendAnswers(int q1, int q2, int q3, float gazeTime = 0f)
    {
        if (!isConnected)
        {
            Debug.LogWarning("[TCPClient] Not connected — answers not sent");
            return;
        }

        SendPacket(new AnswerPacket { cmd = "ANSWER", q1 = q1, q2 = q2, q3 = q3, gazeTime = Mathf.Round(gazeTime * 100f) / 100f });
        Debug.Log($"[TCPClient] Sent answers: Q1={q1} Q2={q2} Q3={q3} Gaze={gazeTime:F2}s");
    }

    public void SendAnswer(int value)
    {
        SendAnswers(value, value, value, 0f);
    }

    // ─── INTERNAL ─────────────────────────────────────────────────

    void SendPacket(object packet)
    {
        try
        {
            string json = JsonUtility.ToJson(packet) + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);

            lock (streamLock)
            {
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TCPClient] Send failed: {e.Message}");
            isConnected = false;
        }
    }

    void OnDestroy()
    {
        isConnected = false;
        stream?.Close();
        client?.Close();
    }

    // ─── SERIALIZABLE PACKETS ─────────────────────────────────────

    [Serializable]
    private class ReadyPacket
    {
        public string cmd;
    }

    [Serializable]
    private class AnswerPacket
    {
        public string cmd;
        public int q1;
        public int q2;
        public int q3;
        public float gazeTime;
    }
}