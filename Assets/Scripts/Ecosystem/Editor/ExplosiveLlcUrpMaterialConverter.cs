using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Turtle.Ecosystem.Editor
{
    public static class ExplosiveLlcUrpMaterialConverter
    {
        private const string AssetRoot = "Assets/ExplosiveLLC";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        [MenuItem("Turtle/Rendering/Convert ExplosiveLLC Materials to URP")]
        public static void ConvertMaterials()
        {
            var urpLit = Shader.Find(UrpLitShaderName);
            if (urpLit == null)
            {
                Debug.LogError(
                    $"Cannot convert ExplosiveLLC materials because {UrpLitShaderName} was not found.");
                return;
            }

            var convertedPaths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { AssetRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == urpLit)
                {
                    continue;
                }

                ConvertMaterial(material, urpLit);
                convertedPaths.Add(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Converted {convertedPaths.Count} ExplosiveLLC materials to URP Lit.\n" +
                string.Join("\n", convertedPaths));
        }

        [MenuItem("Turtle/Rendering/Convert ExplosiveLLC Materials to URP", true)]
        private static bool ValidateConvertMaterials()
        {
            return AssetDatabase.IsValidFolder(AssetRoot);
        }

        private static void ConvertMaterial(Material material, Shader urpLit)
        {
            var mainTexture = GetTexture(material, "_MainTex");
            var mainTextureScale = GetTextureScale(material, "_MainTex");
            var mainTextureOffset = GetTextureOffset(material, "_MainTex");
            var color = GetColor(material, "_Color", Color.white);
            var metallic = GetFloat(material, "_Metallic", 0f);
            var smoothness = GetFloat(material, "_Glossiness", 0.5f);
            var normalMap = GetTexture(material, "_BumpMap");
            var normalScale = GetFloat(material, "_BumpScale", 1f);
            var emissionMap = GetTexture(material, "_EmissionMap");
            var emissionColor = GetColor(material, "_EmissionColor", Color.black);
            var cutoff = GetFloat(material, "_Cutoff", 0.5f);
            var standardMode = Mathf.RoundToInt(GetFloat(material, "_Mode", 0f));

            Undo.RecordObject(material, "Convert ExplosiveLLC Material to URP");
            material.shader = urpLit;

            material.SetTexture("_BaseMap", mainTexture);
            material.SetTextureScale("_BaseMap", mainTextureScale);
            material.SetTextureOffset("_BaseMap", mainTextureOffset);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", normalScale);
            material.SetTexture("_EmissionMap", emissionMap);
            material.SetColor("_EmissionColor", emissionColor);
            material.SetFloat("_Cutoff", cutoff);

            SetKeyword(material, "_NORMALMAP", normalMap != null);
            SetKeyword(
                material,
                "_EMISSION",
                emissionMap != null || emissionColor.maxColorComponent > 0.001f);

            switch (standardMode)
            {
                case 1:
                    ConfigureCutout(material);
                    break;
                case 2:
                case 3:
                    ConfigureTransparent(material);
                    break;
                default:
                    ConfigureOpaque(material);
                    break;
            }

            EditorUtility.SetDirty(material);
        }

        private static void ConfigureOpaque(Material material)
        {
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", false);
            SetKeyword(material, "_ALPHATEST_ON", false);
            material.renderQueue = -1;
        }

        private static void ConfigureCutout(Material material)
        {
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", false);
            SetKeyword(material, "_ALPHATEST_ON", true);
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }

        private static void ConfigureTransparent(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", true);
            SetKeyword(material, "_ALPHATEST_ON", false);
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Texture GetTexture(Material material, string property)
        {
            return material.HasProperty(property)
                ? material.GetTexture(property)
                : null;
        }

        private static Vector2 GetTextureScale(Material material, string property)
        {
            return material.HasProperty(property)
                ? material.GetTextureScale(property)
                : Vector2.one;
        }

        private static Vector2 GetTextureOffset(Material material, string property)
        {
            return material.HasProperty(property)
                ? material.GetTextureOffset(property)
                : Vector2.zero;
        }

        private static Color GetColor(Material material, string property, Color fallback)
        {
            return material.HasProperty(property)
                ? material.GetColor(property)
                : fallback;
        }

        private static float GetFloat(Material material, string property, float fallback)
        {
            return material.HasProperty(property)
                ? material.GetFloat(property)
                : fallback;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
