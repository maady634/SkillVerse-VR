using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
//using Meta.WitAi.TTS.UX;

using System.Collections.Generic;

using UnityEngine.UI;
//using Meta.WitAi.TTS.Utilities;

public class G_ApiManager : MonoBehaviour
{
    public TextMeshProUGUI question;
    public TextMeshProUGUI answer;

    //public VoiceCommandController Speaker;

    [SerializeField] public string prompt;
    private string gasURL = "https://script.google.com/macros/s/AKfycbxZosP_AYCNDpgr80515LJGf9I3G6ZDWLrNeaBVptz8wZvZde_Ozu4_2h-REI0jio3l/exec";

    [ContextMenu("Calling")]
    public void Calling()
    {
        StartCoroutine(SendDataToGAS());

        question.text = prompt;
    }

    private IEnumerator SendDataToGAS()
    {
        WWWForm form = new WWWForm();

        string actualPrompt = "Give precise answer in single sentance: " + question.text;
        form.AddField("parameter", actualPrompt);
        UnityWebRequest www = UnityWebRequest.Post(gasURL, form);

        yield return www.SendWebRequest();
        string response = "";

        if (www.result == UnityWebRequest.Result.Success)
        {
            response = www.downloadHandler.text;
        }
        else
        {
            response = "There is an error in connecting";
        }

        answer.text = response;
        //Speaker.SpeakText(response);
        print("Speaking");
        // Call SpeakText method to speak the answer
    }
}
