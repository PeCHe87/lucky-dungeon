#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures <see cref="FloatingHealthBarView"/> is on the player and combat entity prefabs.
/// </summary>
[InitializeOnLoad]
public static class FloatingHealthBarPrefabSetup
{
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";

    static readonly string[] EntityPrefabPaths =
    {
        "Assets/_project/_prefabs/entities/base_combat_entity.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab",
    };

    static FloatingHealthBarPrefabSetup()
    {
        EditorApplication.delayCall += EnsureFloatingHealthBars;
    }

    static void EnsureFloatingHealthBars()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureFloatingHealthBars;
            return;
        }

        SetupPlayerPrefab();
        SetupEntityPrefabs();
    }

    [MenuItem("Tools/UI/Setup Floating Health Bar Prefabs")]
    public static void SetupAllPrefabsMenu()
    {
        SetupPlayerPrefab();
        SetupEntityPrefabs();
    }

    [MenuItem("Tools/UI/Smoke Test Floating Health Bar")]
    public static void SmokeTestMenu()
    {
        RunSmokeTest();
    }

    public static void SetupPlayerPrefab()
    {
        EnsureFloatingHealthBarOnPrefab(PlayerPrefabPath, hideUntilDamaged: false);
    }

    public static void SetupEntityPrefabs()
    {
        foreach (string path in EntityPrefabPaths)
            EnsureFloatingHealthBarOnPrefab(path, hideUntilDamaged: true);
    }

    static void EnsureFloatingHealthBarOnPrefab(string prefabPath, bool hideUntilDamaged)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.GetComponent<CombatEntityHealth>() == null)
            {
                Debug.LogWarning("[FloatingHealthBarPrefabSetup] Skipped (no CombatEntityHealth): " + prefabPath);
                return;
            }

            var view = root.GetComponent<FloatingHealthBarView>();
            bool changed = false;

            if (view == null)
            {
                view = root.AddComponent<FloatingHealthBarView>();
                changed = true;
            }

            var so = new SerializedObject(view);
            SerializedProperty hideProp = so.FindProperty("hideUntilDamaged");
            if (hideProp != null && hideProp.boolValue != hideUntilDamaged)
            {
                hideProp.boolValue = hideUntilDamaged;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log(
                    "[FloatingHealthBarPrefabSetup] Ensured FloatingHealthBarView " +
                    $"(hideUntilDamaged={hideUntilDamaged}) on {prefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void RunSmokeTest()
    {
        if (!RunPlayerStyleSmokeTest())
            return;
        if (!RunHideUntilDamagedSmokeTest())
            return;

        foreach (string path in EntityPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<FloatingHealthBarView>() == null)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: FloatingHealthBarView missing on " + path);
                return;
            }

            var so = new SerializedObject(prefab.GetComponent<FloatingHealthBarView>());
            SerializedProperty hideProp = so.FindProperty("hideUntilDamaged");
            if (hideProp == null || !hideProp.boolValue)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: hideUntilDamaged should be true on " + path);
                return;
            }
        }

        Debug.Log("[FloatingHealthBarSmoke] PASS: player + hideUntilDamaged entity behavior, prefabs wired.");
    }

    static bool RunPlayerStyleSmokeTest()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null || playerPrefab.GetComponent<FloatingHealthBarView>() == null)
        {
            Debug.LogError("[FloatingHealthBarSmoke] FAIL: FloatingHealthBarView missing on player prefab.");
            return false;
        }

        var go = new GameObject("FloatingHealthBarSmokePlayer");
        try
        {
            CombatEntityHealth health = go.AddComponent<CombatEntityHealth>();
            FloatingHealthBarView view = go.AddComponent<FloatingHealthBarView>();

            InvokePrivate(health, "Awake");
            InvokePrivate(view, "Awake");
            InvokePrivate(view, "OnEnable");

            Transform barRoot = GetPrivateField<Transform>(view, "barRoot");
            Image fill = GetPrivateField<Image>(view, "fillImage");
            if (fill == null || fill.sprite == null || barRoot == null)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: player-style bar hierarchy incomplete.");
                return false;
            }

            if (!barRoot.gameObject.activeSelf)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: player bar should be visible at start.");
                return false;
            }

            health.TakeDamage(25f, default);
            float expected = health.CurrentHitPoints / health.MaxHitPoints;
            if (!Mathf.Approximately(fill.fillAmount, Mathf.Clamp01(expected)))
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: player fill did not update.");
                return false;
            }

            health.TakeDamage(health.MaxHitPoints, default);
            if (barRoot.gameObject.activeSelf)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: player bar should hide on death.");
                return false;
            }

            return true;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    static bool RunHideUntilDamagedSmokeTest()
    {
        var go = new GameObject("FloatingHealthBarSmokeEntity");
        try
        {
            CombatEntityHealth health = go.AddComponent<CombatEntityHealth>();
            FloatingHealthBarView view = go.AddComponent<FloatingHealthBarView>();

            SetPrivateField(view, "hideUntilDamaged", true);

            InvokePrivate(health, "Awake");
            InvokePrivate(view, "Awake");
            InvokePrivate(view, "OnEnable");

            Transform barRoot = GetPrivateField<Transform>(view, "barRoot");
            Image fill = GetPrivateField<Image>(view, "fillImage");
            if (barRoot == null || fill == null)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: entity bar hierarchy incomplete.");
                return false;
            }

            if (barRoot.gameObject.activeSelf)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: entity bar should be hidden at spawn.");
                return false;
            }

            health.TakeDamage(25f, default);
            if (!barRoot.gameObject.activeSelf)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: entity bar should reveal after non-lethal damage.");
                return false;
            }

            float expected = health.CurrentHitPoints / health.MaxHitPoints;
            if (!Mathf.Approximately(fill.fillAmount, Mathf.Clamp01(expected)))
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: entity fill did not update after reveal.");
                return false;
            }

            health.TakeDamage(health.MaxHitPoints, default);
            if (barRoot.gameObject.activeSelf)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: entity bar should hide on death.");
                return false;
            }

            // One-shot kill should never reveal.
            var oneShot = new GameObject("FloatingHealthBarSmokeOneShot");
            try
            {
                CombatEntityHealth oneShotHealth = oneShot.AddComponent<CombatEntityHealth>();
                FloatingHealthBarView oneShotView = oneShot.AddComponent<FloatingHealthBarView>();
                SetPrivateField(oneShotView, "hideUntilDamaged", true);
                InvokePrivate(oneShotHealth, "Awake");
                InvokePrivate(oneShotView, "Awake");
                InvokePrivate(oneShotView, "OnEnable");

                Transform oneShotRoot = GetPrivateField<Transform>(oneShotView, "barRoot");
                oneShotHealth.TakeDamage(oneShotHealth.MaxHitPoints, default);
                if (oneShotRoot != null && oneShotRoot.gameObject.activeSelf)
                {
                    Debug.LogError("[FloatingHealthBarSmoke] FAIL: one-shot kill should not reveal the bar.");
                    return false;
                }
            }
            finally
            {
                Object.DestroyImmediate(oneShot);
            }

            return true;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        method?.Invoke(target, null);
    }

    static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field?.GetValue(target) as T;
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        field?.SetValue(target, value);
    }
}
#endif
