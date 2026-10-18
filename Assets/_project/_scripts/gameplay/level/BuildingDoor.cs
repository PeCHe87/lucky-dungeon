using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Building door that opens (rotates) when a valid entity is near.
/// Place on the non-rotating door root; only the art/hinge child rotates so proximity stays fixed.
/// </summary>
public sealed class BuildingDoor : MonoBehaviour
{
    const string DefaultArtChildName = "art";
    const float AnimSettledEpsilon = 0.02f;

    static int s_playerLayer = -1;
    static int s_entityLayer = -1;
    static readonly Collider[] s_overlap = new Collider[16];

    [Header("Access")]
    [Tooltip("When enabled, only the player can trigger the door, and only with the required key.")]
    [SerializeField] bool requiresSpecialKey;
    [Tooltip("Key id checked on PlayerRunState.Keys when Requires Special Key is enabled.")]
    [SerializeField] string requiredKeyId;

    [Header("Motion")]
    [Tooltip("Art transform that rotates open. Defaults to child named 'art'. This root transform never rotates.")]
    [SerializeField] Transform hinge;
    [Tooltip("Local-space euler offset applied when fully open (relative to the closed pose).")]
    [FormerlySerializedAs("openYawDegrees")]
    [SerializeField] Vector3 openAngleDegrees = new Vector3(0f, 90f, 0f);
    [SerializeField, Min(0.01f)] float openSmoothTime = 0.25f;
    [Tooltip("When enabled, the door closes again after no valid entity remains in proximity.")]
    [SerializeField] bool closeWhenPlayerLeaves = true;

    [Header("Proximity")]
    [SerializeField, Min(0.1f)] float detectRadius = 2.5f;
    [Tooltip("Offset from this (non-rotating) root used for the proximity check.")]
    [SerializeField] Vector3 detectCenterOffset = new Vector3(0f, 1f, 0f);

    Quaternion _closedLocalRotation;
    float _openBlend;
    float _openBlendVelocity;
    bool _wantOpen;
    bool _loggedMissingKey;

    public bool RequiresSpecialKey => requiresSpecialKey;
    public string RequiredKeyId => requiredKeyId;
    public Vector3 OpenAngleDegrees => openAngleDegrees;
    public bool IsOpen => _openBlend > 1f - AnimSettledEpsilon;
    public bool IsAnimating => _openBlend > AnimSettledEpsilon && _openBlend < 1f - AnimSettledEpsilon;

    void Awake()
    {
        ResolveHinge();
        _closedLocalRotation = hinge.localRotation;
    }

    void ResolveHinge()
    {
        if (hinge != null && hinge != transform)
            return;

        Transform art = transform.Find(DefaultArtChildName);
        if (art != null)
        {
            hinge = art;
            return;
        }

        if (hinge == null)
            hinge = transform;
    }

    void Update()
    {
        // Mid open/close: keep current intent; ignore sensor until settled.
        if (!IsAnimating)
            EvaluateSensor();

        float target = _wantOpen ? 1f : 0f;
        _openBlend = Mathf.SmoothDamp(
            _openBlend,
            target,
            ref _openBlendVelocity,
            Mathf.Max(0.0001f, openSmoothTime));

        Quaternion openLocalRotation = _closedLocalRotation * Quaternion.Euler(openAngleDegrees);
        hinge.localRotation = Quaternion.Slerp(_closedLocalRotation, openLocalRotation, _openBlend);
    }

    void EvaluateSensor()
    {
        bool playerNear = IsValidActorNear(requirePlayerLayer: true);
        bool entityNear = !requiresSpecialKey && IsValidActorNear(requirePlayerLayer: false);

        if (requiresSpecialKey)
        {
            if (playerNear && PlayerHasRequiredKey())
            {
                _wantOpen = true;
                _loggedMissingKey = false;
            }
            else if (playerNear)
            {
                _wantOpen = false;
                if (!_loggedMissingKey)
                {
                    _loggedMissingKey = true;
                    Debug.Log(
                        $"[{nameof(BuildingDoor)}] '{name}' requires key '{requiredKeyId}'.",
                        this);
                }
            }
            else if (closeWhenPlayerLeaves)
            {
                _wantOpen = false;
            }

            return;
        }

        // Unlocked: player or any living entity opens the door.
        if (playerNear || entityNear)
        {
            _wantOpen = true;
            _loggedMissingKey = false;
        }
        else if (closeWhenPlayerLeaves)
        {
            _wantOpen = false;
        }
    }

    bool PlayerHasRequiredKey()
    {
        PlayerRunState run = DungeonRunHost.Instance != null ? DungeonRunHost.Instance.Player : null;
        if (run == null)
            return false;

        return run.Keys.HasKey(requiredKeyId);
    }

    /// <summary>
    /// When <paramref name="requirePlayerLayer"/> is true, only Player-layer actors count.
    /// When false, only Entity-layer actors count (non-player).
    /// </summary>
    bool IsValidActorNear(bool requirePlayerLayer)
    {
        EnsureLayersCached();

        int layer = requirePlayerLayer ? s_playerLayer : s_entityLayer;
        if (layer < 0)
            return false;

        Vector3 center = transform.TransformPoint(detectCenterOffset);
        int mask = 1 << layer;
        int count = Physics.OverlapSphereNonAlloc(
            center,
            detectRadius,
            s_overlap,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = s_overlap[i];
            if (col == null)
                continue;

            var health = col.GetComponentInParent<CombatEntityHealth>();
            if (health != null && !health.IsDefeated)
                return true;
        }

        return false;
    }

    static void EnsureLayersCached()
    {
        if (s_playerLayer < 0)
            s_playerLayer = LayerMask.NameToLayer("Player");
        if (s_entityLayer < 0)
            s_entityLayer = LayerMask.NameToLayer("Entity");
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Vector3 center = transform.TransformPoint(detectCenterOffset);
        Gizmos.DrawWireSphere(center, detectRadius);
    }
#endif
}
