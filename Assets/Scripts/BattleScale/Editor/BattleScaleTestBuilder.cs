using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Turtle.DungeonRaid;

namespace Turtle.BattleScale.Editor
{
    /// <summary>
    /// Creates an authored, visible arena while leaving the variable-size armies
    /// as a runtime pool. Re-running the tool updates only the named lab roots.
    /// </summary>
    public static class BattleScaleTestBuilder
    {
        public const string ScenePath = "Assets/Scenes/Battle Scale Test.unity";
        private const string RequestRelativePath =
            "Temp/CodexValidation/setup-battle-scale.request";
        private const string ResultRelativePath =
            "Temp/CodexValidation/setup-battle-scale.result";
        private static readonly Vector2 ArenaSize = new(104f, 56f);

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName
                                             ?? Directory.GetCurrentDirectory();
        private static string RequestPath => Path.Combine(
            ProjectRoot,
            RequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string ResultPath => Path.Combine(
            ProjectRoot,
            ResultRelativePath.Replace('/', Path.DirectorySeparatorChar));

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedSetup()
        {
            EditorApplication.delayCall += RunRequestedSetup;
            EditorApplication.update -= RunRequestedSetup;
            EditorApplication.update += RunRequestedSetup;
        }

        private static void RunRequestedSetup()
        {
            if (!File.Exists(RequestPath))
            {
                EditorApplication.update -= RunRequestedSetup;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= RunRequestedSetup;
            File.Delete(RequestPath);
            try
            {
                SetupSceneInternal();
                var failures = ValidateSceneInternal();
                WriteResult(failures.Count == 0 ? "PASS" : "FAIL",
                    failures.Count == 0
                        ? new[] { "Battle Scale Test setup passed structural validation." }
                        : failures);
                if (failures.Count == 0)
                {
                    Debug.Log("Battle Scale Test setup and validation passed.");
                }
                else
                {
                    Debug.LogError("Battle Scale Test validation failed:\n- " +
                                   string.Join("\n- ", failures));
                }
            }
            catch (Exception exception)
            {
                WriteResult("ERROR", new[] { exception.ToString() });
                Debug.LogException(exception);
            }
        }

        [MenuItem("Turtle/Battle Scale/Setup Battle Scale Test")]
        public static void SetupScene()
        {
            SetupSceneInternal();
        }

        [MenuItem("Turtle/Battle Scale/Validate Battle Scale Test")]
        public static void ValidateScene()
        {
            var failures = ValidateSceneInternal();
            if (failures.Count == 0)
            {
                Debug.Log("Battle Scale Test structural validation passed.");
            }
            else
            {
                Debug.LogError("Battle Scale Test validation failed:\n- " +
                               string.Join("\n- ", failures));
            }
        }

        private static void SetupSceneInternal()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = sceneAsset != null
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }

            try
            {
                var square = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Data/Ecosystem/Spatial/Spatial Square.png");
                var circle = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Data/Ecosystem/Spatial/Spatial Circle.png");
                if (square == null || circle == null)
                {
                    throw new InvalidOperationException(
                        "Battle Scale Test requires the existing Spatial Square and Spatial Circle sprites.");
                }

                var cameraObject = EnsureRoot(scene, "Main Camera");
                var camera = GetOrAdd<Camera>(cameraObject);
                cameraObject.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 31f;
                camera.backgroundColor = new Color(0.018f, 0.026f, 0.04f, 1f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                GetOrAdd<AudioListener>(cameraObject);

                var arena = EnsureRoot(scene, "Battle Scale Arena");
                EnsureArenaVisuals(arena.transform, square);

                var activeUnits = EnsureRoot(scene, "Runtime Battle Units");
                var templates = EnsureRoot(scene, "Battle Scale Templates");
                var template = EnsureChild(templates.transform, "Inactive Unit Template");
                var renderer = GetOrAdd<SpriteRenderer>(template);
                renderer.sprite = circle;
                renderer.color = Color.white;
                renderer.sortingOrder = 30;
                var body = GetOrAdd<Rigidbody2D>(template);
                body.gravityScale = 0f;
                body.freezeRotation = true;
                var collider = GetOrAdd<CircleCollider2D>(template);
                collider.radius = 0.38f;
                collider.isTrigger = true;
                var agent = GetOrAdd<RaidAgent2D>(template);
                GetOrAdd<BattleScaleUnit2D>(template);
                template.transform.localScale = Vector3.one * 0.54f;
                agent.ConfigureEditor(
                    "scale-template",
                    "Scale Unit Template",
                    RaidFaction.Hunters,
                    RaidCombatRole.Fighter,
                    100f,
                    50f,
                    5f,
                    4.2f,
                    11f,
                    1.35f,
                    1.05f,
                    0.9f,
                    false,
                    Color.white,
                    new List<RaidAbilitySpec>(),
                    0.38f,
                    true,
                    true,
                    true,
                    false);
                template.SetActive(false);

                var systems = EnsureRoot(scene, "Battle Scale Systems");
                var resolverObject = EnsureChild(systems.transform, "Shared Cast Resolver");
                var resolver = GetOrAdd<DungeonRaidDirector2D>(resolverObject);
                resolver.enabled = false;
                var simulation = GetOrAdd<BattleScaleSimulation2D>(systems);
                simulation.ConfigureEditor(
                    template,
                    activeUnits.transform,
                    resolver,
                    camera,
                    ArenaSize,
                    100);

                MarkDirty(cameraObject.transform, camera, arena.transform, renderer, body,
                    collider, agent, simulation, resolver, activeUnits.transform, templates.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (sceneAsset == null)
                {
                    EditorSceneManager.SaveScene(scene, ScenePath);
                }
                else
                {
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static List<string> ValidateSceneInternal()
        {
            var failures = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                failures.Add($"Missing scene: {ScenePath}");
                return failures;
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForValidation = !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }
            try
            {
                var roots = scene.GetRootGameObjects();
                var camera = roots.SelectMany(root =>
                    root.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
                var simulation = roots.SelectMany(root =>
                    root.GetComponentsInChildren<BattleScaleSimulation2D>(true)).FirstOrDefault();
                var template = roots.SelectMany(root =>
                    root.GetComponentsInChildren<BattleScaleUnit2D>(true))
                    .FirstOrDefault(candidate => candidate.gameObject.name == "Inactive Unit Template");
                var resolver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DungeonRaidDirector2D>(true)).FirstOrDefault();

                Check(FindRoot(scene, "Battle Scale Arena") != null,
                    "Missing persistent Battle Scale Arena root.", failures);
                Check(FindRoot(scene, "Runtime Battle Units") != null,
                    "Missing Runtime Battle Units pool root.", failures);
                Check(camera != null && camera.orthographic,
                    "The scale-test camera must exist and be orthographic.", failures);
                Check(simulation != null && simulation.IsConfigured,
                    "BattleScaleSimulation2D is missing required scene references.", failures);
                Check(template != null && !template.gameObject.activeSelf,
                    "The inactive unit template is missing or active in the authored scene.", failures);
                Check(template != null && template.GetComponent<RaidAgent2D>() != null &&
                      template.GetComponent<Rigidbody2D>() != null &&
                      template.GetComponent<CircleCollider2D>() != null,
                    "The unit template must retain RaidAgent2D, Rigidbody2D, and CircleCollider2D.",
                    failures);
                Check(resolver != null && !resolver.enabled,
                    "The shared cast resolver must be present but its dungeon Update loop disabled.",
                    failures);
                Check(!EditorBuildSettings.scenes.Any(candidate => candidate.path == ScenePath),
                    "Battle Scale Test is a development lab and should remain outside build settings.",
                    failures);
            }
            finally
            {
                if (openedForValidation && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            return failures;
        }

        private static void EnsureArenaVisuals(Transform arena, Sprite square)
        {
            var floor = EnsureVisual(arena, "Arena Floor", square,
                Vector2.zero, ArenaSize, new Color(0.055f, 0.075f, 0.09f), -30);
            floor.transform.localPosition = Vector3.zero;

            var half = ArenaSize * 0.5f;
            EnsureVisual(arena, "North Boundary", square,
                new Vector2(0f, half.y), new Vector2(ArenaSize.x + 1f, 0.35f),
                new Color(0.26f, 0.34f, 0.4f), -15);
            EnsureVisual(arena, "South Boundary", square,
                new Vector2(0f, -half.y), new Vector2(ArenaSize.x + 1f, 0.35f),
                new Color(0.26f, 0.34f, 0.4f), -15);
            EnsureVisual(arena, "West Boundary", square,
                new Vector2(-half.x, 0f), new Vector2(0.35f, ArenaSize.y),
                new Color(0.26f, 0.34f, 0.4f), -15);
            EnsureVisual(arena, "East Boundary", square,
                new Vector2(half.x, 0f), new Vector2(0.35f, ArenaSize.y),
                new Color(0.26f, 0.34f, 0.4f), -15);
            EnsureVisual(arena, "Center Line", square,
                Vector2.zero, new Vector2(0.12f, ArenaSize.y - 1f),
                new Color(0.32f, 0.42f, 0.5f, 0.52f), -18);
            EnsureVisual(arena, "Azure Deployment Zone", square,
                new Vector2(-ArenaSize.x * 0.38f, 0f), new Vector2(8f, ArenaSize.y - 4f),
                new Color(0.03f, 0.28f, 0.55f, 0.23f), -20);
            EnsureVisual(arena, "Crimson Deployment Zone", square,
                new Vector2(ArenaSize.x * 0.38f, 0f), new Vector2(8f, ArenaSize.y - 4f),
                new Color(0.55f, 0.06f, 0.045f, 0.23f), -20);
        }

        private static GameObject EnsureVisual(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 localPosition,
            Vector2 scale,
            Color color,
            int order)
        {
            var child = EnsureChild(parent, name);
            child.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            child.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = GetOrAdd<SpriteRenderer>(child);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            EditorUtility.SetDirty(renderer);
            return child;
        }

        private static GameObject EnsureRoot(Scene scene, string name)
        {
            var existing = FindRoot(scene, name);
            if (existing != null) return existing;
            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            var existing = parent.Cast<Transform>()
                .FirstOrDefault(child => child.name == name);
            if (existing != null) return existing.gameObject;
            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component != null) return component;
            component = Undo.AddComponent<T>(target);
            return component;
        }

        private static void MarkDirty(params UnityEngine.Object[] objects)
        {
            foreach (var target in objects)
            {
                if (target != null) EditorUtility.SetDirty(target);
            }
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static void WriteResult(string status, IEnumerable<string> lines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectRoot);
            File.WriteAllLines(ResultPath, new[] { status }.Concat(lines));
        }
    }
}
