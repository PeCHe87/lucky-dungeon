#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates sample dungeon cell/action/catalog ScriptableObject assets (create-only).
/// Auto-runs once after scripts compile; also available via menu.
/// </summary>
[InitializeOnLoad]
public static class DungeonSampleAssetsSetup
{
    public const string RootDataPath = "Assets/_project/_data/dungeon";
    public const string ActionsPath = RootDataPath + "/actions";
    public const string CellsPath = RootDataPath + "/cells";
    public const string CatalogsPath = RootDataPath + "/catalogs";
    public const string CatalogAssetPath = CatalogsPath + "/DungeonCellCatalog.asset";

    const string PrefabsPath = "Assets/_project/_prefabs/ui/dungeon";
    const string FallbackCellPrefabPath = PrefabsPath + "/ui_dungeon_cell.prefab";
    const string SetupDonePrefKey = "LuckyDungeon.DungeonSampleAssetsSetupDone";
    const string CellPrefabsWiredPrefKey = "LuckyDungeon.DungeonCellPrefabsWired";

    static DungeonSampleAssetsSetup()
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

        bool catalogReady = AssetDatabase.LoadAssetAtPath<DungeonCellCatalog>(CatalogAssetPath) != null;
        if (!EditorPrefs.GetBool(SetupDonePrefKey, false) || !catalogReady)
        {
            SetupAll();
            EditorPrefs.SetBool(SetupDonePrefKey, true);
            EditorPrefs.SetBool(CellPrefabsWiredPrefKey, true);
            return;
        }

        // Existing projects: assign cell prefabs once without recreating assets.
        if (!EditorPrefs.GetBool(CellPrefabsWiredPrefKey, false))
        {
            WireCellPrefabs();
            EditorPrefs.SetBool(CellPrefabsWiredPrefKey, true);
        }
    }

    [MenuItem("Tools/Dungeon/Setup Sample Cell Assets")]
    public static void SetupAllMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        EditorPrefs.DeleteKey(CellPrefabsWiredPrefKey);
        SetupAll();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
        EditorPrefs.SetBool(CellPrefabsWiredPrefKey, true);
    }

    public static void SetupAll()
    {
        Debug.Log("[DungeonSampleAssetsSetup] Creating sample dungeon assets...");
        EnsureFolder("Assets/_project/_data");
        EnsureFolder(RootDataPath);
        EnsureFolder(ActionsPath);
        EnsureFolder(CellsPath);
        EnsureFolder(CatalogsPath);

        BattleCellAction battleAction = EnsureAction<BattleCellAction>(
            ActionsPath + "/action_battle.asset");
        ShopCellAction shopAction = EnsureAction<ShopCellAction>(
            ActionsPath + "/action_shop.asset");

        ModifyRunStatCellAction hpUp = EnsureModifyStatAction(
            ActionsPath + "/action_increase_player_hp.asset",
            RunStatKind.PlayerMaxHp, 10f);
        ModifyRunStatCellAction hpDown = EnsureModifyStatAction(
            ActionsPath + "/action_decrease_player_hp.asset",
            RunStatKind.PlayerMaxHp, -5f);
        ModifyRunStatCellAction atkUp = EnsureModifyStatAction(
            ActionsPath + "/action_increase_player_attack.asset",
            RunStatKind.PlayerAttack, 2f);
        ModifyRunStatCellAction atkDown = EnsureModifyStatAction(
            ActionsPath + "/action_decrease_player_attack.asset",
            RunStatKind.PlayerAttack, -1f);
        ModifyRunStatCellAction enemyHpUp = EnsureModifyStatAction(
            ActionsPath + "/action_increase_enemies_hp.asset",
            RunStatKind.EnemyMaxHp, 10f);
        GrantCurrencyCellAction currency = EnsureGrantCurrencyAction(
            ActionsPath + "/action_grant_currency.asset", 15);

        DungeonCellData[] cells =
        {
            EnsureCell(
                CellsPath + "/cell_battle_01.asset",
                "cell_battle_01", "Battle", "Fight enemies in the arena.",
                DungeonCellType.Battle, battleAction,
                PrefabsPath + "/ui_dungeon_cell_battle_0.prefab"),
            EnsureCell(
                CellsPath + "/cell_shop.asset",
                "cell_shop", "Shop", "Browse goods for this run.",
                DungeonCellType.Shop, shopAction,
                PrefabsPath + "/ui_dungeon_cell_shop.prefab"),
            EnsureCell(
                CellsPath + "/cell_increase_player_hp.asset",
                "cell_increase_player_hp", "Vitality Boost", "Increase player max HP.",
                DungeonCellType.IncreasePlayerHp, hpUp,
                PrefabsPath + "/ui_dungeon_cell_increaseHp.prefab"),
            EnsureCell(
                CellsPath + "/cell_decrease_player_hp.asset",
                "cell_decrease_player_hp", "Curse of Frailty", "Decrease player max HP.",
                DungeonCellType.DecreasePlayerHp, hpDown,
                FallbackCellPrefabPath),
            EnsureCell(
                CellsPath + "/cell_increase_player_attack.asset",
                "cell_increase_player_attack", "Sharpened Edge", "Increase player attack.",
                DungeonCellType.IncreasePlayerAttack, atkUp,
                PrefabsPath + "/ui_dungeon_cell_increaseAttack.prefab"),
            EnsureCell(
                CellsPath + "/cell_decrease_player_attack.asset",
                "cell_decrease_player_attack", "Dull Blades", "Decrease player attack.",
                DungeonCellType.DecreasePlayerAttack, atkDown,
                FallbackCellPrefabPath),
            EnsureCell(
                CellsPath + "/cell_increase_enemies_hp.asset",
                "cell_increase_enemies_hp", "Hardened Foes", "Enemies gain max HP.",
                DungeonCellType.IncreaseEnemiesHp, enemyHpUp,
                FallbackCellPrefabPath),
            EnsureCell(
                CellsPath + "/cell_extra_run_currency.asset",
                "cell_extra_run_currency", "Coin Cache", "Gain run currency.",
                DungeonCellType.ExtraRunCurrency, currency,
                PrefabsPath + "/ui_dungeon_cell_currency.prefab"),
        };

        EnsureCatalog(cells);
        WireCellPrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DungeonSampleAssetsSetup] Sample dungeon cell/action/catalog assets ready.");
    }

    /// <summary>Assigns type-specific cell prefabs when <c>cellPrefab</c> is null (does not overwrite).</summary>
    public static void WireCellPrefabs()
    {
        AssignCellPrefabIfNull(CellsPath + "/cell_battle_01.asset", PrefabsPath + "/ui_dungeon_cell_battle_0.prefab");
        AssignCellPrefabIfNull(CellsPath + "/cell_extra_run_currency.asset", PrefabsPath + "/ui_dungeon_cell_currency.prefab");
        AssignCellPrefabIfNull(CellsPath + "/cell_increase_player_hp.asset", PrefabsPath + "/ui_dungeon_cell_increaseHp.prefab");
        AssignCellPrefabIfNull(CellsPath + "/cell_increase_player_attack.asset", PrefabsPath + "/ui_dungeon_cell_increaseAttack.prefab");
        AssignCellPrefabIfNull(CellsPath + "/cell_shop.asset", PrefabsPath + "/ui_dungeon_cell_shop.prefab");
        AssignCellPrefabIfNull(CellsPath + "/cell_decrease_player_hp.asset", FallbackCellPrefabPath);
        AssignCellPrefabIfNull(CellsPath + "/cell_decrease_player_attack.asset", FallbackCellPrefabPath);
        AssignCellPrefabIfNull(CellsPath + "/cell_increase_enemies_hp.asset", FallbackCellPrefabPath);
        AssetDatabase.SaveAssets();
    }

    static void AssignCellPrefabIfNull(string cellAssetPath, string prefabPath)
    {
        var cell = AssetDatabase.LoadAssetAtPath<DungeonCellData>(cellAssetPath);
        if (cell == null)
            return;

        var prefab = AssetDatabase.LoadAssetAtPath<DungeonCellView>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[DungeonSampleAssetsSetup] Missing cell prefab at '{prefabPath}'.");
            return;
        }

        var so = new SerializedObject(cell);
        SerializedProperty prefabProp = so.FindProperty("cellPrefab");
        if (prefabProp == null || prefabProp.objectReferenceValue != null)
            return;

        prefabProp.objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cell);
    }

    static T EnsureAction<T>(string path) where T : DungeonCellAction
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
            return existing;

        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static ModifyRunStatCellAction EnsureModifyStatAction(string path, RunStatKind kind, float amount)
    {
        var existing = AssetDatabase.LoadAssetAtPath<ModifyRunStatCellAction>(path);
        if (existing != null)
            return existing;

        var asset = ScriptableObject.CreateInstance<ModifyRunStatCellAction>();
        AssetDatabase.CreateAsset(asset, path);

        var so = new SerializedObject(asset);
        so.FindProperty("statKind").enumValueIndex = (int)kind;
        so.FindProperty("amount").floatValue = amount;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static GrantCurrencyCellAction EnsureGrantCurrencyAction(string path, int amount)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GrantCurrencyCellAction>(path);
        if (existing != null)
            return existing;

        var asset = ScriptableObject.CreateInstance<GrantCurrencyCellAction>();
        AssetDatabase.CreateAsset(asset, path);

        var so = new SerializedObject(asset);
        so.FindProperty("amount").intValue = amount;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static DungeonCellData EnsureCell(
        string path,
        string cellId,
        string displayName,
        string description,
        DungeonCellType cellType,
        DungeonCellAction action,
        string cellPrefabPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<DungeonCellData>(path);
        if (existing != null)
        {
            AssignCellPrefabIfNull(path, cellPrefabPath);
            return existing;
        }

        var asset = ScriptableObject.CreateInstance<DungeonCellData>();
        AssetDatabase.CreateAsset(asset, path);

        var prefab = AssetDatabase.LoadAssetAtPath<DungeonCellView>(cellPrefabPath);
        var so = new SerializedObject(asset);
        so.FindProperty("cellId").stringValue = cellId;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("cellType").enumValueIndex = (int)cellType;
        so.FindProperty("action").objectReferenceValue = action;
        so.FindProperty("cellPrefab").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void EnsureCatalog(DungeonCellData[] cells)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DungeonCellCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<DungeonCellCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        var so = new SerializedObject(catalog);
        SerializedProperty cellsProp = so.FindProperty("cells");
        cellsProp.arraySize = cells.Length;
        for (int i = 0; i < cells.Length; i++)
            cellsProp.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
