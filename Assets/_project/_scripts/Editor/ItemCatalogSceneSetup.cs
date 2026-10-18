#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates <see cref="ItemCatalog"/> + sample <see cref="ItemData"/> and wires
/// <see cref="ItemCatalogBootstrap"/> on init.unity.
/// </summary>
[InitializeOnLoad]
public static class ItemCatalogSceneSetup
{
    public const string CatalogAssetPath = "Assets/_project/_data/items/ItemCatalog.asset";
    public const string SampleItemAssetPath = "Assets/_project/_data/items/item_sample_000.asset";
    public const string SampleItemId = "item_sample_000";

    const string MenuPath = "Tools/Items/Setup Item Catalog";
    /// <summary>Battleground hosts WeaponCatalogBootstrap; item bootstrap is placed alongside it.</summary>
    const string BootstrapScenePath = "Assets/_project/_scenes/battleground.unity";
    const string BootstrapObjectName = "ItemCatalogBootstrap";
    const string SetupDonePrefKey = "LuckyDungeon.ItemCatalogSceneSetupDone.v2";
    const string IconsTexturePath = "Assets/_project/_art/_textures/icons_0.png";

    static ItemCatalogSceneSetup()
    {
        EditorApplication.delayCall += TryPersistInBootstrapScene;
    }

    [MenuItem(MenuPath)]
    public static void SetupMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        SetupAll();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    public static void SetupAll()
    {
        ItemCatalog catalog = EnsureCatalogWithSample();
        if (catalog == null)
        {
            Debug.LogError("[ItemCatalogSceneSetup] Failed to create item catalog asset.");
            return;
        }

        if (!System.IO.File.Exists(BootstrapScenePath))
        {
            Debug.LogError($"[ItemCatalogSceneSetup] Scene not found at '{BootstrapScenePath}'.");
            return;
        }

        string previousScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        EnsureBootstrapInScene(scene, catalog);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        RestorePreviousScene(previousScenePath);
        Debug.Log("[ItemCatalogSceneSetup] Item catalog asset ensured and bootstrap wired on battleground.unity.");
    }

    static void TryPersistInBootstrapScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryPersistInBootstrapScene;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        if (!System.IO.File.Exists(BootstrapScenePath))
            return;

        ItemCatalog catalog = EnsureCatalogWithSample();
        if (catalog == null)
            return;

        string previousScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        EnsureBootstrapInScene(scene, catalog);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
        RestorePreviousScene(previousScenePath);
        Debug.Log("[ItemCatalogSceneSetup] Saved item catalog bootstrap to battleground.unity.");
    }

    public static ItemCatalog EnsureCatalogWithSample()
    {
        EnsureFolder("Assets/_project/_data");
        EnsureFolder("Assets/_project/_data/items");

        ItemData sample = EnsureSampleItem();
        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        // Keep every ItemData under _data/items registered so scene pickups resolve by id.
        string[] itemGuids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/_project/_data/items" });
        var items = new System.Collections.Generic.List<ItemData>(itemGuids.Length);
        for (int i = 0; i < itemGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null && !items.Contains(item))
                items.Add(item);
        }

        if (sample != null && !items.Contains(sample))
            items.Insert(0, sample);

        var so = new SerializedObject(catalog);
        SerializedProperty itemsProp = so.FindProperty("items");
        if (itemsProp != null)
        {
            itemsProp.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        return catalog;
    }

    static ItemData EnsureSampleItem()
    {
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(SampleItemAssetPath);
        if (existing != null)
            return existing;

        var asset = ScriptableObject.CreateInstance<ItemData>();
        AssetDatabase.CreateAsset(asset, SampleItemAssetPath);

        Sprite preview = LoadFirstIconSprite();
        var so = new SerializedObject(asset);
        so.FindProperty("itemId").stringValue = SampleItemId;
        so.FindProperty("displayName").stringValue = "Sample Item";
        so.FindProperty("rarity").enumValueIndex = (int)ItemRarity.Common;
        so.FindProperty("previewImage").objectReferenceValue = preview;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static Sprite LoadFirstIconSprite()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(IconsTexturePath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                return sprite;
        }

        return null;
    }

    static void EnsureBootstrapInScene(Scene scene, ItemCatalog catalog)
    {
        ItemCatalogBootstrap bootstrap = FindBootstrapInScene(scene);
        if (bootstrap == null)
        {
            var go = new GameObject(BootstrapObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create Item Catalog Bootstrap");
            bootstrap = go.AddComponent<ItemCatalogBootstrap>();
        }

        var so = new SerializedObject(bootstrap);
        SerializedProperty catalogProp = so.FindProperty("catalog");
        if (catalogProp != null && catalogProp.objectReferenceValue != catalog)
        {
            catalogProp.objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }
    }

    static ItemCatalogBootstrap FindBootstrapInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ItemCatalogBootstrap[] found = root.GetComponentsInChildren<ItemCatalogBootstrap>(true);
            if (found.Length > 0)
                return found[0];
        }

        return null;
    }

    static void RestorePreviousScene(string previousScenePath)
    {
        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != BootstrapScenePath)
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
