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
    /// Renderable types.
    /// </summary>
    public enum ArcGISRenderableType
    {
        /// <summary>
        /// Scene node.
        /// Deprecated and will be replaced by <see cref="GameEngine.RCQ.ArcGISRenderableType.IntegratedMesh">ArcGISRenderableType.IntegratedMesh</see> or <see cref="GameEngine.RCQ.ArcGISRenderableType.ObjectMesh">ArcGISRenderableType.ObjectMesh</see>.
        /// </summary>
        SceneNode = 0,
        
        /// <summary>
        /// Tile.
        /// Deprecated and will be replaced by <see cref="GameEngine.RCQ.ArcGISRenderableType.Terrain">ArcGISRenderableType.Terrain</see>.
        /// </summary>
        Tile = 1,
        
        /// <summary>
        /// A terrain mesh renderable.
        /// </summary>
        Terrain = 2,
        
        /// <summary>
        /// A scene layer, mesh pyramid profile, integrated mesh renderable.
        /// </summary>
        IntegratedMesh = 3,
        
        /// <summary>
        /// A scene layer, mesh pyramid profile, object mesh renderable.
        /// </summary>
        ObjectMesh = 4,
        
        /// <summary>
        /// A point cloud renderable.
        /// </summary>
        PointCloud = 9
    };
}