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
#if USE_HDRP_PACKAGE || USE_URP_PACKAGE
using Esri.ArcGISMapsSDK.Renderer.Renderables;
using Esri.ArcGISMapsSDK.Utils.Math;
using Esri.HPFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Esri.ArcGISMapsSDK.Renderer
{
	/// <summary>
	/// Renders a depth texture used to occlude terrain meshes
	/// so that integrated meshes are not obscured by them.
	/// </summary>
	public class ArcGISTerrainOcclusionRenderer
	{
		private Func<double4x4> GetWorldMatrixFn { get; set; }

		public bool Enabled { get; set; } = true;

		internal ArcGISTerrainOcclusionRenderer(IRenderableProvider renderableProvider, Func<double4x4> getWorldMatrixFn)
		{
			RenderableProvider = renderableProvider;
			GetWorldMatrixFn = getWorldMatrixFn;

			stencilWriteMaterial = Resources.Load<Material>("Materials/TerrainOcclusion/StencilWrite");
			obbDepthMaterial = Resources.Load<Material>("Materials/TerrainOcclusion/OBBDepth");

			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		}

		public void Release()
		{
			// Set depth texture to zero - (far plane).
			// TODO - verify on other platforms than Windows.
			Shader.SetGlobalTexture("_ArcGISGlobalTerrainOcclusionDepthMap", Texture2D.blackTexture);

			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
		}

		/// <summary>
		/// The terrain occlusion depth texture, sampled in the TileSurface shader to test for occlusion by integrated meshes
		/// </summary>
		private RenderTexture terrainOcclusionDepthTexture;
		public RenderTexture TerrainOcclusionDepthTexture
		{
			get
			{
				return terrainOcclusionDepthTexture;
			}
			private set
			{
				if (terrainOcclusionDepthTexture != null)
				{
					terrainOcclusionDepthTexture.Release();

#if UNITY_EDITOR
					if (Application.isEditor)
					{
						UnityEngine.Object.DestroyImmediate(terrainOcclusionDepthTexture);
					}
					else
#endif
					{
						UnityEngine.Object.Destroy(terrainOcclusionDepthTexture);
					}

				}

				terrainOcclusionDepthTexture = value;

				// TODO: passing the texture to shaders via a single global shader variable may be unacceptable, since it means
				// you can't have multiple views.
				if (terrainOcclusionDepthTexture != null)
				{
					Shader.SetGlobalTexture("_ArcGISGlobalTerrainOcclusionDepthMap", terrainOcclusionDepthTexture);
				}
				else
				{
					Shader.SetGlobalTexture("_ArcGISGlobalTerrainOcclusionDepthMap", Texture2D.blackTexture);
				}
			}
		}

		/// <summary>
		/// 1x1x1 cube mesh, used to render OBBs
		/// </summary>
		private Mesh unitCube;
		private Mesh UnitCube => unitCube ??= Resources.GetBuiltinResource<Mesh>("Cube.fbx");

		internal IRenderableProvider RenderableProvider { get; private set; }

		/// <summary>
		/// Material with shaderlab shader that writes a bit to the stencil buffer
		/// </summary>
		private Material stencilWriteMaterial;

		/// <summary>
		/// Material with shaderlab shader that writes depth to the output
		/// </summary>
		private Material obbDepthMaterial;

		/// <summary>
		/// A flat quad mesh positioned covering the view slightly in front of the near plane.
		///	Drawn to the depth texture when the camera is inside an IM OBB with the result being that
		///	IMs occlude any terrain.
		/// </summary>
		private Mesh nearPlaneBillboardMesh;
		private Mesh NearPlaneBillboardMesh => nearPlaneBillboardMesh ??= Resources.GetBuiltinResource<Mesh>("Quad.fbx");

		internal void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			UpdateTerrainOcclusionDepthTexture(camera);
		}

		IEnumerable<IRenderable> TerrainMaskingMeshes => RenderableProvider?.TerrainMaskingMeshes ?? Enumerable.Empty<IRenderable>();

		/// <summary>
		/// Draw all Terrain Masking Meshes (only i3s integrated meshes)
		/// </summary>
		private void RenderTerrainMaskingMeshes(CommandBuffer commands, Material material)
		{
			// Render terrain-masking meshes
			foreach (var terrainMaskingMesh in TerrainMaskingMeshes)
			{
				commands.DrawMesh(terrainMaskingMesh.Mesh.NativeMesh,
					terrainMaskingMesh.RenderableGameObject.transform.localToWorldMatrix,
					material);
			}
		}

		/// <summary>
		/// Render the terrain occlusion depth map
		/// </summary>
		private void UpdateTerrainOcclusionDepthTexture(Camera camera)
		{
			if (!Enabled)
			{
				TerrainOcclusionDepthTexture = null;
			}
			else
			{
				var worldToCameraMatrix = camera.worldToCameraMatrix;

				var renderType = GraphicsSettings.defaultRenderPipeline.GetType().ToString();

				// WorldToCameraMatrix SceneView Camera Matrix position is (0 0 0) in HDRP shaders.
				if (camera.cameraType == CameraType.SceneView && renderType == "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset")
				{
					worldToCameraMatrix.SetColumn(3, new Vector4(0, 0, 0, 1));
				}

				Shader.SetGlobalMatrix("_ArcGISGlobalTerrainOcclusionViewProjMatrix", GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * worldToCameraMatrix);

				CreateTerrainOcclusionDepthTexture(camera);

				using (var commands = new CommandBuffer { name = "RenderDepthOcclusionTexture" })
				{
					commands.SetRenderTarget(new RenderTargetIdentifier(terrainOcclusionDepthTexture));
					commands.ClearRenderTarget(true, true, Color.black);
					commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

					// Render terrain-masking meshes
					// This writes the 'silhouette' of the integrated meshes to the stencil buffer
					RenderTerrainMaskingMeshes(commands, stencilWriteMaterial);

					// Render terrain-masking mesh OBBs
					// This writes depth values of integrated meshes' OBBs, clipped to previously drawn region in stencil buffer.
					// This gives us some space in front of the terrain-occluding meshes where terrain will get hidden
					RenderTerrainMaskingMeshOBBs(camera, commands, obbDepthMaterial);

					Graphics.ExecuteCommandBuffer(commands);
				}
			}
		}

		private void RenderTerrainMaskingMeshOBBs(Camera camera, CommandBuffer commands, Material material)
		{
			var cameraPosition = camera.transform.position.ToDouble3();

			// Get transform from Universe (OBB coords) to World space
			var worldMatrix = GetWorldMatrixFn();
			var rebaseRotation = worldMatrix.GetRotation();

			material.SetPass(0);

			// Calculate the world transform for an OBB as a unit cube
			double4x4 generateOBBTransform(OrientedBoundingBox obb)
			{
				// Get OBB centre in world coordinates
				var obbCentre = obb.Center;
				var obbTranslation = math.transform(worldMatrix, obbCentre);

				// Get OBB rotation
				var obbRotation = obb.Orientation.AsQuaternion();
				obbRotation = math.mul(rebaseRotation, obbRotation);

				// Get OBB scaling
				var obbScaling = obb.Extent * 2 * 1.1;

				return HPMath.TRS(obbTranslation, obbRotation, obbScaling.ToFloat3());
			}

			// Calculate if the camera is inside an OBB
			bool cameraIsInObb(double4x4 obbWorldTransform)
			{
				var worldToObject = math.inverse(obbWorldTransform);
				var cameraInObjectSpace = worldToObject.HomogeneousTransformPoint(cameraPosition);
				var cameraIsInsideOBB = math.abs(cameraInObjectSpace.x) < 0.5 && math.abs(cameraInObjectSpace.y) < 0.5 && math.abs(cameraInObjectSpace.z) < 0.5;
				return cameraIsInsideOBB;
			}

			// Get all the OBB worldTransforms
			var obbWorldTransforms = TerrainMaskingMeshes.Select(sc => generateOBBTransform(sc.OrientedBoundingBox)).ToArray();

			// If camera is inside an OBB, terrain should be occluded from the entire volume in front of integrated meshes
			if (obbWorldTransforms.Any(cameraIsInObb))
			{
				commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
				commands.DrawMesh(NearPlaneBillboardMesh, Matrix4x4.Scale(new Vector3(2.0f, 2.0f, 2.0f)), material);

				return;
			}

			// Draw the OBBs
			foreach (var transform in obbWorldTransforms)
			{
				commands.DrawMesh(UnitCube, transform.ToMatrix4x4(), material);
			}
		}

		private (int, int) ViewSize(Camera camera)
		{
			return camera == null ? (1, 1) : (Math.Max(1, Display.displays[camera.targetDisplay].renderingWidth), Math.Max(1, Display.displays[camera.targetDisplay].renderingHeight));
		}

		private (int, int) DepthTextureSize()
		{
			return terrainOcclusionDepthTexture == null ? (0, 0) : (terrainOcclusionDepthTexture.width, terrainOcclusionDepthTexture.height);
		}

		/// <summary>
		/// Create the render texture with the same dimensions as the main camera's display
		/// </summary>
		private void CreateTerrainOcclusionDepthTexture(Camera camera)
		{
			var (w, h) = ViewSize(camera);
			var (tw, th) = DepthTextureSize();

			// (Re)-create texture?
			if (terrainOcclusionDepthTexture == null || tw != w || th != h)
			{
				var desc = new RenderTextureDescriptor
				{
					width = w,
					height = h,

					graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,

					depthBufferBits = 32,
					depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat_S8_UInt,
					msaaSamples = 1,
					dimension = TextureDimension.Tex2D,
					sRGB = false,
					volumeDepth = 1,
				};

				TerrainOcclusionDepthTexture = new RenderTexture(desc);
			}
		}
	}
}
#endif
