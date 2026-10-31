#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures enemy combat prefabs have <see cref="EntityRagdoll"/> and strips any pre-authored
/// bone Rigidbodies/joints (ragdoll is built at runtime on death only).
/// Never touches the player prefab.
/// </summary>
[InitializeOnLoad]
public static class EntityRagdollPrefabSetup
{
    const string SetupDonePrefKey = "LuckyDungeon.EntityRagdollPrefabSetupDone.v2";

    static readonly string[] EnemyPrefabPaths =
    {
        "Assets/_project/_prefabs/entities/base_combat_entity.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab",
        "Assets/_project/_prefabs/entities/enemy_melee_patroller.prefab",
    };

    static EntityRagdollPrefabSetup()
    {
        EditorApplication.delayCall += TryAutoSetupOnce;
    }

    static void TryAutoSetupOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoSetupOnce;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        if (SetupAll())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    [MenuItem("Lucky Dungeon/Setup/Entity Ragdoll Prefabs")]
    public static void SetupFromMenu()
    {
        if (SetupAll())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    static bool SetupAll()
    {
        int updated = 0;
        for (int i = 0; i < EnemyPrefabPaths.Length; i++)
        {
            if (SetupPrefab(EnemyPrefabPaths[i]))
                updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[EntityRagdollPrefabSetup] Stripped bone physics + ensured EntityRagdoll on {updated}/{EnemyPrefabPaths.Length} enemy prefabs.");
        return updated > 0;
    }

    static bool SetupPrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogWarning($"[EntityRagdollPrefabSetup] Missing prefab: {path}");
            return false;
        }

        try
        {
            if (root.GetComponent<PlayerDeathHandler>() != null)
            {
                Debug.LogWarning($"[EntityRagdollPrefabSetup] Skipping player-like prefab: {path}");
                return false;
            }

            ClearExistingRagdollParts(root);

            var ragdoll = root.GetComponent<EntityRagdoll>();
            if (ragdoll == null)
                ragdoll = root.AddComponent<EntityRagdoll>();

            WireEntityRagdoll(ragdoll, root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ClearExistingRagdollParts(GameObject root)
    {
        var joints = root.GetComponentsInChildren<CharacterJoint>(true);
        for (int i = 0; i < joints.Length; i++)
            Object.DestroyImmediate(joints[i]);

        var bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null && bodies[i].gameObject != root)
                Object.DestroyImmediate(bodies[i]);
        }

        var capsules = root.GetComponentsInChildren<CapsuleCollider>(true);
        for (int i = 0; i < capsules.Length; i++)
        {
            if (capsules[i] != null && capsules[i].gameObject != root)
                Object.DestroyImmediate(capsules[i]);
        }
    }

    static void WireEntityRagdoll(EntityRagdoll ragdoll, GameObject root)
    {
        var so = new SerializedObject(ragdoll);
        so.FindProperty("health").objectReferenceValue = root.GetComponent<CombatEntityHealth>();
        so.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>(true);
        so.FindProperty("agent").objectReferenceValue = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
        so.FindProperty("rootGameplayCollider").objectReferenceValue = root.GetComponent<CapsuleCollider>();
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ragdoll);
    }
}
#endif
