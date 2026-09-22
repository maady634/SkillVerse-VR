using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SkillVerse.EditorTools
{
    /// <summary>
    /// Built-in -> URP migration. Run from the menu or in batch mode:
    /// Unity -batchmode -quit -projectPath . -executeMethod SkillVerse.EditorTools.URPMigration.ConvertProject
    /// Baked lighting (LightingData + lightmaps) is never regenerated; only materials, shaders and
    /// pipeline settings change, so the baked look of the scenes is preserved.
    /// </summary>
    public static class URPMigration
    {
        public const string FestivalScenePath = "Assets/_Main2/Festival.unity";
        const string SettingsFolder = "Assets/Settings/URP";
        const string PipelineAssetPath = SettingsFolder + "/SkillVerse-URP.asset";
        const string RendererAssetPath = SettingsFolder + "/SkillVerse-URP_Renderer.asset";
        const string BuiltinExtraPath = "Resources/unity_builtin_extra";
        // Content already authored for URP (linear light intensity) - never compensate its lights
        static readonly string[] k_UrpNativeFolders = { "Assets/UnityTechnologies/ParticlePack/" };

        // Passes URP draws with the error (pink) shader
        static readonly HashSet<string> k_LegacyLightModes = new HashSet<string>
            { "Always", "ForwardBase", "ForwardAdd", "PrepassBase", "PrepassFinal", "Vertex", "VertexLMRGBM", "VertexLM", "Deferred" };
        // Passes URP forward rendering draws
        static readonly HashSet<string> k_UrpLightModes = new HashSet<string>
            { "", "SRPDefaultUnlit", "UniversalForward", "UniversalForwardOnly", "Universal2D" };

        static readonly StringBuilder s_Log = new StringBuilder();

        [MenuItem("Tools/URP Migration/Convert Project To URP")]
        public static void ConvertProject()
        {
            s_Log.Clear();
            // Built-in treated light intensity in gamma space; URP forces linear intensity.
            // Only compensate on the first run (while built-in is still active) so re-runs are safe.
            bool compensateLights = GraphicsSettings.defaultRenderPipeline == null && !GraphicsSettings.lightsUseLinearIntensity
                                    && PlayerSettings.colorSpace == ColorSpace.Linear;
            bool disableColorTemperature = GraphicsSettings.defaultRenderPipeline == null && !GraphicsSettings.lightsUseColorTemperature;

            var pipeline = EnsurePipelineAsset();
            AssignPipeline(pipeline);

            Converters.RunInBatchMode(ConverterContainerId.BuiltInToURP,
                new List<ConverterId> { ConverterId.Material, ConverterId.AnimationClip }, ConverterFilter.Inclusive);
            Log("Ran URP Material + AnimationClip converters");

            ReimportModelsWithEmbeddedMaterials();
            FixUnconvertedMaterials(pipeline);
            FixScenesAndPrefabs(pipeline, compensateLights, disableColorTemperature);

            AssetDatabase.SaveAssets();
            Verify();
            WriteLog("URPMigration_Convert.txt");
        }

        // ---------------------------------------------------------------- pipeline asset

        static UniversalRenderPipelineAsset EnsurePipelineAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (asset == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
                if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets/Settings", "URP");

                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
                ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);

                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, PipelineAssetPath);
                Log("Created " + PipelineAssetPath);
            }

            // Tuned for standalone VR (Quest) + baked lighting
            var so = new SerializedObject(asset);
            so.FindProperty("m_SupportsHDR").boolValue = false;                 // no post-processing in use; saves bandwidth on Quest
            so.FindProperty("m_MSAA").intValue = (int)MsaaQuality._4x;
            so.FindProperty("m_RenderScale").floatValue = 1f;
            so.FindProperty("m_RequireDepthTexture").boolValue = false;
            so.FindProperty("m_RequireOpaqueTexture").boolValue = false;
            so.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            so.FindProperty("m_MainLightShadowmapResolution").intValue = (int)UnityEngine.Rendering.Universal.ShadowResolution._2048;
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 4;
            so.FindProperty("m_ShadowDistance").floatValue = 50f;
            so.FindProperty("m_ShadowCascadeCount").intValue = 1;
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.FindProperty("m_MixedLightingSupported").boolValue = true;      // required for Mixed lights + baked indirect
            so.FindProperty("m_UseSRPBatcher").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void AssignPipeline(RenderPipelineAsset pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Log($"Assigned {pipeline.name} to Graphics settings and {QualitySettings.names.Length} quality levels");
        }

        // ---------------------------------------------------------------- materials

        /// Embedded (read-only) model materials are regenerated by URP's model importers once URP is active.
        static void ReimportModelsWithEmbeddedMaterials()
        {
            int count = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                    if (importer.materialImportMode == ModelImporterMaterialImportMode.None) continue;
                    if (!AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Any(m => IsBrokenInUrp(m.shader))) continue;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Log($"Reimported {count} models with embedded built-in materials");
        }

        /// Materials the stock converter doesn't know about (legacy particle shaders etc.).
        static void FixUnconvertedMaterials(UniversalRenderPipelineAsset pipeline)
        {
            var particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || !IsBrokenInUrp(mat.shader)) continue;

                var oldShader = mat.shader ? mat.shader.name : "<null>";
                if (oldShader.Contains("Particles"))
                    ConvertToParticleUnlit(mat, particleUnlit, oldShader);
                else
                    ConvertToLit(mat, lit);
                EditorUtility.SetDirty(mat);
                Log($"Manually converted {path}: {oldShader} -> {mat.shader.name}");
            }
        }

        static void ConvertToParticleUnlit(Material mat, Shader shader, string oldShader)
        {
            var tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            var st = mat.HasProperty("_MainTex") ? new Vector4(mat.mainTextureScale.x, mat.mainTextureScale.y, mat.mainTextureOffset.x, mat.mainTextureOffset.y) : new Vector4(1, 1, 0, 0);
            var tint = mat.HasProperty("_TintColor") ? mat.GetColor("_TintColor") * 2f : (mat.HasProperty("_Color") ? mat.color : Color.white);
            int queue = mat.renderQueue;

            mat.shader = shader;
            mat.SetTexture("_BaseMap", tex);
            mat.SetVector("_BaseMap_ST", st);
            mat.SetColor("_BaseColor", tint);
            // _Blend: 0 Alpha, 1 Premultiply, 2 Additive, 3 Multiply
            float blend = oldShader.Contains("Additive") ? 2 : oldShader.Contains("Multiply") ? 3 : oldShader.Contains("Premultiply") ? 1 : 0;
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", blend);
            UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI.SetMaterialKeywords(mat);
            UnityEditor.BaseShaderGUI.SetMaterialKeywords(mat);
            mat.renderQueue = queue;
        }

        static void ConvertToLit(Material mat, Shader lit)
        {
            var color = mat.HasProperty("_Color") ? mat.color : Color.white;
            var tex = mat.HasProperty("_MainTex") ? mat.mainTexture : null;
            mat.shader = lit;
            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", tex);
            UnityEditor.BaseShaderGUI.SetMaterialKeywords(mat);
        }

        // ---------------------------------------------------------------- scenes & prefabs

        static void FixScenesAndPrefabs(UniversalRenderPipelineAsset pipeline, bool compensateLights, bool disableColorTemperature)
        {
            // Prefab assets first, so scene instances inherit the fix instead of getting overrides
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadMainAssetAtPath(path)) is PrefabAssetType.Model or PrefabAssetType.NotAPrefab)
                    continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changes = FixHierarchy(root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject), pipeline,
                        compensateLights, disableColorTemperature, path, inScene: false);
                    if (changes > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            foreach (var path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
                int changes = FixHierarchy(objects, pipeline, compensateLights, disableColorTemperature, path, inScene: true);
                // Saving does not touch baked data: m_LightingDataAsset and per-renderer lightmap indices are kept as-is
                if (changes > 0) EditorSceneManager.SaveScene(scene);
            }
        }

        static int FixHierarchy(IEnumerable<GameObject> objects, UniversalRenderPipelineAsset pipeline, bool compensateLights,
            bool disableColorTemperature, string context, bool inScene)
        {
            int changes = 0;
            foreach (var go in objects)
            {
                // Built-in default materials (Default-Material etc.) are read-only and use built-in shaders
                foreach (var renderer in go.GetComponents<Renderer>())
                {
                    var mats = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null || AssetDatabase.GetAssetPath(m) != BuiltinExtraPath || !IsBrokenInUrp(m.shader)) continue;
                        mats[i] = renderer is ParticleSystemRenderer ? pipeline.defaultParticleMaterial
                                : renderer is LineRenderer or TrailRenderer ? pipeline.defaultLineMaterial
                                : pipeline.defaultMaterial;
                        changed = true;
                    }
                    if (changed)
                    {
                        renderer.sharedMaterials = mats;
                        EditorUtility.SetDirty(renderer);
                        changes++;
                        Log($"{context}: {GetPath(go)} built-in default material -> URP default");
                    }
                }

                var light = go.GetComponent<Light>();
                if (light == null) continue;
                // In scenes, prefab-instance lights are fixed via their prefab asset unless the value is overridden
                bool intensityOwnedHere = !inScene || !PrefabUtility.IsPartOfPrefabInstance(light) || IsOverridden(light, "m_Intensity");
                bool urpNative = k_UrpNativeFolders.Any(f => context.StartsWith(f, System.StringComparison.Ordinal));
                if (compensateLights && intensityOwnedHere && !urpNative)
                {
                    float before = light.intensity;
                    light.intensity = Mathf.GammaToLinearSpace(before);
                    EditorUtility.SetDirty(light);
                    changes++;
                    Log($"{context}: {GetPath(go)} light intensity {before} -> {light.intensity} (gamma->linear intensity)");
                }
                bool tempOwnedHere = !inScene || !PrefabUtility.IsPartOfPrefabInstance(light) || IsOverridden(light, "m_UseColorTemperature");
                if (disableColorTemperature && tempOwnedHere && light.useColorTemperature)
                {
                    light.useColorTemperature = false;   // was ignored by built-in (project-wide setting off)
                    EditorUtility.SetDirty(light);
                    changes++;
                    Log($"{context}: {GetPath(go)} disabled color temperature");
                }
            }
            return changes;
        }

        static bool IsOverridden(Object obj, string property)
        {
            var mods = PrefabUtility.GetPropertyModifications(obj);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            return mods != null && mods.Any(m => m.target == source && m.propertyPath == property);
        }

        // ---------------------------------------------------------------- verification

        /// True when URP would render this shader pink (error shader) or not at all.
        public static bool IsBrokenInUrp(Shader shader)
        {
            if (shader == null || shader.name == "Hidden/InternalErrorShader" || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                return true;
            bool anyUrpPass = false;
            for (int i = 0; i < shader.passCount; i++)
            {
                var lightMode = shader.FindPassTagValue(i, new ShaderTagId("LightMode")).name ?? "";
                if (k_LegacyLightModes.Contains(lightMode)) return true;
                if (k_UrpLightModes.Contains(lightMode)) anyUrpPass = true;
            }
            return !anyUrpPass;
        }

        [MenuItem("Tools/URP Migration/Verify (pink material report)")]
        public static void Verify()
        {
            var festivalDeps = new HashSet<string>(AssetDatabase.GetDependencies(FestivalScenePath, true));
            int broken = 0, festivalBroken = 0, total = 0;
            var lines = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Distinct())
                {
                    total++;
                    if (!IsBrokenInUrp(mat.shader)) continue;
                    broken++;
                    bool fest = festivalDeps.Contains(path);
                    if (fest) festivalBroken++;
                    lines.Add($"BROKEN {(fest ? "[Festival] " : "")}{path}::{mat.name} ({(mat.shader ? mat.shader.name : "<null>")})");
                }
            }
            foreach (var path in festivalDeps.Where(p => AssetImporter.GetAtPath(p) is ModelImporter))
                foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                    if (IsBrokenInUrp(mat.shader)) { festivalBroken++; lines.Add($"BROKEN [Festival] {path}::{mat.name} ({mat.shader.name}) (embedded)"); }

            Log($"VERIFY: pipeline={GraphicsSettings.defaultRenderPipeline?.name}, materials={total}, broken={broken}, festivalBroken={festivalBroken}");
            foreach (var l in lines) Log(l);
            if (Application.isBatchMode) WriteLog("URPMigration_Verify.txt");
        }

        /// Renders the Festival scene from its main camera to Logs/Festival_URP.png (needs a graphics device, i.e. no -nographics).
        [MenuItem("Tools/URP Migration/Capture Festival Preview")]
        public static void CaptureFestival()
        {
            EditorSceneManager.OpenScene(FestivalScenePath, OpenSceneMode.Single);
            var cam = Camera.main != null ? Camera.main : Object.FindObjectsOfType<Camera>().FirstOrDefault(c => c.targetTexture == null);
            if (cam == null) { Debug.LogError("[URPMigration] No camera found in Festival scene"); return; }

            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.aspect = 1600f / 900f;
            cam.Render();   // warm-up: the first batch-mode render can have uninitialised globals (e.g. fog)
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prev;

            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Festival_URP.png");
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log("[URPMigration] Captured " + outPath + " from camera " + cam.name);
        }

        public static void VerifyAndCapture()
        {
            s_Log.Clear();
            Verify();
            CaptureFestival();
        }

        // ---------------------------------------------------------------- utils

        static string GetPath(GameObject go) => go.transform.parent == null ? go.name : GetPath(go.transform.parent.gameObject) + "/" + go.name;

        static void Log(string msg)
        {
            s_Log.AppendLine(msg);
            Debug.Log("[URPMigration] " + msg);
        }

        static void WriteLog(string file)
        {
            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", file);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, s_Log.ToString());
        }
    }
}
