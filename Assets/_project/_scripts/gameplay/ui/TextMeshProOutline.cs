using TMPro;
using UnityEngine;

/// <summary>
/// Applies inspector-configurable outline color and width to a co-located <see cref="TMP_Text"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class TextMeshProOutline : MonoBehaviour
{
    [SerializeField] bool enableOutline = true;
    [SerializeField] Color outlineColor = Color.black;
    [Tooltip("TMP shader-space outline width (typical range ~0.05–0.3).")]
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.2f;

    TMP_Text _text;

    void Awake() => ResolveAndApply();

    void OnEnable() => Apply();

    void OnValidate() => Apply();

    void ResolveAndApply()
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        Apply();
    }

    void Apply()
    {
        if (_text == null)
            return;

        _text.outlineWidth = enableOutline ? outlineWidth : 0f;
        _text.outlineColor = outlineColor;
    }

    /// <summary>Applies outline settings from an external driver (e.g. <see cref="FloatingHealthBarView"/>).</summary>
    public void Configure(bool enable, Color color, float width)
    {
        enableOutline = enable;
        outlineColor = color;
        outlineWidth = width;
        Apply();
    }
}
