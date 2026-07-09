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
    public GameObject questionnairePanel;
    public TextMeshProUGUI questionText;
    public Slider answerSlider;
    public TextMeshProUGUI sliderValueText;
    public TextMeshProUGUI leftLabel;
    public TextMeshProUGUI rightLabel;
    public Button confirmButton;

    [Header("Waiting Screen")]
    public GameObject waitingPanel;  // Panel with "Please wait..." text

    [Header("Debug — assign any visible TMP text to see status in headset")]
    public TextMeshProUGUI debugText;

    [Header("Settings")]
    public string question = "Which drone is real?";
    public float delayAfterTrialSeconds = 5f;

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

        HideQuestionnaire();

        // Hide waiting panel by default
        if (waitingPanel != null)
            waitingPanel.SetActive(false);

        SetDebug("Initialized OK");
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

    void ShowQuestionnaire()
    {
        SetDebug("Showing panel...");
        if (questionnairePanel != null)
        {
            questionnairePanel.SetActive(true);
            SetDebug("Panel shown!");
        }
        else
        {
            SetDebug("ERROR: panel is null!");
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
        if (tcpClient != null)
            tcpClient.SendAnswer(answer);
        HideQuestionnaire();

        // Show waiting screen until next trial arrives
        if (waitingPanel != null)
            waitingPanel.SetActive(true);

        SetDebug($"Confirmed: {answer} — waiting for next trial");
    }

    public void OnTrialStarted()
    {
        SetDebug("OnTrialStarted called");

        // Hide waiting screen when new trial arrives
        if (waitingPanel != null)
            waitingPanel.SetActive(false);

        HideQuestionnaire();
        timer = delayAfterTrialSeconds;
        timerRunning = true;
    }
}