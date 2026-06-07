#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Ensures <see cref="EntityCombat.controller"/> includes a Run state for NavMesh locomotion.
/// </summary>
[InitializeOnLoad]
public static class EntityCombatControllerSetup
{
    const string ControllerPath = "Assets/_project/_animation/EntityCombat.controller";
    const string RunFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Run.FBX";

    static EntityCombatControllerSetup()
    {
        EditorApplication.delayCall += EnsureRunStateOnLoad;
    }

    static void EnsureRunStateOnLoad()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureRunStateOnLoad;
            return;
        }

        EnsureRunState();
    }

    [MenuItem("Tools/Entities/Ensure Entity Combat Run Animation")]
    public static void EnsureRunStateMenu()
    {
        EnsureRunState();
    }

    public static bool EnsureRunState()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogWarning("[EntityCombatControllerSetup] Missing controller at " + ControllerPath);
            return false;
        }

        if (controller.layers.Length == 0)
            return false;

        var root = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in root.states)
        {
            if (child.state != null && child.state.name == "Run")
                return false;
        }

        AnimationClip runClip = LoadRunClip();
        if (runClip == null)
        {
            Debug.LogWarning("[EntityCombatControllerSetup] Could not load Fighter_Run clip from " + RunFbxPath);
            return false;
        }

        AnimatorState runState = root.AddState("Run", new Vector3(250f, 120f, 0f));
        runState.motion = runClip;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[EntityCombatControllerSetup] Added Run state to " + ControllerPath);
        return true;
    }

    static AnimationClip LoadRunClip()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(RunFbxPath).OfType<AnimationClip>().ToArray();
        if (clips.Length == 0)
            return null;

        AnimationClip named = clips.FirstOrDefault(c => c.name == "Fighter_Run");
        if (named != null)
            return named;

        return clips.FirstOrDefault(c => !c.name.StartsWith("__"));
    }
}
#endif
