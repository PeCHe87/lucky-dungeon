using UnityEngine;

/// <summary>
/// Receives <c>OnMeleeHitFrame</c> animation events from attack clips on the same GameObject as the <see cref="Animator"/>.
/// </summary>
public sealed class MeleeAttackAnimationEventReceiver : MonoBehaviour
{
    [SerializeField] MeleeWeapon meleeWeapon;
    [SerializeField] WeaponHolder weaponHolder;

    [Header("Debug")]
    [SerializeField] bool logHitFrameEvents;

    void Awake()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponentInParent<WeaponHolder>();

        ResolveMeleeWeapon();
    }

    void ResolveMeleeWeapon()
    {
        if (meleeWeapon != null)
            return;

        if (weaponHolder != null && weaponHolder.Current is MeleeWeapon current)
            meleeWeapon = current;
    }

    /// <summary>Animation event function name on melee attack clips.</summary>
    public void OnMeleeHitFrame()
    {
        if (logHitFrameEvents)
            Debug.Log($"[MeleeHitFrame] Animation event invoked on '{name}'.", this);

        ResolveMeleeWeapon();

        if (meleeWeapon == null)
        {
            if (logHitFrameEvents)
                Debug.LogWarning(
                    "[MeleeHitFrame] MeleeWeapon reference is missing (not on parent chain; assign sword MeleeWeapon or use WeaponHolder).",
                    this);
            return;
        }

        if (weaponHolder != null && !weaponHolder.IsMeleeEquipped())
        {
            if (logHitFrameEvents)
                Debug.Log("[MeleeHitFrame] Skipped: melee is not the equipped weapon.", this);
            return;
        }

        if (logHitFrameEvents)
            Debug.Log($"[MeleeHitFrame] Calling ApplyPendingDamage on '{meleeWeapon.name}'.", this);

        meleeWeapon.ApplyPendingDamage();
    }
}
