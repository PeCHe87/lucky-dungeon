using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Building door that opens (rotates) when a valid entity is near.
/// Place on the non-rotating door root; only the art/hinge child rotates so proximity stays fixed.
/// When <see cref="requiredItemId"/> is set, the door stays closed until the player interacts with
/// <c>btnInteract</c> while holding the required item (which is then consumed).
/// </summary>
public sealed class BuildingDoor : MonoBehaviour
{
    const string DefaultArtChildName = "art";
    const float AnimSettledEpsilon = 0.02f;

    static int s_playerLayer = -1;
    static int s_entityLayer = -1;
    static readonly Collider[] s_overlap = new Collider[16];

    static int s_unlockableProximityCount;
    static BuildingDoor s_currentUnlockable;

    [Header("Access")]
    [Tooltip("Inventory item id required to unlock. Empty = unlocked (opens on proximity).")]
    [SerializeField] string requiredItemId;

    [Header("Lock UI")]
    [Tooltip("World-space lock UI root. Shown only when Required Item Id is set.")]
    [SerializeField] GameObject lockUiRoot;
    [Tooltip("Frame/background image tinted by required item rarity.")]
    [SerializeField] Image requiredItemFrame;
    [Tooltip("Image that displays the required item's preview sprite.")]
    [SerializeField] Image requiredItemIcon;
    [Tooltip("Check mark shown when the player already owns the required item.")]
    [SerializeField] GameObject hasItemCheck;
    [Tooltip("Indexed by ItemRarity: Common, Uncommon, Rare, Epic, Legendary, Mythic.")]
    [SerializeField] Sprite[] rarityFrames;

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
    bool _reportedUnlockInteract;

    public string RequiredItemId => requiredItemId;
    public bool IsLocked => !string.IsNullOrEmpty(requiredItemId);
    public Vector3 OpenAngleDegrees => openAngleDegrees;
    public bool IsOpen => _openBlend > 1f - AnimSettledEpsilon;
    public bool IsAnimating => _openBlend > AnimSettledEpsilon && _openBlend < 1f - AnimSettledEpsilon;

    public static BuildingDoor CurrentUnlockable => s_currentUnlockable;

    void Awake()
    {
        ResolveHinge();
        _closedLocalRotation = hinge.localRotation;
    }

    void OnEnable()
    {
        GameEvents.PlayerRunInventoryChanged += OnInventoryChanged;
        GameEvents.DungeonRunStarted += OnDungeonRunStarted;
        RefreshLockUi();
    }

    void Start()
    {
        // Catalog / run host may finish Awake after OnEnable; refresh once more.
        RefreshLockUi();
    }

    void OnDisable()
    {
        GameEvents.PlayerRunInventoryChanged -= OnInventoryChanged;
        GameEvents.DungeonRunStarted -= OnDungeonRunStarted;
        UpdateUnlockInteract(false);
    }

    void OnDungeonRunStarted(Dungeon _)
    {
        RefreshLockUi();
        RefreshUnlockInteract();
    }

    void OnInventoryChanged()
    {
        RefreshLockUi();
        RefreshUnlockInteract();
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

        RefreshUnlockInteract();

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
        // Locked doors stay closed until unlock interact consumes the required item.
        if (IsLocked)
        {
            _wantOpen = false;
            return;
        }

        bool playerNear = IsValidActorNear(requirePlayerLayer: true);
        bool entityNear = IsValidActorNear(requirePlayerLayer: false);

        // Unlocked: player or any living entity opens the door.
        if (playerNear || entityNear)
            _wantOpen = true;
        else if (closeWhenPlayerLeaves)
            _wantOpen = false;
    }

    void RefreshUnlockInteract()
    {
        bool canUnlock = isActiveAndEnabled
            && IsLocked
            && IsValidActorNear(requirePlayerLayer: true)
            && PlayerHasRequiredItem();
        UpdateUnlockInteract(canUnlock);
    }

    void UpdateUnlockInteract(bool canUnlock)
    {
        if (_reportedUnlockInteract == canUnlock)
        {
            if (canUnlock)
                s_currentUnlockable = this;
            return;
        }

        if (_reportedUnlockInteract)
        {
            if (s_unlockableProximityCount > 0)
                s_unlockableProximityCount--;

            if (s_currentUnlockable == this)
                s_currentUnlockable = null;
        }

        if (canUnlock)
        {
            s_unlockableProximityCount++;
            s_currentUnlockable = this;
        }

        _reportedUnlockInteract = canUnlock;
        PlayerInteractButton.SetDoorUnlockAvailable(s_unlockableProximityCount > 0);
    }

    /// <summary>
    /// Consumes the required inventory item, clears the lock, and opens the door.
    /// </summary>
    public bool TryUnlockWithRequiredItem()
    {
        if (!IsLocked || !isActiveAndEnabled)
            return false;

        if (!IsValidActorNear(requirePlayerLayer: true))
            return false;

        if (!PlayerHasRequiredItem())
            return false;

        PlayerRunState run = DungeonRunHost.Instance != null ? DungeonRunHost.Instance.Player : null;
        if (run == null)
            return false;

        string consumedId = requiredItemId;
        if (!run.Inventory.TryRemove(consumedId, 1))
            return false;

        requiredItemId = string.Empty;
        UpdateUnlockInteract(false);
        RefreshLockUi();
        _wantOpen = true;
        return true;
    }

    void RefreshLockUi()
    {
        if (!IsLocked)
        {
            if (lockUiRoot != null)
                lockUiRoot.SetActive(false);
            return;
        }

        if (lockUiRoot != null)
            lockUiRoot.SetActive(true);

        ItemData data = null;
        if (ItemCatalog.Current != null)
            ItemCatalog.Current.TryGetById(requiredItemId, out data);

        if (requiredItemIcon != null)
        {
            Sprite preview = data != null ? data.PreviewImage : null;
            requiredItemIcon.sprite = preview;
            requiredItemIcon.enabled = preview != null;
        }

        ApplyRarityFrame(data != null ? data.Rarity : ItemRarity.Common);

        if (hasItemCheck != null)
            hasItemCheck.SetActive(PlayerHasRequiredItem());
    }

    void ApplyRarityFrame(ItemRarity rarity)
    {
        if (requiredItemFrame == null)
            return;

        Sprite frame = GetRarityFrame(rarity) ?? GetRarityFrame(ItemRarity.Common);
        requiredItemFrame.sprite = frame;
        requiredItemFrame.enabled = frame != null;
    }

    Sprite GetRarityFrame(ItemRarity rarity)
    {
        int index = (int)rarity;
        if (rarityFrames == null || index < 0 || index >= rarityFrames.Length)
            return null;
        return rarityFrames[index];
    }

    bool PlayerHasRequiredItem()
    {
        if (!IsLocked)
            return false;

        PlayerRunState run = DungeonRunHost.Instance != null ? DungeonRunHost.Instance.Player : null;
        if (run == null)
            return false;

        return run.Inventory.GetAmount(requiredItemId) > 0;
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
