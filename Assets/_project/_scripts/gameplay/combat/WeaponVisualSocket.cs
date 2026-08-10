using System;
using UnityEngine;

public enum WeaponHand
{
    Left,
    Right,
}

[Serializable]
public struct WeaponVisualSocket
{
    public GameObject prefab;
    public WeaponHand hand;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale;
}
