#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wires tap-to-attack controls in init.unity: screen tap panel, joystick tap/drag gate, disables legacy attack/dash UI.
/// </summary>
static class TapAttackControlsSceneInstaller
{
    const string ScenePath = "Assets/_project/_scenes/init.unity";
    const string SessionKey = "TapAttackControlsSceneInstaller_v1";
    const string ScreenTapObjectName = "screenTapAttackInput";

    [MenuItem("Tools/Lucky Dungeon/Install Tap Attack Controls")]
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

        Transform controls = FindChildByName(scene, "controls")?.transform;
        if (controls == null)
            goto SaveAndExit;

        dirty |= EnsureScreenTapPanel(controls);
        dirty |= EnsureJoystickGate(scene);
        dirty |= DeactivateIfActive(controls, "btnAttack");

    SaveAndExit:
        if (dirty)
            EditorSceneManager.SaveScene(scene);

        if (!wasLoaded && scene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);

        SessionState.SetBool(SessionKey, true);
    }

    static bool EnsureScreenTapPanel(Transform controls)
    {
        Transform existing = controls.Find(ScreenTapObjectName);
        GameObject tapObject;

        if (existing != null)
        {
            tapObject = existing.gameObject;
        }
        else
        {
            tapObject = new GameObject(ScreenTapObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScreenTapAttackInput));
            tapObject.transform.SetParent(controls, false);
            tapObject.layer = controls.gameObject.layer;

            RectTransform rect = tapObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
        }

        Image image = tapObject.GetComponent<Image>();
        if (image != null)
        {
            Color c = image.color;
            if (c.a != 0f)
            {
                c.a = 0f;
                image.color = c;
            }

            if (!image.raycastTarget)
                image.raycastTarget = true;
        }

        ScreenTapAttackInput input = tapObject.GetComponent<ScreenTapAttackInput>();
        if (input == null)
            input = tapObject.AddComponent<ScreenTapAttackInput>();

        FeneraxJoystickMoveIntentProvider provider = Object.FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();
        bool changed = false;
        using (var so = new SerializedObject(input))
        {
            SerializedProperty providerProp = so.FindProperty("intentProvider");
            if (providerProp != null && providerProp.objectReferenceValue != provider)
            {
                providerProp.objectReferenceValue = provider;
                changed = true;
            }

            if (so.ApplyModifiedPropertiesWithoutUndo())
                changed = true;
        }

        return existing == null || changed;
    }

    static bool EnsureJoystickGate(Scene scene)
    {
        Joystick[] joysticks = Object.FindObjectsByType<Joystick>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool changed = false;

        for (int i = 0; i < joysticks.Length; i++)
        {
            Joystick joystick = joysticks[i];
            if (joystick.gameObject.scene != scene)
                continue;

            JoystickDoubleTapBridge bridge = joystick.GetComponent<JoystickDoubleTapBridge>();
            if (bridge != null)
            {
                Object.DestroyImmediate(bridge);
                changed = true;
            }

            TapOrDragJoystickGate gate = joystick.GetComponent<TapOrDragJoystickGate>();
            if (gate == null)
            {
                gate = joystick.gameObject.AddComponent<TapOrDragJoystickGate>();
                changed = true;
            }

            FeneraxJoystickMoveIntentProvider provider = Object.FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();
            using (var so = new SerializedObject(gate))
            {
                SerializedProperty providerProp = so.FindProperty("intentProvider");
                if (providerProp != null && providerProp.objectReferenceValue != provider)
                {
                    providerProp.objectReferenceValue = provider;
                    changed = true;
                }

                if (so.ApplyModifiedPropertiesWithoutUndo())
                    changed = true;
            }
        }

        return changed;
    }

    static bool DeactivateIfActive(Transform parent, string objectName)
    {
        Transform target = parent.Find(objectName);
        if (target == null || !target.gameObject.activeSelf)
            return false;

        target.gameObject.SetActive(false);
        return true;
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
