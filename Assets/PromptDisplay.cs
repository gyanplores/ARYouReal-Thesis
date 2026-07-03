using UnityEngine;
using TMPro;

/// <summary>
/// Attach to a TextMeshPro object inside ReticleCanvas (or any canvas).
/// Shows "Which drone is real? Left or Right?" prompt after each trial.
/// Hidden by default. Shown/hidden by UnityTCPClient.
///
/// Call OnTrialEnd()  → starts countdown then shows prompt
/// Call OnTrialStart() → immediately hides prompt
/// </summary>
public class PromptDisplay : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI promptText;

    [Header("Text")]
    public string promptMessage = "Which drone is real?\nLeft or Right?";

    void Start()
    {
        // Hidden by default
        if (promptText != null)
            promptText.gameObject.SetActive(false);
    }

    // ─── PUBLIC API — called by UnityTCPClient ─────────────────────

    /// <summary>
    /// Called when a new trial starts — immediately hides the prompt.
    /// </summary>
    public void OnTrialStart()
    {
        if (promptText != null)
            promptText.gameObject.SetActive(false);

        Debug.Log("[PromptDisplay] Prompt hidden");
    }

    /// <summary>
    /// Called when UnityTCPClient's countdown finishes — shows the prompt.
    /// The 5 second delay is handled in UnityTCPClient, not here.
    /// </summary>
    public void OnTrialEnd()
    {
        if (promptText == null) return;
        promptText.text = promptMessage;
        promptText.gameObject.SetActive(true);

        Debug.Log("[PromptDisplay] Prompt shown");
    }

    public bool IsVisible() =>
        promptText != null && promptText.gameObject.activeSelf;
}
