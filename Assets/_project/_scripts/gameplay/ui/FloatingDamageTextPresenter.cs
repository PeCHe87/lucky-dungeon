using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pooled screen-space damage numbers anchored to world positions. Singleton scene service.
/// </summary>
[DisallowMultipleComponent]
public sealed class FloatingDamageTextPresenter : MonoBehaviour
{
    public static FloatingDamageTextPresenter Instance { get; private set; }

    [System.Serializable]
    struct DamageElementColorPair
    {
        public DamageElement element;
        public Color color;
    }

    [Header("References")]
    [SerializeField] Canvas canvas;
    [Tooltip("Used for WorldToScreenPoint. Falls back to Camera.main.")]
    [SerializeField] Camera worldCamera;
    [SerializeField] FloatingDamageTextInstance prefab;
    [Tooltip("Parent for pooled instances. Defaults to this transform.")]
    [SerializeField] Transform poolParent;

    [Header("Pool")]
    [SerializeField, Min(0)] int initialPoolSize = 16;
    [SerializeField, Min(1)] int maxPoolSize = 64;

    [Header("Layout & motion")]
    [SerializeField, Min(1f)] float baseFontSize = 42f;
    [SerializeField, Min(0.01f)] float lifetimeSeconds = 0.85f;
    [SerializeField] Vector2 driftPixelsPerSecond = new Vector2(0f, 120f);
    [SerializeField, Min(0f)] float horizontalRandomSpreadPixels = 28f;
    [SerializeField] float verticalScreenOffsetPixels = 24f;
    [Tooltip("Added to spawn world Y before projecting to screen (all damage numbers).")]
    [SerializeField] float additionalWorldVerticalOffset;
    [Tooltip("Uniform random disk on world XZ around the pivot before projecting (0 = no spread).")]
    [SerializeField, Min(0f)] float worldSpawnRandomRadius = 0.15f;
    [SerializeField] int roundDamageToInt = 1;

    [Header("Critical (multiplies element color)")]
    [SerializeField] Color criticalTint = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField, Min(0.01f)] float criticalFontScale = 1.35f;

    [Header("Element colors")]
    [SerializeField] Color fallbackElementColor = Color.white;
    [SerializeField] DamageElementColorPair[] elementColors =
    {
        new DamageElementColorPair { element = DamageElement.Physical, color = new Color(0.95f, 0.95f, 0.95f) },
        new DamageElementColorPair { element = DamageElement.Fire, color = new Color(1f, 0.45f, 0.2f) },
        new DamageElementColorPair { element = DamageElement.Frost, color = new Color(0.55f, 0.85f, 1f) },
        new DamageElementColorPair { element = DamageElement.Lightning, color = new Color(1f, 1f, 0.45f) },
        new DamageElementColorPair { element = DamageElement.Poison, color = new Color(0.55f, 1f, 0.35f) },
    };

    readonly Stack<FloatingDamageTextInstance> _available = new Stack<FloatingDamageTextInstance>();
    readonly List<FloatingDamageTextInstance> _all = new List<FloatingDamageTextInstance>();
    readonly Dictionary<DamageElement, Color> _colorByElement = new Dictionary<DamageElement, Color>();
    bool _warnedPoolExhausted;
    Transform _poolRoot;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        BuildColorLookup();
        EnsurePoolRoot();
        Prewarm();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void BuildColorLookup()
    {
        _colorByElement.Clear();
        if (elementColors != null)
        {
            for (int i = 0; i < elementColors.Length; i++)
                _colorByElement[elementColors[i].element] = elementColors[i].color;
        }
    }

    void EnsurePoolRoot()
    {
        if (poolParent != null)
        {
            _poolRoot = poolParent;
            return;
        }

        if (canvas != null)
        {
            var go = new GameObject("DamageNumberPool");
            go.transform.SetParent(canvas.transform, false);
            _poolRoot = go.transform;
            return;
        }

        _poolRoot = transform;
    }

    void Prewarm()
    {
        if (prefab == null || canvas == null)
            return;

        int count = Mathf.Clamp(initialPoolSize, 0, maxPoolSize);
        for (int i = 0; i < count; i++)
        {
            FloatingDamageTextInstance inst = Instantiate(prefab, _poolRoot);
            inst.gameObject.SetActive(false);
            _all.Add(inst);
            _available.Push(inst);
        }
    }

    Color ResolveColor(in DamageNumberStyle style)
    {
        Color baseColor = _colorByElement.TryGetValue(style.Element, out Color c) ? c : fallbackElementColor;
        if (style.IsCritical)
            baseColor *= criticalTint;
        baseColor.a = 1f;
        return baseColor;
    }

    /// <summary>Spawns with physical, non-critical styling.</summary>
    public void Spawn(Vector3 worldPosition, float damageAmount)
    {
        Spawn(worldPosition, damageAmount, new DamageNumberStyle(DamageElement.Physical, false));
    }

    public void Spawn(Vector3 worldPosition, float damageAmount, DamageNumberStyle style)
    {
        if (prefab == null || canvas == null)
            return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
            return;

        Vector3 p = worldPosition;
        p.y += additionalWorldVerticalOffset;
        if (worldSpawnRandomRadius > 0f)
        {
            Vector2 r = Random.insideUnitCircle * worldSpawnRandomRadius;
            p.x += r.x;
            p.z += r.y;
        }

        Vector3 screen = cam.WorldToScreenPoint(p);
        if (screen.z <= 0f)
            return;

        FloatingDamageTextInstance inst = Rent();
        if (inst == null)
        {
            if (!_warnedPoolExhausted)
            {
                Debug.LogWarning($"{nameof(FloatingDamageTextPresenter)}: pool exhausted (max {maxPoolSize}).", this);
                _warnedPoolExhausted = true;
            }

            return;
        }

        var canvasRect = canvas.transform as RectTransform;
        Camera uiCamera = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            uiCamera = canvas.worldCamera != null ? canvas.worldCamera : cam;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                new Vector2(screen.x, screen.y),
                uiCamera,
                out local))
        {
            Return(inst);
            return;
        }

        float spread = horizontalRandomSpreadPixels;
        if (spread > 0f)
            local.x += Random.Range(-spread, spread);
        local.y += verticalScreenOffsetPixels;

        string text = FormatDamage(damageAmount);
        float fontSize = baseFontSize * (style.IsCritical ? criticalFontScale : 1f);
        Color color = ResolveColor(style);
        Vector2 drift = driftPixelsPerSecond;

        inst.Play(Return, local, text, color, fontSize, lifetimeSeconds, drift);
    }

    string FormatDamage(float amount)
    {
        if (roundDamageToInt != 0)
            return Mathf.RoundToInt(amount).ToString();
        return amount.ToString("0.#");
    }

    FloatingDamageTextInstance Rent()
    {
        if (_available.Count > 0)
            return _available.Pop();

        if (_all.Count >= maxPoolSize)
            return null;

        FloatingDamageTextInstance inst = Instantiate(prefab, _poolRoot);
        _all.Add(inst);
        inst.gameObject.SetActive(false);
        return inst;
    }

    void Return(FloatingDamageTextInstance inst)
    {
        if (inst == null)
            return;
        inst.gameObject.SetActive(false);
        _available.Push(inst);
    }
}
