using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space health bar bound to a co-located <see cref="CombatEntityHealth"/>.
/// Builds a billboarded canvas hierarchy at runtime when UI refs are unset.
/// Updates fill amount when HP changes.
/// </summary>
[DefaultExecutionOrder(110)]
public sealed class FloatingHealthBarView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CombatEntityHealth health;
    [SerializeField] Image fillImage;
    [SerializeField] Image backgroundImage;
    [SerializeField] Transform barRoot;

    [Header("Layout")]
    [SerializeField] Vector3 localOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] Vector2 barSize = new Vector2(120f, 14f);
    [SerializeField, Min(0.001f)] float canvasWorldScale = 0.01f;
    [SerializeField] Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [SerializeField] Color fillColor = new Color(0.25f, 0.85f, 0.3f, 1f);

    static Sprite _whiteSprite;

    bool _visible = true;

    public void SetVisible(bool visible)
    {
        _visible = visible;
        ApplyVisibility();
    }

    void Awake()
    {
        if (health == null)
            health = GetComponent<CombatEntityHealth>();

        if (fillImage == null)
            EnsureBarHierarchy();

        ApplyVisibility();
        Refresh();
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.HealthChanged += OnHealthChanged;
            health.Died += OnDied;
        }

        ApplyVisibility();
        Refresh();
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
            health.Died -= OnDied;
        }
    }

    void Start()
    {
        Refresh();
    }

    void OnHealthChanged() => Refresh();

    void OnDied()
    {
        Refresh();
        SetVisible(false);
    }

    void ApplyVisibility()
    {
        if (barRoot != null)
            barRoot.gameObject.SetActive(_visible);
    }

    void Refresh()
    {
        if (health == null || fillImage == null)
            return;

        float ratio = health.MaxHitPoints > 0f
            ? health.CurrentHitPoints / health.MaxHitPoints
            : 0f;
        fillImage.fillAmount = Mathf.Clamp01(ratio);
    }

    void EnsureBarHierarchy()
    {
        Sprite sprite = GetWhiteSprite();

        var anchorGo = new GameObject("HealthBar");
        anchorGo.transform.SetParent(transform, false);
        anchorGo.transform.localPosition = localOffset;
        anchorGo.transform.localRotation = Quaternion.identity;
        anchorGo.transform.localScale = Vector3.one;
        barRoot = anchorGo.transform;

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(anchorGo.transform, false);
        canvasGo.transform.localPosition = Vector3.zero;
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * canvasWorldScale;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = barSize;

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        backgroundImage = bgGo.AddComponent<Image>();
        backgroundImage.sprite = sprite;
        backgroundImage.color = backgroundColor;
        backgroundImage.raycastTarget = false;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillImage = fillGo.AddComponent<Image>();
        fillImage.sprite = sprite;
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
        fillImage.raycastTarget = false;

        canvasGo.AddComponent<BillboardFacingCamera>();
    }

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;

        _whiteSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f);
        return _whiteSprite;
    }
}
