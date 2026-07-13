using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Attach to ReticleCanvas (always-active parent).
/// Shows 3 questions each with a slider 1-7.
/// On confirm: sends all 3 answers to Python via TCPClient.
///
/// Python receives: {"cmd":"ANSWER","q1":3,"q2":5,"q3":2}
///
/// Unity Setup:
/// - Assign 3 QuestionRow objects (each has a label TMP + Slider + ValueText TMP)
/// - Assign confirmButton and questionnairePanel
/// </summary>
public class QuestionnaireUI : MonoBehaviour
{
    [Header("References")]
    public TCPClient tcpClient;
    public HeadGazeReticle headGazeReticle; // to read gaze time on confirm

    [Header("Panel")]
    public GameObject questionnairePanel;
    public GameObject waitingPanel;

    [Header("Question 1 — Which drone is real?")]
    public TextMeshProUGUI q1Label;
    public Slider q1Slider;
    public TextMeshProUGUI q1ValueText;

    [Header("Question 2 — Which drone is closer?")]
    public TextMeshProUGUI q2Label;
    public Slider q2Slider;
    public TextMeshProUGUI q2ValueText;

    [Header("Question 3 — Which drone is more safe?")]
    public TextMeshProUGUI q3Label;
    public Slider q3Slider;
    public TextMeshProUGUI q3ValueText;

    [Header("Shared Labels (optional)")]
    public TextMeshProUGUI leftLabel;    // "Left (1)" shown once at bottom
    public TextMeshProUGUI rightLabel;   // "Right (7)" shown once at bottom

    [Header("Confirm Button")]
    public Button confirmButton;

    [Header("Timing")]
    public float delayAfterTrialSeconds = 5f;

    [Header("Debug")]
    public TextMeshProUGUI debugText;

    // Internal
    private float timer = 0f;
    private bool timerRunning = false;

    void SetDebug(string msg)
    {
        Debug.Log("[QuestionnaireUI] " + msg);
        if (debugText != null)
            debugText.text = "Q: " + msg;
    }

    void Start()
    {
        // Force panel active so Start() runs, then hide it
        if (questionnairePanel != null)
            questionnairePanel.SetActive(true);

        // Setup Q1
        SetupSlider(q1Slider, q1ValueText);
        if (q1Label != null) q1Label.text = "Which drone is real?";

        // Setup Q2
        SetupSlider(q2Slider, q2ValueText);
        if (q2Label != null) q2Label.text = "Which drone is closer?";

        // Setup Q3
        SetupSlider(q3Slider, q3ValueText);
        if (q3Label != null) q3Label.text = "Which drone is more safe?";

        // Shared labels
        if (leftLabel != null) leftLabel.text = "Left (1)";
        if (rightLabel != null) rightLabel.text = "Right (7)";

        // Confirm button
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        // Hide everything by default
        HideQuestionnaire();
        if (waitingPanel != null)
            waitingPanel.SetActive(false);

        SetDebug("Initialized OK");
    }

    void SetupSlider(Slider slider, TextMeshProUGUI valueText)
    {
        if (slider == null) return;
        slider.minValue = 1;
        slider.maxValue = 7;
        slider.wholeNumbers = true;
        slider.value = 4;
        slider.onValueChanged.AddListener(_ => UpdateValueText(slider, valueText));
        UpdateValueText(slider, valueText);
    }

    void UpdateValueText(Slider slider, TextMeshProUGUI valueText)
    {
        if (valueText != null && slider != null)
            valueText.text = ((int)slider.value).ToString();
    }

    void Update()
    {
        if (!timerRunning) return;

        timer -= Time.deltaTime;
        SetDebug($"Timer: {timer:F1}s");
        if (timer <= 0f)
        {
            timerRunning = false;
            ShowQuestionnaire();
        }
    }

    // ─── PUBLIC API ───────────────────────────────────────────────

    public void OnTrialStarted()
    {
        SetDebug("OnTrialStarted called");
        if (waitingPanel != null) waitingPanel.SetActive(false);
        HideQuestionnaire();
        timer = delayAfterTrialSeconds;
        timerRunning = true;
    }

    // ─── INTERNAL ─────────────────────────────────────────────────

    void ShowQuestionnaire()
    {
        // Reset all sliders to middle
        if (q1Slider != null) q1Slider.value = 4;
        if (q2Slider != null) q2Slider.value = 4;
        if (q3Slider != null) q3Slider.value = 4;

        if (questionnairePanel != null)
            questionnairePanel.SetActive(true);

        SetDebug("Panel shown!");
    }

    void HideQuestionnaire()
    {
        timerRunning = false;
        if (questionnairePanel != null)
            questionnairePanel.SetActive(false);
    }

    void OnConfirm()
    {
        int a1 = q1Slider != null ? (int)q1Slider.value : 4;
        int a2 = q2Slider != null ? (int)q2Slider.value : 4;
        int a3 = q3Slider != null ? (int)q3Slider.value : 4;
        float gazeTime = headGazeReticle != null ? headGazeReticle.GetGazeTime() : 0f;

        if (tcpClient != null)
            tcpClient.SendAnswers(a1, a2, a3, gazeTime);

        HideQuestionnaire();

        if (waitingPanel != null)
            waitingPanel.SetActive(true);

        SetDebug($"Confirmed: Q1={a1} Q2={a2} Q3={a3} Gaze={gazeTime:F1}s");
    }
}