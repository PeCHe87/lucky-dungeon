using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Ally survivor that can be channel-connected by <see cref="PlayerSurvivorConnector"/>
/// and then NavMesh-follows an assigned follow-slot Transform around the player.
/// Animation: <c>not_connected_idle</c> before connect, one-shot <c>connection_success</c> on connect,
/// then regular idle / move while escorting.
/// </summary>
[RequireComponent(typeof(CombatEntityHealth))]
[DefaultExecutionOrder(114)]
public sealed class SurvivorAlly : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CombatEntityHealth health;
    [SerializeField] NavMeshAgent agent;
    [SerializeField] Animator animator;
    [SerializeField] EntityNavLocomotionAnimator navLocomotionAnimator;

    [Header("Follow")]
    [SerializeField, Min(0.1f)] float followStopDistance = 1.5f;
    [SerializeField, Min(0.1f)] float navMeshSampleRadius = 2f;
    [Tooltip("After arriving at the follow slot, wait this long once the slot leaves stop range before chasing again. Prevents jitter from small player moves.")]
    [SerializeField, Min(0f)] float followResumeDelay = 0.4f;

    [Header("Animation")]
    [Tooltip("Animator state while waiting to be rescued (before connection).")]
    [SerializeField] string notConnectedIdleStateName = "not_connected_idle";
    [Tooltip("One-shot played when the player finishes connecting.")]
    [SerializeField] string connectionSuccessStateName = "connection_success";
    [Tooltip("Animator state while connected and not moving.")]
    [SerializeField] string connectedIdleStateName = "idle";
    [Tooltip("Animator state while connected and following the leader.")]
    [SerializeField] string moveStateName = "Run";
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.15f;
    [SerializeField, Range(0.5f, 1f)] float connectionSuccessCompletionNormalizedTime = 0.95f;
    [SerializeField, Min(0f)] float moveSpeedThreshold = 0.05f;
    [SerializeField, Min(0f)] float remainingDistanceBuffer = 0.05f;

    [Header("Connect progress UI")]
    [SerializeField] Image progressFill;
    [SerializeField] Image progressBackground;
    [SerializeField] Transform progressRoot;
    [SerializeField] Vector3 progressLocalOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] Vector2 progressBarSize = new Vector2(100f, 12f);
    [SerializeField, Min(0.001f)] float progressCanvasWorldScale = 0.01f;
    [SerializeField] Color progressBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [SerializeField] Color progressFillColor = new Color(0.3f, 0.75f, 1f, 1f);

    static Sprite s_whiteSprite;

    Transform _leader;
    Transform _followSlot;
    PlayerSurvivorConnector _leaderConnector;
    bool _connected;
    bool _playingConnectionSuccess;
    Vector3 _lastSampledGoal;
    bool _hasSampledGoal;
    bool _settledAtFollowGoal;
    bool _waitingToResumeFollow;
    float _resumeFollowAtTime;
    int _notConnectedIdleHash;
    int _connectionSuccessHash;
    int _idleHash;
    int _moveHash;
    int _lastPlayedHash = int.MinValue;
    bool _navLocomotionWasEnabled;
    bool _ownsNavLocomotionDisable;

    public bool IsConnected => _connected;
    public Transform Leader => _leader;
    public Transform FollowSlot => _followSlot;
    public bool IsPlayingConnectionSuccess => _playingConnectionSuccess;

    public bool CanConnect =>
        isActiveAndEnabled
        && !_connected
        && health != null
        && !health.IsDefeated;

    public event Action Connected;
    public event Action Disconnected;

    void Awake()
    {
        if (health == null)
            health = GetComponent<CombatEntityHealth>();
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (navLocomotionAnimator == null)
            navLocomotionAnimator = GetComponent<EntityNavLocomotionAnimator>();

        if (animator != null)
            animator.applyRootMotion = false;

        RebuildLocomotionHashes();

        if (progressFill == null)
            EnsureProgressHierarchy();

        SetConnectProgress(0f, false);
    }

    void OnEnable()
    {
        if (health != null)
            health.Died += OnDied;

        TakeOverLocomotionAnimator();
        if (!_connected)
            PlayNotConnectedIdle(force: true);
    }

    void OnDisable()
    {
        if (health != null)
            health.Died -= OnDied;

        ClearConnection();
        RestoreLocomotionAnimator();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
            RebuildLocomotionHashes();
    }

    void Update()
    {
        if (!_connected)
            return;

        if (_leader == null || health == null || health.IsDefeated)
        {
            ClearConnection();
            return;
        }

        if (_playingConnectionSuccess || agent == null || !agent.isOnNavMesh)
            return;

        if (_leaderConnector != null && _leaderConnector.IsFollowerHoldActive)
        {
            if (agent.hasPath)
                agent.ResetPath();
            return;
        }

        Vector3 followPosition = GetFollowPosition();
        float stop = Mathf.Max(0.1f, followStopDistance);
        agent.stoppingDistance = stop;

        if (NavMeshChaseDriver.IsWithinXZRadius(transform.position, followPosition, stop))
        {
            MarkSettledAtFollowGoal();
            if (agent.hasPath)
                agent.ResetPath();
            return;
        }

        if (!ShouldResumeFollow())
        {
            if (agent.hasPath)
                agent.ResetPath();
            return;
        }

        NavMeshChaseDriver.RefreshChaseDestinationIfNeeded(
            agent,
            followPosition,
            navMeshSampleRadius,
            ref _lastSampledGoal,
            ref _hasSampledGoal);
    }

    void LateUpdate()
    {
        if (animator == null || health != null && health.IsDefeated)
            return;

        if (!_connected)
        {
            CrossFadeIfNeeded(_notConnectedIdleHash);
            return;
        }

        if (_playingConnectionSuccess)
        {
            if (IsConnectionSuccessComplete())
                _playingConnectionSuccess = false;
            else
                return;
        }

        bool moving = IsAgentMoving();
        if (moving)
            EntityNavChaseAttackSupport.SyncLocomotionFacing(agent, gameObject);

        CrossFadeIfNeeded(moving ? _moveHash : _idleHash);
    }

    /// <summary>Updates the world progress bar while the player channels a connection.</summary>
    public void SetConnectProgress(float normalized01, bool visible)
    {
        if (progressFill == null)
            EnsureProgressHierarchy();

        if (progressRoot != null)
            progressRoot.gameObject.SetActive(visible && !_connected);

        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(normalized01);
    }

    /// <summary>
    /// Locks this survivor as connected, plays connection success, then follows <paramref name="followSlot"/>.
    /// Falls back to the leader position if the slot is missing.
    /// </summary>
    public void MarkConnected(Transform leader, Transform followSlot)
    {
        if (!CanConnect || leader == null)
            return;

        _leader = leader;
        _followSlot = followSlot != null ? followSlot : leader;
        _leaderConnector = leader.GetComponent<PlayerSurvivorConnector>();
        _connected = true;
        _playingConnectionSuccess = true;
        _hasSampledGoal = false;
        ResetFollowResumeState();
        _lastPlayedHash = int.MinValue;
        SetConnectProgress(0f, false);
        TakeOverLocomotionAnimator();
        StopFollowing();

        if (agent == null)
        {
            Debug.LogWarning(
                $"[{nameof(SurvivorAlly)}] '{name}' connected but has no NavMeshAgent; follow disabled.",
                this);
        }
        else
        {
            agent.stoppingDistance = Mathf.Max(0.1f, followStopDistance);
            if (!agent.enabled)
                agent.enabled = true;
        }

        CrossFadeIfNeeded(_connectionSuccessHash);
        Connected?.Invoke();
    }

    Vector3 GetFollowPosition()
    {
        if (_followSlot != null)
            return _followSlot.position;
        if (_leader != null)
            return _leader.position;
        return transform.position;
    }

    void OnDied()
    {
        SetConnectProgress(0f, false);
        ClearConnection();
    }

    void ClearConnection()
    {
        if (!_connected)
        {
            StopFollowing();
            PlayNotConnectedIdle(force: false);
            return;
        }

        _connected = false;
        _playingConnectionSuccess = false;
        _leader = null;
        _followSlot = null;
        _leaderConnector = null;
        _lastPlayedHash = int.MinValue;
        ResetFollowResumeState();
        StopFollowing();
        PlayNotConnectedIdle(force: true);
        Disconnected?.Invoke();
    }

    void StopFollowing()
    {
        _hasSampledGoal = false;
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (agent.hasPath)
            agent.ResetPath();
    }

    void MarkSettledAtFollowGoal()
    {
        _settledAtFollowGoal = true;
        _waitingToResumeFollow = false;
        _resumeFollowAtTime = 0f;
    }

    void ResetFollowResumeState()
    {
        _settledAtFollowGoal = false;
        _waitingToResumeFollow = false;
        _resumeFollowAtTime = 0f;
    }

    /// <summary>
    /// First chase after connect is immediate. After settling on the slot, wait
    /// <see cref="followResumeDelay"/> once the slot leaves stop range before chasing again.
    /// </summary>
    bool ShouldResumeFollow()
    {
        if (!_settledAtFollowGoal)
            return true;

        float delay = Mathf.Max(0f, followResumeDelay);
        if (delay <= 0f)
            return true;

        if (!_waitingToResumeFollow)
        {
            _waitingToResumeFollow = true;
            _resumeFollowAtTime = Time.time + delay;
            return false;
        }

        if (Time.time < _resumeFollowAtTime)
            return false;

        _settledAtFollowGoal = false;
        _waitingToResumeFollow = false;
        return true;
    }

    void TakeOverLocomotionAnimator()
    {
        if (navLocomotionAnimator == null || _ownsNavLocomotionDisable)
            return;

        _navLocomotionWasEnabled = navLocomotionAnimator.enabled;
        if (_navLocomotionWasEnabled)
            navLocomotionAnimator.enabled = false;
        _ownsNavLocomotionDisable = true;
    }

    void RestoreLocomotionAnimator()
    {
        if (!_ownsNavLocomotionDisable)
            return;

        if (navLocomotionAnimator != null && _navLocomotionWasEnabled)
            navLocomotionAnimator.enabled = true;

        _navLocomotionWasEnabled = false;
        _ownsNavLocomotionDisable = false;
    }

    void PlayNotConnectedIdle(bool force)
    {
        if (force)
            _lastPlayedHash = int.MinValue;
        CrossFadeIfNeeded(_notConnectedIdleHash);
    }

    void RebuildLocomotionHashes()
    {
        _notConnectedIdleHash = Animator.StringToHash(
            string.IsNullOrEmpty(notConnectedIdleStateName) ? "not_connected_idle" : notConnectedIdleStateName);
        _connectionSuccessHash = Animator.StringToHash(
            string.IsNullOrEmpty(connectionSuccessStateName) ? "connection_success" : connectionSuccessStateName);
        _idleHash = Animator.StringToHash(
            string.IsNullOrEmpty(connectedIdleStateName) ? "idle" : connectedIdleStateName);
        _moveHash = Animator.StringToHash(
            string.IsNullOrEmpty(moveStateName) ? "Run" : moveStateName);
        _lastPlayedHash = int.MinValue;
    }

    bool IsConnectionSuccessComplete()
    {
        if (animator == null)
            return true;

        if (animator.IsInTransition(0))
            return false;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.shortNameHash != _connectionSuccessHash)
            return true;

        return info.normalizedTime >= connectionSuccessCompletionNormalizedTime;
    }

    bool IsAgentMoving()
    {
        if (agent == null || !agent.isOnNavMesh || !agent.enabled || agent.isStopped)
            return false;

        if (agent.pathPending)
            return false;

        float speedThresholdSq = moveSpeedThreshold * moveSpeedThreshold;
        if (agent.velocity.sqrMagnitude > speedThresholdSq)
            return true;

        return agent.hasPath
            && agent.remainingDistance > agent.stoppingDistance + remainingDistanceBuffer;
    }

    void CrossFadeIfNeeded(int stateHash)
    {
        if (animator == null)
            return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.shortNameHash == stateHash && !animator.IsInTransition(0))
            return;

        if (stateHash == _lastPlayedHash && animator.IsInTransition(0))
            return;

        animator.CrossFadeInFixedTime(stateHash, crossFadeSeconds, 0, 0f);
        _lastPlayedHash = stateHash;
    }

    void EnsureProgressHierarchy()
    {
        Sprite sprite = GetWhiteSprite();

        var anchorGo = new GameObject("ConnectProgressBar");
        anchorGo.transform.SetParent(transform, false);
        anchorGo.transform.localPosition = progressLocalOffset;
        anchorGo.transform.localRotation = Quaternion.identity;
        anchorGo.transform.localScale = Vector3.one;
        progressRoot = anchorGo.transform;

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(anchorGo.transform, false);
        canvasGo.transform.localPosition = Vector3.zero;
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * progressCanvasWorldScale;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 110;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = progressBarSize;

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        progressBackground = bgGo.AddComponent<Image>();
        progressBackground.sprite = sprite;
        progressBackground.color = progressBackgroundColor;
        progressBackground.raycastTarget = false;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        progressFill = fillGo.AddComponent<Image>();
        progressFill.sprite = sprite;
        progressFill.color = progressFillColor;
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
        progressFill.raycastTarget = false;

        canvasGo.AddComponent<BillboardFacingCamera>();
    }

    static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null)
            return s_whiteSprite;

        s_whiteSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f);
        return s_whiteSprite;
    }
}
