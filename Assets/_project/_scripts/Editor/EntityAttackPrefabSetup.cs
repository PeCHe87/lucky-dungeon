#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Wires weapon + AI attack components on entity prefabs.
/// </summary>
public static class EntityAttackPrefabSetup
{
    const string AttackFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Attack1.FBX";

    public static bool SetupEntityAttackComponents(GameObject root, bool applyDefaults = true)
    {
        if (root == null)
            return false;

        bool changed = false;
        Animator animator = root.GetComponentInChildren<Animator>(true);

        var weaponHolder = root.GetComponent<WeaponHolder>();
        if (weaponHolder == null)
        {
            weaponHolder = root.AddComponent<WeaponHolder>();
            changed = true;
        }

        MeleeWeapon meleeWeapon = root.GetComponentInChildren<MeleeWeapon>(true);
        if (meleeWeapon == null)
        {
            var weaponGo = new GameObject("melee_weapon");
            weaponGo.transform.SetParent(root.transform, false);
            meleeWeapon = weaponGo.AddComponent<MeleeWeapon>();
            changed = true;
        }

        if (applyDefaults)
        {
            changed |= SetFloat(meleeWeapon, "damage", 10f);
            changed |= SetFloat(meleeWeapon, "cooldown", 0.35f);
            changed |= SetFloat(meleeWeapon, "range", 2.5f);
            changed |= SetFloat(meleeWeapon, "moveForwardDistance", 0f);
            changed |= SetBool(meleeWeapon, "enableApproachLunge", false);
        }

        changed |= SetSerializedReference(weaponHolder, "startingWeapon", meleeWeapon);
        changed |= SetSerializedReference(weaponHolder, "meleeWeapon", meleeWeapon);
        changed |= SetBool(weaponHolder, "logEquippedWeaponChanges", false);

        var attackController = root.GetComponent<EntityAttackController>();
        if (attackController == null)
        {
            attackController = root.AddComponent<EntityAttackController>();
            changed = true;
        }

        var attackAnimator = root.GetComponent<EntityAttackAnimator>();
        if (attackAnimator == null)
        {
            attackAnimator = root.AddComponent<EntityAttackAnimator>();
            changed = true;
        }

        changed |= SetSerializedReference(attackController, "weaponHolder", weaponHolder);
        changed |= SetSerializedReference(attackController, "attackAnimator", attackAnimator);
        changed |= SetSerializedReference(attackController, "facingRoot", root.transform);

        changed |= SetSerializedReference(attackAnimator, "attackController", attackController);
        changed |= SetSerializedReference(attackAnimator, "weaponHolder", weaponHolder);
        changed |= SetSerializedReference(attackAnimator, "animator", animator);
        changed |= SetString(attackAnimator, "attackStateName", "Attack1");

        if (animator != null)
        {
            var hitReceiver = animator.GetComponent<MeleeAttackAnimationEventReceiver>();
            if (hitReceiver == null)
            {
                hitReceiver = animator.gameObject.AddComponent<MeleeAttackAnimationEventReceiver>();
                changed = true;
            }

            changed |= SetSerializedReference(hitReceiver, "meleeWeapon", meleeWeapon);
            changed |= SetSerializedReference(hitReceiver, "weaponHolder", weaponHolder);
        }

        var detectChase = root.GetComponent<NavMeshDetectChaseBehavior>();
        if (detectChase != null)
            changed |= SetSerializedReference(detectChase, "attackController", attackController);

        var patrolChase = root.GetComponent<NavMeshPatrolUntilChaseBehavior>();
        if (patrolChase != null)
            changed |= SetSerializedReference(patrolChase, "attackController", attackController);

        var locomotionAnimator = root.GetComponent<EntityNavLocomotionAnimator>();
        if (locomotionAnimator != null)
            changed |= SetSerializedReference(locomotionAnimator, "attackController", attackController);

        return changed;
    }

    static bool SetString(Object target, string propertyName, string value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || prop.stringValue == value)
            return false;
        prop.stringValue = value;
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

    public static AnimationClip LoadAttackClip()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(AttackFbxPath).OfType<AnimationClip>().ToArray();
        if (clips.Length == 0)
            return null;

        AnimationClip named = clips.FirstOrDefault(c => c.name == "Fighter_Attack1");
        return named ?? clips.FirstOrDefault(c => !c.name.StartsWith("__"));
    }
}
#endif
