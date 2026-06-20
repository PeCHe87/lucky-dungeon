#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

static class RangedAttackAnimationEventSetup
{
    const string FireEventFunctionName = "OnRangedFireFrame";
    const float DefaultSampleRate = 30f;
    const float FireTimeFraction = 0.4f;

    static readonly (string assetPath, string clipName)[] AttackClips =
    {
        (
            "Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Attack1.FBX",
            "Bow_Attack1"),
        (
            "Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Attack2.FBX",
            "Bow_Attack2"),
    };

    [MenuItem("Knight Undead/Combat/Setup Ranged Fire Animation Events")]
    static void SetupRangedFireAnimationEvents()
    {
        int updated = 0;
        foreach ((string assetPath, string clipName) in AttackClips)
        {
            if (TryAddFireEventToClip(assetPath, clipName))
                updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[RangedAttackAnimationEventSetup] Updated {updated}/{AttackClips.Length} attack clips with '{FireEventFunctionName}' events. " +
            "Scrub each clip in the Animation import window and move events to the release frame.");
    }

    internal static void EnsureRangedFireAnimationEvents()
    {
        foreach ((string assetPath, string clipName) in AttackClips)
            TryAddFireEventToClip(assetPath, clipName);
    }

    static bool TryAddFireEventToClip(string assetPath, string clipName)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[RangedAttackAnimationEventSetup] ModelImporter not found: {assetPath}");
            return false;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"[RangedAttackAnimationEventSetup] No clip animations on: {assetPath}");
            return false;
        }

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            if (!string.Equals(clip.name, clipName, StringComparison.Ordinal))
                continue;

            float duration = (clip.lastFrame - clip.firstFrame) / DefaultSampleRate;
            if (duration <= 0f)
                duration = 0.5f;

            float fireTime = duration * FireTimeFraction;
            clip.events = new[]
            {
                new AnimationEvent
                {
                    time = fireTime,
                    functionName = FireEventFunctionName,
                },
            };
            clips[i] = clip;
            changed = true;
            break;
        }

        if (!changed)
        {
            Debug.LogWarning(
                $"[RangedAttackAnimationEventSetup] Clip '{clipName}' not found on {assetPath}");
            return false;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return true;
    }
}
#endif
