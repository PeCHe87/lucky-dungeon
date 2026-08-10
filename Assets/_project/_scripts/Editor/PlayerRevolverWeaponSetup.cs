#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates player revolver <see cref="AssaultWeaponData"/> (pistol visual + pistol anim)
/// and assigns it to the player's existing ranged slot.
/// </summary>
[InitializeOnLoad]
public static class PlayerRevolverWeaponSetup
{
    public const string RevolverDataPath = "Assets/_project/_data/weapons/player_weapon_revolver.asset";
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string PistolVisualPath = "Assets/_project/_prefabs/combat/weapons/pistol_visual.prefab";
    const string PistolControllerPath = PlayerPistolAnimationEditorSetup.PistolControllerPath;
    const string PistolProfilePath = PlayerPistolAnimationEditorSetup.PistolProfilePath;

    static PlayerRevolverWeaponSetup()
    {
        EditorApplication.delayCall += EnsureSetup;
    }

    static void EnsureSetup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureSetup;
            return;
        }

        SetupAll();
    }

    [MenuItem("Tools/Combat/Setup Player Revolver Weapon")]
    public static void SetupAllMenu() => SetupAll();

    public static void SetupAll()
    {
        PlayerPistolAnimationEditorSetup.EnsurePistolAssetsExist();

        AssaultWeaponData revolver = EnsureRevolverAsset();
        if (revolver == null)
        {
            Debug.LogError("[PlayerRevolverWeaponSetup] Failed to create revolver weapon data.");
            return;
        }

        AssignRevolverOnPlayer(revolver);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerRevolverWeaponSetup] Player revolver weapon data created and assigned.");
    }

    public static AssaultWeaponData EnsureRevolverAsset()
    {
        EnsureFolder("Assets/_project/_data");
        EnsureFolder("Assets/_project/_data/weapons");

        var asset = AssetDatabase.LoadAssetAtPath<AssaultWeaponData>(RevolverDataPath);
        bool created = asset == null;
        if (created)
        {
            asset = ScriptableObject.CreateInstance<AssaultWeaponData>();
            AssetDatabase.CreateAsset(asset, RevolverDataPath);
        }

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PistolControllerPath);
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(PistolProfilePath);
        var pistolVisual = AssetDatabase.LoadAssetAtPath<GameObject>(PistolVisualPath);

        var so = new SerializedObject(asset);
        so.FindProperty("damage").floatValue = 10f;
        so.FindProperty("cooldown").floatValue = 0.3f;
        so.FindProperty("attackActiveDuration").floatValue = 0.2f;
        so.FindProperty("hitLayers").intValue = CombatHitLayers.PlayerRangedProjectile.value;
        so.FindProperty("pushbackDistance").floatValue = 0.4f;
        so.FindProperty("pushbackDuration").floatValue = 0.08f;
        so.FindProperty("projectileSpeed").floatValue = 28f;
        so.FindProperty("projectileLifetime").floatValue = 5f;
        so.FindProperty("projectileMaxDistance").floatValue = 45f;
        so.FindProperty("minAttackRange").floatValue = 3f;
        so.FindProperty("approachStopDistanceBuffer").floatValue = 2f;
        so.FindProperty("targetDetectionRadius").floatValue = 15f;
        so.FindProperty("omnidirectionalDetectionRadius").floatValue = 20f;
        so.FindProperty("animatorController").objectReferenceValue = controller;
        so.FindProperty("animationProfile").objectReferenceValue = profile;

        SerializedProperty sockets = so.FindProperty("visualSockets");
        sockets.arraySize = 1;
        SerializedProperty socket = sockets.GetArrayElementAtIndex(0);
        socket.FindPropertyRelative("prefab").objectReferenceValue = pistolVisual;
        socket.FindPropertyRelative("hand").enumValueIndex = (int)WeaponHand.Right;
        socket.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
        socket.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
        socket.FindPropertyRelative("localScale").vector3Value = Vector3.one;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void AssignRevolverOnPlayer(AssaultWeaponData data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            RangedWeapon[] weapons = root.GetComponentsInChildren<RangedWeapon>(true);
            if (weapons.Length == 0)
            {
                Debug.LogWarning("[PlayerRevolverWeaponSetup] No RangedWeapon on player prefab.");
                return;
            }

            bool changed = false;
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
