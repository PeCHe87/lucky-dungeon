using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Swaps an enemy from animated locomotion to a bone ragdoll on death,
/// then applies the killing-blow impulse from <see cref="CombatEntityHealth"/>.
/// Builds a Stander/Bip001 ragdoll at runtime when none is authored on the prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class EntityRagdoll : MonoBehaviour
{
    const float DefaultUpwardBias = 0.25f;

    [Header("Refs")]
    [SerializeField] CombatEntityHealth health;
    [SerializeField] Animator animator;
    [SerializeField] NavMeshAgent agent;
    [SerializeField] Collider rootGameplayCollider;
    [SerializeField] Rigidbody impulseBody;

    [Header("Ragdoll parts")]
    [Tooltip("All rigidbodies that make up the ragdoll. Left empty = build/collect at Awake.")]
    [SerializeField] Rigidbody[] ragdollBodies;
    [Tooltip("Bone colliders enabled only while ragdolling. Left empty = colliders on ragdollBodies.")]
    [SerializeField] Collider[] ragdollColliders;

    [Header("Impulse")]
    [SerializeField, Range(0f, 1f)] float upwardBias = DefaultUpwardBias;
    [Tooltip("Fraction of death push velocity also applied to non-pelvis ragdoll bones.")]
    [SerializeField, Range(0f, 1f)] float secondaryBodyVelocityScale = 0.55f;

    bool _active;
    bool _pendingDeathImpulse;
    Vector3 _pendingDeathVelocity;

    public bool IsActive => _active;

    void Awake()
    {
        if (health == null)
            health = GetComponent<CombatEntityHealth>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (rootGameplayCollider == null)
            rootGameplayCollider = GetComponent<CapsuleCollider>();

        EnsureRagdollParts();
        SetRagdollEnabled(false);
    }

    void OnEnable()
    {
        if (health != null)
            health.Died += OnDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.Died -= OnDied;
    }

    void OnDied()
    {
        if (_active)
            return;

        _active = true;
        ActivateRagdoll();
        QueueDeathImpulse();
    }

    void FixedUpdate()
    {
        if (!_pendingDeathImpulse)
            return;

        _pendingDeathImpulse = false;
        ApplyDeathVelocity(_pendingDeathVelocity);
    }

    void ActivateRagdoll()
    {
        if (TryGetComponent(out PushbackReceiver pushback))
            pushback.CancelPushback();

        if (animator != null)
        {
            animator.Update(0f);
            animator.enabled = false;
        }

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.enabled = false;
        }

        if (rootGameplayCollider != null)
            rootGameplayCollider.enabled = false;

        // Drop non-ragdoll colliders (weapon props, etc.) so only bone bodies collide.
        var allCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < allCols.Length; i++)
        {
            Collider col = allCols[i];
            if (col == null)
                continue;
            if (IsRagdollCollider(col))
                continue;
            col.enabled = false;
        }

        SetRagdollEnabled(true);
        Physics.SyncTransforms();
    }

    bool IsRagdollCollider(Collider col)
    {
        if (ragdollColliders == null)
            return false;
        for (int i = 0; i < ragdollColliders.Length; i++)
        {
            if (ragdollColliders[i] == col)
                return true;
        }
        return false;
    }

    void QueueDeathImpulse()
    {
        _pendingDeathImpulse = false;
        _pendingDeathVelocity = Vector3.zero;

        if (health == null)
            return;

        float force = health.LastDeathPushForce;
        if (force <= 0f)
            return;

        Vector3 dir = health.LastDeathPushDirection;
        if (dir.sqrMagnitude < 1e-8f)
            dir = transform.forward;
        else
            dir.Normalize();

        if (upwardBias > 0f)
        {
            dir += Vector3.up * upwardBias;
            dir.Normalize();
        }

        // deathPushForce is treated as VelocityChange (m/s): reliable after kinematic→dynamic.
        _pendingDeathVelocity = dir * force;
        _pendingDeathImpulse = true;
    }

    void ApplyDeathVelocity(Vector3 velocity)
    {
        if (velocity.sqrMagnitude < 1e-8f)
            return;

        if (impulseBody != null && !impulseBody.isKinematic)
            impulseBody.linearVelocity = velocity;

        if (ragdollBodies == null || secondaryBodyVelocityScale <= 0f)
            return;

        Vector3 secondary = velocity * secondaryBodyVelocityScale;
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            Rigidbody rb = ragdollBodies[i];
            if (rb == null || rb == impulseBody || rb.isKinematic)
                continue;
            rb.linearVelocity = secondary;
        }
    }

    void SetRagdollEnabled(bool enabled)
    {
        if (ragdollColliders != null)
        {
            for (int i = 0; i < ragdollColliders.Length; i++)
            {
                if (ragdollColliders[i] != null)
                    ragdollColliders[i].enabled = enabled;
            }
        }

        if (ragdollBodies == null)
            return;

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            Rigidbody rb = ragdollBodies[i];
            if (rb == null)
                continue;

            rb.detectCollisions = enabled;
            rb.isKinematic = !enabled;
            if (enabled)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    void EnsureRagdollParts()
    {
        if (ragdollBodies == null || ragdollBodies.Length == 0)
        {
            if (!TryBuildStanderRagdoll())
                ragdollBodies = CollectExistingChildBodies();
        }

        if (impulseBody == null)
            impulseBody = FindPelvisBody();

        if (ragdollColliders == null || ragdollColliders.Length == 0)
            ragdollColliders = CollectRagdollColliders();
    }

    bool TryBuildStanderRagdoll()
    {
        Transform pelvis = FindBone("Bip001 Pelvis");
        if (pelvis == null)
            return false;

        Transform spine = FindBone("Bip001 Spine1") ?? FindBone("Bip001 Spine");
        Transform head = FindBone("Bip001 Head");
        Transform lThigh = FindBone("Bip001 L Thigh");
        Transform lCalf = FindBone("Bip001 L Calf");
        Transform rThigh = FindBone("Bip001 R Thigh");
        Transform rCalf = FindBone("Bip001 R Calf");
        Transform lUpper = FindBone("Bip001 L UpperArm");
        Transform lFore = FindBone("Bip001 L Forearm");
        Transform rUpper = FindBone("Bip001 R UpperArm");
        Transform rFore = FindBone("Bip001 R Forearm");

        Rigidbody pelvisRb = AddBoneBody(pelvis, 3.5f, 0.14f, 0.35f, 0);
        Rigidbody spineRb = spine != null ? AddBoneBody(spine, 2.5f, 0.14f, 0.32f, 0) : null;
        Rigidbody headRb = head != null ? AddBoneBody(head, 1.2f, 0.12f, 0.22f, 2) : null;
        Rigidbody lThighRb = lThigh != null ? AddBoneBody(lThigh, 2f, 0.1f, 0.4f, 2) : null;
        Rigidbody lCalfRb = lCalf != null ? AddBoneBody(lCalf, 1.5f, 0.08f, 0.38f, 2) : null;
        Rigidbody rThighRb = rThigh != null ? AddBoneBody(rThigh, 2f, 0.1f, 0.4f, 2) : null;
        Rigidbody rCalfRb = rCalf != null ? AddBoneBody(rCalf, 1.5f, 0.08f, 0.38f, 2) : null;
        Rigidbody lUpperRb = lUpper != null ? AddBoneBody(lUpper, 1.2f, 0.07f, 0.3f, 2) : null;
        Rigidbody lForeRb = lFore != null ? AddBoneBody(lFore, 1f, 0.06f, 0.28f, 2) : null;
        Rigidbody rUpperRb = rUpper != null ? AddBoneBody(rUpper, 1.2f, 0.07f, 0.3f, 2) : null;
        Rigidbody rForeRb = rFore != null ? AddBoneBody(rFore, 1f, 0.06f, 0.28f, 2) : null;

        if (spineRb != null)
            Connect(spineRb, pelvisRb, 20f, -20f, 20f, 20f, 30f);
        if (headRb != null && spineRb != null)
            Connect(headRb, spineRb, 15f, -30f, 30f, 30f, 40f);
        if (lThighRb != null)
            Connect(lThighRb, pelvisRb, 20f, -10f, 10f, 40f, 60f);
        if (lCalfRb != null && lThighRb != null)
            Connect(lCalfRb, lThighRb, 20f, 0f, 0f, 0f, 80f);
        if (rThighRb != null)
            Connect(rThighRb, pelvisRb, 20f, -10f, 10f, 40f, 60f);
        if (rCalfRb != null && rThighRb != null)
            Connect(rCalfRb, rThighRb, 20f, 0f, 0f, 0f, 80f);
        if (lUpperRb != null && spineRb != null)
            Connect(lUpperRb, spineRb, 20f, -40f, 40f, 60f, 70f);
        if (lForeRb != null && lUpperRb != null)
            Connect(lForeRb, lUpperRb, 20f, 0f, 0f, 0f, 80f);
        if (rUpperRb != null && spineRb != null)
            Connect(rUpperRb, spineRb, 20f, -40f, 40f, 60f, 70f);
        if (rForeRb != null && rUpperRb != null)
            Connect(rForeRb, rUpperRb, 20f, 0f, 0f, 0f, 80f);

        ragdollBodies = CollectExistingChildBodies();
        impulseBody = pelvisRb;
        return ragdollBodies.Length > 0;
    }

    Transform FindBone(string boneName)
    {
        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == boneName)
                return all[i];
        }
        return null;
    }

    static Rigidbody AddBoneBody(Transform bone, float mass, float radius, float height, int capsuleDirection)
    {
        var rb = bone.GetComponent<Rigidbody>();
        if (rb == null)
            rb = bone.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.isKinematic = true;
        rb.useGravity = true;
        rb.detectCollisions = false;

        var capsule = bone.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = capsuleDirection;
        capsule.radius = radius;
        capsule.height = Mathf.Max(height, radius * 2f);
        capsule.center = Vector3.zero;
        capsule.enabled = false;

        return rb;
    }

    static void Connect(
        Rigidbody body,
        Rigidbody connected,
        float spring,
        float lowTwist,
        float highTwist,
        float swing1,
        float swing2)
    {
        var joint = body.GetComponent<CharacterJoint>();
        if (joint == null)
            joint = body.gameObject.AddComponent<CharacterJoint>();
        joint.connectedBody = connected;
        joint.enableCollision = false;
        joint.enablePreprocessing = false;
        var springCfg = new SoftJointLimitSpring { spring = spring, damper = 1f };
        joint.twistLimitSpring = springCfg;
        joint.swingLimitSpring = springCfg;
        joint.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
        joint.highTwistLimit = new SoftJointLimit { limit = highTwist };
        joint.swing1Limit = new SoftJointLimit { limit = swing1 };
        joint.swing2Limit = new SoftJointLimit { limit = swing2 };
    }

    Rigidbody[] CollectExistingChildBodies()
    {
        var all = GetComponentsInChildren<Rigidbody>(true);
        if (all == null || all.Length == 0)
            return System.Array.Empty<Rigidbody>();

        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject != gameObject)
                count++;
        }

        var result = new Rigidbody[count];
        int w = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject != gameObject)
                result[w++] = all[i];
        }

        return result;
    }

    Collider[] CollectRagdollColliders()
    {
        if (ragdollBodies == null || ragdollBodies.Length == 0)
            return System.Array.Empty<Collider>();

        int count = 0;
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            if (ragdollBodies[i] == null)
                continue;
            count += ragdollBodies[i].GetComponents<Collider>().Length;
        }

        var result = new Collider[count];
        int w = 0;
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            if (ragdollBodies[i] == null)
                continue;
            var cols = ragdollBodies[i].GetComponents<Collider>();
            for (int c = 0; c < cols.Length; c++)
                result[w++] = cols[c];
        }

        return result;
    }

    Rigidbody FindPelvisBody()
    {
        if (ragdollBodies == null)
            return null;

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            Rigidbody rb = ragdollBodies[i];
            if (rb == null)
                continue;
            string n = rb.name;
            if (n.IndexOf("Pelvis", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Hips", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return rb;
        }

        return ragdollBodies.Length > 0 ? ragdollBodies[0] : null;
    }
}