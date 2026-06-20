using UnityEngine;

/// <summary>Per-weapon animator controller and state animation profile for equip-time swapping.</summary>
public interface IWeaponAnimationBinding
{
    RuntimeAnimatorController AnimatorController { get; }
    PlayerEntityStateAnimationProfile AnimationProfile { get; }
}
