#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="ControlsHelperAssistanceFadeView"/> is present on the controls helper prefab.
/// </summary>
static class ControlsHelperAssistanceFadeViewPrefabInstaller
{
    const string PrefabPath = "Assets/_project/_prefabs/ui/ui_ControlsHelperAssistance.prefab";
    const string SessionKey = "ControlsHelperAssistanceFadeViewPrefabInstaller_v1";

    [InitializeOnLoadMethod]
    static void ScheduleInstall()
    {
        EditorApplication.delayCall += EnsurePrefabWired;
    }

    static void EnsurePrefabWired()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsurePrefabWired;
            return;
        }

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabRoot == null)
            return;

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefabRoot);

        ControlsHelperAssistanceFadeView fadeView = prefabRoot.GetComponent<ControlsHelperAssistanceFadeView>();
        CanvasGroup canvasGroup = prefabRoot.GetComponent<CanvasGroup>();
        bool dirty = false;

        if (fadeView == null)
        {
            fadeView = prefabRoot.AddComponent<ControlsHelperAssistanceFadeView>();
            dirty = true;
        }

        using (var so = new SerializedObject(fadeView))
        {
            SerializedProperty canvasGroupProp = so.FindProperty("canvasGroup");
            if (canvasGroupProp != null && canvasGroupProp.objectReferenceValue != canvasGroup)
            {
                canvasGroupProp.objectReferenceValue = canvasGroup;
                dirty = true;
            }

            SerializedProperty displayDurationProp = so.FindProperty("displayDuration");
            if (displayDurationProp != null && displayDurationProp.floatValue <= 0f)
            {
                displayDurationProp.floatValue = 3f;
                dirty = true;
            }

            SerializedProperty fadeOutDurationProp = so.FindProperty("fadeOutDuration");
            if (fadeOutDurationProp != null && fadeOutDurationProp.floatValue <= 0f)
            {
                fadeOutDurationProp.floatValue = 0.75f;
                dirty = true;
            }

            if (so.ApplyModifiedPropertiesWithoutUndo())
                dirty = true;
        }

        if (dirty)
            PrefabUtility.SavePrefabAsset(prefabRoot);

        SessionState.SetBool(SessionKey, true);
    }
}
#endif
