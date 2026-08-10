using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Turtle.BattleSurvival;
using Turtle.Combat;
using Turtle.DungeonRaid;

namespace Turtle.BattleArena3D.Editor
{
    public static class BattleArena3DSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/3D Test Arena.unity";
        private const string FeatureRoot = "Assets/Scripts/BattleArena3D";
        private const string DataFolder = "Assets/Data/BattleArena3D";
        private const string MaterialFolder = "Assets/Materials/BattleArena3D";
        private const string VolumePath = DataFolder + "/Battle Arena Volume.asset";
        private const string NavMeshPath = DataFolder + "/3D Test Arena NavMesh.asset";
        private const string CharacterPath =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Models/Characters/RPG-Character.FBX";
        private const string SwordPath =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Models/Weapons/2Hand-Sword.FBX";
        private const string ControllerPath = "Assets/Data/Combat/Greatsword Combat.controller";
        private const string RequestRelativePath = "Temp/CodexValidation/setup-3d-test-arena.request";
        private const string ResultRelativePath = "Temp/CodexValidation/setup-3d-test-arena.result";
        private static readonly Vector2 ArenaExtents = new(48f, 32f);
        private static double nextRequestPollAt;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName ??
                                             Directory.GetCurrentDirectory();
        private static string RequestPath => Path.Combine(ProjectRoot,
            RequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string ResultPath => Path.Combine(ProjectRoot,
            ResultRelativePath.Replace('/', Path.DirectorySeparatorChar));

        private readonly struct ArenaMaterials
        {
            public ArenaMaterials(
                Material foundation,
                Material floor,
                Material stone,
                Material darkMetal,
                Material gold,
                Material rune,
                Material portal,
                Material aegis,
                Material ember,
                Material vanguard,
                Material monster,
                Material health,
                Material shield,
                Material barBackground,
                Material particle,
                Material trail)
            {
                Foundation = foundation;
                Floor = floor;
                Stone = stone;
                DarkMetal = darkMetal;
                Gold = gold;
                Rune = rune;
                Portal = portal;
                Aegis = aegis;
                Ember = ember;
                Vanguard = vanguard;
                Monster = monster;
                Health = health;
                Shield = shield;
                BarBackground = barBackground;
                Particle = particle;
                Trail = trail;
            }

            public Material Foundation { get; }
            public Material Floor { get; }
            public Material Stone { get; }
            public Material DarkMetal { get; }
            public Material Gold { get; }
            public Material Rune { get; }
            public Material Portal { get; }
            public Material Aegis { get; }
            public Material Ember { get; }
            public Material Vanguard { get; }
            public Material Monster { get; }
            public Material Health { get; }
            public Material Shield { get; }
            public Material BarBackground { get; }
            public Material Particle { get; }
            public Material Trail { get; }

            public Material Squad(int index) => index switch
            {
                0 => Aegis,
                1 => Ember,
                _ => Vanguard
            };
        }

        private readonly struct SelectionMarkerVisuals
        {
            public SelectionMarkerVisuals(Transform root, LineRenderer outer, LineRenderer inner)
            {
                Root = root;
                Outer = outer;
                Inner = inner;
            }

            public Transform Root { get; }
            public LineRenderer Outer { get; }
            public LineRenderer Inner { get; }
        }

        [InitializeOnLoadMethod]
        private static void ScheduleRequestWatcher()
        {
            EditorApplication.update -= RunRequestedSetup;
            EditorApplication.update += RunRequestedSetup;
        }

        private static void RunRequestedSetup()
        {
            if (EditorApplication.timeSinceStartup < nextRequestPollAt) return;
            nextRequestPollAt = EditorApplication.timeSinceStartup + 0.5d;
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            try
            {
                SetupSceneInternal();
                var failures = ValidateSceneInternal();
                WriteResult(failures.Count == 0 ? "PASS" : "FAIL",
                    failures.Count == 0
                        ? new[] { "3D Test Arena setup passed structural validation." }
                        : failures);
                if (failures.Count == 0) Debug.Log("3D Test Arena setup and validation passed.");
                else Debug.LogError("3D Test Arena validation failed:\n- " + string.Join("\n- ", failures));
            }
            catch (Exception exception)
            {
                WriteResult("ERROR", new[] { exception.ToString() });
                Debug.LogException(exception);
            }
        }

        [MenuItem("Turtle/Battle Arena 3D/Setup 3D Test Arena")]
        public static void SetupScene()
        {
            SetupSceneInternal();
        }

        [MenuItem("Turtle/Battle Arena 3D/Validate 3D Test Arena")]
        public static void ValidateScene()
        {
            var failures = ValidateSceneInternal();
            if (failures.Count == 0) Debug.Log("3D Test Arena structural validation passed.");
            else Debug.LogError("3D Test Arena validation failed:\n- " + string.Join("\n- ", failures));
        }

        private static void SetupSceneInternal()
        {
            EnsureFolders();
            ValidateSourceAssets();
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
                var materials = CreateMaterials();
                var environment = ReplaceRoot(scene, "3D Arena Environment");
                var portals = BuildEnvironment(environment.transform, materials);
                var surface = BuildNavigation(environment);

                var hunterRoot = ReplaceRoot(scene, "3D Battle Hunters");
                var rallyRoot = EnsureChild(hunterRoot.transform, "Squad Rally Points");
                var rallyPoints = CreateRallyPoints(rallyRoot.transform, materials);
                var hunters = BuildHunters(hunterRoot.transform, materials);

                var runtimeRoot = ReplaceRoot(scene, "3D Battle Runtime");
                var monsterRoot = EnsureChild(runtimeRoot.transform, "Runtime Monster Pool");
                var projectileRoot = EnsureChild(runtimeRoot.transform, "Runtime Projectile Pool");
                var telegraphRoot = EnsureChild(runtimeRoot.transform, "Runtime Telegraph Pool");

                var templates = ReplaceRoot(scene, "3D Battle Templates");
                var monsterTemplate = BuildMonsterTemplate(templates.transform, materials);
                var projectileTemplate = BuildProjectileTemplate(templates.transform, materials);
                var telegraphTemplate = BuildTelegraphTemplate(templates.transform, materials);

                var systems = ReplaceRoot(scene, "3D Battle Systems");
                var vfxPool = BuildVfx(systems.transform, projectileTemplate, projectileRoot.transform,
                    telegraphTemplate, telegraphRoot.transform, materials);
                var camera = ConfigureCamera(scene, systems.transform);
                ConfigureLighting(scene, systems.transform, materials);
                ConfigureVolume(systems.transform);

                var selectionMarker = BuildSelectionMarker(systems.transform, materials);

                var director = systems.AddComponent<BattleArena3DDirector>();
                var presentation = systems.AddComponent<BattleArena3DPresentationController>();
                var feedback = systems.AddComponent<BattleArena3DCombatFeedback>();
                var cameraRig = GetOrAdd<BattleArena3DCameraRig>(camera.gameObject);
                director.ConfigureEditor(hunters, monsterTemplate, monsterRoot.transform, portals,
                    rallyPoints, vfxPool, camera, 360, presentation, feedback);
                cameraRig.ConfigureEditor(camera, director, ArenaExtents, Vector3.zero, 58f, 52f, 0f,
                    presentation);
                feedback.ConfigureEditor(director, camera, cameraRig, vfxPool, selectionMarker.Root,
                    selectionMarker.Outer, selectionMarker.Inner);
                presentation.ConfigureEditor(director, cameraRig, vfxPool, feedback);

                PersistNavigationData(surface);
                EditorUtility.SetDirty(surface);
                EditorUtility.SetDirty(director);
                EditorUtility.SetDirty(cameraRig);
                EditorUtility.SetDirty(vfxPool);
                EditorUtility.SetDirty(feedback);
                EditorUtility.SetDirty(presentation);
                EditorSceneManager.MarkSceneDirty(scene);
                if (sceneAsset == null) EditorSceneManager.SaveScene(scene, ScenePath);
                else EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (openedForSetup && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform[] BuildEnvironment(Transform root, ArenaMaterials materials)
        {
            CreatePrimitive(PrimitiveType.Cube, "Obsidian Foundation", root,
                new Vector3(0f, -1.1f, 0f), new Vector3(108f, 2f, 76f), materials.Foundation);
            var floor = CreatePrimitive(PrimitiveType.Cube, "Arena Combat Floor", root,
                new Vector3(0f, -0.06f, 0f), new Vector3(96f, 0.25f, 64f), materials.Floor);
            floor.isStatic = true;

            var dais = CreatePrimitive(PrimitiveType.Cylinder, "Central Command Dais", root,
                new Vector3(0f, 0.12f, 0f), new Vector3(22f, 0.18f, 22f), materials.DarkMetal);
            dais.isStatic = true;
            CreatePrimitive(PrimitiveType.Cylinder, "Central Rune", root,
                new Vector3(0f, 0.24f, 0f), new Vector3(18.5f, 0.035f, 18.5f), materials.Rune, true);
            CreatePrimitive(PrimitiveType.Cylinder, "Central Rune Inset", root,
                new Vector3(0f, 0.255f, 0f), new Vector3(15.8f, 0.04f, 15.8f), materials.Floor, true);
            for (var index = 0; index < 12; index++)
            {
                var angle = index * 30f;
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                CreatePrimitive(PrimitiveType.Cube, $"Rune Spoke {index + 1:00}", root,
                    direction * 13.5f + Vector3.up * 0.14f,
                    new Vector3(0.16f, 0.035f, 7.5f), materials.Rune, true,
                    Quaternion.Euler(0f, angle, 0f));
            }

            BuildPerimeterWalls(root, materials);
            BuildInteriorCover(root, materials);
            BuildGrandstands(root, materials);

            var portals = new Transform[4];
            portals[0] = BuildGate(root, "West Gate", new Vector3(-47f, 0f, 0f), 90f,
                new Color(0.15f, 0.55f, 1f), materials);
            portals[1] = BuildGate(root, "East Gate", new Vector3(47f, 0f, 0f), -90f,
                new Color(1f, 0.18f, 0.08f), materials);
            portals[2] = BuildGate(root, "North Gate", new Vector3(0f, 0f, 31f), 180f,
                new Color(0.7f, 0.2f, 1f), materials);
            portals[3] = BuildGate(root, "South Gate", new Vector3(0f, 0f, -31f), 0f,
                new Color(0.1f, 1f, 0.58f), materials);

            var obelisk = new GameObject("Command Obelisk");
            obelisk.transform.SetParent(root);
            obelisk.transform.position = new Vector3(0f, 0.25f, 0f);
            CreatePrimitive(PrimitiveType.Cube, "Obelisk Base", obelisk.transform,
                obelisk.transform.position + Vector3.up * 0.45f, new Vector3(2.6f, 0.8f, 2.6f),
                materials.DarkMetal);
            CreatePrimitive(PrimitiveType.Cube, "Obelisk Spire", obelisk.transform,
                obelisk.transform.position + Vector3.up * 2.4f, new Vector3(0.9f, 3.5f, 0.9f),
                materials.Stone, true, Quaternion.Euler(0f, 45f, 0f));
            CreatePrimitive(PrimitiveType.Cube, "Obelisk Crystal", obelisk.transform,
                obelisk.transform.position + Vector3.up * 4.7f, Vector3.one * 1.3f, materials.Rune, true,
                Quaternion.Euler(45f, 45f, 45f));

            return portals;
        }

        private static void BuildPerimeterWalls(Transform root, ArenaMaterials materials)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                for (var segment = -1; segment <= 1; segment += 2)
                {
                    CreatePrimitive(PrimitiveType.Cube,
                        $"{(side < 0 ? "West" : "East")} Wall {(segment < 0 ? "South" : "North")}",
                        root, new Vector3(side * 49f, 2.5f, segment * 18.5f),
                        new Vector3(2f, 5f, 25f), materials.Stone).isStatic = true;
                    CreatePrimitive(PrimitiveType.Cube,
                        $"{(side < 0 ? "South" : "North")} Wall {(segment < 0 ? "West" : "East")}",
                        root, new Vector3(segment * 29f, 2.5f, side * 33f),
                        new Vector3(39f, 5f, 2f), materials.Stone).isStatic = true;
                }
            }
            var towerPositions = new[]
            {
                new Vector3(-48f, 0f, -32f), new Vector3(-48f, 0f, 32f),
                new Vector3(48f, 0f, -32f), new Vector3(48f, 0f, 32f)
            };
            for (var index = 0; index < towerPositions.Length; index++)
            {
                CreatePrimitive(PrimitiveType.Cylinder, $"Corner Tower {index + 1}", root,
                    towerPositions[index] + Vector3.up * 3.3f, new Vector3(5.5f, 3.3f, 5.5f),
                    materials.DarkMetal).isStatic = true;
                CreatePrimitive(PrimitiveType.Cylinder, "Tower Crown", root,
                    towerPositions[index] + Vector3.up * 6.6f, new Vector3(6.5f, 0.35f, 6.5f),
                    materials.Gold, true);
            }
        }

        private static void BuildInteriorCover(Transform root, ArenaMaterials materials)
        {
            var pillarPositions = new[]
            {
                new Vector3(-28f, 0f, -17f), new Vector3(-28f, 0f, 17f),
                new Vector3(28f, 0f, -17f), new Vector3(28f, 0f, 17f),
                new Vector3(-16f, 0f, -24f), new Vector3(16f, 0f, -24f),
                new Vector3(-16f, 0f, 24f), new Vector3(16f, 0f, 24f)
            };
            for (var index = 0; index < pillarPositions.Length; index++)
            {
                var height = index % 3 == 0 ? 3.3f : 5.2f;
                CreatePrimitive(PrimitiveType.Cylinder, $"War Pillar {index + 1:00}", root,
                    pillarPositions[index] + Vector3.up * height * 0.5f,
                    new Vector3(1.7f, height * 0.5f, 1.7f), materials.Stone).isStatic = true;
                CreatePrimitive(PrimitiveType.Cylinder, "Pillar Rune", root,
                    pillarPositions[index] + Vector3.up * (height + 0.12f),
                    new Vector3(2.15f, 0.12f, 2.15f), materials.Rune, true);
            }
            var barricades = new[]
            {
                (new Vector3(-22f, 0.65f, -8f), 25f), (new Vector3(-22f, 0.65f, 8f), -25f),
                (new Vector3(22f, 0.65f, -8f), -25f), (new Vector3(22f, 0.65f, 8f), 25f)
            };
            for (var index = 0; index < barricades.Length; index++)
            {
                CreatePrimitive(PrimitiveType.Cube, $"Broken Barricade {index + 1}", root,
                    barricades[index].Item1, new Vector3(7f, 1.3f, 0.65f), materials.DarkMetal,
                    false, Quaternion.Euler(0f, barricades[index].Item2, index % 2 == 0 ? 4f : -4f)).isStatic = true;
            }
        }

        private static void BuildGrandstands(Transform root, ArenaMaterials materials)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                for (var tier = 0; tier < 3; tier++)
                {
                    CreatePrimitive(PrimitiveType.Cube,
                        $"{(side < 0 ? "South" : "North")} Grandstand Tier {tier + 1}", root,
                        new Vector3(0f, 1f + tier * 1.1f, side * (35.5f + tier * 1.5f)),
                        new Vector3(72f, 1f + tier * 0.2f, 3f), materials.Foundation).isStatic = true;
                }
                for (var banner = -3; banner <= 3; banner++)
                {
                    CreatePrimitive(PrimitiveType.Cube, $"Battle Banner {side} {banner}", root,
                        new Vector3(banner * 10f, 6.1f, side * 33.8f),
                        new Vector3(3.1f, 3.4f, 0.12f), banner % 2 == 0 ? materials.Aegis : materials.Ember,
                        true);
                }
            }
        }

        private static Transform BuildGate(
            Transform root,
            string name,
            Vector3 position,
            float yaw,
            Color lightColor,
            ArenaMaterials materials)
        {
            var gate = new GameObject(name);
            gate.transform.SetParent(root);
            gate.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            for (var side = -1; side <= 1; side += 2)
            {
                CreateLocalPrimitive(PrimitiveType.Cube, $"Gate Tower {side}", gate.transform,
                    new Vector3(side * 4.2f, 3.5f, 0f), new Vector3(2.4f, 7f, 3.2f), materials.DarkMetal)
                    .isStatic = true;
                CreateLocalPrimitive(PrimitiveType.Cylinder, "Tower Cap", gate.transform,
                    new Vector3(side * 4.2f, 7.1f, 0f), new Vector3(3.2f, 0.35f, 3.2f), materials.Gold, true);
            }
            CreateLocalPrimitive(PrimitiveType.Cube, "Gate Lintel", gate.transform,
                new Vector3(0f, 7.2f, 0f), new Vector3(10.5f, 1.5f, 3.2f), materials.Stone).isStatic = true;
            for (var segment = 0; segment < 16; segment++)
            {
                var angle = segment / 16f * Mathf.PI * 2f;
                var local = new Vector3(Mathf.Cos(angle) * 3.25f, 3.5f + Mathf.Sin(angle) * 3.25f, 0.15f);
                CreateLocalPrimitive(PrimitiveType.Cube, $"Portal Rune {segment + 1:00}", gate.transform,
                    local, new Vector3(1.25f, 0.3f, 0.35f), materials.Portal, true,
                    Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg));
            }
            CreateLocalPrimitive(PrimitiveType.Cylinder, "Portal Energy", gate.transform,
                new Vector3(0f, 3.5f, 0.25f), new Vector3(5.6f, 0.04f, 5.6f), materials.Portal, true,
                Quaternion.Euler(90f, 0f, 0f));
            var lightObject = new GameObject("Portal Light");
            lightObject.transform.SetParent(gate.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 3.5f, 2f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColor;
            light.range = 18f;
            light.intensity = 5f;
            light.shadows = LightShadows.None;
            var spawn = new GameObject("Horde Spawn Point");
            spawn.transform.SetParent(gate.transform, false);
            spawn.transform.localPosition = new Vector3(0f, 0.15f, 4.5f);
            spawn.transform.localRotation = Quaternion.identity;
            return spawn.transform;
        }

        private static NavMeshSurface BuildNavigation(GameObject environment)
        {
            var surface = environment.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            return surface;
        }

        private static void PersistNavigationData(NavMeshSurface surface)
        {
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) != null)
            {
                AssetDatabase.DeleteAsset(NavMeshPath);
            }
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                throw new InvalidOperationException("NavMeshSurface did not produce navigation data.");
            }
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            AssetDatabase.ImportAsset(NavMeshPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static Transform[] CreateRallyPoints(Transform parent, ArenaMaterials materials)
        {
            var positions = new[]
            {
                new Vector3(-7f, 0.34f, -5.5f),
                new Vector3(7f, 0.34f, -5.5f),
                new Vector3(0f, 0.34f, 6.5f)
            };
            var points = new Transform[3];
            for (var index = 0; index < points.Length; index++)
            {
                var point = new GameObject($"{SquadName(index)} Rally");
                point.transform.SetParent(parent);
                point.transform.position = positions[index];
                CreatePrimitive(PrimitiveType.Cylinder, "Squad Sigil", point.transform,
                    positions[index], new Vector3(4.5f, 0.025f, 4.5f), materials.Squad(index), true);
                points[index] = point.transform;
            }
            return points;
        }

        private static BattleArena3DUnit[] BuildHunters(Transform parent, ArenaMaterials materials)
        {
            var profiles = BattleSurvivalHunterCatalog.CreateProfiles();
            var result = new BattleArena3DUnit[profiles.Count];
            var characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            var swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var squadOrigins = new[]
            {
                new Vector3(-9f, 0.15f, -7f), new Vector3(9f, 0.15f, -7f), new Vector3(0f, 0.15f, 8f)
            };
            for (var index = 0; index < profiles.Count; index++)
            {
                var profile = profiles[index];
                var squad = index / BattleSurvivalHunterCatalog.HuntersPerSquad;
                var withinSquad = index % BattleSurvivalHunterCatalog.HuntersPerSquad;
                var row = withinSquad / 5;
                var column = withinSquad % 5;
                var position = squadOrigins[squad] + new Vector3((column - 2f) * 1.75f, 0f, row * 1.8f);
                var root = new GameObject($"{profile.DisplayName} [{profile.BuildLabel}]");
                root.transform.SetParent(parent);
                root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.forward));

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.38f;
                agent.height = 1.85f;
                agent.baseOffset = 0f;
                agent.speed = profile.Speed;
                agent.acceleration = 28f;
                agent.angularSpeed = 900f;
                agent.stoppingDistance = 0.16f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                agent.avoidancePriority = 10 + index;
                var hurtbox = root.AddComponent<CapsuleCollider>();
                hurtbox.center = new Vector3(0f, 0.92f, 0f);
                hurtbox.height = 1.8f;
                hurtbox.radius = 0.38f;
                hurtbox.isTrigger = true;

                var model = (GameObject)PrefabUtility.InstantiatePrefab(characterAsset);
                model.name = "RPG Hunter Model";
                model.transform.SetParent(root.transform, false);
                RemoveImportedComponents(model);
                var animator = model.GetComponentInChildren<Animator>(true);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (animator.GetComponent<CombatAnimationEventRelay>() == null)
                {
                    animator.gameObject.AddComponent<CombatAnimationEventRelay>();
                }

                Transform weapon;
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand) ?? model.transform;
                if (profile.Role is RaidCombatRole.Mage or RaidCombatRole.Healer or RaidCombatRole.Ranger)
                {
                    weapon = BuildStaff(hand, materials.Squad(squad), profile.Color);
                }
                else
                {
                    var sword = (GameObject)PrefabUtility.InstantiatePrefab(swordAsset);
                    sword.name = "Equipped Greatsword";
                    sword.transform.SetParent(hand, false);
                    sword.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    RemoveImportedComponents(sword);
                    weapon = sword.transform;
                }

                var ring = CreatePrimitive(PrimitiveType.Cylinder, "Squad Ring", root.transform,
                    position + Vector3.up * 0.035f, new Vector3(1.25f, 0.025f, 1.25f),
                    materials.Squad(squad), true);
                var crystal = CreatePrimitive(PrimitiveType.Cube, "Role Crystal", root.transform,
                    position + Vector3.up * 2.28f, Vector3.one * 0.26f,
                    RoleMaterial(profile.Role, materials), true, Quaternion.Euler(45f, 45f, 45f));
                var bars = BuildWorldBars(root.transform, 2.5f, materials, true);
                var view = root.AddComponent<BattleArena3DUnitView>();
                view.ConfigureEditor(animator, model.transform, weapon, bars.Root, bars.HealthFill, bars.ShieldFill,
                    model.GetComponentsInChildren<Renderer>(true), crystal.GetComponent<Renderer>(), false);
                view.ApplyPalette(profile.Color);
                var unit = root.AddComponent<BattleArena3DUnit>();
                unit.ConfigureHunterEditor(profile.Id, profile.DisplayName, profile.BuildLabel, profile.TraitLabel,
                    profile.Role, squad, withinSquad == 0, profile.Health, profile.Mana, profile.ManaRegeneration,
                    profile.Speed, profile.BasicDamage, profile.AttackRange, profile.PreferredRange,
                    profile.AttackCooldown, profile.Ranged, profile.Color, profile.Aggression, profile.Cohesion,
                    profile.Support, profile.Abilities, agent, hurtbox, view);
                result[index] = unit;
                EditorUtility.SetDirty(unit);
                EditorUtility.SetDirty(view);
                EditorUtility.SetDirty(agent);
                ring.isStatic = false;
            }
            return result;
        }

        private static BattleArena3DUnit BuildMonsterTemplate(Transform parent, ArenaMaterials materials)
        {
            var root = new GameObject("Inactive Monster Template");
            root.transform.SetParent(parent);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.34f;
            agent.height = 1.75f;
            agent.acceleration = 20f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var hurtbox = root.AddComponent<CapsuleCollider>();
            hurtbox.center = new Vector3(0f, 0.86f, 0f);
            hurtbox.height = 1.7f;
            hurtbox.radius = 0.34f;
            hurtbox.isTrigger = true;
            var visualRoot = new GameObject("Monster Visuals").transform;
            visualRoot.SetParent(root.transform, false);
            var body = CreateLocalPrimitive(PrimitiveType.Capsule, "Body", visualRoot,
                new Vector3(0f, 0.86f, 0f), new Vector3(0.72f, 0.88f, 0.72f), materials.Monster, true);
            var head = CreateLocalPrimitive(PrimitiveType.Sphere, "Head", visualRoot,
                new Vector3(0f, 1.64f, 0.05f), Vector3.one * 0.62f, materials.Monster, true);
            var leftHorn = CreateLocalPrimitive(PrimitiveType.Cylinder, "Left Horn", visualRoot,
                new Vector3(-0.26f, 1.98f, 0.06f), new Vector3(0.11f, 0.38f, 0.11f),
                materials.DarkMetal, true, Quaternion.Euler(0f, 0f, 35f));
            var rightHorn = CreateLocalPrimitive(PrimitiveType.Cylinder, "Right Horn", visualRoot,
                new Vector3(0.26f, 1.98f, 0.06f), new Vector3(0.11f, 0.38f, 0.11f),
                materials.DarkMetal, true, Quaternion.Euler(0f, 0f, -35f));
            var leftClaw = CreateLocalPrimitive(PrimitiveType.Cube, "Left Claw", visualRoot,
                new Vector3(-0.55f, 0.9f, 0.18f), new Vector3(0.16f, 0.75f, 0.16f),
                materials.DarkMetal, true, Quaternion.Euler(18f, 0f, 12f));
            var rightClaw = CreateLocalPrimitive(PrimitiveType.Cube, "Right Claw", visualRoot,
                new Vector3(0.55f, 1f, 0.22f), new Vector3(0.18f, 0.9f, 0.18f),
                materials.DarkMetal, true, Quaternion.Euler(18f, 0f, -12f));
            var crystal = CreateLocalPrimitive(PrimitiveType.Sphere, "Horde Core", visualRoot,
                new Vector3(0f, 1.35f, 0.33f), Vector3.one * 0.2f, materials.Portal, true);
            var bars = BuildWorldBars(root.transform, 2.25f, materials, false);
            var view = root.AddComponent<BattleArena3DUnitView>();
            view.ConfigureEditor(null, visualRoot, rightClaw.transform, bars.Root, bars.HealthFill, bars.ShieldFill,
                new[] { body.GetComponent<Renderer>(), head.GetComponent<Renderer>() },
                crystal.GetComponent<Renderer>(), true, body.transform, head.transform, leftHorn.transform,
                rightHorn.transform, leftClaw.transform, rightClaw.transform, crystal.transform);
            var unit = root.AddComponent<BattleArena3DUnit>();
            unit.ConfigureTemplateEditor(agent, hurtbox, view);
            root.SetActive(false);
            return unit;
        }

        private static Transform BuildStaff(Transform hand, Material material, Color color)
        {
            var root = new GameObject("Runic Staff").transform;
            root.SetParent(hand, false);
            CreateLocalPrimitive(PrimitiveType.Cylinder, "Staff Shaft", root,
                new Vector3(0f, 0.75f, 0f), new Vector3(0.055f, 0.8f, 0.055f), material, true);
            CreateLocalPrimitive(PrimitiveType.Cube, "Staff Focus", root,
                new Vector3(0f, 1.65f, 0f), Vector3.one * 0.22f, material, true,
                Quaternion.Euler(45f, 45f, 45f));
            root.localRotation = Quaternion.Euler(0f, 0f, 180f);
            return root;
        }

        private readonly struct WorldBars
        {
            public WorldBars(Transform root, Transform healthFill, Transform shieldFill)
            {
                Root = root;
                HealthFill = healthFill;
                ShieldFill = shieldFill;
            }
            public Transform Root { get; }
            public Transform HealthFill { get; }
            public Transform ShieldFill { get; }
        }

        private static WorldBars BuildWorldBars(
            Transform parent,
            float height,
            ArenaMaterials materials,
            bool showByDefault)
        {
            var root = new GameObject("World Health Bar").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, height, 0f);
            CreateLocalPrimitive(PrimitiveType.Cube, "Bar Back", root, Vector3.zero,
                new Vector3(1.35f, 0.11f, 0.04f), materials.BarBackground, true);
            var health = CreateLocalPrimitive(PrimitiveType.Cube, "Health", root,
                new Vector3(0f, 0f, -0.025f), new Vector3(1.28f, 0.075f, 0.035f), materials.Health, true);
            var shield = CreateLocalPrimitive(PrimitiveType.Cube, "Shield", root,
                new Vector3(0f, 0.13f, -0.025f), new Vector3(1.28f, 0.04f, 0.035f), materials.Shield, true);
            shield.SetActive(false);
            root.gameObject.SetActive(showByDefault);
            return new WorldBars(root, health.transform, shield.transform);
        }

        private static BattleArena3DProjectile BuildProjectileTemplate(Transform parent, ArenaMaterials materials)
        {
            var root = CreatePrimitive(PrimitiveType.Sphere, "Inactive Projectile Template", parent,
                Vector3.down * 20f, Vector3.one * 0.18f, materials.Portal, true);
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = materials.Trail;
            trail.time = 0.32f;
            trail.minVertexDistance = 0.08f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var projectile = root.AddComponent<BattleArena3DProjectile>();
            projectile.ConfigureEditor(root.GetComponent<Renderer>(), trail);
            root.SetActive(false);
            return projectile;
        }

        private static BattleArena3DTelegraph BuildTelegraphTemplate(Transform parent, ArenaMaterials materials)
        {
            const int segmentCount = 48;
            var root = new GameObject("Inactive Telegraph Template");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.down * 20f;
            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = materials.Particle;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segmentCount;
            line.widthMultiplier = 0.09f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (var index = 0; index < segmentCount; index++)
            {
                var radians = index / (float)segmentCount * Mathf.PI * 2f;
                line.SetPosition(index, new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)));
            }
            var telegraph = root.AddComponent<BattleArena3DTelegraph>();
            telegraph.ConfigureEditor(line, 0.09f);
            root.SetActive(false);
            return telegraph;
        }

        private static SelectionMarkerVisuals BuildSelectionMarker(Transform parent, ArenaMaterials materials)
        {
            var root = new GameObject("Global Selection Marker");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.down * 20f;

            var outer = CreateSelectionRing(root.transform, "Outer Selection Ring", materials.Particle,
                1.8f, 0.11f, new Color(0.18f, 0.9f, 1f, 0.95f));
            var inner = CreateSelectionRing(root.transform, "Inner Selection Ring", materials.Particle,
                1.35f, 0.055f, new Color(1f, 1f, 1f, 0.78f));
            root.SetActive(false);
            return new SelectionMarkerVisuals(root.transform, outer, inner);
        }

        private static LineRenderer CreateSelectionRing(
            Transform parent,
            string name,
            Material material,
            float radius,
            float width,
            Color color)
        {
            const int segmentCount = 64;
            var ringObject = new GameObject(name);
            ringObject.transform.SetParent(parent, false);
            var ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = segmentCount;
            ring.widthMultiplier = width;
            ring.startColor = color;
            ring.endColor = color;
            ring.numCornerVertices = 2;
            ring.numCapVertices = 2;
            ring.textureMode = LineTextureMode.Stretch;
            ring.alignment = LineAlignment.View;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            for (var index = 0; index < segmentCount; index++)
            {
                var radians = index / (float)segmentCount * Mathf.PI * 2f;
                ring.SetPosition(index, new Vector3(Mathf.Cos(radians) * radius, 0f,
                    Mathf.Sin(radians) * radius));
            }
            return ring;
        }

        private static BattleArena3DVfxPool BuildVfx(
            Transform parent,
            BattleArena3DProjectile projectileTemplate,
            Transform projectileRoot,
            BattleArena3DTelegraph telegraphTemplate,
            Transform telegraphRoot,
            ArenaMaterials materials)
        {
            var root = new GameObject("Pooled Battle VFX");
            root.transform.SetParent(parent);
            var impact = CreateParticleSystem(root.transform, "Impact Sparks", materials.Particle,
                2200, 0.55f, 4.2f, 0.11f, 0.25f);
            var magic = CreateParticleSystem(root.transform, "Magic Bursts", materials.Particle,
                2600, 0.9f, 3.2f, 0.18f, -0.15f);
            var healing = CreateParticleSystem(root.transform, "Healing Motes", materials.Particle,
                1800, 1.25f, 1.7f, 0.14f, -0.35f);
            var blood = CreateParticleSystem(root.transform, "Blood Splatters", materials.Particle,
                1800, 0.85f, 2.8f, 0.09f, 0.75f);
            var shield = CreateParticleSystem(root.transform, "Shield Shards", materials.Particle,
                1800, 0.75f, 2.6f, 0.14f, -0.1f);
            var slash = CreateParticleSystem(root.transform, "Shared Slash Streaks", materials.Particle,
                2200, 0.42f, 5.6f, 0.075f, 0.05f);
            var death = CreateParticleSystem(root.transform, "Shared Death Bursts", materials.Particle,
                2600, 1.15f, 4.4f, 0.16f, 0.32f);
            var pool = root.AddComponent<BattleArena3DVfxPool>();
            pool.ConfigureEditor(impact, magic, healing, blood, shield, slash, death, projectileTemplate,
                projectileRoot, 160, telegraphTemplate, telegraphRoot, 64);
            return pool;
        }

        private static ParticleSystem CreateParticleSystem(
            Transform parent,
            string name,
            Material material,
            int maxParticles,
            float lifetime,
            float speed,
            float size,
            float gravity)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            var system = root.AddComponent<ParticleSystem>();
            var main = system.main;
            main.loop = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.gravityModifier = gravity;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.22f;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static Camera ConfigureCamera(Scene scene, Transform systems)
        {
            var cameraObject = FindRoot(scene, "Main Camera") ?? new GameObject("Main Camera");
            if (cameraObject.scene != scene) SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            var camera = GetOrAdd<Camera>(cameraObject);
            GetOrAdd<AudioListener>(cameraObject);
            camera.orthographic = false;
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 350f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 46f, -36f), Quaternion.Euler(52f, 0f, 0f));
            var data = GetOrAdd<UniversalAdditionalCameraData>(cameraObject);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(data);
            return camera;
        }

        private static void ConfigureLighting(Scene scene, Transform systems, ArenaMaterials materials)
        {
            var lightObject = FindRoot(scene, "Directional Light") ?? new GameObject("Directional Light");
            if (lightObject.scene != scene) SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var light = GetOrAdd<Light>(lightObject);
            light.type = LightType.Directional;
            light.color = new Color(0.62f, 0.72f, 1f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            var fill = new GameObject("Warm Battle Fill");
            fill.transform.SetParent(systems);
            fill.transform.rotation = Quaternion.Euler(58f, 148f, 0f);
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(1f, 0.32f, 0.13f);
            fillLight.intensity = 0.42f;
            fillLight.shadows = LightShadows.None;
            var probeObject = new GameObject("Arena Reflection Probe");
            probeObject.transform.SetParent(systems);
            probeObject.transform.position = new Vector3(0f, 7f, 0f);
            var probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.size = new Vector3(100f, 24f, 70f);
            probe.resolution = 64;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0065f;
            RenderSettings.fogColor = new Color(0.025f, 0.035f, 0.065f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.12f, 0.17f, 0.28f);
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.07f, 0.11f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.022f, 0.03f);
            RenderSettings.ambientIntensity = 0.82f;
        }

        private static void ConfigureVolume(Transform systems)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Battle Arena Volume";
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            var bloom = GetOrAddVolume<Bloom>(profile);
            bloom.intensity.Override(0.52f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.66f);
            var color = GetOrAddVolume<ColorAdjustments>(profile);
            color.postExposure.Override(-0.08f);
            color.contrast.Override(17f);
            color.saturation.Override(-6f);
            var vignette = GetOrAddVolume<Vignette>(profile);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.72f);
            var tonemapping = GetOrAddVolume<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);
            EditorUtility.SetDirty(profile);
            var volumeObject = new GameObject("Global Battle Volume");
            volumeObject.transform.SetParent(systems);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        private static T GetOrAddVolume<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) return component;
            return profile.Add<T>(true);
        }

        private static ArenaMaterials CreateMaterials()
        {
            return new ArenaMaterials(
                EnsureMaterial("Void Foundation", new Color(0.018f, 0.023f, 0.04f), 0.52f),
                EnsureMaterial("Slate Combat Floor", new Color(0.06f, 0.075f, 0.11f), 0.42f),
                EnsureMaterial("Ancient Arena Stone", new Color(0.12f, 0.13f, 0.17f), 0.28f),
                EnsureMaterial("Dark Forged Metal", new Color(0.035f, 0.042f, 0.062f), 0.72f),
                EnsureMaterial("Arena Gold", new Color(0.62f, 0.38f, 0.08f), 0.74f, true),
                EnsureMaterial("Azure Runes", new Color(0.05f, 0.42f, 1f), 0.58f, true),
                EnsureMaterial("Horde Portal", new Color(0.5f, 0.05f, 1f), 0.45f, true),
                EnsureMaterial("Aegis Squad", new Color(0.05f, 0.45f, 1f), 0.56f, true),
                EnsureMaterial("Ember Squad", new Color(1f, 0.18f, 0.04f), 0.56f, true),
                EnsureMaterial("Vanguard Squad", new Color(0.58f, 0.16f, 1f), 0.56f, true),
                EnsureMaterial("Horde Flesh", new Color(0.35f, 0.48f, 0.12f), 0.23f),
                EnsureUnlitMaterial("Health Bar", new Color(0.92f, 0.025f, 0.018f)),
                EnsureUnlitMaterial("Temporary Shield", new Color(0.9f, 0.95f, 1f)),
                EnsureUnlitMaterial("Bar Background", new Color(0.018f, 0.02f, 0.028f)),
                EnsureParticleMaterial("Battle Particles"),
                EnsureUnlitMaterial("Projectile Trail", new Color(0.35f, 0.72f, 1f)));
        }

        private static Material RoleMaterial(RaidCombatRole role, ArenaMaterials materials)
        {
            return role switch
            {
                RaidCombatRole.Tank => materials.Aegis,
                RaidCombatRole.Healer => EnsureMaterial("Healer Role", new Color(0.12f, 1f, 0.42f), 0.5f, true),
                RaidCombatRole.Mage => materials.Vanguard,
                RaidCombatRole.Ranger => EnsureMaterial("Ranger Role", new Color(1f, 0.78f, 0.08f), 0.5f, true),
                RaidCombatRole.Assassin => EnsureMaterial("Assassin Role", new Color(1f, 0.08f, 0.52f), 0.5f, true),
                _ => materials.Ember
            };
        }

        private static Material EnsureMaterial(string name, Color color, float smoothness, bool emission = false)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                {
                    name = name,
                    enableInstancing = true
                };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.5f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureUnlitMaterial(string name, Color color)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"))
                {
                    name = name,
                    enableInstancing = true
                };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureParticleMaterial(string name)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                             Shader.Find("Particles/Standard Unlit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool removeCollider = false,
            Quaternion? rotation = null)
        {
            var created = GameObject.CreatePrimitive(type);
            created.name = name;
            created.transform.SetParent(parent);
            created.transform.SetPositionAndRotation(position, rotation ?? created.transform.rotation);
            created.transform.localScale = scale;
            var renderer = created.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = removeCollider ? ShadowCastingMode.Off : ShadowCastingMode.On;
            if (removeCollider) UnityEngine.Object.DestroyImmediate(created.GetComponent<Collider>());
            return created;
        }

        private static GameObject CreateLocalPrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool removeCollider = false,
            Quaternion? localRotation = null)
        {
            var created = CreatePrimitive(type, name, parent, parent.position, localScale, material, removeCollider);
            created.transform.localPosition = localPosition;
            created.transform.localRotation = localRotation ?? Quaternion.identity;
            return created;
        }

        private static void RemoveImportedComponents(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                UnityEngine.Object.DestroyImmediate(behaviour);
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
            if (openedForValidation) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var director = roots.SelectMany(root =>
                    root.GetComponentsInChildren<BattleArena3DDirector>(true)).FirstOrDefault();
                var presentation = roots.SelectMany(root =>
                    root.GetComponentsInChildren<BattleArena3DPresentationController>(true)).FirstOrDefault();
                var feedback = roots.SelectMany(root =>
                    root.GetComponentsInChildren<BattleArena3DCombatFeedback>(true)).FirstOrDefault();
                var vfxPool = roots.SelectMany(root =>
                    root.GetComponentsInChildren<BattleArena3DVfxPool>(true)).FirstOrDefault();
                var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
                var surface = roots.SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true)).FirstOrDefault();
                var units = roots.SelectMany(root => root.GetComponentsInChildren<BattleArena3DUnit>(true)).ToArray();
                var hunters = units.Where(unit => unit.Faction == BattleArenaFaction3D.Hunters).ToArray();
                var template = units.FirstOrDefault(unit => unit.gameObject.name == "Inactive Monster Template");
                var selectionMarker = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(candidate => candidate.name == "Global Selection Marker");
                Check(FindRoot(scene, "3D Arena Environment") != null, "Missing persistent arena environment.", failures);
                Check(FindRoot(scene, "3D Battle Hunters") != null, "Missing persistent hunter root.", failures);
                Check(FindRoot(scene, "3D Battle Systems") != null, "Missing battle systems root.", failures);
                Check(camera != null && !camera.orthographic, "The spectator camera must be perspective.", failures);
                Check(camera != null && camera.GetComponent<BattleArena3DCameraRig>() != null,
                    "The spectator camera is missing BattleArena3DCameraRig.", failures);
                Check(surface != null && surface.navMeshData != null, "The 3D arena NavMesh has not been baked.", failures);
                Check(director != null && director.IsConfigured, "BattleArena3DDirector is missing references.", failures);
                Check(presentation != null && presentation.IsConfigured,
                    "BattleArena3DPresentationController is missing accessibility or feedback references.", failures);
                Check(presentation != null && presentation.Options.WorldBars == BattleArenaWorldBars3D.Contextual,
                    "The authored arena should default to contextual world health bars.", failures);
                Check(feedback != null && feedback.IsConfigured,
                    "BattleArena3DCombatFeedback is missing camera, VFX, or selection-marker references.", failures);
                Check(vfxPool != null && vfxPool.IsConfigured,
                    "The shared 3D arena VFX pool is missing authored particle or pooled-effect references.", failures);
                Check(selectionMarker != null && !selectionMarker.gameObject.activeSelf &&
                      selectionMarker.GetComponentsInChildren<LineRenderer>(true).Length == 2,
                    "The arena needs one inactive global selection marker with two shared rings.", failures);
                Check(hunters.Length == BattleSurvivalHunterCatalog.HunterCount,
                    $"Expected {BattleSurvivalHunterCatalog.HunterCount} persistent hunters; found {hunters.Length}.",
                    failures);
                Check(hunters.Select(hunter => hunter.StableId).Distinct().Count() == hunters.Length,
                    "Hunter stable IDs must be unique.", failures);
                Check(hunters.Count(hunter => hunter.IsSergeant) == BattleSurvivalHunterCatalog.SquadCount,
                    "Expected one authored sergeant per squad.", failures);
                Check(hunters.All(hunter => hunter.NavigationAgent != null && hunter.Hurtbox != null &&
                                                    hunter.View != null && hunter.Abilities.Count == 3),
                    "Every hunter needs NavMeshAgent, 3D hurtbox, view, and three abilities.", failures);
                Check(template != null && !template.gameObject.activeSelf,
                    "The pooled monster template must exist and remain inactive.", failures);
                Check(template != null && template.View != null && template.View.HasMonsterSilhouette,
                    "The monster template is missing its authored archetype silhouette references.", failures);
                var telegraphTemplate = roots.SelectMany(root => root.GetComponentsInChildren<BattleArena3DTelegraph>(true))
                    .FirstOrDefault(candidate => candidate.gameObject.name == "Inactive Telegraph Template");
                Check(telegraphTemplate != null && !telegraphTemplate.gameObject.activeSelf,
                    "The pooled telegraph template must exist and remain inactive.", failures);
                Check(roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                          .Count(transform => transform.name == "Horde Spawn Point") == 4,
                    "Expected four authored horde gates.", failures);
                Check(!EditorBuildSettings.scenes.Any(candidate => candidate.path == ScenePath),
                    "3D Test Arena is a development fixture and should remain outside Build Settings.", failures);
            }
            finally
            {
                if (openedForValidation && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            return failures;
        }

        private static void Check(bool condition, string message, ICollection<string> failures)
        {
            if (!condition) failures.Add(message);
        }

        private static GameObject ReplaceRoot(Scene scene, string name)
        {
            var existing = FindRoot(scene, name);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
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
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            return child;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent<T>(out var component) ? component : target.AddComponent<T>();
        }

        private static string SquadName(int index) => index switch
        {
            0 => "Aegis",
            1 => "Ember",
            _ => "Vanguard"
        };

        private static void ValidateSourceAssets()
        {
            var missing = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath) == null) missing.Add(CharacterPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath) == null) missing.Add(SwordPath);
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) == null) missing.Add(ControllerPath);
            if (missing.Count > 0)
            {
                throw new InvalidOperationException("3D Test Arena is missing source assets:\n" +
                                                    string.Join("\n", missing));
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(DataFolder);
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var separator = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..separator], path[(separator + 1)..]);
        }

        private static void WriteResult(string status, IEnumerable<string> lines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectRoot);
            File.WriteAllLines(ResultPath, new[] { status }.Concat(lines));
        }
    }
}
