using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World loot container. Shows searchable/clear while overlapped, channels open on interact,
/// then grants fixed/random rewards into run inventory and marks looted.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class InteractableLoot : MonoBehaviour
{
    [Serializable]
    public struct FixedLootEntry
    {
        public ItemData item;
        [Min(1)] public int amount;
    }

    [Serializable]
    public struct RandomLootEntry
    {
        public ItemData item;
        [Min(0)] public int minAmount;
        [Min(0)] public int maxAmount;
        [Range(0f, 1f)] public float chance;
    }

    static int s_playerLayer = -1;
    static int s_searchableProximityCount;
    static InteractableLoot s_currentSearchable;

    [Header("Prompt")]
    [SerializeField] GameObject promptRoot;
    [SerializeField] GameObject searchableRoot;
    [SerializeField] GameObject clearRoot;

    [Header("Open progress")]
    [SerializeField] GameObject progressRoot;
    [SerializeField] Image progressFill;
    [SerializeField, Min(0.01f)] float openDuration = 2f;

    [Header("Loot")]
    [SerializeField] FixedLootEntry[] fixedLoot;
    [SerializeField] RandomLootEntry[] randomLoot;
    [SerializeField] bool looted;

    int _playerOverlapCount;
    bool _reportedSearchableProximity;
    bool _opening;
    float _openElapsed;

    public bool IsLooted => looted;
    public bool IsOpening => _opening;

    public static bool IsInteractAvailable => PlayerInteractButton.IsVisible;
    public static InteractableLoot CurrentSearchable => s_currentSearchable;

    public void SetLooted(bool value)
    {
        if (value && _opening)
            CancelOpen();

        looted = value;
        RefreshPrompt();
    }

    /// <summary>Starts the open channel if this loot is searchable and the player is in range.</summary>
    public bool TryBeginOpen()
    {
        if (looted || _opening || !isActiveAndEnabled)
            return false;

        if (_playerOverlapCount <= 0)
            return false;

        _opening = true;
        _openElapsed = 0f;
        SetProgressVisible(true);
        SetProgressFill(0f);
        RefreshPrompt();
        return true;
    }

    void Awake()
    {
        ResolveRefs();

        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[{nameof(InteractableLoot)}] Collider on '{name}' should be a trigger.", this);

        SetProgressVisible(false);
        SetProgressFill(0f);
        RefreshPrompt();
    }

    void Update()
    {
        if (!_opening)
            return;

        if (_playerOverlapCount <= 0 || looted || !isActiveAndEnabled)
        {
            CancelOpen();
            return;
        }

        _openElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, openDuration);
        SetProgressFill(Mathf.Clamp01(_openElapsed / duration));

        if (_openElapsed >= duration)
            CompleteOpen();
    }

    void OnDisable()
    {
        if (_opening)
            CancelOpen();

        _playerOverlapCount = 0;
        RefreshPrompt();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        _playerOverlapCount++;
        RefreshPrompt();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (_playerOverlapCount > 0)
            _playerOverlapCount--;

        if (_opening && _playerOverlapCount <= 0)
            CancelOpen();

        RefreshPrompt();
    }

    void ResolveRefs()
    {
        if (promptRoot == null)
        {
            Transform ui = transform.Find("ui");
            if (ui != null)
                promptRoot = ui.gameObject;
        }

        if (promptRoot == null)
            return;

        if (searchableRoot == null)
        {
            Transform searchable = promptRoot.transform.Find("searchable");
            if (searchable != null)
                searchableRoot = searchable.gameObject;
        }

        if (clearRoot == null)
        {
            Transform clear = promptRoot.transform.Find("clear");
            if (clear != null)
                clearRoot = clear.gameObject;
        }

        if (progressRoot == null)
        {
            Transform progress = promptRoot.transform.Find("progress");
            if (progress != null)
                progressRoot = progress.gameObject;
        }

        if (progressFill == null && progressRoot != null)
        {
            Transform fill = progressRoot.transform.Find("fill");
            if (fill != null)
                progressFill = fill.GetComponent<Image>();
            if (progressFill == null)
                progressFill = progressRoot.GetComponentInChildren<Image>(true);
        }
    }

    void RefreshPrompt()
    {
        bool inRange = _playerOverlapCount > 0 && isActiveAndEnabled;
        bool showPrompt = inRange;

        if (promptRoot != null && promptRoot.activeSelf != showPrompt)
            promptRoot.SetActive(showPrompt);

        if (showPrompt)
        {
            bool showSearchable = !looted && !_opening;
            if (searchableRoot != null)
                searchableRoot.SetActive(showSearchable);

            if (clearRoot != null)
                clearRoot.SetActive(looted);
        }

        UpdateSearchableProximity(inRange && !looted && !_opening);
    }

    void UpdateSearchableProximity(bool inSearchableProximity)
    {
        if (_reportedSearchableProximity == inSearchableProximity)
        {
            if (inSearchableProximity)
                s_currentSearchable = this;
            return;
        }

        if (_reportedSearchableProximity)
        {
            if (s_searchableProximityCount > 0)
                s_searchableProximityCount--;

            if (s_currentSearchable == this)
                s_currentSearchable = null;
        }

        if (inSearchableProximity)
        {
            s_searchableProximityCount++;
            s_currentSearchable = this;
        }

        _reportedSearchableProximity = inSearchableProximity;

        bool available = s_searchableProximityCount > 0 && (s_currentSearchable == null || !s_currentSearchable._opening);
        PlayerInteractButton.SetLootAvailable(available);
    }

    void CancelOpen()
    {
        if (!_opening)
            return;

        _opening = false;
        _openElapsed = 0f;
        SetProgressVisible(false);
        SetProgressFill(0f);
        RefreshPrompt();
    }

    void CompleteOpen()
    {
        _opening = false;
        _openElapsed = 0f;
        SetProgressVisible(false);
        SetProgressFill(0f);

        List<(string itemId, int amount)> granted = GrantRewards();
        looted = true;
        RefreshPrompt();

        if (granted != null && granted.Count > 0)
            GameEvents.RaiseLootObtained(granted);
    }

    List<(string itemId, int amount)> GrantRewards()
    {
        var granted = new List<(string itemId, int amount)>(8);
        var indexById = new Dictionary<string, int>(8);

        DungeonRunHost host = DungeonRunHost.EnsureExists();
        if (host.Player == null)
        {
            Debug.LogWarning($"[{nameof(InteractableLoot)}] No run player; cannot grant loot on '{name}'.", this);
            return granted;
        }

        PlayerRunInventory inventory = host.Player.Inventory;

        if (fixedLoot != null)
        {
            for (int i = 0; i < fixedLoot.Length; i++)
            {
                FixedLootEntry entry = fixedLoot[i];
                if (entry.item == null || string.IsNullOrEmpty(entry.item.ItemId))
                    continue;

                int amount = Mathf.Max(1, entry.amount);
                inventory.Add(entry.item.ItemId, amount);
                MergeGranted(granted, indexById, entry.item.ItemId, amount);
            }
        }

        if (randomLoot != null)
        {
            for (int i = 0; i < randomLoot.Length; i++)
            {
                RandomLootEntry entry = randomLoot[i];
                if (entry.item == null || string.IsNullOrEmpty(entry.item.ItemId))
                    continue;

                if (UnityEngine.Random.value > Mathf.Clamp01(entry.chance))
                    continue;

                int min = Mathf.Max(0, entry.minAmount);
                int max = Mathf.Max(min, entry.maxAmount);
                int amount = UnityEngine.Random.Range(min, max + 1);
                if (amount <= 0)
                    continue;

                inventory.Add(entry.item.ItemId, amount);
                MergeGranted(granted, indexById, entry.item.ItemId, amount);
            }
        }

        return granted;
    }

    static void MergeGranted(
        List<(string itemId, int amount)> granted,
        Dictionary<string, int> indexById,
        string itemId,
        int amount)
    {
        if (indexById.TryGetValue(itemId, out int index))
        {
            (string id, int existing) = granted[index];
            granted[index] = (id, existing + amount);
            return;
        }

        indexById[itemId] = granted.Count;
        granted.Add((itemId, amount));
    }

    void SetProgressVisible(bool visible)
    {
        if (progressRoot != null && progressRoot.activeSelf != visible)
            progressRoot.SetActive(visible);
    }

    void SetProgressFill(float amount)
    {
        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(amount);
    }

    static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (s_playerLayer < 0)
            s_playerLayer = LayerMask.NameToLayer("Player");

        return s_playerLayer >= 0 && other.gameObject.layer == s_playerLayer;
    }
}
