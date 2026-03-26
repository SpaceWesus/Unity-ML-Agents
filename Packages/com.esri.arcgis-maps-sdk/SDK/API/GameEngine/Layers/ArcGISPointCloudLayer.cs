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

namespace Esri.GameEngine.Layers
{
    /// <summary>
    /// Public class that will contain a layer with an point cloud inside.
    /// </summary>
    /// <since>2.2.0</since>
    [StructLayout(LayoutKind.Sequential)]
    public partial class ArcGISPointCloudLayer :
        GameEngine.Layers.Base.ArcGISLayer
    {
        #region Constructors
        /// <summary>
        /// Creates a new layer.
        /// </summary>
        /// <remarks>
        /// Creates a new layer.
        /// </remarks>
        /// <param name="source">Layer source.</param>
        /// <param name="APIKey">API key used to load data.</param>
        /// <since>2.2.0</since>
        public ArcGISPointCloudLayer(string source, string APIKey) :
            base(IntPtr.Zero)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            Handle = PInvoke.RT_GEPointCloudLayer_create(source, APIKey, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Creates a new layer.
        /// </summary>
        /// <remarks>
        /// Creates a new layer.
        /// </remarks>
        /// <param name="source">Layer source.</param>
        /// <param name="name">Layer name.</param>
        /// <param name="opacity">Layer opacity.</param>
        /// <param name="visible">Layer visible or not.</param>
        /// <param name="APIKey">API key used to load data.</param>
        /// <since>2.2.0</since>
        public ArcGISPointCloudLayer(string source, string name, float opacity, bool visible, string APIKey) :
            base(IntPtr.Zero)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            Handle = PInvoke.RT_GEPointCloudLayer_createWithProperties(source, name, opacity, visible, APIKey, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        #endregion // Constructors
        
        #region Internal Members
        internal ArcGISPointCloudLayer(IntPtr handle) : base(handle)
        {
        }
        #endregion // Internal Members
    }
    
    internal static partial class PInvoke
    {
        #region P-Invoke Declarations
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_GEPointCloudLayer_create([MarshalAs(UnmanagedType.LPStr)]string source, [MarshalAs(UnmanagedType.LPStr)]string APIKey, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_GEPointCloudLayer_createWithProperties([MarshalAs(UnmanagedType.LPStr)]string source, [MarshalAs(UnmanagedType.LPStr)]string name, float opacity, [MarshalAs(UnmanagedType.I1)]bool visible, [MarshalAs(UnmanagedType.LPStr)]string APIKey, IntPtr errorHandler);
        #endregion // P-Invoke Declarations
    }
}