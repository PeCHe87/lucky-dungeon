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
    const string MeleeBlockFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Block.FBX";

    static PlayerAnimationEditorSetup()
    {
        EditorApplication.delayCall += RunOnceDelayed;
    }

    static void RunOnceDelayed()
    {
        PlayerEntityStateAnimationProfile.EnsureDefaultAssetExists();
        PlayerBowAnimationEditorSetup.EnsureBowAssetsExist();
        PlayerBowAnimationEditorSetup.EnsureAttackReadyStateOnController(
            LocomotionControllerPath,
            MeleeBlockFbxPath,
            "Fighter_Block_Loop");
        PlayerBowAnimationEditorSetup.EnsureDieStateOnController(LocomotionControllerPath);
        PlayerBowAnimationEditorSetup.EnsureDieStateOnController(BowControllerPath);
        EnsureDefaultProfileAttackReadyConfigured();
        PlayerBowAnimationEditorSetup.EnsureDyingProfileEntry(
            PlayerEntityStateAnimationProfile.DefaultAssetPath);
        PlayerBowAnimationEditorSetup.EnsureDyingProfileEntry(BowProfilePath);
        WirePlayerPrefab();
    }

    static void EnsureDefaultProfileAttackReadyConfigured()
    {
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(
            PlayerEntityStateAnimationProfile.DefaultAssetPath);
        if (profile == null)
            return;

        var so = new SerializedObject(profile);
        var readyState = so.FindProperty("attackReadyAnimatorStateName");
        if (string.IsNullOrWhiteSpace(readyState.stringValue))
        {
            readyState.stringValue = "AttackReady";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
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
            SerializedProperty meleeDataProp = meleeSo.FindProperty("data");
            if (meleeDataProp != null && meleeDataProp.objectReferenceValue is MeleeWeaponData meleeData)
            {
                var dataSo = new SerializedObject(meleeData);
                bool dataDirty = false;

                if (locomotion != null
                    && dataSo.FindProperty("animatorController").objectReferenceValue != locomotion)
                {
                    dataSo.FindProperty("animatorController").objectReferenceValue = locomotion;
                    dataDirty = true;
                }

                if (profile != null
                    && dataSo.FindProperty("animationProfile").objectReferenceValue != profile)
                {
                    dataSo.FindProperty("animationProfile").objectReferenceValue = profile;
                    dataDirty = true;
                }

                if (dataSo.FindProperty("targetDetectionRadius").floatValue <= 0f)
                {
                    dataSo.FindProperty("targetDetectionRadius").floatValue = 6f;
                    dataDirty = true;
                }

                if (dataSo.FindProperty("omnidirectionalDetectionRadius").floatValue <= 0f)
                {
                    dataSo.FindProperty("omnidirectionalDetectionRadius").floatValue = 8f;
                    dataDirty = true;
                }

                if (dataDirty && dataSo.ApplyModifiedPropertiesWithoutUndo())
                {
                    EditorUtility.SetDirty(meleeData);
                    dirty = true;
                }
            }
        }

        if (rangedWeapon != null)
        {
            var rangedSo = new SerializedObject(rangedWeapon);
            SerializedProperty rangedDataProp = rangedSo.FindProperty("data");
            if (rangedDataProp != null && rangedDataProp.objectReferenceValue is AssaultWeaponData assaultData)
            {
                var dataSo = new SerializedObject(assaultData);
                bool dataDirty = false;

                if (bowController != null
                    && dataSo.FindProperty("animatorController").objectReferenceValue != bowController)
                {
                    dataSo.FindProperty("animatorController").objectReferenceValue = bowController;
                    dataDirty = true;
                }

                if (bowProfile != null
                    && dataSo.FindProperty("animationProfile").objectReferenceValue != bowProfile)
                {
                    dataSo.FindProperty("animationProfile").objectReferenceValue = bowProfile;
                    dataDirty = true;
                }

                if (dataSo.FindProperty("targetDetectionRadius").floatValue <= 0f)
                {
                    dataSo.FindProperty("targetDetectionRadius").floatValue = 15f;
                    dataDirty = true;
                }

                if (dataSo.FindProperty("omnidirectionalDetectionRadius").floatValue <= 0f)
                {
                    dataSo.FindProperty("omnidirectionalDetectionRadius").floatValue = 20f;
                    dataDirty = true;
                }

                if (dataDirty && dataSo.ApplyModifiedPropertiesWithoutUndo())
                {
                    EditorUtility.SetDirty(assaultData);
                    dirty = true;
                }
            }
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

        var deathHandler = prefabRoot.GetComponent<PlayerDeathHandler>();
        if (deathHandler == null)
        {
            deathHandler = prefabRoot.AddComponent<PlayerDeathHandler>();
            dirty = true;
        }

        var deathHandlerSo = new SerializedObject(deathHandler);
        if (health != null
            && deathHandlerSo.FindProperty("health").objectReferenceValue != health)
        {
            deathHandlerSo.FindProperty("health").objectReferenceValue = health;
            dirty = true;
        }

        if (attackController != null
            && deathHandlerSo.FindProperty("attackController").objectReferenceValue != attackController)
        {
            deathHandlerSo.FindProperty("attackController").objectReferenceValue = attackController;
            dirty = true;
        }

        if (movement != null
            && deathHandlerSo.FindProperty("movement").objectReferenceValue != movement)
        {
            deathHandlerSo.FindProperty("movement").objectReferenceValue = movement;
            dirty = true;
        }

        if (stateAnimator != null
            && deathHandlerSo.FindProperty("entityStateAnimator").objectReferenceValue != stateAnimator)
        {
            deathHandlerSo.FindProperty("entityStateAnimator").objectReferenceValue = stateAnimator;
            dirty = true;
        }

        if (deathHandlerSo.ApplyModifiedPropertiesWithoutUndo())
            dirty = true;

        if (deathHandler != null
            && entityStateSo.FindProperty("deathHandler").objectReferenceValue != deathHandler)
        {
            entityStateSo.FindProperty("deathHandler").objectReferenceValue = deathHandler;
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

        Transform queryOrigin = nearestTargetQuery != null
            ? nearestTargetQuery.QueryOriginTransform
            : prefabRoot.transform;
        var ringAnchor = detectionRingSo.FindProperty("ringAnchor");
        if (ringAnchor.objectReferenceValue != queryOrigin)
        {
            ringAnchor.objectReferenceValue = queryOrigin;
            dirty = true;
        }

        if (detectionRingSo.ApplyModifiedPropertiesWithoutUndo())
            dirty = true;

        if (dirty)
            PrefabUtility.SavePrefabAsset(prefabRoot);
    }
}
#endif
