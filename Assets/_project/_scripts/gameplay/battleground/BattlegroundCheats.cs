using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug / cheat hooks for the battleground scene.
/// Wire <see cref="CompleteLevel"/> to <c>btnCompleteLevel</c> OnClick, or leave the
/// button field empty to auto-bind a child / scene object named <c>btnCompleteLevel</c>.
/// </summary>
public sealed class BattlegroundCheats : MonoBehaviour
{
    [SerializeField] Button completeLevelButton;

    void Awake()
    {
        if (completeLevelButton == null)
            completeLevelButton = FindCompleteLevelButton();
    }

    void OnEnable()
    {
        if (completeLevelButton != null)
            completeLevelButton.onClick.AddListener(CompleteLevel);
    }

    void OnDisable()
    {
        if (completeLevelButton != null)
            completeLevelButton.onClick.RemoveListener(CompleteLevel);
    }

    /// <summary>
    /// Finishes the current battle, marks the dungeon cell complete, and returns to the dungeon.
    /// Assign this to <c>btnCompleteLevel</c> Button OnClick in the Inspector if not auto-bound.
    /// </summary>
    public void CompleteLevel()
    {
        DungeonRunHost host = DungeonRunHost.Instance;
        if (host == null)
        {
            Debug.LogWarning(
                "[BattlegroundCheats] No DungeonRunHost — start from the dungeon scene first.",
                this);
            return;
        }

        host.NotifyExternalCellSucceeded();
    }

    Button FindCompleteLevelButton()
    {
        Transform local = transform.Find("btnCompleteLevel");
        if (local != null)
            return local.GetComponent<Button>();

        GameObject go = GameObject.Find("btnCompleteLevel");
        return go != null ? go.GetComponent<Button>() : null;
    }
}
