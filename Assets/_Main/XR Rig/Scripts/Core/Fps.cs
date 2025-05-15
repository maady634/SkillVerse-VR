using UnityEngine;
using System.Collections;
using TMPro;

public class Fps : MonoBehaviour
{
    private float count;

    TextMeshProUGUI fpsText;

    private void Awake()
    {
        fpsText = GetComponent<TextMeshProUGUI>();
    }

    private IEnumerator Start()
    {
        GUI.depth = 2;
        while (true)
        {
            count = 1f / Time.unscaledDeltaTime;

            fpsText.text = Mathf.Round(count).ToString();

            yield return new WaitForSeconds(0.1f);
        }
    }
}