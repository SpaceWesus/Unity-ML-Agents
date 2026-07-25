using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Turtle.Combat.Editor
{
    public static class WeaponsTestingSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Weapons Testing.unity";
        private const string DataFolder = "Assets/Data/Combat";
        private const string AbilityFolder = DataFolder + "/Abilities";
        private const string MaterialFolder = "Assets/Materials/WeaponsTesting";
        private const string ControllerPath = DataFolder + "/Greatsword Combat.controller";
        private const string MoveSetPath = DataFolder + "/Training Greatsword.asset";
        private const string MageLoadoutPath = AbilityFolder + "/Mage Prototype Loadout.asset";
        private const string PackRoot =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE";
        private const string CharacterPath = PackRoot + "/Models/Characters/RPG-Character.FBX";
        private const string SwordPath = PackRoot + "/Models/Weapons/2Hand-Sword.FBX";
        private const string AnimationRoot = PackRoot + "/Animations/2Hand-Sword/";

        private readonly struct FighterBuild
        {
            public FighterBuild(
                Combatant combatant,
                CombatAgentDriver driver,
                PlayerCombatCommandSource playerSource,
                CombatAbilityController abilities)
            {
                Combatant = combatant;
                Driver = driver;
                PlayerSource = playerSource;
                Abilities = abilities;
            }

            public Combatant Combatant { get; }
            public CombatAgentDriver Driver { get; }
            public PlayerCombatCommandSource PlayerSource { get; }
            public CombatAbilityController Abilities { get; }
        }

        [MenuItem("Turtle/Combat/Open Weapons Testing")]
        public static void Open()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Build(true);
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Turtle/Combat/Create Weapons Testing (if missing)")]
        public static void CreateIfMissing()
        {
            Build(false);
        }

        [MenuItem("Turtle/Combat/Rebuild Weapons Testing")]
        public static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Weapons Testing?",
                    "This replaces the authored Weapons Testing scene. Combat scripts and tuning assets are preserved.",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }
            Build(true);
        }

        [MenuItem("Turtle/Combat/Validate Weapons Testing")]
        public static void ValidateScene()
        {
            var issues = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                issues.Add($"Missing scene: {ScenePath}");
            }
            if (AssetDatabase.LoadAssetAtPath<WeaponMoveSetDefinition>(MoveSetPath) == null)
            {
                issues.Add($"Missing move set: {MoveSetPath}");
            }
            if (AssetDatabase.LoadAssetAtPath<CombatAbilityLoadoutDefinition>(MageLoadoutPath) == null)
            {
                issues.Add($"Missing ability loadout: {MageLoadoutPath}");
            }
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) == null)
            {
                issues.Add($"Missing animator controller: {ControllerPath}");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath) == null)
            {
                issues.Add($"Missing imported humanoid: {CharacterPath}");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath) == null)
            {
                issues.Add($"Missing imported sword: {SwordPath}");
            }

            if (issues.Count == 0)
            {
                Debug.Log("Weapons Testing validation passed: scene, move set, controller, character, and sword assets are present.");
            }
            else
            {
                Debug.LogError("Weapons Testing validation failed:\n" + string.Join("\n", issues));
            }
        }

        [MenuItem("Turtle/Combat/Upgrade Combat Volumes In Weapons Testing")]
        public static void UpgradeCombatVolumes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before upgrading Weapons Testing combat volumes.");
                return;
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var combatant in root.GetComponentsInChildren<Combatant>(true))
                {
                    var hurtbox = combatant.GetComponentInChildren<CombatHurtbox>(true);
                    if (hurtbox == null)
                    {
                        hurtbox = CreateHurtbox(
                            combatant.transform,
                            combatant,
                            combatant.IsTargetDummy
                                ? new Vector3(0f, 1.15f, 0f)
                                : new Vector3(0f, 0.95f, 0f),
                            combatant.IsTargetDummy ? 2.3f : 1.8f,
                            combatant.IsTargetDummy ? 0.5f : 0.4f);
                    }

                    CombatWeaponHitbox weaponHitbox = null;
                    if (!combatant.IsTargetDummy)
                    {
                        var sword = FindDescendant(combatant.transform, "Equipped 2H Sword");
                        if (sword != null)
                        {
                            weaponHitbox = sword.GetComponent<CombatWeaponHitbox>();
                            if (weaponHitbox == null)
                            {
                                weaponHitbox = sword.gameObject.AddComponent<CombatWeaponHitbox>();
                            }
                            weaponHitbox.ConfigureEditor(combatant);
                        }
                    }

                    combatant.ConfigureCombatVolumesEditor(weaponHitbox, hurtbox);
                    EditorUtility.SetDirty(combatant);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "Weapons Testing combat volumes upgraded and saved without rebuilding the arena.");
        }

        [MenuItem("Turtle/Combat/Upgrade Ability System In Weapons Testing")]
        public static void UpgradeAbilitySystem()
        {
            UpgradeGameplaySystems();
        }

        [MenuItem("Turtle/Combat/Upgrade Arena Feedback And Abilities")]
        public static void UpgradeGameplaySystems()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before upgrading Weapons Testing gameplay systems.");
                return;
            }

            EnsureFolders();
            var loadout = EnsureMageLoadout();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Combatant player = null;
            Camera worldCamera = null;
            GameObject systems = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                worldCamera ??= root.GetComponentInChildren<Camera>(true);
                if (root.name == "Combat Lab Systems")
                {
                    systems = root;
                }

                foreach (var combatant in root.GetComponentsInChildren<Combatant>(true))
                {
                    if (combatant.IsTargetDummy)
                    {
                        UpgradeTargetDummy(combatant);
                        continue;
                    }

                    var abilities = combatant.GetComponent<CombatAbilityController>();
                    if (abilities == null)
                    {
                        abilities = combatant.gameObject.AddComponent<CombatAbilityController>();
                    }
                    abilities.ConfigureEditor(combatant, loadout);
                    combatant.ConfigureAbilityControllerEditor(abilities);
                    EditorUtility.SetDirty(abilities);
                    EditorUtility.SetDirty(combatant);

                    if (combatant.GetComponent<PlayerCombatCommandSource>() != null)
                    {
                        player = combatant;
                    }
                }
            }

            systems ??= new GameObject("Combat Lab Systems");
            foreach (var root in scene.GetRootGameObjects())
            {
                var hud = root.GetComponentInChildren<CombatLabHud>(true);
                if (hud != null && player != null)
                {
                    hud.ConfigureEditor(player);
                    EditorUtility.SetDirty(hud);
                }
            }
            var worldStatusHud = systems.GetComponent<CombatWorldStatusHud>();
            if (worldStatusHud == null)
            {
                worldStatusHud = systems.AddComponent<CombatWorldStatusHud>();
            }
            worldStatusHud.ConfigureEditor(worldCamera, player);
            EditorUtility.SetDirty(worldStatusHud);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "Weapons Testing status bars, target dummies, and ability loadouts upgraded and saved without rebuilding the arena.");
        }

        private static void Build(bool rebuild)
        {
            EnsureFolders();
            if (!ValidateSourceAssets())
            {
                return;
            }
            var moveSet = EnsureMoveSet();
            var abilityLoadout = EnsureMageLoadout();
            if (!rebuild && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EnsureAnimatorController(false);
                Open();
                return;
            }

            var controller = EnsureAnimatorController(rebuild);
            if (rebuild && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.IsValid() && loaded.isLoaded)
                {
                    if (SceneManager.sceneCount == 1)
                    {
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    }
                    EditorSceneManager.CloseScene(loaded, true);
                }
                AssetDatabase.DeleteAsset(ScenePath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildArena(scene, moveSet, abilityLoadout, controller);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Authored production combat laboratory at {ScenePath}");
        }

        private static void BuildArena(
            Scene scene,
            WeaponMoveSetDefinition moveSet,
            CombatAbilityLoadoutDefinition abilityLoadout,
            RuntimeAnimatorController controller)
        {
            var materials = CreateMaterials();
            var environment = new GameObject("Weapons Testing Arena");
            CreateArenaArchitecture(environment.transform, materials);
            var spawnRoot = new GameObject("Combatant Spawn Points").transform;

            var playerSpawn = CreateSpawn(spawnRoot, "Player Spawn", new Vector3(0f, 0.05f, -18f), 0f);
            var player = CreateFighter(
                "Player Agent",
                playerSpawn.position,
                playerSpawn.rotation,
                CombatTeam.Azure,
                true,
                moveSet,
                abilityLoadout,
                controller,
                materials.Azure);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 6.5f, -31f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 220f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.02f, 0.035f);
            cameraObject.AddComponent<AudioListener>();
            var cameraRig = cameraObject.AddComponent<CombatCameraRig>();
            cameraRig.ConfigureEditor(player.Combatant.transform, player.Combatant);
            player.PlayerSource.ConfigureEditor(camera);

            var arenaCombatants = new List<Combatant>();
            var arenaSpawns = new List<Transform>();
            var aiDrivers = new List<CombatAgentDriver>();
            var aiLayout = new[]
            {
                (new Vector3(-10f, 0.05f, -5f), 35f, CombatTeam.Azure, "Azure Vanguard"),
                (new Vector3(-15f, 0.05f, 5f), 70f, CombatTeam.Azure, "Azure Duelist"),
                (new Vector3(-8f, 0.05f, 13f), 120f, CombatTeam.Azure, "Azure Sentinel"),
                (new Vector3(10f, 0.05f, 5f), 215f, CombatTeam.Crimson, "Crimson Vanguard"),
                (new Vector3(15f, 0.05f, -5f), 250f, CombatTeam.Crimson, "Crimson Duelist"),
                (new Vector3(8f, 0.05f, 13f), 190f, CombatTeam.Crimson, "Crimson Sentinel")
            };
            foreach (var (position, yaw, team, name) in aiLayout)
            {
                var spawn = CreateSpawn(spawnRoot, name + " Spawn", position, yaw);
                var teamMaterial = team == CombatTeam.Azure ? materials.Azure : materials.Crimson;
                var fighter = CreateFighter(
                    name,
                    position,
                    spawn.rotation,
                    team,
                    false,
                    moveSet,
                    abilityLoadout,
                    controller,
                    teamMaterial);
                arenaCombatants.Add(fighter.Combatant);
                arenaSpawns.Add(spawn);
                aiDrivers.Add(fighter.Driver);
            }

            var dummyRoot = new GameObject("Target Dummies").transform;
            var dummyPositions = new[]
            {
                new Vector3(-27f, 0f, -17f), new Vector3(-21f, 0f, -17f),
                new Vector3(-15f, 0f, -17f), new Vector3(15f, 0f, -17f),
                new Vector3(21f, 0f, -17f), new Vector3(27f, 0f, -17f),
                new Vector3(-27f, 0f, 17f), new Vector3(-21f, 0f, 17f),
                new Vector3(-15f, 0f, 17f), new Vector3(15f, 0f, 17f),
                new Vector3(21f, 0f, 17f), new Vector3(27f, 0f, 17f),
                new Vector3(-30f, 0f, 0f), new Vector3(30f, 0f, 0f)
            };
            for (var index = 0; index < dummyPositions.Length; index++)
            {
                var position = dummyPositions[index];
                var dummy = CreateDummy(
                    dummyRoot,
                    $"Target Dummy {index + 1:00}",
                    position,
                    moveSet,
                    materials.Wood,
                    materials.Target);
                var spawn = CreateSpawn(spawnRoot, dummy.name + " Spawn", position, 180f);
                arenaCombatants.Add(dummy);
                arenaSpawns.Add(spawn);
            }

            var systems = new GameObject("Combat Lab Systems");
            var director = systems.AddComponent<CombatLabDirector>();
            director.ConfigureEditor(
                player.Combatant,
                playerSpawn,
                arenaCombatants.ToArray(),
                arenaSpawns.ToArray(),
                aiDrivers.ToArray());
            var hud = systems.AddComponent<CombatLabHud>();
            hud.ConfigureEditor(player.Combatant);
            var worldStatusHud = systems.AddComponent<CombatWorldStatusHud>();
            worldStatusHud.ConfigureEditor(camera, player.Combatant);

            CreateLighting();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.17f, 0.21f, 0.31f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.1f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.03f, 0.05f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.02f, 0.028f, 0.045f);
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 145f;
            RenderSettings.skybox = null;
        }

        private static FighterBuild CreateFighter(
            string fighterName,
            Vector3 position,
            Quaternion rotation,
            CombatTeam team,
            bool playerControlled,
            WeaponMoveSetDefinition moveSet,
            CombatAbilityLoadoutDefinition abilityLoadout,
            RuntimeAnimatorController controller,
            Material teamMaterial)
        {
            var root = new GameObject(fighterName);
            root.transform.SetPositionAndRotation(position, rotation);
            var capsule = root.AddComponent<CharacterController>();
            capsule.height = 1.9f;
            capsule.radius = 0.43f;
            capsule.center = new Vector3(0f, 0.95f, 0f);
            capsule.stepOffset = 0.3f;
            capsule.skinWidth = 0.04f;
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "Team Ring",
                root.transform,
                position + Vector3.up * 0.03f,
                new Vector3(0.78f, 0.025f, 0.78f),
                teamMaterial,
                true);

            var characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(characterAsset);
            model.name = "RPG Character Model";
            model.transform.SetParent(root.transform, false);
            RemoveCollidersAndScripts(model);
            var animator = model.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.GetComponent<CombatAnimationEventRelay>() == null)
            {
                animator.gameObject.AddComponent<CombatAnimationEventRelay>();
            }

            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand) ?? model.transform;
            var swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
            var sword = (GameObject)PrefabUtility.InstantiatePrefab(swordAsset);
            sword.name = "Equipped 2H Sword";
            sword.transform.SetParent(hand, false);
            sword.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            RemoveCollidersAndScripts(sword);

            var animationView = root.AddComponent<CombatAnimationView>();
            animationView.ConfigureEditor(animator, sword.transform);
            var combatant = root.AddComponent<Combatant>();
            var hurtbox = CreateHurtbox(
                root.transform,
                combatant,
                new Vector3(0f, 0.95f, 0f),
                1.8f,
                0.4f);
            var weaponHitbox = sword.AddComponent<CombatWeaponHitbox>();
            weaponHitbox.ConfigureEditor(combatant);
            var abilities = root.AddComponent<CombatAbilityController>();
            abilities.ConfigureEditor(combatant, abilityLoadout);
            MonoBehaviour source;
            PlayerCombatCommandSource playerSource = null;
            if (playerControlled)
            {
                playerSource = root.AddComponent<PlayerCombatCommandSource>();
                source = playerSource;
            }
            else
            {
                source = root.AddComponent<AiCombatCommandSource>();
            }
            var driver = root.AddComponent<CombatAgentDriver>();
            driver.ConfigureEditor(source);
            combatant.ConfigureEditor(
                fighterName,
                team,
                false,
                playerControlled ? 140f : 110f,
                playerControlled ? 7f : 5.7f,
                moveSet,
                animationView,
                driver,
                weaponHitbox,
                hurtbox,
                abilities);
            return new FighterBuild(combatant, driver, playerSource, abilities);
        }

        private static Combatant CreateDummy(
            Transform parent,
            string dummyName,
            Vector3 position,
            WeaponMoveSetDefinition moveSet,
            Material wood,
            Material target)
        {
            var root = new GameObject(dummyName);
            root.transform.SetParent(parent);
            root.transform.position = position;
            var controller = root.AddComponent<CharacterController>();
            controller.height = 2.35f;
            controller.radius = 0.48f;
            controller.center = new Vector3(0f, 1.15f, 0f);

            var visualRoot = new GameObject("Target Dummy Visuals").transform;
            visualRoot.SetParent(root.transform, false);
            CreateLocalPrimitive(PrimitiveType.Cylinder, "Base", visualRoot,
                new Vector3(0f, 0.12f, 0f), new Vector3(0.8f, 0.12f, 0.8f), target, true);
            CreateLocalPrimitive(PrimitiveType.Cylinder, "Post", visualRoot,
                new Vector3(0f, 1.12f, 0f), new Vector3(0.16f, 1f, 0.16f), wood, true);
            CreateLocalPrimitive(PrimitiveType.Sphere, "Head", visualRoot,
                new Vector3(0f, 2.05f, 0f), Vector3.one * 0.32f, wood, true);
            CreateLocalPrimitive(PrimitiveType.Cube, "Crossbar", visualRoot,
                new Vector3(0f, 1.55f, 0f), new Vector3(1.4f, 0.14f, 0.14f), wood, true);
            CreateLocalPrimitive(PrimitiveType.Cylinder, "Target", visualRoot,
                new Vector3(0f, 1.3f, -0.13f), new Vector3(0.5f, 0.08f, 0.5f), target, true)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var combatant = root.AddComponent<Combatant>();
            var hurtbox = CreateHurtbox(
                root.transform,
                combatant,
                new Vector3(0f, 1.15f, 0f),
                2.3f,
                0.5f);
            combatant.ConfigureEditor(
                dummyName,
                CombatTeam.Neutral,
                true,
                180f,
                0f,
                moveSet,
                null,
                null,
                null,
                hurtbox);
            var dummyFeedback = root.AddComponent<CombatTargetDummy>();
            dummyFeedback.ConfigureEditor(combatant, visualRoot);
            return combatant;
        }

        private static void UpgradeTargetDummy(Combatant combatant)
        {
            var root = combatant.transform;
            var visualRoot = root.Find("Target Dummy Visuals");
            if (visualRoot == null)
            {
                visualRoot = new GameObject("Target Dummy Visuals").transform;
                visualRoot.SetParent(root, false);
            }
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;

            ConfigureDummyPart(
                root,
                visualRoot,
                "Base",
                new Vector3(0f, 0.12f, 0f),
                Quaternion.identity,
                new Vector3(0.8f, 0.12f, 0.8f));
            ConfigureDummyPart(
                root,
                visualRoot,
                "Post",
                new Vector3(0f, 1.12f, 0f),
                Quaternion.identity,
                new Vector3(0.16f, 1f, 0.16f));
            ConfigureDummyPart(
                root,
                visualRoot,
                "Head",
                new Vector3(0f, 2.05f, 0f),
                Quaternion.identity,
                Vector3.one * 0.32f);
            ConfigureDummyPart(
                root,
                visualRoot,
                "Crossbar",
                new Vector3(0f, 1.55f, 0f),
                Quaternion.identity,
                new Vector3(1.4f, 0.14f, 0.14f));
            ConfigureDummyPart(
                root,
                visualRoot,
                "Target",
                new Vector3(0f, 1.3f, -0.13f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.5f, 0.08f, 0.5f));

            var feedback = root.GetComponent<CombatTargetDummy>();
            if (feedback == null)
            {
                feedback = root.gameObject.AddComponent<CombatTargetDummy>();
            }
            feedback.ConfigureEditor(combatant, visualRoot);
            EditorUtility.SetDirty(feedback);

            foreach (var driver in root.GetComponents<CombatAgentDriver>())
            {
                driver.enabled = false;
                EditorUtility.SetDirty(driver);
            }
            foreach (var source in root.GetComponents<AiCombatCommandSource>())
            {
                source.enabled = false;
                EditorUtility.SetDirty(source);
            }
            foreach (var source in root.GetComponents<PlayerCombatCommandSource>())
            {
                source.enabled = false;
                EditorUtility.SetDirty(source);
            }
        }

        private static void ConfigureDummyPart(
            Transform dummyRoot,
            Transform visualRoot,
            string partName,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            var part = visualRoot.Find(partName) ?? dummyRoot.Find(partName);
            if (part == null)
            {
                Debug.LogWarning(
                    $"{dummyRoot.name} is missing visual part '{partName}'.",
                    dummyRoot);
                return;
            }

            part.SetParent(visualRoot, false);
            part.localPosition = localPosition;
            part.localRotation = localRotation;
            part.localScale = localScale;
            EditorUtility.SetDirty(part);
        }

        private static CombatHurtbox CreateHurtbox(
            Transform parent,
            Combatant owner,
            Vector3 center,
            float height,
            float radius)
        {
            var hurtboxObject = new GameObject("Body Hurtbox");
            hurtboxObject.transform.SetParent(parent, false);
            var capsule = hurtboxObject.AddComponent<CapsuleCollider>();
            capsule.center = center;
            capsule.height = height;
            capsule.radius = radius;
            capsule.direction = 1;
            capsule.isTrigger = true;
            var hurtbox = hurtboxObject.AddComponent<CombatHurtbox>();
            hurtbox.ConfigureEditor(owner, capsule);
            return hurtbox;
        }

        private static Transform FindDescendant(Transform root, string descendantName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == descendantName)
                {
                    return child;
                }
            }
            return null;
        }

        private static void CreateArenaArchitecture(Transform parent, ArenaMaterials materials)
        {
            CreatePrimitive(PrimitiveType.Cube, "Arena Foundation", parent,
                new Vector3(0f, -0.4f, 0f), new Vector3(92f, 0.8f, 68f), materials.Foundation);
            CreatePrimitive(PrimitiveType.Cube, "Combat Floor", parent,
                new Vector3(0f, 0f, 0f), new Vector3(76f, 0.12f, 52f), materials.Floor);

            for (var lane = -3; lane <= 3; lane++)
            {
                CreatePrimitive(PrimitiveType.Cube, $"Lane Marking {lane + 4}", parent,
                    new Vector3(lane * 10f, 0.071f, 0f), new Vector3(0.12f, 0.02f, 48f), materials.Marking, true);
            }
            CreatePrimitive(PrimitiveType.Cylinder, "Central Duel Ring", parent,
                new Vector3(0f, 0.08f, 0f), new Vector3(9f, 0.03f, 9f), materials.Marking, true);
            CreatePrimitive(PrimitiveType.Cylinder, "Central Duel Floor", parent,
                new Vector3(0f, 0.09f, 0f), new Vector3(8.4f, 0.035f, 8.4f), materials.Floor, true);

            for (var side = -1; side <= 1; side += 2)
            {
                CreatePrimitive(PrimitiveType.Cube, side < 0 ? "West Wall" : "East Wall", parent,
                    new Vector3(side * 41f, 2.5f, 0f), new Vector3(2f, 5f, 60f), materials.Foundation);
                CreatePrimitive(PrimitiveType.Cube, side < 0 ? "West Grandstand" : "East Grandstand", parent,
                    new Vector3(side * 36f, 2f, 0f), new Vector3(8f, 1.2f, 56f), materials.Stand);
            }
            for (var side = -1; side <= 1; side += 2)
            {
                CreatePrimitive(PrimitiveType.Cube, side < 0 ? "South Wall" : "North Wall", parent,
                    new Vector3(0f, 2.5f, side * 29f), new Vector3(84f, 5f, 2f), materials.Foundation);
            }

            for (var index = 0; index < 18; index++)
            {
                var x = -38f + index % 9 * 9.5f;
                var z = index < 9 ? -27f : 27f;
                var pillar = CreatePrimitive(PrimitiveType.Cylinder, $"Arena Pillar {index + 1:00}", parent,
                    new Vector3(x, 3.2f, z), new Vector3(0.8f, 3.2f, 0.8f), materials.Foundation);
                CreatePrimitive(PrimitiveType.Sphere, "Arena Light", pillar.transform,
                    pillar.transform.position + Vector3.up * 3.5f, Vector3.one * 0.38f, materials.Emissive, true);
            }

            CreateSign(parent, "PLAYER VS AI", new Vector3(0f, 5.8f, -28f), materials.Azure);
            CreateSign(parent, "AI VS AI", new Vector3(0f, 5.8f, 28f), materials.Crimson);
        }

        private static void CreateSign(Transform parent, string name, Vector3 position, Material material)
        {
            CreatePrimitive(PrimitiveType.Cube, name, parent, position, new Vector3(13f, 1.4f, 0.3f), material, true);
        }

        private static Transform CreateSpawn(Transform parent, string name, Vector3 position, float yaw)
        {
            var spawn = new GameObject(name).transform;
            spawn.SetParent(parent);
            spawn.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return spawn;
        }

        private static void CreateLighting()
        {
            var keyObject = new GameObject("Arena Key Light");
            keyObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.72f, 0.8f, 1f);
            key.intensity = 1.45f;
            key.shadows = LightShadows.Soft;

            var fillObject = new GameObject("Arena Warm Fill");
            fillObject.transform.rotation = Quaternion.Euler(55f, 145f, 0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1f, 0.42f, 0.23f);
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;
        }

        private static void RemoveCollidersAndScripts(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(behaviour);
            }
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool removeCollider = false)
        {
            var created = GameObject.CreatePrimitive(type);
            created.name = name;
            created.transform.SetParent(parent);
            created.transform.position = position;
            created.transform.localScale = scale;
            created.GetComponent<Renderer>().sharedMaterial = material;
            if (removeCollider)
            {
                Object.DestroyImmediate(created.GetComponent<Collider>());
            }
            return created;
        }

        private static GameObject CreateLocalPrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 scale,
            Material material,
            bool removeCollider = false)
        {
            var created = CreatePrimitive(
                type,
                name,
                parent,
                parent.position,
                scale,
                material,
                removeCollider);
            created.transform.localPosition = localPosition;
            created.transform.localRotation = Quaternion.identity;
            return created;
        }

        private static WeaponMoveSetDefinition EnsureMoveSet()
        {
            var moveSet = AssetDatabase.LoadAssetAtPath<WeaponMoveSetDefinition>(MoveSetPath);
            if (moveSet == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(MoveSetPath) != null)
                {
                    AssetDatabase.DeleteAsset(MoveSetPath);
                }
                moveSet = ScriptableObject.CreateInstance<WeaponMoveSetDefinition>();
                AssetDatabase.CreateAsset(moveSet, MoveSetPath);
            }
            moveSet.ConfigureEditor(
                "Training Greatsword",
                new AttackDefinition
                {
                    damage = 24f,
                    range = 2.45f,
                    arc = 82f,
                    windup = 0.336f,
                    activeDuration = 0.444f,
                    recovery = 0.42f,
                    knockback = 1.6f,
                    lunge = 1.1f,
                    animationDuration = 1.2f,
                    animationState = "Attack Light",
                    movementMode = AttackMovementMode.Mobile,
                    dodgeCancelMode = AttackDodgeCancelMode.DodgeAllowed,
                    hitboxWindows = CreateLightAttackHitboxTimeline()
                },
                new AttackDefinition
                {
                    damage = 42f,
                    range = 2.75f,
                    arc = 64f,
                    windup = 0.312f,
                    activeDuration = 0.552f,
                    recovery = 0.336f,
                    knockback = 2.7f,
                    lunge = 1.5f,
                    animationDuration = 1.2f,
                    animationState = "Attack Heavy",
                    movementMode = AttackMovementMode.Anchored,
                    dodgeCancelMode = AttackDodgeCancelMode.DodgeAllowed,
                    hitboxWindows = CreateHeavyAttackHitboxTimeline()
                });
            var serializedMoveSet = new SerializedObject(moveSet);
            var scriptProperty = serializedMoveSet.FindProperty("m_Script");
            if (scriptProperty != null && scriptProperty.objectReferenceValue == null)
            {
                scriptProperty.objectReferenceValue = MonoScript.FromScriptableObject(moveSet);
                serializedMoveSet.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(moveSet);
            AssetDatabase.SaveAssetIfDirty(moveSet);
            return moveSet;
        }

        private static CombatAbilityLoadoutDefinition EnsureMageLoadout()
        {
            var arcaneBolt = EnsureAsset(
                AbilityFolder + "/Arcane Bolt.asset",
                (ProjectileAbilityDefinition ability) => ability.ConfigureEditor(
                    "mage.arcane-bolt",
                    "Arcane Bolt",
                    "Launch a fast elemental projectile that damages the first target struck.",
                    3.5f,
                    18f,
                    0.18f,
                    0.22f,
                    30f,
                    22f,
                    0.25f,
                    2.2f,
                    1.3f,
                    new Color(0.15f, 0.72f, 1f)));
            var spatialStep = EnsureAsset(
                AbilityFolder + "/Spatial Step.asset",
                (TeleportAbilityDefinition ability) => ability.ConfigureEditor(
                    "mage.spatial-step",
                    "Spatial Step",
                    "Fold space to reposition instantly in the aimed direction.",
                    6f,
                    24f,
                    0.08f,
                    0.18f,
                    7f,
                    new Color(0.62f, 0.28f, 1f)));
            var aegisBarrier = EnsureAsset(
                AbilityFolder + "/Aegis Barrier.asset",
                (BarrierAbilityDefinition ability) => ability.ConfigureEditor(
                    "mage.aegis-barrier",
                    "Aegis Barrier",
                    "Create a temporary barrier that absorbs incoming damage.",
                    9f,
                    32f,
                    0.25f,
                    0.25f,
                    70f,
                    6f,
                    new Color(0.12f, 0.85f, 1f)));
            var arcaneNova = EnsureAsset(
                AbilityFolder + "/Arcane Nova.asset",
                (AreaAbilityDefinition ability) => ability.ConfigureEditor(
                    "mage.arcane-nova",
                    "Arcane Nova",
                    "Ultimate: rupture the space ahead in a large damaging blast.",
                    0.65f,
                    0.45f,
                    70f,
                    5.2f,
                    2f,
                    3.5f,
                    new Color(0.8f, 0.18f, 1f)));
            var arcaneTempo = EnsureAsset(
                AbilityFolder + "/Arcane Tempo.asset",
                (CombatPassiveDefinition passive) => passive.ConfigureEditor(
                    "mage.arcane-tempo",
                    "Arcane Tempo",
                    "A Mage-derived passive that slightly accelerates cooldowns, ultimate gain, and spell damage.",
                    "Mage",
                    0.9f,
                    1.15f,
                    1.05f));

            return EnsureAsset(
                MageLoadoutPath,
                (CombatAbilityLoadoutDefinition loadout) => loadout.ConfigureEditor(
                    "Mage Prototype",
                    arcaneBolt,
                    spatialStep,
                    aegisBarrier,
                    arcaneNova,
                    new[] { arcaneTempo },
                    100f,
                    100f,
                    14f,
                    100f,
                    100f,
                    0.65f,
                    0.35f));
        }

        private static T EnsureAsset<T>(string path, Action<T> configure)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            configure(asset);
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        private static AttackHitboxWindow[] CreateLightAttackHitboxTimeline()
        {
            return new[]
            {
                CreateHitboxWindow(0.28f, 0.4f, 1.05f, new Vector3(0.5f, 0.5f, 1.65f)),
                CreateHitboxWindow(0.38f, 0.53f, 1.2f, new Vector3(0.58f, 0.58f, 1.9f)),
                CreateHitboxWindow(0.5f, 0.65f, 1.05f, new Vector3(0.5f, 0.5f, 1.7f))
            };
        }

        private static AttackHitboxWindow[] CreateHeavyAttackHitboxTimeline()
        {
            return new[]
            {
                CreateHitboxWindow(0.26f, 0.42f, 1.1f, new Vector3(0.62f, 0.62f, 1.9f)),
                CreateHitboxWindow(0.4f, 0.58f, 1.2f, new Vector3(0.72f, 0.72f, 2.1f)),
                CreateHitboxWindow(0.56f, 0.72f, 1.1f, new Vector3(0.65f, 0.65f, 1.9f))
            };
        }

        private static AttackHitboxWindow CreateHitboxWindow(
            float start,
            float end,
            float bladeCenter,
            Vector3 size)
        {
            return new AttackHitboxWindow
            {
                startNormalized = start,
                endNormalized = end,
                localCenter = new Vector3(-0.177f, -0.077f, bladeCenter),
                localSize = size,
                localEulerAngles = Vector3.zero
            };
        }

        private static RuntimeAnimatorController EnsureAnimatorController(bool rebuild)
        {
            if (rebuild && AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (existing != null)
            {
                return existing;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            var clips = new[]
            {
                ("Idle", "RPG-Character@2Hand-Sword-Idle.FBX"),
                ("Run", "RPG-Character@2Hand-Sword-Run-Forward.FBX"),
                ("Attack Light", "RPG-Character@2Hand-Sword-Attack1.FBX"),
                ("Attack Heavy", "RPG-Character@2Hand-Sword-Attack4.FBX"),
                ("Dodge", "RPG-Character@2Hand-Sword-DiveRoll-Forward1.max.FBX"),
                ("Hit", "RPG-Character@2Hand-Sword-GetHit-F1.FBX"),
                ("Death", "RPG-Character@2Hand-Sword-Knockdown1.FBX")
            };
            AnimatorState idle = null;
            foreach (var (stateName, fileName) in clips)
            {
                var state = stateMachine.AddState(stateName);
                state.motion = LoadClip(AnimationRoot + fileName);
                state.writeDefaultValues = true;
                if (stateName == "Idle")
                {
                    idle = state;
                }
            }
            stateMachine.defaultState = idle;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        private static AnimationClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null)
            {
                throw new InvalidOperationException($"No animation clip found at {path}");
            }
            return clip;
        }

        private struct ArenaMaterials
        {
            public Material Foundation { get; set; }
            public Material Floor { get; set; }
            public Material Stand { get; set; }
            public Material Marking { get; set; }
            public Material Azure { get; set; }
            public Material Crimson { get; set; }
            public Material Wood { get; set; }
            public Material Target { get; set; }
            public Material Emissive { get; set; }
        }

        private static ArenaMaterials CreateMaterials()
        {
            return new ArenaMaterials
            {
                Foundation = EnsureMaterial("Arena Foundation", new Color(0.035f, 0.045f, 0.07f), 0.55f),
                Floor = EnsureMaterial("Combat Floor", new Color(0.075f, 0.09f, 0.125f), 0.38f),
                Stand = EnsureMaterial("Grandstand", new Color(0.11f, 0.12f, 0.16f), 0.28f),
                Marking = EnsureMaterial("Arena Marking", new Color(0.18f, 0.48f, 0.85f), 0.65f, true),
                Azure = EnsureMaterial("Azure Team", new Color(0.035f, 0.28f, 0.95f), 0.55f, true),
                Crimson = EnsureMaterial("Crimson Team", new Color(0.9f, 0.04f, 0.035f), 0.55f, true),
                Wood = EnsureMaterial("Dummy Wood", new Color(0.34f, 0.16f, 0.055f), 0.22f),
                Target = EnsureMaterial("Dummy Target", new Color(0.75f, 0.48f, 0.09f), 0.4f),
                Emissive = EnsureMaterial("Arena Light", new Color(0.2f, 0.65f, 1f), 0.7f, true)
            };
        }

        private static Material EnsureMaterial(string name, Color color, float smoothness, bool emission = false)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.4f);
            }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static bool ValidateSourceAssets()
        {
            var missing = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath) == null) missing.Add(CharacterPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath) == null) missing.Add(SwordPath);
            if (LoadClipOrNull(AnimationRoot + "RPG-Character@2Hand-Sword-Idle.FBX") == null)
                missing.Add("2H sword animation clips");
            if (missing.Count == 0)
            {
                return true;
            }
            Debug.LogError("Cannot build Weapons Testing. Missing imported assets:\n" + string.Join("\n", missing));
            return false;
        }

        private static AnimationClip LoadClipOrNull(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(DataFolder);
            EnsureFolder(AbilityFolder);
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            var separator = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..separator], path[(separator + 1)..]);
        }

    }
}
