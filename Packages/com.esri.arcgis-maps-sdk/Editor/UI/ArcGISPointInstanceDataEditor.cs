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
using Esri.ArcGISMapsSDK.SDK.Utils;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.Unity;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Esri.ArcGISMapsSDK.Editor.UI
{
	[CustomPropertyDrawer(typeof(ArcGISPointInstanceData))]
	public class ArcGISPointInstanceDataEditor : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var hideAltitude = fieldInfo.GetCustomAttributes<HideAltitudeAttribute>().ToArray().Length > 0;

			var xProp = property.FindPropertyRelative("X");
			var yProp = property.FindPropertyRelative("Y");
			var zProp = property.FindPropertyRelative("Z");
			var spatialReferenceProp = property.FindPropertyRelative("SpatialReference");
			var srProp = spatialReferenceProp.FindPropertyRelative("WKID");

			var xLabel = "X";
			var yLabel = "Y";
			var zLabel = "Z";

			if (srProp.intValue == SpatialReferenceWkid.WGS84 || srProp.intValue == SpatialReferenceWkid.CGCS2000)
			{
				xLabel = "Longitude";
				yLabel = "Latitude";
				zLabel = "Altitude";
			}

			var rectIndex = 0;

			Rect GetRect()
			{
				return new Rect(position.x, position.y + rectIndex++ * EditorGUIUtility.singleLineHeight + (rectIndex - 1) * EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
			}

			using (var propertyScope = new EditorGUI.PropertyScope(position, label, property))
			using (var checkScope = new EditorGUI.ChangeCheckScope())
			{
				if (label != GUIContent.none)
				{
					EditorGUI.LabelField(GetRect(), label);

					EditorGUI.indentLevel++;
				}

				EditorUtilities.DelayedDoubleField(GetRect(), xProp, new GUIContent(xLabel));
				EditorUtilities.DelayedDoubleField(GetRect(), yProp, new GUIContent(yLabel));

				if (!hideAltitude)
				{
					EditorUtilities.DelayedDoubleField(GetRect(), zProp, new GUIContent(zLabel));
				}

				EditorGUI.PropertyField(GetRect(), spatialReferenceProp);

				if (label != GUIContent.none)
				{
					EditorGUI.indentLevel--;
				}

				if (checkScope.changed)
				{
					EditorUtility.SetDirty(property.serializedObject.targetObject);
				}
			}
		}

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var container = new VisualElement();

			var template = MapCreatorUtilities.Assets.LoadVisualTreeAsset("UI/ArcGISPointFieldTemplate.uxml");

			template.CloneTree(container);

			var xField = container.Q<DoubleField>("x");
			var yField = container.Q<DoubleField>("y");
			var zField = container.Q<DoubleField>("z");
			var spatialReferenceField = container.Q<PropertyField>("spatial-reference");

			spatialReferenceField.RegisterValueChangeCallback(@event =>
			{
				var xLabel = "X";
				var yLabel = "Y";
				var zLabel = "Z";

				var spatialReferenceInstanceData = (ArcGISSpatialReferenceInstanceData)@event.changedProperty.boxedValue;

				if (spatialReferenceInstanceData.WKID == SpatialReferenceWkid.WGS84 ||
					spatialReferenceInstanceData.WKID == SpatialReferenceWkid.CGCS2000)
				{
					xLabel = "Longitude";
					yLabel = "Latitude";
					zLabel = "Altitude";
				}

				xField.label = xLabel;
				yField.label = yLabel;
				zField.label = zLabel;
			});

			return container;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var hideAltitude = fieldInfo.GetCustomAttributes<HideAltitudeAttribute>().ToArray().Length > 0;

			var rows = hideAltitude ? 3 : 4;

			if (label != GUIContent.none)
			{
				rows += 1;
			}

			return rows * EditorGUIUtility.singleLineHeight + (rows - 1) * EditorGUIUtility.standardVerticalSpacing;
		}
	}
}
