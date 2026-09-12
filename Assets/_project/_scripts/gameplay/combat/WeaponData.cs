using UnityEngine;

/// <summary>Shared balance/config for any weapon; subclass for melee or assault/ranged specifics.</summary>
public abstract class WeaponData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id for lookups (e.g. crates/rewards). Melee: m_<name>_<nnn> (m_sword_000). Ranged: r_<name>_<nnn> (r_bow_004).")]
    [SerializeField] string weaponId;
    [Tooltip("Player-facing name shown in UI.")]
    [SerializeField] string displayName;
    [SerializeField] WeaponType weaponType = WeaponType.None;

    [Header("Combat")]
    [SerializeField] float damage = 10f;
    [SerializeField] float cooldown = 0.35f;
    [Tooltip("How long IAttackActivity.IsAttackActive stays true after a successful attack.")]
    [SerializeField, Min(0.01f)] float attackActiveDuration = 0.15f;
    [Tooltip("Layers this weapon can damage.")]
    [SerializeField] LayerMask hitLayers = ~0;

    [Header("Damage presentation")]
    [SerializeField] DamageElement damageElement = DamageElement.Physical;
    [SerializeField, Range(0f, 1f)] float criticalStrikeChance;

    [Header("Pushback")]
    [Tooltip("Horizontal travel applied to victims along attacker forward on hit. 0 = none.")]
    [SerializeField, Min(0f)] float pushbackDistance = 0.8f;
    [SerializeField, Min(0.01f)] float pushbackDuration = 0.1f;

    [Header("Animation")]
    [SerializeField] RuntimeAnimatorController animatorController;
    [SerializeField] PlayerEntityStateAnimationProfile animationProfile;

    [Header("Target detection")]
    [Tooltip("Primary XZ detection radius pushed to NearestTargetQuery when this weapon is equipped.")]
    [SerializeField, Min(0.01f)] float targetDetectionRadius = 6f;
    [Tooltip("360° fallback detection radius pushed to NearestTargetQuery when this weapon is equipped.")]
    [SerializeField, Min(0.01f)] float omnidirectionalDetectionRadius = 8f;

    [Header("Equipped visual")]
    [Tooltip("Optional mesh prefabs spawned under hand bones when this weapon is equipped. Empty = no world model.")]
    [SerializeField] WeaponVisualSocket[] visualSockets;

    public string WeaponId => weaponId;
    public string DisplayName => displayName;
    public WeaponType WeaponType => weaponType;
    public float Damage => damage;
    public float Cooldown => cooldown;
    public float AttackActiveDuration => attackActiveDuration;
    public LayerMask HitLayers => hitLayers;
    public DamageElement DamageElement => damageElement;
    public float CriticalStrikeChance => criticalStrikeChance;
    public float PushbackDistance => pushbackDistance;
    public float PushbackDuration => pushbackDuration;
    public RuntimeAnimatorController AnimatorController => animatorController;
    public PlayerEntityStateAnimationProfile AnimationProfile => animationProfile;
    public float TargetDetectionRadius => targetDetectionRadius;
    public float OmnidirectionalDetectionRadius => omnidirectionalDetectionRadius;
    public WeaponVisualSocket[] VisualSockets => visualSockets;
}
