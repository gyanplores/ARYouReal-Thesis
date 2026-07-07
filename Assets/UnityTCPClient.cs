using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;

/// <summary>
/// Attach to any GameObject in the scene (e.g. a "NetworkManager" empty object).
///
/// On Start(), connects to Python's TCP server (trial_sequence.py).
/// Listens for Left/Right trigger presses when the prompt is showing,
/// and sends {"cmd": "USER_PICK", "choice": "LEFT"} or "RIGHT" back to Python.
///
/// Also receives the TRIAL packet timing to show/hide the prompt.
/// Works alongside TrialReceiver.cs (which handles UDP for drone movement).
/// </summary>
public class UnityTCPClient : MonoBehaviour
{
    [Header("TCP Settings")]
    [Tooltip("IP address of the PC running trial_sequence.py")]
    public string pythonIP = "192.168.1.xxx";
    public int tcpPort = 5006;

    [Header("Prompt UI")]
    [Tooltip("The PromptDisplay script that shows 'Which drone is real?'")]
    public PromptDisplay promptDisplay;

    [Header("Prompt Timing")]
    public float delayAfterTrialBeforePrompt = 5f;

    // Internal
    private TcpClient tcpClient;
    private NetworkStream stream;
    private bool isConnected = false;
    private bool waitingForResponse = false;
    private float promptTimer = 0f;
    private bool promptTimerRunning = false;

    void Start()
    {
        // Connect in background thread — never block the main thread
        // If Python isn't running, app still loads normally and retries
        Thread connectThread = new Thread(ConnectToPython);
        connectThread.IsBackground = true;
        connectThread.Start();
    }

    void Update()
    {
        // Countdown before showing prompt after a trial arrives
        if (promptTimerRunning)
        {
            promptTimer -= Time.deltaTime;
            if (promptTimer <= 0f)
            {
                promptTimerRunning = false;
                ShowPromptAndWait();
            }
        }

        // Only listen for trigger input when prompt is showing
        if (!waitingForResponse) return;

        // Left trigger
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
        {
            SendChoice("LEFT");
        }

        // Right trigger
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            SendChoice("RIGHT");
        }
    }

    // ─── TCP CONNECTION ───────────────────────────────────────────

    void ConnectToPython()
    {
        // Only try once on startup — if Python isn't running,
        // log a warning and give up. Don't hammer retries during
        // the critical app startup window on Quest.
        try
        {
            TcpClient client = new TcpClient();

            // 3 second timeout — if Python isn't ready, move on
            IAsyncResult result = client.BeginConnect(pythonIP, tcpPort, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));

            if (!success)
            {
                client.Close();
                Debug.LogWarning("[UnityTCPClient] Python not reachable — USER_PICK responses disabled. Start Python first next time.");
                return;
            }

            client.EndConnect(result);
            tcpClient = client;
            stream = tcpClient.GetStream();
            isConnected = true;
            Debug.Log($"[UnityTCPClient] Connected to Python at {pythonIP}:{tcpPort}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UnityTCPClient] Could not connect to Python: {e.Message}. USER_PICK responses disabled.");
        }
    }

    // ─── CALLED BY TrialReceiver WHEN A TRIAL PACKET ARRIVES ──────

    /// <summary>
    /// Call this from TrialReceiver when a new TRIAL packet is received.
    /// Starts the countdown before showing the prompt.
    /// </summary>
    public void OnTrialReceived()
    {
        // Cancel any previous prompt countdown
        promptTimerRunning = false;
        waitingForResponse = false;

        if (promptDisplay != null)
            promptDisplay.OnTrialStart(); // hide prompt if it was showing

        // Start the countdown
        promptTimer = delayAfterTrialBeforePrompt;
        promptTimerRunning = true;

        Debug.Log($"[UnityTCPClient] Trial received — prompt in {delayAfterTrialBeforePrompt}s");
    }

    // ─── PROMPT DISPLAY ───────────────────────────────────────────

    void ShowPromptAndWait()
    {
        waitingForResponse = true;

        if (promptDisplay != null)
            promptDisplay.OnTrialEnd(); // this triggers the prompt text to appear

        Debug.Log("[UnityTCPClient] Prompt shown — waiting for Left or Right trigger");
    }

    // ─── SEND RESPONSE TO PYTHON ──────────────────────────────────

    void SendChoice(string choice)
    {
        if (!isConnected || stream == null)
        {
            Debug.LogWarning("[UnityTCPClient] Not connected to Python — cannot send choice");
            return;
        }

        waitingForResponse = false;

        if (promptDisplay != null)
            promptDisplay.OnTrialStart(); // hide prompt after response

        try
        {
            var packet = new { cmd = "USER_PICK", choice = choice };
            string json = JsonUtility.ToJson(new UserPickPacket { cmd = "USER_PICK", choice = choice });
            byte[] data = Encoding.UTF8.GetBytes(json + "\n"); // \n is the TCP delimiter Python expects
            stream.Write(data, 0, data.Length);
            stream.Flush();

            Debug.Log($"[UnityTCPClient] Sent choice: {choice}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnityTCPClient] Failed to send choice: {e.Message}");
        }
    }

    // ─── CLEANUP ──────────────────────────────────────────────────

    void OnDestroy()
    {
        stream?.Close();
        tcpClient?.Close();
    }

    // ─── SERIALIZABLE PACKET ──────────────────────────────────────

    [Serializable]
    private class UserPickPacket
    {
        public string cmd;
        public string choice;
    }
}