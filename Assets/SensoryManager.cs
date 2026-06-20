using UnityEngine;

public class SensoryManager : MonoBehaviour
{
    public GameObject blackBox;
    public AudioSource whiteNoise;
    public AudioSource droneAudio;

    public void ApplyAffordanceString(string a)
    {
        a = a.ToUpper();

        bool v = a.Contains("V");
        bool h = a.Contains("H");
        bool au = a.Contains("A");

        SetVisual(v);
        SetAudio(au);
        SetHaptics(h);

        Debug.Log($"Affordance: {a} -> V:{v} A:{au} H:{h}");
    }

    void SetVisual(bool on)
    {
        if (blackBox != null)
            blackBox.SetActive(!on);
    }

    void SetAudio(bool on)
    {
        if (whiteNoise != null)
        {
            if (!on) whiteNoise.Play();
            else whiteNoise.Stop();
        }

        if (droneAudio != null)
        {
            if (on) droneAudio.Play();
            else droneAudio.Stop();
        }
    }

    void SetHaptics(bool on)
    {
        if (!on)
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        }
    }
}
