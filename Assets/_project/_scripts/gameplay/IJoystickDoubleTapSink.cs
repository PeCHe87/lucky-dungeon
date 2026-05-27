using UnityEngine;

/// <summary>Receives joystick pointer events forwarded from <see cref="JoystickDoubleTapBridge"/>.</summary>
public interface IJoystickDoubleTapSink
{
    void OnJoystickPointerDown(Vector2 normalizedOffsetFromCenter);
    void OnJoystickPointerMove(Vector2 normalizedOffsetFromCenter);
    void OnJoystickPointerUp();
}
