#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-click scene setup for the shared projectile hierarchy root.</summary>
[InitializeOnLoad]
public static class ProjectileWorldContainerSetupMenu
{
    const string MenuPath = "Tools/Combat/Create Projectile World Container";
    const string InitScenePath = "Assets/_project/_scenes/init.unity";
    const string ContainerName = "projectilesContainer";
    const string LevelObjectName = "level";

    static ProjectileWorldContainerSetupMenu()
    {
        EditorApplication.delayCall += EnsureInitSceneContainer;
    }

    [MenuItem(MenuPath)]
    public static void CreateProjectileWorldContainer()
    {
        if (Object.FindFirstObjectByType<ProjectileWorldContainer>() != null)
        {
            Debug.Log($"[{nameof(ProjectileWorldContainerSetupMenu)}] A {nameof(ProjectileWorldContainer)} already exists in the open scene.");
            return;
        }

        Transform parent = FindLevelTransform();
        CreateContainerUnder(parent);
    }

    static void EnsureInitSceneContainer()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!System.IO.File.Exists(InitScenePath))
            return;

        Scene scene = EditorSceneManager.GetSceneByPath(InitScenePath);
        bool openedHere = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(InitScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            if (TryEnsureContainerInScene(scene))
                return;

            Transform parent = FindLevelTransformInScene(scene);
            CreateContainerUnder(parent);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[{nameof(ProjectileWorldContainerSetupMenu)}] Added '{ContainerName}' to '{InitScenePath}'.");
        }
        finally
        {
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    static bool TryEnsureContainerInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ProjectileWorldContainer[] containers = root.GetComponentsInChildren<ProjectileWorldContainer>(true);
            if (containers.Length > 0)
                return true;

            Transform existing = FindNamedTransform(root.transform, ContainerName);
            if (existing == null)
                continue;

            if (existing.GetComponent<ProjectileWorldContainer>() == null)
                existing.gameObject.AddComponent<ProjectileWorldContainer>();
            return true;
        }

        return false;
    }

    static void CreateContainerUnder(Transform parent)
    {
        var containerGo = new GameObject(ContainerName);
        Undo.RegisterCreatedObjectUndo(containerGo, "Create Projectile World Container");
        containerGo.transform.SetParent(parent, false);
        containerGo.transform.localPosition = Vector3.zero;
        containerGo.transform.localRotation = Quaternion.identity;
        containerGo.transform.localScale = Vector3.one;
        containerGo.AddComponent<ProjectileWorldContainer>();

        Selection.activeGameObject = containerGo;
        Debug.Log($"[{nameof(ProjectileWorldContainerSetupMenu)}] Created '{ContainerName}' under '{(parent != null ? parent.name : "scene root")}'.");
    }

    static Transform FindLevelTransform()
    {
        var level = GameObject.Find(LevelObjectName);
        return level != null ? level.transform : null;
    }

    static Transform FindLevelTransformInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == LevelObjectName)
                return root.transform;

            Transform found = root.transform.Find(LevelObjectName);
            if (found != null)
                return found;
        }

        return null;
    }

    static Transform FindNamedTransform(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamedTransform(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
