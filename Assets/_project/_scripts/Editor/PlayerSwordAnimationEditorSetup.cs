#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

static class PlayerSwordAnimationEditorSetup
{
    public const string SwordControllerPath = "Assets/_project/_animation/PlayerSwordLocomotion.controller";
    public const string SwordProfilePath = "Assets/_project/_animation/PlayerSwordAnimationProfile.asset";

    const string SwordAnimFolder =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/01_Sword&Shield/";

    const string DamageHitFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/98_Damage/Stander@Damage2.FBX";

    const string DieFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/98_Damage/Stander@KnockDown_B_Light.FBX";

    const string DieStateName = "Die";
    const string DieClipName = "KnockDown_B_Light";

    const string SwordBlockFbxPath = SwordAnimFolder + "Stander@Sword&Shield_Block.FBX";
    const string AttackReadyStateName = "AttackReady";
    const string MeleeApproachStateName = "MeleeApproach";

    static readonly (string fbxPath, string clipName)[] SwordLocomotionClips =
    {
        (SwordAnimFolder + "Stander@Sword&Shield_Idle.FBX", "Sword&Shield_Idle"),
        (SwordAnimFolder + "Stander@Sword&Shield_Walk_F.FBX", "Sword&Shield_Walk_F"),
        (SwordAnimFolder + "Stander@Sword&Shield_Run.FBX", "Sword&Shield_Run"),
        (SwordAnimFolder + "Stander@Sword&Shield_Dodge.FBX", "Sword&Shield_Dodge"),
        (SwordAnimFolder + "Stander@Sword&Shield_Attack1.FBX", "Sword&Shield_Attack1"),
        (SwordAnimFolder + "Stander@Sword&Shield_Attack2.FBX", "Sword&Shield_Attack2"),
        (SwordAnimFolder + "Stander@Sword&Shield_Attack3.FBX", "Sword&Shield_Attack3"),
    };

    [MenuItem("Tools/Combat/Setup Player Sword Animation Assets")]
    public static void EnsureSwordAssetsExistMenu() => EnsureSwordAssetsExist();

    public static void EnsureSwordAssetsExist()
    {
        EnsureAnimationFolderExists();
        MeleeAttackAnimationEventSetup.EnsureMeleeHitAnimationEvents();
        EnsureSwordControllerExists();
        PlayerBowAnimationEditorSetup.EnsureAttackReadyStateOnController(
            SwordControllerPath, SwordBlockFbxPath, "Sword&Shield_Block_Loop");
        PlayerBowAnimationEditorSetup.EnsureDieStateOnController(SwordControllerPath);
        EnsureMeleeApproachStateOnController();
        PlayerEntityStateAnimationProfile.EnsureSwordAssetExists();
        EnsureSwordProfileAttackReadyConfigured();
        PlayerBowAnimationEditorSetup.EnsureDyingProfileEntry(SwordProfilePath);
    }

    static void EnsureAnimationFolderExists()
    {
        if (AssetDatabase.IsValidFolder("Assets/_project/_animation"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/_project"))
            AssetDatabase.CreateFolder("Assets", "_project");
        AssetDatabase.CreateFolder("Assets/_project", "_animation");
    }

    static void EnsureSwordControllerExists()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(SwordControllerPath) != null)
            return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(SwordControllerPath);
        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AddState(root, "Idle", LoadClip(SwordLocomotionClips[0].fbxPath, SwordLocomotionClips[0].clipName));
        AddState(root, "Walking", LoadClip(SwordLocomotionClips[1].fbxPath, SwordLocomotionClips[1].clipName));
        AddState(root, "Running", LoadClip(SwordLocomotionClips[2].fbxPath, SwordLocomotionClips[2].clipName));
        AddState(root, "Dashing", LoadClip(SwordLocomotionClips[3].fbxPath, SwordLocomotionClips[3].clipName));
        AddState(root, "Attacking1", LoadClip(SwordLocomotionClips[4].fbxPath, SwordLocomotionClips[4].clipName), 1f);
        AddState(root, "Attacking2", LoadClip(SwordLocomotionClips[5].fbxPath, SwordLocomotionClips[5].clipName), 1f);
        AddState(root, "Attacking3", LoadClip(SwordLocomotionClips[6].fbxPath, SwordLocomotionClips[6].clipName), 1f);
        AddState(root, AttackReadyStateName, LoadClip(SwordBlockFbxPath, "Sword&Shield_Block_Loop"));
        AddState(root, "Hit", LoadClip(DamageHitFbxPath, "Damage2"));
        AddState(root, MeleeApproachStateName, LoadClip(SwordLocomotionClips[2].fbxPath, SwordLocomotionClips[2].clipName), 0.5f);
        AddState(root, DieStateName, LoadClip(DieFbxPath, DieClipName));

        root.defaultState = root.states[0].state;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static void EnsureMeleeApproachStateOnController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SwordControllerPath);
        if (controller == null)
            return;

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        for (int i = 0; i < root.states.Length; i++)
        {
            if (string.Equals(root.states[i].state.name, MeleeApproachStateName, StringComparison.Ordinal))
                return;
        }

        AnimationClip clip = LoadClip(SwordLocomotionClips[2].fbxPath, SwordLocomotionClips[2].clipName);
        if (clip == null)
            return;

        AddState(root, MeleeApproachStateName, clip, 0.5f);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static void EnsureSwordProfileAttackReadyConfigured()
    {
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(SwordProfilePath);
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

        Debug.LogWarning($"[PlayerSwordAnimationEditorSetup] Clip '{clipName}' not found at {fbxPath}");
        return null;
    }
}
#endif
