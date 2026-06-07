#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Wires NavMesh chase + alignment detection on <c>base_combat_entity_chaser</c> and adds
/// <see cref="CombatEntityHealth"/> to the player for alignment-based detection.
/// </summary>
[InitializeOnLoad]
public static class EntityChaserPrefabSetup
{
    const string ChaserPrefabPath = "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab";
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";

    static EntityChaserPrefabSetup()
    {
        EditorApplication.delayCall += EnsureChaserDependenciesOnLoad;
    }

    static void EnsureChaserDependenciesOnLoad()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureChaserDependenciesOnLoad;
            return;
        }

        EntityCombatControllerSetup.EnsureRunState();
        SetupChaserEntityPrefab();
    }

    [MenuItem("Tools/Entities/Setup Chaser Entity Prefab")]
    public static void SetupChaserEntityPrefabMenu()
    {
        SetupChaserEntityPrefab(applyDefaults: true);
    }

    [MenuItem("Tools/Entities/Setup Player Alignment For Chaser")]
    public static void SetupPlayerAlignmentMenu()
    {
        SetupPlayerAlignment();
    }

    [MenuItem("Tools/Entities/Setup All Chaser Dependencies")]
    public static void SetupAllMenu()
    {
        EntityCombatControllerSetup.EnsureRunState();
        SetupPlayerAlignment();
        SetupChaserEntityPrefab(applyDefaults: true);
    }

    public static void SetupChaserEntityPrefab(bool applyDefaults = false)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ChaserPrefabPath);
        try
        {
            bool changed = false;

            var agent = root.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = root.AddComponent<NavMeshAgent>();
                changed = true;
            }

            changed |= ConfigureNavMeshAgent(agent);

            var host = root.GetComponent<EntityNavBehaviorHost>();
            if (host == null)
            {
                host = root.AddComponent<EntityNavBehaviorHost>();
                changed = true;
            }

            var fov = root.GetComponent<FieldOfViewComponent>();
            if (fov == null)
            {
                fov = root.AddComponent<FieldOfViewComponent>();
                changed = true;
            }

            var chase = root.GetComponent<NavMeshDetectChaseBehavior>();
            if (chase == null)
            {
                chase = root.AddComponent<NavMeshDetectChaseBehavior>();
                changed = true;
            }

            var finder = root.GetComponent<EntityAlignmentTargetFinder>();
            bool finderAdded = false;
            if (finder == null)
            {
                finder = root.AddComponent<EntityAlignmentTargetFinder>();
                finderAdded = true;
                changed = true;
            }

            var locomotionAnimator = root.GetComponent<EntityNavLocomotionAnimator>();
            if (locomotionAnimator == null)
            {
                locomotionAnimator = root.AddComponent<EntityNavLocomotionAnimator>();
                changed = true;
            }

            var stateMachine = root.GetComponent<AIStateMachine>();
            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
                changed = true;
            }

            changed |= SetReferenceIfNull(locomotionAnimator, "agent", agent);
            changed |= SetReferenceIfNull(locomotionAnimator, "animator", animator);
            changed |= SetReferenceIfNull(locomotionAnimator, "stateMachine", stateMachine);

            if (applyDefaults)
            {
                changed |= SetSerializedReference(host, "activeBehavior", chase);
                changed |= SetFloat(host, "warpSearchRadius", 4f);
                changed |= SetBool(host, "warpToNavMeshOnStart", true);

                changed |= SetSerializedReference(fov, "moveRoot", root.transform);
                changed |= SetFloat(fov, "detectionRadius", 8f);
                changed |= SetFloat(fov, "viewAngle", 360f);

                changed |= SetSerializedReference(chase, "fieldOfView", fov);
                changed |= SetFloat(chase, "arrivalRadius", 1f);
                changed |= SetFloat(chase, "pauseDuration", 0.5f);
                changed |= SetFloat(chase, "samplePositionRadius", 2f);
                changed |= SetBool(chase, "debugLog", false);
                changed |= SetBool(chase, "logDistanceToTargetWhileSearching", false);

                changed |= SetSerializedReference(finder, "fieldOfView", fov);
                changed |= SetEnum(finder, "targetAlignment", (int)EntityAlignment.Ally);
                changed |= SetLayerMask(finder, "scanLayers", LayerMask.GetMask("Entity", "Player"));
                changed |= SetBool(finder, "requireVisionCone", false);
                changed |= SetFloat(finder, "scanRate", 5f);
                changed |= SetFloat(finder, "lossRadiusMultiplier", 1.2f);
            }
            else
            {
                changed |= SetReferenceIfNull(host, "activeBehavior", chase);
                changed |= SetReferenceIfNull(fov, "moveRoot", root.transform);
                changed |= SetReferenceIfNull(chase, "fieldOfView", fov);
                changed |= SetReferenceIfNull(finder, "fieldOfView", fov);
                if (GetLayerMask(finder, "scanLayers") == 0)
                    changed |= SetLayerMask(finder, "scanLayers", LayerMask.GetMask("Entity", "Player"));
                if (finderAdded)
                    changed |= SetEnum(finder, "targetAlignment", (int)EntityAlignment.Ally);
            }

            if (!changed)
            {
                Debug.Log("[EntityChaserPrefabSetup] No changes needed for " + ChaserPrefabPath);
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, ChaserPrefabPath);
            Debug.Log("[EntityChaserPrefabSetup] Updated " + ChaserPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void SetupPlayerAlignment()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var health = root.GetComponent<CombatEntityHealth>();
            bool changed = false;

            if (health == null)
            {
                health = root.AddComponent<CombatEntityHealth>();
                changed = true;
            }

            changed |= SetFloat(health, "maxHitPoints", 100f);
            changed |= SetEnum(health, "alignment", (int)EntityAlignment.Ally);
            changed |= SetSerializedReference(health, "stateMachine", null);
            changed |= SetSerializedReference(health, "takeDamageState", null);
            changed |= SetSerializedReference(health, "dieState", null);
            changed |= SetSerializedReference(health, "damageNumberAnchor", null);
            changed |= SetFloat(health, "damageNumberHeightOffset", 1.5f);

            if (!changed)
            {
                Debug.Log("[EntityChaserPrefabSetup] No changes needed for " + PlayerPrefabPath);
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[EntityChaserPrefabSetup] Updated " + PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool ConfigureNavMeshAgent(NavMeshAgent agent)
    {
        if (agent == null)
            return false;

        bool changed = false;
        changed |= SetAgentFloat(agent, "m_Radius", 0.5f);
        changed |= SetAgentFloat(agent, "m_Speed", 6f);
        changed |= SetAgentFloat(agent, "m_Acceleration", 12f);
        changed |= SetAgentFloat(agent, "m_AngularSpeed", 720f);
        changed |= SetAgentFloat(agent, "m_StoppingDistance", 0.5f);
        changed |= SetAgentFloat(agent, "m_Height", 2f);
        changed |= SetAgentInt(agent, "m_AvoidancePriority", 50);
        return changed;
    }

    static bool SetAgentFloat(NavMeshAgent agent, string propertyName, float value)
    {
        var so = new SerializedObject(agent);
        var prop = so.FindProperty(propertyName);
        if (prop == null || Mathf.Approximately(prop.floatValue, value))
            return false;
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool SetAgentInt(NavMeshAgent agent, string propertyName, int value)
    {
        var so = new SerializedObject(agent);
        var prop = so.FindProperty(propertyName);
        if (prop == null || prop.intValue == value)
            return false;
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool SetSerializedReference(Object target, string propertyName, Object value)
    {
        if (target == null)
            return false;

        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || prop.objectReferenceValue == value)
            return false;

        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool SetFloat(Object target, string propertyName, float value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || Mathf.Approximately(prop.floatValue, value))
            return false;
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool SetBool(Object target, string propertyName, bool value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || prop.boolValue == value)
            return false;
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool SetEnum(Object target, string propertyName, int value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || prop.enumValueIndex == value)
            return false;
        prop.enumValueIndex = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool SetLayerMask(Object target, string propertyName, int maskValue)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || prop.intValue == maskValue)
            return false;
        prop.intValue = maskValue;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static int GetLayerMask(Object target, string propertyName)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.intValue : 0;
    }

    static bool SetReferenceIfNull(Object target, string propertyName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || prop.objectReferenceValue != null)
            return false;
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool SetFloatIfDefault(Object target, string propertyName, float value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || !Mathf.Approximately(prop.floatValue, 0f))
            return false;
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool SetBoolIfUnset(Object target, string propertyName, bool value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null)
            return false;
        if (propertyName == "warpToNavMeshOnStart" && prop.boolValue)
            return false;
        if (prop.boolValue == value)
            return false;
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
#endif
