#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ensures melee weapon info visibility and ammo fill are wired on the player prefab,
/// and removes a scene-only duplicate ui_MeleeWeaponHUD under the player instance.
/// </summary>
[InitializeOnLoad]
public static class MeleeWeaponInfoVisibilityPrefabSetup
{
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string MeleeHudPrefabPath = "Assets/_project/_prefabs/ui/ui_MeleeWeaponHUD.prefab";
    const string InitScenePath = "Assets/_project/_scenes/init.unity";
    const string InfoObjectName = "ui_MeleeWeaponHUD";
    const string SetupDonePrefKey = "LuckyDungeon.MeleeWeaponInfoVisibilityPrefabSetupDone_v1";

    static MeleeWeaponInfoVisibilityPrefabSetup()
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

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        SetupPlayerPrefab();
        CleanupSceneDuplicate();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    [MenuItem("Tools/UI/Setup Melee Weapon Info Visibility")]
    public static void SetupFromMenu()
    {
        EditorPrefs.SetBool(SetupDonePrefKey, false);
        SetupPlayerPrefab();
        CleanupSceneDuplicate();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    public static void SetupPlayerPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
        {
            Debug.LogWarning("[MeleeWeaponInfoVisibilityPrefabSetup] Player prefab not found: " + PlayerPrefabPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var holder = root.GetComponent<WeaponHolder>();
            if (holder == null)
            {
                Debug.LogWarning("[MeleeWeaponInfoVisibilityPrefabSetup] No WeaponHolder on player prefab.");
                return;
            }

            Transform infoTransform = FindChildByName(root.transform, InfoObjectName);
            bool changed = false;

            if (infoTransform == null)
            {
                GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeleeHudPrefabPath);
                if (hudPrefab == null)
                {
                    Debug.LogWarning("[MeleeWeaponInfoVisibilityPrefabSetup] Melee HUD prefab not found: " + MeleeHudPrefabPath);
                    return;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, root.transform);
                instance.name = InfoObjectName;
                instance.SetActive(false);
                infoTransform = instance.transform;
                changed = true;
            }

            changed |= EnsureVisibility(root, holder, infoTransform.gameObject);
            changed |= EnsureAmmoFill(root, holder, infoTransform);

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[MeleeWeaponInfoVisibilityPrefabSetup] Wired melee weapon info UI on player prefab.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void CleanupSceneDuplicate()
    {
        if (!System.IO.File.Exists(InitScenePath))
            return;

        string previousScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(InitScenePath, OpenSceneMode.Single);
        bool dirty = false;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (!TryFindChildRecursive(roots[i].transform, "player", out GameObject player))
                continue;

            // Remove additive scene instances of the melee HUD under the player prefab instance.
            var addedObjects = PrefabUtility.GetAddedGameObjects(player);
            for (int a = 0; a < addedObjects.Count; a++)
            {
                GameObject added = addedObjects[a].instanceGameObject;
                if (added == null || added.name != InfoObjectName)
                    continue;

                Object.DestroyImmediate(added);
                dirty = true;
            }
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MeleeWeaponInfoVisibilityPrefabSetup] Removed scene-only ui_MeleeWeaponHUD duplicate.");
        }

        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != InitScenePath)
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
    }

    static bool EnsureVisibility(GameObject root, WeaponHolder holder, GameObject infoObject)
    {
        var view = root.GetComponent<MeleeWeaponInfoVisibility>();
        bool changed = false;
        if (view == null)
        {
            view = root.AddComponent<MeleeWeaponInfoVisibility>();
            changed = true;
        }

        var so = new SerializedObject(view);
        SerializedProperty holderProp = so.FindProperty("weaponHolder");
        SerializedProperty infoProp = so.FindProperty("meleeWeaponInfo");

        if (holderProp != null && holderProp.objectReferenceValue != holder)
        {
            holderProp.objectReferenceValue = holder;
            changed = true;
        }

        if (infoProp != null && infoProp.objectReferenceValue != infoObject)
        {
            infoProp.objectReferenceValue = infoObject;
            changed = true;
        }

        if (so.ApplyModifiedPropertiesWithoutUndo())
            changed = true;

        return changed;
    }

    static bool EnsureAmmoFill(GameObject root, WeaponHolder holder, Transform infoTransform)
    {
        Transform fillTransform = infoTransform.Find("panel/fill");
        if (fillTransform == null)
            fillTransform = FindChildByName(infoTransform, "fill");

        Image fillImage = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
        if (fillImage == null)
        {
            Debug.LogWarning("[MeleeWeaponInfoVisibilityPrefabSetup] No fill Image under ui_MeleeWeaponHUD/panel/fill.");
            return false;
        }

        Transform ammoTransform = infoTransform.Find("panel/fill/txtAmmo");
        if (ammoTransform == null)
            ammoTransform = infoTransform.Find("panel/txtAmmo");
        if (ammoTransform == null)
            ammoTransform = FindChildByName(infoTransform, "txtAmmo");
        TextMeshProUGUI ammoLabel = ammoTransform != null
            ? ammoTransform.GetComponent<TextMeshProUGUI>()
            : null;

        var view = root.GetComponent<MeleeWeaponInfoAmmoFill>();
        bool changed = false;
        if (view == null)
        {
            view = root.AddComponent<MeleeWeaponInfoAmmoFill>();
            changed = true;
        }

        var so = new SerializedObject(view);
        SerializedProperty holderProp = so.FindProperty("weaponHolder");
        SerializedProperty fillProp = so.FindProperty("fillImage");
        SerializedProperty ammoProp = so.FindProperty("ammoLabel");

        if (holderProp != null && holderProp.objectReferenceValue != holder)
        {
            holderProp.objectReferenceValue = holder;
            changed = true;
        }

        if (fillProp != null && fillProp.objectReferenceValue != fillImage)
        {
            fillProp.objectReferenceValue = fillImage;
            changed = true;
        }

        if (ammoProp != null && ammoLabel != null && ammoProp.objectReferenceValue != ammoLabel)
        {
            ammoProp.objectReferenceValue = ammoLabel;
            changed = true;
        }

        if (so.ApplyModifiedPropertiesWithoutUndo())
            changed = true;

        return changed;
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

    static bool TryFindChildRecursive(Transform parent, string name, out GameObject found)
    {
        if (parent.name == name)
        {
            found = parent.gameObject;
            return true;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            if (TryFindChildRecursive(parent.GetChild(i), name, out found))
                return true;
        }

        found = null;
        return false;
    }
}
#endif
