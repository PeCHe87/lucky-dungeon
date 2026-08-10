#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

static class PlayerPistolAnimationEditorSetup
{
    public const string PistolControllerPath = "Assets/_project/_animation/PlayerPistolLocomotion.controller";
    public const string PistolProfilePath = "Assets/_project/_animation/PlayerPistolAnimationProfile.asset";

    const string DamageHitFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/98_Damage/Stander@Damage2.FBX";

    const string DieFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/98_Damage/Stander@KnockDown_B_Light.FBX";

    const string DieStateName = "Die";
    const string DieClipName = "KnockDown_B_Light";

    const string PistolBlockFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Block.FBX";

    const string AttackReadyStateName = "AttackReady";

    static readonly (string fbxPath, string clipName)[] PistolLocomotionClips =
    {
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Idle.FBX", "Pistol_Idle"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Walk_F.FBX", "Pistol_Walk_F"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Run.FBX", "Pistol_Run"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Dodge.FBX", "Pistol_Dodge"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Attack1.FBX", "Pistol_Attack1"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Attack2.FBX", "Pistol_Attack2"),
    };

    [MenuItem("Tools/Combat/Setup Player Pistol Animation Assets")]
    public static void EnsurePistolAssetsExistMenu() => EnsurePistolAssetsExist();

    public static void EnsurePistolAssetsExist()
    {
        EnsureAnimationFolderExists();
        RangedAttackAnimationEventSetup.EnsureRangedFireAnimationEvents();
        EnsurePistolControllerExists();
        PlayerBowAnimationEditorSetup.EnsureAttackReadyStateOnController(
            PistolControllerPath, PistolBlockFbxPath, "Pistol_Block_Loop");
        PlayerBowAnimationEditorSetup.EnsureDieStateOnController(PistolControllerPath);
        PlayerEntityStateAnimationProfile.EnsurePistolAssetExists();
        EnsurePistolProfileAttackReadyConfigured();
        PlayerBowAnimationEditorSetup.EnsureDyingProfileEntry(PistolProfilePath);
    }

    static void EnsureAnimationFolderExists()
    {
        if (AssetDatabase.IsValidFolder("Assets/_project/_animation"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/_project"))
            AssetDatabase.CreateFolder("Assets", "_project");
        AssetDatabase.CreateFolder("Assets/_project", "_animation");
    }

    static void EnsurePistolControllerExists()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(PistolControllerPath) != null)
            return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(PistolControllerPath);
        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AddState(root, "Idle", LoadClip(PistolLocomotionClips[0].fbxPath, PistolLocomotionClips[0].clipName));
        AddState(root, "Walking", LoadClip(PistolLocomotionClips[1].fbxPath, PistolLocomotionClips[1].clipName));
        AddState(root, "Running", LoadClip(PistolLocomotionClips[2].fbxPath, PistolLocomotionClips[2].clipName));
        AddState(root, "Dashing", LoadClip(PistolLocomotionClips[3].fbxPath, PistolLocomotionClips[3].clipName));
        AddState(root, "Attacking1", LoadClip(PistolLocomotionClips[4].fbxPath, PistolLocomotionClips[4].clipName), 1f);
        AddState(root, "Attacking2", LoadClip(PistolLocomotionClips[5].fbxPath, PistolLocomotionClips[5].clipName), 1f);
        AddState(root, AttackReadyStateName, LoadClip(PistolBlockFbxPath, "Pistol_Block_Loop"));
        AddState(root, "Hit", LoadClip(DamageHitFbxPath, "Damage2"));
        AddState(root, DieStateName, LoadClip(DieFbxPath, DieClipName));

        root.defaultState = root.states[0].state;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static void EnsurePistolProfileAttackReadyConfigured()
    {
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(PistolProfilePath);
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

        Debug.LogWarning($"[PlayerPistolAnimationEditorSetup] Clip '{clipName}' not found at {fbxPath}");
        return null;
    }
}
#endif
