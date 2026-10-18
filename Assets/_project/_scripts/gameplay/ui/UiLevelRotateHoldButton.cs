using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Press-and-hold button that drives <see cref="UiLevelRotator"/> yaw in one direction.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class UiLevelRotateHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] UiLevelRotator rotator;
    [Tooltip("Negative = left / decrease yaw. Positive = right / increase yaw.")]
    [SerializeField] float direction = -1f;

    void Awake()
    {
        if (rotator == null)
            rotator = GetComponentInParent<UiLevelRotator>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (rotator == null)
            return;
        rotator.BeginHold(direction);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (rotator == null)
            return;
        rotator.EndHold(direction);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (rotator == null)
            return;
        rotator.EndHold(direction);
    }
}
