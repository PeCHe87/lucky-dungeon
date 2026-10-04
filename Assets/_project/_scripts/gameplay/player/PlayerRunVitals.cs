using UnityEngine;

/// <summary>
/// Run-scoped player HP. Effective max is base plus dungeon bonuses.
/// </summary>
public sealed class PlayerRunVitals
{
    public float CurrentHp { get; private set; }
    public float BaseMaxHp { get; private set; }
    public float MaxHpBonus { get; private set; }

    public float EffectiveMaxHp => Mathf.Max(0.01f, BaseMaxHp + MaxHpBonus);

    public void BeginRun(float baseMaxHp)
    {
        BaseMaxHp = Mathf.Max(0.01f, baseMaxHp);
        MaxHpBonus = 0f;
        CurrentHp = BaseMaxHp;
        GameEvents.RaisePlayerRunVitalsChanged();
    }

    public void Clear()
    {
        CurrentHp = 0f;
        BaseMaxHp = 0f;
        MaxHpBonus = 0f;
        GameEvents.RaisePlayerRunVitalsChanged();
    }

    public void AddMaxHpBonus(float amount)
    {
        MaxHpBonus += amount;
        CurrentHp = Mathf.Clamp(CurrentHp + amount, 0f, EffectiveMaxHp);
        GameEvents.RaisePlayerRunVitalsChanged();
    }

    public void SetCurrentHp(float current)
    {
        float clamped = Mathf.Clamp(current, 0f, EffectiveMaxHp);
        if (Mathf.Approximately(clamped, CurrentHp))
            return;

        CurrentHp = clamped;
        GameEvents.RaisePlayerRunVitalsChanged();
    }
}
