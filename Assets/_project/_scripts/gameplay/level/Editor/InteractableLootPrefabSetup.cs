#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures <see cref="InteractableLoot"/> is on loot_base with prompt/progress refs wired.
/// </summary>
[InitializeOnLoad]
public static class InteractableLootPrefabSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/level/loot_base.prefab";

    static InteractableLootPrefabSetup()
    {
        EditorApplication.delayCall += EnsureOnPrefab;
    }

    [MenuItem("Tools/Loot/Ensure InteractableLoot On Base Prefab")]
    public static void EnsureOnPrefabMenu()
    {
        EnsureOnPrefab();
    }

    static void EnsureOnPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            bool added = false;
            var loot = root.GetComponent<InteractableLoot>();
            if (loot == null)
            {
                loot = root.AddComponent<InteractableLoot>();
                added = true;
            }

            Transform ui = root.transform.Find("ui");
            Transform searchable = ui != null ? ui.Find("searchable") : null;
            Transform clear = ui != null ? ui.Find("clear") : null;
            Transform progress = EnsureProgressUi(ui, out bool progressCreated);

            var so = new SerializedObject(loot);
            SerializedProperty promptRoot = so.FindProperty("promptRoot");
            SerializedProperty searchableRoot = so.FindProperty("searchableRoot");
            SerializedProperty clearRoot = so.FindProperty("clearRoot");
            SerializedProperty progressRoot = so.FindProperty("progressRoot");
            SerializedProperty progressFill = so.FindProperty("progressFill");
            SerializedProperty looted = so.FindProperty("looted");

            bool changed = added || progressCreated;

            if (promptRoot != null && promptRoot.objectReferenceValue == null && ui != null)
            {
                promptRoot.objectReferenceValue = ui.gameObject;
                changed = true;
            }

            if (searchableRoot != null && searchableRoot.objectReferenceValue == null && searchable != null)
            {
                searchableRoot.objectReferenceValue = searchable.gameObject;
                changed = true;
            }

            if (clearRoot != null && clearRoot.objectReferenceValue == null && clear != null)
            {
                clearRoot.objectReferenceValue = clear.gameObject;
                changed = true;
            }

            if (progressRoot != null && progress != null && progressRoot.objectReferenceValue != progress.gameObject)
            {
                progressRoot.objectReferenceValue = progress.gameObject;
                changed = true;
            }

            Image fillImage = progress != null ? progress.Find("fill")?.GetComponent<Image>() : null;
            if (progressFill != null && fillImage != null && progressFill.objectReferenceValue != fillImage)
            {
                progressFill.objectReferenceValue = fillImage;
                changed = true;
            }

            if (looted != null && looted.boolValue)
            {
                looted.boolValue = false;
                changed = true;
            }

            if (progress != null && progress.gameObject.activeSelf)
            {
                progress.gameObject.SetActive(false);
                changed = true;
            }

            if (!changed)
                return;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[InteractableLootPrefabSetup] Wired InteractableLoot on " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform EnsureProgressUi(Transform ui, out bool created)
    {
        created = false;
        if (ui == null)
            return null;

        Transform progress = ui.Find("progress");
        if (progress == null)
        {
            var progressGo = new GameObject("progress", typeof(RectTransform));
            progressGo.layer = ui.gameObject.layer;
            progress = progressGo.transform;
            progress.SetParent(ui, false);

            var progressRect = progressGo.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.5f, 0.5f);
            progressRect.anchorMax = new Vector2(0.5f, 0.5f);
            progressRect.pivot = new Vector2(0.5f, 0.5f);
            progressRect.sizeDelta = new Vector2(180f, 24f);
            progressRect.anchoredPosition = new Vector2(0f, -40f);

            created = true;
        }

        Transform bg = progress.Find("bg");
        if (bg == null)
        {
            var bgGo = new GameObject("bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.layer = progress.gameObject.layer;
            bg = bgGo.transform;
            bg.SetParent(progress, false);

            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bgGo.GetComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.65f);
            bgImage.raycastTarget = false;
            created = true;
        }

        Transform fill = progress.Find("fill");
        if (fill == null)
        {
            var fillGo = new GameObject("fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.layer = progress.gameObject.layer;
            fill = fillGo.transform;
            fill.SetParent(progress, false);

            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = new Color(0.95f, 0.78f, 0.2f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;
            created = true;
        }
        else
        {
            var fillImage = fill.GetComponent<Image>();
            if (fillImage != null && fillImage.type != Image.Type.Filled)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                created = true;
            }
        }

        if (progress.gameObject.activeSelf)
        {
            progress.gameObject.SetActive(false);
            created = true;
        }

        return progress;
    }
}
#endif
