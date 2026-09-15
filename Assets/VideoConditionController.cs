using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Attach to a GameObject in Scene 1 (e.g. a "VideoConditionController" empty).
///
/// Handles switching between "normal drone trial" mode and "360 video"
/// mode WITHOUT loading a second scene. Call StartVideo() when the
/// Nth condition begins and EndVideo() when the operator presses Enter
/// in trial_sequence.py to move on.
///
/// Wire up in the Inspector:
///   - videoPlayer / videoSphere → your 360 video setup
///   - passthroughLayer → your [BuildingBlock] Passthrough component
///   - hideDuringVideo → RealDroneProxy, FINAL_DRONE_1.4, ReticleCanvas,
///     Canvas, StartUpMenuCanvas (whatever should disappear during video)
///   - disableDuringVideo → ISDK_RayCanvasInteraction, ISDK_PokeCanvasInteraction
///     (or any other MonoBehaviour that should stop running during video)
///   - headGazeReticle → your HeadGazeReticle component (paused during video
///     so it doesn't record meaningless gaze data)
/// </summary>
public class VideoConditionController : MonoBehaviour
{
    [Header("Video")]
    public VideoPlayer videoPlayer;
    [Tooltip("Large inverted sphere with the video RenderTexture applied. Disabled by default; enabled only during the video condition.")]
    public GameObject videoSphere;

    [Header("Passthrough")]
    [Tooltip("Drag your [BuildingBlock] Passthrough's OVRPassthroughLayer component here.")]
    public OVRPassthroughLayer passthroughLayer;

    [Header("Hide during video")]
    [Tooltip("GameObjects to SetActive(false) while the video plays, and SetActive(true) again after. e.g. RealDroneProxy, FINAL_DRONE_1.4, ReticleCanvas, Canvas")]
    public GameObject[] hideDuringVideo;

    [Header("Disable during video")]
    [Tooltip("Components to disable (not the whole GameObject) while the video plays. e.g. ISDK_RayCanvasInteraction, ISDK_PokeCanvasInteraction")]
    public MonoBehaviour[] disableDuringVideo;

    [Header("Gaze")]
    public HeadGazeReticle headGazeReticle;

    [Header("Sensory / UI")]
    [Tooltip("Drag your SensoryManager here — forces Visual ON, Audio OFF (both drone sound and white noise), Haptics OFF while the video plays.")]
    public SensoryManager sensoryManager;
    [Tooltip("Drag your QuestionnaireUI here — hides the waiting panel while the video plays, and restores it after so the user is blinded again until the next trial begins.")]
    public QuestionnaireUI questionnaireUI;

    [Header("Virtual Drone Visibility")]
    [Tooltip("Drag FINAL_DRONE_1.4 (or its root) here. Its Renderer(s) are disabled during video and re-enabled after — the GameObject itself is never deactivated, so any scripts on it (UDP-driven movement, haptics, etc.) keep running normally.")]
    public Transform virtualDroneVisual;
    [Tooltip("How far back (world Z) to move the drone while it's hidden during video — extra insurance beyond disabling renderers/lights, in case any light bleed or glow is still visible.")]
    public float hiddenZPosition = 5f;
    [Tooltip("Z position to instantly teleport the drone back to when the video ends, before normal UDP-driven tracking resumes.")]
    public float returnZPosition = 2f;
    private Renderer[] droneRenderers;
    private Light[] droneLights;

    private bool isVideoActive = false;
    public bool IsVideoActive => isVideoActive;

    void Start()
    {
        if (videoSphere != null)
            videoSphere.SetActive(false);

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true; // safety net in case the operator takes longer than the video's length
            videoPlayer.Prepare();        // preload now so Play() has no buffering delay when the condition starts
        }

        // Cache renderers and lights once — GetComponentsInChildren catches
        // any child mesh parts / light sources too, not just ones on the
        // root object. Lights (e.g. simulated LEDs) are a separate system
        // from Renderers and won't be affected by disabling renderers alone.
        if (virtualDroneVisual != null)
        {
            droneRenderers = virtualDroneVisual.GetComponentsInChildren<Renderer>(true);
            droneLights = virtualDroneVisual.GetComponentsInChildren<Light>(true);
        }
    }

    /// <summary>
    /// Call when the Nth condition begins. Safe to call from Unity's main
    /// thread only (e.g. from Update(), not directly from the UDP thread).
    /// </summary>
    public void StartVideo()
    {
        if (isVideoActive) return;
        isVideoActive = true;

        if (passthroughLayer != null)
            passthroughLayer.enabled = false;

        foreach (var go in hideDuringVideo)
            if (go != null) go.SetActive(false);

        foreach (var mb in disableDuringVideo)
            if (mb != null) mb.enabled = false;

        if (headGazeReticle != null)
            headGazeReticle.enabled = false;

        if (sensoryManager != null)
            sensoryManager.EnterVideoCondition();

        if (questionnaireUI != null)
            questionnaireUI.HideWaitingPanel();

        // Hide the drone visually only — GameObject stays active, so
        // its UDP-driven movement script, haptics, etc. keep running.
        if (droneRenderers != null)
            foreach (var r in droneRenderers)
                if (r != null) r.enabled = false;

        if (droneLights != null)
            foreach (var l in droneLights)
                if (l != null) l.enabled = false;

        // Extra insurance: physically move it out of view too, in case
        // any glow/bloom effect was still visible even with renderers and
        // lights off. IsVideoActive (checked by TrialReceiver) pauses its
        // normal UDP-driven position updates so it doesn't glide straight
        // back into view on the next frame.
        if (virtualDroneVisual != null)
        {
            Vector3 p = virtualDroneVisual.position;
            virtualDroneVisual.position = new Vector3(p.x, p.y, hiddenZPosition);
        }

        if (videoSphere != null)
            videoSphere.SetActive(true);

        if (videoPlayer != null)
        {
            if (!videoPlayer.isPrepared)
                videoPlayer.Prepare();
            videoPlayer.Play();
        }

        Debug.Log("[VideoConditionController] Video condition started.");
    }

    /// <summary>
    /// Call when the operator presses Enter in trial_sequence.py to end
    /// the video condition and move to the next one.
    /// </summary>
    public void EndVideo()
    {
        if (!isVideoActive) return;
        isVideoActive = false;

        if (videoPlayer != null)
            videoPlayer.Stop();

        if (videoSphere != null)
            videoSphere.SetActive(false);

        if (passthroughLayer != null)
            passthroughLayer.enabled = true;

        // Restore drone visibility
        if (droneRenderers != null)
            foreach (var r in droneRenderers)
                if (r != null) r.enabled = true;

        if (droneLights != null)
            foreach (var l in droneLights)
                if (l != null) l.enabled = true;

        // Teleport straight back to its resting Z instead of letting
        // TrialReceiver's MoveTowards glide it there over a few seconds —
        // the user can't see it during this transition anyway, so there's
        // no reason to visibly animate it.
        if (virtualDroneVisual != null)
        {
            Vector3 p = virtualDroneVisual.position;
            virtualDroneVisual.position = new Vector3(p.x, p.y, returnZPosition);
        }

        foreach (var go in hideDuringVideo)
            if (go != null) go.SetActive(true);

        foreach (var mb in disableDuringVideo)
            if (mb != null) mb.enabled = true;

        if (headGazeReticle != null)
            headGazeReticle.enabled = true;

        // Restore the "blinded between trials" state — the next TRIAL
        // packet's OnTrialStarted() will hide this again once the
        // following condition actually begins. We don't need to
        // explicitly restore SensoryManager's V/A/H state here — the
        // next TRIAL packet calls ApplyAffordanceString() automatically,
        // which will set Visual/Audio/Haptics correctly for that trial.
        if (questionnaireUI != null)
            questionnaireUI.ShowWaitingPanel();

        Debug.Log("[VideoConditionController] Video condition ended.");
    }
}