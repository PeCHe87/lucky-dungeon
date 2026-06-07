#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Ensures <see cref="EntityCombat.controller"/> includes Run and Attack1 states for entity AI.
/// </summary>
[InitializeOnLoad]
public static class EntityCombatControllerSetup
{
    const string ControllerPath = "Assets/_project/_animation/EntityCombat.controller";
    const string ProfilePath = "Assets/_project/_animation/EntityCombatAnimationProfile.asset";
    const string RunFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Run.FBX";

    static EntityCombatControllerSetup()
    {
        EditorApplication.delayCall += EnsureCombatAssetsOnLoad;
    }

    static void EnsureCombatAssetsOnLoad()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureCombatAssetsOnLoad;
            return;
        }

        EnsureRunState();
        EnsureAttackState();
        EnsureAttackProfileEntry();
    }

    [MenuItem("Tools/Entities/Ensure Entity Combat Run Animation")]
    public static void EnsureRunStateMenu() => EnsureRunState();

    [MenuItem("Tools/Entities/Ensure Entity Combat Attack Animation")]
    public static void EnsureAttackStateMenu() => EnsureAttackState();

    public static bool EnsureRunState()
    {
        return EnsureAnimatorState("Run", LoadRunClip(), new Vector3(250f, 120f, 0f));
    }

    public static bool EnsureAttackState()
    {
        return EnsureAnimatorState("Attack1", EntityAttackPrefabSetup.LoadAttackClip(), new Vector3(400f, 120f, 0f));
    }

    public static bool EnsureAttackProfileEntry()
    {
        var profile = AssetDatabase.LoadAssetAtPath<EntityFsmAnimationProfile>(ProfilePath);
        if (profile == null)
            return false;

        var so = new SerializedObject(profile);
        var entries = so.FindProperty("entries");
        if (entries == null)
            return false;

        for (int i = 0; i < entries.arraySize; i++)
        {
            var stateId = entries.GetArrayElementAtIndex(i).FindPropertyRelative("stateId");
            if (stateId != null && stateId.stringValue == "Attack")
                return false;
        }

        entries.InsertArrayElementAtIndex(entries.arraySize);
        var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        entry.FindPropertyRelative("stateId").stringValue = "Attack";
        entry.FindPropertyRelative("animatorStateName").stringValue = "Attack1";
        entry.FindPropertyRelative("crossFadeSeconds").floatValue = 0.08f;
        entry.FindPropertyRelative("layer").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[EntityCombatControllerSetup] Added Attack entry to " + ProfilePath);
        return true;
    }

    static bool EnsureAnimatorState(string stateName, AnimationClip clip, Vector3 position)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[EntityCombatControllerSetup] Could not load clip for state '{stateName}'.");
            return false;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers.Length == 0)
            return false;

        var root = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in root.states)
        {
            if (child.state != null && child.state.name == stateName)
                return false;
        }

        AnimatorState state = root.AddState(stateName, position);
        state.motion = clip;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[EntityCombatControllerSetup] Added {stateName} state to " + ControllerPath);
        return true;
    }

    static AnimationClip LoadRunClip()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(RunFbxPath).OfType<AnimationClip>().ToArray();
        if (clips.Length == 0)
            return null;

        AnimationClip named = clips.FirstOrDefault(c => c.name == "Fighter_Run");
        return named ?? clips.FirstOrDefault(c => !c.name.StartsWith("__"));
    }
}
#endif
