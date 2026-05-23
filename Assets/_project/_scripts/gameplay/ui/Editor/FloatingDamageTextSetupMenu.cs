#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click scene setup: overlay canvas, presenter, and a pooled TMP template (assigns serialized fields).
/// </summary>
public static class FloatingDamageTextSetupMenu
{
    const string MenuPath = "Tools/UI/Create Floating Damage Text Setup";

    [MenuItem(MenuPath)]
    public static void CreateFloatingDamageTextSetup()
    {
        if (Object.FindFirstObjectByType<FloatingDamageTextPresenter>() != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Floating damage text",
                    "A FloatingDamageTextPresenter already exists in the open scene. Create another setup anyway?",
                    "Create",
                    "Cancel"))
                return;
        }

        var canvasGo = new GameObject("DamageNumbersCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Damage Numbers Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var presenterGo = new GameObject("FloatingDamageTextPresenter");
        Undo.RegisterCreatedObjectUndo(presenterGo, "Create Floating Damage Text Presenter");
        presenterGo.transform.SetParent(canvasGo.transform, false);
        var presenter = presenterGo.AddComponent<FloatingDamageTextPresenter>();

        var template = new GameObject("DamageNumberTemplate");
        Undo.RegisterCreatedObjectUndo(template, "Create Damage Number Template");
        template.transform.SetParent(canvasGo.transform, false);
        var rt = template.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(240f, 90f);
        var tmp = template.AddComponent<TextMeshProUGUI>();
        tmp.text = "0";
        tmp.fontSize = 42f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        template.AddComponent<CanvasGroup>();
        var instance = template.AddComponent<FloatingDamageTextInstance>();
        template.SetActive(false);

        using (var so = new SerializedObject(presenter))
        {
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("worldCamera").objectReferenceValue = null;
            so.FindProperty("prefab").objectReferenceValue = instance;
            so.FindProperty("poolParent").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Selection.activeGameObject = canvasGo;
        Debug.Log($"[{nameof(FloatingDamageTextSetupMenu)}] Created '{canvasGo.name}'. Assign World Camera on the presenter if you use a non-main camera.");
    }
}
#endif
