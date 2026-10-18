#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures <see cref="ItemCollectible"/> and a billboarded world-space icon UI
/// on <c>collectible_item.prefab</c>.
/// </summary>
[InitializeOnLoad]
public static class ItemCollectiblePrefabSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/battle/collectibles/collectible_item.prefab";
    const string MenuPath = "Tools/Collectibles/Setup Item Collectible Prefab";
    const string SetupDonePrefKey = "LuckyDungeon.ItemCollectiblePrefabSetupDone.v3";
    const float CanvasWorldScale = 0.01f;
    const float IconSize = 100f;

    static ItemCollectiblePrefabSetup()
    {
        EditorApplication.delayCall += TryEnsurePrefab;
    }

    [MenuItem(MenuPath)]
    public static void SetupMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        EnsureItemCollectiblePrefab(forceLog: true);
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    /// <summary>Batch/CLI entry: catalog + prefab setup.</summary>
    public static void SetupAllFromCli()
    {
        ItemCatalogSceneSetup.SetupAll();
        EnsureItemCollectiblePrefab(forceLog: true);
        SetBattlegroundSampleItemId();
        AssetDatabase.SaveAssets();
        Debug.Log("[ItemCollectiblePrefabSetup] CLI setup complete.");
    }

    static void TryEnsurePrefab()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryEnsurePrefab;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        ItemCatalogSceneSetup.SetupAll();
        bool prefabOk = EnsureItemCollectiblePrefab();
        SetBattlegroundSampleItemId();
        if (prefabOk)
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    public static bool EnsureItemCollectiblePrefab(bool forceLog = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            if (forceLog)
                Debug.LogWarning("[ItemCollectiblePrefabSetup] Missing prefab: " + PrefabPath);
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            HealthCollectible health = root.GetComponent<HealthCollectible>();
            if (health != null)
                Object.DestroyImmediate(health);

            ItemCollectible item = root.GetComponent<ItemCollectible>();
            if (item == null)
                item = root.AddComponent<ItemCollectible>();

            Image iconImage = EnsureIconHierarchy(root);
            TextMeshProUGUI displayNameLabel = EnsureDisplayNameLabel(root);
            Image rarityImage = EnsureRarityImage(root);
            Sprite[] rarityFrames = LoadRarityFrames();

            var so = new SerializedObject(item);
            SerializedProperty iconProp = so.FindProperty("iconImage");
            if (iconProp != null && iconProp.objectReferenceValue != iconImage)
                iconProp.objectReferenceValue = iconImage;

            SerializedProperty rarityImageProp = so.FindProperty("rarityImage");
            if (rarityImageProp != null && rarityImageProp.objectReferenceValue != rarityImage)
                rarityImageProp.objectReferenceValue = rarityImage;

            SerializedProperty labelProp = so.FindProperty("displayNameLabel");
            if (labelProp != null && labelProp.objectReferenceValue != displayNameLabel)
                labelProp.objectReferenceValue = displayNameLabel;

            SerializedProperty framesProp = so.FindProperty("rarityFrames");
            if (framesProp != null && rarityFrames != null)
            {
                framesProp.arraySize = rarityFrames.Length;
                for (int i = 0; i < rarityFrames.Length; i++)
                    framesProp.GetArrayElementAtIndex(i).objectReferenceValue = rarityFrames[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (forceLog)
                Debug.Log("[ItemCollectiblePrefabSetup] Updated " + PrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Image EnsureIconHierarchy(GameObject root)
    {
        // Parent under root (not art/) so art's VFX scale does not shrink the world-space icon.
        Transform parent = root.transform;
        Transform art = root.transform.Find("art");
        Transform canvasTf = parent.Find("IconCanvas");
        if (canvasTf == null && art != null)
            canvasTf = art.Find("IconCanvas");

        GameObject canvasGo;
        if (canvasTf == null)
        {
            canvasGo = new GameObject("IconCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(parent, false);
        }
        else
        {
            canvasGo = canvasTf.gameObject;
            if (canvasGo.transform.parent != parent)
                canvasGo.transform.SetParent(parent, false);
            if (canvasGo.GetComponent<Canvas>() == null)
                canvasGo.AddComponent<Canvas>();
            if (canvasGo.GetComponent<RectTransform>() == null)
                canvasGo.AddComponent<RectTransform>();
        }

        canvasGo.transform.localPosition = new Vector3(0f, 1f, 0f);
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * CanvasWorldScale;

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(IconSize, IconSize);

        if (canvasGo.GetComponent<BillboardFacingCamera>() == null)
            canvasGo.AddComponent<BillboardFacingCamera>();

        Transform iconTf = canvasGo.transform.Find("Icon");
        GameObject iconGo;
        if (iconTf == null)
        {
            iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(canvasGo.transform, false);
        }
        else
        {
            iconGo = iconTf.gameObject;
            if (iconGo.GetComponent<Image>() == null)
                iconGo.AddComponent<Image>();
        }

        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        iconRect.localScale = Vector3.one;

        var image = iconGo.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    static TextMeshProUGUI EnsureDisplayNameLabel(GameObject root)
    {
        Transform canvasTf = root.transform.Find("IconCanvas");
        if (canvasTf == null)
            return null;

        Transform labelTf = canvasTf.Find("txtDisplay");
        GameObject labelGo;
        if (labelTf == null)
        {
            labelGo = new GameObject("txtDisplay", typeof(RectTransform));
            labelGo.transform.SetParent(canvasTf, false);
            labelGo.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            labelGo = labelTf.gameObject;
            if (labelGo.GetComponent<TextMeshProUGUI>() == null)
                labelGo.AddComponent<TextMeshProUGUI>();
        }

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        if (labelRect.sizeDelta.sqrMagnitude < 1f)
            labelRect.sizeDelta = new Vector2(400f, 100f);
        if (Mathf.Approximately(labelRect.anchoredPosition.y, 0f))
            labelRect.anchoredPosition = new Vector2(0f, 240f);

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    static Image EnsureRarityImage(GameObject root)
    {
        Transform canvasTf = root.transform.Find("IconCanvas");
        if (canvasTf == null)
            return null;

        Transform rarityTf = canvasTf.Find("rarity");
        GameObject rarityGo;
        if (rarityTf == null)
        {
            rarityGo = new GameObject("rarity", typeof(RectTransform), typeof(Image));
            rarityGo.transform.SetParent(canvasTf, false);
        }
        else
        {
            rarityGo = rarityTf.gameObject;
            if (rarityGo.GetComponent<Image>() == null)
                rarityGo.AddComponent<Image>();
        }

        var rarityRect = rarityGo.GetComponent<RectTransform>();
        rarityRect.anchorMin = new Vector2(0.5f, 0.5f);
        rarityRect.anchorMax = new Vector2(0.5f, 0.5f);
        rarityRect.pivot = new Vector2(0.5f, 0.5f);
        if (rarityRect.sizeDelta.sqrMagnitude < 1f)
            rarityRect.sizeDelta = new Vector2(400f, 180f);

        var image = rarityGo.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static Sprite[] LoadRarityFrames()
    {
        const string frameFolder =
            "Assets/_assetStore/Layer Lab/GUI Pro-CasualGame/ResourcesData/Sprites/Components/Frame";
        return new[]
        {
            LoadSprite($"{frameFolder}/ItemFrame01_Single_Brown.png"),
            LoadSprite($"{frameFolder}/ItemFrame01_Single_Green.png"),
            LoadSprite($"{frameFolder}/ItemFrame01_Single_Blue.png"),
            LoadSprite($"{frameFolder}/ItemFrame01_Single_Purple.png"),
            LoadSprite($"{frameFolder}/ItemFrame01_Single_Yellow.png"),
            LoadSprite($"{frameFolder}/ItemFrame01_Single_Red.png"),
        };
    }

    static Sprite LoadSprite(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                return sprite;
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void SetBattlegroundSampleItemId()
    {
        const string scenePath = "Assets/_project/_scenes/battleground.unity";
        if (!System.IO.File.Exists(scenePath))
            return;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath,
            UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            ItemCollectible[] collectibles = Object.FindObjectsByType<ItemCollectible>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            bool dirty = false;
            for (int i = 0; i < collectibles.Length; i++)
            {
                ItemCollectible c = collectibles[i];
                if (c == null || c.gameObject.scene != scene)
                    continue;

                var so = new SerializedObject(c);
                SerializedProperty idProp = so.FindProperty("itemId");
                if (idProp == null)
                    continue;

                if (string.IsNullOrEmpty(idProp.stringValue))
                {
                    idProp.stringValue = ItemCatalogSceneSetup.SampleItemId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(c);
                    dirty = true;
                }
            }

            if (dirty)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
    }
}
#endif
