using UnityEngine;

/// <summary>
/// Base for world collectibles. Detects the player via trigger contact and applies a typed effect.
/// Place on the same GameObject as the trigger collider (see collectableBase prefab scaffold).
/// </summary>
public abstract class Collectible : MonoBehaviour
{
    static int s_playerLayer = -1;

    [SerializeField] bool destroyOnCollect = true;

    [Header("Collection feedback")]
    [SerializeField] bool playCollectVfx;
    [SerializeField] GameObject collectVfxPrefab;
    [SerializeField] Transform collectVfxAnchor;
    [Tooltip("World-space Y offset applied when spawning collect VFX.")]
    [SerializeField] float collectVfxVerticalOffset;
    [SerializeField, Min(0f)] float collectVfxDestroyDelay = 2f;
    [SerializeField] bool playCollectSfx;
    [SerializeField] AudioClip collectSfx;
    [SerializeField, Range(0f, 1f)] float collectSfxVolume = 1f;

    bool _collected;

    void OnTriggerEnter(Collider other)
    {
        if (_collected || other == null)
            return;

        if (!IsPlayerCollider(other))
            return;

        var health = other.GetComponentInParent<CombatEntityHealth>();
        if (health == null || health.Alignment != EntityAlignment.Ally || health.IsDefeated)
            return;

        if (!TryApply(health))
            return;

        _collected = true;
        PlayCollectFeedback();

        if (destroyOnCollect)
            Destroy(gameObject);
    }

    void PlayCollectFeedback()
    {
        Vector3 pos = collectVfxAnchor != null ? collectVfxAnchor.position : transform.position;

        if (playCollectVfx && collectVfxPrefab != null)
        {
            Vector3 vfxPos = pos + Vector3.up * collectVfxVerticalOffset;
            GameObject vfx = Instantiate(collectVfxPrefab, vfxPos, Quaternion.identity);
            ParticleSystem[] systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
                systems[i].Play(true);

            if (collectVfxDestroyDelay > 0f)
                Destroy(vfx, collectVfxDestroyDelay);
        }

        if (playCollectSfx && collectSfx != null)
            AudioSource.PlayClipAtPoint(collectSfx, pos, collectSfxVolume);
    }

    static bool IsPlayerCollider(Collider other)
    {
        if (s_playerLayer < 0)
            s_playerLayer = LayerMask.NameToLayer("Player");

        return s_playerLayer >= 0 && other.gameObject.layer == s_playerLayer;
    }

    /// <summary>Apply the collectible effect to the collector. Return false to skip consumption.</summary>
    protected abstract bool TryApply(CombatEntityHealth collector);
}
