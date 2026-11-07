using System.Collections;
using UnityEngine;

/// <summary>
/// On enemy death, rolls a per-entity drop chance and optionally spawns a world loot container
/// filled from this component's fixed/random tables (same shape as <see cref="InteractableLoot"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDeathLootDrop : MonoBehaviour
{
    const float DefaultSpawnDelay = 0.15f;

    [Header("Refs")]
    [SerializeField] CombatEntityHealth health;
    [SerializeField] GameObject lootPrefab;

    [Header("Drop")]
    [Tooltip("Chance to spawn a lootable when this entity dies. 0 = never.")]
    [SerializeField, Range(0f, 1f)] float dropChance;
    [Tooltip("Seconds after death before spawning (lets death shove settle).")]
    [SerializeField, Min(0f)] float spawnDelay = DefaultSpawnDelay;
    [Tooltip("When true, the spawned lootable destroys itself after the player opens it.")]
    [SerializeField] bool destroyLootAfterOpened = true;
    [Tooltip("Seconds to wait after opening before destroying the spawned lootable.")]
    [SerializeField, Min(0f)] float destroyLootDelay;

    [Header("Loot")]
    [SerializeField] InteractableLoot.FixedLootEntry[] fixedLoot;
    [SerializeField] InteractableLoot.RandomLootEntry[] randomLoot;

    bool _handled;
    Coroutine _spawnRoutine;

    void Awake()
    {
        if (health == null)
            health = GetComponent<CombatEntityHealth>();
    }

    void OnEnable()
    {
        if (health != null)
            health.Died += OnDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.Died -= OnDied;

        StopSpawnRoutine();
    }

    void OnDied()
    {
        if (_handled)
            return;

        _handled = true;

        if (GetComponent<PlayerDeathHandler>() != null)
            return;

        if (dropChance <= 0f || lootPrefab == null)
            return;

        if (!HasAnyLootConfigured())
            return;

        if (Random.value > dropChance)
            return;

        StopSpawnRoutine();
        _spawnRoutine = StartCoroutine(SpawnLootAfterDelay());
    }

    bool HasAnyLootConfigured()
    {
        if (fixedLoot != null)
        {
            for (int i = 0; i < fixedLoot.Length; i++)
            {
                if (fixedLoot[i].item != null)
                    return true;
            }
        }

        if (randomLoot != null)
        {
            for (int i = 0; i < randomLoot.Length; i++)
            {
                if (randomLoot[i].item != null)
                    return true;
            }
        }

        return false;
    }

    void StopSpawnRoutine()
    {
        if (_spawnRoutine == null)
            return;

        StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
    }

    IEnumerator SpawnLootAfterDelay()
    {
        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        Vector3 position = transform.position;
        GameObject instance = Instantiate(lootPrefab, position, Quaternion.identity);
        if (instance != null && instance.TryGetComponent(out InteractableLoot loot))
        {
            loot.ApplyLootConfiguration(fixedLoot, randomLoot);
            loot.ApplyDespawnAfterLoot(destroyLootAfterOpened, destroyLootDelay);
        }

        _spawnRoutine = null;
    }
}
