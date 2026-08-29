using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(101)]
[RequireComponent(typeof(Button))]
public class UiDashButtonBinder : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] FeneraxJoystickMoveIntentProvider intentProvider;
    [SerializeField] DashJoystickDoubleTapController dashController;
    [SerializeField, Range(0f, 1f)] float disabledAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] float enabledAlpha = 1f;

    Button _button;
    CanvasGroup _canvasGroup;

    void Awake()
    {
        if (intentProvider == null)
            intentProvider = FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();
        if (dashController == null)
            dashController = FindFirstObjectByType<DashJoystickDoubleTapController>();

        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void LateUpdate()
    {
        RefreshBlockedVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (dashController != null && !dashController.IsDashAvailable)
            return;

        if (intentProvider != null)
            intentProvider.RegisterUiDashFromUi();
    }

    void RefreshBlockedVisual()
    {
        if (_button == null)
            return;

        bool available = dashController != null && dashController.IsDashAvailable;
        _button.interactable = available;
        if (_canvasGroup != null)
            _canvasGroup.alpha = available ? enabledAlpha : disabledAlpha;
    }
}
