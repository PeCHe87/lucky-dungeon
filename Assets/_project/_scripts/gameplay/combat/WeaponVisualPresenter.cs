using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns/destroys weapon mesh prefabs under hand bones when <see cref="WeaponHolder"/> equips a weapon.
/// Socket config comes from <see cref="WeaponData.VisualSockets"/>.
/// </summary>
public sealed class WeaponVisualPresenter : MonoBehaviour
{
    const string LeftHandBoneName = "Bip001 L Hand";
    const string RightHandBoneName = "Bip001 R Hand";
    const string MuzzleChildName = "muzzle";

    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] Transform leftHand;
    [SerializeField] Transform rightHand;

    readonly List<GameObject> _spawned = new List<GameObject>(4);

    public IReadOnlyList<GameObject> SpawnedVisuals => _spawned;

    void Awake()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        ResolveHandBonesIfNeeded();
    }

    void OnEnable()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged += OnEquippedWeaponChanged;
        RefreshVisuals();
    }

    void OnDisable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged -= OnEquippedWeaponChanged;
        ClearSpawned();
    }

    void OnEquippedWeaponChanged() => RefreshVisuals();

    public void RefreshVisuals()
    {
        ClearSpawned();

        WeaponData data = ResolveWeaponData(weaponHolder != null ? weaponHolder.Current : null);
        if (data == null || data.VisualSockets == null || data.VisualSockets.Length == 0)
        {
            TryRebindRangedFirePoint(null);
            return;
        }

        ResolveHandBonesIfNeeded();

        for (int i = 0; i < data.VisualSockets.Length; i++)
        {
            WeaponVisualSocket socket = data.VisualSockets[i];
            if (socket.prefab == null)
                continue;

            Transform hand = ResolveHand(socket.hand);
            if (hand == null)
            {
                Debug.LogWarning(
                    $"{nameof(WeaponVisualPresenter)} on {name}: no transform for hand {socket.hand}.",
                    this);
                continue;
            }

            GameObject instance = Instantiate(socket.prefab, hand);
            Transform t = instance.transform;
            t.localPosition = socket.localPosition;
            t.localRotation = Quaternion.Euler(socket.localEulerAngles);
            Vector3 scale = socket.localScale;
            if (scale == Vector3.zero)
                scale = Vector3.one;
            t.localScale = scale;
            _spawned.Add(instance);
        }

        TryRebindRangedFirePoint(weaponHolder != null ? weaponHolder.Current : null);
    }

    void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            GameObject go = _spawned[i];
            if (go == null)
                continue;
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    void TryRebindRangedFirePoint(IWeapon weapon)
    {
        if (weapon is not RangedWeapon ranged)
            return;

        Transform muzzle = FindMuzzleAmongSpawned();
        ranged.SetFirePoint(muzzle);
    }

    Transform FindMuzzleAmongSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            GameObject go = _spawned[i];
            if (go == null)
                continue;
            Transform muzzle = FindChildByName(go.transform, MuzzleChildName);
            if (muzzle != null)
                return muzzle;
        }
        return null;
    }

    Transform ResolveHand(WeaponHand hand)
    {
        return hand == WeaponHand.Left ? leftHand : rightHand;
    }

    void ResolveHandBonesIfNeeded()
    {
        if (leftHand != null && rightHand != null)
            return;

        Transform searchRoot = transform;
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null)
            searchRoot = animator.transform;

        if (leftHand == null)
            leftHand = FindChildByName(searchRoot, LeftHandBoneName);
        if (rightHand == null)
            rightHand = FindChildByName(searchRoot, RightHandBoneName);
    }

    static WeaponData ResolveWeaponData(IWeapon weapon)
    {
        if (weapon is IWeaponDataSource source)
            return source.Data;
        return null;
    }

    static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;
        if (root.name == childName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null)
                return found;
        }
        return null;
    }
}
