#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates <see cref="healthCollectible"/> prefab variant and swaps scene placeholders to it.
/// </summary>
[InitializeOnLoad]
public static class CollectiblePrefabSetup
{
    const string CollectableBasePath = "Assets/_project/_prefabs/battle/collectableBase.prefab";
    const string HealthCollectiblePath = "Assets/_project/_prefabs/battle/healthCollectible.prefab";
    const string InitScenePath = "Assets/_project/_scenes/init.unity";

    static CollectiblePrefabSetup()
    {
        EditorApplication.delayCall += EnsureCollectibleSetup;
    }

    static void EnsureCollectibleSetup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureCollectibleSetup;
            return;
        }

        if (!EnsureHealthCollectiblePrefab())
            return;

        ReplaceCollectableBaseInstancesInInitScene();
    }

    [MenuItem("Tools/Collectibles/Setup Health Collectible Prefab")]
    public static void SetupHealthCollectiblePrefabMenu()
    {
        EnsureHealthCollectiblePrefab(forceLog: true);
    }

    [MenuItem("Tools/Collectibles/Replace collectableBase Instances In init Scene")]
    public static void ReplaceCollectableInstancesMenu()
    {
        if (!EnsureHealthCollectiblePrefab(forceLog: true))
            return;

        ReplaceCollectableBaseInstancesInInitScene(forceLog: true);
    }

    public static void RunFullSetup()
    {
        if (!EnsureHealthCollectiblePrefab(forceLog: true))
            return;

        ReplaceCollectableBaseInstancesInInitScene(forceLog: true);
    }

    static bool EnsureHealthCollectiblePrefab(bool forceLog = false)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CollectableBasePath) == null)
        {
            if (forceLog)
                Debug.LogWarning("[CollectiblePrefabSetup] Missing base prefab: " + CollectableBasePath);
            return false;
        }

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HealthCollectiblePath);
        if (existing != null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HealthCollectiblePath);
            try
            {
                bool changed = false;

                if (root.GetComponent<HealthCollectible>() == null)
                {
                    root.AddComponent<HealthCollectible>();
                    changed = true;
                }

                if (root.name != "healthCollectible")
                {
                    root.name = "healthCollectible";
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, HealthCollectiblePath);
                    if (forceLog)
                        Debug.Log("[CollectiblePrefabSetup] Updated " + HealthCollectiblePath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return true;
        }

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectableBasePath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        instance.name = "healthCollectible";
        instance.AddComponent<HealthCollectible>();
        PrefabUtility.SaveAsPrefabAsset(instance, HealthCollectiblePath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();

        if (forceLog)
            Debug.Log("[CollectiblePrefabSetup] Created " + HealthCollectiblePath);

        return true;
    }

    static void ReplaceCollectableBaseInstancesInInitScene(bool forceLog = false)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(InitScenePath) == null)
            return;

        GameObject healthPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthCollectiblePath);
        if (healthPrefab == null)
            return;

        var scene = EditorSceneManager.OpenScene(InitScenePath);
        var instances = FindPrefabInstances(CollectableBasePath);
        if (instances.Count == 0)
            return;

        foreach (GameObject instance in instances)
        {
            Transform parent = instance.transform.parent;
            Vector3 position = instance.transform.position;
            Quaternion rotation = instance.transform.rotation;
            Vector3 scale = instance.transform.localScale;
            int siblingIndex = instance.transform.GetSiblingIndex();

            Object.DestroyImmediate(instance);

            GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(healthPrefab);
            replacement.transform.SetParent(parent, true);
            replacement.transform.SetPositionAndRotation(position, rotation);
            replacement.transform.localScale = scale;
            replacement.transform.SetSiblingIndex(siblingIndex);
        }

        EditorSceneManager.SaveScene(scene);

        if (forceLog)
            Debug.Log($"[CollectiblePrefabSetup] Replaced {instances.Count} collectableBase instance(s) in init scene.");
    }

    static List<GameObject> FindPrefabInstances(string prefabAssetPath)
    {
        var results = new List<GameObject>();
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            CollectPrefabInstances(root.transform, prefabAssetPath, results);
        return results;
    }

    static void CollectPrefabInstances(Transform node, string prefabAssetPath, List<GameObject> results)
    {
        GameObject nearestRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(node);
        if (nearestRoot == node.gameObject
            && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject) == prefabAssetPath)
        {
            results.Add(node.gameObject);
            return;
        }

        for (int i = 0; i < node.childCount; i++)
            CollectPrefabInstances(node.GetChild(i), prefabAssetPath, results);
    }
}
#endif
