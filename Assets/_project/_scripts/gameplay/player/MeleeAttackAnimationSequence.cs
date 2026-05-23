using System;
using UnityEngine;

[Serializable]
public struct MeleeAttackAnimationSequence
{
    [Tooltip("Animator state names in combo order (e.g. Attacking1, Attacking2, Attacking3).")]
    public string[] animatorStateNames;
    [Tooltip("Cross-fade duration between combo steps.")]
    public float crossFadeSeconds;
    [Tooltip("Reset combo to first attack if no melee attack within this many seconds.")]
    public float comboResetSeconds;
    [Tooltip("Treat attack clip as playing until normalized time reaches this value (0-1).")]
    [Range(0.5f, 1f)]
    public float attackCompletionNormalizedTime;

    public bool IsValid => animatorStateNames != null && animatorStateNames.Length > 0;

    public int Count => animatorStateNames != null ? animatorStateNames.Length : 0;

    public bool TryGetState(int index, out string stateName)
    {
        stateName = null;
        if (animatorStateNames == null || index < 0 || index >= animatorStateNames.Length)
            return false;

        stateName = animatorStateNames[index];
        return !string.IsNullOrWhiteSpace(stateName);
    }
}
