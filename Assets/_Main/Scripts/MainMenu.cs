using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void SelectBarrelActivity()
    {
        GameSession.SelectedActivity = ActivityType.BarrelActivity;
    }

    public void SelectNormalActivity()
    {
        GameSession.SelectedActivity = ActivityType.NormalActivity;
    }

    public void StartTutorial()
    {
        GameSession.SelectedMode = ModeType.Tutorial;
        LoadSelectedScene();
    }

    public void StartPractice()
    {
        GameSession.SelectedMode = ModeType.Practice;
        LoadSelectedScene();
    }

    void LoadSelectedScene()
    {
        if (GameSession.SelectedActivity == ActivityType.BarrelActivity)
        {
            SceneManager.LoadScene("FLT- Barrel");
        }
        else
        {
            SceneManager.LoadScene("FLT- Normal");
        }
    }
}