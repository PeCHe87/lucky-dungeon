using UnityEngine;

/// <summary>Receives joystick pointer events forwarded from <see cref="FeneraxJoystickMoveIntentProvider"/>.</summary>
public interface IJoystickDoubleTapSink
{
    void OnJoystickPointerUp();
    void OnJoystickPointerDown(Vector2 normalizedOffsetFromCenter);
}
