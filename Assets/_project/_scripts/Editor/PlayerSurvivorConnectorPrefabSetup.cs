#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="PlayerSurvivorConnector"/> and default follow-slot Transforms on the player prefab.
/// </summary>
[InitializeOnLoad]
static class PlayerSurvivorConnectorPrefabSetup
{
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string SlotsRootName = "SurvivorFollowSlots";
    const int DefaultSlotCount = 4;

    static readonly Vector3[] DefaultSlotLocalOffsets =
    {
        new Vector3(-1.25f, 0f, -1.5f),
        new Vector3(1.25f, 0f, -1.5f),
        new Vector3(-2.25f, 0f, -2.25f),
        new Vector3(2.25f, 0f, -2.25f),
    };

    static PlayerSurvivorConnectorPrefabSetup()
    {
        EditorApplication.delayCall += EnsureConnector;
    }

    [MenuItem("Tools/Player/Setup Player Survivor Connector")]
    static void SetupMenu() => EnsureConnector();

    static void EnsureConnector()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureConnector;
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (root.GetComponent<CombatEntityHealth>() == null)
            {
                Debug.LogWarning("[PlayerSurvivorConnectorPrefabSetup] Skipped (no CombatEntityHealth).");
                return;
            }

            bool changed = false;

            var connector = root.GetComponent<PlayerSurvivorConnector>();
            if (connector == null)
            {
                connector = root.AddComponent<PlayerSurvivorConnector>();
                changed = true;
            }

            Transform slotsRoot = root.transform.Find(SlotsRootName);
            if (slotsRoot == null)
            {
                var slotsGo = new GameObject(SlotsRootName);
                slotsGo.transform.SetParent(root.transform, false);
                slotsGo.transform.localPosition = Vector3.zero;
                slotsGo.transform.localRotation = Quaternion.identity;
                slotsGo.transform.localScale = Vector3.one;
                slotsRoot = slotsGo.transform;
                changed = true;
            }

            var slots = new Transform[DefaultSlotCount];
            for (int i = 0; i < DefaultSlotCount; i++)
            {
                string slotName = $"Slot_{i}";
                Transform slot = slotsRoot.Find(slotName);
                if (slot == null)
                {
                    var slotGo = new GameObject(slotName);
                    slotGo.transform.SetParent(slotsRoot, false);
                    slotGo.transform.localPosition = DefaultSlotLocalOffsets[i];
                    slotGo.transform.localRotation = Quaternion.identity;
                    slotGo.transform.localScale = Vector3.one;
                    slot = slotGo.transform;
                    changed = true;
                }

                slots[i] = slot;
            }

            var so = new SerializedObject(connector);
            SerializedProperty slotsProp = so.FindProperty("followSlots");
            if (slotsProp != null)
            {
                bool slotsNeedAssign = slotsProp.arraySize != DefaultSlotCount;
                if (!slotsNeedAssign)
                {
                    for (int i = 0; i < DefaultSlotCount; i++)
                    {
                        if (slotsProp.GetArrayElementAtIndex(i).objectReferenceValue != slots[i])
                        {
                            slotsNeedAssign = true;
                            break;
                        }
                    }
                }

                if (slotsNeedAssign)
                {
                    slotsProp.arraySize = DefaultSlotCount;
                    for (int i = 0; i < DefaultSlotCount; i++)
                        slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[PlayerSurvivorConnectorPrefabSetup] Ensured PlayerSurvivorConnector + follow slots on player prefab.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
