using System;
using System.Collections.Generic;
using System.Linq;
using Turtle.Ecosystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turtle.Ecosystem.Editor
{
    public static class EcosystemSliceSceneBuilder
    {
        private const string EcosystemScenePath = "Assets/Scenes/Ecosystem Slice.unity";
        private const string HomiesScenePath = "Assets/Scenes/Homies.unity";
        private const string MaterialFolder = "Assets/Materials/Ecosystem";
        private const string DataFolder = "Assets/Data/Ecosystem";

        [MenuItem("Turtle/Ecosystem/Rebuild Requested Scenes")]
        public static void RebuildRequestedScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before rebuilding Ecosystem Slice scenes.");
                return;
            }

            EnsureFolders();
            var gear = EnsureGearAssets();
            PersistHomiesArena();
            BuildEcosystemScene(gear, true);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Ecosystem Slice scene and persistent Homies arena rebuilt.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/Data");
            EnsureFolder(DataFolder);
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

        private static EcosystemGearDefinition[] EnsureGearAssets()
        {
            return new[]
            {
                EnsureGear(
                    $"{DataFolder}/Vanguard Blade.asset",
                    "gear-vanguard",
                    "Vanguard Blade",
                    GearMoveSet.VanguardBlade,
                    13,
                    new Color(0.12f, 0.55f, 1f)),
                EnsureGear(
                    $"{DataFolder}/Titan Greatsword.asset",
                    "gear-titan",
                    "Titan Greatsword",
                    GearMoveSet.TitanGreatsword,
                    20,
                    new Color(1f, 0.25f, 0.08f)),
                EnsureGear(
                    $"{DataFolder}/Rift Daggers.asset",
                    "gear-rift",
                    "Rift Daggers",
                    GearMoveSet.RiftDaggers,
                    16,
                    new Color(0.68f, 0.12f, 1f))
            };
        }

        private static EcosystemGearDefinition EnsureGear(
            string path,
            string id,
            string displayName,
            GearMoveSet moveSet,
            int power,
            Color accent)
        {
            var gear = AssetDatabase.LoadAssetAtPath<EcosystemGearDefinition>(path);
            if (gear == null)
            {
                gear = ScriptableObject.CreateInstance<EcosystemGearDefinition>();
                gear.Configure(id, displayName, moveSet, power, accent);
                AssetDatabase.CreateAsset(gear, path);
            }
            return gear;
        }

        private static void PersistHomiesArena()
        {
            var scene = SceneManager.GetSceneByPath(HomiesScenePath);
            var openedTemporarily = !scene.IsValid() || !scene.isLoaded;
            if (openedTemporarily)
            {
                scene = EditorSceneManager.OpenScene(HomiesScenePath, OpenSceneMode.Additive);
            }

            if (FindRoot(scene, "Dungeon Dressing") != null)
            {
                if (openedTemporarily)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
                return;
            }

            var previousActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var darkStone = EnsureMaterial(
                $"{MaterialFolder}/Dungeon Stone.mat",
                new Color(0.025f, 0.03f, 0.055f),
                0.25f);
            var obsidian = EnsureMaterial(
                $"{MaterialFolder}/Obsidian.mat",
                new Color(0.035f, 0.025f, 0.08f),
                0.45f);
            var blueRune = EnsureMaterial(
                $"{MaterialFolder}/Blue Rune.mat",
                new Color(0.08f, 0.25f, 1f),
                0.7f,
                true);
            var violetRune = EnsureMaterial(
                $"{MaterialFolder}/Violet Rune.mat",
                new Color(0.55f, 0.05f, 0.95f),
                0.7f,
                true);

            var floor = FindTransform(scene, "Floor");
            if (floor != null)
            {
                floor.localScale = new Vector3(32f, 0.25f, 32f);
                AssignMaterial(floor.gameObject, darkStone);
            }

            var dressing = new GameObject("Dungeon Dressing");
            for (var index = 0; index < 12; index++)
            {
                var angle = index * Mathf.PI * 2f / 12f;
                var pillar = CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Obsidian Pillar {index + 1:00}",
                    dressing.transform,
                    new Vector3(Mathf.Cos(angle) * 15f, 2.5f, Mathf.Sin(angle) * 15f),
                    new Vector3(0.8f, 5f + index % 3, 0.8f),
                    obsidian);
                var rune = CreatePrimitive(
                    PrimitiveType.Sphere,
                    "Mana Rune",
                    pillar.transform,
                    Vector3.up * (2.4f + index % 3 * 0.35f),
                    Vector3.one * 0.55f,
                    index % 2 == 0 ? blueRune : violetRune,
                    true);
                rune.transform.position = pillar.transform.position +
                                          Vector3.up * (pillar.transform.localScale.y * 0.52f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }
            if (openedTemporarily)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void BuildEcosystemScene(
            EcosystemGearDefinition[] gear,
            bool rebuild)
        {
            if (rebuild)
            {
                var existing = SceneManager.GetSceneByPath(EcosystemScenePath);
                if (existing.IsValid() && existing.isLoaded)
                {
                    EditorSceneManager.CloseScene(existing, true);
                }
                AssetDatabase.DeleteAsset(EcosystemScenePath);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EcosystemScenePath) != null)
            {
                return;
            }

            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var groundMaterial = EnsureMaterial(
                $"{MaterialFolder}/Guild District Ground.mat",
                new Color(0.04f, 0.055f, 0.08f),
                0.3f);
            var azureMaterial = EnsureMaterial(
                $"{MaterialFolder}/Azure Guild.mat",
                new Color(0.04f, 0.32f, 0.8f),
                0.5f,
                true);
            var crimsonMaterial = EnsureMaterial(
                $"{MaterialFolder}/Crimson Guild.mat",
                new Color(0.68f, 0.035f, 0.06f),
                0.5f,
                true);
            var neutralMaterial = EnsureMaterial(
                $"{MaterialFolder}/Frontier Stone.mat",
                new Color(0.16f, 0.18f, 0.23f),
                0.35f);
            var gateMaterials = new[]
            {
                EnsureMaterial($"{MaterialFolder}/Gate Green.mat", new Color(0.08f, 0.7f, 0.35f), 0.6f, true),
                EnsureMaterial($"{MaterialFolder}/Gate Blue.mat", new Color(0.1f, 0.42f, 1f), 0.6f, true),
                EnsureMaterial($"{MaterialFolder}/Gate Violet.mat", new Color(0.62f, 0.1f, 1f), 0.6f, true)
            };

            var environment = new GameObject("Ecosystem Slice Environment");
            CreatePrimitive(
                PrimitiveType.Cube,
                "Guild District Floor",
                environment.transform,
                new Vector3(0f, -0.125f, 0f),
                new Vector3(44f, 0.25f, 44f),
                groundMaterial);
            CreateHub(environment.transform, neutralMaterial);
            CreateGuildHall(
                environment.transform,
                "guild-azure",
                "Azure Wake Guild Hall",
                new Vector3(-13f, 0f, 4f),
                azureMaterial);
            CreateGuildHall(
                environment.transform,
                "guild-crimson",
                "Crimson Compact Guild Hall",
                new Vector3(13f, 0f, 4f),
                crimsonMaterial);
            CreateMissionGate(
                environment.transform,
                "mission-goblin",
                "Ash-Tunnel Gate",
                new Vector3(-12f, 0f, 16f),
                gateMaterials[0]);
            CreateMissionGate(
                environment.transform,
                "mission-crypt",
                "Drowned Crypt",
                new Vector3(0f, 0f, 18f),
                gateMaterials[1]);
            CreateMissionGate(
                environment.transform,
                "mission-spire",
                "Voidglass Spire",
                new Vector3(12f, 0f, 16f),
                gateMaterials[2]);

            var player = CreatePrimitive(
                PrimitiveType.Capsule,
                "Player Hunter",
                null,
                new Vector3(0f, 1f, -4f),
                Vector3.one,
                azureMaterial);
            UnityEngine.Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            var characterController = player.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.5f;
            var weapon = CreatePrimitive(
                PrimitiveType.Cube,
                "Equipped Gear",
                player.transform,
                new Vector3(0.55f, 0.45f, 0.55f),
                new Vector3(0.2f, 1.15f, 0.14f),
                azureMaterial,
                true);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(8f, 8f, -12f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.018f, 0.035f);
            cameraObject.AddComponent<AudioListener>();
            var cameraRig = cameraObject.AddComponent<EcosystemCameraRig>();
            cameraRig.ConfigureEditor(player.transform);

            var playerController = player.AddComponent<EcosystemPlayerController>();
            playerController.ConfigureEditor(camera, weapon.transform);

            var systemObject = new GameObject("Living Hunter Ecosystem");
            var worldController = systemObject.AddComponent<EcosystemWorldController>();
            worldController.ConfigureEditor(gear, playerController);

            CreateTrainingYard(environment.transform, neutralMaterial);
            CreateLight();
            RenderSettings.ambientLight = new Color(0.11f, 0.13f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.025f, 0.035f, 0.065f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.skybox = null;

            EditorSceneManager.SaveScene(scene, EcosystemScenePath);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void CreateHub(Transform parent, Material material)
        {
            var anchor = new GameObject("Hub_Center");
            anchor.transform.SetParent(parent);
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "Hunter Plaza",
                anchor.transform,
                Vector3.zero,
                new Vector3(6f, 0.18f, 6f),
                material);
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Mathf.PI * 2f / 8f;
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Plaza Marker {index + 1}",
                    anchor.transform,
                    new Vector3(Mathf.Cos(angle) * 6.5f, 0.4f, Mathf.Sin(angle) * 6.5f),
                    new Vector3(0.35f, 0.8f, 0.35f),
                    material);
            }
        }

        private static void CreateGuildHall(
            Transform parent,
            string anchorName,
            string displayName,
            Vector3 position,
            Material material)
        {
            var anchor = new GameObject(anchorName);
            anchor.transform.SetParent(parent);
            anchor.transform.position = position;
            CreatePrimitive(
                PrimitiveType.Cube,
                displayName,
                anchor.transform,
                position + Vector3.up * 2f,
                new Vector3(8f, 4f, 6f),
                material);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Guild Banner",
                anchor.transform,
                position + new Vector3(0f, 4.5f, -3.1f),
                new Vector3(2f, 4f, 0.15f),
                material,
                true);
        }

        private static void CreateMissionGate(
            Transform parent,
            string anchorName,
            string displayName,
            Vector3 position,
            Material material)
        {
            var anchor = new GameObject(anchorName);
            anchor.transform.SetParent(parent);
            anchor.transform.position = position;
            CreatePrimitive(
                PrimitiveType.Cube,
                $"{displayName} Left",
                anchor.transform,
                position + new Vector3(-2f, 2.5f, 0f),
                new Vector3(0.8f, 5f, 0.8f),
                material);
            CreatePrimitive(
                PrimitiveType.Cube,
                $"{displayName} Right",
                anchor.transform,
                position + new Vector3(2f, 2.5f, 0f),
                new Vector3(0.8f, 5f, 0.8f),
                material);
            CreatePrimitive(
                PrimitiveType.Cube,
                $"{displayName} Crown",
                anchor.transform,
                position + new Vector3(0f, 4.7f, 0f),
                new Vector3(4.8f, 0.7f, 0.8f),
                material);
            CreatePrimitive(
                PrimitiveType.Quad,
                $"{displayName} Portal",
                anchor.transform,
                position + new Vector3(0f, 2.4f, 0.12f),
                new Vector3(3.2f, 4.2f, 1f),
                material,
                true);
        }

        private static void CreateTrainingYard(Transform parent, Material material)
        {
            var yard = new GameObject("Training Yard");
            yard.transform.SetParent(parent);
            yard.transform.position = new Vector3(0f, 0f, -12f);
            for (var index = 0; index < 3; index++)
            {
                var target = CreatePrimitive(
                    PrimitiveType.Capsule,
                    $"Training Target {index + 1}",
                    yard.transform,
                    yard.transform.position + new Vector3((index - 1) * 2.5f, 1f, 0f),
                    Vector3.one,
                    material);
                target.AddComponent<EcosystemTrainingTarget>();
            }
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("District Moonlight");
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(0.62f, 0.72f, 1f);
            light.shadows = LightShadows.Soft;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string objectName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool removeCollider = false)
        {
            var created = GameObject.CreatePrimitive(primitiveType);
            created.name = objectName;
            if (parent != null)
            {
                created.transform.SetParent(parent);
            }
            created.transform.position = position;
            created.transform.localScale = scale;
            AssignMaterial(created, material);
            if (removeCollider)
            {
                var attachedCollider = created.GetComponent<Collider>();
                if (attachedCollider != null)
                {
                    UnityEngine.Object.DestroyImmediate(attachedCollider);
                }
            }
            return created;
        }

        private static void AssignMaterial(GameObject target, Material material)
        {
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material EnsureMaterial(
            string path,
            Color color,
            float smoothness,
            bool emission = false)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard");
            material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.2f);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
        }

        private static Transform FindTransform(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == name)
                    {
                        return child;
                    }
                }
            }
            return null;
        }

        private static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != EcosystemScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(EcosystemScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
