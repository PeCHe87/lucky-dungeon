/// <summary>Implement on entities that can be defeated (living combatants or destructibles).</summary>
public interface IDefeatable
{
    bool IsDefeated { get; }
}
