using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Taps <see cref="InteractableLoot.CurrentSearchable"/> to begin the open channel.
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
        if (loot == null)
            return;

        loot.TryBeginOpen();
    }
}
