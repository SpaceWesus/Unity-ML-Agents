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
		internal static ArcGISElement FromDateTime(DateTime value)
		{
			var dateTimeHandle = Unity.Convert.ToArcGISDateTime(value);

			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_fromDateTime(dateTimeHandle, errorHandler);

			PInvoke.RT_DateTime_destroy(dateTimeHandle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal static ArcGISElement FromFloat32(float value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_fromFloat32(value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal static ArcGISElement FromGuid(Guid value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var guidHandle = PInvoke.RT_GUID_createFromString(value.ToString(), errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_fromGUID(guidHandle, errorHandler);

			PInvoke.RT_GUID_destroy(guidHandle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal static ArcGISElement FromInt16(Int16 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_fromInt16(value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal static ArcGISElement FromInt32(Int32 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_fromInt32(value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal static ArcGISElement FromInt64(Int64 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_fromInt64(value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal static ArcGISElement FromUInt16(UInt16 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_fromUInt16(value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal static ArcGISElement FromUInt64(UInt64 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_fromUInt64(value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			ArcGISElement localLocalResult = null;

			if (localResult != IntPtr.Zero)
			{
				localLocalResult = new ArcGISElement(localResult);
			}

			return localLocalResult;
		}

		internal DateTime GetValueAsDateTime()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var dateTimeHandle = PInvoke.RT_Element_getValueAsDateTime(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			return Unity.Convert.FromArcGISDateTime(dateTimeHandle).DateTime;
		}

		internal float GetValueAsFloat32()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_getValueAsFloat32(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			return localResult;
		}

		internal Guid GetValueAsGuid()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var guidHandle = PInvoke.RT_Element_getValueAsGUID(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var coreString = PInvoke.RT_GUID_toString(guidHandle, errorHandler);

			PInvoke.RT_GUID_destroy(guidHandle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			var guidString = Unity.Convert.FromArcGISString(coreString);

			if (Guid.TryParse(guidString, out var guid))
			{
				return guid;
			}
			throw new NotSupportedException();
		}

		internal Int16 GetValueAsInt16()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_getValueAsInt16(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			return localResult;
		}

		internal Int32 GetValueAsInt32()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_getValueAsInt32(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			return localResult;
		}

		internal Int64 GetValueAsInt64()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_getValueAsInt64(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			return localResult;
		}

		internal UInt16 GetValueAsUInt16()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_getValueAsUInt16(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			return localResult;
		}

		internal UInt64 GetValueAsUInt64()
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			var localResult = PInvoke.RT_Element_getValueAsUInt64(Handle, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);

			return localResult;
		}

		internal void SetValueFromFloat32(float value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			PInvoke.RT_Element_setValueFromFloat32(Handle, value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);
		}

		internal void SetValueFromInt16(Int16 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			PInvoke.RT_Element_setValueFromInt16(Handle, value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);
		}

		internal void SetValueFromInt32(Int32 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			PInvoke.RT_Element_setValueFromInt32(Handle, value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);
		}

		internal void SetValueFromInt64(Int64 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			PInvoke.RT_Element_setValueFromInt64(Handle, value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);
		}

		internal void SetValueFromUInt16(UInt16 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			PInvoke.RT_Element_setValueFromUInt16(Handle, value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);
		}

		internal void SetValueFromUInt64(UInt64 value)
		{
			var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

			PInvoke.RT_Element_setValueFromUInt64(Handle, value, errorHandler);

			Unity.ArcGISErrorManager.CheckError(errorHandler);
		}
	}

	internal static partial class PInvoke
	{
		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_DateTime_destroy(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_DateTime_fromUnixMilliseconds(Int64 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_fromDateTime(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_fromFloat32(float value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_fromGUID(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_fromInt16(Int16 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_fromInt32(Int32 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_fromInt64(Int64 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_fromUInt16(UInt16 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_fromUInt64(UInt64 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_getValueAsDateTime(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern float RT_Element_getValueAsFloat32(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_Element_getValueAsGUID(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern Int16 RT_Element_getValueAsInt16(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern Int32 RT_Element_getValueAsInt32(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern Int64 RT_Element_getValueAsInt64(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_GUID_createFromString([MarshalAs(UnmanagedType.LPStr)] string value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_GUID_destroy(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern IntPtr RT_GUID_toString(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern UInt16 RT_Element_getValueAsUInt16(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern UInt64 RT_Element_getValueAsUInt64(IntPtr handle, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern void RT_Element_setValueFromFloat32(IntPtr handle, float value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern void RT_Element_setValueFromInt16(IntPtr handle, Int16 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern void RT_Element_setValueFromInt32(IntPtr handle, Int32 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern void RT_Element_setValueFromInt64(IntPtr handle, Int64 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern void RT_Element_setValueFromUInt16(IntPtr handle, UInt16 value, IntPtr errorHandler);

		[DllImport(Unity.Interop.Dll)]
		internal static extern void RT_Element_setValueFromUInt64(IntPtr handle, UInt64 value, IntPtr errorHandler);
	}
}
