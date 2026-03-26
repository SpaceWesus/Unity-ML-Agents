// COPYRIGHT 1995-2022 ESRI
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
using Esri.ArcGISMapsSDK.Renderer.GPUResources;
using Esri.GameEngine.Math;
using Unity.Mathematics;
using UnityEngine;

namespace Esri.ArcGISMapsSDK.Renderer.Renderables
{
	internal interface IRenderable
	{
		public GameObject RenderableGameObject { get; }

		public OrientedBoundingBox OrientedBoundingBox { get; set; }
		public bool MaskTerrain { get; set; }

		public IGPUResourceMaterial Material { get; set; }

		public IGPUResourceMesh Mesh { get; set; }

		public IGPUResourceInstanceBuffer InstanceBuffer { get; set; }

		public double3 Pivot { get; set; }

		public string Name { get; set; }

		public bool IsVisible { get; set; }

		public bool IsInstanced { get; set; }

		public bool IsMeshColliderEnabled { get; set; }

		public uint LayerId { get; set; }

		public void Destroy();

		/// <summary>
		/// Returns position, relative to the renderable's Location
		/// </summary>
		/// <param name="position">A global position</param>
		/// <returns>position relative to the renderable's Location</returns>
		public Vector4 RelativePosition(ArcGISDoubleVector4 position)
		{
			return new Vector4((float)(position.X - Pivot.x), (float)(position.Y - Pivot.y), (float)(position.Z - Pivot.z), 0);
		}
	}
}
