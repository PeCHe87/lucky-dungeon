using System.Collections;
using UnityEngine;

/// <summary>
/// Optional world blood / hit VFX for combat entities.
/// Spawns configured damage or death prefabs from <see cref="CombatEntityHealth"/> events.
/// Null prefab slots skip that event.
/// </summary>
[DisallowMultipleComponent]
public sealed class EntityCombatVfx : MonoBehaviour
{
    const float DefaultDamageDestroyDelay = 2f;
    const float DefaultDeathDestroyDelay = 3f;

    [Header("Refs")]
    [SerializeField] CombatEntityHealth health;
    [Tooltip("World spawn origin. Defaults to this transform.")]
    [SerializeField] Transform spawnAnchor;

    [Header("Damage VFX")]
    [Tooltip("Spawned on non-fatal hits. Empty = no damage VFX.")]
    [SerializeField] GameObject damageVfxPrefab;
    [SerializeField] Vector3 damageVfxLocalOffset;
    [Tooltip("Auto-destroy spawned damage VFX after this many seconds. 0 = do not auto-destroy.")]
    [SerializeField, Min(0f)] float damageVfxDestroyDelay = DefaultDamageDestroyDelay;

    [Header("Death VFX")]
    [Tooltip("Spawned on death. Empty = fall back to damage VFX on fatal hits.")]
    [SerializeField] GameObject deathVfxPrefab;
    [SerializeField] Vector3 deathVfxLocalOffset;
    [Tooltip("Seconds to wait after death before spawning death VFX. 0 = immediate.")]
    [SerializeField, Min(0f)] float deathVfxSpawnDelay;
    [Tooltip("Auto-destroy spawned death VFX after this many seconds. 0 = do not auto-destroy.")]
    [SerializeField, Min(0f)] float deathVfxDestroyDelay = DefaultDeathDestroyDelay;

    Coroutine _deathVfxRoutine;

    void Awake()
    {
        if (health == null)
            health = GetComponent<CombatEntityHealth>();
        if (spawnAnchor == null)
            spawnAnchor = transform;
    }

    void OnEnable()
    {
        if (health == null)
            return;

        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        if (health == null)
            return;

        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        StopDeathVfxRoutine();
    }

    void OnDamaged(float _)
    {
        if (health == null || health.IsDefeated || health.CurrentHitPoints <= 0f)
            return;

        Spawn(damageVfxPrefab, damageVfxLocalOffset, damageVfxDestroyDelay);
    }

    void OnDied()
    {
        GameObject prefab = deathVfxPrefab != null ? deathVfxPrefab : damageVfxPrefab;
        Vector3 offset = deathVfxPrefab != null ? deathVfxLocalOffset : damageVfxLocalOffset;
        float destroyDelay = deathVfxPrefab != null ? deathVfxDestroyDelay : damageVfxDestroyDelay;

        if (prefab == null)
            return;

        StopDeathVfxRoutine();
        if (deathVfxSpawnDelay <= 0f)
        {
            Spawn(prefab, offset, destroyDelay);
            return;
        }

        _deathVfxRoutine = StartCoroutine(SpawnDeathVfxAfterDelay(prefab, offset, destroyDelay));
    }

    void StopDeathVfxRoutine()
    {
        if (_deathVfxRoutine == null)
            return;

        StopCoroutine(_deathVfxRoutine);
        _deathVfxRoutine = null;
    }

    IEnumerator SpawnDeathVfxAfterDelay(GameObject prefab, Vector3 localOffset, float destroyDelay)
    {
        yield return new WaitForSeconds(deathVfxSpawnDelay);
        Spawn(prefab, localOffset, destroyDelay);
        _deathVfxRoutine = null;
    }

    void Spawn(GameObject prefab, Vector3 localOffset, float destroyDelay)
    {
        if (prefab == null)
            return;

        Transform anchor = spawnAnchor != null ? spawnAnchor : transform;
        Vector3 position = anchor.position + localOffset;
        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        if (instance == null)
            return;

        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
            systems[i].Play(true);

        if (destroyDelay > 0f)
            Destroy(instance, destroyDelay);
    }
}
