using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD health bar bound to the player's <see cref="CombatEntityHealth"/>.
/// Resolves child UI references at runtime when unset.
/// Updates fill amount and a current/max label when HP changes.
/// </summary>
public sealed class HealthPanelView : MonoBehaviour
{
    [SerializeField] CombatEntityHealth playerHealth;
    [SerializeField] Image fillImage;
    [SerializeField] TextMeshProUGUI healthLabel;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged += OnHealthChanged;
            playerHealth.Died += OnHealthChanged;
        }

        Refresh();
    }

    void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= OnHealthChanged;
            playerHealth.Died -= OnHealthChanged;
        }
    }

    void Start()
    {
        ResolveReferences();
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= OnHealthChanged;
            playerHealth.Died -= OnHealthChanged;
            playerHealth.HealthChanged += OnHealthChanged;
            playerHealth.Died += OnHealthChanged;
        }

        Refresh();
    }

    void OnHealthChanged() => Refresh();

    void Refresh()
    {
        if (playerHealth == null)
            return;

        if (fillImage != null)
        {
            float ratio = playerHealth.MaxHitPoints > 0f
                ? playerHealth.CurrentHitPoints / playerHealth.MaxHitPoints
                : 0f;
            fillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        if (healthLabel != null)
        {
            int current = Mathf.CeilToInt(playerHealth.CurrentHitPoints);
            int max = Mathf.CeilToInt(playerHealth.MaxHitPoints);
            healthLabel.text = $"{current} / {max}";
        }
    }

    void ResolveReferences()
    {
        if (playerHealth == null)
        {
            PlayerEntityState playerState = FindFirstObjectByType<PlayerEntityState>();
            if (playerState != null)
                playerHealth = playerState.GetComponent<CombatEntityHealth>();
        }

        if (fillImage == null)
        {
            Transform fillTransform = transform.Find("bg/fill");
            if (fillTransform != null)
                fillImage = fillTransform.GetComponent<Image>();
        }

        if (healthLabel == null)
        {
            Transform labelTransform = transform.Find("txtHealth");
            if (labelTransform != null)
                healthLabel = labelTransform.GetComponent<TextMeshProUGUI>();
        }
    }
}
