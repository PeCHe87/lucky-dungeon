#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class PlayerAnimationEditorSetup
{
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";
    const string LocomotionControllerPath = "Assets/_project/_animation/PlayerLocomotion.controller";

    static PlayerAnimationEditorSetup()
    {
        EditorApplication.delayCall += RunOnceDelayed;
    }

    static void RunOnceDelayed()
    {
        PlayerEntityStateAnimationProfile.EnsureDefaultAssetExists();
        WirePlayerPrefab();
    }

    static void WirePlayerPrefab()
    {
        var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefabRoot == null)
            return;

        var locomotion = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LocomotionControllerPath);
        var profile = AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(
            PlayerEntityStateAnimationProfile.DefaultAssetPath);

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
        var meleeWeapon = prefabRoot.GetComponentInChildren<MeleeWeapon>(true);
        var movement = prefabRoot.GetComponent<TopDownCharacterMovement>();
        var animator = prefabRoot.GetComponentInChildren<Animator>(true);
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
        }

        if (movement != null && animator != null)
        {
            var movementSo = new SerializedObject(movement);
            var rotationTarget = movementSo.FindProperty("rotationTarget");
            if (rotationTarget.objectReferenceValue != animator.transform)
            {
                rotationTarget.objectReferenceValue = animator.transform;
                if (movementSo.ApplyModifiedPropertiesWithoutUndo())
                    dirty = true;
            }
        }

        if (dirty)
            PrefabUtility.SavePrefabAsset(prefabRoot);
    }
}
#endif
