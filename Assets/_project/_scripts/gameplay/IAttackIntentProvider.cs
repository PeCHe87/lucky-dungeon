public interface IAttackIntentProvider
{
    /// <summary>True once per frame when attack was requested from UI and/or input.</summary>
    bool WasAttackPressedThisFrame();

    /// <summary>True while attack is held from UI and/or input.</summary>
    bool IsAttackHeld();
}
