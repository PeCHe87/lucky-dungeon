using System;
using UnityEngine;

[Serializable]
public struct PlayerEntityStateAnimationEntry
{
    public PlayerEntityStateKind kind;
    [Tooltip("Must match an Animator state name in the locomotion controller.")]
    public string animatorStateName;
    [Tooltip("Cross-fade duration in seconds. Zero or negative uses the profile default.")]
    public float crossFadeSeconds;
    [Tooltip("Animator layer index.")]
    public int layer;
}
