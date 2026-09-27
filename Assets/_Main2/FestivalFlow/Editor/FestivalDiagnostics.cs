using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prints where every cracker's fuse marker sits relative to the mesh it belongs to, so a
/// cracker that refuses to light can be diagnosed without a headset.
/// Tools > SkillVerse > Festival > Log Cracker Setup
/// </summary>
public static class FestivalDiagnostics
{
    private const string ScenePath = "Assets/_Main2/Festival.unity";

    [MenuItem("Tools/SkillVerse/Festival/Log Cracker Setup")]
    public static void Report()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var controller = Object.FindObjectOfType<FestivalCrackerController>();
        if (controller == null)
        {
            Debug.LogError("DIAG: no FestivalCrackerController in the scene.");
            return;
        }

        var lighter = controller.lighter;
        if (lighter != null)
        {
            Debug.Log($"DIAG lighter pos={V(lighter.transform.position)} " +
                      $"tip={V(lighter.Tip.position)} " +
                      $"tipOffsetFromPivot={Vector3.Distance(lighter.transform.position, lighter.Tip.position):F3}m " +
                      $"held={V(lighter.heldLocalPosition)}/{V(lighter.heldLocalEuler)}");
        }

        var rig = Object.FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
        if (rig != null) Debug.Log($"DIAG rig pos={V(rig.transform.position)}");

        Debug.Log($"DIAG targets={controller.targets?.Length ?? 0} " +
                  $"pickupStep={controller.pickupStepIndex} igniteStep={controller.igniteStepIndex}");

        if (controller.targets == null) return;

        for (int i = 0; i < controller.targets.Length; i++)
        {
            var t = controller.targets[i];
            if (t == null) { Debug.LogError($"DIAG [{i}] NULL target"); continue; }

            var renderers = t.GetComponentsInChildren<Renderer>();
            string boundsInfo = "no renderers";
            float fuseToCentre = -1f;
            bool inside = false;

            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
                boundsInfo = $"centre={V(b.center)} size={V(b.size)} minY={b.min.y:F3} maxY={b.max.y:F3}";
                fuseToCentre = Vector3.Distance(t.IgnitePosition, b.center);
                inside = b.Contains(t.IgnitePosition);
            }

            Debug.Log($"DIAG [{i}] '{t.name}' type={t.type} active={t.isActiveAndEnabled} " +
                      $"radius={t.igniteRadius} fuse={(t.ignitePoint != null ? t.ignitePoint.name : "NULL")}@{V(t.IgnitePosition)} " +
                      $"| renderers={renderers.Length} {boundsInfo} " +
                      $"| fuseToBoundsCentre={fuseToCentre:F3}m insideBounds={inside} " +
                      $"| fx spark={(t.fuseSparkPrefab != null)} trail={(t.rocketTrailPrefab != null)} " +
                      $"burst={(t.rocketBurstPrefab != null)} shower={(t.fireShowerPrefab != null)} " +
                      $"effectRoot={(t.effectRoot != null ? t.effectRoot.name : "NULL")}");
        }

        // How far apart are the fuses? Overlapping ones would light together.
        var fuses = controller.targets.Where(x => x != null).ToArray();
        for (int a = 0; a < fuses.Length; a++)
        for (int b2 = a + 1; b2 < fuses.Length; b2++)
        {
            float d = Vector3.Distance(fuses[a].IgnitePosition, fuses[b2].IgnitePosition);
            if (d < 1.5f)
                Debug.LogWarning($"DIAG fuses '{fuses[a].name}'[{a}] and '{fuses[b2].name}'[{b2}] are only {d:F3}m apart");
        }
    }

    private static string V(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";
}
