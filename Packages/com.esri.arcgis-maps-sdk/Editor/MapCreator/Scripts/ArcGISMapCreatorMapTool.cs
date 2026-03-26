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
using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Editor.Components;
using Esri.GameEngine.Elevation.Base;
using Esri.GameEngine.Map;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Esri.ArcGISMapsSDK.Editor.UI
{
	public class ArcGISMapCreatorMapTool : ArcGISMapCreatorTool
	{
		private class MapSerializedProperties : ScriptableObject
		{
#pragma warning disable CS0414
			[SerializeField]
			ArcGISSpatialReferenceInstanceData customMapSpatialReference = new();
			[SerializeField]
			ArcGISExtentInstanceData extent = new();
			[SerializeField]
			bool enableExtent = false;
			[SerializeField]
			ArcGISMapType mapType = ArcGISMapType.Local;
			[SerializeField]
			ArcGISPointInstanceData originPosition = new();
			[SerializeField]
			bool useCustomMapSpatialReference = false;
#pragma warning restore CS0414
		}

		private ArcGISMapComponent mapComponent;
		private SerializedObject serializedObject;

		private Toggle extentToggle;
		private Toggle manualSelection;

		private ArcGISMapTypeField mapTypeField;
		private Button createMapButton;
		private Button refreshMapButton;

		private DoubleField extentShapeXField;
		private DoubleField extentShapeYField;
		private EnumField shapeField;
		private Foldout mapExtentFoldout;
		private VisualElement extentFields;
		private VisualElement rootElement;
		private VisualElement spatialReferenceFields;

		public override VisualElement GetContent()
		{
			return rootElement;
		}

		public override void OnEnable()
		{
			rootElement = new VisualElement
			{
				name = "ArcGISMapCreatorMapTool"
			};

			var template = MapCreatorUtilities.Assets.LoadVisualTreeAsset("MapCreator/MapToolTemplate.uxml");
			template.CloneTree(rootElement);

			createMapButton = rootElement.Query<Button>(name: "button-create-map");
			createMapButton.RegisterCallback<MouseDownEvent>(CreateMap);
			refreshMapButton = rootElement.Query<Button>(name: "button-refresh-map");
			refreshMapButton.RegisterCallback<MouseDownEvent>(evnt => RefreshMap());
		}

		public override void OnDeselected()
		{
			rootElement?.Unbind();
		}

		public override void OnDestroy()
		{
			OnDeselected();
		}

		public override void OnSelected(IArcGISMapComponentInterface mapComponentInterface)
		{
			if (rootElement == null)
			{
				return;
			}

			rootElement.Unbind();

			mapComponent = mapComponentInterface as ArcGISMapComponent;

			InitMapTypeCategory(rootElement);
			InitSpatialReferenceCategory(rootElement);
			InitMapExtentCategory(rootElement);
			PopulateExtentFields(rootElement);
			InitCreateMapButton(rootElement);
			InitRefreshMapButton();

			if (mapComponent)
			{
				serializedObject = new SerializedObject(mapComponent);
			}
			else
			{
				serializedObject = new SerializedObject(ScriptableObject.CreateInstance<MapSerializedProperties>());
			}

			rootElement.Bind(serializedObject);

			EditorApplication.hierarchyChanged += () => InitRefreshMapButton();
			Selection.selectionChanged += () => InitRefreshMapButton();
		}

		public override Texture2D GetImage()
		{
			return MapCreatorUtilities.Assets.LoadImage($"MapCreator/Toolbar/{MapCreatorUtilities.Assets.GetThemeFolderName()}/MapToolIcon.png");
		}

		public override string GetLabel()
		{
			return "Map";
		}

		private void InitSpatialReferenceCategory(VisualElement rootElement)
		{
			spatialReferenceFields = rootElement.Query<VisualElement>(name: "map-coordinate-system-buttons");
			manualSelection = rootElement.Query<Toggle>(name: "toggle-enable-manual-selection");

			spatialReferenceFields.SetEnabled(false);

			manualSelection.RegisterValueChangedCallback(evnt =>
			{
				spatialReferenceFields.SetEnabled(evnt.newValue);
			});

			if (mapComponent)
			{
				spatialReferenceFields.SetEnabled(mapComponent.UseCustomMapSpatialReference);
				manualSelection.value = mapComponent.UseCustomMapSpatialReference;
			}
		}

		private void InitMapExtentCategory(VisualElement rootElement)
		{
			mapExtentFoldout = rootElement.Query<Foldout>(name: "category-map-extent");
			extentFields = rootElement.Query<VisualElement>(name: "map-extent-fields");

			var useOriginCenterToggle = (Toggle)rootElement.Query<Toggle>(name: "toggle-origin-center-extent");

			extentToggle = rootElement.Query<Toggle>(name: "toggle-enable-map-extent");
			extentToggle.RegisterValueChangedCallback(evnt =>
			{
				extentFields.SetEnabled(evnt.newValue);
				useOriginCenterToggle.SetEnabled(evnt.newValue);
			});

			var gcFields = (VisualElement)mapExtentFoldout.Query<VisualElement>(name: "geographic-center-fields");
			useOriginCenterToggle.RegisterValueChangedCallback(evnt =>
			{
				gcFields.style.display = evnt.newValue ? DisplayStyle.None : DisplayStyle.Flex;
			});

			shapeField = rootElement.Query<EnumField>(name: "map-shape-selector");
			shapeField.RegisterValueChangedCallback(evnt =>
			{
				if (evnt.newValue != null)
				{
					SwitchVisibleExtentShapeFields((MapExtentShapes)evnt.newValue);
				}
			});

			if (!mapComponent || mapComponent.MapType == ArcGISMapType.Global)
			{
				extentToggle.value = false;
				extentFields.SetEnabled(extentToggle.value);
				shapeField.value = MapExtentShapes.Square;
			}
		}

		private void InitMapTypeCategory(VisualElement rootElement)
		{
			mapTypeField = rootElement.Q<ArcGISMapTypeField>();

			mapTypeField.RegisterCallback<ChangeEvent<int>>(evnt =>
			{
				mapExtentFoldout.style.display = evnt.newValue == (int)ArcGISMapType.Global ? DisplayStyle.None : DisplayStyle.Flex;
			});
		}

		private void PopulateExtentFields(VisualElement rootElement)
		{
			extentShapeXField = MapCreatorUtilities.InitializeDoubleField(rootElement, "map-shape-dimensions-x");
			extentShapeYField = MapCreatorUtilities.InitializeDoubleField(rootElement, "map-shape-dimensions-y");
		}

		private void SwitchVisibleExtentShapeFields(MapExtentShapes shape)
		{
			switch (shape)
			{
				case MapExtentShapes.Square:
					extentShapeXField.label = "Length";
					extentShapeYField.style.display = DisplayStyle.None;
					break;
				case MapExtentShapes.Rectangle:
					extentShapeXField.label = "X";
					extentShapeYField.style.display = DisplayStyle.Flex;
					break;
				case MapExtentShapes.Circle:
					extentShapeXField.label = "Radius";
					extentShapeYField.style.display = DisplayStyle.None;
					break;
				default:
					break;
			}

			shapeField.value = shape;
		}

		private void InitCreateMapButton(VisualElement rootElement)
		{
			createMapButton = rootElement.Query<Button>(name: "button-create-map");
			createMapButton.clickable.activators.Clear();
			createMapButton.SetEnabled(mapComponent == null);
		}

		private void InitRefreshMapButton()
		{
			refreshMapButton.clickable.activators.Clear();
			refreshMapButton.SetEnabled(mapComponent != null);
		}

		private void CreateMap(MouseDownEvent evnt)
		{
			var gameObject = new GameObject("ArcGISMap");
			mapComponent = gameObject.AddComponent<ArcGISMapComponent>();
			GameObjectUtility.EnsureUniqueNameForSibling(gameObject);
			Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
			var button = (Button)evnt.currentTarget;
			button.SetEnabled(false);

			var newSerializedObject = new SerializedObject(mapComponent);

			ArcGISMapCreatorBasemapTool.GetDefaultBasemap().ApplyToSerializedProperty(newSerializedObject.FindProperty("basemap"));
			BasemapTypes.Basemap.ApplyToSerializedProperty(newSerializedObject.FindProperty("basemapType"));

			newSerializedObject.CopyFromSerializedProperty(serializedObject.FindProperty("customMapSpatialReference"));
			newSerializedObject.CopyFromSerializedProperty(serializedObject.FindProperty("extent"));
			newSerializedObject.CopyFromSerializedProperty(serializedObject.FindProperty("enableExtent"));
			newSerializedObject.CopyFromSerializedProperty(serializedObject.FindProperty("mapType"));
			newSerializedObject.CopyFromSerializedProperty(serializedObject.FindProperty("originPosition"));
			newSerializedObject.CopyFromSerializedProperty(serializedObject.FindProperty("useCustomMapSpatialReference"));

			var mapElevationInstanceData = new ArcGISMapElevationInstanceData
			{
				ElevationSources = new List<ArcGISElevationSourceInstanceData>(new ArcGISElevationSourceInstanceData[] { new() {
					AuthenticationType = ArcGISMapsSDK.Authentication.ArcGISAuthenticationType.APIKey,
					IsEnabled = true,
					Name = "Terrain 3D",
					Source = ArcGISMapCreatorElevationTool.GetDefaultElevation(),
					Type = ArcGISElevationSourceType.ArcGISImageElevationSource,
				}})
			};

			mapElevationInstanceData.ApplyToSerializedProperty(newSerializedObject.FindProperty("mapElevation"));

			newSerializedObject.ApplyModifiedProperties();

			Selection.activeGameObject = gameObject;

			OnSelected(mapComponent);
		}

		private void RefreshMap()
		{
			mapComponent.UpdateMap();
		}
	}
}
