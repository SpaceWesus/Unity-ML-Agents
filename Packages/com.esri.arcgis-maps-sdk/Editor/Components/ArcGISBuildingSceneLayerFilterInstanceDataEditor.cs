// COPYRIGHT 1995-2023 ESRI
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
using Esri.ArcGISMapsSDK.Editor.UI;
using Esri.ArcGISMapsSDK.Editor.Utils;
using Esri.GameEngine;
using Esri.GameEngine.Layers;
using Esri.GameEngine.Layers.BuildingScene;
using Esri.Unity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Esri.ArcGISMapsSDK.Editor.Components
{
	[CustomPropertyDrawer(typeof(ArcGISBuildingSceneLayerFilterInstanceData))]
	public class ArcGISBuildingSceneLayerFilterInstanceDataEditor : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			position.height = EditorGUIUtility.singleLineHeight;

			DrawProperty(position, property, "Building Filter", "IsBuildingAttributeFilterEnabled", OpenFiltersWindow);

			position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			DrawProperty(position, property, "Disciplines & Categories", "IsBuildingDisciplinesCategoriesFilterEnabled", OpenDisciplinesCategoriesWindow, EnableDisableDisciplinesCategories);
		}

		private void DrawProperty(Rect position, SerializedProperty property, string label, string isEnabledPropertyName, Action<SerializedProperty> action, Action<SerializedProperty, bool> toggleAction = null)
		{
			var isEnabledProperty = property.FindPropertyRelative(isEnabledPropertyName);

			var labelGUIContent = new GUIContent(label);

			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), labelGUIContent);
			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			var lastIsEnabled = isEnabledProperty.boolValue;
			EditorGUI.PropertyField(position, isEnabledProperty, GUIContent.none);
			if (isEnabledProperty.boolValue != lastIsEnabled)
			{
				toggleAction?.Invoke(property, isEnabledProperty.boolValue);
			}

			var size = EditorStyles.toggle.CalcSize(GUIContent.none);

			position.x += size.x;

			var guiChanged = GUI.changed;
			var guiEnabled = GUI.enabled;
			GUI.enabled = isEnabledProperty.boolValue;
			if (GUI.Button(position, EditorGUIUtility.IconContent("_Popup"), EditorStyles.iconButton) && isEnabledProperty.boolValue)
			{
				GUI.changed = guiChanged;

				action.Invoke(property);
			}
			GUI.enabled = guiEnabled;

			EditorGUI.indentLevel = indent;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return 2 * EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
		}

		private void OpenFiltersWindow(SerializedProperty property)
		{
			var layerInstanceData = property.serializedObject.GetValue(property.propertyPath.Replace(".BuildingSceneLayerFilter", "")) as ArcGISLayerInstanceData;
			var buildingSceneLayerFilterInstanceData = property.GetValue() as ArcGISBuildingSceneLayerFilterInstanceData;
			var statisticsInformation = GetStatisticsFromLayer(layerInstanceData);

			var bslFiltersWindow = EditorWindow.GetWindow<BSLFiltersWindow>();

			bslFiltersWindow.titleContent = new GUIContent($"{layerInstanceData.Name} - Building Filter");

			bslFiltersWindow.Init(
				(ArcGISBuildingSceneLayerFilterInstanceData)buildingSceneLayerFilterInstanceData.Clone(),
				(ArcGISBuildingSceneLayerFilterInstanceData buildingSceneLayerFilterInstanceData) =>
				{
					buildingSceneLayerFilterInstanceData.ActiveBuildingAttributeFilterIndex.ApplyToSerializedProperty(property.FindPropertyRelative("ActiveBuildingAttributeFilterIndex"));
					buildingSceneLayerFilterInstanceData.BuildingAttributeFilters.ApplyToSerializedProperty(property.FindPropertyRelative("BuildingAttributeFilters"));
					property.serializedObject.ApplyModifiedProperties();
				},
				statisticsInformation
			);

			EditorSceneManager.sceneClosing += (Scene scene, bool removingScene) =>
			{
				bslFiltersWindow.Close();
			};

			EditorSceneManager.sceneSaving += (Scene scene, string path) =>
			{
				bslFiltersWindow.Close();
			};

			EditorApplication.playModeStateChanged += (playModeStateChange) =>
			{
				bslFiltersWindow.Close();
			};

			EditorApplication.wantsToQuit += () =>
			{
				bslFiltersWindow.Close();
				return true;
			};
		}

		private List<ArcGISBuildingSceneLayerAttributeStatisticsInstanceData> GetStatisticsFromLayer(ArcGISLayerInstanceData layerInstanceData)
		{
#pragma warning disable CS0618
			var buildingSceneLayer = layerInstanceData.APIObject as ArcGISBuildingSceneLayer;
#pragma warning restore CS0618

			if (buildingSceneLayer.LoadStatus != ArcGISLoadStatus.Loaded)
			{
				return null;
			}

			var statisticsFuture = buildingSceneLayer.FetchStatisticsAsync();

			ArcGISDictionary<string, ArcGISBuildingSceneLayerAttributeStatistics> statistics;

			try
			{
				statistics = statisticsFuture.Get();
			}
			catch (Exception ex)
			{
				// Exception is thrown when layer has no statistics data.
				Debug.LogWarning(ex.Message);
				return null;
			}

			// Key is the field name, list of strings are the most frequent values for that field name.
			var attributeStatistics = new List<ArcGISBuildingSceneLayerAttributeStatisticsInstanceData>();

			for (ulong i = 0; i < statistics.Keys.Size; i++)
			{
				var value = statistics[statistics.Keys.At(i)];

				var mostFrequentValues = new List<string>();

				for (ulong j = 0; j < value.MostFrequentValues.GetSize(); j++)
				{
					mostFrequentValues.Add(value.MostFrequentValues.At(j));
				}

				attributeStatistics.Add(new ArcGISBuildingSceneLayerAttributeStatisticsInstanceData
				{
					FieldName = value.FieldName,
					MostFrequentValues = mostFrequentValues
				});
			}

			return attributeStatistics;
		}

		private void OpenDisciplinesCategoriesWindow(SerializedProperty property)
		{
			var layerInstanceData = property.serializedObject.GetValue(property.propertyPath.Replace(".BuildingSceneLayerFilter", "")) as ArcGISLayerInstanceData;
			var buildingSceneLayerFilterInstanceData = property.GetValue() as ArcGISBuildingSceneLayerFilterInstanceData;

#pragma warning disable CS0618
			var buildingSceneLayer = layerInstanceData.APIObject as ArcGISBuildingSceneLayer;
#pragma warning restore CS0618
			var sublayerInstanceList = GetSublayerInstancesRecursive(buildingSceneLayer);

			var bslCategoriesWindow = EditorWindow.GetWindow<BSLCategoriesWindow>();

			bslCategoriesWindow.titleContent = new GUIContent($"{layerInstanceData.Name} - Disciplines & Categories");

			bslCategoriesWindow.Init(
				(ArcGISBuildingSceneLayerFilterInstanceData)buildingSceneLayerFilterInstanceData.Clone(),
				(ArcGISBuildingSceneLayerFilterInstanceData buildingSceneLayerFilterInstanceData) =>
				{
					buildingSceneLayerFilterInstanceData.EnabledCategories.ApplyToSerializedProperty(property.FindPropertyRelative("EnabledCategories"));
					buildingSceneLayerFilterInstanceData.EnabledDisciplines.ApplyToSerializedProperty(property.FindPropertyRelative("EnabledDisciplines"));
					property.serializedObject.ApplyModifiedProperties();
				},
				sublayerInstanceList
			);

			EditorSceneManager.sceneClosing += (Scene scene, bool removingScene) =>
			{
				bslCategoriesWindow.Close();
			};

			EditorSceneManager.sceneSaving += (Scene scene, string path) =>
			{
				bslCategoriesWindow.Close();
			};

			EditorApplication.playModeStateChanged += (playModeStateChange) =>
			{
				bslCategoriesWindow.Close();
			};

			EditorApplication.wantsToQuit += () =>
			{
				bslCategoriesWindow.Close();
				return true;
			};
		}

		private void EnableDisableDisciplinesCategories(SerializedProperty property, bool newValue)
		{
			var layerInstanceData = property.serializedObject.GetValue(property.propertyPath.Replace(".BuildingSceneLayerFilter", "")) as ArcGISLayerInstanceData;

			// If D&C is being enabled, do nothing.
			if (newValue)
			{
				return;
			}

			// If D&C is being disabled, reload layer to get original visibility values from source.
			var mapComponent = (ArcGISMapComponent)MapCreatorUtilities.MapComponent;
			var index = mapComponent.Layers.IndexOf(layerInstanceData);

			// TODO: Replace workaround for reloading a layer when we have a proper API method.
			mapComponent.Map.Layers.Remove(ulong.Parse(index.ToString()));
			mapComponent.UpdateLayers();
		}

		private List<ArcGISBuildingSceneLayerSublayerInstanceData> GetSublayerInstancesRecursive(ArcGISBuildingSceneLayer layer)
		{
			var sublayerInstances = new List<ArcGISBuildingSceneLayerSublayerInstanceData>();

			for (ulong i = 0; i < layer.Sublayers.GetSize(); i++)
			{
				sublayerInstances.AddRange(GetSublayerInstancesRecursive(layer.Sublayers.At(i)));
			}

			return sublayerInstances;
		}

		private List<ArcGISBuildingSceneLayerSublayerInstanceData> GetSublayerInstancesRecursive(ArcGISBuildingSceneSublayer sublayer)
		{
			var sublayerInstances = new List<ArcGISBuildingSceneLayerSublayerInstanceData>();

			if (sublayer.Sublayers == null || sublayer.Sublayers.GetSize() == 0)
			{
				var sublayerInstanceData = new ArcGISBuildingSceneLayerSublayerInstanceData()
				{
					Name = sublayer.Name,
					ID = sublayer.SublayerId,
					Discipline = sublayer.Discipline,
				};

				sublayerInstances.Add(sublayerInstanceData);

				return sublayerInstances;
			}

			for (ulong i = 0; i < sublayer.Sublayers.GetSize(); i++)
			{
				sublayerInstances.AddRange(GetSublayerInstancesRecursive(sublayer.Sublayers.At(i)));
			}

			return sublayerInstances;
		}
	}
}
