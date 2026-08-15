using UnityEngine;

/// <summary>
/// Shows <see cref="rangedWeaponInfo"/> when a ranged weapon is equipped on <see cref="WeaponHolder"/>;
/// hides it for melee or when nothing is equipped. Host must stay active (do not place this on the toggled root).
/// </summary>
public sealed class RangedWeaponInfoVisibility : MonoBehaviour
{
    const string DefaultInfoObjectName = "rangedWeaponInfo";

    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] GameObject rangedWeaponInfo;

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

        if (rangedWeaponInfo == null)
        {
            Transform found = FindChildByName(transform, DefaultInfoObjectName);
            if (found != null)
                rangedWeaponInfo = found.gameObject;
        }
    }

    void Refresh()
    {
        ResolveRefs();
        if (rangedWeaponInfo == null)
            return;

        bool show = weaponHolder != null && weaponHolder.IsRangedEquipped();
        if (rangedWeaponInfo.activeSelf != show)
            rangedWeaponInfo.SetActive(show);
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
