using UnityEngine;
using UnityEngine.UI;

public class RestartPanel : MonoBehaviour
{
    public Button RestartButton;

    public void Start()
    {
        if (RestartButton != null)
        {
            RestartButton.onClick.AddListener(RestartButtonPressed);
        }
    }

    public void RestartButtonPressed()
    {
        RestartButton.interactable = false;
        StageManager.Instance.ReloadScene();
    }
}
