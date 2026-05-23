using UnityEngine;

/// <summary>
/// Subscribes to <see cref="BaseDestructibleObject.Damaged"/> and applies a short tint blink via
/// <see cref="MaterialPropertyBlock"/> on child renderers (URP Lit: <c>_BaseColor</c>, fallback <c>_Color</c>).
/// Optionally replays an attached hit-damage <see cref="ParticleSystem"/> when enabled.
/// </summary>
public sealed class DestructibleHitFlash : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] Transform rendererSearchRoot;
    [Tooltip("If empty, all renderers under Renderer Search Root (or this transform) are used.")]
    [SerializeField] Renderer[] renderers;
    [SerializeField, Min(0.01f)] float flashDuration = 0.15f;
    [SerializeField, Min(0f)] float blinksPerSecond = 12f;
    [SerializeField] Color flashColor = Color.white;

    [Header("Hit damage VFX")]
    [SerializeField] bool playHitVfx;
    [SerializeField] ParticleSystem hitDamageVfx;

    BaseDestructibleObject _destructible;
    FlashRenderer[] _flashRenderers;
    float _flashTimeRemaining;
    bool _wasFlashing;

    sealed class FlashRenderer
    {
        public Renderer Renderer;
        public MaterialPropertyBlock Block;
        public MaterialFlashData[] Materials;
    }

    struct MaterialFlashData
    {
        public int ColorPropertyId;
        public Color Baseline;

        public readonly bool IsValid => ColorPropertyId >= 0;
    }

    void Awake()
    {
        _destructible = GetComponent<BaseDestructibleObject>();
        BuildFlashTargets();
        PrepareHitVfxIdle();
    }

    void OnEnable()
    {
        if (_destructible != null)
            _destructible.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (_destructible != null)
            _destructible.Damaged -= OnDamaged;
        _flashTimeRemaining = 0f;
        _wasFlashing = false;
        ClearAllPropertyBlocks();
    }

    void OnDamaged(float _)
    {
        _flashTimeRemaining = flashDuration;
        PlayHitVfx();
    }

    void PlayHitVfx()
    {
        if (!playHitVfx || hitDamageVfx == null)
            return;

        hitDamageVfx.Clear(true);
        hitDamageVfx.Play(true);
    }

    void PrepareHitVfxIdle()
    {
        if (hitDamageVfx == null)
            return;

        ParticleSystem[] systems = hitDamageVfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.playOnAwake = false;
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void LateUpdate()
    {
        if (_flashRenderers == null || _flashRenderers.Length == 0)
            return;

        if (_flashTimeRemaining <= 0f)
        {
            if (_wasFlashing)
            {
                ClearAllPropertyBlocks();
                _wasFlashing = false;
            }
            return;
        }

        _wasFlashing = true;
        _flashTimeRemaining -= Time.deltaTime;
        float blink01 = ComputeBlink01();

        for (int r = 0; r < _flashRenderers.Length; r++)
        {
            FlashRenderer fr = _flashRenderers[r];
            if (fr.Renderer == null || fr.Materials == null)
                continue;

            for (int m = 0; m < fr.Materials.Length; m++)
            {
                MaterialFlashData data = fr.Materials[m];
                if (!data.IsValid)
                    continue;

                Color c = Color.LerpUnclamped(data.Baseline, flashColor, blink01);
                fr.Block.SetColor(data.ColorPropertyId, c);
                fr.Renderer.SetPropertyBlock(fr.Block, m);
            }
        }

        if (_flashTimeRemaining <= 0f)
        {
            ClearAllPropertyBlocks();
            _wasFlashing = false;
        }
    }

    float ComputeBlink01()
    {
        if (blinksPerSecond <= 0f)
            return 1f;

        float wave = Mathf.Sin(Time.time * blinksPerSecond * Mathf.PI * 2f);
        return (wave + 1f) * 0.5f;
    }

    void BuildFlashTargets()
    {
        Renderer[] resolved = renderers;
        if (resolved == null || resolved.Length == 0)
        {
            Transform root = rendererSearchRoot != null ? rendererSearchRoot : transform;
            resolved = root.GetComponentsInChildren<Renderer>(true);
        }

        if (resolved == null || resolved.Length == 0)
        {
            _flashRenderers = System.Array.Empty<FlashRenderer>();
            return;
        }

        var list = new System.Collections.Generic.List<FlashRenderer>(resolved.Length);
        for (int i = 0; i < resolved.Length; i++)
        {
            Renderer renderer = resolved[i];
            if (renderer == null)
                continue;

            Material[] shared = renderer.sharedMaterials;
            if (shared == null || shared.Length == 0)
                continue;

            var mats = new MaterialFlashData[shared.Length];
            bool any = false;
            for (int m = 0; m < shared.Length; m++)
            {
                Material mat = shared[m];
                if (mat == null)
                {
                    mats[m] = new MaterialFlashData { ColorPropertyId = -1 };
                    continue;
                }

                int propId = -1;
                if (mat.HasProperty(BaseColorId))
                    propId = BaseColorId;
                else if (mat.HasProperty(ColorId))
                    propId = ColorId;

                if (propId < 0)
                {
                    mats[m] = new MaterialFlashData { ColorPropertyId = -1 };
                    continue;
                }

                mats[m] = new MaterialFlashData
                {
                    ColorPropertyId = propId,
                    Baseline = mat.GetColor(propId),
                };
                any = true;
            }

            if (!any)
                continue;

            list.Add(new FlashRenderer
            {
                Renderer = renderer,
                Block = new MaterialPropertyBlock(),
                Materials = mats,
            });
        }

        _flashRenderers = list.ToArray();
    }

    void ClearAllPropertyBlocks()
    {
        if (_flashRenderers == null)
            return;

        for (int r = 0; r < _flashRenderers.Length; r++)
        {
            FlashRenderer fr = _flashRenderers[r];
            if (fr.Renderer == null || fr.Materials == null)
                continue;

            for (int m = 0; m < fr.Materials.Length; m++)
            {
                if (!fr.Materials[m].IsValid)
                    continue;
                fr.Renderer.SetPropertyBlock(null, m);
            }
        }
    }
}
