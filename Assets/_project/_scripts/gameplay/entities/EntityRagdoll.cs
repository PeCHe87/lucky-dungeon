using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// On death, builds a Stander/Bip001 bone ragdoll and applies the killing-blow impulse.
/// No Rigidbodies/joints exist on bones while alive (avoids skinned-mesh culling bugs).
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

    [Header("Impulse")]
    [SerializeField, Range(0f, 1f)] float upwardBias = DefaultUpwardBias;
    [Tooltip("Fraction of death push velocity also applied to non-pelvis ragdoll bones.")]
    [SerializeField, Range(0f, 1f)] float secondaryBodyVelocityScale = 0.55f;

    Rigidbody _impulseBody;
    Rigidbody[] _ragdollBodies;
    Collider[] _ragdollColliders;

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

        // Strip any leftover prefab-authored bone physics so chase animation stays clean.
        StripPreauthoredBonePhysics();
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

        if (!TryBuildStanderRagdoll())
            return;

        // Drop leftover gameplay colliders (weapon props, etc.); bone colliders stay on.
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

        Physics.SyncTransforms();
    }

    bool IsRagdollCollider(Collider col)
    {
        if (_ragdollColliders == null)
            return false;
        for (int i = 0; i < _ragdollColliders.Length; i++)
        {
            if (_ragdollColliders[i] == col)
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

        _pendingDeathVelocity = dir * force;
        _pendingDeathImpulse = true;
    }

    void ApplyDeathVelocity(Vector3 velocity)
    {
        if (velocity.sqrMagnitude < 1e-8f)
            return;

        if (_impulseBody != null && !_impulseBody.isKinematic)
            _impulseBody.linearVelocity = velocity;

        if (_ragdollBodies == null || secondaryBodyVelocityScale <= 0f)
            return;

        Vector3 secondary = velocity * secondaryBodyVelocityScale;
        for (int i = 0; i < _ragdollBodies.Length; i++)
        {
            Rigidbody rb = _ragdollBodies[i];
            if (rb == null || rb == _impulseBody || rb.isKinematic)
                continue;
            rb.linearVelocity = secondary;
        }
    }

    void StripPreauthoredBonePhysics()
    {
        var joints = GetComponentsInChildren<CharacterJoint>(true);
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] != null)
                DestroyImmediate(joints[i]);
        }

        var bodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody rb = bodies[i];
            if (rb == null || rb.gameObject == gameObject)
                continue;
            DestroyImmediate(rb);
        }

        var capsules = GetComponentsInChildren<CapsuleCollider>(true);
        for (int i = 0; i < capsules.Length; i++)
        {
            CapsuleCollider capsule = capsules[i];
            if (capsule == null || capsule.gameObject == gameObject)
                continue;
            if (rootGameplayCollider != null && capsule == rootGameplayCollider)
                continue;
            DestroyImmediate(capsule);
        }

        _ragdollBodies = null;
        _ragdollColliders = null;
        _impulseBody = null;
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

        _impulseBody = pelvisRb;
        _ragdollBodies = CollectExistingChildBodies();
        _ragdollColliders = CollectRagdollColliders();
        return _ragdollBodies.Length > 0;
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
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        var capsule = bone.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = capsuleDirection;
        capsule.radius = radius;
        capsule.height = Mathf.Max(height, radius * 2f);
        capsule.center = Vector3.zero;
        capsule.enabled = true;

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
        if (_ragdollBodies == null || _ragdollBodies.Length == 0)
            return System.Array.Empty<Collider>();

        int count = 0;
        for (int i = 0; i < _ragdollBodies.Length; i++)
        {
            if (_ragdollBodies[i] == null)
                continue;
            count += _ragdollBodies[i].GetComponents<Collider>().Length;
        }

        var result = new Collider[count];
        int w = 0;
        for (int i = 0; i < _ragdollBodies.Length; i++)
        {
            if (_ragdollBodies[i] == null)
                continue;
            var cols = _ragdollBodies[i].GetComponents<Collider>();
            for (int c = 0; c < cols.Length; c++)
                result[w++] = cols[c];
        }

        return result;
    }
}
