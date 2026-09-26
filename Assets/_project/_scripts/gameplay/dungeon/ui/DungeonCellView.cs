using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for a single <c>ui_dungeon_cell</c> prefab: toggles locked/active/completed and binds icon/description.
/// The child <c>btn</c> Button logs cell type, description, and progress status on click.
/// </summary>
public sealed class DungeonCellView : MonoBehaviour
{
    [SerializeField] GameObject lockedRoot;
    [SerializeField] GameObject activeRoot;
    [SerializeField] GameObject completedRoot;
    [SerializeField] Image[] icons;
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

        string description = _definition != null ? _definition.Description : string.Empty;
        Sprite icon = _definition != null ? _definition.Icon : null;

        if (descriptionLabel != null)
            descriptionLabel.text = description;

        if (icons != null)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null)
                    continue;
                icons[i].sprite = icon;
                icons[i].enabled = icon != null;
            }
        }

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
        string description = _definition != null ? _definition.Description : string.Empty;
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

        if (icons == null || icons.Length == 0)
        {
            Image[] allImages = GetComponentsInChildren<Image>(true);
            int count = 0;
            for (int i = 0; i < allImages.Length; i++)
            {
                if (allImages[i] != null && allImages[i].gameObject.name == "icon")
                    count++;
            }

            if (count > 0)
            {
                icons = new Image[count];
                int write = 0;
                for (int i = 0; i < allImages.Length; i++)
                {
                    if (allImages[i] != null && allImages[i].gameObject.name == "icon")
                        icons[write++] = allImages[i];
                }
            }
        }
    }
}
