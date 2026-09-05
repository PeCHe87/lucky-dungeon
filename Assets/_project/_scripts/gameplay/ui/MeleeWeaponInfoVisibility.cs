using UnityEngine;

/// <summary>
/// Shows <see cref="meleeWeaponInfo"/> when a melee weapon is equipped on <see cref="WeaponHolder"/>;
/// hides it for ranged or when nothing is equipped. Host must stay active (do not place this on the toggled root).
/// </summary>
public sealed class MeleeWeaponInfoVisibility : MonoBehaviour
{
    const string DefaultInfoObjectName = "ui_MeleeWeaponHUD";

    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] GameObject meleeWeaponInfo;

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged += OnEquippedWeaponChanged;

        Refresh();
    }

    void OnDisable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged -= OnEquippedWeaponChanged;
    }

    void Start()
    {
        // After WeaponHolder Awake initial equip.
        Refresh();
    }

    void OnEquippedWeaponChanged() => Refresh();

    void ResolveRefs()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        if (weaponHolder == null)
            weaponHolder = GetComponentInParent<WeaponHolder>();

        if (meleeWeaponInfo == null)
        {
            Transform found = FindChildByName(transform, DefaultInfoObjectName);
            if (found != null)
                meleeWeaponInfo = found.gameObject;
        }
    }

    void Refresh()
    {
        ResolveRefs();
        if (meleeWeaponInfo == null)
            return;

        bool show = weaponHolder != null && weaponHolder.IsMeleeEquipped();
        if (meleeWeaponInfo.activeSelf != show)
            meleeWeaponInfo.SetActive(show);
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
