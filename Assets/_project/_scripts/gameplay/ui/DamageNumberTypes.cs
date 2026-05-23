/// <summary>Elemental / damage typing used for floating number coloring.</summary>
public enum DamageElement
{
    Physical = 0,
    Fire = 1,
    Frost = 2,
    Lightning = 3,
    Poison = 4,
}

/// <summary>Visual style for a floating damage number (element + critical).</summary>
public readonly struct DamageNumberStyle
{
    public DamageNumberStyle(DamageElement element, bool isCritical)
    {
        Element = element;
        IsCritical = isCritical;
    }

    public DamageElement Element { get; }
    public bool IsCritical { get; }
}
