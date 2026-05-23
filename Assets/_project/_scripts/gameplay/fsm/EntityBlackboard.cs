using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared data context attached to any entity using the FSM.
/// States and conditions read/write data through the blackboard.
/// </summary>
public class EntityBlackboard : MonoBehaviour
{
    [Header("Initial Values (optional)")]
    [Tooltip("Optional: values copied into the runtime blackboard dictionary on Awake().")]
    public List<FSMParam> initialValues = new();

    // ── Generic key-value store ──────────────────────────────────────────────
    private readonly Dictionary<string, object> _data = new();

    void Awake()
    {
        ApplyInitialValues();
    }

    public void Set<T>(string key, T value) => _data[key] = value;

    public T Get<T>(string key, T defaultValue = default)
    {
        if (_data.TryGetValue(key, out var val) && val is T typed)
            return typed;
        return defaultValue;
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (_data.TryGetValue(key, out var val) && val is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public bool Has(string key) => _data.ContainsKey(key);
    public void Remove(string key) => _data.Remove(key);

    private void ApplyInitialValues()
    {
        if (initialValues == null) return;

        for (int i = 0; i < initialValues.Count; i++)
        {
            var p = initialValues[i];
            if (p == null || string.IsNullOrEmpty(p.key)) continue;

            switch (p.type)
            {
                case FSMParamType.Float:   Set(p.key, p.floatValue); break;
                case FSMParamType.Int:     Set(p.key, p.intValue); break;
                case FSMParamType.Bool:    Set(p.key, p.boolValue); break;
                case FSMParamType.String:  Set(p.key, p.stringValue ?? ""); break;
                case FSMParamType.Vector3: Set(p.key, p.vector3Value); break;
                case FSMParamType.Object:  Set(p.key, p.objectValue); break;
                default: break;
            }
        }
    }

    // ── Well-known entity fields (set externally by sensors/controllers) ─────
    /// <summary>The entity's own transform. Use Self instead of transform for clarity.</summary>
    public Transform Self => transform;

    /// <summary>Currently detected target (player, enemy, etc.). Null = no target.</summary>
    public Transform Target;

    /// <summary>The point to patrol around. Set by PatrolStateHandler on enter.</summary>
    public Vector3 PatrolCenter;

    /// <summary>Current patrol waypoint destination.</summary>
    public Vector3 PatrolTarget;
}
