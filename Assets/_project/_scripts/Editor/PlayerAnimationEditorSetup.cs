#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class PlayerAnimationEditorSetup
{
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string LocomotionControllerPath = "Assets/_project/_animation/PlayerLocomotion.controller";
    const string BowControllerPath = PlayerBowAnimationEditorSetup.BowControllerPath;
    const string BowProfilePath = PlayerBowAnimationEditorSetup.BowProfilePath;

    static PlayerAnimationEditorSetup()
    {
        EditorApplication.delayCall += RunOnceDelayed;
    }

    static void RunOnceDelayed()
    {
        PlayerEntityStateAnimationProfile.EnsureDefaultAssetExists();
        PlayerBowAnimationEditorSetup.EnsureBowAssetsExist();
        WirePlayerPrefab();
    }

    static void WirePlayerPrefab()
    {
        var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefabRoot == null)
            return;

        var locomotion = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LocomotionControllerPath);
        var bowController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BowControllerPath);
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(
            PlayerEntityStateAnimationProfile.DefaultAssetPath);
        var bowProfile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(BowProfilePath);

        bool dirty = false;

        var entityState = prefabRoot.GetComponent<PlayerEntityState>();
        if (entityState == null)
        {
            entityState = prefabRoot.AddComponent<PlayerEntityState>();
            dirty = true;
        }

        var stateAnimator = prefabRoot.GetComponent<PlayerEntityStateAnimator>();
        if (stateAnimator == null)
        {
            stateAnimator = prefabRoot.AddComponent<PlayerEntityStateAnimator>();
            dirty = true;
        }

        var attackController = prefabRoot.GetComponent<PlayerAttackController>();
        var weaponHolder = prefabRoot.GetComponent<WeaponHolder>();
        var nearestTargetQuery = prefabRoot.GetComponent<NearestTargetQuery>();
        var meleeWeapon = prefabRoot.GetComponentInChildren<MeleeWeapon>(true);
        var rangedWeapon = prefabRoot.GetComponentInChildren<RangedWeapon>(true);
        var movement = prefabRoot.GetComponent<TopDownCharacterMovement>();
        var animator = prefabRoot.GetComponentInChildren<Animator>(true);
        var health = prefabRoot.GetComponent<CombatEntityHealth>();
        var invulnerability = prefabRoot.GetComponent<DamageInvulnerability>();
        var dashController = prefabRoot.GetComponent<DashJoystickDoubleTapController>();
        if (prefabRoot.GetComponent<PushbackReceiver>() == null)
        {
            var receiver = prefabRoot.AddComponent<PushbackReceiver>();
            var receiverSo = new SerializedObject(receiver);
            receiverSo.FindProperty("pushbackResistance").floatValue = 0.2f;
            receiverSo.ApplyModifiedPropertiesWithoutUndo();
            dirty = true;
        }
        if (animator != null)
        {
            if (animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
                dirty = true;
            }

            if (locomotion != null && animator.runtimeAnimatorController != locomotion)
            {
                animator.runtimeAnimatorController = locomotion;
                dirty = true;
            }

        }

        if (meleeWeapon != null)
        {
            var meleeSo = new SerializedObject(meleeWeapon);
            if (locomotion != null
                && meleeSo.FindProperty("animatorController").objectReferenceValue != locomotion)
            {
                meleeSo.FindProperty("animatorController").objectReferenceValue = locomotion;
                dirty = true;
            }

            if (profile != null
                && meleeSo.FindProperty("animationProfile").objectReferenceValue != profile)
            {
                meleeSo.FindProperty("animationProfile").objectReferenceValue = profile;
                dirty = true;
            }

            if (meleeSo.FindProperty("targetDetectionRadius").floatValue <= 0f)
            {
                meleeSo.FindProperty("targetDetectionRadius").floatValue = 6f;
                dirty = true;
            }

            if (meleeSo.FindProperty("omnidirectionalDetectionRadius").floatValue <= 0f)
            {
                meleeSo.FindProperty("omnidirectionalDetectionRadius").floatValue = 8f;
                dirty = true;
            }

            if (meleeSo.ApplyModifiedPropertiesWithoutUndo())
                dirty = true;
        }

        if (rangedWeapon != null)
        {
            var rangedSo = new SerializedObject(rangedWeapon);
            if (bowController != null
                && rangedSo.FindProperty("animatorController").objectReferenceValue != bowController)
            {
                rangedSo.FindProperty("animatorController").objectReferenceValue = bowController;
                dirty = true;
            }

            if (bowProfile != null
                && rangedSo.FindProperty("animationProfile").objectReferenceValue != bowProfile)
            {
                rangedSo.FindProperty("animationProfile").objectReferenceValue = bowProfile;
                dirty = true;
            }

            if (rangedSo.FindProperty("targetDetectionRadius").floatValue <= 0f)
            {
                rangedSo.FindProperty("targetDetectionRadius").floatValue = 15f;
                dirty = true;
            }

            if (rangedSo.FindProperty("omnidirectionalDetectionRadius").floatValue <= 0f)
            {
                rangedSo.FindProperty("omnidirectionalDetectionRadius").floatValue = 20f;
                dirty = true;
            }

            if (rangedSo.ApplyModifiedPropertiesWithoutUndo())
                dirty = true;
        }

        if (nearestTargetQuery != null && weaponHolder != null)
        {
            var querySo = new SerializedObject(nearestTargetQuery);
            if (querySo.FindProperty("weaponHolder").objectReferenceValue != weaponHolder)
            {
                querySo.FindProperty("weaponHolder").objectReferenceValue = weaponHolder;
                dirty = true;
            }

            if (querySo.ApplyModifiedPropertiesWithoutUndo())
                dirty = true;
        }

        var animatorSo = new SerializedObject(stateAnimator);
        if (animatorSo.FindProperty("playerEntityState").objectReferenceValue != entityState)
        {
            animatorSo.FindProperty("playerEntityState").objectReferenceValue = entityState;
            dirty = true;
        }

        if (animator != null
            && animatorSo.FindProperty("animator").objectReferenceValue != animator)
        {
            animatorSo.FindProperty("animator").objectReferenceValue = animator;
            dirty = true;
        }

        if (locomotion != null
            && animatorSo.FindProperty("locomotionController").objectReferenceValue != locomotion)
        {
            animatorSo.FindProperty("locomotionController").objectReferenceValue = locomotion;
            dirty = true;
        }

        if (profile != null && animatorSo.FindProperty("profile").objectReferenceValue != profile)
        {
            animatorSo.FindProperty("profile").objectReferenceValue = profile;
            dirty = true;
        }

        if (attackController != null
            && animatorSo.FindProperty("attackController").objectReferenceValue != attackController)
        {
            animatorSo.FindProperty("attackController").objectReferenceValue = attackController;
            dirty = true;
        }

        if (weaponHolder != null
            && animatorSo.FindProperty("weaponHolder").objectReferenceValue != weaponHolder)
        {
            animatorSo.FindProperty("weaponHolder").objectReferenceValue = weaponHolder;
            dirty = true;
        }

        if (animatorSo.ApplyModifiedPropertiesWithoutUndo())
            dirty = true;

        var hitReact = prefabRoot.GetComponent<PlayerHitReact>();
        if (hitReact == null)
        {
            hitReact = prefabRoot.AddComponent<PlayerHitReact>();
            dirty = true;
        }

        var hitReactSo = new SerializedObject(hitReact);
        if (hitReactSo.FindProperty("invulnerabilityDuration").floatValue != 0.5f)
        {
            hitReactSo.FindProperty("invulnerabilityDuration").floatValue = 0.5f;
            dirty = true;
        }

        if (health != null
            && hitReactSo.FindProperty("health").objectReferenceValue != health)
        {
            hitReactSo.FindProperty("health").objectReferenceValue = health;
            dirty = true;
        }

        if (invulnerability != null
            && hitReactSo.FindProperty("invulnerability").objectReferenceValue != invulnerability)
        {
            hitReactSo.FindProperty("invulnerability").objectReferenceValue = invulnerability;
            dirty = true;
        }

        if (attackController != null
            && hitReactSo.FindProperty("attackController").objectReferenceValue != attackController)
        {
            hitReactSo.FindProperty("attackController").objectReferenceValue = attackController;
            dirty = true;
        }

        if (movement != null
            && hitReactSo.FindProperty("movement").objectReferenceValue != movement)
        {
            hitReactSo.FindProperty("movement").objectReferenceValue = movement;
            dirty = true;
        }

        if (stateAnimator != null
            && hitReactSo.FindProperty("entityStateAnimator").objectReferenceValue != stateAnimator)
        {
            hitReactSo.FindProperty("entityStateAnimator").objectReferenceValue = stateAnimator;
            dirty = true;
        }

        if (hitReactSo.ApplyModifiedPropertiesWithoutUndo())
            dirty = true;

        var entityStateSo = new SerializedObject(entityState);
        if (hitReact != null
            && entityStateSo.FindProperty("hitReact").objectReferenceValue != hitReact)
        {
            entityStateSo.FindProperty("hitReact").objectReferenceValue = hitReact;
            dirty = true;
        }

        if (entityStateSo.ApplyModifiedPropertiesWithoutUndo())
            dirty = true;

        if (dashController != null)
        {
            var dashSo = new SerializedObject(dashController);
            if (entityState != null
                && dashSo.FindProperty("playerEntityState").objectReferenceValue != entityState)
            {
                dashSo.FindProperty("playerEntityState").objectReferenceValue = entityState;
                if (dashSo.ApplyModifiedPropertiesWithoutUndo())
                    dirty = true;
            }
        }

        if (animator != null)
        {
            var receiver = animator.GetComponent<MeleeAttackAnimationEventReceiver>();
            if (receiver == null)
            {
                receiver = animator.gameObject.AddComponent<MeleeAttackAnimationEventReceiver>();
                dirty = true;
            }

            var receiverSo = new SerializedObject(receiver);
            if (meleeWeapon != null
                && receiverSo.FindProperty("meleeWeapon").objectReferenceValue != meleeWeapon)
            {
                receiverSo.FindProperty("meleeWeapon").objectReferenceValue = meleeWeapon;
                dirty = true;
            }

            if (weaponHolder != null
                && receiverSo.FindProperty("weaponHolder").objectReferenceValue != weaponHolder)
            {
                receiverSo.FindProperty("weaponHolder").objectReferenceValue = weaponHolder;
                dirty = true;
            }

            if (receiverSo.ApplyModifiedPropertiesWithoutUndo())
                dirty = true;

            var rangedReceiver = animator.GetComponent<RangedAttackAnimationEventReceiver>();
            if (rangedReceiver == null)
            {
                rangedReceiver = animator.gameObject.AddComponent<RangedAttackAnimationEventReceiver>();
                dirty = true;
            }

            var rangedReceiverSo = new SerializedObject(rangedReceiver);
            if (rangedWeapon != null
                && rangedReceiverSo.FindProperty("rangedWeapon").objectReferenceValue != rangedWeapon)
            {
                rangedReceiverSo.FindProperty("rangedWeapon").objectReferenceValue = rangedWeapon;
                dirty = true;
            }

            if (weaponHolder != null
                && rangedReceiverSo.FindProperty("weaponHolder").objectReferenceValue != weaponHolder)
            {
                rangedReceiverSo.FindProperty("weaponHolder").objectReferenceValue = weaponHolder;
                dirty = true;
            }

            if (rangedReceiverSo.ApplyModifiedPropertiesWithoutUndo())
                dirty = true;
        }

        if (movement != null)
        {
            var movementSo = new SerializedObject(movement);
            if (entityState != null
                && movementSo.FindProperty("playerEntityState").objectReferenceValue != entityState)
            {
                movementSo.FindProperty("playerEntityState").objectReferenceValue = entityState;
                dirty = true;
            }

            if (animator != null)
            {
                var rotationTarget = movementSo.FindProperty("rotationTarget");
                if (rotationTarget.objectReferenceValue != animator.transform)
                    rotationTarget.objectReferenceValue = animator.transform;
            }

            if (movementSo.ApplyModifiedPropertiesWithoutUndo())
                dirty = true;
        }

        var attackerFacing = prefabRoot.GetComponent<DamageAttackerFacing>();
        if (attackerFacing == null)
        {
            attackerFacing = prefabRoot.AddComponent<DamageAttackerFacing>();
            dirty = true;
        }

        var attackerFacingSo = new SerializedObject(attackerFacing);
        if (!attackerFacingSo.FindProperty("faceAttackerOnDamage").boolValue)
        {
            attackerFacingSo.FindProperty("faceAttackerOnDamage").boolValue = true;
            dirty = true;
        }

        if (attackerFacingSo.ApplyModifiedPropertiesWithoutUndo())
            dirty = true;

        var detectionRingView = prefabRoot.GetComponent<WeaponDetectionRadiusRingView>();
        if (detectionRingView == null)
        {
            detectionRingView = prefabRoot.AddComponent<WeaponDetectionRadiusRingView>();
            dirty = true;
        }

        var detectionRingSo = new SerializedObject(detectionRingView);
        if (weaponHolder != null
            && detectionRingSo.FindProperty("weaponHolder").objectReferenceValue != weaponHolder)
        {
            detectionRingSo.FindProperty("weaponHolder").objectReferenceValue = weaponHolder;
            dirty = true;
        }

        if (nearestTargetQuery != null
            && detectionRingSo.FindProperty("nearestTargetQuery").objectReferenceValue != nearestTargetQuery)
        {
            detectionRingSo.FindProperty("nearestTargetQuery").objectReferenceValue = nearestTargetQuery;
            dirty = true;
        }

        if (animator != null)
        {
            var ringAnchor = detectionRingSo.FindProperty("ringAnchor");
            if (ringAnchor.objectReferenceValue != animator.transform)
            {
                ringAnchor.objectReferenceValue = animator.transform;
                dirty = true;
            }
        }

        if (detectionRingSo.ApplyModifiedPropertiesWithoutUndo())
            dirty = true;

        if (dirty)
            PrefabUtility.SavePrefabAsset(prefabRoot);
    }
}
#endif
