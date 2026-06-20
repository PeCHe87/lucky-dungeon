using UnityEngine;

/// <summary>
/// Receives <c>OnRangedFireFrame</c> animation events from attack clips on the same GameObject as the <see cref="Animator"/>.
/// </summary>
public sealed class RangedAttackAnimationEventReceiver : MonoBehaviour
{
    [SerializeField] RangedWeapon rangedWeapon;
    [SerializeField] WeaponHolder weaponHolder;

    [Header("Debug")]
    [SerializeField] bool logFireFrameEvents;

    void Awake()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponentInParent<WeaponHolder>();

        ResolveRangedWeapon();
    }

    void ResolveRangedWeapon()
    {
        if (rangedWeapon != null)
            return;

        if (weaponHolder != null && weaponHolder.Current is RangedWeapon current)
            rangedWeapon = current;
    }

    /// <summary>Animation event function name on ranged attack clips.</summary>
    public void OnRangedFireFrame()
    {
        if (logFireFrameEvents)
            Debug.Log($"[RangedFireFrame] Animation event invoked on '{name}'.", this);

        ResolveRangedWeapon();

        if (rangedWeapon == null)
        {
            if (logFireFrameEvents)
                Debug.LogWarning(
                    "[RangedFireFrame] RangedWeapon reference is missing (assign on weapon or use WeaponHolder).",
                    this);
            return;
        }

        if (weaponHolder != null && !weaponHolder.IsRangedEquipped())
        {
            if (logFireFrameEvents)
                Debug.Log("[RangedFireFrame] Skipped: ranged is not the equipped weapon.", this);
            return;
        }

        if (logFireFrameEvents)
            Debug.Log($"[RangedFireFrame] Calling ApplyPendingFire on '{rangedWeapon.name}'.", this);

        rangedWeapon.ApplyPendingFire();
    }
}
