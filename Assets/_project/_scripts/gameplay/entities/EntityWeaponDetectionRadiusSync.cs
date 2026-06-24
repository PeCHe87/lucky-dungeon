using UnityEngine;

/// <summary>
/// Syncs <see cref="FieldOfViewComponent"/> acquisition and loss radii from the equipped weapon's
/// <see cref="IWeaponTargetDetection"/> values (mirrors player <see cref="NearestTargetQuery"/>).
/// </summary>
[DefaultExecutionOrder(-5)]
[RequireComponent(typeof(FieldOfViewComponent))]
public sealed class EntityWeaponDetectionRadiusSync : MonoBehaviour
{
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] FieldOfViewComponent fieldOfView;

    float _defaultAcquireRadius;
    float _defaultLossRadius;
    bool _hadExplicitLossRadius;

    void Awake()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();

        _defaultAcquireRadius = fieldOfView.DetectionRadius;
        _hadExplicitLossRadius = fieldOfView.HasExplicitLossRadius;
        _defaultLossRadius = fieldOfView.LossDetectionRadius;
        ApplyWeaponDetectionRadii();
    }

    void OnEnable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged += ApplyWeaponDetectionRadii;
    }

    void OnDisable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged -= ApplyWeaponDetectionRadii;
    }

    void ApplyWeaponDetectionRadii()
    {
        if (fieldOfView == null)
            return;

        if (weaponHolder != null
            && weaponHolder.Current is MonoBehaviour weaponBehaviour
            && weaponBehaviour is IWeaponTargetDetection detection
            && detection.TargetDetectionRadius > 0f
            && detection.OmnidirectionalDetectionRadius > 0f)
        {
            fieldOfView.SetDetectionRadii(
                detection.TargetDetectionRadius,
                detection.OmnidirectionalDetectionRadius);
            return;
        }

        fieldOfView.SetDetectionRadii(
            _defaultAcquireRadius,
            _hadExplicitLossRadius ? _defaultLossRadius : 0f);
    }
}
