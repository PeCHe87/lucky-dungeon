using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Routes <c>btnInteract</c> clicks to the current loot container or locked door unlock.
/// Place on <c>btnInteract</c> (or assign the Button explicitly).
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class LootInteractButtonBinder : MonoBehaviour
{
    [SerializeField] Button button;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    void OnEnable()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnInteractClicked);
    }

    void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnInteractClicked);
    }

    void OnInteractClicked()
    {
        InteractableLoot loot = InteractableLoot.CurrentSearchable;
        if (loot != null)
        {
            loot.TryBeginOpen();
            return;
        }

        BuildingDoor door = BuildingDoor.CurrentUnlockable;
        if (door != null)
            door.TryUnlockWithRequiredItem();
    }
}
