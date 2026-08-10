#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates weapon visual prefabs, assigns <see cref="WeaponData"/> visual sockets,
/// and wires <see cref="WeaponVisualPresenter"/> on combat entity prefabs.
/// </summary>
[InitializeOnLoad]
public static class WeaponVisualPrefabSetup
{
    const string WeaponsVisualFolder = "Assets/_project/_prefabs/combat/weapons";
    const string SwordVisualPath = WeaponsVisualFolder + "/sword_visual.prefab";
    const string BowVisualPath = WeaponsVisualFolder + "/bow_visual.prefab";
    const string SourceBowPrefabPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Prefabs/Weapon_Bow.prefab";

    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string PlayerBowDataPath = "Assets/_project/_data/weapons/player_weapon_bow.asset";
    const string EnemyAssaultDataPath = "Assets/_project/_data/weapons/EnemyAssaultWeapon.asset";

    static readonly string[] EntityPrefabPaths =
    {
        PlayerPrefabPath,
        "Assets/_project/_prefabs/entities/base_combat_entity.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab",
    };

    static WeaponVisualPrefabSetup()
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

    [MenuItem("Tools/Combat/Setup Weapon Visual Prefabs And Presenters")]
    public static void SetupAllMenu() => SetupAll();

    public static void SetupAll()
    {
        EnsureFolder("Assets/_project/_prefabs/combat");
        EnsureFolder(WeaponsVisualFolder);

        GameObject swordVisual = EnsureSwordVisualPrefab();
        GameObject bowVisual = EnsureBowVisualPrefab();

        if (bowVisual != null)
        {
            AssignLeftHandSocket(PlayerBowDataPath, bowVisual);
            AssignLeftHandSocket(EnemyAssaultDataPath, bowVisual);
        }

        // Sword visual is available for future melee SOs; punch stays mesh-less.
        _ = swordVisual;

        foreach (string path in EntityPrefabPaths)
            EnsurePresenterAndClearBakedVisuals(path);

        AssetDatabase.SaveAssets();
        Debug.Log("[WeaponVisualPrefabSetup] Weapon visual prefabs/presenters ensured.");
    }

    static GameObject EnsureSwordVisualPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SwordVisualPath);
        if (existing != null)
            return existing;

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform swordModel = FindChildByName(root.transform, "sword_model");
            if (swordModel == null)
            {
                Debug.LogWarning("[WeaponVisualPrefabSetup] sword_model not found on player prefab.");
                return null;
            }

            GameObject clone = Object.Instantiate(swordModel.gameObject);
            clone.name = "sword_visual";
            clone.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(clone, SwordVisualPath);
            Object.DestroyImmediate(clone);
            return AssetDatabase.LoadAssetAtPath<GameObject>(SwordVisualPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static GameObject EnsureBowVisualPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BowVisualPath);
        if (existing != null)
            return existing;

        GameObject sourceBow = AssetDatabase.LoadAssetAtPath<GameObject>(SourceBowPrefabPath);
        var root = new GameObject("bow_visual");
        try
        {
            if (sourceBow != null)
            {
                GameObject bowInstance = (GameObject)PrefabUtility.InstantiatePrefab(sourceBow);
                bowInstance.name = "Weapon_Bow";
                bowInstance.transform.SetParent(root.transform, false);
            }

            var muzzle = new GameObject("muzzle");
            muzzle.transform.SetParent(root.transform, false);
            // Match previous player muzzle offset relative to ranged root.
            muzzle.transform.localPosition = new Vector3(0f, 0.483f, 0.666f);

            PrefabUtility.SaveAsPrefabAsset(root, BowVisualPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BowVisualPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void AssignLeftHandSocket(string dataPath, GameObject visualPrefab)
    {
        var data = AssetDatabase.LoadAssetAtPath<WeaponData>(dataPath);
        if (data == null || visualPrefab == null)
            return;

        var so = new SerializedObject(data);
        SerializedProperty sockets = so.FindProperty("visualSockets");
        if (sockets == null)
            return;

        bool needsWrite = sockets.arraySize != 1;
        if (!needsWrite)
        {
            SerializedProperty entry = sockets.GetArrayElementAtIndex(0);
            needsWrite =
                entry.FindPropertyRelative("prefab").objectReferenceValue != visualPrefab
                || entry.FindPropertyRelative("hand").enumValueIndex != (int)WeaponHand.Left;
        }

        if (!needsWrite)
            return;

        sockets.arraySize = 1;
        SerializedProperty socket = sockets.GetArrayElementAtIndex(0);
        socket.FindPropertyRelative("prefab").objectReferenceValue = visualPrefab;
        socket.FindPropertyRelative("hand").enumValueIndex = (int)WeaponHand.Left;
        socket.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
        socket.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
        socket.FindPropertyRelative("localScale").vector3Value = Vector3.one;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    static void EnsurePresenterAndClearBakedVisuals(string prefabPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.GetComponent<WeaponHolder>() == null)
                return;

            bool changed = false;

            var presenter = root.GetComponent<WeaponVisualPresenter>();
            if (presenter == null)
            {
                presenter = root.AddComponent<WeaponVisualPresenter>();
                changed = true;
            }

            var presenterSo = new SerializedObject(presenter);
            var holder = root.GetComponent<WeaponHolder>();
            SerializedProperty holderProp = presenterSo.FindProperty("weaponHolder");
            if (holderProp != null && holderProp.objectReferenceValue != holder)
            {
                holderProp.objectReferenceValue = holder;
                changed = true;
            }

            Transform left = FindChildByName(root.transform, "Bip001 L Hand");
            Transform right = FindChildByName(root.transform, "Bip001 R Hand");
            SerializedProperty leftProp = presenterSo.FindProperty("leftHand");
            SerializedProperty rightProp = presenterSo.FindProperty("rightHand");
            if (leftProp != null && left != null && leftProp.objectReferenceValue != left)
            {
                leftProp.objectReferenceValue = left;
                changed = true;
            }
            if (rightProp != null && right != null && rightProp.objectReferenceValue != right)
            {
                rightProp.objectReferenceValue = right;
                changed = true;
            }

            if (presenterSo.ApplyModifiedPropertiesWithoutUndo())
                changed = true;

            changed |= DisableNamedChild(root.transform, "sword_model");
            changed |= DisableNamedChild(root.transform, "range_model");
            changed |= DisableNamedChild(root.transform, "BowBasic");

            RangedWeapon[] ranged = root.GetComponentsInChildren<RangedWeapon>(true);
            for (int i = 0; i < ranged.Length; i++)
            {
                var rangedSo = new SerializedObject(ranged[i]);
                SerializedProperty firePoint = rangedSo.FindProperty("firePoint");
                // Clear static fire point; presenter rebinds from spawned muzzle when equipped.
                if (firePoint != null && firePoint.objectReferenceValue != null)
                {
                    firePoint.objectReferenceValue = null;
                    rangedSo.ApplyModifiedPropertiesWithoutUndo();
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

    static bool DisableNamedChild(Transform root, string childName)
    {
        Transform child = FindChildByName(root, childName);
        if (child == null || !child.gameObject.activeSelf)
            return false;
        child.gameObject.SetActive(false);
        return true;
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

    static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;
        if (root.name == childName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
