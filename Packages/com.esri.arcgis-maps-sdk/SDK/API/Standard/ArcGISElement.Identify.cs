// COPYRIGHT 1995-2025 ESRI
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
using System.Runtime.InteropServices;
using System;

namespace Esri.Standard
{
	/// <summary>
	/// Defines an element that can be added to a container object.
	/// </summary>
	internal partial class ArcGISElement
	{
		internal static ArcGISElement FromFeature(GameEngine.Data.Feature value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localValue = value.Handle;

			var localResult = PInvoke.RT_Element_getValueAsFeature(localValue, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal object GetValueAsFeature()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_getValueAsFeature(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			GameEngine.Data.Feature localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new GameEngine.Data.Feature(localResult);
			}

			return localLocalResult;
		}

		internal static ArcGISElement FromIdentifyLayerResult(GameEngine.MapView.ArcGISIdentifyLayerResult value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localValue = value.Handle;

			var localResult = PInvoke.RT_Element_getValueAsIdentifyLayerResult(localValue, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal GameEngine.MapView.ArcGISIdentifyLayerResult GetValueAsIdentifyLayerResult()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_getValueAsIdentifyLayerResult(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			GameEngine.MapView.ArcGISIdentifyLayerResult localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new GameEngine.MapView.ArcGISIdentifyLayerResult(localResult);
			}

			return localLocalResult;
		}

		internal static ArcGISElement FromIdentifyLayerResultImmutableCollection(Unity.ArcGISImmutableCollection<GameEngine.MapView.ArcGISIdentifyLayerResult> value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localValue = value.Handle;

			var localResult = PInvoke.RT_Element_getValueAsIdentifyLayerResultImmutableCollection(localValue, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal Unity.ArcGISImmutableCollection<GameEngine.MapView.ArcGISIdentifyLayerResult> GetValueAsIdentifyLayerResultImmutableCollection()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_getValueAsIdentifyLayerResultImmutableCollection(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			Unity.ArcGISImmutableCollection<GameEngine.MapView.ArcGISIdentifyLayerResult> localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new Unity.ArcGISImmutableCollection<GameEngine.MapView.ArcGISIdentifyLayerResult>(localResult);
			}

			return localLocalResult;
		}
	}

	internal static partial class PInvoke
	{
		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_getValueAsFeature(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_getValueAsIdentifyLayerResult(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_getValueAsIdentifyLayerResultImmutableCollection(IntPtr handle, IntPtr errorHandler);
	}
}
