#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the empty <see cref="WeaponCatalog"/> asset and wires <see cref="WeaponCatalogBootstrap"/> on init.unity.
/// Auto-ensures once scripts are ready; also available via menu.
/// </summary>
[InitializeOnLoad]
public static class WeaponCatalogSceneSetup
{
    public const string CatalogAssetPath = "Assets/_project/_data/weapons/WeaponCatalog.asset";
    const string MenuPath = "Tools/Combat/Setup Weapon Catalog";
    const string InitScenePath = "Assets/_project/_scenes/init.unity";
    const string BootstrapObjectName = "WeaponCatalogBootstrap";
    const string SetupDonePrefKey = "LuckyDungeon.WeaponCatalogSceneSetupDone";

    static WeaponCatalogSceneSetup()
    {
        EditorApplication.delayCall += TryPersistInInitScene;
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
        WeaponCatalog catalog = EnsureCatalogAsset();
        if (catalog == null)
        {
            Debug.LogError("[WeaponCatalogSceneSetup] Failed to create weapon catalog asset.");
            return;
        }

        if (!System.IO.File.Exists(InitScenePath))
        {
            Debug.LogError($"[WeaponCatalogSceneSetup] Init scene not found at '{InitScenePath}'.");
            return;
        }

        string previousScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(InitScenePath, OpenSceneMode.Single);
        EnsureBootstrapInScene(scene, catalog);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        RestorePreviousScene(previousScenePath);
        Debug.Log("[WeaponCatalogSceneSetup] Weapon catalog asset ensured and bootstrap wired on init.unity.");
    }

    static void TryPersistInInitScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryPersistInInitScene;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        if (!System.IO.File.Exists(InitScenePath))
            return;

        WeaponCatalog catalog = EnsureCatalogAsset();
        if (catalog == null)
            return;

        string previousScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(InitScenePath, OpenSceneMode.Single);
        EnsureBootstrapInScene(scene, catalog);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
        RestorePreviousScene(previousScenePath);
        Debug.Log("[WeaponCatalogSceneSetup] Saved weapon catalog bootstrap to init.unity.");
    }

    public static WeaponCatalog EnsureCatalogAsset()
    {
        EnsureFolder("Assets/_project/_data");
        EnsureFolder("Assets/_project/_data/weapons");

        var asset = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogAssetPath);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<WeaponCatalog>();
        AssetDatabase.CreateAsset(asset, CatalogAssetPath);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void EnsureBootstrapInScene(Scene scene, WeaponCatalog catalog)
    {
        WeaponCatalogBootstrap bootstrap = FindBootstrapInScene(scene);
        if (bootstrap == null)
        {
            var go = new GameObject(BootstrapObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create Weapon Catalog Bootstrap");
            bootstrap = go.AddComponent<WeaponCatalogBootstrap>();
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

    static WeaponCatalogBootstrap FindBootstrapInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            WeaponCatalogBootstrap[] found = root.GetComponentsInChildren<WeaponCatalogBootstrap>(true);
            if (found.Length > 0)
                return found[0];
        }

        return null;
    }

    static void RestorePreviousScene(string previousScenePath)
    {
        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != InitScenePath)
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
