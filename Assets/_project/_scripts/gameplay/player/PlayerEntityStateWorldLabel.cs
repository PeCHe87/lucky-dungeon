using TMPro;
using UnityEngine;

/// <summary>
/// World-space label above the player showing <see cref="PlayerEntityState.Current"/>.
/// Subscribes to <see cref="PlayerEntityState.StateChanged"/>; builds UI at runtime if unset.
/// </summary>
[DefaultExecutionOrder(110)]
public sealed class PlayerEntityStateWorldLabel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PlayerEntityState playerEntityState;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Transform labelRoot;

    [Header("Layout")]
    [SerializeField] Vector3 localOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField, Min(1f)] float fontSize = 36f;
    [SerializeField] Color textColor = Color.white;
    [SerializeField, Min(0.001f)] float canvasWorldScale = 0.01f;

    [Header("Visibility")]
    [SerializeField] bool showLabel = true;

    void Awake()
    {
        if (playerEntityState == null)
            playerEntityState = GetComponent<PlayerEntityState>();

        if (label == null)
            EnsureLabelHierarchy();
    }

    void OnEnable()
    {
        if (playerEntityState != null)
            playerEntityState.StateChanged += OnStateChanged;
        ApplyVisibility();
        RefreshLabel();
    }

    void OnDisable()
    {
        if (playerEntityState != null)
            playerEntityState.StateChanged -= OnStateChanged;
    }

    void Start()
    {
        RefreshLabel();
    }

    void OnValidate()
    {
        ApplyVisibility();
    }

    void OnStateChanged(PlayerEntityStateKind previous, PlayerEntityStateKind current)
    {
        RefreshLabel(current);
    }

    void RefreshLabel()
    {
        if (playerEntityState == null)
            return;
        RefreshLabel(playerEntityState.Current);
    }

    void RefreshLabel(PlayerEntityStateKind state)
    {
        if (label != null)
            label.text = state.ToString().ToUpperInvariant();
    }

    void ApplyVisibility()
    {
        if (labelRoot != null)
            labelRoot.gameObject.SetActive(showLabel);
    }

    void EnsureLabelHierarchy()
    {
        var anchorGo = new GameObject("StateLabel");
        anchorGo.transform.SetParent(transform, false);
        anchorGo.transform.localPosition = localOffset;
        anchorGo.transform.localRotation = Quaternion.identity;
        anchorGo.transform.localScale = Vector3.one;
        labelRoot = anchorGo.transform;

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(anchorGo.transform, false);
        canvasGo.transform.localPosition = Vector3.zero;
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * canvasWorldScale;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200f, 60f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(canvasGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(200f, 60f);

        label = textGo.AddComponent<TextMeshProUGUI>();
        label.text = "Idle";
        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        canvasGo.AddComponent<BillboardFacingCamera>();
    }
}
