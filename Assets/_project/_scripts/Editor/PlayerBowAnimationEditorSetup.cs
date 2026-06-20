#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

static class PlayerBowAnimationEditorSetup
{
    public const string BowControllerPath = "Assets/_project/_animation/PlayerBowLocomotion.controller";
    public const string BowProfilePath = "Assets/_project/_animation/PlayerBowAnimationProfile.asset";

    const string DamageHitFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/98_Damage/Stander@Damage2.FBX";

    static readonly (string fbxPath, string clipName)[] BowLocomotionClips =
    {
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Idle.FBX", "Bow_Idle"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Walk_F.FBX", "Bow_Walk_F"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Run.FBX", "Bow_Run"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Dodge.FBX", "Bow_Dodge"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Attack1.FBX", "Bow_Attack1"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Attack2.FBX", "Bow_Attack2"),
    };

    public static void EnsureBowAssetsExist()
    {
        EnsureAnimationFolderExists();
        RangedAttackAnimationEventSetup.EnsureRangedFireAnimationEvents();
        EnsureBowControllerExists();
        PlayerEntityStateAnimationProfile.EnsureBowAssetExists();
    }

    static void EnsureAnimationFolderExists()
    {
        if (AssetDatabase.IsValidFolder("Assets/_project/_animation"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/_project"))
            AssetDatabase.CreateFolder("Assets", "_project");
        AssetDatabase.CreateFolder("Assets/_project", "_animation");
    }

    static void EnsureBowControllerExists()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(BowControllerPath) != null)
            return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(BowControllerPath);
        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AddState(root, "Idle", LoadClip(BowLocomotionClips[0].fbxPath, BowLocomotionClips[0].clipName));
        AddState(root, "Walking", LoadClip(BowLocomotionClips[1].fbxPath, BowLocomotionClips[1].clipName));
        AddState(root, "Running", LoadClip(BowLocomotionClips[2].fbxPath, BowLocomotionClips[2].clipName));
        AddState(root, "Dashing", LoadClip(BowLocomotionClips[3].fbxPath, BowLocomotionClips[3].clipName));
        AddState(root, "Attacking1", LoadClip(BowLocomotionClips[4].fbxPath, BowLocomotionClips[4].clipName), 1f);
        AddState(root, "Attacking2", LoadClip(BowLocomotionClips[5].fbxPath, BowLocomotionClips[5].clipName), 1f);
        AddState(root, "Hit", LoadClip(DamageHitFbxPath, "Damage2"));

        root.defaultState = root.states[0].state;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
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

        Debug.LogWarning($"[PlayerBowAnimationEditorSetup] Clip '{clipName}' not found at {fbxPath}");
        return null;
    }
}
#endif
