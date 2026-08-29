using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Full-screen transparent panel: pointer down registers an attack (multi-touch safe).
/// Place below interactive HUD controls so joystick / buttons keep priority.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class ScreenTapAttackInput : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] FeneraxJoystickMoveIntentProvider intentProvider;
    [Tooltip("If unset, uses first PlayerEntityState in the scene.")]
    [SerializeField] PlayerEntityState playerEntityState;

    Image _image;
    readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);

    void Awake()
    {
        _image = GetComponent<Image>();
        if (intentProvider == null)
            intentProvider = FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();
        if (playerEntityState == null)
            playerEntityState = FindFirstObjectByType<PlayerEntityState>();

        if (_image != null)
        {
            Color c = _image.color;
            c.a = 0f;
            _image.color = c;
            _image.raycastTarget = true;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanAcceptInput())
            return;

        if (IsBlockedByInteractiveUi(eventData))
            return;

        if (intentProvider != null)
            intentProvider.RegisterUiAttackFromUi();
    }

    bool CanAcceptInput() =>
        playerEntityState == null || !playerEntityState.IsInputBlocked;

    bool IsBlockedByInteractiveUi(PointerEventData eventData)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        _raycastResults.Clear();
        eventSystem.RaycastAll(eventData, _raycastResults);

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            GameObject hit = _raycastResults[i].gameObject;
            if (hit == null || hit == gameObject || hit.transform.IsChildOf(transform))
                continue;

            if (hit.GetComponentInParent<Selectable>() != null)
                return true;

            if (hit.GetComponentInParent<Joystick>() != null)
                return true;

            if (hit.GetComponentInParent<TapOrDragJoystickGate>() != null)
                return true;
        }

        return false;
    }
}
