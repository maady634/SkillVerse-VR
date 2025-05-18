using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreCard : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI TrainingScoreText;

    [SerializeField]
    TextMeshProUGUI AssessmentScoreText;

    [SerializeField]
    Slider TrainingSlider;

    [SerializeField]
    Slider AssessmentSlider;

    private void OnEnable()
    {
        int assScore = StageManager.Instance.AssessmentScore;
        AssessmentScoreText.text = assScore.ToString() + " %";

        // Invert value since slider visually behaves backwards
        AssessmentSlider.value = 1f - (assScore / 100f);

        int trainScore = StageManager.Instance.TrainingScore;
        TrainingScoreText.text = trainScore.ToString() + " %";

        // Invert value since slider visually behaves backwards
        TrainingSlider.value = 1f - (trainScore / 100f);
    }
}
