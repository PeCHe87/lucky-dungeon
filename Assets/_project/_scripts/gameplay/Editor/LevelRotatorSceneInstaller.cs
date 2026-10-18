#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wires hold-to-rotate camera yaw controls under ui_levelRotator in battleground.unity.
/// </summary>
static class LevelRotatorSceneInstaller
{
    const string ScenePath = "Assets/_project/_scenes/battleground.unity";
    const string SessionKey = "LevelRotatorSceneInstaller_v1";
    const string PanelName = "ui_levelRotator";
    const string LeftButtonName = "btn_rotateLeft";
    const string RightButtonName = "btn_rotateRight";

    [MenuItem("Tools/Lucky Dungeon/Install Level Rotator Controls")]
    static void InstallFromMenu()
    {
        SessionState.SetBool(SessionKey, false);
        EnsureSceneWired();
    }

    [InitializeOnLoadMethod]
    static void ScheduleInstall()
    {
        EditorApplication.delayCall += EnsureSceneWired;
    }

    static void EnsureSceneWired()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureSceneWired;
            return;
        }

        Scene activeScene = EditorSceneManager.GetActiveScene();
        bool wasLoaded = activeScene.path == ScenePath;
        Scene scene;

        if (wasLoaded)
        {
            scene = activeScene;
        }
        else
        {
            if (!System.IO.File.Exists(ScenePath))
                return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        bool dirty = false;

        GameObject panelObject = FindChildByName(scene, PanelName);
        if (panelObject == null)
            goto SaveAndExit;

        dirty |= EnsureRotator(panelObject);
        dirty |= EnsureRotateButton(panelObject.transform, LeftButtonName, -1f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f));
        dirty |= EnsureRotateButton(panelObject.transform, RightButtonName, 1f, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f));

    SaveAndExit:
        if (dirty)
            EditorSceneManager.SaveScene(scene);

        if (!wasLoaded && scene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);

        SessionState.SetBool(SessionKey, true);
    }

    static bool EnsureRotator(GameObject panelObject)
    {
        bool changed = false;

        UiLevelRotator rotator = panelObject.GetComponent<UiLevelRotator>();
        if (rotator == null)
        {
            rotator = panelObject.AddComponent<UiLevelRotator>();
            changed = true;
        }

        ArcheroStyleCameraFollow cameraFollow = Object.FindFirstObjectByType<ArcheroStyleCameraFollow>();
        using (var so = new SerializedObject(rotator))
        {
            SerializedProperty cameraProp = so.FindProperty("cameraFollow");
            if (cameraProp != null && cameraProp.objectReferenceValue != cameraFollow)
            {
                cameraProp.objectReferenceValue = cameraFollow;
                changed = true;
            }

            SerializedProperty speedProp = so.FindProperty("rotationSpeedDegreesPerSecond");
            if (speedProp != null && Mathf.Approximately(speedProp.floatValue, 0f))
            {
                speedProp.floatValue = 90f;
                changed = true;
            }

            if (so.ApplyModifiedPropertiesWithoutUndo())
                changed = true;
        }

        return changed;
    }

    static bool EnsureRotateButton(
        Transform panel,
        string buttonName,
        float direction,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition)
    {
        bool changed = false;
        Transform existing = panel.Find(buttonName);
        GameObject buttonObject;

        if (existing != null)
        {
            buttonObject = existing.gameObject;
        }
        else
        {
            buttonObject = new GameObject(
                buttonName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(UiLevelRotateHoldButton));
            buttonObject.transform.SetParent(panel, false);
            buttonObject.layer = panel.gameObject.layer;
            changed = true;
        }

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(88f, 88f);
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        if (image != null)
        {
            if (!image.raycastTarget)
            {
                image.raycastTarget = true;
                changed = true;
            }

            // Placeholder visible tint so hold targets are obvious until art is assigned.
            Color targetColor = direction < 0f
                ? new Color(0.85f, 0.85f, 0.9f, 0.65f)
                : new Color(0.9f, 0.85f, 0.85f, 0.65f);
            if (image.color != targetColor)
            {
                image.color = targetColor;
                changed = true;
            }
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
            changed = true;
        }

        if (button.targetGraphic != image)
        {
            button.targetGraphic = image;
            changed = true;
        }

        UiLevelRotateHoldButton hold = buttonObject.GetComponent<UiLevelRotateHoldButton>();
        if (hold == null)
        {
            hold = buttonObject.AddComponent<UiLevelRotateHoldButton>();
            changed = true;
        }

        UiLevelRotator rotator = panel.GetComponent<UiLevelRotator>();
        using (var so = new SerializedObject(hold))
        {
            SerializedProperty rotatorProp = so.FindProperty("rotator");
            if (rotatorProp != null && rotatorProp.objectReferenceValue != rotator)
            {
                rotatorProp.objectReferenceValue = rotator;
                changed = true;
            }

            SerializedProperty directionProp = so.FindProperty("direction");
            if (directionProp != null && !Mathf.Approximately(directionProp.floatValue, direction))
            {
                directionProp.floatValue = direction;
                changed = true;
            }

            if (so.ApplyModifiedPropertiesWithoutUndo())
                changed = true;
        }

        return changed || existing == null;
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
}
#endif
