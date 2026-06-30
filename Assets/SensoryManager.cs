using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

/// <summary>
/// Attach to any GameObject in the scene.
/// Receives a number 1-7 via UDP from Python and switches sensory combinations.
/// </summary>
public class SensoryManager : MonoBehaviour
{
    [Header("UDP Settings")]
    public int listenPort = 5006;

    [Header("Visual — Black Box")]
    [Tooltip("A quad/cube placed in front of the camera to block vision")]
    public GameObject blackBox;

    [Header("Audio — White Noise")]
    public AudioSource whiteNoiseSource;

    [Header("Audio — Drone Sound")]
    [Tooltip("The drone's audio source — gets muted when audio is not present")]
    public AudioSource droneAudioSource;

    [Header("Haptics — Drone Hover")]
    [Tooltip("Drag the DroneHaptics component (on the drone) here. SensoryManager only tells it whether the H condition is active; DroneHaptics handles the actual hover detection and vibration.")]
    public DroneHaptics droneHaptics;

    // Internal
    private UdpClient udpClient;
    private Thread udpThread;
    private bool isRunning = false;
    private int pendingMode = -1;
    private readonly object modeLock = new object();

    private bool visualEnabled = true;
    private bool audioEnabled = true;
    private bool hapticsEnabled = false;

    [Header("Startup Menu Handoff")]
    [Tooltip("If true, StartupMenu controls the black box until the user picks Mirror/Mask. SensoryManager waits before applying its own default visual state.")]
    public bool waitForStartupMenu = true;
    private bool menuHasReleasedControl = false;

    void Start()
    {
        if (waitForStartupMenu)
        {
            // Don't touch blackBox yet — StartupMenu owns it until the user picks a mode.
            // Audio/haptics can still default normally since the menu only controls vision.
            SetAudio(true);
            SetHaptics(true);
        }
        else
        {
            // No startup menu in this scene — behave as before
            SetVisual(true);
            SetAudio(true);
            SetHaptics(true);
        }

        StartUDP();
        Debug.Log($"[SensoryManager] Listening on port {listenPort}");
    }

    void Update()
    {
        // Check for pending mode switch from UDP thread
        int mode = -1;
        lock (modeLock)
        {
            mode = pendingMode;
            pendingMode = -1;
        }

        if (mode >= 1 && mode <= 7)
            ApplyMode(mode);
    }

    // ─── MODE SWITCHER ────────────────────────────────────────────

    /// <summary>
    /// Public entry point for string-based affordance combinations
    /// (e.g. "VAH", "VA", "H") coming from TrialReceiver.
    /// Called instead of the numeric 1-7 UDP path when using trial_sequence.py.
    /// </summary>
    public void ApplyAffordanceString(string affordance)
    {
        if (string.IsNullOrEmpty(affordance))
        {
            Debug.LogWarning("[SensoryManager] Empty affordance string received");
            return;
        }

        affordance = affordance.ToUpper();

        bool v = affordance.Contains("V");
        bool a = affordance.Contains("A");
        bool h = affordance.Contains("H");

        SetVisual(v);
        SetAudio(a);
        SetHaptics(h);

        Debug.Log($"[SensoryManager] Affordance '{affordance}' → Visual:{v} Audio:{a} Haptics:{h}");
    }

    void ApplyMode(int mode)
    {
        // Map 1-7 to V/A/H combinations
        // V=Visual, A=Audio, H=Haptics
        switch (mode)
        {
            case 1: // V+A+H
                SetVisual(true);
                SetAudio(true);
                SetHaptics(true);
                break;
            case 2: // V+A
                SetVisual(true);
                SetAudio(true);
                SetHaptics(false);
                break;
            case 3: // V+H
                SetVisual(true);
                SetAudio(false);
                SetHaptics(true);
                break;
            case 4: // A+H
                SetVisual(false);
                SetAudio(true);
                SetHaptics(true);
                break;
            case 5: // V only
                SetVisual(true);
                SetAudio(false);
                SetHaptics(false);
                break;
            case 6: // A only
                SetVisual(false);
                SetAudio(true);
                SetHaptics(false);
                break;
            case 7: // H only
                SetVisual(false);
                SetAudio(false);
                SetHaptics(true);
                break;
        }

        Debug.Log($"[SensoryManager] Mode {mode} → Visual:{visualEnabled} Audio:{audioEnabled} Haptics:{hapticsEnabled}");
    }

    // ─── SENSORY CONTROLS ─────────────────────────────────────────

    void SetVisual(bool enabled)
    {
        visualEnabled = enabled;
        // Black box ON = vision blocked = visual DISABLED
        if (blackBox != null)
            blackBox.SetActive(!enabled);
    }

    void SetAudio(bool enabled)
    {
        audioEnabled = enabled;

        // White noise ON = audio DISABLED (masking real sound)
        if (whiteNoiseSource != null)
        {
            if (!enabled)
            {
                whiteNoiseSource.loop = true;
                whiteNoiseSource.Play();
            }
            else
            {
                whiteNoiseSource.Stop();
            }
        }

        // Drone audio ON = audio ENABLED, OFF = cut drone sound
        if (droneAudioSource != null)
        {
            if (enabled)
            {
                if (!droneAudioSource.isPlaying)
                    droneAudioSource.Play();
            }
            else
            {
                droneAudioSource.Stop();
            }
        }
    }

    void SetHaptics(bool enabled)
    {
        hapticsEnabled = enabled;

        // Tell DroneHaptics whether the H condition is currently active.
        // DroneHaptics itself decides whether to actually vibrate,
        // based on whether the controller ray is hovering the drone.
        if (droneHaptics != null)
            droneHaptics.SetHapticsCondition(enabled);

        if (!enabled)
        {
            // Safety — stop all vibrations immediately regardless of hover state
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }
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
            Debug.LogError($"[SensoryManager] UDP failed: {e.Message}");
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
                string message = Encoding.UTF8.GetString(data).Trim();
                Debug.Log($"[SensoryManager] Received: {message}");

                if (int.TryParse(message, out int mode))
                {
                    lock (modeLock)
                        pendingMode = mode;
                }
                else
                {
                    Debug.LogWarning($"[SensoryManager] Invalid message: '{message}' — expected 1-7");
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogWarning($"[SensoryManager] Receive error: {e.Message}");
            }
        }
    }

    // ─── CLEANUP ──────────────────────────────────────────────────

    void OnDestroy()
    {
        isRunning = false;
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        udpClient?.Close();
        udpThread?.Abort();
    }

    // ─── PUBLIC API — called by StartupMenu.cs ─────────────────────

    /// <summary>
    /// Called once by StartupMenu after the user picks Mirror or Mask.
    /// Hands control of the black box back to SensoryManager's normal
    /// affordance-driven behavior (defaults to Visual ON / VAH state).
    /// </summary>
    public void ReleaseControlFromMenu()
    {
        menuHasReleasedControl = true;
        SetVisual(true); // experiment starts with visual ON (VAH default)
        Debug.Log("[SensoryManager] Control released from StartupMenu — Visual now ON");
    }
}