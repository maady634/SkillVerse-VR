using System.Collections;
using System.IO;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[CustomEditor(typeof(StageSO))]
public class StageSOEditor : Editor
{
    private const string TTS_URL = "http://localhost:5000/tts";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        GUILayout.Label("Audio Generation", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Audios (Chatterbox)"))
        {
            StageSO stage = (StageSO)target;
            EditorCoroutineUtility.StartCoroutineOwnerless(
                GenerateAllAudios(stage)
            );
        }
    }

    private IEnumerator GenerateAllAudios(StageSO stage)
    {
        string baseDir = $"Assets/GeneratedAudio/StageSO/{stage.name}";
        Directory.CreateDirectory(baseDir);

        // Each text → its target AudioClip setter
        yield return GenerateAndAssign(
            stage.stepSubtitle,
            baseDir + "/StepAudio.wav",
            clip => stage.stepAudio = clip
        );

        yield return GenerateAndAssign(
            stage.stepSubtitle2,
            baseDir + "/StepAudio2.wav",
            clip => stage.stepAudio2 = clip
        );

        yield return GenerateAndAssign(
            stage.helpText,
            baseDir + "/HelpAudio.wav",
            clip => stage.helpAudio = clip
        );

        yield return GenerateAndAssign(
            stage.completionText,
            baseDir + "/CompletionAudio.wav",
            clip => stage.completionAudio = clip
        );

        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();

        Debug.Log($"[StageSO] Audio generation complete for {stage.name}");
    }

    private IEnumerator GenerateAndAssign(
        string text,
        string assetPath,
        System.Action<AudioClip> assignClip
    )
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        string json =
            "{\"text\":\"" + text.Replace("\"", "\\\"") + "\"}";

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(TTS_URL, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Chatterbox TTS failed: " + req.error);
                yield break;
            }

            File.WriteAllBytes(assetPath, req.downloadHandler.data);
        }

        AssetDatabase.ImportAsset(assetPath);
        AssetDatabase.Refresh();

        AudioClip clip =
            AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

        if (clip != null)
            assignClip(clip);
        else
            Debug.LogError("Failed to import AudioClip: " + assetPath);
    }
}
