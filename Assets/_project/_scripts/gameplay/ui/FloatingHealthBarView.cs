using TMPro;
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

    [Header("Visibility")]
    [Tooltip("When enabled, the bar stays hidden until the first non-lethal hit.")]
    [SerializeField] bool hideUntilDamaged;

    [Header("Health label")]
    [Tooltip("When enabled, shows current HP as text on the bar.")]
    [SerializeField] bool showHealthNumbers;
    [SerializeField] TextMeshProUGUI healthLabel;
    [SerializeField] TMP_FontAsset healthLabelFont;
    [SerializeField, Min(1f)] float healthLabelFontSize = 10f;
    [SerializeField] bool healthLabelBold;
    [SerializeField] Color healthLabelColor = Color.white;
    [Tooltip("Vertical offset in canvas pixels (positive moves the label up).")]
    [SerializeField] float healthLabelVerticalOffset;
    [SerializeField] bool healthLabelOutline;
    [SerializeField] Color healthLabelOutlineColor = Color.black;
    [Tooltip("TMP shader-space outline width (typical range ~0.05–0.3).")]
    [SerializeField, Range(0f, 1f)] float healthLabelOutlineWidth = 0.2f;

    static Sprite _whiteSprite;

    bool _visible = true;
    bool _revealed;

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
        else if (showHealthNumbers)
            EnsureHealthLabel();

        if (hideUntilDamaged)
        {
            _revealed = false;
            _visible = false;
        }

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

    void OnHealthChanged()
    {
        Refresh();

        if (!hideUntilDamaged || _revealed || health == null)
            return;

        if (health.CurrentHitPoints > 0f)
        {
            _revealed = true;
            SetVisible(true);
        }
    }

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
        RefreshHealthLabel();
    }

    void RefreshHealthLabel()
    {
        if (!showHealthNumbers)
        {
            if (healthLabel != null)
                healthLabel.gameObject.SetActive(false);
            return;
        }

        EnsureHealthLabel();

        if (healthLabel == null)
            return;

        healthLabel.gameObject.SetActive(true);
        ApplyHealthLabelStyle();
        healthLabel.text = Mathf.CeilToInt(health.CurrentHitPoints).ToString();
    }

    void ApplyHealthLabelStyle()
    {
        if (healthLabel == null)
            return;

        if (healthLabelFont != null)
            healthLabel.font = healthLabelFont;

        healthLabel.fontSize = healthLabelFontSize;
        healthLabel.fontStyle = healthLabelBold ? FontStyles.Bold : FontStyles.Normal;
        healthLabel.color = healthLabelColor;

        RectTransform labelRect = healthLabel.rectTransform;
        labelRect.offsetMin = new Vector2(labelRect.offsetMin.x, healthLabelVerticalOffset);
        labelRect.offsetMax = new Vector2(labelRect.offsetMax.x, healthLabelVerticalOffset);
        ApplyHealthLabelOutline();
    }

    void ApplyHealthLabelOutline()
    {
        if (healthLabel == null)
            return;

        TextMeshProOutline outline = healthLabel.GetComponent<TextMeshProOutline>();
        if (!healthLabelOutline)
        {
            if (outline != null)
                outline.Configure(false, healthLabelOutlineColor, 0f);
            return;
        }

        if (outline == null)
            outline = healthLabel.gameObject.AddComponent<TextMeshProOutline>();

        outline.Configure(true, healthLabelOutlineColor, healthLabelOutlineWidth);
    }

    void EnsureHealthLabel()
    {
        if (!showHealthNumbers || healthLabel != null)
            return;

        if (barRoot != null)
        {
            Transform existing = barRoot.Find("Canvas/HealthLabel");
            if (existing != null)
            {
                healthLabel = existing.GetComponent<TextMeshProUGUI>();
                if (healthLabel != null)
                    return;
            }
        }

        Transform canvasTransform = fillImage != null
            ? fillImage.transform.parent?.parent
            : barRoot != null ? barRoot.Find("Canvas") : null;

        if (canvasTransform == null)
            return;

        healthLabel = CreateHealthLabel(canvasTransform);
    }

    TextMeshProUGUI CreateHealthLabel(Transform canvasTransform)
    {
        var labelGo = new GameObject("HealthLabel");
        labelGo.transform.SetParent(canvasTransform, false);

        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        healthLabel = label;
        ApplyHealthLabelStyle();
        return label;
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

        if (showHealthNumbers)
            healthLabel = CreateHealthLabel(canvasGo.transform);

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
