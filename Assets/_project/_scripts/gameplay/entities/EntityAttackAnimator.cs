using UnityEngine;

/// <summary>
/// Plays a single melee attack clip for AI entities when <see cref="EntityAttackController"/> fires an attack.
/// </summary>
[DefaultExecutionOrder(114)]
public sealed class EntityAttackAnimator : MonoBehaviour
{
    [SerializeField] EntityAttackController attackController;
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] Animator animator;
    [SerializeField] string attackStateName = "Attack1";
    [SerializeField] string preAttackStateName = "PreAttack1";
    [SerializeField] int attackLayer;
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.08f;
    [Tooltip("Treat attack clip as playing until normalized time reaches this value (0-1).")]
    [SerializeField, Range(0.5f, 1f)] float attackCompletionNormalizedTime = 0.95f;
    [SerializeField] string cancelToStateName = "Idle";

    int _attackStateHash;
    int _preAttackStateHash;
    int _cancelToStateHash;
    MeleeWeapon _meleeWeapon;
    RangedWeapon _rangedWeapon;
    bool _attackClipCancelled;
    bool _preAttackClipCancelled;

    public bool IsAttackClipPlaying
    {
        get
        {
            if (_attackClipCancelled)
                return false;

            if (animator == null || _attackStateHash == 0)
                return false;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(attackLayer);
            if (info.shortNameHash != _attackStateHash)
                return false;

            return info.normalizedTime < attackCompletionNormalizedTime;
        }
    }

    public bool IsPreAttackClipPlaying
    {
        get
        {
            if (_preAttackClipCancelled)
                return false;

            if (animator == null || _preAttackStateHash == 0)
                return false;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(attackLayer);
            if (info.shortNameHash != _preAttackStateHash)
                return false;

            return info.normalizedTime < attackCompletionNormalizedTime;
        }
    }

    public bool IsCombatFacingLocked => IsPreAttackClipPlaying || IsAttackClipPlaying;

    void Awake()
    {
        if (attackController == null)
            attackController = GetComponent<EntityAttackController>();
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
            animator.applyRootMotion = false;

        _attackStateHash = Animator.StringToHash(attackStateName);
        _preAttackStateHash = string.IsNullOrWhiteSpace(preAttackStateName)
            ? 0
            : Animator.StringToHash(preAttackStateName);
        _cancelToStateHash = string.IsNullOrWhiteSpace(cancelToStateName)
            ? 0
            : Animator.StringToHash(cancelToStateName);
        ResolveMeleeWeapon();
        ResolveRangedWeapon();
    }

    void OnEnable()
    {
        if (attackController != null)
            attackController.AttackStarted += OnAttackStarted;
    }

    void OnDisable()
    {
        if (attackController != null)
            attackController.AttackStarted -= OnAttackStarted;
    }

    void OnAttackStarted()
    {
        if (animator == null)
        {
            ApplyImmediateMeleeDamage();
            return;
        }

        PlayAttackClip();
    }

    void ApplyImmediateMeleeDamage()
    {
        ResolveMeleeWeapon();
        if (_meleeWeapon == null)
            return;

        _meleeWeapon.ArmHitForCurrentSwing();
        _meleeWeapon.ApplyPendingDamage();
    }

    public void CancelAttackAnimation()
    {
        _attackClipCancelled = true;

        if (animator == null || _cancelToStateHash == 0)
            return;

        animator.CrossFadeInFixedTime(_cancelToStateHash, crossFadeSeconds, attackLayer, 0f);
    }

    public void PlayPreAttackClip()
    {
        if (animator == null || _preAttackStateHash == 0)
            return;

        _preAttackClipCancelled = false;
        animator.CrossFadeInFixedTime(_preAttackStateHash, crossFadeSeconds, attackLayer, 0f);
    }

    public void CancelPreAttackAnimation()
    {
        _preAttackClipCancelled = true;

        if (animator == null || _cancelToStateHash == 0)
            return;

        animator.CrossFadeInFixedTime(_cancelToStateHash, crossFadeSeconds, attackLayer, 0f);
    }

    void PlayAttackClip()
    {
        if (animator == null || string.IsNullOrWhiteSpace(attackStateName))
            return;

        _attackClipCancelled = false;
        ResolveMeleeWeapon();
        ResolveRangedWeapon();
        animator.CrossFadeInFixedTime(_attackStateHash, crossFadeSeconds, attackLayer, 0f);

        if (weaponHolder != null && weaponHolder.IsRangedEquipped())
            _rangedWeapon?.ArmFireForCurrentShot();
        else
            _meleeWeapon?.ArmHitForCurrentSwing();
    }

    void ResolveMeleeWeapon()
    {
        if (weaponHolder != null && weaponHolder.Current is MeleeWeapon current)
            _meleeWeapon = current;
        else
            _meleeWeapon = GetComponentInChildren<MeleeWeapon>(true);
    }

    void ResolveRangedWeapon()
    {
        if (weaponHolder != null && weaponHolder.Current is RangedWeapon current)
            _rangedWeapon = current;
        else
            _rangedWeapon = GetComponentInChildren<RangedWeapon>(true);
    }
}
