#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures <see cref="FloatingHealthBarView"/> is on the player prefab.
/// </summary>
[InitializeOnLoad]
public static class FloatingHealthBarPrefabSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/entities/player.prefab";

    static FloatingHealthBarPrefabSetup()
    {
        EditorApplication.delayCall += EnsureFloatingHealthBar;
    }

    static void EnsureFloatingHealthBar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureFloatingHealthBar;
            return;
        }

        SetupPlayerPrefab();
    }

    [MenuItem("Tools/UI/Setup Floating Health Bar on Player")]
    public static void SetupPlayerPrefabMenu()
    {
        SetupPlayerPrefab();
    }

    [MenuItem("Tools/UI/Smoke Test Floating Health Bar")]
    public static void SmokeTestMenu()
    {
        RunSmokeTest();
    }

    public static void SetupPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.GetComponent<FloatingHealthBarView>() != null)
                return;

            root.AddComponent<FloatingHealthBarView>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[FloatingHealthBarPrefabSetup] Added FloatingHealthBarView to " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void RunSmokeTest()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (playerPrefab == null || playerPrefab.GetComponent<FloatingHealthBarView>() == null)
        {
            Debug.LogError("[FloatingHealthBarSmoke] FAIL: FloatingHealthBarView missing on player prefab.");
            return;
        }

        var go = new GameObject("FloatingHealthBarSmoke");
        try
        {
            CombatEntityHealth health = go.AddComponent<CombatEntityHealth>();
            FloatingHealthBarView view = go.AddComponent<FloatingHealthBarView>();

            InvokePrivate(health, "Awake");
            InvokePrivate(view, "Awake");
            InvokePrivate(view, "OnEnable");

            Image fill = GetPrivateField<Image>(view, "fillImage");
            if (fill == null)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: fillImage was not created.");
                return;
            }

            if (fill.sprite == null)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: fillImage has no sprite (fillAmount will not render).");
                return;
            }

            if (!Mathf.Approximately(fill.fillAmount, 1f))
            {
                Debug.LogError($"[FloatingHealthBarSmoke] FAIL: expected full fill, got {fill.fillAmount}.");
                return;
            }

            health.TakeDamage(25f, default);

            float expected = health.MaxHitPoints > 0f
                ? health.CurrentHitPoints / health.MaxHitPoints
                : 0f;
            if (!Mathf.Approximately(fill.fillAmount, Mathf.Clamp01(expected)))
            {
                Debug.LogError(
                    $"[FloatingHealthBarSmoke] FAIL: expected fill {expected}, got {fill.fillAmount}.");
                return;
            }

            if (fill.sprite == null)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: fill sprite cleared after damage.");
                return;
            }

            Transform barRoot = GetPrivateField<Transform>(view, "barRoot");
            if (barRoot == null || !barRoot.gameObject.activeSelf)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: barRoot should be active after non-lethal damage.");
                return;
            }

            health.TakeDamage(health.MaxHitPoints, default);

            if (barRoot.gameObject.activeSelf)
            {
                Debug.LogError("[FloatingHealthBarSmoke] FAIL: barRoot should be hidden after death.");
                return;
            }

            Debug.Log(
                $"[FloatingHealthBarSmoke] PASS: fill updated after damage and bar hidden on death " +
                $"({health.CurrentHitPoints}/{health.MaxHitPoints}).");
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
}
#endif
