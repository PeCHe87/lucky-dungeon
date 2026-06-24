#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists ScreensManager and GameOverScreen setup into init.unity once after scripts compile.
/// </summary>
[InitializeOnLoad]
public static class ScreensManagerSceneSetup
{
    const string InitScenePath = "Assets/_project/_scenes/init.unity";
    const string SetupDonePrefKey = "LuckyDungeon.ScreensManagerSceneSetupDone";

    static ScreensManagerSceneSetup()
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

        Transform screensTransform = GameObject.Find("ui")?.transform.Find("screens");
        Transform gameOverTransform = screensTransform != null
            ? screensTransform.Find("screen_gameOver")
            : null;

        if (screensTransform == null || gameOverTransform == null)
        {
            RestorePreviousScene(previousScenePath);
            return;
        }

        bool dirty = false;

        if (screensTransform.GetComponent<ScreensManager>() == null)
        {
            screensTransform.gameObject.AddComponent<ScreensManager>();
            dirty = true;
        }

        if (gameOverTransform.GetComponent<GameOverScreen>() == null)
        {
            gameOverTransform.gameObject.AddComponent<GameOverScreen>();
            dirty = true;
        }

        if (gameOverTransform.gameObject.activeSelf)
        {
            gameOverTransform.gameObject.SetActive(false);
            dirty = true;
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ScreensManagerSceneSetup] Saved screens manager setup to init.unity.");
        }

        EditorPrefs.SetBool(SetupDonePrefKey, true);
        RestorePreviousScene(previousScenePath);
    }

    static void RestorePreviousScene(string previousScenePath)
    {
        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != InitScenePath)
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
    }
}
#endif
