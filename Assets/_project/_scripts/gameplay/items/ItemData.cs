using UnityEngine;

/// <summary>
/// Catalog entry for a collectible/gameplay item. Start with id + preview; extend fields as needed.
/// </summary>
[CreateAssetMenu(menuName = "Items/Item Data", fileName = "ItemData")]
public sealed class ItemData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id for lookups (e.g. item_potion_000).")]
    [SerializeField] string itemId;
    [Tooltip("Player-facing name shown on world collectibles and future inventory UI.")]
    [SerializeField] string displayName;
    [SerializeField] ItemRarity rarity = ItemRarity.Common;

    [Header("Presentation")]
    [Tooltip("Icon shown on world collectibles and future inventory UI.")]
    [SerializeField] Sprite previewImage;

    public string ItemId => itemId;
    public string DisplayName => displayName;
    public ItemRarity Rarity => rarity;
    public Sprite PreviewImage => previewImage;
}
