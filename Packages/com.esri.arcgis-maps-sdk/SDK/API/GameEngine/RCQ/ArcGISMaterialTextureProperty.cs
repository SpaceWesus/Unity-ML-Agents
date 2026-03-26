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
    /// Material parameter textures.
    /// </summary>
    public enum ArcGISMaterialTextureProperty
    {
        /// <summary>
        /// Imagery.
        /// </summary>
        Imagery = 0,
        
        /// <summary>
        /// Normal map.
        /// </summary>
        NormalMap = 1,
        
        /// <summary>
        /// Base map.
        /// </summary>
        BaseMap = 2,
        
        /// <summary>
        /// Uv region lut.
        /// </summary>
        UvRegionLut = 3,
        
        /// <summary>
        /// Positions map.
        /// </summary>
        PositionsMap = 4,
        
        /// <summary>
        /// Feature IDs.
        /// </summary>
        /// <remarks>
        /// Present on scene node meshes with feature data.
        /// The feature id for a given feature index (see <see cref="GameEngine.RCQ.ArcGISSetRenderableMeshCommandParameters.FeatureIndices">ArcGISSetRenderableMeshCommandParameters.FeatureIndices</see>) is stored at:
        /// x = feature_index % (tex_width / 2)
        /// y = floor(feature_index / (tex_width / 2))
        /// </remarks>
        FeatureIds = 5,
        
        /// <summary>
        /// MetallicRoughness.
        /// </summary>
        MetallicRoughness = 6,
        
        /// <summary>
        /// Emissive.
        /// </summary>
        Emissive = 7,
        
        /// <summary>
        /// Occlusion map.
        /// </summary>
        OcclusionMap = 8,
        
        /// <summary>
        /// Feature Offsets.
        /// </summary>
        /// <remarks>
        /// Present on scene node meshes relative to ground with feature data.
        /// The feature offset for a given feature index (see <see cref="GameEngine.RCQ.ArcGISSetRenderableMeshCommandParameters.FeatureIndices">ArcGISSetRenderableMeshCommandParameters.FeatureIndices</see>) is stored at:
        /// x = feature_index % tex_width
        /// y = floor(feature_index / tex_width)
        /// </remarks>
        FeatureOffsets = 9,
        
        /// <summary>
        /// Feature representation specfying hiddenness/selectedness and mesh symbology
        /// </summary>
        /// <remarks>
        /// Document in detail when used by game engines
        /// </remarks>
        FeatureRepresentation = 10
    };
}