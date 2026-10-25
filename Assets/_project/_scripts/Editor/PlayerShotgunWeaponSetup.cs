#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates player shotgun <see cref="AssaultWeaponData"/> (shotgun visual + Shinabro loco / Wolff fire)
/// and assigns it on the player's <see cref="WeaponHolder"/> ranged data slot (and RangedWeapon fallback).
/// Create-only for SO field defaults — never overwrites an existing asset's tuned values.
/// Menu-only — does not auto-run on domain reload / Play.
/// </summary>
public static class PlayerShotgunWeaponSetup
{
    public const string ShotgunDataPath =
        "Assets/_project/_data/weapons/PlayerWeapons/player_weapon_shotgun.asset";

    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string ShotgunVisualPath = "Assets/_project/_prefabs/combat/weapons/shotgun_visual.prefab";
    const string ShotgunControllerPath = PlayerShotgunAnimationEditorSetup.ShotgunControllerPath;
    const string ShotgunProfilePath = PlayerShotgunAnimationEditorSetup.ShotgunProfilePath;
    const string CatalogAssetPath = WeaponCatalogSceneSetup.CatalogAssetPath;

    [MenuItem("Tools/Combat/Setup Player Shotgun Weapon")]
    public static void SetupAllMenu() => SetupAll();

    public static void SetupAll()
    {
        PlayerShotgunAnimationEditorSetup.EnsureShotgunAssetsExist();
        EnsureShotgunVisualHasMuzzle();

        AssaultWeaponData shotgun = EnsureShotgunAsset();
        if (shotgun == null)
        {
            Debug.LogError("[PlayerShotgunWeaponSetup] Failed to create shotgun weapon data.");
            return;
        }

        EnsureShotgunInCatalog(shotgun);
        AssignShotgunOnPlayer(shotgun);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerShotgunWeaponSetup] Player shotgun weapon data created and assigned.");
    }

    public static AssaultWeaponData EnsureShotgunAsset()
    {
        EnsureFolder("Assets/_project/_data");
        EnsureFolder("Assets/_project/_data/weapons");
        EnsureFolder("Assets/_project/_data/weapons/PlayerWeapons");

        var asset = AssetDatabase.LoadAssetAtPath<AssaultWeaponData>(ShotgunDataPath);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<AssaultWeaponData>();
        AssetDatabase.CreateAsset(asset, ShotgunDataPath);

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ShotgunControllerPath);
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(ShotgunProfilePath);
        var shotgunVisual = AssetDatabase.LoadAssetAtPath<GameObject>(ShotgunVisualPath);

        var so = new SerializedObject(asset);
        so.FindProperty("weaponId").stringValue = "r_shotgun_0";
        so.FindProperty("displayName").stringValue = "Shotgun";
        so.FindProperty("weaponType").enumValueIndex = (int)WeaponType.Range;
        so.FindProperty("damage").floatValue = 5f;
        so.FindProperty("cooldown").floatValue = 0.55f;
        so.FindProperty("attackActiveDuration").floatValue = 0.25f;
        so.FindProperty("hitLayers").intValue = CombatHitLayers.PlayerRangedProjectile.value;
        so.FindProperty("pushbackDistance").floatValue = 0.5f;
        so.FindProperty("pushbackDuration").floatValue = 0.08f;
        so.FindProperty("projectileSpeed").floatValue = 22f;
        so.FindProperty("projectileLifetime").floatValue = 0.4f;
        so.FindProperty("projectileMaxDistance").floatValue = 14f;
        so.FindProperty("projectileCount").intValue = 6;
        so.FindProperty("spreadAngleDegrees").floatValue = 25f;
        so.FindProperty("projectileSpawnAtFrame").intValue = 12;
        so.FindProperty("projectileFramesPerSecond").floatValue = 0f;

        var muzzleFx = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_assetStore/Synty/PolygonParticleFX/Prefabs/FX_Gunshot_Shotgun_01.prefab");
        SerializedProperty muzzle = so.FindProperty("muzzleVfx");
        muzzle.FindPropertyRelative("prefab").objectReferenceValue = muzzleFx;
        muzzle.FindPropertyRelative("spawnAtFrame").intValue = 8;
        muzzle.FindPropertyRelative("framesPerSecond").floatValue = 0f;
        muzzle.FindPropertyRelative("localOffset").vector3Value = Vector3.zero;
        muzzle.FindPropertyRelative("destroyDelaySeconds").floatValue = 2f;

        so.FindProperty("minAttackRange").floatValue = 1.5f;
        so.FindProperty("approachStopDistanceBuffer").floatValue = 2f;
        so.FindProperty("targetDetectionRadius").floatValue = 12f;
        so.FindProperty("omnidirectionalDetectionRadius").floatValue = 16f;
        so.FindProperty("magazineSize").intValue = 4;
        so.FindProperty("reloadTime").floatValue = 2f;
        so.FindProperty("animatorController").objectReferenceValue = controller;
        so.FindProperty("animationProfile").objectReferenceValue = profile;

        SerializedProperty sockets = so.FindProperty("visualSockets");
        sockets.arraySize = 1;
        SerializedProperty socket = sockets.GetArrayElementAtIndex(0);
        socket.FindPropertyRelative("prefab").objectReferenceValue = shotgunVisual;
        socket.FindPropertyRelative("hand").enumValueIndex = (int)WeaponHand.Right;
        socket.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
        socket.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
        socket.FindPropertyRelative("localScale").vector3Value = Vector3.one;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void EnsureShotgunVisualHasMuzzle()
    {
        GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(ShotgunVisualPath);
        if (visual == null)
        {
            Debug.LogWarning($"[PlayerShotgunWeaponSetup] Shotgun visual missing at {ShotgunVisualPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(ShotgunVisualPath);
        try
        {
            Transform existing = FindChildNamed(root.transform, "muzzle");
            if (existing != null)
                return;

            var muzzle = new GameObject("muzzle");
            muzzle.transform.SetParent(root.transform, false);
            // Approx barrel tip relative to shotgun visual root; tune in editor if needed.
            muzzle.transform.localPosition = new Vector3(0f, 0.08f, 0.55f);
            PrefabUtility.SaveAsPrefabAsset(root, ShotgunVisualPath);
            Debug.Log("[PlayerShotgunWeaponSetup] Added muzzle child to shotgun_visual.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindChildNamed(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildNamed(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    static void EnsureShotgunInCatalog(AssaultWeaponData data)
    {
        WeaponCatalog catalog = WeaponCatalogSceneSetup.EnsureCatalogAsset();
        if (catalog == null)
        {
            Debug.LogWarning("[PlayerShotgunWeaponSetup] WeaponCatalog asset missing.");
            return;
        }

        var so = new SerializedObject(catalog);
        SerializedProperty weapons = so.FindProperty("weapons");
        for (int i = 0; i < weapons.arraySize; i++)
        {
            if (weapons.GetArrayElementAtIndex(i).objectReferenceValue == data)
                return;
        }

        int index = weapons.arraySize;
        weapons.arraySize = index + 1;
        weapons.GetArrayElementAtIndex(index).objectReferenceValue = data;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    static void AssignShotgunOnPlayer(AssaultWeaponData data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var holder = root.GetComponent<WeaponHolder>();
            if (holder == null)
            {
                Debug.LogWarning("[PlayerShotgunWeaponSetup] No WeaponHolder on player prefab.");
                return;
            }

            bool changed = false;
            var holderSo = new SerializedObject(holder);
            SerializedProperty startingDataProp = holderSo.FindProperty("startingWeaponData");
            if (startingDataProp != null && startingDataProp.objectReferenceValue != data)
            {
                startingDataProp.objectReferenceValue = data;
                changed = true;
            }

            SerializedProperty holderDataProp = holderSo.FindProperty("rangedWeaponData");
            if (holderDataProp != null && holderDataProp.objectReferenceValue != data)
            {
                holderDataProp.objectReferenceValue = data;
                changed = true;
            }

            if (changed)
                holderSo.ApplyModifiedPropertiesWithoutUndo();

            RangedWeapon[] weapons = root.GetComponentsInChildren<RangedWeapon>(true);
            for (int i = 0; i < weapons.Length; i++)
            {
                var so = new SerializedObject(weapons[i]);
                SerializedProperty dataProp = so.FindProperty("data");
                if (dataProp != null && dataProp.objectReferenceValue != data)
                {
                    dataProp.objectReferenceValue = data;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
