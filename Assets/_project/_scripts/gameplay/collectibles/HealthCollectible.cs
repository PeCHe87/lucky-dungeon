using UnityEngine;

/// <summary>
/// Heals the player on contact. Use as a prefab variant of collectableBase.
/// </summary>
public sealed class HealthCollectible : Collectible
{
    [SerializeField, Min(0.01f)] float healAmount = 25f;
    [Tooltip("When true, the pickup is not consumed if the collector is already at full HP.")]
    [SerializeField] bool requireMissingHealth = false;
    [SerializeField] bool showFloatingHealText = true;
    [SerializeField] bool useCustomFloatingHealFontSize;
    [SerializeField, Min(1f)] float floatingHealFontSize = 42f;

    protected override bool TryApply(CombatEntityHealth collector)
    {
        if (requireMissingHealth && collector.CurrentHitPoints >= collector.MaxHitPoints)
            return false;

        float before = collector.CurrentHitPoints;
        collector.Heal(healAmount);
        float actualHeal = collector.CurrentHitPoints - before;

        if (showFloatingHealText && actualHeal > 0f)
        {
            var presenter = FloatingDamageTextPresenter.Instance;
            if (presenter != null)
            {
                Vector3 textPosition = collector.GetFloatingTextWorldPosition();
                if (useCustomFloatingHealFontSize)
                    presenter.SpawnHeal(textPosition, actualHeal, floatingHealFontSize);
                else
                    presenter.SpawnHeal(textPosition, actualHeal);
            }
        }

        if (requireMissingHealth)
            return actualHeal > 0f;

        return true;
    }
}
