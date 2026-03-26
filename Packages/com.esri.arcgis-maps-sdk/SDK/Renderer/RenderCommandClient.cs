// COPYRIGHT 1995-2021 ESRI
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
using Esri.ArcGISMapsSDK.Renderer.GPUComputing;
using Esri.ArcGISMapsSDK.Renderer.GPUResources;
using Esri.ArcGISMapsSDK.Renderer.Renderables;
using Esri.ArcGISMapsSDK.SDK.Utils.Math;
using Esri.GameEngine.Extent;
using Esri.GameEngine.Math;
using Esri.GameEngine.RCQ;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Esri.ArcGISMapsSDK.Renderer
{
	internal class RenderCommandClient : IRenderCommandClient
	{
		private static readonly int baseTextureUVIndex = 0;
		private static readonly int featureIndicesUVIndex = 1;
		private static readonly int uvRegionIdsUVIndex = 2;
		private static readonly int flattenedAreaUVIndex = 3;

		private readonly IGPUResourcesProvider gpuResourceProvider;
		private readonly IRenderableProvider renderableProvider;
		private readonly INormalMapGenerator normalMapGenerator;
		private readonly IImageComposer imageComposer;

		private IGPUResourceTexture2D flatElevationTexture;

		// Cached global properties
		private Dictionary<ArcGISMaterialVectorProperty, ArcGISDoubleVector4> GlobalVectorProperties { get; } = new Dictionary<ArcGISMaterialVectorProperty, ArcGISDoubleVector4>();
		private Dictionary<ArcGISMaterialScalarProperty, float> GlobalScalarProperties { get; } = new Dictionary<ArcGISMaterialScalarProperty, float>();

		public event ArcGISExtentUpdatedEventHandler ExtentUpdated;

		public RenderCommandClient(IGPUResourcesProvider gpuResourceProvider, IRenderableProvider renderableProvider, IImageComposer imageComposer, INormalMapGenerator normalMapGenerator)
		{
			this.gpuResourceProvider = gpuResourceProvider;
			this.renderableProvider = renderableProvider;
			this.normalMapGenerator = normalMapGenerator;
			this.imageComposer = imageComposer;
			flatElevationTexture = CreateFlatElevationTexture();
		}

		private static IGPUResourceTexture2D CreateFlatElevationTexture()
		{
			// Create flat elevation texture
			var nativeTexture = new UnityEngine.Texture2D(1, 1, UnityEngine.TextureFormat.RFloat, false, true)
			{
				wrapMode = UnityEngine.TextureWrapMode.Clamp,
				filterMode = UnityEngine.FilterMode.Bilinear
			};
			var texture = new GPUResourceTexture2D(nativeTexture);
			texture.NativeTexture.SetPixelData(new float[] { 0.0f }, 0);
			return texture;
		}

		public ulong[] ExecuteRenderCommand(RenderCommand renderCommand)
		{
			var callbackTokens = new List<ulong>();

			switch (renderCommand.Type)
			{
				// Creation Commands
				case ArcGISRenderCommandType.CreateRenderTarget:
					{
						var parameters = (ArcGISCreateRenderTargetCommandParameters)renderCommand.CommandParameters;
						gpuResourceProvider.CreateRenderTexture(parameters.RenderTargetId, parameters.Width, parameters.Height, parameters.TextureFormat, parameters.HasMipMaps);
					}
					break;

				case ArcGISRenderCommandType.CreateRenderable:
					{
						var parameters = (ArcGISCreateRenderableCommandParameters)renderCommand.CommandParameters;

						var isInstanced = parameters.RenderableType == ArcGISRenderableType.PointCloud;

						var renderable = renderableProvider.CreateRenderable(parameters.RenderableId, parameters.LayerId, isInstanced);

						if (parameters.RenderableType == ArcGISRenderableType.PointCloud)
						{
							// Create a billboard mesh for point cloud
							renderable.Mesh = gpuResourceProvider.CreateBillboardMesh(parameters.RenderableId);
						}

						var material = Unity.Convert.FromArcGISMaterialReference(parameters.Material);
						gpuResourceProvider.CreateMaterial(parameters.RenderableId, parameters.RenderableType, parameters.MaterialType, material);
						renderable.Material = gpuResourceProvider.Materials[parameters.RenderableId];

						callbackTokens.Add(parameters.CallbackToken);
					}
					break;

				case ArcGISRenderCommandType.CreateTexture:
					{
						var parameters = (ArcGISCreateTextureCommandParameters)renderCommand.CommandParameters;
						gpuResourceProvider.CreateTexture(parameters.TextureId, parameters.Width, parameters.Height, parameters.TextureFormat, parameters.IsSRGB);
					}
					break;

				// Destruction Commands
				case ArcGISRenderCommandType.DestroyRenderTarget:
					{
						var parameters = (ArcGISDestroyRenderTargetCommandParameters)renderCommand.CommandParameters;

						gpuResourceProvider.DestroyRenderTexture(parameters.RenderTargetId);
					}
					break;

				case ArcGISRenderCommandType.DestroyRenderable:
					{
						var parameters = (ArcGISDestroyRenderableCommandParameters)renderCommand.CommandParameters;

						Debug.Assert(renderableProvider.Renderables.ContainsKey(parameters.RenderableId));

						gpuResourceProvider.DestroyMesh(parameters.RenderableId);
						gpuResourceProvider.DestroyMaterial(parameters.RenderableId);
						renderableProvider.DestroyRenderable(parameters.RenderableId);
					}
					break;

				case ArcGISRenderCommandType.DestroyTexture:
					{
						var parameters = (ArcGISDestroyTextureCommandParameters)renderCommand.CommandParameters;

						gpuResourceProvider.DestroyTexture(parameters.TextureId);
					}
					break;

				// Renderable operations
				case ArcGISRenderCommandType.SetRenderableVisible:
					{
						var parameters = (ArcGISSetRenderableVisibleCommandParameters)renderCommand.CommandParameters;

						Debug.Assert(renderableProvider.Renderables.ContainsKey(parameters.RenderableId));

						renderableProvider.Renderables[parameters.RenderableId].IsVisible = parameters.IsVisible;
					}
					break;

				case ArcGISRenderCommandType.SetRenderableMesh:
					{
						var parameters = (ArcGISSetRenderableMeshCommandParameters)renderCommand.CommandParameters;

						Debug.Assert(renderableProvider.Renderables.ContainsKey(parameters.RenderableId));

						var renderable = renderableProvider.Renderables[parameters.RenderableId];

						var mesh = renderable.Mesh;

						if (mesh == null)
						{
							mesh = renderable.Mesh = gpuResourceProvider.CreateMesh(parameters.RenderableId);
							mesh.MarkDynamic();
						}

						mesh.Clear();

						mesh.SetVertices(parameters.Positions.ToNativeArray<UnityEngine.Vector3>());
						mesh.SetNormals(parameters.Normals.ToNativeArray<UnityEngine.Vector3>());
						mesh.SetTangents(parameters.Tangents.ToNativeArray<UnityEngine.Vector4>());
						mesh.SetColors(parameters.Colors.ToNativeArray<UnityEngine.Color32>());
						mesh.SetUVs(baseTextureUVIndex, parameters.Uvs.ToNativeArray<UnityEngine.Vector2>());
						mesh.SetUVs(featureIndicesUVIndex, parameters.FeatureIndices.ToNativeArray<float>());
						mesh.SetUVs(uvRegionIdsUVIndex, parameters.UvRegionIds.ToNativeArray<float>());
						mesh.SetUVs(flattenedAreaUVIndex, parameters.FlattenedArea.ToNativeArray<float>());
						mesh.SetTriangles(parameters.Triangles.ToNativeArray<int>());
						mesh.RecalculateBounds();

						renderable.MaskTerrain = parameters.MaskTerrain;
						renderable.Mesh = mesh;

						var material = gpuResourceProvider.Materials[parameters.RenderableId];
						material.SetFloat("_HasFlattenedArea", parameters.FlattenedArea.Size != 0 ? 1.0f : 0.0f);

						renderable.OrientedBoundingBox = new OrientedBoundingBox(
							math.double3(parameters.OrientedBoundingBox.CenterX, parameters.OrientedBoundingBox.CenterY, parameters.OrientedBoundingBox.CenterZ),
							math.double3(parameters.OrientedBoundingBox.ExtentX, parameters.OrientedBoundingBox.ExtentY, parameters.OrientedBoundingBox.ExtentZ),
							math.double4(parameters.OrientedBoundingBox.OrientationX, parameters.OrientedBoundingBox.OrientationY, parameters.OrientedBoundingBox.OrientationZ, parameters.OrientedBoundingBox.OrientationW));
					}
					break;

				case ArcGISRenderCommandType.SetRenderablePivot:
					{
						var parameters = (ArcGISSetRenderablePivotCommandParameters)renderCommand.CommandParameters;

						Debug.Assert(renderableProvider.Renderables.ContainsKey(parameters.RenderableId));

						// Set renderable pivot
						var pivot = math.double3(parameters.X, parameters.Y, parameters.Z);
						var renderable = renderableProvider.Renderables[parameters.RenderableId];
						renderable.Pivot = pivot;

						// Now that mesh-relative positions can be calculated, set the clipping properties on renderable
						var material = gpuResourceProvider.Materials[parameters.RenderableId];
						if (GlobalVectorProperties.TryGetValue(ArcGISMaterialVectorProperty.MapAreaMin, out var mapAreaMin))
						{
							material.SetVector(getVectorMaterialShaderParameterName(ArcGISMaterialVectorProperty.MapAreaMin),
								renderable.RelativePosition(mapAreaMin));
						}
						if (GlobalVectorProperties.TryGetValue(ArcGISMaterialVectorProperty.MapAreaMax, out var mapAreaMax))
						{
							material.SetVector(getVectorMaterialShaderParameterName(ArcGISMaterialVectorProperty.MapAreaMax),
								renderable.RelativePosition(mapAreaMax));
						}
						if (GlobalScalarProperties.TryGetValue(ArcGISMaterialScalarProperty.ClippingMode, out var clippingMode))
						{
							material.SetFloat(getScalarMaterialShaderParameterName(ArcGISMaterialScalarProperty.ClippingMode),
								clippingMode);
						}
					}
					break;

				// Texture and RenderTexture Operations
				case ArcGISRenderCommandType.SetTexturePixelData:
					{
						var parameters = (ArcGISSetTexturePixelDataCommandParameters)renderCommand.CommandParameters;

						Debug.Assert(gpuResourceProvider.Textures.ContainsKey(parameters.TextureId));

						gpuResourceProvider.Textures[parameters.TextureId].SetPixelData(parameters.Pixels.Data, parameters.Pixels.Size);
					}
					break;

				// Material Operations
				case ArcGISRenderCommandType.SetRenderableMaterialRenderTargetProperty:
					{
						var parameters = (ArcGISSetRenderableMaterialRenderTargetPropertyCommandParameters)renderCommand.CommandParameters;

						Debug.Assert(gpuResourceProvider.Materials.ContainsKey(parameters.RenderableId) && gpuResourceProvider.RenderTextures.ContainsKey(parameters.Value));

						var shaderParam = getTextureMaterialShaderParameterName(parameters.MaterialTextureProperty);
						gpuResourceProvider.Materials[parameters.RenderableId].SetTexture(shaderParam, gpuResourceProvider.RenderTextures[parameters.Value]);
					}
					break;

				case ArcGISRenderCommandType.SetRenderableMaterialScalarProperty:
					{
						var parameters = (ArcGISSetRenderableMaterialScalarPropertyCommandParameters)renderCommand.CommandParameters;
						var shaderParam = getScalarMaterialShaderParameterName(parameters.MaterialScalarProperty);

						if (parameters.RenderableId == 0)
						{
							// Global property, of which there is currently one
							Debug.Assert(parameters.MaterialScalarProperty == ArcGISMaterialScalarProperty.ClippingMode);

							// Cache the global property value so it can be set on subsequently created renderables
							GlobalScalarProperties[parameters.MaterialScalarProperty] = parameters.Value;

							foreach (var kv in gpuResourceProvider.Materials)
							{
								gpuResourceProvider.Materials[kv.Key].SetFloat(shaderParam, parameters.Value);
							}

							if (parameters.MaterialScalarProperty == ArcGISMaterialScalarProperty.ClippingMode)
							{
								// ClippingMode is always advertised after MapAreaMin and MapAreaMax
								// so GlobalVectorProperties should have all the values updated by now
								var e = new ArcGISExtentUpdatedEventArgs();

								if (parameters.Value != 0)
								{
									Debug.Assert(parameters.Value == 1 || parameters.Value == 2);

									double3 areaMin;

									areaMin.x = GlobalVectorProperties[ArcGISMaterialVectorProperty.MapAreaMin].X;
									areaMin.y = GlobalVectorProperties[ArcGISMaterialVectorProperty.MapAreaMin].Y;
									areaMin.z = GlobalVectorProperties[ArcGISMaterialVectorProperty.MapAreaMin].Z;

									double3 areaMax;

									areaMax.x = GlobalVectorProperties[ArcGISMaterialVectorProperty.MapAreaMax].X;
									areaMax.y = GlobalVectorProperties[ArcGISMaterialVectorProperty.MapAreaMax].Y;
									areaMax.z = GlobalVectorProperties[ArcGISMaterialVectorProperty.MapAreaMax].Z;

									e.AreaMax = areaMax;
									e.AreaMin = areaMin;
									e.Type = parameters.Value == 1 ? ArcGISExtentType.ArcGISExtentCircle : ArcGISExtentType.ArcGISExtentRectangle;
								}

								ExtentUpdated?.Invoke(e);
							}
						}
						else
						{
							// Per-instance property
							Debug.Assert(
								parameters.MaterialScalarProperty == ArcGISMaterialScalarProperty.Opacity ||
								parameters.MaterialScalarProperty == ArcGISMaterialScalarProperty.UseUvRegionLut ||
								parameters.MaterialScalarProperty == ArcGISMaterialScalarProperty.Metallic ||
								parameters.MaterialScalarProperty == ArcGISMaterialScalarProperty.Roughness);

							gpuResourceProvider.Materials[parameters.RenderableId].SetFloat(shaderParam, parameters.Value);
						}
					}
					break;

				case ArcGISRenderCommandType.SetRenderableMaterialTextureProperty:
					{
						var parameters = (ArcGISSetRenderableMaterialTexturePropertyCommandParameters)renderCommand.CommandParameters;

						Debug.Assert(gpuResourceProvider.Materials.ContainsKey(parameters.RenderableId) && gpuResourceProvider.Textures.ContainsKey(parameters.Value));

						var shaderParam = getTextureMaterialShaderParameterName(parameters.MaterialTextureProperty);
						gpuResourceProvider.Materials[parameters.RenderableId].SetTexture(shaderParam, gpuResourceProvider.Textures[parameters.Value]);
					}
					break;

				case ArcGISRenderCommandType.SetRenderableMaterialNamedTextureProperty:
					{
						var parameters = (ArcGISSetRenderableMaterialNamedTexturePropertyCommandParameters)renderCommand.CommandParameters;

						Debug.Assert(gpuResourceProvider.Materials.ContainsKey(parameters.RenderableId) && gpuResourceProvider.Textures.ContainsKey(parameters.Value));

						var textureName = Marshal.PtrToStringAnsi(parameters.TextureName.Data);

						gpuResourceProvider.Materials[parameters.RenderableId].SetTexture(textureName, gpuResourceProvider.Textures[parameters.Value]);
					}
					break;

				case ArcGISRenderCommandType.SetRenderableMaterialVectorProperty:
					{
						var parameters = (ArcGISSetRenderableMaterialVectorPropertyCommandParameters)renderCommand.CommandParameters;
						var shaderParam = getVectorMaterialShaderParameterName(parameters.MaterialVectorProperty);

						if (parameters.RenderableId == 0)
						{
							// Global property, set on all renderables
							Debug.Assert(parameters.MaterialVectorProperty == ArcGISMaterialVectorProperty.MapAreaMax ||
								parameters.MaterialVectorProperty == ArcGISMaterialVectorProperty.MapAreaMin);

							// Cache global vector property values so they can be set on subsequently created renderables
							GlobalVectorProperties[parameters.MaterialVectorProperty] = parameters.Value;

							// Set the new vector property value on every renderable's material.
							// Both of the defined VectorProperties, mapAreaMin and mapAreaMax, are world positions that need to
							// be translated to relative to the renderable location, before being set
							// on the material.
							foreach (var kv in gpuResourceProvider.Materials)
							{
								var renderableId = kv.Key;
								var renderable = renderableProvider.Renderables[renderableId];
								gpuResourceProvider.Materials[renderableId].SetVector(shaderParam, renderable.RelativePosition(parameters.Value));
							}
						}
						else
						{
							// Per-instance property
							Debug.Assert(parameters.MaterialVectorProperty == ArcGISMaterialVectorProperty.NormalMapRegion);

							var normalMapRegion = parameters.Value;
							gpuResourceProvider.Materials[parameters.RenderableId].SetVector(shaderParam, normalMapRegion.AsVector4());
						}
					}
					break;

				case ArcGISRenderCommandType.MultipleCompose:
					{
						var parameters = (ArcGISMultipleComposeCommandParameters)renderCommand.CommandParameters;

						Debug.Assert(gpuResourceProvider.RenderTextures.ContainsKey(parameters.TargetId));

						var composables = parameters.ComposedTextures.ToArray<ArcGISComposedTextureElement>();
						var blenderInputArray = new ComposableImage[composables.Length];
						var pos = 0;

						foreach (var composable in composables)
						{
							Debug.Assert(gpuResourceProvider.Textures.ContainsKey(composable.TextureId));

							ComposableImage blenderInput;

							blenderInput.opacity = composable.Opacity;
							blenderInput.image = gpuResourceProvider.Textures[composable.TextureId];
							blenderInput.extent = new UnityEngine.Vector4(composable.Region.X, composable.Region.Y, composable.Region.Z, composable.Region.W);

							blenderInputArray[pos++] = blenderInput;
						}

						imageComposer.Compose(blenderInputArray, gpuResourceProvider.RenderTextures[parameters.TargetId]);
						gpuResourceProvider.RenderTextures[parameters.TargetId].GenerateMipMaps();
					}
					break;

				case ArcGISRenderCommandType.Copy:
					{
						// TODO: Implement when blending is added
					}
					break;

				case ArcGISRenderCommandType.GenerateNormalTexture:
					{
						var parameters = (ArcGISGenerateNormalTextureCommandParameters)renderCommand.CommandParameters;

						Debug.Assert((parameters.ElevationId == 0 || gpuResourceProvider.Textures.ContainsKey(parameters.ElevationId)) && gpuResourceProvider.RenderTextures.ContainsKey(parameters.TargetId));

						var elevationTexture = parameters.ElevationId == 0 ? flatElevationTexture : gpuResourceProvider.Textures[parameters.ElevationId];

						var outputTexture = gpuResourceProvider.RenderTextures[parameters.TargetId];
						if ((elevationTexture.Height != 1 && elevationTexture.Width != 1) && (elevationTexture.Height != outputTexture.Height + 1 || elevationTexture.Width != outputTexture.Height + 1))
						{
							UnityEngine.Debug.Log("Normal map generator expects input elevation texture to be either 1 texel in size, or 1 texel wider and taller than the output normal map.");
						}

						normalMapGenerator.Compute(elevationTexture, new UnityEngine.Vector4(parameters.TileExtent.X, parameters.TileExtent.Y, parameters.TileExtent.Z, parameters.TileExtent.W),
												new UnityEngine.Vector4(parameters.TextureExtent.X, parameters.TextureExtent.Y, parameters.TextureExtent.Z, parameters.TextureExtent.W), outputTexture);
					}
					break;

				case ArcGISRenderCommandType.CommandGroupBegin:
				case ArcGISRenderCommandType.CommandGroupEnd:
					break;

				case ArcGISRenderCommandType.SetRenderableInstanceBuffers:
					{
						var parameters = (ArcGISSetRenderableInstanceBuffersCommandParameters)renderCommand.CommandParameters;
						var renderable = renderableProvider.Renderables[parameters.RenderableId];

						// Get the instance data from command parameters.
						var instanceData = parameters.Instances.ToNativeArray<PointCloudInstanceData>();

						// Set instance buffer to renderable
						renderable.InstanceBuffer = gpuResourceProvider.CreateInstanceBuffer(instanceData);

						renderable.OrientedBoundingBox = new OrientedBoundingBox(
							math.double3(parameters.OrientedBoundingBox.CenterX, parameters.OrientedBoundingBox.CenterY, parameters.OrientedBoundingBox.CenterZ),
							math.double3(parameters.OrientedBoundingBox.ExtentX, parameters.OrientedBoundingBox.ExtentY, parameters.OrientedBoundingBox.ExtentZ),
							math.double4(parameters.OrientedBoundingBox.OrientationX, parameters.OrientedBoundingBox.OrientationY, parameters.OrientedBoundingBox.OrientationZ, parameters.OrientedBoundingBox.OrientationW));

					}
					break;

				default:

					Debug.Fail("Unknown RenderCommand Type!");

					break;
			}

			return callbackTokens.ToArray();
		}


		private static string getTextureMaterialShaderParameterName(ArcGISMaterialTextureProperty parameter)
		{
			switch (parameter)
			{
				case ArcGISMaterialTextureProperty.Imagery:
				case ArcGISMaterialTextureProperty.BaseMap:
					return "_MainTex";
				case ArcGISMaterialTextureProperty.NormalMap:
					return "_BumpMap";
				case ArcGISMaterialTextureProperty.UvRegionLut:
					return "_UVRegionLUT";
				case ArcGISMaterialTextureProperty.FeatureIds:
					return "_FeatureIds";
				case ArcGISMaterialTextureProperty.PositionsMap:
					return "_VertexOffset";
				case ArcGISMaterialTextureProperty.MetallicRoughness:
					return "_MetallicRoughness";
				case ArcGISMaterialTextureProperty.Emissive:
					return "_Emissive";
				case ArcGISMaterialTextureProperty.OcclusionMap:
					return "_OcclusionMap";
				case ArcGISMaterialTextureProperty.FeatureOffsets:
					return "_FeatureOffsets";
				default:
					Debug.Fail("Not implemented ArcGISMaterialTextureProperty!");
					return "";
			}
		}

		private static string getScalarMaterialShaderParameterName(ArcGISMaterialScalarProperty parameter)
		{
			switch (parameter)
			{
				case ArcGISMaterialScalarProperty.ClippingMode:
					return "_ClippingMode";
				case ArcGISMaterialScalarProperty.UseUvRegionLut:
					return "_UseUvRegionLut";
				case ArcGISMaterialScalarProperty.Metallic:
					return "_Metallic";
				case ArcGISMaterialScalarProperty.Roughness:
					return "_Roughness";
				case ArcGISMaterialScalarProperty.Opacity:
					return "_Opacity";
				default:
					Debug.Fail($"Not implemented ArcGISMaterialScalarProperty: {parameter}!");
					return "";
			}
		}

		private static string getVectorMaterialShaderParameterName(ArcGISMaterialVectorProperty parameter)
		{
			switch (parameter)
			{
				case ArcGISMaterialVectorProperty.MapAreaMax:
					return "_MapAreaMax";
				case ArcGISMaterialVectorProperty.MapAreaMin:
					return "_MapAreaMin";
				case ArcGISMaterialVectorProperty.NormalMapRegion:
					return "_NormalMapRegion";
				case ArcGISMaterialVectorProperty.ImageryRegion:
					return "_ImageryRegion";
				case ArcGISMaterialVectorProperty.PclSize:
					return "_PclSize";
				default:
					Debug.Fail("Not implemented ArcGISMaterialVectorProperty!");
					return "";
			}
		}
	}
}
