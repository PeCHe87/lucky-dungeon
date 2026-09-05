#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces misplaced UiDashButtonBinder on btnSwapWeapon with UiSwapWeaponButtonBinder once.
/// </summary>
[InitializeOnLoad]
public static class SwapWeaponButtonSceneSetup
{
    const string InitScenePath = "Assets/_project/_scenes/init.unity";
    const string SetupDonePrefKey = "LuckyDungeon.SwapWeaponButtonSceneSetupDone";

    static SwapWeaponButtonSceneSetup()
    {
        EditorApplication.delayCall += TryPersistInInitScene;
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

        string previousScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(InitScenePath, OpenSceneMode.Single);

        GameObject swapButton = FindChildByName(scene, "btnSwapWeapon");
        if (swapButton == null)
        {
            RestorePreviousScene(previousScenePath);
            return;
        }

        bool dirty = false;

        UiDashButtonBinder dashBinder = swapButton.GetComponent<UiDashButtonBinder>();
        if (dashBinder != null)
        {
            Object.DestroyImmediate(dashBinder);
            dirty = true;
        }

        if (swapButton.GetComponent<UiSwapWeaponButtonBinder>() == null)
        {
            swapButton.AddComponent<UiSwapWeaponButtonBinder>();
            dirty = true;
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SwapWeaponButtonSceneSetup] Wired UiSwapWeaponButtonBinder on btnSwapWeapon.");
        }

        EditorPrefs.SetBool(SetupDonePrefKey, true);
        RestorePreviousScene(previousScenePath);
    }

    static GameObject FindChildByName(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (TryFindChildRecursive(roots[i].transform, name, out GameObject found))
                return found;
        }

        return null;
    }

    static bool TryFindChildRecursive(Transform parent, string name, out GameObject found)
    {
        if (parent.name == name)
        {
            found = parent.gameObject;
            return true;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            if (TryFindChildRecursive(parent.GetChild(i), name, out found))
                return true;
        }

        found = null;
        return false;
    }

    static void RestorePreviousScene(string previousScenePath)
    {
        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != InitScenePath)
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
    }
}
#endif
