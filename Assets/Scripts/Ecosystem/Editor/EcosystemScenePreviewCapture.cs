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
    /// Renders the serialized spatial scene without entering Play Mode or touching
    /// campaign save data. This is useful for automated visual-authoring checks.
    /// </summary>
    public static class EcosystemScenePreviewCapture
    {
        private const string RequestPath =
            "Temp/CodexValidation/capture-2d-ecosystem-preview.request";
        private const string ResultPath =
            "Temp/CodexValidation/capture-2d-ecosystem-preview.result";
        public const string CapturePath =
            "Temp/CodexValidation/2d-ecosystem-editmode-preview.png";

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedCapture()
        {
            EditorApplication.delayCall += RunRequestedCapture;
        }

        private static void RunRequestedCapture()
        {
            if (!File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            try
            {
                CapturePreview();
                File.WriteAllLines(ResultPath, new[] { "PASS", CapturePath });
                Debug.Log($"2D Ecosystem Edit Mode preview captured at {CapturePath}.");
            }
            catch (Exception exception)
            {
                File.WriteAllLines(ResultPath, new[] { "FAIL", exception.ToString() });
                Debug.LogException(exception);
            }
        }

        [MenuItem("Turtle/Ecosystem/Capture 2D Ecosystem Edit Mode Preview")]
        public static void CaptureFromMenu()
        {
            CapturePreview();
            Debug.Log($"2D Ecosystem Edit Mode preview captured at {CapturePath}.");
        }

        private static void CapturePreview()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                Ecosystem2DSceneBuilder.ScenePath);
            if (sceneAsset == null)
            {
                throw new InvalidOperationException("The 2D Ecosystem scene asset is missing.");
            }

            var scene = SceneManager.GetSceneByPath(Ecosystem2DSceneBuilder.ScenePath);
            var openedTemporarily = !scene.IsValid() || !scene.isLoaded;
            if (openedTemporarily)
            {
                scene = EditorSceneManager.OpenScene(
                    Ecosystem2DSceneBuilder.ScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                var roots = scene.GetRootGameObjects();
                var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault(item => item.CompareTag("MainCamera"));
                var authoring = roots
                    .SelectMany(root => root.GetComponentsInChildren<EcosystemSpatialAuthoring>(true))
                    .FirstOrDefault();
                if (camera == null || authoring == null)
                {
                    throw new InvalidOperationException(
                        "The scene needs its authored spatial map and orthographic Main Camera.");
                }

                const int width = 1600;
                const int height = 1000;
                var previousPosition = camera.transform.position;
                var previousRotation = camera.transform.rotation;
                var previousOrthographic = camera.orthographic;
                var previousSize = camera.orthographicSize;
                var previousTarget = camera.targetTexture;
                var previousActive = RenderTexture.active;
                var bounds = authoring.PlanarBounds;
                var aspect = width / (float)height;

                RenderTexture renderTexture = null;
                Texture2D capture = null;
                try
                {
                    camera.orthographic = true;
                    camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
                    camera.transform.rotation = Quaternion.identity;
                    camera.orthographicSize = Mathf.Max(
                        bounds.height * 0.5f + 2f,
                        bounds.width / (2f * aspect) + 2f);

                    renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                    {
                        name = "2D Ecosystem Edit Mode Preview"
                    };
                    renderTexture.Create();
                    camera.targetTexture = renderTexture;
                    RenderTexture.active = renderTexture;
                    camera.Render();

                    capture = new Texture2D(width, height, TextureFormat.RGB24, false);
                    capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                    capture.Apply(false, false);
                    var directory = Path.GetDirectoryName(CapturePath);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllBytes(CapturePath, capture.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    camera.orthographic = previousOrthographic;
                    camera.orthographicSize = previousSize;
                    camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                    RenderTexture.active = previousActive;
                    if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                    if (renderTexture != null)
                    {
                        renderTexture.Release();
                        UnityEngine.Object.DestroyImmediate(renderTexture);
                    }
                }
            }
            finally
            {
                if (openedTemporarily)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
