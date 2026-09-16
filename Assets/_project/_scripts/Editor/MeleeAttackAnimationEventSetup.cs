#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

static class MeleeAttackAnimationEventSetup
{
    const string HitEventFunctionName = "OnMeleeHitFrame";
    const float DefaultSampleRate = 30f;
    const float HitTimeFraction = 0.4f;

    static readonly (string assetPath, string clipName)[] AttackClips =
    {
        (
            "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Attack1.FBX",
            "Fighter_Attack1"),
        (
            "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Attack2.FBX",
            "Fighter_Attack2"),
        (
            "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Attack3.FBX",
            "Fighter_Attack3"),
        (
            "Assets/_assetStore/Shinabro/Platform_Animation/Animation/02_Hammer/Stander@Hammer_Attack1.FBX",
            "Hammer_Attack1"),
        (
            "Assets/_assetStore/Shinabro/Platform_Animation/Animation/02_Hammer/Stander@Hammer_Attack2.FBX",
            "Hammer_Attack2"),
        (
            "Assets/_assetStore/Shinabro/Platform_Animation/Animation/02_Hammer/Stander@Hammer_Attack3.FBX",
            "Hammer_Attack3"),
    };

    [MenuItem("Knight Undead/Combat/Setup Melee Hit Animation Events")]
    static void SetupMeleeHitAnimationEventsMenu() => EnsureMeleeHitAnimationEvents();

    public static void EnsureMeleeHitAnimationEvents()
    {
        int updated = 0;
        foreach ((string assetPath, string clipName) in AttackClips)
        {
            if (TryAddHitEventToClip(assetPath, clipName))
                updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[MeleeAttackAnimationEventSetup] Updated {updated}/{AttackClips.Length} attack clips with '{HitEventFunctionName}' events. " +
            "Scrub each clip in the Animation import window and move events to the visual impact frame.");
    }

    static bool TryAddHitEventToClip(string assetPath, string clipName)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[MeleeAttackAnimationEventSetup] ModelImporter not found: {assetPath}");
            return false;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"[MeleeAttackAnimationEventSetup] No clip animations on: {assetPath}");
            return false;
        }

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            if (!string.Equals(clip.name, clipName, StringComparison.Ordinal))
                continue;

            if (HasHitEvent(clip.events))
                return true;

            float duration = (clip.lastFrame - clip.firstFrame) / DefaultSampleRate;
            if (duration <= 0f)
                duration = 0.5f;

            float hitTime = duration * HitTimeFraction;
            clip.events = new[]
            {
                new AnimationEvent
                {
                    time = hitTime,
                    functionName = HitEventFunctionName,
                },
            };
            clips[i] = clip;
            changed = true;
            break;
        }

        if (!changed)
        {
            Debug.LogWarning(
                $"[MeleeAttackAnimationEventSetup] Clip '{clipName}' not found on {assetPath}");
            return false;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return true;
    }

    static bool HasHitEvent(AnimationEvent[] events)
    {
        if (events == null)
            return false;

        for (int i = 0; i < events.Length; i++)
        {
            if (string.Equals(events[i].functionName, HitEventFunctionName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
#endif
