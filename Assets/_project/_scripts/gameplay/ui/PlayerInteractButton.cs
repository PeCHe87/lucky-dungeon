using UnityEngine;

/// <summary>
/// Shared HUD <c>btnInteract</c> visibility for loot search, repairables, and locked-door unlock.
/// Sources OR together so one system leaving range does not hide the button for the other.
/// </summary>
public static class PlayerInteractButton
{
    static GameObject s_interactButton;
    static bool s_lootAvailable;
    static bool s_repairAvailable;
    static bool s_doorUnlockAvailable;

    public static bool IsVisible => s_lootAvailable || s_repairAvailable || s_doorUnlockAvailable;

    public static void SetLootAvailable(bool available)
    {
        if (s_lootAvailable == available)
            return;

        s_lootAvailable = available;
        Refresh();
    }

    public static void SetRepairAvailable(bool available)
    {
        if (s_repairAvailable == available)
            return;

        s_repairAvailable = available;
        Refresh();
    }

    public static void SetDoorUnlockAvailable(bool available)
    {
        if (s_doorUnlockAvailable == available)
            return;

        s_doorUnlockAvailable = available;
        Refresh();
    }

    static void Refresh()
    {
        EnsureInteractButton();

        bool visible = IsVisible;
        if (s_interactButton != null && s_interactButton.activeSelf != visible)
            s_interactButton.SetActive(visible);

        GameEvents.RaiseLootInteractAvailableChanged(visible);
    }

    static void EnsureInteractButton()
    {
        if (s_interactButton == null)
        {
            GameObject controls = GameObject.Find("controls");
            if (controls != null)
            {
                Transform button = controls.transform.Find("btnInteract");
                if (button != null)
                    s_interactButton = button.gameObject;
            }
        }

        if (s_interactButton == null)
            return;

        if (s_interactButton.GetComponent<LootInteractButtonBinder>() == null)
            s_interactButton.AddComponent<LootInteractButtonBinder>();

        if (s_interactButton.GetComponent<UiBounceLoopAnimation>() == null)
            s_interactButton.AddComponent<UiBounceLoopAnimation>();
    }
}
