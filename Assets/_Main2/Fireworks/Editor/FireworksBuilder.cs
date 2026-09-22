using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the Firework prefab (Particle System port of the Brackeys VFX Graph fireworks)
/// and scatters launchers around the market in the Festival scene.
/// </summary>
public static class FireworksBuilder
{
    private const string Folder = "Assets/_Main2/Fireworks";
    private const string TexturePath = Folder + "/FireworkSpark.png";
    private const string MaterialPath = Folder + "/FireworkAdditive.mat";
    private const string PrefabPath = Folder + "/Firework Launcher.prefab";
    private const string ScenePath = "Assets/_Main2/Festival.unity";
    private const string RootName = "-- Fireworks --";
    private const int LauncherCount = 8;

    // flash, burst start, burst end
    private static readonly Color[][] Palettes =
    {
        new[] { Color.white, new Color(0.2f, 1f, 1f), new Color(0.8f, 0.3f, 1f) },     // cyan -> purple (video)
        new[] { Color.white, new Color(1f, 0.8f, 0.2f), new Color(1f, 0.2f, 0.1f) },   // gold -> red
        new[] { Color.white, new Color(0.3f, 1f, 0.3f), new Color(1f, 1f, 0.3f) },     // green -> yellow
        new[] { Color.white, new Color(1f, 0.35f, 0.8f), new Color(0.6f, 0.6f, 1f) },  // pink -> lavender
        new[] { Color.white, new Color(0.25f, 0.5f, 1f), new Color(0.3f, 1f, 1f) },    // blue -> cyan
        new[] { Color.white, new Color(1f, 0.25f, 0.2f), new Color(1f, 0.65f, 0.1f) }, // red -> orange
        new[] { Color.white, new Color(1f, 1f, 0.9f), new Color(1f, 0.75f, 0.3f) },    // silver -> gold
        new[] { Color.white, new Color(0.7f, 0.3f, 1f), new Color(1f, 0.4f, 0.7f) },   // purple -> pink
    };

    [MenuItem("Tools/SkillVerse/Fireworks/Build Prefab and Place in Festival Scene")]
    public static void BuildAndPlace()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var prefab = BuildPrefab();
        PlaceLaunchers(prefab);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Fireworks] Built prefab and placed launchers in " + ScenePath);
    }

    [MenuItem("Tools/SkillVerse/Fireworks/Rebuild Prefab Only")]
    public static void RebuildPrefabOnly() => BuildPrefab();

    // ---------------------------------------------------------------- assets

    private static Texture2D BuildTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
            // bright core + soft halo (fakes bloom since the project has HDR/post off)
            float a = Mathf.Pow(1f - r, 3f) * 0.75f + Mathf.Pow(Mathf.Clamp01(1f - r * 2.5f), 2f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }
        File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TexturePath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    private static Material BuildMaterial(Texture2D tex)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f); // transparent
        mat.SetFloat("_Blend", 2f);   // additive
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------- prefab

    private static GameObject BuildPrefab()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/_Main2", "Fireworks");

        var mat = BuildMaterial(BuildTexture());

        // Step 1 - Rockets
        var root = new GameObject("Firework Launcher");
        var rockets = root.AddComponent<ParticleSystem>();
        {
            var main = rockets.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(24f, 30f);
            main.startSize = 0.35f;
            main.startColor = new Color(1f, 0.9f, 0.45f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;

            var emission = rockets.emission;
            emission.rateOverTime = 0f; // FireworkLauncher calls Emit()

            var shape = rockets.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 3.5f; // spread of launch points (video used a 7m line)
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }
        SetupRenderer(rockets, mat);

        // Step 3 - Rocket trail (Birth event)
        var rocketTrail = CreateChild(root, "Rocket Trail");
        {
            var main = rocketTrail.main;
            main.loop = true;
            main.startLifetime = 0.35f;
            main.startSpeed = 0.4f;
            main.startSize3D = true;
            main.startSizeX = 0.18f;
            main.startSizeY = 0.54f; // stretched on Y like the video's Set Scale Y = 3
            main.startSizeZ = 0.18f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;

            var emission = rocketTrail.emission;
            emission.rateOverTime = 90f;

            var shape = rocketTrail.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var col = rocketTrail.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(new Color(1f, 0.65f, 0.2f), new Color(0.8f, 0.3f, 0.05f));
        }
        SetupRenderer(rocketTrail, mat);

        // Step 2 - Explosion (Death event)
        var explosion = CreateChild(root, "Explosion");
        {
            var main = explosion.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.gravityModifier = 0.1f; // gravity -1 in the video
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1000;

            var emission = explosion.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 120) });

            var shape = explosion.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            // Linear drag (air resistance)
            var limit = explosion.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.drag = 3f;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }
        SetupRenderer(explosion, mat);

        // Step 4 - Explosion trails (Birth event on explosion particles, rate over time)
        var explosionTrail = CreateChild(explosion.gameObject, "Explosion Trail");
        {
            var main = explosionTrail.main;
            main.loop = true;
            main.startLifetime = 0.5f;
            main.startSpeed = 0.2f;
            main.startSize = 0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 5000;

            var emission = explosionTrail.emission;
            emission.rateOverTime = 25f;

            var shape = explosionTrail.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var col = explosionTrail.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(new Color(1f, 0.45f, 0.15f), new Color(0.7f, 0.15f, 0.05f));
        }
        SetupRenderer(explosionTrail, mat);

        // Extra - big soft flash at the burst point (stands in for the HDR bloom pop)
        var flash = CreateChild(root, "Flash");
        {
            var main = flash.main;
            main.loop = true;
            main.startLifetime = 0.3f;
            main.startSpeed = 0f;
            main.startSize = 9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 10;

            var emission = flash.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var flashShape = flash.shape;
            flashShape.enabled = false;
        }
        SetupRenderer(flash, mat);

        // Wire up GPU-event equivalents
        var rocketEvents = rockets.subEmitters;
        rocketEvents.enabled = true;
        rocketEvents.AddSubEmitter(rocketTrail, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
        rocketEvents.AddSubEmitter(explosion, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
        rocketEvents.AddSubEmitter(flash, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);

        var explosionEvents = explosion.subEmitters;
        explosionEvents.enabled = true;
        explosionEvents.AddSubEmitter(explosionTrail, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);

        var launcher = root.AddComponent<FireworkLauncher>();
        launcher.SetReferences(explosion, flash);
        launcher.ApplyColours();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    private static ParticleSystem CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        var shape = ps.shape;
        shape.enabled = false;
        return ps;
    }

    private static void SetupRenderer(ParticleSystem ps, Material mat)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.maxParticleSize = 2f;
    }

    private static ParticleSystem.MinMaxGradient Fade(Color from, Color to)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    // ---------------------------------------------------------------- scene

    private static void PlaceLaunchers(GameObject prefab)
    {
        var sceneRoots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var old in sceneRoots.Where(g => g.name == RootName))
            Object.DestroyImmediate(old);

        // Main area = where the player stands during the festival training.
        var player = Object.FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
        var centre = player != null ? player.transform.position : Vector3.zero;
        Debug.Log($"[Fireworks] Ringing launchers around {centre}");

        var parent = new GameObject(RootName).transform;
        var rng = new System.Random(1234);
        for (int i = 0; i < LauncherCount; i++)
        {
            float angle = (i + 0.5f) / LauncherCount * Mathf.PI * 2f + (float)(rng.NextDouble() - 0.5) * 0.3f;
            float radius = i % 2 == 0 ? 28f : 36f;
            var pos = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"Firework Launcher ({i + 1})";
            go.transform.position = pos;

            var launcher = go.GetComponent<FireworkLauncher>();
            var palette = Palettes[i % Palettes.Length];
            launcher.flashColor = palette[0];
            launcher.burstStartColor = palette[1];
            launcher.burstEndColor = palette[2];
            launcher.startDelay = new Vector2(0.5f + i * 0.4f, 2f + i * 0.6f);
            launcher.ApplyColours();
            PrefabUtility.RecordPrefabInstancePropertyModifications(launcher);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
                PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
        }
    }
}
