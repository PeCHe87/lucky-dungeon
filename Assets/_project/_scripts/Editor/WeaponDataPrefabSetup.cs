#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates starter <see cref="MeleeWeaponData"/> / <see cref="AssaultWeaponData"/> assets and assigns them
/// on entity prefabs after the weapon ScriptableObject refactor.
/// Menu-only — does not auto-run on domain reload / Play.
/// </summary>
public static class WeaponDataPrefabSetup
{
    const string WeaponsFolder = "Assets/_project/_data/weapons";

    const string PlayerMeleePath = WeaponsFolder + "/PlayerMeleeWeapon.asset";
    const string PlayerAssaultPath = WeaponsFolder + "/PlayerAssaultWeapon.asset";
    const string EnemyChaserMeleePath = WeaponsFolder + "/EnemyChaserMeleeWeapon.asset";
    const string EnemyPatrollerMeleePath = WeaponsFolder + "/EnemyPatrollerMeleeWeapon.asset";
    const string EnemyRangeMeleeSlotPath = WeaponsFolder + "/EnemyRangeMeleeSlotWeapon.asset";
    const string EnemyAssaultPath = WeaponsFolder + "/EnemyAssaultWeapon.asset";

    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string ChaserPrefabPath = "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab";
    const string PatrollerMeleePrefabPath = "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab";
    const string PatrollerRangePrefabPath = "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab";

    const string PlayerMeleeAnimatorGuid = "3867d71e261b62b4c97e4562b1fa8efc";
    const string PlayerMeleeProfileGuid = "23438f43f4ac2d441b21f5b3c030f092";
    const string BowAnimatorGuid = "b96b7f5ff05fde648bdb8df74d2deade";
    const string BowProfileGuid = "d9884aae61c0b53449b8b7f6c662b7a9";

    [MenuItem("Tools/Combat/Setup Weapon Data Assets And Prefabs")]
    public static void SetupAllMenu() => SetupAll();

    public static void SetupAll()
    {
        EnsureFolder("Assets/_project/_data");
        EnsureFolder(WeaponsFolder);

        MeleeWeaponData playerMelee = EnsureMeleeAsset(
            PlayerMeleePath,
            damage: 10f,
            cooldown: 0.35f,
            attackActiveDuration: 0.15f,
            hitLayers: ~0,
            pushbackDistance: 0.8f,
            pushbackDuration: 0.1f,
            range: 2.5f,
            enableApproachLunge: true,
            animatorGuid: PlayerMeleeAnimatorGuid,
            profileGuid: PlayerMeleeProfileGuid,
            targetDetectionRadius: 6f,
            omnidirectionalDetectionRadius: 8f);

        AssaultWeaponData playerAssault = EnsureAssaultAsset(
            PlayerAssaultPath,
            damage: 10f,
            cooldown: 0.35f,
            attackActiveDuration: 0.2f,
            hitLayers: CombatHitLayers.PlayerRangedProjectile,
            pushbackDistance: 0.4f,
            pushbackDuration: 0.08f,
            projectileSpeed: 22f,
            projectileLifetime: 5f,
            projectileMaxDistance: 45f,
            animatorGuid: BowAnimatorGuid,
            profileGuid: BowProfileGuid,
            targetDetectionRadius: 15f,
            omnidirectionalDetectionRadius: 20f);

        MeleeWeaponData enemyChaserMelee = EnsureMeleeAsset(
            EnemyChaserMeleePath,
            damage: 15f,
            cooldown: 2f,
            attackActiveDuration: 0.15f,
            hitLayers: ~0,
            pushbackDistance: 1.5f,
            pushbackDuration: 0.1f,
            range: 2.5f,
            enableApproachLunge: false,
            animatorGuid: null,
            profileGuid: null,
            targetDetectionRadius: 6f,
            omnidirectionalDetectionRadius: 8f);

        MeleeWeaponData enemyPatrollerMelee = EnsureMeleeAsset(
            EnemyPatrollerMeleePath,
            damage: 15f,
            cooldown: 0.5f,
            attackActiveDuration: 0.15f,
            hitLayers: ~0,
            pushbackDistance: 1.5f,
            pushbackDuration: 0.1f,
            range: 3f,
            enableApproachLunge: false,
            animatorGuid: null,
            profileGuid: null,
            targetDetectionRadius: 6f,
            omnidirectionalDetectionRadius: 8f);

        MeleeWeaponData enemyRangeMeleeSlot = EnsureMeleeAsset(
            EnemyRangeMeleeSlotPath,
            damage: 1f,
            cooldown: 0.5f,
            attackActiveDuration: 0.15f,
            hitLayers: ~0,
            pushbackDistance: 1.5f,
            pushbackDuration: 0.1f,
            range: 3f,
            enableApproachLunge: false,
            animatorGuid: null,
            profileGuid: null,
            targetDetectionRadius: 6f,
            omnidirectionalDetectionRadius: 8f);

        AssaultWeaponData enemyAssault = EnsureAssaultAsset(
            EnemyAssaultPath,
            damage: 5f,
            cooldown: 1f,
            attackActiveDuration: 0.2f,
            hitLayers: CombatHitLayers.EnemyRangedProjectile,
            pushbackDistance: 0.4f,
            pushbackDuration: 0.08f,
            projectileSpeed: 30f,
            projectileLifetime: 5f,
            projectileMaxDistance: 45f,
            animatorGuid: BowAnimatorGuid,
            profileGuid: BowProfileGuid,
            targetDetectionRadius: 15f,
            omnidirectionalDetectionRadius: 20f);

        AssignMeleeOnPrefab(PlayerPrefabPath, playerMelee);
        // Player ranged slot uses revolver (PlayerRevolverWeaponSetup); keep PlayerAssaultWeapon for legacy/ref.
        AssaultWeaponData playerRevolver =
            AssetDatabase.LoadAssetAtPath<AssaultWeaponData>(PlayerRevolverWeaponSetup.RevolverDataPath);
        if (playerRevolver != null)
            AssignAssaultOnPrefab(PlayerPrefabPath, playerRevolver);
        else
            AssignAssaultOnPrefab(PlayerPrefabPath, playerAssault);
        AssignMeleeOnPrefab(ChaserPrefabPath, enemyChaserMelee);
        AssignMeleeOnPrefab(PatrollerMeleePrefabPath, enemyPatrollerMelee);
        AssignMeleeOnPrefab(PatrollerRangePrefabPath, enemyRangeMeleeSlot);
        AssignAssaultOnPrefab(PatrollerRangePrefabPath, enemyAssault);

        AssetDatabase.SaveAssets();
        Debug.Log("[WeaponDataPrefabSetup] Weapon data assets ensured and assigned on entity prefabs.");
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

    static MeleeWeaponData EnsureMeleeAsset(
        string path,
        float damage,
        float cooldown,
        float attackActiveDuration,
        LayerMask hitLayers,
        float pushbackDistance,
        float pushbackDuration,
        float range,
        bool enableApproachLunge,
        string animatorGuid,
        string profileGuid,
        float targetDetectionRadius,
        float omnidirectionalDetectionRadius)
    {
        var asset = AssetDatabase.LoadAssetAtPath<MeleeWeaponData>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<MeleeWeaponData>();
        AssetDatabase.CreateAsset(asset, path);

        var so = new SerializedObject(asset);
        SetFloat(so, "damage", damage);
        SetFloat(so, "cooldown", cooldown);
        SetFloat(so, "attackActiveDuration", attackActiveDuration);
        SetLayerMask(so, "hitLayers", hitLayers);
        SetFloat(so, "pushbackDistance", pushbackDistance);
        SetFloat(so, "pushbackDuration", pushbackDuration);
        SetFloat(so, "range", range);
        SetBool(so, "enableApproachLunge", enableApproachLunge);
        SetFloat(so, "targetDetectionRadius", targetDetectionRadius);
        SetFloat(so, "omnidirectionalDetectionRadius", omnidirectionalDetectionRadius);
        SetFloat(so, "minPushTravelFraction", 0.9f);
        SetObjectRef(so, "animatorController", LoadByGuid<RuntimeAnimatorController>(animatorGuid));
        SetObjectRef(so, "animationProfile", LoadByGuid<PlayerEntityStateAnimationProfile>(profileGuid));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static AssaultWeaponData EnsureAssaultAsset(
        string path,
        float damage,
        float cooldown,
        float attackActiveDuration,
        LayerMask hitLayers,
        float pushbackDistance,
        float pushbackDuration,
        float projectileSpeed,
        float projectileLifetime,
        float projectileMaxDistance,
        string animatorGuid,
        string profileGuid,
        float targetDetectionRadius,
        float omnidirectionalDetectionRadius)
    {
        var asset = AssetDatabase.LoadAssetAtPath<AssaultWeaponData>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<AssaultWeaponData>();
        AssetDatabase.CreateAsset(asset, path);

        var so = new SerializedObject(asset);
        SetFloat(so, "damage", damage);
        SetFloat(so, "cooldown", cooldown);
        SetFloat(so, "attackActiveDuration", attackActiveDuration);
        SetLayerMask(so, "hitLayers", hitLayers);
        SetFloat(so, "pushbackDistance", pushbackDistance);
        SetFloat(so, "pushbackDuration", pushbackDuration);
        SetFloat(so, "projectileSpeed", projectileSpeed);
        SetFloat(so, "projectileLifetime", projectileLifetime);
        SetFloat(so, "projectileMaxDistance", projectileMaxDistance);
        SetFloat(so, "minAttackRange", 3f);
        SetFloat(so, "approachStopDistanceBuffer", 2f);
        SetFloat(so, "targetDetectionRadius", targetDetectionRadius);
        SetFloat(so, "omnidirectionalDetectionRadius", omnidirectionalDetectionRadius);
        SetInt(so, "magazineSize", 6);
        SetFloat(so, "reloadTime", 1.5f);
        SetObjectRef(so, "animatorController", LoadByGuid<RuntimeAnimatorController>(animatorGuid));
        SetObjectRef(so, "animationProfile", LoadByGuid<PlayerEntityStateAnimationProfile>(profileGuid));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void AssignMeleeOnPrefab(string prefabPath, MeleeWeaponData data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var holder = root.GetComponent<WeaponHolder>();
            if (holder == null)
            {
                Debug.LogWarning("[WeaponDataPrefabSetup] No WeaponHolder on " + prefabPath);
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
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void AssignAssaultOnPrefab(string prefabPath, AssaultWeaponData data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var holder = root.GetComponent<WeaponHolder>();
            if (holder == null)
            {
                Debug.LogWarning("[WeaponDataPrefabSetup] No WeaponHolder on " + prefabPath);
                return;
            }

            bool changed = false;
            var holderSo = new SerializedObject(holder);
            SerializedProperty holderDataProp = holderSo.FindProperty("rangedWeaponData");
            if (holderDataProp != null && holderDataProp.objectReferenceValue != data)
            {
                holderDataProp.objectReferenceValue = data;
                holderSo.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

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
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static T LoadByGuid<T>(string guid) where T : Object
    {
        if (string.IsNullOrEmpty(guid))
            return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }

    static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.floatValue = value;
    }

    static void SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.intValue = value;
    }

    static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.boolValue = value;
    }

    static void SetLayerMask(SerializedObject so, string propertyName, LayerMask value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.intValue = value.value;
    }

    static void SetObjectRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.objectReferenceValue = value;
    }
}
#endif
