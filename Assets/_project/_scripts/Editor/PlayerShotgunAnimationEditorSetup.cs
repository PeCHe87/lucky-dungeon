#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

static class PlayerShotgunAnimationEditorSetup
{
    public const string ShotgunControllerPath = "Assets/_project/_animation/PlayerShotgunLocomotion.controller";
    public const string ShotgunProfilePath = PlayerEntityStateAnimationProfile.ShotgunAssetPath;

    const string DamageHitFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/98_Damage/Stander@Damage2.FBX";

    const string DieFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/98_Damage/Stander@KnockDown_B_Light.FBX";

    const string DieStateName = "Die";
    const string DieClipName = "KnockDown_B_Light";

    const string PistolBlockFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Block.FBX";

    const string ShotgunShootFbxPath =
        "Assets/_assetStore/Wolff's Studio/TacticalShotGun/Animations/Shoot/Anim_ShotGunShoot01.fbx";

    const string ShotgunShootClipName = "Anim_ShotGunShoot01";

    const string AttackReadyStateName = "AttackReady";

    static readonly (string fbxPath, string clipName)[] ShotgunLocomotionClips =
    {
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Idle.FBX", "Pistol_Idle"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Walk_F.FBX", "Pistol_Walk_F"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Run.FBX", "Pistol_Run"),
        ("Assets/_assetStore/Shinabro/Platform_Animation/Animation/05_Pistol/Stander@Pistol_Dodge.FBX", "Pistol_Dodge"),
    };

    [MenuItem("Tools/Combat/Setup Player Shotgun Animation Assets")]
    public static void EnsureShotgunAssetsExistMenu() => EnsureShotgunAssetsExist();

    public static void EnsureShotgunAssetsExist()
    {
        EnsureAnimationFolderExists();
        RangedAttackAnimationEventSetup.EnsureRangedFireAnimationEvents();
        EnsureShotgunControllerExists();
        PlayerBowAnimationEditorSetup.EnsureAttackReadyStateOnController(
            ShotgunControllerPath, PistolBlockFbxPath, "Pistol_Block_Loop");
        PlayerBowAnimationEditorSetup.EnsureDieStateOnController(ShotgunControllerPath);
        PlayerEntityStateAnimationProfile.EnsureShotgunAssetExists();
        EnsureShotgunProfileAttackReadyConfigured();
        PlayerBowAnimationEditorSetup.EnsureDyingProfileEntry(ShotgunProfilePath);
    }

    static void EnsureAnimationFolderExists()
    {
        if (AssetDatabase.IsValidFolder("Assets/_project/_animation"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/_project"))
            AssetDatabase.CreateFolder("Assets", "_project");
        AssetDatabase.CreateFolder("Assets/_project", "_animation");
    }

    static void EnsureShotgunControllerExists()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ShotgunControllerPath) != null)
            return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ShotgunControllerPath);
        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AddState(root, "Idle", LoadClip(ShotgunLocomotionClips[0].fbxPath, ShotgunLocomotionClips[0].clipName));
        AddState(root, "Walking", LoadClip(ShotgunLocomotionClips[1].fbxPath, ShotgunLocomotionClips[1].clipName));
        AddState(root, "Running", LoadClip(ShotgunLocomotionClips[2].fbxPath, ShotgunLocomotionClips[2].clipName));
        AddState(root, "Dashing", LoadClip(ShotgunLocomotionClips[3].fbxPath, ShotgunLocomotionClips[3].clipName));
        AnimationClip shootClip = LoadClip(ShotgunShootFbxPath, ShotgunShootClipName);
        AddState(root, "Attacking1", shootClip, 1f);
        AddState(root, "Attacking2", shootClip, 1f);
        AddState(root, AttackReadyStateName, LoadClip(PistolBlockFbxPath, "Pistol_Block_Loop"));
        AddState(root, "Hit", LoadClip(DamageHitFbxPath, "Damage2"));
        AddState(root, DieStateName, LoadClip(DieFbxPath, DieClipName));

        root.defaultState = root.states[0].state;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static void EnsureShotgunProfileAttackReadyConfigured()
    {
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(ShotgunProfilePath);
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

        Debug.LogWarning($"[PlayerShotgunAnimationEditorSetup] Clip '{clipName}' not found at {fbxPath}");
        return null;
    }
}
#endif
