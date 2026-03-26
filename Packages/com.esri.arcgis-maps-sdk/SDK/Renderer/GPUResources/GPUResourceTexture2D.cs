// COPYRIGHT 1995-2020 ESRI
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
using System;
using UnityEngine;

namespace Esri.ArcGISMapsSDK.Renderer.GPUResources
{
	internal class GPUResourceTexture2D : IGPUResourceTexture2D
	{
		public int Width
		{
			get
			{
				return NativeTexture.width;
			}
		}

		public int Height
		{
			get
			{
				return NativeTexture.height;
			}
		}

		public Texture2D NativeTexture { get; }

		public GPUResourceTexture2D(Texture2D texture)
		{
			NativeTexture = texture;
		}

		public void SetPixelData(IntPtr buffer, uint sizeBytes)
		{
			NativeTexture.LoadRawTextureData(buffer, (int)sizeBytes);
			NativeTexture.Apply(true);
		}

		public void Destroy()
		{
			if (Application.isEditor)
			{
				UnityEngine.Object.DestroyImmediate(NativeTexture);
			}
			else
			{
				UnityEngine.Object.Destroy(NativeTexture);
			}
		}
	}
}
