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
using Esri.HPFramework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Esri.ArcGISMapsSDK.Renderer.Renderables
{
	internal class RenderableProvider : IRenderableProvider
	{
		private readonly Dictionary<GameObject, IRenderable> gameObjectToRenderableMap = new();
		private readonly Dictionary<uint, IRenderable> activeRenderables = new();
		private readonly List<IRenderable> freeRenderables = new();

		private bool areMeshCollidersEnabled = false;

		private readonly GameObject unused = null;

		private readonly GameObject parent;

		public IReadOnlyDictionary<uint, IRenderable> Renderables => activeRenderables;

		public bool AreMeshCollidersEnabled
		{
			get
			{
				return areMeshCollidersEnabled;
			}

			set
			{
				if (areMeshCollidersEnabled != value)
				{
					areMeshCollidersEnabled = value;

					foreach (var activeRenderable in activeRenderables)
					{
						activeRenderable.Value.IsMeshColliderEnabled = value;
					}
				}
			}
		}

		public IEnumerable<IRenderable> TerrainMaskingMeshes => Renderables.Values.Where(sc => sc.IsVisible && sc.MaskTerrain);

		public RenderableProvider(int initSize, GameObject parent, bool areMeshCollidersEnabled)
		{
			this.parent = parent;
			this.areMeshCollidersEnabled = areMeshCollidersEnabled;

			unused = new GameObject("UnusedPoolGOs")
			{
				hideFlags = HideFlags.DontSaveInEditor
			};
			unused.transform.SetParent(parent.transform, false);

			for (var i = 0; i < initSize; i++)
			{
				var renderable = new Renderable(CreateGameObject(i));
				renderable.RenderableGameObject.transform.SetParent(unused.transform, false);
				freeRenderables.Add(renderable);
			}
		}

		public IRenderable CreateRenderable(uint id, uint layerId, bool isInstanced)
		{
			IRenderable renderable;

			if (freeRenderables.Count > 0)
			{
				renderable = freeRenderables[0];

				renderable.IsVisible = false;

				freeRenderables.RemoveAt(0);
			}
			else
			{
				renderable = new Renderable(CreateGameObject(activeRenderables.Count + freeRenderables.Count));
			}

			if (isInstanced)
			{
				PrepareInstancedRenderable(renderable);
			}
			else
			{
				PrepareNonInstancedRenderable(renderable);
			}

			renderable.RenderableGameObject.transform.SetParent(parent.transform, false);
			renderable.Name = "ArcGISGameObject_" + id;
			renderable.LayerId = layerId;

			activeRenderables.Add(id, renderable);
			gameObjectToRenderableMap.Add(renderable.RenderableGameObject, renderable);

			return renderable;
		}

		public void DestroyRenderable(uint id)
		{
			var activeRenderable = activeRenderables[id];

			activeRenderable.RenderableGameObject.transform.SetParent(unused.transform, false);
			activeRenderable.IsVisible = false;
			activeRenderable.InstanceBuffer = null;
			activeRenderable.Mesh = null;

			gameObjectToRenderableMap.Remove(activeRenderable.RenderableGameObject);
			activeRenderables.Remove(id);
			freeRenderables.Add(activeRenderable);
		}

		private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
		{
			if (!gameObject.TryGetComponent<T>(out var component))
			{
				component = gameObject.AddComponent<T>();
			}

			return component;
		}

		private static void RemoveComponentIfExists<T>(GameObject gameObject) where T : Component
		{
			if (gameObject.TryGetComponent<T>(out var component))
			{
				if (Application.isEditor)
				{
					Object.DestroyImmediate(component);
				}
				else
				{
					Object.Destroy(component);
				}
			}
		}

		private void PrepareInstancedRenderable(IRenderable renderable)
		{
			var gameObject = renderable.RenderableGameObject;

			RemoveComponentIfExists<MeshCollider>(gameObject);
			RemoveComponentIfExists<MeshFilter>(gameObject);
			RemoveComponentIfExists<MeshRenderer>(gameObject);

			GetOrAddComponent<InstancedMeshRenderer>(gameObject);

			renderable.IsInstanced = true;
			renderable.IsMeshColliderEnabled = false;
		}

		private void PrepareNonInstancedRenderable(IRenderable renderable)
		{
			var gameObject = renderable.RenderableGameObject;

			RemoveComponentIfExists<InstancedMeshRenderer>(gameObject);

			GetOrAddComponent<MeshCollider>(gameObject);
			GetOrAddComponent<MeshFilter>(gameObject);

			var renderer = GetOrAddComponent<MeshRenderer>(gameObject);

			renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
			renderer.enabled = true;

			renderable.IsInstanced = false;
			renderable.IsMeshColliderEnabled = areMeshCollidersEnabled;
		}

		public void Release()
		{
			foreach (var activeRenderable in activeRenderables)
			{
				activeRenderable.Value.Destroy();
			}

			foreach (var freeRenderable in freeRenderables)
			{
				freeRenderable.Destroy();
			}

			activeRenderables.Clear();
			freeRenderables.Clear();

			if (unused)
			{
				if (Application.isEditor)
				{
					Object.DestroyImmediate(unused);
				}
				else
				{
					Object.Destroy(unused);
				}
			}
		}

		private static GameObject CreateGameObject(int id)
		{
			var gameObject = new GameObject("ArcGISGameObject" + id)
			{
				hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable
			};

			gameObject.SetActive(false);
			gameObject.AddComponent<HPTransform>();

			return gameObject;
		}

		public IRenderable GetRenderableFrom(GameObject gameObject)
		{
			gameObjectToRenderableMap.TryGetValue(gameObject, out var renderable);

			return renderable;
		}
	}
}
