/// <summary>Weapon reports whether an attack presentation window is still active (for locomotion/state observers).</summary>
public interface IAttackActivity
{
    bool IsAttackActive { get; }
}
