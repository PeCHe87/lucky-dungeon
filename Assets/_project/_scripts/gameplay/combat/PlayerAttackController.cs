using System;
using UnityEngine;

/// <summary>Polls <see cref="IAttackIntentProvider"/> and forwards to <see cref="WeaponHolder"/>.</summary>
[DefaultExecutionOrder(101)]
public sealed class PlayerAttackController : MonoBehaviour
{
    public event Action AttackPressed;
    public event Action AttackPerformed;
    [Tooltip("Implements IAttackIntentProvider. If unset, uses first IAttackIntentProvider on this GameObject.")]
    [SerializeField] MonoBehaviour attackIntentProvider;
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] NearestTargetQuery targetQuery;
    [Tooltip("If unset, uses this transform for attack facing (XZ forward).")]
    [SerializeField] Transform facingRoot;
    [Tooltip("When enabled, horizontal yaw snaps toward the nearest target from NearestTargetQuery before TryAttack. When disabled, facing is unchanged.")]
    [SerializeField] bool snapFacingToNearestTargetBeforeAttack = true;
    [Tooltip("If true, logs the weapon used each time an attack is processed (i.e. TryAttack succeeded).")]
    [SerializeField] bool logProcessedAttack;
    [Tooltip("If unset, uses PlayerEntityState on this GameObject or in the scene.")]
    [SerializeField] PlayerEntityState playerEntityState;

    IAttackIntentProvider _attackProvider;
    TopDownCharacterMovement _movement;

    void Awake()
    {
        if (attackIntentProvider != null)
            _attackProvider = attackIntentProvider as IAttackIntentProvider;
        if (_attackProvider == null)
            _attackProvider = GetComponent<IAttackIntentProvider>();

        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();

        _movement = GetComponent<TopDownCharacterMovement>();

        if (playerEntityState == null)
            playerEntityState = GetComponent<PlayerEntityState>();
        if (playerEntityState == null)
            playerEntityState = FindFirstObjectByType<PlayerEntityState>();
    }

    void LateUpdate()
    {
        if (_attackProvider == null || weaponHolder == null)
            return;
        if (!_attackProvider.WasAttackPressedThisFrame())
            return;
        if (playerEntityState != null && playerEntityState.IsAttackInputBlocked)
            return;

        AttackPressed?.Invoke();

        Transform attacker = transform;
        Transform face = facingRoot != null ? facingRoot : transform;

        Transform optionalTarget = null;
        if (targetQuery != null && targetQuery.TryGetNearestTransform(out Transform t))
            optionalTarget = t;

        if (snapFacingToNearestTargetBeforeAttack
            && optionalTarget != null)
        {
            if (_movement != null)
                _movement.SnapHorizontalFacingTowardWorldPosition(optionalTarget.position);
            else
            {
                Vector3 dir = optionalTarget.position - face.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-8f)
                    face.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        Vector3 f = face.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-8f)
            f = Vector3.forward;
        else
            f.Normalize();

        var ctx = new AttackContext
        {
            attacker = attacker,
            facing = f,
            optionalTarget = optionalTarget
        };
        bool performed = weaponHolder.TryAttack(in ctx);
        if (performed)
            AttackPerformed?.Invoke();

        if (performed && logProcessedAttack)
        {
            IWeapon w = weaponHolder.Current;
            MonoBehaviour wmb = w as MonoBehaviour;
            string label = wmb != null
                ? $"'{wmb.name}' ({wmb.GetType().Name})"
                : (w != null ? $"({w.GetType().Name})" : "<none>");
            Debug.Log($"{nameof(PlayerAttackController)} on {name}: attack processed with <color=yellow>{label}</color>", this);
        }
    }
}
