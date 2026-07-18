using UnityEngine;
using TMPro;

/// <summary>
/// Attach to the StartupMenu Canvas (World Space, placed in front of user at app start).
/// Shows the title "Are You Real Experiment" with two buttons: Mirror and Mask.
/// Once the user selects one (via controller ray + trigger, using standard
/// Unity Button onClick — same as your existing mode circle buttons),
/// the menu hides itself and tells TrialReceiver which mode to use.
/// </summary>
public class StartupMenu : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your TrialReceiver GameObject here")]
    public TrialReceiver trialReceiver;

    [Tooltip("Drag your SensoryManager GameObject here")]
    public SensoryManager sensoryManager;

    [Header("Passthrough Block")]
    [Tooltip("The same black box used by SensoryManager — blocks vision until a mode is chosen")]
    public GameObject blackBox;

    [Header("UI Elements")]
    public GameObject menuRoot;          // the whole canvas/panel to hide after selection
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI mirrorButtonLabel;
    public TextMeshProUGUI maskButtonLabel;

    [Header("Settings")]
    public string titleMessage = "Are You Real Experiment";

    void Start()
    {
        if (titleText != null)
            titleText.text = titleMessage;

        if (mirrorButtonLabel != null)
            mirrorButtonLabel.text = "MIRROR";

        if (maskButtonLabel != null)
            maskButtonLabel.text = "MASK";

        // Menu is visible by default at app start
        if (menuRoot != null)
            menuRoot.SetActive(true);

        // Block passthrough/vision until the user picks a mode —
        // the menu itself is a World Space canvas so it's still visible
        // even with the black box active (canvas renders in front of it)
        if (blackBox != null)
            blackBox.SetActive(true);
    }

    // ─── BUTTON CALLBACKS — wire these to Button On Click() ────────

    public void OnMirrorSelected()
    {
        if (trialReceiver != null)
            trialReceiver.SetMirrorMode(true);

        Debug.Log("[StartupMenu] MIRROR mode selected");
        ConfirmSelection();
    }

    public void OnMaskSelected()
    {
        if (trialReceiver != null)
            trialReceiver.SetMirrorMode(false);

        Debug.Log("[StartupMenu] MASK mode selected");
        ConfirmSelection();
    }

    void ConfirmSelection()
    {
        StartCoroutine(UnblindAfterDelay());
    }

    void HideMenu()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    System.Collections.IEnumerator UnblindAfterDelay()
    {
        // Hide the menu immediately
        HideMenu();

        // Keep the black box active while waiting
        yield return new WaitForSeconds(2f);

        // Remove the black box
        if (blackBox != null)
            
        // Give control back to the SensoryManager
        if (sensoryManager != null)
            sensoryManager.ReleaseControlFromMenu();
    }
}