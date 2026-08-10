using UnityEngine;

/// <summary>Melee-specific balance/config for <see cref="MeleeWeapon"/>.</summary>
[CreateAssetMenu(menuName = "Combat/Melee Weapon Data", fileName = "MeleeWeaponData")]
public sealed class MeleeWeaponData : WeaponData
{
    [Header("Melee")]
    [SerializeField] float range = 2.5f;
    [Tooltip("Moves the attacker forward in facing direction when the attack is processed.")]
    [SerializeField, Min(0f)] float moveForwardDistance;
    [Tooltip("Duration in seconds of the forward lunge. Distance / duration = lunge speed.")]
    [SerializeField, Min(0.01f)] float moveForwardDuration = 0.15f;
    [Tooltip("Total angle in degrees in the horizontal plane around facing. 360 = no cone limit.")]
    [SerializeField, Range(1f, 360f)] float coneAngle = 120f;
    [SerializeField, Min(1)] int maxTargetsPerSwing = 8;
    [SerializeField] QueryTriggerInteraction overlapQueryTriggerInteraction = QueryTriggerInteraction.Ignore;
    [Tooltip("Min fraction of push travel that must be unobstructed; lower values allow partial wall clearance.")]
    [SerializeField, Range(0f, 1f)] float minPushTravelFraction = PushbackGeometryProbe.DefaultMinPushTravelFraction;

    [Header("Approach lunge")]
    [Tooltip("Lunge toward the detected target before the attack clip when outside melee range.")]
    [SerializeField] bool enableApproachLunge = true;
    [Tooltip("Stops the approach this far inside damage range (horizontal distance from target).")]
    [SerializeField, Min(0f)] float approachStopBuffer = 0.3f;
    [Tooltip("Max horizontal travel per approach lunge.")]
    [SerializeField, Min(0.01f)] float maxApproachLungeDistance = 6f;
    [SerializeField, Min(0.01f)] float approachLungeSpeed = 12f;

    public float Range => range;
    public float MoveForwardDistance => moveForwardDistance;
    public float MoveForwardDuration => moveForwardDuration;
    public float ConeAngle => coneAngle;
    public int MaxTargetsPerSwing => maxTargetsPerSwing;
    public QueryTriggerInteraction OverlapQueryTriggerInteraction => overlapQueryTriggerInteraction;
    public float MinPushTravelFraction => minPushTravelFraction;
    public bool EnableApproachLunge => enableApproachLunge;
    public float ApproachStopBuffer => approachStopBuffer;
    public float MaxApproachLungeDistance => maxApproachLungeDistance;
    public float ApproachLungeSpeed => approachLungeSpeed;
}
