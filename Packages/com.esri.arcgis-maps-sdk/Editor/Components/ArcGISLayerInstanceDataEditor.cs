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

namespace Esri.ArcGISMapsSDK.Editor.Components
{
	[CustomPropertyDrawer(typeof(ArcGISLayerInstanceData))]
	public class ArcGISLayerInstanceDataEditor : PropertyDrawer
	{
		private const int RowCount = 7;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var nameProp = property.FindPropertyRelative("Name");
			var typeProp = property.FindPropertyRelative("Type");
			var SourceProp = property.FindPropertyRelative("Source");
			var opacityProp = property.FindPropertyRelative("Opacity");
			var visibilityProp = property.FindPropertyRelative("IsVisible");
			var authenticationTypeProp = property.FindPropertyRelative("AuthenticationType");

			var rectIndex = 0;
			Rect GetRect()
			{
				return new Rect(position.x, position.y + rectIndex++ * EditorGUIUtility.singleLineHeight + (rectIndex - 1) * EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
			}

			using (var propertyScope = new EditorGUI.PropertyScope(position, label, property))
			using (var checkScope = new EditorGUI.ChangeCheckScope())
			{
				EditorGUI.LabelField(GetRect(), label);

				using (new EditorGUI.IndentLevelScope())
				{
					EditorGUI.PropertyField(GetRect(), nameProp);
					EditorGUI.PropertyField(GetRect(), typeProp);
					EditorGUI.PropertyField(GetRect(), SourceProp);
					EditorGUI.PropertyField(GetRect(), opacityProp);
					EditorGUI.PropertyField(GetRect(), visibilityProp);
					EditorGUI.PropertyField(GetRect(), authenticationTypeProp);
					if (checkScope.changed)
					{
						EditorUtility.SetDirty(property.serializedObject.targetObject);
					}
				}
			}
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return RowCount * EditorGUIUtility.singleLineHeight + (RowCount - 1) * EditorGUIUtility.standardVerticalSpacing;
		}
	}
}
