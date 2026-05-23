# AI Finite State Machine — Setup Guide

## Folder Structure

```
Assets/
└── Scripts/
    └── FSM/
        ├── Core/
        │   ├── EntityBlackboard.cs      ← Shared data context (MonoBehaviour)
        │   ├── AIStateData.cs           ← ScriptableObject definition per state
        │   ├── AIStateMachine.cs        ← Runtime engine (MonoBehaviour)
        │   ├── FSMInterfaces.cs         ← IStateHandler, IConditionEvaluator
        │   ├── FSMRegistries.cs         ← StateHandlerRegistry, ConditionEvaluatorRegistry
        │   └── FSMTypes.cs              ← Condition, StateTransition (data structs)
        ├── Handlers/
        │   └── StateHandlers.cs         ← IdleStateHandler, PatrolStateHandler, ChaseStateHandler
        ├── Conditions/
        │   └── ConditionEvaluators.cs   ← All IConditionEvaluator implementations
        ├── Bootstrap/
        │   └── AISystemBootstrap.cs     ← Registers everything on startup
        └── EnemyTargetSensor.cs         ← Example sensor that feeds the blackboard
```

---

## Architecture at a Glance

```
 AISystemBootstrap                          (Runs before scene load)
      │
      ├── StateHandlerRegistry               (Dictionary<string, IStateHandler>)
      │       "Idle"    → IdleStateHandler
      │       "Patrol"  → PatrolStateHandler
      │       "Chase"   → ChaseStateHandler
      │
      └── ConditionEvaluatorRegistry         (Dictionary<string, IConditionEvaluator>)
              "TimerExpired"  → TimerExpiredEvaluator
              "TargetInRange" → TargetInRangeEvaluator
              "TargetReached" → TargetReachedEvaluator
              ...

 ┌──────────────────────────────────────┐
 │  GameObject: Enemy                   │
 │  ├── EntityBlackboard (data)         │  ← written by sensors, read by conditions
 │  ├── AIStateMachine  (engine)        │  ← ticks FSM every frame
 │  └── EnemyTargetSensor (feeds data)  │  ← optional, writes bb.Target
 └──────────────────────────────────────┘

 ScriptableObject Assets (in Project view):
   State_Idle.asset
   State_Patrol.asset
   State_Chase.asset
```

---

## Step 1 — Create State ScriptableObject Assets

Right-click in the Project window → **Create → FSM → State**

### State_Idle.asset
| Field             | Value                          |
|-------------------|--------------------------------|
| stateId           | `Idle`                         |
| handlerType       | `Idle`                         |
| handlerParamsJson | `{"duration":3.0}`             |

**Transitions:**
```
[0] label: "To Patrol when timer expires"
    conditions:
      [0] type: TimerExpired    paramsJson: {"key":"fsm.timer","duration":3.0}
    targetState: State_Patrol
```

---

### State_Patrol.asset
| Field             | Value                              |
|-------------------|------------------------------------|
| stateId           | `Patrol`                           |
| handlerType       | `Patrol`                           |
| handlerParamsJson | `{"radius":5,"speed":2,"waypointThreshold":0.3}` |

**Transitions:**
```
[0] label: "To Chase when target detected"
    conditions:
      [0] type: TargetInRange   paramsJson: {"range":6.0}
    targetState: State_Chase
```

---

### State_Chase.asset
| Field             | Value                                  |
|-------------------|----------------------------------------|
| stateId           | `Chase`                                |
| handlerType       | `Chase`                                |
| handlerParamsJson | `{"speed":4,"stoppingDistance":1.0}`   |

**Transitions:**
```
[0] label: "Back to Patrol when target lost"
    conditions:
      [0] type: TargetLost      paramsJson: {"range":8.0}
    targetState: State_Patrol

[1] label: "Stop when target reached"
    conditions:
      [0] type: TargetReached   paramsJson: {"distance":1.2}
    targetState: State_Idle    ← or a future "Attack" state
```

---

## Step 2 — Set Up the Enemy GameObject

1. Create or select your enemy GameObject.
2. Add components:
   - `EntityBlackboard`
   - `AIStateMachine` → set **Initial State** to `State_Idle`
   - `EnemyTargetSensor` → set **Target Layer** to your Player layer, **Sensor Radius** ≥ 8
3. Press Play. The FSM boots automatically.

---

## Step 3 — Adding a New State

Example: adding an **Attack** state.

### 3a. Create the handler
```csharp
// Handlers/AttackStateHandler.cs
[System.Serializable]
public class AttackParams { public float cooldown = 1.5f; public float damage = 8f; }

public class AttackStateHandler : IStateHandler
{
    public void OnEnter(EntityBlackboard bb, string paramsJson)
    {
        bb.Set("attack.cooldown", 0f); // Ready to attack immediately
    }

    public void OnTick(EntityBlackboard bb, string paramsJson, float dt)
    {
        var p = JsonUtility.FromJson<AttackParams>(paramsJson);
        // ... your attack logic here
    }

    public void OnExit(EntityBlackboard bb, string paramsJson) { }
}
```

### 3b. Register it
```csharp
// AISystemBootstrap.cs → RegisterHandlers()
StateHandlerRegistry.Register("Attack", new AttackStateHandler());
```

### 3c. Create ScriptableObject asset
Create a new State asset, set handlerType = "Attack", configure params, wire transitions.

---

## Step 4 — Adding a New Condition

Example: **HPBelow** — true when the entity's HP is below a threshold.

### 4a. Write a custom HP field on EntityBlackboard
```csharp
public float CurrentHP = 100f;
public float MaxHP     = 100f;
```

### 4b. Create the evaluator
```csharp
// Conditions/ConditionEvaluators.cs (add at the bottom)
[System.Serializable]
class HPBelowParams { public float threshold = 30f; }

public class HPBelowEvaluator : IConditionEvaluator
{
    public bool Evaluate(string paramsJson, EntityBlackboard bb)
    {
        var p = JsonUtility.FromJson<HPBelowParams>(paramsJson);
        return bb.CurrentHP <= p.threshold;
    }
}
```

### 4c. Register it
```csharp
// AISystemBootstrap.cs → RegisterEvaluators()
ConditionEvaluatorRegistry.Register("HPBelow", new HPBelowEvaluator());
```

### 4d. Use in any State asset
```
conditions:
  [0] type: HPBelow    paramsJson: {"threshold":30.0}
```

---

## Available Conditions Reference

| Key              | paramsJson example                           | True when…                          |
|------------------|----------------------------------------------|--------------------------------------|
| `TimerExpired`   | `{"key":"fsm.timer","duration":3.0}`         | Blackboard float >= duration         |
| `TargetInRange`  | `{"range":6.0}`                              | Target exists and is within range    |
| `TargetLost`     | `{"range":8.0}`                              | Target is null or beyond range       |
| `TargetReached`  | `{"distance":1.2}`                           | Target is within stopping distance   |
| `TargetNull`     | (none)                                       | bb.Target == null                    |
| `BlackboardBool` | `{"key":"isStunned","expected":true}`        | Blackboard bool matches expected     |
| `AlwaysTrue`     | (none)                                       | Always — use as default fallback     |

---

## Tips

- **Transition order matters.** The FSM evaluates transitions top-to-bottom and fires the first match. Put high-priority exits (e.g. HP-critical flee) above low-priority ones.
- **Hysteresis on range conditions.** Use a slightly larger range on `TargetLost` than `TargetInRange` to avoid rapid flickering between states.
- **NavMesh integration.** Replace the direct `Transform.position` movement in handlers with `NavMeshAgent.SetDestination()` — everything else stays the same.
- **Multiple conditions = AND.** Add multiple conditions to one transition for compound logic (e.g. "target in range AND HP above 50%").
- **Chaining conditions for OR.** Create two separate transitions pointing to the same target state.
