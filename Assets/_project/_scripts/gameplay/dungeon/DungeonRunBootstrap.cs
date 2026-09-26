using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Registers a <see cref="DungeonCellCatalog"/> (optional) and hosts <see cref="DungeonRunController"/> wiring.
/// Ensures an EventSystem exists so dungeon UI buttons receive clicks.
/// </summary>
public sealed class DungeonRunBootstrap : MonoBehaviour
{
    [SerializeField] DungeonCellCatalog catalog;
    [SerializeField] DungeonRunController runController;

    public DungeonRunController RunController => runController;

    void Awake()
    {
        EnsureEventSystem();

        if (catalog != null)
            DungeonCellCatalog.SetCurrent(catalog);

        if (runController == null)
            runController = GetComponent<DungeonRunController>();
    }

    void OnDestroy()
    {
        if (catalog != null && DungeonCellCatalog.Current == catalog)
            DungeonCellCatalog.SetCurrent(null);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null
            || Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
