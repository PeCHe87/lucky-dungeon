using System;
using UnityEngine;

/// <summary>
/// Optional world VFX for one melee combo step (array index 0 = Attacking1, etc.).
/// Null <see cref="prefab"/> skips spawn for that step.
/// </summary>
[Serializable]
public struct MeleeAttackSwingVfx
{
    public GameObject prefab;

    [Tooltip("Clip frame at which to spawn (delay = spawnAtFrame / framesPerSecond).")]
    [Min(0)]
    public int spawnAtFrame;

    [Tooltip("Frames per second used to convert spawnAtFrame to delay (Shinabro clips are typically 30). Leave 0 to use 30.")]
    [Min(0f)]
    public float framesPerSecond;

    [Tooltip("Offset from the attacker in facing-local space (forward = +Z).")]
    public Vector3 localOffset;

    [Tooltip("Destroy spawned instance after this many seconds. 0 = do not auto-destroy.")]
    [Min(0f)]
    public float destroyDelaySeconds;

    public float ResolveSpawnDelaySeconds()
    {
        float fps = framesPerSecond > 0f ? framesPerSecond : 30f;
        return spawnAtFrame / fps;
    }
}
