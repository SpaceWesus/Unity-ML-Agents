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
using UnityEditor;
using UnityEngine;

namespace Esri.ArcGISMapsSDK.Editor.UI
{
	[CustomPropertyDrawer(typeof(ArcGISSpatialFeatureFilterInstanceData))]
	public class ArcGISSpatialFeatureFilterInstanceDataEditor : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var isEnabledProperty = property.FindPropertyRelative("IsEnabled");

			EditorGUI.BeginProperty(position, label, property);
			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			var propRect = new Rect(position.x, position.y, position.width, position.height);

			EditorGUI.PropertyField(propRect, isEnabledProperty, GUIContent.none);

			var size = EditorStyles.toggle.CalcSize(GUIContent.none);

			propRect.x += size.x;

			var guiChanged = GUI.changed;
			var guiEnabled = GUI.enabled;
			GUI.enabled = isEnabledProperty.boolValue;
			if (GUI.Button(propRect, EditorGUIUtility.IconContent("_Popup"), EditorStyles.iconButton))
			{
				GUI.changed = guiChanged;

				if (EditorWindow.HasOpenInstances<ArcGISMapCreator>())
				{
					var mapCreator = EditorWindow.GetWindow<ArcGISMapCreator>();
					var mapCreatorTool = mapCreator.GetActiveTool();

					if (mapCreatorTool is ArcGISMapCreatorLayerTool mapCreatorLayerTool)
					{
						mapCreatorLayerTool.OpenPropertyEditTool(property);
					}
				}
			}
			GUI.enabled = guiEnabled;

			EditorGUI.indentLevel = indent;
			EditorGUI.EndProperty();
		}
	}
}
