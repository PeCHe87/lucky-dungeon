using System;
using UnityEngine;

/// <summary>
/// MonoBehaviour that drives the Finite State Machine for one entity.
///
/// Setup in Inspector:
///   1. Assign 'initialState' to a State ScriptableObject.
///   2. Attach EntityBlackboard to the same GameObject.
///   3. That's it — the FSM boots automatically on Start.
///
/// The FSM evaluates transitions every frame (top-to-bottom per state).
/// First matching transition fires immediately that frame.
/// </summary>
public class AIStateMachine : MonoBehaviour
{
    [Header("FSM Configuration")]
    [Tooltip("The state the entity starts in")]
    public AIStateData initialState;

    [Tooltip("Enable to log every state transition to the Console")]
    public bool debugLog = true;

    // ── Runtime state ────────────────────────────────────────────────────────
    private EntityBlackboard _bb;
    private AIStateData      _currentStateData;
    private IStateHandler    _currentHandler;

    const string ReturnStateKey = "fsm.returnState";

    // ── Public read access ───────────────────────────────────────────────────
    public string CurrentStateId => _currentStateData?.stateId ?? "—";
    public AIStateData CurrentStateData => _currentStateData;

    /// <summary>Fired after a successful transition. Previous is null on the first enter.</summary>
    public event Action<AIStateData, AIStateData> StateChanged;

    // ────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _bb = GetComponent<EntityBlackboard>();
        if (_bb == null)
            Debug.LogError($"[FSM] {name}: EntityBlackboard component is missing!", this);
    }

    void Start()
    {
        if (initialState != null)
            TransitionTo(initialState);
        else
            Debug.LogError($"[FSM] {name}: No initialState assigned!", this);
    }

    void Update() => Tick(Time.deltaTime);

    // ── Core tick ────────────────────────────────────────────────────────────
    private void Tick(float dt)
    {
        if (_currentStateData == null || _currentHandler == null) return;

        // Let the current state do its work this frame
        _currentHandler.OnTick(_bb, _currentStateData.handlerParams, dt);

        // Evaluate transitions top-to-bottom — first match wins
        foreach (var transition in _currentStateData.transitions)
        {
            if (transition.targetState == null) continue;
            if (AllConditionsMet(transition))
            {
                TransitionTo(transition.targetState);
                return; // Stop evaluating — new state owns next tick
            }
        }
    }

    // ── Transition ───────────────────────────────────────────────────────────
    private void TransitionTo(AIStateData nextStateData)
    {
        AIStateData previousStateData = _currentStateData;

        // Exit current state
        if (_currentStateData != null && _currentHandler != null)
            _currentHandler.OnExit(_bb, _currentStateData.handlerParams);

        // Swap state
        _currentStateData = nextStateData;
        _currentHandler   = StateHandlerRegistry.Get(nextStateData.handlerType);

        if (_currentHandler == null)
        {
            Debug.LogError($"[FSM] {name}: Handler '{nextStateData.handlerType}' not found. " +
                           "The FSM will stall until fixed.");
            return;
        }

        // Enter new state
        _currentHandler.OnEnter(_bb, _currentStateData.handlerParams);

        if (debugLog)
            Debug.Log($"[FSM] {name} → <b>{nextStateData.stateId}</b>", this);

        StateChanged?.Invoke(previousStateData, _currentStateData);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private bool AllConditionsMet(StateTransition transition)
    {
        foreach (var condition in transition.conditions)
            if (!ConditionEvaluatorRegistry.Evaluate(condition, _bb))
                return false;
        return true;
    }

    /// <summary>
    /// Force an immediate transition from external code
    /// (e.g. fatal damage triggering Die).
    /// </summary>
    public void ForceTransition(AIStateData nextState) => TransitionTo(nextState);

    /// <summary>
    /// Saves the current state on the blackboard, then transitions to an interrupt state (e.g. TakeDamage).
    /// </summary>
    public void ForceInterrupt(AIStateData interruptState)
    {
        if (_bb != null && _currentStateData != null)
            _bb.Set(ReturnStateKey, _currentStateData);
        TransitionTo(interruptState);
    }

    /// <summary>
    /// Returns to the state saved by <see cref="ForceInterrupt"/>, or <see cref="initialState"/> if none.
    /// </summary>
    public void ForceReturnFromInterrupt()
    {
        if (_bb == null)
            return;

        AIStateData returnState = _bb.Get<AIStateData>(ReturnStateKey);
        _bb.Remove(ReturnStateKey);
        TransitionTo(returnState != null ? returnState : initialState);
    }

    // ── Debug ─────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (!debugLog) return;
        var pos = Camera.main?.WorldToScreenPoint(transform.position) ?? Vector2.zero;
        GUI.Label(new Rect(pos.x - 40, Screen.height - pos.y - 30, 120, 20),
                  $"[{CurrentStateId}]");
    }
}

