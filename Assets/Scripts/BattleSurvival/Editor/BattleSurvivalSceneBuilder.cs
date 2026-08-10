using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Turtle.DungeonRaid;

namespace Turtle.BattleSurvival.Editor
{
    public static class BattleSurvivalSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Battle Test.unity";
        private const string RequestRelativePath =
            "Temp/CodexValidation/setup-battle-test.request";
        private const string ResultRelativePath =
            "Temp/CodexValidation/setup-battle-test.result";
        private static readonly Vector2 ArenaSize = new(120f, 72f);
        private static double nextRequestPollAt;

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
            nextRequestPollAt = 0d;
            EditorApplication.delayCall += RunRequestedSetup;
            EditorApplication.update -= RunRequestedSetup;
            EditorApplication.update += RunRequestedSetup;
        }

        private static void RunRequestedSetup()
        {
            if (EditorApplication.timeSinceStartup < nextRequestPollAt) return;
            nextRequestPollAt = EditorApplication.timeSinceStartup + 0.5d;
            if (!File.Exists(RequestPath))
            {
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(RequestPath);
            try
            {
                SetupSceneInternal();
                var failures = ValidateSceneInternal();
                WriteResult(failures.Count == 0 ? "PASS" : "FAIL",
                    failures.Count == 0
                        ? new[] { "Battle Test survival scene passed structural validation." }
                        : failures);
                if (failures.Count == 0)
                {
                    Debug.Log("Battle Test survival setup and validation passed.");
                }
                else
                {
                    Debug.LogError("Battle Test survival validation failed:\n- " +
                                   string.Join("\n- ", failures));
                }
            }
            catch (Exception exception)
            {
                WriteResult("ERROR", new[] { exception.ToString() });
                Debug.LogException(exception);
            }
        }

        [MenuItem("Turtle/Battle Survival/Setup Battle Test")]
        public static void SetupScene()
        {
            SetupSceneInternal();
        }

        [MenuItem("Turtle/Battle Survival/Validate Battle Test")]
        public static void ValidateScene()
        {
            var failures = ValidateSceneInternal();
            if (failures.Count == 0)
            {
                Debug.Log("Battle Test survival structural validation passed.");
            }
            else
            {
                Debug.LogError("Battle Test survival validation failed:\n- " +
                               string.Join("\n- ", failures));
            }
        }

        private static void SetupSceneInternal()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                throw new InvalidOperationException(
                    $"Create and save the requested scene at {ScenePath} before running setup.");
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
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
                        "Battle Test requires the existing Spatial Square and Spatial Circle sprites.");
                }

                var cameraObject = FindRoot(scene, "Main Camera") ?? EnsureRoot(scene, "Main Camera");
                cameraObject.tag = "MainCamera";
                var camera = GetOrAdd<Camera>(cameraObject);
                camera.orthographic = true;
                camera.orthographicSize = 40.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.012f, 0.02f, 0.032f, 1f);
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                GetOrAdd<AudioListener>(cameraObject);

                var directionalLight = FindRoot(scene, "Directional Light");
                if (directionalLight != null) directionalLight.SetActive(false);

                var arena = EnsureRoot(scene, "Battle Test Arena");
                ConfigureArena(arena.transform, square);

                var hunterRoot = EnsureRoot(scene, "Three Hunter Squads");
                var hunters = ConfigureHunters(hunterRoot.transform, circle);

                var runtimeHorde = EnsureRoot(scene, "Runtime Horde Pool");
                var templates = EnsureRoot(scene, "Battle Test Templates");
                var monsterTemplate = EnsureChild(templates.transform, "Inactive Horde Template");
                ConfigureMonsterTemplate(monsterTemplate, circle);

                var systems = EnsureRoot(scene, "Battle Survival Systems");
                var fxObject = EnsureChild(systems.transform, "Survival FX Pool");
                var fx = GetOrAdd<RaidFxPool2D>(fxObject);
                fx.ConfigureEditor(circle, 96, 48);
                var resolverObject = EnsureChild(systems.transform, "Shared Combat Resolver");
                var resolver = GetOrAdd<DungeonRaidDirector2D>(resolverObject);
                resolver.ConfigureEditor(
                    null,
                    Array.Empty<RaidEnemyPodBrain2D>(),
                    Array.Empty<RaidRoom2D>(),
                    Array.Empty<RaidRoomConnection2D>(),
                    Array.Empty<RaidChest2D>(),
                    fx,
                    null,
                    null);
                resolver.enabled = false;

                var director = GetOrAdd<BattleSurvivalDirector2D>(systems);
                director.ConfigureEditor(
                    hunters,
                    monsterTemplate,
                    runtimeHorde.transform,
                    resolver,
                    fx,
                    camera,
                    ArenaSize,
                    600);

                MarkDirty(cameraObject.transform, camera, arena.transform, hunterRoot.transform,
                    runtimeHorde.transform, templates.transform, fx, resolver, director);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
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

        private static RaidAgent2D[] ConfigureHunters(Transform root, Sprite sprite)
        {
            var profiles = BattleSurvivalHunterCatalog.CreateProfiles();
            var result = new RaidAgent2D[profiles.Count];
            for (var squadIndex = 0; squadIndex < BattleSurvivalHunterCatalog.SquadCount;
                 squadIndex++)
            {
                var first = profiles[squadIndex * BattleSurvivalHunterCatalog.HuntersPerSquad];
                var squadObject = EnsureChild(root, $"Squad {squadIndex + 1} - {first.SquadName}");
                var home = SquadHomeAnchor(squadIndex);
                squadObject.transform.localPosition = Vector3.zero;
                ConfigureSquadLabel(squadObject.transform, first.SquadName, home);

                for (var memberIndex = 0;
                     memberIndex < BattleSurvivalHunterCatalog.HuntersPerSquad;
                     memberIndex++)
                {
                    var profileIndex = squadIndex * BattleSurvivalHunterCatalog.HuntersPerSquad +
                                       memberIndex;
                    var profile = profiles[profileIndex];
                    var hunterObject = EnsureChild(squadObject.transform, profile.DisplayName);
                    var position = home + FormationOffset(memberIndex);
                    hunterObject.transform.position = new Vector3(position.x, position.y, 0f);
                    hunterObject.transform.localScale = Vector3.one *
                                                        (memberIndex == 0 ? 0.82f : 0.68f);
                    var renderer = GetOrAdd<SpriteRenderer>(hunterObject);
                    renderer.sprite = sprite;
                    renderer.color = profile.Color;
                    renderer.sortingOrder = 35 + squadIndex;
                    var body = GetOrAdd<Rigidbody2D>(hunterObject);
                    body.gravityScale = 0f;
                    body.freezeRotation = true;
                    var collider = GetOrAdd<CircleCollider2D>(hunterObject);
                    collider.radius = 0.4f;
                    collider.isTrigger = true;
                    var agent = GetOrAdd<RaidAgent2D>(hunterObject);
                    agent.ConfigureEditor(
                        profile.Id,
                        profile.DisplayName,
                        RaidFaction.Hunters,
                        profile.Role,
                        profile.Health,
                        profile.Mana,
                        profile.ManaRegeneration,
                        profile.Speed,
                        profile.BasicDamage,
                        profile.AttackRange,
                        profile.PreferredRange,
                        profile.AttackCooldown,
                        profile.Ranged,
                        profile.Color,
                        profile.Abilities,
                        0.4f,
                        false,
                        true,
                        true,
                        true);
                    var unit = GetOrAdd<BattleSurvivalUnit2D>(hunterObject);
                    unit.ConfigureHunter(profile, squadIndex, memberIndex, memberIndex == 0);
                    hunterObject.SetActive(true);
                    result[profileIndex] = agent;
                    MarkDirty(renderer, body, collider, agent, unit, hunterObject.transform);
                }
            }
            return result;
        }

        private static void ConfigureMonsterTemplate(GameObject template, Sprite sprite)
        {
            var renderer = GetOrAdd<SpriteRenderer>(template);
            renderer.sprite = sprite;
            renderer.color = new Color(0.56f, 0.9f, 0.12f);
            renderer.sortingOrder = 28;
            var body = GetOrAdd<Rigidbody2D>(template);
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var collider = GetOrAdd<CircleCollider2D>(template);
            collider.radius = 0.36f;
            collider.isTrigger = true;
            var agent = GetOrAdd<RaidAgent2D>(template);
            agent.ConfigureEditor(
                "horde-template",
                "Horde Template",
                RaidFaction.Monsters,
                RaidCombatRole.Melee,
                48f,
                0f,
                0f,
                3.7f,
                6.5f,
                1.25f,
                0.95f,
                1f,
                false,
                renderer.color,
                new List<RaidAbilitySpec>(),
                0.36f,
                true,
                true,
                true,
                false);
            GetOrAdd<BattleSurvivalUnit2D>(template);
            template.transform.localScale = Vector3.one * 0.5f;
            template.SetActive(false);
            MarkDirty(renderer, body, collider, agent, template.transform);
        }

        private static void ConfigureArena(Transform arena, Sprite square)
        {
            EnsureVisual(arena, "Arena Floor", square, Vector2.zero, ArenaSize,
                new Color(0.045f, 0.06f, 0.07f), -40);
            var half = ArenaSize * 0.5f;
            ConfigureBoundary(arena, "North Arena Wall", square,
                new Vector2(0f, half.y), new Vector2(ArenaSize.x + 1f, 0.45f));
            ConfigureBoundary(arena, "South Arena Wall", square,
                new Vector2(0f, -half.y), new Vector2(ArenaSize.x + 1f, 0.45f));
            ConfigureBoundary(arena, "West Arena Wall", square,
                new Vector2(-half.x, 0f), new Vector2(0.45f, ArenaSize.y));
            ConfigureBoundary(arena, "East Arena Wall", square,
                new Vector2(half.x, 0f), new Vector2(0.45f, ArenaSize.y));

            EnsureVisual(arena, "Central Defense Ring", square, Vector2.zero,
                new Vector2(28f, 21f), new Color(0.08f, 0.18f, 0.24f, 0.8f), -32);
            EnsureVisual(arena, "Central Rally Core", square, Vector2.zero,
                new Vector2(5f, 5f), new Color(0.1f, 0.48f, 0.64f, 0.72f), -29);

            var gateColor = new Color(0.95f, 0.12f, 0.055f, 0.9f);
            EnsureVisual(arena, "West Horde Gate", square,
                new Vector2(-half.x + 2.5f, 0f), new Vector2(3f, 13f), gateColor, -20);
            EnsureVisual(arena, "East Horde Gate", square,
                new Vector2(half.x - 2.5f, 0f), new Vector2(3f, 13f), gateColor, -20);
            EnsureVisual(arena, "North Horde Gate", square,
                new Vector2(0f, half.y - 2.5f), new Vector2(13f, 3f), gateColor, -20);
            EnsureVisual(arena, "South Horde Gate", square,
                new Vector2(0f, -half.y + 2.5f), new Vector2(13f, 3f), gateColor, -20);

            EnsureVisual(arena, "West Defense Lane", square,
                new Vector2(-28f, 0f), new Vector2(0.16f, 48f),
                new Color(0.18f, 0.42f, 0.52f, 0.5f), -28);
            EnsureVisual(arena, "East Defense Lane", square,
                new Vector2(28f, 0f), new Vector2(0.16f, 48f),
                new Color(0.18f, 0.42f, 0.52f, 0.5f), -28);
            EnsureVisual(arena, "North Defense Lane", square,
                new Vector2(0f, 20f), new Vector2(64f, 0.16f),
                new Color(0.18f, 0.42f, 0.52f, 0.5f), -28);
            EnsureVisual(arena, "South Defense Lane", square,
                new Vector2(0f, -20f), new Vector2(64f, 0.16f),
                new Color(0.18f, 0.42f, 0.52f, 0.5f), -28);
        }

        private static void ConfigureBoundary(
            Transform parent, string name, Sprite sprite, Vector2 position, Vector2 scale)
        {
            var wall = EnsureVisual(parent, name, sprite, position, scale,
                new Color(0.24f, 0.31f, 0.35f), -18);
            var collider = GetOrAdd<BoxCollider2D>(wall);
            collider.isTrigger = false;
            collider.size = Vector2.one;
            MarkDirty(collider);
        }

        private static GameObject EnsureVisual(
            Transform parent, string name, Sprite sprite, Vector2 position,
            Vector2 scale, Color color, int order)
        {
            var child = EnsureChild(parent, name);
            child.transform.localPosition = new Vector3(position.x, position.y, 0f);
            child.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = GetOrAdd<SpriteRenderer>(child);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            MarkDirty(child.transform, renderer);
            return child;
        }

        private static void ConfigureSquadLabel(Transform parent, string squadName, Vector2 home)
        {
            var labelObject = EnsureChild(parent, $"{squadName} Squad Label");
            labelObject.transform.position = new Vector3(home.x, home.y + 4.2f, 0f);
            labelObject.transform.localScale = Vector3.one;
            var label = GetOrAdd<TextMesh>(labelObject);
            label.text = $"{squadName.ToUpperInvariant()} SQUAD";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.14f;
            label.fontSize = 36;
            label.color = new Color(0.7f, 0.88f, 1f, 0.8f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mesh = label.GetComponent<MeshRenderer>();
            if (label.font != null && mesh != null) mesh.sharedMaterial = label.font.material;
            if (mesh != null) mesh.sortingOrder = 20;
            MarkDirty(labelObject.transform, label, mesh);
        }

        private static Vector2 SquadHomeAnchor(int squadIndex)
        {
            return squadIndex switch
            {
                0 => new Vector2(-11f, 4f),
                1 => new Vector2(11f, 4f),
                _ => new Vector2(0f, -9f)
            };
        }

        private static Vector2 FormationOffset(int memberIndex)
        {
            return memberIndex switch
            {
                0 => new Vector2(0f, 1.8f),
                1 => new Vector2(-1.4f, 0.8f),
                2 => new Vector2(1.4f, 0.8f),
                3 => new Vector2(-2.8f, -0.2f),
                4 => new Vector2(0f, -0.2f),
                5 => new Vector2(2.8f, -0.2f),
                6 => new Vector2(-3.6f, -1.5f),
                7 => new Vector2(-1.2f, -1.5f),
                8 => new Vector2(1.2f, -1.5f),
                _ => new Vector2(3.6f, -1.5f)
            };
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
                var director = roots.SelectMany(root =>
                    root.GetComponentsInChildren<BattleSurvivalDirector2D>(true)).FirstOrDefault();
                var agents = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidAgent2D>(true)).ToArray();
                var hunters = agents.Where(agent => agent.Faction == RaidFaction.Hunters).ToArray();
                var template = agents.FirstOrDefault(agent => agent.AgentId == "horde-template");
                var units = roots.SelectMany(root =>
                    root.GetComponentsInChildren<BattleSurvivalUnit2D>(true)).ToArray();
                var resolver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DungeonRaidDirector2D>(true)).FirstOrDefault();
                var camera = roots.SelectMany(root =>
                    root.GetComponentsInChildren<Camera>(true)).FirstOrDefault();

                Check(FindRoot(scene, "Battle Test Arena") != null,
                    "Missing persistent Battle Test Arena root.", failures);
                Check(FindRoot(scene, "Three Hunter Squads") != null,
                    "Missing authored Three Hunter Squads root.", failures);
                Check(director != null && director.IsConfigured,
                    "BattleSurvivalDirector2D is missing required references.", failures);
                Check(camera != null && camera.orthographic,
                    "Battle Test requires an orthographic camera.", failures);
                Check(hunters.Length == BattleSurvivalHunterCatalog.HunterCount,
                    $"Expected 18 authored hunters, found {hunters.Length}.", failures);
                Check(hunters.Select(hunter => hunter.AgentId).Distinct().Count() == hunters.Length,
                    "Every authored hunter must have a unique stable ID.", failures);
                Check(hunters.All(hunter => hunter.Abilities.Count == 3),
                    "Every authored hunter must have three assigned cooldown abilities.", failures);
                Check(units.Count(unit => unit.IsHunter && unit.IsSergeant) == 3,
                    "Exactly three authored squad sergeants are required.", failures);
                Check(template != null && !template.gameObject.activeSelf,
                    "The horde pool template must exist and remain inactive.", failures);
                Check(resolver != null && !resolver.enabled,
                    "The shared combat resolver must have its dungeon Update loop disabled.", failures);
                Check(FindRoot(scene, "Runtime Horde Pool") != null,
                    "Missing runtime horde pool root.", failures);
                Check(!EditorBuildSettings.scenes.Any(candidate => candidate.path == ScenePath),
                    "Battle Test is a development stress scene and should remain outside Build Settings.",
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
            var existing = parent.Cast<Transform>().FirstOrDefault(child => child.name == name);
            if (existing != null) return existing.gameObject;
            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
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
