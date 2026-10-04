using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds the dungeon progress UI to the active runtime <see cref="Dungeon"/>.
/// On cell complete, slides the cells strip upward by a fixed <see cref="slideMovementSize"/>,
/// then refreshes Locked/Active/Completed visuals.
/// When loading with existing progress, plays a short resume intro: previous step → delay → slide to current.
/// </summary>
public sealed class DungeonProgressPresenter : MonoBehaviour
{
    [SerializeField] DungeonRunController runController;
    [SerializeField] Transform cellsContainer;
    [Tooltip("Used when a cell definition has no CellPrefab assigned.")]
    [SerializeField] DungeonCellView cellViewPrefab;
    [SerializeField] Button goButton;
    [SerializeField] TextMeshProUGUI levelLabel;
    [SerializeField, Min(0.01f)] float slideDuration = 0.35f;
    [Tooltip("Fixed upward UI units moved per cell advance.")]
    [SerializeField, Min(0f)] float slideMovementSize = 100f;
    [Tooltip("Delay before playing the resume slide (previous cell → current) when loading with progress.")]
    [SerializeField, Min(0f)] float resumeSlideDelay = 0.5f;

    readonly List<DungeonCellView> _cellViews = new List<DungeonCellView>();
    readonly List<DungeonCellView> _spawnedViews = new List<DungeonCellView>();
    Dungeon _boundDungeon;
    RectTransform _cellsRect;
    float _slideBaseY;
    bool _hasSlideBaseY;
    bool _goButtonBound;
    bool _isSliding;
    bool _hasPlayedResumeIntro;
    Coroutine _slideRoutine;
    Coroutine _snapRoutine;
    Coroutine _resumeIntroRoutine;

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
        StopSnap();
        StopResumeIntro();
        UnbindGoButton();
        DestroySpawnedViews();
        _hasSlideBaseY = false;
        _hasPlayedResumeIntro = false;
    }

    bool IsProgressBusy => _isSliding || _resumeIntroRoutine != null;

    void OnGoClicked()
    {
        if (IsProgressBusy)
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
        if (_boundDungeon == null || _resumeIntroRoutine != null)
            return;

        StartSlideToCurrentThenRefresh();
    }

    void OnDungeonRunFinished(Dungeon dungeon)
    {
        if (dungeon != null)
            _boundDungeon = dungeon;

        // If a slide / resume intro is running, it will refresh at the end.
        if (!IsProgressBusy)
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

        // Avoid tearing down / restarting an in-flight resume intro on duplicate binds (OnEnable + Start + Resume).
        if (ShouldSkipRebind(dungeon))
        {
            // Do not RefreshStatuses during resume intro — that would reveal the real current cell early.
            if (!IsProgressBusy)
                RefreshStatuses();
            return;
        }

        StopSlide();
        StopSnap();
        StopResumeIntro();
        _boundDungeon = dungeon;

        if (dungeon == null)
        {
            DestroySpawnedViews();
            HideScenePlaceholderChildren();
            UpdateGoButton(false);
            return;
        }

        RebuildViewsFromDungeon(dungeon);
        EnsureSlideBaseY();

        int currentSteps = GetProgressSlideSteps(dungeon);
        // Finished run: snap to last cell as Completed — no resume intro (congrats comes later).
        bool playResumeIntro = currentSteps > 0
            && !dungeon.IsComplete
            && !_hasPlayedResumeIntro
            && isActiveAndEnabled;

        if (playResumeIntro)
        {
            _hasPlayedResumeIntro = true;
            ApplyResumeIntroStatuses(currentSteps);
            _resumeIntroRoutine = StartCoroutine(PlayResumeSlideIntro(currentSteps));
        }
        else
        {
            if (dungeon.IsComplete)
                _hasPlayedResumeIntro = true;

            RefreshStatuses();
            ApplyProgressOffset(immediate: true);
        }
    }

    bool ShouldSkipRebind(Dungeon dungeon)
    {
        if (dungeon == null || _boundDungeon != dungeon)
            return false;

        if (_cellViews.Count != dungeon.CellCount)
            return false;

        return _resumeIntroRoutine != null || _isSliding || _hasPlayedResumeIntro;
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
        UpdateGoButton(!_boundDungeon.IsComplete && !IsProgressBusy);
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

        EnsureSlideBaseY();
        float targetY = GetSlideTargetY(GetProgressSlideSteps(_boundDungeon));

        StopSlide();
        StopSnap();
        _slideRoutine = StartCoroutine(SlideToYThenRefresh(targetY));
    }

    IEnumerator SlideToYThenRefresh(float targetY)
    {
        yield return SlideToY(targetY);
        _slideRoutine = null;
        RefreshStatuses();
    }

    IEnumerator SlideToY(float targetY)
    {
        _isSliding = true;
        UpdateGoButton(false);

        if (_cellsRect == null)
        {
            _isSliding = false;
            yield break;
        }

        float startY = _cellsRect.anchoredPosition.y;
        float elapsed = 0f;

        if (slideDuration <= 0.01f || Mathf.Approximately(startY, targetY))
        {
            SetCellsAnchoredY(targetY);
        }
        else
        {
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = t * t * (3f - 2f * t); // SmoothStep
                SetCellsAnchoredY(Mathf.Lerp(startY, targetY, eased));
                yield return null;
            }

            SetCellsAnchoredY(targetY);
        }

        _isSliding = false;
    }

    /// <summary>
    /// Resume intro: show previous as Active / current as Locked, wait, slide, then real status refresh.
    /// Example: current cell index 2 → place at step 1, delay, animate to step 2.
    /// </summary>
    IEnumerator PlayResumeSlideIntro(int currentSteps)
    {
        int fromSteps = Mathf.Max(0, currentSteps - 1);
        ApplyResumeIntroStatuses(currentSteps);

        // Survive layout rebuilds at the previous step first.
        SetCellsAnchoredY(GetSlideTargetY(fromSteps));
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (_cellsRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cellsRect);
        SetCellsAnchoredY(GetSlideTargetY(fromSteps));
        ApplyResumeIntroStatuses(currentSteps);
        yield return new WaitForEndOfFrame();
        SetCellsAnchoredY(GetSlideTargetY(fromSteps));
        ApplyResumeIntroStatuses(currentSteps);

        if (resumeSlideDelay > 0f)
            yield return new WaitForSecondsRealtime(resumeSlideDelay);

        float targetY = GetSlideTargetY(currentSteps);
        yield return SlideToY(targetY);

        _resumeIntroRoutine = null;
        RefreshStatuses();
    }

    /// <summary>
    /// Temporary visuals for resume intro: previous cell Active (in progress), current Locked.
    /// Earlier cells Completed; later cells Locked.
    /// </summary>
    void ApplyResumeIntroStatuses(int currentSteps)
    {
        if (_boundDungeon == null)
            return;

        int fromSteps = Mathf.Max(0, currentSteps - 1);
        int total = _boundDungeon.CellCount;

        for (int i = 0; i < total && i < _cellViews.Count; i++)
        {
            DungeonCellView view = _cellViews[i];
            if (view == null)
                continue;

            view.Bind(_boundDungeon.GetCell(i));

            if (i < fromSteps)
                view.SetStatus(DungeonCellStatus.Completed);
            else if (i == fromSteps)
                view.SetStatus(DungeonCellStatus.Active);
            else
                view.SetStatus(DungeonCellStatus.Locked);
        }

        if (levelLabel != null && total > 0)
            levelLabel.text = $"Level {fromSteps + 1}/{total}";

        UpdateGoButton(false);
    }

    /// <summary>
    /// Instantly positions the strip for existing progress (completed cells × slideMovementSize).
    /// Re-applies after layout so VerticalLayoutGroup / ContentSizeFitter cannot wipe it.
    /// </summary>
    void ApplyProgressOffset(bool immediate)
    {
        if (_boundDungeon == null || _cellsRect == null)
            return;

        int steps = GetProgressSlideSteps(_boundDungeon);
        SetCellsAnchoredY(GetSlideTargetY(steps));

        if (!immediate || !isActiveAndEnabled)
            return;

        StopSnap();
        _snapRoutine = StartCoroutine(ReapplyProgressOffsetAfterLayout(steps));
    }

    IEnumerator ReapplyProgressOffsetAfterLayout(int steps)
    {
        // Layout rebuilds can reset anchoredPosition after BindDungeon.
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (_cellsRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cellsRect);

        SetCellsAnchoredY(GetSlideTargetY(steps));
        yield return new WaitForEndOfFrame();
        SetCellsAnchoredY(GetSlideTargetY(steps));

        _snapRoutine = null;
    }

    void EnsureSlideBaseY()
    {
        ResolveReferences();
        if (_cellsRect == null)
            return;

        // Keep the unslid layout Y across rebinds so resume does not stack offsets.
        if (_hasSlideBaseY)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_cellsRect);
        _slideBaseY = _cellsRect.anchoredPosition.y;
        _hasSlideBaseY = true;
    }

    float GetSlideTargetY(int completedSteps)
    {
        int steps = Mathf.Max(0, completedSteps);
        // Positive Y moves the strip upward in UI space.
        return _slideBaseY + steps * slideMovementSize;
    }

    /// <summary>How many fixed slide steps to apply — equals cells already completed.</summary>
    static int GetProgressSlideSteps(Dungeon dungeon)
    {
        if (dungeon == null || dungeon.CellCount == 0)
            return 0;

        if (dungeon.IsComplete)
            return Mathf.Max(0, dungeon.CellCount - 1);

        // CurrentIndex advances after each completion, so it equals completed count.
        return Mathf.Clamp(dungeon.CurrentIndex, 0, dungeon.CellCount - 1);
    }

    void SetCellsAnchoredY(float y)
    {
        if (_cellsRect == null)
            return;

        Vector2 pos = _cellsRect.anchoredPosition;
        pos.y = y;
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

    void StopSnap()
    {
        if (_snapRoutine == null)
            return;

        StopCoroutine(_snapRoutine);
        _snapRoutine = null;
    }

    void StopResumeIntro()
    {
        if (_resumeIntroRoutine != null)
        {
            StopCoroutine(_resumeIntroRoutine);
            _resumeIntroRoutine = null;
        }

        // Nested SlideToY inside the intro may have left this true if the coroutine was stopped early.
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
