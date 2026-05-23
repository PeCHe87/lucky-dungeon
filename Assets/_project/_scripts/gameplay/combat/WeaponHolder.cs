using System;
using UnityEngine;

/// <summary>Holds the currently equipped weapon; swap via <see cref="Equip"/> for pickups / loadouts.</summary>
public sealed class WeaponHolder : MonoBehaviour
{
    [Tooltip("MonoBehaviour on this object or a child that implements IWeapon (e.g. MeleeWeapon).")]
    [SerializeField] MonoBehaviour startingWeapon;
    [Header("Weapon slots for UI / loadout")]
    [Tooltip("MeleeWeapon or any component on this hierarchy that implements IWeapon; used by EquipMelee.")]
    [SerializeField] MonoBehaviour meleeWeapon;
    [Tooltip("RangedWeapon or any component on this hierarchy that implements IWeapon; used by EquipRanged.")]
    [SerializeField] MonoBehaviour rangedWeapon;
    [Header("Debug")]
    [SerializeField] bool logEquippedWeaponChanges = true;

    IWeapon _current;

    public IWeapon Current => _current;

    /// <summary>Raised after the equipped weapon changes (including initial <see cref="Awake"/> resolution).</summary>
    public event Action EquippedWeaponChanged;

    void Awake()
    {
        if (startingWeapon == null)
        {
            _current = null;
            NotifyEquippedWeaponChanged();
            return;
        }

        _current = startingWeapon as IWeapon;
        if (_current == null)
            Debug.LogWarning($"{nameof(WeaponHolder)} on {name}: startingWeapon '{startingWeapon.name}' does not implement {nameof(IWeapon)}.", this);
        NotifyEquippedWeaponChanged();
    }

    public void Equip(IWeapon weapon)
    {
        _current = weapon;
        NotifyEquippedWeaponChanged();
    }

    void NotifyEquippedWeaponChanged()
    {
        SyncEquippedWeaponPresentation();
        LogEquippedWeaponTypeIfEnabled();
        EquippedWeaponChanged?.Invoke();
    }

    void SyncEquippedWeaponPresentation()
    {
        if (meleeWeapon is IWeaponEquippedPresentation meleePresentation)
            meleePresentation.SetEquippedVisuals(IsMeleeEquipped());
        if (rangedWeapon is IWeaponEquippedPresentation rangedPresentation)
            rangedPresentation.SetEquippedVisuals(IsRangedEquipped());
    }

    void LogEquippedWeaponTypeIfEnabled()
    {
        if (!logEquippedWeaponChanges)
            return;

        string typeInfo = DescribeEquippedWeaponType(_current);
        Debug.Log($"{nameof(WeaponHolder)} on {name}: equipped weapon type = {typeInfo}", this);
    }

    static string DescribeEquippedWeaponType(IWeapon weapon)
    {
        if (weapon == null)
            return "none";
        if (weapon is MeleeWeapon)
            return nameof(MeleeWeapon);
        if (weapon is RangedWeapon)
            return nameof(RangedWeapon);
        return $"IWeapon:{weapon.GetType().Name}";
    }

    /// <summary>True when <see cref="Current"/> is a <see cref="MeleeWeapon"/>.</summary>
    public bool IsMeleeEquipped() => _current is MeleeWeapon;

    /// <summary>True when <see cref="Current"/> is a <see cref="RangedWeapon"/>.</summary>
    public bool IsRangedEquipped() => _current is RangedWeapon;

    /// <summary>UI / input: equips <see cref="meleeWeapon"/> if it implements <see cref="IWeapon"/>.</summary>
    public void EquipMelee() => TryEquipFromSlot(meleeWeapon, nameof(meleeWeapon));

    /// <summary>UI / input: equips <see cref="rangedWeapon"/> if it implements <see cref="IWeapon"/>.</summary>
    public void EquipRanged() => TryEquipFromSlot(rangedWeapon, nameof(rangedWeapon));

    void TryEquipFromSlot(MonoBehaviour slot, string fieldName)
    {
        if (slot == null)
        {
            Debug.LogWarning($"{nameof(WeaponHolder)} on {name}: {fieldName} is not assigned.", this);
            return;
        }

        IWeapon weapon = slot as IWeapon;
        if (weapon == null)
        {
            Debug.LogWarning($"{nameof(WeaponHolder)} on {name}: {fieldName} '{slot.name}' does not implement {nameof(IWeapon)}.", this);
            return;
        }

        Equip(weapon);
    }

    public bool TryAttack(in AttackContext ctx)
    {
        if (_current == null)
            return false;
        return _current.TryAttack(in ctx);
    }
}
