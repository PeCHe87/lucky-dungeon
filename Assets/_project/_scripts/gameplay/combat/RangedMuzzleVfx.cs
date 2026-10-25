using System;
using UnityEngine;

/// <summary>
/// Optional muzzle flash VFX for a ranged shot.
/// Null <see cref="prefab"/> skips spawn.
/// </summary>
[Serializable]
public struct RangedMuzzleVfx
{
    public GameObject prefab;

    [Tooltip("Clip frame at which to spawn (delay = spawnAtFrame / framesPerSecond).")]
    [Min(0)]
    public int spawnAtFrame;

    [Tooltip("Frames per second used to convert spawnAtFrame to delay. Leave 0 to use 30.")]
    [Min(0f)]
    public float framesPerSecond;

    [Tooltip("Offset from the fire point (or attacker) in facing-local space (forward = +Z).")]
    public Vector3 localOffset;

    [Tooltip("Destroy spawned instance after this many seconds. 0 = do not auto-destroy.")]
    [Min(0f)]
    public float destroyDelaySeconds;

    public bool HasPrefab => prefab != null;

    public float ResolveSpawnDelaySeconds()
    {
        float fps = framesPerSecond > 0f ? framesPerSecond : 30f;
        return spawnAtFrame / fps;
    }
}
