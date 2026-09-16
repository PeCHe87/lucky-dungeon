#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates player hammer <see cref="MeleeWeaponData"/> (hammer visual + hammer anim),
/// assigns it on the player's <see cref="WeaponHolder"/> melee data slot, and registers it in
/// <see cref="WeaponCatalog"/>.
/// Create-only for SO field defaults — never overwrites an existing asset's tuned values.
/// Auto-runs once after scripts compile; also available via menu.
/// </summary>
[InitializeOnLoad]
public static class PlayerHammerWeaponSetup
{
    public const string HammerDataPath =
        "Assets/_project/_data/weapons/PlayerWeapons/player_weapon_hammer.asset";

    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string HammerVisualPath = "Assets/_project/_prefabs/combat/weapons/hammer_visual.prefab";
    const string CatalogAssetPath = WeaponCatalogSceneSetup.CatalogAssetPath;
    const string HammerControllerPath = PlayerHammerAnimationEditorSetup.HammerControllerPath;
    const string HammerProfilePath = PlayerHammerAnimationEditorSetup.HammerProfilePath;
    const string SetupDonePrefKey = "LuckyDungeon.PlayerHammerWeaponSetupDone";

    static PlayerHammerWeaponSetup()
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

        if (EditorPrefs.GetBool(SetupDonePrefKey, false)
            && AssetDatabase.LoadAssetAtPath<MeleeWeaponData>(HammerDataPath) != null)
            return;

        SetupAll();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    [MenuItem("Tools/Combat/Setup Player Hammer Weapon")]
    public static void SetupAllMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        SetupAll();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    public static void SetupAll()
    {
        PlayerHammerAnimationEditorSetup.EnsureHammerAssetsExist();

        MeleeWeaponData hammer = EnsureHammerAsset();
        if (hammer == null)
        {
            Debug.LogError("[PlayerHammerWeaponSetup] Failed to create hammer weapon data.");
            return;
        }

        AssignHammerOnPlayer(hammer);
        EnsureHammerInCatalog(hammer);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerHammerWeaponSetup] Player hammer weapon data created and assigned.");
    }

    public static MeleeWeaponData EnsureHammerAsset()
    {
        EnsureFolder("Assets/_project/_data");
        EnsureFolder("Assets/_project/_data/weapons");
        EnsureFolder("Assets/_project/_data/weapons/PlayerWeapons");

        var asset = AssetDatabase.LoadAssetAtPath<MeleeWeaponData>(HammerDataPath);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<MeleeWeaponData>();
        AssetDatabase.CreateAsset(asset, HammerDataPath);

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HammerControllerPath);
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(HammerProfilePath);
        var hammerVisual = AssetDatabase.LoadAssetAtPath<GameObject>(HammerVisualPath);

        var so = new SerializedObject(asset);
        so.FindProperty("weaponId").stringValue = "m_hammer_0";
        so.FindProperty("displayName").stringValue = "Hammer";
        so.FindProperty("weaponType").enumValueIndex = (int)WeaponType.Melee;
        so.FindProperty("damage").floatValue = 12f;
        so.FindProperty("cooldown").floatValue = 0.4f;
        so.FindProperty("attackActiveDuration").floatValue = 0.2f;
        so.FindProperty("hitLayers").intValue = ~0;
        so.FindProperty("pushbackDistance").floatValue = 1f;
        so.FindProperty("pushbackDuration").floatValue = 0.12f;
        so.FindProperty("targetDetectionRadius").floatValue = 6f;
        so.FindProperty("omnidirectionalDetectionRadius").floatValue = 8f;
        so.FindProperty("animatorController").objectReferenceValue = controller;
        so.FindProperty("animationProfile").objectReferenceValue = profile;
        so.FindProperty("range").floatValue = 2.8f;
        so.FindProperty("moveForwardDistance").floatValue = 0.5f;
        so.FindProperty("moveForwardDuration").floatValue = 0.15f;
        so.FindProperty("coneAngle").floatValue = 120f;
        so.FindProperty("maxTargetsPerSwing").intValue = 8;
        so.FindProperty("enableApproachLunge").boolValue = true;
        so.FindProperty("approachStopBuffer").floatValue = 0.3f;
        so.FindProperty("maxApproachLungeDistance").floatValue = 6f;
        so.FindProperty("approachLungeSpeed").floatValue = 12f;
        so.FindProperty("magazineSize").intValue = 8;
        so.FindProperty("reloadTime").floatValue = 2.5f;

        SerializedProperty sockets = so.FindProperty("visualSockets");
        sockets.arraySize = 1;
        SerializedProperty socket = sockets.GetArrayElementAtIndex(0);
        socket.FindPropertyRelative("prefab").objectReferenceValue = hammerVisual;
        socket.FindPropertyRelative("hand").enumValueIndex = (int)WeaponHand.Right;
        socket.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
        socket.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
        socket.FindPropertyRelative("localScale").vector3Value = Vector3.one;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void AssignHammerOnPlayer(MeleeWeaponData data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var holder = root.GetComponent<WeaponHolder>();
            if (holder == null)
            {
                Debug.LogWarning("[PlayerHammerWeaponSetup] No WeaponHolder on player prefab.");
                return;
            }

            bool changed = false;
            var holderSo = new SerializedObject(holder);
            SerializedProperty holderDataProp = holderSo.FindProperty("meleeWeaponData");
            if (holderDataProp != null && holderDataProp.objectReferenceValue != data)
            {
                holderDataProp.objectReferenceValue = data;
                holderSo.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            MeleeWeapon[] weapons = root.GetComponentsInChildren<MeleeWeapon>(true);
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

    static void EnsureHammerInCatalog(MeleeWeaponData data)
    {
        WeaponCatalog catalog = WeaponCatalogSceneSetup.EnsureCatalogAsset();
        if (catalog == null)
        {
            Debug.LogWarning("[PlayerHammerWeaponSetup] WeaponCatalog asset missing.");
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
