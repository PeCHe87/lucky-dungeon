using System.Collections;
using UnityEngine;

/// <summary>
/// When the player stops moving with a ranged weapon equipped, snaps facing toward the nearest detected target
/// after a configurable delay. Skips while input is blocked; cancels if Idle is left before the delay elapses.
/// </summary>
public sealed class PlayerRangedStopFaceTarget : MonoBehaviour
{
    [SerializeField] PlayerEntityState entityState;
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] NearestTargetQuery targetQuery;
    [SerializeField] TopDownCharacterMovement movement;

    [Tooltip("Seconds to wait after stopping before facing the nearest target. 0 = immediate.")]
    [SerializeField, Min(0f)] float faceDelaySeconds = 0.15f;

    Coroutine _pendingFace;

    void Reset()
    {
        entityState = GetComponent<PlayerEntityState>();
        weaponHolder = GetComponent<WeaponHolder>();
        targetQuery = GetComponent<NearestTargetQuery>();
        movement = GetComponent<TopDownCharacterMovement>();
    }

    void Awake()
    {
        if (entityState == null)
            entityState = GetComponent<PlayerEntityState>();
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        if (targetQuery == null)
            targetQuery = GetComponent<NearestTargetQuery>();
        if (movement == null)
            movement = GetComponent<TopDownCharacterMovement>();
    }

    void OnEnable()
    {
        if (entityState != null)
            entityState.StateChanged += OnStateChanged;
    }

    void OnDisable()
    {
        if (entityState != null)
            entityState.StateChanged -= OnStateChanged;
        CancelPendingFace();
    }

    void OnStateChanged(PlayerEntityStateKind previous, PlayerEntityStateKind current)
    {
        if (current != PlayerEntityStateKind.Idle)
        {
            CancelPendingFace();
            return;
        }

        if (previous != PlayerEntityStateKind.Running && previous != PlayerEntityStateKind.Walking)
            return;
        if (!CanStartFace())
            return;

        CancelPendingFace();
        if (faceDelaySeconds <= 0f)
        {
            TryFaceNearestTarget();
            return;
        }

        _pendingFace = StartCoroutine(FaceAfterDelay());
    }

    IEnumerator FaceAfterDelay()
    {
        yield return new WaitForSeconds(faceDelaySeconds);
        _pendingFace = null;
        TryFaceNearestTarget();
    }

    void TryFaceNearestTarget()
    {
        if (!CanStartFace())
            return;
        if (entityState != null && entityState.Current != PlayerEntityStateKind.Idle)
            return;
        if (!targetQuery.TryGetNearestTransform(out Transform target) || target == null)
            return;
        if (target.GetComponentInParent<CombatEntityHealth>() == null)
            return;

        movement.SnapHorizontalFacingTowardWorldPosition(target.position);
    }

    bool CanStartFace()
    {
        if (entityState == null || entityState.IsInputBlocked)
            return false;
        if (weaponHolder == null || !weaponHolder.IsRangedEquipped())
            return false;
        return movement != null && targetQuery != null;
    }

    void CancelPendingFace()
    {
        if (_pendingFace == null)
            return;
        StopCoroutine(_pendingFace);
        _pendingFace = null;
    }
}
