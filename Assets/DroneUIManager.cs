using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach this to a Canvas GameObject (World Space).
/// Assign the DroneController and the four mode buttons in the Inspector.
/// </summary>
public class DroneUIManager : MonoBehaviour
{
    [Header("Drone Reference")]
    public DroneController droneController;

    [Header("Mode Buttons")]
    public Button stationaryButton;
    public Button activeButton;
    public Button mirrorButton;
    public Button overlayButton;

    [Header("Status Display")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI coordinatesText;

    [Header("Button Colors")]
    public Color activeColor = new Color(0.2f, 0.8f, 0.4f);     // green
    public Color inactiveColor = new Color(0.15f, 0.15f, 0.2f); // dark
    public Color textActiveColor = Color.black;
    public Color textInactiveColor = Color.white;

    private Button currentActiveButton;

    void Start()
    {
        if (droneController == null)
        {
            Debug.LogError("[DroneUIManager] DroneController not assigned!");
            return;
        }

        // Wire up button listeners
        stationaryButton.onClick.AddListener(() => OnModeSelected(DroneMode.Stationary, stationaryButton));
        activeButton.onClick.AddListener(() => OnModeSelected(DroneMode.Active, activeButton));
        mirrorButton.onClick.AddListener(() => OnModeSelected(DroneMode.CoordinatesMirror, mirrorButton));
        overlayButton.onClick.AddListener(() => OnModeSelected(DroneMode.CoordinatesOverlay, overlayButton));

        // Set default
        OnModeSelected(DroneMode.Stationary, stationaryButton);
    }

    void Update()
    {
        UpdateStatusText();
    }

    void OnModeSelected(DroneMode mode, Button selectedButton)
    {
        droneController.SetMode(mode);

        // Reset all buttons
        SetButtonStyle(stationaryButton, false);
        SetButtonStyle(activeButton, false);
        SetButtonStyle(mirrorButton, false);
        SetButtonStyle(overlayButton, false);

        // Highlight selected
        SetButtonStyle(selectedButton, true);
        currentActiveButton = selectedButton;
    }

    void SetButtonStyle(Button button, bool isActive)
    {
        if (button == null) return;

        // Background color
        var colors = button.colors;
        colors.normalColor = isActive ? activeColor : inactiveColor;
        colors.highlightedColor = isActive ? activeColor * 1.1f : inactiveColor * 1.3f;
        colors.selectedColor = isActive ? activeColor : inactiveColor;
        button.colors = colors;

        // Text color
        var tmp = button.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
            tmp.color = isActive ? textActiveColor : textInactiveColor;
    }

    void UpdateStatusText()
    {
        if (statusText == null || droneController == null) return;

        DroneMode mode = droneController.GetMode();
        string modeLabel = mode switch
        {
            DroneMode.Stationary => "STATIONARY",
            DroneMode.Active => "ACTIVE",
            DroneMode.CoordinatesMirror => "MIRROR",
            DroneMode.CoordinatesOverlay => "OVERLAY",
            _ => "UNKNOWN"
        };

        statusText.text = $"Mode: {modeLabel}";

        // Show drone position
        if (coordinatesText != null)
        {
            Vector3 pos = droneController.transform.position;
            coordinatesText.text = $"Pos: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})";
        }
    }
}
