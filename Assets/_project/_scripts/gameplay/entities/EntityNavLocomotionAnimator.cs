using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Cross-fades Idle/Run on the entity <see cref="Animator"/> from <see cref="NavMeshAgent"/> movement
/// while the FSM is in a locomotion-eligible state (default: Idle only).
/// </summary>
[DefaultExecutionOrder(113)]
public sealed class EntityNavLocomotionAnimator : MonoBehaviour
{
    const string DefaultLocomotionFsmStateId = "Idle";
    const string TakeDamageFsmStateId = "TakeDamage";

    [SerializeField] NavMeshAgent agent;
    [SerializeField] Animator animator;
    [SerializeField] AIStateMachine stateMachine;
    [SerializeField] EntityAttackController attackController;
    [SerializeField] EntityTargetDetectedTelegraph targetDetectedTelegraph;
    [SerializeField] string locomotionFsmStateId = DefaultLocomotionFsmStateId;
    [SerializeField] string idleStateName = "Idle";
    [SerializeField] string runStateName = "Run";
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.15f;
    [SerializeField, Min(0f)] float moveSpeedThreshold = 0.05f;
    [SerializeField, Min(0f)] float remainingDistanceBuffer = 0.05f;

    int _idleHash;
    int _runHash;
    int _lastPlayedHash = int.MinValue;

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (stateMachine == null)
            stateMachine = GetComponent<AIStateMachine>();
        if (attackController == null)
            attackController = GetComponent<EntityAttackController>();
        if (targetDetectedTelegraph == null)
            targetDetectedTelegraph = GetComponent<EntityTargetDetectedTelegraph>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
            animator.applyRootMotion = false;

        _idleHash = Animator.StringToHash(idleStateName);
        _runHash = Animator.StringToHash(runStateName);
    }

    void LateUpdate()
    {
        if (animator == null || agent == null)
            return;

        if (!CanDriveLocomotion())
        {
            _lastPlayedHash = int.MinValue;
            return;
        }

        if (attackController != null
            && (attackController.IsBusy || attackController.IsHoldingAttackReadyStance))
        {
            _lastPlayedHash = int.MinValue;
            return;
        }

        if (targetDetectedTelegraph != null && targetDetectedTelegraph.IsActive)
        {
            _lastPlayedHash = int.MinValue;
            return;
        }

        int targetHash = IsAgentMoving() ? _runHash : _idleHash;
        CrossFadeIfNeeded(targetHash);
    }

    bool CanDriveLocomotion()
    {
        if (stateMachine == null || stateMachine.CurrentStateData == null)
            return true;

        string stateId = stateMachine.CurrentStateData.stateId;
        if (stateId == TakeDamageFsmStateId)
            return false;

        return stateId == locomotionFsmStateId;
    }

    bool IsAgentMoving()
    {
        if (!agent.isOnNavMesh || !agent.enabled || agent.isStopped)
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
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.shortNameHash == stateHash && !animator.IsInTransition(0))
            return;

        if (stateHash == _lastPlayedHash && animator.IsInTransition(0))
            return;

        animator.CrossFadeInFixedTime(stateHash, crossFadeSeconds, 0, 0f);
        _lastPlayedHash = stateHash;
    }
}
