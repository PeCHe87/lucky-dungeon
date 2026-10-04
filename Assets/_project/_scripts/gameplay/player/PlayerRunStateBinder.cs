using UnityEngine;

/// <summary>
/// Applies and snapshots <see cref="PlayerRunVitals"/> onto the scene player.
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class PlayerRunStateBinder : MonoBehaviour
{
    [SerializeField] CombatEntityHealth health;

    void Awake()
    {
        if (health == null)
            health = GetComponent<CombatEntityHealth>();
    }

    void OnEnable()
    {
        if (health == null)
            return;

        health.HealthChanged += SnapshotCurrentHp;
        health.Died += SnapshotCurrentHp;
        ApplyFromRun();
    }

    void OnDisable()
    {
        if (health == null)
            return;

        SnapshotCurrentHp();
        health.HealthChanged -= SnapshotCurrentHp;
        health.Died -= SnapshotCurrentHp;
    }

    void ApplyFromRun()
    {
        PlayerRunState player = GetActivePlayerState();
        if (player == null)
            return;

        PlayerRunVitals vitals = player.Vitals;
        if (vitals.CurrentHp <= 0f)
            vitals.SetCurrentHp(vitals.EffectiveMaxHp);

        health.ApplyVitals(vitals.CurrentHp, vitals.EffectiveMaxHp);
    }

    void SnapshotCurrentHp()
    {
        PlayerRunState player = GetActivePlayerState();
        if (player == null)
            return;

        player.Vitals.SetCurrentHp(health.CurrentHitPoints);
    }

    static PlayerRunState GetActivePlayerState()
    {
        DungeonRunHost host = DungeonRunHost.Instance;
        if (host == null || !host.HasActiveRun)
            return null;

        return host.Player;
    }
}
