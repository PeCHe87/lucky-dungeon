using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the ranged weapon info fill Image and ammo count from equipped <see cref="RangedWeapon"/>.
/// When not reloading, fill stays at 100%. While reloading, snaps fill to 0 then animates 0→1,
/// uses <see cref="reloadFillColor"/>, and hides <c>txtAmmo</c>.
/// Host must stay active (player root); do not place on the toggled <c>rangedWeaponInfo</c> root.
/// </summary>
public sealed class RangedWeaponInfoAmmoFill : MonoBehaviour
{
    const string DefaultFillPath = "rangedWeaponInfo/panel/fill";
    const string DefaultAmmoLabelPath = "rangedWeaponInfo/panel/txtAmmo";

    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] Image fillImage;
    [SerializeField] TextMeshProUGUI ammoLabel;
    [SerializeField] Color ammoFillColor = new Color(0.9f, 0.46f, 0f, 1f);
    [SerializeField] Color reloadFillColor = new Color(0.35f, 0.75f, 1f, 1f);

    RangedWeapon _subscribedWeapon;

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged += OnEquippedWeaponChanged;

        RefreshSubscription();
        Refresh();
    }

    void OnDisable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged -= OnEquippedWeaponChanged;

        UnsubscribeWeapon();
    }

    void Start()
    {
        RefreshSubscription();
        Refresh();
    }

    void Update()
    {
        if (fillImage == null || _subscribedWeapon == null || !_subscribedWeapon.IsReloading)
            return;

        fillImage.fillAmount = _subscribedWeapon.ReloadProgress;
        ApplyFillColor(reloading: true);
    }

    void OnEquippedWeaponChanged()
    {
        RefreshSubscription();
        Refresh();
    }

    void OnAmmoChanged() => Refresh();

    void OnReloadStarted()
    {
        ResolveRefs();
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            ApplyFillColor(reloading: true);
        }
        ApplyAmmoLabel(show: false, ammo: 0);
    }

    void RefreshSubscription()
    {
        ResolveRefs();

        RangedWeapon next = weaponHolder != null && weaponHolder.Current is RangedWeapon ranged
            ? ranged
            : null;

        if (_subscribedWeapon == next)
            return;

        UnsubscribeWeapon();
        _subscribedWeapon = next;
        if (_subscribedWeapon != null)
        {
            _subscribedWeapon.AmmoChanged += OnAmmoChanged;
            _subscribedWeapon.ReloadStarted += OnReloadStarted;
        }
    }

    void UnsubscribeWeapon()
    {
        if (_subscribedWeapon == null)
            return;

        _subscribedWeapon.AmmoChanged -= OnAmmoChanged;
        _subscribedWeapon.ReloadStarted -= OnReloadStarted;
        _subscribedWeapon = null;
    }

    void Refresh()
    {
        ResolveRefs();

        if (_subscribedWeapon == null || _subscribedWeapon.MagazineSize <= 0)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
                ApplyFillColor(reloading: false);
            }
            ApplyAmmoLabel(show: false, ammo: 0);
            return;
        }

        if (_subscribedWeapon.IsReloading)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = _subscribedWeapon.ReloadProgress;
                ApplyFillColor(reloading: true);
            }
            ApplyAmmoLabel(show: false, ammo: 0);
            return;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = 1f;
            ApplyFillColor(reloading: false);
        }
        ApplyAmmoLabel(show: true, ammo: _subscribedWeapon.CurrentAmmo);
    }

    void ApplyFillColor(bool reloading)
    {
        if (fillImage == null)
            return;
        fillImage.color = reloading ? reloadFillColor : ammoFillColor;
    }

    void ApplyAmmoLabel(bool show, int ammo)
    {
        if (ammoLabel == null)
            return;

        if (ammoLabel.gameObject.activeSelf != show)
            ammoLabel.gameObject.SetActive(show);

        if (show)
            ammoLabel.text = ammo.ToString();
    }

    void ResolveRefs()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        if (weaponHolder == null)
            weaponHolder = GetComponentInParent<WeaponHolder>();

        if (fillImage == null)
        {
            Transform fillTransform = transform.Find(DefaultFillPath);
            if (fillTransform != null)
                fillImage = fillTransform.GetComponent<Image>();
        }

        if (ammoLabel == null)
        {
            Transform labelTransform = transform.Find(DefaultAmmoLabelPath);
            if (labelTransform != null)
                ammoLabel = labelTransform.GetComponent<TextMeshProUGUI>();
        }
    }
}
