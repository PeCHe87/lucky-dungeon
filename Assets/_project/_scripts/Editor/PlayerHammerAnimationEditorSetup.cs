#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

static class PlayerHammerAnimationEditorSetup
{
    public const string HammerControllerPath = "Assets/_project/_animation/PlayerHammerLocomotion.controller";
    public const string HammerProfilePath = "Assets/_project/_animation/PlayerHammerAnimationProfile.asset";

    const string HammerAnimFolder =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/02_Hammer/";

    const string DamageHitFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/98_Damage/Stander@Damage2.FBX";

    const string DieFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/98_Damage/Stander@KnockDown_B_Light.FBX";

    const string DieStateName = "Die";
    const string DieClipName = "KnockDown_B_Light";

    const string HammerBlockFbxPath = HammerAnimFolder + "Stander@Hammer_Block.FBX";
    const string AttackReadyStateName = "AttackReady";
    const string MeleeApproachStateName = "MeleeApproach";

    static readonly (string fbxPath, string clipName)[] HammerLocomotionClips =
    {
        (HammerAnimFolder + "Stander@Hammer_Idle.FBX", "Hammer_Idle"),
        (HammerAnimFolder + "Stander@Hammer_Walk_F.FBX", "Hammer_Walk_F"),
        (HammerAnimFolder + "Stander@Hammer_Run.FBX", "Hammer_Run"),
        (HammerAnimFolder + "Stander@Hammer_Dodge.FBX", "Hammer_Dodge"),
        (HammerAnimFolder + "Stander@Hammer_Attack1.FBX", "Hammer_Attack1"),
        (HammerAnimFolder + "Stander@Hammer_Attack2.FBX", "Hammer_Attack2"),
        (HammerAnimFolder + "Stander@Hammer_Attack3.FBX", "Hammer_Attack3"),
    };

    [MenuItem("Tools/Combat/Setup Player Hammer Animation Assets")]
    public static void EnsureHammerAssetsExistMenu() => EnsureHammerAssetsExist();

    public static void EnsureHammerAssetsExist()
    {
        EnsureAnimationFolderExists();
        MeleeAttackAnimationEventSetup.EnsureMeleeHitAnimationEvents();
        EnsureHammerControllerExists();
        PlayerBowAnimationEditorSetup.EnsureAttackReadyStateOnController(
            HammerControllerPath, HammerBlockFbxPath, "Hammer_Block_Loop");
        PlayerBowAnimationEditorSetup.EnsureDieStateOnController(HammerControllerPath);
        EnsureMeleeApproachStateOnController();
        PlayerEntityStateAnimationProfile.EnsureHammerAssetExists();
        EnsureHammerProfileAttackReadyConfigured();
        PlayerBowAnimationEditorSetup.EnsureDyingProfileEntry(HammerProfilePath);
    }

    static void EnsureAnimationFolderExists()
    {
        if (AssetDatabase.IsValidFolder("Assets/_project/_animation"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/_project"))
            AssetDatabase.CreateFolder("Assets", "_project");
        AssetDatabase.CreateFolder("Assets/_project", "_animation");
    }

    static void EnsureHammerControllerExists()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(HammerControllerPath) != null)
            return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(HammerControllerPath);
        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AddState(root, "Idle", LoadClip(HammerLocomotionClips[0].fbxPath, HammerLocomotionClips[0].clipName));
        AddState(root, "Walking", LoadClip(HammerLocomotionClips[1].fbxPath, HammerLocomotionClips[1].clipName));
        AddState(root, "Running", LoadClip(HammerLocomotionClips[2].fbxPath, HammerLocomotionClips[2].clipName));
        AddState(root, "Dashing", LoadClip(HammerLocomotionClips[3].fbxPath, HammerLocomotionClips[3].clipName));
        AddState(root, "Attacking1", LoadClip(HammerLocomotionClips[4].fbxPath, HammerLocomotionClips[4].clipName), 1f);
        AddState(root, "Attacking2", LoadClip(HammerLocomotionClips[5].fbxPath, HammerLocomotionClips[5].clipName), 1f);
        AddState(root, "Attacking3", LoadClip(HammerLocomotionClips[6].fbxPath, HammerLocomotionClips[6].clipName), 1f);
        AddState(root, AttackReadyStateName, LoadClip(HammerBlockFbxPath, "Hammer_Block_Loop"));
        AddState(root, "Hit", LoadClip(DamageHitFbxPath, "Damage2"));
        AddState(root, MeleeApproachStateName, LoadClip(HammerLocomotionClips[2].fbxPath, HammerLocomotionClips[2].clipName), 0.5f);
        AddState(root, DieStateName, LoadClip(DieFbxPath, DieClipName));

        root.defaultState = root.states[0].state;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static void EnsureMeleeApproachStateOnController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HammerControllerPath);
        if (controller == null)
            return;

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        for (int i = 0; i < root.states.Length; i++)
        {
            if (string.Equals(root.states[i].state.name, MeleeApproachStateName, StringComparison.Ordinal))
                return;
        }

        AnimationClip clip = LoadClip(HammerLocomotionClips[2].fbxPath, HammerLocomotionClips[2].clipName);
        if (clip == null)
            return;

        AddState(root, MeleeApproachStateName, clip, 0.5f);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static void EnsureHammerProfileAttackReadyConfigured()
    {
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(HammerProfilePath);
        if (profile == null)
            return;

        var so = new SerializedObject(profile);
        var readyState = so.FindProperty("attackReadyAnimatorStateName");
        if (string.IsNullOrWhiteSpace(readyState.stringValue))
        {
            readyState.stringValue = AttackReadyStateName;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
    }

    static void AddState(AnimatorStateMachine root, string stateName, AnimationClip clip, float speed = 1f)
    {
        var state = root.AddState(stateName);
        state.motion = clip;
        state.speed = speed;
    }

    static AnimationClip LoadClip(string fbxPath, string clipName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip
                && string.Equals(clip.name, clipName, StringComparison.Ordinal)
                && !clip.name.StartsWith("__", StringComparison.Ordinal))
            {
                return clip;
            }
        }

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__", StringComparison.Ordinal))
                return clip;
        }

        Debug.LogWarning($"[PlayerHammerAnimationEditorSetup] Clip '{clipName}' not found at {fbxPath}");
        return null;
    }
}
#endif
