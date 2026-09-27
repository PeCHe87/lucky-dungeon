using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for a dungeon cell prefab: toggles locked/active/completed only.
/// Icon and description come from the prefab; they are not overwritten from data.
/// </summary>
public sealed class DungeonCellView : MonoBehaviour
{
    [SerializeField] GameObject lockedRoot;
    [SerializeField] GameObject activeRoot;
    [SerializeField] GameObject completedRoot;
    [SerializeField] TextMeshProUGUI descriptionLabel;
    [SerializeField] Button cellButton;

    DungeonCellData _definition;
    DungeonCellStatus _status;
    bool _bound;
    bool _buttonBound;

    void Awake() => ResolveReferences();

    void OnEnable()
    {
        ResolveReferences();
        BindButton();
    }

    void OnDisable()
    {
        if (cellButton != null && _buttonBound)
        {
            cellButton.onClick.RemoveListener(OnCellClicked);
            _buttonBound = false;
        }
    }

    public void Bind(DungeonCell cell)
    {
        ResolveReferences();
        BindButton();

        _definition = cell.Definition;
        _status = cell.Status;
        _bound = true;

        SetStatus(_status);
    }

    public void SetStatus(DungeonCellStatus status)
    {
        ResolveReferences();
        _status = status;

        if (lockedRoot != null)
            lockedRoot.SetActive(status == DungeonCellStatus.Locked);
        if (activeRoot != null)
            activeRoot.SetActive(status == DungeonCellStatus.Active);
        if (completedRoot != null)
            completedRoot.SetActive(status == DungeonCellStatus.Completed);
    }

    /// <summary>Wired to the child <c>btn</c> Button onClick.</summary>
    public void OnCellClicked()
    {
        if (!_bound)
        {
            Debug.Log("[DungeonCell] Clicked unbound cell view.", this);
            return;
        }

        string cellType = _definition != null ? _definition.CellType.ToString() : "(none)";
        string description = descriptionLabel != null
            ? descriptionLabel.text
            : string.Empty;
        string progress = StatusToProgressLabel(_status);

        Debug.Log(
            $"[DungeonCell] type={cellType} | description=\"{description}\" | status={progress}",
            this);
    }

    void BindButton()
    {
        if (_buttonBound || cellButton == null)
            return;

        cellButton.onClick.AddListener(OnCellClicked);
        _buttonBound = true;
    }

    static string StatusToProgressLabel(DungeonCellStatus status)
    {
        switch (status)
        {
            case DungeonCellStatus.Locked:
                return "locked";
            case DungeonCellStatus.Active:
                return "in progress";
            case DungeonCellStatus.Completed:
                return "completed";
            default:
                return status.ToString();
        }
    }

    void ResolveReferences()
    {
        if (lockedRoot == null)
        {
            Transform t = transform.Find("locked");
            if (t != null)
                lockedRoot = t.gameObject;
        }

        if (activeRoot == null)
        {
            Transform t = transform.Find("active");
            if (t != null)
                activeRoot = t.gameObject;
        }

        if (completedRoot == null)
        {
            Transform t = transform.Find("completed");
            if (t != null)
                completedRoot = t.gameObject;
        }

        if (cellButton == null)
        {
            Transform t = transform.Find("btn");
            if (t != null)
                cellButton = t.GetComponent<Button>();
        }

        if (descriptionLabel == null)
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].gameObject.name == "txtDescription")
                {
                    descriptionLabel = labels[i];
                    break;
                }
            }
        }
    }
}
