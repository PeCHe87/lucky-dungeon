using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds the dungeon progress UI to the active runtime <see cref="Dungeon"/>.
/// On cell complete, slides the cells strip so the next current cell is screen-centered,
/// then refreshes Locked/Active/Completed visuals.
/// </summary>
public sealed class DungeonProgressPresenter : MonoBehaviour
{
    [SerializeField] DungeonRunController runController;
    [SerializeField] Transform cellsContainer;
    [Tooltip("Used when a cell definition has no CellPrefab assigned.")]
    [SerializeField] DungeonCellView cellViewPrefab;
    [SerializeField] Button goButton;
    [SerializeField] TextMeshProUGUI levelLabel;
    [Tooltip("World/UI point the current cell should align under. Defaults to content/currentStage.")]
    [SerializeField] RectTransform centerAnchor;
    [SerializeField, Min(0.01f)] float slideDuration = 0.35f;

    readonly List<DungeonCellView> _cellViews = new List<DungeonCellView>();
    readonly List<DungeonCellView> _spawnedViews = new List<DungeonCellView>();
    Dungeon _boundDungeon;
    RectTransform _cellsRect;
    bool _goButtonBound;
    bool _isSliding;
    Coroutine _slideRoutine;

    void Awake() => ResolveReferences();

    void OnEnable()
    {
        GameEvents.DungeonRunStarted += OnDungeonRunStarted;
        GameEvents.DungeonCellCompleted += OnDungeonCellCompleted;
        GameEvents.DungeonRunFinished += OnDungeonRunFinished;

        ResolveReferences();
        BindGoButton();
        RefreshFromController();
    }

    void Start()
    {
        ResolveReferences();
        BindGoButton();
        RefreshFromController();
    }

    void OnDisable()
    {
        GameEvents.DungeonRunStarted -= OnDungeonRunStarted;
        GameEvents.DungeonCellCompleted -= OnDungeonCellCompleted;
        GameEvents.DungeonRunFinished -= OnDungeonRunFinished;

        StopSlide();
        UnbindGoButton();
        DestroySpawnedViews();
    }

    void OnGoClicked()
    {
        if (_isSliding)
            return;

        ResolveReferences();
        DungeonRunHost host = DungeonRunHost.Instance ?? DungeonRunHost.EnsureExists();
        if (host == null)
        {
            Debug.LogWarning("[DungeonProgressPresenter] GO pressed but no DungeonRunHost found.", this);
            return;
        }

        host.ResolveCurrentCell();
    }

    void OnDungeonRunStarted(Dungeon dungeon) => BindDungeon(dungeon);

    void OnDungeonCellCompleted(int index, DungeonCellData definition)
    {
        if (_boundDungeon == null)
            return;

        StartSlideToCurrentThenRefresh();
    }

    void OnDungeonRunFinished(Dungeon dungeon)
    {
        if (dungeon != null)
            _boundDungeon = dungeon;

        // If a slide is already running from CellCompleted, it will refresh at the end.
        if (!_isSliding)
            RefreshStatuses();
    }

    void RefreshFromController()
    {
        Dungeon dungeon = null;
        if (runController != null)
            dungeon = runController.ActiveDungeon;
        if (dungeon == null && DungeonRunHost.Instance != null)
            dungeon = DungeonRunHost.Instance.ActiveDungeon;

        if (dungeon != null)
            BindDungeon(dungeon);
    }

    void BindDungeon(Dungeon dungeon)
    {
        ResolveReferences();
        StopSlide();
        _boundDungeon = dungeon;

        if (dungeon == null)
        {
            DestroySpawnedViews();
            HideScenePlaceholderChildren();
            UpdateGoButton(false);
            return;
        }

        RebuildViewsFromDungeon(dungeon);
        RefreshStatuses();
        SnapCenterToIndex(GetCenterTargetIndex(dungeon));
    }

    void RefreshStatuses()
    {
        if (_boundDungeon == null)
            return;

        for (int i = 0; i < _boundDungeon.CellCount && i < _cellViews.Count; i++)
        {
            if (_cellViews[i] != null)
                _cellViews[i].Bind(_boundDungeon.GetCell(i));
        }

        UpdateLevelLabel();
        UpdateGoButton(!_boundDungeon.IsComplete && !_isSliding);
    }

    void UpdateLevelLabel()
    {
        if (levelLabel == null || _boundDungeon == null)
            return;

        int total = _boundDungeon.CellCount;
        int current = total == 0
            ? 0
            : _boundDungeon.IsComplete
                ? total
                : _boundDungeon.CurrentIndex + 1;

        levelLabel.text = $"Level {current}/{total}";
    }

    void StartSlideToCurrentThenRefresh()
    {
        if (_boundDungeon == null || _cellsRect == null)
        {
            RefreshStatuses();
            return;
        }

        int targetIndex = GetCenterTargetIndex(_boundDungeon);
        if (!TryGetCenteredTargetX(targetIndex, out float targetX))
        {
            RefreshStatuses();
            return;
        }

        StopSlide();
        _slideRoutine = StartCoroutine(SlideToXThenRefresh(targetX));
    }

    IEnumerator SlideToXThenRefresh(float targetX)
    {
        _isSliding = true;
        UpdateGoButton(false);

        float startX = _cellsRect.anchoredPosition.x;
        float elapsed = 0f;

        if (slideDuration <= 0.01f || Mathf.Approximately(startX, targetX))
        {
            SetCellsAnchoredX(targetX);
        }
        else
        {
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = t * t * (3f - 2f * t); // SmoothStep
                SetCellsAnchoredX(Mathf.Lerp(startX, targetX, eased));
                yield return null;
            }

            SetCellsAnchoredX(targetX);
        }

        _isSliding = false;
        _slideRoutine = null;
        RefreshStatuses();
    }

    void SnapCenterToIndex(int index)
    {
        if (!TryGetCenteredTargetX(index, out float targetX))
            return;

        SetCellsAnchoredX(targetX);
    }

    bool TryGetCenteredTargetX(int index, out float targetX)
    {
        targetX = 0f;
        ResolveReferences();

        if (_cellsRect == null || centerAnchor == null)
            return false;

        if (index < 0 || index >= _cellViews.Count || _cellViews[index] == null)
            return false;

        var cellRect = _cellViews[index].transform as RectTransform;
        if (cellRect == null)
            return false;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_cellsRect);

        Transform parent = _cellsRect.parent;
        if (parent == null)
            return false;

        Vector3 cellWorld = cellRect.TransformPoint(cellRect.rect.center);
        Vector3 anchorWorld = centerAnchor.TransformPoint(centerAnchor.rect.center);
        float cellLocalX = parent.InverseTransformPoint(cellWorld).x;
        float anchorLocalX = parent.InverseTransformPoint(anchorWorld).x;
        float deltaX = anchorLocalX - cellLocalX;

        targetX = _cellsRect.anchoredPosition.x + deltaX;
        return true;
    }

    static int GetCenterTargetIndex(Dungeon dungeon)
    {
        if (dungeon == null || dungeon.CellCount == 0)
            return 0;

        if (dungeon.IsComplete)
            return dungeon.CellCount - 1;

        return Mathf.Clamp(dungeon.CurrentIndex, 0, dungeon.CellCount - 1);
    }

    void SetCellsAnchoredX(float x)
    {
        if (_cellsRect == null)
            return;

        Vector2 pos = _cellsRect.anchoredPosition;
        pos.x = x;
        _cellsRect.anchoredPosition = pos;
    }

    void StopSlide()
    {
        if (_slideRoutine != null)
        {
            StopCoroutine(_slideRoutine);
            _slideRoutine = null;
        }

        _isSliding = false;
    }

    void RebuildViewsFromDungeon(Dungeon dungeon)
    {
        if (cellsContainer == null || dungeon == null)
            return;

        DestroySpawnedViews();
        HideScenePlaceholderChildren();
        _cellViews.Clear();

        for (int i = 0; i < dungeon.CellCount; i++)
        {
            DungeonCellData definition = dungeon.GetCell(i).Definition;
            DungeonCellView prefab = definition != null ? definition.CellPrefab : null;
            if (prefab == null)
                prefab = cellViewPrefab;

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[DungeonProgressPresenter] No cell prefab for index {i}" +
                    (definition != null ? $" ('{definition.CellId}')" : string.Empty) +
                    " and no fallback cellViewPrefab assigned.",
                    this);
                continue;
            }

            DungeonCellView view = Instantiate(prefab, cellsContainer);
            view.gameObject.SetActive(true);
            _spawnedViews.Add(view);
            _cellViews.Add(view);
        }
    }

    void DestroySpawnedViews()
    {
        for (int i = 0; i < _spawnedViews.Count; i++)
        {
            if (_spawnedViews[i] == null)
                continue;
            Destroy(_spawnedViews[i].gameObject);
        }

        _spawnedViews.Clear();
        _cellViews.Clear();
    }

    void HideScenePlaceholderChildren()
    {
        if (cellsContainer == null)
            return;

        for (int i = 0; i < cellsContainer.childCount; i++)
        {
            Transform child = cellsContainer.GetChild(i);
            if (child == null)
                continue;

            // Skip instances we just spawned this frame (they are already tracked).
            bool isSpawned = false;
            for (int s = 0; s < _spawnedViews.Count; s++)
            {
                if (_spawnedViews[s] != null && _spawnedViews[s].transform == child)
                {
                    isSpawned = true;
                    break;
                }
            }

            if (!isSpawned)
                child.gameObject.SetActive(false);
        }
    }

    void UpdateGoButton(bool interactable)
    {
        if (goButton != null)
            goButton.interactable = interactable;
    }

    void BindGoButton()
    {
        if (_goButtonBound || goButton == null)
            return;

        goButton.onClick.AddListener(OnGoClicked);
        _goButtonBound = true;
    }

    void UnbindGoButton()
    {
        if (!_goButtonBound || goButton == null)
            return;

        goButton.onClick.RemoveListener(OnGoClicked);
        _goButtonBound = false;
    }

    void ResolveReferences()
    {
        if (runController == null)
            runController = FindFirstObjectByType<DungeonRunController>();

        DungeonRunHost.EnsureExists();

        if (cellsContainer == null)
        {
            Transform t = transform.Find("progression/cells");
            if (t == null)
                t = FindDeepChild(transform, "cells");
            if (t != null)
                cellsContainer = t;
        }

        if (cellsContainer != null)
            _cellsRect = cellsContainer as RectTransform;

        if (centerAnchor == null)
        {
            Transform t = FindDeepChild(transform, "currentStage");
            if (t == null)
                t = FindDeepChild(transform, "content");
            if (t != null)
                centerAnchor = t as RectTransform;
        }

        if (goButton == null)
        {
            Transform t = FindDeepChild(transform, "btnGo");
            if (t != null)
                goButton = t.GetComponent<Button>();
        }

        if (levelLabel == null)
        {
            Transform t = FindDeepChild(transform, "txtLevel");
            if (t != null)
                levelLabel = t.GetComponent<TextMeshProUGUI>();
        }
    }

    static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
                return child;

            Transform found = FindDeepChild(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
