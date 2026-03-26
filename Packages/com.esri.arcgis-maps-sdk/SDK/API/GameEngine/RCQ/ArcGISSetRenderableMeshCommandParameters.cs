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
using System.Runtime.InteropServices;

namespace Esri.GameEngine.RCQ
{
    /// <summary>
    /// A set mesh render command.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ArcGISSetRenderableMeshCommandParameters
    {
        /// <summary>
        /// The renderable parameter of this render command.
        /// </summary>
        public uint RenderableId;
        
        /// <summary>
        /// The triangles parameter of this render command.
        /// </summary>
        public ArcGISDataBufferView Triangles;
        
        /// <summary>
        /// The positions parameter of this render command.
        /// </summary>
        public ArcGISDataBufferView Positions;
        
        /// <summary>
        /// The normals parameter of this render command.
        /// </summary>
        public ArcGISDataBufferView Normals;
        
        /// <summary>
        /// The tangents parameter of this render command.
        /// </summary>
        public ArcGISDataBufferView Tangents;
        
        /// <summary>
        /// The uvs parameter of this render command.
        /// </summary>
        public ArcGISDataBufferView Uvs;
        
        /// <summary>
        /// The colors parameter of this render command.
        /// </summary>
        public ArcGISDataBufferView Colors;
        
        /// <summary>
        /// The ID of the uv region parameter of this render command.
        /// </summary>
        public ArcGISDataBufferView UvRegionIds;
        
        /// <summary>
        /// The mesh's feature indices.
        /// </summary>
        /// <remarks>
        /// A zero-based id that is unique for each feature contained in the scene node. 
        /// Used to look up feature ID in the <see cref="GameEngine.RCQ.ArcGISMaterialTextureProperty.FeatureIds">ArcGISMaterialTextureProperty.FeatureIds</see> texture.
        /// </remarks>
        public ArcGISDataBufferView FeatureIndices;
        
        /// <summary>
        /// The mesh's flattened area flag buffer
        /// </summary>
        /// <remarks>
        /// Applicable only to terrain meshes with replaced/flattened area, this vertex 
        /// attribute specifies which vertices are part of the mesh's flattened area.
        /// </remarks>
        public ArcGISDataBufferView FlattenedArea;
        
        /// <summary>
        /// The oriented bounding box of the mesh.
        /// </summary>
        public GameEngine.Math.ArcGISOrientedBoundingBox OrientedBoundingBox;
        
        /// <summary>
        /// The boolean indicating whether this mesh should be used to mask terrain.
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool MaskTerrain;
    }
}