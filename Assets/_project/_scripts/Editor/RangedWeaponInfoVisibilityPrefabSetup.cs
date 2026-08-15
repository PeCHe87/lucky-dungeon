#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures ranged weapon info visibility, billboard, and ammo fill are wired on the player prefab.
/// Create-only for missing component/refs — does not rewrite weapon ScriptableObjects.
/// </summary>
[InitializeOnLoad]
public static class RangedWeaponInfoVisibilityPrefabSetup
{
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string InfoObjectName = "rangedWeaponInfo";

    static RangedWeaponInfoVisibilityPrefabSetup()
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

        SetupPlayerPrefab();
    }

    [MenuItem("Tools/UI/Setup Ranged Weapon Info Visibility")]
    public static void SetupPlayerPrefabMenu() => SetupPlayerPrefab();

    public static void SetupPlayerPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
        {
            Debug.LogWarning("[RangedWeaponInfoVisibilityPrefabSetup] Player prefab not found: " + PlayerPrefabPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var holder = root.GetComponent<WeaponHolder>();
            if (holder == null)
            {
                Debug.LogWarning("[RangedWeaponInfoVisibilityPrefabSetup] No WeaponHolder on player prefab.");
                return;
            }

            Transform infoTransform = FindChildByName(root.transform, InfoObjectName);
            if (infoTransform == null)
            {
                Debug.LogWarning("[RangedWeaponInfoVisibilityPrefabSetup] No '" + InfoObjectName + "' under player prefab.");
                return;
            }

            bool changed = false;
            changed |= EnsureVisibility(root, holder, infoTransform.gameObject);
            changed |= EnsureBillboard(infoTransform.gameObject);
            changed |= EnsureAmmoFill(root, holder, infoTransform);

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[RangedWeaponInfoVisibilityPrefabSetup] Wired ranged weapon info UI on player prefab.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool EnsureVisibility(GameObject root, WeaponHolder holder, GameObject infoObject)
    {
        var view = root.GetComponent<RangedWeaponInfoVisibility>();
        bool changed = false;
        if (view == null)
        {
            view = root.AddComponent<RangedWeaponInfoVisibility>();
            changed = true;
        }

        var so = new SerializedObject(view);
        SerializedProperty holderProp = so.FindProperty("weaponHolder");
        SerializedProperty infoProp = so.FindProperty("rangedWeaponInfo");

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

    static bool EnsureBillboard(GameObject infoObject)
    {
        if (infoObject.GetComponent<BillboardFacingCamera>() != null)
            return false;

        infoObject.AddComponent<BillboardFacingCamera>();
        return true;
    }

    static bool EnsureAmmoFill(GameObject root, WeaponHolder holder, Transform infoTransform)
    {
        Transform fillTransform = infoTransform.Find("panel/fill");
        if (fillTransform == null)
            fillTransform = FindChildByName(infoTransform, "fill");

        Image fillImage = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
        if (fillImage == null)
        {
            Debug.LogWarning("[RangedWeaponInfoVisibilityPrefabSetup] No fill Image under rangedWeaponInfo/panel/fill.");
            return false;
        }

        Transform ammoTransform = infoTransform.Find("panel/txtAmmo");
        if (ammoTransform == null)
            ammoTransform = FindChildByName(infoTransform, "txtAmmo");
        TextMeshProUGUI ammoLabel = ammoTransform != null
            ? ammoTransform.GetComponent<TextMeshProUGUI>()
            : null;

        var view = root.GetComponent<RangedWeaponInfoAmmoFill>();
        bool changed = false;
        if (view == null)
        {
            view = root.AddComponent<RangedWeaponInfoAmmoFill>();
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
}
#endif
