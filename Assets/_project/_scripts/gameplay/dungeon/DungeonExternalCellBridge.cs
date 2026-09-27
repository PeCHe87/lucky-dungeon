using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Place in an external cell scene (e.g. battleground). Call <see cref="NotifySucceeded"/>
/// when the player finishes the cell successfully to complete it and return to the dungeon.
/// </summary>
public sealed class DungeonExternalCellBridge : MonoBehaviour
{
    [Tooltip("If set, clicking this Button calls NotifySucceeded (debug / prototype).")]
    [SerializeField] UnityEngine.UI.Button debugCompleteButton;

    [Tooltip("Optional keyboard shortcut to complete the external cell (debug). None = disabled.")]
    [SerializeField] Key debugCompleteKey = Key.F6;

    void OnEnable()
    {
        if (debugCompleteButton != null)
            debugCompleteButton.onClick.AddListener(NotifySucceeded);
    }

    void OnDisable()
    {
        if (debugCompleteButton != null)
            debugCompleteButton.onClick.RemoveListener(NotifySucceeded);
    }

    void Update()
    {
        if (debugCompleteKey == Key.None || Keyboard.current == null)
            return;

        if (Keyboard.current[debugCompleteKey].wasPressedThisFrame)
            NotifySucceeded();
    }

    /// <summary>Completes the awaiting dungeon cell and loads back to the dungeon scene.</summary>
    public void NotifySucceeded()
    {
        DungeonRunHost host = DungeonRunHost.Instance;
        if (host == null)
        {
            Debug.LogWarning("[DungeonExternalCellBridge] No DungeonRunHost — open dungeon first.", this);
            return;
        }

        host.NotifyExternalCellSucceeded();
    }
}