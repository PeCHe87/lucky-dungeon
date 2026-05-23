using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Sibling to Joystick Pack on the same UI object; forwards pointer up/down for double-tap dash detection.
/// </summary>
[DisallowMultipleComponent]
public sealed class JoystickDoubleTapBridge : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    static readonly FieldInfo BackgroundField = typeof(Joystick).GetField(
        "background",
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    Joystick _joystick;
    RectTransform _background;
    Canvas _canvas;
    IJoystickDoubleTapSink _sink;

    void Awake()
    {
        _joystick = GetComponent<Joystick>();
        CacheBackground();
        _canvas = GetComponentInParent<Canvas>();
    }

    public void SetSink(IJoystickDoubleTapSink sink)
    {
        _sink = sink;
    }

    void CacheBackground()
    {
        _background = null;
        if (_joystick == null || BackgroundField == null)
            return;
        _background = BackgroundField.GetValue(_joystick) as RectTransform;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_sink == null || _background == null || _canvas == null)
            return;
        if (!TryGetNormalizedOffset(eventData.position, out Vector2 normalized))
            return;

        _sink.OnJoystickPointerDown(normalized);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_sink == null)
            return;
        _sink.OnJoystickPointerUp();
    }

    bool TryGetNormalizedOffset(Vector2 screenPosition, out Vector2 normalized)
    {
        normalized = Vector2.zero;
        Camera cam = null;
        if (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
            cam = _canvas.worldCamera;

        Vector2 center = RectTransformUtility.WorldToScreenPoint(cam, _background.position);
        Vector2 radius = _background.sizeDelta / 2f;
        float scale = _canvas.scaleFactor;
        if (radius.x <= 0f || radius.y <= 0f || scale <= 0f)
            return false;

        normalized = (screenPosition - center) / (radius * scale);
        float magnitude = normalized.magnitude;
        if (magnitude > 1f)
            normalized /= magnitude;
        return true;
    }
}
