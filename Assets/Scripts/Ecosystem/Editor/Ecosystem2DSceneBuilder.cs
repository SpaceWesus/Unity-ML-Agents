using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turtle.Ecosystem.Editor
{
    /// <summary>
    /// Non-destructively wires the authored 2D Ecosystem scene to the persistent world.
    /// Runtime hunter tokens remain projections of save data and are intentionally not
    /// serialized as eighty duplicate scene objects.
    /// </summary>
    public static class Ecosystem2DSceneBuilder
    {
        // Scene generation is editor-explicit; Play Mode only projects serialized authoring.
        public const string ScenePath = "Assets/Scenes/2D Ecosystem.unity";
        private const string SystemObjectName = "Living 2D Hunter Ecosystem";
        public const string AutomationRequestPath =
            "Temp/CodexValidation/build-2d-ecosystem.request";

        private static readonly string[] GearPaths =
        {
            "Assets/Data/Ecosystem/Vanguard Blade.asset",
            "Assets/Data/Ecosystem/Titan Greatsword.asset",
            "Assets/Data/Ecosystem/Rift Daggers.asset"
        };

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedBuild()
        {
            EditorApplication.delayCall += RunRequestedBuild;
        }

        private static void RunRequestedBuild()
        {
            if (!File.Exists(AutomationRequestPath))
            {
                return;
            }
            var rebuildOwnedWorld = string.Equals(
                File.ReadAllText(AutomationRequestPath).Trim(),
                "rebuild",
                StringComparison.OrdinalIgnoreCase);
            File.Delete(AutomationRequestPath);
            BuildSceneInternal(rebuildOwnedWorld);
        }

        [MenuItem("Turtle/Ecosystem/Build 2D Ecosystem Scene")]
        public static void BuildScene()
        {
            BuildSceneInternal(false);
        }

        [MenuItem("Turtle/Ecosystem/Rebuild Authored 2D Ecosystem World")]
        public static void RebuildAuthoredWorld()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Authored 2D World",
                    "This replaces only the hierarchy owned by the ecosystem world builder. " +
                    "The replacement is serialized in the scene and will remain visible " +
                    "outside Play Mode.",
                    "Rebuild Owned World",
                    "Cancel"))
            {
                return;
            }
            BuildSceneInternal(true);
        }

        private static void BuildSceneInternal(bool rebuildAuthoredWorld)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before wiring the 2D Ecosystem scene.");
                return;
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                Debug.LogError($"Create and save the scene at {ScenePath} before running this builder.");
                return;
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            SceneManager.SetActiveScene(scene);

            var gear = GearPaths
                .Select(AssetDatabase.LoadAssetAtPath<EcosystemGearDefinition>)
                .ToArray();
            if (gear.Any(item => item == null))
            {
                Debug.LogError("The 2D Ecosystem scene requires all three ecosystem gear definitions.");
                return;
            }

            var systemObject = scene.GetRootGameObjects()
                .FirstOrDefault(item => item.name == SystemObjectName);
            if (systemObject == null)
            {
                systemObject = new GameObject(SystemObjectName);
                Undo.RegisterCreatedObjectUndo(systemObject, "Create 2D Ecosystem world host");
                SceneManager.MoveGameObjectToScene(systemObject, scene);
            }

            var controller = systemObject.GetComponent<EcosystemWorldController>() ??
                             Undo.AddComponent<EcosystemWorldController>(systemObject);
            var legacyStrategyView = systemObject.GetComponent<EcosystemStrategyView>();
            if (legacyStrategyView != null)
            {
                legacyStrategyView.enabled = false;
                EditorUtility.SetDirty(legacyStrategyView);
            }

            var camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault();
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create 2D Ecosystem camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.gameObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 12f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.025f, 0.04f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(camera);

            var spatial = EcosystemSpatialSceneAuthoringBuilder.Ensure(
                scene,
                gear,
                rebuildAuthoredWorld);
            if (spatial.Authoring == null || spatial.AuthoredWorldRoot == null ||
                spatial.DynamicActorRoot == null || spatial.DungeonStageRoot == null)
            {
                Debug.LogError("The authored 2D world hierarchy could not be created or collected.");
                return;
            }

            var spatialView = systemObject.GetComponent<EcosystemSpatialWorldView>() ??
                              Undo.AddComponent<EcosystemSpatialWorldView>(systemObject);
            var spatialHud = systemObject.GetComponent<EcosystemSpatialHud>() ??
                             Undo.AddComponent<EcosystemSpatialHud>(systemObject);
            var playerInput = systemObject.GetComponent<EcosystemPlayerInput2D>() ??
                              Undo.AddComponent<EcosystemPlayerInput2D>(systemObject);
            var mapCamera = camera.GetComponent<EcosystemMapCameraController>() ??
                            Undo.AddComponent<EcosystemMapCameraController>(camera.gameObject);
            var dungeonView = systemObject.GetComponent<EcosystemDungeonWorldView>() ??
                              Undo.AddComponent<EcosystemDungeonWorldView>(systemObject);

            mapCamera.ConfigureEditor(camera, spatial.Authoring, spatialHud, true);
            spatialHud.ConfigureEditor(controller, spatialView);
            playerInput.ConfigureEditor(camera, spatial.Authoring, spatialHud);
            spatialView.ConfigureEditor(
                controller,
                spatial.Authoring,
                mapCamera,
                spatialHud,
                playerInput,
                camera,
                spatial.DynamicActorRoot,
                null,
                spatial.PawnSlots);
            dungeonView.ConfigureEditor(
                spatial.AuthoredWorldRoot,
                spatial.DungeonStageRoot,
                spatial.Authoring,
                spatialView,
                mapCamera,
                camera,
                spatial.CircleSprite,
                spatial.SquareSprite);
            controller.ConfigureEditor(
                gear,
                null,
                spatialView,
                spatialHud,
                playerInput,
                mapCamera,
                dungeonView);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(spatialView);
            EditorUtility.SetDirty(spatialHud);
            EditorUtility.SetDirty(playerInput);
            EditorUtility.SetDirty(mapCamera);
            EditorUtility.SetDirty(dungeonView);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(item => item.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            Selection.activeGameObject = systemObject;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "2D Ecosystem scene wired: the persistent v5 world host, authored terrain, roads, " +
                "towns, gates, pooled hunter pawns, spatial HUD, and orthographic camera are " +
                "serialized and visible outside Play Mode.");
        }
    }
}
