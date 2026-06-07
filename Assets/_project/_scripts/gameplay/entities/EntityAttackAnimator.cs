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
    [SerializeField] int attackLayer;
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.08f;
    [Tooltip("Treat attack clip as playing until normalized time reaches this value (0-1).")]
    [SerializeField, Range(0.5f, 1f)] float attackCompletionNormalizedTime = 0.95f;

    int _attackStateHash;
    MeleeWeapon _meleeWeapon;

    public bool IsAttackClipPlaying
    {
        get
        {
            if (animator == null || _attackStateHash == 0)
                return false;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(attackLayer);
            if (info.shortNameHash != _attackStateHash)
                return false;

            return info.normalizedTime < attackCompletionNormalizedTime;
        }
    }

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
        ResolveMeleeWeapon();
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
        // Locomotion animator resumes on next LateUpdate when IsBusy clears.
    }

    void PlayAttackClip()
    {
        if (animator == null || string.IsNullOrWhiteSpace(attackStateName))
            return;

        ResolveMeleeWeapon();
        animator.CrossFadeInFixedTime(_attackStateHash, crossFadeSeconds, attackLayer, 0f);
        _meleeWeapon?.ArmHitForCurrentSwing();
    }

    void ResolveMeleeWeapon()
    {
        if (weaponHolder != null && weaponHolder.Current is MeleeWeapon current)
            _meleeWeapon = current;
        else
            _meleeWeapon = GetComponentInChildren<MeleeWeapon>(true);
    }
}
