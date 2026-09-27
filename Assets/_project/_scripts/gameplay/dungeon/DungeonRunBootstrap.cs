using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Ensures <see cref="DungeonRunHost"/> exists, registers catalog, and EventSystem for UI.
/// </summary>
public sealed class DungeonRunBootstrap : MonoBehaviour
{
    [SerializeField] DungeonCellCatalog catalog;
    [SerializeField] DungeonRunController runController;

    public DungeonRunController RunController => runController;

    void Awake()
    {
        DungeonRunHost.EnsureExists();
        EnsureEventSystem();

        if (catalog != null)
            DungeonCellCatalog.SetCurrent(catalog);

        if (runController == null)
            runController = GetComponent<DungeonRunController>();
    }

    void OnDestroy()
    {
        // Host outlives this scene object — do not clear catalog while a run host remains.
        if (DungeonRunHost.Instance != null)
            return;

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
