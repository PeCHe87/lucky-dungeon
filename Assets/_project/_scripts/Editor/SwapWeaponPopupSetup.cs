#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures <see cref="SwapWeaponPopup"/> is on the swap-weapon popup prefab and init scene instance.
/// </summary>
[InitializeOnLoad]
public static class SwapWeaponPopupSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/ui/ui_popup_swapWeapon.prefab";
    const string InitScenePath = "Assets/_project/_scenes/init.unity";
    const string PopupObjectName = "ui_popup_swapWeapon";
    const string MenuPath = "Tools/UI/Setup Swap Weapon Popup";
    const string SetupDonePrefKey = "LuckyDungeon.SwapWeaponPopupSetupDone.v2";

    static SwapWeaponPopupSetup()
    {
        EditorApplication.delayCall += TryEnsure;
    }

    [MenuItem(MenuPath)]
    public static void SetupMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        EnsureAll(forceLog: true);
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    static void TryEnsure()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryEnsure;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        if (EnsureAll())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    public static bool EnsureAll(bool forceLog = false)
    {
        bool ok = EnsureOnPrefab(forceLog);
        ok &= EnsureOnInitScene(forceLog);
        return ok;
    }

    static bool EnsureOnPrefab(bool forceLog)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            if (forceLog)
                Debug.LogWarning("[SwapWeaponPopupSetup] Missing prefab: " + PrefabPath);
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.GetComponent<SwapWeaponPopup>() != null)
            {
                if (forceLog)
                    Debug.Log("[SwapWeaponPopupSetup] Prefab already has SwapWeaponPopup.");
                return true;
            }

            root.AddComponent<SwapWeaponPopup>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[SwapWeaponPopupSetup] Added SwapWeaponPopup to " + PrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool EnsureOnInitScene(bool forceLog)
    {
        if (!System.IO.File.Exists(InitScenePath))
        {
            if (forceLog)
                Debug.LogWarning("[SwapWeaponPopupSetup] Missing init scene: " + InitScenePath);
            return false;
        }

        string previousScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(InitScenePath, OpenSceneMode.Single);
        bool dirty = false;

        GameObject popupGo = FindNamed(scene, PopupObjectName);
        if (popupGo == null)
        {
            if (forceLog)
                Debug.LogWarning("[SwapWeaponPopupSetup] No '" + PopupObjectName + "' in init scene.");
            RestorePreviousScene(previousScenePath);
            return false;
        }

        if (popupGo.GetComponent<SwapWeaponPopup>() == null)
        {
            popupGo.AddComponent<SwapWeaponPopup>();
            dirty = true;
            Debug.Log("[SwapWeaponPopupSetup] Added SwapWeaponPopup to init scene instance.");
        }
        else if (forceLog)
        {
            Debug.Log("[SwapWeaponPopupSetup] Init scene popup already has SwapWeaponPopup.");
        }

        // Root must stay active so Awake registers Instance; content starts hidden in the prefab.
        if (!popupGo.activeSelf)
        {
            popupGo.SetActive(true);
            dirty = true;
        }

        Transform content = popupGo.transform.Find("content");
        if (content != null && content.gameObject.activeSelf)
        {
            content.gameObject.SetActive(false);
            dirty = true;
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        RestorePreviousScene(previousScenePath);
        return true;
    }

    static GameObject FindNamed(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;

            Transform found = FindChildRecursive(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    static Transform FindChildRecursive(Transform parent, string objectName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == objectName)
                return child;

            Transform nested = FindChildRecursive(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    static void RestorePreviousScene(string previousScenePath)
    {
        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != InitScenePath)
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
    }
}
#endif
