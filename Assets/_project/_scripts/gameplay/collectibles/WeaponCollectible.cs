using UnityEngine;

/// <summary>
/// World pickup configured by weapon id. Resolves <see cref="WeaponData"/> from <see cref="WeaponCatalog"/>
/// and opens <see cref="SwapWeaponPopup"/> with the player's matching slot vs the pickup.
/// </summary>
public sealed class WeaponCollectible : Collectible
{
    [SerializeField] string weaponId;

    protected override bool TryApply(CombatEntityHealth collector)
    {
        if (string.IsNullOrEmpty(weaponId))
        {
            Debug.LogWarning($"[WeaponCollectible] Empty weaponId on '{name}'.", this);
            return false;
        }

        WeaponCatalog catalog = WeaponCatalog.Current;
        if (catalog == null)
        {
            Debug.LogWarning("[WeaponCollectible] WeaponCatalog.Current is not set.", this);
            return false;
        }

        if (!catalog.TryGetById(weaponId, out WeaponData data) || data == null)
        {
            Debug.LogWarning($"[WeaponCollectible] No weapon found for id '{weaponId}'.", this);
            return false;
        }

        WeaponHolder holder = collector != null
            ? collector.GetComponentInParent<WeaponHolder>()
            : null;
        if (holder == null)
        {
            Debug.LogWarning("[WeaponCollectible] Collector has no WeaponHolder.", this);
            return false;
        }

        WeaponData playerSlotData = holder.GetSlotWeaponData(data.WeaponType);

        Debug.Log(
            $"[WeaponCollectible] Collected {FormatWeapon(data)}; player {data.WeaponType} slot = {FormatWeapon(playerSlotData)}",
            this);

        SwapWeaponPopup popup = SwapWeaponPopup.Instance;
        if (popup == null)
        {
            popup = Object.FindFirstObjectByType<SwapWeaponPopup>(FindObjectsInactive.Include);
            if (popup != null && !popup.gameObject.activeSelf)
                popup.gameObject.SetActive(true);
        }

        if (popup == null)
        {
            Debug.LogWarning("[WeaponCollectible] SwapWeaponPopup.Instance is not set.", this);
            return false;
        }

        popup.Show(holder, playerSlotData, data);
        return true;
    }

    static string FormatWeapon(WeaponData data)
    {
        if (data == null)
            return "none";

        string display = string.IsNullOrEmpty(data.DisplayName) ? "(no display name)" : data.DisplayName;
        string id = string.IsNullOrEmpty(data.WeaponId) ? "(no id)" : data.WeaponId;
        return $"'{display}' ({id})";
    }
}
