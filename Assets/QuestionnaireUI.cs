using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to a GameObject inside your ReticleCanvas.
/// Controls the questionnaire panel that appears 5 seconds after each trial.
///
/// Shows:
///   "Which drone is real?"
///   Slider 1-7 with current value displayed
///   Confirm button
///
/// On confirm: sends the answer to Python via TCPClient, hides itself.
/// Call ShowQuestionnaire() to show, HideQuestionnaire() to hide.
/// TrialReceiver calls ShowQuestionnaire() after the 5s delay.
/// </summary>
public class QuestionnaireUI : MonoBehaviour
{
    [Header("References")]
    public TCPClient tcpClient;

    [Header("UI Elements")]
    public GameObject questionnairePanel;   // root panel to show/hide
    public TextMeshProUGUI questionText;    // "Which drone is real?"
    public Slider answerSlider;             // slider 1-7
    public TextMeshProUGUI sliderValueText; // shows current slider value e.g. "4"
    public TextMeshProUGUI leftLabel;       // "Left" label on left end
    public TextMeshProUGUI rightLabel;      // "Right" label on right end
    public Button confirmButton;            // confirm button

    [Header("Settings")]
    public string question = "Which drone is real?";
    public float delayAfterTrialSeconds = 5f;

    // Internal
    private float timer = 0f;
    private bool timerRunning = false;

    void Start()
    {
        // Force panel active first so this Start() actually runs
        // (if panel is inactive, Start() never gets called)
        if (questionnairePanel != null)
            questionnairePanel.SetActive(true);

        // Setup slider
        if (answerSlider != null)
        {
            answerSlider.minValue = 1;
            answerSlider.maxValue = 7;
            answerSlider.wholeNumbers = true;
            answerSlider.value = 4;
            answerSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        if (questionText != null)
            questionText.text = question;

        if (leftLabel != null)
            leftLabel.text = "Left\n(1)";

        if (rightLabel != null)
            rightLabel.text = "Right\n(7)";

        UpdateSliderText();

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        // Now hide it — hidden by default until a trial starts
        HideQuestionnaire();
        Debug.Log("[QuestionnaireUI] Initialized and hidden");
    }

    void Update()
    {
        if (!timerRunning) return;

        timer -= Time.deltaTime;
        Debug.Log($"[QuestionnaireUI] Timer: {timer:F1}s remaining");
        if (timer <= 0f)
        {
            timerRunning = false;
            ShowQuestionnaire();
        }
    }

    // ─── PUBLIC API — called by TrialReceiver ─────────────────────

    /// <summary>
    /// Call when a new trial packet arrives.
    /// Starts the countdown before showing the questionnaire.
    /// </summary>
    public void OnTrialStarted()
    {
        Debug.Log("[QuestionnaireUI] OnTrialStarted() called");
        HideQuestionnaire();
        timer = delayAfterTrialSeconds;
        timerRunning = true;
        Debug.Log($"[QuestionnaireUI] Timer started — will show in {delayAfterTrialSeconds}s");
    }

    // ─── INTERNAL ─────────────────────────────────────────────────

    void ShowQuestionnaire()
    {
        Debug.Log("[QuestionnaireUI] ShowQuestionnaire() called");
        if (questionnairePanel != null)
        {
            questionnairePanel.SetActive(true);
            Debug.Log("[QuestionnaireUI] Panel set active");
        }
        else
        {
            Debug.LogError("[QuestionnaireUI] questionnairePanel is NULL — not assigned!");
        }

        if (answerSlider != null)
            answerSlider.value = 4;

        UpdateSliderText();
    }

    void HideQuestionnaire()
    {
        timerRunning = false;

        if (questionnairePanel != null)
            questionnairePanel.SetActive(false);

        Debug.Log("[QuestionnaireUI] Questionnaire hidden");
    }

    void OnSliderChanged(float value)
    {
        UpdateSliderText();
    }

    void UpdateSliderText()
    {
        if (sliderValueText != null && answerSlider != null)
            sliderValueText.text = ((int)answerSlider.value).ToString();
    }

    void OnConfirm()
    {
        int answer = (int)answerSlider.value;

        // Send answer to Python via TCP
        if (tcpClient != null)
            tcpClient.SendAnswer(answer);

        HideQuestionnaire();

        Debug.Log($"[QuestionnaireUI] Confirmed answer: {answer}");
    }
}