using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World pickup configured by item id. Resolves <see cref="ItemData"/> from <see cref="ItemCatalog"/>
/// and shows the preview icon, display name, and rarity frame on a billboarded world-space UI.
/// Grants into <see cref="PlayerRunInventory"/> when collected.
/// </summary>
public sealed class ItemCollectible : Collectible
{
    [SerializeField] string itemId;
    [SerializeField] Image iconImage;
    [SerializeField] Image rarityImage;
    [SerializeField] TextMeshProUGUI displayNameLabel;
    [Tooltip("Indexed by ItemRarity: Common, Uncommon, Rare, Epic, Legendary, Mythic.")]
    [SerializeField] Sprite[] rarityFrames;

    void Start() => RefreshVisuals();

    protected override bool TryApply(CombatEntityHealth collector)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning($"[ItemCollectible] Empty itemId on '{name}'.", this);
            return false;
        }

        if (!TryResolveItem(out ItemData data, logFailures: true))
            return false;

        DungeonRunHost host = DungeonRunHost.EnsureExists();
        if (host.Player == null || !host.Player.Inventory.Add(data.ItemId))
        {
            Debug.LogWarning(
                $"[ItemCollectible] Failed to add '{data.ItemId}' to run inventory.",
                this);
            return false;
        }

        return true;
    }

    void RefreshVisuals()
    {
        if (!TryResolveItem(out ItemData data, logFailures: false))
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (displayNameLabel != null)
            {
                displayNameLabel.text = string.Empty;
                displayNameLabel.enabled = false;
            }

            ApplyRarityFrame(ItemRarity.Common);
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = data.PreviewImage;
            iconImage.enabled = data.PreviewImage != null;
            if (data.PreviewImage == null)
                Debug.LogWarning($"[ItemCollectible] Item '{itemId}' has no previewImage.", this);
        }

        if (displayNameLabel != null)
        {
            bool hasName = !string.IsNullOrEmpty(data.DisplayName);
            displayNameLabel.text = hasName ? data.DisplayName : string.Empty;
            displayNameLabel.enabled = hasName;
        }

        ApplyRarityFrame(data.Rarity);
    }

    void ApplyRarityFrame(ItemRarity rarity)
    {
        if (rarityImage == null)
            return;

        Sprite frame = GetRarityFrame(rarity);
        rarityImage.sprite = frame;
        rarityImage.enabled = frame != null;
    }

    Sprite GetRarityFrame(ItemRarity rarity)
    {
        int index = (int)rarity;
        if (rarityFrames == null || index < 0 || index >= rarityFrames.Length)
            return null;
        return rarityFrames[index];
    }

    bool TryResolveItem(out ItemData data, bool logFailures)
    {
        data = null;
        if (string.IsNullOrEmpty(itemId))
        {
            if (logFailures)
                Debug.LogWarning($"[ItemCollectible] Empty itemId on '{name}'.", this);
            return false;
        }

        ItemCatalog catalog = ItemCatalog.Current;
        if (catalog == null)
        {
            if (logFailures)
                Debug.LogWarning("[ItemCollectible] ItemCatalog.Current is not set.", this);
            return false;
        }

        if (!catalog.TryGetById(itemId, out data) || data == null)
        {
            if (logFailures)
                Debug.LogWarning($"[ItemCollectible] No item found for id '{itemId}'.", this);
            return false;
        }

        return true;
    }
}
