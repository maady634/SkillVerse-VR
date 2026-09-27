using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Wires the interactive half of the festival flow into the Festival scene: makes the Lighter
/// grabbable, turns the two Rockets and two Fire Showers into <see cref="CrackerTarget"/>s, and
/// builds the particle effects they set off. Follows the same approach as FireworksBuilder and
/// reuses its additive spark material.
///
/// Run it from Tools > SkillVerse > Festival > Build Cracker Interactions. It is safe to re-run.
/// </summary>
public static class FestivalInteractionBuilder
{
    private const string FlowFolder = "Assets/_Main2/FestivalFlow";
    private const string FxFolder = FlowFolder + "/FX";
    private const string SparkMaterialPath = "Assets/_Main2/Fireworks/FireworkAdditive.mat";
    private const string ScenePath = "Assets/_Main2/Festival.unity";

    private const string RootName = "-- Festival Crackers --";
    private const string FxRootName = "FX";
    private const string FuseRootName = "Fuses";
    private const string LighterName = "Lighter";
    private const string RocketName = "Rocket";
    private const string ShowerName = "FireShower";
    private const string TipName = "Tip";
    private const string HelpArrowName = "HelpArrow";

    [MenuItem("Tools/SkillVerse/Festival/Build Cracker Interactions")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorUtility.DisplayDialog(
                    "Festival scene is not open",
                    $"The active scene is '{scene.name}', not Festival.\n\n" +
                    "Open Festival now? Unsaved changes in the current scene will be lost.",
                    "Open Festival", "Cancel"))
                return;

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        if (BuildInScene(scene))
            Selection.activeGameObject = GameObject.Find(RootName);
    }

    /// <summary>
    /// Entry point for headless runs:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod FestivalInteractionBuilder.BuildBatch
    /// Opens the Festival scene, wires everything up and saves.
    /// </summary>
    public static void BuildBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (!BuildInScene(scene))
        {
            Debug.LogError("[Festival] Build failed - scene not saved.");
            EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Festival] Saved " + ScenePath);
    }

    private static bool BuildInScene(Scene scene)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
        if (material == null)
        {
            Debug.LogError($"[Festival] Missing {SparkMaterialPath}. " +
                           "Run Tools > SkillVerse > Fireworks first so the spark material exists.");
            return false;
        }

        EnsureFolder(FlowFolder, "FX");
        var fx = BuildEffectPrefabs(material);

        var lighter = FindByName(scene, LighterName).FirstOrDefault();
        if (lighter == null)
        {
            Debug.LogError($"[Festival] No GameObject called '{LighterName}' in the scene.");
            return false;
        }

        var rockets = FindByName(scene, RocketName);
        var showers = FindByName(scene, ShowerName);
        if (rockets.Count == 0 && showers.Count == 0)
        {
            Debug.LogError($"[Festival] Found no '{RocketName}' or '{ShowerName}' GameObjects.");
            return false;
        }

        var root = FindOrCreateRoot(scene, RootName);
        var fxRoot = FindOrCreateChild(root.transform, FxRootName);
        var fuseRoot = FindOrCreateChild(root.transform, FuseRootName);

        var audio = root.GetComponent<AudioSource>();
        if (audio == null) audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        var grab = SetUpLighter(lighter);

        var targets = new List<CrackerTarget>();
        for (int i = 0; i < rockets.Count; i++)
            targets.Add(SetUpTarget(rockets[i], CrackerTarget.CrackerType.Rocket, i + 1,
                                    fxRoot, fuseRoot, audio, fx));
        for (int i = 0; i < showers.Count; i++)
            targets.Add(SetUpTarget(showers[i], CrackerTarget.CrackerType.FireShower, i + 1,
                                    fxRoot, fuseRoot, audio, fx));

        var controller = root.GetComponent<FestivalCrackerController>();
        if (controller == null) controller = root.AddComponent<FestivalCrackerController>();
        controller.lighter = grab;
        controller.targets = targets.ToArray();
        controller.helpArrow = SetUpHelpArrow(scene, lighter);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[Festival] Wired {targets.Count} crackers " +
                  $"({rockets.Count} rocket, {showers.Count} fire shower) and the lighter. " +
                  "Save the scene to keep it.");
        return true;
    }

    // ------------------------------------------------------------------ scene

    private static LighterGrab SetUpLighter(GameObject lighter)
    {
        var body = lighter.GetComponent<Rigidbody>();
        if (body == null) body = lighter.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        // Interpolation must stay off: it writes the body's own world pose back onto the
        // Transform every frame, which overrides parenting once the lighter is in the hand.
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // The lighter is a very thin cylinder; widen its collider so the direct interactor can
        // actually catch it, and make it cover the full length of the stick.
        var box = lighter.GetComponent<BoxCollider>();
        if (box == null) box = lighter.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.center = new Vector3(0f, 0.2f, 0f);
        box.size = new Vector3(6f, 1.6f, 6f);

        var grabbable = lighter.GetComponent<XRGrabInteractable>();
        if (grabbable == null) grabbable = lighter.AddComponent<XRGrabInteractable>();
        grabbable.enabled = true;
        grabbable.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grabbable.throwOnDetach = false;
        grabbable.retainTransformParent = true;
        grabbable.useDynamicAttach = false;

        // Burning end: top of the primitive cylinder in local space.
        var tip = FindOrCreateChild(lighter.transform, TipName);
        tip.localPosition = new Vector3(0f, 1f, 0f);
        tip.localRotation = Quaternion.identity;

        var grab = lighter.GetComponent<LighterGrab>();
        if (grab == null) grab = lighter.AddComponent<LighterGrab>();
        grab.tip = tip;

        EditorUtility.SetDirty(lighter);
        return grab;
    }

    /// <summary>
    /// Finds the designer-placed help arrow - it sits at the scene root above the lighter, but
    /// a child of the lighter is accepted too - gives it the bob loop and hands it back so the
    /// controller can show and hide it. A missing arrow is not an error; the flow still works.
    /// </summary>
    private static GameObject SetUpHelpArrow(Scene scene, GameObject lighter)
    {
        var arrow = lighter.transform.Find(HelpArrowName);
        GameObject go = arrow != null ? arrow.gameObject : FindByName(scene, HelpArrowName).FirstOrDefault();

        if (go == null)
        {
            Debug.LogWarning($"[Festival] No '{HelpArrowName}' in the scene - skipping the pick-up hint.");
            return null;
        }

        if (go.GetComponent<HelpArrowBobber>() == null) go.AddComponent<HelpArrowBobber>();

        // The controller turns it on when the pick-up step starts.
        go.SetActive(false);

        EditorUtility.SetDirty(go);
        return go;
    }

    private static CrackerTarget SetUpTarget(GameObject go, CrackerTarget.CrackerType type,
                                             int index, Transform fxRoot, Transform fuseRoot,
                                             AudioSource audio, EffectSet fx)
    {
        var target = go.GetComponent<CrackerTarget>();
        if (target == null) target = go.AddComponent<CrackerTarget>();

        target.type = type;
        target.effectRoot = fxRoot;
        target.audioSource = audio;
        target.fuseSparkPrefab = fx.FuseSpark;
        target.rocketTrailPrefab = fx.RocketTrail;
        target.rocketBurstPrefab = fx.RocketBurst;
        target.fireShowerPrefab = fx.FireShower;

        // Keep a fuse marker the designer can drag, rather than a collider - these meshes are
        // imported at 46x and 1000x scale, which makes authored colliders unusable.
        string fuseName = $"Fuse - {type} {index}";
        var fuse = FindOrCreateChild(fuseRoot, fuseName);
        if (target.ignitePoint == null)
        {
            fuse.position = DefaultFusePosition(go, type);
            target.ignitePoint = fuse;
        }
        else
        {
            target.ignitePoint = fuse;
        }

        target.igniteRadius = type == CrackerTarget.CrackerType.Rocket ? 0.3f : 0.25f;

        EditorUtility.SetDirty(go);
        return target;
    }

    /// <summary>Rocket fuses sit at the middle of the mesh, fire shower fuses on top of it.</summary>
    private static Vector3 DefaultFusePosition(GameObject go, CrackerTarget.CrackerType type)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return go.transform.position;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        return type == CrackerTarget.CrackerType.Rocket
            ? bounds.center
            : bounds.center + Vector3.up * (bounds.extents.y * 0.9f);
    }

    private static List<GameObject> FindByName(Scene scene, string name)
    {
        var hits = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) hits.Add(t.gameObject);
        return hits;
    }

    private static GameObject FindOrCreateRoot(Scene scene, string name)
    {
        var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    // ----------------------------------------------------------------- effects

    private struct EffectSet
    {
        public ParticleSystem FuseSpark;
        public ParticleSystem RocketTrail;
        public ParticleSystem RocketBurst;
        public ParticleSystem FireShower;
    }

    private static EffectSet BuildEffectPrefabs(Material spark)
    {
        return new EffectSet
        {
            FuseSpark = Save(BuildFuseSpark(spark), "Fuse Spark FX"),
            RocketTrail = Save(BuildRocketTrail(spark), "Rocket Trail FX"),
            RocketBurst = Save(BuildRocketBurst(spark), "Rocket Burst FX"),
            FireShower = Save(BuildFireShower(spark), "Fire Shower FX"),
        };
    }

    private static ParticleSystem Save(GameObject go, string assetName)
    {
        string path = $"{FxFolder}/{assetName}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        return prefab.GetComponent<ParticleSystem>();
    }

    /// <summary>Small bright crackle where the lighter touches the fuse.</summary>
    private static GameObject BuildFuseSpark(Material spark)
    {
        var root = new GameObject("Fuse Spark FX");
        var ps = root.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        main.startColor = new Color(1f, 0.95f, 0.7f);
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.02f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.4f, 0.05f));

        SetupRenderer(ps, spark);
        return root;
    }

    /// <summary>Sparks streaming off the rocket as it climbs. Looping - stopped by CrackerTarget.</summary>
    private static GameObject BuildRocketTrail(Material spark)
    {
        var root = new GameObject("Rocket Trail FX");
        var ps = root.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.gravityModifier = 0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 700;

        var emission = ps.emission;
        emission.rateOverTime = 150f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(new Color(1f, 0.75f, 0.3f), new Color(0.85f, 0.25f, 0.05f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

        SetupRenderer(ps, spark);
        return root;
    }

    /// <summary>
    /// The burst at the top of the climb: a spray of sparks, a slower sparkle layer and a soft
    /// flash. Built as three sibling one-shot systems so a plain Instantiate plays the lot.
    /// </summary>
    private static GameObject BuildRocketBurst(Material spark)
    {
        var root = new GameObject("Rocket Burst FX");
        var explosion = root.AddComponent<ParticleSystem>();
        {
            var main = explosion.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.gravityModifier = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var emission = explosion.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 110) });

            var shape = explosion.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var limit = explosion.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.drag = 3f;

            var col = explosion.colorOverLifetime;
            col.enabled = true;
            col.color = Burst(Color.white, new Color(1f, 0.85f, 0.35f), new Color(1f, 0.25f, 0.15f));
        }
        SetupRenderer(explosion, spark);

        var sparkle = AddChild(root, "Burst Sparkle");
        {
            var main = sparkle.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;

            var emission = sparkle.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 90) });

            var shape = sparkle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var col = sparkle.colorOverLifetime;
            col.enabled = true;
            col.color = Burst(new Color(1f, 1f, 0.85f), new Color(1f, 0.6f, 0.9f),
                              new Color(0.5f, 0.45f, 1f));

            var size = sparkle.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        }
        SetupRenderer(sparkle, spark);

        var flash = AddChild(root, "Flash");
        {
            var main = flash.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = 0.3f;
            main.startSpeed = 0f;
            main.startSize = 7f;
            main.startColor = new Color(1f, 0.95f, 0.8f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4;

            var emission = flash.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = flash.shape;
            shape.enabled = false;

            var col = flash.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.7f, 0.3f));
        }
        SetupRenderer(flash, spark);

        return root;
    }

    /// <summary>
    /// The ground fountain - the Diwali flower pot / anar. Looping so CrackerTarget can run it
    /// for a set time and then let the last sparks die off.
    /// </summary>
    private static GameObject BuildFireShower(Material spark)
    {
        var root = new GameObject("Fire Shower FX");
        var fountain = root.AddComponent<ParticleSystem>();
        {
            var main = fountain.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.11f);
            main.gravityModifier = 1.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1200;

            var emission = fountain.emission;
            emission.rateOverTime = 230f;

            var shape = fountain.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.03f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // point the cone straight up

            var col = fountain.colorOverLifetime;
            col.enabled = true;
            col.color = Burst(new Color(1f, 1f, 0.9f), new Color(1f, 0.8f, 0.35f),
                              new Color(1f, 0.3f, 0.05f));

            var size = fountain.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));
        }
        SetupRenderer(fountain, spark);

        var glow = AddChild(root, "Mouth Glow");
        {
            var main = glow.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.25f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startColor = new Color(1f, 0.85f, 0.45f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;

            var emission = glow.emission;
            emission.rateOverTime = 18f;

            var shape = glow.shape;
            shape.enabled = false;

            var col = glow.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.55f, 0.15f));
        }
        SetupRenderer(glow, spark);

        return root;
    }

    private static ParticleSystem AddChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go.AddComponent<ParticleSystem>();
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

    private static ParticleSystem.MinMaxGradient Burst(Color flash, Color mid, Color end)
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(flash, 0f),
                new GradientColorKey(mid, 0.25f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(g);
    }
}
