using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
   [Header("Scene Configuration")]
   [SerializeField] private string blackSceneName = "";
   void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch) || 
            OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            Debug.Log("Transitioning...");
            TransitionToBlackScreen();
        }
    }

    private void TransitionToWaitingScreen()
    {
        // Loads the waiting scene set it up in scenes
        //SceneManager.LoadScene(blackSceneName);
    }

}
