using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Ghost trail and/or burst particles during dash (world-space, not HUD).</summary>
public sealed class DashMotionTrailEffect : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Ghost trail")]
    [FormerlySerializedAs("isEnabled")]
    [SerializeField] bool enableGhostTrail = true;
    [SerializeField] Transform visualRoot;
    [SerializeField, Min(0.01f)] float ghostInterval = 0.04f;
    [SerializeField, Min(0.01f)] float ghostFadeDuration = 0.18f;
    [SerializeField, Range(0.05f, 1f)] float ghostAlpha = 0.45f;
    [SerializeField] Color ghostTint = new Color(0.75f, 0.85f, 1f, 1f);

    [Header("Burst particles")]
    [SerializeField] bool playBurstParticles = true;
    [SerializeField] ParticleSystem dashBurst;
    [SerializeField] Transform burstAnchor;
    [SerializeField] bool orientBurstToDashDirection = true;
    [SerializeField] bool clearBurstOnStop;

    readonly List<GhostInstance> _ghosts = new List<GhostInstance>();
    Renderer[] _sourceRenderers;
    float _playTimeRemaining;
    float _spawnTimer;

    sealed class GhostInstance
    {
        public GameObject Root;
        public Renderer Renderer;
        public MaterialPropertyBlock Block;
        public int ColorPropertyId;
        public Color Baseline;
        public float TimeRemaining;
    }

    void Awake()
    {
        Transform root = visualRoot != null ? visualRoot : transform;
        _sourceRenderers = root.GetComponentsInChildren<Renderer>(true);
        PrepareDashBurstIdle();
    }

    void Update()
    {
        if (!enableGhostTrail)
            return;

        if (_playTimeRemaining > 0f)
        {
            _playTimeRemaining -= Time.deltaTime;
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                SpawnGhost();
                _spawnTimer = ghostInterval;
            }
        }

        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            GhostInstance g = _ghosts[i];
            g.TimeRemaining -= Time.deltaTime;
            if (g.TimeRemaining <= 0f)
            {
                if (g.Root != null)
                    Destroy(g.Root);
                _ghosts.RemoveAt(i);
                continue;
            }

            if (g.Renderer == null || g.ColorPropertyId < 0)
                continue;

            float t = 1f - (g.TimeRemaining / ghostFadeDuration);
            Color c = Color.Lerp(g.Baseline, ghostTint, t);
            c.a = Mathf.Lerp(ghostAlpha, 0f, t);
            g.Block.SetColor(g.ColorPropertyId, c);
            g.Renderer.SetPropertyBlock(g.Block);
        }
    }

    public void Play(float duration)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.forward;
        Play(forward, duration);
    }

    public void Play(Vector3 dashDirection, float duration)
    {
        if (duration <= 0f)
            return;

        if (enableGhostTrail)
        {
            _playTimeRemaining = duration;
            _spawnTimer = 0f;
            SpawnGhost();
        }

        PlayBurst(dashDirection);
    }

    public void Stop()
    {
        _playTimeRemaining = 0f;
        ClearAllGhosts();

        if (clearBurstOnStop && dashBurst != null)
            dashBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void PlayBurst(Vector3 dashDirection)
    {
        if (!playBurstParticles || dashBurst == null)
            return;

        Transform anchor = burstAnchor != null ? burstAnchor : visualRoot != null ? visualRoot : transform;
        if (orientBurstToDashDirection)
        {
            dashDirection.y = 0f;
            if (dashDirection.sqrMagnitude > 1e-6f)
                anchor.rotation = Quaternion.LookRotation(dashDirection.normalized, Vector3.up);
        }

        dashBurst.Clear(true);
        dashBurst.Play(true);
    }

    void PrepareDashBurstIdle()
    {
        if (dashBurst == null)
            return;

        ParticleSystem[] systems = dashBurst.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.playOnAwake = false;
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void ClearAllGhosts()
    {
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            if (_ghosts[i].Root != null)
                Destroy(_ghosts[i].Root);
        }
        _ghosts.Clear();
    }

    void SpawnGhost()
    {
        if (_sourceRenderers == null || _sourceRenderers.Length == 0)
            return;

        for (int i = 0; i < _sourceRenderers.Length; i++)
        {
            Renderer src = _sourceRenderers[i];
            if (src == null || !src.enabled || !src.gameObject.activeInHierarchy)
                continue;
            if (src is ParticleSystemRenderer || src is LineRenderer || src is TrailRenderer)
                continue;

            GameObject ghostGo = new GameObject("DashGhost");
            ghostGo.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            ghostGo.transform.localScale = src.transform.lossyScale;

            Renderer ghostRenderer = null;
            if (src is MeshRenderer meshSrc)
            {
                MeshFilter srcFilter = meshSrc.GetComponent<MeshFilter>();
                if (srcFilter == null || srcFilter.sharedMesh == null)
                {
                    Destroy(ghostGo);
                    continue;
                }
                ghostGo.AddComponent<MeshFilter>().sharedMesh = srcFilter.sharedMesh;
                ghostRenderer = ghostGo.AddComponent<MeshRenderer>();
                ghostRenderer.sharedMaterials = meshSrc.sharedMaterials;
            }
            else if (src is SkinnedMeshRenderer skinSrc)
            {
                var skinGhost = ghostGo.AddComponent<SkinnedMeshRenderer>();
                skinGhost.sharedMesh = skinSrc.sharedMesh;
                skinGhost.sharedMaterials = skinSrc.sharedMaterials;
                skinGhost.rootBone = skinSrc.rootBone;
                skinGhost.bones = skinSrc.bones;
                ghostRenderer = skinGhost;
            }
            else
            {
                Destroy(ghostGo);
                continue;
            }

            int propId = ResolveColorProperty(ghostRenderer.sharedMaterial);
            Color baseline = propId >= 0 ? ghostRenderer.sharedMaterial.GetColor(propId) : Color.white;

            _ghosts.Add(new GhostInstance
            {
                Root = ghostGo,
                Renderer = ghostRenderer,
                Block = new MaterialPropertyBlock(),
                ColorPropertyId = propId,
                Baseline = baseline,
                TimeRemaining = ghostFadeDuration,
            });
        }
    }

    static int ResolveColorProperty(Material mat)
    {
        if (mat == null)
            return -1;
        if (mat.HasProperty(BaseColorId))
            return BaseColorId;
        if (mat.HasProperty(ColorId))
            return ColorId;
        return -1;
    }
}
