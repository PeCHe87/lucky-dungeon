#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires dungeon run controller, bootstrap, and progress presenter into dungeon.unity (create-only).
/// </summary>
[InitializeOnLoad]
public static class DungeonSceneSetup
{
    const string DungeonScenePath = "Assets/_project/_scenes/dungeon.unity";
    const string CatalogAssetPath = DungeonSampleAssetsSetup.CatalogAssetPath;
    const string SetupDonePrefKey = "LuckyDungeon.DungeonSceneSetupDone";
    const string LayoutBuilderWiredPrefKey = "LuckyDungeon.DungeonLayoutBuilderWired";

    static DungeonSceneSetup()
    {
        EditorApplication.delayCall += TryAutoSetupOnce;
    }

    static void TryAutoSetupOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoSetupOnce;
            return;
        }

        if (!System.IO.File.Exists(DungeonScenePath))
            return;

        if (!EditorPrefs.GetBool(SetupDonePrefKey, false))
        {
            SetupScene();
            EditorPrefs.SetBool(SetupDonePrefKey, true);
            EditorPrefs.SetBool(LayoutBuilderWiredPrefKey, true);
            return;
        }

        if (!EditorPrefs.GetBool(LayoutBuilderWiredPrefKey, false))
        {
            SetupScene();
            EditorPrefs.SetBool(LayoutBuilderWiredPrefKey, true);
        }
    }

    [MenuItem("Tools/Dungeon/Setup Dungeon Scene")]
    public static void SetupSceneMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        EditorPrefs.DeleteKey(LayoutBuilderWiredPrefKey);
        DungeonSampleAssetsSetup.SetupAll();
        SetupScene();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
        EditorPrefs.SetBool(LayoutBuilderWiredPrefKey, true);
    }

    public static void SetupScene()
    {
        DungeonSampleAssetsSetup.SetupAll();

        var scene = EditorSceneManager.OpenScene(DungeonScenePath, OpenSceneMode.Single);

        EnsureEventSystemInScene();

        GameObject host = GameObject.Find("DungeonRun");
        if (host == null)
            host = new GameObject("DungeonRun");

        var bootstrap = host.GetComponent<DungeonRunBootstrap>();
        if (bootstrap == null)
            bootstrap = host.AddComponent<DungeonRunBootstrap>();

        var controller = host.GetComponent<DungeonRunController>();
        if (controller == null)
            controller = host.AddComponent<DungeonRunController>();

        var layoutBuilder = host.GetComponent<DungeonLayoutBuilder>();
        if (layoutBuilder == null)
            layoutBuilder = host.AddComponent<DungeonLayoutBuilder>();

        var catalog = AssetDatabase.LoadAssetAtPath<DungeonCellCatalog>(CatalogAssetPath);
        var bootstrapSo = new SerializedObject(bootstrap);
        bootstrapSo.FindProperty("catalog").objectReferenceValue = catalog;
        bootstrapSo.FindProperty("runController").objectReferenceValue = controller;
        bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

        // Sample linear run: battle → hp up → currency → shop → battle
        DungeonCellData[] samplePath =
        {
            AssetDatabase.LoadAssetAtPath<DungeonCellData>(
                DungeonSampleAssetsSetup.CellsPath + "/cell_battle_01.asset"),
            AssetDatabase.LoadAssetAtPath<DungeonCellData>(
                DungeonSampleAssetsSetup.CellsPath + "/cell_increase_player_hp.asset"),
            AssetDatabase.LoadAssetAtPath<DungeonCellData>(
                DungeonSampleAssetsSetup.CellsPath + "/cell_extra_run_currency.asset"),
            AssetDatabase.LoadAssetAtPath<DungeonCellData>(
                DungeonSampleAssetsSetup.CellsPath + "/cell_shop.asset"),
            AssetDatabase.LoadAssetAtPath<DungeonCellData>(
                DungeonSampleAssetsSetup.CellsPath + "/cell_battle_01.asset"),
        };

        var layoutSo = new SerializedObject(layoutBuilder);
        layoutSo.FindProperty("runController").objectReferenceValue = controller;
        layoutSo.FindProperty("buildAndStartOnStart").boolValue = true;
        SerializedProperty cellsProp = layoutSo.FindProperty("cells");
        cellsProp.arraySize = samplePath.Length;
        for (int i = 0; i < samplePath.Length; i++)
            cellsProp.GetArrayElementAtIndex(i).objectReferenceValue = samplePath[i];
        layoutSo.ApplyModifiedPropertiesWithoutUndo();

        // Progress presenter on the screen prefab instance if present
        GameObject progressScreen = GameObject.Find("screen_dungeon_progress");
        if (progressScreen == null)
            progressScreen = GameObject.Find("ui_screen_dungeon_progress");

        if (progressScreen != null)
        {
            var presenter = progressScreen.GetComponent<DungeonProgressPresenter>();
            if (presenter == null)
                presenter = progressScreen.AddComponent<DungeonProgressPresenter>();

            var presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("runController").objectReferenceValue = controller;

            Transform cells = FindDeepChild(progressScreen.transform, "cells");
            if (cells != null)
                presenterSo.FindProperty("cellsContainer").objectReferenceValue = cells;

            Transform btnGo = FindDeepChild(progressScreen.transform, "btnGo");
            if (btnGo != null)
                presenterSo.FindProperty("goButton").objectReferenceValue =
                    btnGo.GetComponent<UnityEngine.UI.Button>();

            Transform txtLevel = FindDeepChild(progressScreen.transform, "txtLevel");
            if (txtLevel != null)
                presenterSo.FindProperty("levelLabel").objectReferenceValue =
                    txtLevel.GetComponent<TMPro.TextMeshProUGUI>();

            EnsureCellViewOnPrefab();
            presenterSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[DungeonSceneSetup] Dungeon scene wired with layout builder and sample path.");
    }

    static void EnsureEventSystemInScene()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    static void EnsureCellViewOnPrefab()
    {
        const string cellPrefabPath = "Assets/_project/_prefabs/ui/dungeon/ui_dungeon_cell.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(cellPrefabPath);
        try
        {
            if (root.GetComponent<DungeonCellView>() == null)
            {
                root.AddComponent<DungeonCellView>();
                PrefabUtility.SaveAsPrefabAsset(root, cellPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
                return child;

            Transform found = FindDeepChild(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
