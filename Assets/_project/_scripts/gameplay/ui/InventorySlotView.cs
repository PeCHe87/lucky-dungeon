using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single inventory HUD slot: rarity frame, item icon, and stack amount.
/// </summary>
public sealed class InventorySlotView : MonoBehaviour
{
    [SerializeField] Image frameImage;
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI amountLabel;

    void Awake() => ResolveReferences();

    public void SetEmpty(Sprite emptyFrame)
    {
        ResolveReferences();

        if (frameImage != null)
            frameImage.sprite = emptyFrame;

        SetActive(iconImage, false);
        SetActive(amountLabel, false);

        if (iconImage != null)
            iconImage.sprite = null;

        if (amountLabel != null)
            amountLabel.text = string.Empty;
    }

    public void SetItem(Sprite frame, Sprite icon, int amount)
    {
        ResolveReferences();

        if (frameImage != null)
            frameImage.sprite = frame;

        SetActive(iconImage, true);
        SetActive(amountLabel, true);

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = true;
        }

        if (amountLabel != null)
        {
            amountLabel.text = amount.ToString();
            amountLabel.enabled = true;
        }
    }

    static void SetActive(Component component, bool active)
    {
        if (component != null)
            component.gameObject.SetActive(active);
    }

    void ResolveReferences()
    {
        if (frameImage == null)
        {
            Transform container = transform.Find("container");
            if (container != null)
                frameImage = container.GetComponent<Image>();
        }

        if (iconImage == null)
        {
            Transform icon = transform.Find("container/icon");
            if (icon == null)
                icon = transform.Find("icon");
            if (icon != null)
                iconImage = icon.GetComponent<Image>();
        }

        if (amountLabel == null)
        {
            Transform amount = transform.Find("container/txtAmount");
            if (amount == null)
                amount = transform.Find("txtAmount");
            if (amount != null)
                amountLabel = amount.GetComponent<TextMeshProUGUI>();
        }
    }
}
