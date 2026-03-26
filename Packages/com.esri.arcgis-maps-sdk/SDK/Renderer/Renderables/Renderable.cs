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
using Esri.HPFramework;
using Unity.Mathematics;
using UnityEngine;

namespace Esri.ArcGISMapsSDK.Renderer.Renderables
{
	internal class Renderable : IRenderable
	{
		public GameObject RenderableGameObject { get; }

		private IGPUResourceMaterial material;
		public IGPUResourceMaterial Material
		{
			get => material;
			set
			{
				material = value;

				if (isInstanced)
				{
					var instancedMeshRenderer = RenderableGameObject.GetComponent<InstancedMeshRenderer>();

					instancedMeshRenderer.Material = material?.NativeMaterial;
				}
				else
				{
					RenderableGameObject.GetComponent<MeshRenderer>().material = material?.NativeMaterial;
				}
			}
		}

		private IGPUResourceMesh mesh;
		public IGPUResourceMesh Mesh
		{
			get => mesh;
			set
			{
				mesh = value;

				if (isInstanced)
				{
					var instancedMeshRenderer = RenderableGameObject.GetComponent<InstancedMeshRenderer>();

					instancedMeshRenderer.Mesh = mesh?.NativeMesh;
				}
				else
				{
					var meshFilter = RenderableGameObject.GetComponent<MeshFilter>();
					var collisionComponent = RenderableGameObject.GetComponent<MeshCollider>();

					meshFilter.sharedMesh = mesh?.NativeMesh;
					collisionComponent.sharedMesh = collisionComponent.enabled ? mesh?.NativeMesh : null;
				}
			}
		}

		private IGPUResourceInstanceBuffer instanceBuffer;
		public IGPUResourceInstanceBuffer InstanceBuffer
		{
			get => instanceBuffer;
			set
			{
				instanceBuffer = value;

				if (isInstanced)
				{
					var instancedMeshRenderer = RenderableGameObject.GetComponent<InstancedMeshRenderer>();

					instancedMeshRenderer.InstanceBuffer = instanceBuffer?.NativeBuffer;
				}
			}
		}

		double3 pivot;
		public double3 Pivot
		{
			get
			{
				return pivot;
			}
			set
			{
				pivot = value;

				var hpTransform = RenderableGameObject.GetComponent<HPTransform>();

				hpTransform.UniversePosition = value;
				hpTransform.UniverseRotation = Quaternion.identity;
				hpTransform.LocalScale = Vector3.one;
			}
		}

		public string Name
		{
			get
			{
				return RenderableGameObject.name;
			}

			set
			{
				RenderableGameObject.name = value;
			}
		}

		public bool IsVisible
		{
			get
			{
				return RenderableGameObject.activeInHierarchy;
			}

			set
			{
				RenderableGameObject.SetActive(value);
			}
		}

		bool isInstanced = false;
		public bool IsInstanced
		{
			get
			{
				return isInstanced;
			}

			set
			{
				isInstanced = value;
			}
		}

		public bool IsMeshColliderEnabled
		{
			get
			{
				return RenderableGameObject.GetComponent<MeshCollider>();
			}

			set
			{
				if (!isInstanced)
				{
					var component = RenderableGameObject.GetComponent<MeshCollider>();
					component.enabled = value;

					if (value)
					{
						component.sharedMesh = RenderableGameObject.GetComponent<MeshFilter>().sharedMesh;
					}
				}
			}
		}

		public uint LayerId { get; set; } = 0;

		private OrientedBoundingBox orientedBoundingBox;
		public OrientedBoundingBox OrientedBoundingBox
		{
			get
			{
				return orientedBoundingBox;
			}
			set
			{
				orientedBoundingBox = value;

				if (isInstanced)
				{
					var instancedRenderer = RenderableGameObject.GetComponent<InstancedMeshRenderer>();

					instancedRenderer.OrientedBoundingBox = orientedBoundingBox;
				}
			}
		}

		public bool MaskTerrain { get; set; }

		public Renderable(GameObject gameObject)
		{
			RenderableGameObject = gameObject;
		}

		public void Destroy()
		{
			if (Application.isEditor)
			{
				Object.DestroyImmediate(RenderableGameObject);
			}
			else
			{
				Object.Destroy(RenderableGameObject);
			}
		}
	}
}
