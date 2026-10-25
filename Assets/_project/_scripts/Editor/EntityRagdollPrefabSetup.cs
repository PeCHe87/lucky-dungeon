#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors kinematic bone ragdolls + <see cref="EntityRagdoll"/> on enemy combat prefabs.
/// Auto-runs once after compile; also available via menu. Never touches the player prefab.
/// </summary>
[InitializeOnLoad]
public static class EntityRagdollPrefabSetup
{
    const string SetupDonePrefKey = "LuckyDungeon.EntityRagdollPrefabSetupDone.v1";

    static readonly string[] EnemyPrefabPaths =
    {
        "Assets/_project/_prefabs/entities/base_combat_entity.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab",
        "Assets/_project/_prefabs/entities/enemy_melee_patroller.prefab",
    };

    static EntityRagdollPrefabSetup()
    {
        EditorApplication.delayCall += TryAutoSetupOnce;
    }

    static void TryAutoSetupOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoSetupOnce;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        if (SetupAll())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    [MenuItem("Lucky Dungeon/Setup/Entity Ragdoll Prefabs")]
    public static void SetupFromMenu()
    {
        if (SetupAll())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    static bool SetupAll()
    {
        int updated = 0;
        for (int i = 0; i < EnemyPrefabPaths.Length; i++)
        {
            if (SetupPrefab(EnemyPrefabPaths[i]))
                updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[EntityRagdollPrefabSetup] Updated {updated}/{EnemyPrefabPaths.Length} enemy prefabs.");
        return updated > 0;
    }

    static bool SetupPrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogWarning($"[EntityRagdollPrefabSetup] Missing prefab: {path}");
            return false;
        }

        try
        {
            if (root.GetComponent<PlayerDeathHandler>() != null)
            {
                Debug.LogWarning($"[EntityRagdollPrefabSetup] Skipping player-like prefab: {path}");
                return false;
            }

            ClearExistingRagdollParts(root);
            BuildStanderRagdoll(root);

            var ragdoll = root.GetComponent<EntityRagdoll>();
            if (ragdoll == null)
                ragdoll = root.AddComponent<EntityRagdoll>();

            WireEntityRagdoll(ragdoll, root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ClearExistingRagdollParts(GameObject root)
    {
        var joints = root.GetComponentsInChildren<CharacterJoint>(true);
        for (int i = 0; i < joints.Length; i++)
            Object.DestroyImmediate(joints[i]);

        var bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null && bodies[i].gameObject != root)
                Object.DestroyImmediate(bodies[i]);
        }

        // Remove bone capsules we may have added previously (keep root CapsuleCollider).
        var capsules = root.GetComponentsInChildren<CapsuleCollider>(true);
        for (int i = 0; i < capsules.Length; i++)
        {
            if (capsules[i] != null && capsules[i].gameObject != root)
                Object.DestroyImmediate(capsules[i]);
        }
    }

    static void BuildStanderRagdoll(GameObject root)
    {
        Transform FindBone(string name)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }

        Transform pelvis = FindBone("Bip001 Pelvis");
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

        if (pelvis == null)
        {
            Debug.LogError($"[EntityRagdollPrefabSetup] No Bip001 Pelvis under {root.name}");
            return;
        }

        Rigidbody pelvisRb = AddBoneBody(pelvis, 3.5f, 0.14f, 0.35f, Vector3.right);
        Rigidbody spineRb = spine != null ? AddBoneBody(spine, 2.5f, 0.14f, 0.32f, Vector3.right) : null;
        Rigidbody headRb = head != null ? AddBoneBody(head, 1.2f, 0.12f, 0.22f, Vector3.forward) : null;
        Rigidbody lThighRb = lThigh != null ? AddBoneBody(lThigh, 2f, 0.1f, 0.4f, Vector3.forward) : null;
        Rigidbody lCalfRb = lCalf != null ? AddBoneBody(lCalf, 1.5f, 0.08f, 0.38f, Vector3.forward) : null;
        Rigidbody rThighRb = rThigh != null ? AddBoneBody(rThigh, 2f, 0.1f, 0.4f, Vector3.forward) : null;
        Rigidbody rCalfRb = rCalf != null ? AddBoneBody(rCalf, 1.5f, 0.08f, 0.38f, Vector3.forward) : null;
        Rigidbody lUpperRb = lUpper != null ? AddBoneBody(lUpper, 1.2f, 0.07f, 0.3f, Vector3.forward) : null;
        Rigidbody lForeRb = lFore != null ? AddBoneBody(lFore, 1f, 0.06f, 0.28f, Vector3.forward) : null;
        Rigidbody rUpperRb = rUpper != null ? AddBoneBody(rUpper, 1.2f, 0.07f, 0.3f, Vector3.forward) : null;
        Rigidbody rForeRb = rFore != null ? AddBoneBody(rFore, 1f, 0.06f, 0.28f, Vector3.forward) : null;

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
    }

    static Rigidbody AddBoneBody(Transform bone, float mass, float radius, float height, Vector3 capsuleDirection)
    {
        var rb = bone.gameObject.GetComponent<Rigidbody>();
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

        var capsule = bone.gameObject.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = DirectionIndex(capsuleDirection);
        capsule.radius = radius;
        capsule.height = Mathf.Max(height, radius * 2f);
        capsule.center = Vector3.zero;
        capsule.enabled = false;

        return rb;
    }

    static int DirectionIndex(Vector3 dir)
    {
        dir = dir.normalized;
        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);
        float az = Mathf.Abs(dir.z);
        if (ax >= ay && ax >= az) return 0;
        if (ay >= ax && ay >= az) return 1;
        return 2;
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
        var joint = body.gameObject.GetComponent<CharacterJoint>();
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

    static void WireEntityRagdoll(EntityRagdoll ragdoll, GameObject root)
    {
        var so = new SerializedObject(ragdoll);
        so.FindProperty("health").objectReferenceValue = root.GetComponent<CombatEntityHealth>();
        so.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>(true);
        so.FindProperty("agent").objectReferenceValue = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
        so.FindProperty("rootGameplayCollider").objectReferenceValue = root.GetComponent<CapsuleCollider>();

        var bodies = new List<Rigidbody>();
        var cols = new List<Collider>();
        Rigidbody pelvis = null;
        var allBodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < allBodies.Length; i++)
        {
            Rigidbody rb = allBodies[i];
            if (rb == null || rb.gameObject == root)
                continue;
            bodies.Add(rb);
            if (rb.name.IndexOf("Pelvis", System.StringComparison.OrdinalIgnoreCase) >= 0)
                pelvis = rb;
            var boneCols = rb.GetComponents<Collider>();
            for (int c = 0; c < boneCols.Length; c++)
                cols.Add(boneCols[c]);
        }

        if (pelvis == null && bodies.Count > 0)
            pelvis = bodies[0];

        so.FindProperty("impulseBody").objectReferenceValue = pelvis;

        SerializedProperty bodiesProp = so.FindProperty("ragdollBodies");
        bodiesProp.arraySize = bodies.Count;
        for (int i = 0; i < bodies.Count; i++)
            bodiesProp.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];

        SerializedProperty colsProp = so.FindProperty("ragdollColliders");
        colsProp.arraySize = cols.Count;
        for (int i = 0; i < cols.Count; i++)
            colsProp.GetArrayElementAtIndex(i).objectReferenceValue = cols[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ragdoll);
    }
}
#endif
