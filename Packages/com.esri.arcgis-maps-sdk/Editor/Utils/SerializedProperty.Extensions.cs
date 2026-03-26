// COPYRIGHT 1995-2023 ESRI
// TRADE SECRETS: ESRI PROPRIETARY AND CONFIDENTIAL
// Unpublished material - all rights reserved under the
// Copyright Laws of the United States and applicable international
// laws, treaties, and conventions.
//
// For additional information, contact:
// Environmental Systems Research Institute, Inc.
// Attn: Contracts and Legal Services Department
// 380 New York Street
// Redlands, California, 92373
// USA
//
// email: contracts@esri.com
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Esri.ArcGISMapsSDK.Editor.Utils
{
	internal static class SerializedPropertyExtensions
	{
		private static object GetObjectMember(object targetObject, string memberName)
		{
			if (targetObject == null)
			{
				return null;
			}

			// TODO: consider the base types as well
			var type = targetObject.GetType();

			var field = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			if (field != null)
			{
				return field.GetValue(targetObject);
			}

			var property = type.GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			if (property != null)
			{
				return property.GetValue(targetObject);
			}

			return null;
		}

		private static object GetObjectMember(object targetObject, string arrayName, int arrayIndex)
		{
			if (GetObjectMember(targetObject, arrayName) is not IEnumerable enumerable)
			{
				return null;
			}

			var enumerator = enumerable.GetEnumerator();

			for (var i = 0; i <= arrayIndex; i++)
			{
				if (!enumerator.MoveNext())
				{
					return null;
				}
			}

			return enumerator.Current;
		}

		private static void SetObjectMember(object targetObject, string memberName, object value)
		{
			if (targetObject == null)
			{
				return;
			}

			// TODO: consider the base types as well
			var type = targetObject.GetType();

			var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);

			field?.SetValue(targetObject, value);

			var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);

			property?.SetValue(targetObject, value);
		}

		internal static object GetValue(this SerializedObject serializedObject, string propertyPath)
		{
			var members = propertyPath.Replace(".Array.data[", "[").Split('.');

			object targetObject = serializedObject.targetObject;

			foreach (var member in members)
			{
				if (member.Contains("["))
				{
					var arrayName = member.Substring(0, member.IndexOf("["));
					var arrayIndex = member.Substring(member.IndexOf("[")).Replace("[", "").Replace("]", "");

					targetObject = GetObjectMember(targetObject, arrayName, Convert.ToInt32(arrayIndex));
				}
				else
				{
					targetObject = GetObjectMember(targetObject, member);
				}
			}

			return targetObject;
		}

		internal static object GetValue(this SerializedProperty serializedProperty)
		{
			return serializedProperty.serializedObject.GetValue(serializedProperty.propertyPath);
		}

		internal static void SetValue(this SerializedProperty serializedProperty, object value)
		{
			var members = serializedProperty.propertyPath.Replace(".Array.data[", "[").Split('.');

			object targetObject = serializedProperty.serializedObject.targetObject;

			foreach (var member in members.Take(members.Length - 1))
			{
				if (member.Contains("["))
				{
					var arrayName = member.Substring(0, member.IndexOf("["));
					var arrayIndex = member.Substring(member.IndexOf("[")).Replace("[", "").Replace("]", "");

					targetObject = GetObjectMember(targetObject, arrayName, Convert.ToInt32(arrayIndex));
				}
				else
				{
					targetObject = GetObjectMember(targetObject, member);
				}
			}

			var lastMember = members.Last();

			if (lastMember.Contains("["))
			{
				var arrayName = lastMember.Substring(0, lastMember.IndexOf("["));
				var arrayIndex = lastMember.Substring(lastMember.IndexOf("[")).Replace("[", "").Replace("]", "");

				var list = GetObjectMember(targetObject, arrayName) as IList;

				list[Convert.ToInt32(arrayIndex)] = value;
			}
			else
			{
				SetObjectMember(targetObject, lastMember, value);
			}
		}
	}
}
