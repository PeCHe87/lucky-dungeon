using System;
using UnityEngine;

/// <summary>Holds the currently equipped weapon; swap via <see cref="Equip"/> for pickups / loadouts.</summary>
[DefaultExecutionOrder(-100)]
public sealed class WeaponHolder : MonoBehaviour
{
    [Tooltip("Optional. When set, Awake equips the matching melee/ranged slot from this asset by type.")]
    [SerializeField] WeaponData startingWeaponData;
    [Tooltip("Fallback when startingWeaponData is null. MonoBehaviour that implements IWeapon.")]
    [SerializeField] MonoBehaviour startingWeapon;
    [Header("Weapon slots for UI / loadout")]
    [Tooltip("MeleeWeapon or any component on this hierarchy that implements IWeapon; used by EquipMelee.")]
    [SerializeField] MonoBehaviour meleeWeapon;
    [Tooltip("RangedWeapon or any component on this hierarchy that implements IWeapon; used by EquipRanged.")]
    [SerializeField] MonoBehaviour rangedWeapon;
    [Header("Weapon data")]
    [Tooltip("Applied to meleeWeapon when it is a MeleeWeapon.")]
    [SerializeField] MeleeWeaponData meleeWeaponData;
    [Tooltip("Applied to rangedWeapon when it is a RangedWeapon.")]
    [SerializeField] AssaultWeaponData rangedWeaponData;
    [Header("Debug")]
    [SerializeField] bool logEquippedWeaponChanges = true;

    IWeapon _current;

    public IWeapon Current => _current;

    /// <summary>Raised after the equipped weapon changes (including initial <see cref="Awake"/> resolution).</summary>
    public event Action EquippedWeaponChanged;

    void Awake()
    {
        ApplySlotWeaponData();

        if (TryEquipFromStartingWeaponData())
            return;

        EquipFromStartingWeaponComponent();
    }

    void ApplySlotWeaponData()
    {
        if (meleeWeaponData != null && meleeWeapon is MeleeWeapon melee)
            melee.SetData(meleeWeaponData);

        if (rangedWeaponData != null && rangedWeapon is RangedWeapon ranged)
            ranged.SetData(rangedWeaponData);
    }

    bool TryEquipFromStartingWeaponData()
    {
        if (startingWeaponData == null)
            return false;

        if (startingWeaponData is MeleeWeaponData meleeData)
        {
            if (meleeWeapon is MeleeWeapon melee)
            {
                melee.SetData(meleeData);
                Equip(melee);
                return true;
            }

            Debug.LogWarning(
                $"{nameof(WeaponHolder)} on {name}: {nameof(startingWeaponData)} is melee but {nameof(meleeWeapon)} is missing or not a {nameof(MeleeWeapon)}.",
                this);
            NotifyEquippedWeaponChanged();
            return true;
        }

        if (startingWeaponData is AssaultWeaponData assaultData)
        {
            if (rangedWeapon is RangedWeapon ranged)
            {
                ranged.SetData(assaultData);
                Equip(ranged);
                return true;
            }

            Debug.LogWarning(
                $"{nameof(WeaponHolder)} on {name}: {nameof(startingWeaponData)} is ranged but {nameof(rangedWeapon)} is missing or not a {nameof(RangedWeapon)}.",
                this);
            NotifyEquippedWeaponChanged();
            return true;
        }

        Debug.LogWarning(
            $"{nameof(WeaponHolder)} on {name}: unsupported {nameof(startingWeaponData)} type '{startingWeaponData.GetType().Name}'.",
            this);
        NotifyEquippedWeaponChanged();
        return true;
    }

    void EquipFromStartingWeaponComponent()
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
        LogEquippedWeaponTypeIfEnabled();
        EquippedWeaponChanged?.Invoke();
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

    /// <summary>UI / input: toggles between melee and ranged slots.</summary>
    public void SwapEquippedWeapon()
    {
        if (IsRangedEquipped())
            EquipMelee();
        else
            EquipRanged();
    }

    /// <summary>
    /// Returns the configured data for the melee or ranged slot (not necessarily currently equipped).
    /// </summary>
    public WeaponData GetSlotWeaponData(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Melee:
                if (meleeWeapon is IWeaponDataSource meleeSource && meleeSource.Data != null)
                    return meleeSource.Data;
                return meleeWeaponData;
            case WeaponType.Range:
                if (rangedWeapon is IWeaponDataSource rangedSource && rangedSource.Data != null)
                    return rangedSource.Data;
                return rangedWeaponData;
            default:
                return null;
        }
    }

    /// <summary>
    /// Replaces the matching slot's <see cref="WeaponData"/>, applies it to the slot component, and equips that slot.
    /// </summary>
    public bool TryReplaceSlotWeapon(WeaponData data)
    {
        if (data == null)
        {
            Debug.LogWarning($"{nameof(WeaponHolder)} on {name}: {nameof(TryReplaceSlotWeapon)} called with null data.", this);
            return false;
        }

        WeaponType type = data.WeaponType;
        if (type == WeaponType.None)
        {
            if (data is MeleeWeaponData)
                type = WeaponType.Melee;
            else if (data is AssaultWeaponData)
                type = WeaponType.Range;
        }

        switch (type)
        {
            case WeaponType.Melee:
            {
                if (data is not MeleeWeaponData meleeData)
                {
                    Debug.LogWarning(
                        $"{nameof(WeaponHolder)} on {name}: melee replace requires {nameof(MeleeWeaponData)}, got '{data.GetType().Name}'.",
                        this);
                    return false;
                }

                if (meleeWeapon is not MeleeWeapon melee)
                {
                    Debug.LogWarning(
                        $"{nameof(WeaponHolder)} on {name}: {nameof(meleeWeapon)} is missing or not a {nameof(MeleeWeapon)}.",
                        this);
                    return false;
                }

                meleeWeaponData = meleeData;
                melee.SetData(meleeData);
                Equip(melee);
                return true;
            }
            case WeaponType.Range:
            {
                if (data is not AssaultWeaponData rangedData)
                {
                    Debug.LogWarning(
                        $"{nameof(WeaponHolder)} on {name}: range replace requires {nameof(AssaultWeaponData)}, got '{data.GetType().Name}'.",
                        this);
                    return false;
                }

                if (rangedWeapon is not RangedWeapon ranged)
                {
                    Debug.LogWarning(
                        $"{nameof(WeaponHolder)} on {name}: {nameof(rangedWeapon)} is missing or not a {nameof(RangedWeapon)}.",
                        this);
                    return false;
                }

                rangedWeaponData = rangedData;
                ranged.SetData(rangedData);
                Equip(ranged);
                return true;
            }
            default:
                Debug.LogWarning(
                    $"{nameof(WeaponHolder)} on {name}: cannot replace slot for WeaponType '{type}' ({data.WeaponId}).",
                    this);
                return false;
        }
    }

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

    public void CancelActiveAttack()
    {
        if (_current is IAttackActivity activity)
            activity.CancelAttack();
    }
}
