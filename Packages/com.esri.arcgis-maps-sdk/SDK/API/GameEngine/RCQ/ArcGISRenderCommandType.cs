// COPYRIGHT 1995-2025 ESRI
// TRADE SECRETS: ESRI PROPRIETARY AND CONFIDENTIAL
// Unpublished material - all rights reserved under the
// Copyright Laws of the United States and applicable international
// laws, treaties, and conventions.
//
// For additional information, contact:
// Attn: Contracts and Legal Department
// Environmental Systems Research Institute, Inc.
// 380 New York Street
// Redlands, California 92373
// USA
//
// email: legal@esri.com
namespace Esri.GameEngine.RCQ
{
    /// <summary>
    /// Render command types.
    /// </summary>
    public enum ArcGISRenderCommandType
    {
        /// <summary>
        /// Create render target.
        /// </summary>
        CreateRenderTarget = 1,
        
        /// <summary>
        /// Create texture.
        /// </summary>
        CreateTexture = 2,
        
        /// <summary>
        /// Create renderable.
        /// </summary>
        CreateRenderable = 3,
        
        /// <summary>
        /// Destroy render target.
        /// </summary>
        DestroyRenderTarget = 5,
        
        /// <summary>
        /// Destroy texture.
        /// </summary>
        DestroyTexture = 6,
        
        /// <summary>
        /// Destroy renderable.
        /// </summary>
        DestroyRenderable = 7,
        
        /// <summary>
        /// Multiple compose.
        /// </summary>
        MultipleCompose = 8,
        
        /// <summary>
        /// Copy.
        /// </summary>
        Copy = 10,
        
        /// <summary>
        /// Generate normal texture.
        /// </summary>
        GenerateNormalTexture = 11,
        
        /// <summary>
        /// Set the pixel data of a texture.
        /// </summary>
        SetTexturePixelData = 12,
        
        /// <summary>
        /// Set the material scalar property of a renderable.
        /// </summary>
        SetRenderableMaterialScalarProperty = 13,
        
        /// <summary>
        /// Set the material vector property of a renderable.
        /// </summary>
        SetRenderableMaterialVectorProperty = 14,
        
        /// <summary>
        /// Set the material render target property of a renderable.
        /// </summary>
        SetRenderableMaterialRenderTargetProperty = 15,
        
        /// <summary>
        /// Set the material texture property of a renderable.
        /// </summary>
        SetRenderableMaterialTextureProperty = 16,
        
        /// <summary>
        /// Set the visibility of a renderable.
        /// </summary>
        SetRenderableVisible = 18,
        
        /// <summary>
        /// Set the mesh of a renderable.
        /// </summary>
        SetRenderableMesh = 20,
        
        /// <summary>
        /// Set the pivot position of a renderable.
        /// </summary>
        SetRenderablePivot = 22,
        
        /// <summary>
        /// Set the named material texture of a renderable.
        /// </summary>
        SetRenderableMaterialNamedTextureProperty = 23,
        
        /// <summary>
        /// Mark the start of a group of commands that should be executed atomically.
        /// </summary>
        CommandGroupBegin = 24,
        
        /// <summary>
        /// Mark the end of a group of commands that should be executed atomically.
        /// </summary>
        CommandGroupEnd = 25,
        
        /// <summary>
        /// Set the instance buffers of a renderable.
        /// </summary>
        SetRenderableInstanceBuffers = 26
    };
}