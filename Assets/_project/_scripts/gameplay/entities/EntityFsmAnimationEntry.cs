using System;
using UnityEngine;

[Serializable]
public struct EntityFsmAnimationEntry
{
    [Tooltip("Must match AIStateData.stateId on the entity FSM (e.g. Idle, TakeDamage, Die).")]
    public string stateId;
    [Tooltip("Must match an Animator state name in the entity controller.")]
    public string animatorStateName;
    [Tooltip("Cross-fade duration in seconds. Zero or negative uses the profile default.")]
    public float crossFadeSeconds;
    [Tooltip("Animator layer index.")]
    public int layer;
}
