using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that defines one FSM state.
/// Create via: Assets → Create → FSM → State
/// 
/// handlerType      → maps to a registered IStateHandler (the behaviour logic)
/// handlerParams    → inline key/value params list (no runtime JSON parsing, no extra assets)
/// transitions      → ordered list of guarded transitions evaluated every tick
/// </summary>
[CreateAssetMenu(menuName = "FSM/State", fileName = "New State")]
public class AIStateData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Human-readable state name used in logs and the Inspector")]
    public string stateId;

    [Header("Handler")]
    [Tooltip("Must match a registered IStateHandler key, e.g. 'Idle', 'Patrol', 'Chase'")]
    public string handlerType;

    [Tooltip("Inline parameters forwarded to the handler on Enter / Tick / Exit")]
    public List<FSMParam> handlerParams = new();

    [Header("Transitions")]
    [Tooltip("Evaluated top-to-bottom each tick. First matching transition wins.")]
    public List<StateTransition> transitions = new();
}
