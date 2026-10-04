#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="PlayerRunStateBinder"/> is on the player prefab.
/// </summary>
[InitializeOnLoad]
static class PlayerRunStateBinderPrefabSetup
{
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";

    static PlayerRunStateBinderPrefabSetup()
    {
        EditorApplication.delayCall += EnsureBinder;
    }

    [MenuItem("Tools/Player/Setup Player Run State Binder")]
    static void SetupMenu() => EnsureBinder();

    static void EnsureBinder()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureBinder;
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            CombatEntityHealth health = root.GetComponent<CombatEntityHealth>();
            if (health == null)
            {
                Debug.LogWarning("[PlayerRunStateBinderPrefabSetup] Skipped (no CombatEntityHealth).");
                return;
            }

            var binder = root.GetComponent<PlayerRunStateBinder>();
            bool changed = false;

            if (binder == null)
            {
                binder = root.AddComponent<PlayerRunStateBinder>();
                changed = true;
            }

            var so = new SerializedObject(binder);
            SerializedProperty healthProp = so.FindProperty("health");
            if (healthProp != null && healthProp.objectReferenceValue != health)
            {
                healthProp.objectReferenceValue = health;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[PlayerRunStateBinderPrefabSetup] Ensured PlayerRunStateBinder on player prefab.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
