using System.Collections;
using System.IO;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[CustomEditor(typeof(StageSO))]
public class StageSOEditor : Editor
{
    private const string TTS_URL = "http://127.0.0.1:5000/tts";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        GUILayout.Label("Audio Generation", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Audios (Chatterbox)"))
        {
            StageSO stage = (StageSO)target;
            Debug.Log($"[StageSO TTS] ▶ Button clicked for Stage: {stage.name}");

            EditorCoroutineUtility.StartCoroutineOwnerless(
                GenerateAllAudios(stage)
            );
        }
    }

    private IEnumerator GenerateAllAudios(StageSO stage)
    {
        string baseDir = $"Assets/GeneratedAudio/StageSO/{stage.name}";
        Directory.CreateDirectory(baseDir);

        Debug.Log($"[StageSO TTS] Output directory: {baseDir}");

        yield return GenerateAndAssign(
            "StepAudio",
            stage.stepSubtitle,
            baseDir + "/StepAudio.wav",
            clip => stage.stepAudio = clip
        );

        yield return GenerateAndAssign(
            "StepAudio2",
            stage.stepSubtitle2,
            baseDir + "/StepAudio2.wav",
            clip => stage.stepAudio2 = clip
        );

        yield return GenerateAndAssign(
            "HelpAudio",
            stage.helpText,
            baseDir + "/HelpAudio.wav",
            clip => stage.helpAudio = clip
        );

        yield return GenerateAndAssign(
            "CompletionAudio",
            stage.completionText,
            baseDir + "/CompletionAudio.wav",
            clip => stage.completionAudio = clip
        );

        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();

        Debug.Log($"[StageSO TTS] ✅ All audio generation COMPLETE for {stage.name}");
    }

    private IEnumerator GenerateAndAssign(
        string label,
        string text,
        string assetPath,
        System.Action<AudioClip> assignClip
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning($"[StageSO TTS] ⚠ {label} skipped (empty text)");
            yield break;
        }

        Debug.Log($"[StageSO TTS] 🔊 Generating {label}...");
        Debug.Log($"[StageSO TTS] Text: \"{text}\"");

        string json = "{\"text\":\"" + text.Replace("\"", "\\\"") + "\"}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(TTS_URL, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"[StageSO TTS] 📡 Sending request to Chatterbox...");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[StageSO TTS] ❌ {label} request failed: {req.error}");
                yield break;
            }

            Debug.Log($"[StageSO TTS] 📥 Audio received ({req.downloadHandler.data.Length} bytes)");

            File.WriteAllBytes(assetPath, req.downloadHandler.data);
            Debug.Log($"[StageSO TTS] 💾 Saved WAV → {assetPath}");
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

        if (clip != null)
        {
            assignClip(clip);
            Debug.Log($"[StageSO TTS] ✅ {label} imported & assigned");
        }
        else
        {
            Debug.LogError($"[StageSO TTS] ❌ Failed to import AudioClip: {assetPath}");
        }
    }
}
